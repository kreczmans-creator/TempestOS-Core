# Feature Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Feature Register |
| **Purpose** | The capability-level index of what TempestOS actually delivers, one row per major feature, cross-referencing the Work Package(s) that built it — the delivery lens, as distinct from the Platform Services Register's architectural lens. |
| **Scope** | Every major capability named in `docs/releases/v0.3.0.md` through `docs/releases/v0.15.0/WorkPackages.md`/`Release Notes.md` (extended `WP 16.2A`, previously `v0.3.0`–`v0.5.0` only). |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Each release's own `docs/releases/vX.Y.0/WorkPackages.md` and `ReleaseNotes.md`/`Release Notes.md`. |
| **Review Frequency** | Updated whenever a Work Package delivers or changes a major capability. |
| **Last Reviewed** | 2026-09-04 (WP 16.2A, Register and Status Currency) — added nine new sections, one per release, `v0.6.0` through `v0.15.0` (this register had not been reviewed since `WP 5.3`/`v0.5.0`); see the "Backfill note" immediately above the Cross-Reference Check for method and disclosed limitations. Total feature-row count 27 → 27 + 74 = 101 (direct row count, `grep -c` across the nine new section tables). See `docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md`. Previously reviewed 2026-07-28 (WP 5.3, Developer Experience Improvements) — Developer Experience Improvements row corrected from "Not started" to Implemented, closing this release's own Feature Register. |
| **Related Documents** | `Platform Services Register.md`; `Release Register.md`; `Traceability Matrix.md`. |
| **Related ADRs** | See `Platform Services Register.md` for the full per-service ADR list. |
| **Related Academy Articles** | See `Academy Register.md`'s "03 Work Packages" table. |
| **Coverage Status** | **Complete through `v0.5.0`; extended `WP 16.2A` through `v0.15.0` at capability-row granularity** (see the "Backfill note" for the disclosed condensation). |

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

## Platform Services Programme (v0.6.0, Released 2026-07-29)

| Feature | Status | Work Package |
|---|---|---|
| Reporting (`IReportingService`/`IReportTemplate<T>`) | Implemented | WP 6.0 |
| Identity & Permissions (`IIdentityService`/`IPermissionEvaluator`) | Implemented | WP 6.1 |
| Notifications (`NotificationDispatcher`) | Implemented | WP 6.2 |
| REST API (`RestApiHostedService`) | Implemented | WP 6.3 |
| Settings & Persistence (`ISettingsProvider`/`IPersistenceStore`) | Implemented | WP 6.4 |
| Audit (`IAuditRecorder`/`IAuditQuery`) | Implemented | WP 6.5 |
| Licensing (`ILicenseValidator`/`ILicenseProvider`) | Implemented | WP 6.6 |
| Export/Import (`IExportService`/`IImportService`) | Implemented | WP 6.7 |
| Platform Certification (Product Approval) | Complete | WP 6.8 |

## Engineering Foundation & Requirements Engine (v0.7.0, Released 2026-07-30)

| Feature | Status | Work Package |
|---|---|---|
| Engineering Data Model (`IEngineeringDocumentStore`) | Implemented | WP 7.1A |
| Units & Quantities (`Quantity<TDimension>`) | Implemented | WP 7.1B |
| Materials (`IMaterialCatalog`) | Implemented | WP 7.1C |
| Engineering Calculations (`ICalculationEngine`) | Implemented | WP 7.1D |
| Verification (`IVerificationService`) | Implemented | WP 7.1E |
| Engineering Core Certification | Complete | WP 7.1F |
| Strategic Programme Selection (Requirements & Verification chosen) | Complete | WP 7.2A |
| Requirements Engine (`IRequirementsService`) | Implemented | WP 7.3A |
| Release Preparation & Product Approval | Complete | WP 7.4.0 |

## Engineering Workspace & Domain (v0.8.0, Released 2026-07-31)

| Feature | Status | Work Package |
|---|---|---|
| Engineering Workspace (five-region shell, replacing console `TempestShell`) | Implemented | WP 8.1A/WP 8.1B/WP 8.1C |
| Engineering Cockpit (default landing screen) | Implemented | WP 8.1C |
| Engineering Domain (`Tempest.Core.EngineeringDomain`, 83 contracts, 38 canonical object classes) | Implemented | WP 8.2B (contracts), WP 8.2C (implementation) |
| `v0.8.0` Product Approval | Complete | WP 8.9.0 |

