using Avalonia.Headless.XUnit;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Standards;

namespace Tempest.Desktop.Tests;

// The engineer-facing journey, run through the desktop application's own
// host rather than through a test-built service graph:
//
//   find reference information -> see whether it may be used -> follow an
//   engineering result back to the data it stood on
//
// No view is added and no navigation is changed. What is proved is that the
// application composition really does reach the populated libraries through
// their governed services, which is the smallest claim worth making before
// any surface is built on top.
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public class EngineeringDataJourneyTests
{
    [AvaloniaFact]
    public async Task TheApplicationReachesTheReferenceLibrariesThroughItsOwnComposition()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            Assert.NotNull(host.ReferenceLibraries);
            Assert.NotNull(host.EngineeringTrace);

            var libraries = await host.ReferenceLibraries!.ListAsync();
            Assert.Equal(6, libraries.Count);

            // Five libraries are empty, because the host does not seed
            // itself. Materials is not: the sample modules register two
            // fictional demonstration alloys into the real library at
            // start-up. That is by design and long-standing, and it is
            // exactly the situation the population phase's honesty rules
            // exist for, so it is asserted rather than tolerated.
            var populated = libraries.Where(l => !l.IsEmpty).ToList();
            var sampleLibrary = Assert.Single(populated);

            Assert.Equal("Materials", sampleLibrary.Library);
            Assert.Equal(2, sampleLibrary.RecordCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnEngineerCanFindPopulatedDataAndBeToldWhyItIsNotYetUsable()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService));
            var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog));
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));

            await seeder.ApplyAsync(standards, StandardSeed.Instance);
            await seeder.ApplyAsync(materials, MaterialSeed.Instance);

            var summary = await host.ReferenceLibraries!.ListLibraryAsync("Materials");

            // Six seeded grades plus the two fictional sample alloys the
            // application registers at start-up.
            Assert.Equal(8, summary.RecordCount);
            Assert.Equal(0, summary.ReleasedCount);

            // The distinction an engineer has to be shown: full, and not
            // usable. A surface that displayed the six records without this
            // would be actively misleading.
            Assert.True(summary.IsPopulatedButUnusable);

            var entry = Assert.Single(summary.Entries, e => e.RecordId == MaterialSeed.Aluminium6082T6);

            Assert.Equal("6082-T6 wrought aluminium alloy", entry.DisplayName);
            Assert.Equal("Aalco Metals Limited", entry.SourceOrganisation);
            Assert.False(entry.IsUsableAsAuthoritative);
            Assert.NotNull(entry.UnusableReason);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnEngineerCanFollowACalculationBackToTheDocumentItsNumbersCameFrom()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService));
            var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog));
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
            var templates = (ITemplateCatalog)host.Services!.GetService(typeof(ITemplateCatalog));
            var packs = (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog));

            await seeder.ApplyAsync(standards, StandardSeed.Instance);
            await seeder.ApplyAsync(materials, MaterialSeed.Instance);
            await seeder.ApplyAsync(templates, EngineeringAssetSeed.Templates);

            var assets = await EngineeringAssetSeed.CreateAsync(materials, templates);
            await seeder.ApplyAsync(packs, assets.CalculationPacks);

            var trace = await host.EngineeringTrace!.TraceCalculationAsync(
                EngineeringAssetSeed.CalculationPackRecordId);

            Assert.NotNull(trace);
            Assert.True(trace!.IsFullyResolved);

            var material = Assert.Single(trace.AllReferences, r => r.RecordId == MaterialSeed.Aluminium6082T6);

            Assert.Equal("Aalco Metals Limited", material.SourceOrganisation);
            Assert.Contains("6082 - T6 Extrusions", material.SourceDocument);
            Assert.False(material.HasMovedOnSincePinned);
            Assert.False(material.WasVerified);

            // The two inputs nobody has established are surfaced, not
            // hidden, which is the thing a reviewer most needs from a
            // traceability view.
            Assert.Equal(2, trace.UntraceableInputs.Count);
            Assert.False(trace.RestsEntirelyOnVerifiedData);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheApplicationStillSeesTheOldRevisionAfterTheDataIsCorrected()
    {
        var rootPath = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        int pinnedRevision;

        var host = new WorkspaceHost(rootPath);
        try
        {
            await host.StartAsync();

            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService));
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
            var templates = (ITemplateCatalog)host.Services!.GetService(typeof(ITemplateCatalog));
            var packs = (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog));

            await seeder.ApplyAsync(materials, MaterialSeed.Instance);
            await seeder.ApplyAsync(templates, EngineeringAssetSeed.Templates);
            var assets = await EngineeringAssetSeed.CreateAsync(materials, templates);
            await seeder.ApplyAsync(packs, assets.CalculationPacks);

            var before = await host.EngineeringTrace!.TraceCalculationAsync(
                EngineeringAssetSeed.CalculationPackRecordId);
            pinnedRevision = Assert.Single(
                before!.AllReferences, r => r.RecordId == MaterialSeed.Aluminium6082T6).PinnedRevision;

            var record = await materials.FindAsync(MaterialSeed.Aluminium6082T6);
            await materials.ReviseAsync(
                MaterialSeed.Aluminium6082T6,
                record!.Definition with { Notes = "Corrected during review." },
                record.Provenance,
                "Reviewer correction.");

            var after = await host.EngineeringTrace!.TraceCalculationAsync(
                EngineeringAssetSeed.CalculationPackRecordId);
            var reference = Assert.Single(after!.AllReferences, r => r.RecordId == MaterialSeed.Aluminium6082T6);

            // The application shows the engineer the revision their
            // calculation used, and tells them the record has since moved
            // on. Both facts, neither hidden.
            Assert.Equal(pinnedRevision, reference.PinnedRevision);
            Assert.True(reference.HasMovedOnSincePinned);
            Assert.Equal(pinnedRevision + 1, reference.CurrentRevision);
            Assert.Single(after.StaleReferences);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheFictionalSampleAlloysAreDistinguishableFromSourceBackedData()
    {
        // The application ships two invented materials in the same library
        // as the real ones. Nothing stops an engineer browsing past them,
        // so the only protection is that they say what they are — in
        // provenance, which is where a claim about where data came from
        // belongs, rather than in a naming convention a record could drop.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService));
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
            await seeder.ApplyAsync(materials, MaterialSeed.Instance);

            var summary = await host.ReferenceLibraries!.ListLibraryAsync("Materials");

            var fictional = summary.Entries
                .Where(e => e.SourceOrganisation == "TempestOS sample module")
                .ToList();

            Assert.Equal(2, fictional.Count);
            Assert.All(fictional, e => Assert.Contains(
                "Fictional test fixture", e.SourceDocument!, StringComparison.Ordinal));

            // And the source-backed records name real publishers.
            var sourced = summary.Entries
                .Where(e => e.SourceOrganisation != "TempestOS sample module")
                .ToList();

            Assert.Equal(6, sourced.Count);
            Assert.DoesNotContain(sourced, e => e.SourceOrganisation is null);

            // Neither kind can be relied on, and for the same reason —
            // which means the fictional pair can never reach a reasoning
            // service, whatever an engineer does with the list.
            Assert.All(summary.Entries, e => Assert.False(e.IsUsableAsAuthoritative));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
