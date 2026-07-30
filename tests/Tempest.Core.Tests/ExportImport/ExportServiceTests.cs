using Tempest.Core.DependencyInjection;
using Tempest.Core.ExportImport;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.ExportImport;

// Proves the approved Export/Import contract against the real
// ExportService implementation - every source's own bytes are captured
// under its own IExportableKind.Kind (or, absent that, its own runtime
// type name), framed together by IExportFormat, with a source's own
// exception or a destination stream's own IOException propagating
// unmodified (Platform Service Contracts.md's own Failure Behaviour).
public class ExportServiceTests
{
    // ------------------------------------------------------------------
    // Single-source export
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_SingleSource_WritesAFrameableArtifact()
    {
        var service = new ExportService(new JsonExportFormat());
        var source = new RecordingExportable("kind.a", 1, "hello"u8.ToArray());
        using var destination = new MemoryStream();

        await service.ExportAsync(destination, [source]);

        Assert.True(source.ExportCalled);
        Assert.True(destination.Length > 0);
    }

    [Fact]
    public async Task ExportAsync_SourceWithoutIExportableKind_FallsBackToItsOwnRuntimeTypeName()
    {
        var format = new JsonExportFormat();
        var service = new ExportService(format);
        var source = new UnkeyedRecordingExportable(1, "hello"u8.ToArray());
        using var destination = new MemoryStream();

        await service.ExportAsync(destination, [source]);

        destination.Position = 0;
        var sections = await format.ReadAsync(destination);

        Assert.Equal(typeof(UnkeyedRecordingExportable).FullName, Assert.Single(sections).Kind);
    }

    // ------------------------------------------------------------------
    // Multi-source export
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_MultipleSources_WritesEveryOneAsItsOwnSection()
    {
        var format = new JsonExportFormat();
        var service = new ExportService(format);
        var sourceA = new RecordingExportable("kind.a", 1, "a"u8.ToArray());
        var sourceB = new RecordingExportable("kind.b", 2, "b"u8.ToArray());
        using var destination = new MemoryStream();

        await service.ExportAsync(destination, [sourceA, sourceB]);

        destination.Position = 0;
        var sections = await format.ReadAsync(destination);

        Assert.Equal(2, sections.Count);
        Assert.Contains(sections, s => s.Kind == "kind.a" && s.SchemaVersion == 1);
        Assert.Contains(sections, s => s.Kind == "kind.b" && s.SchemaVersion == 2);
    }

    // ------------------------------------------------------------------
    // Failure propagation
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_SourceThrows_ExceptionPropagatesUnmodifiedToTheCaller()
    {
        var service = new ExportService(new JsonExportFormat());
        var source = new RecordingExportable("kind.a", 1, [], new InvalidOperationException("boom"));
        using var destination = new MemoryStream();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExportAsync(destination, [source]));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task ExportAsync_DestinationStreamThrows_IOExceptionPropagatesUnmodifiedToTheCaller()
    {
        var service = new ExportService(new JsonExportFormat());
        var source = new RecordingExportable("kind.a", 1, "hello"u8.ToArray());
        using var destination = new ThrowingStream(throwOnWrite: true);

        await Assert.ThrowsAsync<IOException>(() => service.ExportAsync(destination, [source]));
    }

    // ------------------------------------------------------------------
    // Argument validation
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_NullDestination_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new ExportService(new JsonExportFormat()).ExportAsync(null!, []));

    [Fact]
    public async Task ExportAsync_NullSources_ThrowsArgumentNullException()
    {
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new ExportService(new JsonExportFormat()).ExportAsync(destination, null!));
    }

    [Fact]
    public void Constructor_NullFormat_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new ExportService(null!));

    // ------------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_Succeeds_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var service = new ExportService(new JsonExportFormat(), logger);
        using var destination = new MemoryStream();

        await service.ExportAsync(destination, [new RecordingExportable("kind.a", 1, "hello"u8.ToArray())]);

        Assert.True(logger.HasEntryAt(LogLevel.Information, "Exported"));
    }

    // ------------------------------------------------------------------
    // Platform Service registration (ordinary Phase 6 singleton)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesIExportServiceToExportService()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.AddInstance<IExportFormat>(new JsonExportFormat());
        services.Singleton<IExportService, ExportService>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(IExportService));

        Assert.IsType<ExportService>(resolved);
    }
}
