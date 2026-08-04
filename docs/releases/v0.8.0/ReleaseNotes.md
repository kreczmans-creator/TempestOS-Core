# TempestOS v0.8.0 — "Engineering Workspace"

**Status:** Prepared for Product Approval (`WP 8.9.0`, Release
Preparation & Product Baseline). Not yet tagged, merged, or released —
the physical Git tag and GitHub Release are created by the Product
Owner after approval, per this Work Package's own explicit constraint.
**Branch:** `feature/v0.8.0-engineering-workspace` (not yet merged to
`main`).

---

## Executive Summary

TempestOS v0.8.0 delivers the **Engineering Workspace** — the
platform's first user-facing engineering product surface, a
five-region terminal shell answering "where am I / what needs
attention / is the project healthy / what should I do next" on every
visit — and, alongside it, the **Engineering Domain**: a shared,
canonical vocabulary of ~49 named engineering object families,
compiled and given real, tested implementation for the first time, that
every future engineering discipline module will build on rather than
reinvent.

Two independent tracks, each following this project's own standing
architecture-first discipline in full:

1. **Engineering Workspace** (`WP 8.0A`–`WP 8.1C`) — architecture,
   contracts, shell implementation, an interleaved UX specification,
   navigation/project explorer, and the Engineering Cockpit.
2. **Engineering Domain** (`WP 8.2A`–`WP 8.2C`) — architecture,
   contracts, and implementation for the canonical Engineering Object
   model every current and future discipline framework will share.

Nine Work Packages, zero architectural rework across any phase
boundary — every architecture and contract package this release
approved was implemented exactly as approved, with every genuine
implementation-phase finding disclosed via an ADR rather than silently
absorbed.

## Major Capabilities

### Engineering Workspace

`Tempest.App`'s own default launch target is now the Workspace, not
console `TempestShell` (`ADR-0068`) — a five-region shell (Areas,
Project Explorer, Documents, Properties, Status Bar), terminal-realised
by deliberate design (`ADR-0066`), not a graphical desktop framework.
Navigation carries real history, breadcrumbs, and recent items
(`WP 8.1B`); the Project Explorer is populated and navigable against a
representative, disclosed sample tree, proving its own Kind-keyed
extensibility mechanism (`ADR-0067`) end to end.

### Engineering Cockpit

The Workspace's own default landing screen (`ADR-0069`), answering four
questions every visit — where am I, what needs attention, is the
project healthy, what should I do next. Every card with a real
Workspace-service backing (`NavigationService`, `ICommandRegistry`) is a
live read; every other card is fixed, explicitly disclosed placeholder
content — never fabricated to look real.

### Engineering Domain

