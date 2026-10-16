using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PortfolioManager.Data.Migrations.Sqlite;

/// <summary>Used only by 'dotnet ef'. Override the connection with the PM_SQLITE_CONNECTION environment variable.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PM_SQLITE_CONNECTION") ?? "Data Source=portfolio-design.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection, b => b.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name))
            .Options;
        return new AppDbContext(options);
    }
}
