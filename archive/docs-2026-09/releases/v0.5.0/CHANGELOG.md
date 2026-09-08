# TempestOS v0.5.0 — Changelog

## Status

**Released — 2026-07-29.** `v0.5.0`, "Developer Experience," ships every
Work Package in this release's final scope (`WP 5.0A` through `WP 5.3`),
verified complete by `WP 5.4` (v0.5.0 Release Candidate & Engineering
Sign-Off) before Product Approval authorised the release itself. Entries
were added as each Work Package actually landed — not written in advance
as predictions.

---

## Release Summary — v0.5.0 "Developer Experience"

**Major capabilities delivered.** Navigation (`INavigationProvider`/
`NavigationService`) — a DI-public, UI-agnostic registry of navigable
destinations. The Shell & Composition Framework (`TempestShell`,
`ITempestHost.Services`) — `Tempest.App` runs the real platform for the
first time in this project's history. The Command Framework
(`ICommandDispatcher`/`ICommandRegistry`) — a typed dispatcher and an
Id-keyed registry for invoking application logic uniformly from any
caller. Diagnostics (`IDiagnosticsProvider`/`DiagnosticsProvider`) — a
read-only projection over the Host's own lifecycle state. A `dotnet new`
module-scaffolding template.

**Governance and security milestones.** The first comprehensive security
audit of the entire platform (`WP 5.0S`), establishing the v0.5.0
Security Baseline every subsequent Work Package's Definition of Done is
checked against.

**Engineering improvements.** A fourth and fifth independent "should this
be a DI-public Platform Service" decision (Navigation, the Command
Framework, Diagnostics) each reached the same shape as the Event Bus
before them, without needing to invent a new pattern. A genuine
implementation finding — two independent singleton registrations against
one concrete type do not share an instance in this container — resolved
with a small, shared, container-registered collaborator
(`CommandHandlerTable`), not a container redesign. A second genuine
finding — `IDiagnosticsProvider`'s two Host-owned dependencies do not
exist yet at the point in the Host Lifecycle where DI registrations
happen — resolved with `Func<T>` lazy accessors, mirroring
`ITempestHost.Services`'s own established "not yet available" convention.
A long-documented-but-unenforced Discovery pitfall (a module with no
`[ModuleMetadata]` and no parameterless constructor crashing with a raw,
unhelpful `MissingMethodException`) was finally closed with a clear,
actionable `ModuleDiscoveryException`.

**Architecture improvements.** 9 new ADRs (`ADR-0031`–`ADR-0039`), all
Accepted, none superseded. The four-layer platform model (`ADR-0023`)
absorbed four more genuinely new capabilities without needing to change.
The Composition Root pattern (`ADR-0009`) was reused a fourth time
(Diagnostics) and proved general enough to combine with a DI-public
registration for the first time — a novel combination this release's own
Ownership Matrix now documents explicitly.

**Testing growth.** 355 → 552 tests (+197), zero regressions at any Work
Package boundary, verified stable across multiple consecutive full-suite
runs at every milestone.

**Documentation growth.** 16 → 20 architecture documents under
`docs/architecture/`; 30 → 39 ADRs; 29 → 45 Rejected Designs entries;
63 → 77 Academy articles; a new top-level `docs/security/` tree (four
documents).

**Governance growth.** The 27-register governance suite held through
nine Work Packages, with every ADR/Rejected-Designs/Academy obligation
met. Several genuine, pre-existing documentation drifts were found and
corrected along the way — see "Repository Review Findings, Across the
Release," below — none of them a regression this release introduced,
all of them older drift finally noticed.

**Breaking changes.** None. Every Platform Foundation (`v0.4.0`) public
contract — Configuration, Logging, Discovery, Registration, Dependency
Injection, Lifecycle, the Event Bus, Background Services, Plugin
Manifest — is unchanged. This release extends the platform *around*
those services and the ones `v0.4.0` itself established; it does not
reopen any of them.

