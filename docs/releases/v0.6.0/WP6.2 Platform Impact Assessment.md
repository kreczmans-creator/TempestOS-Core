# WP 6.2 — Notification Framework — Platform Impact Assessment

## Purpose

A dedicated assessment of whether `WP 6.2`'s own implementation
confirms, extends, or exposes a weakness in the platform architecture
established by prior Work Packages — explicitly required by this Work
Package's own brief, distinct from its Implementation Report and
Engineering Review Report. The brief also explicitly requires assessing
this Work Package's own interactions with Identity, Settings,
Persistence, and Audit, below.

## Does This Work Package Confirm Earlier Platform Architecture?

**Yes, on three separate points, each independently verified rather
than assumed:**

1. **The Composition Root / ordinary-singleton registration pattern
   (`ADR-0009`) continues to scale cleanly to a seventh new service**
   (`INotificationDispatcher`), registered in the same Phase 6 block as
   every other DI-public Platform Service since `WP 4.4`, immediately
   after `IEventBus`. No new registration mechanism was needed.
2. **`Required ADRs.md`'s own anticipated Notifications/Event Bus
   orthogonality holds under real implementation pressure.** The
   anticipated decision — "built on top of the existing `IEventBus`,
   never a parallel implementation of subscription/dispatch machinery"
   — could not be realised *literally* (a genuine C# generic-constraint
   incompatibility rules out direct delegation), but its *intent* was
   fully realised by mirroring `EventBus`'s own proven internal shape.
   This is the first Work Package in this release to find that an
   anticipated architectural decision's literal wording and its
   achievable implementation diverge, while its underlying intent still
   holds — see `ADR-0046` for the complete reasoning.
