using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Navigation;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.Navigation;

// Proves ADR-0031/ADR-0032 against the real NavigationService implementation
// - imperative registration, deterministic ordering, duplicate/unknown-id
// handling, and Navigate publishing NavigationRequestedEvent through a real
// IEventBus. No module or Runtime Host is exercised here - see
// NavigationModuleIntegrationTests for that end-to-end proof.
public class NavigationServiceTests
{
    // ------------------------------------------------------------------
    // Register / Unregister
    // ------------------------------------------------------------------

    [Fact]
    public void Register_ThenItems_ContainsTheRegisteredItem()
    {
        var service = new NavigationService(new EventBus());
        var item = new NavigationItem("home", "Home");

        service.Register(item);

        Assert.Same(item, Assert.Single(service.Items));
    }

    [Fact]
    public void Register_DuplicateId_ThrowsDuplicateNavigationItemException()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("home", "Home"));

        var exception = Assert.Throws<DuplicateNavigationItemException>(
            () => service.Register(new NavigationItem("home", "Home Again")));

        Assert.Equal("home", exception.Id);
    }

    [Fact]
    public void Register_DuplicateId_DoesNotReplaceTheOriginalItem()
    {
        var service = new NavigationService(new EventBus());
        var original = new NavigationItem("home", "Home");
        service.Register(original);

        Assert.ThrowsAny<DuplicateNavigationItemException>(
            () => service.Register(new NavigationItem("home", "Replacement")));

        Assert.Same(original, Assert.Single(service.Items));
    }

    [Fact]
    public void Register_NullItem_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new NavigationService(new EventBus()).Register(null!));

    [Fact]
    public void Unregister_RegisteredId_RemovesTheItem()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("home", "Home"));

        service.Unregister("home");

        Assert.Empty(service.Items);
    }

    [Fact]
    public void Unregister_UnknownId_IsNoOp()
    {
        var service = new NavigationService(new EventBus());

        var exception = Record.Exception(() => service.Unregister("does-not-exist"));

        Assert.Null(exception);
    }

    [Fact]
    public void Unregister_NullId_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new NavigationService(new EventBus()).Unregister(null!));

    [Fact]
    public void Register_AfterUnregister_SameId_SucceedsAgain()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("home", "Home"));
        service.Unregister("home");

        var exception = Record.Exception(() => service.Register(new NavigationItem("home", "Home v2")));

        Assert.Null(exception);
        Assert.Equal("Home v2", Assert.Single(service.Items).Title);
    }

    [Fact]
    public void RepeatedRegisterUnregisterCycles_LeaveNoResidualState()
    {
        var service = new NavigationService(new EventBus());

        for (var i = 0; i < 5; i++)
        {
            service.Register(new NavigationItem("cycle", $"Cycle {i}"));
            Assert.Single(service.Items);

            service.Unregister("cycle");
            Assert.Empty(service.Items);
        }
    }

    // ------------------------------------------------------------------
    // Hierarchy (ParentId)
    // ------------------------------------------------------------------

    [Fact]
    public void Register_ChildItem_RetainsParentIdLinkToRegisteredParent()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("parent", "Parent"));
        service.Register(new NavigationItem("child", "Child", parentId: "parent"));

        var child = Assert.Single(service.Items, item => item.Id == "child");

        Assert.Equal("parent", child.ParentId);
        Assert.Contains(service.Items, item => item.Id == child.ParentId);
    }

    [Fact]
    public void Register_TopLevelItem_HasNullParentId()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("home", "Home"));

        Assert.Null(Assert.Single(service.Items).ParentId);
    }

    // ------------------------------------------------------------------
    // Ordering: Group (nulls first), then Order, then Id
    // ------------------------------------------------------------------

    [Fact]
    public void Items_UngroupedItems_AreOrderedByOrderThenId()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("charlie", "Charlie", order: 1));
        service.Register(new NavigationItem("alpha", "Alpha", order: 0));
        service.Register(new NavigationItem("bravo", "Bravo", order: 1));

        Assert.Equal(["alpha", "bravo", "charlie"], service.Items.Select(item => item.Id));
    }

    [Fact]
    public void Items_GroupedAndUngroupedItems_UngroupedSortFirst()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("grouped", "Grouped", group: "Admin"));
        service.Register(new NavigationItem("ungrouped", "Ungrouped"));

        Assert.Equal(["ungrouped", "grouped"], service.Items.Select(item => item.Id));
    }

    [Fact]
    public void Items_MultipleGroups_AreOrderedAlphabeticallyByGroup()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("z-item", "Z", group: "Zeta"));
        service.Register(new NavigationItem("a-item", "A", group: "Alpha"));

        Assert.Equal(["a-item", "z-item"], service.Items.Select(item => item.Id));
    }

    [Fact]
    public void Items_RegistrationOrder_DoesNotAffectDeterministicOrdering()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("second", "Second", order: 2));
        service.Register(new NavigationItem("first", "First", order: 1));
        service.Register(new NavigationItem("third", "Third", order: 3));

        Assert.Equal(["first", "second", "third"], service.Items.Select(item => item.Id));
    }

    // ------------------------------------------------------------------
    // Visibility predicate: stored, never evaluated or filtered by the service
    // ------------------------------------------------------------------

    [Fact]
    public void Items_IncludesItemsRegardlessOfIsVisiblePredicateValue()
    {
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("hidden", "Hidden", isVisible: () => false));
        service.Register(new NavigationItem("shown", "Shown", isVisible: () => true));

        Assert.Equal(2, service.Items.Count);
        Assert.Contains(service.Items, item => item.Id == "hidden");
        Assert.Contains(service.Items, item => item.Id == "shown");
    }

    [Fact]
    public void Items_NeverInvokesTheIsVisiblePredicate()
    {
        var invoked = false;
        var service = new NavigationService(new EventBus());
        service.Register(new NavigationItem("home", "Home", isVisible: () => { invoked = true; return true; }));

        _ = service.Items;

        Assert.False(invoked);
    }

    // ------------------------------------------------------------------
    // Navigate: publishes NavigationRequestedEvent via IEventBus
    // ------------------------------------------------------------------

    [Fact]
    public async Task Navigate_RegisteredId_PublishesNavigationRequestedEventWithTheItem()
    {
        var bus = new EventBus();
        var service = new NavigationService(bus);
        var item = new NavigationItem("home", "Home");
        service.Register(item);

        var received = new List<NavigationRequestedEvent>();
        bus.Subscribe(new RecordingHandler<NavigationRequestedEvent>((e, ct) => { received.Add(e); return Task.CompletedTask; }));

        await service.Navigate("home", CancellationToken.None);

        var published = Assert.Single(received);
        Assert.Same(item, published.Item);
    }

    [Fact]
    public async Task Navigate_UnknownId_ThrowsNavigationItemNotFoundException_AndPublishesNothing()
    {
        var bus = new EventBus();
        var service = new NavigationService(bus);
        var received = new List<NavigationRequestedEvent>();
        bus.Subscribe(new RecordingHandler<NavigationRequestedEvent>((e, ct) => { received.Add(e); return Task.CompletedTask; }));

        var exception = await Assert.ThrowsAsync<NavigationItemNotFoundException>(
            () => service.Navigate("does-not-exist", CancellationToken.None));

        Assert.Equal("does-not-exist", exception.Id);
        Assert.Empty(received);
    }

    [Fact]
    public async Task Navigate_NullId_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => new NavigationService(new EventBus()).Navigate(null!));

    [Fact]
    public async Task Navigate_MultipleSubscribers_AllReceiveTheEvent()
    {
        var bus = new EventBus();
        var service = new NavigationService(bus);
        service.Register(new NavigationItem("home", "Home"));

        var first = new List<NavigationRequestedEvent>();
        var second = new List<NavigationRequestedEvent>();
        bus.Subscribe(new RecordingHandler<NavigationRequestedEvent>((e, ct) => { first.Add(e); return Task.CompletedTask; }));
        bus.Subscribe(new RecordingHandler<NavigationRequestedEvent>((e, ct) => { second.Add(e); return Task.CompletedTask; }));

        await service.Navigate("home", CancellationToken.None);

        Assert.Single(first);
        Assert.Single(second);
    }

    [Fact]
    public async Task Navigate_UsesTheCancellationTokenPassedToPublish()
    {
        var bus = new EventBus();
        var service = new NavigationService(bus);
        service.Register(new NavigationItem("home", "Home"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        bus.Subscribe(new RecordingHandler<NavigationRequestedEvent>());

        // EventBus checks cancellation between subscribers, before invoking
        // the (only) subscriber - so a pre-cancelled token propagates
        // uncaught, exactly as EventBusTests proves for PublishAsync itself.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.Navigate("home", cts.Token));
    }

    // ------------------------------------------------------------------
    // Logging (mirrors EventBus's own optional-logger convention)
    // ------------------------------------------------------------------

    [Fact]
    public void Register_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var service = new NavigationService(new EventBus(), logger);

        service.Register(new NavigationItem("home", "Home"));

        Assert.True(logger.HasEntryAt(LogLevel.Information, "'home' registered"));
    }

    [Fact]
    public async Task Navigate_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var service = new NavigationService(new EventBus(), logger);
        service.Register(new NavigationItem("home", "Home"));

        await service.Navigate("home", CancellationToken.None);

        Assert.True(logger.HasEntryAt(LogLevel.Information, "Navigation requested to 'home'"));
    }

    // ------------------------------------------------------------------
    // Platform Service registration (ADR-0032: an ordinary singleton, no
    // Composition Root treatment needed)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesINavigationProviderToNavigationService()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(INavigationProvider));

        Assert.IsType<NavigationService>(resolved);
    }

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesTheSameInstanceEveryTime()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        var provider = new TempestServiceProvider(services);

        var first = provider.GetService(typeof(INavigationProvider));
        var second = provider.GetService(typeof(INavigationProvider));

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ServiceCollection_SingletonRegistration_NavigationServiceResolvesTheSameEventBusInstance()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        var provider = new TempestServiceProvider(services);

        var eventBus = provider.GetService(typeof(IEventBus));
        var navigationProvider = (NavigationService)provider.GetService(typeof(INavigationProvider));

        // Proven indirectly: Navigate publishes through the exact same bus
        // instance the container hands out elsewhere, not a private one.
        navigationProvider.Register(new NavigationItem("home", "Home"));
        var received = new List<NavigationRequestedEvent>();
        ((IEventBus)eventBus).Subscribe(new RecordingHandler<NavigationRequestedEvent>((e, ct) => { received.Add(e); return Task.CompletedTask; }));

        await navigationProvider.Navigate("home", CancellationToken.None);

        Assert.Single(received);
    }
}
