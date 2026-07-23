# TempestOS v0.4.0 — Architecture Review

## Purpose

Before proposing any v0.4.0 work package, this document reviews what already
exists and states, explicitly, what this release reuses unchanged, what it
extends, and where — if anywhere — it has a compelling reason to introduce
something genuinely new. Per the release brief: *reuse everything that
already exists; do not redesign completed architecture unless there is a
compelling engineering reason; assume every ADR accepted in a previous
release remains valid.*

## What Exists Today (v0.3.0 Baseline)

The Runtime Foundation, as of the `v0.3.0` tag:

- **Six platform services** — Configuration, Logging, Discovery,
  Registration, Dependency Injection, Lifecycle — each independently
  designed, implemented, tested, and documented (WP 2.1–2.6).
- **The Runtime Host** (`TempestHost`/`TempestHostBuilder`,
  `Tempest.Core.Runtime`) — the single composition root and entry point
  (WP 2.7A/2.7B), owning orchestration, startup, shutdown, cancellation,
  and disposal, with its own independent 7-state machine (ADR-0012).
- **The platform-service/module failure boundary** (ADR-0013) — a
  platform-service failure is Host-fatal; a module failure is isolated.
  Every future component this release adds must be classified against this
  boundary, not left ambiguous.
- **19 ADRs**, an Engineering Governance model, an Engineering Glossary, and
  a full Academy (principles, runtime architecture, work package
  retrospectives, design patterns, case studies, engineering standards).
- **A named, not-yet-designed extensibility seam.** `Runtime Host
  Architecture.md`'s "Future Extensibility" section already anticipates
  most of what v0.4.0 sets out to build: hosted services (background work
  starting alongside, and stopping symmetrically with, the module
  pipeline), a Requirements Engine and/or Project Engine (each requiring
  ADR-0013 classification before being added), and plugins (assemblies
  loaded from disk, before Module Discovery runs). None of these were
  designed in detail — that design work is what several of this release's
  work packages now pick up.

## What This Release Must Not Redesign

Unless a specific work package's own planning surfaces a compelling,
documented reason (in which case it gets its own ADR, per Engineering
Governance §5):

