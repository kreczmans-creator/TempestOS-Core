using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Verification;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module demonstrating `WP 9.5A`'s
/// Manufacturing Workspace — real, representative Manufacturing Operations,
/// a Routing sequence, a Supplier Operation, Tooling/Fixture documentation,
/// a Work Instruction, and a recorded Inspection result for the Engineering
/// Workspace's own Manufacturing area, Engineering Cockpit KPIs, and
/// Digital Thread to present, per this Work Package's own explicit
/// "meaningful engineering data rather than placeholders" requirement (the
/// same requirement every prior real-discipline Work Package already
/// established this precedent for).
/// </summary>
/// <remarks>
/// <para>
/// Builds one real <see cref="ManufacturingOperation"/> Routing container
/// (<c>Classification = "Routing"</c>, `ADR-0091`) with three real,
/// sequenced <see cref="ManufacturingOperation"/> steps
/// (<c>Classification = "Operation"</c>) as its own <see cref="IHasParent"/>
/// children, ordered via <see cref="IHasBomLine.SetBomLineAsync"/>'s own
/// <c>itemNumber</c> ("1"/"2"/"3") — the identical "ItemNumber as sibling
/// sequence" convention <c>MechanicalProductStructureNodeProvider.OrderForBom</c>
/// already establishes, reused, not reinvented (`ADR-0091`). Each step's
/// own real <see cref="IManufacturingOperation.PartId"/> plus a
/// <c>"references"</c> link connect it to a real Mechanical Part in turn:
/// step 1 to the Wing Assembly, step 2 to the Spar Web Plate, step 3 to the
/// Shared Fastener Component.
/// </para>
/// <para>
/// One further real <see cref="ManufacturingOperation"/>
/// (<c>Classification = "Supplier Operation"</c>) is <c>"manufacturedBy"</c>-linked
/// to the base <see cref="EngineeringDomainSampleModule"/>'s own already-live
/// Supplier (queried by Kind, never duplicated — the identical "query, not
/// inject" precedent `WP 9.3A`/`WP 9.4A` already establish for that
/// module's own un-exposed sample objects). One Tooling and one Fixture
/// plain <c>"Document"</c> (`WP 9.5A`'s own extension of
/// <c>DocumentObjectFactoryRegistry</c>'s <c>Classification</c> taxonomy,
/// `ADR-0088`) complete the named Resource/Tooling/Fixture set.
/// </para>
/// <para>
/// One real <see cref="WorkInstruction"/> is <c>"documentedBy"</c>-linked
/// from the Routing's own first step (the Wing Assembly operation) — the
/// identical subject-as-source direction
/// <see cref="EngineeringDomainSampleModule"/>'s own Assembly→Drawing
/// precedent and `WP 9.4A`'s own Assembly/Part→Drawing precedent already
/// establish. One real <see cref="Inspection"/> (Kind <c>"Inspection"</c>,
/// a <see cref="VerificationActivity"/> subtype, `WP 8.2C`, confirmed never
/// instantiated anywhere before this Work Package) verifies the same first
/// step (<c>"verifiedBy"</c>, subject-as-source, mirroring `WP 9.3A`'s own
/// identical convention) and is left with a real, recorded
/// <see cref="VerificationOutcome.Pass"/> result via
/// <see cref="IVerificationService.RecordAsync"/> directly — the same
/// Domain-level service `WP 9.3A`'s own sample module already calls, never
/// <c>Tempest.App.Workspace.Verification.RecordVerificationResultCommand</c>
/// itself, which lives in <c>Tempest.App</c> and is never referenced by
/// this project (the same direction-of-dependency boundary every prior
/// sample module's own remarks already disclose). The Inspection's own
/// recorded evidence references the Documents sample's own real Test
/// Report.
/// </para>
/// <para>
/// <b>Digital Thread cross-links, using only already-mapped relationship
/// kinds</b> (<c>"references"</c>/<c>"manufacturedBy"</c>/<c>"documentedBy"</c>/
/// <c>"verifiedBy"</c>, all already categorised in <c>RelationshipKindCategoryMap</c>
/// since `WP 8.2A`/`WP 8.2B`/`WP 8.2C`): the Routing itself
/// <c>"references"</c> a real Requirement (Requirements↔Manufacturing) and
/// the Spar Web Plate step <c>"references"</c> the real, already-executed
/// Beam Bending Stress Calculation (Calculations↔Manufacturing) — together
/// with the Part links, Work Instruction, and Inspection above, covering
/// all six Digital Thread nodes this Work Package's own scope lists
/// (Requirements/Mechanical/Calculations/Verification/Documents/Manufacturing).
/// </para>
/// <para>
/// <b>A disclosed, deliberate fourth cross-sample-module dependency —
/// never a fifth:</b> constructor-injects
/// <see cref="MechanicalProductStructureSampleModule"/>,
/// <see cref="RequirementsWorkspaceSampleModule"/>,
/// <see cref="EngineeringCalculationsWorkspaceSampleModule"/>, and
/// <see cref="EngineeringDocumentsWorkspaceSampleModule"/> directly — the
/// same four <see cref="EngineeringDocumentsWorkspaceSampleModule"/> itself
/// already establishes, extended by none, mirrored exactly. Deliberately
/// <em>not</em> <see cref="EngineeringVerificationWorkspaceSampleModule"/>:
/// this module builds its own Inspection directly rather than reusing
/// anything from that module's own sample data, and — decisively — that
/// module's own id (<c>tempest.samples.workspaceverification</c>) sorts
/// <em>after</em> this module's own id
/// (<c>tempest.samples.workspacemanufacturing</c>), so a constructor
/// dependency on it would be a genuine <see cref="ModuleLifecycleManager"/>
/// ordinal-initialisation-order defect, not merely an unnecessary one —
/// checked and disclosed, not assumed. Safe for the identical reason every
/// prior sample module's own remarks already disclose:
/// <see cref="ModuleServiceCollectionExtensions.AddDiscoveredModules"/>
/// registers every discovered module type as a DI singleton, and
/// <c>ModuleLifecycleManager</c> initialises modules in ordinal Id order —
/// <c>tempest.samples.engineeringdomain</c>, then
/// <c>tempest.samples.mechanicalproductstructure</c>, then
/// <c>tempest.samples.requirementsworkspace</c>, then
/// <c>tempest.samples.workspacecalculations</c>, then
/// <c>tempest.samples.workspacedocuments</c>, then this module's own
/// <c>tempest.samples.workspacemanufacturing</c> sort in exactly that
/// order (each strictly before <c>tempest.samples.workspaceverification</c>
/// and <c>tempest.samples.manufacturing-workspace-explorer</c> alike — the
/// latter sorts before every <c>workspace*</c> id, including this one,
/// checked directly, but carries no dependency on this module either), so
/// every dependency's own Id is already populated by the time this
/// module's own <see cref="InitialiseAsync"/> runs.
/// </para>
/// <para>
/// Builds its own <see cref="EngineeringObjectFactory{T}"/> instances
/// directly, in its own composition root — never through
/// <c>Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry</c>,
/// which lives in <c>Tempest.App</c> (never referenced by this project),
/// mirroring <see cref="EngineeringVerificationWorkspaceSampleModule"/>'s
/// own identical, disclosed precedent. The <c>"Resource"</c>/<c>"Tooling"</c>/
/// <c>"Fixture"</c>/<c>"Routing"</c>/<c>"Operation"</c>/<c>"Supplier Operation"</c>
/// <see cref="EngineeringObjectMetadata.Classification"/> literals below
/// must match <c>DocumentObjectFactoryRegistry"</c>'s/<c>ManufacturingObjectFactoryRegistry</c>'s
/// own identically-named constants exactly, duplicated here rather than
/// referenced, the same disclosed boundary
/// <see cref="EngineeringDocumentsWorkspaceSampleModule"/>'s own remarks
/// already establish.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.workspacemanufacturing", "Manufacturing Workspace Sample", "1.0.0")]
public sealed class EngineeringManufacturingWorkspaceSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.manufacturingworkspace-user";

    private const string Routing = "Routing";
    private const string Operation = "Operation";
    private const string SupplierOperation = "Supplier Operation";
    private const string Tooling = "Tooling";
    private const string Fixture = "Fixture";

    private readonly IIdentityService _identityService;
    private readonly EngineeringDomainContext _context;
    private readonly IVerificationService _verificationService;
    private readonly MechanicalProductStructureSampleModule _mechanicalSampleModule;
    private readonly RequirementsWorkspaceSampleModule _requirementsSampleModule;
    private readonly EngineeringCalculationsWorkspaceSampleModule _calculationsSampleModule;
    private readonly EngineeringDocumentsWorkspaceSampleModule _documentsSampleModule;

    /// <summary>Initialises a new instance of the <see cref="EngineeringManufacturingWorkspaceSampleModule"/> class.</summary>
    public EngineeringManufacturingWorkspaceSampleModule(
        IIdentityService identityService,
        EngineeringDomainContext context,
        IVerificationService verificationService,
        MechanicalProductStructureSampleModule mechanicalSampleModule,
        RequirementsWorkspaceSampleModule requirementsSampleModule,
        EngineeringCalculationsWorkspaceSampleModule calculationsSampleModule,
        EngineeringDocumentsWorkspaceSampleModule documentsSampleModule)
        : base("tempest.samples.workspacemanufacturing", "Manufacturing Workspace Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(verificationService);
        ArgumentNullException.ThrowIfNull(mechanicalSampleModule);
        ArgumentNullException.ThrowIfNull(requirementsSampleModule);
        ArgumentNullException.ThrowIfNull(calculationsSampleModule);
        ArgumentNullException.ThrowIfNull(documentsSampleModule);

        _identityService = identityService;
        _context = context;
        _verificationService = verificationService;
        _mechanicalSampleModule = mechanicalSampleModule;
        _requirementsSampleModule = requirementsSampleModule;
        _calculationsSampleModule = calculationsSampleModule;
        _documentsSampleModule = documentsSampleModule;
    }

    public Guid? RoutingId { get; private set; }
    public Guid? WingAssemblyOperationId { get; private set; }
    public Guid? SparWebPlateOperationId { get; private set; }
    public Guid? SharedFastenerOperationId { get; private set; }
    public Guid? SupplierOperationId { get; private set; }
    public Guid? ToolingDocumentId { get; private set; }
    public Guid? FixtureDocumentId { get; private set; }
    public Guid? WorkInstructionId { get; private set; }
    public Guid? InspectionId { get; private set; }
    public IReadOnlyList<Guid> AllSampleObjectIds { get; private set; } = [];
    public bool HasRegistered { get; private set; }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        var objectIds = new List<Guid>();

        // ---- Routing: a Classification="Routing" container, ADR-0091 ----
        var routing = await CreateOperationAsync(
            "Wing Spar Assembly Routing", Routing, _mechanicalSampleModule.WingAssemblyId ?? Guid.Empty,
            "Fictional sample Manufacturing Routing — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(routing.Id);
        RoutingId = routing.Id;

        if (_requirementsSampleModule.AllSampleRequirementIds.Count > 4)
            await routing.LinkAsync(_requirementsSampleModule.AllSampleRequirementIds[4], "references", cancellationToken).ConfigureAwait(false);

        // ---- Step 1: verifies/references the real Wing Assembly ----
        var step1 = await CreateOperationAsync(
            "Wing Assembly Fit-Up", Operation, _mechanicalSampleModule.WingAssemblyId ?? Guid.Empty,
            "Fictional sample Manufacturing Operation — for demonstration only.", cancellationToken, routing.Id).ConfigureAwait(false);
        objectIds.Add(step1.Id);
        WingAssemblyOperationId = step1.Id;
        await step1.SetBomLineAsync(1m, "EA", itemNumber: "1", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.WingAssemblyId is { } wingAssemblyId)
            await step1.LinkAsync(wingAssemblyId, "references", cancellationToken).ConfigureAwait(false);
        await step1.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);

        // ---- Step 2: verifies/references the real Spar Web Plate; also references the real Beam Calculation ----
        var step2 = await CreateOperationAsync(
            "Spar Web Plate Machining", Operation, _mechanicalSampleModule.SparWebPlateId ?? Guid.Empty,
            "Fictional sample Manufacturing Operation — for demonstration only.", cancellationToken, routing.Id).ConfigureAwait(false);
        objectIds.Add(step2.Id);
        SparWebPlateOperationId = step2.Id;
        await step2.SetBomLineAsync(1m, "EA", itemNumber: "2", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.SparWebPlateId is { } sparWebPlateId)
            await step2.LinkAsync(sparWebPlateId, "references", cancellationToken).ConfigureAwait(false);
        if (_calculationsSampleModule.BeamCalculationId is { } beamCalculationId)
            await step2.LinkAsync(beamCalculationId, "references", cancellationToken).ConfigureAwait(false);
        await step2.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await step2.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        await step2.TransitionAsync(LifecycleState.Released, cancellationToken).ConfigureAwait(false);

        // ---- Step 3: verifies/references the real Shared Fastener Component — left Draft, the honest, un-started "Open" baseline ----
        var step3 = await CreateOperationAsync(
            "Shared Fastener Installation", Operation, _mechanicalSampleModule.SharedFastenerComponentId ?? Guid.Empty,
            "Fictional sample Manufacturing Operation — for demonstration only.", cancellationToken, routing.Id).ConfigureAwait(false);
        objectIds.Add(step3.Id);
        SharedFastenerOperationId = step3.Id;
        await step3.SetBomLineAsync(4m, "EA", itemNumber: "3", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.SharedFastenerComponentId is { } fastenerComponentId)
            await step3.LinkAsync(fastenerComponentId, "references", cancellationToken).ConfigureAwait(false);

        // ---- Supplier Operation: manufacturedBy the base sample's own real, already-live Supplier (queried, not duplicated) ----
        var supplierOperation = await CreateOperationAsync(
            "Fastener Kit Supplier Operation", SupplierOperation, _mechanicalSampleModule.SharedFastenerComponentId ?? Guid.Empty,
            "Fictional sample Supplier Operation — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(supplierOperation.Id);
        SupplierOperationId = supplierOperation.Id;

        var existingSuppliers = await _context.Repository.ListByKindAsync("Supplier", cancellationToken).ConfigureAwait(false);
        if (existingSuppliers.FirstOrDefault(s => s is not IDeletable { IsDeleted: true }) is { } existingSupplier)
            await supplierOperation.LinkAsync(existingSupplier.Id, "manufacturedBy", cancellationToken).ConfigureAwait(false);

        // ---- Tooling / Fixture: plain "Document" objects, WP 9.5A's own Classification extension ----
        var tooling = await CreateDocumentAsync(
            "Wing Spar Drill Jig", Tooling, "Fictional sample Tooling document — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(tooling.Id);
        ToolingDocumentId = tooling.Id;

        var fixture = await CreateDocumentAsync(
            "Wing Spar Assembly Fixture", Fixture, "Fictional sample Fixture document — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(fixture.Id);
        FixtureDocumentId = fixture.Id;

        // ---- Work Instruction: documentedBy the Routing's own first step ----
        var workInstructionFactory = new EngineeringObjectFactory<WorkInstruction>(
            "WorkInstruction", _context, (doc, rev) => new WorkInstruction(
                doc, rev, _context, "SAMPLE-WI-001", "Wing Assembly Fit-Up Work Instruction", EngineeringObjectMetadata.Empty, step1.Id));
        var workInstruction = (WorkInstruction)await workInstructionFactory.CreateAsync(
            "Fictional sample Work Instruction — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(workInstruction.Id);
        WorkInstructionId = workInstruction.Id;
        await step1.LinkAsync(workInstruction.Id, "documentedBy", cancellationToken).ConfigureAwait(false);

        // ---- Inspection: verifies the Routing's own first step; recorded Pass, referencing the real Documents sample's own Test Report ----
        var inspectionFactory = new EngineeringObjectFactory<Inspection>(
            "Inspection", _context, (doc, rev) => new Inspection(
                doc, rev, _context, "Wing Assembly Fit-Up Inspection", EngineeringObjectMetadata.Empty, step1.Id, "Inspection"));
        var inspection = (Inspection)await inspectionFactory.CreateAsync(
            "Fictional sample Inspection activity — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(inspection.Id);
        InspectionId = inspection.Id;
        await inspection.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await step1.LinkAsync(inspection.Id, "verifiedBy", cancellationToken).ConfigureAwait(false);

        var inspectionContext = new VerificationContext();
        inspectionContext.RecordCriterion("Wing Assembly fit-up meets the drawing's own tolerance band.", isSatisfied: true);
        inspectionContext.RecordEvidence("Fictional sample Inspection record — for demonstration only.");

        if (_documentsSampleModule.TestReportId is { } testReportId)
            inspectionContext.LinkDocument(testReportId);

        await _verificationService.RecordAsync(inspection.Id, VerificationOutcome.Pass, "Inspection", inspectionContext, cancellationToken).ConfigureAwait(false);

        AllSampleObjectIds = objectIds;
        HasRegistered = true;
    }

    private async Task<ManufacturingOperation> CreateOperationAsync(
        string displayName, string classification, Guid partId, string initialContent, CancellationToken cancellationToken, Guid? parentId = null)
    {
        var factory = new EngineeringObjectFactory<ManufacturingOperation>(
            "ManufacturingOperation", _context, (doc, rev) => new ManufacturingOperation(
                doc, rev, _context, identifier: null, displayName, new EngineeringObjectMetadata(Classification: classification), partId));

        var operation = (ManufacturingOperation)await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);

        if (parentId is { } target)
            await operation.MoveAsync(target, cancellationToken).ConfigureAwait(false);

        return operation;
    }

    private async Task<Document> CreateDocumentAsync(string displayName, string classification, string initialContent, CancellationToken cancellationToken)
    {
        var factory = new EngineeringObjectFactory<Document>(
            "Document", _context, (doc, rev) => new Document(
                doc, rev, _context, identifier: null, displayName, new EngineeringObjectMetadata(Classification: classification)));

        return (Document)await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);
    }
}
