using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PortfolioManager.Core.Engine;
using PortfolioManager.Core.Execution;
using PortfolioManager.Core.Strategy;

namespace PortfolioManager.Core.Tests.TestDoubles;

/// <summary>Wires a TradingEngine with in-memory fakes. Fees and slippage default to ZERO so test math stays exact.</summary>
public sealed class EngineEnv
{
    public InMemoryStore Store { get; } = new();
    public FakePriceProvider Prices { get; } = new();
    public FakeClock Clock { get; } = new(TestData.T0);
    public TradingOptions Opts { get; } = new() { FeePercent = 0m, SlippagePercent = 0m };

    public TradingEngine Create(IBuyStrategy? strategy = null) => new(
        Store, Store, Prices, Store, Store, Store,
        new SimulatedTradeExecutor(Options.Create(Opts)),
        strategy ?? new EqualWeightBuyStrategy(),
        new TradeRuleGuard(),
        Clock,
        NullLogger<TradingEngine>.Instance);

    public Task<CycleResult> RunAsync(CancellationToken ct = default) => Create().RunCycleAsync(ct);
}
