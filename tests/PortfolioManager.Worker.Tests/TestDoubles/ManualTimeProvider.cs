namespace PortfolioManager.Worker.Tests.TestDoubles;

/// <summary>A TimeProvider whose timers only fire when the test says so (works with PeriodicTimer).</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    public List<ManualTimer> Timers { get; } = new();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state);
        lock (Timers) Timers.Add(timer);
        return timer;
    }

    /// <summary>Fires every live timer once.</summary>
    public void Tick()
    {
        ManualTimer[] snapshot;
        lock (Timers) snapshot = Timers.Where(t => !t.Disposed).ToArray();
        foreach (var timer in snapshot) timer.Fire();
    }

    public sealed class ManualTimer : ITimer
    {
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public ManualTimer(TimerCallback callback, object? state)
        {
            _callback = callback;
            _state = state;
        }

        public bool Disposed { get; private set; }
        public void Fire() => _callback(_state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
