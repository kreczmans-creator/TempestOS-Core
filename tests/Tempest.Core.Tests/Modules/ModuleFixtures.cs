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
