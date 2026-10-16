using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Calculation;

public static class ReentryPolicy
{
    /// <summary>
    /// Whether an asset may be bought now. Enabled + valid price are required.
    /// No position (or None) is eligible; Holding is not; Cooldown needs the cooldown to be over AND the price to have
    /// recovered at least ReentryRecoveryPercent above the last sell price.
    /// </summary>
    public static bool IsEligible(Asset asset, Position? position, decimal priceUsdt, DateTime nowUtc, Portfolio portfolio)
    {
        if (!asset.IsEnabled || priceUsdt <= 0m) return false;
        if (position is null || position.Status == PositionStatus.None) return true;
        if (position.Status == PositionStatus.Holding) return false;

        var cooldownOver = position.CooldownUntilUtc is null || nowUtc >= position.CooldownUntilUtc.Value;
        if (!cooldownOver) return false;
        if (position.LastSellPriceUsdt is null) return true;

        var requiredPrice = position.LastSellPriceUsdt.Value * (1m + portfolio.ReentryRecoveryPercent / 100m);
        return priceUsdt >= requiredPrice;
    }
}
