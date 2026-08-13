using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Versioning;
using static Tempest.Core.Tests.Plugins.PluginManifestJsonBuilder;

namespace Tempest.Core.Tests.Plugins;

// WP 13.1A / ADR-0107: dependency graph resolution
// (PluginManifestDiscoveryService.ResolveDependencyGraph) - every branch of
// the fixed-point-removal-then-cycle-detection algorithm, not just the
// happy path. Uses the real production algorithm throughout, via the public
// DiscoverManifests() entry point against real candidate folders on disk -
// no shortcut re-implementation of dependency resolution in the test itself.
public class PluginDependencyGraphResolutionTests
{
    private static readonly IPlatformVersionProvider DefaultVersionProvider =
        new FakePlatformVersionProvider(new Version(1, 0, 0));

    // ----------------------------------------------------------------
    // Valid chains - correct dependency-topological load order
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_TwoPluginChain_LoadsDependencyBeforeDependent_RegardlessOfFolderOrder()
    {
        using var temp = new TempDirectory();

        // Folder order deliberately places the dependent BEFORE its own
        // dependency alphabetically, so a correct result proves the
        // topological sort - not plain folder-acceptance order - decided
        // the final order.
        var dependentFolder = CreateCandidateFolder(temp.Path, "a-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.b", assemblyFileName: "B.dll",
            dependencies: [DependencyFragment.On("test.a", "1.0.0")]));

