using PortfolioManager.Core.Calculation;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class PortfolioValuatorTests
{
    [Fact]
    public void Position_value_is_quantity_times_price()
    {
        Assert.Equal(250m, PortfolioValuator.PositionValue(TestData.Holding(1, 5m, 100m), 50m));
    }

    [Fact]
    public void Portfolio_with_only_usdt_is_worth_its_balance()
    {
        var p = TestData.NewPortfolio(1, 321m);
        Assert.True(PortfolioValuator.TryGetPortfolioValue(p, new Dictionary<int, decimal>(), out var value));
        Assert.Equal(321m, value);
    }

    [Fact]
    public void Portfolio_value_adds_usdt_and_all_positions()
    {
        var p = TestData.NewPortfolio(1, 100m, TestData.Holding(1, 2m, 100m), TestData.Holding(2, 10m, 30m));
        var prices = new Dictionary<int, decimal> { [1] = 50m, [2] = 3m };
        Assert.True(PortfolioValuator.TryGetPortfolioValue(p, prices, out var value));
        Assert.Equal(230m, value);
    }

    [Fact]
    public void Missing_price_for_a_held_position_makes_the_value_unavailable()
    {
        var p = TestData.NewPortfolio(1, 100m, TestData.Holding(1, 2m, 100m));
        Assert.False(PortfolioValuator.TryGetPortfolioValue(p, new Dictionary<int, decimal>(), out var value));
        Assert.Equal(0m, value);
    }

    [Fact]
    public void Non_positive_price_makes_the_value_unavailable()
    {
        var p = TestData.NewPortfolio(1, 100m, TestData.Holding(1, 2m, 100m));
        Assert.False(PortfolioValuator.TryGetPortfolioValue(p, new Dictionary<int, decimal> { [1] = 0m }, out _));
    }

    [Fact]
    public void Position_with_zero_quantity_needs_no_price()
    {
        var p = TestData.NewPortfolio(1, 100m, TestData.InCooldown(1, 90m, TestData.T0));
        Assert.True(PortfolioValuator.TryGetPortfolioValue(p, new Dictionary<int, decimal>(), out var value));
        Assert.Equal(100m, value);
    }
}