## Mechanical Foundation — Six Real Disciplines (v0.9.0, Released 2026-08-01)

| Feature | Status | Work Package |
|---|---|---|
| Mechanical Product Structure discipline | Implemented | WP 9.0A/WP 9.0B |
| Requirements Management discipline (Workspace) | Implemented | WP 9.1A |
| Engineering Calculations discipline (Workspace) | Implemented | WP 9.2A |
| Verification Management discipline (Workspace) | Implemented | WP 9.3A |
| Engineering Documents discipline (Workspace) | Implemented | WP 9.4A |
| Manufacturing discipline (Workspace), cross-discipline provider/command reuse | Implemented | WP 9.5A |
| Platform Service Register Reconciliation (Engineering Foundation gap closed) | Complete | WP 9.8B |
| `v0.9.0` Product Approval | Complete | WP 9.9.0/WP 9.9.1 |

## Desktop Application & UX Modernisation (v0.10.0, Released 2026-08-08)

| Feature | Status | Work Package |
|---|---|---|
| Real graphical desktop application (`Tempest.Desktop`, `ADR-0092`) | Implemented | WP 10.0B |
| Engineering Cockpit Implementation (live dashboard) | Implemented | WP 10.1A |
| Workspace Modernisation & Docking/Layouts | Implemented | WP 10.2A/WP 10.2B |
| Engineering Object Editors (generic engine, five discipline sections) | Implemented | WP 10.3A |
| Digital Thread Visualisation (graphical relationship graph) | Implemented | WP 10.4A |
| Workspace Visual Polish, Workflow, Commercial UX | Implemented | WP 10.5A/WP 10.5B/WP 10.5C |
| Ribbon, Toolbar & Command Experience | Implemented | WP 10.3B |
| Command Execution & Productivity (Undo/Redo, Macros, Input Binding) | Implemented | WP 10.6A |
| Feature Completion Closeout (final placeholder closure) | Complete | WP 10.6D/WP 10.7A/WP 10.8A |
| `v0.10.0` Engineering Release | Complete | WP 10.9A |

## v1.0 Roadmap & Governance Hardening (v0.11.0, Released 2026-08-11)

| Feature | Status | Work Package |
|---|---|---|
| Platform Architecture & Code Quality Review | Complete | WP 11.0A |
| v1.0 Architecture Roadmap & Release Planning | Complete | WP 11.0B |
| Continuous Integration (`.github/workflows/ci.yml`) | Implemented | WP 11.1A |
| Branch Protection & Engineering Workflow Hardening (`release.yml`) | Implemented | WP 11.1B |
| Governance Health-Check Automation (`FCR-0005`, `governance-healthcheck.ps1`) | Implemented | WP 11.2A |
| Presentation Strategy Review & Consolidation | Complete | WP 11.3A |
| Presentation Strategy Implementation (`TempestShell` retired, `ADR-0101`) | Implemented | WP 11.3B |
| Release Process Corrections | Complete | WP 11.4A/WP 11.4B |
| Governance Currency & Documentation Integrity | Complete | WP 11.5A |
| `v0.11.0` Release Sign-Off | Complete | WP 11.9.0 |

## Composition Root Hardening & Vocabulary Safety (v0.12.0, Released 2026-08-13)

| Feature | Status | Work Package |
|---|---|---|
| Fault Injection & Validation Framework (`Tempest.Validation`) | Implemented | WP 12.3A/WP 12.3B |
| Desktop Composition Root Decomposition (`ADR-0103`) | Implemented | WP 12.0A/WP 12.0B |
| Desktop Command & Event Wiring Review (`ADR-0104`) | Complete | WP 12.4A/WP 12.4B |
| Classification & Relationship Vocabulary Safety Net (`ADR-0105`, Engineering Vocabulary Register) | Implemented | WP 12.1A/WP 12.1B |
| `v0.12.0` Engineering Release(s) | Complete | WP 12.9.0/WP 12.9.2/WP 12.9.4 |

