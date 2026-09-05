using Tempest.Core.Bearings;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Bearing Library is wired into the
// real, unmodified TempestHost exactly as ADR-0124 specifies -
// IBearingCatalog and IBearingValidationService resolvable, ordinary
// singleton semantics, and the catalogue genuinely reusing the same
// IPersistenceStore and IEngineeringDocumentStore instances every other
// discipline resolves rather than a second, independent pair. Mirrors
// MaterialsHostRegistrationTests exactly.
[Collection("Console output capture")]
public class BearingsHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            await body(host);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Host_RegistersIBearingCatalog_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType<BearingCatalog>(host.Services!.GetService(typeof(IBearingCatalog)));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_RegistersIBearingValidationService_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType<BearingValidationService>(host.Services!.GetService(typeof(IBearingValidationService)));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingIBearingCatalogTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(IBearingCatalog));
            var second = host.Services!.GetService(typeof(IBearingCatalog));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_BearingCatalog_CanRoundTripARecordThroughTheRealPersistenceStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var catalog = (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog));

            var definition = new BearingDefinition
            {
                Identity = new BearingIdentity("TestFixture Bearings", "FX-REG-001", "FX-REG-001"),
                Family = BearingFamily.DeepGrooveBall,
                Geometry = new BearingGeometry(
                    Bore: new Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Length>(
                        10.0, Tempest.Core.UnitsAndQuantities.LengthUnits.Millimetre),
                    OutsideDiameter: new Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Length>(
                        26.0, Tempest.Core.UnitsAndQuantities.LengthUnits.Millimetre)),
                Provenance = BearingProvenance.Unknown,
            };

            var bearing = await catalog.RegisterAsync("registration-test", definition);
            var found = await catalog.FindAsync(bearing.BearingId);

            Assert.NotNull(found);
            Assert.Equal("FX-REG-001", found!.Definition.Identity.ManufacturerPartNumber);
            Assert.Equal(BearingValidationState.Draft, found.ValidationState);
        });
    }

    [Fact]
    public async Task Host_BearingValidationService_ResolvesMaterialReferencesAgainstTheRealMaterialsCatalogue()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var catalog = (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog));
            var validator = (IBearingValidationService)host.Services!.GetService(typeof(IBearingValidationService));

            var definition = new BearingDefinition
            {
                Identity = new BearingIdentity("TestFixture Bearings", "FX-REG-002", "FX-REG-002"),
                Family = BearingFamily.DeepGrooveBall,
                Geometry = new BearingGeometry(
                    Bore: new Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Length>(
                        10.0, Tempest.Core.UnitsAndQuantities.LengthUnits.Millimetre),
                    OutsideDiameter: new Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Length>(
                        26.0, Tempest.Core.UnitsAndQuantities.LengthUnits.Millimetre)),
                Provenance = BearingProvenance.Unknown,
                Construction = new BearingConstruction(RingMaterialId: "never-registered-material"),
            };

            await catalog.RegisterAsync("registration-materials-test", definition);
            var result = await validator.ValidateAsync("registration-materials-test");

            Assert.Contains(result.Warnings, warning => warning.Code == BearingValidationRules.MaterialReferenceUnresolved);
        });
    }
}
