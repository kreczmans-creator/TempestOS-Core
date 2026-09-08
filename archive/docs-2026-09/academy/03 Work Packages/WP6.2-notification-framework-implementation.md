# WP 6.2 — Notification Framework Implementation

## 1. Introduction

WP 6.2 delivers the Notification Framework — the fourth Work Package of
the Platform Services phase (`v0.6.0`) to ship real code, and the fourth
to be implemented ahead of its own nominal numeric order (`WP 6.0` is
listed first in `WorkPackages.md`), following `Platform Service
Implementation Order.md`'s own recommendation. Implemented in a single
pass, directly against the already-approved architecture and Contract
Review packages — no separate architecture phase, mirroring `WP 6.1`'s,
`WP 6.4`'s, and `WP 6.5`'s own precedent.

## 2. Purpose

To build `Tempest.Core.Notifications` exactly as the approved
architecture specified — `INotification`, `INotificationHandler<TNotification>`,
`INotificationDispatcher` — built on top of the existing Event Bus's own
proven dispatch model rather than a second, parallel publish/subscribe
implementation; to fill the "severity"/"category" gap this Work
Package's own brief named but the original interface draft never gave
members, without modifying any approved interface; and to prove
"Background notifications" concretely, with a real, working hosted
service, not merely a theoretical capability.

## 3. Background

