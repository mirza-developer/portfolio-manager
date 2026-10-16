using PortfolioManager.Core.Calculation;

namespace PortfolioManager.Core.Tests;

public class StopLossEvaluatorTests
{
    [Fact]
    public void Rising_value_moves_the_benchmark_up_and_does_not_sell()
    {
        var r = StopLossEvaluator.Evaluate(1200m, 1000m, 5m);
        Assert.Equal(1200m, r.NewBenchmark);
        Assert.False(r.ShouldSell);
        Assert.Equal(0m, r.DrawdownPercent);
    }

    [Fact]
    public void Falling_value_never_lowers_the_benchmark()
    {
        var r = StopLossEvaluator.Evaluate(1180m, 1200m, 5m);
        Assert.Equal(1200m, r.NewBenchmark);
        Assert.False(r.ShouldSell);
        Assert.True(r.DrawdownPercent > 1.6m && r.DrawdownPercent < 1.7m);
    }

    [Fact]
    public void Exactly_at_the_limit_does_not_sell()
    {
        var r = StopLossEvaluator.Evaluate(1140m, 1200m, 5m);
        Assert.False(r.ShouldSell);
    }

    [Fact]
    public void Just_beyond_the_limit_sells()
    {
        var r = StopLossEvaluator.Evaluate(1139m, 1200m, 5m);
        Assert.True(r.ShouldSell);
        Assert.Equal(1200m, r.NewBenchmark);
    }

    [Fact]
    public void Documented_example_1000_to_1200_then_1139_sells_and_1141_keeps()
    {
        var up = StopLossEvaluator.Evaluate(1200m, 1000m, 5m);
        Assert.Equal(1200m, up.NewBenchmark);
        Assert.True(StopLossEvaluator.Evaluate(1139m, up.NewBenchmark, 5m).ShouldSell);
        Assert.False(StopLossEvaluator.Evaluate(1141m, up.NewBenchmark, 5m).ShouldSell);
    }

    [Fact]
    public void A_custom_limit_is_respected()
    {
        Assert.False(StopLossEvaluator.Evaluate(900m, 1000m, 10m).ShouldSell);
        Assert.True(StopLossEvaluator.Evaluate(899m, 1000m, 10m).ShouldSell);
    }

    [Fact]
    public void Drawdown_is_the_percent_below_the_benchmark()
    {
        Assert.Equal(5m, StopLossEvaluator.Evaluate(950m, 1000m, 5m).DrawdownPercent);
    }

    [Fact]
    public void Zero_benchmark_and_zero_value_do_not_sell()
    {
        var r = StopLossEvaluator.Evaluate(0m, 0m, 5m);
        Assert.False(r.ShouldSell);
        Assert.Equal(0m, r.DrawdownPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100)]
    [InlineData(150)]
    public void Invalid_limit_throws(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => { StopLossEvaluator.Evaluate(100m, 100m, limit); });
    }

    [Fact]
    public void Negative_value_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => { StopLossEvaluator.Evaluate(-1m, 100m, 5m); });
    }
}
