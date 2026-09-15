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
/// <b>`WP 21.6A`, OSA-12/OSA-14.</b> <see cref="SetCurrent"/> was
/// deliberately not part of <see cref="ICurrentPrincipalAccessor"/> itself
/// since `WP 17.2A`, but stayed <see langword="public"/> on this concrete
/// type — and this type was registered in the DI container under its own
/// concrete key (`ADR-0044`, <c>TempestHost.cs</c>), so <em>any</em>
/// component that resolved it by concrete type, not only <c>WorkspaceHost</c>,
/// could call it (demonstrated in practice: <c>Tempest.Samples.SamplePrincipalFactory</c>
/// did exactly that, with an arbitrary caller-supplied identity string and
/// no credential check — `WP 21.5F`'s own OSA-12 finding). <see cref="SetCurrent"/>
/// is now <see langword="internal"/>: nothing outside this assembly can
/// call it directly, even holding a concrete reference obtained by an
/// <see langword="is"/> check against the interface — only <see cref="PrincipalSession"/>,
/// declared beside this type, can (it is the "single internal seam" the
/// two legitimate callers — <c>WorkspaceHost</c>'s start-up and its own
/// "Switch person…" confirmation, and <c>Tempest.Harness</c>'s own
/// start-up — reach through, and the only place <see cref="SetCurrent"/>
/// is ever called from outside this file). See <see cref="PrincipalSession"/>'s
/// own remarks for how a first-party module (`Tempest.Samples`, never
/// shipped) still legitimately demonstrates establishing a principal
/// without regaining the old, unrestricted reach.
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
    internal void SetCurrent(IPrincipal? principal)
    {
        lock (_gate)
            _current = principal;
    }
}

/// <summary>
/// The single seam through which <see cref="CurrentPrincipalAccessor.SetCurrent"/>
/// is reachable from outside <c>Tempest.Core</c> (`WP 21.6A`, OSA-12/OSA-14).
/// </summary>
/// <remarks>
/// <para>
/// <b>Construction is as restricted as the capability it carries.</b> The
/// constructor is <see langword="internal"/> — only <c>TempestHost</c>,
/// which already constructs the one <see cref="CurrentPrincipalAccessor"/>
/// instance a running host ever has, can build a session wrapping it.
/// Nothing else can mint a second one around an accessor it obtained some
/// other way (an <see langword="is"/> check against a resolved
/// <see cref="ICurrentPrincipalAccessor"/>, for instance) — the
/// <em>instance</em>, not merely the method, is what a caller must already
/// legitimately hold.
/// </para>
/// <para>
/// <b>Reached two different ways, deliberately.</b> <c>Tempest.Desktop</c>'s
/// <c>WorkspaceHost</c> and <c>Tempest.Harness</c>'s own start-up — the
/// shipped product's two legitimate callers — resolve this type from the
/// running host's own DI container (registered under its own concrete
/// type, exactly as <see cref="Establish"/>'s public method is the only
/// capability that registration now exposes — never the accessor's own
/// full read/write surface, the shape `TempestHost.cs`'s own remarks
/// disclosed as reachable by "any in-process component" before this Work
/// Package). <c>Tempest.Samples</c> (never shipped — `WP 21.5F`'s own
/// audit confirmed this directly) constructor-injects it the identical
/// way for its own demonstration modules, each establishing its own named
/// sample identity during its own initialisation — the same pattern
/// `SamplePrincipalFactory` always used, now narrowed to <see cref="Establish"/>
/// alone rather than the accessor's own <c>Current</c> getter and every
/// other capability a bare <see cref="CurrentPrincipalAccessor"/> reference
/// carried.
/// </para>
/// </remarks>
public sealed class PrincipalSession
{
    private readonly CurrentPrincipalAccessor _accessor;

    internal PrincipalSession(CurrentPrincipalAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _accessor = accessor;
    }

    /// <summary>
    /// Establishes <paramref name="principal"/> as this session's own
    /// principal from this moment on, or clears it if
    /// <paramref name="principal"/> is <see langword="null"/> — published
    /// unconditionally, exactly as <c>WorkspaceHost.StartAsync</c>'s own
    /// remarks require: a session that genuinely has no principal must
    /// report none, not inherit whatever a prior caller happened to
    /// establish.
    /// </summary>
    /// <param name="principal">The principal to establish, or <see langword="null"/> to clear.</param>
    public void Establish(IPrincipal? principal) => _accessor.SetCurrent(principal);
}
