using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that tracks its own lifecycle
/// timestamps and running state in memory.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 4.4</c> through <c>WP 4.7</c> extend
/// and validate against — see <c>Sample Module Architecture.md</c>. Written
/// exactly as <em>Building a Module</em> (Academy) documents: a public,
/// zero-argument constructor calling <see cref="ModuleLifecycleBase"/>'s
/// own constructor with literal values, since a normally-discovered module
/// cannot receive any constructor-injected dependency (Discovery's own
/// metadata probe requires a public parameterless constructor — see
/// <c>Sample Module Architecture.md</c>'s Repository Investigation for the
/// full reasoning).
/// </para>
/// <para>
/// Consumes no platform service — not by choice, but because none is
/// currently reachable from a discovered module at all. Each lifecycle
/// method records real, observable state; none is an empty override.
/// </para>
/// </remarks>
public sealed class ClockModule : ModuleLifecycleBase
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ClockModule"/> class.
    /// </summary>
    public ClockModule()
        : base("tempest.samples.clock", "System Clock", "1.0.0")
    {
    }

    /// <summary>
    /// Gets the moment <see cref="InitialiseAsync"/> completed, or
    /// <see langword="null"/> if it has not run yet.
    /// </summary>
    public DateTimeOffset? InitialisedAt { get; private set; }

    /// <summary>
    /// Gets the moment <see cref="StartAsync"/> completed, or
    /// <see langword="null"/> if it has not run yet.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// Gets the moment <see cref="StopAsync"/> completed, or
    /// <see langword="null"/> if it has not run yet.
    /// </summary>
    public DateTimeOffset? StoppedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the clock is currently running —
    /// <see langword="true"/> from the moment <see cref="StartAsync"/>
    /// completes until <see cref="StopAsync"/> completes.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets how long the clock has been running, computed from
    /// <see cref="StartedAt"/>, or <see langword="null"/> if it is not
    /// currently running.
    /// </summary>
    public TimeSpan? Uptime => IsRunning && StartedAt is { } started
        ? DateTimeOffset.UtcNow - started
        : null;

    /// <inheritdoc />
    /// <remarks>Records <see cref="InitialisedAt"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        InitialisedAt = DateTimeOffset.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Records <see cref="StartedAt"/> and sets <see cref="IsRunning"/>.</remarks>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        StartedAt = DateTimeOffset.UtcNow;
        IsRunning = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Records <see cref="StoppedAt"/> and clears <see cref="IsRunning"/>.</remarks>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        StoppedAt = DateTimeOffset.UtcNow;
        IsRunning = false;

        return Task.CompletedTask;
    }
}
