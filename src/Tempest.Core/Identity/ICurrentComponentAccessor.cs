namespace Tempest.Core.Identity;

/// <summary>
/// Resolves the <see cref="IPrincipal"/> representing whichever loaded
/// component's own code is currently executing.
/// </summary>
/// <remarks>
/// <para>
/// A second, independent identity axis alongside
/// <see cref="ICurrentPrincipalAccessor"/> (ADR-0111). Mirrors
/// <see cref="ICurrentPrincipalAccessor"/>'s exact shape (a single, nullable
/// <see cref="Current"/> property) but answers a genuinely different
/// question: <see cref="ICurrentPrincipalAccessor"/> answers "which
/// <b>user</b> is acting" — a single, ambient, process-wide value,
/// deliberately not call-chain-scoped, because a human user is established
/// once and expected to remain visible to unrelated later callers. This
/// interface answers "which loaded <b>component's own code</b> is currently
/// executing" — the wrong shape for that would be exactly
/// <see cref="ICurrentPrincipalAccessor"/>'s own ambient design, since this
/// value must revert the instant control returns from a plugin's code back
/// to its caller, and must nest correctly when one component's code calls
/// into another's.
/// </para>
/// <para>
/// The Host pushes a plugin's component principal onto the concrete
/// <see cref="CurrentComponentAccessor"/>'s scope stack around every point
/// it re-enters plugin-owned code: a module's own
/// <c>InitialiseAsync</c>/<c>StartAsync</c>/<c>StopAsync</c>/<c>DisposeAsync</c>
/// calls, an event subscriber invocation, and a command handler invocation
/// — popping on return, so nested cross-component calls resolve correctly
/// and control returning to first-party code is never mistakenly attributed
/// to a plugin.
/// </para>
/// </remarks>
public interface ICurrentComponentAccessor
{
    /// <summary>
    /// Gets the currently-executing component's own principal, or
    /// <see langword="null"/> if first-party code is currently executing
    /// (no component scope pushed).
    /// </summary>
    IPrincipal? Current { get; }
}
