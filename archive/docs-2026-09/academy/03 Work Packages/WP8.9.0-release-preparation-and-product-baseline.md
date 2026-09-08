# WP 8.9.0 — Release Preparation & Product Baseline

## What This Document Is

Like `WP 5.4`, `WP 6.8`, and `WP 7.4.0` before it, `WP 8.9.0` did not
design or implement a platform capability — it verified and prepared an
entire release for Product Approval. This document mirrors those three
Work Packages' own whole-review retrospective format (What Was
Achieved, Architectural Lessons, Implementation Lessons, Repository
Maturity, Recommendations, Key Takeaways), not the standard 13-section
per-feature template — that template's own "Alternatives
Considered"/"Trade-offs" sections don't meaningfully apply to a
release-preparation pass either.

## 1. Introduction

`WP 8.9.0` is `v0.8.0`'s own closing activity — the release-preparation
review this project's own standing practice performs before every
tagged release (`WP 5.4` for `v0.5.0`, `WP 6.8` for `v0.6.0`, `WP 7.4.0`
for `v0.7.0`), extended here to cover a release built from two
independent tracks (the Engineering Workspace, `WP 8.0A`–`WP 8.1C`; the
Engineering Domain, `WP 8.2A`–`WP 8.2C`) rather than one flat Work
Package list or two sequential programmes. This is the fourth such
closing review this project has performed, and the first with a
numbering scheme (`WP 8.9.0`) breaking from the `X.4.0` pattern
`WP 5.4`/`WP 6.8`/`WP 7.4.0` each used — a naming difference only, the
same role.

## 2. What Was Achieved

A complete release readiness review across repository, build, test,
version, governance, and architecture verification, covering all nine
`v0.8.0` Work Packages — nine deliverables produced (`Release Readiness
Report`, `Engineering Statistics Report`, `Architecture Baseline
Summary`, `Workspace Baseline Summary`, `Engineering Domain Baseline
Summary`, `Product Approval Report`, `Release Checklist`, `Product
Owner Release Checklist`, this retrospective), plus three
previously-stale release-document skeletons fully populated
(`ReleaseNotes.md`, `Retrospective.md`, `WorkPackages.md`'s own status
corrected and marked superseded, not silently left wrong).

