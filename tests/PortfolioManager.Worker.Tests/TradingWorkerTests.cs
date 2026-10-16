using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PortfolioManager.Core;
using PortfolioManager.Core.Engine;
using PortfolioManager.Worker.Tests.TestDoubles;

namespace PortfolioManager.Worker.Tests;

public class TradingWorkerTests
{
    private sealed class Harness : IAsyncDisposable
    {
        public FakeEngine Engine { get; } = new();
        public ManualTimeProvider Time { get; } = new();
        public List<Guid> ScopeIds { get; } = new();
        public ServiceProvider Provider { get; }
        public TradingWorker Worker { get; }

        public Harness(TimeSpan? interval = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(Engine);
            services.AddScoped<ScopeMarker>();
            // The engine is resolved per scope; it records which scope it ran in.
            services.AddScoped<ITradingEngine>(sp =>
            {
                ScopeIds.Add(sp.GetRequiredService<ScopeMarker>().Id);
                return sp.GetRequiredService<FakeEngine>();
            });
            Provider = services.BuildServiceProvider();
            Worker = new TradingWorker(
                Provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new TradingOptions { CycleInterval = interval ?? TimeSpan.FromHours(1) }),
                Time,
                NullLogger<TradingWorker>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await Worker.StopAsync(CancellationToken.None);
            Worker.Dispose();
            await Provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task Runs_a_cycle_immediately_at_start()
    {
        await using var h = new Harness();
        await h.Worker.StartAsync(CancellationToken.None);
        await h.Engine.WaitForCallsAsync(1);
        Assert.Equal(1, h.Engine.Calls);
    }

    [Fact]
    public async Task Runs_another_cycle_on_every_interval_tick()
    {
        await using var h = new Harness();
        await h.Worker.StartAsync(CancellationToken.None);
        await h.Engine.WaitForCallsAsync(1);

        h.Time.Tick();
        await h.Engine.WaitForCallsAsync(2);
        h.Time.Tick();
        await h.Engine.WaitForCallsAsync(3);

        Assert.Equal(3, h.Engine.Calls);
    }

    [Fact]
    public async Task A_failing_cycle_does_not_stop_the_loop()
    {
        await using var h = new Harness();
        h.Engine.FailOnCall = call => call == 1 ? new InvalidOperationException("exchange down") : null;
        await h.Worker.StartAsync(CancellationToken.None);
        await h.Engine.WaitForCallsAsync(1);

        h.Time.Tick();
        await h.Engine.WaitForCallsAsync(2);
        h.Time.Tick();
        await h.Engine.WaitForCallsAsync(3);

        Assert.Equal(3, h.Engine.Calls);
    }

    [Fact]
    public async Task Every_cycle_gets_its_own_dependency_injection_scope()
    {
        await using var h = new Harness();
        await h.Worker.StartAsync(CancellationToken.None);
        await h.Engine.WaitForCallsAsync(1);
        h.Time.Tick();
        await h.Engine.WaitForCallsAsync(2);

        Assert.Equal(2, h.ScopeIds.Count);
        Assert.NotEqual(h.ScopeIds[0], h.ScopeIds[1]);
    }

    [Fact]
    public async Task Stops_gracefully_and_runs_no_more_cycles_after_stopping()
    {
        await using var h = new Harness();
        await h.Worker.StartAsync(CancellationToken.None);
        await h.Engine.WaitForCallsAsync(1);

        await h.Worker.StopAsync(CancellationToken.None);
        h.Time.Tick();
        await Task.Delay(200);

        Assert.Equal(1, h.Engine.Calls);
    }

    [Fact]
    public async Task A_cancelled_start_token_ends_the_loop_without_an_error()
    {
        await using var h = new Harness();
        using var cts = new CancellationTokenSource();
        await h.Worker.StartAsync(cts.Token);
        await h.Engine.WaitForCallsAsync(1);

        cts.Cancel();
        await h.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, h.Engine.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_cycle_interval_of_zero_or_less_is_rejected_at_start(int seconds)
    {
        var h = new Harness(TimeSpan.FromSeconds(seconds));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Worker.StartAsync(CancellationToken.None));
        Assert.Equal(0, h.Engine.Calls);
        h.Worker.Dispose();
        await h.Provider.DisposeAsync();
    }
}
