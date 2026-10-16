using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data;
using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Services;

public interface IAssetAdminService
{
    /// <summary>Any signed-in user may list assets (the log filters need them).</summary>
    Task<IReadOnlyList<Asset>> ListAsync(CancellationToken ct = default);
    Task<int> SaveAsync(UserContext admin, AssetEdit edit, CancellationToken ct = default);
    Task SetEnabledAsync(UserContext admin, int id, bool enabled, CancellationToken ct = default);
    Task DeleteAsync(UserContext admin, int id, CancellationToken ct = default);
}

public sealed partial class AssetAdminService(IDbContextFactory<AppDbContext> factory) : IAssetAdminService
{
    [GeneratedRegex("^[A-Z0-9]{2,20}$")]
    private static partial Regex SymbolPattern();

    public async Task<IReadOnlyList<Asset>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Assets.AsNoTracking().OrderBy(a => a.Id).ToListAsync(ct);
    }

    public async Task<int> SaveAsync(UserContext admin, AssetEdit edit, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        var symbol = (edit.Symbol ?? string.Empty).Trim().ToUpperInvariant();
        Guard.Check(SymbolPattern().IsMatch(symbol), "The symbol must be 2 to 20 letters or digits.");
        Guard.Check(!string.IsNullOrWhiteSpace(edit.Name) && edit.Name.Trim().Length <= 100, "A name (up to 100 characters) is required.");
        Guard.Check(!string.IsNullOrWhiteSpace(edit.PriceSource) && edit.PriceSource.Trim().Length <= 50, "A price source (up to 50 characters) is required.");
        Guard.Check(edit.MinTradeSizeUsdt >= 0m, "The minimum trade size cannot be negative.");

        await using var db = await factory.CreateDbContextAsync(ct);
        Guard.Check(!await db.Assets.AnyAsync(a => a.Symbol == symbol && a.Id != (edit.Id ?? 0), ct), $"An asset with symbol {symbol} already exists.");

        Asset asset;
        if (edit.Id is int id)
        {
            asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new KeyNotFoundException("The asset does not exist.");
        }
        else
        {
            asset = new Asset();
            db.Assets.Add(asset);
        }

        asset.Symbol = symbol;
        asset.Name = edit.Name.Trim();
        asset.IsEnabled = edit.IsEnabled;
        asset.MinTradeSizeUsdt = edit.MinTradeSizeUsdt;
        asset.PriceSource = edit.PriceSource.Trim();
        await db.SaveChangesAsync(ct);
        return asset.Id;
    }

    public async Task SetEnabledAsync(UserContext admin, int id, bool enabled, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        await using var db = await factory.CreateDbContextAsync(ct);
        var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new KeyNotFoundException("The asset does not exist.");
        asset.IsEnabled = enabled;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(UserContext admin, int id, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        await using var db = await factory.CreateDbContextAsync(ct);
        var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new KeyNotFoundException("The asset does not exist.");
        var used = await db.Positions.AnyAsync(p => p.AssetId == id, ct)
                   || await db.TradeLogs.AnyAsync(l => l.AssetId == id, ct)
                   || await db.PriceLogs.AnyAsync(l => l.AssetId == id, ct);
        Guard.Check(!used, $"{asset.Symbol} is used by positions or logs and cannot be deleted. Disable it instead.");
        db.Assets.Remove(asset);
        await db.SaveChangesAsync(ct);
    }
}
