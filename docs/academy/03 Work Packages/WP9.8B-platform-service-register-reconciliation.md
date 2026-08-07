# WP 9.8B — Platform Service Register Reconciliation

## What This Document Is

Like `WP 7.1F`'s own governance-backfill portion and `WP 6.8`'s own full
register audit, `WP 9.8B` did not design or implement a platform
capability — it reconciled governance documentation against the real,
already-shipped platform. Unlike either precedent, this Work Package's
own scope was narrow and singular by design: five documents, four
frameworks, one disclosed gap, zero code. This document mirrors the
whole-review retrospective format `WP 5.4`/`WP 6.8`/`WP 7.1F`/`WP
7.4.0`/`WP 8.9.0`/`WP 9.9.0` each already established (What Was
Achieved, Architectural Lessons, Implementation Lessons, Repository
Maturity, Recommendations, Key Takeaways) rather than the standard
13-section per-feature template — this Work Package produced no
Alternatives Considered or Trade-offs of its own kind either.

## 1. Introduction

`WP 9.8B` closes the single most persistent disclosed governance finding
in this project's history: the four-Engineering-Foundation-framework
Platform Service Register/Map gap, first found by `WP 7.3A`
(2026-07-30), confirmed still open by `WP 7.4.0` and `WP 8.9.0` (one
release-closing review each), and confirmed open a third consecutive
time by `WP 9.9.0`, whose own Product Approval Report named making a
firm decision about it as `v0.9.0`'s own single most important standing
recommendation. Commissioned directly in response — after `WP 9.9.0`
itself, despite carrying an earlier number (`9.8B`, inside the `WP
9.6A`–`WP 9.8A` range `WP 9.5A`'s own controlling instruction explicitly
skipped) — this Work Package is the first in this project's history
commissioned specifically, and only, to close a standing governance
recommendation.

## 2. What Was Achieved

Five governance documents reviewed directly against source (`Platform
Services Register.md`, `Platform Service Map.md`, `Dependency Injection
Register.md`, `Module Register.md`, `Interface Register.md`); four
Engineering Foundation frameworks (Engineering Data Model, Materials,
Engineering Calculations, Verification) verified end to end. The
disclosed gap confirmed real but narrower than three prior reviews'
own repeated description implied — confined to exactly two of the five
documents; the other three had already been correctly backfilled by
`WP 7.1F`, long before the gap was first named. Four rows added to
`Platform Services Register.md`; four complete Responsibility/Key
types/Dependencies/Consumers/Lifecycle/ADR/Academy-reference sections
added to `Platform Service Map.md`. Two further, previously-undisclosed
findings surfaced and corrected in the same pass: a distinct arithmetic
error in the register's own headline total (independent of the
four-row omission), and two stale "Depended on by" entries on the
Identity & Permissions and Persistence rows, neither of which had ever
been updated to name the Engineering Foundation frameworks that began
consuming them in `v0.7.0`.

**No outstanding Platform Service governance inconsistency remains** —
confirmed by a direct, five-document cross-check performed after the
backfill, not merely asserted.

## 3. Architectural Lessons

**A disclosed gap's own precise scope is worth re-verifying directly
before starting a fix, even when — especially when — it has already
been disclosed identically by three prior, independent reviews.** Each
of `WP 7.4.0`/`WP 8.9.0`/`WP 9.9.0` described "the four-framework gap"
in language that could be read as total governance absence for these
four services. A direct, document-by-document check found three of five
governance documents had never actually drifted — `WP 7.1F`'s own
backfill of `Interface Register.md`/`Dependency Injection Register.md`/
`Module Register.md` (2026-07-30) held completely, un-degraded, across
every one of the seven `v0.9.0` Work Packages that followed it. The
lesson generalises beyond this one gap: a repeated disclosure's own
wording is not guaranteed to be a precise scope statement, only a
faithful one — precision is worth re-deriving, not assumed transitively
from repetition.

**Reconciling a documented gap surfaced a second-order inconsistency no
prior review's own narrower scope could have found.** Two services
*not* named in the original disclosed gap (Identity & Permissions,
Persistence) had stale "Depended on by" text specifically *because* the
four Engineering Foundation frameworks that depend on them had no
governance row of their own to be named from. This is a genuine
architectural lesson about governance documentation specifically:
a missing row's own consequences are not always confined to the row
that is missing.

