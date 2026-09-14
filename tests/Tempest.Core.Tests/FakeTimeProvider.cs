namespace Tempest.Core.Tests;

/// <summary>A clock the test pins, so "when" is asserted rather than tolerated.</summary>
/// <remarks>
/// Extracted from <c>Tempest.Core.Tests.EngineeringIntelligence.EngineeringIntelligenceFixtures</c>
/// (WP 18.0C): several namespace fixture suites each built their own
/// <c>Clock()</c> helper around this type, and the type itself belongs to
/// none of them — it is generic test infrastructure, not P02 domain
/// content, so it stays here rather than moving to <c>src/Frozen/</c> with
/// the reasoning layer.
/// </remarks>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
