using Microsoft.EntityFrameworkCore;
using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Domain;

namespace PortfolioManager.Data.Repositories;

public sealed class EfAssetRepository(AppDbContext db) : IAssetRepository
{
    public async Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken ct = default) =>
        await db.Assets.OrderBy(a => a.Id).ToListAsync(ct);
}

public sealed class EfPortfolioRepository(AppDbContext db) : IPortfolioRepository
{
    public async Task<IReadOnlyList<Portfolio>> GetAutoTradingPortfoliosAsync(CancellationToken ct = default) =>
        await db.Portfolios
            .Include(p => p.Positions)
            .Where(p => p.IsAutoTradingEnabled)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
}

public sealed class EfTradeLogRepository(AppDbContext db) : ITradeLogRepository
{
    public void Add(TradeLog log) => db.TradeLogs.Add(log);
}

public sealed class EfPriceLogRepository(AppDbContext db) : IPriceLogRepository
{
    public void AddRange(IEnumerable<PriceLog> logs) => db.PriceLogs.AddRange(logs);
}

/// <summary>
/// Saves the shared DbContext. When a save fails, the pending changes are discarded (added rows detached, modified rows
/// restored) so one failing portfolio cannot poison the next save in the same trading cycle.
/// </summary>
public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            DiscardPendingChanges();
            throw;
        }
    }

    private void DiscardPendingChanges()
    {
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }
    }
}
