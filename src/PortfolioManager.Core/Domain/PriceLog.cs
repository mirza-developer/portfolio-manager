namespace PortfolioManager.Core.Domain;

public class PriceLog
{
    public long Id { get; set; }
    public int AssetId { get; set; }
    public decimal PriceUsdt { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Source { get; set; } = string.Empty;
}
