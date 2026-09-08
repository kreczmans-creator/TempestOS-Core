using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Events;

// ADR-0111: EventBus's own capability enforcement and component-scope
// propagation retrofit - the publisher's own plugin.events.publish:<Type>
// permission check, each subscriber's own captured owner being pushed onto
// ICurrentComponentAccessor immediately around its own HandleAsync call
// (never the publisher's), and Unsubscribe removing exactly one matching
// entry. None of this is exercised by EventBusTests.cs, which only proves
// dispatch/ordering/isolation behaviour with no component scope ever pushed.
public class EventBusCapabilityTests
{
    // ------------------------------------------------------------------
    // Publisher capability check
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_PublisherLacksEventPublishCapability_ThrowsPermissionDeniedException()
    {
        var (bus, accessor, _) = CreateBus();
        var noCapability = CreatePrincipal("plugin.no-cap", PluginTrustPermission.UnsignedLocal);

        using (accessor.BeginScope(noCapability))
        {
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task PublishAsync_PublisherHasEventPublishCapabilityForThisEventType_Succeeds()
    {
        var (bus, accessor, _) = CreateBus();
        var withCapability = CreatePrincipal(
            "plugin.a", PluginTrustPermission.UnsignedLocal, PluginCapability.EventPublish(typeof(RecordedEventA).FullName!));
        var handler = new RecordingHandler<RecordedEventA>();
        bus.Subscribe(handler);

        using (accessor.BeginScope(withCapability))
        {
            await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);
        }

        Assert.Single(handler.Received);
    }

    [Fact]
    public async Task PublishAsync_PublisherHasCapabilityForADifferentEventType_ThrowsPermissionDeniedException()
    {
        var (bus, accessor, _) = CreateBus();
        var wrongCapability = CreatePrincipal(
            "plugin.a", PluginTrustPermission.UnsignedLocal, PluginCapability.EventPublish(typeof(RecordedEventB).FullName!));

        using (accessor.BeginScope(wrongCapability))
        {
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task PublishAsync_NullCurrentComponent_SkipsPublisherCheck_ReproducesTodaysBehaviour()
    {
        var (bus, _, _) = CreateBus();
        var handler = new RecordingHandler<RecordedEventA>();
        bus.Subscribe(handler);

        // No scope pushed - first-party publisher, check is skipped entirely.
        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Single(handler.Received);
    }

    [Fact]
    public async Task PublishAsync_NullCurrentComponentAccessor_NeverThrows_ReproducesTodaysBehaviour()
    {
        var bus = new EventBus(); // no accessor, no evaluator at all
        var handler = new RecordingHandler<RecordedEventA>();
        bus.Subscribe(handler);

        var exception = await Record.ExceptionAsync(() => bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None));

        Assert.Null(exception);
    }

    // ------------------------------------------------------------------
    // Subscriber's own captured owner is pushed per-invocation, never the
    // publisher's.
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_SubscriberObservesItsOwnCapturedOwner_AsCurrentComponent_DuringItsOwnHandleAsync()
    {
        var (bus, accessor, _) = CreateBus();
        var subscriberOwner = CreatePrincipal("plugin.subscriber", PluginTrustPermission.UnsignedLocal);

        Tempest.Core.Identity.IPrincipal? observedDuringHandle = null;

        using (accessor.BeginScope(subscriberOwner))
        {
            bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) =>
            {
                observedDuringHandle = accessor.Current;
                return Task.CompletedTask;
            }));
        }

        // Publish from first-party (no scope) - the subscriber must still
        // observe ITS OWN owner, not the publisher's null/first-party state.
        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Same(subscriberOwner, observedDuringHandle);
    }

    [Fact]
    public async Task PublishAsync_SubscriberScope_RevertsAfterHandleAsyncReturns()
    {
        var (bus, accessor, _) = CreateBus();
        var subscriberOwner = CreatePrincipal("plugin.subscriber", PluginTrustPermission.UnsignedLocal);

        using (accessor.BeginScope(subscriberOwner))
        {
            bus.Subscribe(new RecordingHandler<RecordedEventA>());
        }

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        // Back in first-party code after publish completes.
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribersWithDifferentOwners_EachObservesOnlyItsOwnOwner()
    {
        var (bus, accessor, _) = CreateBus();
        var ownerA = CreatePrincipal("plugin.a", PluginTrustPermission.UnsignedLocal);
        var ownerB = CreatePrincipal("plugin.b", PluginTrustPermission.VerifiedSigned);

        Tempest.Core.Identity.IPrincipal? observedByA = null;
        Tempest.Core.Identity.IPrincipal? observedByB = null;

        using (accessor.BeginScope(ownerA))
        {
            bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { observedByA = accessor.Current; return Task.CompletedTask; }));
        }

        using (accessor.BeginScope(ownerB))
        {
            bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { observedByB = accessor.Current; return Task.CompletedTask; }));
        }

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Same(ownerA, observedByA);
        Assert.Same(ownerB, observedByB);
    }

    [Fact]
    public async Task PublishAsync_FirstPartySubscriber_NoScopePushedAroundIt()
    {
        var (bus, accessor, _) = CreateBus();

        Tempest.Core.Identity.IPrincipal? observed = new PlatformPrincipal(new PlatformIdentity("sentinel", "sentinel"), []);

        // Subscribed with no BeginScope active - a genuine first-party subscriber.
        bus.Subscribe(new RecordingHandler<RecordedEventA>((e, ct) => { observed = accessor.Current; return Task.CompletedTask; }));

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Null(observed);
    }

    // ------------------------------------------------------------------
    // Unsubscribe removes exactly one matching subscription
    // ------------------------------------------------------------------

    [Fact]
    public async Task Unsubscribe_SameHandlerInstanceSubscribedTwice_RemovesOnlyOneSubscription()
    {
        var bus = new EventBus();
        var handler = new RecordingHandler<RecordedEventA>();

        bus.Subscribe(handler);
        bus.Subscribe(handler);

        bus.Unsubscribe(handler);

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        // One subscription remains - handler receives exactly once, not zero.
        Assert.Single(handler.Received);
    }

    [Fact]
    public async Task Unsubscribe_SameHandlerInstanceSubscribedTwice_UnsubscribedTwice_RemovesBoth()
    {
        var bus = new EventBus();
        var handler = new RecordingHandler<RecordedEventA>();

        bus.Subscribe(handler);
        bus.Subscribe(handler);

        bus.Unsubscribe(handler);
        bus.Unsubscribe(handler);

        await bus.PublishAsync(new RecordedEventA("x"), CancellationToken.None);

        Assert.Empty(handler.Received);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static (EventBus Bus, CurrentComponentAccessor Accessor, PermissionEvaluator Evaluator) CreateBus()
    {
        var accessor = new CurrentComponentAccessor();
        var evaluator = new PermissionEvaluator();
        var bus = new EventBus(currentComponentAccessor: accessor, permissionEvaluator: evaluator);
        return (bus, accessor, evaluator);
    }

    private static PlatformPrincipal CreatePrincipal(string id, string tierPermissionKey, params string[] additionalPermissionKeys)
    {
        var permissions = new List<Permission> { new(tierPermissionKey) };
        permissions.AddRange(additionalPermissionKeys.Select(key => new Permission(key)));
        return new PlatformPrincipal(new PlatformIdentity(id, id), permissions);
    }
}
