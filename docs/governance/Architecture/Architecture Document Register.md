# Architecture Document Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Architecture Document Register |
| **Purpose** | The complete index of every standing architecture document TempestOS maintains — what each one covers, which Work Package(s) produced or last touched it, and its current implementation status. |
| **Scope** | Every file in `docs/architecture/`, plus `docs/releases/v0.4.0/Architecture.md` (the release-scoped architecture review) and `docs/releases/FOUNDATION.md` (the permanent, cross-release constitution). |
| **Owner** | Project Maintainer. |
| **Source of Truth** | The documents themselves, under `docs/architecture/` and `docs/releases/`. |
| **Review Frequency** | Each document is updated as part of the Definition of Done for any Work Package that changes the subsystem it describes (Engineering Governance §6, extended explicitly to `Platform Service Map.md` and, by the same reasoning, every other architecture document). |
| **Last Reviewed** | 2026-08-12 (WP 12.1A Architecture Review Follow-Up) — **full re-derivation of the Total and Coverage sections, not a narrow correction**: closing a finding from this Work Package's own architecture review, every figure in both sections was recomputed directly from the Entries table above (26 rows, re-counted directly) and cross-checked against the physical file system (`ls docs/architecture/`: 24 files), rather than carried forward by incrementing a prior figure. Found and corrected two compounding, pre-existing errors: the Total line had read "24 documents (22 under `docs/architecture/`...)" against an Entries table that actually has 26 rows (24 + 2) — an error that predated this Work Package, inherited by incrementing an already-stale base figure rather than re-deriving it; the Coverage table's own "Implemented" row had read "18," with a 17-item list, against a true count of 20 (three Implemented documents — `Fault Injection & Validation Architecture.md`, `Desktop Composition Architecture.md`, `Desktop Command & Event Wiring Architecture.md` — were previously disclosed as missing from this row's own list without the count itself ever being corrected; both the list and the count are now accurate). All five Coverage buckets (20 + 1 + 3 + 1 + 1 = 26) now sum to exactly the Entries table's own row count, independently re-derived, not reconciled by adjusting one number to match the other. Documentation only; no `src/`/`tests/` files touched. Previously reviewed 2026-08-12 (WP 12.1A, Classification & Relationship Vocabulary Safety Net Architecture) — narrow correction only, not a full re-derivation: adds `Classification & Relationship Vocabulary Safety Net Architecture.md` (new) — realises `WP11.0A` Finding `A-6`, auditing every Kind/`Classification`/`RelationshipKind` vocabulary platform-wide and producing `ADR-0105` (declare-once discipline, a new Engineering Vocabulary Register, an additive consistency test — never validation at write time). Architecture only, no code, on its own branch (`feature/v0.12.0-classification-relationship-vocabulary-safety-net`). Previously reviewed 2026-08-12 (WP 12.4A, Desktop Command & Event Wiring Architecture) — narrow correction only, not a full re-derivation: adds `Desktop Command & Event Wiring Architecture.md` (new) — a second directly-commissioned Work Package (`WP 12.3A`/`WP 12.3B`'s own precedent), reviewing Desktop command/event wiring after `WP 12.0B`'s decomposition, and formally evaluating six candidate cross-collaborator communication mechanisms on their own merits. Produces `ADR-0104`: direct delegates remain the default, typed callback interfaces sanctioned narrowly (three or more bundled callbacks), a Desktop-local Mediator/Command Dispatcher/Event Dispatcher each explicitly rejected. Architecture only, no code, on its own branch (`feature/v0.12.0-desktop-command-event-wiring`). Previously reviewed 2026-08-12 (WP 12.0B, Desktop Composition Root Decomposition Implementation) — narrow correction only, not a full re-derivation: flips `Desktop Composition Architecture.md`'s own Status from Designed to Implemented — `MainWindow` (1,556→544 lines; ~1,000→~370-line constructor) and `EngineeringCockpit` (1,398→575 lines) both now realise `ADR-0103` for real, via nine and six `Tempest.Desktop.Composition`/per-discipline collaborators respectively; realises `WP11.0A` Finding `A-1` in full. Previously reviewed 2026-08-12 (WP 12.0A, Desktop Composition Root Decomposition Architecture) — narrow correction only, not a full re-derivation (same disclosed staleness as the entry below): adds `Desktop Composition Architecture.md` (new, `ADR-0103`) — architecture only, no code. Previously reviewed 2026-08-12 (WP 12.3B, Fault-Injection Validation Framework Implementation) — narrow correction only, not a full re-derivation (this register has otherwise gone unreviewed since `WP 5.2`, a large, pre-existing, disclosed-here-not-fixed staleness spanning every release since — `Related ADRs`'s own "All 39" below is one direct symptom): adds `Fault Injection & Validation Architecture.md` (new, ADR-0102). Previously reviewed 2026-07-28 (WP 5.2, Diagnostics Improvements) — corrected a stale marker found during this Work Package's own repository review: `Command Framework Architecture.md` still read "implementation pending... not yet started" despite `WP 5.1B` having completed it; also adds `Diagnostics Architecture.md`. |
| **Related Documents** | `Platform Services Register.md`; `Decision Register.md`; `Documentation Register.md` (the superset index, including Academy and release docs). |
| **Related ADRs** | All 39 — every architecture document in this register is the realisation of one or more ADRs. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/` mirrors much of this register's own subject matter at a teaching, rather than reference, depth — see the Academy Register for the pairing. |
| **Coverage Status** | Complete. |

---

## Entries

| Document | Covers | Status | Primary Work Package(s) |
|---|---|---|---|
| `Runtime Host Architecture.md` | The Host's responsibilities, non-responsibilities, threading model, future extensibility | Implemented | WP 2.7 (design), WP 2.7B (implementation), updated WP 4.2, WP 4.5 |
| `Host Lifecycle.md` | Every startup/shutdown phase, in order, with entry/exit/failure criteria | Implemented | WP 2.7/2.7B; extended WP 4.2C (Plugin phases 3.1/3.2), WP 4.5 (Hosted Service phases 8.1/10.1) |
| `Runtime State Machine.md` | The Host's seven-state machine and legal/illegal transitions | Implemented | WP 2.7/2.7B; updated WP 4.2, WP 4.5 |
| `Startup Sequence.md` | The startup half of the Host's lifecycle, as a standalone sequence | Implemented | WP 2.7/2.7B |
| `Shutdown Sequence.md` | Controlled shutdown and post-fault teardown, side by side | Implemented | WP 2.7/2.7B |
| `Failure Behaviour.md` | Every named failure mode, classified Host-fatal vs. isolated | Implemented | WP 2.7/2.7B; extended WP 4.2 (Plugin), WP 4.5 (Hosted Service) |
| `Ownership Matrix.md` | Who owns every significant runtime object | Implemented | WP 2.7B; extended WP 4.2A, WP 4.4D, WP 4.5 |
| `Platform Service Map.md` | Living, service-by-service index: responsibility, dependencies, consumers, lifecycle | Implemented (living document) | Introduced WP 2.6-era (commit `5bbd75f`); updated by every subsequent Work Package that adds/changes a platform service |
| `Engineering Glossary.md` | Project vocabulary, alphabetical, cross-referenced | Living document | Introduced WP 2.7-era (commit `2ea9c3a`); updated by every Work Package introducing new terminology |
| `Rejected Designs.md` | The permanent Rejected Designs Log | Living document | Introduced WP 4.0-era (commit `466334c`); see `Rejected Designs Register.md` |
| `Platform Version.md` | Runtime platform version infrastructure design | Implemented | WP 4.2A |
| `Plugin Manifest Architecture.md` | Plugin manifest schema, discovery, loading design | Implemented | WP 4.2 (design and implementation), WP 4.2B, WP 4.2C |
| `Module Dependency Injection Architecture.md` | `ModuleMetadataAttribute` design (ADR-0027) | Implemented | WP 4.4A (design), WP 4.4B (implementation) |
| `Event Bus Architecture.md` | `IEventBus`/`EventBus` dispatch/subscription/failure design (ADR-0028) | Implemented | WP 4.4 (design), WP 4.4D (implementation) |
| `Background Services Architecture.md` | Hosted service discovery/ownership/orchestration/Host Lifecycle placement design (ADR-0029/0030) | Implemented | WP 4.5 (design), WP 4.5 (implementation) |
| `Sample Module Architecture.md` | `ClockModule`/`ClockLifecycleObserverModule` design | Implemented | WP 4.3 (design and implementation) |
| `Navigation Framework Architecture.md` | Navigation model, ownership, registration, and rendering boundary design (ADR-0031/0032) | Implemented | WP 5.0A (design), WP 5.0B (implementation) |
| `Shell & Composition Framework Architecture.md` | The application shell's composition-root role, `ITempestHost.Services`, and page/view ownership design (ADR-0033/0034/0035) | Implemented | WP 5.0C (design), WP 5.0D (implementation) |
| `Command Framework Architecture.md` | `ICommandDispatcher`/`ICommandRegistry` dispatch/discovery/registration/failure design (ADR-0036/0037/0038) | Implemented | WP 5.1A (design), WP 5.1B (implementation) |
| `Diagnostics Architecture.md` | `CompositeLogSink`/`IDiagnosticsProvider` fan-out/read-only-projection design (ADR-0039) | Implemented | WP 5.2 (design and implementation) |
| `Fault Injection & Validation Architecture.md` | Fault-injection module isolation: `Tempest.Validation` project placement, `IFaultInjectionModule`/`ReflectionFrameworkDiscoveryService`/`ITempestHostBuilder` design (ADR-0102) | Implemented | WP 12.3A (design), WP 12.3B (implementation) |
| `Desktop Composition Architecture.md` | The general "Composition Roots Own Collaborators" pattern (`ADR-0103`) — composition-root/collaborator responsibilities, ownership/lifetime, construction/dependency rules; motivated by, but not scoped to, `MainWindow`/`EngineeringCockpit` | Implemented | WP 12.0A (design); WP 12.0B (implementation) |
| `Desktop Command & Event Wiring Architecture.md` | Desktop layer command/event wiring after `WP 12.0B`'s decomposition — command ownership (four mechanisms), event ownership/lifetime/disposal, remaining orchestration hotspots, and a full six-option evaluation of cross-collaborator communication mechanisms (`ADR-0104`) | Implemented | WP 12.4A (design); WP 12.4B (implementation) |
| `Classification & Relationship Vocabulary Safety Net Architecture.md` | Platform-wide audit of every Kind/`Classification`/`RelationshipKind` vocabulary across `Tempest.Core`/`Tempest.App`/`Tempest.Desktop`, confirmed duplication (`VerifiedByRelationshipKind`, Mechanical's own zero declared Kind constants), and a canonical declare-once/governance-register/consistency-test model (`ADR-0105`), realising `WP11.0A` Finding `A-6` | Designed | WP 12.1A (design only; `WP 12.1B` implementation named, not yet commissioned) |
| `docs/releases/v0.4.0/Architecture.md` | The v0.4.0 release's own architecture review, decisions, and reuse map | Living document (release-scoped) | v0.4.0 planning; updated across the release |
| `docs/releases/FOUNDATION.md` | Permanent, cross-release engineering constitution — what must never change | Permanent | Established at v0.1.0-era stabilisation; not release-scoped |

**Total: 26 documents (24 under `docs/architecture/`, 2 under `docs/releases/`) — re-derived directly from the Entries table above (a direct row count, `grep -c "^| \`"`) and cross-checked against the physical file system (`ls docs/architecture/`: 24 files), not carried forward from any prior figure.**

## Coverage by Implementation Status

| Status | Count | Documents |
|---|---|---|
| Implemented | 20 | Runtime Host Architecture, Host Lifecycle, Runtime State Machine, Startup Sequence, Shutdown Sequence, Failure Behaviour, Ownership Matrix, Platform Version, Plugin Manifest Architecture, Module Dependency Injection Architecture, Event Bus Architecture, Background Services Architecture, Sample Module Architecture, Navigation Framework Architecture, Shell & Composition Framework Architecture, Command Framework Architecture, Diagnostics Architecture, Fault Injection & Validation Architecture, Desktop Composition Architecture, Desktop Command & Event Wiring Architecture (**corrected, `WP 12.1A`'s own follow-up**: the count and list above were re-derived by direct re-count against the Entries table, rather than carried forward — the prior "18" figure, and its own 17-item list, both undercounted; `Fault Injection & Validation Architecture.md`, `Desktop Composition Architecture.md`, and `Desktop Command & Event Wiring Architecture.md` are now included, matching their own Implemented status in the Entries table above) |
| Designed (not yet implemented) | 1 | `Classification & Relationship Vocabulary Safety Net Architecture.md` (`WP 12.1A` — architecture only; `WP 12.1B` implementation named in the roadmap, not yet commissioned) |
| Living document (continuously updated, not phase-gated) | 3 | Platform Service Map, Engineering Glossary, Rejected Designs |
| Living document (release-scoped) | 1 | `docs/releases/v0.4.0/Architecture.md` |
| Permanent (cross-release) | 1 | `docs/releases/FOUNDATION.md` |

**20 + 1 + 3 + 1 + 1 = 26 — matches the Entries table's own row count exactly, both independently re-derived (direct table row count; direct per-row Status-column classification), not reconciled by adjusting one to match the other.**

Every document in this register marked Implemented describes a subsystem
this repository's Platform Services Register also lists as
**Implemented**, with the Shell as its own disclosed exception: it is
`Tempest.App`'s own architecture, not a platform service, so it is
deliberately absent from `Platform Services Register.md` entirely rather
than listed there. **Verified** by direct cross-check against `Platform
Services Register.md`.

## Cross-Reference Check

- Every architecture document is cited by at least one ADR, one Work
  Package retrospective, and one entry in `Platform Service Map.md` or
  the Engineering Glossary — confirmed by direct grep of each document's
  own inbound references.
- No stale "designed, not yet implemented" marker remains for any
  document that has since actually been implemented — `Navigation
  Framework Architecture.md`'s own marker was updated to Implemented in
  the same commit that moved `Platform Services Register.md`'s Navigation
  row to Implemented, per this register's own cross-check discipline.
  `Shell & Composition Framework Architecture.md`'s own marker was
  updated to Implemented in this same commit, for the identical reason.
  **One stale marker was found and corrected by `WP 5.2`'s own repository
  review**: `Command Framework Architecture.md` still read "implementation
  pending... not yet started" despite `WP 5.1B` having completed it two
  Work Packages earlier — neither `WP 5.1B` nor any review since had
  updated this specific register row, even though `Feature Register.md`
  and `Traceability Matrix.md` were both updated correctly at the time.
  Corrected here, consistent with this project's own practice of fixing
  pre-existing governance drift found along the way, not only the drift a
  Work Package's own brief names.
