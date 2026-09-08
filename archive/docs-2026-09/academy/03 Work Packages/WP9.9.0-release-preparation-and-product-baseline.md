# WP 9.9.0 — Release Preparation & Product Baseline

## What This Document Is

Like `WP 5.4`, `WP 6.8`, `WP 7.4.0`, and `WP 8.9.0` before it, `WP 9.9.0`
did not design or implement a platform capability — it verified and
prepared an entire release for Product Approval. This document mirrors
those four Work Packages' own whole-review retrospective format (What
Was Achieved, Architectural Lessons, Implementation Lessons, Repository
Maturity, Recommendations, Key Takeaways), not the standard 13-section
per-feature template — that template's own "Alternatives
Considered"/"Trade-offs" sections don't meaningfully apply to a
release-preparation pass either. This is also this Work Package's own
formal "Academy Retrospective" deliverable, satisfying its own
controlling instruction's explicit naming of it as distinct from the
"Retrospective" produced at `docs/releases/v0.9.0/Retrospective.md`.

## 1. Introduction

`WP 9.9.0` is `v0.9.0`'s own closing activity — the release-preparation
review this project's own standing practice performs before every
tagged release (`WP 5.4` for `v0.5.0`, `WP 6.8` for `v0.6.0`, `WP 7.4.0`
for `v0.7.0`, `WP 8.9.0` for `v0.8.0`), the fifth such closing review
this project has performed, and the first to cover a release built from
**seven sequential Work Packages, all real Engineering Discipline
implementations** — a structurally simpler shape than `v0.8.0`'s own two
independent tracks, but a release carrying two genuine, disclosed
numbering irregularities (a completion-order gap and a Work-Package-number
skip) neither of `v0.8.0`'s own tracks needed to navigate.

## 2. What Was Achieved

A complete release readiness review across repository, build, test,
version, architecture, Workspace integration, Engineering lifecycle,
Digital Thread, Cockpit, and governance verification, covering all
seven `v0.9.0` Work Packages — eight deliverables produced (`Release
Readiness Report`, `Engineering Statistics Report`, `Architecture
Baseline Summary`, `Engineering Capability Summary`, `Product Approval
Report`, `Release Notes`, `Retrospective`, this Academy Retrospective),
plus two previously-nonexistent release documents created for the first
time this release (`ReleaseNotes.md`, `Retrospective.md` — `v0.9.0`
never had either before this Work Package, unlike `v0.8.0`'s own stale
skeletons `WP 8.9.0` found and populated).

