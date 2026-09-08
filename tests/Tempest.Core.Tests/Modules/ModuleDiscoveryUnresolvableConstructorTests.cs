using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

// WP 13.11B (TD-51, reopened by WP 13.11A). ReflectionFrameworkDiscoveryService
// is, by design, wholly plugin-unaware (ADR-0110) - it is handed candidate
// types and asked for descriptors, with no knowledge of where they came from
// or whether anything upstream vetted them. That makes its own
// CreateDescriptor the last line of defence against a Type whose mere
// constructor SIGNATURE cannot be resolved.
//
// The unresolvable-constructor-parameter proof this class used to also carry
// depended on DynamicPluginAssemblyBuilder to construct such a type on disk;
// that machinery was frozen by ADR-0146 (WP 17.2A) along with the rest of
// the plugin trust platform, and the test moved with it to
// tests/Frozen/Tempest.Core.Tests/Modules/ModuleDiscoveryUnresolvableConstructorTests.cs.
// This narrowness proof needs none of that machinery and stays live: the
// ordinary, long-documented "no parameterless constructor and no
// [ModuleMetadataAttribute]" case must still throw its own actionable
// ModuleDiscoveryException, never be silently swallowed by that same catch.
// (ReflectionFrameworkDiscoveryServiceTests guards this same behaviour on
// its own terms; this test guards it specifically against that fix.)
public class ModuleDiscoveryUnresolvableConstructorTests
{
    [Fact]
    public void DiscoverModules_TypeWithNoParameterlessConstructorAndNoMetadataAttribute_StillThrowsActionableModuleDiscoveryException()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules([typeof(ConstructorDependencyModuleWithoutMetadata)]));

        Assert.Contains("ConstructorDependencyModuleWithoutMetadata", exception.Message);
        Assert.Contains("ModuleMetadataAttribute", exception.Message);
        Assert.Contains("parameterless constructor", exception.Message);
    }
}