`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), and
`WP 6.5` (Audit Framework) were all already implemented, each ahead of
its own nominal order, per `Platform Service Implementation Order.md`'s
own recommendation. Notifications has no dependency on any of the
three — its own approved contract names Dependency Injection as its
only real dependency, plus the Event Bus's own *design*, not the Event
Bus's own running instance. `Required ADRs.md` had already anticipated
this Work Package's own core decision in outline: `INotification` is
derived from (or raised alongside) an `IEvent`, and
`INotificationDispatcher` is built on top of the existing `IEventBus`,
never a parallel implementation of subscription/dispatch machinery.

## 4. The Problem

Four things needed to exist:

1. **A standard way to publish user-facing and platform-generated
   notifications** — nothing in this codebase today distinguishes "an
   event a module might care about" (the Event Bus) from "something a
   human should be told about" (Information/Success/Warning/Error).
2. **Orthogonality with the Event Bus, not duplication** — a poorly
   scoped Notification Framework could easily become a second,
   redundant dispatch mechanism, exactly the risk `Required ADRs.md`
   flagged in advance.
3. **A genuine C# constraint the anticipated decision did not
   foresee** — `INotificationDispatcher.Subscribe<TNotification>` is
   constrained only by `where TNotification : INotification` (the
   approved, drafted shape), which cannot literally delegate to
   `IEventBus.Subscribe<TEvent>` (constrained by `where TEvent : IEvent`)
   without illegally tightening the interface's own generic constraint
   in the implementation, or resorting to reflection.
4. **"Background notifications"** — the brief named this deliverable
   explicitly; nothing in the approved contracts said what it meant in
   concrete terms.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:**
`INotification` (`OccurredAt`), `INotificationHandler<TNotification>.HandleAsync`,
`INotificationDispatcher` (`Subscribe`/`Unsubscribe`/`PublishAsync`),
`NotificationException` (concrete, single-constructor, base only —
mirroring `AuditException`'s own precedent).

**Dispatch model (`ADR-0046`):** the C# generic-constraint impossibility
above rules out literal delegation from `NotificationDispatcher` to an
internal `IEventBus`, so `NotificationDispatcher` instead mirrors
`EventBus`'s own internal shape exactly — a single, lock-guarded
`Dictionary<Type, List<object>>` keyed by exact notification type, a
per-call immutable snapshot taken under the lock and dispatched outside
it, sequential awaited dispatch in subscription order, cancellation
checked only between subscribers, `OperationCanceledException` never
isolated. This satisfies `Required ADRs.md`'s own "never a parallel
implementation" intent in substance — the dispatch *model* is the Event
Bus's own proven design, reused deliberately, even though the two types
cannot literally share one implementation given their independent
generic constraints.

**Severity/category elaboration (`ADR-0046`):** `IPlatformNotification
: INotification, Events.IEvent` is a new, additive interface — not a
modification to `INotification` itself — adding `Category` (free-form),
`Severity` (`NotificationSeverity`: `Information`, `Success`, `Warning`,
`Error`), and `Message`. Extending `Events.IEvent` concretely realises
`INotification`'s own doc comment ("typically derived from... an
`IEvent`") for this one general-purpose shape. `PlatformNotification` is
the concrete, immutable implementation.

**Logging level, a deliberate departure from the Event Bus:** an
isolated subscriber failure is logged at `LogLevel.Warning`, not
`LogLevel.Error` — `Platform Service Contracts.md`'s own Logging
Requirements state this explicitly. A notification is presentation-
oriented and lower-stakes than a platform event.

**Persistence:** none. `Platform Service Contracts.md`'s own Persistence
Requirements state "None... a notification is not retained after
dispatch" — this resolves the brief's own "support both transient and
persistent notification models where defined by the approved contracts"
instruction to transient-only, mirroring `WP 6.4`'s own precedent for
declining an unapproved deliverable.

**"Background notifications":** `NotificationSampleHostedService` — a
real, working `IHostedService` publishing on `StartAsync`/`StopAsync` —
is the codebase's first genuine, non-infrastructure hosted service.
Disclosed, not overclaimed: `AT-07`'s own revisit trigger names `WP 6.3`
(REST API) as its intended retiree; this Work Package does not claim
that milestone.

**`NotificationSampleModule`** (`Tempest.Samples`, the eleventh
production sample module) subscribes to `IPlatformNotification` during
its own initialisation, registers a command
(`PublishSampleNotificationCommand`) that publishes one on demand, and —
run together with `NotificationSampleHostedService` through the real
Host — observes the hosted service's own "started" notification,
proving "Background notifications" end-to-end, since Module
Initialisation (Phase 8) completes before Hosted Services Started
(Phase 8.1).

## 6. Alternatives Considered

See `ADR-0046` for the complete reasoning. In summary: literally
delegating `NotificationDispatcher` to an internal `IEventBus` instance
was rejected as impossible without illegally tightening a generic
constraint or resorting to reflection; a fully independent Notification
dispatch pipeline was rejected per `Required ADRs.md`'s own anticipated
decision; modifying `INotification` directly to add
`Severity`/`Category`/`Message` was rejected since neither was part of
the approved draft; supporting a persistent notification model this
release was rejected since the approved contract states "None"
explicitly; logging isolated failures at `Error` (matching the Event
Bus) was rejected since the approved contract names `Warning`
explicitly.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so any
future consumer (Reporting, the REST API, Export/Import, Licensing, an
engineering module, a future UI Shell) can depend on
`INotification`/`INotificationHandler<T>`/`INotificationDispatcher` with
full confidence in their shape. Reusing the Event Bus's own proven
dispatch *model* — even without literal code sharing — means
Notifications introduces no second, independently-trusted dispatch
implementation to reason about from scratch. The additive
`IPlatformNotification`/`NotificationSeverity` elaboration gives every
future consumer a ready-to-use, general-purpose notification shape
today, without constraining a future purpose-built notification type's
own design.

## 8. Architectural Principles

- **Reuse the Model, Not Necessarily the Code** — when two approved
  interfaces' own independent generic constraints make literal
  delegation impossible, mirroring the proven internal shape satisfies
  the spirit of "don't reinvent" without requiring illegal or
  reflection-based workarounds.
- **Additive Elaboration Over Approved-Interface Modification** — the
  fourth consecutive Work Package this release to fill a brief-named
  gap (severity, category) with a new type rather than reshape an
  already-approved interface (`IRole`/`IIdentityService`,
  `SettingDefinition`, the `Detail`-carried correlation id, and now
  `IPlatformNotification`).
- **Contract-Defined Scope Boundaries Override Brief-Stated
  Deliverables** — the approved contract's explicit "Persistence
  Requirements: None" resolves the brief's own more general instruction
  to transient-only, exactly as `WP 6.4` declined unapproved settings
  deliverables before it.
- **Disclose a Finding Rather Than Overclaim a Milestone** — the first
  real hosted service is named plainly as exactly that, not as `AT-07`'s
  own retirement, which remains another Work Package's own assignment.

## 9. Files Added

`src/Tempest.Core/Notifications/INotification.cs`;
`src/Tempest.Core/Notifications/INotificationHandler.cs`;
`src/Tempest.Core/Notifications/INotificationDispatcher.cs`;
`src/Tempest.Core/Notifications/NotificationException.cs`;
`src/Tempest.Core/Notifications/NotificationDispatcher.cs`;
`src/Tempest.Core/Notifications/NotificationSeverity.cs`;
`src/Tempest.Core/Notifications/IPlatformNotification.cs`;
`src/Tempest.Core/Notifications/PlatformNotification.cs`;
`src/Samples/Tempest.Samples/NotificationSampleModule.cs`;
`src/Samples/Tempest.Samples/NotificationSampleHostedService.cs`;
`src/Samples/Tempest.Samples/PublishSampleNotificationCommand.cs`;
`src/Samples/Tempest.Samples/PublishSampleNotificationCommandHandler.cs`;
`tests/Tempest.Core.Tests/Notifications/NotificationDispatcherFixtures.cs`;
`tests/Tempest.Core.Tests/Notifications/RecordingLevelLogger.cs`;
`tests/Tempest.Core.Tests/Notifications/NotificationDispatcherTests.cs`;
`tests/Tempest.Core.Tests/Notifications/PlatformNotificationTests.cs`;
`tests/Tempest.Core.Tests/Notifications/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Runtime/NotificationHostRegistrationTests.cs`;
`tests/Tempest.Core.Tests/Samples/NotificationSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0046-notifications-are-derived-from-events-not-a-replacement-pubsub.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Runtime/TempestHost.cs` (registration only);
`tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 10 → 11).

