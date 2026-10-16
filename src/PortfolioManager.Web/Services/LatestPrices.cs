using Microsoft.EntityFrameworkCore;
using PortfolioManager.Data;

namespace PortfolioManager.Web.Services;

internal static class LatestPrices
{
    /// <summary>The most recent logged USDT price per asset id (one small query per asset; there are few assets).</summary>
    public static async Task<Dictionary<int, decimal>> GetAsync(AppDbContext db, CancellationToken ct)
    {
        var result = new Dictionary<int, decimal>();
        var assetIds = await db.Assets.AsNoTracking().Select(a => a.Id).ToListAsync(ct);
        foreach (var id in assetIds)
        {
            var price = await db.PriceLogs.AsNoTracking()
                .Where(l => l.AssetId == id)
                .OrderByDescending(l => l.TimestampUtc).ThenByDescending(l => l.Id)
                .Select(l => (decimal?)l.PriceUsdt)
                .FirstOrDefaultAsync(ct);
            if (price is > 0m) result[id] = price.Value;
        }
        return result;
    }
}
