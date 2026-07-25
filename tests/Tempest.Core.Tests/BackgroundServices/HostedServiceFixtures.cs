using Tempest.Core.BackgroundServices;
using Tempest.Core.Events;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.BackgroundServices;

/// <summary>
/// A shared, resettable call log every fixture in this file appends to,
/// mirroring <c>LifecycleTestLog</c>'s own established pattern from the
/// WP 2.3 module lifecycle tests — necessary because hosted service
/// fixtures are constructed via <c>TempestServiceProvider</c>, with no
/// direct way for a test to inject shared, cross-instance recording state
/// through the constructor alone.
/// </summary>
internal static class HostedServiceCallLog
{
    private static readonly List<string> _entries = [];
    private static readonly object _gate = new();

    public static IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate)
                return _entries.ToList();
        }
    }

    public static void Record(string entry)
    {
        lock (_gate)
            _entries.Add(entry);
    }

    public static void Reset()
    {
        lock (_gate)
            _entries.Clear();
    }
}

/// <summary>
/// Lets a test arrange for <see cref="CancellingHostedService"/> to cancel a
/// specific token source from within its own <c>StartAsync</c>, proving
/// cancellation is observed between services rather than only before the
/// first one.
/// </summary>
internal static class CancellingHostedServiceControl
{
    public static CancellationTokenSource? TokenSourceToCancel { get; set; }
}

internal sealed class AlphaHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(AlphaHostedService)}:Start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(AlphaHostedService)}:Stop");
        return Task.CompletedTask;
    }
}

internal sealed class BetaHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(BetaHostedService)}:Start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(BetaHostedService)}:Stop");
        return Task.CompletedTask;
    }
}

internal sealed class GammaHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(GammaHostedService)}:Start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(GammaHostedService)}:Stop");
        return Task.CompletedTask;
    }
}

/// <summary>Sorts between <see cref="AlphaHostedService"/> and <see cref="GammaHostedService"/> — "C" &lt; "G".</summary>
internal sealed class CancellingHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(CancellingHostedService)}:Start");
        CancellingHostedServiceControl.TokenSourceToCancel?.Cancel();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(CancellingHostedService)}:Stop");
        return Task.CompletedTask;
    }
}

/// <summary>A non-critical service that throws from both lifecycle methods — proves isolation.</summary>
internal sealed class IsolatedThrowingHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(IsolatedThrowingHostedService)}:Start");
        throw new InvalidOperationException("Isolated start failure.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(IsolatedThrowingHostedService)}:Stop");
        throw new InvalidOperationException("Isolated stop failure.");
    }
}

/// <summary>A critical service whose <c>StartAsync</c> throws — proves Host-fatal escalation on start.</summary>
internal sealed class CriticalStartFailureHostedService : ICriticalBackgroundService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(CriticalStartFailureHostedService)}:Start");
        throw new InvalidOperationException("Critical start failure.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A critical service that starts successfully but whose <c>StopAsync</c> throws — proves Host-fatal escalation on stop.</summary>
internal sealed class CriticalStopFailureHostedService : ICriticalBackgroundService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(CriticalStopFailureHostedService)}:Start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(CriticalStopFailureHostedService)}:Stop");
        throw new InvalidOperationException("Critical stop failure.");
    }
}

/// <summary>
/// Requests two genuine, already-registered DI-public platform services via
/// ordinary constructor injection — proves a hosted service is
/// constructor-injectable with no attribute or metadata prerequisite of any
/// kind, unlike a discovered module.
/// </summary>
internal sealed class ConstructorInjectedHostedService : IHostedService
{
    public ConstructorInjectedHostedService(ILogger logger, IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventBus);

        Logger = logger;
        EventBus = eventBus;
    }

    public ILogger Logger { get; }

    public IEventBus EventBus { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(ConstructorInjectedHostedService)}:Start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        HostedServiceCallLog.Record($"{nameof(ConstructorInjectedHostedService)}:Stop");
        return Task.CompletedTask;
    }
}

/// <summary>An abstract type — must be excluded from discovery.</summary>
internal abstract class AbstractHostedService : IHostedService
{
    public abstract Task StartAsync(CancellationToken cancellationToken);

    public abstract Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>An open generic type definition — must be excluded from discovery.</summary>
internal sealed class GenericHostedService<T> : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
