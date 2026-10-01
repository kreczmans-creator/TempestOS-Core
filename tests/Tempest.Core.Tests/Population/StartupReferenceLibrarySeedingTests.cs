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
    public async Task AFreshHostOverAnEmptyRoot_SeedsEveryShippedRecordAcrossTheFiveLibraries_AllReleasedWithTheirCitation()
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

            Assert.Equal(StandardSeed.Instance.Records.Count, standardRecords.Count);
            Assert.Equal(MaterialSeed.Instance.Records.Count, materialRecords.Count);
            Assert.Equal(ConstantSeed.Instance.Records.Count, constantRecords.Count);
            Assert.Equal(FastenerSeed.Instance.Records.Count, fastenerRecords.Count);
            Assert.Equal(BearingSeed.Instance.Records.Count, bearingRecords.Count);

            var total = standardRecords.Count + materialRecords.Count + constantRecords.Count
                + fastenerRecords.Count + bearingRecords.Count;
            Assert.Equal(29 + 77 + 12 + 131 + 39, total);

            // PO decision 2026-10-01: every shipped record is released at
            // seed — verified and released through the review path as the
            // named seed principal — and every one still carries the
            // structured citation its own dataset gives it (`ADR-0149`).
            // Five separate, typed calls rather than one shared loop,
            // because the five libraries have five different definition
            // types.
            AssertAllReleasedWithCitation(standardRecords);
            AssertAllReleasedWithCitation(materialRecords);
            AssertAllReleasedWithCitation(constantRecords);
            AssertAllReleasedWithCitation(fastenerRecords);
            AssertAllReleasedWithCitation(bearingRecords);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void AssertAllReleasedWithCitation<TDefinition>(IReadOnlyList<IReferenceRecord<TDefinition>> records)
        where TDefinition : class
    {
        Assert.All(records, r => Assert.Equal(ReferenceValidationState.Released, r.ValidationState));
        Assert.All(records, r => Assert.Equal(ReferenceSeedReleasePolicy.SeedPrincipalId, r.Provenance.ReviewerPrincipalId));
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

                // Still exactly the shipped counts - the second launch's own
                // pass found every shipped record already present and
                // released, and changed nothing
                // (ReferenceSeedService.ApplyAtStartupAsync is additive).
                Assert.Equal(StandardSeed.Instance.Records.Count, (await standards.ListAsync()).Count);
                Assert.Equal(MaterialSeed.Instance.Records.Count, (await materials.ListAsync()).Count);
                Assert.Equal(ConstantSeed.Instance.Records.Count, (await constants.ListAsync()).Count);
                Assert.Equal(FastenerSeed.Instance.Records.Count, (await fasteners.ListAsync()).Count);
                Assert.Equal(BearingSeed.Instance.Records.Count, (await bearings.ListAsync()).Count);

                // And nothing was revised again: S355J2 is at the revision the
                // first launch's release left it at.
                var s355 = (await materials.FindAsync(MaterialSeed.S355J2))!;
                Assert.Equal(ReferenceValidationState.Released, s355.ValidationState);
                // Registered (1), verified (2), Checked (3), Validated (4), Released (5).
                Assert.Equal(5, s355.RevisionNumber);
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
                Assert.Equal(StandardSeed.Instance.Records.Count, (await standards.ListAsync()).Count);
                Assert.Equal(ConstantSeed.Instance.Records.Count, (await constants.ListAsync()).Count);
                Assert.Equal(FastenerSeed.Instance.Records.Count, (await fasteners.ListAsync()).Count);
                Assert.Equal(BearingSeed.Instance.Records.Count, (await bearings.ListAsync()).Count);
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ARootSeededBeforeThePoDecision_IsToppedUpAndReleased_OnTheNextLaunch()
    {
        using var temp = new TempDirectory();

        // An installation seeded Draft-only by the earlier six-grade dataset:
        // reproduced by registering just the six first-acquisition records,
        // Draft, the way the earlier seeder left them.
        {
            var (host, manager) = await StartHostAsync(temp.Path);
            try
            {
                var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
                var firstSix = new[]
                {
                    MaterialSeed.S355J2, MaterialSeed.Stainless1Point4301, MaterialSeed.Stainless1Point4404,
                    MaterialSeed.Aluminium6082T6, MaterialSeed.Aluminium5083OH111, MaterialSeed.CopperCw004A,
                };

                foreach (var record in MaterialSeed.Instance.Records.Where(r => firstSix.Contains(r.RecordId)))
                    await materials.RegisterAsync(record.RecordId, record.Definition, record.Provenance, record.Source);
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

                var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
                var records = await materials.ListAsync();

                Assert.Equal(MaterialSeed.Instance.Records.Count, records.Count);
                Assert.All(records, r => Assert.Equal(ReferenceValidationState.Released, r.ValidationState));
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }
}
