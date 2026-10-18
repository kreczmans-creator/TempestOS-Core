namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

/// <summary>
/// A clock a Xero test moves by hand, so rate-limit windows, <c>Retry-After</c>,
/// <c>UpdatedDateUTC</c> and <c>If-Modified-Since</c> are asserted exactly,
/// with no sleeps.
/// </summary>
internal sealed class XeroSimulatorClock : TimeProvider
{
    private readonly object _sync = new();
    private DateTimeOffset _now;

    /// <summary>Starts the clock at <paramref name="start"/>; <see langword="null"/> for 2026-10-02 09:00 UTC.</summary>
    public XeroSimulatorClock(DateTimeOffset? start = null) => _now = start ?? new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_sync)
            return _now;
    }

    /// <summary>Moves the clock forward by <paramref name="by"/>.</summary>
    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        lock (_sync)
            _now += by;
    }
}
