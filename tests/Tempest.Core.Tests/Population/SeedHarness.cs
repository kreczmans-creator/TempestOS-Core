using Tempest.Core.Bearings;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Materials;

namespace Tempest.Core.Tests.Population;

// One document store and one persistence store behind every P01 library,
// exactly as the running host wires them — so a cross-library reference in
// these tests resolves the same way it resolves in the product, rather
// than only inside a per-test fake.
internal sealed class SeedHarness
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
        Rules = new RuleCatalog(DocumentStore, PersistenceStore);
        Suppliers = new SupplierCatalog(DocumentStore, PersistenceStore);
        Costs = new ProcessCostCatalog(DocumentStore, PersistenceStore);
        LeadTimes = new LeadTimeCatalog(DocumentStore, PersistenceStore);

        Seeder = new ReferenceSeedService();
    }

    public InMemoryPersistenceStore PersistenceStore { get; }

    public IEngineeringDocumentStore DocumentStore { get; }

    public MaterialCatalog Materials { get; }

    public StandardCatalog Standards { get; }

    public ConstantCatalog Constants { get; }

    public FastenerCatalog Fasteners { get; }

    public BearingCatalog Bearings { get; }

    public ProcessCatalog Processes { get; }

    public RuleCatalog Rules { get; }

    public SupplierCatalog Suppliers { get; }

    public ProcessCostCatalog Costs { get; }

    public LeadTimeCatalog LeadTimes { get; }

    public ReferenceSeedService Seeder { get; }

    /// <summary>Applies every P01 seed dataset, in citation order.</summary>
    public async Task<IReadOnlyList<ReferenceSeedOutcome>> SeedEverythingAsync()
    {
        // Standards first: every other dataset cites records in it, and
        // seeding in citation order means a reference is resolvable the
        // moment the record carrying it exists.
        return
        [
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
        ];
    }
}
