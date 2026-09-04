# v0.16.0 — Work Packages

## Status

**Proposed, not approved; not started.** Scope, sequencing, and
acceptance for every row are in `v0.16.0 Release Plan.md` in this
folder. `VERSION` remains `0.15.0` until `WP 16.9.0`. This folder
exists so the plan lives where this project keeps release plans; it is
not a claim that `v0.16.0` has begun.

## Purpose

v1.0 Readiness Hygiene — every item `docs/releases/v1.0.0/v1.0.0
Release Candidate Audit.md` §5.1 rated mandatory under the approved
v1.0 definition, except the v1.0 gate itself. No new product capability.
Closes with the first Product Approval verdict since `v0.12.0`.

## Work Packages

| Work Package | Scope | Type | Wave | Closes | Status |
|---|---|---|---|---|---|
| `WP 16.0A` | v1.0 Scope & Support Decision Record — six decisions (governing definition, Companion, plugins, REST/`AT-10`, platform matrix, `v0.15.1` folder), each a `D-0xx` entry; `Product Roadmap.md` Phase 5.5 | Decision | 0 | Audit §2, M1, M9 | Not started |
| `WP 16.0B` | Integrate off-`main` work — merge `WP 15.2A`; fold `docs/releases/v0.15.1/` into this release; Companion branch decision applied; `FCR-0092` citation resolved | Governance/Integration | 1 | `TD-120`, `TD-82` | Not started |
| `WP 16.1A` | Enforce the release gate — GitHub branch protection per `WP11.1B` §4; `CI Gate` depends on `Governance Health Check` | Configuration | 1 | `TD-45` | Not started |
| `WP 16.3A` | Durable state schema versioning — architecture, `ADR-0120` | Architecture | 1 | `TD-87` (design) | Not started |
| `WP 16.4A` | Test determinism — `CompositeLogSink` console dependency, last fixed wait, real image decode, `MainWindow` resize coverage; five consecutive CI runs | Implementation (tests) | 1 | `TD-34`, `TD-119`, `TD-100`, `TD-83` | Not started |
| `WP 16.5B` | Linux/X11 — timeboxed Avalonia/`Tmds.DBus.Protocol` upgrade spike; fix, or state the support matrix in three documents | Implementation or Documentation | 1 | `TD-116` | Not started |
| `WP 16.2A` | Register & status currency — six `TD-57` registers, Feature and Release registers, Repository Metrics, `Product Roadmap.md`, `PROJECT_STATUS.md` lower sections archived and re-derived | Documentation/Governance | 2 | `TD-57`, `DNB-7` | Not started |
| `WP 16.3B` | Durable state schema versioning — implementation, migration chain, golden corpus | Implementation | 2 | `TD-87` | Not started |
| `WP 16.5A` | Accessibility baseline — modal dialogs, automation names, live regions, graph keyboard operability, contrast; modality test | Implementation | 2 | `TD-65` (partial) | Not started |
| `WP 16.1B` | Health-check extension — Interface/Exception/Namespace registers re-derived from `src/`, TD/FCR summary consistency, ADR count; script exception path | Implementation | 3 | `TD-57` root cause, `TD-43` | Not started |
| `WP 16.2B` | Academy retrospective backfill — `v0.11.0`'s ten, `WP 15.1A`/`15.1B`, the pre-`v0.14.0` programme (~20); Academy Register annotations; Learning Path pointer | Documentation | 3 | `WP-Z3` §12 gap | Not started |
| `WP 16.4B` | Durability & loopback hygiene — orphan detection and sweep, attachment content release, bounded keyed lock, DI duplicate guard, gated OpenAPI route, REST listener off by default | Implementation | 3 | `TD-67`, `TD-68`, `TD-69`, `TD-97`, `TD-62`, `AT-10` decision | Not started |
| `WP 16.9.0` | Engineering Readiness Review, `VERSION` 0.16.0, Release Notes, merge/tag/publish under the enforced gate, **Product Approval verdict recorded** | Verification/Release | 4 | Audit finding 2 | Not started |

## Carried in from `main`'s own line, once `WP 16.0B` lands

| Work Package | Scope | Status |
|---|---|---|
| `WP 15.2A` | Desktop Test Suite Persistence Root Cleanup (closes `TD-120`) — delivered on `feature/wp15.2a-td120-persistence-root-cleanup`, CI green; merged into `feature/v0.16.0` by `WP 16.0B`, its report now at `docs/releases/v0.16.0/WP15.2A Desktop Test Suite Persistence Root Cleanup — Implementation Report.md`, the `v0.15.1` folder deleted (`D-026`, Proposed) | **Complete — merged** |

## Deferred out of this release, by the plan

| Item | Disposition |
|---|---|
| `TD-109` remaining `MainWindow` shell services, `TD-108`, `TD-118` | Conditional group C8 — `v0.17.0` or later; revisit triggers unchanged. |
| `TD-56`, `TD-61`, `TD-64` plugin-enablement preconditions | Conditional group C2 — only if `WP 16.0A` puts third-party plugins in v1.0. |
| `TD-13`, `TD-14`, `TD-16`, `FCR-0003`, `FCR-0004` | Conditional group C1 — only if Companion or non-loopback REST enters v1.0. |
| `TD-65` remaining inventory beyond the top four | Re-listed by `WP 16.5A`; v1.x. |
| `FCR-0073`, `FCR-0074`, `AT-23`, `TD-115`, `TD-77` | Conditional group C6. |

## Related Documents

`docs/releases/v0.16.0/v0.16.0 Release Plan.md`; `docs/releases/v1.0.0/
v1.0.0 Release Candidate Audit.md`; `docs/releases/v1.0.0/WorkPackages.md`;
`docs/governance/Quality/Technical Debt Register.md`; `PROJECT_STATUS.md`.
