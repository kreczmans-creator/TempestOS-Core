# Feature Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Feature Register |
| **Purpose** | The capability-level index of what TempestOS actually delivers, one row per major feature, cross-referencing the Work Package(s) that built it — the delivery lens, as distinct from the Platform Services Register's architectural lens. |
| **Scope** | Every major capability named in `docs/releases/v0.5.0/WorkPackages.md`, `docs/releases/v0.4.0/WorkPackages.md`, and `docs/releases/v0.3.0.md`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/releases/v0.5.0/WorkPackages.md`; `docs/releases/v0.4.0/WorkPackages.md`; `docs/releases/v0.4.0/CHANGELOG.md`; `docs/releases/v0.3.0.md`. |
| **Review Frequency** | Updated whenever a Work Package delivers or changes a major capability. |
| **Last Reviewed** | 2026-07-28 (WP 5.3, Developer Experience Improvements) — Developer Experience Improvements row corrected from "Not started" to Implemented, closing this release's own Feature Register. |
| **Related Documents** | `Platform Services Register.md`; `Release Register.md`; `Traceability Matrix.md`. |
| **Related ADRs** | See `Platform Services Register.md` for the full per-service ADR list. |
| **Related Academy Articles** | See `Academy Register.md`'s "03 Work Packages" table. |
| **Coverage Status** | Complete. |

---

## Runtime Foundation (v0.3.0)

| Feature | Status | Work Package |
|---|---|---|
| Module Discovery | Implemented | WP 2.1 |
| Runtime Registration | Implemented | WP 2.2 |
| Runtime Lifecycle | Implemented | WP 2.3 |
| Dependency Injection | Implemented | WP 2.4 |
| Configuration Framework | Implemented | WP 2.5 |
| Logging & Diagnostics Framework | Implemented | WP 2.6 |
| Runtime Host & Composition Root | Implemented | WP 2.7 (design), WP 2.7B (implementation) |

## Platform Foundation (v0.4.0, Released 2026-07-27)

| Feature | Status | Work Package |
|---|---|---|
| Platform Contracts (`IModule` reaffirmed, `IHostedService`, `ICriticalBackgroundService`, `ICommand`, `IEvent`, `IEventHandler<T>`) | Implemented (contracts) | WP 4.0 |
| Module SDK (`ModuleBase`, `ModuleLifecycleBase`) | Implemented | WP 4.1 |
| Plugin Manifest (discovery, loading) | Implemented | WP 4.2 (+ 4.2A/4.2B/4.2C prerequisites) |
| Platform Services Architecture Review | Complete (review, no code) | WP 4.2D |
| Sample Module (`ClockModule`) | Implemented | WP 4.3 |
| Dependency Injection for Discovered Modules (`ModuleMetadataAttribute`) | Implemented | WP 4.4A (design), WP 4.4B (implementation) |
| Event Bus (`IEventBus`/`EventBus`) | Implemented | WP 4.4 (design), WP 4.4D (implementation) |
| Sample Module Event Integration (`ClockLifecycleObserverModule`) | Implemented | WP 4.4E |
| Academy & Documentation Baseline Audit | Complete (audit, no code) | WP 4.4F |
| Background Services (hosted service discovery/orchestration) | Implemented | WP 4.5 (design), WP 4.5 (implementation) |
| Governance Register Baseline | Complete | WP 4.5A |
| Platform Foundation Closeout | Complete | WP 4.5B |

## Developer Experience Phase (v0.5.0, in progress — renumbered from `v0.4.0`'s deferred scope)

| Feature | Status | Work Package |
|---|---|---|
| Navigation Framework Architecture | **Complete** | WP 5.0A (formerly WP 4.6A) |
| Navigation Framework Implementation | **Implemented** | WP 5.0B (formerly WP 4.6B) |
| Shell & Composition Framework Architecture | **Complete** | WP 5.0C (new — not part of the original `v0.4.0` plan) |
| Shell & Composition Framework Implementation | **Implemented** | WP 5.0D (new — not part of the original `v0.4.0` plan) |
| Command Framework | **Implemented** | WP 4.0 (contract), WP 5.1A (design), WP 5.1B (implementation) (formerly WP 4.7) |
| Platform Security Baseline | **Complete** | WP 5.0S (new — not part of the original `v0.4.0` plan) |
| Diagnostics Improvements | **Implemented** | WP 5.2 (formerly WP 4.8) |
| Developer Experience Improvements | **Implemented** | WP 5.3 (formerly WP 4.9) |

**Total: 27 features tracked across all three phases (Verified by direct
row count) — 27 Implemented/Complete (including 3 audit/review
milestones with no code by design: Platform Services Architecture
Review, Academy & Documentation Baseline Audit, Platform Security
Baseline), 0 Not Started — every feature originally scoped across
`v0.3.0`, `v0.4.0`, and `v0.5.0`'s renumbered plan is now complete — see
`docs/releases/v0.5.0/ReleasePlan.md`'s "A Note on Renumbering".**

## Cross-Reference Check

Every "Implemented" feature above corresponds to exactly one row in
`Platform Services Register.md` marked Implemented, and one or more rows
in `ADR Register.md`/`Rejected Designs Register.md` where applicable — no
feature is claimed Implemented here without a corresponding platform
service, test, and Academy retrospective all agreeing.
