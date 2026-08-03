# TempestOS v0.7.0 — "Engineering Foundation"

**Status:** Prepared for Product Approval (`WP 7.4.0`, Release
Preparation & Product Baseline). Not yet tagged, merged, or released —
the physical Git tag and GitHub Release are created by the Product
Owner after approval, per this Work Package's own explicit constraint.
**Branch:** `feature/v0.7.0-engineering-foundation` (not yet merged to
`main`).

---

## Overview

TempestOS v0.7.0 delivers the **Engineering Foundation** — five
discipline-neutral engineering frameworks (Engineering Data Model,
Units & Quantities, Materials, Calculations, Verification) that give
every future engineering discipline module a shared, proven foundation
to build on — and, going beyond its own original working name, the
first real capability of the **Systems Engineering Foundation** that
follows it: a complete Requirements Engine, consuming that Engineering
Core directly.

Unlike `v0.6.0`, which built eight independent platform services against
one shared architecture/contract package, `v0.7.0` is structured as two
sequential, evidence-driven programme phases, each following this
project's own standing architecture-first discipline in full:

1. **Engineering Foundation** (`WP 7.0A`–`WP 7.1F`) — vision, planning,
   contracts, five framework implementations, and a closing
   certification review.
2. **Systems Engineering Foundation** (`WP 7.2A`–`WP 7.3A`) — a
   from-repository-evidence strategic programme selection, architecture,
   contract review, and the first real implementation (the Requirements
   Engine).

Twelve Work Packages, zero architectural rework across either phase
boundary — every architecture and contract package this release
approved was implemented exactly as approved, with every genuine
implementation-phase finding disclosed via an ADR rather than silently
absorbed.

## Highlights

- **Five Engineering Foundation frameworks, zero new storage mechanism
  between them.** `Tempest.Core.EngineeringData`, `.UnitsAndQuantities`,
  `.Materials`, `.Calculations`, and `.Verification` each build directly
  on `IEngineeringDocumentStore` (itself built directly on the existing
  `IPersistenceStore`) — no framework introduced a second, parallel
  storage or query mechanism of its own.
- **The Requirements Engine — the first Systems Engineering Foundation
  capability — proves the same pattern a sixth time**, and additionally
  proves `WP7.2B Digital Thread Architecture.md`'s own central claim in
  running code: a digital thread requires no dedicated traversal
  mechanism, only composed reads (`GetEvidenceAsync`).
- **A rigorous, evidence-based strategic programme selection.**
  `WP 7.2A` evaluated seven candidate programmes against eleven criteria
  using repository evidence exclusively, scoring Requirements &
  Verification 46/55 — the highest of all seven, and the only one with
  both a completed technical foundation and a named platform-level ADR
  hook.
- **Two full closing certification reviews, both independent, both
  evidence-based, neither a rubber stamp.** `WP 7.1F` certified the
  entire Engineering Core (`ENGINEERING CORE CERTIFIED WITH ACCEPTED
  TECHNICAL DEBT`); `WP 7.4.0` (this release-preparation review) then
  independently re-verified the complete `v0.7.0` repository state
  end-to-end for Product Approval.
- **1406 tests, up from 1016 at v0.6.0** — zero regressions at any Work
  Package boundary, re-verified across four full-suite runs (two Debug,
  two Release, both from a clean rebuild) during this release's own
  final readiness check.
- **Three dedicated Security Reviews** (`WP 7.1D`, `WP 7.1E`, `WP 7.3A`)
  — zero Release Blocking findings across all three.
- **A recurring governance-drift pattern, found and closed five times
  this release alone.** `Interface Register.md`/`Dependency Injection
  Register.md`/`Module Register.md` (closed by `WP 7.1F`); `Platform
  Services Register.md`/`Platform Service Map.md` (found by `WP 7.3A`,
  partially closed); `Documentation Register.md`/`Governance
  Register.md` (found and fully closed by `WP 7.4.0`). `FCR-0005`
  (Governance Register Health-Check Tooling) remains open, its own
  priority repeatedly reconfirmed by each recurrence.

## Completed Programmes

### Engineering Foundation (`WP 7.0A`–`WP 7.1F`)

