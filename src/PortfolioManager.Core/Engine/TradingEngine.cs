using System.Globalization;
using Microsoft.Extensions.Logging;
using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Calculation;
using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Strategy;

namespace PortfolioManager.Core.Engine;

/// <param name="StartedUtc">Time of the cycle.</param>
/// <param name="PortfoliosProcessed">Portfolios processed and saved successfully.</param>
/// <param name="PortfoliosFailed">Portfolios that threw; nothing was applied for them.</param>
/// <param name="TradesExecuted">Number of trades (buys and sells) executed in the cycle.</param>
public sealed record CycleResult(DateTime StartedUtc, int PortfoliosProcessed, int PortfoliosFailed, int TradesExecuted);

public interface ITradingEngine
{
    Task<CycleResult> RunCycleAsync(CancellationToken ct = default);
}

/// <summary>
/// One trading cycle: fetch prices, log them, then for every auto-trading portfolio apply
/// (1) trailing stop-loss protection, (2) re-entry eligibility, (3) reinvestment of USDT. Tether is the base currency.
/// </summary>
public sealed class TradingEngine : ITradingEngine
{
    private readonly IAssetRepository _assets;
    private readonly IPortfolioRepository _portfolios;
    private readonly IPriceProvider _priceProvider;
    private readonly IPriceLogRepository _priceLogs;
    private readonly ITradeLogRepository _tradeLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITradeExecutor _executor;
    private readonly IBuyStrategy _strategy;
    private readonly TradeRuleGuard _guard;
    private readonly TimeProvider _time;
    private readonly ILogger<TradingEngine> _logger;

    public TradingEngine(
        IAssetRepository assets,
        IPortfolioRepository portfolios,
        IPriceProvider priceProvider,
        IPriceLogRepository priceLogs,
        ITradeLogRepository tradeLogs,
        IUnitOfWork unitOfWork,
        ITradeExecutor executor,
        IBuyStrategy strategy,
        TradeRuleGuard guard,
        TimeProvider time,
        ILogger<TradingEngine> logger)
    {
        _assets = assets;
        _portfolios = portfolios;
        _priceProvider = priceProvider;
        _priceLogs = priceLogs;
        _tradeLogs = tradeLogs;
        _unitOfWork = unitOfWork;
        _executor = executor;
        _strategy = strategy;
        _guard = guard;
        _time = time;
        _logger = logger;
    }

    public async Task<CycleResult> RunCycleAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var now = _time.GetUtcNow().UtcDateTime;

        var assets = await _assets.GetAllAsync(ct);
        var quotes = await _priceProvider.GetPricesAsync(assets, ct);

        var prices = new Dictionary<int, decimal>();
        var priceLogs = new List<PriceLog>();
        foreach (var asset in assets)
        {
            if (quotes.TryGetValue(asset.Symbol, out var price) && price > 0m)
            {
                prices[asset.Id] = price;
                priceLogs.Add(new PriceLog { AssetId = asset.Id, PriceUsdt = price, TimestampUtc = now, Source = asset.PriceSource });
            }
            else
            {
                _logger.LogWarning("No valid price for {Symbol}; the asset is skipped in this cycle.", asset.Symbol);
            }
        }

        if (priceLogs.Count > 0)
        {
            _priceLogs.AddRange(priceLogs);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var portfolios = await _portfolios.GetAutoTradingPortfoliosAsync(ct);
        int processed = 0, failed = 0, trades = 0;

        foreach (var portfolio in portfolios)
        {
            ct.ThrowIfCancellationRequested();
            if (!portfolio.IsAutoTradingEnabled) continue;

            try
            {
                // Work on a copy: if anything throws, the original portfolio stays untouched (nothing partial is saved).
                var working = portfolio.Clone();
                var logs = new List<TradeLog>();
                await ProcessPortfolioAsync(working, assets, prices, now, logs, ct);

                portfolio.ApplyFrom(working);
                foreach (var log in logs) _tradeLogs.Add(log);
                await _unitOfWork.SaveChangesAsync(ct);

                processed++;
                trades += logs.Count;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Portfolio {PortfolioId} failed; nothing was applied for it.", portfolio.Id);
            }
        }

        return new CycleResult(now, processed, failed, trades);
    }

