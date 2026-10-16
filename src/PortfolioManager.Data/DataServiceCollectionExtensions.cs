using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortfolioManager.Core.Abstractions;
using PortfolioManager.Data.Identity;
using PortfolioManager.Data.Repositories;
using PortfolioManager.Data.Seeding;

namespace PortfolioManager.Data;

public static class DataServiceCollectionExtensions
{
    public const string SqlServerMigrationsAssembly = "PortfolioManager.Data.Migrations.SqlServer";
    public const string SqliteMigrationsAssembly = "PortfolioManager.Data.Migrations.Sqlite";

    /// <summary>Reads the 'Database' and 'Seed' configuration sections.</summary>
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var database = configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.Configure<SeedOptions>(configuration.GetSection("Seed"));
        return services.AddDataLayer(database);
    }

    public static IServiceCollection AddDataLayer(this IServiceCollection services, DatabaseOptions database)
    {
        if (string.IsNullOrWhiteSpace(database.ConnectionString))
            throw new InvalidOperationException("Database:ConnectionString is required.");

        void Configure(DbContextOptionsBuilder options)
        {
            switch (database.Provider)
            {
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(database.ConnectionString, b => b.MigrationsAssembly(SqlServerMigrationsAssembly));
                    break;
                case DatabaseProvider.Sqlite:
                    options.UseSqlite(database.ConnectionString, b => b.MigrationsAssembly(SqliteMigrationsAssembly));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database provider: {database.Provider}");
            }
        }

        // Factory for Blazor Server (long-lived circuits) plus a scoped context for the trading repositories.
        services.AddDbContextFactory<AppDbContext>(Configure);
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        services.AddScoped<IAssetRepository, EfAssetRepository>();
        services.AddScoped<IPortfolioRepository, EfPortfolioRepository>();
        services.AddScoped<ITradeLogRepository, EfTradeLogRepository>();
        services.AddScoped<IPriceLogRepository, EfPriceLogRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(database);
        services.AddScoped<DbSeeder>();
        return services;
    }

    /// <summary>Applies pending migrations when <see cref="DatabaseOptions.AutoMigrate"/> is true, then runs the seeder.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DatabaseOptions>();
        if (options.AutoMigrate)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync(ct);
        }
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(ct);
    }
}
