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
| **Last Reviewed** | 2026-07-28 (WP 5.1A, Command Framework Architecture). |
| **Related Documents** | `Platform Services Register.md`; `Decision Register.md`; `Documentation Register.md` (the superset index, including Academy and release docs). |
| **Related ADRs** | All 38 — every architecture document in this register is the realisation of one or more ADRs. |
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
| `Command Framework Architecture.md` | `ICommandDispatcher`/`ICommandRegistry` dispatch/discovery/registration/failure design (ADR-0036/0037/0038) | **Designed** — implementation pending | WP 5.1A (design); WP 5.1B (implementation, not yet started) |
| `docs/releases/v0.4.0/Architecture.md` | The v0.4.0 release's own architecture review, decisions, and reuse map | Living document (release-scoped) | v0.4.0 planning; updated across the release |
| `docs/releases/FOUNDATION.md` | Permanent, cross-release engineering constitution — what must never change | Permanent | Established at v0.1.0-era stabilisation; not release-scoped |

**Total: 21 documents (19 under `docs/architecture/`, 2 under `docs/releases/`).**

## Coverage by Implementation Status

| Status | Count | Documents |
|---|---|---|
| Implemented | 15 | Runtime Host Architecture, Host Lifecycle, Runtime State Machine, Startup Sequence, Shutdown Sequence, Failure Behaviour, Ownership Matrix, Platform Version, Plugin Manifest Architecture, Module Dependency Injection Architecture, Event Bus Architecture, Background Services Architecture, Sample Module Architecture, Navigation Framework Architecture, Shell & Composition Framework Architecture |
| Living document (continuously updated, not phase-gated) | 3 | Platform Service Map, Engineering Glossary, Rejected Designs |
| Living document (release-scoped) | 1 | `docs/releases/v0.4.0/Architecture.md` |
| Permanent (cross-release) | 1 | `docs/releases/FOUNDATION.md` |

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
