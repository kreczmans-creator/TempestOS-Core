using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Modules;

// WP 13.11B (TD-51, reopened by WP 13.11A). ReflectionFrameworkDiscoveryService
// is, by design, wholly plugin-unaware (ADR-0110) - it is handed candidate
// types and asked for descriptors, with no knowledge of where they came from
// or whether anything upstream vetted them. That makes its own
// CreateDescriptor the last line of defence against a Type whose mere
// constructor SIGNATURE cannot be resolved: type.GetConstructor(Type.EmptyTypes)
// is itself a genuine CLR type-load, resolving every public constructor's own
// full parameter signature to arity-match, so for a type declaring a
// constructor parameter whose own assembly is unreachable it throws
// TypeLoadException/FileNotFoundException - not the MissingMethodException the
// WP 5.3 guard at that line was written to pre-empt, and not any exception the
// discovery loop had ever caught. WP 13.11A confirmed this live: it propagated
// uncaught through TempestHost.RunAsync to a whole-Host crash.
//
// PluginAssemblyLoader's own WP 13.11B RecordDenied fix closes the root cause
// and is what actually keeps a denied plugin's types excluded; these tests
// cover the fail-closed backstop in this deliberately plugin-unaware class,
// whose isTypeExcluded predicate is optional and whose candidates need not
// have come from a plugin at all. Equally important, they pin the guard's
// narrowness: the ordinary, long-documented "no parameterless constructor and
// no [ModuleMetadataAttribute]" case must still throw its own actionable
// ModuleDiscoveryException, never be silently swallowed by the same catch.
//
// [Collection("Console output capture")] - see PluginAssemblyLoaderTests.cs's
// own comment on this same attribute: the real Assembly.LoadFrom calls the
// helper below makes must not race against any other test class that also
// loads a real assembly. ReflectionFrameworkDiscoveryServiceTests itself is
// deliberately left out of that collection - it is pure in-memory and
// parallel-safe, and tagging it would needlessly serialise a dozen unrelated
// tests.
[Collection("Console output capture")]
public class ModuleDiscoveryUnresolvableConstructorTests
{
    [Fact]
    public void DiscoverModules_CandidateWithUnresolvableConstructorParameter_SkipsItAndStillDiscoversOtherCandidates()
    {
        var logger = new RecordingLogger();
        var moduleType = BuildModuleTypeWithUnresolvableConstructorParameter("Td51DiscoveryGuard");

        // Deliberately NO isTypeExcluded predicate: this exercises the
        // discovery boundary's own guard in isolation, entirely
        // independently of whether PluginAssemblyLoader recorded anything.
        var service = new ReflectionFrameworkDiscoveryService(logger);

        // The unresolvable candidate is deliberately FIRST: before this fix
        // it threw immediately, so SampleModuleA behind it was never reached
        // at all - proving the guard skips the candidate rather than
        // aborting the whole scan.
        var exception = Record.Exception(() => service.DiscoverModules([moduleType, typeof(SampleModuleA)]));

        Assert.Null(exception);

        var result = service.DiscoverModules([moduleType, typeof(SampleModuleA)]);

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.sample.alpha", descriptor.Id);
        Assert.DoesNotContain(result, d => d.ModuleType == moduleType);

        // Exclusion must be visible, not silent.
        Assert.Contains(logger.Messages, message => message.Contains(moduleType.FullName!, StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoverModules_TypeWithNoParameterlessConstructorAndNoMetadataAttribute_StillThrowsActionableModuleDiscoveryException()
    {
        // The narrowness proof, co-located with the guard that could regress
        // it. ConstructorDependencyModuleWithoutMetadata's own constructor
        // parameter type is perfectly resolvable - this is the ordinary
        // WP 5.3 module-author mistake, and it must still surface as a
        // ModuleDiscoveryException naming the actual fix, never be absorbed
        // by WP 13.11B's own fail-closed catch. (ReflectionFrameworkDiscoveryServiceTests
        // guards this same behaviour on its own terms; this test guards it
        // specifically against this fix.)
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules([typeof(ConstructorDependencyModuleWithoutMetadata)]));

        Assert.Contains("ConstructorDependencyModuleWithoutMetadata", exception.Message);
        Assert.Contains("ModuleMetadataAttribute", exception.Message);
        Assert.Contains("parameterless constructor", exception.Message);
    }

    /// <summary>
    /// Builds, on disk and at test time, a real public concrete
    /// <see cref="IModule"/> implementer whose sole public constructor takes
    /// one parameter of a type declared in a second assembly saved to a
    /// directory the default <c>AssemblyLoadContext</c> will never probe —
    /// reusing <c>PluginAssemblyLoaderMultiAssemblyTrustTests</c>'s own
    /// established, shipped unreachable-dependency mechanism verbatim
    /// (WP 13.10B), so <c>type.GetConstructor(Type.EmptyTypes)</c> genuinely
    /// throws <see cref="TypeLoadException"/>/<see cref="FileNotFoundException"/>
    /// rather than merely returning <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="Type"/>'s own assembly is loaded into the
    /// default, non-collectible <c>AssemblyLoadContext</c> and stays resident
    /// for the test process's lifetime (ADR-0015: a load cannot be undone) —
    /// identical, deliberate behaviour to every other real-assembly test in
    /// this suite. The <see cref="TempDirectory"/> is deliberately not
    /// disposed: its own recursive delete would fail against the locked,
    /// loaded DLL and be swallowed anyway.
    /// </remarks>
    private static Type BuildModuleTypeWithUnresolvableConstructorParameter(string namePrefix)
    {
        var temp = new TempDirectory();

        var externalOnlyDirectory = Path.Combine(temp.Path, "unreachable");
        Directory.CreateDirectory(externalOnlyDirectory);

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            externalOnlyDirectory,
            $"{namePrefix}Secondary",
            "SharedParameterType",
            "InertModule",
            "test.td51-discovery-secondary-module",
            "Inert Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);

        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
            temp.Path,
            $"{namePrefix}Primary.dll",
            secondaryAssemblyPath,
            $"{secondaryAssemblyName}.SharedParameterType",
            moduleId: "test.td51-discovery-module",
            moduleName: "TD-51 Discovery Module",
            moduleVersion: "1.0.0",
            addAlternateCompliantConstructor: false);

        // Assembly.GetTypes resolves base types and implemented interfaces
        // only - never constructor signatures - so this succeeds even though
        // GetConstructor on the returned type does not.
        return System.Reflection.Assembly.LoadFrom(primaryAssemblyPath).GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
    }
}