**Recommendation: APPROVED.** Zero release-blocking findings across
build, test, or governance. One genuine documentation finding corrected
directly (a 39→38 arithmetic error in `WP 8.2C`'s own summary prose);
two further findings disclosed, not fixed, as outside this Work
Package's own scope (the four-Engineering-Foundation-framework Platform
Service gap, now confirmed open a second consecutive release; `WP8.2B`'s
own `IRelease` inheritance-depth inconsistency); one genuine process gap
weighed explicitly rather than hidden (zero dedicated Security Reviews
this release, against `v0.7.0`'s own three-review standard).

## 3. Architectural Lessons

**A release built from two genuinely independent tracks, rather than
one flat list or two sequential dependent programmes, is a new shape
for this project's own release-closing review to verify — and it held.**
Unlike `v0.7.0` (Systems Engineering Foundation consuming Engineering
Foundation as a real dependency), the Engineering Workspace and the
Engineering Domain share no dependency relationship at all in
`v0.8.0` — verified directly, not assumed: `Tempest.App.Workspace`
carries zero reference to `Tempest.Core.EngineeringDomain`, and neither
track's own architecture decisions constrained the other's. Two
independent architecture-then-contracts-then-implementation cycles,
completed in parallel within one release, is now a proven shape this
project can repeat.

**The identical class of contract-vs-prior-decision tension recurred at
a second stage of the same design sequence, and was resolved
identically both times.** `ADR-0076` (`WP 8.2B`, contract stage) and
`ADR-0077` (`WP 8.2C`, implementation stage) each faced a literal
reading of their own controlling brief that would have silently
undone an already-locked decision one stage earlier (`ADR-0073`, then
`ADR-0072`) — each resolved by distinguishing what the instruction
actually needed from what its most literal reading would produce. This
review's own architecture audit confirms both resolutions still hold in
real, compiled code.

## 4. Implementation Lessons

**A release-closing review's own value now demonstrably includes
re-deriving a number the immediately-preceding Work Package itself just
published, not only numbers from older, settled Work Packages.**
`WP 8.2C`'s own Implementation Report, retrospective, and governing ADR
all claimed 39 concrete canonical object classes; this Work Package's
own direct `grep` against the compiled source found 38. The
discrepancy traces to simple arithmetic, not a functional defect — but
finding it required treating even a same-session, just-completed Work
Package's own claim as something to verify, not something to trust by
proximity.

**Discovering that `src/TempestOS.slnx` — the exact solution file
`scripts/new-release.ps1` builds and tests against — actually exists,
after initially, incorrectly, claiming no solution file existed, is
itself a small but real finding about this Work Package's own process.**
The correction was made and disclosed within this same Work Package,
before any deliverable was finalised, rather than shipped as an error —
but it is a useful, generalisable reminder: "no solution file" is a
claim worth actually checking with `find`, not inferring from "every
project was built individually via `dotnet build` in this session."

**A discipline followed correctly by one Work Package's own controlling
instruction can still contradict a *different*, earlier standing
recommendation without either instruction being wrong.** `v0.7.0`'s own
Retrospective explicitly recommended continuing the three-Security-Review
standard for every future implementation Work Package. None of
`v0.8.0`'s own four implementation Work Packages performed one — not
because any of them was instructed not to, but because none of their
own controlling instructions named it, and the standing recommendation
lived in a document (a prior release's retrospective) none of them was
required to re-read. This is a genuine coordination gap worth naming,
not a violation by any single Work Package.

## 5. Repository Maturity

**The four-Engineering-Foundation-framework Platform Service gap has
now survived two consecutive release-closing reviews without either
being fixed or being escalated beyond "recommended."** `WP 7.3A` found
it; `WP 7.4.0` confirmed it open and deferred it; `WP 8.9.0` confirms it
open a second time. This is a materially different pattern from the
`Interface`/`DI`/`Module` Register drift `WP 6.8`/`WP 7.1F` each found
*and closed* — a gap surviving two full release cycles' worth of
dedicated review is no longer merely "not yet gotten to," and this
Work Package escalates it explicitly rather than deferring it a third
time with the same soft language.

**Every count this Work Package independently re-derived from the
repository directly matched the register that claimed it, with exactly
one disclosed exception** (the 39-vs-38 concrete class count, above).
ADRs (79), Rejected Designs (45), Technical Debt items (25), Future
Capability entries (38), Academy articles (116), interfaces (163), DI
registrations (41 named, 43 raw), production modules (22) — all
verified directly. Test suite stability was re-confirmed across five
full-suite-equivalent runs, zero flakes, matching every prior release's
own closing-review standard.

**`FCR-0005` (Governance Register Health-Check Tooling) has now been
recommended by six consecutive release-adjacent reviews without being
built.** Combined with the Platform Service gap's own new escalation,
this is the strongest evidence yet that a manual, periodic sweep —
however thorough, and this review performed eight separate verification
passes — will keep finding the identical classes of drift indefinitely.

## 6. Recommendations for the Next Work Package

1. **Perform a dedicated Security Review as part of, or immediately
   before, the first implementation Work Package of Programme 9** —
   this release's own single most important disclosed gap, and the
   most actionable of everything this review found.
2. **Make a firm decision about the four-Engineering-Foundation-framework
   Platform Service Map/Register gap** — schedule a dedicated backfill,
   or formally accept it as permanent. Deferring it a third time without
   a decision either way is itself now the wrong outcome.
3. **Build `FCR-0005` (Governance Register Health-Check Tooling)** — six
   recurrences across four releases is no longer a pattern worth
   re-discovering a seventh time by hand.
4. **Build a real Physical/Configuration Engineering Discipline Module**
   against `WP 8.2C`'s own compiled classes — the natural next proof
   that the Engineering Domain's own "consumed by every future
   discipline" claim holds under a real, non-sample consumer.

## Key Takeaways

1. A release-preparation Work Package's own distinct value, confirmed a
   fourth time (`WP 5.4`, `WP 6.8`, `WP 7.4.0`, now `WP 8.9.0`), is
   re-deriving every claim directly from the repository — including a
   number the immediately-preceding, same-session Work Package itself
   just published.
2. Two independent, non-dependent tracks can be verified and released
   together cleanly, provided the review checks — rather than assumes —
   that neither track secretly depends on the other.
3. A standing recommendation buried in a prior release's own
   retrospective (continue dedicated Security Reviews) is easy for a
   subsequent Work Package to miss through no fault of its own — a
   process gap worth naming, not blaming any single Work Package for.
4. A documentation gap confirmed open across two consecutive
   release-closing reviews has stopped being "recommended for later" and
   started being a standing question this project needs to actually
   decide, one way or the other.

## Related Documents

`docs/releases/v0.8.0/WP8.9.0 Release Readiness Report.md` and its
seven companion deliverables; `docs/releases/v0.8.0/ReleaseNotes.md`;
`docs/releases/v0.8.0/Retrospective.md`; `docs/academy/03 Work
Packages/WP5.4-v0.5.0-release-candidate-and-engineering-sign-off.md`;
`docs/academy/03 Work Packages/
WP6.8-platform-services-integration-review.md`; `docs/academy/03 Work
Packages/WP7.4.0-release-preparation-and-product-baseline.md`;
`docs/governance/Future Capability Register.md` (`FCR-0005`).
