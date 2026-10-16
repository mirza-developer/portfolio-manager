using Microsoft.EntityFrameworkCore;
using PortfolioManager.Core.Calculation;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data;
using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Services;

public interface IPortfolioService
{
    Task<IReadOnlyList<PortfolioSummary>> ListAsync(UserContext user, CancellationToken ct = default);
    /// <summary>Null when the portfolio does not exist or belongs to someone else (and the caller is not an admin).</summary>
    Task<PortfolioDetails?> GetAsync(UserContext user, int id, CancellationToken ct = default);
    Task UpdateSettingsAsync(UserContext user, int id, PortfolioSettings settings, CancellationToken ct = default);
    Task<int> CreatePortfolioAsync(UserContext admin, string ownerUserId, string name, decimal usdtBalance, CancellationToken ct = default);
    Task UpdateUsdtBalanceAsync(UserContext admin, int id, decimal usdtBalance, CancellationToken ct = default);
    Task AddPositionAsync(UserContext admin, int portfolioId, int assetId, decimal quantity, decimal initialValueUsdt, CancellationToken ct = default);
    Task UpdatePositionAsync(UserContext admin, int portfolioId, int positionId, PositionEdit edit, CancellationToken ct = default);
}

public sealed class PortfolioService(IDbContextFactory<AppDbContext> factory, TimeProvider time) : IPortfolioService
{
    public async Task<IReadOnlyList<PortfolioSummary>> ListAsync(UserContext user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Portfolios.AsNoTracking().Include(p => p.Positions).AsQueryable();
        if (!user.IsAdmin) query = query.Where(p => p.UserId == user.UserId);
        var portfolios = await query.OrderBy(p => p.Id).ToListAsync(ct);

        var prices = await LatestPrices.GetAsync(db, ct);
        var owners = await OwnerNamesAsync(db, portfolios.Select(p => p.UserId).Distinct().ToList(), ct);

        return portfolios.Select(p => new PortfolioSummary(
            p.Id, p.Name, owners.GetValueOrDefault(p.UserId, string.Empty), p.UsdtBalance, ValueOf(p, prices),
            p.StopLossPercent, p.IsAutoTradingEnabled, p.Positions.Count(x => x.Status == PositionStatus.Holding))).ToList();
    }

    public async Task<PortfolioDetails?> GetAsync(UserContext user, int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var portfolio = await db.Portfolios.AsNoTracking()
            .Include(p => p.Positions).ThenInclude(x => x.Asset)
            .SingleOrDefaultAsync(p => p.Id == id, ct);
        if (portfolio is null || (!user.IsAdmin && portfolio.UserId != user.UserId)) return null;

        var prices = await LatestPrices.GetAsync(db, ct);
        var owners = await OwnerNamesAsync(db, new[] { portfolio.UserId }, ct);

        var rows = portfolio.Positions.OrderBy(x => x.Id).Select(x =>
        {
            var hasPrice = prices.TryGetValue(x.AssetId, out var price);
            var value = hasPrice ? x.Quantity * price : 0m;
            var limit = x.StopLossPercentOverride ?? portfolio.StopLossPercent;
            var drawdown = hasPrice && x.Status == PositionStatus.Holding && limit is > 0m and < 100m
                ? StopLossEvaluator.Evaluate(value, x.BenchmarkValueUsdt, limit).DrawdownPercent
                : 0m;
            return new PositionRow(x.Id, x.AssetId, x.Asset?.Symbol ?? "?", x.Quantity, value, x.BenchmarkValueUsdt,
                x.InitialValueUsdt, drawdown, limit, x.StopLossPercentOverride, x.Status, hasPrice);
        }).ToList();

        return new PortfolioDetails(portfolio.Id, portfolio.Name, owners.GetValueOrDefault(portfolio.UserId, string.Empty),
            portfolio.UsdtBalance, ValueOf(portfolio, prices), ToSettings(portfolio), rows);
    }

