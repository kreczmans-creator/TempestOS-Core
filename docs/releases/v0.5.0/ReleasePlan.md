# TempestOS v0.5.0 — Release Plan

## Status

**In progress.** `WP 5.0A` (Navigation Framework Architecture),
`WP 5.0B` (Navigation Framework Implementation), and `WP 5.0C` (Shell &
Composition Framework Architecture) are complete. `WP 5.0D` onward have
not begun. This document exists to scope
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

Design and implement the Navigation Framework, design and implement the
Shell that lets `Tempest.App` finally consume it, then build the
remaining Developer Experience capabilities `v0.4.0` deliberately
deferred — Command Framework, Diagnostics Improvements, and Developer
Experience tooling itself — without weakening or redesigning any
architectural guarantee the Platform Foundation (`v0.4.0`) established.

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
- Shell & Composition Framework Architecture (`WP 5.0C`) — **complete**.
- Shell & Composition Framework Implementation (`WP 5.0D`).
- Command Framework (`WP 5.1`).
- Diagnostics Improvements (`WP 5.2`).
- Developer Experience Improvements (`WP 5.3`).

`WP 5.0C`/`WP 5.0D` were not part of this release's original scope list —
Repository Investigation during `WP 5.0C` confirmed `Tempest.App` still
does not consume the platform at all (a gap `WP 5.0A` first disclosed),
and this release's own scope grew, deliberately, to include designing and
building the thing that finally closes it, rather than leaving Navigation
implemented with no real consumer for another full release.

## Explicitly Out of Scope

- Any change to the Runtime Host's state machine, failure model, or
  disposal contract (`HostState`, ADR-0012, ADR-0013, ADR-0019), Platform
  Services' existing public contracts, or any ADR already Accepted in
  `v0.3.0`/`v0.4.0` — the Platform Foundation is stable and is being
  built *on*, not revisited, absent a specific, documented engineering
  reason (`docs/governance/Future Work Package Guidelines.md`).
- A rendering/UI *implementation* of any kind in `WP 5.0C` specifically —
  `WP 5.0A`'s own architecture keeps rendering out of `Tempest.Core`, and
  `WP 5.0B` implemented exactly that boundary; `WP 5.0C` designs, but does
  not build, the Shell that will eventually render into `Tempest.App` —
  that implementation is `WP 5.0D`'s own, explicitly scheduled, concern.
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
  Five are already decided ahead of implementation: `ADR-0031` (Navigation
  belongs in `Tempest.Core`; rendering is an application responsibility),
  `ADR-0032` (Navigation is DI-public, registered imperatively, reusing
  the Event Bus), `ADR-0033` (the Shell is a composition root, not a
  module or hosted service), `ADR-0034` (`ITempestHost` exposes a
  read-only service resolution surface), and `ADR-0035` (the Shell owns
  page/view construction, independent of the DI container).
- The Academy gains a retrospective for every Work Package in this
  release, exactly as every prior release's Work Packages did.

## Related Documents

`WorkPackages.md` · `docs/architecture/Navigation Framework
Architecture.md` · `docs/architecture/Shell & Composition Framework
Architecture.md` · `ADR-0022`, `ADR-0031`, `ADR-0032`, `ADR-0033`,
`ADR-0034`, `ADR-0035` · `docs/releases/v0.4.0/ReleasePlan.md` ·
`docs/releases/v0.4.0/WorkPackages.md` · `../FOUNDATION.md` ·
`../v0.4.0.md`.