    private async Task ProcessPortfolioAsync(
        Portfolio working,
        IReadOnlyList<Asset> assets,
        IReadOnlyDictionary<int, decimal> prices,
        DateTime now,
        List<TradeLog> logs,
        CancellationToken ct)
    {
        var assetsById = assets.ToDictionary(a => a.Id);
        var soldNow = new HashSet<int>();

        // 1) Protection: trailing stop-loss (runs before any buy).
        foreach (var position in working.Positions)
        {
            if (position.Status != PositionStatus.Holding || position.Quantity <= 0m) continue;

            if (!prices.TryGetValue(position.AssetId, out var price) || !assetsById.TryGetValue(position.AssetId, out var asset))
            {
                _logger.LogWarning("Portfolio {PortfolioId}: no price for asset {AssetId}; position skipped.", working.Id, position.AssetId);
                continue;
            }

            var value = PortfolioValuator.PositionValue(position, price);
            var limit = position.StopLossPercentOverride ?? working.StopLossPercent;
            var result = StopLossEvaluator.Evaluate(value, position.BenchmarkValueUsdt, limit);

            if (result.NewBenchmark != position.BenchmarkValueUsdt)
            {
                position.BenchmarkValueUsdt = result.NewBenchmark;
                position.UpdatedUtc = now;
            }
            if (!result.ShouldSell) continue;

            var quantity = position.Quantity;
            var execution = await _executor.SellAsync(asset, quantity, price, ct);
            working.UsdtBalance += execution.ValueUsdt;

            logs.Add(new TradeLog
            {
                PortfolioId = working.Id,
                AssetId = asset.Id,
                Side = TradeSide.Sell,
                Reason = TradeReason.StopLoss,
                Quantity = quantity,
                PriceUsdt = execution.ExecutionPriceUsdt,
                ValueUsdt = execution.ValueUsdt,
                FeeUsdt = execution.FeeUsdt,
                TimestampUtc = now,
                Note = string.Create(CultureInfo.InvariantCulture, $"Stop-loss {limit}% below benchmark {result.NewBenchmark}")
            });

            position.Quantity = 0m;
            position.Status = PositionStatus.Cooldown;
            position.CooldownUntilUtc = now.AddHours(working.CooldownHours);
            position.LastSellPriceUsdt = price;
            position.BenchmarkValueUsdt = 0m;
            position.UpdatedUtc = now;
            soldNow.Add(asset.Id);
        }

        // 2) Reinvestment needs a reliable portfolio value.
        if (!PortfolioValuator.TryGetPortfolioValue(working, prices, out var portfolioValue))
        {
            _logger.LogWarning("Portfolio {PortfolioId}: a held asset has no valid price; no buys in this cycle.", working.Id);
            return;
        }

        var investable = working.UsdtBalance - portfolioValue * working.CashReservePercent / 100m;
        if (investable <= 0m) return;

        // 3) Re-entry: which assets may be bought now.
        var candidates = new List<BuyCandidate>();
        foreach (var asset in assets)
        {
            if (soldNow.Contains(asset.Id) || !prices.TryGetValue(asset.Id, out var price)) continue;
            var position = working.Positions.FirstOrDefault(p => p.AssetId == asset.Id);
            if (!ReentryPolicy.IsEligible(asset, position, price, now, working)) continue;
            candidates.Add(new BuyCandidate(asset, price, position is null ? 0m : position.Quantity * price));
        }
        if (candidates.Count == 0) return;

        // 4) Compounding: the strategy proposes, the guard enforces the hard rules, then we buy with USDT.
        var context = new BuyContext(working, investable, portfolioValue, candidates);
        var plan = _guard.Apply(context, _strategy.Plan(context));

        foreach (var buy in plan)
        {
            if (buy.SpendUsdt > working.UsdtBalance) continue;

            var price = prices[buy.Asset.Id];
            var execution = await _executor.BuyAsync(buy.Asset, buy.SpendUsdt, price, ct);
            working.UsdtBalance -= execution.ValueUsdt;

            var position = working.Positions.FirstOrDefault(p => p.AssetId == buy.Asset.Id);
            var reason = position is null ? TradeReason.Initial : TradeReason.Reinvest;
            if (position is null)
            {
                position = new Position
                {
                    PortfolioId = working.Id,
                    AssetId = buy.Asset.Id,
                    InitialValueUsdt = execution.Quantity * price
                };
                working.Positions.Add(position);
            }

            position.Quantity += execution.Quantity;
            position.Status = PositionStatus.Holding;
            position.BenchmarkValueUsdt = position.Quantity * price;
            position.CooldownUntilUtc = null;
            position.LastSellPriceUsdt = null;
            position.UpdatedUtc = now;

            logs.Add(new TradeLog
            {
                PortfolioId = working.Id,
                AssetId = buy.Asset.Id,
                Side = TradeSide.Buy,
                Reason = reason,
                Quantity = execution.Quantity,
                PriceUsdt = execution.ExecutionPriceUsdt,
                ValueUsdt = execution.ValueUsdt,
                FeeUsdt = execution.FeeUsdt,
                TimestampUtc = now
            });
        }
    }
}
