using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.BusinessOperations.Finance;
using Tempest.Core.BusinessOperations.Purchasing;
using Tempest.Core.BusinessOperations.Quality;
using Tempest.Core.BusinessOperations.Records;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// WP 18.0C (D-028): the archived half of the live
// tests/Tempest.Core.Tests/Runtime/BusinessOperationsHostRegistrationTests.cs,
// split out the same day Organisation/Contact/OrganisationValidationService/
// Budget stayed live and registered. Interaction, FinancialEntry,
// Purchasing, Quality, Records.
public class BusinessOperationsArchivedHostRegistrationTests
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
    [InlineData(typeof(IInteractionCatalog), typeof(InteractionCatalog))]
    [InlineData(typeof(ICrmValidationService), typeof(CrmValidationService))]
    [InlineData(typeof(IFinancialEntryCatalog), typeof(FinancialEntryCatalog))]
    [InlineData(typeof(IBudgetValidationService), typeof(BudgetValidationService))]
    [InlineData(typeof(IBudgetPositionService), typeof(BudgetPositionService))]
    [InlineData(typeof(IPurchaseRequisitionCatalog), typeof(PurchaseRequisitionCatalog))]
    [InlineData(typeof(IPurchaseRequisitionValidationService), typeof(PurchaseRequisitionValidationService))]
    [InlineData(typeof(IPurchaseOrderCatalog), typeof(PurchaseOrderCatalog))]
    [InlineData(typeof(IPurchaseOrderValidationService), typeof(PurchaseOrderValidationService))]
    [InlineData(typeof(INonConformanceCatalog), typeof(NonConformanceCatalog))]
    [InlineData(typeof(INonConformanceValidationService), typeof(NonConformanceValidationService))]
    [InlineData(typeof(IBusinessRecordCatalog), typeof(BusinessRecordCatalog))]
    [InlineData(typeof(IBusinessRecordValidationService), typeof(BusinessRecordValidationService))]
    public async Task Host_RegistersEveryArchivedBusinessOperationsLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryArchivedOperationalLibrary_IsAnOrdinarySingleton()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IInteractionCatalog), typeof(IFinancialEntryCatalog),
                         typeof(IPurchaseRequisitionCatalog), typeof(IPurchaseOrderCatalog),
                         typeof(INonConformanceCatalog), typeof(IBusinessRecordCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheBudgetPositionService_ReadsTheSameLibrariesTheContainerHandsOut()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var budgets = (IBudgetCatalog)host.Services!.GetService(typeof(IBudgetCatalog))!;
            var entries = (IFinancialEntryCatalog)host.Services!.GetService(typeof(IFinancialEntryCatalog))!;
            var position = (IBudgetPositionService)host.Services!.GetService(typeof(IBudgetPositionService))!;

            await budgets.RegisterAsync("bud-host-probe", OperationsFixtures.Budget("BUD-HOST-PROBE"), OperationsFixtures.Verified());
            await entries.RegisterAsync(
                "fe-host-probe",
                OperationsFixtures.Entry("FE-HOST-PROBE", 2_500m, budget: "BUD-HOST-PROBE"),
                OperationsFixtures.Verified());

            var result = await position.PositionAsync("BUD-HOST-PROBE");

            Assert.NotNull(result);
            Assert.Equal(OperationsFixtures.Gbp_(2_500m), result.Committed);
            Assert.Equal(OperationsFixtures.Gbp_(7_500m), result.UncommittedRemaining);
        });
    }
}
