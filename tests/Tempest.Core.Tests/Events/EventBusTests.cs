using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Events;

// Proves ADR-0028's dispatch, subscription, and failure model against the
// real EventBus implementation - imperative subscription, sequential
// snapshot-based dispatch in subscription order, unconditional per-subscriber
// failure isolation, and safe re-entrant publishing. No module, ClockModule,
// or event-publishing feature is exercised here - only EventBus itself.
public class EventBusTests
{
    // ------------------------------------------------------------------
    // Subscribe / Unsubscribe
    // ------------------------------------------------------------------

    [Fact]
    public async Task Subscribe_ThenPublish_HandlerReceivesTheEvent()
    {
        var bus = new EventBus();
        var handler = new RecordingHandler<RecordedEventA>();
        var raised = new RecordedEventA("payload");

        bus.Subscribe(handler);
        await bus.PublishAsync(raised, CancellationToken.None);

        Assert.Same(raised, Assert.Single(handler.Received));
    }

    [Fact]
    public async Task Unsubscribe_StopsFurtherDelivery_WithoutAffectingOtherSubscribers()
    {
        var bus = new EventBus();
        var removed = new RecordingHandler<RecordedEventA>();
        var remaining = new RecordingHandler<RecordedEventA>();

        bus.Subscribe(removed);
        bus.Subscribe(remaining);
        bus.Unsubscribe(removed);

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Empty(removed.Received);
        Assert.Single(remaining.Received);
    }

    [Fact]
    public void Unsubscribe_HandlerNeverSubscribed_IsNoOp()
    {
        var bus = new EventBus();
        var handler = new RecordingHandler<RecordedEventA>();

        var exception = Record.Exception(() => bus.Unsubscribe(handler));

        Assert.Null(exception);
    }

