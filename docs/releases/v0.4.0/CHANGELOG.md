# TempestOS v0.4.0 — Changelog

## Status

**Unreleased.** This release is in planning; no work package has begun
implementation. Entries below are added as each work package actually
lands — not written in advance as predictions. Compare against
`WorkPackages.md` for what is planned but not yet reflected here.

---

## [Unreleased]

### Added

_Nothing implemented yet. Planned additions, per `WorkPackages.md`
(revised numbering and order, following planning review):_

- Platform Contracts (WP 4.0)
- Module SDK (WP 4.1)
- Plugin Manifest (WP 4.2)
- Sample Module (WP 4.3) — built early, as a living reference module
  later work packages extend, not a final integration proof.
- Event Bus (WP 4.4)
- Background Services (WP 4.5)
- Navigation Architecture (WP 4.6A), then Navigation Implementation
  (WP 4.6B) — architecture-only phase first, per its own risk profile.
- Command Framework (WP 4.7)
- Diagnostics Improvements (WP 4.8)
- Developer Experience Improvements (WP 4.9)

### Changed

_Nothing yet._

### Fixed

_Nothing yet._

### Architecture Decision Records

- **ADR-0020** — The Event Bus Is a DI-Public Platform Service. Decided
  during planning, before implementation (WP 4.0/4.4).
- **ADR-0021** — Background Service Failures Are Isolated by Default;
  Criticality Is Opt-In. Decided during planning, before implementation
  (WP 4.0/4.5).
- **ADR-0022** — Navigation and Commands Are Orthogonal Platform Services.
  Decided during planning, before implementation (WP 4.0/4.6A/4.7).
- **ADR-0023** — Platform Layering: Dependencies Flow Downward Only.
  Decided during planning; applies platform-wide, not only to this
  release (see `docs/releases/FOUNDATION.md`).
- Expected, not yet written: Plugin Manifest's phase-sequence placement
  (WP 4.2) and Navigation's `Tempest.Core` placement (WP 4.6A) — see
  `Architecture.md`.

---

## How This File Is Maintained

Each work package adds its own entries here as part of its own Definition
of Done (`ReleaseChecklist.md`), under the correct `Added`/`Changed`/
`Fixed` heading, referencing its work package number (e.g. "WP 4.4 — Event
Bus: added `IEventBus` with per-subscriber failure isolation."). This file
is not written retroactively at release time from memory — it is a running
record, exactly like `Risks.md`.
