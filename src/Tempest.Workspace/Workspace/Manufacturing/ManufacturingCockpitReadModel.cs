using Tempest.Workspace;
using Tempest.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.Workspace.Manufacturing;

/// <summary>
/// The Manufacturing discipline's own Engineering Cockpit read-model —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="EngineeringCockpit"/>'s
/// own previous Manufacturing-specific members, unmodified in behaviour.
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="EngineeringCockpit"/> (the composition root), declaring
/// only the one dependency it actually needs, never DI-registered, never
/// referencing <see cref="EngineeringCockpit"/> or any sibling
/// discipline collaborator back.
/// </summary>
/// <remarks>
/// <b>`WP 18.1A-R1`.</b> Supersedes `WP-E`'s own <see cref="CockpitReadScope"/>-backed
/// memoisation (a lazy cell, computed once per open <c>Begin()</c> pass
/// but still blocked on synchronously outside one — the exact shape
/// `TD-108`/`TD-118` found) with an eager <see cref="LoadAsync"/>: every
/// persistence-backed read this discipline needs — the live object
/// listing, one <see cref="VerificationRecordReader"/> read per
/// Inspection, and one relationships read per Supplier Operation —
/// happens there, awaited once per Cockpit render
/// (<see cref="EngineeringCockpit.PrimeAsync"/>). Every property below is
/// now a pure, in-memory read of what <see cref="LoadAsync"/> last
/// loaded — still computed once per render, never per property, but
/// without a single blocking call left in this file's own source.
/// </remarks>
internal sealed class ManufacturingCockpitReadModel
{
    private readonly EngineeringDomainContext _domainContext;
    private IReadOnlyList<IEngineeringObject> _liveObjects = [];
    private IReadOnlyList<(IEngineeringObject Inspection, VerificationRecordSnapshot? LatestRecord)> _inspectionSnapshots = [];
    private int _unfulfilledSupplierOperations;

    /// <summary>Initialises a new instance of the <see cref="ManufacturingCockpitReadModel"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this read-model queries directly.</param>
    public ManufacturingCockpitReadModel(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);