**Recommendation: APPROVED.** Zero release-blocking findings across
build, test, architecture, or governance. Zero new arithmetic-correction
findings this review (unlike `WP 8.9.0`'s own disclosed 39→38 concrete-class
correction) — every headline figure this Work Package independently
re-derived matched the register or prior Work Package that claimed it.
Two governance-completeness findings reconfirmed open, neither newly
found, neither fixed (the four-Engineering-Foundation-framework Platform
Service gap, now a third consecutive release cycle; the "32 vs. 35
governance documents" count drift, one release cycle old). Seven
dedicated Security Reviews performed — a full, disclosed recovery from
`WP 8.9.0`'s own named single most important gap.

## 3. Architectural Lessons

**A release built from seven sequential, structurally-similar Work
Packages is a different verification shape from `v0.8.0`'s own two
independent tracks — and it surfaced a different class of finding.**
Where `WP 8.9.0` needed to verify two tracks shared no hidden
dependency, `WP 9.9.0` needed to verify seven Work Packages' own
disclosed numbering irregularities were each still accurately described
everywhere they appear (`PROJECT_STATUS.md`, each Work Package's own
retrospective, the governance registers) — a consistency-of-narrative
check, not a dependency-graph check. Both held.

**The "reuse what already exists" pattern, proven six times in
`v0.7.0` and confirmed a third time across `v0.8.0`'s own two tracks, is
now confirmed a fourth time — and this release's own closing Work
Package (`WP 9.5A`, Manufacturing) extended the pattern into new
territory: reusing another Work Package's own already-shipped
*instance*, not merely an existing *mechanism*.** This review's own
Architecture Review independently re-verified that extension holds in
real, compiled code — `Tempest.App.Workspace.Manufacturing` genuinely
depends on `.Documents`/`.Verification`, one-directional, confirmed by
direct `grep`, not merely asserted by `WP 9.5A`'s own retrospective.

## 4. Implementation Lessons

**A release-closing review's own value, confirmed a fifth time, extends
to verifying disclosed *narrative* consistency across many documents,
not only numeric claims.** This release carries two genuine, disclosed
irregularities — `WP 9.3A` completing after `WP 9.4A` despite its own
earlier number, and `WP 9.5A`'s own controlling instruction skipping
`WP 9.6A`–`WP 9.8A` — each requiring this review to confirm the same
account appears consistently in `PROJECT_STATUS.md`, each affected Work
Package's own retrospective, and this Work Package's own new deliverables,
rather than merely re-checking a single number in one place.

**Independently re-summing a multi-Work-Package addition chain (test
counts, ADR counts) arithmetically, rather than trusting the final
total alone, is a cheap, high-value check this review performed for the
first time at this scale.** The full `1631 → 1695 → 1738 → 1808 → 1865
→ 1922 → 1972 → 2026` test-count chain (395 total) and the `79 → 82 →
83 → 85 → 87 → 88 → 90 → 91` ADR-count chain (12 total) were each summed
against all seven Work Packages' own individually-stated deltas and
found to match exactly — a stronger check than re-verifying only the
start and end totals, which would not have caught an error that
happened to cancel out across two Work Packages.

**A governance finding first disclosed mid-release (not at a prior
release's own close) can still recur and be reconfirmed at this
release's own closing review, following the identical discipline.** The
"32 vs. 35 governance documents" drift was first found by `WP 9.3A`,
mid-`v0.9.0`, not by a prior release-closing Work Package — this review
treated it identically to a longer-standing finding (the Platform
Service gap), reconfirming rather than re-discovering it, and escalating
it as a standing recommendation on its own merits rather than treating
its shorter history as reason to deprioritise it.

## 5. Repository Maturity

**The four-Engineering-Foundation-framework Platform Service gap has
now survived *three* consecutive release-closing reviews without either
being fixed or being escalated beyond "recommended."** `WP 7.3A` found
it; `WP 7.4.0` and `WP 8.9.0` each confirmed it open; `WP 9.9.0` confirms
it open a third time, after `v0.8.0`'s own Retrospective explicitly
named making a firm decision about it as its own top recommendation.
**That recommendation was not acted on.** This is now the single most
persistent disclosed governance finding across this project's entire
history — named, again, more forcefully, as this release's own top
standing recommendation, in the hope a fourth consecutive review does
not need to repeat it a third time.

**Every count this Work Package independently re-derived from the
repository directly matched the register or prior Work Package that
claimed it — zero disclosed exceptions this review**, a genuine
improvement over `WP 8.9.0`'s own one disclosed 39-vs-38 correction.
ADRs (91), Rejected Designs (45), Technical Debt items (33), Future
Capability entries (62), Academy articles (124), interfaces (168),
production modules (34) — all verified directly. Test suite stability
was re-confirmed across four full-suite runs plus one scoped run plus
one flake-check run, zero flakes, matching every prior release's own
closing-review standard.

**`FCR-0005` (Governance Register Health-Check Tooling) has now been
recommended by a seventh consecutive release-adjacent review without
being built.** Combined with the Platform Service gap's own now-three-cycle
persistence, this release's own experience makes the case, for a second
consecutive time, that a manual periodic sweep — however thorough, and
this review performed twenty verification steps across repository,
build, test, version, architecture, integration, and governance — will
keep finding the identical classes of drift indefinitely without the
tooling itself.

## 6. Recommendations for the Next Work Package

1. **Make a firm decision about the four-Engineering-Foundation-framework
   Platform Service Map/Register gap — this time, actually decide.**
   `WP 8.9.0` already asked for this decision to be made; it was not.
   Three consecutive closing reviews finding the identical, unfixed gap
   is no longer a documentation-currency question deferred with soft
   language — it is a standing process failure.
2. **Reconstruct or formally retire the "32 governance documents"
   figure** before it, too, accumulates a multi-release history.
3. **Build `FCR-0005` (Governance Register Health-Check Tooling)** —
   seven recurrences across five releases is no longer a pattern worth
   re-discovering an eighth time by hand.
4. **Continue the now-fully-restored dedicated-Security-Review
   discipline** into whatever Work Package follows — the one
   `WP 8.9.0`-era recommendation this release actually acted on; do not
   let it lapse a second time the way it lapsed once already.
5. **Build a dedicated Governance & Risk Workspace** (`FCR-0056`) — the
   most concrete, ready-to-start next Engineering Discipline candidate
   this release names, every Domain class it needs already compiled and
   live.

## Key Takeaways

1. A release-preparation Work Package's own distinct value, confirmed a
   fifth time (`WP 5.4`, `WP 6.8`, `WP 7.4.0`, `WP 8.9.0`, now `WP 9.9.0`),
   is re-deriving every claim directly from the repository — this time
   finding zero arithmetic errors, itself evidence the "disclose and
   correct at the Work Package that finds it" discipline is compounding
   correctly across releases.
2. Reuse can generalise from "an existing mechanism" to "another Work
   Package's own already-shipped instance" — a qualitatively new
   dimension this release's own closing Work Package (`WP 9.5A`) added
   to a pattern four releases already proved in a narrower sense.
3. A standing recommendation named explicitly in one release's own
   closing Retrospective (the Platform Service gap, `WP 8.9.0`) is not
   guaranteed to be acted on by the next release, even when named as the
   top recommendation — worth escalating again rather than assuming a
   named recommendation is the same as a resolved one.
4. A documentation gap confirmed open across three consecutive
   release-closing reviews has moved well past "recommended for later"
   — it is now this project's own longest-standing unresolved governance
   question, and deserves a definitive answer before a fourth review
   repeats this same paragraph.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report.md` and its six
companion deliverables; `docs/releases/v0.9.0/ReleaseNotes.md`;
`docs/releases/v0.9.0/Retrospective.md`; `docs/academy/03 Work
Packages/WP5.4-v0.5.0-release-candidate-and-engineering-sign-off.md`;
`docs/academy/03 Work Packages/WP6.8-platform-services-integration-
review.md`; `docs/academy/03 Work
Packages/WP7.4.0-release-preparation-and-product-baseline.md`;
`docs/academy/03 Work Packages/WP8.9.0-release-preparation-and-product-
baseline.md`; `docs/governance/Future Capability Register.md`
(`FCR-0005`).