## 10. Trade-offs

- **No notification history or inbox capability exists this release**
  (`AT-08`) — a disclosed, deliberate limitation matching the approved
  contract's own Persistence Requirements, not an oversight.
- **`NotificationDispatcher` is a second, independently-maintained
  implementation of the Event Bus's own dispatch shape** — a future
  change to that shape must be applied to both types by hand, since
  C#'s generic constraints prevent literal code sharing between them.
- **Exact-static-type dispatch is a sharp edge** (`AT-03`, now also
  applying to Notifications) — any future caller unaware of it can
  repeat the exact mistake this Work Package found in its own sample
  code; documented on `IPlatformNotification`'s own remarks, but not
  eliminated as a possibility.
- **This Work Package does not claim `AT-07`'s own retirement** — a
  real hosted service now exists, but that assignment remains `WP 6.3`'s
  own, per its own revisit trigger.

## 11. Common Mistakes

- **Assuming `NotificationDispatcher` could simply call into
  `IEventBus` internally** — it cannot, without illegally tightening a
  generic type constraint or resorting to reflection; the two types'
  own approved constraints are genuinely incompatible for literal
  delegation.
- **Assuming "persistent notification models" was in scope because the
  brief mentioned it** — the approved contract's own Persistence
  Requirements ("None") controls; the brief's own qualifier ("where
  defined by the approved contracts") makes this explicit.
