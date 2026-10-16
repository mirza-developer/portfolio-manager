namespace PortfolioManager.Core.Tests.TestDoubles;

public sealed class FakeClock : TimeProvider
{
    private DateTime _utcNow;

    public FakeClock(DateTime utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => new(_utcNow, TimeSpan.Zero);

    public void Advance(TimeSpan by) => _utcNow += by;
}
