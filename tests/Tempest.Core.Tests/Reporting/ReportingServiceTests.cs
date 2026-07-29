using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;
using Tempest.Core.Reporting;

namespace Tempest.Core.Tests.Reporting;

// Proves the approved Reporting Framework contract against the real
// ReportingService implementation - imperative registration, dispatch
// by definition Id, unmodified renderer-failure propagation (mirroring
// ADR-0038's Command dispatch model, not the Event Bus's own isolation
// model), and safe concurrent generation once registration is complete.
public class ReportingServiceTests
{
    // ------------------------------------------------------------------
    // Registration
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterDefinition_ThenGenerate_DispatchesToTheRegisteredRenderer()
    {
        var service = new ReportingService();
        var renderer = new RecordingRenderer<RecordedReportDefinitionA>();
        var definition = new RecordedReportDefinitionA();

        service.RegisterDefinition(definition, renderer);

        Assert.Single(service.RegisteredDefinitions);
        Assert.Same(definition, service.RegisteredDefinitions[0]);
    }

    [Fact]
    public void RegisterDefinition_DuplicateId_ThrowsDuplicateReportDefinitionException()
    {
        var service = new ReportingService();
        service.RegisterDefinition(new RecordedReportDefinitionA(), new RecordingRenderer<RecordedReportDefinitionA>());

        var exception = Assert.Throws<DuplicateReportDefinitionException>(() =>
            service.RegisterDefinition(new RecordedReportDefinitionA(), new RecordingRenderer<RecordedReportDefinitionA>()));

        Assert.Equal("definition.a", exception.DefinitionId);
    }

