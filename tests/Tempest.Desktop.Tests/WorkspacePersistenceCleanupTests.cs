namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-120` closure — proves the two claims
/// <see cref="WorkspacePersistenceCollection"/>'s own remarks make:
/// every isolated root shares one common parent, and the recursive-delete
/// mechanism <see cref="PersistenceRootCleanupFixture"/> relies on
/// actually deletes what it is given, and tolerates what it is not.
/// </summary>
/// <remarks>
/// Deliberately not in the "Tempest.Desktop WorkspaceHost persistence"
/// collection: these tests construct no <see cref="WorkspaceHost"/> and
/// touch no Avalonia dispatcher state, and exercising
/// <see cref="TestTempDirectoryCleanup.TryDeleteDirectoryRecursively"/>
/// against a throwaway directory of its own is exactly how this file
/// proves the mechanism without ever touching the real, live
/// <see cref="WorkspacePersistenceCollection.RunRootPath"/> that
/// collection's own concurrently-running tests may still be using — see
/// that class's own remarks for why deleting the real one here would
/// defeat the point.
/// </remarks>
public sealed class WorkspacePersistenceCleanupTests
{
    [Fact]
    public void NewIsolatedPersistenceRootPath_TwoCalls_ReturnDifferentPathsUnderTheSameParent()
    {
        var first = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var second = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Assert.NotEqual(first, second);
        Assert.Equal(WorkspacePersistenceCollection.RunRootPath, Path.GetDirectoryName(first));
        Assert.Equal(WorkspacePersistenceCollection.RunRootPath, Path.GetDirectoryName(second));
    }

    [Fact]
    public void TryDeleteDirectoryRecursively_RealDirectoryWithNestedContent_ActuallyRemovesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TempestOS.Desktop.Tests.CleanupProbe.{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(root, "root-file.txt"), "root");
        File.WriteAllText(Path.Combine(nested, "nested-file.txt"), "nested");
        Assert.True(Directory.Exists(root), "test setup itself failed to create the probe directory");

        TestTempDirectoryCleanup.TryDeleteDirectoryRecursively(root);

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void TryDeleteDirectoryRecursively_PathDoesNotExist_IsANoOpNotAThrow()
    {
        var neverCreated = Path.Combine(Path.GetTempPath(), $"TempestOS.Desktop.Tests.NeverExists.{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(neverCreated));

        var exception = Record.Exception(() => TestTempDirectoryCleanup.TryDeleteDirectoryRecursively(neverCreated));

        Assert.Null(exception);
    }

    [Fact]
    public void TryDeleteDirectoryRecursively_CalledTwiceOnTheSamePath_SecondCallIsAlsoANoOp()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TempestOS.Desktop.Tests.CleanupProbe.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        TestTempDirectoryCleanup.TryDeleteDirectoryRecursively(root);
        var exception = Record.Exception(() => TestTempDirectoryCleanup.TryDeleteDirectoryRecursively(root));

        Assert.Null(exception);
        Assert.False(Directory.Exists(root));
    }
}
