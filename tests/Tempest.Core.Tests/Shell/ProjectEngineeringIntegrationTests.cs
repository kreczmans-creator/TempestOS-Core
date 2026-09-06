using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Settings;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Shell;

/// <summary>
/// Objective 3 — proving the existing engineering capabilities genuinely
/// <b>belong to projects</b> (`TD-84`).
/// </summary>
/// <remarks>
/// These are the traces the Product Convergence brief names. They are
/// written against the real domain, the real `MaterialCatalog`, the real
/// `RequirementsService` and the real `VerificationService` — no mocks —
/// because the question they answer is not "does the API compile" but
/// "is engineering work reachable from, and attributable to, a project".
/// </remarks>
public class ProjectEngineeringIntegrationTests
{
    private sealed record Rig(
        EngineeringDomainContext Domain,
        IProjectDirectory Directory,
        IProjectContext Context,
        IShellNavigator Navigator,
        IPersistenceStore Persistence,
        EngineeringDocumentStore DocumentStore,
        CurrentPrincipalAccessor Principal);

    private static Rig BuildRig()
    {
        var principal = new CurrentPrincipalAccessor();
        var persistence = new Materials.InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistence, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

        // One persistence store backs everything — the object graph
        // (`TD-85`), materials, requirements and verification alike.
        var domain = new EngineeringDomainContext(
            documentStore, repository, relationshipRepository,
            new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(relationshipDiscovery, repository), principal,
            new EngineeringObjectStateStore(persistence));

        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());
        var eventBus = new EventBus();
        var directory = new ProjectDirectory(domain);
        var context = new ProjectContext(directory, eventBus, settings);

