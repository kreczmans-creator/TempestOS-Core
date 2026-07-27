# Event Catalogue

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Event Catalogue |
| **Purpose** | The index of every real (non-test-fixture) `IEvent` implementation published through `IEventBus`, together with its publisher(s) and subscriber(s). |
| **Scope** | Concrete classes implementing `IEvent` under `src/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `src/Samples/Tempest.Samples/ClockModuleLifecycleEvent.cs`; `docs/architecture/Event Bus Architecture.md`. |
| **Review Frequency** | Updated whenever a new production event type is added anywhere under `src/`. |
| **Last Reviewed** | 2026-07-27 (WP 5.0A). |
| **Related Documents** | `docs/architecture/Event Bus Architecture.md`; `Module Register.md`; `Platform Services Register.md`. |
| **Related ADRs** | ADR-0020, ADR-0028. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/04-building-an-event-driven-module.md`; `docs/academy/03 Work Packages/WP4.4E-sample-module-event-integration.md`. |
| **Coverage Status** | Complete. |

---

## Entries

| Event | Namespace | Publisher | Subscriber(s) | Payload | Originating Work Package |
|---|---|---|---|---|---|
| `ClockModuleLifecycleEvent` | `Tempest.Samples` | `ClockModule` (from `InitialiseAsync`/`StartAsync`/`StopAsync`) | `ClockLifecycleObserverModule` | `ModuleId`, `ModuleName`, `Transition` (`ClockModuleLifecycleTransition`: `Initialised`/`Started`/`Stopped`), `Timestamp`, `CorrelationId` | WP 4.4E |

**Total: 1 production event type — Verified directly against
`src/Samples/Tempest.Samples/ClockModuleLifecycleEvent.cs`.**

## A Documented, Tested Non-Delivery Case

`ClockLifecycleObserverModule` does **not** observe `ClockModule`'s own
`Initialised` transition, because `ClockModule`'s Id sorts first in
`ModuleLifecycleManager`'s ascending-order Initialise batch — the observer
has not yet subscribed when that specific event publishes. This is a
tested, documented consequence of Deterministic Systems ordering (WP 2.3),
not a bug — see the WP 4.4E retrospective and
`docs/academy/02 Runtime Architecture/04-building-an-event-driven-module.md`.
Recorded here because it is part of this one event's own real, observed
behaviour, not a hypothetical edge case.

## Note — Navigation (Designed, Not Yet Implemented)

`NavigationRequestedEvent` (`Tempest.Core.Navigation`, designed
`WP 5.0A`, `ADR-0032`) is **deliberately not listed in the Entries table
above** — no such source file exists yet (Verified). Once `WP 5.0B`
implements it, it is added here with `NavigationService` as publisher
and `Tempest.App` (or whatever renders) as subscriber — the same
publisher-knows-nothing-about-subscribers shape `ClockModuleLifecycleEvent`
already proves.

## Cross-Reference Check

`ClockModuleLifecycleEvent` is cited in `Module Register.md` (both
publisher and subscriber), `Platform Services Register.md` (Event Bus's
"first real consumer"), and `Test Register.md`
(`ClockModuleEventIntegrationTests.cs`). No production event type exists
without a corresponding test.
