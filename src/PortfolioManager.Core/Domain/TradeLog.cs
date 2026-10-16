namespace PortfolioManager.Core.Domain;

public class TradeLog
{
    public long Id { get; set; }
    public int PortfolioId { get; set; }
    public int AssetId { get; set; }
    public TradeSide Side { get; set; }
    public TradeReason Reason { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Execution price in USDT (includes slippage).</summary>
    public decimal PriceUsdt { get; set; }
    /// <summary>Net USDT moved: spent on a buy, received (after fee) on a sell.</summary>
    public decimal ValueUsdt { get; set; }
    public decimal FeeUsdt { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? Note { get; set; }
}
