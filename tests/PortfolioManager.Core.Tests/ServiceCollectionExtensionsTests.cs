using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Engine;
using PortfolioManager.Core.Execution;
using PortfolioManager.Core.Prices;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    private static ServiceCollection NewServices(InMemoryStore store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAssetRepository>(store);
        services.AddSingleton<IPortfolioRepository>(store);
        services.AddSingleton<ITradeLogRepository>(store);
        services.AddSingleton<IPriceLogRepository>(store);
        services.AddSingleton<IUnitOfWork>(store);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }

    [Fact]
    public void AddTradingCore_makes_the_engine_resolvable_with_default_options()
    {
        var services = NewServices(new InMemoryStore());
        services.AddTradingCore();
        services.AddSimulatedExchange();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.True(scope.ServiceProvider.GetRequiredService<ITradingEngine>() is TradingEngine);
        var options = provider.GetRequiredService<IOptions<TradingOptions>>().Value;
        Assert.Equal(TimeSpan.FromHours(1), options.CycleInterval);
        Assert.Equal(0.1m, options.FeePercent);
        Assert.Equal(0.05m, options.SlippagePercent);
    }

    [Fact]
    public void AddTradingCore_applies_the_configure_callback()
    {
        var services = NewServices(new InMemoryStore());
        services.AddTradingCore(o => { o.FeePercent = 0.5m; o.CycleInterval = TimeSpan.FromMinutes(30); });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TradingOptions>>().Value;
        Assert.Equal(0.5m, options.FeePercent);
        Assert.Equal(TimeSpan.FromMinutes(30), options.CycleInterval);
    }

    [Fact]
    public void AddSimulatedExchange_registers_the_simulated_provider_and_executor()
    {
        var services = NewServices(new InMemoryStore());
        services.AddTradingCore();
        services.AddSimulatedExchange(seed: 3);
        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<IPriceProvider>() is SimulatedPriceProvider);
        Assert.True(provider.GetRequiredService<ITradeExecutor>() is SimulatedTradeExecutor);
    }

    [Fact]
    public void A_host_registered_exchange_is_not_replaced()
    {
        var services = NewServices(new InMemoryStore());
        var fake = new FakePriceProvider();
        services.AddSingleton<IPriceProvider>(fake);
        services.AddTradingCore();
        services.AddSimulatedExchange();
        using var provider = services.BuildServiceProvider();
        Assert.True(ReferenceEquals(fake, provider.GetRequiredService<IPriceProvider>()));
    }

    [Fact]
    public async Task The_resolved_engine_runs_a_full_cycle_end_to_end()
    {
        var store = new InMemoryStore();
        store.Assets.Add(TestData.Gold());
        var portfolio = TestData.NewPortfolio(1, 1000m);
        store.Portfolios.Add(portfolio);

        var services = NewServices(store);
        services.AddTradingCore();
        services.AddSimulatedExchange();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ITradingEngine>().RunCycleAsync();

        Assert.Equal(1, result.PortfoliosProcessed);
        Assert.Single(store.PriceLogs);
        var log = Assert.Single(store.TradeLogs);
        Assert.Equal(TradeSide.Buy, log.Side);
        Assert.Equal(PositionStatus.Holding, portfolio.Positions.Single().Status);
        Assert.True(portfolio.UsdtBalance < 1000m);
    }
}
