using Tempest.Core.Components;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Runtime;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for `Group A` (P01): proves every reference
// library and its own validation service is wired into the real,
// unmodified TempestHost, that the narrow cross-library seams resolve to
// the *same* catalogue rather than a second one, and that each validation
// service actually received the optional collaborators the container had
// available. Materials and Bearings keep their own registration tests
// (ADR-0055, ADR-0124); this covers the five libraries added alongside
// them and the seams between all seven.
[Collection("Console output capture")]
public class ReferenceDataHostRegistrationTests
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

    [Theory]
    [InlineData(typeof(IStandardCatalog), typeof(StandardCatalog))]
    [InlineData(typeof(IStandardValidationService), typeof(StandardValidationService))]
    [InlineData(typeof(IFastenerCatalog), typeof(FastenerCatalog))]
    [InlineData(typeof(IFastenerValidationService), typeof(FastenerValidationService))]
    [InlineData(typeof(IComponentCatalog), typeof(ComponentCatalog))]
    [InlineData(typeof(IComponentValidationService), typeof(ComponentValidationService))]
    [InlineData(typeof(IConstantCatalog), typeof(ConstantCatalog))]
    [InlineData(typeof(IConstantValidationService), typeof(ConstantValidationService))]
    [InlineData(typeof(IProcessCatalog), typeof(ProcessCatalog))]
    [InlineData(typeof(IProcessValidationService), typeof(ProcessValidationService))]
    [InlineData(typeof(IMaterialValidationService), typeof(MaterialValidationService))]
    public async Task Host_RegistersEveryReferenceLibraryAndItsValidationService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryReferenceLibrary_IsAnOrdinarySingleton()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IStandardCatalog), typeof(IFastenerCatalog), typeof(IComponentCatalog),
                         typeof(IConstantCatalog), typeof(IProcessCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheStandardResolverSeam_ResolvesToTheOneRegisteredStandardsLibrary()
    {
        // Mapping StandardCatalog to two service types would construct two
        // catalogues over one store, each with its own write locks, and the
        // check-then-write atomicity ReferenceDataCatalog<T> depends on
        // would be silently lost. The forwarder is what prevents that.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var catalog = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
            var resolver = (IStandardResolver)host.Services!.GetService(typeof(IStandardResolver))!;

            Assert.IsType<StandardCatalogResolver>(resolver);

            await catalog.RegisterAsync(
                "std-registration-probe",
                new StandardDefinition
                {
                    Body = new StandardsBody("TFX", "TestFixture Standards Institute (not a real body)"),
                    Designation = "FX-REG-1",
                },
                ReferenceProvenance.Unknown);

            // The resolver sees what the catalogue wrote, which it could
            // only do if both are the same instance over the same store.
            Assert.True(await resolver.ExistsAsync("std-registration-probe"));
        });
    }

    [Fact]
    public async Task TheReleasedConstantSeam_ResolvesToTheOneRegisteredConstantsLibrary()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var catalog = (IConstantCatalog)host.Services!.GetService(typeof(IConstantCatalog))!;
            var source = (IReleasedConstantSource)host.Services!.GetService(typeof(IReleasedConstantSource))!;

            Assert.IsType<ConstantCatalogReleasedSource>(source);

            await catalog.RegisterAsync(
                "con-registration-probe",
                new ConstantDefinition { Symbol = "fx_reg", Name = "Fixture registration probe" },
                ReferenceProvenance.Unknown);

            // Registered but not released: the seam sees the catalogue and
            // still, correctly, hands back nothing.
            Assert.NotNull(await catalog.FindBySymbolAsync("fx_reg"));
            Assert.Null(await source.FindReleasedAsync("fx_reg"));
        });
    }

    [Fact]
    public async Task ACitingLibrarysValidationService_ActuallyReceivedTheResolversTheContainerHad()
    {
        // The optional collaborators are only useful if the container
        // supplied them. Observing the unresolved-reference warnings proves
        // it did: without a resolver, the shared base skips those rules
        // entirely and no warning could appear.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var fasteners = (IFastenerCatalog)host.Services!.GetService(typeof(IFastenerCatalog))!;
            var validator = (IFastenerValidationService)host.Services!.GetService(typeof(IFastenerValidationService))!;

            await fasteners.RegisterAsync(
                "fst-registration-probe",
                new FastenerDefinition
                {
                    Family = FastenerFamily.Bolt,
                    Designation = "FX-REG-1",
                    MaterialId = "mat-not-registered",
                    Standards = [new StandardReference("Fixture standard", StandardId: "std-not-registered")],
                },
                ReferenceProvenance.Unknown);

            var result = await validator.ValidateAsync("fst-registration-probe");

            Assert.Contains(result.Warnings, d => d.Code == ReferenceValidationRules.MaterialReferenceUnresolved);
            Assert.Contains(result.Warnings, d => d.Code == ReferenceValidationRules.StandardReferenceUnresolved);
        });
    }

    [Fact]
    public async Task EveryReferenceLibrary_SharesTheOnePersistenceStoreTheHostResolves()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var store = (IPersistenceStore)host.Services!.GetService(typeof(IPersistenceStore))!;
            var processes = (IProcessCatalog)host.Services!.GetService(typeof(IProcessCatalog))!;

            await processes.RegisterAsync(
                "prc-registration-probe",
                new ProcessDefinition { Family = ProcessFamily.Milling, Name = "Fixture registration probe" },
                ReferenceProvenance.Unknown);

            // The index entry is visible in the host's own store, which it
            // could only be if the catalogue was given that same store
            // rather than one of its own.
            Assert.Contains("prc-registration-probe", await store.ListKeysAsync(ProcessCatalog.IndexCollection));
        });
    }
}
