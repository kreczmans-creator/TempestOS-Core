# TempestOS v0.9.0 — "Mechanical Foundation"

**Status:** Prepared for Product Approval, confirmed by **two**
independent release-readiness passes (`WP 9.9.0` first pass; `WP 9.9.0`
second pass, after `WP 9.8B` closed the first pass's own top standing
recommendation). Not yet tagged, merged, or released — the physical Git
tag and GitHub Release are created by the Product Owner after approval,
per each of those Work Packages' own explicit constraint. **Branch:**
narrated throughout this release's own documentation as
`feature/v0.9.0-calculations-workspace`; `git branch -a` shows only
`main` today — disclosed, not silently reconciled (see `WP9.9.0 Release
Readiness Report (Second Pass).md` §1). **Updated in place for the
second pass** — this document is a living release artifact, not a
Work-Package-dated one; see `docs/releases/v0.9.0/Retrospective.md` for
the full account of what changed between passes.

---

## Executive Summary

TempestOS v0.9.0 delivers the **Mechanical Foundation** — the release
that turns the Engineering Workspace (`v0.8.0`'s own proven, but empty,
presentation shell) and the Engineering Domain (`v0.8.0`'s own compiled,
but never-instantiated, canonical object model) into a genuine
engineering product: **six real Engineering Disciplines, end to end** —
Mechanical Product Structure, Requirements Management, Engineering
Calculations, Engineering Documents, Verification Management, and
Manufacturing — each with a browsable Project Explorer area, a real
Property Inspector, a full command set, real Digital Thread links, and
real Engineering Cockpit KPIs.

Seven Work Packages, zero architectural rework across any of them —
four required zero Domain-layer change at all, and the three that did
each made only small, additive facet extensions, never a reopened,
frozen contract. Every genuine implementation-phase finding was
disclosed via an ADR or a Technical Debt item rather than silently
absorbed, including two disclosed numbering irregularities in how the
seven Work Packages were commissioned (a genuine gap, and a genuine,
harmless skip) — both recorded plainly, neither hidden.

## Major Capabilities

### Six Real Engineering Disciplines

`Program.cs` now registers six real `*WorkspaceRegistration.Register`
calls, in dependency order — Mechanical (`WP 9.0A`/`WP 9.0B`),
Requirements (`WP 9.1A`), Calculations (`WP 9.2A`), Documents (`WP 9.4A`),
Verification (`WP 9.3A`), Manufacturing (`WP 9.5A`) — each proving the
Kind-keyed Workspace extension model (`ADR-0067`) against a genuinely
different Engineering Domain shape, without a single frozen Workspace
contract being reopened.

### Engineering Cockpit, Now Substantively Real

Five of six disciplines gained a dedicated, real KPI card set and a
derived health status (Requirements/Calculations/Documents/Verification/
Manufacturing); Mechanical's own original `WP 8.1C`-era generic reads
were extended rather than replaced. `AttentionItems`/`OpenActions` each
carry one real, conditional entry per discipline.

### A New, Disclosed Reuse Pattern: Cross-Work-Package Facet/View Provider Sharing

`WP 9.5A` (Manufacturing) is the first Work Package in this project's
history to register a foreign discipline's own already-shipped Property
Facet Provider/Workspace View types directly, for its own Kind, rather
than duplicating equivalent code — `"WorkInstruction"` reuses Documents'
own; `"Inspection"` reuses Verification's own — both proven correct by
dedicated tests, not merely assumed compatible.

### Real, Zero-New-Code Cross-Discipline Command Reuse

Two commands from earlier disciplines were dispatched, unmodified,
against a *different* discipline's own Domain Kind, and proven correct
by dedicated tests: `Mechanical.SetBomLineCommand` against a
`"ManufacturingOperation"`, and `Verification.RecordVerificationResultCommand`
against an `"Inspection"` — both already Kind-agnostic by design,
exercised outside their own originating discipline for the first time.

## Completed Work Packages

| Work Package | What It Delivered |
|---|---|
| `WP 9.0A` | Mechanical Product Structure — the first real Engineering Discipline wired into the Workspace; `ADR-0080`–`ADR-0082` |
| `WP 9.0B` | Product Configuration & BOM Management — extends `WP 9.0A` in place with BOM lines, Baselines, Configuration comparison; `ADR-0083` |
| `WP 9.1A` | Requirements Management Workspace — the second real Engineering Discipline; `ADR-0084`/`ADR-0085` |
| `WP 9.2A` | Engineering Calculations Workspace — the third real Engineering Discipline, first requiring zero Domain-layer changes; `ADR-0086`/`ADR-0087` |
| `WP 9.4A` | Engineering Documents Workspace — the fourth real Engineering Discipline; `ADR-0088` |
| `WP 9.3A` | Verification Management Workspace — the fifth real Engineering Discipline, completed after `WP 9.4A` despite its own earlier number (disclosed numbering gap, see Known Limitations); `ADR-0089`/`ADR-0090` |
| `WP 9.5A` | Manufacturing Workspace — the sixth real Engineering Discipline, first to demonstrate cross-Work-Package facet/view provider reuse; `ADR-0091` |
| `WP 9.8B` | Platform Service Register Reconciliation — commissioned after `WP 9.9.0`'s own first pass despite an earlier number; closes the four-Engineering-Foundation-framework Platform Service gap `WP 7.3A` first disclosed |

