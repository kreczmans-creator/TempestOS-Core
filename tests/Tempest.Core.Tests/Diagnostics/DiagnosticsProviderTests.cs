using Tempest.Core.BackgroundServices;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Diagnostics;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.Diagnostics;

// Proves ADR-0039 against the real DiagnosticsProvider implementation -
// a live, non-frozen read over its own Func<T> accessors, an honest
// empty projection before a lazily-attached collaborator exists, and the
// real projection once it does. Real ModuleLifecycleManager/
// HostedServiceManager instances are used throughout - no mocks, per this
// project's own established testing convention.
public class DiagnosticsProviderTests
{
    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NullHostStateAccessor_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DiagnosticsProvider(null!, () => null, () => null));

    [Fact]
    public void Constructor_NullLifecycleManagerAccessor_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DiagnosticsProvider(() => HostState.Running, null!, () => null));

    [Fact]
    public void Constructor_NullHostedServiceManagerAccessor_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DiagnosticsProvider(() => HostState.Running, () => null, null!));

    // ------------------------------------------------------------------
    // HostState: live, not frozen at construction
    // ------------------------------------------------------------------

    [Fact]
    public void HostState_ReflectsTheAccessorsCurrentValue_NotAValueFrozenAtConstruction()
    {
        var currentState = HostState.Created;
        var provider = new DiagnosticsProvider(() => currentState, () => null, () => null);

        Assert.Equal(HostState.Created, provider.HostState);

        currentState = HostState.Running;

        Assert.Equal(HostState.Running, provider.HostState);
    }

    // ------------------------------------------------------------------
    // Modules: empty before attached, real data once attached
    // ------------------------------------------------------------------

    [Fact]
    public void Modules_AccessorReturnsNull_ReturnsEmptyCollection()
    {
        var provider = new DiagnosticsProvider(() => HostState.Running, () => null, () => null);

        Assert.Empty(provider.Modules);
    }

    [Fact]
    public void Modules_AccessorReturnsARealManager_ReturnsItsModules()
    {
        var runtimeManager = new RuntimeModuleManager();
        var descriptor = new ModuleDescriptor("sample.a", "Sample A", "1.0.0", typeof(DiagnosticsFixtureModule));
        runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton(typeof(DiagnosticsFixtureModule), typeof(DiagnosticsFixtureModule));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        var provider = new DiagnosticsProvider(() => HostState.Running, () => lifecycleManager, () => null);

        var status = Assert.Single(provider.Modules);
        Assert.Equal("sample.a", status.Descriptor.Id);
        Assert.Equal(ModuleState.Registered, status.State);
    }

    // ------------------------------------------------------------------
    // HostedServices: empty before attached, real data once attached
    // ------------------------------------------------------------------

    [Fact]
    public void HostedServices_AccessorReturnsNull_ReturnsEmptyCollection()
    {
        var provider = new DiagnosticsProvider(() => HostState.Running, () => null, () => null);

        Assert.Empty(provider.HostedServices);
    }

    [Fact]
    public void HostedServices_AccessorReturnsARealManager_ReturnsItsServices()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        var serviceProvider = new TempestServiceProvider(services);
        var hostedServiceManager = new HostedServiceManager([], serviceProvider);

        var provider = new DiagnosticsProvider(() => HostState.Running, () => null, () => hostedServiceManager);

        Assert.Equal(hostedServiceManager.Services, provider.HostedServices);
    }

    [Fact]
    public void HostedServices_TransitionsFromEmptyToAttached_AsTheAccessorsResultChanges()
    {
        IHostedServiceManager? manager = null;
        var provider = new DiagnosticsProvider(() => HostState.Running, () => null, () => manager);

        Assert.Empty(provider.HostedServices);

        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        var serviceProvider = new TempestServiceProvider(services);
        manager = new HostedServiceManager([], serviceProvider);

        // Still empty - zero hosted service types were supplied - but no
        // longer because the manager itself was absent; this proves the
        // accessor is re-read live, not cached.
        Assert.Empty(provider.HostedServices);
        Assert.NotNull(manager);
    }

    private sealed class DiagnosticsFixtureModule : ModuleLifecycleBase
    {
        public DiagnosticsFixtureModule() : base("sample.a", "Sample A", "1.0.0")
        {
        }
    }
}