| Work Package | What It Delivered |
|---|---|
| `WP 7.0A` | `VISION.md`, `Future Capability Register.md` (established), `Capability Categories.md`, `Product Roadmap.md` |
| `WP 7.0B` | Full capability dependency graph, six engineering programmes, ten candidate Work Packages |
| `WP 7.0C` | Public C# contracts for all five Engineering Foundation frameworks; reserved `ADR-0053`–`ADR-0057` |
| `WP 7.1A` | `Tempest.Core.EngineeringData` (`ADR-0053`) — the shared document/revision/reference storage foundation |
| `WP 7.1B` | `Tempest.Core.UnitsAndQuantities` (`ADR-0054`) — compile-time dimension-safe quantities |
| `WP 7.1C` | `Tempest.Core.Materials` (`ADR-0055`) — provenance-carrying material specifications |
| `WP 7.1D` | `Tempest.Core.Calculations` (`ADR-0056`) — durable, evidentiary calculation records |
| `WP 7.1E` | `Tempest.Core.Verification` (`ADR-0057`) — engineering claim verification, distinct from Audit and Calculation Record |
| `WP 7.1F` | **ENGINEERING CORE CERTIFIED WITH ACCEPTED TECHNICAL DEBT** — independent closing certification |

### Systems Engineering Foundation (`WP 7.2A`–`WP 7.3A`)

| Work Package | What It Delivered |
|---|---|
| `WP 7.2A` | Recommended Programme A (Requirements & Verification Platform, `FCR-0027`), scoring 46/55 against seven candidates |
| `WP 7.2B` | Complete architecture: twelve domain concepts, a three-layer Engineering Core/Systems Engineering Foundation/Engineering Discipline Modules model, a digital thread design |
| `WP 7.2C` | Complete public contracts for all thirteen named domain concepts; reserved `ADR-0058`–`ADR-0061` |
| `WP 7.3A` | `Tempest.Core.Requirements` (`ADR-0058`–`ADR-0061`) — the Requirements Engine, `FCR-0027` now **Implemented** |

## Engineering Statistics

| Metric | v0.6.0 | v0.7.0 | Change |
|---|---|---|---|
| Automated tests | 1016 | 1406 | +390 |
| ADRs | 52 | 61 | +9 |
| Rejected Designs | 45 | 45 | — |
| Academy articles | 86 | 104 | +18 |
| Governance registers | 27 | 27 | — |
| Architecture documents | 20 | 20 | — |
| Platform services catalogued | 26 | 27 | +1 |
| Modules (production) | 15 | 20 | +5 |
| Public interfaces (`src/Tempest.Core/`) | 64 | 80 | +16 |
| DI registrations (named) | 26 | 31 | +5 |
| Custom exception types | 52 | 66 | +14 |
| Technical Debt Register items | 24 | 25 | +1 |
| Future Capability Register entries | 33 | 38 | +5 |

Full detail: `docs/releases/v0.7.0/WP7.4.0 Engineering Statistics
Report.md`.

## Architecture Highlights

- **Zero circular dependencies, zero layering violations**, confirmed
  directly by dependency-graph inspection, not assumed — every one of
  the five Engineering Foundation frameworks and the Requirements Engine
  depends only downward, never on a sibling or sideways.
- **A three-layer engineering model now proven in code**: Engineering
  Core (Data Model, Units & Quantities, Materials, Calculations,
  Verification) → Systems Engineering Foundation (Requirements) →
  Engineering Discipline Modules (not yet built). Each layer consumes
  only the layer(s) beneath it.
- **The Requirement Status / Verification Outcome separation** — a
  central design principle argued for at the architecture stage
  (`WP 7.2B`) — is now demonstrated structurally in the shipped
  `RequirementsService`: zero code path connects `RequirementStatus` to
  `VerificationOutcome`.
- **This platform's first substantial dependency remains
  ASP.NET Core/Kestrel** (`ADR-0049`, `v0.6.0`) — unchanged this
  release; every Engineering Foundation and Systems Engineering
  framework introduces zero new third-party dependency.
- Full detail: `docs/releases/v0.7.0/WP7.4.0 Architecture Baseline
  Summary.md`.

## Breaking Changes

**None.** No Platform Foundation, Developer Experience, or Platform
Services contract was modified this release. Every Engineering
Foundation and Systems Engineering Foundation framework is new,
additive surface area — `Tempest.Core.EngineeringData`,
`.UnitsAndQuantities`, `.Materials`, `.Calculations`, `.Verification`,
and `.Requirements` are all six brand-new namespaces with zero existing
consumers, so no compatibility question exists for any of them.

## Known Limitations

