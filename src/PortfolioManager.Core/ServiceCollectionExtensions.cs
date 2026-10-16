using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Engine;
using PortfolioManager.Core.Execution;
using PortfolioManager.Core.Prices;
using PortfolioManager.Core.Strategy;

namespace PortfolioManager.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the trading engine, default strategy, rule guard and options. The host must also register the
    /// repositories, IUnitOfWork, IPriceProvider and ITradeExecutor (see <see cref="AddSimulatedExchange"/> for the simulated ones).
    /// </summary>
    public static IServiceCollection AddTradingCore(this IServiceCollection services, Action<TradingOptions>? configure = null)
    {
        var options = services.AddOptions<TradingOptions>();
        if (configure is not null) options.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IBuyStrategy, EqualWeightBuyStrategy>();
        services.TryAddSingleton<TradeRuleGuard>();
        services.TryAddScoped<ITradingEngine, TradingEngine>();
        return services;
    }

    /// <summary>Registers the simulated price provider and trade executor (no real exchange, no real money).</summary>
    public static IServiceCollection AddSimulatedExchange(this IServiceCollection services, int seed = 42, decimal volatilityPercent = 1m)
    {
        services.TryAddSingleton<IPriceProvider>(_ => new SimulatedPriceProvider(seed, volatilityPercent));
        services.TryAddSingleton<ITradeExecutor, SimulatedTradeExecutor>();
        return services;
    }
}