    [Fact]
    public void RegisterDefinition_NullDefinition_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new ReportingService().RegisterDefinition<RecordedReportDefinitionA>(null!, new RecordingRenderer<RecordedReportDefinitionA>()));

    [Fact]
    public void RegisterDefinition_NullRenderer_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new ReportingService().RegisterDefinition(new RecordedReportDefinitionA(), null!));

    [Fact]
    public void RegisteredDefinitions_NoRegistrations_IsEmpty() =>
        Assert.Empty(new ReportingService().RegisteredDefinitions);

    [Fact]
    public void RegisteredDefinitions_MultipleRegistrations_ListsEveryOne()
    {
        var service = new ReportingService();
        service.RegisterDefinition(new RecordedReportDefinitionA(), new RecordingRenderer<RecordedReportDefinitionA>());
        service.RegisterDefinition(new RecordedReportDefinitionB(), new RecordingRenderer<RecordedReportDefinitionB>());

        Assert.Equal(2, service.RegisteredDefinitions.Count);
        Assert.Contains(service.RegisteredDefinitions, d => d.Id == "definition.a");
        Assert.Contains(service.RegisteredDefinitions, d => d.Id == "definition.b");
    }

    // ------------------------------------------------------------------
    // Dispatch
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_RegisteredDefinition_ReturnsTheRenderersOwnResult()
    {
        var service = new ReportingService();
        var expected = new ReportResult("text/plain", "hello"u8.ToArray());
        var renderer = new RecordingRenderer<RecordedReportDefinitionA>((d, r, ct) => Task.FromResult(expected));
        service.RegisterDefinition(new RecordedReportDefinitionA(), renderer);

        var result = await service.GenerateAsync("definition.a", new ReportRequest(new Dictionary<string, string>()));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GenerateAsync_PassesTheRequestThroughToTheRenderer()
    {
        var service = new ReportingService();
        var renderer = new RecordingRenderer<RecordedReportDefinitionA>();
        service.RegisterDefinition(new RecordedReportDefinitionA(), renderer);
        var request = new ReportRequest(new Dictionary<string, string> { ["x"] = "y" });

        await service.GenerateAsync("definition.a", request);

        Assert.Same(request, Assert.Single(renderer.Received));
    }

    [Fact]
    public async Task GenerateAsync_UnregisteredId_ThrowsReportDefinitionNotFoundException()
    {
        var service = new ReportingService();

        var exception = await Assert.ThrowsAsync<ReportDefinitionNotFoundException>(() =>
            service.GenerateAsync("missing", new ReportRequest(new Dictionary<string, string>())));

        Assert.Equal("missing", exception.DefinitionId);
    }

    [Fact]
    public async Task GenerateAsync_NullDefinitionId_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new ReportingService().GenerateAsync(null!, new ReportRequest(new Dictionary<string, string>())));

    [Fact]
    public async Task GenerateAsync_NullRequest_ThrowsArgumentNullException()
    {
        var service = new ReportingService();
        service.RegisterDefinition(new RecordedReportDefinitionA(), new RecordingRenderer<RecordedReportDefinitionA>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GenerateAsync("definition.a", null!));
    }

    // ------------------------------------------------------------------
    // Renderer failure propagation (ADR-0038's Command dispatch model,
    // not the Event Bus's own per-subscriber isolation) and logging
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_RendererThrows_ExceptionPropagatesUnmodifiedToTheCaller()
    {
        var service = new ReportingService();
        var renderer = new RecordingRenderer<RecordedReportDefinitionA>((d, r, ct) => throw new InvalidOperationException("boom"));
        service.RegisterDefinition(new RecordedReportDefinitionA(), renderer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync("definition.a", new ReportRequest(new Dictionary<string, string>())));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_RendererThrows_LogsAtWarningLevel()
    {
        var logger = new RecordingLevelLogger();
        var service = new ReportingService(logger);
        var renderer = new RecordingRenderer<RecordedReportDefinitionA>((d, r, ct) => throw new InvalidOperationException("boom"));
        service.RegisterDefinition(new RecordedReportDefinitionA(), renderer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync("definition.a", new ReportRequest(new Dictionary<string, string>())));

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "generation failed"));
    }

    [Fact]
    public async Task GenerateAsync_Succeeds_LogsNothingAtWarningLevel()
    {
        var logger = new RecordingLevelLogger();
        var service = new ReportingService(logger);
        service.RegisterDefinition(new RecordedReportDefinitionA(), new RecordingRenderer<RecordedReportDefinitionA>());

        await service.GenerateAsync("definition.a", new ReportRequest(new Dictionary<string, string>()));

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    // ------------------------------------------------------------------
    // Concurrency
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_ConcurrentCallsForDistinctDefinitions_BothCompleteCorrectly()
    {
        var service = new ReportingService();
        service.RegisterDefinition(new RecordedReportDefinitionA(),
            new RecordingRenderer<RecordedReportDefinitionA>(async (d, r, ct) => { await Task.Delay(20, ct); return new ReportResult("text/plain", "a"u8.ToArray()); }));
        service.RegisterDefinition(new RecordedReportDefinitionB(),
            new RecordingRenderer<RecordedReportDefinitionB>(async (d, r, ct) => { await Task.Delay(20, ct); return new ReportResult("text/plain", "b"u8.ToArray()); }));

        var requestA = service.GenerateAsync("definition.a", new ReportRequest(new Dictionary<string, string>()));
        var requestB = service.GenerateAsync("definition.b", new ReportRequest(new Dictionary<string, string>()));
        await Task.WhenAll(requestA, requestB);

        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((await requestA).Content));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((await requestB).Content));
    }

    [Fact]
    public async Task GenerateAsync_ManyConcurrentCallsForTheSameDefinition_AllCompleteCorrectly()
    {
        var service = new ReportingService();
        var callCount = 0;
        service.RegisterDefinition(new RecordedReportDefinitionA(), new RecordingRenderer<RecordedReportDefinitionA>(async (d, r, ct) =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(5, ct);
            return new ReportResult("text/plain", []);
        }));

        var tasks = Enumerable.Range(0, 25)
            .Select(_ => service.GenerateAsync("definition.a", new ReportRequest(new Dictionary<string, string>())))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(25, callCount);
    }

    // ------------------------------------------------------------------
    // Platform Service registration (ADR-0040: an ordinary singleton, no
    // Composition Root treatment needed)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesIReportingServiceToReportingService()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IReportingService, ReportingService>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(IReportingService));

        Assert.IsType<ReportingService>(resolved);
    }

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesTheSameInstanceEveryTime()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IReportingService, ReportingService>();
        var provider = new TempestServiceProvider(services);

        var first = provider.GetService(typeof(IReportingService));
        var second = provider.GetService(typeof(IReportingService));

        Assert.Same(first, second);
    }
}
