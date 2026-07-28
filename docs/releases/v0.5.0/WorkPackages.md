# TempestOS v0.5.0 — Work Packages

## How to Read This Document

This release renumbers four Work Packages `docs/releases/v0.4.0/
WorkPackages.md` originally scoped as `WP 4.6A` through `WP 4.9`, then
rescoped out of `v0.4.0` — see `ReleasePlan.md`'s "A Note on Renumbering"
table for the full mapping. Objective, scope, and dependencies are
carried forward unchanged from the `v0.4.0` plan except where a Work
Package has since actually completed and gained real findings (`WP 5.0A`,
below).

Two architecture-significant questions remain decided, carried forward
from `v0.4.0` planning, applied throughout this document:

- **ADR-0022** — Navigation and Command Framework are orthogonal; neither
  depends on the other.
- **ADR-0023** — platform-wide dependency layering (Modules → Platform
  APIs → Platform Services → Runtime Host, downward only). Applies beyond
  any one release; see `docs/releases/FOUNDATION.md`.

Two more are now decided as of this release's own first Work Package:

- **ADR-0031** — Navigation contracts belong in `Tempest.Core`; rendering
  remains an application responsibility.
- **ADR-0032** — Navigation is a DI-public platform service, registered
  imperatively, reusing the Event Bus for its own notification.

---

## WP 5.0A — Navigation Framework Architecture

**Status note.** Complete. Design: `docs/architecture/Navigation
Framework Architecture.md`, `ADR-0031`, `ADR-0032`, and the `WP 5.0A`
Academy retrospective. Formerly `WP 4.6A` under the `v0.4.0` plan.

### Objective

Decide what "navigation" means for TempestOS before writing any
implementation — the least architecturally grounded objective in this
release, and the one Work Package explicitly run as an architecture-only
phase, mirroring `WP 2.7A`'s approach to the Runtime Host itself.

### Scope

- Navigation's data model, registration model, ownership, dependency
  direction, and the platform/application rendering boundary — defined
  in full; `WP 4.0` deliberately left `INavigationProvider` undefined
  (`RD-0002`), naming this Work Package as its own revisit trigger.
- **The Navigation/Command Framework dependency question was already
  resolved — `ADR-0022`.** This Work Package did not revisit it;
  Navigation's own design is a standalone peer service, consumed by
  application logic exactly as `IEventBus` is.
- **Resolved — does Navigation belong in `Tempest.Core` at all?** Yes
  (`ADR-0031`) — the model does; rendering explicitly does not.

### Dependencies

**`WP 4.0`** (whatever base contracts already exist). Not dependent on
**`WP 5.1`** (Command Framework) — `ADR-0022`.

### Deliverables — Done

- A written architecture document, mirroring `Background Services
  Architecture.md`'s own rigour, answering: what navigation means here;
  that it belongs in `Tempest.Core`; how application logic wires commands
  and navigation together per `ADR-0022`'s own shape, without either
  service depending on the other.
- `INavigationProvider`, `NavigationItem`, and `NavigationRequestedEvent`,
  entirely undefined until now, designed in full (`ADR-0031`, `ADR-0032`).
- Two new ADRs; four new Rejected Designs entries (`RD-0030`–`RD-0033`).

### Acceptance Criteria — Met

- A written architecture decision exists and was reviewed before
  `WP 5.0B` begins. **Met.**
- The design demonstrates at least one of `ADR-0022`'s two illustrative
  shapes concretely — the "direct application-logic call"
  (`OpenModuleCommand → NavigationService.Navigate(...)`) shape, via
  `Navigate`'s own designed signature. **Met.**

### Estimated Complexity

**Realised as M** — the Command Framework half of this Work Package's
original "Unknown — provisionally L" estimate (`v0.4.0` plan) had
already been resolved by `ADR-0022`; the remaining `Tempest.Core`
placement question resolved cleanly by reusing the Event Bus's own
already-proven precedent, rather than requiring genuinely new
architectural reasoning.

### Risks

- Was the highest-risk Work Package in the release by a clear margin
  (`Risks.md`, R2) — resolved by this design; see R2's own update.

---

## WP 5.0B — Navigation Framework Implementation

**Status note.** Complete. Implementation:
`src/Tempest.Core/Navigation/`, three new `Tempest.Samples` reference
modules, 45 new tests (400 total), and the `WP 5.0B` Academy
retrospective. Formerly `WP 4.6B` under the `v0.4.0` plan.

### Objective