## Plugin & Registration Trust Isolation (v0.13.0/v0.13.1, Released 2026-08-19)

| Feature | Status | Work Package |
|---|---|---|
| Plugin & Registration Trust Isolation Architecture (`ADR-0107`–`ADR-0112`) | Complete | WP 13.0A |
| Plugin Runtime & Composition Root Implementation (manifest v2, `IPluginRegistry`) | Implemented | WP 13.1A |
| Plugin Trust & Capability Enforcement Implementation (signature verification, trust tiers) | Implemented | WP 13.2A |
| Plugin Platform Integration & End-to-End Validation | Complete | WP 13.3A |
| Multi-Assembly / Trust-Denial / Module-Discovery Trust Boundary Remediation | Implemented | WP 13.9.3/WP 13.9.4/WP 13.9.6 |
| `v0.13.0`/`v0.13.1` Engineering Release(s) | Complete | WP 13.9.0/WP 13.12.2 |

## Engineering Object Durability & Workspace Layout (v0.14.0, Released 2026-08-27)

| Feature | Status | Work Package |
|---|---|---|
| Engineering Object Durability & Rehydration (`ADR-0113`) | Implemented | v0.14.0 programme |
| Attachment Content Store (`ADR-0114`) | Implemented | v0.14.0 programme |
| Data-Driven Docking/Layout (`ADR-0095`) | Implemented | v0.14.0 programme |
| Document Viewer (`ADR-0115`) | Implemented | v0.14.0 programme |
| Project Management (Tasks, Milestones, Risks, Issues, Decisions, `ADR-0117`) | Implemented | v0.14.0 programme |
| Canonical Command Binding (`TD-77` remediation programme) | Implemented | v0.14.0 programme |
| `v0.14.0` Engineering Release | Complete | v0.14.0 Engineering Release Report |

*(`v0.14.0`'s own `docs/releases/v0.14.0/WorkPackages.md` does not
number individual Work Packages the way `v0.6.0`–`v0.13.0` do; the row
above cites "v0.14.0 programme" per that folder's own convention —
see `Release Notes.md`'s "Headline changes" for the full attribution.)*

## Governance Currency & Desktop Productisation (v0.15.0, Released 2026-09-04)

| Feature | Status | Work Package |
|---|---|---|
| Desktop Brand Recovery (theme, `ChromeStyles`, icon set) | Implemented | v0.15.0 programme |
| Windows Startup Crash Fix (`TD-121`, Resolved) | Implemented | v0.15.0 programme |
| Desktop Productisation, two phases (navigation, Cockpit, Ribbon fixes) | Implemented | v0.15.0 programme |
| Ribbon Overflow Affordance Fix (`TD-122`, Resolved) | Implemented | v0.15.0 programme |
| Governance Currency Restoration (`Governance Index.md`, `Documentation Register.md`) | Complete | WP 11.5A (found), WP 15.1A/WP 15.1B (formalised, re-verified) |
| `v0.15.0` Release Preparation & Readiness | Complete | WP 15.1A/WP 15.1B |

**Backfill note (`WP 16.2A`).** The nine sections above (`v0.6.0`
through `v0.15.0`) are new — this register had not been reviewed since
`WP 5.3` (`v0.5.0`). Rows are drawn from each release's own
`ReleaseNotes.md`/`Release Notes.md` "Major Capabilities"/"Highlights"
section and `WorkPackages.md`, condensed to one row per major capability
rather than one row per Work Package — a disclosed simplification for
releases with many small Work Packages (`v0.10.0` onward); the full,
authoritative per-Work-Package list for each release remains that
release's own `WorkPackages.md`. Every "Implemented"/"Complete" status
above is drawn directly from that release's own published Release
Notes, not independently re-verified against source for this
backfill — consistent with this register's own delivery-lens purpose
(cross-referencing what shipped, not re-auditing it).

## Cross-Reference Check

Every "Implemented" feature above corresponds to exactly one row in
`Platform Services Register.md` marked Implemented, and one or more rows
in `ADR Register.md`/`Rejected Designs Register.md` where applicable — no
feature is claimed Implemented here without a corresponding platform
service, test, and Academy retrospective all agreeing.