- **The Runtime Host's state machine** (`HostState`, ADR-0012) — `Created →
  Starting → Running → Stopping → Stopped/Faulted → Disposed`, with restart
  prohibited (ADR-0015) and disposal always explicit and idempotent
  (ADR-0019).
- **The platform-service/module failure boundary** (ADR-0013) — a new
  capability is *classified* against this boundary; the boundary itself
  does not move to accommodate a new capability's convenience.
- **Discovery, Registration, and Lifecycle's Host-owned, non-DI-public
  status** (ADR-0017) — a module still has no path back into the machinery
  orchestrating it. Any new module-facing capability (Event Bus, Command
  Framework) must be reachable via ordinary constructor injection like
  `IConfigurationProvider`/`ILogger`, never by exposing Discovery,
  Registration, or Lifecycle themselves.
- **The Atomic Phase Principle** (Engineering Principle 11) — any new
  batch or sequenced operation this release introduces (background service
  startup/shutdown, event dispatch ordering) observes cancellation only
  between atomic operations, never mid-operation, exactly as
  `ModuleLifecycleManager` and `TempestHost` already do.
- **The Composition Root pattern** (ADR-0009, reused by WP 2.6 and WP
  2.7B) — a service that cannot be constructed by ordinary reflection-based
  DI (because it needs to exist before the container does, or requires a
  method call rather than a constructor) is built at the composition root
  and registered via `AddInstance`, not worked around with a bespoke
  mechanism.

## Decisions Made During Planning

Four architecture-significant questions this document originally raised, or
that a later planning revision itself surfaced, are now **decided**, before
any implementation:

1. **Event Bus placement — ADR-0020.** `IEventBus` is DI-public, resolved
   like `IConfigurationProvider`/`ILogger`. Not Host-owned like Discovery,
   Registration, or Lifecycle (ADR-0017), because it does not orchestrate
   the module pipeline — it carries messages between modules, and carries
   no authority to register, initialise, start, stop, or dispose anything.
2. **Background service failure classification — ADR-0021.** Isolated by
   default, mirroring module failure isolation (ADR-0013); Host-fatal only
   if a service explicitly declares itself critical. Extends ADR-0013's
   boundary with a third default rather than contradicting it.
3. **Navigation/Command Framework relationship — ADR-0022.** Orthogonal.
   Neither depends on the other; application logic wires intent (a
   command) to execution (navigation, or an event another service reacts
   to) explicitly. This resolves the dependency-direction inversion the
   Sample-Module-first reordering introduced (previously an open question,
   tracked as Risk R10 — now retired) and means `WP 4.6A` no longer
   depends on `WP 4.7`.
4. **Platform-wide dependency layering — ADR-0023.** Modules → Platform
   APIs → Platform Services → Runtime Host, dependencies flow downward
   only. This formalises, as one named rule, boundaries ADR-0013,
   ADR-0017, and ADR-0020 already established independently — it does not
   add a new constraint, it consolidates existing ones into one checkable
   review question. Applies platform-wide, not only to this release — see
   `docs/releases/FOUNDATION.md`.

All four are applied throughout `WorkPackages.md` rather than re-derived
per work package.

## Where a New Decision Is Still Needed

1. **What does a Plugin Manifest add that `ModuleDescriptor` does not
   already capture?** `ModuleDescriptor` is discovery-time metadata read
   from a loaded, reflectable type. A Plugin Manifest is (implicitly)
   metadata read from *disk*, *before* the containing assembly is
   necessarily loaded at all. This is a materially different moment in the
   sequence — likely inserted *before* Module Discovery, exactly as
   `Runtime Host Architecture.md` already anticipated for plugins generally
   — and needs its own design, not a reuse of `ModuleDescriptor`'s shape by
   assumption. (`WP 4.2`.)
2. **Does Navigation belong in `Tempest.Core` at all?** Everything built so
   far is UI-agnostic; `Tempest.App` is a console loop. This remains the
   one objective in this release with the least existing architectural
   grounding — explicitly split into an architecture-only phase
   (`WP 4.6A`) before any implementation (`WP 4.6B`), mirroring WP 2.7A's
   approach to the Runtime Host itself. (Its relationship to Command
   Framework, previously the harder half of this question, is now settled
   by ADR-0022 — only the `Tempest.Core` placement question remains open.)
3. **`IHostedService` naming risk.** `Microsoft.Extensions.Hosting`'s own
   `IHostedService` is a well-known name elsewhere in .NET. TempestOS's is
   unrelated (ADR-0005: no dependency on that package), but the naming
   proximity is worth a deliberate decision in `WP 4.0` — rename, or
   document the distinction plainly, mirroring ADR-0016's treatment of
   `Tempest.Core.Runtime` vs. `Tempest.Core.Hosting`.

`WP 4.0`'s risk of defining contracts ahead of design (`INavigationProvider`,
`IDiagnosticsProvider`) is no longer an open question to manage — it is
resolved structurally: `WP 4.0` does not define either contract at all,
not even provisionally. See `WorkPackages.md`'s `WP 4.0` entry.

## Reuse Map

| Work Package | Primarily Reuses | Status of Its New Decision |
|---|---|---|
| WP 4.0 Platform Contracts | Existing `IModule`/`IModuleLifecycle`; applies ADR-0020/0021 | Packaging/namespace decision only; scope deliberately narrowed |
| WP 4.1 Module SDK | `IModule`, `IModuleLifecycle`, Discovery, Registration | Packaging/versioning story only |
| WP 4.2 Plugin Manifest | `ModuleDescriptor`'s role as a model | **Open** — manifest-reading placement (see above) |
| WP 4.3 Sample Module | Everything from WP 4.0–4.2 | None — this is a proof, not a new component |
| WP 4.4 Event Bus | Composition Root pattern, DI container | **Decided — ADR-0020** |
| WP 4.5 Background Services | Runtime Host's named "hosted services" seam | **Decided — ADR-0021** |
| WP 4.6A Navigation Architecture | (least reuse — see above) | **Decided — ADR-0022** (Command dependency); **Open** — `Tempest.Core` placement |
| WP 4.6B Navigation Implementation | Whatever WP 4.6A decides | Inherits only WP 4.6A's remaining open question |
| WP 4.7 Command Framework | DI container, Module SDK | Command/event distinction to document; orthogonal to Navigation (ADR-0022) |
| WP 4.8 Diagnostics Improvements | `IModuleLifecycleManager` snapshots, ADR-0017's Future Considerations | Mostly closes existing, named debt — low new-decision risk |
| WP 4.9 Developer Experience | Everything above | None expected |

## Platform Layering (ADR-0023)

Every row in the Reuse Map above, and every future work package this
release or any later one proposes, is additionally checked against
ADR-0023's four-layer rule: Modules → Platform APIs → Platform Services →
Runtime Host, dependencies flowing downward only, with Service → Module,
Module → Module, and Runtime → Feature all explicitly forbidden. This
review document's own "What This Release Must Not Redesign" section above
already named the three existing ADRs (ADR-0013, ADR-0017, ADR-0020)
ADR-0023 formalises; ADR-0023 itself, not this document, is the citable
record of the general rule.

## Governing Instruction for Every Work Package

Every work package in `WorkPackages.md` states, explicitly, what it reuses
and what — if anything — it proposes as new. A work package that cannot name
what it reuses has not been scoped carefully enough yet.