## Engineering Statistics

| Metric | v0.8.0 | v0.9.0 | Change |
|---|---|---|---|
| Automated tests | 1631 | 2026 | +395 |
| ADRs | 79 | 91 | +12 |
| Rejected Designs | 45 | 45 | — |
| Academy articles | 116 | 127 | +11 |
| Governance registers | 27 | 27 | — |
| Architecture documents | 20 | 20 | — |
| Platform services catalogued | 27 (claimed; true prior count 26) | **30** | **+4** (Engineering Data Model, Materials, Engineering Calculations, Verification — `WP 9.8B`, closing the disclosed gap) |
| Modules (production) | 22 | 34 | +12 |
| Public interfaces (`src/Tempest.Core/`) | 163 | 168 | +5 |
| DI registrations (named) | 41 | 42 | +1 |
| Custom exception types | 69 | 72 | +3 |
| Technical Debt Register items | 25 | **34** | **+9** (includes `TD-34`, a newly-registered, non-blocking test flake — see Known Limitations) |
| Future Capability Register entries | 38 | 62 | +24 |

Full detail: `docs/releases/v0.9.0/WP9.9.0 Engineering Statistics
Report (Second Pass).md`.

## Architecture Highlights

- **Zero circular dependencies, zero layering violations**, confirmed
  directly by dependency-graph inspection. One new, disclosed
  intra-`Tempest.App` namespace dependency (`.Manufacturing` →
  `.Documents`/`.Verification`), confirmed one-directional.
- **The "reuse what already exists" pattern, now proven a fourth time
  at a new scale.** Twelve new ADRs this release independently reach
  "reuse the existing mechanism, introduce nothing new" as their own
  central decision, extending to a *sequencing* concept (`ADR-0091`,
  Routings) for the first time, not only a classification one.
- **A first for this project: genuine cross-Work-Package Workspace-layer
  reuse**, proven correct by dedicated tests rather than assumed.
- Full detail: `docs/releases/v0.9.0/WP9.9.0 Architecture Baseline
  Summary.md`.

## Breaking Changes

**None.** No Platform Foundation, Developer Experience, Platform
Services, Engineering Foundation, or Systems Engineering Foundation
contract was modified this release. Every `Tempest.Core.EngineeringDomain`
change is additive (three new facets total, across `WP 9.0A`/`WP 9.0B`);
every `Tempest.Core.Requirements` change is additive (one new
interface, `WP 9.1A`). `Tempest.Core.Calculations`/`Tempest.Core.Verification`/
`Tempest.Core.Materials` are byte-for-byte unchanged.

## Known Limitations

Carried forward from `v0.8.0`, still open, none worsened this release:
25 of the 34 tracked Technical Debt items predate this release, none
Release Blocking.

New this release, all disclosed, none Release Blocking:

- **A disclosed numbering gap**: `WP 9.3A` was commissioned, completed,
  and documented *after* `WP 9.4A` despite carrying the earlier number —
  fully disclosed by both Work Packages' own retrospectives and by
  `PROJECT_STATUS.md`, neither silently reordered.
- **A disclosed numbering skip**: this release's own controlling
  instructions moved directly from `WP 9.5A` to `WP 9.9.0`, skipping
  `WP 9.6A`–`WP 9.8A` — never named or reserved anywhere in this
  repository, a deliberate Product Owner sequencing choice, not an
  error.
- **A disclosed, third numbering irregularity**: `WP 9.8B` was
  commissioned *after* `WP 9.9.0`'s own first pass despite carrying an
  earlier number — a direct, deliberate response to that pass's own top
  standing recommendation, not an error either.
- **The four-framework Platform Service Map/Register gap — RESOLVED.**
  Found by `WP 7.3A`, confirmed open across three consecutive
  release-closing reviews (`WP 7.4.0`, `WP 8.9.0`, `WP 9.9.0` first
  pass), closed by `WP 9.8B`, independently re-confirmed closed by
  `WP 9.9.0`'s own second pass.
- **A "32 vs. 35 governance documents" count drift** — found by
  `WP 9.3A`, reconfirmed open by `WP 9.5A`, `WP 9.9.0`, and `WP 9.8B`;
  the underlying 27 individually-tracked registers remain accurate and
  current.
