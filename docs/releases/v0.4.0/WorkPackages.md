# TempestOS v0.4.0 — Work Packages

## How to Read This Document

**Revised** following review of the first planning pass. Three structural
changes from that first pass, all incorporated below:

1. **Numbering aligned to the release**: `WP 4.0` through `WP 4.9`, not
   `WP 3.x` — a work package's number should tell a reader, unambiguously,
   which release it belongs to, years later.
2. **A new work package, WP 4.0 (Platform Contracts), precedes everything
   else** — every core interface is defined once, before any
   implementation, so no later work package invents its own conventions.
3. **The Sample Module moved from last to early (WP 4.3)**, and is no
   longer a one-time final integration proof — it is a **living reference
   module** that WP 4.4 onward extend and validate against, so every
   subsequent subsystem is built against a real consumer instead of a
   hypothetical one. Each later work package's Acceptance Criteria says so
   explicitly, not as an afterthought.

Navigation (`WP 4.6`) is split into an architecture-only phase (`4.6A`) and
an implementation phase (`4.6B`), per its own risk profile — see
`Architecture.md` and `Risks.md`.

Four architecture-significant questions are now **decided**, before any
implementation, applied throughout this document rather than re-litigated
per work package:

- **ADR-0020** — the Event Bus is DI-public.
- **ADR-0021** — background service failures are isolated by default;
  criticality is opt-in.
- **ADR-0022** — Navigation and Command Framework are orthogonal; neither
  depends on the other. This resolves the Navigation/Command Framework
  dependency question the reordering above raised — `WP 4.6A` no longer
  depends on `WP 4.7`.
- **ADR-0023** — platform-wide dependency layering (Modules → Platform
  APIs → Platform Services → Runtime Host, downward only). Applies beyond
  this release; see `docs/releases/FOUNDATION.md`.

`WP 4.0`'s own scope is narrower than first drafted, per a governing
philosophy adopted during review: **only define a contract when there is
enough understanding to make it stable.** `INavigationProvider` and
`IDiagnosticsProvider` are not defined by `WP 4.0` at all, not even
provisionally — see `WP 4.0`'s own Scope section.

**Update, WP 4.2D (Platform Services Architecture Review):** `WP 4.0`
through `WP 4.2` are now complete — see each work package's own entry
below, and its Academy retrospective(s), for current status. `WP 4.3`
onward have not begun.

---

## WP 4.0 — Platform Contracts

### Objective

Define every core platform interface before implementing any of them, so
that WP 4.1 through WP 4.9 build against one settled contract surface
instead of each inventing its own conventions along the way.

### Scope

**Governing philosophy, adopted during planning review: only define a
contract when there is enough understanding to make it stable. Everything
else waits until its owning work package has done the design work.**

Author exactly the contracts already well-reasoned enough to be stable:

- `IModule`, `IModuleLifecycle` (both already exist — re-affirmed as the
  base of the contract surface, not redefined).
- `IHostedService` (the background-service contract, per ADR-0021's
  failure model).
