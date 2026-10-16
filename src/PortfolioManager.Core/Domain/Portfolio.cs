namespace PortfolioManager.Core.Domain;

public class Portfolio
{
    public int Id { get; set; }
    /// <summary>Id of the Identity user that owns this portfolio.</summary>
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Tether balance: the main currency of the system.</summary>
    public decimal UsdtBalance { get; set; }

    public decimal StopLossPercent { get; set; } = 5m;
    public decimal MaxAllocationPercent { get; set; } = 40m;
    public decimal CashReservePercent { get; set; } = 10m;
    public int CooldownHours { get; set; } = 4;
    public decimal ReentryRecoveryPercent { get; set; } = 1m;
    public bool IsAutoTradingEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }

    public List<Position> Positions { get; set; } = new();

    /// <summary>Deep copy (positions are cloned). The engine works on a copy so a failure leaves the original untouched.</summary>
    public Portfolio Clone()
    {
        var copy = (Portfolio)MemberwiseClone();
        copy.Positions = Positions.Select(p => p.Clone()).ToList();
        return copy;
    }

    /// <summary>Applies the result of working on a clone: balance and positions (matched by AssetId; new ones are added).</summary>
    public void ApplyFrom(Portfolio working)
    {
        UsdtBalance = working.UsdtBalance;
        foreach (var wp in working.Positions)
        {
            var existing = Positions.FirstOrDefault(p => p.AssetId == wp.AssetId);
            if (existing is not null)
            {
                existing.CopyFrom(wp);
            }
            else
            {
                var added = wp.Clone();
                added.PortfolioId = Id;
                Positions.Add(added);
            }
        }
    }
}
