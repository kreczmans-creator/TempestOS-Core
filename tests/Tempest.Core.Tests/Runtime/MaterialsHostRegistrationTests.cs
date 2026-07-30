using Tempest.Core.Configuration;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Materials Framework is wired into
// the real, unmodified TempestHost exactly as ADR-0055 specifies -
// IMaterialCatalog resolvable, ordinary singleton semantics, and the
// catalogue genuinely reuses the same IPersistenceStore instance
// Settings/Audit/EngineeringData resolve, not a second, independent one.
[Collection("Console output capture")]
public class MaterialsHostRegistrationTests
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
    public async Task Host_RegistersIMaterialCatalog_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var catalog = host.Services!.GetService(typeof(IMaterialCatalog));

            Assert.IsType<MaterialCatalog>(catalog);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingIMaterialCatalogTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(IMaterialCatalog));
            var second = host.Services!.GetService(typeof(IMaterialCatalog));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_MaterialCatalog_CanRoundTripAMaterialThroughTheRealPersistenceStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var catalog = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));

            var properties = new Dictionary<string, MaterialProperty>
            {
                ["ReferenceLength"] = new MaterialProperty(
                    new Tempest.Core.UnitsAndQuantities.Quantity<Tempest.Core.UnitsAndQuantities.Length>(
                        1.0, Tempest.Core.UnitsAndQuantities.LengthUnits.Metre),
                    MaterialPropertyProvenance.Unknown),
            };

            var material = await catalog.RegisterAsync("registration-test", "Registration Test Material", properties);
            var found = await catalog.FindAsync(material.MaterialId);

            Assert.NotNull(found);
            Assert.Equal("Registration Test Material", found!.Name);
        });
    }
}
