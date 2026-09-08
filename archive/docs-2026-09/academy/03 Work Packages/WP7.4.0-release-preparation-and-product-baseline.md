# WP 7.4.0 — Release Preparation & Product Baseline

## What This Document Is

Like `WP 5.4`, `WP 6.8`, and `WP 7.1F` before it, `WP 7.4.0` did not
design or implement a platform capability — it verified and prepared an
entire release for Product Approval. This document mirrors those three
Work Packages' own whole-review retrospective format (What Was
Achieved, Architectural Lessons, Implementation Lessons, Repository
Maturity, Recommendations, Key Takeaways), not the standard 13-section
per-feature template — that template's own "Alternatives
Considered"/"Trade-offs" sections don't meaningfully apply to a
release-preparation pass either.

## 1. Introduction

`WP 7.4.0` is `v0.7.0`'s own closing activity — the release-preparation
review this project's own standing practice performs before every
tagged release (`WP 5.4` for `v0.5.0`, `WP 6.8` for `v0.6.0`), extended
here to cover a release built from two sequential programmes rather
than one flat Work Package list. Unlike `WP 7.1F` (which certified only
the Engineering Foundation programme, five Work Packages), this Work
Package reviews the complete `v0.7.0` scope: all twelve Work Packages
across both the Engineering Foundation and Systems Engineering
Foundation programmes.

## 2. What Was Achieved

A complete release readiness review across seventeen named areas
(repository health, build, test, documentation, Academy, governance
registers, ADR consistency, Work Package traceability, version
consistency, dependency consistency, module/platform-service/interface/
DI inventories, Technical Debt Register, Future Capability Register,
Known Issues, Release Notes) — five deliverables produced
(`Release Readiness Report`, `Engineering Statistics Report`,
`Architecture Baseline Summary`, `Product Approval Report`, this
retrospective), plus two previously-stale release-document skeletons
fully populated (`ReleaseNotes.md`, `Retrospective.md`) and one
corrected (`WorkPackages.md`'s own status, marked superseded rather than
left silently wrong).

**Recommendation: APPROVED.** Zero release-blocking findings across
build, test, security, or governance. Five genuine documentation/
governance staleness findings identified and corrected; one further
finding disclosed, not fixed, as outside this Work Package's own scope.

## 3. Architectural Lessons

**A release spanning two sequential programmes, each following the
architecture-first discipline independently, is stronger evidence of
that discipline's own value than a single-programme release could
provide.** `v0.6.0` validated the discipline once, across eight parallel
services sharing one architecture package. `v0.7.0` validates it twice,
sequentially, with the second programme (Systems Engineering Foundation)
consuming the first (Engineering Foundation) as a real, proven
dependency rather than a peer — and finding zero rework was needed at
either boundary.

