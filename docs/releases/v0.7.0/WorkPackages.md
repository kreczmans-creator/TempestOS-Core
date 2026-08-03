# TempestOS v0.7.0 — Work Packages

## Status

**Superseded by actual events — retained for historical record, not
deleted, per this project's own "never delete, mark superseded"
convention.** The candidate items below (`C1`–`C4`), sourced from
`WP 6.8`'s own recommendations, were **not** the scope `v0.7.0` actually
pursued. Real Product Approval instead directed the release toward the
**Engineering Foundation** and **Systems Engineering Foundation**
programmes — twelve real Work Packages (`WP 7.0A` through `WP 7.3A`),
none of which is `C1`–`C4` below. See `docs/releases/v0.7.0/
ReleaseNotes.md` and `PROJECT_STATUS.md` for what `v0.7.0` actually
delivered. Of the four candidates named below: `C1` (Runtime↔Diagnostics
namespace reference) and `C3` (`IPermissionEvaluator` retrofit) remain
undecided, carried forward as open Technical Debt/trade-off items
(`TD-04`, `TD-09`/`TD-10`/`TD-11`); `C2` (Governance Register health
check) was reconfirmed, not resolved, as `FCR-0005`, having recurred as
a real finding twice more during this very release (`WP 7.3A`,
`WP 7.4.0`); `C4` was not pursued as scoped. This correction was made by
`WP 7.4.0` (Release Preparation & Product Baseline) — a genuine,
disclosed documentation-consistency finding: this document's own
"Not started, not yet scoped" status text survived unchanged through
all twelve real Work Packages that followed it, never updated by any of
them, since none had this document specifically in its own scope.

### Original Text (Historical, Not Corrected Below This Line)

**Not started. Not yet scoped.** `feature/v0.7.0-engineering-foundation`
was cut from `main` at the `v0.6.0` tag, per this release's own opening
release-activity instruction. Unlike every prior release's own
`WorkPackages.md` (each of which recorded an already-agreed scope
following a dedicated Architecture, Planning, and Contract Review
phase — see `docs/releases/v0.6.0/WorkPackages.md`'s own opening
section for the most recent example of that discipline), this document
does **not** yet record an approved Work Package breakdown. No such
review has been held for `v0.7.0`. Product Approval was granted for the
`v0.6.0` *release* (merge, tag, and next-branch preparation) — not for
any `v0.7.0` implementation scope.

This document instead records the **candidate items** a genuine,
evidence-backed source — `docs/academy/03 Work Packages/
WP6.8-platform-services-integration-review.md` §6 ("Recommendations for
`v0.7.0` and Beyond") — already named as worth considering. None of the
below is authorised for implementation. Each must still pass through
this project's own standing discipline (`FOUNDATION.md` §1: architecture
precedes implementation for anything non-trivial) before any code is
written.

**Update, `WP 7.0A`:** each candidate below now has a permanent
`FCR-NNNN` identifier in `docs/governance/Future Capability Register.md`
— `C1` is `FCR-0006`, `C2` is `FCR-0005`, `C3` is `FCR-0001`, `C4` is
`FCR-0003`/`FCR-0004`. That register, not this document, is now this
project's authoritative source for future capability tracking; this
document's own `C1`–`C4` labels are kept only because `docs/releases/
v0.7.0/WP7.0A Recommended v0.7 Candidate Work Packages.md` already
refers to them by these names.

## Candidate Items (Not Yet Approved)

### C1 — Resolve the `Runtime`↔`Diagnostics` Namespace Reference

`WP 6.8`'s own Architecture Review found one genuine, disclosed, but
open architectural note: `Diagnostics` imports the `HostState` enum from
`Runtime`, a narrow exception to `ADR-0023`'s "dependencies flow
downward only" rule. Two resolutions were named, neither yet decided:
formally document the reference as an accepted `ADR-0023` exception, or
relocate `HostState` to a neutral namespace. This is the release's own
namesake candidate — closing an architectural note is exactly the kind
of "engineering foundation" work this branch name describes.

### C2 — Governance-Register Health Check

`WP 6.8` found `docs/governance/Documentation/Governance Register.md`
had gone unmaintained for nine consecutive Work Packages before its own
closing review caught it — the second time this specific register has
gone stale for several Work Packages running (`WP 5.3` found and fixed
the first instance). `WP 6.8`'s own recommendation: a lightweight,
periodic (not only closing-review-triggered) health check — a script or
convention flagging a register whose `Last Reviewed` Work Package
predates the most recent Work Package to touch its own subject area.

### C3 — Retrofit `IPermissionEvaluator` Enforcement (`TD-09`/`TD-10`/`TD-11`)

Three long-open, `v0.6.0`-accepted debt items — no plugin/first-party
trust isolation, `NavigationService.Unregister` has no ownership check,
and Command/Navigation registration-order squatting — each deferred on
identical reasoning: the enforcement mechanism (`IPermissionEvaluator`,
`ADR-0044`) already exists, but no real third-party plugin or
adversarial multi-tenant scenario exists yet to make retrofitting it
non-speculative. `WP 6.8`'s own disposition: revisit once a concrete
need (a real third-party plugin, in particular) exists — not assumed
here to have arrived.

### C4 — REST API Authentication and TLS (`TD-13`/`TD-14`)

`v0.6.0`'s REST API shipped with no real authentication (mitigated, not
fixed, by binding to loopback only by default, `ADR-0049`) and no TLS
on its Kestrel listener — both explicitly accepted, named future
requirements, not current-release defects, since the approved `v0.6.0`
contract never promised either. `WP 6.8`'s own disposition: design real
authentication and TLS once a concrete deployment scenario beyond a
trusted local network exists — not assumed here to have arrived either.

## What Happens Next

Per this project's own standing discipline, `v0.7.0`'s real scope is
decided by a Product Architecture, Planning, and Contract Review phase
— the same phase every prior release (`v0.5.0`, `v0.6.0`) held before
any Work Package began. That review may adopt some, all, or none of the
candidates above, and may add scope this document does not anticipate.
**No implementation on any candidate above is authorised by this
document.**
