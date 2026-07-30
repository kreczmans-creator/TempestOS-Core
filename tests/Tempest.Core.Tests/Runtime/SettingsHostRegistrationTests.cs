using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves Persistence and Settings are wired into
// the real, unmodified TempestHost exactly as Service Registration
// Matrix.md specifies - both resolvable, ordinary singleton semantics,
// registered ahead of any module's own construction (Phase 6).
[Collection("Console output capture")]
public class SettingsHostRegistrationTests
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

    // Every test below is deliberately `async Task`, awaiting
    // RunAgainstRunningHostAsync directly - not `Task` returning the call
    // unawaited. With a `using` resource declared in the same method, an
    // unawaited return disposes that resource (deleting the temp
    // directory) the instant this method returns control to the caller,
    // which happens well before the awaited body actually runs to
    // completion - a real, found-and-fixed bug (see this Work Package's
    // own Lessons Learned) that produced non-deterministic empty results
    // for any test that actually depends on the directory surviving the
    // full operation.

    [Fact]
    public async Task Host_RegistersIPersistenceStore_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var store = host.Services!.GetService(typeof(IPersistenceStore));

            Assert.IsType<PersistenceStore>(store);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_RegistersISettingsProvider_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var provider = host.Services!.GetService(typeof(ISettingsProvider));

            Assert.IsType<SettingsProvider>(provider);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingIPersistenceStoreTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(IPersistenceStore));
            var second = host.Services!.GetService(typeof(IPersistenceStore));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingISettingsProviderTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(ISettingsProvider));
            var second = host.Services!.GetService(typeof(ISettingsProvider));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_SettingsProvider_CanRoundTripAValueThroughTheRealPersistenceStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var settingsProvider = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
            settingsProvider.RegisterDefinition(new SettingDefinition("registration-test.key", "Test", "default"));

            await settingsProvider.SetValueAsync("registration-test.key", "written-through-real-host");
            var value = await settingsProvider.GetValueAsync("registration-test.key");

            Assert.Equal("written-through-real-host", value);
        });
    }
}
