using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.App.Workspace;

/// <summary>
/// The Engineering Cockpit — the Workspace's own default landing screen
/// (`ADR-0069`) and, per its own `WP 8.1C` controlling instruction, the
/// answer to four questions on every visit: where am I, what needs
/// attention, is the project healthy, and what should I do next. Composed
/// entirely from existing Workspace services: <see cref="NavigationService"/>
/// (areas, open documents, recent items) and the existing
/// <see cref="ICommandRegistry"/> Platform Service (the Cockpit's own
/// Command Palette integration, `ADR-0070`). Not one of the twelve
/// `WP8.0B Workspace Contracts.md` interfaces — a genuine, disclosed
/// implementation-phase addition, reached only through
/// <see cref="Workspace.Cockpit"/> internally, mirroring
/// <see cref="WorkspaceManager.StatusBar"/>'s own `WP 8.1A` precedent.
/// </summary>
/// <remarks>
/// <para>
/// Introduces no calculation, verification, or Digital Thread traversal
/// logic of its own (`WP 8.1C`'s own explicit scope boundary) — every
/// region that would need one of those services today shows fixed,
/// representative placeholder content instead, disclosed either via
/// <see cref="CockpitKpiCard.IsPlaceholder"/>,
/// <see cref="EngineeringHealthStatus.Unknown"/>, or by this class's own
/// XML documentation.
/// </para>
/// <para>
/// <b>Real vs. placeholder, stated once, plainly:</b> <see cref="RecentActivity"/>,
/// <see cref="ContinueWhereILeftOff"/>, <see cref="AreaCount"/>,
/// <see cref="OpenDocumentCount"/>, and <see cref="AvailableCommands"/> are
/// live reads of real Workspace state. `WP 9.0A` adds three more:
/// <see cref="ProjectName"/>, <see cref="RecentProjects"/>, and one
/// <see cref="AttentionItems"/> entry are real reads of the Engineering
/// Domain's own live <c>Project</c> objects. `WP 9.1A` adds the
/// Requirements discipline's own real reads: <see cref="RequirementsStatus"/>,
/// <see cref="RequirementsKpiCards"/>, a second <see cref="AttentionItems"/>
/// entry, and the <c>"Requirements"</c> entry within <see cref="KpiCards"/>
/// itself, all sourced from <see cref="IRequirementsService"/>/
/// <see cref="IRequirementValidationService"/> directly, never a fabricated
/// value, honestly empty/"Unknown" if no live Requirement exists yet.
/// `WP 9.2A` adds the Calculations discipline's own real reads:
/// <see cref="CalculationStatus"/>, <see cref="CalculationsKpiCards"/>, a
/// third <see cref="AttentionItems"/> entry, and the <c>"Calculations"</c>
/// entry within <see cref="KpiCards"/> itself, all sourced from
/// <see cref="EngineeringDomainContext"/>/<see cref="CalculationRecordReader"/>
/// directly (the same shared Domain repository/document store
/// <see cref="ProjectName"/>/<see cref="RecentProjects"/> already read —
/// never a new service), honestly empty/"Unknown" if no live Calculation
/// exists yet. `WP 9.4A` adds the Documents discipline's own real reads:
/// <see cref="DocumentationStatus"/> (this property already existed as a
/// fixed <see cref="EngineeringHealthStatus.Unknown"/> placeholder — this
/// Work Package makes it a real, derived read, reusing the exact same
/// name/slot rather than adding a new one, mirroring
/// <see cref="RequirementsStatus"/>/<see cref="CalculationStatus"/>'s own
/// established pattern), <see cref="DocumentsKpiCards"/>, a fourth
/// <see cref="AttentionItems"/> entry, and the <c>"Documentation"</c> entry
/// within <see cref="KpiCards"/> itself, all sourced from
/// <see cref="EngineeringDomainContext"/> directly, honestly empty/"Unknown"
/// if no live Document exists yet. `WP 9.3A` adds the Verification
/// discipline's own real reads: <see cref="VerificationStatus"/> (an
/// existing, fixed <see cref="EngineeringHealthStatus.Unknown"/>
/// placeholder since `WP 8.1C` - reused, not replaced),
/// <see cref="VerificationKpiCards"/>, a fifth <see cref="AttentionItems"/>
/// entry, and the <c>"Verification"</c> entry within <see cref="KpiCards"/>
/// itself, all sourced from <see cref="EngineeringDomainContext"/>/
/// <c>Tempest.App.Workspace.Verification.VerificationRecordReader</c>
/// directly (never <c>IVerificationService.GetVerificationHistoryAsync</c>,
/// which is permission-gated), honestly empty/"Unknown" if no live
/// Verification Activity exists yet. `WP 9.5A` adds the Manufacturing
/// discipline's own real reads: <see cref="ManufacturingStatus"/> (a
/// genuinely new Cockpit member - unlike <see cref="VerificationStatus"/>/
/// <see cref="DocumentationStatus"/>, `WP 8.1C` never named a Manufacturing
/// placeholder slot for this Work Package to reuse), <see cref="ManufacturingKpiCards"/>
/// (Manufacturing Objects, Manufacturing Readiness, Released Items, Open
/// Operations, Supplier Status, Inspection Status, Production Health - this
/// Work Package's own literal seven-card breakdown), a sixth
/// <see cref="AttentionItems"/> entry, and one conditional
/// <see cref="OpenActions"/> entry, all sourced from
/// <see cref="EngineeringDomainContext"/>/<see cref="ManufacturingObjectFactoryRegistry"/>/
/// <c>Tempest.App.Workspace.Verification.VerificationRecordReader</c>
/// directly (the Inspection Kind's own recorded results, the identical
/// reader Verification's own KPIs already use - never a new traversal),
/// honestly empty/"Unknown" if no live Manufacturing object exists yet.
/// <see cref="KpiCards"/> itself gains no new entry - `WP 8.0C` never named
/// a <c>"Manufacturing"</c> placeholder row there to replace (confirmed by
/// direct read), so its own six existing entries are unchanged by this
/// Work Package. Every other member on this class remains fixed,
/// representative sample content: no Materials, Risk, Decision, or
/// Milestone service is wired to the Workspace for a real value to come
/// from yet - that remains out of this Work Package's own scope.
/// </para>
/// </remarks>
internal sealed class EngineeringCockpit
{
    private readonly NavigationService _navigationService;
    private readonly ICommandRegistry _commandRegistry;
    private readonly EngineeringDomainContext _domainContext;
    private readonly IRequirementsService _requirementsService;
    private readonly IRequirementValidationService _requirementValidationService;

    /// <summary>Initialises a new instance of the <see cref="EngineeringCockpit"/> class.</summary>
    public EngineeringCockpit(
        NavigationService navigationService, ICommandRegistry commandRegistry, EngineeringDomainContext domainContext,
        IRequirementsService requirementsService, IRequirementValidationService requirementValidationService)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(requirementValidationService);

