# ADR-0046: Notifications Are Derived From Events, Not a Replacement Pub/Sub — Dispatch Model, Severity/Category Elaboration, and Logging Level

## Status

Accepted — `WP 6.2` (Notification Framework), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.2`'s own implementation
phase. `Required ADRs.md` named the core orthogonality question
(Notifications vs. the Event Bus) as this Work Package's own required
ADR, anticipating that `INotificationDispatcher` would be "built on top
of the existing `IEventBus`, never a parallel implementation of
subscription/dispatch machinery." Implementation surfaced a genuine C#
constraint the anticipated decision did not foresee: `INotificationDispatcher.Subscribe<TNotification>`
is constrained only by `where TNotification : INotification` (the
approved, drafted shape in `Public Interface Catalogue.md`) and cannot
literally delegate to `IEventBus.Subscribe<TEvent>` (constrained by
`where TEvent : IEvent`) without illegally tightening the interface's
own generic constraint in its implementation — not permitted in C# —
or resorting to reflection. This Work Package's own brief also named
two deliverables ("Notification severity," "Notification categories")
that `Public Interface Catalogue.md`'s own draft never gave interface
members, and asked whether "persistent notification models" should be
supported, which `Platform Service Contracts.md`'s own Persistence
Requirements dimension answers directly ("None").

## Decision

**`INotification`, `INotificationHandler<TNotification>`, and
`INotificationDispatcher` are implemented exactly as `Public Interface
Catalogue.md` drafted** — zero signature deviation. `NotificationException`
is a concrete, single-constructor base type with no subtype, mirroring
`Audit.AuditException`'s own established precedent (a base type for the
approved contract's own sake, never thrown directly this release).

**`NotificationDispatcher` mirrors `EventBus`'s internal shape exactly,
rather than literally forwarding calls to it.** The C# generic-constraint
impossibility above rules out literal delegation, so `NotificationDispatcher`
reuses the identical internal design instead: a single, lock-guarded
`Dictionary<Type, List<object>>` keyed by exact notification type,
`PublishAsync` taking an immutable snapshot under the lock and
dispatching outside it, sequential awaited dispatch in subscription
order, cancellation checked only between subscribers (never mid-
`HandleAsync`), and `OperationCanceledException` never isolated. This
satisfies `Required ADRs.md`'s own intent — "never a parallel
implementation of subscription/dispatch machinery" — in substance: the
dispatch *model* is the Event Bus's own proven design, reused
deliberately rather than reinvented, even though the two types cannot
literally share one implementation given their independent generic
constraints. `INotificationHandler<T>` is subscribed imperatively at
runtime, exactly mirroring `IEventHandler<T>`'s own proven shape — never
resolved generically through the container (`RD-0040`), as anticipated.

**`IPlatformNotification` is a new, additive interface — `INotification`
itself is not changed.** `Public Interface Catalogue.md`'s own draft
named only the three types above; "Notification severity" and
"Notification categories" were named in this Work Package's own
implementation brief but never drafted as interface members. Mirroring
`WP 6.1`'s own `IRole`/`IIdentityService` and `WP 6.4`'s own
`SettingDefinition` precedent, this gap is filled with a new type rather
than a modification to the already-approved `INotification`:
`IPlatformNotification : INotification, Events.IEvent`, adding
`Category` (free-form, caller-defined grouping), `Severity`
(`NotificationSeverity`: `Information`, `Success`, `Warning`, `Error`),
and `Message`. Extending `Events.IEvent` concretely realises
`INotification`'s own doc comment — "typically derived from... an
`IEvent`" — for this one general-purpose shape; a future, more specific
notification type remains free to implement only `INotification` if it
is instead "raised alongside" a separate event object. `PlatformNotification`
is the concrete, immutable implementation, validating `Category` and
`Message` as non-null/empty/whitespace.

**Notifications are transient only; no persistence is introduced.**
`Platform Service Contracts.md`'s own Persistence Requirements state
explicitly "None... a notification is not retained after dispatch" —
this directly resolves the brief's own "where defined by the approved
contracts" qualifier on supporting "persistent notification models" to
transient-only, mirroring `WP 6.4`'s own precedent for declining
unapproved deliverables. A future history/inbox capability remains
possible without touching `INotification`'s own shape (see this Work
Package's own Future Capability Recommendations).

**An isolated subscriber failure is logged at `LogLevel.Warning`, not
`LogLevel.Error`** — a deliberate, disclosed departure from `EventBus`'s
own convention. `Platform Service Contracts.md`'s own Logging
Requirements state explicitly "Logs a warning for each isolated handler
failure." A notification is presentation-oriented and lower-stakes than
a platform event; a failed notification handler is judged a
warning-level operational concern, not an error-level one — never
rethrown either way, in both cases.

**Publishing must be typed against the interface a subscriber
subscribes to, not the concrete implementation type — a genuine
implementation defect found and fixed while writing this Work Package's
own integration tests.** Because dispatch is by exact static generic
type (the same design `EventBus` already uses), a caller that writes
`PublishAsync(new PlatformNotification(...))` has its type argument
inferred as the concrete `PlatformNotification`, which never matches a
subscriber that subscribed via `Subscribe<IPlatformNotification>` — the
two are different dictionary keys. This was found against this Work
Package's own sample consumers (`NotificationSampleHostedService`,
`PublishSampleNotificationCommandHandler`), both of which originally
published the concrete type while `NotificationSampleModule` subscribed
against the interface, so no notification was ever observed until the
integration tests caught it. Fixed by declaring/passing the notification
as `IPlatformNotification` at every publish call site; documented
directly on `IPlatformNotification`'s own remarks as calling guidance for
every future consumer.

**"Background notifications" is proven with a real, working sample
`IHostedService`** (`NotificationSampleHostedService`), publishing on
`StartAsync`/`StopAsync`. This is the codebase's first genuine,
non-infrastructure `IHostedService` — `docs/governance/Quality/Technical
Debt Register.md`'s own `AT-07` names `WP 6.3` (REST API) as its
intended retiree, and this Work Package does not claim that milestone;
see this Work Package's own Platform Impact Assessment for the full,
disclosed reasoning.

## Consequences

**Positive:**

- Every approved interface is implemented with zero deviation, so any
  future consumer (Reporting, the REST API, Export/Import, Licensing, an
  Engineering Module, a future UI Shell) can depend on
  `INotification`/`INotificationHandler<T>`/`INotificationDispatcher`
  with full confidence in their shape.
- The dispatch model is proven, reused machinery, not a second
  implementation to maintain and independently trust.
- The additive `IPlatformNotification`/`NotificationSeverity` elaboration
  gives every future consumer a ready-to-use, general-purpose
  notification shape today, without constraining a future
  purpose-built notification type's own design.
- The exact-static-type-dispatch calling guidance is now documented
  directly on the type future consumers will read first
  (`IPlatformNotification`), rather than left as a latent trap.

**Negative:**

- `NotificationDispatcher` is a second, independently-maintained
  implementation of the same dispatch *shape* `EventBus` uses — a future
  change to that shape (for example, a different failure-isolation
  policy) must be applied to both types by hand, since C#'s generic
  constraints prevent literal code sharing between them without
  reflection.
- Exact-static-type dispatch is a sharp edge for any future caller who
  is not aware of it — the documentation mitigates this but does not
  eliminate the possibility of a future consumer repeating the same
  mistake this Work Package found in its own sample code.
- No notification history or inbox capability exists this release — a
  disclosed, deliberate limitation, not an oversight.

## Alternatives Considered

**Literally delegating `NotificationDispatcher` to an internal
`IEventBus` instance.** Rejected — not possible without illegally
tightening `Subscribe<TNotification>`'s own generic constraint in the
implementation, or resorting to reflection to bridge
`where TNotification : INotification` to `where TEvent : IEvent`;
reflection-based dispatch was judged an unjustified complexity and
performance cost for no behavioural benefit over mirroring the proven
shape directly.

**A fully independent Notification dispatch pipeline with its own
subscription model**, unrelated to the Event Bus's own design. Rejected
per `Required ADRs.md`'s own anticipated decision — unjustified
duplication of machinery the Event Bus already provides and has already
proven (`ADR-0028`).

**Modifying `INotification` directly to add `Severity`/`Category`/
`Message`.** Rejected — these were never part of the approved
`Public Interface Catalogue.md` draft; changing an already-approved
interface's own shape is the exact pattern this Work Package's own
governance rules out in favour of additive elaboration
(`IPlatformNotification`).

**Supporting a persistent/durable notification model this release.**
Rejected — `Platform Service Contracts.md`'s own Persistence
Requirements state "None" explicitly; building it anyway would be
exactly the kind of unapproved, speculative scope this Work Package's
own instructions warn against.

**Logging isolated subscriber failures at `LogLevel.Error`, matching
`EventBus`.** Rejected — `Platform Service Contracts.md`'s own Logging
Requirements name `Warning` explicitly for this framework; a
notification's own lower stakes relative to a platform event justifies
the distinction.

**Leaving the exact-static-type-dispatch defect undocumented, treating
it as an isolated sample-code bug with no wider relevance.** Rejected —
the same mistake is trivially repeatable by any future consumer that
subscribes against `IPlatformNotification` and later publishes a
concrete notification type without thinking about the type argument;
documenting it on `IPlatformNotification` itself costs nothing and
directly prevents recurrence.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (Notifications' own 15-dimension
contract this ADR implements); `ADR-0028` (Event Bus dispatch/failure
model, the design reused here); `RD-0040` (imperative subscription, not
generic container resolution); `docs/governance/Quality/Technical Debt
Register.md` (`AT-07`); `docs/academy/03 Work
Packages/WP6.2-notification-framework-implementation.md`.
