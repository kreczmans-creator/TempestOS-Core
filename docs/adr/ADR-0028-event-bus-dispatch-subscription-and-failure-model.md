# ADR-0028: Event Bus Dispatch, Subscription, and Failure Model

## Status

Accepted — v0.4.0, WP 4.4 (architecture phase), 2026-07-25. Resolves the
mechanics ADR-0020 deliberately left open when it decided `IEventBus`'s
*placement* (DI-public platform service) without deciding how publish,
subscribe, ordering, or failure actually work. Architecture only; no
production code accompanies this decision — see `Event Bus
Architecture.md` for the full design this ADR resolves.

## Context

ADR-0020 decided *where* the Event Bus lives — a DI-public platform
service, resolved by constructor injection like `IConfigurationProvider`/
`ILogger`, never a Host-owned collaborator. It deliberately did not decide
*how* publishing and subscribing work: dispatch ordering, per-subscriber
failure isolation, and re-entrancy were each named, in `WorkPackages.md`'s
own `WP 4.4` Scope, as questions "decided explicitly here, not discovered
as a bug later." With `WP 4.4A`/`WP 4.4B` now resolving the last
prerequisite (a discovered module can constructor-inject a DI-public
service at all), those questions can no longer be deferred — this ADR
answers them.

### What already exists

Exactly two contracts, both from `WP 4.0`, both unchanged by this ADR:

- **`IEvent`** (`Tempest.Core.Events`) — an empty marker interface. A
  concrete event type carries whatever facts its subscribers need as
  ordinary properties.
- **`IEventHandler<TEvent>`** (`Tempest.Core.Events`) — one method,
  `Task HandleAsync(TEvent @event, CancellationToken cancellationToken)`.
  Its own XML documentation already states the isolation requirement this
  ADR formalises: "a handler that throws must not prevent any other
  subscriber... and must not fault the Runtime Host."

**No `IEventBus` exists anywhere in the repository** — confirmed directly,
not assumed: no file, no interface, no implementation. `WP 4.4` is the
first work package to design or build it.

## Decision

### Is the Event Bus a Platform Service?

Not re-decided — ADR-0020 already answered yes, DI-public, and nothing in
this ADR revisits that.

### Subscription model: imperative, not DI-discovered

A module resolves `IEventBus` via ordinary constructor injection and calls
`Subscribe<TEvent>(IEventHandler<TEvent> handler)` explicitly — typically
passing itself, during its own `InitialiseAsync` — rather than the bus
scanning the DI container for every registered `IEventHandler<T>`
implementation and wiring them automatically. `Unsubscribe<TEvent>(...)`
is the symmetric removal.

```csharp
public interface IEventBus
{
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : IEvent;
}
```

Rejected alternative: DI-auto-discovered handlers, requiring
`TempestServiceProvider` to resolve *every* registration for a given
service type (`IEnumerable<IEventHandler<TEvent>>`) — a genuine new
container capability `ADR-0005`'s minimal container has never needed and
does not have. Recorded as RD-0019.

### Dispatch: sequential, awaited, snapshot-based

`PublishAsync` invokes every current subscriber's `HandleAsync`, one at a
time, in **subscription order** (the order `Subscribe` was called — the
one natural, deterministic ordering key a subscriber has, since unlike a
module it carries no `Id`), awaiting each before starting the next —
mirroring `ModuleLifecycleManager.RunBatchAsync`'s own established
sequential-batch shape exactly, not a new concurrency model. Cancellation
is checked between subscribers, never mid-handler-call, the same Atomic
Phase Principle boundary every other batch operation in the platform
already observes; `OperationCanceledException` is never isolated — it
propagates directly to the publisher, exactly as `RunBatchAsync` already
treats cancellation as categorically different from a failure.

Each `PublishAsync` call operates over an independent, point-in-time
snapshot of the current subscriber list for that event type, taken once at
the start of dispatch. This is what makes **re-entrant publishing** (a
handler calling `PublishAsync` again, for the same or a different event
type, from inside its own `HandleAsync`) safe and well-defined without any
deferred-queue machinery: the nested call dispatches over its own
snapshot, synchronously, and returns before control resumes in the outer
loop — ordinary nested method calls, not a new mechanism. Unbounded
recursion (a handler that republishes the same event unconditionally) is
a bug in that handler's own code, exactly as unbounded recursion anywhere
else in the platform would be — not a condition the bus is designed to
detect or guard against. Rejected alternative: deferring a re-entrant
`PublishAsync` call to a queue, processed only after the current dispatch
completes. Recorded as RD-0020.

### Dispatch is by exact event type only

A subscriber to `TEvent` receives only publications of exactly `TEvent` —
no polymorphic dispatch to subscribers of a base type or shared interface.
No current event has, or needs, a type hierarchy; deciding a dispatch
rule for one that does not exist yet would be speculative. Recorded as
RD-0021.

### Failure model: always isolated, no critical opt-in

**A subscriber's exception is caught, logged, and never rethrown to the
publisher or the Host — full stop, unconditionally.** This is stricter
(and simpler) than ADR-0021's Background Service model: there is no
per-subscriber "critical" declaration that escalates a handler's failure
to Host-fatal. A subscriber failure is reasoned about exactly like an
individual module's own lifecycle failure (ADR-0013's isolated half) — a
bug in one piece of application logic reacting to something that
happened, never treated as evidence the platform itself is unsound.
Logged at `LogLevel.Error` (a subscriber throwing is a genuine defect in
that handler's own code, not an expected, benign outcome the way, for
example, an incompatible plugin version is — ADR-0025's own precedent for
distinguishing "expected" from "buggy" outcomes by severity, applied here
to a single-category failure model rather than a multi-category one).

