# Runtime Host Architecture

**Status: implemented — WP 2.7B.** `TempestHost`, `TempestHostBuilder`,
`ITempestHost`, and `ITempestHostBuilder` implement this design in
`Tempest.Core.Runtime`.

**Clarification, WP 2.7B:** implementation confirmed that Configuration,
Logging, Discovery, Registration, and Dependency Injection are constructed by
`TempestHost` itself, directly, exactly as this document's "Relationship to
Existing Services" section below already said — not by `TempestHostBuilder`,
despite WP 2.7B's own brief summarising the composition root the other way
around. `Host Lifecycle.md`'s Phase 1 ("Host Created… holding no platform
service references yet") preceding Phase 2 ("Configuration Built") only holds
if the Host itself builds these services after it exists, not before — see
the WP 2.7B retrospective's Alternatives Considered section for the full
reasoning, and ADR-0011 for the precedent this reconciliation follows.

## Overview

The Runtime Host is the single entry point to TempestOS: the one component
whose job is to bring every platform service up, in the right order, hold the
platform in a running state, and bring everything back down again, cleanly,
whenever asked or whenever something goes wrong. It is the "Host (planned)"
entry in the Platform Service Map, and this document, together with the other
five produced by WP 2.7, is that entry's design.

The Host is deliberately thin. It does not implement Configuration, Logging,
Dependency Injection, Discovery, Registration, or Lifecycle — those are
already fully implemented, tested, and documented (WP 2.1–2.6). The Host's job
is to call them, in order, and to own the questions none of them individually
answers: what order do the six services come up in; what happens if one
fails; how does the whole platform shut down; what does "the platform's
current state" even mean, holistically.

## Responsibilities

The Host owns:

- **Orchestration** — deciding the order platform services are built,
  registered, and started in, and calling each one's existing public
  contract to do so. See *Host Lifecycle.md* and *Startup Sequence.md*.
- **Startup** — driving the platform from nothing to `Running` (see *Runtime
  State Machine.md*), including the ordering clarification in ADR-0011
  (Discovery and Registration precede DI container construction).
- **Shutdown** — driving a running platform back down to `Stopped`/`Disposed`,
  in the reverse of startup order, whether triggered by an explicit request or
  by a fault. See *Shutdown Sequence.md*.
- **Cancellation** — accepting and propagating the two distinct signals
  described in ADR-0014 (a startup-time cancellation token, and a separate
  running-time shutdown request).
- **Disposal ordering** — ensuring that whatever was brought up during a
  (possibly partial) startup is torn down in a safe, deterministic order,
  following the same permissive-disposal philosophy ADR-0004 established for
  individual modules, now applied to the Host's own platform services (see
  ADR-0004's WP 2.7 update).

## Explicit Non-Responsibilities

The Host does **not** own:

- **Business logic.** The Host has no domain knowledge of what any module
  does; it only knows that modules exist and have a lifecycle to drive.
- **Configuration parsing.** `ConfigurationBuilder`/`ConfigurationProvider`
  already own this entirely; the Host calls `AddSource`/`Build` and holds the
  result, nothing more.
- **Module implementation.** The Host never authors, modifies, or reasons
  about what a specific `IModule`/`IModuleLifecycle` implementation does — see
  *The Module Pipeline* (Runtime Architecture) for why this boundary already
  exists and is preserved here.
- **Logging implementation.** The Host calls `AddLogging` and depends on
  `ILogger` like everything else (ADR-0010); it does not know about
  `ConsoleLogSink`, or any future sink, at all.

## Relationship to Existing Services

The Host is a new, thin layer sitting *above* the six existing services,
calling each through its already-established public contract — it introduces
no new capability to any of them (with the one narrow exception ADR-0011
clarifies: the *order* in which the Host calls them, not any change to what
they do). Concretely, the Host:

- Calls `ConfigurationBuilder.AddSource`/`Build()` directly.
- Constructs `ConsoleLogSink`/`LoggerFactory`/a default `ILogger` directly
  (exactly as `LoggingServiceCollectionExtensions.AddLogging` already does),
  or calls `AddLogging` itself.
- Constructs `ReflectionFrameworkDiscoveryService` directly and calls
  `DiscoverModules()`.
- Constructs `RuntimeModuleManager` directly and calls `Register` for each
  discovered descriptor.
- Populates a `ServiceCollection` (configuration, logging, discovered module
  types via `AddDiscoveredModules`) and builds a `TempestServiceProvider` from
  it.
- Constructs `ModuleLifecycleManager` directly and drives
  `InitialiseAllAsync`/`StartAllAsync`/`StopAllAsync`/`DisposeAllAsync`.

None of these six services need to change to support the Host. This is a
direct, deliberate test of whether WP 2.1–2.6's separation-of-concerns
discipline actually held — see the WP 2.7 Academy review for the verdict.

