using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module demonstrating `WP 9.3A`'s
/// Verification Management Workspace — real, representative Verification
/// Activities and recorded results for the Engineering Workspace's own
/// Verification area, Engineering Cockpit KPIs, and Digital Thread to
/// present, per this Work Package's own explicit "meaningful engineering
/// data rather than placeholders" requirement (the same requirement every
/// prior real-discipline Work Package already established this precedent
/// for).
/// </summary>
/// <remarks>
/// <para>
/// Builds four real <see cref="VerificationActivity"/> Domain objects — one
/// per named method this Work Package's own "Representative Data" section
/// lists (Inspection, Analysis, Test, Demonstration) — deliberately
/// covering every Engineering Cockpit KPI bucket
/// (<c>EngineeringCockpit.VerificationKpiCards</c>) with a real, honest
/// example: an Inspection activity verifying the real Mechanical Shared
/// Fastener Component, left <c>InReview</c> with no recorded result ("In
/// Progress"/"Outstanding"); an Analysis activity verifying a real
/// Requirement, with a recorded <see cref="VerificationOutcome.Pass"/>
/// result linking the real, already-executed Beam Bending Stress
/// Calculation record (found by relationship query, never fabricated) and
/// referencing the real sample Material ("Passed"/"Verification
/// Coverage"); a Test activity verifying the real Mechanical Wing
/// Assembly, with a recorded <see cref="VerificationOutcome.Fail"/> result
/// referencing the real Documents sample's own Test Report — a genuine,
/// disclosed "Outstanding"/<c>Blocked</c>-health demonstration, mirroring
/// `WP 9.2A`'s own honest <c>Conditional</c> precedent, never hidden; a
/// Demonstration activity left in <c>Draft</c> ("Planned," zero records —
/// the honest, un-executed baseline).
/// </para>
/// <para>
/// <b>Digital Thread cross-links, using only already-mapped relationship
/// kinds</b> (<c>"verifiedBy"</c>/<c>"references"</c>/<c>"basedOnCalculation"</c>,
/// all already <see cref="RelationshipCategory.Verification"/>/<c>.Reference</c>/
/// <c>.Calculation</c> since `WP 8.2A`/`WP 8.2B`): each subject
/// (Component/Requirement/Assembly) is linked <c>"verifiedBy"</c> to its
/// own Activity — the identical mechanism
/// <see cref="RequirementsWorkspaceSampleModule"/>'s own directly-against-
/// a-Requirement recording already establishes, reused one link-hop
/// earlier; <see cref="IVerificationService.RecordAsync"/> itself then
/// links each Activity <c>"verifiedBy"</c> its own recorded
/// <see cref="IVerificationRecord"/>, giving a real, two-hop Subject →
/// Activity → Record chain. The Inspection activity additionally
/// <c>"references"</c> the base sample's own already-existing live Risk
/// (queried, never duplicated); the Analysis activity <c>"references"</c>
/// the Documents sample's own live Decision — together with the
/// Calculation/Material/Document links above, covering all eight Digital
/// Thread nodes this Work Package's own scope lists (Requirements/
/// Verification/Calculations/Mechanical/Materials/Risks/Decisions/
/// Documents).
/// </para>
/// <para>
/// <b>A disclosed, deliberate fifth cross-sample-module dependency:</b>
/// constructor-injects <see cref="MechanicalProductStructureSampleModule"/>,
/// <see cref="RequirementsWorkspaceSampleModule"/>,
/// <see cref="EngineeringCalculationsWorkspaceSampleModule"/>, and
/// <see cref="EngineeringDocumentsWorkspaceSampleModule"/> directly,
/// mirroring <see cref="EngineeringDocumentsWorkspaceSampleModule"/>'s own
/// already-established precedent, extended by one; plus one further,
/// disclosed query-not-inject edge (the base
/// <see cref="EngineeringDomainSampleModule"/>'s own already-live Risk,
/// queried by Kind, mirroring `WP 9.4A`'s own identical precedent). Safe
/// for the identical reason: <see cref="ModuleServiceCollectionExtensions.AddDiscoveredModules"/>
/// registers every discovered module type as a DI singleton, and
/// <c>ModuleLifecycleManager</c> initialises modules in ordinal Id order —
/// <c>tempest.samples.engineeringdomain</c>, then
/// <c>tempest.samples.mechanicalproductstructure</c>, then
/// <c>tempest.samples.requirementsworkspace</c>, then
/// <c>tempest.samples.workspacecalculations</c>, then
/// <c>tempest.samples.workspacedocuments</c>, then this module's own
/// <c>tempest.samples.workspaceverification</c> sort in exactly that
/// order, so every dependency's own Id is already populated by the time
/// this module's own <see cref="InitialiseAsync"/> runs.
/// </para>
/// <para>
/// Builds its own <see cref="EngineeringObjectFactory{T}"/> instance
/// directly, in its own composition root — never through
/// <c>Tempest.App.Workspace.Verification.VerificationActivityFactoryRegistry</c>,
/// which lives in <c>Tempest.App</c> (never referenced by this project),
/// mirroring <see cref="MechanicalProductStructureSampleModule"/>'s own
/// identical, disclosed precedent.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.workspaceverification", "Verification Workspace Sample", "1.0.0")]
public sealed class EngineeringVerificationWorkspaceSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.verificationworkspace-user";

    private readonly IIdentityService _identityService;
    private readonly EngineeringDomainContext _context;
    private readonly IVerificationService _verificationService;
    private readonly IRequirementsService _requirementsService;
    private readonly MechanicalProductStructureSampleModule _mechanicalSampleModule;
    private readonly RequirementsWorkspaceSampleModule _requirementsSampleModule;
    private readonly EngineeringCalculationsWorkspaceSampleModule _calculationsSampleModule;
    private readonly EngineeringDocumentsWorkspaceSampleModule _documentsSampleModule;

    /// <summary>Initialises a new instance of the <see cref="EngineeringVerificationWorkspaceSampleModule"/> class.</summary>
    public EngineeringVerificationWorkspaceSampleModule(
        IIdentityService identityService,
        EngineeringDomainContext context,
        IVerificationService verificationService,
        IRequirementsService requirementsService,
        MechanicalProductStructureSampleModule mechanicalSampleModule,
        RequirementsWorkspaceSampleModule requirementsSampleModule,
        EngineeringCalculationsWorkspaceSampleModule calculationsSampleModule,
        EngineeringDocumentsWorkspaceSampleModule documentsSampleModule)
        : base("tempest.samples.workspaceverification", "Verification Workspace Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(verificationService);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(mechanicalSampleModule);
        ArgumentNullException.ThrowIfNull(requirementsSampleModule);
        ArgumentNullException.ThrowIfNull(calculationsSampleModule);
        ArgumentNullException.ThrowIfNull(documentsSampleModule);

        _identityService = identityService;
        _context = context;
        _verificationService = verificationService;
        _requirementsService = requirementsService;
        _mechanicalSampleModule = mechanicalSampleModule;
        _requirementsSampleModule = requirementsSampleModule;
        _calculationsSampleModule = calculationsSampleModule;
        _documentsSampleModule = documentsSampleModule;
    }

    public Guid? InspectionActivityId { get; private set; }
    public Guid? AnalysisActivityId { get; private set; }
    public Guid? TestActivityId { get; private set; }
    public Guid? DemonstrationActivityId { get; private set; }
    public IReadOnlyList<Guid> AllSampleActivityIds { get; private set; } = [];
    public bool HasRegistered { get; private set; }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        var activityIds = new List<Guid>();

        // ---- Inspection: verifies the real Mechanical Shared Fastener Component; InReview, no result yet ----
        var inspection = await CreateActivityAsync(
            "Wing Attach Bolt Inspection", "Inspection",
            _mechanicalSampleModule.SharedFastenerComponentId ?? Guid.Empty,
            "Fictional sample Inspection activity — for demonstration only.", cancellationToken).ConfigureAwait(false);
        activityIds.Add(inspection.Id);
        InspectionActivityId = inspection.Id;
        await inspection.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        if (_mechanicalSampleModule.SharedFastenerComponentId is { } fastenerComponentId)
            await LinkSubjectToActivityAsync(fastenerComponentId, inspection.Id, cancellationToken).ConfigureAwait(false);

        var existingRisks = await _context.Repository.ListByKindAsync("Risk", cancellationToken).ConfigureAwait(false);
        if (existingRisks.FirstOrDefault(r => r is not IDeletable { IsDeleted: true }) is { } existingRisk)
            await inspection.LinkAsync(existingRisk.Id, "references", cancellationToken).ConfigureAwait(false);

        // ---- Analysis: verifies a real Requirement; recorded Pass, linking the real executed Beam Calculation record and referencing the real Material ----
        var analysis = await CreateActivityAsync(
            "Wing Spar Bending Analysis Verification", "Analysis",
            _requirementsSampleModule.AllSampleRequirementIds.Count > 2 ? _requirementsSampleModule.AllSampleRequirementIds[2] : Guid.Empty,
            "Fictional sample Analysis activity — for demonstration only.", cancellationToken).ConfigureAwait(false);
        activityIds.Add(analysis.Id);
        AnalysisActivityId = analysis.Id;
        await analysis.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await analysis.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);

        if (_requirementsSampleModule.AllSampleRequirementIds.Count > 2)
            await _requirementsService.LinkAsync(_requirementsSampleModule.AllSampleRequirementIds[2], analysis.Id, "verifiedBy", cancellationToken).ConfigureAwait(false);

        var analysisContext = new VerificationContext();
        analysisContext.RecordCriterion("Applied bending stress remains below the material's own allowable stress.", isSatisfied: true);
        analysisContext.RecordEvidence("Fictional sample Beam Bending Stress Calculation output — for demonstration only.");

        if (_calculationsSampleModule.BeamCalculationId is { } beamCalculationId)
        {
            var beamRecordLinks = await _context.RelationshipRepository.GetOutgoingAsync(beamCalculationId, cancellationToken).ConfigureAwait(false);
            var beamRecordId = beamRecordLinks.FirstOrDefault(r => string.Equals(r.RelationshipKind, "calculatedBy", StringComparison.Ordinal))?.TargetId;
            if (beamRecordId is { } recordId)
                analysisContext.LinkCalculationRecord(recordId);
        }

        analysisContext.ReferenceMaterial(MaterialsSampleModule.SampleMaterialId);

        await _verificationService.RecordAsync(analysis.Id, VerificationOutcome.Pass, "Analysis", analysisContext, cancellationToken).ConfigureAwait(false);

        if (_documentsSampleModule.DecisionId is { } decisionId)
            await analysis.LinkAsync(decisionId, "references", cancellationToken).ConfigureAwait(false);

        // ---- Test: verifies the real Mechanical Wing Assembly; recorded Fail, referencing the real Documents sample's own Test Report ----
        var test = await CreateActivityAsync(
            "Wing Spar Static Test Verification", "Test",
            _mechanicalSampleModule.WingAssemblyId ?? Guid.Empty,
            "Fictional sample Test activity — for demonstration only.", cancellationToken).ConfigureAwait(false);
        activityIds.Add(test.Id);
        TestActivityId = test.Id;
        await test.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);

        if (_mechanicalSampleModule.WingAssemblyId is { } wingAssemblyId)
            await LinkSubjectToActivityAsync(wingAssemblyId, test.Id, cancellationToken).ConfigureAwait(false);

        var testContext = new VerificationContext();
        testContext.RecordCriterion("Wing spar withstood ultimate load without failure.", isSatisfied: false, "Fictional sample failure mode — for demonstration only, not a real engineering finding.");
        testContext.RecordEvidence("Fictional sample static test report — for demonstration only.", "static-test-report.pdf");

        if (_documentsSampleModule.TestReportId is { } testReportId)
            testContext.LinkDocument(testReportId);

        await _verificationService.RecordAsync(test.Id, VerificationOutcome.Fail, "Test", testContext, cancellationToken).ConfigureAwait(false);

        // ---- Demonstration: left Draft, zero records - the honest, un-executed "Planned" baseline ----
        var demonstration = await CreateActivityAsync(
            "Avionics Bay Cooling Demonstration", "Demonstration",
            _requirementsSampleModule.AllSampleRequirementIds.Count > 6 ? _requirementsSampleModule.AllSampleRequirementIds[6] : Guid.Empty,
            "Fictional sample Demonstration activity — for demonstration only.", cancellationToken).ConfigureAwait(false);
        activityIds.Add(demonstration.Id);
        DemonstrationActivityId = demonstration.Id;

        AllSampleActivityIds = activityIds;
        HasRegistered = true;
    }

    private async Task<VerificationActivity> CreateActivityAsync(
        string displayName, string method, Guid subjectId, string initialContent, CancellationToken cancellationToken)
    {
        var factory = new EngineeringObjectFactory<VerificationActivity>(
            "VerificationActivity", _context, (doc, rev) => new VerificationActivity(
                doc, rev, _context, displayName, EngineeringObjectMetadata.Empty, subjectId, method));

        return (VerificationActivity)await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Links a real Mechanical Domain subject to its own Verification Activity via <c>"verifiedBy"</c> (subject is verified by the activity) — the same relationship kind <see cref="IVerificationService.RecordAsync"/> itself uses one link-hop later, from Activity to Record.</summary>
    private async Task LinkSubjectToActivityAsync(Guid subjectId, Guid activityId, CancellationToken cancellationToken)
    {
        var subject = await _context.Repository.FindAsync(subjectId, cancellationToken).ConfigureAwait(false);
        if (subject is IHasRelationships subjectLinks)
            await subjectLinks.LinkAsync(activityId, "verifiedBy", cancellationToken).ConfigureAwait(false);
    }
}
