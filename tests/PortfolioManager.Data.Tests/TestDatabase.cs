using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioManager.Data.Identity;
using PortfolioManager.Data.Seeding;

namespace PortfolioManager.Data.Tests;

/// <summary>
/// A throw-away SQLite database file with the real SQLite migrations applied, wired through AddDataLayer.
/// SQLite is used only to mock the database in tests; the SQL Server migrations are checked separately.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pm-test-{Guid.NewGuid():N}.db");

    public ServiceProvider Services { get; }

    public TestDatabase(Action<SeedOptions>? configureSeed = null, Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataLayer(new DatabaseOptions
        {
            Provider = DatabaseProvider.Sqlite,
            ConnectionString = $"Data Source={_path}",
            AutoMigrate = true
        });
        services.Configure<SeedOptions>(o => configureSeed?.Invoke(o));
        configureServices?.Invoke(services);
        Services = services.BuildServiceProvider();

        using var context = NewContext();
        context.Database.Migrate();
        if (!context.Database.GetAppliedMigrations().Any())
            throw new InvalidOperationException(
                "No SQLite migrations found. Run scripts/phase2-setup (or scripts/add-migration) to create them first.");
    }

    public AppDbContext NewContext() => Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    public IServiceScope NewScope() => Services.CreateScope();

    /// <summary>Portfolios have a foreign key to an Identity user, so tests need a user row first.</summary>
    public static async Task<string> AddUserAsync(AppDbContext context, string name = "user@example.com")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            Email = name,
            NormalizedEmail = name.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            DisplayName = name
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    public void Dispose()
    {
        Services.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }
}