- **A genuine, found-not-invented lesson**: `INotificationDispatcher`
  dispatches by exact static generic type, mirroring the Event Bus. A
  caller that writes `PublishAsync(new PlatformNotification(...))` has
  its type argument inferred as the concrete `PlatformNotification`,
  which never matches a subscriber that subscribed via
  `Subscribe<IPlatformNotification>` — the two are different dictionary
  keys. This affected this Work Package's own first-draft sample
  consumers (`NotificationSampleHostedService`,
  `PublishSampleNotificationCommandHandler`); no notification was ever
  observed by `NotificationSampleModule` until its own integration
  tests caught it. Fixed by declaring/passing the notification as
  `IPlatformNotification` at every publish call site, and documented
  directly on `IPlatformNotification`'s own remarks as calling guidance
  for every future consumer.

## 12. Future Evolution

A notification history/inbox capability (a UI Shell's own notification
centre) if a real need emerges (`AT-08`'s own revisit trigger); real
consumption by Reporting, the REST API, Export/Import, Licensing, and
any engineering module once each of those Work Packages actually
begins; a future UI Shell rendering `Information`/`Success`/`Warning`/
`Error` notifications visually — all named explicitly as future,
separately-scoped responsibilities, not designed now.

## 13. Key Takeaways

1. Two independently-approved generic interface constraints can make
   literal delegation between them impossible in C# — the correct
   response is mirroring the proven design's *shape*, not forcing a
   reflection-based workaround or abandoning the reuse principle
   entirely.
2. A brief's own stated deliverable list ("severity," "category,"
   "persistent notification models") does not itself expand an already-
   approved interface's own shape — the approved contract's own scope
   boundaries (Persistence Requirements: "None") control, and a gap is
   filled additively, never by reshaping what was already approved.
3. Exact-static-type dispatch is a subtle, easy-to-repeat mistake for
   any interface hierarchy where a general-purpose concrete type and a
   more specific interface both exist — an integration test that
   actually wires a subscriber and a publisher together through the
   real dispatcher is what caught it here, not the unit tests, which
   used matching concrete types throughout and never exposed the gap.

## Architectural Debt Assessment

`docs/governance/Quality/Technical Debt Register.md`'s `AT-03` (exact-
event-type-only dispatch) is annotated to record that the identical
limitation now also applies to `NotificationDispatcher`; `AT-07` (zero
real hosted services) is annotated to disclose that a real,
non-infrastructure hosted service now exists, without claiming its
retirement, which remains `WP 6.3`'s own assignment. One new, permanent
trade-off, `AT-08`, records the deliberate absence of a persistent
notification model this release. No tracked Technical Debt item (`TD-01`
through `TD-12`) was touched — Notifications introduces no new instance
of any of those gaps.

## Observations

This Work Package's own integration-test-writing phase found and fixed
a genuine, deterministic bug in its own sample consumers: exact-static-
type dispatch meant `NotificationSampleHostedService` and
`PublishSampleNotificationCommandHandler` published notifications typed
as the concrete `PlatformNotification`, while `NotificationSampleModule`
subscribed against `IPlatformNotification` — no notification was ever
observed until a dedicated integration test asserted on it directly.
This Work Package's own repository review, re-deriving every touched
register directly rather than trusting existing text, also found three
further, genuine, pre-existing drifts unrelated to its own scope: `ADR
Register.md`'s own commit count had not been re-derived in some time;
`Namespace Register.md`'s `Tempest.Samples` row had drifted stale at
"14" since `WP 5.2`, three intervening Work Packages having each added
files without the row being updated; and `PROJECT_STATUS.md`'s own
Academy Status section had drifted stale at "77 articles" since before
`WP 6.1`, for the identical reason. All three are corrected in this
same commit.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0028`;
`ADR-0046`; `docs/architecture/Platform Service Map.md` (Notifications
entry); `docs/governance/Quality/Technical Debt Register.md` (`AT-03`,
`AT-07`, `AT-08`); `docs/academy/03 Work Packages/WP6.1-permissions-and-
identity-implementation.md`, `WP6.4-settings-framework-implementation.md`,
`WP6.5-audit-framework-implementation.md` (the precedents this Work
Package's own single-pass implementation approach follows).
