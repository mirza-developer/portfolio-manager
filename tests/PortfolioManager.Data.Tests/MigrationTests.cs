using Microsoft.EntityFrameworkCore;

namespace PortfolioManager.Data.Tests;

public class MigrationTests
{
    [Fact]
    public void Sqlite_migrations_apply_from_scratch_and_leave_nothing_pending()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        Assert.NotEmpty(context.Database.GetAppliedMigrations());
        Assert.Empty(context.Database.GetPendingMigrations());
        Assert.Equal(0, context.Assets.Count());
    }

    [Fact]
    public void Sqlite_migrations_are_up_to_date_with_the_model()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        Assert.False(ModelDiff.HasPendingChanges(context));
    }

    [Fact]
    public void SqlServer_migrations_are_up_to_date_with_the_model()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=never-connected;Trusted_Connection=True;TrustServerCertificate=True",
                b => b.MigrationsAssembly(DataServiceCollectionExtensions.SqlServerMigrationsAssembly))
            .Options;
        using var context = new AppDbContext(options); // no connection is opened
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(ModelDiff.HasPendingChanges(context));
    }

    [Fact]
    public void Both_providers_have_the_same_migration_names()
    {
        using var db = new TestDatabase();
        using var sqlite = db.NewContext();
        var sqlServerOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=never-connected;Trusted_Connection=True;TrustServerCertificate=True",
                b => b.MigrationsAssembly(DataServiceCollectionExtensions.SqlServerMigrationsAssembly))
            .Options;
        using var sqlServer = new AppDbContext(sqlServerOptions);
        Assert.Equal(sqlite.Database.GetMigrations().ToList(), sqlServer.Database.GetMigrations().ToList());
    }
}
