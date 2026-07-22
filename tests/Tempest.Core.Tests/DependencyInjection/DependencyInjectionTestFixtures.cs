namespace Tempest.Core.Tests.DependencyInjection;

// Test-only fixtures used exclusively to exercise TempestServiceProvider and
// ServiceCollection. None of these represent real application services.

internal interface IGreeter
{
    string Greet();
}

internal sealed class Greeter : IGreeter
{
    public string Greet() => "Hello";
}

internal sealed class GreeterConsumer
{
    public GreeterConsumer(IGreeter greeter)
    {
        Greeter = greeter;
    }

    public IGreeter Greeter { get; }
}

internal sealed class MultiDependencyConsumer
{
    public MultiDependencyConsumer(IGreeter greeter, GreeterConsumer consumer)
    {
        Greeter = greeter;
        Consumer = consumer;
    }

    public IGreeter Greeter { get; }

    public GreeterConsumer Consumer { get; }
}

internal interface IUnregisteredService
{
}

internal sealed class MissingDependencyConsumer
{
    public MissingDependencyConsumer(IUnregisteredService dependency)
    {
    }
}

internal sealed class MultipleConstructorsService
{
    public MultipleConstructorsService()
    {
    }

    public MultipleConstructorsService(IGreeter greeter)
    {
    }
}

internal sealed class NoPublicConstructorService
{
    private NoPublicConstructorService()
    {
    }
}

internal sealed class CircularServiceA
{
    public CircularServiceA(CircularServiceB dependency)
    {
    }
}

internal sealed class CircularServiceB
{
    public CircularServiceB(CircularServiceA dependency)
    {
    }
}