**Migration notes.** None required. A consumer of `v0.4.0` upgrading to
`v0.5.0` needs no code changes — every new capability (Navigation, the
Shell, the Command Framework, Diagnostics, `CompositeLogSink`, the
module template) is additive, opt-in, and inert until a module or
application actually uses it. `Tempest.App` now runs through
`TempestHost` for the first time — a behavioural upgrade, not a breaking
one, since nothing depended on its previous bootstrap-era behaviour.

**Known limitations.** The Shell's own input handling (menus, keyboard
shortcuts) is not yet wired to the Command Framework — both exist,
resolvable through `ITempestHost.Services`, but nothing yet connects
them; a later Work Package's own scope. Two Technical Debt items
(`TD-09`, plugin isolation boundary; `TD-10`/`TD-11`, Navigation/Command
registration-order ownership) remain open, each requiring a future
Architecture Work Package once third-party plugin support becomes a real
requirement. `TD-01` (the legacy `LoggingService`) remains open,
deliberately re-scoped forward again rather than migrated, since the code
it concerns has had no live caller since `WP 5.0D`. See
`docs/governance/Quality/Technical Debt Register.md` for the complete,
current list.

**Next milestone.** No Work Package is currently scoped beyond `v0.5.0`.
Whether to cut this release, and what a future `v0.6.0` should contain,
are both Product Approval decisions — see `PROJECT_STATUS.md` for
current status.

---

## [0.5.0] - 2026-07-29

### Added

- **Navigation Framework Architecture (WP 5.0A)** — design only; no
  code. `ADR-0031` (Navigation contracts belong in `Tempest.Core`;
  rendering remains an application responsibility), `ADR-0032`
  (Navigation is DI-public, registered imperatively, reusing the Event
  Bus). `Navigation Framework Architecture.md`. Four new Rejected
  Designs entries (`RD-0030`–`RD-0033`).
- **Navigation Framework Implementation (WP 5.0B)** —
  `Tempest.Core.Navigation` (`NavigationItem`, `INavigationProvider`/
  `NavigationService`, `NavigationRequestedEvent`, `NavigationException`
  and two subtypes), registered during the existing Platform Services
  Registered phase, alongside `IEventBus`. Three new `Tempest.Samples`
  reference modules (`NavigationSampleModule`,
  `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`)
  and a real, dynamically-built, on-disk plugin assembly proving a
  plugin-loaded module contributes navigation through the identical path
  an ordinarily-discovered module uses. 45 new tests.
- **Shell & Composition Framework Architecture (WP 5.0C)** — design
  only; no code. Not part of the original release plan — Repository
  Investigation confirmed `Tempest.App` still did not consume the
  platform at all, and this release's own scope grew to close that gap.
  `ADR-0033` (the Shell is a composition root, not a module or hosted
  service), `ADR-0034` (`ITempestHost` exposes a read-only service
  resolution surface), `ADR-0035` (the Shell owns page/view
  construction, independent of the DI container). `Shell & Composition
  Framework Architecture.md`. Four new Rejected Designs entries
  (`RD-0034`–`RD-0037`).
- **Shell & Composition Framework Implementation (WP 5.0D)** —
  `ITempestHost.Services`; `Tempest.App.Shell` (`TempestShell`,
  `IPage`/`PlaceholderPage`); `Program.cs` rewritten as the platform's
  real entry point. `Tempest.App` now constructs and runs a real
  `TempestHost`, discovers every `Tempest.Samples` module, and presents
  a real, interactive Navigation/Content region — the first time in this
  project's history the built application actually runs through the
  module pipeline. 46 new tests.