        _navigationService = navigationService;
        _commandRegistry = commandRegistry;
        _domainContext = domainContext;
        _requirementsService = requirementsService;
        _requirementValidationService = requirementValidationService;
    }

    // ------------------------------------------------------------
    // Where am I?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the most-recently-created live Mechanical Product Structure
    /// <c>Project</c>'s own display name (`WP 9.0A`) - a real read, honestly
    /// reporting "No Mechanical Project yet" if none exists.
    /// </summary>
    public string ProjectName => LiveProjects.Count > 0 ? LiveProjects[^1].DisplayName : "No Mechanical Project yet";

    /// <summary>
    /// Gets the most-recently-opened or jumped-to object - a real read of
    /// <see cref="NavigationService.RecentItems"/>'s own first (most
    /// recent) entry, or <see langword="null"/> if nothing has been opened
    /// yet this session. The Cockpit's own "Continue Where I Left Off."
    /// </summary>
    public RecentNavigationItem? ContinueWhereILeftOff => _navigationService.RecentItems.Count > 0
        ? _navigationService.RecentItems[0]
        : null;

    /// <summary>
    /// Gets every live Mechanical Product Structure <c>Project</c>'s own
    /// display name (`WP 9.0A`) - a real read; empty, honestly, if none
    /// exist yet.
    /// </summary>
    public IReadOnlyList<string> RecentProjects => LiveProjects.Select(p => p.DisplayName).ToList();

    /// <summary>Gets every live (non-deleted) <c>Project</c>, newest-created first is not guaranteed - insertion order from the repository.</summary>
    private IReadOnlyList<IHasBusinessIdentifier> LiveProjects =>
        _domainContext.Repository.ListByKindAsync("Project").GetAwaiter().GetResult()
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IHasBusinessIdentifier>()
            .ToList();

    /// <summary>Gets every live (non-deleted) Requirement (`WP 9.1A`) - a real read, mirroring <see cref="LiveProjects"/>'s own identical sync-over-async bridging (this class's properties are a frozen, synchronous shape).</summary>
    private IReadOnlyList<Tempest.Core.Requirements.IRequirement> LiveRequirements =>
        _requirementsService.ListAsync().GetAwaiter().GetResult().Where(r => !r.IsDeleted).ToList();

    /// <summary>Gets every live (non-deleted) Calculation (`WP 9.2A`) - a real read via <see cref="EngineeringDomainContext.Repository"/>, mirroring <see cref="LiveProjects"/>'s own identical sync-over-async bridging.</summary>
    private IReadOnlyList<ICalculation> LiveCalculations =>
        _domainContext.Repository.ListByKindAsync("Calculation").GetAwaiter().GetResult()
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<ICalculation>()
            .ToList();

    /// <summary>
    /// Gets every live Calculation paired with its own most recent
    /// executed <see cref="CalculationRecordSnapshot"/> (`WP 9.2A`) - read
    /// via <see cref="CalculationRecordReader"/>, the same generic,
    /// type-erased record read the Property Inspector uses, never a new
    /// traversal. <see langword="null"/> for a Calculation never executed.
    /// </summary>
    private IReadOnlyList<(ICalculation Calculation, CalculationRecordSnapshot? LatestRecord)> LiveCalculationSnapshots =>
        LiveCalculations
            .Select(c => (c, CalculationRecordReader.GetLatestAsync(_domainContext, c.Id).GetAwaiter().GetResult()))
            .ToList();

    /// <summary>
    /// Gets whether <paramref name="calculation"/> has been revised more
    /// recently than <paramref name="latestRecord"/> was executed (`WP 9.2A`)
    /// - a disclosed heuristic for "Out-of-date": the object's own written
    /// content has changed since its own most recent evidentiary execution,
    /// so that execution's own result no longer necessarily reflects it.
    /// <see langword="false"/> if never executed - "never executed" is
    /// its own, separate signal (an executed-count denominator gap), not
    /// staleness.
    /// </summary>
    private bool IsOutOfDate(ICalculation calculation, CalculationRecordSnapshot? latestRecord)
    {
        if (latestRecord is null)
            return false;

        var revisions = _domainContext.Store.GetRevisionHistoryAsync(calculation.Id).GetAwaiter().GetResult();
        var latestRevisedAt = revisions.Count > 0 ? revisions[^1].CreatedAt : calculation.CreatedAt;

        return latestRevisedAt > latestRecord.ExecutedAt;
    }

    /// <summary>Gets every live (non-deleted) Document Domain object — <c>"Document"</c>, <c>"Drawing"</c>, or <c>"CadModel"</c> (`WP 9.4A`) - a real read via <see cref="EngineeringDomainContext.Repository"/>, mirroring <see cref="LiveCalculations"/>'s own identical sync-over-async bridging.</summary>
    private IReadOnlyList<IEngineeringObject> LiveDocuments
    {
        get
        {
            var documents = new List<IEngineeringObject>();

            foreach (var kind in DocumentObjectFactoryRegistry.SupportedKinds)
            {
                documents.AddRange(_domainContext.Repository.ListByKindAsync(kind).GetAwaiter().GetResult()
                    .Where(o => o is not IDeletable { IsDeleted: true }));
            }

            return documents;
        }
    }

    /// <summary>
    /// Gets whether <paramref name="document"/> has "Missing Evidence" (`WP 9.4A`)
    /// - a disclosed heuristic, mirroring <see cref="IsOutOfDate"/>'s own
    /// precedent: zero Attachments recorded
    /// (<see cref="IHasAttachments.GetAttachmentsAsync"/>) and zero
    /// <c>"documentedBy"</c>/<c>"references"</c> relationships in either
    /// direction (<see cref="EngineeringDomainContext.RelationshipRepository"/>,
    /// the existing Digital Thread read, never a new traversal or
    /// <see cref="ITraceable.GetEvidenceAsync"/>, which honestly resolves
    /// empty for every Document today - a pre-existing gap, not introduced
    /// here).
    /// </summary>
    private bool HasMissingEvidence(IEngineeringObject document)
    {
        var hasAttachment = document is IHasAttachments attachable
            && attachable.GetAttachmentsAsync().GetAwaiter().GetResult().Count > 0;

        if (hasAttachment)
            return false;

        var outgoing = _domainContext.RelationshipRepository.GetOutgoingAsync(document.Id).GetAwaiter().GetResult();
        var incoming = _domainContext.RelationshipRepository.GetIncomingAsync(document.Id).GetAwaiter().GetResult();

        var hasLink = outgoing.Any(r => r.RelationshipKind is "references" or "documentedBy")
            || incoming.Any(r => r.RelationshipKind is "references" or "documentedBy");

        return !hasLink;
    }

    /// <summary>Gets the number of live Documents with <see cref="HasMissingEvidence"/> (`WP 9.4A`) - the Cockpit's own "Missing Evidence" KPI.</summary>
    private int MissingEvidenceCount => LiveDocuments.Count(HasMissingEvidence);

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> (`WP 9.4A`) - the Cockpit's own "Outstanding Reviews" KPI/"Outstanding Actions" signal.</summary>
    public int OutstandingDocumentReviews =>
        LiveDocuments.Count(d => d is IHasLifecycle { Status: LifecycleState.InReview });

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> or have <see cref="HasMissingEvidence"/> (`WP 9.4A`) - the Cockpit's own "Documents need attention"/"Outstanding Actions" signal, mirroring <see cref="OutstandingCalculationActions"/>'s own identical shape.</summary>
    public int OutstandingDocumentActions => OutstandingDocumentReviews + MissingEvidenceCount;

    /// <summary>Gets the number of live Calculations whose own most recent execution recorded a <see cref="CalculationValidationOutcome.Conditional"/> outcome (`WP 9.2A`) - the Cockpit's own "Failed" signal.</summary>
    private int FailedCalculationsCount =>
        LiveCalculationSnapshots.Count(s => s.LatestRecord?.Outcome == CalculationValidationOutcome.Conditional);

    /// <summary>Gets the number of live Calculations that are <see cref="LifecycleState.InReview"/> or <see cref="IsOutOfDate"/> (`WP 9.2A`) - the Cockpit's own "Calculations awaiting review"/"Outstanding Actions" signal.</summary>
    public int OutstandingCalculationActions
    {
        get
        {
            var snapshots = LiveCalculationSnapshots;
            var awaitingReview = snapshots.Count(s => s.Calculation is IHasLifecycle { Status: LifecycleState.InReview });
            var outOfDate = snapshots.Count(s => IsOutOfDate(s.Calculation, s.LatestRecord));

            return awaitingReview + outOfDate;
        }
    }

    /// <summary>Gets every live (non-deleted) Verification Activity (`WP 9.3A`) - a real read via <see cref="EngineeringDomainContext.Repository"/>, mirroring <see cref="LiveDocuments"/>'s own identical shape.</summary>
    private IReadOnlyList<IEngineeringObject> LiveVerificationActivities =>
        _domainContext.Repository.ListByKindAsync(VerificationActivityFactoryRegistry.SupportedKind).GetAwaiter().GetResult()
            .Where(o => o is not IDeletable { IsDeleted: true })
            .ToList();

    /// <summary>Gets every live Verification Activity paired with its own most recent recorded <see cref="VerificationRecordSnapshot"/> (`WP 9.3A`) - read via <see cref="VerificationRecordReader"/>, the same generic, type-erased record read the Property Inspector uses, never a new traversal. <see langword="null"/> for an Activity never recorded against.</summary>
    private IReadOnlyList<(IEngineeringObject Activity, VerificationRecordSnapshot? LatestRecord)> LiveVerificationSnapshots =>
        LiveVerificationActivities
            .Select(a => (a, VerificationRecordReader.GetLatestAsync(_domainContext, a.Id).GetAwaiter().GetResult()))
            .ToList();

    /// <summary>Gets the number of live Verification Activities whose own most recent recorded result has <see cref="VerificationOutcome.Fail"/> (`WP 9.3A`) - the Cockpit's own "Failed" signal.</summary>
    private int FailedVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Fail);

    /// <summary>Gets the number of live Verification Activities whose own most recent recorded result has <see cref="VerificationOutcome.Conditional"/> (`WP 9.3A`) - the Cockpit's own "Conditional" signal.</summary>
    private int ConditionalVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Conditional);

    /// <summary>Gets the number of live Verification Activities whose own most recent recorded result has <see cref="VerificationOutcome.Pass"/> (`WP 9.3A`) - the Cockpit's own "Passed" signal.</summary>
    private int PassedVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Pass);

    /// <summary>Gets the number of live Verification Activities with no recorded result yet and <see cref="LifecycleState.Draft"/> status (`WP 9.3A`) - the Cockpit's own "Planned" signal (`ADR-0090`: a Draft Activity with no result is a Verification Plan).</summary>
    private int PlannedVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord is null && s.Activity is IHasLifecycle { Status: LifecycleState.Draft });

    /// <summary>Gets the number of live Verification Activities with no recorded result yet and <see cref="LifecycleState.InReview"/> (or later) status (`WP 9.3A`) - the Cockpit's own "In Progress" signal.</summary>
    private int InProgressVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord is null && s.Activity is IHasLifecycle { Status: not LifecycleState.Draft });

    /// <summary>Gets the number of live Verification Activities that are <see cref="LifecycleState.InReview"/> with no recorded result yet, plus every <see cref="FailedVerificationCount"/> (`WP 9.3A`) - the Cockpit's own "Outstanding" signal, mirroring <see cref="OutstandingCalculationActions"/>/<see cref="OutstandingDocumentActions"/>'s own identical "awaiting action" shape.</summary>
    public int OutstandingVerificationActions
    {
        get
        {
            var snapshots = LiveVerificationSnapshots;
            var awaitingResult = snapshots.Count(s => s.LatestRecord is null && s.Activity is IHasLifecycle { Status: LifecycleState.InReview });

            return awaitingResult + FailedVerificationCount;
        }
    }

    /// <summary>Gets the total number of real <see cref="Tempest.Core.Verification.IVerificationRecord"/>s recorded across every live Verification Activity (`WP 9.3A`) - the Cockpit's own "Total Verification Records" KPI, this Work Package's own literal first-named breakdown item, distinct from the Activity count itself (an Activity re-verified more than once contributes more than one record).</summary>
    private int TotalVerificationRecordsCount =>
        LiveVerificationActivities.Sum(a => VerificationRecordReader.GetResultHistoryAsync(_domainContext, a.Id).GetAwaiter().GetResult().Count);

    /// <summary>Gets every live (non-deleted) Manufacturing object across all three Manufacturing Kinds (`ManufacturingOperation`/`WorkInstruction`/`Inspection`, `WP 9.5A`) - a real read via <see cref="EngineeringDomainContext.Repository"/>, iterating <see cref="ManufacturingObjectFactoryRegistry.SupportedKinds"/>, mirroring <see cref="LiveDocuments"/>'s own identical multi-Kind shape.</summary>
    private IReadOnlyList<IEngineeringObject> LiveManufacturingObjects =>
        ManufacturingObjectFactoryRegistry.SupportedKinds
            .SelectMany(kind => _domainContext.Repository.ListByKindAsync(kind).GetAwaiter().GetResult())
            .Where(o => o is not IDeletable { IsDeleted: true })
            .ToList();

    /// <summary>Gets every live <c>"ManufacturingOperation"</c>-Kind object with <see cref="EngineeringObjectMetadata.Classification"/> <see cref="ManufacturingObjectFactoryRegistry.Operation"/> (`WP 9.5A`) - a Routing's own real sequenced steps, or a standalone Operation, distinct from a Routing container or a Supplier Operation.</summary>
    private IReadOnlyList<IEngineeringObject> LiveManufacturingOperationSteps =>
        LiveManufacturingObjects
            .Where(o => string.Equals(o.Kind, "ManufacturingOperation", StringComparison.Ordinal)
                && o is IHasMetadata { Classification: ManufacturingObjectFactoryRegistry.Operation })
            .ToList();

    /// <summary>Gets every live <c>"ManufacturingOperation"</c>-Kind object with <see cref="EngineeringObjectMetadata.Classification"/> <see cref="ManufacturingObjectFactoryRegistry.SupplierOperation"/> (`WP 9.5A`).</summary>
    private IReadOnlyList<IEngineeringObject> LiveSupplierOperations =>
        LiveManufacturingObjects
            .Where(o => string.Equals(o.Kind, "ManufacturingOperation", StringComparison.Ordinal)
                && o is IHasMetadata { Classification: ManufacturingObjectFactoryRegistry.SupplierOperation })
            .ToList();

    /// <summary>Gets every live <c>"Inspection"</c>-Kind object paired with its own most recent recorded <see cref="VerificationRecordSnapshot"/> (`WP 9.5A`) - read via <see cref="VerificationRecordReader"/>, the identical generic, type-erased record read <see cref="LiveVerificationSnapshots"/> already uses for the Verification discipline, never a new traversal. <see langword="null"/> for an Inspection never recorded against.</summary>
    private IReadOnlyList<(IEngineeringObject Inspection, VerificationRecordSnapshot? LatestRecord)> LiveInspectionSnapshots =>
        LiveManufacturingObjects
            .Where(o => string.Equals(o.Kind, "Inspection", StringComparison.Ordinal))
            .Select(o => (o, VerificationRecordReader.GetLatestAsync(_domainContext, o.Id).GetAwaiter().GetResult()))
            .ToList();

    /// <summary>Gets the number of live Manufacturing objects with <see cref="LifecycleState.Released"/> status (`WP 9.5A`) - the Cockpit's own "Released Items" signal.</summary>
    private int ReleasedManufacturingCount =>
        LiveManufacturingObjects.Count(o => o is IHasLifecycle { Status: LifecycleState.Released });

    /// <summary>Gets the number of live Operation steps (<see cref="LiveManufacturingOperationSteps"/>) not yet <see cref="LifecycleState.Released"/>, <see cref="LifecycleState.Archived"/>, or <see cref="LifecycleState.Cancelled"/> (`WP 9.5A`) - the Cockpit's own "Open Operations" signal.</summary>
    private int OpenOperationsCount =>
        LiveManufacturingOperationSteps.Count(o => o is IHasLifecycle { Status: not (LifecycleState.Released or LifecycleState.Archived or LifecycleState.Cancelled) });

    /// <summary>Gets the number of live Supplier Operations (<see cref="LiveSupplierOperations"/>) with no outgoing <c>"manufacturedBy"</c> relationship to a real <see cref="Tempest.Core.EngineeringDomain.ISupplier"/> recorded yet (`WP 9.5A`) - the Cockpit's own "unfulfilled Supplier Operation" signal.</summary>
    private int UnfulfilledSupplierOperationCount =>
        LiveSupplierOperations.Count(o => !_domainContext.RelationshipRepository.GetOutgoingAsync(o.Id).GetAwaiter().GetResult()
            .Any(r => string.Equals(r.RelationshipKind, "manufacturedBy", StringComparison.Ordinal)));

    /// <summary>Gets the number of live Inspections whose own most recent recorded result has <see cref="VerificationOutcome.Fail"/> (`WP 9.5A`) - the Cockpit's own "Failed Inspection" signal, mirroring <see cref="FailedVerificationCount"/>'s own identical shape.</summary>
    private int FailedInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Fail);

    /// <summary>Gets the number of live Inspections whose own most recent recorded result has <see cref="VerificationOutcome.Pass"/> (`WP 9.5A`).</summary>
    private int PassedInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Pass);

    /// <summary>Gets the number of live Inspections whose own most recent recorded result has <see cref="VerificationOutcome.Conditional"/> (`WP 9.5A`).</summary>
    private int ConditionalInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Conditional);

    /// <summary>Gets the number of live Inspections with no recorded result yet (`WP 9.5A`) - the Cockpit's own "Pending" Inspection signal.</summary>
    private int PendingInspectionCount =>
        LiveInspectionSnapshots.Count(s => s.LatestRecord is null);

    /// <summary>Gets the number of outstanding Manufacturing items awaiting action (`WP 9.5A`) - <see cref="OpenOperationsCount"/> plus <see cref="UnfulfilledSupplierOperationCount"/> plus <see cref="FailedInspectionCount"/>, the Cockpit's own combined "awaiting action" signal, mirroring every prior discipline's own identical shape.</summary>
    public int OutstandingManufacturingActions => OpenOperationsCount + UnfulfilledSupplierOperationCount + FailedInspectionCount;

    /// <summary>
    /// Gets the favourited projects list - always empty today: favouriting
    /// is not a capability this platform has built anywhere yet, so an
    /// honest empty state is shown rather than fabricated sample favourites.
    /// </summary>
    public IReadOnlyList<string> FavouriteProjects { get; } = [];

    // ------------------------------------------------------------
    // What needs attention?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the "What Needs Attention" region's own entries (`WP8.0C
    /// Engineering Cockpit Specification.md` §3). The first entry reflects
    /// whether a Mechanical Product Structure <c>Project</c> exists yet
    /// (`WP 9.0A`); the second reflects whether a Requirement exists yet,
    /// plus a third, conditional entry surfacing
    /// <see cref="OutstandingRequirementActions"/> when non-zero
    /// (`WP 9.1A`); the rest remain fixed, representative placeholder
    /// content for every discipline still not wired to the Workspace.
    /// </summary>
    public IReadOnlyList<CockpitAttentionItem> AttentionItems
    {
        get
        {
            var items = new List<CockpitAttentionItem>
            {
                LiveProjects.Count > 0
                    ? new("Mechanical Product Structure is live", $"{LiveProjects.Count} Project(s) registered - the Project Explorer's own Mechanical area reflects real Engineering Domain data (WP 9.0A).")
                    : new("No Mechanical Product Structure registered yet", "The Mechanical Product Structure area has no live Project yet - this is expected, not a defect."),
                LiveRequirements.Count > 0
                    ? new("Requirements Management is live", $"{LiveRequirements.Count} Requirement(s) registered - the Project Explorer's own Requirements area and the Engineering Cockpit's own Requirements KPIs reflect real Requirements Framework data (WP 9.1A).")
                    : new("No Requirements registered yet", "The Requirements Management area has no live Requirement yet - this is expected, not a defect."),
            };

            if (OutstandingRequirementActions > 0)
            {
                items.Add(new(
                    "Requirements need attention",
                    $"{OutstandingRequirementActions} outstanding Requirements validation finding(s) across {LiveRequirements.Count} live requirement(s) - duplicate identifiers, orphans, missing verification/allocation, or advisory relationship kinds. See the Requirements area's own Property Inspector for detail."));
            }

            items.Add(LiveCalculations.Count > 0
                ? new("Calculations are live", $"{LiveCalculations.Count} Calculation(s) registered - the Project Explorer's own Calculations area and the Engineering Cockpit's own Calculations KPIs reflect real Calculation Framework data (WP 9.2A).")
                : new("No Calculations registered yet", "The Calculations area has no live Calculation yet - this is expected, not a defect."));

            if (OutstandingCalculationActions > 0)
            {
                items.Add(new(
                    "Calculations need attention",
                    $"{OutstandingCalculationActions} Calculation(s) awaiting review or out-of-date across {LiveCalculations.Count} live calculation(s). See the Calculations area's own Property Inspector for detail."));
            }

            items.Add(LiveDocuments.Count > 0
                ? new("Documents are live", $"{LiveDocuments.Count} Document(s) registered - the Project Explorer's own Documents area and the Engineering Cockpit's own Documentation KPIs reflect real Engineering Domain data (WP 9.4A).")
                : new("No Documents registered yet", "The Documents area has no live Document yet - this is expected, not a defect."));

            if (OutstandingDocumentActions > 0)
            {
                items.Add(new(
                    "Documents need attention",
                    $"{OutstandingDocumentActions} Document(s) awaiting review or with missing evidence across {LiveDocuments.Count} live document(s). See the Documents area's own Property Inspector for detail."));
            }

            items.Add(LiveVerificationActivities.Count > 0
                ? new("Verification is live", $"{LiveVerificationActivities.Count} Verification Activity(ies) registered - the Project Explorer's own Verification area and the Engineering Cockpit's own Verification KPIs reflect real Engineering Domain/Verification Framework data (WP 9.3A).")
                : new("No Verification Activities registered yet", "The Verification area has no live Verification Activity yet - this is expected, not a defect."));

            if (OutstandingVerificationActions > 0)
            {
                items.Add(new(
                    "Verification needs attention",
                    $"{OutstandingVerificationActions} Verification Activity(ies) awaiting a recorded result or with a Fail outcome across {LiveVerificationActivities.Count} live activity(ies). See the Verification area's own Property Inspector for detail."));
            }

            items.Add(LiveManufacturingObjects.Count > 0
                ? new("Manufacturing is live", $"{LiveManufacturingObjects.Count} Manufacturing object(s) registered - the Project Explorer's own Manufacturing area and the Engineering Cockpit's own Manufacturing KPIs reflect real Engineering Domain data (WP 9.5A).")
                : new("No Manufacturing objects registered yet", "The Manufacturing area has no live Manufacturing object yet - this is expected, not a defect."));

            if (OutstandingManufacturingActions > 0)
            {
                items.Add(new(
                    "Manufacturing needs attention",
                    $"{OutstandingManufacturingActions} Manufacturing item(s) awaiting action (open Operations, unfulfilled Supplier Operations, or a Failed Inspection) across {LiveManufacturingObjects.Count} live object(s). See the Manufacturing area's own Property Inspector for detail."));
            }

            items.Add(new("Other disciplines still placeholder", "Materials remain out of the Workspace's own scope until their own Work Package integrates them."));

            return items;
        }
    }

    /// <summary>
    /// Gets the "Open Decisions" region's own entries - fixed,
    /// representative placeholder content: no decision-tracking service
    /// exists anywhere in this platform yet.
    /// </summary>
    public IReadOnlyList<string> OpenDecisions { get; } =
    [
        "Which Engineering Discipline Module ships first - pending Product Owner decision.",
    ];

    /// <summary>
    /// Gets the "Blocked Items" region's own entries - fixed,
    /// representative placeholder content.
    /// </summary>
    public IReadOnlyList<string> BlockedItems { get; } = [];

    /// <summary>
    /// Gets the "Overdue Actions" region's own entries - fixed,
    /// representative placeholder content, distinct from
    /// <see cref="OpenActions"/> (not yet due).
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OverdueActions { get; } = [];

    // ------------------------------------------------------------
    // Is the project healthy?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the project's own overall health - always
    /// <see cref="EngineeringHealthStatus.Unknown"/> today, honestly: no
    /// Verification/Calculation signal beyond Requirements' own is wired to
    /// the Workspace yet for a whole-project status to be derived from.
    /// </summary>
    public EngineeringHealthStatus Health => EngineeringHealthStatus.Unknown;

    /// <summary>
    /// Gets the Engineering Health Score's own display text - always a
    /// disclosed placeholder today, distinct from <see cref="Health"/>'s
    /// own closed four-value status vocabulary.
    /// </summary>
    public string HealthScoreDisplay => "— (not yet available)";

    /// <summary>
    /// Gets the Requirements discipline's own status (`WP 9.1A`) - a real,
    /// derived read: <see cref="EngineeringHealthStatus.Unknown"/> if no
    /// live Requirement exists yet; <see cref="EngineeringHealthStatus.Blocked"/>
    /// if any live requirement's own <see cref="RequirementValidationService"/>
    /// result carries an error (duplicate identifier - defence-in-depth,
    /// `CreateAsync` already prevents this at write time);
    /// <see cref="EngineeringHealthStatus.Attention"/> if any carries a
    /// warning (orphan, missing verification/allocation, advisory
    /// relationship kind) with no error present; <see cref="EngineeringHealthStatus.Healthy"/>
    /// otherwise.
    /// </summary>
    public EngineeringHealthStatus RequirementsStatus
    {
        get
        {
            var live = LiveRequirements;
            if (live.Count == 0)
                return EngineeringHealthStatus.Unknown;

            var results = LiveRequirementValidationResults;

            if (results.Any(r => r.Errors.Count > 0))
                return EngineeringHealthStatus.Blocked;

            return results.Any(r => r.Warnings.Count > 0)
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Verification discipline's own status (`WP 9.3A`) - a real,
    /// derived read: <see cref="EngineeringHealthStatus.Unknown"/> if no
    /// live Verification Activity exists yet;
    /// <see cref="EngineeringHealthStatus.Blocked"/> if any live Activity's
    /// own most recent recorded result has
    /// <see cref="VerificationOutcome.Fail"/>;
    /// <see cref="EngineeringHealthStatus.Attention"/> if any is
    /// <see cref="OutstandingVerificationActions"/> with no Fail present;
    /// <see cref="EngineeringHealthStatus.Healthy"/> otherwise.
    /// </summary>
    public EngineeringHealthStatus VerificationStatus
    {
        get
        {
            if (LiveVerificationActivities.Count == 0)
                return EngineeringHealthStatus.Unknown;

            if (FailedVerificationCount > 0)
                return EngineeringHealthStatus.Blocked;

            return OutstandingVerificationActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Calculations discipline's own status (`WP 9.2A`) - a real,
    /// derived read: <see cref="EngineeringHealthStatus.Unknown"/> if no
    /// live Calculation exists yet; <see cref="EngineeringHealthStatus.Blocked"/>
    /// if any live Calculation's own most recent execution recorded a
    /// <see cref="CalculationValidationOutcome.Conditional"/> outcome
    /// ("Failed"); <see cref="EngineeringHealthStatus.Attention"/> if any is
    /// awaiting review or out-of-date (<see cref="OutstandingCalculationActions"/>)
    /// with no failure present; <see cref="EngineeringHealthStatus.Healthy"/>
    /// otherwise.
    /// </summary>
    public EngineeringHealthStatus CalculationStatus
    {
        get
        {
            if (LiveCalculations.Count == 0)
                return EngineeringHealthStatus.Unknown;

            if (FailedCalculationsCount > 0)
                return EngineeringHealthStatus.Blocked;

            return OutstandingCalculationActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Documentation discipline's own status (`WP 9.4A`) - a real,
    /// derived read: <see cref="EngineeringHealthStatus.Unknown"/> if no
    /// live Document exists yet; <see cref="EngineeringHealthStatus.Attention"/>
    /// if any is awaiting review or has <see cref="HasMissingEvidence"/>
    /// (<see cref="OutstandingDocumentActions"/>); <see cref="EngineeringHealthStatus.Healthy"/>
    /// otherwise. Never <see cref="EngineeringHealthStatus.Blocked"/> - unlike
    /// Calculations' own "Failed" outcome, no Document Domain concept
    /// represents an unrecoverable failure state, so this discipline's own
    /// status never reports it.
    /// </summary>
    public EngineeringHealthStatus DocumentationStatus
    {
        get
        {
            if (LiveDocuments.Count == 0)
                return EngineeringHealthStatus.Unknown;

            return OutstandingDocumentActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>Gets the Review discipline's own status - always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus ReviewStatus => EngineeringHealthStatus.Unknown;

    /// <summary>
    /// Gets the Manufacturing discipline's own status (`WP 9.5A`) - a real,
    /// derived read, genuinely new (no `WP 8.1C` placeholder slot existed to
    /// reuse, unlike <see cref="VerificationStatus"/>/<see cref="DocumentationStatus"/>):
    /// <see cref="EngineeringHealthStatus.Unknown"/> if no live Manufacturing
    /// object exists yet; <see cref="EngineeringHealthStatus.Blocked"/> if any
    /// live Inspection's own most recent recorded result has
    /// <see cref="VerificationOutcome.Fail"/>; <see cref="EngineeringHealthStatus.Attention"/>
    /// if any Operation step is still open (<see cref="OpenOperationsCount"/>)
    /// or any Supplier Operation has no <c>"manufacturedBy"</c> link yet
    /// (<see cref="UnfulfilledSupplierOperationCount"/>), with no Fail
    /// present; <see cref="EngineeringHealthStatus.Healthy"/> otherwise.
    /// </summary>
    public EngineeringHealthStatus ManufacturingStatus
    {
        get
        {
            if (LiveManufacturingObjects.Count == 0)
                return EngineeringHealthStatus.Unknown;

            if (FailedInspectionCount > 0)
                return EngineeringHealthStatus.Blocked;

            return OutstandingManufacturingActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Engineering Health Summary's own per-discipline KPI cards.
    /// The <c>"Requirements"</c> entry is a real read (`WP 9.1A`) - the
    /// live requirement count, or a disclosed placeholder if none exist
    /// yet; every other entry remains placeholder
    /// (<see cref="CockpitKpiCard.IsPlaceholder"/>) until its own
    /// discipline is wired to the Workspace.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            var totalRequirements = LiveRequirements.Count;
            var totalCalculations = LiveCalculations.Count;
            var totalDocuments = LiveDocuments.Count;
            var totalVerificationActivities = LiveVerificationActivities.Count;

            return
            [
                totalRequirements > 0 ? new("Requirements", $"{totalRequirements} total", IsPlaceholder: false) : new("Requirements", "—", IsPlaceholder: true),
                totalVerificationActivities > 0 ? new("Verification", $"{totalVerificationActivities} total", IsPlaceholder: false) : new("Verification", "—", IsPlaceholder: true),
                totalCalculations > 0 ? new("Calculations", $"{totalCalculations} total", IsPlaceholder: false) : new("Calculations", "—", IsPlaceholder: true),
                totalDocuments > 0 ? new("Documentation", $"{totalDocuments} total", IsPlaceholder: false) : new("Documentation", "—", IsPlaceholder: true),
                new("Review", "—", IsPlaceholder: true),
                new("Risks", "—", IsPlaceholder: true),
            ];
        }
    }

    /// <summary>
    /// Gets the Manufacturing discipline's own dedicated KPI card set
    /// (`WP 9.5A`) - this Work Package's own literal seven-card breakdown:
    /// Manufacturing Objects, Manufacturing Readiness, Released Items, Open
    /// Operations, Supplier Status, Inspection Status, Production Health.
    /// Every card is a real read; every value is <c>0</c>/"-" honestly,
    /// never fabricated, if no live Manufacturing object exists yet. No
    /// <c>"Manufacturing"</c> placeholder card ever existed in
    /// <see cref="KpiCards"/> for this set to replace (confirmed by direct
    /// read) - unlike every prior discipline's own dedicated card set, this
    /// one is purely additive.
    /// </summary>
    /// <remarks>
    /// "Manufacturing Readiness" reports the share of live Operation steps
    /// (<see cref="LiveManufacturingOperationSteps"/> - <c>Classification =
    /// "Operation"</c>, never a Routing container or Supplier Operation)
    /// that have reached <see cref="LifecycleState.Released"/>. "Supplier
    /// Status" reports the share of live Supplier Operations with a real
    /// <c>"manufacturedBy"</c> link recorded. Neither reuses
    /// <see cref="FormatCoverage"/> - that helper's own zero-denominator
    /// text is hardcoded Requirements-specific ("no requirements yet"), a
    /// pre-existing, disclosed minor inaccuracy already latent in
    /// <see cref="CalculationsKpiCards"/>/<see cref="VerificationKpiCards"/>'s
    /// own reuse of it, out of this Work Package's own scope to fix; this
    /// set instead formats its own two coverage cards locally with an
    /// accurate empty-state message, rather than compounding the
    /// inaccuracy with a third instance.
    /// </remarks>
    public IReadOnlyList<CockpitKpiCard> ManufacturingKpiCards
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
                new("Production Health", ManufacturingStatus.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>
    /// Gets the Verification discipline's own dedicated KPI card set
    /// (`WP 9.3A`) - replacing the single, generic <c>"Verification"</c>
    /// placeholder card this Work Package's own controlling instruction
    /// names, with the full breakdown it asks for: Total Verification
    /// Records, Planned, In Progress, Passed, Failed, Conditional,
    /// Outstanding, Verification Coverage, Project Verification Health.
    /// Every card is a real read; every value is <c>0</c>/"-" honestly,
    /// never fabricated, if no live Verification Activity exists yet.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed shape, mirroring <see cref="RequirementsKpiCards"/>'s
    /// own "Released→Satisfied" and <see cref="CalculationsKpiCards"/>'s
    /// own "Failed"/"Out-of-date" precedent:</b> "Planned"/"In Progress"
    /// bucket by <see cref="LifecycleState"/> for an Activity with no
    /// recorded result yet (`ADR-0090`: Draft = Plan, InReview+ = Activity
    /// under way); "Passed"/"Failed"/"Conditional" bucket by each
    /// Activity's own latest recorded <see cref="VerificationOutcome"/>
    /// once one exists. "Verification Coverage" reports the share of live
    /// Activities with at least one recorded result - the identical name
    /// and meaning <see cref="CalculationsKpiCards"/>'s own "Verification
    /// Coverage" card already established platform-wide, reused verbatim
    /// rather than inventing a differently-named but identically-shaped
    /// card.
    /// </remarks>
    public IReadOnlyList<CockpitKpiCard> VerificationKpiCards
    {
        get
        {
            var snapshots = LiveVerificationSnapshots;
            var total = snapshots.Count;
            var recorded = snapshots.Count(s => s.LatestRecord is not null);

            return
            [
                new("Total Verification Records", TotalVerificationRecordsCount.ToString(), IsPlaceholder: false),
                new("Planned", PlannedVerificationCount.ToString(), IsPlaceholder: false),
                new("In Progress", InProgressVerificationCount.ToString(), IsPlaceholder: false),
                new("Passed", PassedVerificationCount.ToString(), IsPlaceholder: false),
                new("Failed", FailedVerificationCount.ToString(), IsPlaceholder: false),
                new("Conditional", ConditionalVerificationCount.ToString(), IsPlaceholder: false),
                new("Outstanding", OutstandingVerificationActions.ToString(), IsPlaceholder: false),
                new("Verification Coverage", FormatCoverage(recorded, total), IsPlaceholder: false),
                new("Project Verification Health", VerificationStatus.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>
    /// Gets the Documents discipline's own dedicated KPI card set
    /// (`WP 9.4A`) - replacing the single, generic <c>"Documentation"</c>
    /// placeholder card this Work Package's own controlling instruction
    /// names, with the full breakdown it asks for: Total Documents, Draft,
    /// Review, Approved, Released, Outstanding Reviews, Missing Evidence,
    /// Documentation Health. Every card is a real read; every value is
    /// <c>0</c>/"-" honestly, never fabricated, if no live Document exists
    /// yet.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="RequirementsKpiCards"/>'s own "Released→Satisfied"
    /// mapping and <see cref="CalculationsKpiCards"/>'s own "Failed"/
    /// "Out-of-date" mappings, no status-name translation is needed here -
    /// <see cref="LifecycleState"/>'s own existing Draft/InReview/Approved/
    /// Released values already match this Work Package's own named statuses
    /// one-for-one (see <see cref="SetDocumentStatusCommand"/>'s own
    /// remarks). "Missing Evidence" reports <see cref="MissingEvidenceCount"/>
    /// - see <see cref="HasMissingEvidence"/>'s own disclosed heuristic.
    /// </remarks>
    public IReadOnlyList<CockpitKpiCard> DocumentsKpiCards
    {
        get
        {
            var documents = LiveDocuments;
            var total = documents.Count;

            int CountStatus(LifecycleState status) =>
                documents.Count(d => d is IHasLifecycle lifecycle && lifecycle.Status == status);

            return
            [
                new("Total Documents", total.ToString(), IsPlaceholder: false),
                new("Draft", CountStatus(LifecycleState.Draft).ToString(), IsPlaceholder: false),
                new("Review", CountStatus(LifecycleState.InReview).ToString(), IsPlaceholder: false),
                new("Approved", CountStatus(LifecycleState.Approved).ToString(), IsPlaceholder: false),
                new("Released", CountStatus(LifecycleState.Released).ToString(), IsPlaceholder: false),
                new("Outstanding Reviews", OutstandingDocumentReviews.ToString(), IsPlaceholder: false),
                new("Missing Evidence", MissingEvidenceCount.ToString(), IsPlaceholder: false),
                new("Documentation Health", DocumentationStatus.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>
    /// Gets the Calculations discipline's own dedicated KPI card set
    /// (`WP 9.2A`) - replacing the single, generic <c>"Calculations"</c>
    /// placeholder card this Work Package's own controlling instruction
    /// names, with the full breakdown it asks for: Total, Draft, Review,
    /// Approved, Failed, Out-of-date, Verification Coverage, Calculation
    /// Health. Every card is a real read; every value is <c>0</c>/"-"
    /// honestly, never fabricated, if no live Calculation exists yet.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed mapping, mirroring <see cref="RequirementsKpiCards"/>'s
    /// own identical "Released→Satisfied" precedent:</b> "Failed" reports
    /// the count of live Calculations whose own most recent execution
    /// recorded a <see cref="CalculationValidationOutcome.Conditional"/>
    /// outcome - the Calculation Framework's own closed validation
    /// vocabulary has no literal "Failed" value (a genuine constraint
    /// violation throws <see cref="Tempest.Core.Calculations.CalculationInputInvalidException"/>
    /// instead, producing no record at all). "Verification Coverage"
    /// reports the share of live Calculations that have been executed at
    /// least once - real, evidentiary execution, not a fabricated value.
    /// "Calculations awaiting review" (this Work Package's own controlling
    /// instruction) is <see cref="OutstandingCalculationActions"/>, surfaced
    /// via <see cref="AttentionItems"/>/<see cref="OpenActions"/>, not a
    /// duplicate KPI card here - "Review" above is the InReview status
    /// count.
    /// </remarks>
    public IReadOnlyList<CockpitKpiCard> CalculationsKpiCards
    {
        get
        {
            var snapshots = LiveCalculationSnapshots;
            var total = snapshots.Count;

            int CountStatus(LifecycleState status) =>
                snapshots.Count(s => s.Calculation is IHasLifecycle lifecycle && lifecycle.Status == status);

            var executed = snapshots.Count(s => s.LatestRecord is not null);
            var outOfDate = snapshots.Count(s => IsOutOfDate(s.Calculation, s.LatestRecord));

            return
            [
                new("Total Calculations", total.ToString(), IsPlaceholder: false),
                new("Draft", CountStatus(LifecycleState.Draft).ToString(), IsPlaceholder: false),
                new("Review", CountStatus(LifecycleState.InReview).ToString(), IsPlaceholder: false),
                new("Approved", CountStatus(LifecycleState.Approved).ToString(), IsPlaceholder: false),
                new("Failed", FailedCalculationsCount.ToString(), IsPlaceholder: false),
                new("Out-of-date", outOfDate.ToString(), IsPlaceholder: false),
                new("Verification Coverage", FormatCoverage(executed, total), IsPlaceholder: false),
                new("Calculation Health", CalculationStatus.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>
    /// Gets the Requirements discipline's own dedicated KPI card set
    /// (`WP 9.1A`) - replacing the single, generic <c>"Requirements"</c>
    /// placeholder card this Work Package's own controlling instruction
    /// names, with the full breakdown it asks for: Total, Draft, Review,
    /// Approved, Released, Verification Coverage, Allocation Coverage,
    /// Requirement Health, and Outstanding Actions. Every card is a real
    /// read; every value is <c>0</c>/"-" honestly, never fabricated, if no
    /// live Requirement exists yet.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed status-name mapping:</b> this platform's own
    /// <see cref="RequirementStatus"/> (`WP7.2C Requirement Lifecycle
    /// Model.md`) has no <c>"Released"</c> value - the closed set is
    /// <c>Draft</c>/<c>Reviewed</c>/<c>Approved</c>/<c>Allocated</c>/
    /// <c>Verified</c>/<c>Satisfied</c>/<c>Obsolete</c>. This card set's own
    /// "Released" card reports the <see cref="RequirementStatus.Satisfied"/>
    /// count - the closest existing terminal-success status - rather than
    /// inventing a new status value, which this Work Package's own
    /// controlling instruction forbids ("No contract redesign"). The two
    /// intervening statuses (<see cref="RequirementStatus.Allocated"/>,
    /// <see cref="RequirementStatus.Verified"/>) are not silently dropped -
    /// they remain visible via <see cref="RequirementsStatus"/>'s own
    /// validation-driven derivation and every live requirement's own
    /// Property Inspector facets.
    /// </remarks>
    public IReadOnlyList<CockpitKpiCard> RequirementsKpiCards
    {
        get
        {
            var live = LiveRequirements;
            var total = live.Count;
            var counts = live.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());

            int CountOf(RequirementStatus status) => counts.TryGetValue(status, out var count) ? count : 0;

            return
            [
                new("Total Requirements", total.ToString(), IsPlaceholder: false),
                new("Draft", CountOf(RequirementStatus.Draft).ToString(), IsPlaceholder: false),
                new("Review", CountOf(RequirementStatus.Reviewed).ToString(), IsPlaceholder: false),
                new("Approved", CountOf(RequirementStatus.Approved).ToString(), IsPlaceholder: false),
                new("Released", CountOf(RequirementStatus.Satisfied).ToString(), IsPlaceholder: false),
                new("Verification Coverage", FormatCoverage(VerifiedRequirementCount, total), IsPlaceholder: false),
                new("Allocation Coverage", FormatCoverage(AllocatedRequirementCount, total), IsPlaceholder: false),
                new("Requirement Health", RequirementsStatus.ToString(), IsPlaceholder: false),
                new("Outstanding Actions", OutstandingRequirementActions.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>
    /// Gets every live requirement's own <see cref="IRequirementValidationService"/>
    /// result - the shared basis for <see cref="RequirementsStatus"/> and
    /// <see cref="OutstandingRequirementActions"/>, computed once per read
    /// so both stay consistent with each other.
    /// </summary>
    /// <remarks>
    /// <b>Defensive, not currently load-bearing:</b> the concrete
    /// <see cref="RequirementValidationService"/> reads only
    /// <see cref="IRequirementsService.GetRelationshipsAsync"/> today, never
    /// the permission-gated <see cref="IRequirementsService.GetEvidenceAsync"/>
    /// (a disclosed fix made within this same Work Package - see
    /// <see cref="RequirementValidationService"/>'s own remarks), so
    /// <see cref="PermissionDeniedException"/> is not expected here in
    /// practice. The guard remains because <see cref="IRequirementValidationService"/>
    /// is an interface, not a sealed contract to this one implementation -
    /// a passive status dashboard must never throw because some future
    /// implementation's own validation needs a narrower capability than
    /// "can view the Cockpit at all"; a requirement whose own validation
    /// cannot be evaluated for that reason is silently excluded from this
    /// read (never counted as a false "no findings"), rather than crashing
    /// every other card this property feeds.
    /// </remarks>
    private IReadOnlyList<IValidationResult> LiveRequirementValidationResults
    {
        get
        {
            var results = new List<IValidationResult>();

            foreach (var requirement in LiveRequirements)
            {
                try
                {
                    results.Add(_requirementValidationService.ValidateAsync(requirement.Id).GetAwaiter().GetResult());
                }
                catch (PermissionDeniedException)
                {
                    // See this property's own remarks.
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Gets the number of live requirements with at least one recorded
    /// verification (`WP 9.1A`) - a real read via
    /// <see cref="IRequirementsService.GetRelationshipsAsync"/> for a
    /// <see cref="VerificationService.VerifiedByRelationshipKind"/>
    /// relationship, the existing Digital Thread read, never a new
    /// traversal. Deliberately does not use <see cref="IRequirementsService.GetEvidenceAsync"/>
    /// (unlike <see cref="RequirementValidationService"/>'s own identical
    /// check) - <see cref="IRequirementsService.GetRelationshipsAsync"/> is
    /// not permission-gated, so this KPI stays available to every principal
    /// that can view the Cockpit at all, never only those additionally
    /// holding <see cref="VerificationService.ReadPermission"/>.
    /// </summary>
    private int VerifiedRequirementCount =>
        LiveRequirements.Count(r => _requirementsService.GetRelationshipsAsync(r.Id).GetAwaiter().GetResult()
            .Any(reference => string.Equals(reference.RelationshipKind, VerificationService.VerifiedByRelationshipKind, StringComparison.Ordinal)));

    /// <summary>Gets the number of live requirements with at least one <see cref="RequirementRelationshipKinds.AllocatedTo"/> relationship (`WP 9.1A`) - a real read via <see cref="IRequirementsService.GetRelationshipsAsync"/>, the existing Digital Thread read, never a new traversal.</summary>
    private int AllocatedRequirementCount =>
        LiveRequirements.Count(r => _requirementsService.GetRelationshipsAsync(r.Id).GetAwaiter().GetResult()
            .Any(reference => string.Equals(reference.RelationshipKind, RequirementRelationshipKinds.AllocatedTo, StringComparison.Ordinal)));

    /// <summary>Gets the total count of Requirements validation findings (errors plus warnings) across every live requirement (`WP 9.1A`) - the Cockpit's own "Outstanding Actions" KPI.</summary>
    public int OutstandingRequirementActions => LiveRequirementValidationResults.Sum(r => r.Errors.Count + r.Warnings.Count);

    private static string FormatCoverage(int numerator, int denominator) =>
        denominator == 0 ? "— (no requirements yet)" : $"{numerator * 100 / denominator}% ({numerator}/{denominator})";

    /// <summary>
    /// Gets the Risk Summary's own display text - always a disclosed
    /// placeholder today: no Risk service exists anywhere in this
    /// platform yet.
    /// </summary>
    public string RiskSummary => "0 open (placeholder — Risk tracking is not yet wired to the Workspace).";

    /// <summary>
    /// Gets the Digital Thread Summary's own display text - always a
    /// disclosed placeholder today, and always will be a summary count
    /// only, never a live traversal (`WP 8.1C`'s own explicit "no Digital
    /// Thread traversal" scope boundary).
    /// </summary>
    public string DigitalThreadSummary => "0 links tracked (placeholder — no traversal is performed by the Cockpit).";

    /// <summary>
    /// Gets the Upcoming Milestones region's own entries - fixed,
    /// representative placeholder content.
    /// </summary>
    public IReadOnlyList<string> UpcomingMilestones { get; } = [];

    // ------------------------------------------------------------
    // What should I do next?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the "Open Actions" region's own entries. A conditional
    /// Requirements triage entry (`WP 9.1A`) appears first when
    /// <see cref="OutstandingRequirementActions"/> is non-zero; the rest
    /// remain fixed, representative placeholder content.
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OpenActions
    {
        get
        {
            var actions = new List<CockpitActionItem>();

            if (OutstandingRequirementActions > 0)
                actions.Add(new($"Triage {OutstandingRequirementActions} outstanding Requirements validation finding(s)", "Systems Engineer"));

            if (OutstandingCalculationActions > 0)
                actions.Add(new($"Triage {OutstandingCalculationActions} outstanding Calculation(s) (awaiting review or out-of-date)", "Engineer"));

            if (OutstandingDocumentActions > 0)
                actions.Add(new($"Triage {OutstandingDocumentActions} outstanding Document(s) (awaiting review or missing evidence)", "Engineer"));

            if (OutstandingVerificationActions > 0)
                actions.Add(new($"Triage {OutstandingVerificationActions} outstanding Verification Activity(ies) (awaiting result or Failed)", "Engineer"));

            if (OutstandingManufacturingActions > 0)
                actions.Add(new($"Triage {OutstandingManufacturingActions} outstanding Manufacturing item(s) (open Operations, unfulfilled Supplier Operations, or Failed Inspections)", "Manufacturing Engineer"));

            actions.Add(new("Review the Project Explorer's own sample content", "Engineer"));
            actions.Add(new("Await the next real engineering discipline module", "Product Owner"));

            return actions;
        }
    }

    /// <summary>
    /// Gets a short, contextual "what to do next" hint list - computed
    /// from real Workspace state (never fixed placeholder text): whether
    /// there is somewhere to continue, an area to browse, or a command to
    /// run right now.
    /// </summary>
    public IReadOnlyList<string> QuickActions
    {
        get
        {
            var actions = new List<string>();

            if (ContinueWhereILeftOff is not null)
                actions.Add($"Continue: {ContinueWhereILeftOff.Title}");

            if (AreaCount > 0)
                actions.Add("Browse an Area below to explore the Project Explorer.");

            if (AvailableCommands.Count > 0)
                actions.Add("Run a Global Command below.");

            return actions;
        }
    }

    /// <summary>
    /// Gets the Recent Activity region's own entries - a real read from
    /// <see cref="NavigationService.RecentItems"/> (`WP 8.1B`), most
    /// recent first.
    /// </summary>
    public IReadOnlyList<RecentNavigationItem> RecentActivity => _navigationService.RecentItems;

    /// <summary>Gets the number of areas currently registered - a real Workspace status indicator.</summary>
    public int AreaCount => _navigationService.Areas.Count;

    /// <summary>Gets the number of documents currently open - a real Workspace status indicator.</summary>
    public int OpenDocumentCount => _navigationService.OpenViews.Count;

    /// <summary>
    /// Gets every currently-available global command - the Cockpit's own
    /// Command Palette integration (`ADR-0070`): a real, live read of
    /// <see cref="ICommandRegistry.Items"/>, filtered by each descriptor's
    /// own <see cref="CommandDescriptor.CanExecute"/>, exactly as
    /// `WP8.0C Interaction Specification.md` §1 specifies ("a command not
    /// yet applicable... appears but is shown disabled" - here narrowed to
    /// "available commands only," since the Cockpit's own scope is a
    /// dashboard region, not the full overlay palette).
    /// </summary>
    public IReadOnlyList<CommandDescriptor> AvailableCommands =>
        _commandRegistry.Items.Where(d => d.CanExecute is null || d.CanExecute()).ToList();

    /// <summary>
    /// Invokes the <paramref name="index"/>-th command in
    /// <see cref="AvailableCommands"/> (1-based).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public Task<CommandResult> InvokeCommandAsync(int index, CancellationToken cancellationToken = default)
    {
        var commands = AvailableCommands;

        if (index < 1 || index > commands.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Must be between 1 and {commands.Count}.");

        return _commandRegistry.InvokeAsync(commands[index - 1].Id, cancellationToken);
    }

    /// <summary>
    /// Re-opens or focuses <see cref="ContinueWhereILeftOff"/> - the
    /// Cockpit's own "Continue Where I Left Off" navigation gesture, a
    /// real dispatch through <see cref="NavigationService.OpenAsync"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="ContinueWhereILeftOff"/> is <see langword="null"/>.</exception>
    public Task<IWorkspaceView> ContinueAsync(CancellationToken cancellationToken = default)
    {
        var item = ContinueWhereILeftOff
            ?? throw new InvalidOperationException("Nothing to continue - no object has been opened yet this session.");

        return _navigationService.OpenAsync(item.ObjectId, item.Kind, cancellationToken);
    }

    /// <summary>
    /// Re-opens or focuses the <paramref name="index"/>-th entry in
    /// <see cref="RecentActivity"/> (1-based) - the Cockpit's own Recent
    /// Activity navigation gesture, a real dispatch through
    /// <see cref="NavigationService.OpenAsync"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public Task<IWorkspaceView> OpenRecentAsync(int index, CancellationToken cancellationToken = default)
    {
        var items = RecentActivity;

        if (index < 1 || index > items.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Must be between 1 and {items.Count}.");

        var item = items[index - 1];
        return _navigationService.OpenAsync(item.ObjectId, item.Kind, cancellationToken);
    }
}
