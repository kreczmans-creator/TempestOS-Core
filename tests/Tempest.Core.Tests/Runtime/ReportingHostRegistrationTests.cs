using Tempest.Core.Reporting;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Reporting Framework is wired into
// the real, unmodified TempestHost exactly as Service Registration
// Matrix.md specifies - IReportingService resolvable, ordinary
// singleton semantics, and a real register/generate round trip through
// the container-resolved instance.
[Collection("Console output capture")]
public class ReportingHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();
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
    public Task Host_RegistersIReportingService_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var reportingService = host.Services!.GetService(typeof(IReportingService));

            Assert.IsType<ReportingService>(reportingService);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ResolvingIReportingServiceTwice_ReturnsTheSameInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            var first = host.Services!.GetService(typeof(IReportingService));
            var second = host.Services!.GetService(typeof(IReportingService));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ReportingService_CanRoundTripARegisterAndGenerateThroughTheRealContainerResolvedInstance() =>
        RunAgainstRunningHostAsync(async host =>
        {
            var reportingService = (IReportingService)host.Services!.GetService(typeof(IReportingService));
            var definition = new RegistrationRoundTripReportDefinition();
            var renderer = new RegistrationRoundTripReportRenderer();

            reportingService.RegisterDefinition(definition, renderer);
            var result = await reportingService.GenerateAsync(definition.Id, new ReportRequest(new Dictionary<string, string>()));

            Assert.Equal("text/plain", result.ContentType);
        });

    private sealed class RegistrationRoundTripReportDefinition : IReportDefinition
    {
        public string Id => "tempest.tests.registration-round-trip";
        public string Name => "Registration Round Trip";
    }

    private sealed class RegistrationRoundTripReportRenderer : IReportRenderer<RegistrationRoundTripReportDefinition>
    {
        public Task<ReportResult> RenderAsync(RegistrationRoundTripReportDefinition definition, ReportRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReportResult("text/plain", []));
    }
}