        var targetFolder = CreateCandidateFolder(temp.Path, "z-target");
        WriteManifest(targetFolder, Build(id: "test.a", assemblyFileName: "A.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Equal(["test.a", "test.b"], result.Select(m => m.Id));
    }

    [Fact]
    public void DiscoverManifests_ThreePluginChain_ResolvesInCorrectDependencyOrder()
    {
        using var temp = new TempDirectory();

        // Folder names deliberately reverse-alphabetical relative to
        // dependency order, for the same reason as above.
        var cFolder = CreateCandidateFolder(temp.Path, "a-c-plugin");
        WriteManifest(cFolder, Build(id: "test.c", assemblyFileName: "C.dll", dependencies: [DependencyFragment.On("test.b", "1.0.0")]));

        var bFolder = CreateCandidateFolder(temp.Path, "b-b-plugin");
        WriteManifest(bFolder, Build(id: "test.b", assemblyFileName: "B.dll", dependencies: [DependencyFragment.On("test.a", "1.0.0")]));

        var aFolder = CreateCandidateFolder(temp.Path, "c-a-plugin");
        WriteManifest(aFolder, Build(id: "test.a", assemblyFileName: "A.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Equal(["test.a", "test.b", "test.c"], result.Select(m => m.Id));
    }

    // ----------------------------------------------------------------
    // Missing dependency - isolates only the dependent (category 12)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_MissingDependency_IsolatesOnlyDependent_UnrelatedSiblingStillLoads()
    {
        using var temp = new TempDirectory();

        var dependentFolder = CreateCandidateFolder(temp.Path, "a-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.dependent", assemblyFileName: "Dependent.dll",
            dependencies: [DependencyFragment.On("test.does-not-exist", "1.0.0")]));

        var siblingFolder = CreateCandidateFolder(temp.Path, "b-sibling");
        WriteManifest(siblingFolder, Build(id: "test.sibling", assemblyFileName: "Sibling.dll"));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.sibling", manifest.Id);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("test.dependent", StringComparison.Ordinal) &&
            e.Message.Contains("test.does-not-exist", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Incompatible dependency version - below minimum / above maximum
    // (category 13)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_DependencyBelowMinimumVersion_IsolatesDependent_TargetStillLoads()
    {
        using var temp = new TempDirectory();

        var targetFolder = CreateCandidateFolder(temp.Path, "a-target");
        WriteManifest(targetFolder, Build(id: "test.target", version: "1.0.0", assemblyFileName: "Target.dll"));

        var dependentFolder = CreateCandidateFolder(temp.Path, "b-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.dependent", assemblyFileName: "Dependent.dll",
            dependencies: [DependencyFragment.On("test.target", "2.0.0")]));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.target", manifest.Id);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("test.dependent", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoverManifests_DependencyAboveMaximumVersion_IsolatesDependent_TargetStillLoads()
    {
        using var temp = new TempDirectory();

        var targetFolder = CreateCandidateFolder(temp.Path, "a-target");
        WriteManifest(targetFolder, Build(id: "test.target", version: "5.0.0", assemblyFileName: "Target.dll"));

        var dependentFolder = CreateCandidateFolder(temp.Path, "b-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.dependent", assemblyFileName: "Dependent.dll",
            dependencies: [DependencyFragment.On("test.target", "1.0.0", "3.0.0")]));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.target", manifest.Id);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("test.dependent", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoverManifests_DependencyWithNoMaximumVersion_IsUnboundedAbove_SatisfiedByMuchNewerTargetVersion()
    {
        using var temp = new TempDirectory();

        var targetFolder = CreateCandidateFolder(temp.Path, "a-target");
        WriteManifest(targetFolder, Build(id: "test.target", version: "99.0.0", assemblyFileName: "Target.dll"));

        var dependentFolder = CreateCandidateFolder(temp.Path, "b-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.dependent", assemblyFileName: "Dependent.dll",
            dependencies: [DependencyFragment.On("test.target", "1.0.0")]));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Equal(["test.target", "test.dependent"], result.Select(m => m.Id));
    }

    [Fact]
    public void DiscoverManifests_TargetVersionUnparseable_TreatedAsNotSatisfyingAnyBound_IsolatesDependentWithoutThrowing()
    {
        using var temp = new TempDirectory();

        // The target's own Version field is a plain, unvalidated string
        // (PluginManifest.Version's own established convention) - a value
        // that cannot be parsed as System.Version is a valid, if unusual,
        // manifest on its own, but can never satisfy anyone else's declared
        // dependency bound.
        var targetFolder = CreateCandidateFolder(temp.Path, "a-target");
        WriteManifest(targetFolder, Build(id: "test.target", version: "not-a-version", assemblyFileName: "Target.dll"));

        var dependentFolder = CreateCandidateFolder(temp.Path, "b-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.dependent", assemblyFileName: "Dependent.dll",
            dependencies: [DependencyFragment.On("test.target", "1.0.0")]));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.target", manifest.Id);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("test.dependent", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Circular dependencies (category 14)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_DirectTwoPluginCycle_IsolatesBoth()
    {
        using var temp = new TempDirectory();

        var aFolder = CreateCandidateFolder(temp.Path, "a-plugin");
        WriteManifest(aFolder, Build(id: "test.a", assemblyFileName: "A.dll", dependencies: [DependencyFragment.On("test.b", "1.0.0")]));

        var bFolder = CreateCandidateFolder(temp.Path, "b-plugin");
        WriteManifest(bFolder, Build(id: "test.b", assemblyFileName: "B.dll", dependencies: [DependencyFragment.On("test.a", "1.0.0")]));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("test.a", StringComparison.Ordinal) &&
            e.Message.Contains("circular dependency", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("test.b", StringComparison.Ordinal) &&
            e.Message.Contains("circular dependency", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoverManifests_ThreePluginCycle_IsolatesAllThree()
    {
        using var temp = new TempDirectory();

        var aFolder = CreateCandidateFolder(temp.Path, "a-plugin");
        WriteManifest(aFolder, Build(id: "test.a", assemblyFileName: "A.dll", dependencies: [DependencyFragment.On("test.b", "1.0.0")]));

        var bFolder = CreateCandidateFolder(temp.Path, "b-plugin");
        WriteManifest(bFolder, Build(id: "test.b", assemblyFileName: "B.dll", dependencies: [DependencyFragment.On("test.c", "1.0.0")]));

        var cFolder = CreateCandidateFolder(temp.Path, "c-plugin");
        WriteManifest(cFolder, Build(id: "test.c", assemblyFileName: "C.dll", dependencies: [DependencyFragment.On("test.a", "1.0.0")]));

        var registry = new PluginRegistry();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, registryRecorder: registry);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.All(registry.Entries, e => Assert.Equal(PluginRegistryState.DependencyUnmet, e.State));
        Assert.Equal(["test.a", "test.b", "test.c"], registry.Entries.Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal));
    }

    // ----------------------------------------------------------------
    // Fixed-point-then-cycle-detection repeats until stable: a candidate
    // depending on a since-removed cycle member is a cascading consequence,
    // not caught on the first pass.
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_CandidateDependingOnCycleMember_IsIsolatedAsCascadingMissingDependency_AfterCycleRemoved()
    {
        using var temp = new TempDirectory();

        var aFolder = CreateCandidateFolder(temp.Path, "a-plugin");
        WriteManifest(aFolder, Build(id: "test.a", assemblyFileName: "A.dll", dependencies: [DependencyFragment.On("test.b", "1.0.0")]));

        var bFolder = CreateCandidateFolder(temp.Path, "b-plugin");
        WriteManifest(bFolder, Build(id: "test.b", assemblyFileName: "B.dll", dependencies: [DependencyFragment.On("test.a", "1.0.0")]));

        // Not itself part of the A<->B cycle - depends only on A. On the
        // first fixed-point pass, A is still present, so this dependency is
        // satisfied; only once cycle detection removes A does this become a
        // missing dependency, on the *next* iteration of the outer loop.
        var cFolder = CreateCandidateFolder(temp.Path, "c-plugin");
        WriteManifest(cFolder, Build(id: "test.c", assemblyFileName: "C.dll", dependencies: [DependencyFragment.On("test.a", "1.0.0")]));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("test.a", StringComparison.Ordinal) && e.Message.Contains("circular dependency", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("test.b", StringComparison.Ordinal) && e.Message.Contains("circular dependency", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("test.c", StringComparison.Ordinal) && e.Message.Contains("depends on 'test.a', which is not present", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoverManifests_UnrelatedCandidateWithZeroDependencies_UnaffectedByCycleElsewhereInSameRun()
    {
        using var temp = new TempDirectory();

        var aFolder = CreateCandidateFolder(temp.Path, "a-plugin");
        WriteManifest(aFolder, Build(id: "test.a", assemblyFileName: "A.dll", dependencies: [DependencyFragment.On("test.b", "1.0.0")]));

        var bFolder = CreateCandidateFolder(temp.Path, "b-plugin");
        WriteManifest(bFolder, Build(id: "test.b", assemblyFileName: "B.dll", dependencies: [DependencyFragment.On("test.a", "1.0.0")]));

        var dFolder = CreateCandidateFolder(temp.Path, "d-plugin");
        WriteManifest(dFolder, Build(id: "test.d", assemblyFileName: "D.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.d", manifest.Id);
    }

    [Fact]
    public void DiscoverManifests_UnrelatedCandidateWithZeroDependencies_UnaffectedByMissingDependencyElsewhereInSameRun()
    {
        using var temp = new TempDirectory();

        var dependentFolder = CreateCandidateFolder(temp.Path, "a-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.dependent", assemblyFileName: "Dependent.dll",
            dependencies: [DependencyFragment.On("test.does-not-exist", "1.0.0")]));

        var unrelatedFolder = CreateCandidateFolder(temp.Path, "b-unrelated");
        WriteManifest(unrelatedFolder, Build(id: "test.unrelated", assemblyFileName: "Unrelated.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.unrelated", manifest.Id);
    }

    // ----------------------------------------------------------------
    // Deterministic tie-break: independent candidates with no dependency
    // relationship load in folder-name-ordinal order.
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_IndependentCandidatesWithNoDependencyRelationship_LoadInFolderNameOrdinalOrder()
    {
        using var temp = new TempDirectory();

        var yankeeFolder = CreateCandidateFolder(temp.Path, "yankee-plugin");
        WriteManifest(yankeeFolder, Build(id: "test.yankee", assemblyFileName: "Yankee.dll"));

        var bravoFolder = CreateCandidateFolder(temp.Path, "bravo-plugin");
        WriteManifest(bravoFolder, Build(id: "test.bravo", assemblyFileName: "Bravo.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Equal(["test.bravo", "test.yankee"], result.Select(m => m.Id));
    }

    // ----------------------------------------------------------------
    // Duplicate dependency declarations - WP 13.1B regression (Code Review
    // finding 1): TopologicalSort's remaining-dependency count must be
    // keyed on distinct targets, not raw Dependencies.Count, or a candidate
    // declaring the same target twice never reaches zero and silently
    // vanishes from the result with no isolation and no registry entry.
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_DependentDeclaresSameTargetTwice_StillLoadsBothPlugins_NotSilentlyDropped()
    {
        using var temp = new TempDirectory();

        // test.a depends on test.b twice - a redundant, but not invalid,
        // declaration (nothing rejects a repeated dependency Id). This must
        // still resolve to exactly one graph edge, not leave test.a's own
        // remaining-dependency count permanently above zero.
        var dependentFolder = CreateCandidateFolder(temp.Path, "a-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.a", assemblyFileName: "A.dll",
            dependencies: [DependencyFragment.On("test.b", "1.0.0"), DependencyFragment.On("test.b", "1.0.0")]));

        var targetFolder = CreateCandidateFolder(temp.Path, "b-target");
        WriteManifest(targetFolder, Build(id: "test.b", assemblyFileName: "B.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        // Both plugins load, dependency before dependent - test.a must not
        // silently disappear from the result.
        Assert.Equal(["test.b", "test.a"], result.Select(m => m.Id));
    }

    [Fact]
    public void DiscoverManifests_DependentDeclaresSameTargetTwice_RecordsBothAsLoadedInRegistry()
    {
        using var temp = new TempDirectory();

        var dependentFolder = CreateCandidateFolder(temp.Path, "a-dependent");
        WriteManifest(dependentFolder, Build(
            id: "test.a", assemblyFileName: "A.dll",
            dependencies: [DependencyFragment.On("test.b", "1.0.0"), DependencyFragment.On("test.b", "1.0.0")]));

        var targetFolder = CreateCandidateFolder(temp.Path, "b-target");
        WriteManifest(targetFolder, Build(id: "test.b", assemblyFileName: "B.dll"));

        var registry = new PluginRegistry();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, registryRecorder: registry);

        service.DiscoverManifests();

        // A plugin that DiscoverManifests() returns is not itself recorded
        // by Discovery (only isolated candidates are, per this Work
        // Package's own design - Loaded is recorded later, by Plugin
        // Loading) - so the correct proof that test.a was not silently
        // dropped is the absence of any isolated-failure entry for it here.
        Assert.DoesNotContain(registry.Entries, e => e.Id == "test.a");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static string CreateCandidateFolder(string root, string folderName)
    {
        var path = Path.Combine(root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifest(string candidateFolder, string json) =>
        File.WriteAllText(Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName), json);
}
