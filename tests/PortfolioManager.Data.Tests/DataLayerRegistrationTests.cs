using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortfolioManager.Core.Abstractions;

namespace PortfolioManager.Data.Tests;

public class DataLayerRegistrationTests
{
    private static ServiceProvider Build(DatabaseOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataLayer(options);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Sqlite_provider_resolves_a_sqlite_context()
    {
        using var provider = Build(new DatabaseOptions { Provider = DatabaseProvider.Sqlite, ConnectionString = "Data Source=unused.db" });
        using var scope = provider.CreateScope();
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ProviderName);
    }

    [Fact]
    public void SqlServer_provider_resolves_a_sql_server_context_without_connecting()
    {
        using var provider = Build(new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=localhost;Database=never-connected;Trusted_Connection=True;TrustServerCertificate=True"
        });
        using var scope = provider.CreateScope();
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ProviderName);
    }

    [Fact]
    public void A_missing_connection_string_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => Build(new DatabaseOptions { ConnectionString = " " }));
    }

    [Fact]
    public void The_configuration_overload_reads_the_Database_section()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SqlServer",
            ["Database:ConnectionString"] = "Server=localhost;Database=x;Trusted_Connection=True;TrustServerCertificate=True",
            ["Database:AutoMigrate"] = "true"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataLayer(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<DatabaseOptions>();
        Assert.Equal(DatabaseProvider.SqlServer, options.Provider);
        Assert.True(options.AutoMigrate);
    }

    [Fact]
    public void Core_repositories_and_the_unit_of_work_are_registered_per_scope()
    {
        using var provider = Build(new DatabaseOptions { ConnectionString = "Data Source=unused.db" });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAssetRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPortfolioRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITradeLogRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPriceLogRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }

    [Fact]
    public void The_factory_creates_independent_contexts_for_blazor_circuits()
    {
        using var provider = Build(new DatabaseOptions { ConnectionString = "Data Source=unused.db" });
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var a = factory.CreateDbContext();
        using var b = factory.CreateDbContext();
        Assert.False(ReferenceEquals(a, b));
    }
}
