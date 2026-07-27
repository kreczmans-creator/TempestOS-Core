# TempestOS v0.5.0 — Release Plan

## Status

**In progress.** `WP 5.0A` (Navigation Framework Architecture) and
`WP 5.0B` (Navigation Framework Implementation) are complete. `WP 5.1`
onward have not begun. This document exists to scope
the release before any code is written — per `docs/releases/
FOUNDATION.md`, architecture precedes implementation for anything
non-trivial. `docs/releases/v0.5.0/WorkPackages.md` is the living record
of what has actually shipped; this document is not re-litigated as work
lands.

## Branch

`feature/v0.5.0-developer-experience`, cut from `main` at the `v0.4.0`
tag. No work occurs directly on `main` (Engineering Governance §1.3).

## Release Theme

**Developer Experience.** `v0.4.0` ("Platform Foundation") proved the
Runtime Host, Platform Services, Dependency Injection, Plugin
Infrastructure, the Event Bus, and Hosted Services are stable platform
capabilities. `v0.5.0` begins transforming TempestOS from an engineering
*platform* into an engineering *application* — the first release where a
human-facing surface (navigation between built-in pages, modules, and
plugins) is designed at all, not merely a runtime other things load into.

## Objective

Design and implement the Navigation Framework, then build the remaining
Developer Experience capabilities `v0.4.0` deliberately deferred —
Command Framework, Diagnostics Improvements, and Developer Experience
tooling itself — without weakening or redesigning any architectural
guarantee the Platform Foundation (`v0.4.0`) established.

## A Note on Renumbering

This release renumbers the four Work Packages `docs/releases/v0.4.0/
WorkPackages.md` originally scoped as `WP 4.6A` through `WP 4.9`, then
rescoped out of `v0.4.0` during that release's own Release Engineering
(see `v0.4.0/ReleasePlan.md`'s "Scope" section). Nothing about their own
objective, scope, or dependencies changed — only the Work Package number,
to reflect that they now belong to `v0.5.0`, not `v0.4.0`:

| Former number (`v0.4.0` plan) | Current number (`v0.5.0`) |
|---|---|
| `WP 4.6A` — Navigation Architecture | `WP 5.0A` — Navigation Framework Architecture |
| `WP 4.6B` — Navigation Implementation | `WP 5.0B` — Navigation Framework Implementation |
| `WP 4.7` — Command Framework | `WP 5.1` — Command Framework |
| `WP 4.8` — Diagnostics Improvements | `WP 5.2` — Diagnostics Improvements |
| `WP 4.9` — Developer Experience Improvements | `WP 5.3` — Developer Experience Improvements |

`docs/releases/v0.4.0/WorkPackages.md`'s own entries for `WP 4.6A`
through `WP 4.9` remain in place, each carrying a redirect note to this
table — per this project's own "never delete, mark superseded" governance
convention (the same treatment a superseded ADR or a retired risk
already receives).

## Scope

See `docs/releases/v0.5.0/WorkPackages.md` for the full breakdown, in
implementation order:

- Navigation Framework Architecture (`WP 5.0A`) — **complete**.
- Navigation Framework Implementation (`WP 5.0B`) — **complete**.
- Command Framework (`WP 5.1`).
- Diagnostics Improvements (`WP 5.2`).
- Developer Experience Improvements (`WP 5.3`).

## Explicitly Out of Scope

- Any change to the Runtime Host's state machine, failure model, or
  disposal contract (`HostState`, ADR-0012, ADR-0013, ADR-0019), Platform
  Services' existing public contracts, or any ADR already Accepted in
  `v0.3.0`/`v0.4.0` — the Platform Foundation is stable and is being
  built *on*, not revisited, absent a specific, documented engineering
  reason (`docs/governance/Future Work Package Guidelines.md`).
- A rendering/UI implementation of any kind. `WP 5.0A`'s own architecture
  explicitly keeps rendering out of `Tempest.Core`, and `WP 5.0B`
  implemented exactly that boundary — `Tempest.App` was left untouched.
  `Tempest.App`'s own eventual navigation-aware console rendering remains
  unscheduled, a future Work Package's own concern, not a platform
  capability.
- A permission/authorization model — `RD-0033` defers this explicitly
  until a real authentication/authorization concept exists anywhere in
  this platform.

## Success Criteria

- Every Work Package in this release's scope reaches its own stated
  Acceptance Criteria.
- The Build Gate and Test Gate (Engineering Governance §2) pass on every
  commit, exactly as they did throughout the Platform Foundation.
- No Platform Foundation ADR is silently contradicted; every genuinely
  new architectural decision this release requires gets its own ADR.
  Two are already decided ahead of implementation: `ADR-0031` (Navigation
  belongs in `Tempest.Core`; rendering is an application responsibility)
  and `ADR-0032` (Navigation is DI-public, registered imperatively,
  reusing the Event Bus).
- The Academy gains a retrospective for every Work Package in this
  release, exactly as every prior release's Work Packages did.

## Related Documents

`WorkPackages.md` · `docs/architecture/Navigation Framework
Architecture.md` · `ADR-0022`, `ADR-0031`, `ADR-0032` ·
`docs/releases/v0.4.0/ReleasePlan.md` · `docs/releases/v0.4.0/
WorkPackages.md` · `../FOUNDATION.md` · `../v0.4.0.md`.
