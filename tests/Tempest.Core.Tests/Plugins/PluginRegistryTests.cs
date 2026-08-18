using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

// WP 13.1A / ADR-0108: PluginRegistry's own Record/Entries contract, and the
// exception-to-PluginRegistryState mapping PluginFailureLogging.
// RecordIsolatedFailure implements - a focused, table-driven proof of this
// Work Package's own stated contract: IncompatiblePluginVersionException ->
// Incompatible; the three new dependency exceptions -> DependencyUnmet;
// everything else -> Failed.
public class PluginRegistryTests
{
    // ----------------------------------------------------------------
    // Record / Entries
    // ----------------------------------------------------------------

    [Fact]
    public void Record_ThenEntries_ReflectsTheRecordedEntry()
    {
        var registry = new PluginRegistry();
        var entry = new PluginRegistryEntry("test.a", "A", "1.0.0", PluginRegistryState.Loaded, null);

        registry.Record(entry);

        var recorded = Assert.Single(registry.Entries);
        Assert.Same(entry, recorded);
    }

    [Fact]
    public void Record_MultipleEntries_AccumulateInRecordedOrder()
    {
        var registry = new PluginRegistry();
        var first = new PluginRegistryEntry("test.a", "A", "1.0.0", PluginRegistryState.Loaded, null);
        var second = new PluginRegistryEntry("test.b", "B", "1.0.0", PluginRegistryState.Failed, "boom");
        var third = new PluginRegistryEntry("test.c", "C", "1.0.0", PluginRegistryState.Disabled, "disabled");

        registry.Record(first);
        registry.Record(second);
        registry.Record(third);

        Assert.Equal([first, second, third], registry.Entries);
    }

    [Fact]
    public void Record_NullEntry_ThrowsArgumentNullException()
    {
        var registry = new PluginRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Record(null!));
    }

    [Fact]
    public void Entries_NoneRecorded_IsEmpty()
    {
        var registry = new PluginRegistry();

        Assert.Empty(registry.Entries);
    }

    // ----------------------------------------------------------------
    // Exception -> PluginRegistryState mapping (PluginFailureLogging,
    // exercised directly via its internal test seam - InternalsVisibleTo).
    // ----------------------------------------------------------------

    [Fact]
    public void RecordIsolatedFailure_IncompatiblePluginVersionException_MapsToIncompatible()
    {
        var registry = new PluginRegistry();
        var exception = new IncompatiblePluginVersionException("test.a", new Version(2, 0, 0), new Version(1, 0, 0));

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.a", entry.Id);
        Assert.Equal(PluginRegistryState.Incompatible, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_MissingPluginDependencyException_MapsToDependencyUnmet()
    {
        var registry = new PluginRegistry();
        var exception = new MissingPluginDependencyException("test.a", "test.missing");

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.a", entry.Id);
        Assert.Equal(PluginRegistryState.DependencyUnmet, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_IncompatiblePluginDependencyVersionException_MapsToDependencyUnmet()
    {
        var registry = new PluginRegistry();
        var exception = new IncompatiblePluginDependencyVersionException(
            "test.a", "test.dep", new Version(1, 0, 0), null, "0.5.0");

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.a", entry.Id);
        Assert.Equal(PluginRegistryState.DependencyUnmet, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_CircularPluginDependencyException_MapsToDependencyUnmet()
    {
        var registry = new PluginRegistry();
        var exception = new CircularPluginDependencyException("test.a", ["test.a", "test.b", "test.a"]);

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.a", entry.Id);
        Assert.Equal(PluginRegistryState.DependencyUnmet, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_InvalidPluginManifestException_MapsToFailed()
    {
        var registry = new PluginRegistry();
        var exception = new InvalidPluginManifestException("malformed.");

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("folder-a", entry.Id);
        Assert.Equal(PluginRegistryState.Failed, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_DuplicatePluginIdException_MapsToFailed()
    {
        var registry = new PluginRegistry();
        var exception = new DuplicatePluginIdException("test.a");

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.a", entry.Id);
        Assert.Equal(PluginRegistryState.Failed, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_PluginAssemblyNotFoundException_MapsToFailed()
    {
        var registry = new PluginRegistry();
        var exception = new PluginAssemblyNotFoundException("test.a", "C:\\nowhere.dll");

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.a", entry.Id);
        Assert.Equal(PluginRegistryState.Failed, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_PluginAssemblyLoadException_MapsToFailed()
    {
        var registry = new PluginRegistry();
        var exception = new PluginAssemblyLoadException("test.a", "C:\\corrupt.dll", new InvalidOperationException("boom"));

        PluginFailureLogging.RecordIsolatedFailure(registry, exception, "folder-a");

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.a", entry.Id);
        Assert.Equal(PluginRegistryState.Failed, entry.State);
    }

    [Fact]
    public void RecordIsolatedFailure_NullRecorder_DoesNotThrow()
    {
        var exception = new InvalidPluginManifestException("malformed.");

        PluginFailureLogging.RecordIsolatedFailure(null, exception, "folder-a");
    }
}
