using PortfolioManager.Core.Calculation;
using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class ReentryPolicyTests
{
    private static readonly Portfolio P = TestData.NewPortfolio(); // re-entry recovery = 1%

    [Fact]
    public void Disabled_asset_is_not_eligible() =>
        Assert.False(ReentryPolicy.IsEligible(TestData.Gold(enabled: false), null, 100m, TestData.T0, P));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Asset_without_a_valid_price_is_not_eligible(int price) =>
        Assert.False(ReentryPolicy.IsEligible(TestData.Gold(), null, price, TestData.T0, P));

    [Fact]
    public void Asset_without_a_position_is_eligible() =>
        Assert.True(ReentryPolicy.IsEligible(TestData.Gold(), null, 100m, TestData.T0, P));

    [Fact]
    public void Position_with_status_none_is_eligible() =>
        Assert.True(ReentryPolicy.IsEligible(TestData.Gold(), new Position { Status = PositionStatus.None }, 100m, TestData.T0, P));

    [Fact]
    public void Held_asset_is_not_eligible() =>
        Assert.False(ReentryPolicy.IsEligible(TestData.Gold(), TestData.Holding(1, 1m, 100m), 100m, TestData.T0, P));

    [Fact]
    public void Cooldown_not_over_is_not_eligible()
    {
        var pos = TestData.InCooldown(1, 100m, TestData.T0.AddHours(4));
        Assert.False(ReentryPolicy.IsEligible(TestData.Gold(), pos, 200m, TestData.T0.AddHours(3), P));
    }

    [Fact]
    public void Cooldown_over_but_price_not_recovered_is_not_eligible()
    {
        var pos = TestData.InCooldown(1, 100m, TestData.T0.AddHours(4));
        Assert.False(ReentryPolicy.IsEligible(TestData.Gold(), pos, 100.5m, TestData.T0.AddHours(5), P));
    }

    [Fact]
    public void Cooldown_over_and_price_recovered_is_eligible()
    {
        var pos = TestData.InCooldown(1, 100m, TestData.T0.AddHours(4));
        Assert.True(ReentryPolicy.IsEligible(TestData.Gold(), pos, 102m, TestData.T0.AddHours(5), P));
    }

    [Fact]
    public void Boundaries_are_inclusive_cooldown_end_and_exact_recovery_price()
    {
        var pos = TestData.InCooldown(1, 100m, TestData.T0.AddHours(4));
        Assert.True(ReentryPolicy.IsEligible(TestData.Gold(), pos, 101m, TestData.T0.AddHours(4), P));
    }

    [Fact]
    public void Cooldown_without_a_last_sell_price_only_waits_for_the_time()
    {
        var pos = TestData.InCooldown(1, 100m, TestData.T0);
        pos.LastSellPriceUsdt = null;
        Assert.True(ReentryPolicy.IsEligible(TestData.Gold(), pos, 1m, TestData.T0, P));
    }

    [Fact]
    public void Cooldown_without_an_end_time_counts_as_over()
    {
        var pos = TestData.InCooldown(1, 100m, TestData.T0);
        pos.CooldownUntilUtc = null;
        Assert.True(ReentryPolicy.IsEligible(TestData.Gold(), pos, 150m, TestData.T0, P));
    }
}