        _domainContext = domainContext;
    }

    /// <summary>Loads every live Manufacturing object, its own Inspection results, and the unfulfilled Supplier Operation count — the reads every property below is derived from.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var objects = new List<IEngineeringObject>();
        foreach (var kind in ManufacturingObjectFactoryRegistry.SupportedKinds)
        {
            var byKind = await _domainContext.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);
            objects.AddRange(byKind.Where(o => o is not IDeletable { IsDeleted: true }));
        }

        _liveObjects = objects;

        var inspections = objects.Where(o => string.Equals(o.Kind, "Inspection", StringComparison.Ordinal)).ToList();
        var inspectionSnapshots = new List<(IEngineeringObject Inspection, VerificationRecordSnapshot? LatestRecord)>(inspections.Count);
        foreach (var inspection in inspections)
        {
            var latest = await VerificationRecordReader.GetLatestAsync(_domainContext, inspection.Id, cancellationToken).ConfigureAwait(false);
            inspectionSnapshots.Add((inspection, latest));
        }

        _inspectionSnapshots = inspectionSnapshots;

        var supplierOperations = objects
            .Where(o => string.Equals(o.Kind, "ManufacturingOperation", StringComparison.Ordinal)
                && o is IHasMetadata { Classification: ManufacturingObjectFactoryRegistry.SupplierOperation })
            .ToList();

        var unfulfilled = 0;
        foreach (var operation in supplierOperations)
        {
            var outgoing = await _domainContext.RelationshipRepository.GetOutgoingAsync(operation.Id, cancellationToken).ConfigureAwait(false);
            if (!outgoing.Any(r => string.Equals(r.RelationshipKind, "manufacturedBy", StringComparison.Ordinal)))
                unfulfilled++;
        }

        _unfulfilledSupplierOperations = unfulfilled;
    }

    /// <summary>Gets every live (non-deleted) Manufacturing object across all three Manufacturing Kinds (<c>ManufacturingOperation</c>/<c>WorkInstruction</c>/<c>Inspection</c>) — loaded by <see cref="LoadAsync"/>.</summary>
    public IReadOnlyList<IEngineeringObject> LiveManufacturingObjects => _liveObjects;

    /// <summary>Gets the number of live Manufacturing objects — the Cockpit's own cross-discipline KPI summary reads this directly.</summary>
    public int Count => LiveManufacturingObjects.Count;

    /// <summary>Gets every live <c>"ManufacturingOperation"</c>-Kind object with <see cref="EngineeringObjectMetadata.Classification"/> <see cref="ManufacturingObjectFactoryRegistry.Operation"/> — a Routing's own real sequenced steps, or a standalone Operation, distinct from a Routing container or a Supplier Operation.</summary>
    private IReadOnlyList<IEngineeringObject> LiveManufacturingOperationSteps =>
        LiveManufacturingObjects
            .Where(o => string.Equals(o.Kind, "ManufacturingOperation", StringComparison.Ordinal)
                && o is IHasMetadata { Classification: ManufacturingObjectFactoryRegistry.Operation })
            .ToList();

    /// <summary>Gets every live <c>"ManufacturingOperation"</c>-Kind object with <see cref="EngineeringObjectMetadata.Classification"/> <see cref="ManufacturingObjectFactoryRegistry.SupplierOperation"/>.</summary>
    private IReadOnlyList<IEngineeringObject> LiveSupplierOperations =>
        LiveManufacturingObjects
            .Where(o => string.Equals(o.Kind, "ManufacturingOperation", StringComparison.Ordinal)
                && o is IHasMetadata { Classification: ManufacturingObjectFactoryRegistry.SupplierOperation })
            .ToList();

    /// <summary>Gets every live <c>"Inspection"</c>-Kind object paired with its own most recent recorded <see cref="VerificationRecordSnapshot"/> — read via <see cref="VerificationRecordReader"/>, the identical generic, type-erased record read the Verification discipline's own read-model already uses, never a new traversal. <see langword="null"/> for an Inspection never recorded against.</summary>
    private IReadOnlyList<(IEngineeringObject Inspection, VerificationRecordSnapshot? LatestRecord)> LiveInspectionSnapshots =>
        _inspectionSnapshots;

    /// <summary>Gets the number of live Manufacturing objects with <see cref="LifecycleState.Released"/> status — the Cockpit's own "Released Items" signal.</summary>
    private int ReleasedManufacturingCount =>
        LiveManufacturingObjects.Count(o => o is IHasLifecycle { Status: LifecycleState.Released });

    /// <summary>Gets the number of live Operation steps (<see cref="LiveManufacturingOperationSteps"/>) not yet <see cref="LifecycleState.Released"/>, <see cref="LifecycleState.Archived"/>, or <see cref="LifecycleState.Cancelled"/> — the Cockpit's own "Open Operations" signal.</summary>
    private int OpenOperationsCount =>
        LiveManufacturingOperationSteps.Count(o => o is IHasLifecycle { Status: not (LifecycleState.Released or LifecycleState.Archived or LifecycleState.Cancelled) });

    /// <summary>Gets the number of live Supplier Operations (<see cref="LiveSupplierOperations"/>) with no outgoing <c>"manufacturedBy"</c> relationship to a real Supplier recorded yet — the Cockpit's own "unfulfilled Supplier Operation" signal.</summary>
    private int UnfulfilledSupplierOperationCount => _unfulfilledSupplierOperations;

    /// <summary>Gets the number of live Inspections whose own most recent recorded result has <see cref="VerificationOutcome.Fail"/> — the Cockpit's own "Failed Inspection" signal.</summary>
    private int FailedInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Fail);

    /// <summary>Gets the number of live Inspections whose own most recent recorded result has <see cref="VerificationOutcome.Pass"/>.</summary>
    private int PassedInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Pass);

    /// <summary>Gets the number of live Inspections whose own most recent recorded result has <see cref="VerificationOutcome.Conditional"/>.</summary>
    private int ConditionalInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Conditional);

    /// <summary>Gets the number of live Inspections with no recorded result yet — the Cockpit's own "Pending" Inspection signal.</summary>
    private int PendingInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord is null);

    /// <summary>Gets the number of outstanding Manufacturing items awaiting action — <see cref="OpenOperationsCount"/> plus <see cref="UnfulfilledSupplierOperationCount"/> plus <see cref="FailedInspectionCount"/>.</summary>
    public int OutstandingActions => OpenOperationsCount + UnfulfilledSupplierOperationCount + FailedInspectionCount;

    /// <summary>
    /// Gets the Manufacturing discipline's own status:
    /// <see cref="EngineeringHealthStatus.Unknown"/> if no live
    /// Manufacturing object exists yet; <see cref="EngineeringHealthStatus.Blocked"/>
    /// if any live Inspection's own most recent recorded result has
    /// <see cref="VerificationOutcome.Fail"/>; <see cref="EngineeringHealthStatus.Attention"/>
    /// if any Operation step is still open or any Supplier Operation has
    /// no <c>"manufacturedBy"</c> link yet, with no Fail present;
    /// <see cref="EngineeringHealthStatus.Healthy"/> otherwise.
    /// </summary>
    public EngineeringHealthStatus Status
    {
        get
        {
            if (LiveManufacturingObjects.Count == 0)
                return EngineeringHealthStatus.Unknown;

            if (FailedInspectionCount > 0)
                return EngineeringHealthStatus.Blocked;

            return OutstandingActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Manufacturing discipline's own dedicated seven-card KPI
    /// set: Manufacturing Objects, Manufacturing Readiness, Released
    /// Items, Open Operations, Supplier Status, Inspection Status,
    /// Production Health.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            static string FormatShare(int numerator, int denominator, string emptyLabel) =>
                denominator == 0 ? emptyLabel : $"{numerator * 100 / denominator}% ({numerator}/{denominator})";

            var total = LiveManufacturingObjects.Count;
            var steps = LiveManufacturingOperationSteps;
            var readySteps = steps.Count(o => o is IHasLifecycle { Status: LifecycleState.Released });
            var supplierOperations = LiveSupplierOperations;
            var fulfilledSupplierOperations = supplierOperations.Count - UnfulfilledSupplierOperationCount;
            var inspections = LiveInspectionSnapshots;

            var inspectionStatusDisplay = inspections.Count == 0
                ? "— (no Inspections yet)"
                : $"{PassedInspectionCount} Passed / {FailedInspectionCount} Failed / {ConditionalInspectionCount} Conditional / {PendingInspectionCount} Pending";

            return
            [
                new("Manufacturing Objects", total.ToString(), IsPlaceholder: false),
                new("Manufacturing Readiness", FormatShare(readySteps, steps.Count, "— (no Operations yet)"), IsPlaceholder: false),
                new("Released Items", ReleasedManufacturingCount.ToString(), IsPlaceholder: false),
                new("Open Operations", OpenOperationsCount.ToString(), IsPlaceholder: false),
                new("Supplier Status", FormatShare(fulfilledSupplierOperations, supplierOperations.Count, "— (no Supplier Operations yet)"), IsPlaceholder: false),
                new("Inspection Status", inspectionStatusDisplay, IsPlaceholder: false),
                new("Production Health", Status.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>Gets this discipline's own "What Needs Attention" contribution — a base entry, plus a conditional second entry when <see cref="OutstandingActions"/> is non-zero.</summary>
    public IReadOnlyList<CockpitAttentionItem> GetAttentionItems()
    {
        var items = new List<CockpitAttentionItem>
        {
            LiveManufacturingObjects.Count > 0
                ? new("Manufacturing is live", $"{LiveManufacturingObjects.Count} Manufacturing object(s) registered - the Project Explorer's own Manufacturing area and the Engineering Cockpit's own Manufacturing KPIs reflect real Engineering Domain data (WP 9.5A).")
                : new("No Manufacturing objects registered yet", "The Manufacturing area has no live Manufacturing object yet - this is expected, not a defect."),
        };

        if (OutstandingActions > 0)
        {
            items.Add(new(
                "Manufacturing needs attention",
                $"{OutstandingActions} Manufacturing item(s) awaiting action (open Operations, unfulfilled Supplier Operations, or a Failed Inspection) across {LiveManufacturingObjects.Count} live object(s). See the Manufacturing area's own Property Inspector for detail."));
        }

        return items;
    }

    /// <summary>Gets this discipline's own "Open Actions" triage entry, or <see langword="null"/> if nothing is currently outstanding.</summary>
    public CockpitActionItem? GetOpenActionItem() =>
        OutstandingActions > 0
            ? new($"Triage {OutstandingActions} outstanding Manufacturing item(s) (open Operations, unfulfilled Supplier Operations, or Failed Inspections)", "Manufacturing Engineer")
            : null;

    /// <summary>Gets this discipline's own "Blocked Items" contribution — one message per live Inspection with a Fail outcome.</summary>
    public IReadOnlyList<string> GetBlockedMessages() =>
        LiveInspectionSnapshots
            .Where(s => s.LatestRecord?.Outcome == VerificationOutcome.Fail)
            .Select(s => $"Inspection '{((IHasBusinessIdentifier)s.Inspection).DisplayName}' recorded a Fail outcome.")
            .ToList();
}
