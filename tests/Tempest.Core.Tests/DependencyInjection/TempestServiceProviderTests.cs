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
}
