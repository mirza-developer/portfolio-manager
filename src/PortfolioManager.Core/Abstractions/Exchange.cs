using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Abstractions;

public interface IPriceProvider
{
    /// <summary>Returns the current USDT price per asset symbol. Missing or non-positive prices are treated as unavailable.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetPricesAsync(IReadOnlyCollection<Asset> assets, CancellationToken ct = default);
}

/// <param name="Quantity">Asset quantity bought or sold.</param>
/// <param name="ExecutionPriceUsdt">Price actually obtained (market price adjusted by slippage).</param>
/// <param name="FeeUsdt">Fee paid in USDT.</param>
/// <param name="ValueUsdt">Net USDT moved: the amount spent on a buy, the amount received after fee on a sell.</param>
public readonly record struct TradeExecution(decimal Quantity, decimal ExecutionPriceUsdt, decimal FeeUsdt, decimal ValueUsdt);

public interface ITradeExecutor
{
    Task<TradeExecution> BuyAsync(Asset asset, decimal spendUsdt, decimal marketPriceUsdt, CancellationToken ct = default);
    Task<TradeExecution> SellAsync(Asset asset, decimal quantity, decimal marketPriceUsdt, CancellationToken ct = default);
}
