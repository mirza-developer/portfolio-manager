using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PortfolioManager.Data.Migrations.SqlServer;

/// <summary>Used only by 'dotnet ef'. Override the connection with the PM_SQLSERVER_CONNECTION environment variable.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PM_SQLSERVER_CONNECTION")
                         ?? @"Server=(localdb)\mssqllocaldb;Database=PortfolioManagerDesign;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, b => b.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name))
            .Options;
        return new AppDbContext(options);
    }
}