    public async Task UpdateSettingsAsync(UserContext user, int id, PortfolioSettings s, CancellationToken ct = default)
    {
        Guard.Check(s.StopLossPercent > 0m && s.StopLossPercent < 100m, "Stop-loss must be greater than 0 and less than 100 percent.");
        Guard.Check(s.MaxAllocationPercent > 0m && s.MaxAllocationPercent <= 100m, "Max allocation must be greater than 0 and at most 100 percent.");
        Guard.Check(s.CashReservePercent >= 0m && s.CashReservePercent < 100m, "Cash reserve must be at least 0 and less than 100 percent.");
        Guard.Check(s.CooldownHours is >= 0 and <= 720, "Cooldown must be between 0 and 720 hours.");
        Guard.Check(s.ReentryRecoveryPercent >= 0m && s.ReentryRecoveryPercent <= 100m, "Re-entry recovery must be between 0 and 100 percent.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var portfolio = await LoadOwnedAsync(db, user, id, ct);
        portfolio.StopLossPercent = s.StopLossPercent;
        portfolio.MaxAllocationPercent = s.MaxAllocationPercent;
        portfolio.CashReservePercent = s.CashReservePercent;
        portfolio.CooldownHours = s.CooldownHours;
        portfolio.ReentryRecoveryPercent = s.ReentryRecoveryPercent;
        portfolio.IsAutoTradingEnabled = s.IsAutoTradingEnabled;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CreatePortfolioAsync(UserContext admin, string ownerUserId, string name, decimal usdtBalance, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        Guard.Check(!string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200, "A portfolio name (up to 200 characters) is required.");
        Guard.Check(usdtBalance >= 0m, "The USDT balance cannot be negative.");

        await using var db = await factory.CreateDbContextAsync(ct);
        Guard.Check(await db.Users.AnyAsync(u => u.Id == ownerUserId, ct), "The owner does not exist.");
        var portfolio = new Portfolio { UserId = ownerUserId, Name = name.Trim(), UsdtBalance = usdtBalance, CreatedUtc = time.GetUtcNow().UtcDateTime };
        db.Portfolios.Add(portfolio);
        await db.SaveChangesAsync(ct);
        return portfolio.Id;
    }

    public async Task UpdateUsdtBalanceAsync(UserContext admin, int id, decimal usdtBalance, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        Guard.Check(usdtBalance >= 0m, "The USDT balance cannot be negative.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var portfolio = await LoadOwnedAsync(db, admin, id, ct);
        portfolio.UsdtBalance = usdtBalance;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddPositionAsync(UserContext admin, int portfolioId, int assetId, decimal quantity, decimal initialValueUsdt, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        Guard.Check(quantity > 0m, "The quantity must be greater than 0.");
        Guard.Check(initialValueUsdt > 0m, "The initial value must be greater than 0.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var portfolio = await LoadOwnedAsync(db, admin, portfolioId, ct, includePositions: true);
        var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == assetId, ct);
        Guard.Check(asset is not null, "The asset does not exist.");
        Guard.Check(asset!.IsEnabled, "The asset is disabled.");
        Guard.Check(portfolio.Positions.All(p => p.AssetId != assetId), "The portfolio already has a position for this asset.");

        portfolio.Positions.Add(new Position
        {
            AssetId = assetId,
            Quantity = quantity,
            InitialValueUsdt = initialValueUsdt,
            BenchmarkValueUsdt = initialValueUsdt,
            Status = PositionStatus.Holding,
            UpdatedUtc = time.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePositionAsync(UserContext admin, int portfolioId, int positionId, PositionEdit edit, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        Guard.Check(edit.Quantity >= 0m, "The quantity cannot be negative.");
        Guard.Check(edit.InitialValueUsdt >= 0m, "The initial value cannot be negative.");
        Guard.Check(edit.StopLossPercentOverride is null or (> 0m and < 100m), "The stop-loss override must be greater than 0 and less than 100 percent.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var portfolio = await LoadOwnedAsync(db, admin, portfolioId, ct, includePositions: true);
        var position = portfolio.Positions.SingleOrDefault(p => p.Id == positionId)
                       ?? throw new KeyNotFoundException("The position does not exist.");

        position.Quantity = edit.Quantity;
        position.InitialValueUsdt = edit.InitialValueUsdt;
        position.StopLossPercentOverride = edit.StopLossPercentOverride;
        if (edit.Quantity > 0m)
        {
            position.Status = PositionStatus.Holding;
            position.BenchmarkValueUsdt = edit.InitialValueUsdt; // a manual change restarts the trailing benchmark at v0
            position.CooldownUntilUtc = null;
            position.LastSellPriceUsdt = null;
        }
        else
        {
            position.Status = PositionStatus.None;
            position.BenchmarkValueUsdt = 0m;
        }
        position.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Portfolio> LoadOwnedAsync(AppDbContext db, UserContext user, int id, CancellationToken ct, bool includePositions = false)
    {
        var query = includePositions ? db.Portfolios.Include(p => p.Positions) : db.Portfolios.AsQueryable();
        var portfolio = await query.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new KeyNotFoundException("The portfolio does not exist.");
        if (!user.IsAdmin && portfolio.UserId != user.UserId) throw new UnauthorizedAccessException("This portfolio belongs to another user.");
        return portfolio;
    }

    private static decimal ValueOf(Portfolio p, IReadOnlyDictionary<int, decimal> prices) =>
        p.UsdtBalance + p.Positions.Where(x => x.Quantity > 0m).Sum(x => prices.TryGetValue(x.AssetId, out var price) ? x.Quantity * price : 0m);

    private static PortfolioSettings ToSettings(Portfolio p) => new()
    {
        StopLossPercent = p.StopLossPercent,
        MaxAllocationPercent = p.MaxAllocationPercent,
        CashReservePercent = p.CashReservePercent,
        CooldownHours = p.CooldownHours,
        ReentryRecoveryPercent = p.ReentryRecoveryPercent,
        IsAutoTradingEnabled = p.IsAutoTradingEnabled
    };

    private static async Task<Dictionary<string, string>> OwnerNamesAsync(AppDbContext db, IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName, u.Email }).ToListAsync(ct);
        return users.ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email ?? string.Empty : u.DisplayName);
    }
}