**Discovery, Registration, and Lifecycle are Host-owned collaborators, never
DI-public — decided by ADR-0017.** `IFrameworkDiscoveryService`,
`IRuntimeModuleManager`, and `IModuleLifecycleManager` are constructed
directly by `TempestHost`/`TempestHostBuilder` and are never registered into
the `ServiceCollection`. A module resolving them via constructor injection
would be able to reach back into the machinery orchestrating it — registering
new modules mid-startup, stopping other modules, retriggering discovery —
which would break the deterministic startup model this document and its
companions exist to establish. See the Ownership Matrix for the full,
object-by-object breakdown this decision applies to.

## Naming and Placement — Decided

**Resolved by ADR-0016.** The Runtime Host lives in a new namespace,
`Tempest.Core.Runtime`, distinct from the pre-existing `Tempest.Core.Hosting`
(the platform's original, pre-module-pipeline `HostingService`, which creates
a handful of workspace directories on disk and has nothing to do with the
module pipeline). The governing rule: **Runtime = platform. Hosting =
environment.** `Tempest.Core.Hosting`'s scope is reframed, not removed:
environment and deployment adapters (console application, Windows Service,
Linux daemon, container, embedded process) that embed and drive a
`TempestHost` — never the platform's own orchestration, which belongs
entirely to `Tempest.Core.Runtime`.

Names (not yet implemented — no interface members are specified here, per
this work package's own constraint): **`TempestHost`** (the running
instance), **`TempestHostBuilder`** (assembles configuration sources and any
other pre-registration inputs, then produces a `TempestHost`),
**`ITempestHost`**, **`ITempestHostBuilder`** (the corresponding
abstractions).

## Threading

**Single-threaded orchestration.** The Host's own control flow — the sequence
of steps described in *Startup Sequence.md* and *Shutdown Sequence.md* — is
sequential, not parallel. Phases happen one after another; the Host does not,
for example, build Configuration and Logging concurrently, even though
nothing would obviously prevent it. This follows the same Deterministic
Systems principle already applied throughout the platform (see the Engineering
Principles document of that name): a fixed, sequential order is easier to
reason about, test, and diagnose than a concurrent one, and nothing about the
Host's own startup cost currently justifies the added complexity of
parallelising it.

**Concurrency within a phase is inherited, not introduced.** Where an existing
service already processes multiple items within one phase (`ModuleLifecycleManager`
processing each module in `InitialiseAllAsync`, for example), it already does
so sequentially, in deterministic order (WP 2.3) — the Host does not change
this, and does not itself introduce any new parallelism across modules within
a phase. Existing services' own internal thread safety (`RuntimeModuleManager`'s
lock, `TempestServiceProvider`'s lock, `Logger`'s lock-free immutable design)
is unaffected by, and unrelated to, the Host's own sequential orchestration.

**Cancellation model.** See ADR-0014: a startup-time `CancellationToken` and a
distinct, running-time shutdown-request signal, deliberately not unified into
one.

**Future extensibility.** Nothing about the Host's sequential design precludes
a future, deliberately concurrent extension (parallel module initialisation,
for example) — but that would be a new, explicit decision for whichever future
work package proposes it, revisiting this section rather than silently
diverging from it.

## Future Extensibility

The Host is designed as the seam future capabilities plug into, without
requiring their own, separate entry point:

- **Hosted services** (background work that starts alongside, and stops
  symmetrically with, the module pipeline) would slot in between Module
  Initialisation and Runtime Running at startup, and at the front of Shutdown
  — started after modules are initialised, stopped before modules are.
- **Requirements Engine** and **Project Engine** (Platform Service Map,
  planned) would each need to be classified, per ADR-0013, as either a
  platform service (Host-fatal on failure) or a set of modules (isolated
  failure, via the existing pipeline) before being added — this is an open
  design question for whichever work package designs them, not resolved here.
- **Background workers** and **scheduling** are most likely expressed as
  hosted services (above) or as modules with their own internal timers/loops
  — either fits the existing pipeline without requiring a new Host concept.
- **Plugins** — loading assemblies from disk (the still-empty `src/Plugins/`
  directory, a gap noted since WP 2.1) would need to happen *before* Module
  Discovery in the Host's sequence, so that Discovery's
  `AppDomain.CurrentDomain.GetAssemblies()` default actually sees them. This
  is the Host's first legitimate opportunity to close that long-standing gap,
  though doing so is out of WP 2.7's own scope.

## Related Documents

*Host Lifecycle.md* (phase-by-phase detail) · *Startup Sequence.md* ·
*Shutdown Sequence.md* · *Runtime State Machine.md* · *Failure Behaviour.md* ·
*Ownership Matrix.md* · *Platform Service Map.md* · *The Module Pipeline* and
*The Startup Sequence* (Academy, Runtime Architecture) · ADR-0004, ADR-0008,
ADR-0009, ADR-0011 through ADR-0017.
