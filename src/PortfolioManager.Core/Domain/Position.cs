namespace PortfolioManager.Core.Domain;

public class Position
{
    public int Id { get; set; }
    public int PortfolioId { get; set; }
    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public decimal Quantity { get; set; }
    /// <summary>v0: the value (USDT) when the position was first created.</summary>
    public decimal InitialValueUsdt { get; set; }
    /// <summary>Trailing benchmark: the highest value (USDT) seen while holding.</summary>
    public decimal BenchmarkValueUsdt { get; set; }
    /// <summary>When set, replaces the portfolio stop-loss percent for this position.</summary>
    public decimal? StopLossPercentOverride { get; set; }

    public PositionStatus Status { get; set; } = PositionStatus.None;
    public DateTime? CooldownUntilUtc { get; set; }
    public decimal? LastSellPriceUsdt { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public Position Clone() => (Position)MemberwiseClone();

    /// <summary>Copies all state except the identity (Id, PortfolioId).</summary>
    public void CopyFrom(Position other)
    {
        AssetId = other.AssetId;
        Asset = other.Asset;
        Quantity = other.Quantity;
        InitialValueUsdt = other.InitialValueUsdt;
        BenchmarkValueUsdt = other.BenchmarkValueUsdt;
        StopLossPercentOverride = other.StopLossPercentOverride;
        Status = other.Status;
        CooldownUntilUtc = other.CooldownUntilUtc;
        LastSellPriceUsdt = other.LastSellPriceUsdt;
        UpdatedUtc = other.UpdatedUtc;
    }
}
