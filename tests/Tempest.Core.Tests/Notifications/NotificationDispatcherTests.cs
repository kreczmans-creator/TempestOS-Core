using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;
using Tempest.Core.Notifications;

namespace Tempest.Core.Tests.Notifications;

// Proves ADR-0046's dispatch, subscription, and failure model against the
// real NotificationDispatcher implementation - imperative subscription,
// sequential snapshot-based dispatch in subscription order, unconditional
// per-subscriber failure isolation logged at Warning (not Error, the level
// EventBus itself uses - see NotificationDispatcher's own remarks), and
// safe re-entrant publishing. No module or sample hosted service is
// exercised here - only NotificationDispatcher itself.
public class NotificationDispatcherTests
{
    // ------------------------------------------------------------------
    // Subscribe / Unsubscribe
    // ------------------------------------------------------------------

    [Fact]
    public async Task Subscribe_ThenPublish_HandlerReceivesTheNotification()
    {
        var dispatcher = new NotificationDispatcher();
        var handler = new RecordingHandler<RecordedNotificationA>();
        var raised = new RecordedNotificationA("payload");

        dispatcher.Subscribe(handler);
        await dispatcher.PublishAsync(raised, CancellationToken.None);

        Assert.Same(raised, Assert.Single(handler.Received));
    }

    [Fact]
    public async Task Unsubscribe_StopsFurtherDelivery_WithoutAffectingOtherSubscribers()
    {
        var dispatcher = new NotificationDispatcher();
        var removed = new RecordingHandler<RecordedNotificationA>();
        var remaining = new RecordingHandler<RecordedNotificationA>();

        dispatcher.Subscribe(removed);
        dispatcher.Subscribe(remaining);
        dispatcher.Unsubscribe(removed);

        await dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None);

