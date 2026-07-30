namespace Tempest.Core.Identity;

/// <summary>
/// The concrete <see cref="ICurrentPrincipalAccessor"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Backed by a single, <see langword="lock"/>-protected mutable field,
/// not <see cref="AsyncLocal{T}"/> — a deliberate departure from
/// <c>Platform Service Contracts.md</c>'s own tentative "likely requires
/// an <see cref="AsyncLocal{T}"/>-backed implementation" language, made
/// explicitly during this Work Package's own implementation phase (which
/// that same document named as the point where this specific question
/// would be resolved, not a frozen prior decision this change overrides).
/// </para>
/// <para>
/// This release's identity model is local-only (ADR-0043): there is no
/// REST API yet (<c>WP 6.3</c>), no concurrent, per-request principal to
/// isolate, and exactly one principal is expected to be current for the
/// life of a running instance. An <see cref="AsyncLocal{T}"/>-backed
/// accessor would make that single, ambient principal invisible to any
/// caller outside the exact async call chain that established it — for
/// example, a value set during Module Initialisation would not be
/// visible to a command dispatched later from a test's or a future
/// Shell's own, separate call chain, since <see cref="AsyncLocal{T}"/>
/// flows forward to child operations, never sideways to an unrelated,
/// later caller. That behaviour is exactly right for a genuinely
/// concurrent, per-request scenario (which does not exist yet) and
/// exactly wrong for this release's own actual, simpler need: one
/// ambient principal, established once, visible to every subsequent
/// caller for the life of the process.
/// </para>
/// <para>
/// <b>Revisit trigger for <c>WP 6.3</c> (REST API):</b> once concurrent,
/// per-request principals become a real, demonstrated need, this field
/// should become <see cref="AsyncLocal{T}"/>-backed (or the REST API
/// should introduce its own request-scoped accessor) — see this Work
/// Package's own Lessons Learned and Technical Debt Assessment.
/// </para>
/// <para>
/// <see cref="SetCurrent"/> is deliberately not part of
/// <see cref="ICurrentPrincipalAccessor"/> itself — every ordinary
/// consumer resolves the read-only interface exactly as designed; only
/// <see cref="IIdentityService"/> is constructed with a direct reference
/// to this concrete type, so it alone can establish a current principal.
/// </para>
/// </remarks>
public sealed class CurrentPrincipalAccessor : ICurrentPrincipalAccessor
{
    private readonly object _gate = new();
    private IPrincipal? _current;

    /// <inheritdoc />
    public IPrincipal? Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>
    /// Establishes <paramref name="principal"/> as the current, ambient
    /// principal for the life of the running instance, or clears it if
    /// <paramref name="principal"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="principal">The principal to establish, or <see langword="null"/> to clear.</param>
    public void SetCurrent(IPrincipal? principal)
    {
        lock (_gate)
            _current = principal;
    }
}
