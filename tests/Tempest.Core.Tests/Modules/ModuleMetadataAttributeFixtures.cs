using Tempest.Core.Logging;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

// Test-only fixtures exercising ModuleMetadataAttribute (ADR-0027). None
// represents a real application module.

/// <summary>
/// Carries no parameterless constructor at all - if discovery ever fell
/// back to <c>Activator.CreateInstance(type)</c> for this type (ignoring
/// the attribute), it would throw immediately. Successful discovery is
/// therefore direct proof the attribute path never instantiates it.
/// </summary>
[ModuleMetadata("test.attribute.declared", "Attribute Declared Module", "1.0.0")]
internal sealed class AttributeDeclaredModule : IModule
{
    public AttributeDeclaredModule(string requiredDependency)
    {
        RequiredDependency = requiredDependency;
    }

    public string RequiredDependency { get; }

    public string Id => "test.attribute.declared";
    public string Name => "Attribute Declared Module";
    public string Version => "1.0.0";
}

/// <summary>
/// Carries both the attribute and an ordinary, callable parameterless
/// constructor - proving the attribute path works even when the legacy
/// path would also have succeeded.
/// </summary>
[ModuleMetadata("test.attribute.zeroarg", "Attribute Zero Arg Module", "1.0.0")]
internal sealed class AttributeZeroArgModule : IModule
{
    public string Id => "test.attribute.zeroarg";
    public string Name => "Attribute Zero Arg Module";
    public string Version => "1.0.0";
}

/// <summary>
/// Carries the attribute alongside a working parameterless constructor
/// whose own instance properties deliberately return different values -
/// proving the attribute takes precedence over construction, rather than
/// merely being usable when construction happens to be unavailable.
/// </summary>
[ModuleMetadata("test.attribute.precedence", "Attribute Wins", "1.0.0")]
internal sealed class AttributePrecedenceModule : IModule
{
    public string Id => "test.legacy.would-be-used-if-attribute-were-ignored";
    public string Name => "Legacy Value - Should Never Be Observed";
    public string Version => "0.0.1";
}

[ModuleMetadata("", "Malformed Empty Id", "1.0.0")]
internal sealed class MalformedEmptyIdModule : IModule
{
    public string Id => "";
    public string Name => "Malformed Empty Id";
    public string Version => "1.0.0";
}

[ModuleMetadata("test.malformed.whitespace-name", "   ", "1.0.0")]
internal sealed class MalformedWhitespaceNameModule : IModule
{
    public string Id => "test.malformed.whitespace-name";
    public string Name => "   ";
    public string Version => "1.0.0";
}

[ModuleMetadata("test.malformed.empty-version", "Malformed Empty Version", "")]
internal sealed class MalformedEmptyVersionModule : IModule
{
    public string Id => "test.malformed.empty-version";
    public string Name => "Malformed Empty Version";
    public string Version => "";
}

[ModuleMetadata("test.attribute.duplicate", "Duplicate A", "1.0.0")]
internal sealed class DuplicateAttributeModuleA : IModule
{
    public string Id => "test.attribute.duplicate";
    public string Name => "Duplicate A";
    public string Version => "1.0.0";
}

[ModuleMetadata("test.attribute.duplicate", "Duplicate B", "2.0.0")]
internal sealed class DuplicateAttributeModuleB : IModule
{
    public string Id => "test.attribute.duplicate";
    public string Name => "Duplicate B";
    public string Version => "2.0.0";
}

/// <summary>
/// Declares the same Id as the existing legacy fixture <see cref="SampleModuleA"/>
/// (<c>ModuleFixtures.cs</c>) - proving duplicate detection works across
/// the two metadata-reading mechanisms, not only within one.
/// </summary>
[ModuleMetadata("tempest.sample.alpha", "Attribute Clone of Sample Alpha", "1.0.0")]
internal sealed class AttributeDuplicateOfSampleModuleA : IModule
{
    public string Id => "tempest.sample.alpha";
    public string Name => "Attribute Clone of Sample Alpha";
    public string Version => "1.0.0";
}

internal interface ITestGreeter
{
    string Greet();
}

internal sealed class TestGreeter : ITestGreeter
{
    public string Greet() => "hello";
}

/// <summary>
/// A genuinely constructor-injected module: its sole constructor requires
/// a registered, DI-resolved service - exactly what ADR-0027 exists to
/// make possible for a discovered module. Proves the full, real pipeline
/// (Discovery -&gt; Registration -&gt; DI -&gt; Lifecycle), not merely that
/// discovery itself succeeds.
/// </summary>
[ModuleMetadata("test.attribute.injected", "Constructor Injected Module", "1.0.0")]
internal sealed class ConstructorInjectedModule : IModule, IModuleLifecycle
{
    private readonly ITestGreeter _greeter;

    public ConstructorInjectedModule(ITestGreeter greeter)
    {
        _greeter = greeter;
    }

    public string Id => "test.attribute.injected";
    public string Name => "Constructor Injected Module";
    public string Version => "1.0.0";

    public string? GreetingObservedDuringInitialise { get; private set; }

    public Task InitialiseAsync(CancellationToken cancellationToken)
    {
        GreetingObservedDuringInitialise = _greeter.Greet();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Constructor-injects <see cref="ILogger"/> - a real platform service
/// <see cref="Tempest.Core.Runtime.TempestHost"/> already registers for
/// every run, with no test-only wiring required. Proves constructor
/// injection into a discovered module works through the real,
/// unmodified Host pipeline end-to-end.
/// </summary>
[ModuleMetadata("test.attribute.hostinjected", "Host Injected Module", "1.0.0")]
internal sealed class HostInjectedModule : IModule, IModuleLifecycle
{
    private readonly ILogger _logger;

    public HostInjectedModule(ILogger logger)
    {
        _logger = logger;
    }

    public string Id => "test.attribute.hostinjected";
    public string Name => "Host Injected Module";
    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _logger.Information("HostInjectedModule initialised with a constructor-injected ILogger.");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
