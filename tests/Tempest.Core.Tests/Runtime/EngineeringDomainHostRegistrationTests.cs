using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Engineering Domain's own shared
// services are wired into the real, unmodified TempestHost, and that
// EngineeringDomainContext genuinely reuses the same, real IEngineeringDocumentStore
// every Engineering Core sibling resolves - not a second, in-memory one
// (that substitution is reserved for tests and the sample module's own
// composition-root wiring, never for the Host).
[Collection("Console output capture")]
public class EngineeringDomainHostRegistrationTests
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
    [InlineData(typeof(IEngineeringObjectRepository), typeof(InMemoryEngineeringObjectRepository))]
    [InlineData(typeof(IEngineeringRelationshipRepository), typeof(InMemoryEngineeringRelationshipRepository))]
    [InlineData(typeof(ILifecycleTransitionTable), typeof(LifecycleTransitionTable))]
    [InlineData(typeof(IValidationRuleSet), typeof(ValidationRuleSet))]
    [InlineData(typeof(IReferenceIntegrityChecker), typeof(ReferenceIntegrityChecker))]
    [InlineData(typeof(IRelationshipDiscovery), typeof(RelationshipDiscoveryService))]
    [InlineData(typeof(IDependencyTraversal), typeof(RelationshipDiscoveryService))]
    [InlineData(typeof(IImpactAnalysis), typeof(RelationshipDiscoveryService))]
    [InlineData(typeof(IEvidenceComposer), typeof(EvidenceComposer))]
    [InlineData(typeof(EngineeringDomainContext), typeof(EngineeringDomainContext))]
    public async Task Host_RegistersEngineeringDomainService_Resolvable(Type serviceType, Type expectedImplementationType)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var service = host.Services!.GetService(serviceType);

            Assert.IsType(expectedImplementationType, service);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingEngineeringDomainContextTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(EngineeringDomainContext));
            var second = host.Services!.GetService(typeof(EngineeringDomainContext));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_EngineeringDomainContext_SharesTheSameDocumentStoreAsEngineeringData()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var documentStore = (IEngineeringDocumentStore)host.Services!.GetService(typeof(IEngineeringDocumentStore));
            var context = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            var factory = new EngineeringObjectFactory<Portfolio>(
                "Portfolio", context, (doc, rev) => new Portfolio(doc, rev, context, "HOST-PORT-001", "Host Portfolio", EngineeringObjectMetadata.Empty));

            var portfolio = await factory.CreateAsync("Host registration test.");
            var document = await documentStore.FindAsync(portfolio.Id);

            Assert.NotNull(document);
            Assert.Equal("Portfolio", document!.Kind);
        });
    }
}
