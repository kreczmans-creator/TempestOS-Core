using Tempest.Core.EngineeringData;
using Tempest.Core.Events;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// A single engineering calculation (`WP 9.2A`). Gains, from `WP 20.1B`
/// (`TD-181`, Product Owner decision 2026-09-15 §2), the minimal
/// "complete" flag that is a calculation task's own completion signal —
/// mirroring <c>Tempest.Core.Tasks.ManualTask.Done</c>/<c>CompletedOn</c>'s
/// own shape exactly, the Kind template `WP 19.5C` already established.
/// </summary>
public sealed class Calculation : EngineeringObjectBase, ICalculation, IRehydratable<Calculation>
{
    private bool _completed;
    private DateOnly? _completedOn;
    private DateOnly? _dueOn;

    public Calculation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        bool completed = false, DateOnly? completedOn = null, DateOnly? dueOn = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _completed = completed;
        _completedOn = completedOn;
        _dueOn = dueOn;
    }

    /// <summary>Whether <c>calculations.complete</c> has been run against this calculation (`TD-181`) — leaves it out of the Tasks read model's own Calculations bucket.</summary>
    public bool Completed => _completed;

    /// <summary>When this calculation was marked complete. <see langword="null"/> while <see cref="Completed"/> is <see langword="false"/>.</summary>
    public DateOnly? CompletedOn => _completedOn;

    /// <summary>
    /// When this calculation is due (`WP 20.10B`, T2). Set at creation for
    /// every calculation created under a project — the create prompt's own
    /// date field, default today + 14 days, offered editable, never blank
    /// (<c>Tempest.Workspace.Calculations.CalculationObjectFactoryRegistry.CreateAsync</c>);
    /// a calculation created outside any project (standalone Engineering)
    /// carries none. Editable afterwards through <see cref="SetDueOnAsync"/>,
    /// mirroring the generic editor's own row pattern for a Kind-specific
    /// field.
    /// </summary>
    public DateOnly? DueOn => _dueOn;

    /// <summary>
    /// Marks this calculation complete — public, unlike
    /// <c>Tempest.Core.Tasks.ManualTask.MarkDoneAsync</c>'s own <c>internal</c>,
    /// because Calculations registers no dedicated service layer of its
    /// own to gate it from outside <c>Tempest.Core</c>; whether that is
    /// permitted (already complete or not) is
    /// <c>Tempest.Workspace.Calculations.CompleteCalculationCommandHandler</c>'s
    /// own concern, decided before this mutator ever runs, mirroring
    /// <c>SetCalculationStatusCommandHandler</c>'s own identical shape for
    /// this Kind's five status transitions.
    /// </summary>
    public Task MarkCompletedAsync(DateOnly completedOn, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Completed)] = true.ToString() };
                WriteJson(state, nameof(CompletedOn), completedOn);
                return state;
            },
            () => { _completed = true; _completedOn = completedOn; },
            $"Marked complete on {completedOn:O}.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <summary>
    /// Sets, or clears, <see cref="DueOn"/> (`WP 20.10B`, T2) — public, for
    /// the identical reason <see cref="MarkCompletedAsync"/> is:
    /// <c>Tempest.Workspace.Calculations.SetCalculationDueDateCommandHandler</c>
    /// is the one place whether the value is acceptable is decided, before
    /// this mutator ever runs. An ordinary field change
    /// (<see cref="WorkspaceChangeType.Updated"/>, this method's own
    /// default), never a status move.
    /// </summary>
    public Task SetDueOnAsync(DateOnly? dueOn, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(DueOn), dueOn);
                return state;
            },
            () => { _dueOn = dueOn; },
            dueOn is { } d ? $"Due date set to {d:O}." : "Due date cleared.",
            cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Completed)] = _completed.ToString();
        WriteJson(state, nameof(CompletedOn), _completedOn);
        WriteJson(state, nameof(DueOn), _dueOn);
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _completed = bool.TryParse(state.Type(nameof(Completed)), out var completed) && completed;
        _completedOn = state.TypeJson<DateOnly?>(nameof(CompletedOn));
        _dueOn = state.TypeJson<DateOnly?>(nameof(DueOn));
    }

    static Calculation IRehydratable<Calculation>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            bool.TryParse(state.Type(nameof(Completed)), out var completed) && completed,
            state.TypeJson<DateOnly?>(nameof(CompletedOn)),
            state.TypeJson<DateOnly?>(nameof(DueOn)));
}

public sealed class CalculationSet : EngineeringObjectBase, ICalculationSet, IRehydratable<CalculationSet>
{
    public IReadOnlyList<Guid> MemberCalculationIds { get; }

    public CalculationSet(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? memberCalculationIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MemberCalculationIds = memberCalculationIds ?? Array.Empty<Guid>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        WriteGuidList(state, nameof(MemberCalculationIds), MemberCalculationIds);

    static CalculationSet IRehydratable<CalculationSet>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidList(nameof(MemberCalculationIds)));
}
