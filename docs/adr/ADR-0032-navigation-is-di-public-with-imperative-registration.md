# ADR-0032: Navigation Is a DI-Public Platform Service, Registered Imperatively, Reusing the Event Bus for Its Own Notification

## Status

Accepted — `v0.5.0` "Developer Experience" release, `WP 5.0A` (Navigation
Framework Architecture), 2026-07-27. Depends on `ADR-0031` (Navigation
contracts belong in `Tempest.Core`) already being decided; this ADR
answers the three mechanical questions that decision leaves open:
ownership, registration model, and how a navigation request is
communicated.

## Context

Three, related mechanical questions needed answers before implementation
could begin, each with a real, structurally-different precedent already
in this platform to measure against:

1. **Ownership.** Is `NavigationService` a Host-owned collaborator
   (`ADR-0017`'s pattern — Discovery, Registration, Lifecycle, and, by
   extension, `HostedServiceManager`), or a DI-public platform service
   (`ADR-0020`'s pattern — the Event Bus)?
2. **Registration model.** Does a module *declare* its navigation items
   (an attribute read by reflection, mirroring `ModuleMetadataAttribute`,
   `ADR-0027`), or does it *register* them imperatively at runtime
   (mirroring Event Bus subscription, `ADR-0028`)?
3. **Notification mechanism.** When application logic calls
   `NavigationService.Navigate(id)`, how does whatever is rendering find
   out a navigation was requested — a bespoke Navigation-specific
   publish/subscribe mechanism, or reuse of the already-implemented
   `IEventBus`?

## Decision

**All three questions are answered by reusing an already-proven pattern,
not inventing a new one:**

1. **Ownership: DI-public**, exactly like the Event Bus. Applying
   `ADR-0017`'s own test — does this component carry authority to
   register, initialise, start, stop, or dispose anything in the module
   pipeline — `NavigationService` clearly does not; it holds data and
   raises one notification. It is registered as an ordinary
   container-constructed singleton
   (`services.Singleton<INavigationProvider, NavigationService>()`)
   during the *existing* Platform Services Registered phase (Phase 6),
   introducing no new Host Lifecycle phase and no change to `Runtime
   State Machine.md`.
2. **Registration: imperative**, exactly like Event Bus subscription. A
   module or plugin-loaded module constructor-injects
   `INavigationProvider` and calls `Register(NavigationItem)` from its
   own `InitialiseAsync`/`StartAsync` — the identical shape
   `ClockLifecycleObserverModule` already uses for
   `IEventBus.Subscribe<T>`. No declarative, attribute-based, or
   reflection-driven contribution mechanism is introduced (see
   `RD-0030`).
3. **Notification: reuse `IEventBus`.** `NavigationService` constructor-
   injects `IEventBus` and, when `Navigate(id)` is called, publishes a
   `NavigationRequestedEvent` (an ordinary `IEvent`) — whatever is
   rendering subscribes to it through the existing, unmodified
   `IEventHandler<T>` contract. No second, Navigation-specific
   publish/subscribe mechanism is built (see `RD-0031`).

## Consequences

**Positive:**

- Zero new mechanism is introduced anywhere in this decision — every
  piece reuses a pattern this platform has already built, tested, and
  proven at least once before. This is the third time reflection-based
  discovery was considered and *not* needed (after Modules and Plugins
  both genuinely needed it, and Hosted Services needed a simplified
  form of it) — a real, if quieter, data point that not every new
  platform capability needs a new discovery mechanism just because
  three prior ones did.
- No change to `TempestHost`, `Host Lifecycle.md`'s phase table, or
  `Runtime State Machine.md` — Navigation slots in exactly where the
  Event Bus already proved a DI-public service can, with the identical
  one-line registration shape.
- A module's own `Register` failure (a duplicate `Id`) is already fully
  handled by `ModuleLifecycleManager`'s existing per-module isolation
  (`ADR-0013`), since registration happens *inside* a module's own
  lifecycle method — no new Host-level failure classification is
  required for Navigation at all.
- A platform-service-to-platform-service dependency
  (`NavigationService` → `IEventBus`) has direct precedent
  (`LoggerFactory` → `IConfigurationProvider`) and introduces no cycle,
  confirming `ADR-0023`'s layering is not violated.

**Negative:**

- `NavigationService` now has a mandatory dependency on `IEventBus` —
  a module or test constructing it directly needs a real (or
  appropriately faked) event bus in hand, exactly as any `EventBus`
  consumer already does. This is a disclosed, accepted coupling, not an
  oversight.
- Navigation's own failure story is *inherited* from Module Lifecycle's
  and Event Bus's existing behaviour rather than independently designed
  — a subtle risk if a future contributor assumes Navigation has its own
  bespoke failure classification, when it in fact has none, by design.

## Future Considerations

If a future capability's own registration genuinely cannot avoid the
instantiation-avoidance problem `ModuleMetadataAttribute` was built to
solve (unlike Navigation, which has no such problem — see the Navigation
Framework Architecture document's own "Registration Model" section),
that capability should design its own declarative mechanism on its own
merits, not retrofit one onto Navigation after the fact.
