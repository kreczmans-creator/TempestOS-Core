# TempestOS v0.5.0 — "Developer Experience"

**Release Candidate date:** 2026-07-28
**Tag:** not yet cut — pending Product Approval (Engineering Governance §7)
**Branch:** `feature/v0.5.0-developer-experience` → `main` (not yet merged)

---

## Overview

TempestOS v0.5.0 closes the **Developer Experience** phase: everything
the Platform Foundation (v0.4.0) needed to become an actual, *usable*
application — a way for a user to navigate it, a composition root that
runs it, a uniform way to invoke application logic, visibility into what
the platform is doing, and a way for a new contributor to scaffold a
module without hand-copying an existing one.

This release also delivered the platform's first comprehensive security
audit, establishing a standing Security Baseline every future Work
Package is checked against — not part of the original plan, but folded
in as a dedicated, formal engineering milestone once the platform reached
a size that warranted one.

Every Work Package in this release's final scope is complete. This
document, together with `CHANGELOG.md` and `docs/releases/v0.5.0.md`,
constitutes the Release Candidate produced by `WP 5.4` — engineering
verification and sign-off, not the release cut itself.

## Highlights

- **`Tempest.App` runs the real platform for the first time in this
  project's history.** `TempestShell` constructs and runs a genuine
  `TempestHost`, resolves `INavigationProvider`/`IEventBus` through
  `ITempestHost.Services`, and presents a real, interactive Navigation/
  Content region.
- **A fourth, fifth, and sixth independent "should this be DI-public"
  decision, all reaching the same answer.** Navigation, the Command
  Framework, and Diagnostics each concluded, independently, that they
  belong alongside the Event Bus as an ordinary DI-public Platform
  Service — with Diagnostics reaching a genuinely novel combination
  (DI-public *and* Composition-Root-constructed) that this release's own
  Ownership Matrix now documents as a reusable pattern.
- **The platform's first comprehensive security audit.** Every
  production file reviewed against a purpose-built Threat Model; no
  Critical or High severity finding; one isolated fix applied; the
  remainder disclosed as future security debt with named triggers, not
  hidden or deferred silently.
- **A new contributor no longer has to hand-copy a sample module.**
  `dotnet new tempest-module` scaffolds a correctly-shaped module
  directly, verified with the real `dotnet new` CLI end to end.
- **552 tests, up from 355 at v0.4.0** — zero regressions at any Work
  Package boundary across the entire release.
- **Nine Work Packages found and corrected real, pre-existing
  documentation drift during their own repository reviews** — a genuine
  discipline, not a formality; see `CHANGELOG.md`'s own "Repository
  Review Findings, Across the Release" for the full list.

## Major Features

| Feature | What It Does |
|---|---|
| **Navigation** | `INavigationProvider`/`NavigationService` — a DI-public, UI-agnostic registry of navigable destinations, notified via the existing Event Bus. |
| **Shell & Composition Framework** | `TempestShell`, `ITempestHost.Services` — `Tempest.App`'s own composition root, presenting Navigation and the Event Bus to a real user for the first time. |
| **Command Framework** | `ICommandDispatcher`/`ICommandRegistry` — invoke a discrete unit of application logic from a typed caller or a string Id, uniformly, from a menu, a keyboard shortcut, or future automation. |
| **Diagnostics** | `IDiagnosticsProvider`/`DiagnosticsProvider` — a read-only projection over the Host's own current lifecycle state, without granting write access to the machinery that produces it. |
| **Composite Logging** | `CompositeLogSink` — fan a log entry out to any number of sinks simultaneously, with per-child failure isolation. |
| **Module Template** | `dotnet new tempest-module` — scaffold a correctly-shaped module without hand-copying an existing one. |

## Engineering Improvements

- Two genuine implementation findings, each resolved without a container
  redesign: two independent singleton registrations against
  `CommandHandlerTable` do not share an instance in this DI container
  (resolved with a shared, separately-registered collaborator); two of
  `IDiagnosticsProvider`'s dependencies do not exist yet at the point in
  the Host Lifecycle where DI registrations happen (resolved with
  `Func<T>` lazy accessors, mirroring `ITempestHost.Services`'s own
  established convention).
- A four-Work-Package-old Discovery pitfall — documented in prose since
  `WP 4.1`, never enforced in code — finally closed: a module with no
  `[ModuleMetadata]` and no parameterless constructor now fails with a
  clear, actionable message instead of a raw runtime exception.
