# Building an Event-Driven Module

## What This Document Is

A practical guide for writing a TempestOS module that publishes or
subscribes to events through the Event Bus (`IEventBus`,
`Tempest.Core.Events` — ADR-0020, ADR-0028). It assumes you have already
read *Building a Module* — this document only covers what changes once a
module needs a DI-public platform service, using `Tempest.Samples.
ClockModule` and its companion, `ClockLifecycleObserverModule`
(`WP 4.4E`), as real, working examples throughout.

## Why `ModuleMetadataAttribute` Exists

*Building a Module* documents a real constraint: a normally-discovered
module's sole public constructor must take zero arguments, because
`ReflectionFrameworkDiscoveryService`'s metadata probe calls
`Activator.CreateInstance(type)` — the zero-argument overload —
unconditionally, for every candidate, purely to read `Id`/`Name`/`Version`
before discarding the instance. A constructor requiring `IEventBus` (or
any other service) makes that call throw, uncaught, before your module is
ever registered — a Host-fatal crash, not an isolated module failure.

`ModuleMetadataAttribute` (`Tempest.Core.Modules`, ADR-0027) removes this
specific obstacle: it lets Discovery read your module's identity directly
from the type itself, without constructing it at all. A module carrying
the attribute is never touched by `Activator.CreateInstance` — Discovery
reads three strings off the attribute and moves on. This is what frees
your constructor to require whatever `TempestServiceProvider` can
resolve.

```csharp
[ModuleMetadata("tempest.samples.clock", "System Clock", "1.0.0")]
public sealed class ClockModule : ModuleLifecycleBase
{
    public ClockModule(IEventBus eventBus)
        : base("tempest.samples.clock", "System Clock", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        _eventBus = eventBus;
    }
    // ...
}
```

**The attribute's values and the base constructor's literal values must
agree.** Discovery reads the attribute alone and never cross-checks it
against the eventually-constructed instance — keeping the two in sync is
your own responsibility, structurally the same accepted risk a
`PluginManifest`'s declared version carries relative to a loaded plugin's
own `IModule.Version`.

**Nothing changes for a module that doesn't need this.** Every module
without the attribute — the overwhelming majority, and every example in
*Building a Module* itself — keeps the exact zero-argument-constructor
shape it always has, forever. The attribute is opt-in, for exactly the
modules that need constructor injection and no others.

## Constructor Injection for Discovered Modules

Once your module carries `[ModuleMetadata]`, its constructor is resolved
by `TempestServiceProvider` during Module Initialisation, exactly like any
other DI-registered service — recursively, including any dependency your
own dependencies themselves need. This was never actually broken:
`TempestServiceProvider.Construct` has always supported constructor-
injected dependencies; the only thing standing in the way was Discovery's
own throwaway metadata probe, which the attribute now bypasses entirely.

An unregistered dependency, or any other construction failure, surfaces as
an ordinary, isolated `ModuleState.Failed` — logged, marked, and the batch
continues with every other module — never a Host-fatal crash. This is
itself a small, welcome correction the attribute-based path introduced as
a side effect: a construction failure now behaves the way every other
kind of module failure already does (ADR-0013), instead of escaping that
classification the way an uncaught `Activator.CreateInstance` exception
used to.

## Event Bus Usage

`IEventBus` is a DI-public platform service (ADR-0020) — resolved by
ordinary constructor injection, exactly like `ILogger` or
`IConfigurationProvider`. It is never a Host-owned collaborator: it
carries no authority to register, initialise, start, stop, or dispose
anything, and a module never needs a compile-time or run-time reference to
another module to communicate through it.

**Publishing.** Call `PublishAsync` with your event, typically from
whichever lifecycle method just did something worth reporting:

```csharp
await _eventBus.PublishAsync(
    new ClockModuleLifecycleEvent(Id, Name, ClockModuleLifecycleTransition.Started, StartedAt.Value, _correlationId),
    cancellationToken);
```

Publishing with zero subscribers is a no-op, not an error — you never need
to check whether anyone is listening.

**Subscribing.** Call `Subscribe`, typically during your own
`InitialiseAsync`, passing an object implementing
`IEventHandler<TEvent>` — usually your own module:

```csharp
public override Task InitialiseAsync(CancellationToken cancellationToken)
{
    _eventBus.Subscribe(this);
    return Task.CompletedTask;
}

public Task HandleAsync(ClockModuleLifecycleEvent @event, CancellationToken cancellationToken)
{
    // react to the event
    return Task.CompletedTask;
}
```

**Never reference the publisher's module type.** Your subscriber depends
only on the shared event type (`ClockModuleLifecycleEvent`, an ordinary
`IEvent`-implementing data type) and `IEventBus` itself — never on
`ClockModule`'s own class. This is the entire point of ADR-0020's governing
shape: `Module → IEventBus → Runtime`, never `Module A → Module B`. Two
modules that only share an event type, never each other's assembly
reference, can be developed, tested, and deployed independently.

