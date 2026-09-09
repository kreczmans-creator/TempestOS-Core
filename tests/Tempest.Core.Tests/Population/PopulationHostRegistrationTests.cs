using Tempest.Core.Configuration;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Runtime;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Plugins;

using Tempest.Core.Tests.Runtime;
namespace Tempest.Core.Tests.Population;

// The minimal integration seam the population phase owes the next one: not
// a user interface, just proof that a populated record can be retrieved
// through the real application container rather than only through a
// catalogue a test constructed for itself.
public class PopulationHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();


        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task TheSeedServiceIsAnOrdinarySingletonInTheRealHost()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var seeder = host.Services!.GetService(typeof(ReferenceSeedService));

            Assert.IsType<ReferenceSeedService>(seeder);
            Assert.Same(seeder, host.Services!.GetService(typeof(ReferenceSeedService)));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheHostDoesNotSeedItselfAtStartUp()
    {
        // Registering the seam is not the same as firing it. When a library
        // gets populated is a governance decision, and a host that quietly
        // wrote reference data into every new installation would take that
        // decision away from whoever owns the data.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

            Assert.Empty(await materials.ListAsync());
        });
    }

    [Fact]
    public async Task APopulatedRecordIsRetrievableThroughTheContainer()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
            var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

            await seeder.ApplyAsync(standards, StandardSeed.Instance);
            var outcome = await seeder.ApplyAsync(materials, MaterialSeed.Instance);

            Assert.Equal(6, outcome.RegisteredCount);

            // Read back through a second resolution of the same service, so
            // the record is coming out of the container's singleton and its
            // durable store rather than out of the instance that wrote it.
            var again = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var record = await again.FindAsync(MaterialSeed.S355J2);

            Assert.NotNull(record);
            Assert.Equal("S355J2", record.Definition.Designation);
            Assert.Equal(ReferenceValidationState.Draft, record.ValidationState);

            // The standard the material cites resolves through the
            // container too, which is the cross-library link working in the
            // real composition rather than only in a test harness.
            var citation = record.Definition.Standards.Single();
            Assert.NotNull(await standards.FindAsync(citation.StandardId!));
        });
    }

    [Fact]
    public async Task SeededDataSurvivesAHostRestart()
    {
        // The persistence question the foundation phase's own CostFigure
        // defect was about: a record is only populated if it is still there
        // after the process that wrote it has gone.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

            await seeder.ApplyAsync(materials, MaterialSeed.Instance);
        });

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

            var record = await materials.FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.NotNull(record);

            // Not merely present: the dimensioned values came back
            // dimensioned, in the units they were written in.
            var yield = record.Definition.Properties[MaterialPropertyNames.YieldStrength];
            var quantity = Assert.IsType<Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Pressure>>(yield.Value);

            Assert.Equal(260.0, quantity.Value, 6);
            Assert.Equal("MPa", quantity.Unit.Symbol);
            Assert.Contains("20 mm to 150 mm", yield.Conditions);
            Assert.Equal(ReferenceValueOrigin.Standard, yield.Origin);

            // And so did the provenance, which is the part that makes the
            // value worth having.
            Assert.Equal("Aalco Metals Limited", record.Provenance.SourceOrganisation);
            Assert.False(record.Provenance.IsVerified);

            // Re-seeding the restarted host recognises what is already
            // there rather than failing on duplicates.
            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
            var second = await seeder.ApplyAsync(materials, MaterialSeed.Instance);

            Assert.True(second.MadeNoChange);
            Assert.Equal(6, second.AlreadyPresentCount);
        });
    }
}
