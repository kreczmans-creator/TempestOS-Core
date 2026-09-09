# WP 6.2 — Notification Framework — Technical Debt Assessment

## Purpose

Updates the architecture-phase `Technical Debt Assessment.md`'s own
predictions against what `WP 6.2` actually shipped, mirroring `WP6.1`/
`WP6.4`/`WP6.5 Technical Debt Assessment.md`'s own format.

## Existing Debt: What Actually Happened

### `R7` (`docs/releases/v0.6.0/Risk Register.md`) — Notifications-vs-Event-Bus boundary blurring during implementation

**Prediction:** the Notifications/Event-Bus distinction could be blurred
during implementation if `ADR-0046` isn't followed precisely.

**What actually happened:** Confirmed resolved — `NotificationDispatcher`
is a distinct type from `EventBus`, mirroring its internal shape
deliberately rather than blurring into or replacing it; `IPlatformNotification`'s
own `Events.IEvent` extension is a disclosed, deliberate, type-level
relationship, not an accidental merging of the two concepts. **Retired**
— this was the last of `R7`'s own three boundary-blurring halves still
open (Settings-vs-Configuration retired at `WP 6.4`; Audit-vs-Logging/
Diagnostics retired at `WP 6.5`, backfilled into the Risk Register here
after this Work Package's own repository review found that update had
never actually been recorded).

### `AT-03` (`docs/governance/Quality/Technical Debt Register.md`) — Exact-event-type-only dispatch, no polymorphic dispatch

**Prediction (Event-Bus-scoped, `WP 4.4D`):** not anticipated to extend
beyond the Event Bus itself.

**What actually happened:** Confirmed to extend identically to
`NotificationDispatcher`, since it deliberately mirrors `EventBus`'s
own dispatch design (`ADR-0046`). **Annotated, not newly created** — the
same trade-off, now known to apply in a second place, with the
practical risk (a publish/subscribe type mismatch) demonstrated
concretely by this Work Package's own sample-consumer defect (see
Lessons Learned).

## New Debt Actually Disclosed by This Work Package

### `AT-08` — No persistent/durable notification model, no history or inbox capability

**Not anticipated as a standalone trade-off by the architecture
phase** — the architecture package's own brief asked whether persistent
notification models should be supported "where defined by the approved
contracts," and `Platform Service Contracts.md`'s own Persistence
Requirements answered "None" directly. Recorded as a disclosed,
accepted trade-off (mirroring `AT-06`'s own "no real plugin yet"
precedent) rather than a defect, since it is an explicit,
contract-conformant scope decision, not an oversight.

### The exact-static-type-dispatch defect in this Work Package's own sample consumers

**Not a Technical Debt Register item** — this was a genuine
*implementation bug* in this Work Package's own new code
(`NotificationSampleHostedService`, `PublishSampleNotificationCommandHandler`),
found and fixed within the same Work Package that introduced it, not a
lingering architectural characteristic left unresolved. It is
disclosed in the retrospective, `ADR-0046`, and `IPlatformNotification`'s
own remarks as calling guidance, rather than tracked as debt, because
the underlying dispatch design itself is not being changed — only the
calling convention around it is now documented.

## A Genuine, Disclosed Process Finding (Not Platform Debt)

This Work Package's own repository review, re-deriving every touched
register directly rather than trusting existing text, found three
further, genuine, pre-existing governance-documentation drifts: `ADR
Register.md`'s own commit count had not been re-derived in some time;
`Namespace Register.md`'s `Tempest.Samples` row had drifted stale at
"14" since `WP 5.2`, three intervening Work Packages having each added
files without the row being updated; and `PROJECT_STATUS.md`'s own
Academy Status section had drifted stale at "77 articles" since before
`WP 6.1`, for the identical reason. These are **governance-documentation**
findings, not platform architecture debt items, and are not registered
in `Technical Debt Register.md` for that reason — they are fully
disclosed in this Work Package's own retrospective, `PROJECT_STATUS.md`,
and the relevant registers themselves instead.

## Summary Table

| Item | Predicted by architecture phase? | Actual outcome |
|---|---|---|
| `R7` — Notifications-vs-Event-Bus boundary blurring | Yes | Confirmed resolved; retired (the last of `R7`'s own three halves) |
| `AT-03` — Exact-event-type-only dispatch | Event-Bus-scoped only | Annotated to extend to Notifications; same trade-off, second confirmed instance |
| `AT-08` — No persistent notification model | Discussed via the brief's own contract-scope qualifier | New, disclosed trade-off, matching the approved contract exactly |
| Exact-static-type-dispatch sample-consumer bug | Not anticipated at all | Found and fixed within this same Work Package; not a debt item |
| Three governance-register drifts (commit count, namespace file count, Academy article count) | Not anticipated at all | Found, fixed, disclosed as governance-documentation findings — not platform debt |

## Related Documents

`docs/releases/v0.6.0/Technical Debt Assessment.md` (the architecture-
phase document this one updates); `docs/releases/v0.6.0/Risk
Register.md` (`R7`); `docs/governance/Quality/Technical Debt
Register.md` (`AT-03`, `AT-07`, `AT-08`); `ADR-0046`; `WP6.2
Implementation Report.md`; `WP6.2 Engineering Review Report.md`; `WP6.2
Platform Impact Assessment.md`; `WP6.2 Lessons Learned.md`.
