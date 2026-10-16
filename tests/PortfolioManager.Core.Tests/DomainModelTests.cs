using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class DomainModelTests
{
    [Fact]
    public void Defaults_match_the_documented_rules()
    {
        var p = new Portfolio();
        Assert.Equal(5m, p.StopLossPercent);
        Assert.Equal(40m, p.MaxAllocationPercent);
        Assert.Equal(10m, p.CashReservePercent);
        Assert.Equal(4, p.CooldownHours);
        Assert.Equal(1m, p.ReentryRecoveryPercent);
        Assert.True(p.IsAutoTradingEnabled);
        Assert.Equal(PositionStatus.None, new Position().Status);
        Assert.True(new Asset().IsEnabled);
    }

    [Fact]
    public void Clone_is_deep_so_changes_do_not_leak_back()
    {
        var original = TestData.NewPortfolio(1, 100m, TestData.Holding(1, 10m, 1000m));
        var copy = original.Clone();
        copy.UsdtBalance = 5m;
        copy.Positions[0].Quantity = 99m;
        copy.Positions.Add(new Position { AssetId = 2 });
        Assert.Equal(100m, original.UsdtBalance);
        Assert.Equal(10m, original.Positions[0].Quantity);
        Assert.Single(original.Positions);
    }

    [Fact]
    public void ApplyFrom_updates_existing_positions_and_adds_new_ones()
    {
        var original = TestData.NewPortfolio(7, 100m, TestData.Holding(1, 10m, 1000m));
        original.Positions[0].Id = 55;
        var working = original.Clone();
        working.UsdtBalance = 40m;
        working.Positions[0].Quantity = 0m;
        working.Positions[0].Status = PositionStatus.Cooldown;
        working.Positions.Add(new Position { AssetId = 2, Quantity = 3m, Status = PositionStatus.Holding });

        original.ApplyFrom(working);

        Assert.Equal(40m, original.UsdtBalance);
        Assert.Equal(2, original.Positions.Count);
        Assert.Equal(55, original.Positions[0].Id); // identity is kept
        Assert.Equal(PositionStatus.Cooldown, original.Positions[0].Status);
        Assert.Equal(7, original.Positions[1].PortfolioId);
        Assert.Equal(3m, original.Positions[1].Quantity);
    }

    [Fact]
    public void CopyFrom_keeps_id_and_portfolio_id()
    {
        var target = new Position { Id = 9, PortfolioId = 4 };
        var source = TestData.Holding(2, 5m, 50m, 8m);
        source.Id = 1;
        source.PortfolioId = 1;
        target.CopyFrom(source);
        Assert.Equal(9, target.Id);
        Assert.Equal(4, target.PortfolioId);
        Assert.Equal(2, target.AssetId);
        Assert.Equal(8m, target.StopLossPercentOverride);
    }
}
