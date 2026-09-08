# TempestOS v0.6.0 — Platform Service Implementation Order

## Purpose

Justifies a concrete implementation order for the nine `v0.6.0` Work
Packages, derived directly from the dependency edges established in
`Platform Service Dependency Diagram.md` — no Work Package should begin
implementation before every service it depends on (per that diagram) has
at least completed its own architecture phase, and, for a hard runtime
dependency, ideally completed implementation.

This is a scheduling recommendation for `WP 6.8` and whoever sequences
the release's own implementation phase to follow — it does not itself
change `WorkPackages.md`'s own numbering, and a Work Package's number
(`6.0`–`6.7`) is not the same thing as its implementation order, exactly
as `v0.4.0`'s own final shipped order departed from its original
numbering (`WP 4.6`/`4.7` were rescoped; `WP 4.2D` was inserted) without
anyone renumbering anything.

## Dependency-Derived Ordering Constraints

Reading directly from `Platform Service Dependency Diagram.md`:

1. **Persistence has no proposed-service dependency** — it depends only
   on the existing platform (Dependency Injection). It can begin as soon
   as its own owning Work Package (`WP 6.4`) starts.
2. **Identity & Permissions has no proposed-service dependency** — same
   position as Persistence. It can begin immediately, independent of
   everything else in this release.
3. **Reporting has no *hard* proposed-service dependency** — its
   Command Framework integration is optional/invocation-only (a dotted
   edge in the diagram), so it can begin immediately as well.
4. **Licensing has no proposed-service dependency at all** (its only
   edge is to the existing Host) — it can begin immediately, in parallel
   with anything else, and has no bearing on any other Work Package's
   own scheduling.
5. **Notifications depends on the Event Bus only** (existing platform,
   already implemented) — no proposed-service dependency. It can begin
   immediately.
6. **Settings depends on Persistence** — cannot complete implementation
   before Persistence exists, though its own architecture phase can run
   in parallel with Persistence's, since `WP 6.4` is the Work Package
   that establishes Persistence in the first place (see below).
7. **Audit depends on Persistence and Identity & Permissions** — cannot
   complete implementation before both exist.
8. **Export/Import depends on whatever service owns the data being
   exported** — in practice, its first real integration is with Settings
   (and optionally Reporting), so it is naturally sequenced after at
   least one genuine `IExportable` source exists.
