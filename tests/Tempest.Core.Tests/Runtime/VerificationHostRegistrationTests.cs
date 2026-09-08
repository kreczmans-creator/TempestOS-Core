using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Verification Framework is wired into
// the real, unmodified TempestHost exactly as ADR-0057 specifies -
// IVerificationService resolvable, ordinary singleton semantics, and the
// service genuinely reuses the same IEngineeringDocumentStore Materials/
// Calculations resolve, not a second, independent one.
public class VerificationHostRegistrationTests
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

    [Fact]
    public async Task Host_RegistersIVerificationService_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var service = host.Services!.GetService(typeof(IVerificationService));

            Assert.IsType<VerificationService>(service);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingIVerificationServiceTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(IVerificationService));
            var second = host.Services!.GetService(typeof(IVerificationService));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_VerificationService_CanRecordThroughTheRealDocumentStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var documentStore = (IEngineeringDocumentStore)host.Services!.GetService(typeof(IEngineeringDocumentStore));
            var service = (IVerificationService)host.Services!.GetService(typeof(IVerificationService));

            var subject = await documentStore.CreateAsync("Requirement", "registration-test content");
            var record = await service.RecordAsync(subject.Id, VerificationOutcome.Pass, "inspection", new VerificationContext());

            Assert.Equal(subject.Id, record.SubjectDocumentId);
        });
    }
}
