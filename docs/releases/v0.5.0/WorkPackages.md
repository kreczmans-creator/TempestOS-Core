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

**Status note.** Not started.

### Objective

Implement what `WP 5.0C` designed.

### Scope

Defined entirely by `WP 5.0C`'s own deliverable (`docs/architecture/Shell
& Composition Framework Architecture.md`) — this entry is intentionally
thin until implementation begins; the architecture itself, not this
entry, is authoritative on shape.

### Dependencies

**`WP 5.0C`** (required, blocking; complete). **Not `WP 5.1`** (Command
Framework) — `WP 5.0D` may proceed regardless of whether `WP 5.1` has
landed yet, per the same orthogonality reasoning `ADR-0022` already
established for Navigation.

### Deliverables

Whatever `WP 5.0C`'s architecture document specifies: `ITempestHost.Services`;
the Shell itself as `Tempest.App`'s own composition root; Workspace,
Navigation Region, and Content Region, populated; a reserved, unpopulated
Status Bar region.

### Acceptance Criteria

Whatever `WP 5.0C`'s architecture document specifies, at minimum
including: `Tempest.App` constructs and runs a real `ITempestHost`; the
Shell resolves `INavigationProvider` through `Services` and renders its
`Items`; selecting a navigation item is observed to update the Content
Region via a real `NavigationRequestedEvent`.

### Estimated Complexity

**M.**

### Risks

Inherits any risk `WP 5.0C` did not fully resolve — none currently named;
see the `WP 5.0C` retrospective's own "Readiness assessment."

---

## WP 5.1 — Command Framework

**Status note.** Not started; only its contract (`ICommand`, `WP 4.0`)
exists. Formerly `WP 4.7` under the `v0.4.0` plan.

### Objective

Give the platform a uniform way to define and dispatch commands.

### Scope

- Implement `ICommand` (per `WP 4.0`) and a dispatcher.
- **Document the command/event distinction explicitly**: a command has
  exactly one handler and an expected result; an event has zero or more
  subscribers and no expected result (`WP 4.4`'s Event Bus).
- **Respect `ADR-0022`**: the command dispatcher never depends on, or
  calls into, `INavigationProvider`/`NavigationService` directly. Where a
  command's application logic needs to trigger navigation, it depends on
  `NavigationService` itself, as a peer — not routed through the command
  framework.

### Dependencies

**`WP 4.0`**, **`WP 4.3`** (extend the sample module set). Cross-reference
**`WP 4.4`** (Event Bus) for the command/event distinction. Not dependent
on **`WP 5.0A`/`WP 5.0B`** (Navigation) — `ADR-0022`.

### Deliverables

- Command contract implementation and dispatcher, DI-resolvable like the
  Event Bus.
- The command/event distinction documented in the Engineering Glossary.
- The sample module set extended with at least one registered command
  handler.

### Acceptance Criteria

- A module can register a command handler and have it invoked by ID with
  typed parameters, proven against the sample module set.
- The command/event distinction is documented clearly enough to resolve
  `Risks.md`'s R3.

### Estimated Complexity

**M.**

### Risks

- See `Risks.md`, R3 — the sequence gap between the Event Bus and Command
  Framework's own implementation increases the risk that nobody circles
  back to cross-reference them explicitly. This Work Package's own
  deliverables make that cross-reference mandatory, not optional.

---

## WP 5.2 — Diagnostics Improvements

**Status note.** Not started. Formerly `WP 4.8` under the `v0.4.0` plan.

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

### Deliverables

- Composite `ILogSink`.
- A documented decision on the legacy `LoggingService` migration question.
- A read-only health/status service, DI-resolvable.

### Acceptance Criteria

- Log output can be written to more than one sink simultaneously without
  any consumer of `ILogger` changing.
- A consumer can query every module's state without gaining write access
  to `IRuntimeModuleManager`/`IModuleLifecycleManager` themselves.

### Estimated Complexity

**S–M.**

### Risks

- Scope creep into a full legacy `LoggingService` migration larger than
  this release can absorb alongside its other Work Packages.

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
