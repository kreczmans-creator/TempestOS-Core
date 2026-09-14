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

    public Calculation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        bool completed = false, DateOnly? completedOn = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _completed = completed;
        _completedOn = completedOn;
    }

    /// <summary>Whether <c>calculations.complete</c> has been run against this calculation (`TD-181`) — leaves it out of the Tasks read model's own Calculations bucket.</summary>
    public bool Completed => _completed;

    /// <summary>When this calculation was marked complete. <see langword="null"/> while <see cref="Completed"/> is <see langword="false"/>.</summary>
    public DateOnly? CompletedOn => _completedOn;

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

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Completed)] = _completed.ToString();
        WriteJson(state, nameof(CompletedOn), _completedOn);
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _completed = bool.TryParse(state.Type(nameof(Completed)), out var completed) && completed;
        _completedOn = state.TypeJson<DateOnly?>(nameof(CompletedOn));
    }

    static Calculation IRehydratable<Calculation>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            bool.TryParse(state.Type(nameof(Completed)), out var completed) && completed,
            state.TypeJson<DateOnly?>(nameof(CompletedOn)));
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
