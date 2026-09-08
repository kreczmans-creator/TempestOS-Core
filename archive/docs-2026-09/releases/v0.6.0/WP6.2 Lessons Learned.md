# WP 6.2 — Notification Framework — Lessons Learned

## 1. "Built on Top of X" Can Mean "Mirrors X's Design" When Literal Delegation Is Impossible

`Required ADRs.md`'s own anticipated decision said `INotificationDispatcher`
would be "built on top of the existing `IEventBus`." Taken literally,
that would mean `NotificationDispatcher` holds and calls into an
`IEventBus` instance — but `Subscribe<TNotification>`'s own approved
constraint (`where TNotification : INotification`) cannot be narrowed
to match `IEventBus.Subscribe<TEvent>`'s own constraint
(`where TEvent : IEvent`) without illegally tightening the interface in
the implementation, or resorting to reflection. **Lesson: when an
anticipated architectural decision's literal wording turns out to be
unachievable within the approved interface shapes, look for what the
decision's own *intent* actually requires — here, "don't reinvent
dispatch machinery" — and satisfy that intent even if the literal
mechanism has to differ.**

## 2. A Brief's Own Deliverable List Does Not Itself Expand an Approved Interface

The brief named "Notification severity" and "Notification categories"
as deliverables, but neither was part of the original,
already-approved `INotification` draft. The correct response — proven
correct three times already this release (`WP 6.1`'s `IRole`, `WP
6.4`'s `SettingDefinition`, `WP 6.5`'s `Detail`-carried correlation id)
— is a new, additive type, never a reshaping of what reviewers already
approved. **Lesson: "the brief asked for it" and "the approved contract
covers it" are different questions; when they diverge, additive
elaboration resolves the gap without reopening what's already signed
off.**

## 3. Exact-Static-Type Dispatch Is a Trap That Repeats Across Independently-Approved Interfaces

`EventBus` dispatches by exact static generic type (`AT-03`), and this
Work Package's own `NotificationDispatcher` mirrors that design
deliberately. What was not anticipated: the *same* trap recurs the
moment a general-purpose concrete type (`PlatformNotification`) and a
more specific subscription-time interface (`IPlatformNotification`)
both exist for the identical dispatcher. A caller publishing the
concrete type and a subscriber subscribing against the interface will
never meet — two different dictionary keys — and nothing in the
compiler flags it, since both are perfectly valid generic type
arguments on their own. **Lesson: any dispatcher using exact-type
keying needs its own calling convention documented explicitly wherever
a general-purpose concrete type sits alongside a more specific
interface — not just documented once, at the dispatcher itself.**

## 4. Unit Tests Using Matching Concrete Types Can Hide a Bug Integration Tests Expose

Every unit test written for `NotificationDispatcher` itself
(`RecordedNotificationA`, `RecordedNotificationB`) subscribed and
published using the *same* concrete type on both sides — so the
exact-type-dispatch mismatch above never had a chance to surface there.
It was the integration test wiring a real subscriber
(`NotificationSampleModule`, subscribing via the interface) against a
real publisher (the sample hosted service and command handler,
publishing via the concrete type) that caught it. **Lesson: a
dispatcher's own unit tests proving its internal mechanics are correct
are not a substitute for an integration test proving two independently-
written consumers can actually talk to each other through it.**

## 5. A Governance Register's Own "Last Reviewed" Line Can Drift Independently of Its Own Content

Three separate documents (`ADR Register.md`'s commit count,
`Namespace Register.md`'s `Tempest.Samples` file count,
`PROJECT_STATUS.md`'s Academy Status article count) were each found to
have accurate *content* additions across several Work Packages, but a
stale summary figure or "Last Reviewed" line that was never bumped to
match. Each individual Work Package correctly added its own row or
paragraph; none re-derived the document's own top-level summary figure
while doing so. **Lesson: adding a new entry to a register is not the
same task as re-deriving that register's own summary/total figure —
both must happen at every touch, or the two silently diverge exactly
the way this Work Package's own repository review found three times
over.**

## Related Documents

`WP6.2 Implementation Report.md`; `WP6.2 Engineering Review Report.md`;
`WP6.2 Platform Impact Assessment.md`; `WP6.2 Technical Debt
Assessment.md`; `WP6.2 Future Capability Recommendations.md`;
`docs/academy/03 Work Packages/WP6.2-notification-framework-
implementation.md`; `ADR-0046`.
