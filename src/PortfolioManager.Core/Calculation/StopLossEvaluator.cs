namespace PortfolioManager.Core.Calculation;

public readonly record struct StopLossResult(decimal NewBenchmark, bool ShouldSell, decimal DrawdownPercent);

/// <summary>Trailing stop-loss: the benchmark only moves up; selling happens when value falls MORE than the limit below it.</summary>
public static class StopLossEvaluator
{
    public static StopLossResult Evaluate(decimal value, decimal benchmark, decimal limitPercent)
    {
        if (limitPercent <= 0m || limitPercent >= 100m)
            throw new ArgumentOutOfRangeException(nameof(limitPercent), limitPercent, "Limit must be greater than 0 and less than 100.");
        if (value < 0m)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be negative.");

        var newBenchmark = Math.Max(benchmark, value);
        var drawdown = newBenchmark > 0m ? (newBenchmark - value) / newBenchmark * 100m : 0m;
        var sellLine = newBenchmark * (1m - limitPercent / 100m);
        return new StopLossResult(newBenchmark, value < sellLine, drawdown);
    }
}
