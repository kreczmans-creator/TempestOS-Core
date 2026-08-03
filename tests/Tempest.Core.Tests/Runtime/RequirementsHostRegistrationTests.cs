using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Requirements Engine is wired into the
// real, unmodified TempestHost exactly as ADR-0058 specifies - IRequirementsService
// resolvable, ordinary singleton semantics, and the service genuinely reuses
// the same IEngineeringDocumentStore every Engineering Core sibling resolves,
// not a second, independent one.
[Collection("Console output capture")]
public class RequirementsHostRegistrationTests
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
    public async Task Host_RegistersIRequirementsService_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var service = host.Services!.GetService(typeof(IRequirementsService));

            Assert.IsType<RequirementsService>(service);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingIRequirementsServiceTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(IRequirementsService));
            var second = host.Services!.GetService(typeof(IRequirementsService));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_RequirementsService_CanCreateThroughTheRealDocumentStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var service = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

            var requirement = await service.CreateAsync("HOST-REQ-001", "Registration-test requirement.");

            Assert.Equal("HOST-REQ-001", requirement.Identifier);
        });
    }

    [Fact]
    public async Task Host_RequirementsService_SharesTheSameDocumentStoreAsEngineeringData()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var documentStore = (IEngineeringDocumentStore)host.Services!.GetService(typeof(IEngineeringDocumentStore));
            var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

            var requirement = await requirementsService.CreateAsync("HOST-REQ-002", "Shared-store test.");
            var document = await documentStore.FindAsync(requirement.Id);

            Assert.NotNull(document);
            Assert.Equal("Requirement", document!.Kind);
        });
    }
}