- **Platform Security Baseline Audit (WP 5.0S)** — the first
  comprehensive security audit of the entire platform, across every
  production file implemented through `WP 5.0D`. `docs/security/` (new
  top-level tree): `Threat Model.md`, `Security Principles.md`,
  `Platform Security Review v0.5.0.md`, `Security Roadmap.md`. One
  isolated, non-breaking fix: `PluginManifestDiscoveryService`'s
  `AssemblyFileName` path-containment check (Finding PL-1). Two future
  security debt items disclosed and deferred (`TD-09`, plugin isolation;
  `TD-10`, Navigation ownership). No Critical or High severity
  vulnerability found. 2 new regression tests.
- **Command Framework Architecture (WP 5.1A)** — design only; no code.
  Split from a single `WP 5.1` entry into architecture and implementation
  phases (`D-018`), mirroring the Navigation and Shell precedent.
  `ADR-0036` (the Command Framework is DI-public), `ADR-0037`
  (imperative, two-part registration — a type-keyed handler and an
  Id-keyed descriptor), `ADR-0038` (dispatch propagates handler
  exceptions to the caller, diverging deliberately from the Event Bus's
  per-subscriber isolation). `Command Framework Architecture.md`. Four
  new Rejected Designs entries (`RD-0038`–`RD-0041`). Mandatory security
  review against the `WP 5.0S` baseline surfaced one new finding
  (`CMD-1`/`TD-11`, registration-order squatting — affecting both the new
  Command Framework and the already-implemented Navigation Framework).
- **Command Framework Implementation (WP 5.1B)** —
  `Tempest.Core.Commands`: `ICommandHandler<TCommand>`,
  `ICommandDispatcher`/`CommandDispatcher`, `ICommandRegistry`/
  `CommandRegistry`, `CommandDescriptor`, `CommandResult`,
  `CommandHandlerTable`, and the `CommandException` hierarchy (five
  types), registered during the existing Platform Services Registered
  phase. A genuine implementation finding — two independent singleton
  registrations against `CommandHandlerTable` do not share an instance in
  this container — resolved by introducing the shared collaborator as
  its own, separately-registered singleton, not by reflection or a
  container redesign. `CommandSampleModule` (`Tempest.Samples`),
  registering `IncrementCounterCommand` (success/failure) and
  `NavigateToSampleHomeCommand` — the first concrete realisation of
  `ADR-0022`'s own `OpenModuleCommand → NavigationService.Navigate(...)`
  illustration. 66 new tests.
- **Diagnostics Improvements (WP 5.2)** — `CompositeLogSink`
  (`Tempest.Core.Logging`), fanning a log entry out to any number of
  child `ILogSink`s with per-child failure isolation — closes `TD-02`.
  The legacy `LoggingService` migration question decided, not
  migrated — `TD-01` re-scoped forward again (`D-020`), since
  `Program.cs` has had no live caller into that code since `WP 5.0D`.
  `IDiagnosticsProvider`/`DiagnosticsProvider` (new
  `Tempest.Core.Diagnostics` namespace) — a read-only projection over
  `IModuleLifecycleManager`/`IHostedServiceManager`'s own existing
  snapshot data, registered via the Composition Root pattern with
  `Func<T>` accessors (`ADR-0039`), since neither manager exists yet at
  the point in the Host Lifecycle where DI registrations happen.
  `DiagnosticsSampleModule` and `GetDiagnosticsSummaryCommand`
  (`Tempest.Samples`), demonstrating the Command Framework and
  Diagnostics interacting. This Work Package's own brief, as originally
  written, described an "Event Framework Implementation" against a
  non-existent architecture document — investigated and redirected to
  this, the real, current `WP 5.2` (`D-019`), before any code was
  written. 28 new tests.
