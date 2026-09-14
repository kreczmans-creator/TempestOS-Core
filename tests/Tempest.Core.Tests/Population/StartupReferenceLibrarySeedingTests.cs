using Tempest.Core.Bearings;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Runtime;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace;
using Tempest.Workspace.Composition;

namespace Tempest.Core.Tests.Population;

/// <summary>
/// `WP 18.0B-R1` (`TD-163`): the gap a backlog audit found — `WP 18.0A`
/// gave all 41 seeded records a structured source citation, but only
/// Materials ever reached a shipped call site
/// (<c>BracketCalculationWorkbench.PopulateMaterialLibraryAsync</c>), so
/// Fasteners, Bearings, Standards and Constants (35 of the 41 records)
/// stayed empty in every real launch. These tests drive the real, shared
/// composition root (<see cref="EngineeringWorkspaceComposer.RegisterEngineeringDisciplines"/>
/// then <see cref="EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync"/>)
/// both <c>Tempest.Desktop</c> and <c>Tempest.Harness</c> go through, with
/// an explicit, empty module list — never <see cref="EngineeringWorkspaceComposer.Build"/>'s
/// own reflective discovery, which would load <c>Tempest.Samples</c>' own
/// demonstration material and confound the counts below — exactly the
/// convention <c>Workspace.CommandDescriptorBindingTests</c> and
/// <c>EngineeringDomain.SchemaVersioning.RestartProofTests</c> already
/// establish for this assembly.
/// </summary>
public sealed class StartupReferenceLibrarySeedingTests
{
    private static async Task<(ITempestHost Host, WorkspaceManager Manager)> StartHostAsync(string persistenceRoot)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, persistenceRoot),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

        return (host, manager);
    }

    [Fact]
    public async Task AFreshHostOverAnEmptyRoot_Seeds41RecordsAcrossTheFiveLibraries_AllDraftWithTheirCitation()
    {
        using var temp = new TempDirectory();

        var (host, manager) = await StartHostAsync(temp.Path);
        try
        {
            await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);

            var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var constants = (IConstantCatalog)host.Services!.GetService(typeof(IConstantCatalog))!;
            var fasteners = (IFastenerCatalog)host.Services!.GetService(typeof(IFastenerCatalog))!;
            var bearings = (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog))!;

            var standardRecords = await standards.ListAsync();
            var materialRecords = await materials.ListAsync();
            var constantRecords = await constants.ListAsync();
            var fastenerRecords = await fasteners.ListAsync();
            var bearingRecords = await bearings.ListAsync();

            Assert.Equal(14, standardRecords.Count);
            Assert.Equal(6, materialRecords.Count);
            Assert.Equal(12, constantRecords.Count);
            Assert.Equal(7, fastenerRecords.Count);
            Assert.Equal(2, bearingRecords.Count);

            var total = standardRecords.Count + materialRecords.Count + constantRecords.Count
                + fastenerRecords.Count + bearingRecords.Count;
            Assert.Equal(41, total);

            // Every one of the 41 landed Draft, and every one carries the
            // structured citation its own dataset gives it (`ADR-0149`) -
            // seeding is population, not verification. Five separate,
            // typed calls rather than one shared loop, because the five
            // libraries have five different definition types.
            AssertAllDraftWithCitation(standardRecords);
            AssertAllDraftWithCitation(materialRecords);
            AssertAllDraftWithCitation(constantRecords);
            AssertAllDraftWithCitation(fastenerRecords);
            AssertAllDraftWithCitation(bearingRecords);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void AssertAllDraftWithCitation<TDefinition>(IReadOnlyList<IReferenceRecord<TDefinition>> records)
        where TDefinition : class
    {
        Assert.All(records, r => Assert.Equal(ReferenceValidationState.Draft, r.ValidationState));
        Assert.All(records, r => Assert.NotNull(r.Source));
    }

    [Fact]
    public async Task ASecondStartOverTheSameRoot_AddsNone()
    {
        using var temp = new TempDirectory();

        {
            var (host, manager) = await StartHostAsync(temp.Path);
            try
            {
                await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }

        {
            var (host, manager) = await StartHostAsync(temp.Path);
            try
            {
                await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);

                var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
                var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
                var constants = (IConstantCatalog)host.Services!.GetService(typeof(IConstantCatalog))!;
                var fasteners = (IFastenerCatalog)host.Services!.GetService(typeof(IFastenerCatalog))!;
                var bearings = (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog))!;

                // Still exactly 41 - the second launch's own pass found
                // every library already holding records and left every one
                // of them alone, per ReferenceSeedService.ApplyIfEmptyAsync's
                // own whole-library gate.
                Assert.Equal(14, (await standards.ListAsync()).Count);
                Assert.Equal(6, (await materials.ListAsync()).Count);
                Assert.Equal(12, (await constants.ListAsync()).Count);
                Assert.Equal(7, (await fasteners.ListAsync()).Count);
                Assert.Equal(2, (await bearings.ListAsync()).Count);
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ARootWhereTheUserAlreadyRegisteredOneMaterialOfTheirOwn_LeavesMaterialsAlone_ButStillSeedsTheOtherFour()
    {
        using var temp = new TempDirectory();

        // First launch: nothing has seeded yet. The user (or, in the real
        // product, a sample module - see EngineeringDataJourneyTests)
        // registers one material of their own, under an identity none of
        // the six shipped grades use, before this host's own rehydration
        // phase ever gets a chance to run.
        const string usersOwnRecordId = "mat-users-own-alloy";
        {
            var (host, manager) = await StartHostAsync(temp.Path);
            try
            {
                var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

                await materials.RegisterAsync(
                    usersOwnRecordId,
                    new MaterialDefinition { Name = "The user's own alloy", Family = MaterialFamily.Steel },
                    new ReferenceProvenance(SourceOrganisation: "The user's own notebook"));

                await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }

        {
            var (host, manager) = await StartHostAsync(temp.Path);
            try
            {
                await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);

                var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
                var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
                var constants = (IConstantCatalog)host.Services!.GetService(typeof(IConstantCatalog))!;
                var fasteners = (IFastenerCatalog)host.Services!.GetService(typeof(IFastenerCatalog))!;
                var bearings = (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog))!;

                // Materials held one record before this host's own
                // rehydration phase ever ran, so it was left exactly alone:
                // still the user's one record, none of the six shipped
                // grades poured in beside it.
                var materialRecords = await materials.ListAsync();
                var onlyRecord = Assert.Single(materialRecords);
                Assert.Equal(usersOwnRecordId, onlyRecord.Id);
                Assert.Null(await materials.FindAsync(MaterialSeed.S355J2));

                // The other four libraries had nothing of their own in
                // them, so they still seeded in full.
                Assert.Equal(14, (await standards.ListAsync()).Count);
                Assert.Equal(12, (await constants.ListAsync()).Count);
                Assert.Equal(7, (await fasteners.ListAsync()).Count);
                Assert.Equal(2, (await bearings.ListAsync()).Count);
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }
}
