# TempestOS v0.4.0 — Release Plan

## Status

**Released.** `v0.4.0` is rescoped and shipped as **"Platform
Foundation"** — `WP 4.0` through `WP 4.5B` — Platform Contracts, Module
SDK, Plugin Manifest, Sample Module, Event Bus, Background Services, two
milestone reviews (`WP 4.2D`, `WP 4.4F`), the Governance Register
Baseline (`WP 4.5A`), and the Foundation Closeout (`WP 4.5B`). This is a
deliberate, documented rescoping (see "Scope," below) performed as part
of Release Engineering for this version: `WP 4.6A` (Navigation
Architecture) onward — Navigation, Command Framework, Diagnostics
Improvements, and Developer Experience Improvements — do **not** ship in
this release. They were part of this document's own original scope list
below when first written, before implementation began; the Platform
Foundation Completion Report (`docs/releases/Platform Foundation
Completion Report.md`) confirms Remaining Foundation Work is NONE, which
is what this release actually closes. See `WorkPackages.md` and
`CHANGELOG.md` for current, authoritative status of what shipped in
`v0.4.0` versus what remains for the next milestone.

## Branch

`feature/v0.4.0-platform-services`, cut from `main` at the `v0.3.0` tag
(`f2176d7`). No work occurs directly on `main` — see Engineering Governance
§1.3.

## Release Theme

**From Runtime to Platform.** v0.3.0 established a single, working entry
point — the Runtime Host — and proved that six independently-built platform
services could be assembled without redesigning any of them. v0.4.0 does not
touch that foundation. It builds *on* it: the runtime gains the capabilities
that turn "a platform that can run modules" into "a platform other people can
build on" — a real SDK, a manifest format, cross-module communication, a
command surface, navigation, background execution, and the developer
experience to make all of it approachable.

## Objective

Transform TempestOS from a runtime into an extensible platform, without
weakening or redesigning any architectural guarantee the Runtime Foundation
(v0.3.0) established.

## Scope

See `WorkPackages.md` for the full breakdown. Revised following planning
review: a foundational contracts-first work package now precedes
everything else, and the Sample Module moved from a closing proof to an
early, living reference the rest of the release builds against and
extends.

**Shipped in `v0.4.0` ("Platform Foundation"), in implementation order:**

- Platform Contracts
- Module SDK
- Plugin Manifest
- Sample Module
- Event Bus
- Background Services
- Two milestone reviews (Platform Services Architecture Review, Academy &
  Documentation Baseline Audit)
- Governance Register Baseline
- Foundation Closeout

**Rescoped out of `v0.4.0`, deferred to the next milestone** (this
document's own scope list originally named these before implementation
began; Release Engineering for `v0.4.0` formally deferred them once the
Foundation-phase work above reached Remaining Foundation Work: NONE — see
`docs/releases/Platform Foundation Completion Report.md`):

- Navigation Architecture, then Navigation Implementation
- Command Framework
- Diagnostics Improvements
- Developer Experience Improvements

None of the four deferred items has begun (`WP 4.6A` is the next planned
Work Package — see `PROJECT_STATUS.md`); deferring them is a scope
decision, not a schedule slip against any commitment already made to
ship them in this specific version.

## Explicitly Out of Scope

- Any change to the Runtime Host's state machine, failure model, or
  disposal contract (`HostState`, ADR-0012, ADR-0013, ADR-0019) unless a
  work package's own planning surfaces a compelling, documented engineering
  reason — see `Architecture.md`.
- Any change to Configuration, Logging, Discovery, Registration, Dependency
  Injection, or Lifecycle's existing public contracts. This release
  extends the platform *around* these six services; it does not reopen
  them.
- Restart support, multiple concurrent hosts, and anything else already
  explicitly decided against in the Runtime Foundation's ADRs (see
  `Architecture.md` for the specific list).

## Success Criteria

- Every work package within this release's shipped scope (`WP 4.0`
  through `WP 4.5B` — see "Scope," above) reaches its own stated
  Acceptance Criteria. The four rescoped-out work packages are not a
  criterion for this release — they are the next milestone's own scope.
- The Build Gate and Test Gate (Engineering Governance §2) pass on every
  commit, exactly as they did throughout v0.3.0.
- No Runtime Foundation ADR is silently contradicted; every genuinely new
  architectural decision this release requires gets its own ADR, following
  the same discipline as the Runtime Foundation. Four are already decided
  ahead of implementation: ADR-0020 (Event Bus is DI-public), ADR-0021
  (background service failures are isolated by default), ADR-0022
  (Navigation and Commands are orthogonal), and ADR-0023 (platform-wide
  dependency layering flows downward only).
- The Academy (`docs/academy/`) gains a retrospective for every work
  package in this release, exactly as WP 2.1 through WP 2.7B did.

## Related Documents

`WorkPackages.md` · `Architecture.md` · `Risks.md` · `Testing.md` ·
`ReleaseChecklist.md` · `CHANGELOG.md` · `../FOUNDATION.md` ·
`../v0.3.0.md`.
