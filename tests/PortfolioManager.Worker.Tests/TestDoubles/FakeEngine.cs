using PortfolioManager.Core.Engine;

namespace PortfolioManager.Worker.Tests.TestDoubles;

public sealed class FakeEngine : ITradingEngine
{
    private readonly SemaphoreSlim _signal = new(0);
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);
    public Func<int, Exception?>? FailOnCall { get; set; }
    public List<object> InstancesSeenPerCall { get; } = new();

    public Task<CycleResult> RunCycleAsync(CancellationToken ct = default)
    {
        var call = Interlocked.Increment(ref _calls);
        _signal.Release();
        var failure = FailOnCall?.Invoke(call);
        if (failure is not null) throw failure;
        return Task.FromResult(new CycleResult(DateTime.UtcNow, 1, 0, 0));
    }

    /// <summary>Waits until the engine has been called at least <paramref name="count"/> times in total.</summary>
    public async Task WaitForCallsAsync(int count)
    {
        while (Calls < count)
        {
            if (!await _signal.WaitAsync(TimeSpan.FromSeconds(5)))
                throw new TimeoutException($"Expected {count} engine call(s) but saw {Calls}.");
        }
    }
}

/// <summary>Scoped marker used to prove that every cycle gets its own DI scope.</summary>
public sealed class ScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}