- **Developer Experience Improvements (WP 5.3)** — `dotnet new
  tempest-module` (`src/Templates/Tempest.Templates.Module/`), generating
  a module shaped exactly as `Building a Module.md` describes, installed
  locally rather than as a NuGet package (`RD-0045` — this repository has
  no publishing pipeline yet). `ReflectionFrameworkDiscoveryService.
  CreateDescriptor` now checks for a public parameterless constructor
  before calling `Activator.CreateInstance`, raising a clear
  `ModuleDiscoveryException` naming the actual fix instead of a raw
  `MissingMethodException` — closing a Discovery pitfall documented in
  prose since `WP 4.1` but never enforced in code. Verified with the
  real `dotnet new` CLI (installed, generated, built, then removed,
  leaving no trace) and by an automated test suite that substitutes,
  builds, and proves the result discoverable by the real, unmodified
  Discovery service. 10 new tests.
- **v0.5.0 Release Candidate & Engineering Sign-Off (WP 5.4)** —
  end-to-end verification: every ADR implemented or intentionally
  deferred; every Work Package closed; every governance register
  internally consistent (one genuine arithmetic error found and
  corrected — the Exception Register's own stated total undercounted its
  own Entries table by one); architecture, governance, and onboarding
  documentation brought current, closing several drifts that had
  survived unnoticed across multiple prior Work Packages' own repository
  reviews (see this Work Package's own retrospective for the full
  account). This Changelog, `Release Notes.md`, and the top-level
  `docs/releases/v0.5.0.md` summary. No feature added; no architecture
  redesigned.

### Fixed

- `ReflectionFrameworkDiscoveryService`'s failure message for a module
  with no `[ModuleMetadata]` and no parameterless constructor (`WP 5.3`)
  — see Added, above.
- `PluginManifestDiscoveryService`'s `AssemblyFileName` path-containment
  check (`WP 5.0S`, Finding PL-1) — an absolute path or a `../` escape in
  a plugin manifest's declared assembly file name previously resolved
  outside the plugin's own folder.

### Repository Review Findings, Across the Release

Genuine, pre-existing documentation and governance drifts found and
corrected during this release's own Work Packages, none a regression
this release introduced:

- `Technical Debt Register.md`'s `TD-07` still described Navigation's
  `Tempest.Core` placement as an open question, under its old `WP 4.6A`
  number, three Work Packages after `ADR-0031` had already resolved it
  (`WP 5.0D`).
- `Ownership Matrix.md` had never received a row for Navigation (found
  `WP 5.1A`, alongside that Work Package's own new Command Framework
  row).
- `WorkPackages.md` had never gained an entry for `WP 5.0S` at all (found
  `WP 5.1A`); several Engineering and Delivery governance registers had
  gone stale since `WP 5.0D` (found `WP 5.1B`).
- `Architecture Document Register.md` still read the Command Framework
  as "implementation pending... not yet started" two Work Packages after
  `WP 5.1B` had actually completed it (found `WP 5.2`).
- `Rejected Designs Register.md` had added `RD-0042`–`RD-0044` without
  the corresponding full entries ever being written into the source log;
  `Engineering Governance.md` §11 had not been updated when `WP 5.2`
  added a new namespace; `Governance Register.md`'s own Compliance
  Matrix had not been updated since `WP 5.0D`, missing four completed
  Work Packages entirely, including a never-backfilled commit-hash
  placeholder (found `WP 5.3`).
- The Exception Register's own stated total (30) had always undercounted
  its own Entries/Distribution tables (31); `docs/releases/v0.5.0/
  ReleasePlan.md`'s "Status" and "Scope" sections had not been updated
  since `WP 5.0C`; `docs/academy/Contributor Learning Path.md` still
  pointed a new contributor at `v0.4.0/WorkPackages.md` and cited a
  30-ADR count, and never mentioned Navigation, the Shell, the Command
  Framework, Diagnostics, or the new module template at all (found
  `WP 5.4`).

This pattern — each Work Package's own repository review finding drift
none of its predecessors caught — is itself a genuine, disclosed finding
about this project's own governance overhead at release-boundary scale,
not a criticism of any one Work Package; see `WP 5.4`'s own retrospective
for the full discussion.
