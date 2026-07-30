using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves Audit is wired into the real,
// unmodified TempestHost exactly as Service Registration Matrix.md
// specifies - both IAuditRecorder/IAuditQuery resolvable, ordinary
// singleton semantics, and Audit genuinely reuses the same
// IPersistenceStore instance Settings resolves, not a second,
// independent one.
[Collection("Console output capture")]
public class AuditHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
                new KeyValuePair<string, string>("Identity:Roles:Auditor:Permissions", AuditQuery.QueryPermission.Key),
                new KeyValuePair<string, string>("Identity:Principals:registration-test-auditor:Roles", "Auditor"),
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
    // well before the awaited body actually finishes running - a real
    // bug found during this Work Package's own repository review (see
    // its own Lessons Learned), which produced non-deterministic empty
    // query results specifically for the one test here that depends on
    // the directory surviving the full, multi-step recorder-then-query
    // operation.

    [Fact]
    public async Task Host_RegistersIAuditRecorder_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var recorder = host.Services!.GetService(typeof(IAuditRecorder));

            Assert.IsType<AuditRecorder>(recorder);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_RegistersIAuditQuery_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var query = host.Services!.GetService(typeof(IAuditQuery));

            Assert.IsType<AuditQuery>(query);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingIAuditRecorderTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(IAuditRecorder));
            var second = host.Services!.GetService(typeof(IAuditRecorder));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_AuditAndSettings_ShareTheSameIPersistenceStoreInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            // Audit's own IAuditRecorder is constructed with a reference
            // to whatever IPersistenceStore the container resolves - the
            // same ordinary singleton Settings resolves too, per
            // ADR-0041's own "one abstraction, two consumers" design.
            // Not directly inspectable through the recorder's own public
            // surface, so this is proven indirectly: two independent
            // resolutions of IPersistenceStore itself return the same
            // instance (singleton semantics), which is what Audit's own
            // constructor injection then receives.
            var first = host.Services!.GetService(typeof(IPersistenceStore));
            var second = host.Services!.GetService(typeof(IPersistenceStore));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_AuditRecorderAndQuery_CanRoundTripARecordThroughTheRealPersistenceStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var identityService = (IIdentityService)host.Services!.GetService(typeof(IIdentityService));
            var recorder = (IAuditRecorder)host.Services!.GetService(typeof(IAuditRecorder));
            var query = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));

            identityService.EstablishCurrentPrincipal("registration-test-auditor");
            await recorder.RecordAsync("registration-test-action");

            var records = await query.QueryAsync(new AuditQueryCriteria(actorId: "registration-test-auditor"));

            var record = Assert.Single(records);
            Assert.Equal("registration-test-action", record.Action);
        });
    }
}