Implement what `WP 5.0A` designed.

### Scope

Defined entirely by `WP 5.0A`'s own deliverable
(`docs/architecture/Navigation Framework Architecture.md`) — realised
with zero deviation from that design.

### Dependencies

**`WP 5.0A`** (required, blocking; complete). **Not `WP 5.1`** (Command
Framework) — `ADR-0022`; `WP 5.0B` may proceed regardless of whether
`WP 5.1` has landed yet.

### Deliverables — Done

- `Tempest.Core.Navigation` (`NavigationItem`, `INavigationProvider`/
  `NavigationService`, `NavigationRequestedEvent`, and the
  `NavigationException` hierarchy), registered during the existing
  Platform Services Registered phase.
- Three real, discovered reference modules
  (`NavigationSampleModule`, `SecondaryNavigationSampleModule`,
  `DuplicateNavigationSampleModule`) each contributing a real navigation
  item.
- A real, dynamically-built, on-disk plugin assembly proving a
  plugin-loaded module contributes navigation through the identical path
  an ordinarily-discovered module uses.

### Acceptance Criteria — Met

- A real module registers a navigation item and a real `Navigate(...)`
  call is observed to publish `NavigationRequestedEvent` through the
  real, unmodified `IEventBus`. **Met** — proven end to end through the
  real Runtime Host.

### Estimated Complexity

**Realised as M.**

### Risks

None materialised. `WP 5.0A`'s design required zero revision to
implement; see the `WP 5.0B` retrospective's own "Readiness assessment."

---

## WP 5.0C — Shell & Composition Framework Architecture

**Status note.** Complete. Design: `docs/architecture/Shell &
Composition Framework Architecture.md`, `ADR-0033`, `ADR-0034`,
`ADR-0035`, and the `WP 5.0C` Academy retrospective. Not part of the
original `v0.4.0` plan — a new Work Package this release's own sequence
grew to need, inserted between `WP 5.0B` and `WP 5.1` without renumbering
either.

### Objective

Design how `Tempest.App` consumes the platform: the Shell that becomes
its own composition root, presenting Navigation and the Event Bus to a
user, without becoming a second place platform behaviour is decided.

### Scope

- How `Tempest.App` currently works (confirmed: it does not construct or
  run `TempestHost` at all — a bootstrap-era console loop, unchanged
  since `WP 5.0A`'s own disclosure of the same fact).
- The Shell's own structural classification (composition root, not a
  module or hosted service) and the mechanism by which it reaches
  DI-public platform services (`ITempestHost.Services`).
- The application model: Workspace, Navigation Region, Content Region,
  Status Bar, Dialogs, Notifications — which are required for `v0.5` and
  which are explicitly deferred.
- Page/view construction ownership, and whether dependency injection
  participates in it.
- **Explicitly out of scope**: any production code; migrating or
  touching the bootstrap-era `BootstrapService`/`HostingService`/
  `ProjectService` code itself.

### Dependencies

**`WP 5.0A`/`WP 5.0B`** (Navigation, complete — the first real platform
capability the Shell consumes). Not dependent on **`WP 5.1`** (Command
Framework) — the Shell's own composition model anticipates Commands as a
future input source without requiring the dispatcher to exist yet.

### Deliverables — Done

- A written architecture document answering: what the Shell is
  structurally; the platform/application boundary; the composition
  model; how the Shell integrates with Navigation, the Event Bus, Hosted
  Services, and (in future) Commands and Diagnostics.
- Three new ADRs (`ADR-0033`, `ADR-0034`, `ADR-0035`); four new Rejected
  Designs entries (`RD-0034`–`RD-0037`).

### Acceptance Criteria — Met

- A written architecture decision exists and was reviewed before
  `WP 5.0D` begins. **Met.**
- The design resolves how the Shell reaches a DI-public platform service
  without weakening `ADR-0017`'s existing Host-owned/DI-public boundary.
  **Met** — `ITempestHost.Services` (`ADR-0034`) exposes only what was
  already DI-public; Discovery/Registration/Lifecycle/Hosted Service
  orchestration remain unregistered and therefore unreachable.

### Estimated Complexity

**Realised as M** — three related mechanical questions (structural
classification, service access, page ownership), each resolved by
applying a test this release has now used repeatedly (does an
already-proven pattern already answer this; does this dependency point
downward), rather than requiring genuinely new architectural reasoning.

### Risks