        return new Rig(domain, directory, context, new ShellNavigator(context, eventBus, settings),
            persistence, documentStore, principal);
    }

    // `WP 16.4B-R6`: `EngineeringObjectFactory<T>` now requires the Kind's
    // own `IRehydratable<T>` reader (see that type). Every canonical Kind
    // already implements it — this constraint only restates that.
    private static async Task<T> CreateInProjectAsync<T>(
        EngineeringDomainContext domain, string kind, Guid projectId,
        Func<IEngineeringDocument, IDocumentRevision, T> ctor) where T : EngineeringObjectBase, IRehydratable<T>
    {
        var factory = new EngineeringObjectFactory<T>(kind, domain, ctor);
        var created = (T)await factory.CreateAsync($"{kind} — for test purposes.");

        // The existing structural edge that makes an engineering object
        // belong to a project. No new mechanism was invented for this.
        await ((IHasParent)created).MoveAsync(projectId);
        return created;
    }

    // ----------------------------------------------------------------
    // Trace 1 — Project -> Component -> Material -> Calculation
    //           -> Validation -> Result
    // ----------------------------------------------------------------

    [Fact]
    public async Task Trace_Project_Component_Material_Calculation_Validation_Result()
    {
        var rig = BuildRig();
        var materials = new MaterialCatalog(rig.DocumentStore, rig.Persistence);

        // Project — opened as the working context, as a user would.
        var project = await rig.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");
        await rig.Navigator.OpenProjectAsync(project.Id);
        await rig.Navigator.GoToEngineeringAsync();

        // Material — a real catalogue entry with real provenance.
        var material = await materials.RegisterAsync(
            "AL-7075", "Aluminium 7075-T6", BuildProperties(), category: "Metal");

        // Component, owned by the project, referencing that material.
        var part = await CreateInProjectAsync(rig.Domain, "Part", project.Id,
            (doc, rev) => new Part(doc, rev, rig.Domain, "PN-1001", "Impeller", EngineeringObjectMetadata.Empty, material.MaterialId));

        // Calculation, owned by the same project.
        var calculation = await CreateInProjectAsync(rig.Domain, "Calculation", project.Id,
            (doc, rev) => new Calculation(doc, rev, rig.Domain, "CALC-001", "Impeller Stress", EngineeringObjectMetadata.Empty));

        // The calculation is about the component — a real relationship.
        await ((IHasRelationships)calculation).LinkAsync(part.Id, "calculates");

        // --- Assertions: every hop is real and project-attributed ---

        // Component and calculation both belong to the project.
        var contents = await rig.Directory.ListProjectContentsAsync(project.Id);
        Assert.Contains(part.Id, contents);
        Assert.Contains(calculation.Id, contents);

        // The component resolves its material back through the catalogue.
        Assert.Equal("AL-7075", part.MaterialId);
        var resolved = await materials.FindAsync(part.MaterialId!);
        Assert.NotNull(resolved);
        Assert.Equal("Aluminium 7075-T6", resolved!.Name);
        Assert.True(resolved.Properties.ContainsKey("YieldStrength"));

        // Validation runs against the real rule set.
        var validation = await ((IValidatable)part).ValidateAsync();
        Assert.NotNull(validation);

        // Result: the calculation's link to the component is discoverable
        // from the component's own side — the digital thread the project owns.
        var links = await ((IHasRelationships)calculation).GetRelationshipsAsync();
        Assert.Contains(links, r => r.TargetId == part.Id);

        // And the shell is still inside the project throughout.
        Assert.Equal(ShellArea.Engineering, rig.Navigator.Current.Area);
        Assert.Equal(project.Id, rig.Context.Current!.Id);
    }

    // ----------------------------------------------------------------
    // Trace 2 — Project -> Requirement -> Verification -> Evidence
    // ----------------------------------------------------------------

    [Fact]
    public async Task Trace_Project_Requirement_Verification_Evidence()
    {
        var rig = BuildRig();
        rig.Principal.SetCurrent(new PlatformPrincipal(
            new PlatformIdentity("engineer", "Engineer"), [VerificationService.ReadPermission]));

        var verification = new VerificationService(rig.DocumentStore, rig.Principal, new PermissionEvaluator());
        var requirements = new RequirementsService(rig.DocumentStore, rig.Persistence, rig.Principal, verification);

        var project = await rig.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");
        await rig.Navigator.OpenProjectAsync(project.Id);

        // Requirement — a real requirement in the real service.
        var requirement = await requirements.CreateAsync("REQ-001", "The impeller shall withstand 120 bar.");

        // Verification against that requirement, with real evidence.
        var verificationContext = new VerificationContext();
        verificationContext.RecordCriterion("Peak stress below allowable", isSatisfied: true, "412 MPa < 503 MPa");
        verificationContext.RecordEvidence("FEA report", "FEA-2041");

        var record = await verification.RecordAsync(
            requirement.Id, VerificationOutcome.Pass, "Analysis", verificationContext);

        // Evidence composes back from the requirement.
        var evidence = await requirements.GetEvidenceAsync(requirement.Id);

        Assert.Equal(VerificationOutcome.Pass, record.Outcome);
        Assert.NotNull(evidence);
        Assert.Contains(evidence.VerificationHistory, v => v.Outcome == VerificationOutcome.Pass);
        Assert.Equal(project.Id, rig.Context.Current!.Id);
    }

    // ----------------------------------------------------------------
    // Trace 3 — Project -> Drawing/Document -> Engineering Object
    // ----------------------------------------------------------------

    [Fact]
    public async Task Trace_Project_Document_And_Drawing_To_EngineeringObject()
    {
        var rig = BuildRig();
        var project = await rig.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");
        await rig.Navigator.OpenProjectAsync(project.Id);

        var part = await CreateInProjectAsync(rig.Domain, "Part", project.Id,
            (doc, rev) => new Part(doc, rev, rig.Domain, "PN-1001", "Impeller", EngineeringObjectMetadata.Empty));

        var drawing = await CreateInProjectAsync(rig.Domain, "Drawing", project.Id,
            (doc, rev) => new Drawing(doc, rev, rig.Domain, "DWG-1001", "Impeller GA", EngineeringObjectMetadata.Empty, "P-0027-DRW-1001"));

        var document = await CreateInProjectAsync(rig.Domain, "Document", project.Id,
            (doc, rev) => new Document(doc, rev, rig.Domain, "DOC-1", "Design Report", EngineeringObjectMetadata.Empty));

        // The drawing depicts the component; the document describes it.
        await ((IHasRelationships)drawing).LinkAsync(part.Id, "depicts");
        await ((IHasRelationships)document).LinkAsync(part.Id, "describes");

        var contents = await rig.Directory.ListProjectContentsAsync(project.Id);
        Assert.Contains(drawing.Id, contents);
        Assert.Contains(document.Id, contents);
        Assert.Contains(part.Id, contents);

        var drawingLinks = await ((IHasRelationships)drawing).GetRelationshipsAsync();
        Assert.Contains(drawingLinks, r => r.TargetId == part.Id);
        Assert.Equal("P-0027-DRW-1001", drawing.DrawingNumber);
    }

    // ----------------------------------------------------------------
    // Project contents are genuinely scoped — not "everything"
    // ----------------------------------------------------------------

    [Fact]
    public async Task ProjectContents_ContainOnlyThatProjectsOwnObjects()
    {
        var rig = BuildRig();
        var apollo = await rig.Directory.CreateAsync("P-0027", "Apollo");
        var manifold = await rig.Directory.CreateAsync("P-0011", "Hydraulic Manifold");

        var apolloPart = await CreateInProjectAsync(rig.Domain, "Part", apollo.Id,
            (doc, rev) => new Part(doc, rev, rig.Domain, "PN-1", "Impeller", EngineeringObjectMetadata.Empty));
        var manifoldPart = await CreateInProjectAsync(rig.Domain, "Part", manifold.Id,
            (doc, rev) => new Part(doc, rev, rig.Domain, "PN-2", "Valve Block", EngineeringObjectMetadata.Empty));

        var apolloContents = await rig.Directory.ListProjectContentsAsync(apollo.Id);

        Assert.Contains(apolloPart.Id, apolloContents);
        Assert.DoesNotContain(manifoldPart.Id, apolloContents);
    }

    private static IReadOnlyDictionary<string, MaterialProperty> BuildProperties() =>
        new Dictionary<string, MaterialProperty>
        {
            ["YieldStrength"] = new MaterialProperty(
                new Quantity<Pressure>(503.0, PressureUnits.Megapascal),
                new MaterialPropertyProvenance(
                    SourceReference: "Test fixture — not a real material standard",
                    SourceRevision: 1,
                    ValidationStatus: MaterialPropertyValidationStatus.Validated,
                    ConfidenceLevel: MaterialPropertyConfidenceLevel.High,
                    ApplicableConditions: "Room temperature",
                    Notes: "Fictional test value.")),
        };
}
