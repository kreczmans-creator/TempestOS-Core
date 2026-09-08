# WP 6.5 — Audit Framework — Platform Impact Assessment

## Purpose

A dedicated assessment of whether `WP 6.5`'s own implementation
confirms, extends, or exposes a weakness in the platform architecture
established by prior Work Packages — explicitly required by this Work
Package's own brief, distinct from its Implementation Report and
Engineering Review Report.

## Does This Work Package Confirm Earlier Platform Architecture?

**Yes, on four separate points, each independently verified rather than
assumed:**

1. **The Composition Root / ordinary-singleton registration pattern
   (`ADR-0009`) continues to scale cleanly to a fifth and sixth new
   service** (`IAuditRecorder`, `IAuditQuery`) registered in the same
   Phase 6 block as every other DI-public Platform Service since `WP
   4.4`. No new registration mechanism was needed.
2. **The single authorization enforcement point (`ADR-0044`) is
   genuinely reusable by a service its own author did not design.**
   `IPermissionEvaluator.RequirePermission` was written for Identity &
   Permissions' own internal needs, and Audit — a completely separate
   Work Package, implemented independently — was able to depend on it
   directly, with zero modification, to gate `IAuditQuery`. This is the
   first real evidence that `ADR-0044`'s own "single enforcement point"
   design actually generalises beyond its own originating service.
3. **The shared Persistence abstraction (`ADR-0041`) is genuinely
   reusable by a second, independently-implemented consumer.** Audit
   depends on the exact same `IPersistenceStore` contract Settings
   depends on, with zero modification and zero coordination between the
   two Work Packages beyond the approved interface itself. Each owns its
   own collection name (`"Settings"`, `"Audit"`), and collection-scoping
   isolation was proven correct in practice, not merely asserted.
4. **The Event Bus's exact-type dispatch model (`AT-03`) was already
   proven to support an interface-typed event (`ISettingsChangedEvent`,
   `WP 6.4`) — Audit did not need this pattern itself (Audit publishes
   no events), but its own absence is itself confirmatory**: `Platform
   Service Contracts.md`'s own Event Publication Rules for Audit
   ("Publishes no events... a future 'new audit record recorded'
   notification is conceivable but not part of this release's own
   scope") held exactly as stated — no event publication logic was
   built, and none was needed.

## Does This Work Package Extend Earlier Platform Architecture?

**Yes, in one specific, disclosed way:** the internal, shared
`Tempest.Core.Concurrency.AsyncKeyedLock` utility `WP 6.4` introduced
for Persistence and Settings was evaluated for reuse by Audit and
found *not* needed — Audit's own record keys are always unique (a
UTC-ticks-plus-random-component composite), so no two writes ever
target the same key, and the race `AsyncKeyedLock` exists to prevent
cannot occur here. This is not an extension of the utility itself, but
it is a genuine architectural finding: not every Persistence consumer
needs the same concurrency-control mechanism, and assuming otherwise
would have added unnecessary complexity to `AuditRecorder`.

No new namespace, Host Lifecycle phase, or registration mechanism was
introduced.

## Does This Work Package Expose Any Architectural Weakness?

**One, directly related to this Work Package's own explicit mandate:**
`IPersistenceStore`'s lack of a native query capability is now a
*confirmed*, not merely anticipated, limitation — the first real
consumer with a genuine filtering need (`IAuditQuery`) had to implement
that filtering itself, client-side, rather than delegating it to
Persistence. This is disclosed as `TD-12` (a new, permanent Technical
Debt Register item) precisely because it is a real architectural
characteristic worth tracking beyond this one release, not a defect
introduced by this Work Package — `ADR-0041` itself named this as
Persistence's own deliberately minimal scope from the start.

**A second, narrower observation:** this Work Package's own repository
review found a genuine, previously-uncaught bug in two already-committed
Work Packages' own test infrastructure (see `WP6.5 Engineering Review
Report.md` and the retrospective's own Observations). This is not a
weakness in the *platform* architecture itself, but it is a disclosed
weakness in this project's own prior test-writing practice
specifically for tests combining a `using`-scoped resource with a
non-`async` method returning an unawaited `Task` — worth naming here
since it could recur in a future Work Package's own tests if not
watched for.

## Explicit Assessment: `WP 6.4` — Persistence

**Recorded per this Work Package's own explicit instruction.**

- **Sufficiency:** `IPersistenceStore` is **adequate** for Audit's own
  correctness needs. Every approved `IAuditQuery` filter (`ActorId`,
  `Action`, `From`, `To`, individually and combined) is fully and
  correctly satisfiable via `ListKeysAsync` plus a per-key `ReadAsync`,
  proven by `AuditQueryTests`' own filter-correctness suite.
- **No extension was made.** No concrete scale or performance
  requirement exists anywhere in this release's own approved scope to
  justify adding a native query capability now.
- **`docs/releases/v0.6.0/Risk Register.md`'s `R8` — record: remains
  Open, must not be retired.** This is the second confirmation of the
  same anticipated limitation (the first was `WP 6.4`'s own
  implementation shipping the minimal shape as designed); this Work
  Package's own contribution is proving, via real filter-correctness
  tests, that the minimal shape is *functionally* sufficient, while
  confirming its *performance* characteristic (linear scan cost) is
  real, not hypothetical. Both facts are true simultaneously, which is
  exactly why `R8` is neither fully retired (a real limitation exists)
  nor left as a vague, un-actioned "risk" (a concrete, permanent
  Technical Debt item, `TD-12`, now tracks it with a specific revisit
  trigger).
- **Collection-scoping isolation, confirmed in practice.** Audit's own
  `"Audit"` collection and Settings' own `"Settings"` collection never
  collide or interfere — proven directly by `AuditQuery`'s own
  filtering never returning a Settings-originated value and vice versa,
  since each service only ever reads from its own named collection.

## Related Documents

`WP6.5 Implementation Report.md`; `WP6.5 Engineering Review Report.md`;
`WP6.5 Lessons Learned.md`; `WP6.5 Technical Debt Assessment.md`;
`WP6.5 Future Capability Recommendations.md`; `ADR-0041`; `ADR-0044`;
`ADR-0045`; `docs/releases/v0.6.0/Risk Register.md` (`R8`);
`docs/governance/Quality/Technical Debt Register.md` (`TD-12`).