**A subscriber's own exception is always isolated.** If your
`HandleAsync` throws, the Event Bus catches it, logs it at `Error`, and
moves on to the next subscriber — your handler's own bug never prevents a
sibling subscriber from receiving the same event, and never faults the
Host (ADR-0028). You do not need your own `try`/`catch` around your
handler's own logic purely to protect the platform; the bus already does
that. (You may still want one for your own diagnostic purposes.)

## Lifecycle Event Publication

`ClockModule` publishes a `ClockModuleLifecycleEvent` from each of
`InitialiseAsync`, `StartAsync`, and `StopAsync` — carrying the module's
own `Id`/`Name`, which transition occurred, a timestamp, and a
**correlation identifier** generated once, in the constructor, and reused
across all three events. This lets a subscriber tie the whole
Initialised → Started → Stopped sequence from one module instance back
together, without needing anything beyond the event's own data.

```csharp
public sealed class ClockModuleLifecycleEvent : IEvent
{
    public ClockModuleLifecycleEvent(
        string moduleId, string moduleName,
        ClockModuleLifecycleTransition transition,
        DateTimeOffset timestamp, Guid correlationId)
    { /* ... */ }

    public string ModuleId { get; }
    public string ModuleName { get; }
    public ClockModuleLifecycleTransition Transition { get; }
    public DateTimeOffset Timestamp { get; }
    public Guid CorrelationId { get; }
}
```

An event is plain data — `IEvent` itself is an empty marker interface.
Carry exactly what a subscriber needs to react correctly, and nothing a
subscriber would need to reach back into your own module to get.

## Lessons Learned

**A real, non-obvious interaction, found only by building a genuine second
consumer.** `ModuleLifecycleManager` initialises modules in ascending-Id
order and stops them in descending order — both correct, unchanged
behaviour, in place since `WP 2.3`. `ClockModule`'s own Id
("tempest.samples.clock") sorts before its companion's
("tempest.samples.clock.observer"), so `ClockModule` publishes its own
`Initialised` event and completes *before* the companion's own
`InitialiseAsync` — where it subscribes — even runs. **The companion never
observes that specific event.** It reliably observes `Started` and
`Stopped`, because every module completes Module Initialisation before
Module Start begins for any module, regardless of Id order.

The temptation, on finding this, is to make it go away quietly — rename
the companion so it sorts first, or move `ClockModule`'s first publish to
a later lifecycle method. Neither was done. The real behaviour was tested,
documented, and explained instead (see the WP 4.4E retrospective's own
"Alternatives Considered"), because the finding is genuinely useful to any
future module author pairing a publisher and a subscriber: **if your
subscriber needs to observe every event from construction onward,
subscribing in `InitialiseAsync` is not early enough if the publisher's
own Id sorts first.** Know this before it surprises you, not after.

**Do not unsubscribe reflexively in `StopAsync`.** It looks like the
obvious symmetric counterpart to subscribing in `InitialiseAsync`, but
`StopAllAsync` runs in the *reverse* of Initialise order — a module
initialised after another will stop before it. `ClockLifecycleObserverModule`
deliberately does not unsubscribe at all, avoiding exactly this hazard,
consistent with ADR-0028's own accepted trade-off that a subscriber's
reference is held for the bus's whole remaining lifetime unless the module
author explicitly chooses otherwise.

**Constructor injection into a discovered module is no longer exotic.**
What `WP 4.4A`/`4.4B` proved against small, dedicated test fixtures,
`WP 4.4E` proved against a real, living reference module. If you are
writing a new module that needs a DI-public service, the path is settled:
carry `[ModuleMetadata]`, request what you need, and trust
`TempestServiceProvider` to resolve it exactly as it already resolves
every other registered service.

## Related ADRs

- **ADR-0020** — *The Event Bus Is a DI-Public Platform Service.* Decides
  where `IEventBus` lives and the governing `Module → IEventBus → Runtime`
  shape.
- **ADR-0027** — *A Declarative `ModuleMetadataAttribute` Decouples
  Discovery From Construction.* Decides how a discovered module can
  declare a DI-resolvable constructor at all.
- **ADR-0028** — *Event Bus Dispatch, Subscription, and Failure Model.*
  Decides `Subscribe`/`Unsubscribe`/`PublishAsync`'s exact semantics:
  sequential, subscription-ordered dispatch over a per-call snapshot,
  unconditional per-subscriber failure isolation, no automatic
  unsubscription.

## Related Documents

*Building a Module* (this folder) · `docs/architecture/Module Dependency
Injection Architecture.md` · `docs/architecture/Event Bus Architecture.md`
· `docs/architecture/Sample Module Architecture.md` · WP 4.4A retrospective
(*Dependency Injection for Discovered Modules*) · WP 4.4B retrospective
(*ADR-0027 Implementation*) · WP 4.4 retrospective (*Event Bus
Architecture*) · WP 4.4D retrospective (*Event Bus Implementation*) ·
WP 4.4E retrospective (*Sample Module Event Integration*) ·
`src/Samples/Tempest.Samples/ClockModule.cs` ·
`src/Samples/Tempest.Samples/ClockLifecycleObserverModule.cs`.