- A premise mismatch was caught before implementation began, twice: once
  when a Work Package's own brief described an architecture document
  that did not exist (`WP 5.2`, mirroring `WP 4.4C`'s own precedent), and
  a governance-scale pattern was named explicitly — every Work Package
  in this release's second half found real, previously-unnoticed drift
  during its own repository review, none of it caused by that Work
  Package's own changes.

## Architecture

- **9 new ADRs** (`ADR-0031`–`ADR-0039`), all Accepted, none superseded.
- **15 new Rejected Designs entries** (`RD-0030`–`RD-0045`), recording
  every genuine alternative seriously considered and declined.
- **The four-layer platform model** (`ADR-0023`) absorbed four more
  genuinely new capabilities without needing to change.
- **The Composition Root pattern** (`ADR-0009`) was reused a fourth time
  and combined, for the first time, with a DI-public registration —
  Diagnostics is both Host-constructed and directly resolvable by any
  module, a combination possible only because it carries no orchestration
  authority of its own.
- **No breaking changes.** Every Platform Foundation contract —
  Configuration, Logging, Discovery, Registration, Dependency Injection,
  Lifecycle, the Event Bus, Background Services, Plugin Manifest — is
  unchanged.

## Security

The first comprehensive security audit of the entire platform (`WP 5.0S`)
reviewed every production file against a purpose-built Threat Model.
**No Critical or High severity vulnerability was found.** One isolated,
non-breaking fix was applied (a plugin manifest path-containment check);
the remaining findings are disclosed, future-facing security debt with
named triggers — see `docs/security/Platform Security Review v0.5.0.md`
and `docs/governance/Quality/Technical Debt Register.md`. Every Work
Package since has performed its own security review against this
baseline and disclosed no new debt beyond what was already tracked.

## Documentation

- 20 standing architecture documents under `docs/architecture/` (22
  including the two release-scoped documents), all cross-referenced, all
  current.
- A new top-level `docs/security/` tree: `Threat Model.md`, `Security
  Principles.md`, `Platform Security Review v0.5.0.md`, `Security
  Roadmap.md`.
- `docs/academy/Contributor Learning Path.md` updated to point at this
  release's own Work Package plan, current ADR count, and the four new
  platform services this release added — corrected during `WP 5.4` after
  going stale since `v0.4.0`.

## Academy

77 articles across 7 categories — every completed Work Package has a
matching retrospective, plus a new `v0.5.0 Release Retrospective`
(`WP 5.4`) reflecting on the release as a whole. New concept guides this
release: *Navigation Architecture*, *Shell & Application Composition*,
*Command Framework*, *Diagnostics & Composite Logging*. A new "Security"
category teaches threat modelling, secure plugin architecture, trust
boundaries, and least privilege from first principles.

## Governance

The 27-register governance suite held through nine Work Packages. Every
ADR, Rejected Designs, and Academy obligation was met. Several genuine,
pre-existing documentation drifts were found and corrected along the way
— see `CHANGELOG.md`'s "Repository Review Findings, Across the Release"
for the complete list, including one internal arithmetic error in the
Exception Register (stated total undercounted its own entries by one),
found and corrected during this release's own closing Work Package.

## Testing

| Metric | v0.4.0 | v0.5.0 | Change |
|---|---|---|---|
| Automated tests | 355 | 552 | +197 |
| Test failures | 0 | 0 | — |
| Build warnings | 0 | 0 | — |
| Build errors | 0 | 0 | — |

Verified stable across multiple consecutive full-suite runs at every
Work Package boundary throughout the release. Testing philosophy
unchanged: prefer real implementations over mocks — this release added
one new instance of the pattern, proving the module template's own
generated content compiles and is discoverable by shelling out to the
real `dotnet build` compiler, not a mock of it.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 552 |
| ADRs | 39 (`ADR-0001`–`ADR-0039`), all Accepted |
| Rejected Designs | 45 (`RD-0001`–`RD-0045`) |
| Academy articles | 77 |
| Governance registers | 27 (32 governance documents total) |
| Architecture documents | 20 (22 including the two release-scoped documents) |
| Platform services | 17 catalogued — 14 Implemented, 2 not implemented, 1 developer-convenience layer |
| Modules (production) | 7 |
| Hosted services (production) | 0 (infrastructure complete; zero shipped consumers by design) |
| Plugins (production) | 0 (infrastructure complete; `src/Plugins/` empty by design) |
| Commits (this release) | 10 (`WP 5.0A`–`WP 5.3`, plus one small follow-up fix) since the `v0.4.0` tag, before `WP 5.4`'s own commit |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

## Known Limitations

- The Shell's own input handling (menus, keyboard shortcuts) is not yet
  wired to the Command Framework — both exist and are resolvable through
  `ITempestHost.Services`; nothing yet connects them.
- `TD-09` (no isolation boundary between a loaded plugin and a
  first-party module) and `TD-10`/`TD-11` (Navigation/Command
  registration-order ownership) remain open, each requiring a future
  Architecture Work Package once third-party plugin support becomes a
  real requirement — see `docs/security/Security Roadmap.md`.
- `TD-01` (the legacy `LoggingService`) remains open, deliberately
  re-scoped forward again rather than migrated.
- Zero real plugins and zero real hosted services ship in this release —
  both pieces of infrastructure are complete and tested; no Work Package
  has yet built a real consumer for either, by deliberate scope choice.

Full, current detail: `docs/governance/Quality/Technical Debt Register.md`.

## What's Next

No Work Package is currently scoped beyond `v0.5.0`. Two decisions remain,
both Product Approval's, not engineering's: whether and when to cut this
release (merge to `main`, tag `v0.5.0`, push), and what a future `v0.6.0`
should contain. See `PROJECT_STATUS.md` for current status.

## Acknowledgements

The Developer Experience phase was developed using the same
architecture-first engineering process the Platform Foundation
established: every non-trivial component was designed, reviewed,
implemented, tested, and documented, in that order, before the next one
began. Every genuine alternative seriously considered and declined was
recorded, not merely forgotten. This release marks the platform's
transition from infrastructure a module can run inside to an application
a person can actually use.
