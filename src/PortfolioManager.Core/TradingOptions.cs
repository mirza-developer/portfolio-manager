namespace PortfolioManager.Core;

public sealed class TradingOptions
{
    /// <summary>How often the host (Windows service) runs a trading cycle.</summary>
    public TimeSpan CycleInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Fee in percent of the traded amount.</summary>
    public decimal FeePercent { get; set; } = 0.1m;

    /// <summary>Adverse price movement in percent applied to every execution.</summary>
    public decimal SlippagePercent { get; set; } = 0.05m;
}
