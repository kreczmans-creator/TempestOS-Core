using Tempest.Core.Commands;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.Commands;

// Proves ADR-0036/ADR-0037/ADR-0038 against the real CommandDispatcher
// implementation - imperative handler registration, duplicate rejection,
// dispatch to exactly one handler, and failure propagation (both an
// expected CommandResult.Failure and an unexpected thrown exception) -
// deliberately diverging from EventBus's own per-subscriber isolation.
public class CommandDispatcherTests
{
    private static CommandDispatcher CreateDispatcher(ILogger? logger = null) => new(new CommandHandlerTable(), logger);

    // ------------------------------------------------------------------
    // Registration
    // ------------------------------------------------------------------

    [Fact]
    public async Task RegisterHandler_ThenDispatch_InvokesTheRegisteredHandler()
    {
        var dispatcher = CreateDispatcher();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);

        var command = new RecordedCommandA("hello");
        await dispatcher.DispatchAsync(command, CancellationToken.None);

        Assert.Same(command, Assert.Single(handler.Received));
    }

    [Fact]
    public void RegisterHandler_NullHandler_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => CreateDispatcher().RegisterHandler<RecordedCommandA>(null!));

    [Fact]
    public void RegisterHandler_DuplicateCommandType_ThrowsDuplicateCommandHandlerException()
    {
        var dispatcher = CreateDispatcher();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>());

        var exception = Assert.Throws<DuplicateCommandHandlerException>(
            () => dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>()));

        Assert.Equal(typeof(RecordedCommandA), exception.CommandType);
    }

    [Fact]
    public async Task RegisterHandler_DuplicateCommandType_DoesNotReplaceTheOriginalHandler()
    {
        var dispatcher = CreateDispatcher();
        var original = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(original);

        Assert.ThrowsAny<DuplicateCommandHandlerException>(
            () => dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>()));

        await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.Single(original.Received);
    }

    [Fact]
    public async Task RegisterHandler_DistinctCommandTypes_BothDispatchIndependently()
    {
        var dispatcher = CreateDispatcher();
        var handlerA = new RecordingCommandHandler<RecordedCommandA>();
        var handlerB = new RecordingCommandHandler<RecordedCommandB>();
        dispatcher.RegisterHandler(handlerA);
        dispatcher.RegisterHandler(handlerB);

        await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);
        await dispatcher.DispatchAsync(new RecordedCommandB(), CancellationToken.None);

        Assert.Single(handlerA.Received);
        Assert.Single(handlerB.Received);
    }

    // ------------------------------------------------------------------
    // Dispatch: success, expected failure, and unknown command type
    // ------------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_HandlerSucceeds_ReturnsTheHandlersResult()
    {
        var dispatcher = CreateDispatcher();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => Task.FromResult(CommandResult.Success("done"))));

        var result = await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("done", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_HandlerReturnsFailure_ReturnsTheFailureResult_WithoutThrowing()
    {
        var dispatcher = CreateDispatcher();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => Task.FromResult(CommandResult.Failure("invalid"))));

        var result = await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_NoHandlerRegistered_ThrowsCommandHandlerNotRegisteredException()
    {
        var dispatcher = CreateDispatcher();

        var exception = await Assert.ThrowsAsync<CommandHandlerNotRegisteredException>(
            () => dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None));

        Assert.Equal(typeof(RecordedCommandA), exception.CommandType);
    }

    [Fact]
    public async Task DispatchAsync_NullCommand_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateDispatcher().DispatchAsync<RecordedCommandA>(null!, CancellationToken.None));

    // ------------------------------------------------------------------
    // Failure propagation (ADR-0038): a handler's own exception propagates
    // uncaught - deliberately not isolated, unlike EventBus's own
    // per-subscriber isolation (ADR-0028).
    // ------------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_HandlerThrows_ExceptionPropagatesUncaughtToTheCaller()
    {
        var dispatcher = CreateDispatcher();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => throw new InvalidOperationException("boom")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_LogsAtErrorLevelBeforePropagating()
    {
        var logger = new RecordingLevelLogger();
        var dispatcher = CreateDispatcher(logger);
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => throw new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None));

        Assert.True(logger.HasEntryAt(LogLevel.Error, "handler threw"));
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_HandlerObservesCancellation_PropagatesUncaught()
    {
        var dispatcher = CreateDispatcher();
        using var cts = new CancellationTokenSource();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(CommandResult.Success());
        }));

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(new RecordedCommandA(), cts.Token));
    }

    // ------------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterHandler_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var dispatcher = CreateDispatcher(logger);

        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>());

        Assert.True(logger.HasEntryAt(LogLevel.Information, "handler registered"));
    }

    [Fact]
    public async Task DispatchAsync_Succeeds_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var dispatcher = CreateDispatcher(logger);
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>());

        await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.True(logger.HasEntryAt(LogLevel.Information, "Succeeded"));
    }

    [Fact]
    public async Task DispatchAsync_Fails_LogsAtWarningLevel()
    {
        var logger = new RecordingLevelLogger();
        var dispatcher = CreateDispatcher(logger);
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => Task.FromResult(CommandResult.Failure("nope"))));

        await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "Failed"));
    }

    // ------------------------------------------------------------------
    // Repeated execution / determinism
    // ------------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_RepeatedDispatchOfTheSameCommandType_EachInvocationIndependent()
    {
        var dispatcher = CreateDispatcher();
        var counter = 0;
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>((_, _) =>
        {
            counter++;
            return Task.FromResult(CommandResult.Success(counter.ToString()));
        }));

        for (var i = 1; i <= 5; i++)
        {
            var result = await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);
            Assert.Equal(i.ToString(), result.Message);
        }
    }

    // ------------------------------------------------------------------
    // Thread safety: concurrent registration of distinct command types,
    // and concurrent dispatch of an already-registered type, both succeed
    // without corruption - mirroring the coarse-grained-lock discipline
    // every other stateful platform service in this codebase already uses.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RegisterHandler_CalledConcurrentlyForDistinctCommandTypes_AllSucceed()
    {
        var dispatcher = CreateDispatcher();

        var registerA = Task.Run(() => dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>()));
        var registerB = Task.Run(() => dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandB>()));

        await Task.WhenAll(registerA, registerB);

        var resultA = await dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None);
        var resultB = await dispatcher.DispatchAsync(new RecordedCommandB(), CancellationToken.None);

        Assert.True(resultA.Succeeded);
        Assert.True(resultB.Succeeded);
    }

    [Fact]
    public async Task DispatchAsync_CalledConcurrently_EveryCallIsCountedExactlyOnce()
    {
        var dispatcher = CreateDispatcher();
        var gate = new object();
        var invocationCount = 0;

        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(async (_, _) =>
        {
            await Task.Delay(1);
            lock (gate)
                invocationCount++;
            return CommandResult.Success();
        }));

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => dispatcher.DispatchAsync(new RecordedCommandA(), CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(20, invocationCount);
    }

    // ------------------------------------------------------------------
    // Platform Service registration (ADR-0036: an ordinary singleton, no
    // Composition Root treatment needed for the public contracts).
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesICommandDispatcherToCommandDispatcher()
    {
        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(ICommandDispatcher));

        Assert.IsType<CommandDispatcher>(resolved);
    }

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesTheSameInstanceEveryTime()
    {
        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        var provider = new TempestServiceProvider(services);

        var first = provider.GetService(typeof(ICommandDispatcher));
        var second = provider.GetService(typeof(ICommandDispatcher));

        Assert.Same(first, second);
    }
}
