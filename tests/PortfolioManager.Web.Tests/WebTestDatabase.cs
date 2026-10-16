using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data;
using PortfolioManager.Data.Identity;
using PortfolioManager.Data.Seeding;
using PortfolioManager.Web.Security;
using PortfolioManager.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace PortfolioManager.Web.Tests;

/// <summary>SQLite file database with the real migrations, the Web services registered, and GOLD (id 1) + SILVER (id 2) seeded.</summary>
public sealed class WebTestDatabase : IDisposable
{
    public const string Password = "Passw0rd#1";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pm-web-test-{Guid.NewGuid():N}.db");

    public ServiceProvider Services { get; }

    public WebTestDatabase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataLayer(new DatabaseOptions { Provider = DatabaseProvider.Sqlite, ConnectionString = $"Data Source={_path}", AutoMigrate = true });
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<ILogQueryService, LogQueryService>();
        services.AddScoped<IAssetAdminService, AssetAdminService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IDashboardService, DashboardService>();
        Services = services.BuildServiceProvider();

        using var context = NewContext();
        context.Database.Migrate();
        if (!context.Database.GetAppliedMigrations().Any())
            throw new InvalidOperationException("No SQLite migrations found. Run scripts/phase2-setup first.");
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync().GetAwaiter().GetResult();
    }

    public AppDbContext NewContext() => Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    public IServiceScope NewScope() => Services.CreateScope();

    public async Task<UserContext> AddUserAsync(string email, bool admin)
    {
        using var scope = NewScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = email };
        var created = await users.CreateAsync(user, Password);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join(";", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, admin ? AppRoles.Admin : AppRoles.User);
        return new UserContext(user.Id, admin, email);
    }

    public async Task<int> AddPortfolioAsync(string ownerId, string name, decimal usdt, params (int AssetId, decimal Quantity, decimal Benchmark)[] holdings)
    {
        using var context = NewContext();
        var portfolio = new Portfolio { UserId = ownerId, Name = name, UsdtBalance = usdt, CreatedUtc = DateTime.UtcNow };
        foreach (var h in holdings)
        {
            portfolio.Positions.Add(new Position
            {
                AssetId = h.AssetId, Quantity = h.Quantity, InitialValueUsdt = h.Benchmark, BenchmarkValueUsdt = h.Benchmark,
                Status = PositionStatus.Holding, UpdatedUtc = DateTime.UtcNow
            });
        }
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();
        return portfolio.Id;
    }

    public async Task AddPriceAsync(int assetId, decimal price, DateTime timestampUtc)
    {
        using var context = NewContext();
        context.PriceLogs.Add(new PriceLog { AssetId = assetId, PriceUsdt = price, TimestampUtc = timestampUtc, Source = "simulated" });
        await context.SaveChangesAsync();
    }

    public async Task AddTradeAsync(int portfolioId, int assetId, TradeSide side, DateTime timestampUtc, TradeReason reason = TradeReason.StopLoss)
    {
        using var context = NewContext();
        context.TradeLogs.Add(new TradeLog
        {
            PortfolioId = portfolioId, AssetId = assetId, Side = side, Reason = reason,
            Quantity = 1m, PriceUsdt = 10m, ValueUsdt = 10m, TimestampUtc = timestampUtc
        });
        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        Services.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }
}