- `ICriticalBackgroundService` (the opt-in critical marker, ADR-0021).
- `ICommand` (Command Framework's shape).
- `IEvent`, `IEventHandler<T>` (Event Bus, per ADR-0020).

**Nothing else.** `INavigationProvider` and `IDiagnosticsProvider` are not
defined here — not even provisionally. Navigation's own architecture
(`WP 4.6A`) and Diagnostics' own scoping (`WP 4.8`) have not happened yet;
defining either contract now, even as a marked placeholder, would still be
speculative abstraction ahead of real understanding — exactly what
ADR-0015's Future Considerations already warned against ("designing…
speculatively, ahead of a real need, is exactly the kind of premature
complexity this Academy's own principles… argue against"). Each contract
is defined once, by the work package that has actually done the design
work to make it stable — never earlier, for the sake of completeness.

### Dependencies

None — this is the first work package in the release.

### Deliverables

- A dedicated contracts surface (a decision this work package makes
  explicitly: a new `Tempest.Core.Contracts` namespace, or contracts
  distributed across existing namespaces by capability — either is
  acceptable; an unstated default is not).
- Exactly the six contracts named above, each carrying XML documentation
  sufficient for a module author to build against it without reading a
  reference implementation first.
- No other contract. `INavigationProvider` and `IDiagnosticsProvider`
  remain entirely undefined until `WP 4.6A` and `WP 4.8` respectively.

### Acceptance Criteria

- Every subsequent work package that needs one of these six contracts
  builds against the definition here, not one it invents itself.
- No contract exists in this work package for a capability whose
  architecture has not yet been decided — checked as a literal review
  gate, not a guideline.

### Estimated Complexity

**S–M.** Narrower than originally scoped — six well-understood interfaces,
not a broad catalogue.

### Risks

- **The single biggest risk this work package carries is doing the exact
  opposite of its own purpose**: defining a contract speculatively, ahead
  of real design. The mitigation is no longer "mark it provisional" — it is
  "do not include it at all." `INavigationProvider` and
  `IDiagnosticsProvider` are absent from this work package's deliverables,
  full stop.
- **`IHostedService` naming.** `Microsoft.Extensions.Hosting.IHostedService`
  is a well-known name in the wider .NET ecosystem. TempestOS's own
  `IHostedService` is unrelated, and the project has no dependency on that
  package (ADR-0005), but a contributor arriving with ASP.NET Core
  experience could reasonably expect different semantics. Worth the same
  kind of explicit clarifying note ADR-0016 gave `Tempest.Core.Runtime` vs.
  `Tempest.Core.Hosting` — a decision for this work package to make
  (rename, or document the distinction plainly), not left implicit.

---

## WP 4.1 — Module SDK

### Objective

Give module authors a real, documented, stable surface to build against —
packaged from the contracts `WP 4.0` defines, not a separate, competing
description of them.

### Scope

- Package `WP 4.0`'s module-facing contracts (`IModule`, `IModuleLifecycle`)
  into a documented SDK surface.
- Author guidance: constructor side-effect-freedom (ADR-0003), the
  discovery/registration/lifecycle sequence a module can expect, what a
  module must never attempt (reaching into Discovery, Registration, or
  Lifecycle directly — ADR-0017; reaching into another module directly —
  ADR-0020).
- Decide and document packaging (a dedicated `Tempest.SDK`
  project/package, or the existing `Tempest.Core.Modules` namespace
  re-exposed as the public surface).

### Dependencies

**WP 4.0 (Platform Contracts)** — the SDK documents and packages contracts
defined there; it does not define its own.

### Deliverables

- SDK packaging decision, documented (ADR if it meets Governance §5
  criteria).
- Module-author documentation (a new Academy "Building a Module" guide).
- No new runtime behaviour — this work package packages and documents an
  existing contract; it does not change `IModule`/`IModuleLifecycle` unless
  scope investigation finds a genuine gap.

### Acceptance Criteria

- A module author unfamiliar with TempestOS's internals can, from SDK
  documentation alone, build a working module without reading
  `ModuleLifecycleManager`'s source.
- All 164 existing tests continue to pass unmodified.

### Estimated Complexity

**S–M.**

### Risks

- Scope creep: a temptation to "improve" `IModule`/`IModuleLifecycle` while
  documenting them. Resist unless `WP 4.0` itself already found a genuine
  gap.

---

## WP 4.2 — Plugin Manifest

**Status note.** This work package's own design phase (architecture only)
surfaced three prerequisites that turned into their own, separately
tracked sub-work-packages before implementation could proceed: **WP 4.2A**
(*Runtime Platform Version Infrastructure* — complete), **WP 4.2B**
(*ADR: Plugin Failure Classification* — complete, ADR-0025), and **WP
4.2C** (*ADR: Plugin Discovery Lifecycle Placement* — complete,
ADR-0026). **Implementation is now complete** — `Tempest.Core.Plugins`
(`PluginManifest`, `PluginManifestDiscoveryService`, `PluginAssemblyLoader`),
wired into `TempestHost` exactly per ADR-0026, with 27 new tests. See
`Plugin Manifest Architecture.md`'s own Recommendation section, and the
WP 4.2 implementation retrospective, for the current, authoritative
status. **`WP 4.2D`** (*Platform Services Architecture Review*) then
formally reviewed the entire `WP 4.0`–`WP 4.2` milestone before `WP 4.3`
began — see `Platform Services Architecture Review.md`. No architectural
issue was found; nine documentation cross-references were corrected.

### Objective

Define a manifest format describing a plugin before its assembly is
necessarily loaded — closing the gap `Runtime Host Architecture.md` named
since WP 2.7A.

### Scope

- Design a manifest schema (likely JSON): at minimum what
  `ModuleDescriptor` already captures (Id, Name, Version) plus what
  discovery cannot know before an assembly is loaded (which file to load,
  declared dependencies on other plugins, compatible TempestOS version
  range).
- Design where manifest reading sits in the Host's sequence — logically
  before Module Discovery, per the existing architecture's own note.
  **Decided — ADR-0026**: two new phases, `3.1 Plugin Discovery` and
  `3.2 Plugin Loading`, between Logging Built and Module Discovery.
- Out of scope unless this work package's own risk assessment concludes
  otherwise: dynamic assembly loading/unloading (`AssemblyLoadContext`
  isolation).

### Dependencies

**WP 4.0 (Platform Contracts)**, **WP 4.1 (Module SDK)** — a manifest
describes an SDK-built module against a settled contract surface, not a
moving target.

### Deliverables

- A versioned manifest schema — **done**: `PluginManifest`
  (`Tempest.Core.Plugins`), read from `plugin.manifest.json`.
- A manifest-reading step in the Host's startup sequence design — **done**:
  `Host Lifecycle.md` gains two new phases (`3.1`/`3.2`), per ADR-0026,
  implemented by `PluginManifestDiscoveryService`/`PluginAssemblyLoader`.
- `src/Plugins/` (empty since WP 2.1) remains a placeholder a little
  longer, by design: this work package builds the runtime infrastructure
  a plugin needs, not a first real plugin itself — that is `WP 4.3`'s own
  scope ("optionally packaged via `WP 4.2`'s Plugin Manifest, if ready").

### Acceptance Criteria

- A plugin manifest can be authored and read without loading the plugin's
  assembly first — **verified**: `PluginManifestDiscoveryServiceTests`.
- `Host Lifecycle.md` is updated to reflect exactly where manifest reading
  occurs, with the same rigour every existing phase already has — **done**.

### Estimated Complexity

**M–L.**

### Risks

- Risk of quietly reopening `Host Lifecycle.md`'s frozen phase table
  without WP 2.7A/B's original rigour — **resolved via ADR-0026**, given
  the same rigour as the original phase table. Coordinate explicitly with
  **WP 4.5 (Background Services)**, which also touches this table (see
  `Risks.md`, R4) — WP 4.5 now has ADR-0026's decimal sub-numbering
  precedent to follow.

---

## WP 4.3 — Sample Module

**Status note.** This work package's own design phase (architecture only)
is complete — see `Sample Module Architecture.md` and the WP 4.3
architecture retrospective. No blocking prerequisite exists for `WP 4.3`
itself; implementation may begin directly. The design phase did surface
one significant finding relevant to `WP 4.4`: extending the sample module
to publish an event via `IEventBus` requires resolving a real tension
between Discovery's zero-argument metadata probe and constructor
injection — identified as an ADR `WP 4.4` should resolve as its own first
step, not decided or implemented here.

### Objective

Build one concrete, non-trivial module against `WP 4.0`'s contracts and
`WP 4.1`'s SDK — early enough that it becomes a **living reference** every
subsequent work package extends and validates against, rather than a
one-time proof written after everything else already exists.

### Scope

- One realistic sample module (and, where a scenario genuinely requires a
  second party, one small companion module — see `WP 4.4`'s acceptance
  criteria) — not a "hello world" stub.
- Written *as* SDK documentation would expect a third-party author to
  write it — this validates `WP 4.1`'s documentation as much as it builds
  a module.
- Optionally packaged via `WP 4.2`'s Plugin Manifest, if ready.

### Dependencies

**WP 4.0, WP 4.1, WP 4.2.**

### Deliverables

- A working sample module, buildable and discoverable exactly as a
  third-party module would be.
- **An explicit commitment, binding on WP 4.4 onward**: later work
  packages extend this same module (or its small companion, where two
  parties are needed) rather than each writing their own disposable test
  fixture to prove their own subsystem in isolation.

### Acceptance Criteria

- The sample module builds, is discovered, registers, initialises, starts,
  and stops cleanly through the ordinary Runtime Host sequence, with no
  special-casing.
- Any gap found in `WP 4.0`/`WP 4.1`/`WP 4.2` as a result is fed back into
  those work packages' own documentation, not silently worked around.

### Estimated Complexity

**S.**

### Risks

- **The benefit of moving this early is entirely lost if later work
  packages don't actually come back and extend it.** Mitigated by making
  "extends the WP 4.3 sample module" an explicit, checked line in every
  later work package's own Acceptance Criteria — see `WP 4.4` onward,
  below.

---

## WP 4.4 — Event Bus

### Objective

Give modules a way to publish and subscribe to events without reaching
into each other directly. **Placement decided — ADR-0020**: `IEventBus` is
DI-public, resolved like `IConfigurationProvider`/`ILogger`, never a
Host-owned collaborator.

**Status note, from WP 4.3's own design phase.** Extending the sample
module to publish an event (this work package's own Deliverable, below)
requires constructor-injecting `IEventBus` into a normally-discovered
module — which collides directly with the parameterless-constructor-only
constraint `WP 4.1` documented and `WP 4.3`'s design phase traced to its
exact cause (`Sample Module Architecture.md`, "Required ADRs"). Resolving
this should be `WP 4.4`'s own first step, via its own ADR, before
attempting the event-publishing extension itself.

### Scope

- Implement `IEventBus` against `WP 4.0`'s `IEvent`/`IEventHandler<T>`
  contracts.
- Publish/subscribe semantics: dispatch ordering, and — mirroring the
  module pipeline's own per-module failure isolation — a throwing
  subscriber must not prevent sibling subscribers from receiving the same
  event, and must not fault the Host.
- Re-entrancy policy (a subscriber publishing a new event from within its
  own handler): decided explicitly here, not discovered as a bug later.

### Dependencies

**WP 4.0** (contracts), **WP 4.3** (extend the sample module rather than
building a new, disposable test fixture).

### Deliverables

- `IEventBus` implementation, registered during Platform Services
  Registered per ADR-0020.
- The `WP 4.3` sample module extended to publish an event; its companion
  module (added here, if one does not already exist) extended to subscribe
  to it — proving the bus against two real, SDK-conformant modules, not a
  synthetic fixture.

### Acceptance Criteria

- The sample module (or its companion) publishes an event; a second,
  separate module receives it, resolved entirely through constructor
  injection — no direct reference between the two modules exists anywhere
  in the proof.
- A throwing subscriber does not prevent a sibling subscriber from
  receiving the same event, and does not fault the Host.

### Estimated Complexity

**M.**

### Risks

- See `Risks.md` R3 for the Event Bus/Command Framework distinction, which
  this work package should document explicitly even though Command
  Framework (`WP 4.7`) lands later in this release's sequence.

---

## WP 4.5 — Background Services

### Objective

Implement the hosted-service extensibility seam `Runtime Host
Architecture.md` named but left undesigned. **Failure classification
decided — ADR-0021**: isolated by default; Host-fatal only if a service
explicitly declares itself critical.

### Scope

- Implement `IHostedService` (per `WP 4.0`) and the Host-level wiring to
  start it between Module Initialisation and Runtime Running, and stop it
  symmetrically at the front of Shutdown.
- Implement the critical-service opt-in (`ICriticalBackgroundService` or
  `BackgroundServiceOptions.IsCritical` — WP 4.0/4.5's own choice) and wire
  its Host-fatal path.

### Dependencies

**WP 4.0** (contracts, ADR-0021), **WP 4.3** (extend the sample module or
its companion with a background service, rather than a synthetic fixture).
Coordinate explicitly with **WP 4.2**, which also touches `Host
Lifecycle.md`'s phase table (see `Risks.md`, R4).

### Deliverables

- Hosted-service contract and Host-level start/stop wiring.
- `Host Lifecycle.md`, `Runtime State Machine.md`, and `Failure
  Behaviour.md` updated with the new phase(s) and ADR-0021's failure rule,
  including an explicit new row in `Failure Behaviour.md`'s Required
  Behaviour Summary table.
- The sample module set gains a background service demonstrating both the
  isolated-failure default and the critical opt-in.

### Acceptance Criteria

- A background service starts after Module Initialisation and stops
  before Module Disposal, observably, in a test.
- An ordinary (non-critical) background service that throws is isolated —
  the Host reaches or remains `Running` regardless.
- A service declared critical that throws is Host-fatal, mirroring a
  platform-service failure.

### Estimated Complexity

**L.** Touches the Host's frozen startup/shutdown sequence directly — the
single riskiest touch-point in this release.

### Risks

- The work package most likely to tempt a change to `TempestHost`'s core
  sequencing. Review any change here with the same weight WP 2.7A/B gave
  the original design.

---

## WP 4.6A — Navigation Architecture

### Objective

Decide what "navigation" means for TempestOS before writing any
implementation — the least architecturally grounded objective in this
release, and the one work package explicitly run as an architecture-only
phase, mirroring WP 2.7A's approach to the Runtime Host itself.

### Scope

- Routing model, state model, and the `INavigationProvider`/
  `NavigationService` contract (interfaces, not implementation) — defined
  here, from scratch, since `WP 4.0` deliberately does not define it.
- **The Navigation/Command Framework dependency question is resolved —
  ADR-0022.** Navigation and Command Framework are orthogonal platform
  services; neither depends on the other. `WP 4.6A` designs its routing
  model and service interface as a standalone peer service, consumed by
  application logic exactly as `IEventBus` is — never by the command
  dispatcher directly, and never depending on `ICommand` itself.
- Does *not* decide whether this belongs in `Tempest.Core` at all — see
  `Architecture.md`'s open question.

### Dependencies

**WP 4.0** (whatever base contracts already exist). **No longer depends on
WP 4.7 (Command Framework)** — ADR-0022 makes the two orthogonal, which is
exactly what allows this release to sequence Navigation before Command
Framework coherently.

### Deliverables

- A written architecture document (before any code), mirroring `Runtime
  Host Architecture.md`'s own rigour, explicitly answering: what does
  navigation mean here; does it belong in `Tempest.Core`; how application
  logic is expected to wire commands and navigation together per
  ADR-0022's shape (event-reaction or direct call), without the two
  services depending on each other.
- `INavigationProvider`, entirely undefined until now, is designed here.

### Acceptance Criteria

- A written architecture decision exists and is reviewed before `WP 4.6B`
  begins.
- The design demonstrates at least one of ADR-0022's two illustrative
  shapes (event-reaction, or direct application-logic call) concretely,
  not only in the abstract.

### Estimated Complexity

**Unknown — provisionally L** (reduced from XL now that the Command
Framework dependency question is resolved rather than open) until this
work package's remaining question — whether Navigation belongs in
`Tempest.Core` at all — is answered. Complexity here is blocked on that
decision, not on effort.

### Risks

- Highest-risk work package in the release by a clear margin — see
  `Risks.md`, R2 (reduced risk; R10 is now retired).

---

## WP 4.6B — Navigation Implementation

### Objective

Implement what `WP 4.6A` designs.

### Scope

Defined entirely by `WP 4.6A`'s own deliverable — this entry is
intentionally thin until that architecture exists.

### Dependencies

**WP 4.6A** (required, blocking). **Not WP 4.7 (Command Framework)** —
ADR-0022 makes the two orthogonal; `WP 4.6B` may proceed once `WP 4.6A`
completes, regardless of whether `WP 4.7` has landed yet.

### Deliverables

Whatever `WP 4.6A`'s architecture document specifies.

### Acceptance Criteria

Whatever `WP 4.6A`'s architecture document specifies, at minimum including:
the sample module set (`WP 4.3`) demonstrates at least one real navigation
transition.

### Estimated Complexity

Not estimated until `WP 4.6A` completes.

### Risks

Inherits every risk `WP 4.6A` does not fully resolve.

---

## WP 4.7 — Command Framework

### Objective

Give the platform a uniform way to define and dispatch commands.

### Scope

- Implement `ICommand` (per `WP 4.0`) and a dispatcher.
- **Document the command/event distinction explicitly**: a command has
  exactly one handler and an expected result; an event has zero or more
  subscribers and no expected result (`WP 4.4`'s Event Bus). State this
  plainly enough that a future contributor does not need to guess which
  one to reach for.
- **Respect ADR-0022**: the command dispatcher never depends on, or calls
  into, `INavigationProvider`/`NavigationService` directly. Where a
  command's application logic needs to trigger navigation, it depends on
  `NavigationService` itself, as a peer — not routed through the command
  framework.

### Dependencies

**WP 4.0**, **WP 4.3** (extend the sample module set rather than a
disposable fixture). Cross-reference **WP 4.4 (Event Bus)** explicitly for
the command/event distinction, despite the sequence gap between them. Not
dependent on **WP 4.6A/4.6B (Navigation)** — ADR-0022.

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

- See `Risks.md`, R3 — the sequence gap between `WP 4.4` and `WP 4.7`
  reduces the risk of the two being designed in a rush and blurring
  together, but increases the risk that nobody circles back to
  cross-reference them explicitly. This work package's own deliverables
  make that cross-reference mandatory, not optional.

---

## WP 4.8 — Diagnostics Improvements

### Objective

Close existing, named diagnostics debt and extend health/status visibility
using data the platform already produces.

### Scope

- **Single-sink logging limitation** (debt since WP 2.6): composite
  `ILogSink` for fan-out to multiple sinks.
- **Two coexisting logging mechanisms** (`ILogger` vs. legacy
  `LoggingService`): assess whether this release migrates
  `BootstrapService`/`HostingService`/`Program.cs` (ADR-0010's Future
  Considerations), or explicitly re-scopes the debt forward again.
- **Health/status reporting**: a read-only projection over
  `IModuleLifecycleManager`'s existing snapshot data, exactly as
  ADR-0017's Future Considerations anticipated.
- Define `IDiagnosticsProvider` from scratch — `WP 4.0` deliberately does
  not define it, per its own governing philosophy (only define a contract
  once there is enough understanding to make it stable).

### Dependencies

Benefits from, but does not strictly require, **WP 4.5 (Background
Services)** if health reporting runs as a periodic background check.

### Deliverables

- Composite `ILogSink`.
- A documented decision on the legacy `LoggingService` migration question.
- A read-only health/status service, DI-resolvable, built the way
  ADR-0017 prescribed.

### Acceptance Criteria

- Log output can be written to more than one sink simultaneously without
  any consumer of `ILogger` changing.
- A consumer can query every module's state without gaining write access
  to `IRuntimeModuleManager`/`IModuleLifecycleManager` themselves.

### Estimated Complexity

**S–M.**

### Risks

- Scope creep into a full legacy `LoggingService` migration larger than
  this release can absorb alongside eight other work packages.

---

## WP 4.9 — Developer Experience Improvements

### Objective

Package and polish the release: templates, scaffolding, and documentation
that make everything above approachable, not just possible.

### Scope

- Project/module templates (for example, `dotnet new` templates for a new
  TempestOS module), informed directly by `WP 4.3`'s sample module and
  everything it grew into by `WP 4.7`.
- Improved diagnostic/error messages surfaced as unclear during this
  release's own development.
- Documentation polish across the Academy and SDK docs this release
  produced.

### Dependencies

**WP 4.0, WP 4.1, WP 4.2, WP 4.3** — templates require the surfaces they
template to be stable first.

### Deliverables

- At least one project/module template.
- A documentation pass across every new Academy/SDK document this release
  produced.

### Acceptance Criteria

- A new contributor can scaffold a working module using a template alone,
  without hand-copying the sample module.

### Estimated Complexity

**S–M.**

### Risks

- Treated as a dumping ground for anything left unfinished elsewhere,
  rather than a scoped polish pass.
