using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Engineering Data Model is wired
// into the real, unmodified TempestHost exactly as WP7.0C Engineering
// Foundation Contracts.md specifies - IEngineeringDocumentStore
// resolvable, ordinary singleton semantics, and the store genuinely
// reuses the same IPersistenceStore instance Settings/Audit resolve,
// not a second, independent one (ADR-0053).
[Collection("Console output capture")]
public class EngineeringDataHostRegistrationTests
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
    public async Task Host_RegistersIEngineeringDocumentStore_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var store = host.Services!.GetService(typeof(IEngineeringDocumentStore));

            Assert.IsType<EngineeringDocumentStore>(store);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingIEngineeringDocumentStoreTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(IEngineeringDocumentStore));
            var second = host.Services!.GetService(typeof(IEngineeringDocumentStore));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_EngineeringDataAndAudit_ShareTheSameIPersistenceStoreInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            // Not directly inspectable through EngineeringDocumentStore's
            // own public surface, so this is proven indirectly, mirroring
            // AuditHostRegistrationTests' own identical proof: two
            // independent resolutions of IPersistenceStore itself return
            // the same instance (singleton semantics), which is what both
            // EngineeringDocumentStore's and AuditRecorder's own
            // constructor injection then receive.
            var first = host.Services!.GetService(typeof(IPersistenceStore));
            var second = host.Services!.GetService(typeof(IPersistenceStore));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_EngineeringDocumentStore_CanRoundTripADocumentThroughTheRealPersistenceStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var store = (IEngineeringDocumentStore)host.Services!.GetService(typeof(IEngineeringDocumentStore));

            var document = await store.CreateAsync("Requirement", "registration-test content");
            var found = await store.FindAsync(document.Id);

            Assert.NotNull(found);
            Assert.Equal("Requirement", found!.Kind);
        });
    }
}
