using Tempest.Core.ExportImport;

namespace Tempest.Core.Tests.ExportImport;

public class ExceptionTests
{
    [Fact]
    public void ExportImportException_MessageConstructor_SetsMessage()
    {
        var exception = new ExportImportException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void IncompatibleExportSchemaException_VersionMismatchConstructor_IsAnExportImportException()
    {
        var exception = new IncompatibleExportSchemaException("kind.a", 2, 1);

        Assert.IsAssignableFrom<ExportImportException>(exception);
        Assert.Equal("kind.a", exception.Kind);
        Assert.Equal(2, exception.ArtifactSchemaVersion);
        Assert.Equal(1, exception.SupportedSchemaVersion);
        Assert.Contains("kind.a", exception.Message);
    }

    [Fact]
    public void IncompatibleExportSchemaException_UnknownKindConstructor_LeavesVersionsNull()
    {
        var exception = new IncompatibleExportSchemaException("kind.unknown");

        Assert.IsAssignableFrom<ExportImportException>(exception);
        Assert.Equal("kind.unknown", exception.Kind);
        Assert.Null(exception.ArtifactSchemaVersion);
        Assert.Null(exception.SupportedSchemaVersion);
        Assert.Contains("kind.unknown", exception.Message);
    }

    [Fact]
    public void CorruptedExportArtifactException_IsAnExportImportException()
    {
        var exception = new CorruptedExportArtifactException("truncated JSON");

        Assert.IsAssignableFrom<ExportImportException>(exception);
        Assert.Equal("truncated JSON", exception.Reason);
        Assert.Contains("truncated JSON", exception.Message);
    }

    [Fact]
    public void DuplicateImportableKindException_IsAnExportImportException()
    {
        var exception = new DuplicateImportableKindException("kind.a");

        Assert.IsAssignableFrom<ExportImportException>(exception);
        Assert.Equal("kind.a", exception.Kind);
        Assert.Contains("kind.a", exception.Message);
    }
}
