namespace PortfolioManager.Data.Seeding;

public sealed class SeedOptions
{
    public string? AdminEmail { get; set; }
    /// <summary>Set through user-secrets or environment variables. Never commit a real password.</summary>
    public string? AdminPassword { get; set; }
    public bool CreateDemoPortfolio { get; set; }
}