        Assert.Empty(removed.Received);
        Assert.Single(remaining.Received);
    }

    [Fact]
    public void Unsubscribe_HandlerNeverSubscribed_IsNoOp()
    {
        var dispatcher = new NotificationDispatcher();
        var handler = new RecordingHandler<RecordedNotificationA>();

        var exception = Record.Exception(() => dispatcher.Unsubscribe(handler));

        Assert.Null(exception);
    }

    [Fact]
    public void Subscribe_NullHandler_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new NotificationDispatcher().Subscribe<RecordedNotificationA>(null!));

    [Fact]
    public void Unsubscribe_NullHandler_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new NotificationDispatcher().Unsubscribe<RecordedNotificationA>(null!));

    [Fact]
    public async Task PublishAsync_NullNotification_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new NotificationDispatcher().PublishAsync<RecordedNotificationA>(null!, CancellationToken.None));

    // ------------------------------------------------------------------
    // Ordering and sequential dispatch
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_AreInvokedInSubscriptionOrder()
    {
        var dispatcher = new NotificationDispatcher();
        var order = new List<string>();

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("first"); return Task.CompletedTask; }));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("second"); return Task.CompletedTask; }));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("third"); return Task.CompletedTask; }));

        await dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None);

        Assert.Equal(["first", "second", "third"], order);
    }

    [Fact]
    public async Task PublishAsync_DispatchesSequentially_NeverMoreThanOneHandlerInFlight()
    {
        var dispatcher = new NotificationDispatcher();
        var gate = new object();
        int inFlight = 0;
        int maxInFlight = 0;

        async Task Handle(RecordedNotificationA n, CancellationToken ct)
        {
            lock (gate)
            {
                inFlight++;
                maxInFlight = Math.Max(maxInFlight, inFlight);
            }

            await Task.Delay(20, ct);

            lock (gate)
                inFlight--;
        }

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>(Handle));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>(Handle));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>(Handle));

        await dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None);

        Assert.Equal(1, maxInFlight);
    }

    [Fact]
    public async Task PublishAsync_SubscriptionOrder_IsDeterministicAcrossRepeatedPublishes()
    {
        var dispatcher = new NotificationDispatcher();
        var order = new List<string>();

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("alpha"); return Task.CompletedTask; }));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("beta"); return Task.CompletedTask; }));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("gamma"); return Task.CompletedTask; }));

        for (var i = 0; i < 5; i++)
        {
            order.Clear();
            await dispatcher.PublishAsync(new RecordedNotificationA($"run-{i}"), CancellationToken.None);
            Assert.Equal(["alpha", "beta", "gamma"], order);
        }
    }

    // ------------------------------------------------------------------
    // Snapshot semantics
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_SubscriberAddedDuringDispatch_DoesNotReceiveTheInFlightPublish()
    {
        var dispatcher = new NotificationDispatcher();
        var lateHandler = new RecordingHandler<RecordedNotificationA>();

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) =>
        {
            dispatcher.Subscribe(lateHandler);
            return Task.CompletedTask;
        }));

        await dispatcher.PublishAsync(new RecordedNotificationA("first"), CancellationToken.None);

        Assert.Empty(lateHandler.Received);
    }

    [Fact]
    public async Task PublishAsync_SubscriberAddedDuringDispatch_ReceivesTheNextPublish()
    {
        var dispatcher = new NotificationDispatcher();
        var lateHandler = new RecordingHandler<RecordedNotificationA>();

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) =>
        {
            dispatcher.Subscribe(lateHandler);
            return Task.CompletedTask;
        }));

        await dispatcher.PublishAsync(new RecordedNotificationA("first"), CancellationToken.None);
        await dispatcher.PublishAsync(new RecordedNotificationA("second"), CancellationToken.None);

        Assert.Single(lateHandler.Received);
    }

    [Fact]
    public async Task PublishAsync_SubscriberRemovedDuringDispatch_StillReceivesTheInFlightPublish()
    {
        var dispatcher = new NotificationDispatcher();
        RecordingHandler<RecordedNotificationA>? selfRemoving = null;
        selfRemoving = new RecordingHandler<RecordedNotificationA>((n, ct) =>
        {
            dispatcher.Unsubscribe(selfRemoving!);
            return Task.CompletedTask;
        });

        dispatcher.Subscribe(selfRemoving);

        await dispatcher.PublishAsync(new RecordedNotificationA("first"), CancellationToken.None);

        Assert.Single(selfRemoving.Received);
    }

    [Fact]
    public async Task PublishAsync_SubscriberRemovedDuringDispatch_DoesNotReceiveTheNextPublish()
    {
        var dispatcher = new NotificationDispatcher();
        RecordingHandler<RecordedNotificationA>? selfRemoving = null;
        selfRemoving = new RecordingHandler<RecordedNotificationA>((n, ct) =>
        {
            dispatcher.Unsubscribe(selfRemoving!);
            return Task.CompletedTask;
        });

        dispatcher.Subscribe(selfRemoving);

        await dispatcher.PublishAsync(new RecordedNotificationA("first"), CancellationToken.None);
        await dispatcher.PublishAsync(new RecordedNotificationA("second"), CancellationToken.None);

        Assert.Single(selfRemoving.Received);
    }

    // ------------------------------------------------------------------
    // Re-entrant publishing
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ReentrantPublishOfADifferentNotificationType_CompletesSafely()
    {
        var dispatcher = new NotificationDispatcher();
        var innerReceived = new List<RecordedNotificationB>();

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationB>((n, ct) => { innerReceived.Add(n); return Task.CompletedTask; }));

        var outerHandler = new RecordingHandler<RecordedNotificationA>(async (n, ct) =>
            await dispatcher.PublishAsync(new RecordedNotificationB(), ct));
        dispatcher.Subscribe(outerHandler);

        await dispatcher.PublishAsync(new RecordedNotificationA("outer"), CancellationToken.None);

        Assert.Single(outerHandler.Received);
        Assert.Single(innerReceived);
    }

    [Fact]
    public async Task PublishAsync_ReentrantPublishOfTheSameNotificationType_DispatchesOverIndependentSnapshotsInNestedOrder()
    {
        var dispatcher = new NotificationDispatcher();
        var order = new List<string>();
        var reentered = false;

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>(async (n, ct) =>
        {
            order.Add($"enter:{n.Payload}");

            if (!reentered)
            {
                reentered = true;
                await dispatcher.PublishAsync(new RecordedNotificationA("inner"), ct);
            }

            order.Add($"exit:{n.Payload}");
        }));

        await dispatcher.PublishAsync(new RecordedNotificationA("outer"), CancellationToken.None);

        Assert.Equal(["enter:outer", "enter:inner", "exit:inner", "exit:outer"], order);
    }

    // ------------------------------------------------------------------
    // Exception isolation and logging
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ThrowingSubscriber_DoesNotPreventSiblingSubscribers()
    {
        var dispatcher = new NotificationDispatcher();
        var order = new List<string>();

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("first"); return Task.CompletedTask; }));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) =>
        {
            order.Add("throwing");
            throw new InvalidOperationException("boom");
        }));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { order.Add("third"); return Task.CompletedTask; }));

        await dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None);

        Assert.Equal(["first", "throwing", "third"], order);
    }

    [Fact]
    public async Task PublishAsync_ThrowingSubscriber_ExceptionIsNeverRethrownToThePublisher()
    {
        var dispatcher = new NotificationDispatcher();
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => throw new InvalidOperationException("boom")));

        var exception = await Record.ExceptionAsync(() =>
            dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAsync_ThrowingSubscriber_LogsAtWarningLevel_NotErrorLevel()
    {
        // Platform Service Contracts.md's own Logging Requirements state
        // "Logs a warning for each isolated handler failure" - a
        // deliberate departure from EventBus's own Error-level assertion
        // (see EventBusTests.PublishAsync_ThrowingSubscriber_LogsAtErrorLevel),
        // disclosed in NotificationDispatcher's own remarks.
        var logger = new RecordingLevelLogger();
        var dispatcher = new NotificationDispatcher(logger);
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => throw new InvalidOperationException("boom")));

        await dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None);

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "threw while handling"));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task PublishAsync_NoThrowingSubscribers_LogsNothingAtWarningLevel()
    {
        var logger = new RecordingLevelLogger();
        var dispatcher = new NotificationDispatcher(logger);
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>());

        await dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None);

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_CancelledBetweenSubscribers_PropagatesUncaught_WithoutInvokingRemainingSubscribers()
    {
        var dispatcher = new NotificationDispatcher();
        using var cts = new CancellationTokenSource();
        var invoked = new List<string>();

        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) =>
        {
            invoked.Add("first");
            cts.Cancel();
            return Task.CompletedTask;
        }));
        dispatcher.Subscribe(new RecordingHandler<RecordedNotificationA>((n, ct) => { invoked.Add("second"); return Task.CompletedTask; }));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            dispatcher.PublishAsync(new RecordedNotificationA("x"), cts.Token));

        Assert.Equal(["first"], invoked);
    }

    // ------------------------------------------------------------------
    // No-op and multi-notification-type behaviour
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_NoSubscribers_CompletesWithoutError()
    {
        var dispatcher = new NotificationDispatcher();

        var exception = await Record.ExceptionAsync(() =>
            dispatcher.PublishAsync(new RecordedNotificationA("x"), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAsync_DispatchesOnlyToSubscribersOfTheExactNotificationType()
    {
        var dispatcher = new NotificationDispatcher();
        var aHandler = new RecordingHandler<RecordedNotificationA>();
        var bHandler = new RecordingHandler<RecordedNotificationB>();

        dispatcher.Subscribe(aHandler);
        dispatcher.Subscribe(bHandler);

        await dispatcher.PublishAsync(new RecordedNotificationA("a"), CancellationToken.None);

        Assert.Single(aHandler.Received);
        Assert.Empty(bHandler.Received);
    }

    // ------------------------------------------------------------------
    // Platform Service registration (ADR-0046: an ordinary singleton, no
    // Composition Root treatment needed)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesINotificationDispatcherToNotificationDispatcher()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<INotificationDispatcher, NotificationDispatcher>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(INotificationDispatcher));

        Assert.IsType<NotificationDispatcher>(resolved);
    }

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesTheSameInstanceEveryTime()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<INotificationDispatcher, NotificationDispatcher>();
        var provider = new TempestServiceProvider(services);

        var first = provider.GetService(typeof(INotificationDispatcher));
        var second = provider.GetService(typeof(INotificationDispatcher));

        Assert.Same(first, second);
    }
}
