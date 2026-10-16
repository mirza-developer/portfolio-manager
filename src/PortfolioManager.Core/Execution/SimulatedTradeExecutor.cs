using Microsoft.Extensions.Options;
using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Execution;

/// <summary>Simulated exchange: applies slippage against the trader and a fee in USDT. No network calls.</summary>
public sealed class SimulatedTradeExecutor : ITradeExecutor
{
    private readonly TradingOptions _options;

    public SimulatedTradeExecutor(IOptions<TradingOptions> options) => _options = options.Value;

    public Task<TradeExecution> BuyAsync(Asset asset, decimal spendUsdt, decimal marketPriceUsdt, CancellationToken ct = default)
    {
        if (spendUsdt <= 0m) throw new ArgumentOutOfRangeException(nameof(spendUsdt));
        if (marketPriceUsdt <= 0m) throw new ArgumentOutOfRangeException(nameof(marketPriceUsdt));

        var fee = spendUsdt * _options.FeePercent / 100m;
        var executionPrice = marketPriceUsdt * (1m + _options.SlippagePercent / 100m);
        var quantity = (spendUsdt - fee) / executionPrice;
        return Task.FromResult(new TradeExecution(quantity, executionPrice, fee, spendUsdt));
    }

    public Task<TradeExecution> SellAsync(Asset asset, decimal quantity, decimal marketPriceUsdt, CancellationToken ct = default)
    {
        if (quantity <= 0m) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (marketPriceUsdt <= 0m) throw new ArgumentOutOfRangeException(nameof(marketPriceUsdt));

        var executionPrice = marketPriceUsdt * (1m - _options.SlippagePercent / 100m);
        var gross = quantity * executionPrice;
        var fee = gross * _options.FeePercent / 100m;
        return Task.FromResult(new TradeExecution(quantity, executionPrice, fee, gross - fee));
    }
}
