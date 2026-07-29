using Tempest.Core.DependencyInjection;
using Tempest.Core.ExportImport;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.ExportImport;

// Proves the approved Export/Import contract against the real
// ImportService implementation - registration by Kind (mirroring
// ReportingService's own RegisterDefinition/GenerateAsync pattern),
// exact schema-version equality, "never a best-effort partial import"
// (every section is validated before any is applied), and unmodified
// failure propagation.
public class ImportServiceTests
{
    private static async Task<byte[]> BuildArtifactAsync(params ExportSection[] sections)
    {
        using var stream = new MemoryStream();
        await new JsonExportFormat().WriteAsync(sections, stream);
        return stream.ToArray();
    }

    // ------------------------------------------------------------------
    // Registration
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterImportable_DuplicateKind_ThrowsDuplicateImportableKindException()
    {
        var service = new ImportService(new JsonExportFormat());
        service.RegisterImportable(new RecordingImportable("kind.a", 1));

        var exception = Assert.Throws<DuplicateImportableKindException>(() =>
            service.RegisterImportable(new RecordingImportable("kind.a", 1)));

        Assert.Equal("kind.a", exception.Kind);
    }

    [Fact]
    public void RegisterImportable_NullImportable_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new ImportService(new JsonExportFormat()).RegisterImportable(null!));

    // ------------------------------------------------------------------
    // Round trip / routing
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_RegisteredKind_RoutesThePayloadToTheMatchingImportable()
    {
        var service = new ImportService(new JsonExportFormat());
        var importable = new RecordingImportable("kind.a", 1);
        service.RegisterImportable(importable);
        var artifact = await BuildArtifactAsync(new ExportSection("kind.a", 1, "hello"u8.ToArray()));

        await service.ImportAsync(new MemoryStream(artifact));

        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(Assert.Single(importable.ReceivedPayloads)));
    }

    [Fact]
    public async Task ImportAsync_MultipleSections_RoutesEachToItsOwnMatchingImportable()
    {
        var service = new ImportService(new JsonExportFormat());
        var importableA = new RecordingImportable("kind.a", 1);
        var importableB = new RecordingImportable("kind.b", 2);
        service.RegisterImportable(importableA);
        service.RegisterImportable(importableB);
        var artifact = await BuildArtifactAsync(
            new ExportSection("kind.a", 1, "a"u8.ToArray()),
            new ExportSection("kind.b", 2, "b"u8.ToArray()));

        await service.ImportAsync(new MemoryStream(artifact));

        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(Assert.Single(importableA.ReceivedPayloads)));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(Assert.Single(importableB.ReceivedPayloads)));
    }

    [Fact]
    public async Task ImportAsync_ExportThenImportRoundTrip_ProducesTheOriginalData()
    {
        var format = new JsonExportFormat();
        var exportService = new ExportService(format);
        var importService = new ImportService(format);
        var exportable = new RecordingExportable("kind.roundtrip", 1, "round-trip data"u8.ToArray());
        var importable = new RecordingImportable("kind.roundtrip", 1);
        importService.RegisterImportable(importable);

        using var artifact = new MemoryStream();
        await exportService.ExportAsync(artifact, [exportable]);
        artifact.Position = 0;
        await importService.ImportAsync(artifact);

        Assert.Equal("round-trip data", System.Text.Encoding.UTF8.GetString(Assert.Single(importable.ReceivedPayloads)));
    }

    // ------------------------------------------------------------------
    // Version compatibility
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_SchemaVersionMismatch_ThrowsIncompatibleExportSchemaException()
    {
        var service = new ImportService(new JsonExportFormat());
        service.RegisterImportable(new RecordingImportable("kind.a", 2));
        var artifact = await BuildArtifactAsync(new ExportSection("kind.a", 1, []));

        var exception = await Assert.ThrowsAsync<IncompatibleExportSchemaException>(() =>
            service.ImportAsync(new MemoryStream(artifact)));

        Assert.Equal("kind.a", exception.Kind);
        Assert.Equal(1, exception.ArtifactSchemaVersion);
        Assert.Equal(2, exception.SupportedSchemaVersion);
    }

    [Fact]
    public async Task ImportAsync_NoImportableRegisteredForKind_ThrowsIncompatibleExportSchemaException()
    {
        var service = new ImportService(new JsonExportFormat());
        var artifact = await BuildArtifactAsync(new ExportSection("kind.unknown", 1, []));

        var exception = await Assert.ThrowsAsync<IncompatibleExportSchemaException>(() =>
            service.ImportAsync(new MemoryStream(artifact)));

        Assert.Equal("kind.unknown", exception.Kind);
        Assert.Null(exception.ArtifactSchemaVersion);
        Assert.Null(exception.SupportedSchemaVersion);
    }

    [Fact]
    public async Task ImportAsync_OneOfMultipleSectionsIsIncompatible_NoSectionIsImported()
    {
        var service = new ImportService(new JsonExportFormat());
        var goodImportable = new RecordingImportable("kind.good", 1);
        var mismatchedImportable = new RecordingImportable("kind.bad", 99);
        service.RegisterImportable(goodImportable);
        service.RegisterImportable(mismatchedImportable);
        var artifact = await BuildArtifactAsync(
            new ExportSection("kind.good", 1, "good"u8.ToArray()),
            new ExportSection("kind.bad", 1, "bad"u8.ToArray()));

        await Assert.ThrowsAsync<IncompatibleExportSchemaException>(() =>
            service.ImportAsync(new MemoryStream(artifact)));

        Assert.Empty(goodImportable.ReceivedPayloads);
        Assert.Empty(mismatchedImportable.ReceivedPayloads);
    }

    // ------------------------------------------------------------------
    // Failure propagation
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_ImportableThrows_ExceptionPropagatesUnmodifiedToTheCaller()
    {
        var service = new ImportService(new JsonExportFormat());
        service.RegisterImportable(new RecordingImportable("kind.a", 1, new InvalidOperationException("boom")));
        var artifact = await BuildArtifactAsync(new ExportSection("kind.a", 1, []));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(new MemoryStream(artifact)));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_SourceStreamThrows_IOExceptionPropagatesUnmodifiedToTheCaller()
    {
        var service = new ImportService(new JsonExportFormat());

        await Assert.ThrowsAsync<IOException>(() =>
            service.ImportAsync(new ThrowingStream(throwOnRead: true)));
    }

    // ------------------------------------------------------------------
    // Corrupted artifact
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_CorruptedArtifact_ThrowsCorruptedExportArtifactException()
    {
        var service = new ImportService(new JsonExportFormat());
        var corrupted = new MemoryStream("not valid json at all {{{"u8.ToArray());

        await Assert.ThrowsAsync<CorruptedExportArtifactException>(() => service.ImportAsync(corrupted));
    }

    [Fact]
    public async Task ImportAsync_TruncatedArtifact_ThrowsCorruptedExportArtifactException()
    {
        var format = new JsonExportFormat();
        var service = new ImportService(format);
        var wellFormed = await BuildArtifactAsync(new ExportSection("kind.a", 1, "hello"u8.ToArray()));
        var truncated = wellFormed[..(wellFormed.Length / 2)];

        await Assert.ThrowsAsync<CorruptedExportArtifactException>(() => service.ImportAsync(new MemoryStream(truncated)));
    }

    // ------------------------------------------------------------------
    // Argument validation
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_NullSource_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new ImportService(new JsonExportFormat()).ImportAsync(null!));

    [Fact]
    public void Constructor_NullFormat_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new ImportService(null!));

    // ------------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_Succeeds_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var service = new ImportService(new JsonExportFormat(), logger);
        service.RegisterImportable(new RecordingImportable("kind.a", 1));
        var artifact = await BuildArtifactAsync(new ExportSection("kind.a", 1, []));

        await service.ImportAsync(new MemoryStream(artifact));

        Assert.True(logger.HasEntryAt(LogLevel.Information, "Imported"));
    }

    [Fact]
    public async Task ImportAsync_IncompatibleSchema_LogsAtWarningLevel()
    {
        var logger = new RecordingLevelLogger();
        var service = new ImportService(new JsonExportFormat(), logger);
        service.RegisterImportable(new RecordingImportable("kind.a", 2));
        var artifact = await BuildArtifactAsync(new ExportSection("kind.a", 1, []));

        await Assert.ThrowsAsync<IncompatibleExportSchemaException>(() => service.ImportAsync(new MemoryStream(artifact)));

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "rejected"));
    }

    // ------------------------------------------------------------------
    // Concurrency (registration is expected to complete before concurrent
    // ImportAsync calls, mirroring ReportingService's own convention)
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_ConcurrentCallsForDistinctArtifacts_BothCompleteCorrectly()
    {
        var format = new JsonExportFormat();
        var service = new ImportService(format);
        var importableA = new RecordingImportable("kind.a", 1);
        var importableB = new RecordingImportable("kind.b", 1);
        service.RegisterImportable(importableA);
        service.RegisterImportable(importableB);
        var artifactA = await BuildArtifactAsync(new ExportSection("kind.a", 1, "a"u8.ToArray()));
        var artifactB = await BuildArtifactAsync(new ExportSection("kind.b", 1, "b"u8.ToArray()));

        await Task.WhenAll(
            service.ImportAsync(new MemoryStream(artifactA)),
            service.ImportAsync(new MemoryStream(artifactB)));

        Assert.Single(importableA.ReceivedPayloads);
        Assert.Single(importableB.ReceivedPayloads);
    }

    // ------------------------------------------------------------------
    // Platform Service registration (dual-registered, mirroring ADR-0044's
    // CurrentPrincipalAccessor precedent)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_DualRegistration_ResolvesTheSameInstanceUnderBothKeys()
    {
        var importService = new ImportService(new JsonExportFormat());
        var services = new ServiceCollection();
        services.AddInstance<IImportService>(importService);
        services.AddInstance(importService);
        var provider = new TempestServiceProvider(services);

        var viaInterface = provider.GetService(typeof(IImportService));
        var viaConcreteType = provider.GetService(typeof(ImportService));

        Assert.Same(importService, viaInterface);
        Assert.Same(importService, viaConcreteType);
    }
}
