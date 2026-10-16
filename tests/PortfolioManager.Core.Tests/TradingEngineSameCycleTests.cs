using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class TradingEngineSameCycleTests
{
    [Fact]
    public async Task An_asset_sold_by_the_stop_loss_is_not_rebought_in_the_same_cycle_even_with_no_cooldown_and_no_recovery_rule()
    {
        var env = new EngineEnv();
        env.Store.Assets.Add(TestData.Gold());
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m));
        p.CooldownHours = 0;
        p.ReentryRecoveryPercent = 0m;
        p.MaxAllocationPercent = 100m;
        p.CashReservePercent = 0m;
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 90m;

        await env.RunAsync();

        Assert.Equal(PositionStatus.Cooldown, p.Positions[0].Status);
        Assert.Equal(0m, p.Positions[0].Quantity);
        Assert.Equal(900m, p.UsdtBalance);
        Assert.Equal(TradeSide.Sell, Assert.Single(env.Store.TradeLogs).Side);

        // The next cycle (cooldown 0, price not lower) may buy it back.
        env.Clock.Advance(TimeSpan.FromHours(1));
        await env.RunAsync();
        Assert.Equal(PositionStatus.Holding, p.Positions[0].Status);
        Assert.Equal(10m, p.Positions[0].Quantity);
    }
}
