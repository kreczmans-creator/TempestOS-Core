using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessGovernance.Quotations;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for the live half of `Group C` (P07):
// Contracts, RateCard, PricingService and Quotations.
// WP 18.0C (D-028): the Risk, Assets (IP/data), Finance (Assumption/
// Scenario/Control), Development (Opportunity/Pipeline) and Operating
// rows moved to tests/Frozen/Tempest.Core.Tests/Runtime/
// BusinessGovernanceHostRegistrationTests.cs the same day those libraries
// were frozen. The Contracts and PricingService rows, and the
// contract-service reasoning test, moved with them and returned with
// ADR-0150 (2026-09-21); the Quotation rows are new with the same ADR.
public class BusinessGovernanceHostRegistrationTests
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
    [InlineData(typeof(IContractTemplateCatalog), typeof(ContractTemplateCatalog))]
    [InlineData(typeof(IContractTemplateValidationService), typeof(ContractTemplateValidationService))]
    [InlineData(typeof(IIssuedContractCatalog), typeof(IssuedContractCatalog))]
    [InlineData(typeof(IIssuedContractValidationService), typeof(IssuedContractValidationService))]
    [InlineData(typeof(IContractService), typeof(ContractService))]
    [InlineData(typeof(IRateCardCatalog), typeof(RateCardCatalog))]
    [InlineData(typeof(IRateCardValidationService), typeof(RateCardValidationService))]
    [InlineData(typeof(IPricingService), typeof(PricingService))]
    [InlineData(typeof(IQuotationCatalog), typeof(QuotationCatalog))]
    [InlineData(typeof(IQuotationValidationService), typeof(QuotationValidationService))]
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
    public async Task EveryBusinessLibrary_IsAnOrdinarySingleton()
    {
        // Two catalogues over one store would each hold their own write
        // locks, and the check-then-write atomicity the shared base
        // depends on would be silently lost.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IContractTemplateCatalog), typeof(IIssuedContractCatalog),
                         typeof(IRateCardCatalog), typeof(IQuotationCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AReasoningService_ReadsTheSameLibraryTheContainerHandsOut()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var contracts = (IIssuedContractCatalog)host.Services!.GetService(typeof(IIssuedContractCatalog))!;
            var service = (IContractService)host.Services!.GetService(typeof(IContractService))!;

            await contracts.RegisterAsync(
                "con-host-probe",
                new IssuedContract
                {
                    Reference = "CON-HOST-PROBE",
                    Title = "Host registration probe",
                    Parties = Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Parties(),
                    Governance = Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Governance(),
                    Obligations =
                    [
                        new ContractObligation("OB-1", "A probe obligation.", "A", "B",
                            DueBy: Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Today.AddDays(-1)),
                    ],
                },
                Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Verified());

            // The service sees what the catalogue wrote, which it could
            // only do if both are the same instance.
            var position = await service.ReportObligationsAsync(
                Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Today);

            Assert.Single(position.OverdueObligations);
        });
    }

    [Fact]
    public async Task AddingP07ChangedNothingAboutP01()
    {
        // P07 reads the platform's own document store, persistence and
        // identity. It does not read the reference libraries, and they did
        // not change to accommodate it.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType<Tempest.Core.Materials.MaterialCatalog>(
                host.Services!.GetService(typeof(Tempest.Core.Materials.IMaterialCatalog)));
            Assert.IsType<ContractTemplateCatalog>(host.Services!.GetService(typeof(IContractTemplateCatalog)));
            Assert.IsType<RateCardCatalog>(host.Services!.GetService(typeof(IRateCardCatalog)));

            return Task.CompletedTask;
        });
    }
}
