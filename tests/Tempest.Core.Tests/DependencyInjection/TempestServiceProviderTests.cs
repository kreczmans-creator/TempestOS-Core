using Tempest.Core.DependencyInjection;
using Tempest.Core.Modules;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Modules;

namespace Tempest.Core.Tests.DependencyInjection;

public class TempestServiceProviderTests
{
    [Fact]
    public void GetService_Singleton_ReturnsSameInstanceOnEveryResolution()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();

        var provider = new TempestServiceProvider(services);

        var first = (IGreeter)provider.GetService(typeof(IGreeter));
        var second = (IGreeter)provider.GetService(typeof(IGreeter));

        Assert.Same(first, second);
    }

    [Fact]
    public void GetService_InstanceRegistration_ReturnsTheExactSameInstance()
    {
        var services = new ServiceCollection();
        var greeter = new Greeter();
        services.AddInstance<IGreeter>(greeter);

        var provider = new TempestServiceProvider(services);

        var resolved = (IGreeter)provider.GetService(typeof(IGreeter));

        Assert.Same(greeter, resolved);
    }

    [Fact]
    public void GetService_ConsumerDependingOnInstanceRegistration_ReceivesTheSameInstance()
    {
        var services = new ServiceCollection();
        var greeter = new Greeter();
        services.AddInstance<IGreeter>(greeter);
        services.Transient<GreeterConsumer>();

        var provider = new TempestServiceProvider(services);

        var consumer = (GreeterConsumer)provider.GetService(typeof(GreeterConsumer));

        Assert.Same(greeter, consumer.Greeter);
    }

    [Fact]
    public void GetService_Transient_ReturnsDifferentInstanceEveryResolution()
    {
        var services = new ServiceCollection();
        services.Transient<IGreeter, Greeter>();

        var provider = new TempestServiceProvider(services);

        var first = (IGreeter)provider.GetService(typeof(IGreeter));
        var second = (IGreeter)provider.GetService(typeof(IGreeter));

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetService_ResolvesConstructorDependency()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();
        services.Transient<GreeterConsumer>();

        var provider = new TempestServiceProvider(services);

        var consumer = (GreeterConsumer)provider.GetService(typeof(GreeterConsumer));

        Assert.Equal("Hello", consumer.Greeter.Greet());
    }

    [Fact]
    public void GetService_MultipleDependencyChains_ShareSameSingletonAcrossBothPaths()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();
        services.Transient<GreeterConsumer>();
        services.Transient<MultiDependencyConsumer>();

        var provider = new TempestServiceProvider(services);

        var resolved = (MultiDependencyConsumer)provider.GetService(typeof(MultiDependencyConsumer));

        Assert.Same(resolved.Greeter, resolved.Consumer.Greeter);
    }

    [Fact]
    public void GetService_ThrowsServiceNotRegisteredException_WhenDependencyMissing()
    {
        var services = new ServiceCollection();
        services.Transient<MissingDependencyConsumer>();

        var provider = new TempestServiceProvider(services);

        var exception = Assert.Throws<ServiceNotRegisteredException>(() =>
            (MissingDependencyConsumer)provider.GetService(typeof(MissingDependencyConsumer)));

        Assert.Equal(typeof(IUnregisteredService), exception.MissingServiceType);
        Assert.Equal(typeof(MissingDependencyConsumer), exception.RequestedService);
        Assert.Contains("MissingDependencyConsumer", exception.Message);
        Assert.Contains("IUnregisteredService", exception.Message);
    }

    [Fact]
    public void GetService_ThrowsCircularServiceDependencyException_WhenServicesDependOnEachOther()
    {
        var services = new ServiceCollection();
        services.Transient<CircularServiceA>();
        services.Transient<CircularServiceB>();

        var provider = new TempestServiceProvider(services);

        var exception = Assert.Throws<CircularServiceDependencyException>(() =>
            (CircularServiceA)provider.GetService(typeof(CircularServiceA)));

        Assert.Equal(typeof(CircularServiceA), exception.RequestedService);
        Assert.Contains("CircularServiceA", exception.Message);
        Assert.Contains("CircularServiceB", exception.Message);
    }

    [Fact]
    public void GetService_ThrowsAmbiguousConstructorException_WhenMultiplePublicConstructorsExist()
    {
        var services = new ServiceCollection();
        services.Transient<MultipleConstructorsService>();

        var provider = new TempestServiceProvider(services);

        var exception = Assert.Throws<AmbiguousConstructorException>(() =>
            (MultipleConstructorsService)provider.GetService(typeof(MultipleConstructorsService)));

        Assert.Equal(typeof(MultipleConstructorsService), exception.ImplementationType);
        Assert.Equal(2, exception.PublicConstructorCount);
    }

    [Fact]
    public void GetService_ThrowsServiceResolutionException_WhenNoPublicConstructorExists()
    {
        var services = new ServiceCollection();
        services.Transient<NoPublicConstructorService>();

        var provider = new TempestServiceProvider(services);

        Assert.Throws<ServiceResolutionException>(() =>
            (NoPublicConstructorService)provider.GetService(typeof(NoPublicConstructorService)));
    }

    [Fact]
    public void GetService_ThrowsArgumentNullException_WhenServiceTypeIsNull()
    {
        var provider = new TempestServiceProvider(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() => provider.GetService(null!));
    }

    [Fact]
    public void GetService_ResolvesDiscoveredModuleType_ForRuntimeModuleCreation()
    {
        var descriptor = new ModuleDescriptor(
            "lifecycle.alpha",
            "Recording Lifecycle Module Alpha",
            "1.0.0",
            typeof(RecordingLifecycleModuleAlpha));

        var services = new ServiceCollection();
        services.AddDiscoveredModules([descriptor]);

        var provider = new TempestServiceProvider(services);

        var first = provider.GetService(descriptor.ModuleType);
        var second = provider.GetService(descriptor.ModuleType);

        Assert.IsType<RecordingLifecycleModuleAlpha>(first);
        Assert.Same(first, second);
    }

    // TD-69: a constructor parameter whose type has no registration falls
    // back to the parameter's own declared default rather than throwing,
    // mirroring real platform types such as EventBus(ILogger? logger = null, ...).
    [Fact]
    public void GetService_OptionalDependencyUnregistered_UsesDeclaredDefault()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();
        services.Transient<OptionalDependencyConsumer>();

        var provider = new TempestServiceProvider(services);

        var consumer = (OptionalDependencyConsumer)provider.GetService(typeof(OptionalDependencyConsumer));

        Assert.Equal("Hello", consumer.Greeter.Greet());
        Assert.Null(consumer.Optional);
    }

    // A required (non-optional) parameter of an unregistered type must
    // still fail exactly as before - the optional-parameter fallback must
    // never mask a genuinely missing, required dependency.
    [Fact]
    public void GetService_RequiredDependencyUnregistered_AlongsideAnOptionalOne_StillThrowsServiceNotRegisteredException()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();
        services.Transient<RequiredAndOptionalDependencyConsumer>();

        var provider = new TempestServiceProvider(services);

        var exception = Assert.Throws<ServiceNotRegisteredException>(() =>
            (RequiredAndOptionalDependencyConsumer)provider.GetService(typeof(RequiredAndOptionalDependencyConsumer)));

        Assert.Equal(typeof(IUnregisteredService), exception.MissingServiceType);
    }

    // `WP 17.0A`: the fallback to a declared default is taken only where the
    // author annotated the parameter as nullable (or it is a value type).
    // A non-nullable reference with a default is refused, and a fallback
    // that IS taken is logged at Warning so a registration slip is visible.
    [Fact]
    public void GetService_NonNullableReferenceParameterWithDefault_OfUnregisteredType_ThrowsServiceNotRegisteredException()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();
        services.Transient<NonNullableDefaultedDependencyConsumer>();

        var provider = new TempestServiceProvider(services);

        var exception = Assert.Throws<ServiceNotRegisteredException>(() =>
            provider.GetService(typeof(NonNullableDefaultedDependencyConsumer)));

        Assert.Equal(typeof(IUnregisteredService), exception.MissingServiceType);
    }

    [Fact]
    public void GetService_ValueTypeParametersWithDefaults_UseTheirDefaults()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();
        services.Transient<ValueTypeDefaultedConsumer>();

        var provider = new TempestServiceProvider(services);

        var consumer = (ValueTypeDefaultedConsumer)provider.GetService(typeof(ValueTypeDefaultedConsumer));

        Assert.Equal(3, consumer.Retries);
        Assert.Equal(TimeSpan.Zero, consumer.Timeout);
    }

    [Fact]
    public void GetService_NullableOptionalDependencyUnregistered_LogsAWarningNamingTheParameter()
    {
        var logger = new RecordingLogger();
        var services = new ServiceCollection(logger);
        services.Singleton<IGreeter, Greeter>();
        services.Transient<OptionalDependencyConsumer>();

        var provider = new TempestServiceProvider(services, logger);

        var consumer = (OptionalDependencyConsumer)provider.GetService(typeof(OptionalDependencyConsumer));

        Assert.Null(consumer.Optional);
        Assert.Contains(logger.Messages, m => m.Contains("no registration; using its declared default", StringComparison.Ordinal)
            && m.Contains(nameof(IUnregisteredService), StringComparison.Ordinal));
    }

    [Fact]
    public void GetService_WithLogger_DoesNotThrowAndRecordsProgress()
    {
        var logger = new RecordingLogger();
        var services = new ServiceCollection(logger);
        services.Singleton<IGreeter, Greeter>();

        var provider = new TempestServiceProvider(services, logger);

        var greeter = (IGreeter)provider.GetService(typeof(IGreeter));

        Assert.Equal("Hello", greeter.Greet());
        Assert.NotEmpty(logger.Messages);
    }

    // ----------------------------------------------------------------
    // Disposal (`TD-03`) — reflection-constructed singletons only; an
    // AddInstance registration remains the registering Host's own
    // responsibility to dispose.
    // ----------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_ReflectionConstructedSingleton_IsDisposed()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddInstance(order);
        services.Singleton<DisposableService>();

        var provider = new TempestServiceProvider(services);
        var instance = (DisposableService)provider.GetService(typeof(DisposableService));

        await provider.DisposeAsync();

        Assert.Equal(1, instance.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_NeverConstructed_DisposesNothing()
    {
        var services = new ServiceCollection();
        services.AddInstance(new List<string>());
        services.Singleton<DisposableService>();

        var provider = new TempestServiceProvider(services);

        // GetService is never called - the singleton is registered but
        // never actually built, so there is nothing for DisposeAsync to do.
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_TransientService_NeverTracked_DisposeCallCountStaysZero()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddInstance(order);
        services.Transient<DisposableService>();

        var provider = new TempestServiceProvider(services);
        var first = (DisposableService)provider.GetService(typeof(DisposableService));
        var second = (DisposableService)provider.GetService(typeof(DisposableService));

        await provider.DisposeAsync();

        Assert.Equal(0, first.DisposeCallCount);
        Assert.Equal(0, second.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_InstanceRegistration_IsNeverDisposedByTheProvider()
    {
        // `TD-03`'s own text scopes this to reflection-constructed
        // singletons: an AddInstance registration (the persistence store,
        // `ADR-0144`) is the registering Host's own responsibility
        // (TempestHost's own Service Disposal phase), not the container's.
        var services = new ServiceCollection();
        var instance = new DisposableService(disposalOrder: []);
        services.AddInstance(instance);

        var provider = new TempestServiceProvider(services);
        provider.GetService(typeof(DisposableService));

        await provider.DisposeAsync();

        Assert.Equal(0, instance.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_PrefersAsyncDisposalOverSynchronousWhereATypeOffersBoth()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddInstance(order);
        services.Singleton<AsyncDisposableService>();

        var provider = new TempestServiceProvider(services);
        var instance = (AsyncDisposableService)provider.GetService(typeof(AsyncDisposableService));

        await provider.DisposeAsync();

        Assert.True(instance.AsyncDisposeCalled);
        Assert.False(instance.SyncDisposeCalled);
    }

    [Fact]
    public async Task DisposeAsync_TwoSingletons_DisposesInTheReverseOfConstructionOrder()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddInstance(order);
        services.Singleton<DisposableDependency>();
        services.Singleton<DisposableDependent>();

        var provider = new TempestServiceProvider(services);

        // Resolving the dependent constructs its own dependency first (and
        // caches it), then itself - DisposeAsync must reverse that.
        provider.GetService(typeof(DisposableDependent));

        await provider.DisposeAsync();

        Assert.Equal(["dependent", "dependency"], order);
    }

    [Fact]
    public async Task DisposeAsync_OneInstanceThrows_TheRemainingInstancesAreStillDisposed()
    {
        var order = new List<string>();
        var logger = new RecordingLogger();
        var services = new ServiceCollection(logger);
        services.AddInstance(order);
        services.Singleton<DisposableService>();
        services.Singleton<ThrowingDisposableService>();

        var provider = new TempestServiceProvider(services, logger);
        var wellBehaved = (DisposableService)provider.GetService(typeof(DisposableService));
        provider.GetService(typeof(ThrowingDisposableService));

        await provider.DisposeAsync();

        Assert.Equal(1, wellBehaved.DisposeCallCount);
        Assert.Contains(logger.Messages, m => m.Contains("threw while being disposed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DisposesOnlyOnce()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddInstance(order);
        services.Singleton<DisposableService>();

        var provider = new TempestServiceProvider(services);
        var instance = (DisposableService)provider.GetService(typeof(DisposableService));

        await provider.DisposeAsync();
        await provider.DisposeAsync();

        Assert.Equal(1, instance.DisposeCallCount);
    }
}
