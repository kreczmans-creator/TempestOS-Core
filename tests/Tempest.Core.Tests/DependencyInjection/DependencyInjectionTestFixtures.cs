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

// TD-69: exercises Construct's fallback to a constructor parameter's own
// declared default when the parameter's type has no registration at all -
// mirroring real platform types such as EventBus(ILogger? logger = null, ...).
internal sealed class OptionalDependencyConsumer
{
    public OptionalDependencyConsumer(IGreeter greeter, IUnregisteredService? optional = null)
    {
        Greeter = greeter;
        Optional = optional;
    }

    public IGreeter Greeter { get; }

    public IUnregisteredService? Optional { get; }
}

// A required parameter of an unregistered type must still fail exactly as
// before, even alongside an unrelated optional one - the optional-parameter
// fallback must never mask a genuinely missing, required dependency.
internal sealed class RequiredAndOptionalDependencyConsumer
{
    public RequiredAndOptionalDependencyConsumer(IUnregisteredService required, IGreeter? optional = null)
    {
    }
}
