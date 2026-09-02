namespace Tempest.App.Workspace;

/// <summary>
/// The Engineering Cockpit's own per-refresh read scope (`WP-E`) — while
/// one is open, every <see cref="CockpitReadCell{T}"/> registered against
/// it performs its underlying read exactly once, and every property
/// derived from that read consumes the same snapshot.
/// </summary>
/// <remarks>
/// <para>
/// <b>The problem this exists for.</b> Every discipline read-model
/// exposes its data as expression-bodied properties over a live read —
/// <c>LiveRequirements</c>, <c>LiveVerificationSnapshots</c> and their
/// siblings. Nothing cached, so each property re-read from scratch, and
/// the composite properties above them (a <c>Status</c>, a KPI card set,
/// an attention item) each re-read every leaf they touch. One
/// <c>CockpitView.Refresh()</c> therefore evaluated the same
/// persistence-backed read eight or more times, and
/// <c>RequirementValidationService.ValidateAsync</c> — itself
/// <c>O(N)</c> in stored requirements — once per requirement inside each
/// of those evaluations. That is the <c>O(N²)</c> `WP-E` found, performed
/// synchronously on the UI thread.
/// </para>
/// <para>
/// <b>Why a scope and not a plain cache.</b> A cache that outlived the
/// render pass would change what the Cockpit means: these properties are
/// live reads, and every existing caller — the acceptance tests
/// included — relies on reading one immediately after mutating the
/// workspace. So the memoisation is bounded by an explicit scope.
/// Outside a scope every cell reads through, exactly as before; inside
/// one, the first read wins and the rest of the pass sees a consistent
/// snapshot of it. Entering and leaving both invalidate, so nothing
/// survives a pass.
/// </para>
/// <para>
/// <b>Internal consistency is the second thing this buys.</b> Before
/// this, a KPI card set could report a total taken from one read and a
/// coverage percentage taken from a later one — genuinely different
/// numbers if the workspace changed between them. Within a scope they
/// cannot disagree.
/// </para>
/// <para>
/// Re-entrant by depth count, so a nested <c>Begin</c> joins the
/// outermost scope rather than starting a second one and discarding what
/// the caller above already read.
/// </para>
/// </remarks>
internal sealed class CockpitReadScope
{
    private readonly List<Action> _invalidators = [];
    private int _depth;

    /// <summary>Gets whether a refresh pass is currently open — <see langword="false"/> means every cell reads through, live.</summary>
    public bool IsActive => _depth > 0;

    /// <summary>
    /// Opens a refresh pass. Dispose the returned handle to close it.
    /// Nested calls join the open pass; only the outermost one bounds it.
    /// </summary>
    public IDisposable Begin()
    {
        // No invalidation here: closing a pass already clears every cell,
        // and a handle is always disposed (the callers use `using`), so
        // invalidating on the way in as well would be a second guard on
        // the same condition — one that no behaviour could ever
        // distinguish, and so one that nothing could ever test.
        _depth++;
        return new Handle(this);
    }

    /// <summary>Creates a cell whose <paramref name="read"/> runs once per open pass, and every time outside one.</summary>
    public CockpitReadCell<T> Cell<T>(Func<T> read) => new(this, read);

    internal void Register(Action invalidate) => _invalidators.Add(invalidate);

    private void Invalidate()
    {
        foreach (var invalidate in _invalidators)
            invalidate();
    }

    private void End()
    {
        if (_depth == 0)
            return;

        _depth--;

        // Nothing is retained past the pass that read it. This is the one
        // invalidation point, and it is load-bearing twice over: it frees
        // the snapshot (which holds every live requirement and every
        // validation result for them), and it is what makes the *next*
        // pass a fresh read rather than a replay of this one.
        if (_depth == 0)
            Invalidate();
    }

    private sealed class Handle(CockpitReadScope scope) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            scope.End();
        }
    }
}

/// <summary>
/// One memoised read belonging to a <see cref="CockpitReadScope"/> — live
/// outside an open pass, computed exactly once inside one.
/// </summary>
/// <typeparam name="T">The read's own result type.</typeparam>
internal sealed class CockpitReadCell<T>
{
    private readonly CockpitReadScope _scope;
    private readonly Func<T> _read;
    private T _value = default!;
    private bool _hasValue;

    internal CockpitReadCell(CockpitReadScope scope, Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(read);

        _scope = scope;
        _read = read;
        scope.Register(() =>
        {
            _value = default!;
            _hasValue = false;
        });
    }

    /// <summary>Gets this read's own result — from the open pass's snapshot if there is one, otherwise read live.</summary>
    public T Value
    {
        get
        {
            if (!_scope.IsActive)
                return _read();

            if (!_hasValue)
            {
                _value = _read();
                _hasValue = true;
            }

            return _value;
        }
    }
}
