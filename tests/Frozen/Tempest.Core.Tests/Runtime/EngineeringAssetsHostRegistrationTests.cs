using Tempest.Core.Configuration;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// WP 18.0C (D-028): the archived half of the live
// tests/Tempest.Core.Tests/Runtime/EngineeringAssetsHostRegistrationTests.cs,
// split out the same day Templates/CalculationPacks/Verification stayed
// live and registered. DesignReviews, TechnicalDocumentation.
public class EngineeringAssetsArchivedHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    [Theory]
    [InlineData(typeof(IDesignReviewCatalog), typeof(DesignReviewCatalog))]
    [InlineData(typeof(IDesignReviewValidationService), typeof(DesignReviewValidationService))]
    [InlineData(typeof(ITechnicalDocumentCatalog), typeof(TechnicalDocumentCatalog))]
    [InlineData(typeof(ITechnicalDocumentValidationService), typeof(TechnicalDocumentValidationService))]
    public async Task Host_RegistersEveryArchivedEngineeringAssetLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryArchivedAssetLibrary_IsAnOrdinarySingleton()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IDesignReviewCatalog), typeof(ITechnicalDocumentCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }
}
