# WP 6.2 — Notification Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.2`'s own implementation found, mirroring `WP6.1`/`WP6.4`/`WP6.5
Future Capability Recommendations.md`'s own format.

## Recommendation 1 — Publish `IPlatformNotification` (or a Type That Extends It), Never the Bare Concrete Type Alone, When a Subscriber Depends on the Interface

**What.** Any future consumer publishing a general-purpose notification
should declare the notification through `IPlatformNotification` before
calling `PublishAsync` (or supply the type argument explicitly), rather
than letting the compiler infer the concrete `PlatformNotification` as
the type argument.

**Why this matters.** `INotificationDispatcher` dispatches by exact
static generic type, mirroring the Event Bus — this Work Package's own
sample consumers got this wrong on first draft, and no notification was
ever observed until a dedicated integration test caught it. Documented
directly on `IPlatformNotification`'s own remarks, but worth restating
here for any future Work Package's own design review.

## Recommendation 2 — A Durable Notification History/Inbox, if a Real Need Emerges

**What.** `Platform Service Contracts.md`'s own Future Extension Points
for Notifications name "a durable notification history/inbox (would
depend on Persistence, if built)" explicitly. If a future UI Shell
needs a notification centre a user can review after the fact, this
would be the natural next step — likely implemented as a new,
consuming type that subscribes to `IPlatformNotification` and records
what it observes through `IPersistenceStore` (`WP 6.4`), rather than
adding persistence to `INotification`/`INotificationDispatcher`
themselves.

**Why not build it now.** No concrete UI Shell requirement exists yet,
and `Platform Service Contracts.md`'s own Persistence Requirements for
this release state "None" explicitly — building it now would be
exactly the speculative capability this Work Package's own instructions
warned against.

## Recommendation 3 — Delivery-Channel Abstractions (Email, Webhook, Push) as First-Party Handler Implementations

**What.** `Platform Service Contracts.md`'s own Future Extension Points
also name "delivery-channel abstractions (email, webhook, push) as
first-party handler implementations rather than each module writing
its own." When a real need for external notification delivery emerges,
the natural shape is an ordinary `INotificationHandler<IPlatformNotification>`
implementation per channel (an `EmailNotificationHandler`, a
`WebhookNotificationHandler`), registered exactly like
`NotificationSampleModule`'s own handler — no change to
`INotificationDispatcher` itself required.

**Why not build it now.** No concrete delivery-channel requirement
exists yet in this release's own approved scope.

## Recommendation 4 — `WP 6.3` (REST API) Should Consider Webhook/Callback Support as a Notifications Integration

**What.** `Platform Service Contracts.md`'s own REST API entry already
names "webhook/callback support (a plausible Notifications
integration)" as a future extension point. When `WP 6.3` begins, it
should consider whether an HTTP client wants to be notified of
platform events via webhook, using `INotificationDispatcher` as the
internal trigger mechanism — a REST-specific `INotificationHandler`
implementation, not a change to Notifications itself.

**Why not build it now.** `WP 6.3` has not begun, and no concrete
webhook requirement exists yet.

## Recommendation 5 — Any Future Consumer Building Delivery-Channel or History Capability Should Reuse `NotificationSeverity`/`Category`, Not Invent a Parallel Classification

**What.** `NotificationSeverity` (`Information`, `Success`, `Warning`,
`Error`) and `IPlatformNotification.Category` are already the
platform's own general-purpose classification for a notification's
importance and subject matter. A future delivery-channel or history
capability should filter or route using these existing properties,
rather than inventing a second, parallel severity or category concept.

**Why this is worth naming.** Not because it is required, but because
this project's own convention (`Reuse Before Invention`) is best served
by naming the reusable classification explicitly here, while its
reasoning is fresh, rather than leaving a future Work Package to
rediscover or reinvent it.

## Not Recommended

- **Adding a native, in-process history/inbox capability now.** No
  named `v0.6.0` Work Package or UI Shell has a concrete requirement for
  one; `Platform Service Contracts.md` itself names this as a plausible
  future requirement, not a current one.
- **Extending `IPersistenceStore` or introducing a second storage
  mechanism for Notifications.** The approved contract's own Persistence
  Requirements state "None" — any future durability need should be
  built as a consuming layer over `IPlatformNotification`, per
  Recommendation 2, not as a change to Notifications' own core shape.
- **Adding permission-gating to `INotificationDispatcher` speculatively.**
  No approved contract or brief instruction names an access-control
  requirement for publishing or subscribing to a notification; adding
  one now, absent a real need, would be exactly the kind of speculative
  capability this Work Package's own instructions warn against.

## Related Documents

`WP6.2 Implementation Report.md`; `WP6.2 Engineering Review Report.md`;
`WP6.2 Platform Impact Assessment.md`; `WP6.2 Lessons Learned.md`;
`WP6.2 Technical Debt Assessment.md`; `ADR-0046`; `docs/releases/v0.6.0/
Platform Service Contracts.md` (Notification Framework's own Future
Extension Points); `docs/releases/v0.6.0/WorkPackages.md` (`WP 6.3`);
`docs/governance/Quality/Technical Debt Register.md` (`AT-08`).
