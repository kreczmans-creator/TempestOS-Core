# TempestOS v0.5.0 — Release Plan

## Status

**Complete.** Every Work Package in this release's final scope —
`WP 5.0A` through `WP 5.0D`, `WP 5.0S` (a dedicated security audit added
mid-release, not part of the original plan below), `WP 5.1A`/`WP 5.1B`
(the Command Framework, split into architecture and implementation
phases after this plan was first written), `WP 5.2`, and `WP 5.3` — is
now complete. This document exists to scope the release before any code
was written — per `docs/releases/FOUNDATION.md`, architecture precedes
implementation for anything non-trivial. `docs/releases/v0.5.0/
WorkPackages.md` is the living record of what has actually shipped, and
is the authoritative source on final scope; this document's own "Status"
and "Scope" sections were last updated during `WP 5.4` (v0.5.0 Release
Candidate & Engineering Sign-Off) to close a drift found during that Work
Package's own repository review — this document had not been revisited
since `WP 5.0C`, three Work Packages before the release actually
finished.

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
implementation order — every item below is now **complete**:

- Navigation Framework Architecture (`WP 5.0A`) — **complete**.
- Navigation Framework Implementation (`WP 5.0B`) — **complete**.
- Shell & Composition Framework Architecture (`WP 5.0C`) — **complete**.
- Shell & Composition Framework Implementation (`WP 5.0D`) — **complete**.
- Platform Security Baseline Audit (`WP 5.0S`) — **complete**. Added
  mid-release, not part of this plan's original scope list — a
  dedicated, formal engineering audit, not a feature Work Package.
- Command Framework Architecture (`WP 5.1A`) — **complete**. Split from
  a single `WP 5.1` entry into architecture (`WP 5.1A`) and
  implementation (`WP 5.1B`) phases, mirroring the `WP 5.0A`/`WP 5.0B`
  and `WP 5.0C`/`WP 5.0D` precedent (`D-018`) — this plan's own single
  `WP 5.1` line, below, predates that split.
- Command Framework Implementation (`WP 5.1B`) — **complete**.
- Diagnostics Improvements (`WP 5.2`) — **complete**.
- Developer Experience Improvements (`WP 5.3`) — **complete**.

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

## Success Criteria — All Met

- Every Work Package in this release's scope reached its own stated
  Acceptance Criteria — see `WorkPackages.md`'s own "— Met" entries.
- The Build Gate and Test Gate (Engineering Governance §2) passed on
  every commit throughout the release — 552/552 tests, 0 warnings, 0
  errors, as of `WP 5.3`'s own completion (re-verified `WP 5.4`).
- No Platform Foundation ADR was silently contradicted; every genuinely
  new architectural decision this release required got its own ADR — 9
  in total: `ADR-0031`/`ADR-0032` (Navigation), `ADR-0033`–`ADR-0035`
  (the Shell), `ADR-0036`–`ADR-0038` (the Command Framework), and
  `ADR-0039` (Diagnostics) — this plan's own original list named only
  the first five, written before the Command Framework's and
  Diagnostics' own architecture phases began.
- The Academy gained a retrospective for every Work Package in this
  release — `WP 5.0A` through `WP 5.3`, plus this release's own closing
  retrospective (`WP 5.4`).

## Related Documents

`WorkPackages.md` · `docs/architecture/Navigation Framework
Architecture.md` · `docs/architecture/Shell & Composition Framework
Architecture.md` · `ADR-0022`, `ADR-0031`, `ADR-0032`, `ADR-0033`,
`ADR-0034`, `ADR-0035` · `docs/releases/v0.4.0/ReleasePlan.md` ·
`docs/releases/v0.4.0/WorkPackages.md` · `../FOUNDATION.md` ·
`../v0.4.0.md`.