3. **Additive elaboration over approved-interface modification remains
   the correct pattern for a fourth consecutive Work Package.**
   `IPlatformNotification`/`NotificationSeverity` fills the brief-named
   "severity"/"category" gap exactly the way `WP 6.1`'s
   `IRole`/`IIdentityService`, `WP 6.4`'s `SettingDefinition`, and `WP
   6.5`'s `Detail`-carried correlation id each filled their own
   analogous gap — without modifying `INotification` itself.

## Does This Work Package Extend Earlier Platform Architecture?

**Yes, in one specific, disclosed way:** `Required ADRs.md`'s own
anticipated Notifications decision is now understood more precisely
than it was when written — "built on top of the existing `IEventBus`"
must mean "mirrors its dispatch model," not "literally forwards calls
to it," whenever the two types' own generic constraints are
independently approved and mutually incompatible. This is a genuine
refinement of what "built on top of" means in this codebase's own
governance vocabulary, worth naming explicitly for any future Work
Package that reads `Required ADRs.md`'s own anticipated-decision
language literally.

No new namespace convention, Host Lifecycle phase, or registration
mechanism was introduced.

## Does This Work Package Expose Any Architectural Weakness?

**One, directly related to this Work Package's own implementation:**
exact-static-type dispatch (`AT-03`, previously an Event-Bus-only
characteristic) is now confirmed to apply identically to
`NotificationDispatcher`, and is genuinely easy to get wrong — this
Work Package's own sample consumers
(`NotificationSampleHostedService`, `PublishSampleNotificationCommandHandler`)
got it wrong on first draft, publishing a notification typed as the
concrete `PlatformNotification` while `NotificationSampleModule`
subscribed against the interface `IPlatformNotification` — two
different dictionary keys, so no notification was ever observed. This
is disclosed as a sharpened understanding of `AT-03`'s own real-world
risk (see `Technical Debt Register.md`), not a defect in the dispatch
design itself — `EventBus` carries the identical characteristic and has
for several Work Packages.

**A second, narrower observation:** this Work Package's own repository
review, re-deriving every touched register directly rather than
trusting existing text, found three further, genuine, pre-existing
governance-documentation drifts (see the retrospective's own
Observations, and this document's own explicit interaction assessments
below where relevant). None is a weakness in the *platform*
architecture itself, but each is a disclosed weakness in this project's
own governance-maintenance discipline specifically around re-deriving
figures at every touch point, not only the ones a Work Package's own
brief names.

## Explicit Assessment: Interactions With Identity, Settings, Persistence, and Audit

**Recorded per this Work Package's own explicit instruction.**

- **Identity & Permissions (`WP 6.1`).** **No dependency, either
  direction.** `Tempest.Core.Notifications` does not reference
  `Tempest.Core.Identity` anywhere — confirmed directly by inspecting
  every `using` directive in `src/Tempest.Core/Notifications/`.
  Notifications carries no permission-gating of its own kind: unlike
  `IAuditQuery.QueryAsync` (`WP 6.5`), publishing or subscribing to a
  notification is not access-controlled in this release — anyone
  holding an `INotificationDispatcher` reference may publish or
  subscribe to anything. This is consistent with `Platform Service
  Contracts.md`'s own approved contract, which names no permission
  requirement for Notifications, and is not treated as a gap requiring
  a new Technical Debt item, since no approved contract or brief
  instruction calls for one.
- **Settings (`WP 6.4`).** **No dependency, either direction.**
  `Tempest.Core.Notifications` does not reference
  `Tempest.Core.Settings`, and vice versa — confirmed directly.
  `ISettingsChangedEvent` (`WP 6.4`) is published through the Event
  Bus, not through Notifications, and the two frameworks remain
  orthogonal: a future consumer wanting a user-facing notification when
  a setting changes would need to write its own bridging code (a
  handler subscribing to `ISettingsChangedEvent` that then publishes an
  `IPlatformNotification`) — not something either framework provides
  automatically, and not part of this release's own approved scope.
- **Persistence (`WP 6.4`).** **No dependency, either direction.**
  `Tempest.Core.Notifications` does not reference
  `Tempest.Core.Persistence` — confirmed directly, and expected:
  `Platform Service Contracts.md`'s own Persistence Requirements for
  Notifications state "None... a notification is not retained after
  dispatch," so there was never a reason for this dependency to exist.
  This is the first platform service since Persistence was introduced
  (`WP 6.4`) that has a genuine, approved reason *not* to depend on it
  — worth stating plainly as a confirmation that Persistence's own
  reuse is driven by actual need, not applied reflexively to every new
  service regardless of whether it stores anything durable.
- **Audit (`WP 6.5`).** **No dependency, either direction.**
  `Tempest.Core.Notifications` does not reference `Tempest.Core.Audit`,
  and vice versa — confirmed directly. Notifications does not record
  who published or received a notification anywhere durably; a future
  consumer wanting an audit trail of notification activity ("user X was
  shown warning Y at time Z") would need to build that itself by
  subscribing an `IAuditRecorder`-aware handler to `IPlatformNotification`
  — not something this release's own approved scope requires or this
  Work Package built.

**Summary: Notifications is architecturally orthogonal to all four of
Identity, Settings, Persistence, and Audit** — a clean, disclosed
result. No hidden coupling was found in either direction for any of the
four; the only real coupling this Work Package's own implementation has
is to `Tempest.Core.Events` (a type-level relationship, `IPlatformNotification
: Events.IEvent`) and `Tempest.Core.Logging` (an ordinary, optional
diagnostic dependency every platform service shares).

## Related Documents

`WP6.2 Implementation Report.md`; `WP6.2 Engineering Review Report.md`;
`WP6.2 Lessons Learned.md`; `WP6.2 Technical Debt Assessment.md`; `WP6.2
Future Capability Recommendations.md`; `ADR-0028`; `ADR-0046`;
`docs/releases/v0.6.0/Risk Register.md` (`R7`);
`docs/governance/Quality/Technical Debt Register.md` (`AT-03`, `AT-07`,
`AT-08`).