A new `Tempest.Core.EngineeringDomain` namespace: 83 compiled contract
types (facets, relationships, lifecycle, validation, digital thread —
`WP 8.2B`) and 38 concrete canonical object classes over one shared
`EngineeringObjectBase` (`WP 8.2C`), constructed through two generic
factory types rather than dozens of hand-written ones. Every object's
own real storage reuses the existing, shared `IEngineeringDocumentStore`
— zero new persistence introduced; a new, purely in-memory repository
layer answers the one question that store cannot ("list every object of
Kind X"). The five canonical Kinds an existing framework already owns
(Requirement, RequirementCollection/Group, VerificationRecord,
CalculationRecord, MaterialSpecification) are deliberately not given a
competing concrete realisation — their ownership stays exactly where it
already was.

## Completed Work Packages

### Engineering Workspace Track (`WP 8.0A`–`WP 8.1C`)

| Work Package | What It Delivered |
|---|---|
| `WP 8.0A` | Complete Workspace architecture across twelve named areas; `ADR-0062`–`ADR-0065` |
| `WP 8.0B` | Complete public contracts for all twelve Workspace interfaces; resolved `ADR-0066`/`ADR-0067` |
| `WP 8.1A` | `Tempest.App.Workspace` — the shell itself, now `Tempest.App`'s own default launch target (`ADR-0068`) |
| `WP 8.0C` | Complete target UX specification across 28 named scope areas; `ADR-0069`/`ADR-0070` |
| `WP 8.1B` | Navigation history/breadcrumbs/recent items, Project Explorer, first real Kind-keyed registration; `ADR-0071` |
| `WP 8.1C` | Engineering Cockpit — the Workspace's own default landing screen |

### Engineering Domain Track (`WP 8.2A`–`WP 8.2C`)

| Work Package | What It Delivered |
|---|---|
| `WP 8.2A` | Complete canonical Engineering Domain Architecture — ~49 objects, 13 families, 20 relationship kinds; `ADR-0072`–`ADR-0074` |
| `WP 8.2B` | Complete public contracts for the entire canonical model; `ADR-0075`/`ADR-0076` |
| `WP 8.2C` | 38 concrete object classes, a shared implementation framework, a new in-memory repository layer, a representative sample module; `ADR-0077`–`ADR-0079` |

## Engineering Statistics

| Metric | v0.7.0 | v0.8.0 | Change |
|---|---|---|---|
| Automated tests | 1406 | 1631 | +225 |
| ADRs | 61 | 79 | +18 |
| Rejected Designs | 45 | 45 | — |
| Academy articles | 104 | 116 | +12 |
| Governance registers | 27 | 27 | — |
| Architecture documents | 20 | 20 | — |
| Platform services catalogued | 27 | 27 | — |
| Modules (production) | 20 | 22 | +2 |
| Public interfaces (`src/Tempest.Core/`) | 80 | 163 | +83 |
| DI registrations (named) | 31 | 41 | +10 |
| Custom exception types | 66 | 69 | +3 |
| Technical Debt Register items | 25 | 25 | — |
| Future Capability Register entries | 38 | 38 | — |

Full detail: `docs/releases/v0.8.0/WP8.9.0 Engineering Statistics
Report.md`.

## Architecture Highlights

- **Zero circular dependencies, zero layering violations**, confirmed
  directly by dependency-graph inspection — `Tempest.Core.EngineeringDomain`
  depends only on `Tempest.Core.EngineeringData`; the Workspace depends
  only on Platform Services and Engineering Core reads via
  `ITempestHost.Services`; neither track was asked to consume the
  other, and neither accidentally does.
- **The "reuse what already exists" pattern, now proven a third time at
  a new scale.** Fifteen new ADRs this release independently reach
  "reuse the existing mechanism, introduce nothing new" as their own
  central decision — first proven across six Engineering Core/Systems
  Engineering frameworks (`v0.7.0`), now confirmed holding for a
  presentation layer and a shared-vocabulary layer too.
- **A genuine architectural tension, found and resolved twice at
  different stages of the same design.** `ADR-0076` (contract stage)
  and `ADR-0077` (implementation stage) each resolve the identical
  shape of conflict — a literal reading of the controlling brief versus
  a prior, binding decision — the same way, at two different points in
  the same three-Work-Package sequence.
- Full detail: `docs/releases/v0.8.0/WP8.9.0 Architecture Baseline
  Summary.md`.

## Breaking Changes

**None.** No Platform Foundation, Developer Experience, Platform
Services, or Engineering Foundation/Systems Engineering Foundation
contract was modified this release. The Workspace and the Engineering
Domain are both entirely new, additive surface area — `Tempest.App.Workspace`
and `Tempest.Core.EngineeringDomain` are both brand-new namespaces with
zero prior consumers, so no compatibility question exists for either.

## Known Limitations

Carried forward from `v0.7.0`, still open, none worsened this release:
25 Technical Debt items, 17 disclosed trade-offs, none Release
Blocking (see Technical Debt Summary, below).

New this release, all disclosed, none Release Blocking:

- **Command Palette is reachable from the Cockpit only**, not
  screen-independently — a disclosed, partial realisation of `ADR-0070`.
- **Properties/Inspector panel does not exist** — named, not designed.
- **Project Explorer content is entirely fictional** — no real
  Engineering Core `Kind` is wired into it yet.
- **The Engineering Cockpit renders no Engineering Domain object** —
  the two tracks this release shipped in parallel, not integrated.
- **The five already-Implemented canonical Kinds cannot be constructed
  through `Tempest.Core.EngineeringDomain`** — by design (`ADR-0078`).
- **The in-memory repository does not rebuild itself from the real
  store on Host restart** — the underlying documents survive; the
  by-Kind index over them does not, yet.
- **Zero dedicated Security Reviews performed this release** — a
  disclosed departure from `v0.7.0`'s own three-review standard,
  mitigated by this release's own narrow, low-risk technical footprint
  (no new external attack surface, no new authentication path, no new
  persistence technology) — see `WP8.9.0 Product Approval Report.md`.

Full, current detail: `docs/governance/Quality/Technical Debt
Register.md`.

## Deferred Work

- **A real Physical/Configuration Engineering Discipline Module** —
  now buildable directly against `WP 8.2C`'s own compiled
  `IAssembly`/`ISubAssembly`/`IPart`/`IComponent` classes; the most
  natural next Work Package all three Engineering Domain Work Packages
  name.
- **The first real, production `IWorkspaceViewFactory`/
  `IProjectExplorerNodeProvider` pair** for an actual Engineering Core
  `Kind` — `WP 8.1B` proved the mechanism only against fictional sample
  content.
- **The Properties/Inspector panel and the Project Dashboard** — both
  named, neither designed.
- **The four Engineering Foundation frameworks' own missing `Platform
  Service Map.md`/`Platform Services Register.md` rows** — found by
  `WP 7.3A`, confirmed still open by `WP 7.4.0`, confirmed still open a
  second consecutive release cycle by `WP 8.9.0`. Disclosed, not fixed,
  outside release-preparation scope both times.
- **Governance Register Health-Check Tooling** (`FCR-0005`) — still not
  built.
- **A dedicated Security Review**, recommended as this release's own
  single most important carry-forward item for whatever Work Package
  begins Programme 9.

## Technical Debt Summary

25 tracked debt items, 17 disclosed trade-offs — **unchanged this
release, zero new items raised across all nine `v0.8.0` Work
Packages**, and **zero Release Blocking**. Every genuine limitation
this release surfaced was disclosed as an ADR consequence or a named
Future Evolution item at the Work Package that found it, rather than
deferred into a new Technical Debt Register entry. Full detail:
`docs/governance/Quality/Technical Debt Register.md`;
`docs/releases/v0.8.0/WP8.9.0 Release Readiness Report.md`.

## Future Roadmap

Per `docs/governance/Future Capability Register.md` and `WP7.2A
Recommended Programme.md`: Programme F (Platform Hardening) remains
recommended as the next programme, at `v0.9.0`, scoring 36/55. A real
Physical/Configuration Engineering Discipline Module — now the most
concrete, ready-to-start candidate this release names — a further
Systems Engineering capability, or Programme F all remain open,
unscheduled candidates. **No further Work Package begins until Product
Approval authorises it**, per this project's own standing discipline
(`FOUNDATION.md` §1).

## Acknowledgements

Both tracks in this release were developed using the same
architecture-first engineering process every prior release established:
a complete architecture and contract review package was approved before
implementation began in each of the two tracks, and every
implementation Work Package built directly against those unrevised
documents, disclosing genuine implementation-phase findings via ADRs
rather than silently absorbing them. This release marks the platform's
transition from an engineering *foundation* (`v0.7.0` — the platform
understands what a requirement, a material, a calculation, and a
verified claim are) to an engineering *product* — the first release
where a user can actually open TempestOS and see their own engineering
work, and where every future discipline module inherits a shared,
proven vocabulary rather than inventing its own.

## Related Documents

`docs/releases/v0.8.0/WP8.9.0 Release Readiness Report.md`;
`docs/releases/v0.8.0/WP8.9.0 Product Approval Report.md`;
`docs/releases/v0.8.0/WP8.9.0 Engineering Statistics Report.md`;
`docs/releases/v0.8.0/WP8.9.0 Architecture Baseline Summary.md`;
`docs/releases/v0.8.0/WP8.9.0 Workspace Baseline Summary.md`;
`docs/releases/v0.8.0/WP8.9.0 Engineering Domain Baseline Summary.md`;
`docs/releases/v0.8.0/Retrospective.md`; `PROJECT_STATUS.md`.
