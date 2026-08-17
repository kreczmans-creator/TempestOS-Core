using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

// Test-only IModule fixtures used exclusively to exercise
// ReflectionFrameworkDiscoveryService. None of these represent real
// application modules.

internal sealed class SampleModuleA : IModule
{
    public string Id => "tempest.sample.alpha";
    public string Name => "Sample Module Alpha";
    public string Version => "1.0.0";
}

internal sealed class SampleModuleB : IModule
{
    public string Id => "tempest.sample.beta";
    public string Name => "Sample Module Beta";
    public string Version => "1.0.0";
}

internal sealed class SampleModuleC : IModule
{
    public string Id => "tempest.sample.gamma";
    public string Name => "Sample Module Gamma";
    public string Version => "1.0.0";
}

internal sealed class InvalidIdModule : IModule
{
    public string Id => "";
    public string Name => "Invalid Module";
    public string Version => "1.0.0";
}

internal abstract class AbstractModule : IModule
{
    public string Id => "abstract.module";
    public string Name => "Abstract Module";
    public string Version => "1.0.0";
}

internal sealed class GenericModule<T> : IModule
{
    public string Id => "generic.module";
    public string Name => "Generic Module";
    public string Version => "1.0.0";
}

internal sealed class NotAModule
{
}

// No [ModuleMetadata] attribute, and no public parameterless constructor -
// the exact shape ReflectionFrameworkDiscoveryServiceTests' own
// "unclear diagnostic message" fix (WP 5.3) targets.
internal sealed class ConstructorDependencyModuleWithoutMetadata : IModule
{
    public ConstructorDependencyModuleWithoutMetadata(string dependency)
    {
        _ = dependency;
    }

    public string Id => "tempest.sample.no-parameterless-ctor";
    public string Name => "No Parameterless Constructor";
    public string Version => "1.0.0";
}

// WP 13.9.6: a minimal IModule fixture whose constructor increments a
// static counter - an observable side effect requiring no dynamic IL
// emission at all, used to prove ReflectionFrameworkDiscoveryService's own
// new isTypeExcluded predicate parameter genuinely prevents
// Activator.CreateInstance from ever running, in complete isolation from
// the plugin-trust pipeline (mirrors ConstructorExecutionProbe's own role
// in Tempest.Core.Tests.Plugins, at the mechanism level rather than the
// end-to-end Host level).
internal sealed class ConstructorTrackingModule : IModule
{
    public static int ConstructionCount;

    public ConstructorTrackingModule()
    {
        ConstructionCount++;
    }

    public string Id => "tempest.sample.constructor-tracking";
    public string Name => "Constructor Tracking Module";
    public string Version => "1.0.0";
}

// WP 12.3B (ADR-0102): a minimal IFaultInjectionModule fixture, isolated
// from the real Tempest.Validation.FaultInjection.DuplicateNavigationModule
// (which requires INavigationProvider), used to prove
// ReflectionFrameworkDiscoveryService's own default-exclusion filter at the
// unit level in ReflectionFrameworkDiscoveryServiceTests.
internal sealed class SampleFaultInjectionModule : IModule, IFaultInjectionModule
{
    public string Id => "tempest.sample.fault-injection";
    public string Name => "Sample Fault Injection Module";
    public string Version => "1.0.0";
}