## 4. Implementation Lessons

**A register's own headline total is a distinct failure mode from a
missing row, and can be wrong even when every individual row is
correct.** The Platform Services Register's own "27 entries" had never
matched its own stated bucket arithmetic (24 + 1 + 1 = 26) — a
pre-existing arithmetic slip, found only because this Work Package's
own edit required recomputing the total directly rather than adding a
delta to the previously-stated figure, applying the identical
discipline `WP 9.3A`/`WP 9.5A` already established for the Technical
Debt Register to a different governance document, by habit rather than
explicit instruction.

**A backfilled documentation section should disclose that it was
backfilled, not merely appear as though it had always been current.**
Every one of the four new Platform Service Map sections carries an
explicit `**Disclosed, WP 9.8B.**` closing note — a small, deliberate
choice that keeps this Work Package's own contribution traceable for a
future reader, mirroring the same "disclose the correction, don't just
make it invisibly" discipline this project applies to every governance
edit.

## 5. Repository Maturity

**The four-Engineering-Foundation-framework Platform Service gap,
disclosed across three consecutive release-closing reviews without
being acted on, is now closed** — the first standing recommendation in
this project's own governance history to be addressed by a dedicated
Work Package created specifically for that purpose, rather than folded
into a broader implementation or certification Work Package's own
incidental scope. This is itself evidence for a broader pattern worth
naming: some disclosed governance findings are better served by a
small, targeted Work Package than by continuing to ask successive,
differently-scoped Work Packages to fit them in around their own
primary work — none of `WP 9.0A`–`WP 9.5A` ever had "backfill the
Platform Service gap" as more than a footnote in their own Platform
Services Register entry, and none was wrong not to fix it, since it was
never their own scope to begin with.

**`FCR-0005` (Governance Register Health-Check Tooling) remains
unbuilt, now with its own strongest evidence yet.** This Work Package's
entire existence — five documents manually cross-checked by hand, a
task a health-check tool could perform in seconds and would have
caught the original gap the moment it first appeared, not three release
cycles later — is a direct, concrete demonstration of the exact
capability `FCR-0005` names.

## 6. Recommendations for the Next Work Package

1. **Build `FCR-0005` (Governance Register Health-Check Tooling)** —
   see Repository Maturity, above; this Work Package is the strongest
   argument yet in its own favour.
2. **When a future Work Package finds a disclosed, multi-review-old
   governance gap, check whether a small, dedicated reconciliation Work
   Package (mirroring this one) would close it faster and more
   completely than folding a partial fix into unrelated scope.**
3. **Continue applying the "recompute totals directly, don't add a
   delta" discipline to every governance register this project
   maintains**, not only the Technical Debt Register it was first
   established for.

## Key Takeaways

1. A disclosed governance gap's own precise scope is worth re-deriving
   directly, even after multiple independent reviews have already
   described it — repetition is not the same as precision.
2. Reconciling a documented gap can surface second-order
   inconsistencies (stale dependent-service text) that no prior,
   narrower-scoped review could have found, because their own root
   cause was the very gap being fixed.
3. A small, dedicated Work Package, commissioned specifically to close
   one standing recommendation, can succeed where three successive,
   differently-scoped release-closing reviews correctly declined to —
   not because any of them failed, but because none of them was ever
   the right scope for it.
4. `FCR-0005`'s own case is now stronger than at any prior point in this
   project's history — this Work Package is direct, first-hand evidence
   of the manual-effort cost automation would eliminate.

## Related Documents

`WP9.8B Reconciliation Report.md`; `WP9.8B Engineering Review.md`;
`WP9.8B Security Review.md`; `WP9.8B Systems Engineering Review.md`;
`WP9.8B Lessons Learned.md`; `docs/academy/03 Work
Packages/WP7.1F-engineering-core-integration-review-and-certification.md`;
`docs/governance/Future Capability Register.md` (`FCR-0005`);
`docs/governance/Engineering/Platform Services Register.md`;
`docs/architecture/Platform Service Map.md`.