- **No governed Approval/Review workflow, no file/URL attachment
  storage service** — both carried forward from `v0.8.0`'s own
  `WP 7.x`-era disclosures, now consequential for a fourth/second real
  discipline respectively (`TD-30`, `TD-31`).
- **`EngineeringCockpit.FormatCoverage`'s own zero-denominator text is
  hardcoded Requirements-specific** — found by `WP 9.5A` while building
  Manufacturing's own KPI cards, worked around locally, not fixed at the
  shared source (`TD-33`).
- **`VerificationService.RecordAsync`'s own subject→record link remains
  invisible to `RelationshipRepository`** — found by `WP 9.3A`, now
  reconfirmed harmlessly consequential for a second discipline
  (Manufacturing's own Inspection recording, `TD-32`).
- **A previously-informally-disclosed, non-reproducible test flake is
  now formally registered** — `CompositeLogSinkTests`'s own
  cross-test-class `Console.Error`-capture race, named narratively
  since `WP 6.3` but never tracked as its own item until `WP 9.9.0`'s
  own second pass actually observed a live instance (`TD-34`). No
  data-correctness consequence; the underlying logging behaviour is
  correct and proven so by the same test's own repeated, isolated
  passes.

Full, current detail: `docs/governance/Quality/Technical Debt
Register.md`.

## Deferred Work

- **A dedicated Governance & Risk Workspace** for `Risk`/`Issue`/
  `Decision`/`Hazard`/`Assumption` (`FCR-0056`) — the most natural next
  Engineering Discipline candidate, all four Domain classes already
  compiled and instantiated by the base sample module.
- **A genuine `Routing`/`SupplierOperation` Domain Kind** with structured
  fields, beyond today's `Classification`-tagged representation
  (`FCR-0060`).
- **A governed Approval/Review workflow** (`FCR-0052`/`FCR-0058`), a real
  file/URL attachment storage service (`FCR-0054`), and
  `VerificationService.RecordAsync` additionally linking through
  `IHasRelationships` (`FCR-0057`/`FCR-0062`) — all carried forward,
  none built this release, all named Future Capability candidates.
- ~~The four Engineering Foundation frameworks' own missing `Platform
  Service Map.md`/`Platform Services Register.md` rows~~ — **Done,
  `WP 9.8B`**, after being confirmed open across three consecutive
  release-closing reviews.
- **Governance Register Health-Check Tooling** (`FCR-0005`) — still not
  built, now disclosed as recurring across a third consecutive
  release-closing review — `WP 9.8B`'s own existence (a dedicated Work
  Package needed to close what automation could have caught
  immediately) is itself further evidence for it.

## Technical Debt Summary

34 tracked debt items (+9 this release: one per implementation Work
Package, plus `TD-34`, found during `WP 9.9.0`'s own second
verification pass), 17 disclosed trade-offs (unchanged) — **zero
Release Blocking**. Every genuine limitation this release surfaced was
disclosed at the Work Package that found it. Full detail:
`docs/governance/Quality/Technical Debt Register.md`;
`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report (Second
Pass).md`.

## Future Roadmap

Per `docs/governance/Future Capability Register.md`: a dedicated
Governance & Risk Workspace (`FCR-0056`) is the most concrete,
ready-to-start next Engineering Discipline candidate — every Domain
class it needs is already compiled and already live in the base sample
module, the identical starting position every one of this release's own
six disciplines began from. **No further Work Package begins until
Product Approval authorises it**, per this project's own standing
discipline (`FOUNDATION.md` §1).

## Acknowledgements

Every Work Package in this release was developed using the same
architecture-first engineering process every prior release established
— reusing the existing Engineering Domain, Workspace, and Digital
Thread exclusively, disclosing every genuine implementation-phase
finding via an ADR or a Technical Debt item rather than silently
absorbing it. This release marks the platform's transition from an
engineering *product with one proven discipline* (`v0.8.0` — the
Workspace and the Engineering Domain existed, but rendered nothing real)
to an engineering *product a user can actually work in* — six
disciplines, one shared Digital Thread, one Cockpit, real from end to
end.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report.md` (first pass)
and `docs/releases/v0.9.0/WP9.9.0 Release Readiness Report (Second
Pass).md`; `docs/releases/v0.9.0/WP9.9.0 Product Approval Report.md`
and `(Second Pass).md`; `docs/releases/v0.9.0/WP9.9.0 Engineering
Statistics Report.md` and `(Second Pass).md`; `docs/releases/v0.9.0/
WP9.9.0 Architecture Baseline Summary.md` and `(Second Pass).md`;
`docs/releases/v0.9.0/WP9.9.0 Engineering Capability Summary.md` and
`(Second Pass).md`; `docs/releases/v0.9.0/WP9.8B Reconciliation
Report.md`; `docs/releases/v0.9.0/Retrospective.md`; `PROJECT_STATUS.md`.
