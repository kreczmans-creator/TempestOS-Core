using Tempest.Core.DependencyInjection;

namespace Tempest.Core.Tests.DependencyInjection;

public class ServiceCollectionTests
{
    [Fact]
    public void Singleton_Self_RegistersServiceTypeAsImplementationType()
    {
        var services = new ServiceCollection();

        services.Singleton<Greeter>();

        var descriptor = Assert.Single(services.Descriptors);
        Assert.Equal(typeof(Greeter), descriptor.ServiceType);
        Assert.Equal(typeof(Greeter), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void Singleton_InterfaceAndImplementation_RegistersBothTypes()
    {
        var services = new ServiceCollection();

        services.Singleton<IGreeter, Greeter>();

        var descriptor = Assert.Single(services.Descriptors);
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.Equal(typeof(Greeter), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void Transient_Self_RegistersServiceTypeAsImplementationType()
    {
        var services = new ServiceCollection();

        services.Transient<Greeter>();

        var descriptor = Assert.Single(services.Descriptors);
        Assert.Equal(typeof(Greeter), descriptor.ServiceType);
        Assert.Equal(typeof(Greeter), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void Transient_InterfaceAndImplementation_RegistersBothTypes()
    {
        var services = new ServiceCollection();

        services.Transient<IGreeter, Greeter>();

        var descriptor = Assert.Single(services.Descriptors);
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.Equal(typeof(Greeter), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void Add_ThrowsArgumentException_WhenImplementationDoesNotSatisfyServiceType()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.Add(typeof(IGreeter), typeof(GreeterConsumer), ServiceLifetime.Singleton));
    }

    [Fact]
    public void Add_SameServiceTypeTwice_LastRegistrationWins()
    {
        var services = new ServiceCollection();

        services.Singleton<IGreeter, Greeter>();
        services.Transient<IGreeter, Greeter>();

        var descriptor = Assert.Single(services.Descriptors);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddInstance_RegistersExistingInstanceAsSingleton()
    {
        var services = new ServiceCollection();
        var greeter = new Greeter();

        services.AddInstance<IGreeter>(greeter);

        var descriptor = Assert.Single(services.Descriptors);
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.Equal(typeof(Greeter), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(greeter, descriptor.ExistingInstance);
    }

    [Fact]
    public void AddInstance_ThrowsArgumentException_WhenInstanceDoesNotSatisfyServiceType()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddInstance(typeof(IGreeter), new GreeterConsumer(new Greeter())));
    }

    [Fact]
    public void AddInstance_ThrowsArgumentNullException_WhenInstanceIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddInstance<IGreeter>(null!));
    }
}
