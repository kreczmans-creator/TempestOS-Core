using Tempest.Core.Configuration;
using Tempest.Calc.Library;
using Tempest.Calc.Tests.Sheets;
using Tempest.Core.Bearings;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Standards;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Calc.Tests.Library;

/// <summary>An in-memory host for the Library: the real EvidenceService over the Core test domain, a commit-failing store wrapper, and the catalogue with the Sheets test calculator.</summary>
internal sealed class LibraryTestHost
{
    public LibraryTestHost(bool withProject = true, IReadOnlyList<Tempest.Calc.Sheets.ICalcCalculator>? calculators = null, Tempest.Core.Events.IEventBus? events = null, IReadOnlyList<CalcPackTemplate>? packTemplates = null)
    {
        Backing = new InMemoryQueryablePersistenceStore();
        Failing = new CommitFailingPersistenceStore(Backing);
        Context = TestEngineeringDomain.NewContextOver(Failing, Backing);
        var documentStore = new EngineeringDocumentStore(Backing, new CurrentPrincipalAccessor());
        Evidence = new EvidenceService(
            Context,
            new MaterialCatalog(documentStore, Backing),
            new FastenerCatalog(documentStore, Backing),
            new BearingCatalog(documentStore, Backing),
            new StandardCatalog(documentStore, Backing),
            new ConstantCatalog(documentStore, Backing),
            new CurrentPrincipalAccessor(),
            new ConfigurationBuilder().Build());
        Catalogue = new CalcCatalogue([new DirectStressCalculator(), .. calculators ?? []]);
        Store = new CalcInstanceStore(Failing);
        Service = new CalcInstanceService(Catalogue, Store, Evidence, Context, renderer: null, applicationVersion: "TempestOS test", events: events, packTemplates: packTemplates);
        WithProject = withProject;
    }

    public InMemoryQueryablePersistenceStore Backing { get; }

    public CommitFailingPersistenceStore Failing { get; }

    public EngineeringDomainContext Context { get; }

    public EvidenceService Evidence { get; }

    public CalcCatalogue Catalogue { get; }

    public CalcInstanceStore Store { get; }

    public CalcInstanceService Service { get; }

    public bool WithProject { get; }

    public async Task<Guid?> ProjectAsync()
    {
        if (!WithProject)
            return null;

        var factory = new EngineeringObjectFactory<Project>(
            "Project", Context, (doc, rev) => new Project(doc, rev, Context, "CALC-PRJ", "Calc test project", EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Calc test project.");
        return project.Id;
    }

    public static IReadOnlyList<Tempest.Calc.Sheets.CalcInputValue> ProvenancedInputs(double f = 12) =>
    [
        Tempest.Calc.Sheets.CalcInputValue.Quantity("F", f, "kN", Tempest.Calc.Sheets.CalcProvenance.Drawing("DRG-001", "B")),
        Tempest.Calc.Sheets.CalcInputValue.Quantity("A", 200, "mm^2", Tempest.Calc.Sheets.CalcProvenance.Drawing("DRG-001", "B")),
        Tempest.Calc.Sheets.CalcInputValue.Quantity("sigma_allow", 160, "MPa", Tempest.Calc.Sheets.CalcProvenance.Specification("SPEC-7 §4")),
        Tempest.Calc.Sheets.CalcInputValue.Choice("grade", "S275", Tempest.Calc.Sheets.CalcProvenance.Assumed("Not stated.")),
        Tempest.Calc.Sheets.CalcInputValue.Switch("slender", false, Tempest.Calc.Sheets.CalcProvenance.Assumed("Stocky by inspection.")),
    ];
}
