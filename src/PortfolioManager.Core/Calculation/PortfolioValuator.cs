using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Calculation;

public static class PortfolioValuator
{
    public static decimal PositionValue(Position position, decimal priceUsdt) => position.Quantity * priceUsdt;

    /// <summary>
    /// Portfolio value = USDT balance + sum(quantity x price). Returns false when a held position has no valid price,
    /// because the value would then be unreliable. Positions with zero quantity need no price.
    /// </summary>
    public static bool TryGetPortfolioValue(Portfolio portfolio, IReadOnlyDictionary<int, decimal> pricesByAssetId, out decimal value)
    {
        value = portfolio.UsdtBalance;
        foreach (var position in portfolio.Positions)
        {
            if (position.Quantity <= 0m) continue;
            if (!pricesByAssetId.TryGetValue(position.AssetId, out var price) || price <= 0m)
            {
                value = 0m;
                return false;
            }
            value += position.Quantity * price;
        }
        return true;
    }
}