Carried forward from `v0.6.0`, still open, none worsened this release:
`TD-09`/`TD-10`/`TD-11` (no plugin/first-party trust isolation),
`TD-13`/`TD-14` (REST API has no real authentication or TLS, mitigated
by loopback-only binding), `TD-16` (no cryptographic license file
signature verification).

New this release, all disclosed, none Release Blocking:
`TD-17`/`TD-18` (Engineering Data Model: string-only content, no
high-concurrency `LinkAsync` load test), `TD-19` (no affine/offset unit
conversion — Temperature deferred), `TD-20` (`MaterialCatalog` reads
full revision history for a latest-only lookup), `TD-21`/`TD-22`
(Calculation: no cancellation reaches a running calculation; no bound
on recorded data volume), `TD-23`/`TD-24` (Verification: `RecordAsync`'s
multi-link sequence is not transactional; no bound on recorded data
volume), `TD-25` (Requirements: no concurrency-conflict detection on
`ReviseAsync`/`SetStatusAsync`).

Full, current detail: `docs/governance/Quality/Technical Debt
Register.md`.

## Deferred Work

- **String-based Requirement allocation targets** (`FCR-0037`) —
  `WP7.2B`'s own broader architectural vision (an open-string allocation
  target for a not-yet-created design element) was never carried into
  `WP7.2C`'s own approved, Guid-only `LinkAsync` contract; disclosed as
  a Future Capability, not a defect.
- **Requirement baselining** (`FCR-0038`) and **change impact
  analysis** — both plausible once a non-trivial requirement set with
  real relationship depth exists.
- **Compliance and Workflow capability** — explicitly out of scope for
  the entire Systems Engineering Foundation programme so far; no
  requirement lifecycle automation, electronic approval, or
  standards-compliance mapping exists yet.
- **The four Engineering Foundation frameworks' own missing
  `Platform Service Map.md`/`Platform Services Register.md` rows** —
  found by `WP 7.3A`, only Requirements Engine's own row was corrected;
  Engineering Data Model, Materials, Calculations, and Verification
  still lack rows in these two documents, disclosed as an outstanding
  gap for a future Work Package, not fixed here (outside `WP 7.4.0`'s
  own release-preparation scope).
- **Governance Register Health-Check Tooling** (`FCR-0005`) — still not
  built; this release's own audit found the identical class of drift a
  fourth and fifth time.

## Technical Debt Summary

25 tracked debt items (3 Resolved, 1 Partially resolved, 21 Open) and 17
disclosed trade-offs (1 Retired, 16 active) — **zero Release Blocking**
across the entire release. Nine new items disclosed this release
(`TD-17`–`TD-25`), each the product of a dedicated engineering
self-review or Security Review, not discovered after the fact. Full
detail: `docs/governance/Quality/Technical Debt Register.md`;
`docs/releases/v0.7.0/WP7.4.0 Release Readiness Report.md`.

## Future Roadmap

Per `docs/governance/Future Capability Register.md` and `WP7.2A
Recommended Programme.md`: Programme F (Platform Hardening) is
recommended as the next programme, at `v0.9.0`, scoring 36/55 (second
highest of the seven `WP 7.2A` evaluated). A further Systems Engineering
capability (baselining, change impact analysis, string-based
allocation) or the first discipline-specific engineering module
(Mechanical, HVAC, Structural, Electrical — each currently scoring
14/55 for lack of any identified capability) both remain open,
unscheduled candidates. **No further Work Package begins until Product
Approval authorises it**, per this project's own standing discipline
(`FOUNDATION.md` §1).

## Acknowledgements

Both programmes in this release were developed using the same
architecture-first engineering process every prior release established:
a complete architecture and contract review package was approved before
implementation began in each of the two programmes, and every
implementation Work Package built directly against those unrevised
documents, disclosing genuine implementation-phase findings via ADRs
rather than silently absorbing them. This release marks the platform's
transition from a collection of independent platform services (`v0.6.0`)
to a genuine **engineering** platform — the first release where
TempestOS itself understands what a requirement, a material, a
calculation, and a verified engineering claim actually are.

## Related Documents

`docs/releases/v0.7.0/WP7.4.0 Release Readiness Report.md`;
`docs/releases/v0.7.0/WP7.4.0 Product Approval Report.md`;
`docs/releases/v0.7.0/WP7.4.0 Engineering Statistics Report.md`;
`docs/releases/v0.7.0/WP7.4.0 Architecture Baseline Summary.md`;
`docs/releases/v0.7.0/Retrospective.md`; `PROJECT_STATUS.md`.
