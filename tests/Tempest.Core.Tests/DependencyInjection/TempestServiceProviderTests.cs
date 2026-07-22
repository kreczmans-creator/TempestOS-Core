using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
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

        var first = provider.GetService<IGreeter>();
        var second = provider.GetService<IGreeter>();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetService_InstanceRegistration_ReturnsTheExactSameInstance()
    {
        var services = new ServiceCollection();
        var greeter = new Greeter();
        services.AddInstance<IGreeter>(greeter);

        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService<IGreeter>();

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

        var consumer = provider.GetService<GreeterConsumer>();

        Assert.Same(greeter, consumer.Greeter);
    }

    [Fact]
    public void GetService_Transient_ReturnsDifferentInstanceEveryResolution()
    {
        var services = new ServiceCollection();
        services.Transient<IGreeter, Greeter>();

        var provider = new TempestServiceProvider(services);

        var first = provider.GetService<IGreeter>();
        var second = provider.GetService<IGreeter>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetService_ResolvesConstructorDependency()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();
        services.Transient<GreeterConsumer>();

        var provider = new TempestServiceProvider(services);

        var consumer = provider.GetService<GreeterConsumer>();

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

        var resolved = provider.GetService<MultiDependencyConsumer>();

        Assert.Same(resolved.Greeter, resolved.Consumer.Greeter);
    }

    [Fact]
    public void GetService_ThrowsServiceNotRegisteredException_WhenDependencyMissing()
    {
        var services = new ServiceCollection();
        services.Transient<MissingDependencyConsumer>();

        var provider = new TempestServiceProvider(services);

        var exception = Assert.Throws<ServiceNotRegisteredException>(() =>
            provider.GetService<MissingDependencyConsumer>());

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
            provider.GetService<CircularServiceA>());

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
            provider.GetService<MultipleConstructorsService>());

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
            provider.GetService<NoPublicConstructorService>());
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

    [Fact]
    public void GetService_WithLogger_DoesNotThrowAndRecordsProgress()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), $"tempest-di-tests-{Guid.NewGuid():N}");

        try
        {
            var logger = new LoggingService(logDirectory);
            var services = new ServiceCollection(logger);
            services.Singleton<IGreeter, Greeter>();

            var provider = new TempestServiceProvider(services, logger);

            var greeter = provider.GetService<IGreeter>();

            Assert.Equal("Hello", greeter.Greet());
        }
        finally
        {
            if (Directory.Exists(logDirectory))
                Directory.Delete(logDirectory, recursive: true);
        }
    }
}
