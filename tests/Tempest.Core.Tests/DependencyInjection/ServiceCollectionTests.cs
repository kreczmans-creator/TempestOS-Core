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

    // TD-69: a second Add for the same service type is far more likely to
    // be a mistake (an accidental re-registration silently swapping the
    // platform implementation) than a genuine need to replace one, so it
    // throws by default; allowReplace: true opts in to the rare deliberate
    // case explicitly. This test replaces the old
    // Add_SameServiceTypeTwice_LastRegistrationWins, which asserted the
    // exact silent-overwrite behaviour TD-69 closes.
    [Fact]
    public void Add_SameServiceTypeTwice_ThrowsDuplicateServiceRegistrationException()
    {
        var services = new ServiceCollection();

        services.Singleton<IGreeter, Greeter>();

        var exception = Assert.Throws<DuplicateServiceRegistrationException>(() =>
            services.Transient<IGreeter, Greeter>());

        Assert.Equal(typeof(IGreeter), exception.ServiceType);
        Assert.Contains("IGreeter", exception.Message);
    }

    [Fact]
    public void Add_SameServiceTypeTwiceWithAllowReplace_ReplacesTheRegistration()
    {
        var services = new ServiceCollection();

        services.Add(typeof(IGreeter), typeof(Greeter), ServiceLifetime.Singleton);
        services.Add(typeof(IGreeter), typeof(Greeter), ServiceLifetime.Transient, allowReplace: true);

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
    public void AddInstance_SameServiceTypeTwice_ThrowsDuplicateServiceRegistrationException()
    {
        var services = new ServiceCollection();
        services.AddInstance<IGreeter>(new Greeter());

        var exception = Assert.Throws<DuplicateServiceRegistrationException>(() =>
            services.AddInstance<IGreeter>(new Greeter()));

        Assert.Equal(typeof(IGreeter), exception.ServiceType);
        Assert.Contains("IGreeter", exception.Message);
    }

    [Fact]
    public void AddInstance_SameServiceTypeTwiceWithAllowReplace_ReplacesTheRegistration()
    {
        var services = new ServiceCollection();
        var first = new Greeter();
        var second = new Greeter();

        services.AddInstance(typeof(IGreeter), first);
        services.AddInstance(typeof(IGreeter), second, allowReplace: true);

        var descriptor = Assert.Single(services.Descriptors);
        Assert.Same(second, descriptor.ExistingInstance);
    }

    [Fact]
    public void Add_ThenAddInstanceSameServiceType_ThrowsDuplicateServiceRegistrationException()
    {
        var services = new ServiceCollection();
        services.Singleton<IGreeter, Greeter>();

        Assert.Throws<DuplicateServiceRegistrationException>(() =>
            services.AddInstance<IGreeter>(new Greeter()));
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
