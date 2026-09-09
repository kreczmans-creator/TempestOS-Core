using Tempest.Core.Bearings;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.Decisions;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Knowledge.Academy;
using Tempest.Core.Knowledge.Challenges;
using Tempest.Core.Knowledge.Prompts;
using Tempest.Core.Knowledge.WorkedExamples;
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
// exactly as the running host wires them — so a cross-library reference in
// these tests resolves the same way it resolves in the product, rather
// than only inside a per-test fake.
internal class SeedHarness
{
    public SeedHarness()
    {
        SqlitePersistenceStore = new InMemoryPersistenceStore();
        DocumentStore = new EngineeringDocumentStore(SqlitePersistenceStore, new CurrentPrincipalAccessor());

        Materials = new MaterialCatalog(DocumentStore, SqlitePersistenceStore);
        Standards = new StandardCatalog(DocumentStore, SqlitePersistenceStore);
        Constants = new ConstantCatalog(DocumentStore, SqlitePersistenceStore);
        Fasteners = new FastenerCatalog(DocumentStore, SqlitePersistenceStore);
        Bearings = new BearingCatalog(DocumentStore, SqlitePersistenceStore);
        Processes = new ProcessCatalog(DocumentStore, SqlitePersistenceStore);
        Rules = new RuleCatalog(DocumentStore, SqlitePersistenceStore);
        DecisionTrees = new DecisionTreeCatalog(DocumentStore, SqlitePersistenceStore);
        Suppliers = new SupplierCatalog(DocumentStore, SqlitePersistenceStore);
        Costs = new ProcessCostCatalog(DocumentStore, SqlitePersistenceStore);
        LeadTimes = new LeadTimeCatalog(DocumentStore, SqlitePersistenceStore);
        Templates = new TemplateCatalog(DocumentStore, SqlitePersistenceStore);
        CalculationPacks = new CalculationPackCatalog(DocumentStore, SqlitePersistenceStore);
        VerificationArtefacts = new VerificationArtefactCatalog(DocumentStore, SqlitePersistenceStore);
        DesignReviews = new DesignReviewCatalog(DocumentStore, SqlitePersistenceStore);
        TechnicalDocuments = new TechnicalDocumentCatalog(DocumentStore, SqlitePersistenceStore);
        Prompts = new PromptCatalog(DocumentStore, SqlitePersistenceStore);
        AcademyNodes = new AcademyCatalog(DocumentStore, SqlitePersistenceStore);
        Challenges = new ChallengeCatalog(DocumentStore, SqlitePersistenceStore);
        WorkedExamples = new WorkedExampleCatalog(DocumentStore, SqlitePersistenceStore);

        var principals = new CurrentPrincipalAccessor();
        Requirements = new RequirementsService(
            (EngineeringDocumentStore)DocumentStore,
            SqlitePersistenceStore,
            principals,
            new VerificationService((EngineeringDocumentStore)DocumentStore, principals, new PermissionEvaluator()));

        Seeder = new ReferenceSeedService();
    }

    /// <summary>The identifier of the requirement the seeded assets hang from.</summary>
    public const string BracketRequirementIdentifier = "REQ-BRACKET-001";

    public InMemoryPersistenceStore SqlitePersistenceStore { get; }

    public IEngineeringDocumentStore DocumentStore { get; }

    public MaterialCatalog Materials { get; }

    public StandardCatalog Standards { get; }

    public ConstantCatalog Constants { get; }

    public FastenerCatalog Fasteners { get; }

    public BearingCatalog Bearings { get; }

    public ProcessCatalog Processes { get; }

    public RuleCatalog Rules { get; }

    /// <summary>Empty: no decision tree is seeded, so screening runs on capability data alone.</summary>
    public DecisionTreeCatalog DecisionTrees { get; }

    public SupplierCatalog Suppliers { get; }

    public ProcessCostCatalog Costs { get; }

    public LeadTimeCatalog LeadTimes { get; }

    public TemplateCatalog Templates { get; }

    public CalculationPackCatalog CalculationPacks { get; }

    public VerificationArtefactCatalog VerificationArtefacts { get; }

    public DesignReviewCatalog DesignReviews { get; }

    public TechnicalDocumentCatalog TechnicalDocuments { get; }

    public PromptCatalog Prompts { get; }

    public AcademyCatalog AcademyNodes { get; }

    public ChallengeCatalog Challenges { get; }

    public WorkedExampleCatalog WorkedExamples { get; }

    public RequirementsService Requirements { get; }

    public ReferenceSeedService Seeder { get; }

    /// <summary>Applies every P01 seed dataset, in citation order.</summary>
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
            await Seeder.ApplyAsync(Rules, RuleSeed.Instance),
            await Seeder.ApplyAsync(Suppliers, CommercialSeed.Suppliers),
            await Seeder.ApplyAsync(Costs, CommercialSeed.Costs),
            await Seeder.ApplyAsync(LeadTimes, CommercialSeed.LeadTimes),
            await Seeder.ApplyAsync(Templates, EngineeringAssetSeed.Templates),
        };

        // A real requirement, created before the assets that cite it. The
        // verification model refuses an artefact naming an empty identity,
        // so the alternative to creating one was to seed no verification
        // artefact at all — and a requirement is the head of the scenario
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
        outcomes.Add(await Seeder.ApplyAsync(DesignReviews, assets.DesignReviews));
        outcomes.Add(await Seeder.ApplyAsync(TechnicalDocuments, assets.TechnicalDocuments));

        outcomes.Add(await Seeder.ApplyAsync(Prompts, KnowledgeSeed.Prompts));
        outcomes.Add(await Seeder.ApplyAsync(AcademyNodes, KnowledgeSeed.AcademyNodes));
        outcomes.Add(await Seeder.ApplyAsync(Challenges, KnowledgeSeed.Challenges));

        var aluminium = await Materials.FindAsync(MaterialSeed.Aluminium6082T6);
        outcomes.Add(await Seeder.ApplyAsync(
            WorkedExamples,
            KnowledgeSeed.WorkedExamples(ReferencePin.For(Materials.LibraryName, aluminium!))));

        return outcomes;
    }
}
