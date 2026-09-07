using Tempest.Core.Configuration;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.EngineeringAssets;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for `Group E` (P05): proves the engineering-
// asset layer is wired into the real, unmodified TempestHost, that each
// library is one instance over one store, and that a reasoning service
// reads the same library the container hands out.
[Collection("Console output capture")]
public class EngineeringAssetsHostRegistrationTests
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
    [InlineData(typeof(ITemplateCatalog), typeof(TemplateCatalog))]
    [InlineData(typeof(ITemplateValidationService), typeof(TemplateValidationService))]
    [InlineData(typeof(ICalculationPackCatalog), typeof(CalculationPackCatalog))]
    [InlineData(typeof(ICalculationPackValidationService), typeof(CalculationPackValidationService))]
    [InlineData(typeof(IVerificationArtefactCatalog), typeof(VerificationArtefactCatalog))]
    [InlineData(typeof(IVerificationArtefactValidationService), typeof(VerificationArtefactValidationService))]
    [InlineData(typeof(IVerificationTraceService), typeof(VerificationTraceService))]
    [InlineData(typeof(IDesignReviewCatalog), typeof(DesignReviewCatalog))]
    [InlineData(typeof(IDesignReviewValidationService), typeof(DesignReviewValidationService))]
    [InlineData(typeof(ITechnicalDocumentCatalog), typeof(TechnicalDocumentCatalog))]
    [InlineData(typeof(ITechnicalDocumentValidationService), typeof(TechnicalDocumentValidationService))]
    public async Task Host_RegistersEveryEngineeringAssetLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryAssetLibrary_IsAnOrdinarySingleton()
    {
        // Two catalogues over one store would each hold their own write
        // locks, and the shared base's check-then-write atomicity would be
        // silently lost.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(ITemplateCatalog), typeof(ICalculationPackCatalog),
                         typeof(IVerificationArtefactCatalog), typeof(IDesignReviewCatalog),
                         typeof(ITechnicalDocumentCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheTraceService_ReadsTheSameLibraryTheContainerHandsOut()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var artefacts = (IVerificationArtefactCatalog)host.Services!.GetService(typeof(IVerificationArtefactCatalog))!;
            var trace = (IVerificationTraceService)host.Services!.GetService(typeof(IVerificationTraceService))!;

            await artefacts.RegisterAsync("ver-host-probe", AssetFixtures.Artefact("VER-HOST-PROBE"), AssetFixtures.Verified());

            var result = await trace.TraceAsync(AssetFixtures.RequirementId);

            Assert.Single(result.Artefacts);
            Assert.Equal(VerificationStanding.Passed, result.Standing);
        });
    }

    [Fact]
    public async Task ATemplatePinnedThroughTheHost_CarriesTheRealRevision()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var templates = (ITemplateCatalog)host.Services!.GetService(typeof(ITemplateCatalog))!;

            await templates.RegisterAsync("tpl-host-probe", AssetFixtures.Template("TPL-HOST-PROBE"), AssetFixtures.Verified());

            var pin = await templates.PinAsync("TPL-HOST-PROBE");

            Assert.NotNull(pin);
            Assert.Equal(TemplateCatalog.TemplateLibraryName, pin.Library);
            Assert.Equal(1, pin.RevisionNumber);
        });
    }
}
