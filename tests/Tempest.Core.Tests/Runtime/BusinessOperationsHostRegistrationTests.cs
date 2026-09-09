using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.BusinessOperations.Finance;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for the kept half of `P04` (Business OS):
// Organisation, Contact, Budget. WP 18.0C (D-028): the Interaction,
// FinancialEntry, Purchasing, Quality and Records rows, and the
// BudgetPositionService test that reads FinancialEntry, moved to
// tests/Frozen/Tempest.Core.Tests/Runtime/
// BusinessOperationsHostRegistrationTests.cs the same day those libraries
// were frozen.
public class BusinessOperationsHostRegistrationTests
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

    [Theory]
    [InlineData(typeof(IOrganisationCatalog), typeof(OrganisationCatalog))]
    [InlineData(typeof(IContactCatalog), typeof(ContactCatalog))]
    [InlineData(typeof(IOrganisationValidationService), typeof(OrganisationValidationService))]
    [InlineData(typeof(IBudgetCatalog), typeof(BudgetCatalog))]
    public async Task Host_RegistersEveryKeptBusinessOperationsLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryKeptOperationalLibrary_IsAnOrdinarySingleton()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IOrganisationCatalog), typeof(IContactCatalog), typeof(IBudgetCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }
}
