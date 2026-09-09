using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for the kept half of `Group C` (P07): RateCard.
// WP 18.0C (D-028): the Contracts, Risk, Assets (IP/data), Finance
// (Assumption/Scenario/Control), Development (Opportunity/Pipeline),
// Operating and PricingService rows, and the contract-service reasoning
// test, moved to tests/Frozen/Tempest.Core.Tests/Runtime/
// BusinessGovernanceHostRegistrationTests.cs the same day those libraries
// were frozen.
public class BusinessGovernanceHostRegistrationTests
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
    [InlineData(typeof(IRateCardCatalog), typeof(RateCardCatalog))]
    [InlineData(typeof(IRateCardValidationService), typeof(RateCardValidationService))]
    public async Task Host_RegistersEveryKeptBusinessGovernanceLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RateCardCatalog_IsAnOrdinarySingleton()
    {
        // Two catalogues over one store would each hold their own write
        // locks, and the check-then-write atomicity the shared base
        // depends on would be silently lost.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.Same(
                host.Services!.GetService(typeof(IRateCardCatalog)),
                host.Services!.GetService(typeof(IRateCardCatalog)));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AddingP07ChangedNothingAboutP01()
    {
        // P07 reads the platform's own document store, persistence and
        // identity. It does not read the reference libraries, and they did
        // not change to accommodate it.
        //
        // WP 18.0C (D-028): this used to check P01's MaterialCatalog
        // against Contracts' own ContractTemplateCatalog; Contracts is now
        // frozen, so the P07 half of the check reads RateCardCatalog,
        // still live, instead.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType<Tempest.Core.Materials.MaterialCatalog>(
                host.Services!.GetService(typeof(Tempest.Core.Materials.IMaterialCatalog)));
            Assert.IsType<RateCardCatalog>(host.Services!.GetService(typeof(IRateCardCatalog)));

            return Task.CompletedTask;
        });
    }
}
