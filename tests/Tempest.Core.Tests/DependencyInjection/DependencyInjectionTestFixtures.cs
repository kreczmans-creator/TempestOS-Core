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

// `TD-03`: a reflection-constructed singleton the container itself builds -
// as opposed to an AddInstance registration, which the registering Host
// already tracks and disposes on its own path (TempestHost's own Service
// Disposal phase). Takes only a List<string> constructor dependency (never
// a defaulted parameter of its own - a non-nullable reference type with a
// default is deliberately NOT resolved from its default by this container,
// `TD-64`/`WP 17.0A`, so a test fixture built through it must not carry
// one) so it can be both container-constructed and instantiated directly.
internal sealed class DisposableService : IDisposable
{
    public DisposableService(List<string> disposalOrder)
    {
    }

    public int DisposeCallCount { get; private set; }

    public void Dispose() => DisposeCallCount++;
}

// The async-disposal counterpart - proves DisposeAsync is preferred over
// Dispose where a type offers both, mirroring TempestHost's own
// DisposeRegisteredServiceInstancesAsync convention exactly.
internal sealed class AsyncDisposableService : IAsyncDisposable, IDisposable
{
    public AsyncDisposableService(List<string> disposalOrder)
    {
    }

    public bool AsyncDisposeCalled { get; private set; }

    public bool SyncDisposeCalled { get; private set; }

    public ValueTask DisposeAsync()
    {
        AsyncDisposeCalled = true;
        return ValueTask.CompletedTask;
    }

    public void Dispose() => SyncDisposeCalled = true;
}

// A singleton whose own disposal throws - proves one failing dispose does
// not stop the remaining instances from being disposed (`FOUNDATION.md`
// principle 5, the same guarantee TempestHost's own instance disposal
// already gives).
internal sealed class ThrowingDisposableService : IDisposable
{
    public void Dispose() => throw new InvalidOperationException("Deliberately fails disposal, for the test.");
}

// A dependency chain of two reflection-constructed singletons, so a test
// can prove disposal happens in the reverse of construction order: B
// depends on A, so A is constructed (and cached) first, and must be
// disposed last.
internal sealed class DisposableDependency : IDisposable
{
    private readonly List<string> _disposalOrder;

    public DisposableDependency(List<string> disposalOrder)
    {
        _disposalOrder = disposalOrder;
    }

    public void Dispose() => _disposalOrder.Add("dependency");
}

internal sealed class DisposableDependent : IDisposable
{
    private readonly List<string> _disposalOrder;

    public DisposableDependent(DisposableDependency dependency, List<string> disposalOrder)
    {
        _disposalOrder = disposalOrder;
    }

    public void Dispose() => _disposalOrder.Add("dependent");
}
