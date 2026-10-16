using Microsoft.EntityFrameworkCore;
using PortfolioManager.Data;
using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Services;

public interface ILogQueryService
{
    /// <summary>Non-admin users only ever see the logs of their own portfolios. <paramref name="page"/> is zero-based.</summary>
    Task<PagedResult<TradeLogRow>> GetTradeLogsAsync(UserContext user, TradeLogFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<PriceLogRow>> GetPriceLogsAsync(PriceLogFilter filter, int page, int pageSize, CancellationToken ct = default);
}

public sealed class LogQueryService(IDbContextFactory<AppDbContext> factory) : ILogQueryService
{
    public async Task<PagedResult<TradeLogRow>> GetTradeLogsAsync(UserContext user, TradeLogFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = from l in db.TradeLogs.AsNoTracking()
                    join p in db.Portfolios.AsNoTracking() on l.PortfolioId equals p.Id
                    join a in db.Assets.AsNoTracking() on l.AssetId equals a.Id
                    select new { Log = l, PortfolioName = p.Name, p.UserId, a.Symbol };

        if (!user.IsAdmin) query = query.Where(x => x.UserId == user.UserId);
        if (f.PortfolioId is int portfolioId) query = query.Where(x => x.Log.PortfolioId == portfolioId);
        if (f.AssetId is int assetId) query = query.Where(x => x.Log.AssetId == assetId);
        if (f.Side is { } side) query = query.Where(x => x.Log.Side == side);
        if (f.Reason is { } reason) query = query.Where(x => x.Log.Reason == reason);
        if (f.FromUtc is { } from) query = query.Where(x => x.Log.TimestampUtc >= from);
        if (f.ToUtc is { } to) query = query.Where(x => x.Log.TimestampUtc < to);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Log.TimestampUtc).ThenByDescending(x => x.Log.Id)
            .Skip(page * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<TradeLogRow>(
            rows.Select(x => new TradeLogRow(x.Log.Id, x.Log.TimestampUtc, x.PortfolioName, x.Symbol, x.Log.Side, x.Log.Reason,
                x.Log.Quantity, x.Log.PriceUsdt, x.Log.ValueUsdt, x.Log.FeeUsdt)).ToList(),
            total);
    }

    public async Task<PagedResult<PriceLogRow>> GetPriceLogsAsync(PriceLogFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = from l in db.PriceLogs.AsNoTracking()
                    join a in db.Assets.AsNoTracking() on l.AssetId equals a.Id
                    select new { Log = l, a.Symbol };

        if (f.AssetId is int assetId) query = query.Where(x => x.Log.AssetId == assetId);
        if (f.FromUtc is { } from) query = query.Where(x => x.Log.TimestampUtc >= from);
        if (f.ToUtc is { } to) query = query.Where(x => x.Log.TimestampUtc < to);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Log.TimestampUtc).ThenByDescending(x => x.Log.Id)
            .Skip(page * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<PriceLogRow>(
            rows.Select(x => new PriceLogRow(x.Log.Id, x.Log.TimestampUtc, x.Symbol, x.Log.PriceUsdt, x.Log.Source)).ToList(),
            total);
    }

    private static (int Page, int PageSize) Clamp(int page, int pageSize) => (Math.Max(0, page), Math.Clamp(pageSize, 1, 200));
}
