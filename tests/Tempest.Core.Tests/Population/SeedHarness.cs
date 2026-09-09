using Tempest.Core.Bearings;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.EngineeringData;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Materials;

namespace Tempest.Core.Tests.Population;

// One document store and one persistence store behind every P01 library,
// exactly as the running host wires them - so a cross-library reference in
// these tests resolves the same way it resolves in the product, rather
// than only inside a per-test fake.
//
// WP 18.0C (D-028): this harness used to seed every P01-P06 library,
// including the P02 (Rules), P03 (Suppliers/Costs/LeadTimes), P05-archived
// (DesignReviews/TechnicalDocuments) and P06 (Prompts/Academy/Challenges/
// WorkedExamples) libraries this Work Package froze to src/Frozen/. It is
// trimmed here to the libraries that remain live, because
// tests/Tempest.Core.Tests/Calculations/BracketEngineeringDemonstrationTests.cs
// subclasses it and is out of this Work Package's "files you own" list to
// edit. The full, untrimmed scenario population this class used to drive
// (BracketScenarioTests, RefusalTests, CrossDomainReferenceTests,
// ScenarioReadinessTests, SeedDatasetTests, ScenarioHarness) moved to
// tests/Frozen/Tempest.Core.Tests/ alongside the namespaces they proved.
internal class SeedHarness
{
    public SeedHarness()
    {
        PersistenceStore = new InMemoryPersistenceStore();
        DocumentStore = new EngineeringDocumentStore(PersistenceStore, new CurrentPrincipalAccessor());

        Materials = new MaterialCatalog(DocumentStore, PersistenceStore);
        Standards = new StandardCatalog(DocumentStore, PersistenceStore);
        Constants = new ConstantCatalog(DocumentStore, PersistenceStore);
        Fasteners = new FastenerCatalog(DocumentStore, PersistenceStore);
        Bearings = new BearingCatalog(DocumentStore, PersistenceStore);
        Processes = new ProcessCatalog(DocumentStore, PersistenceStore);
        Templates = new TemplateCatalog(DocumentStore, PersistenceStore);
        CalculationPacks = new CalculationPackCatalog(DocumentStore, PersistenceStore);
        VerificationArtefacts = new VerificationArtefactCatalog(DocumentStore, PersistenceStore);

        var principals = new CurrentPrincipalAccessor();
        Requirements = new RequirementsService(
            (EngineeringDocumentStore)DocumentStore,
            PersistenceStore,
            principals,
            new VerificationService((EngineeringDocumentStore)DocumentStore, principals, new PermissionEvaluator()));

        Seeder = new ReferenceSeedService();
    }

    /// <summary>The identifier of the requirement the seeded assets hang from.</summary>
    public const string BracketRequirementIdentifier = "REQ-BRACKET-001";

    public InMemoryPersistenceStore PersistenceStore { get; }

    public IEngineeringDocumentStore DocumentStore { get; }

    public MaterialCatalog Materials { get; }

    public StandardCatalog Standards { get; }

    public ConstantCatalog Constants { get; }

    public FastenerCatalog Fasteners { get; }

    public BearingCatalog Bearings { get; }

    public ProcessCatalog Processes { get; }

    public TemplateCatalog Templates { get; }

    public CalculationPackCatalog CalculationPacks { get; }

    public VerificationArtefactCatalog VerificationArtefacts { get; }

    public RequirementsService Requirements { get; }

    public ReferenceSeedService Seeder { get; }

    /// <summary>Applies every remaining P01 seed dataset, in citation order.</summary>
    public async Task<IReadOnlyList<ReferenceSeedOutcome>> SeedEverythingAsync()
    {
        // Citation order, and it matters. Standards come first because
        // every reference library cites them; materials before the assets
        // that pin material revisions; the template before the calculation
        // pack that pins the template. Seeding in this order means a
        // reference is resolvable the moment the record carrying it exists,
        // rather than dangling until a later pass repairs it.
        var outcomes = new List<ReferenceSeedOutcome>
        {
            await Seeder.ApplyAsync(Standards, StandardSeed.Instance),
            await Seeder.ApplyAsync(Materials, MaterialSeed.Instance),
            await Seeder.ApplyAsync(Constants, ConstantSeed.Instance),
            await Seeder.ApplyAsync(Fasteners, FastenerSeed.Instance),
            await Seeder.ApplyAsync(Bearings, BearingSeed.Instance),
            await Seeder.ApplyAsync(Processes, ProcessSeed.Instance),
            await Seeder.ApplyAsync(Templates, EngineeringAssetSeed.Templates),
        };

        // A real requirement, created before the assets that cite it. The
        // verification model refuses an artefact naming an empty identity,
        // so the alternative to creating one was to seed no verification
        // artefact at all - and a requirement is the head of the scenario
        // the next phase has to exercise anyway.
        var requirement = await Requirements.FindByIdentifierAsync(BracketRequirementIdentifier)
            ?? await Requirements.CreateAsync(
                BracketRequirementIdentifier,
                "The bracket shall carry its design load with a positive margin against the material's "
                + "specified minimum proof stress.",
                "Structural");

        var verifiedRequirement = new Tempest.Core.EngineeringAssets.Verification.VerifiedRequirement(
            requirement.Id,
            requirement.Identifier,
            requirement.Statement,
            requirement.RevisionNumber);

        // Built rather than declared: the assets pin the revisions the
        // material and template records are actually at, which is a fact
        // about this catalogue and cannot be written as a literal.
        var assets = await EngineeringAssetSeed.CreateAsync(Materials, Templates, verifiedRequirement);

        outcomes.Add(await Seeder.ApplyAsync(CalculationPacks, assets.CalculationPacks));
        outcomes.Add(await Seeder.ApplyAsync(VerificationArtefacts, assets.VerificationArtefacts));

        return outcomes;
    }
}