None discovered. The central fact this Work Package designs around
(`Tempest.App` never having constructed a real `ITempestHost`) was
already disclosed once, during `WP 5.0A`'s own investigation — this Work
Package is the designed response to it, not a newly found risk.

---

## WP 5.0D — Shell & Composition Framework Implementation

**Status note.** Complete. Implementation: `src/Tempest.App/Shell/`.
**Correction (found during `WP 5.2`'s own repository review):** this
entry had read "Not started" since it was first written, even though
this Work Package completed long ago (see `PROJECT_STATUS.md`, every
governance register, and the codebase itself) — no Work Package since had
touched this specific line. Corrected here.

### Objective

Implement what `WP 5.0C` designed.

### Scope

Defined entirely by `WP 5.0C`'s own deliverable (`docs/architecture/Shell
& Composition Framework Architecture.md`) — realised with zero deviation
from that design's own approved shape.

### Dependencies

**`WP 5.0C`** (required, blocking; complete). **Not `WP 5.1`** (Command
Framework) — `WP 5.0D` may proceed regardless of whether `WP 5.1` has
landed yet, per the same orthogonality reasoning `ADR-0022` already
established for Navigation.

### Deliverables — Done

`ITempestHost.Services` (`ADR-0034`); the Shell itself
(`TempestShell`, `IPage`/`PlaceholderPage`, `Tempest.App.Shell`) as
`Tempest.App`'s own composition root; Workspace, Navigation Region, and
Content Region, populated; a reserved, unpopulated Status Bar region.

### Acceptance Criteria — Met

`Tempest.App` constructs and runs a real `ITempestHost`; the Shell
resolves `INavigationProvider` through `Services` and renders its
`Items`; selecting a navigation item is observed to update the Content
Region via a real `NavigationRequestedEvent` — all proven directly
against the real, unmodified Host and sample modules.

### Estimated Complexity

**Realised as M.**

### Risks

Inherits any risk `WP 5.0C` did not fully resolve — none currently named;
see the `WP 5.0C` retrospective's own "Readiness assessment."

---

## WP 5.0S — Platform Security Baseline Audit

**Status note.** Complete. A dedicated, formal engineering audit — not a
feature Work Package — inserted between `WP 5.0D` and `WP 5.1A` without
renumbering anything else, mirroring `D-016`'s own precedent for
mid-release scope growth. **This entry was missing from this document
entirely until `WP 5.1A` found and corrected the gap** — disclosed here
as pre-existing drift, not caused by `WP 5.1A`'s own scope.

### Objective

Establish the first comprehensive security audit of the entire platform
— every production file, across 15 audit areas — and produce the
v0.5.0 Security Baseline every subsequent Work Package's Definition of
Done is checked against.

### Scope

No new feature; no architecture redesigned. One isolated, non-breaking
fix (a plugin manifest `AssemblyFileName` path-containment check); two
future security debt items disclosed (`TD-09`, `TD-10`) and deferred to
a future Architecture Work Package.

### Dependencies

**`WP 5.0A`–`WP 5.0D`** (everything built so far — the audit's own
scope).

### Deliverables — Done

`docs/security/Threat Model.md`, `Security Principles.md`, `Platform
Security Review v0.5.0.md`, `Security Roadmap.md`; `Technical Debt
Register.md` (`TD-09`, `TD-10`); `Decision Register.md` (`D-017`).

### Acceptance Criteria — Met

No Critical or High severity vulnerability found; build clean, tests
unchanged plus 2 new regression tests (448/448).

### Estimated Complexity

**L.**

### Risks

None named; see the `WP 5.0S` retrospective's own Security Baseline
Statement.

---

## WP 5.1A — Command Framework Architecture

**Status note.** Complete. Design: `docs/architecture/Command Framework
Architecture.md`, `ADR-0036`–`ADR-0038`, and the `WP 5.1A` Academy
retrospective. This Work Package split the originally single-phase
`WP 5.1` entry into an architecture phase (`WP 5.1A`) and an
implementation phase (`WP 5.1B`), mirroring the `WP 5.0A`/`WP 5.0B` and
`WP 5.0C`/`WP 5.0D` precedent exactly (`D-018`).

### Objective

Decide what "Command Framework" means for TempestOS before writing any
implementation — a handler contract and a dispatcher for `ICommand`
(`WP 4.0`), integrating cleanly with the Runtime Host, Event Bus,
Navigation, and Application Shell.

### Scope

- `ICommandDispatcher`, `ICommandRegistry`, `CommandDescriptor`,
  `CommandResult`, and five exception types — designed in full.
- **The command/event distinction, documented explicitly**: a command
  has exactly one handler and an expected result; an event has zero or
  more subscribers and no expected result — restated directly in
  `Command Framework Architecture.md`'s own Repository Investigation,
  resolving `Risks.md` R3.
- **`ADR-0022` respected, not reopened**: the Command Framework never
  depends on, or is invoked through, `INavigationProvider`/
  `NavigationService`.
- **Mandatory security review against the `WP 5.0S` baseline**,
  surfacing one new finding (`CMD-1`/`TD-11`, registration-order
  squatting — affecting both the new Command Framework and the
  already-implemented Navigation Framework).

### Dependencies

**`WP 4.0`** (`ICommand`). Cross-reference **`WP 4.4`** (Event Bus) for
the command/event distinction. Not dependent on **`WP 5.0A`/`WP 5.0B`**
(Navigation) — `ADR-0022`. **`WP 5.0S`** (Platform Security Baseline —
mandatory security review).

### Deliverables — Done

A written architecture document; three new ADRs (`ADR-0036`–`ADR-0038`);
four new Rejected Designs entries (`RD-0038`–`RD-0041`); a new Academy
concept guide (`11-command-framework.md`) and retrospective; `Risks.md`
R3 retired; `Technical Debt Register.md` `TD-11` added, `TD-09` widened.

### Acceptance Criteria — Met

Every open question a `WP 5.1B` implementer would face is answered in
writing: registration model, dispatch model, failure model, integration
with every existing platform service, and a mandatory security review
with no unresolved STOP condition.

### Estimated Complexity

**M.**

### Risks

None named; `CMD-1`/`TD-11` is disclosed debt, not a Work Package risk —
see `Technical Debt Register.md`.

---

## WP 5.1B — Command Framework Implementation

**Status note.** Complete. Implementation: `src/Tempest.Core/Commands/`,
a new `Tempest.Samples` reference module (`CommandSampleModule`) plus two
reference commands, 66 new tests (514 total), and the `WP 5.1B` Academy
retrospective. Formerly the implementation half of a single `WP 5.1`
entry, formerly `WP 4.7` under the `v0.4.0` plan.

### Objective

Implement exactly what `WP 5.1A` designed.

### Scope

Defined entirely by `WP 5.1A`'s own deliverable (`docs/architecture/
Command Framework Architecture.md`) — realised with zero deviation from
that design's own approved public contracts. One genuine implementation
finding (documented in that same architecture document's own
"Implementation Note" section, added by this Work Package): two
independent singleton registrations against the same concrete type do
not share an instance in this container, resolved by introducing a
small, shared, container-registered collaborator (`CommandHandlerTable`)
rather than by reflection or a container redesign.

### Dependencies

**`WP 5.1A`** (required, blocking; complete). **`WP 4.3`** (extended the
sample module set with `CommandSampleModule`). Not dependent on
**`WP 5.0A`/`WP 5.0B`** (Navigation) — `ADR-0022` — though
`CommandSampleModule` does depend on `INavigationProvider` directly, as
ordinary application logic, to realise ADR-0022's own illustration.

### Deliverables — Done

- `ICommandDispatcher`/`CommandDispatcher`, `ICommandRegistry`/
  `CommandRegistry`, `CommandDescriptor`, `CommandResult`,
  `CommandHandlerTable`, and the `CommandException` hierarchy, registered
  during the existing Platform Services Registered phase, exactly like
  the Event Bus and Navigation.
- `CommandSampleModule` (`Tempest.Samples`), registering
  `IncrementCounterCommand` (successful execution and expected failure,
  by amount) and `NavigateToSampleHomeCommand` (the first concrete
  realisation of ADR-0022's own `OpenModuleCommand → NavigationService.
  Navigate(...)` illustration).

### Acceptance Criteria — Met

A module registers a command handler and has it invoked either by its
concrete type (`ICommandDispatcher.DispatchAsync`) or by a string Id
(`ICommandRegistry.InvokeAsync`), proven against `CommandSampleModule`
and the real, unmodified `TempestHost`. Duplicate registration is
rejected, not silently overridden (`DuplicateCommandHandlerException`/
`DuplicateCommandIdException`). A handler's own exception propagates to
the caller, proven directly, both for a synthetic fixture and for the
real `CommandSampleModule`'s own negative-amount case (a `CommandResult.
Failure`, not a thrown exception — the expected, foreseeable path).

### Estimated Complexity

**Realised as M.**

### Risks

`CMD-1`/`TD-11` (registration-order squatting) remains open, exactly as
`WP 5.1A` disclosed and this Work Package's own brief scoped it — not
fixed, since doing so requires an architectural ownership/priority
model. Confirmed present in the real implementation by direct test,
not merely asserted.

---

## WP 5.2 — Diagnostics Improvements

**Status note.** Complete. Formerly `WP 4.8` under the `v0.4.0` plan. This
Work Package's own brief, as originally written, described an "Event
Framework Implementation" against a non-existent architecture document;
investigation before any code was written confirmed the Event Bus has
been fully implemented since `WP 4.4D`, and the real, current `WP 5.2` —
per this entry, unchanged since planning — is Diagnostics Improvements.
Redirected explicitly (`D-019`), not assumed, mirroring `D-009`'s
precedent.

### Objective

Close existing, named diagnostics debt and extend health/status
visibility using data the platform already produces.

### Scope

- **Single-sink logging limitation** (debt since `WP 2.6`): composite
  `ILogSink` for fan-out to multiple sinks.
- **Two coexisting logging mechanisms** (`ILogger` vs. legacy
  `LoggingService`): assess whether this release migrates
  `BootstrapService`/`HostingService`/`Program.cs`, or explicitly
  re-scopes the debt forward again.
- **Health/status reporting**: a read-only projection over
  `IModuleLifecycleManager`'s existing snapshot data.
- Define `IDiagnosticsProvider` from scratch — `WP 4.0` deliberately does
  not define it.

### Dependencies

Benefits from, but does not strictly require, **`WP 4.5`** (Background
Services) if health reporting runs as a periodic background check.

### Deliverables — Done

- `CompositeLogSink` (`Tempest.Core.Logging`) — fans a log entry out to
  any number of child `ILogSink`s, isolating one child's own write
  failure from every other. Closes `TD-02`.
- The legacy `LoggingService` migration question — **decided, not
  migrated**: `Program.cs` has not called this code since `WP 5.0D`;
  migrating dead code was judged pure risk with no behavioural benefit.
  `TD-01` re-scoped forward again (`D-020`).
- `IDiagnosticsProvider`/`DiagnosticsProvider` (`Tempest.Core.Diagnostics`)
  — a read-only, DI-resolvable projection over `IModuleLifecycleManager`/
  `IHostedServiceManager`'s own existing snapshot data, registered via
  the Composition Root pattern with `Func<T>` accessors (`ADR-0039`).
- `DiagnosticsSampleModule` and `GetDiagnosticsSummaryCommand`
  (`Tempest.Samples`), demonstrating the Command Framework and
  Diagnostics interacting.

### Acceptance Criteria — Met

- Log output can be written to more than one sink simultaneously without
  any consumer of `ILogger` changing — proven directly by
  `CompositeLogSinkTests.cs`'s own `Logger`-integration test.
- A consumer can query every module's state without gaining write access
  to `IRuntimeModuleManager`/`IModuleLifecycleManager` themselves —
  `IDiagnosticsProvider` exposes only read-only snapshot collections, no
  method of either manager.

### Estimated Complexity

**Realised as S–M.**

### Risks

- Scope creep into a full legacy `LoggingService` migration larger than
  this release can absorb alongside its other Work Packages. **Avoided**
  — this Work Package deliberately decided not to migrate, rather than
  attempting a partial migration.

---

## WP 5.3 — Developer Experience Improvements

**Status note.** Not started. Formerly `WP 4.9` under the `v0.4.0` plan.

### Objective

Package and polish the release: templates, scaffolding, and documentation
that make everything above approachable, not just possible.

### Scope

- Project/module templates (for example, `dotnet new` templates for a
  new TempestOS module), informed directly by `WP 4.3`'s sample module
  and everything it grew into.
- Improved diagnostic/error messages surfaced as unclear during this
  release's own development.
- Documentation polish across the Academy and SDK docs this release
  produced.

### Dependencies

**`WP 4.0`, `WP 4.1`, `WP 4.2`, `WP 4.3`** — templates require the
surfaces they template to be stable first.

### Deliverables

- At least one project/module template.
- A documentation pass across every new Academy/SDK document this
  release produced.

### Acceptance Criteria

- A new contributor can scaffold a working module using a template alone,
  without hand-copying the sample module.

### Estimated Complexity

**S–M.**

### Risks

- Treated as a dumping ground for anything left unfinished elsewhere,
  rather than a scoped polish pass.
