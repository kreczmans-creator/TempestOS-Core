using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module demonstrating `WP 9.4A`'s
/// Engineering Documents Workspace — real, representative engineering
/// documentation for the Engineering Workspace's own Documents area,
/// Engineering Cockpit KPIs, and Digital Thread to present, per this Work
/// Package's own explicit "meaningful engineering data rather than
/// placeholders" requirement (the same requirement `WP 9.1A`/`WP 9.2A`
/// already established this precedent for).
/// </summary>
/// <remarks>
/// <para>
/// Builds nine real Document Domain objects — a General Arrangement Drawing
/// and a Detail Drawing (real <c>DrawingNumber</c>s, the Detail Drawing
/// structurally nested under the GA Drawing via <see cref="IHasParent.MoveAsync"/>,
/// demonstrating <c>DocumentsNodeProvider</c>'s own real-parent Explorer
/// nesting), a Specification, a Test Report (carries a real
/// <see cref="Attachment"/>), a Design Report, a Material Datasheet, a
/// Procedure, a Standard, and an External Reference — covering every named
/// Document type this Work Package's own scope lists, expanding on the six
/// the "Representative Data" section names by name, disclosed the same way
/// `WP 8.1C` disclosed its own scope expansion. Every Document beyond
/// <c>"Drawing"</c> is a plain <c>"Document"</c> distinguished by
/// <c>Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry</c>'s
/// own named <see cref="EngineeringObjectMetadata.Classification"/>
/// constants (`ADR-0088`) — never a new Domain Kind.
/// </para>
/// <para>
/// <b>Digital Thread cross-links, using only already-mapped relationship
/// kinds</b> (<c>"documentedBy"</c>/<c>"references"</c>, both already
/// <see cref="RelationshipCategory.Documentation"/>/<see cref="RelationshipCategory.Reference"/>
/// in <c>RelationshipKindCategoryMap</c> since `WP 8.2C`): the GA Drawing and
/// Detail Drawing are <c>"documentedBy"</c>-linked from the real Mechanical
/// sample data's own Wing Assembly/Spar Web Plate (mirroring the base
/// <see cref="EngineeringDomainSampleModule"/>'s own identical Drawing
/// precedent exactly); the Specification <c>"references"</c> a real
/// Requirement; the Test Report <c>"references"</c> the one real Requirement
/// with an actually-recorded Verification (<c>REQ-STR-005</c> — the closest
/// real, live Verification anchor this platform has: no concrete
/// <see cref="Tempest.Core.Verification.IVerificationResult"/>-equivalent
/// Domain object exists anywhere, a genuine, disclosed, pre-existing gap —
/// see this Work Package's own Technical Debt Assessment); the Design Report
/// <c>"references"</c> a real Calculation; the Material Datasheet
/// <c>"references"</c> the real Spar Web Plate Part (a genuine
/// Part↔material-documentation link, not a direct Material Domain object
/// reference — <see cref="EngineeringDomainSampleModule"/> exposes no public
/// Id for its own sample Material); the Procedure <c>"references"</c> the
/// base sample's own already-existing live Risk (queried by Kind, not
/// duplicated — <see cref="EngineeringDomainSampleModule"/> already creates
/// one <c>"SAMPLE-RISK-001"</c> Risk, exposing no public Id for it either).
/// One <see cref="Decision"/> (`WP 8.2C`, a real, already-compiled concrete
/// class instantiated by no sample module anywhere before this) is created
/// here and <c>"references"</c> the GA Drawing — needed to honour this Work
/// Package's own explicit "Documents ↔ Decisions" Digital Thread
/// requirement using zero Domain-layer change, disclosed as a deliberate,
/// minimal, in-scope addition.
/// </para>
/// <para>
/// <b>Deliberately left unlinked, disclosed:</b> the External Reference
/// document carries no Attachment and no relationship in either direction —
/// the Engineering Cockpit's own real "Missing Evidence" KPI
/// (<c>EngineeringCockpit.HasMissingEvidence</c>) needs at least one live,
/// honest example to report, never a fabricated count. Its own <c>Content</c>
/// holds a placeholder URI — no file/URL storage service exists anywhere in
/// this platform, a genuine, disclosed, pre-existing gap (see this Work
/// Package's own Technical Debt Assessment), not fixed here.
/// </para>
/// <para>
/// A mix of every named lifecycle status this Work Package's own controlling
/// instruction lists: Draft (Specification, External Reference), InReview
/// (Detail Drawing, Procedure), Approved (GA Drawing, Test Report,
/// Datasheet, Standard), Released (Design Report — the only object taken
/// through the full Draft→InReview→Approved→Released chain,
/// <see cref="LifecycleTransitionTable"/>'s own existing permitted-transition
/// rule, unmodified).
/// </para>
/// <para>
/// <b>A disclosed, deliberate fourth cross-sample-module dependency:</b>
/// constructor-injects <see cref="MechanicalProductStructureSampleModule"/>,
/// <see cref="RequirementsWorkspaceSampleModule"/>, and
/// <see cref="EngineeringCalculationsWorkspaceSampleModule"/> directly,
/// mirroring <see cref="EngineeringCalculationsWorkspaceSampleModule"/>'s
/// own already-established precedent for the first two, extended by one.
/// Safe for the identical reason: <see cref="ModuleServiceCollectionExtensions.AddDiscoveredModules"/>
/// registers every discovered module type as a DI singleton, and
/// <c>ModuleLifecycleManager</c> initialises modules in ordinal Id order —
/// <c>tempest.samples.engineeringdomain</c> (for the base sample's own live
/// Risk), then <c>tempest.samples.mechanicalproductstructure</c>, then
/// <c>tempest.samples.requirementsworkspace</c>, then
/// <c>tempest.samples.workspacecalculations</c>, then this module's own
/// <c>tempest.samples.workspacedocuments</c> sort in exactly that order, so
/// every dependency's own Id is already populated by the time this module's
/// own <see cref="InitialiseAsync"/> runs.
/// </para>
/// <para>
/// Builds its own <see cref="EngineeringObjectFactory{T}"/> instances
/// directly, in its own composition root — never through
/// <c>Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry</c>,
/// which lives in <c>Tempest.App</c> (never referenced by this project),
/// mirroring <see cref="MechanicalProductStructureSampleModule"/>'s own
/// identical, disclosed precedent.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.workspacedocuments", "Documents Workspace Sample", "1.0.0")]
public sealed class EngineeringDocumentsWorkspaceSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.documentsworkspace-user";

    /// <summary>
    /// This project (<c>Tempest.Samples</c>) is never referenced by
    /// <c>Tempest.App</c>'s dependants in the reverse direction (<c>Tempest.App</c>
    /// depends on <c>Tempest.Samples</c>, never the reverse — the same
    /// boundary <see cref="EngineeringCalculationsWorkspaceSampleModule"/>'s
    /// own <c>CalculatedByRelationshipKind</c> remarks already disclose), so
    /// these must match <c>Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry</c>'s
    /// own identically-named constants exactly (`ADR-0088`), duplicated here
    /// rather than referenced.
    /// </summary>
    private const string Specification = "Specification";
    private const string Report = "Report";
    private const string Procedure = "Procedure";
    private const string Standard = "Standard";
    private const string Datasheet = "Datasheet";
    private const string ExternalReferenceClassification = "External Reference";

    private readonly IIdentityService _identityService;
    private readonly EngineeringDomainContext _context;
    private readonly MechanicalProductStructureSampleModule _mechanicalSampleModule;
    private readonly RequirementsWorkspaceSampleModule _requirementsSampleModule;
    private readonly EngineeringCalculationsWorkspaceSampleModule _calculationsSampleModule;

    /// <summary>Initialises a new instance of the <see cref="EngineeringDocumentsWorkspaceSampleModule"/> class.</summary>
    public EngineeringDocumentsWorkspaceSampleModule(
        IIdentityService identityService,
        EngineeringDomainContext context,
        MechanicalProductStructureSampleModule mechanicalSampleModule,
        RequirementsWorkspaceSampleModule requirementsSampleModule,
        EngineeringCalculationsWorkspaceSampleModule calculationsSampleModule)
        : base("tempest.samples.workspacedocuments", "Documents Workspace Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mechanicalSampleModule);
        ArgumentNullException.ThrowIfNull(requirementsSampleModule);
        ArgumentNullException.ThrowIfNull(calculationsSampleModule);

        _identityService = identityService;
        _context = context;
        _mechanicalSampleModule = mechanicalSampleModule;
        _requirementsSampleModule = requirementsSampleModule;
        _calculationsSampleModule = calculationsSampleModule;
    }

    public Guid? GeneralArrangementDrawingId { get; private set; }
    public Guid? DetailDrawingId { get; private set; }
    public Guid? SpecificationId { get; private set; }
    public Guid? TestReportId { get; private set; }
    public Guid? DesignReportId { get; private set; }
    public Guid? MaterialDatasheetId { get; private set; }
    public Guid? ProcedureId { get; private set; }
    public Guid? StandardId { get; private set; }
    public Guid? ExternalReferenceId { get; private set; }
    public Guid? DecisionId { get; private set; }
    public IReadOnlyList<Guid> AllSampleDocumentIds { get; private set; } = [];
    public bool HasRegistered { get; private set; }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        var documentIds = new List<Guid>();

        // ---- General Arrangement Drawing: documentedBy the real Wing Assembly ----
        var gaDrawingFactory = new EngineeringObjectFactory<Drawing>(
            "Drawing", _context, (doc, rev) => new Drawing(
                doc, rev, _context, "SAMPLE-DOC-GA-001", "Aircraft General Arrangement Drawing", EngineeringObjectMetadata.Empty, "GA-1000"));
        var gaDrawing = (Drawing)await gaDrawingFactory.CreateAsync(
            "Fictional sample General Arrangement Drawing — for demonstration only.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(gaDrawing.Id);
        GeneralArrangementDrawingId = gaDrawing.Id;
        await gaDrawing.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await gaDrawing.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.WingAssemblyId is { } wingAssemblyId)
        {
            var wingAssembly = await _context.Repository.FindAsync(wingAssemblyId, cancellationToken).ConfigureAwait(false);
            if (wingAssembly is IHasRelationships wingAssemblyLinks)
                await wingAssemblyLinks.LinkAsync(gaDrawing.Id, "documentedBy", cancellationToken).ConfigureAwait(false);
        }

        // ---- Detail Drawing: nested under the GA Drawing, documentedBy the real Spar Web Plate ----
        var detailDrawingFactory = new EngineeringObjectFactory<Drawing>(
            "Drawing", _context, (doc, rev) => new Drawing(
                doc, rev, _context, "SAMPLE-DOC-DWG-002", "Spar Web Plate Detail Drawing", EngineeringObjectMetadata.Empty, "DWG-2001"));
        var detailDrawing = (Drawing)await detailDrawingFactory.CreateAsync(
            "Fictional sample Detail Drawing — for demonstration only.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(detailDrawing.Id);
        DetailDrawingId = detailDrawing.Id;
        await detailDrawing.MoveAsync(gaDrawing.Id, cancellationToken).ConfigureAwait(false);
        await detailDrawing.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.SparWebPlateId is { } sparWebPlateId)
        {
            var sparWebPlate = await _context.Repository.FindAsync(sparWebPlateId, cancellationToken).ConfigureAwait(false);
            if (sparWebPlate is IHasRelationships sparWebPlateLinks)
                await sparWebPlateLinks.LinkAsync(detailDrawing.Id, "documentedBy", cancellationToken).ConfigureAwait(false);
        }

        // ---- Specification: references a real Requirement ----
        var specification = await CreateDocumentAsync(
            "SPEC-100", "Wing Structural Design Specification", Specification,
            "Fictional sample Specification — for demonstration only.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(specification.Id);
        SpecificationId = specification.Id;
        if (_requirementsSampleModule.AllSampleRequirementIds.Count > 2)
            await specification.LinkAsync(_requirementsSampleModule.AllSampleRequirementIds[2], "references", cancellationToken).ConfigureAwait(false);

        // ---- Test Report: carries a real Attachment, references the one Requirement with a real recorded Verification ----
        var testReport = await CreateDocumentAsync(
            "RPT-TR-001", "Wing Spar Static Test Report", Report,
            "Fictional sample Test Report — for demonstration only.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(testReport.Id);
        TestReportId = testReport.Id;
        await testReport.AttachAsync(new Attachment("static-test-report.pdf", "application/pdf", 2_048_000), cancellationToken).ConfigureAwait(false);
        await testReport.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await testReport.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        if (_requirementsSampleModule.AllSampleRequirementIds.Count > 4)
            await testReport.LinkAsync(_requirementsSampleModule.AllSampleRequirementIds[4], "references", cancellationToken).ConfigureAwait(false);

        // ---- Design Report: references a real Calculation; the only object taken to Released ----
        var designReport = await CreateDocumentAsync(
            "RPT-DR-001", "Wing Spar Bending Design Report", Report,
            "Fictional sample Design Report — for demonstration only.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(designReport.Id);
        DesignReportId = designReport.Id;
        await designReport.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await designReport.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        await designReport.TransitionAsync(LifecycleState.Released, cancellationToken).ConfigureAwait(false);
        if (_calculationsSampleModule.BeamCalculationId is { } beamCalculationId)
            await designReport.LinkAsync(beamCalculationId, "references", cancellationToken).ConfigureAwait(false);

        // ---- Material Datasheet: references the real Spar Web Plate Part ----
        var datasheet = await CreateDocumentAsync(
            "DS-001", "Fictional Sample Alloy Material Datasheet", Datasheet,
            "Fictional sample Material Datasheet — for demonstration only.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(datasheet.Id);
        MaterialDatasheetId = datasheet.Id;
        await datasheet.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await datasheet.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.SparWebPlateId is { } sparWebPlateForDatasheetId)
            await datasheet.LinkAsync(sparWebPlateForDatasheetId, "references", cancellationToken).ConfigureAwait(false);

        // ---- Procedure: references the base sample's own already-existing live Risk (queried, never duplicated) ----
        var procedure = await CreateDocumentAsync(
            "PROC-001", "Wing Structural Inspection Procedure", Procedure,
            "Fictional sample Procedure — for demonstration only.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(procedure.Id);
        ProcedureId = procedure.Id;
        await procedure.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        var existingRisks = await _context.Repository.ListByKindAsync("Risk", cancellationToken).ConfigureAwait(false);
        if (existingRisks.FirstOrDefault(r => r is not IDeletable { IsDeleted: true }) is { } existingRisk)
            await procedure.LinkAsync(existingRisk.Id, "references", cancellationToken).ConfigureAwait(false);

        // ---- Standard: a fixed, external-body reference document ----
        var standard = await CreateDocumentAsync(
            "STD-AS9100", "AS9100 Quality Management Standard", Standard,
            "Fictional reference to AS9100 — for demonstration only, no real standard body content reproduced.", cancellationToken).ConfigureAwait(false);
        documentIds.Add(standard.Id);
        StandardId = standard.Id;
        await standard.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await standard.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        await procedure.LinkAsync(standard.Id, "references", cancellationToken).ConfigureAwait(false);

        // ---- External Reference: deliberately left with zero Attachments and zero relationships — the Cockpit's own real "Missing Evidence" example, disclosed above ----
        var externalReference = await CreateDocumentAsync(
            "EXT-001", "Aircraft Structural Design Handbook (External)", ExternalReferenceClassification,
            "external://vendor-portal/handbook/ASDH-7th-ed — placeholder URI; no file/URL storage service exists in this platform.",
            cancellationToken).ConfigureAwait(false);
        documentIds.Add(externalReference.Id);
        ExternalReferenceId = externalReference.Id;

        // ---- Decision: a real, already-compiled Domain concrete class (WP 8.2C), instantiated by no sample module anywhere before this ----
        var decisionFactory = new EngineeringObjectFactory<Decision>(
            "Decision", _context, (doc, rev) => new Decision(
                doc, rev, _context, "SAMPLE-DEC-001", "Baseline the General Arrangement Drawing configuration",
                EngineeringObjectMetadata.Empty, rationale: "Fictional sample rationale — the GA Drawing's own current revision is adopted as the Wing configuration baseline, for demonstration only."));
        var decision = (Decision)await decisionFactory.CreateAsync(
            "Fictional sample Decision — for demonstration only.", cancellationToken).ConfigureAwait(false);
        DecisionId = decision.Id;
        await decision.LinkAsync(gaDrawing.Id, "references", cancellationToken).ConfigureAwait(false);

        AllSampleDocumentIds = documentIds;
        HasRegistered = true;
    }

    private async Task<Document> CreateDocumentAsync(
        string identifier, string displayName, string classification, string initialContent, CancellationToken cancellationToken)
    {
        var factory = new EngineeringObjectFactory<Document>(
            "Document", _context, (doc, rev) => new Document(
                doc, rev, _context, identifier, displayName, new EngineeringObjectMetadata(Classification: classification)));

        return (Document)await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);
    }
}
