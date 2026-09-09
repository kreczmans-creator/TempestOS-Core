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

// `WP 17.0A`: a NON-nullable reference parameter that merely carries a
// default is not an optional dependency, and the container must not pass
// its default silently - that is how a registration slip disarmed a
// permission gate (`TD-64`). It is refused with the ordinary
// not-registered error instead.
internal sealed class NonNullableDefaultedDependencyConsumer
{
    public NonNullableDefaultedDependencyConsumer(IGreeter greeter, IUnregisteredService optional = null!)
    {
        Greeter = greeter;
        Optional = optional;
    }

    public IGreeter Greeter { get; }

    public IUnregisteredService? Optional { get; }
}

// A value-type parameter with a default is a real value, not a missing
// service, and keeps working exactly as before.
internal sealed class ValueTypeDefaultedConsumer
{
    public ValueTypeDefaultedConsumer(IGreeter greeter, int retries = 3, TimeSpan timeout = default)
    {
        Greeter = greeter;
        Retries = retries;
        Timeout = timeout;
    }

    public IGreeter Greeter { get; }

    public int Retries { get; }

    public TimeSpan Timeout { get; }
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
