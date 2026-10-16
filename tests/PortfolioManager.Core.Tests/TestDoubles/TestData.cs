using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Tests.TestDoubles;

public static class TestData
{
    public static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static Asset Gold(bool enabled = true) => new() { Id = 1, Symbol = "GOLD", Name = "Digital Gold", IsEnabled = enabled };
    public static Asset Silver(bool enabled = true) => new() { Id = 2, Symbol = "SILVER", Name = "Digital Silver", IsEnabled = enabled };
    public static Asset Doge(bool enabled = true) => new() { Id = 3, Symbol = "DOGE", Name = "Doge", IsEnabled = enabled };

    public static Portfolio NewPortfolio(int id = 1, decimal usdt = 0m, params Position[] positions)
    {
        foreach (var p in positions) p.PortfolioId = id;
        return new Portfolio
        {
            Id = id,
            UserId = "user-" + id,
            Name = "Portfolio " + id,
            UsdtBalance = usdt,
            CreatedUtc = T0,
            Positions = positions.ToList()
        };
    }

    public static Position Holding(int assetId, decimal quantity, decimal benchmark, decimal? stopOverride = null) => new()
    {
        AssetId = assetId,
        Quantity = quantity,
        InitialValueUsdt = benchmark,
        BenchmarkValueUsdt = benchmark,
        Status = PositionStatus.Holding,
        StopLossPercentOverride = stopOverride,
        UpdatedUtc = T0
    };

    public static Position InCooldown(int assetId, decimal lastSellPrice, DateTime until, decimal initialValue = 1000m) => new()
    {
        AssetId = assetId,
        Quantity = 0m,
        InitialValueUsdt = initialValue,
        BenchmarkValueUsdt = 0m,
        Status = PositionStatus.Cooldown,
        CooldownUntilUtc = until,
        LastSellPriceUsdt = lastSellPrice,
        UpdatedUtc = T0
    };
}