    [Fact]
    public void Subscribe_NullHandler_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new EventBus().Subscribe<RecordedEventA>(null!));

    [Fact]
    public void Unsubscribe_NullHandler_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new EventBus().Unsubscribe<RecordedEventA>(null!));

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new EventBus().PublishAsync<RecordedEventA>(null!, CancellationToken.None));

    // ------------------------------------------------------------------
    // Ordering and sequential dispatch
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_AreInvokedInSubscriptionOrder()
    {
        var bus = new EventBus();
        var order = new List<string>();

        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("first"); return Task.CompletedTask; }));
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("second"); return Task.CompletedTask; }));
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("third"); return Task.CompletedTask; }));

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Equal(["first", "second", "third"], order);
    }

    [Fact]
    public async Task PublishAsync_DispatchesSequentially_NeverMoreThanOneHandlerInFlight()
    {
        var bus = new EventBus();
        var gate = new object();
        int inFlight = 0;
        int maxInFlight = 0;

        async Task Handle(RecordedEventA e, CancellationToken ct)
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

        bus.Subscribe(new RecordingHandler<RecordedEventA>(Handle));
        bus.Subscribe(new RecordingHandler<RecordedEventA>(Handle));
        bus.Subscribe(new RecordingHandler<RecordedEventA>(Handle));

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Equal(1, maxInFlight);
    }

    [Fact]
    public async Task PublishAsync_SubscriptionOrder_IsDeterministicAcrossRepeatedPublishes()
    {
        var bus = new EventBus();
        var order = new List<string>();

        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("alpha"); return Task.CompletedTask; }));
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("beta"); return Task.CompletedTask; }));
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("gamma"); return Task.CompletedTask; }));

        for (var i = 0; i < 5; i++)
        {
            order.Clear();
            await bus.PublishAsync(new RecordedEventA($"run-{i}"), CancellationToken.None);
            Assert.Equal(["alpha", "beta", "gamma"], order);
        }
    }

    // ------------------------------------------------------------------
    // Snapshot semantics
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_SubscriberAddedDuringDispatch_DoesNotReceiveTheInFlightPublish()
    {
        var bus = new EventBus();
        var lateHandler = new RecordingHandler<RecordedEventA>();

        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) =>
        {
            bus.Subscribe(lateHandler);
            return Task.CompletedTask;
        }));

        await bus.PublishAsync(new RecordedEventA("first"), CancellationToken.None);

        Assert.Empty(lateHandler.Received);
    }

    [Fact]
    public async Task PublishAsync_SubscriberAddedDuringDispatch_ReceivesTheNextPublish()
    {
        var bus = new EventBus();
        var lateHandler = new RecordingHandler<RecordedEventA>();

        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) =>
        {
            bus.Subscribe(lateHandler);
            return Task.CompletedTask;
        }));

        await bus.PublishAsync(new RecordedEventA("first"), CancellationToken.None);
        await bus.PublishAsync(new RecordedEventA("second"), CancellationToken.None);

        Assert.Single(lateHandler.Received);
    }

    [Fact]
    public async Task PublishAsync_SubscriberRemovedDuringDispatch_StillReceivesTheInFlightPublish()
    {
        var bus = new EventBus();
        RecordingHandler<RecordedEventA>? selfRemoving = null;
        selfRemoving = new RecordingHandler<RecordedEventA>((e, ct) =>
        {
            bus.Unsubscribe(selfRemoving!);
            return Task.CompletedTask;
        });

        bus.Subscribe(selfRemoving);

        await bus.PublishAsync(new RecordedEventA("first"), CancellationToken.None);

        Assert.Single(selfRemoving.Received);
    }

    [Fact]
    public async Task PublishAsync_SubscriberRemovedDuringDispatch_DoesNotReceiveTheNextPublish()
    {
        var bus = new EventBus();
        RecordingHandler<RecordedEventA>? selfRemoving = null;
        selfRemoving = new RecordingHandler<RecordedEventA>((e, ct) =>
        {
            bus.Unsubscribe(selfRemoving!);
            return Task.CompletedTask;
        });

        bus.Subscribe(selfRemoving);

        await bus.PublishAsync(new RecordedEventA("first"), CancellationToken.None);
        await bus.PublishAsync(new RecordedEventA("second"), CancellationToken.None);

        Assert.Single(selfRemoving.Received);
    }

    // ------------------------------------------------------------------
    // Re-entrant publishing
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ReentrantPublishOfADifferentEventType_CompletesSafely()
    {
        var bus = new EventBus();
        var innerReceived = new List<RecordedEventB>();

        bus.Subscribe(new RecordingHandler<RecordedEventB>((e, ct) => { innerReceived.Add(e); return Task.CompletedTask; }));

        var outerHandler = new RecordingHandler<RecordedEventA>(async (e, ct) =>
            await bus.PublishAsync(new RecordedEventB(), ct));
        bus.Subscribe(outerHandler);

        await bus.PublishAsync(new RecordedEventA("outer"), CancellationToken.None);

        Assert.Single(outerHandler.Received);
        Assert.Single(innerReceived);
    }

    [Fact]
    public async Task PublishAsync_ReentrantPublishOfTheSameEventType_DispatchesOverIndependentSnapshotsInNestedOrder()
    {
        var bus = new EventBus();
        var order = new List<string>();
        var reentered = false;

        bus.Subscribe(new RecordingHandler<RecordedEventA>(async (e, ct) =>
        {
            order.Add($"enter:{e.Payload}");

            if (!reentered)
            {
                reentered = true;
                await bus.PublishAsync(new RecordedEventA("inner"), ct);
            }

            order.Add($"exit:{e.Payload}");
        }));

        await bus.PublishAsync(new RecordedEventA("outer"), CancellationToken.None);

        Assert.Equal(["enter:outer", "enter:inner", "exit:inner", "exit:outer"], order);
    }

    // ------------------------------------------------------------------
    // Exception isolation and logging
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ThrowingSubscriber_DoesNotPreventSiblingSubscribers()
    {
        var bus = new EventBus();
        var order = new List<string>();

        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("first"); return Task.CompletedTask; }));
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) =>
        {
            order.Add("throwing");
            throw new InvalidOperationException("boom");
        }));
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { order.Add("third"); return Task.CompletedTask; }));

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Equal(["first", "throwing", "third"], order);
    }

    [Fact]
    public async Task PublishAsync_ThrowingSubscriber_ExceptionIsNeverRethrownToThePublisher()
    {
        var bus = new EventBus();
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => throw new InvalidOperationException("boom")));

        var exception = await Record.ExceptionAsync(() =>
            bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAsync_ThrowingSubscriber_LogsAtErrorLevel()
    {
        var logger = new RecordingLevelLogger();
        var bus = new EventBus(logger);
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => throw new InvalidOperationException("boom")));

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.True(logger.HasEntryAt(LogLevel.Error, "threw while handling"));
    }

    [Fact]
    public async Task PublishAsync_NoThrowingSubscribers_LogsNothingAtErrorLevel()
    {
        var logger = new RecordingLevelLogger();
        var bus = new EventBus(logger);
        bus.Subscribe(new RecordingHandler<RecordedEventA>());

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_CancelledBetweenSubscribers_PropagatesUncaught_WithoutInvokingRemainingSubscribers()
    {
        var bus = new EventBus();
        using var cts = new CancellationTokenSource();
        var invoked = new List<string>();

        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) =>
        {
            invoked.Add("first");
            cts.Cancel();
            return Task.CompletedTask;
        }));
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { invoked.Add("second"); return Task.CompletedTask; }));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            bus.PublishAsync(new RecordedEventA("x"), cts.Token));

        Assert.Equal(["first"], invoked);
    }

    // ------------------------------------------------------------------
    // No-op and multi-event-type behaviour
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_NoSubscribers_CompletesWithoutError()
    {
        var bus = new EventBus();

        var exception = await Record.ExceptionAsync(() =>
            bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAsync_DispatchesOnlyToSubscribersOfTheExactEventType()
    {
        var bus = new EventBus();
        var aHandler = new RecordingHandler<RecordedEventA>();
        var bHandler = new RecordingHandler<RecordedEventB>();

        bus.Subscribe(aHandler);
        bus.Subscribe(bHandler);

        await bus.PublishAsync(new RecordedEventA("a"), CancellationToken.None);

        Assert.Single(aHandler.Received);
        Assert.Empty(bHandler.Received);
    }

    // ------------------------------------------------------------------
    // Platform Service registration (ADR-0028: an ordinary singleton, no
    // Composition Root treatment needed)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesIEventBusToEventBus()
    {
        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(IEventBus));

        Assert.IsType<EventBus>(resolved);
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
        services.Singleton<IEventBus, EventBus>();
        var provider = new TempestServiceProvider(services);

        var first = provider.GetService(typeof(IEventBus));
        var second = provider.GetService(typeof(IEventBus));

        Assert.Same(first, second);
    }
}
