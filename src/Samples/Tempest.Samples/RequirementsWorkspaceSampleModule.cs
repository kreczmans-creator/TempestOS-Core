using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module demonstrating `WP 9.1A`'s
/// Requirements Management Workspace — real, representative Requirements
/// data for the Engineering Workspace's own Requirements area, Engineering
/// Cockpit KPIs, and Digital Thread to present, per this Work Package's
/// own explicit "meaningful engineering data rather than placeholders"
/// requirement.
/// </summary>
/// <remarks>
/// <para>
/// Builds a three-level Group hierarchy (<c>Aircraft Requirements</c> →
/// <c>Wing Requirements</c> → <c>Spar Requirements</c>, plus <c>Avionics
/// Requirements</c> created as a root group and then moved under
/// <c>Aircraft Requirements</c> via <see cref="IRequirementsService.MoveGroupAsync"/>
/// - a direct, working demonstration of this Work Package's own
/// <c>RequirementGroupDto</c> storage-model fix, not just a unit test of
/// it); ten Requirements spanning every named lifecycle status this
/// Work Package's own controlling instruction lists (<c>Draft</c>/
/// <c>Reviewed</c>/<c>Approved</c>/<c>Allocated</c>/<c>Verified</c>), plus
/// <c>Satisfied</c>; parent/child requirement links (<c>DependsOn</c>/
/// <c>DerivesFrom</c>/<c>Satisfies</c>); allocations to the existing
/// Mechanical sample data's own Wing Assembly and Spar Web Plate
/// (<c>AllocatedTo</c> - the real cross-discipline integration point this
/// Work Package's own "allocations to assemblies and parts" scope item
/// asks for); one verification recorded directly through
/// <see cref="IVerificationService.RecordAsync"/>; one soft-deleted
/// Requirement demonstrating <see cref="IRequirement.IsDeleted"/>; two
/// Requirement Collections ("Requirement Sets"); and one
/// <see cref="RequirementCollectionExportAdapter"/> registration
/// demonstrating this Work Package's own Import/Export integration.
/// </para>
/// <para>
/// <b>A disclosed, deliberate first:</b> constructor-injects
/// <see cref="MechanicalProductStructureSampleModule"/> directly to reach
/// its own live <c>WingAssemblyId</c>/<c>SparWebPlateId</c> - no prior
/// sample module has ever depended on another one's own instance before
/// this. Safe because <see cref="ModuleServiceCollectionExtensions.AddDiscoveredModules"/>
/// registers every discovered module type as a DI singleton, and
/// <c>ModuleLifecycleManager</c> initialises modules in ordinal Id order
/// (`tempest.samples.mechanicalproductstructure` sorts before
/// `tempest.samples.requirementsworkspace`) - by the time this module's
/// own <see cref="InitialiseAsync"/> runs, the Mechanical module's own has
/// already completed, so its own Ids are already populated. A genuine
/// coupling, not a hidden one: any host that discovers this module without
/// also discovering <see cref="MechanicalProductStructureSampleModule"/>
/// fails DI resolution immediately and loudly (<c>ServiceNotRegisteredException</c>),
/// never silently.
/// </para>
/// <para>
/// <see cref="CreateRequirementCommand"/> and every other `Tempest.App`
/// Requirements Workspace command are deliberately not exercised here -
/// both live in <c>Tempest.App</c>, which this project (<c>Tempest.Samples</c>)
/// is never referenced by (`Tempest.App` depends on `Tempest.Samples`,
/// never the reverse); they are covered instead by dedicated Workspace
/// command tests, mirroring <see cref="MechanicalProductStructureSampleModule"/>'s
/// own identical, already-disclosed precedent.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.requirementsworkspace", "Requirements Workspace Sample", "1.0.0")]
public sealed class RequirementsWorkspaceSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.requirementsworkspace-user";

    /// <summary>The artifact section kind <see cref="RequirementCollectionExportAdapter"/> is registered under.</summary>
    public const string ExportAdapterKind = "tempest.samples.requirementsworkspace.collection";

    private readonly IIdentityService _identityService;
    private readonly IRequirementsService _requirementsService;
    private readonly IVerificationService _verificationService;
    private readonly ImportService _importService;
    private readonly MechanicalProductStructureSampleModule _mechanicalSampleModule;

    /// <summary>Initialises a new instance of the <see cref="RequirementsWorkspaceSampleModule"/> class.</summary>
    public RequirementsWorkspaceSampleModule(
        IIdentityService identityService,
        IRequirementsService requirementsService,
        IVerificationService verificationService,
        ImportService importService,
        MechanicalProductStructureSampleModule mechanicalSampleModule)
        : base("tempest.samples.requirementsworkspace", "Requirements Workspace Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(verificationService);
        ArgumentNullException.ThrowIfNull(importService);
        ArgumentNullException.ThrowIfNull(mechanicalSampleModule);

        _identityService = identityService;
        _requirementsService = requirementsService;
        _verificationService = verificationService;
        _importService = importService;
        _mechanicalSampleModule = mechanicalSampleModule;
    }

    public Guid? AircraftRootGroupId { get; private set; }
    public Guid? WingGroupId { get; private set; }
    public Guid? SparGroupId { get; private set; }
    public Guid? AvionicsGroupId { get; private set; }
    public Guid? StructuralCollectionId { get; private set; }
    public Guid? AvionicsCollectionId { get; private set; }
    public Guid? DeletedRequirementId { get; private set; }
    public IReadOnlyList<Guid> AllSampleRequirementIds { get; private set; } = [];
    public bool HasRegistered { get; private set; }

    /// <remarks>
    /// <b>Idempotent restart (`WP 10.1B`, `TD-37`):</b> every Requirement
    /// below carries a fixed, literal <c>Identifier</c>, durably checked
    /// for uniqueness by <see cref="IRequirementsService"/> (`ADR-0058`) —
    /// a second real launch from the same working directory (`ADR-0041`)
    /// would otherwise collide on <c>"REQ-STR-001"</c>, the first one
    /// created, after having already silently duplicated the four Group
    /// objects that precede it (<c>CreateGroupAsync</c> enforces no
    /// name-uniqueness of its own). This module checks the same durable
    /// signal up front — <c>"REQ-STR-001"</c> already existing — and skips
    /// the entire sequence outright if so, leaving every Id property at
    /// its honest, unset default and not (re-)registering the Export
    /// adapter (which needs a real collection Id this run never creates) —
    /// mirroring <see cref="EngineeringDomainSampleModule"/>'s own
    /// identical, same-Work-Package fix.
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        if (await _requirementsService.FindByIdentifierAsync("REQ-STR-001", cancellationToken).ConfigureAwait(false) is not null)
        {
            // Already durably seeded by an earlier launch against this same
            // persistence store (TD-37) - skip the whole sequence rather
            // than silently duplicating the four Group objects that
            // precede REQ-STR-001, then crashing on it anyway.
            HasRegistered = true;
            return;
        }

        var requirementIds = new List<Guid>();

        // ---- Group hierarchy: three levels deep, plus one root-created-then-moved group ----
        var aircraftRoot = await _requirementsService.CreateGroupAsync("Aircraft Requirements", cancellationToken: cancellationToken).ConfigureAwait(false);
        AircraftRootGroupId = aircraftRoot.Id;

        var wingGroup = await _requirementsService.CreateGroupAsync("Wing Requirements", aircraftRoot.Id, cancellationToken).ConfigureAwait(false);
        WingGroupId = wingGroup.Id;

        var sparGroup = await _requirementsService.CreateGroupAsync("Spar Requirements", wingGroup.Id, cancellationToken).ConfigureAwait(false);
        SparGroupId = sparGroup.Id;

        // Created as a root group, then moved - a real, working demonstration
        // of this Work Package's own RequirementGroupDto storage-model fix
        // (MoveGroupAsync's own live ParentGroupId resolution), not just a
        // unit test of it.
        var avionicsGroup = await _requirementsService.CreateGroupAsync("Avionics Requirements", cancellationToken: cancellationToken).ConfigureAwait(false);
        AvionicsGroupId = avionicsGroup.Id;
        await _requirementsService.MoveGroupAsync(avionicsGroup.Id, aircraftRoot.Id, cancellationToken).ConfigureAwait(false);

        // ---- Structural Requirements: Draft -> Reviewed -> Approved -> Allocated -> Verified -> Satisfied ----
        var strDraft = await _requirementsService.CreateAsync(
            "REQ-STR-001", "The wing structure shall withstand limit load without permanent deformation.", "structural", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(strDraft.Id);
        await _requirementsService.MoveToGroupAsync(strDraft.Id, wingGroup.Id, cancellationToken).ConfigureAwait(false);

        var strReviewed = await _requirementsService.CreateAsync(
            "REQ-STR-002", "The wing spar shall maintain a positive margin of safety at ultimate load.", "structural", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(strReviewed.Id);
        await _requirementsService.MoveToGroupAsync(strReviewed.Id, sparGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strReviewed.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
        await _requirementsService.LinkAsync(strReviewed.Id, strDraft.Id, RequirementRelationshipKinds.DependsOn, cancellationToken).ConfigureAwait(false);

        var strApproved = await _requirementsService.CreateAsync(
            "REQ-STR-003", "The wing spar web plate thickness shall meet the structural sizing requirement.", "structural", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(strApproved.Id);
        await _requirementsService.MoveToGroupAsync(strApproved.Id, sparGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strApproved.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strApproved.Id, RequirementStatus.Approved, cancellationToken).ConfigureAwait(false);
        await _requirementsService.LinkAsync(strApproved.Id, strReviewed.Id, RequirementRelationshipKinds.DerivesFrom, cancellationToken).ConfigureAwait(false);

        var strAllocated = await _requirementsService.CreateAsync(
            "REQ-STR-004", "The wing assembly shall be demonstrated by structural test.", "structural", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(strAllocated.Id);
        await _requirementsService.MoveToGroupAsync(strAllocated.Id, wingGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strAllocated.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strAllocated.Id, RequirementStatus.Approved, cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.WingAssemblyId is { } wingAssemblyId)
            await _requirementsService.LinkAsync(strAllocated.Id, wingAssemblyId, RequirementRelationshipKinds.AllocatedTo, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strAllocated.Id, RequirementStatus.Allocated, cancellationToken).ConfigureAwait(false);

        var strVerified = await _requirementsService.CreateAsync(
            "REQ-STR-005", "The wing spar shall be verified by static test to ultimate load.", "structural", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(strVerified.Id);
        await _requirementsService.MoveToGroupAsync(strVerified.Id, sparGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strVerified.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strVerified.Id, RequirementStatus.Approved, cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.SparWebPlateId is { } sparWebPlateId)
            await _requirementsService.LinkAsync(strVerified.Id, sparWebPlateId, RequirementRelationshipKinds.AllocatedTo, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strVerified.Id, RequirementStatus.Allocated, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strVerified.Id, RequirementStatus.Verified, cancellationToken).ConfigureAwait(false);

        var verificationContext = new VerificationContext();
        verificationContext.RecordCriterion("Wing spar demonstrated positive margin of safety at ultimate load.", isSatisfied: true);
        verificationContext.RecordEvidence("Fictional static test report — for demonstration only.");
        await _verificationService.RecordAsync(strVerified.Id, VerificationOutcome.Pass, "test", verificationContext, cancellationToken).ConfigureAwait(false);

        var strSatisfied = await _requirementsService.CreateAsync(
            "REQ-STR-006", "The wing structure shall satisfy the limit-load requirement upon completion of static test.", "structural", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(strSatisfied.Id);
        await _requirementsService.MoveToGroupAsync(strSatisfied.Id, wingGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strSatisfied.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strSatisfied.Id, RequirementStatus.Approved, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strSatisfied.Id, RequirementStatus.Allocated, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strSatisfied.Id, RequirementStatus.Verified, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(strSatisfied.Id, RequirementStatus.Satisfied, cancellationToken).ConfigureAwait(false);
        // Satisfies is recorded from the satisfying target to the requirement it satisfies (RequirementRelationshipKinds.Satisfies's own remarks).
        await _requirementsService.LinkAsync(strSatisfied.Id, strDraft.Id, RequirementRelationshipKinds.Satisfies, cancellationToken).ConfigureAwait(false);

        // ---- Avionics Requirements: Draft -> Reviewed -> Approved, plus owner/priority ----
        var avDraft = await _requirementsService.CreateAsync(
            "REQ-AV-001", "The avionics bay shall maintain thermal limits under all operating conditions.", "avionics", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(avDraft.Id);
        await _requirementsService.MoveToGroupAsync(avDraft.Id, avionicsGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetPriorityAsync(avDraft.Id, RequirementPriority.Medium, cancellationToken).ConfigureAwait(false);

        var avReviewed = await _requirementsService.CreateAsync(
            "REQ-AV-002", "The avionics bay cooling system shall derive from the thermal limits requirement.", "avionics", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(avReviewed.Id);
        await _requirementsService.MoveToGroupAsync(avReviewed.Id, avionicsGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(avReviewed.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
        await _requirementsService.LinkAsync(avReviewed.Id, avDraft.Id, RequirementRelationshipKinds.DerivesFrom, cancellationToken).ConfigureAwait(false);

        var avApproved = await _requirementsService.CreateAsync(
            "REQ-AV-003", "The system shall log every avionics fault code for post-flight analysis.", "avionics", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(avApproved.Id);
        await _requirementsService.MoveToGroupAsync(avApproved.Id, avionicsGroup.Id, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(avApproved.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetStatusAsync(avApproved.Id, RequirementStatus.Approved, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetPriorityAsync(avApproved.Id, RequirementPriority.High, cancellationToken).ConfigureAwait(false);
        await _requirementsService.SetOwnerAsync(avApproved.Id, SampleIdentityId, cancellationToken).ConfigureAwait(false);

        // ---- IRequirement.IsDeleted, exercised directly: a superseded requirement, soft-deleted ----
        var deprecated = await _requirementsService.CreateAsync(
            "REQ-AV-999", "Deprecated avionics requirement, retained only to demonstrate soft delete.", "avionics", cancellationToken)
            .ConfigureAwait(false);
        requirementIds.Add(deprecated.Id);
        await _requirementsService.DeleteAsync(deprecated.Id, cancellationToken).ConfigureAwait(false);
        DeletedRequirementId = deprecated.Id;

        // ---- Requirement Collections ("Requirement Sets") ----
        var structuralCollection = await _requirementsService.CreateCollectionAsync("Structural Requirements Baseline", cancellationToken).ConfigureAwait(false);
        StructuralCollectionId = structuralCollection.Id;
        foreach (var id in new[] { strDraft.Id, strReviewed.Id, strApproved.Id, strAllocated.Id, strVerified.Id, strSatisfied.Id })
            await _requirementsService.AddToCollectionAsync(structuralCollection.Id, id, cancellationToken).ConfigureAwait(false);

        var avionicsCollection = await _requirementsService.CreateCollectionAsync("Avionics Requirements Review Package", cancellationToken).ConfigureAwait(false);
        AvionicsCollectionId = avionicsCollection.Id;
        foreach (var id in new[] { avDraft.Id, avReviewed.Id, avApproved.Id })
            await _requirementsService.AddToCollectionAsync(avionicsCollection.Id, id, cancellationToken).ConfigureAwait(false);

        // ---- Import/Export (WP 9.1A) ----
        _importService.RegisterImportable(new RequirementCollectionExportAdapter(_requirementsService, ExportAdapterKind, structuralCollection.Id));

        AllSampleRequirementIds = requirementIds;
        HasRegistered = true;
    }
}
