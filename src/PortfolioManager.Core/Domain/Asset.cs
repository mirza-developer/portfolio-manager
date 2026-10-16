namespace PortfolioManager.Core.Domain;

/// <summary>A tradable crypto asset. Tether (USDT) is NOT an asset: it is Portfolio.UsdtBalance.</summary>
public class Asset
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public decimal MinTradeSizeUsdt { get; set; }
    public string PriceSource { get; set; } = "simulated";
}