9. **REST API depends on Background Services (existing), the Command
   Framework (existing), and Identity & Permissions (proposed)** — this
   is the release's only *hard-blocked* dependency on a proposed
   service, already stated explicitly in `WorkPackages.md` ("`WP 6.3`
   explicitly blocked on `WP 6.1` landing first").

## Recommended Order

| Order | Work Package | Rationale |
|---|---|---|
| 1 (tied) | `WP 6.1` — Permissions & Identity | No proposed-service dependency; the single most depended-on new service in the release (`Platform Service Dependency Diagram.md`); highest risk (`Risk Register.md` `R1`) and therefore benefits most from starting early, giving its own likely architecture/implementation split (mirroring `WP 5.0A`/`WP 5.0B`) the most schedule room. |
| 1 (tied) | `WP 6.6` — Licensing | No proposed-service dependency of any kind; fully independent leaf; can proceed in parallel with everything else without coordination overhead. |
| 2 | `WP 6.4` — Settings (establishes Persistence) | No proposed-service dependency for Persistence itself; Settings' own Event Bus dependency is already-implemented platform. Placed after `WP 6.1` only for risk-management reasons (see "Why Identity First," below), not because of a hard dependency edge. |
| 3 (tied) | `WP 6.0` — Reporting | No hard proposed-service dependency; can proceed once the team has bandwidth, independent of `6.1`/`6.4`/`6.6`. |
| 3 (tied) | `WP 6.2` — Notifications | No proposed-service dependency beyond the existing Event Bus; can proceed independent of `6.1`/`6.4`/`6.6`. |
| 4 | `WP 6.5` — Audit | Hard dependency on both Persistence (`WP 6.4`) and Identity & Permissions (`WP 6.1`) — cannot complete before both. |
| 5 | `WP 6.3` — REST API | Hard dependency on Identity & Permissions (`WP 6.1`), and benefits from Reporting/Notifications/Settings already existing as plausible things to expose over HTTP, though it is not strictly blocked on any of those three. Explicitly the last service-level Work Package to start, per `WorkPackages.md`'s own stated block. |
| 6 | `WP 6.7` — Export/Import | Benefits from at least one real `IExportable` source existing (Settings, from `WP 6.4`; optionally Reporting, from `WP 6.0`) to integrate against meaningfully — not a hard dependency in the dependency-graph sense, but a practical one for building something testable rather than speculative. |
| 7 | `WP 6.8` — Platform Services Integration Review | By definition last — reviews the other eight once substantially delivered, mirroring `WP 4.2D`/`WP 5.0S`/`WP 5.4`'s own closing-review precedent. |

## Why Identity First

Unlike Persistence, Notifications, Reporting, or Licensing — each also
free of a hard proposed-service dependency — Identity & Permissions is
placed first deliberately, not merely because the dependency graph
permits it. Three reasons converge:

1. It is the single most depended-on new service (`Audit` and the
   `REST API` both require it) — starting it last among the
   "no-dependency" group would simply delay everything downstream of it
   to the end of the release, compounding risk rather than retiring it
   early.
2. It carries the highest risk rating in the release (`Risk Register.md`
   `R1`) and is explicitly anticipated to need its own dedicated
   architecture-then-implementation split (mirroring `WP 5.0A`/`WP
   5.0B`) — starting early gives that split room to actually happen
   without compressing the rest of the release's own schedule.
3. It is the intended vehicle for resolving `TD-09`/`TD-10`/`TD-11`
   (`Technical Debt Assessment.md`) — the longer it is deferred, the
   longer those three items remain open for a third consecutive release.

## Why Settings (and Persistence) Second, Not Tied for First

Persistence has no dependency of its own, but placing `WP 6.4` in the
same first wave as `WP 6.1`/`WP 6.6` would mean three Work Packages
starting simultaneously with no completed precedent for how this
release's own governance discipline holds up under genuine parallelism
(`Risk Register.md` `R6`). Sequencing `WP 6.4` to begin once `WP 6.1` is
underway (not necessarily finished) lets the team apply anything learned
from Identity & Permissions' own early architecture-phase experience
(e.g., how `ADR-0044`'s enforcement-point pattern actually reads in
practice) before Settings/Persistence's own `ADR-0041`/`ADR-0042` are
finalized — a soft, risk-management-driven ordering choice, not a hard
dependency-graph requirement.

## Parallelization Opportunities

Given adequate team capacity, the following genuinely have no
dependency relationship to each other and may proceed fully in
parallel: `WP 6.0` (Reporting), `WP 6.2` (Notifications), and `WP 6.6`
(Licensing) — none appears anywhere in another's dependency chain
(`Platform Service Dependency Diagram.md`). `WP 6.1` and `WP 6.6` may
also run in parallel with each other from the very start of the
release.

## What This Order Does Not Determine

This order governs *implementation* sequencing only. Every Work
Package's own architecture phase, where one is warranted (see
`WorkPackages.md`'s own per-WP notes), may begin in any order —
architecture work produces no runtime dependency of the kind this
document is concerned with. `WP 6.8`'s own review is the only Work
Package genuinely fixed in absolute position (last), since its entire
purpose is reviewing what the other eight actually shipped.

## Related Documents

`Platform Service Dependency Diagram.md`; `Release Architecture.md`;
`Risk Register.md`; `Technical Debt Assessment.md`;
`docs/releases/v0.6.0/WorkPackages.md`; `Service Registration Matrix.md`.