**The reuse-of-existing-mechanism pattern is now a release-level
finding, not merely a framework-level one.** Every one of the nine ADRs
this release produced independently reaches "reuse the Data Model's
existing mechanism" as its own central decision (see `WP7.4.0
Architecture Baseline Summary.md`) — six frameworks, zero new storage
abstractions, one release.

## 4. Implementation Lessons

**Confirming a version-file discrepancy is *not* a discrepancy required
checking git history, not just the current file state.** The root
`VERSION` file reading `0.6.0` during `v0.7.0` development looked, at
first inspection, like a stale oversight. Checking the actual commit
history (`18e61d5`) revealed an established, deliberate precedent:
`VERSION` is bumped to match a new tag only as part of the "prepare next
branch" activity performed immediately after that tag is cut, never
before. This is a useful general lesson for any release-preparation
review: a value that looks wrong in isolation may be exactly right once
its own change history is checked, and the check costs far less than an
incorrect "fix."

**A governance-drift pattern found in a fifth consecutive location
within one release confirms the pattern is structural, not
coincidental.** `Interface Register.md`/`Dependency Injection
Register.md`/`Module Register.md` (found stale by `WP 7.1F`);
`Platform Services Register.md`/`Platform Service Map.md` (found stale
by `WP 7.3A`); `Documentation Register.md`/`Governance Register.md`
(found stale by this Work Package). Three different closing/
release-preparation reviews, three different sets of registers, the
identical root cause each time: no Work Package's own narrowly-scoped
repository review reaches a register outside its own declared subject
area.

## 5. Repository Maturity

**Every independently re-derived count matched its register's own claim
once the five staleness findings were corrected — the governance suite
remains fundamentally sound, not merely lucky.** ADRs, Rejected Designs,
Technical Debt items, Future Capability entries, Academy articles,
interfaces, DI registrations, and production modules were all
re-verified directly against the repository (`grep`, `find`,
`dotnet test`), not assumed from a prior claim — see `WP7.4.0 Release
Readiness Report.md` §7 for the complete cross-check table.

**`FCR-0005` (Governance Register Health-Check Tooling) has now been
recommended by four consecutive release-adjacent reviews without being
built.** `WP 5.4` first identified the pattern; `WP 6.8` confirmed it;
`WP 7.1F` raised its priority to High after a third recurrence; this
Work Package found a fourth and fifth recurrence in the same single
release. The pattern is no longer worth re-discovering manually — it is
worth building the tool `FCR-0005` already describes.

**Disclosing a gap honestly, rather than fixing it beyond scope, is
itself a governance discipline worth naming.** The
`Platform Services Register.md`/`Platform Service Map.md` gap (missing
rows for four Engineering Foundation frameworks) was found, confirmed,
and explicitly left open — backfilling it properly would require
documentation work exceeding what a release-preparation Work Package's
own "no new functionality, no refactoring" constraint permits. Recording
this honestly, with a clear recommendation for a future Work Package, is
more valuable than either silently fixing it (scope creep) or silently
ignoring it (a hidden gap at Product Approval time).

## 6. Recommendations for the Next Work Package

1. **Build `FCR-0005` (Governance Register Health-Check Tooling)** —
   five recurrences across three releases is the strongest possible
   argument for automating the check this Work Package, and three
   before it, each performed by hand.
2. **Backfill `Platform Services Register.md`/`Platform Service Map.md`
   for the four Engineering Foundation frameworks** — disclosed, not
   fixed, by this Work Package; a bounded, well-scoped documentation
   task for a future Work Package.
3. **Decide the next programme** — Programme F (Platform Hardening), a
   further Systems Engineering capability, or the first
   discipline-specific engineering module all remain open, unscheduled
   candidates pending Product Approval.

## Key Takeaways

1. A release-preparation Work Package's own distinct value, confirmed a
   third time (`WP 5.4`, `WP 6.8`, now `WP 7.4.0`), is re-deriving every
   claim directly from the repository — not re-reading what each prior
   Work Package's own retrospective already said about itself.
2. A value that looks stale in isolation may be correct by design — the
   `VERSION` file's own "0.6.0 during v0.7.0 development" reading is
   exactly this project's own established precedent, confirmed by
   checking git history before treating it as a defect.
3. Disclosing a finding honestly and explicitly declining to fix it,
   because fixing it would exceed the current Work Package's own scope,
   is a legitimate and necessary governance discipline — not every
   found gap should be closed by whichever Work Package happens to find
   it.
4. A governance-drift pattern recommended for automation after its
   fourth and fifth recurrence, within a single release, is no longer a
   recommendation worth repeating a sixth time without action.

## Related Documents

`docs/releases/v0.7.0/WP7.4.0 Release Readiness Report.md` and its four
companion deliverables; `docs/releases/v0.7.0/ReleaseNotes.md`;
`docs/releases/v0.7.0/Retrospective.md`; `docs/academy/03 Work
Packages/WP5.4-v0.5.0-release-candidate-and-engineering-sign-off.md`;
`docs/academy/03 Work Packages/
WP6.8-platform-services-integration-review.md`; `docs/academy/03 Work
Packages/WP7.1F-engineering-core-integration-review-and-certification.md`;
`docs/governance/Future Capability Register.md` (`FCR-0005`).