Rejected alternative: a per-subscriber "critical" opt-in mirroring
`ICriticalBackgroundService` (ADR-0021), escalating a declared-critical
handler's failure to Host-fatal. Recorded as RD-0022 — for the same
reason RD-0011 already rejected an analogous opt-in for plugins: no
current subscriber has a demonstrated need to be load-bearing enough that
its own failure should abort the entire platform, and the asymmetry with
Background Services is deliberate, not an oversight (a background
service's whole existence is to *run*, unsupervised, on its own; an event
subscriber is invoked synchronously, by something that already exists and
is already running — a different enough shape that the same opt-in
pattern does not obviously transfer).

### Registration: an ordinary singleton, no Composition Root treatment needed

Unlike `IConfigurationProvider`, `ILoggerFactory`, or
`IPlatformVersionProvider` — each registered via `AddInstance` (ADR-0009)
because each needs something the container cannot produce through
reflection alone (already-merged data, a method call, eager
every-run construction) — `EventBus`'s own constructor needs nothing the
container cannot already provide (at most an optional `ILogger?`, the same
convention every platform service already follows). It is registered the
ordinary way, `services.Singleton<IEventBus, EventBus>()` — a capability
`ServiceCollection` has had since `WP 2.4`, requiring no DI change
whatsoever. Registered during the existing Platform Services Registered
phase (`Host Lifecycle.md`, Phase 6), alongside `AddDiscoveredModules` —
one new line in `TempestHost.cs`'s already-existing block, not a new
phase.

### Thread safety

`EventBus`'s internal subscriber list is guarded by a single lock,
mirroring `RuntimeModuleManager`'s own `_gate` pattern exactly — the same
baseline every stateful platform service in this codebase already
provides, not a response to any concurrent-publisher scenario that exists
today (none does).

### Diagnostics

`EventBus` takes an optional `ILogger?`, the universal convention.
Subscribe/Unsubscribe and publish-started/publish-completed are logged at
`Information`; an isolated subscriber failure is logged at `Error`,
including the event type, the failing handler's own type, and the
captured exception.

### Interaction with plugins and future Background Services

Requires no special-casing in either direction. A plugin-loaded module
(`Tempest.Core.Plugins`, `WP 4.2`) flows through the identical Discovery →
Registration → DI → Lifecycle pipeline as any other module — Module
Discovery does not know or care that a type came from a plugin, and
`IEventBus` does not know or care either; a plugin-sourced module
constructor-injects `IEventBus` exactly like `ClockModule` will. A future
`IHostedService` (`WP 4.5`, not yet built) would, if also DI-resolved,
gain access the same way — `IEventBus`'s DI-public registration does not
discriminate based on what kind of DI-registered component is asking.

### Event Bus vs. Command Framework

Restated here explicitly, per `Risks.md`'s own R3, ahead of `WP 4.7`: an
event has zero or more subscribers and no expected result; a command has
exactly one handler and an expected result. `IEventBus` never depends on,
and is never invoked through, a future command dispatcher, and vice versa
— the two remain orthogonal platform services, exactly as `IEvent`'s own
existing XML documentation and the Engineering Glossary already state.
This ADR does not change that distinction; it only reaffirms it before
`WP 4.7` needs to rely on it.

## Consequences

**Positive:**

- Every dispatch, ordering, and failure question `WP 4.4`'s own Scope
  named as needing an explicit decision now has one, in writing, before
  any implementation.
- Zero new DI capability required — `ServiceCollection.Singleton<TService,
  TImplementation>()` already exists; `EventBus` needs nothing `AddInstance`
  provides that ordinary reflection-based construction does not.
- Re-entrant publishing is safe by construction (independent snapshots per
  call), not by a new queueing mechanism — the simplest correct answer,
  not the most elaborate one.
- Fully consistent with ADR-0013 (isolated failure, extended, not
  reopened), ADR-0017 (Event Bus is not Host-owned and carries no
  orchestration authority — unaffected), ADR-0020 (placement, reaffirmed),
  and ADR-0023 (downward dependency direction — a module depends on
  `IEventBus`; `IEventBus` depends on no module).

**Negative:**

- No automatic unsubscription when a module stops or is disposed —
  `IEventBus` does not know about module lifecycle (by design; see ADR-0017's
  own reasoning against granting orchestration awareness to a DI-public
  service) and does not attempt to. A module that wants to stop receiving
  events must call `Unsubscribe` itself, typically from its own
  `StopAsync`. An accepted, named gap, not an oversight.
- Subscriber references are held strongly, not weakly — a module that
  subscribes and is never explicitly unsubscribed is kept alive by the bus
  for as long as the bus itself lives (the Host's whole run). Acceptable
  today because every current and anticipated subscriber (a module, a
  future hosted service) already lives exactly that long as a DI singleton
  regardless.
- Exact-type-only dispatch (no polymorphism) means a future event
  hierarchy, if one is ever introduced, would need its own explicit
  dispatch-rule decision — not a reason to guess at one now.

## Alternatives Considered

Recorded in full, with reasoning, above and permanently indexed as
RD-0019 (DI-auto-discovered handlers), RD-0020 (deferred/queued
re-entrant publishing), RD-0021 (polymorphic event dispatch), and RD-0022
(a per-subscriber critical opt-in mirroring ADR-0021).

## Future Considerations

If a genuine need arises for automatic unsubscription tied to module
lifecycle, the correct mechanism is a narrow, additive convenience in the
Module SDK (a `ModuleLifecycleBase` override calling `Unsubscribe`
automatically during `StopAsync`, if a module opted in) — not a change to
`IEventBus` itself, which should not need to know what a module is. If a
future event type genuinely needs polymorphic dispatch or a critical-
subscriber escalation, revisit RD-0021/RD-0022 respectively, with a real,
demonstrated need in hand — not speculatively now.
