# Engineering Glossary

## Purpose

A single, alphabetical reference for the vocabulary TempestOS's architecture
documents and ADRs rely on — so a term used precisely in one document isn't
read loosely in another. Where a term was defined or clarified by a specific
document or ADR, this glossary cites it rather than restating the reasoning;
this is a place to look a term up, not a substitute for the document that
established it.

**This document must be kept current** as new architectural vocabulary is
introduced — it is Academy-adjacent material (see Engineering Governance,
§6) and, like the Platform Service Map, is worse than useless if it drifts
out of date and is trusted anyway. A work package that introduces a new term
of art (not just a new class name) should add or update an entry here as
part of its own documentation obligations.

## How to Read an Entry

Each entry gives the term, a precise definition, and — where the term was
established or clarified by a specific document — a citation. Terms that are
easily confused with one another cross-reference each other explicitly rather
than leaving the distinction to be inferred.

---

### Atomic Operation

The actual indivisible unit of work the Atomic Phase Principle (Engineering
Principle 11) governs: it either completes in full or fails in full, with no
observable intermediate state. Not to be confused with **Lifecycle Phase**,
which is a scheduling label that may itself be composed of many atomic
operations. See ADR-0018's Terminology section for the full distinction, and
its worked example: one module's own `InitialiseAsync` call is the atomic
operation; "Module Initialisation" is the (batch) phase built from many of
them.

### Atomic Phase Principle

The Engineering Principle (11) stating that runtime lifecycle phases shall be
atomic — a phase either completes successfully or fails, and external
cancellation is observed only between phases, never in the middle of one. See
`docs/academy/01 Engineering Principles/11-atomic-phase-principle.md` for the
full principle, and ADR-0018 for how it applies once **Lifecycle Phase** and
**Atomic Operation** are correctly distinguished.

### Composition Root

Whatever code assembles a running TempestOS instance — today exercised
directly by test setup; once implemented, the **Runtime Host**. The
composition root is recognised (ADR-0009) as owning a category of service the
DI container will never construct itself, because construction depends on
information (which configuration sources, in what order) that is a property
of startup, not derivable from a type's own constructor parameters.

### Consumer

Any component that holds a reference to an object and may call methods on it,
but does not decide the object's lifetime and must not attempt to destroy it
— contrasted with **Owner**. See *Ownership Matrix.md*, "What 'Owner' Means."

### Controlled Shutdown

The single shared teardown procedure — Module Disposal, then Service
Disposal — used whenever the Host transitions to `Stopping`, regardless of
whether `Stopping` was entered from `Running` (a graceful, post-startup
shutdown request) or from `Starting` (startup cancellation, or an early
shutdown request — ADR-0018). Not to be confused with **Post-Fault
Teardown**, a separate procedure for genuine startup failures, which never
passes through `Stopping` at all. See *Shutdown Sequence.md*.

### Dependency Injection

The platform service constructing and resolving service instances via
constructor injection, with singleton and transient lifetimes (`ADR-0005`
through `ADR-0009`). Owns *how* things are built; never owns *what* they do.
See the Platform Service Map's "Dependency Injection" entry.

### Deterministic Systems

The Engineering Principle (07) that a fixed, sequential, reproducible order
of operations is easier to reason about, test, and diagnose than a concurrent
or non-deterministic one. Cited throughout the Runtime Host architecture to
justify single-threaded orchestration (*Runtime Host Architecture.md*,
"Threading") and fixed startup/shutdown ordering.

### Discovery

The platform service that finds `IModule` implementations across loaded
assemblies via reflection, validates their metadata, and returns them in
deterministic order — answering exactly one question: what modules exist.
Deliberately independent of the DI container (ADR-0008) and of Configuration.
See the Platform Service Map's "Discovery" entry.

### Fail Fast

The Engineering Principle (06) that a system should surface an invalid state
immediately and loudly, rather than continuing in a state that is silently
wrong. Underpins why Configuration, Discovery, and Registration failures are
all **Host-Fatal** rather than tolerated or worked around.

### Faulted

One of the Runtime Host's seven states: reached when a platform-service
failure aborts startup (`Starting → Faulted`), or when a genuine Host-level
defect occurs during `Running` or `Stopping`. Not reachable from a module
failure alone — see **Host-Fatal** and **Isolated Failure**. See *Runtime
State Machine.md*.

### Graceful Shutdown

A **Controlled Shutdown** entered from `Running` — i.e., triggered by a
**Shutdown Request** arriving after the platform reached its steady state,
as opposed to a shutdown triggered by cancellation during `Starting`. Both
follow the identical procedure once `Stopping` begins (ADR-0018); "graceful"
here describes only which state `Stopping` was entered from, not a different
procedure.

### Hosted Service *(planned)*

Background work that would start alongside, and stop symmetrically with, the
module pipeline — slotting in between Module Initialisation and Runtime
Running at startup, and at the front of Shutdown. Not yet designed; named in
*Runtime Host Architecture.md*'s Future Extensibility section as a seam the
Host is designed to accept without requiring its own entry point.

### Host

Short form of **Runtime Host**, used interchangeably throughout the
architecture documents once the term has been introduced in a given
document.

### Host State

The state of the Runtime Host itself (its own state machine, ADR-0012),
independent of any individual **Module State**. Seven values:

| State | Meaning |
|---|---|
| `Created` | The Host object exists; nothing has been built. |
| `Starting` | Any lifecycle phase from Configuration Built through Module Initialisation is in progress. |
| `Running` | Module Initialisation completed; the platform is up. Does not imply every module succeeded. |
| `Stopping` | Controlled shutdown in progress: Module Disposal, then Service Disposal. Entered from `Running` or from `Starting`, procedure identical either way. |
| `Stopped` | Controlled shutdown completed. |
| `Faulted` | A platform-service failure aborted startup, or a genuine Host-level defect occurred during `Running` or `Stopping`. |
| `Disposed` | Terminal. Every resource that could be released has had release attempted. |

See *Runtime State Machine.md* for the full diagram, transition table, and
illegal-transitions list.

### Host-Fatal

Describes a failure that aborts the Runtime Host's startup or running state
outright, transitioning it to `Faulted` — as opposed to an **Isolated
Failure**, which is contained at the module level and does not affect the
Host's own state. Governed by ADR-0013: platform-service failures are
Host-fatal; module failures are isolated. See *Failure Behaviour.md*'s
"Required Behaviour Summary" for the full table of which failure is which.

### Isolated Failure

A failure — typically an individual module's exception during
Initialise/Start/Stop/Dispose — that is caught, logged, and marked against
that module alone (`ModuleState.Failed`), without aborting the batch it
occurred in or affecting the Runtime Host's own state. Established by WP 2.3
(`ModuleLifecycleManager`'s per-module isolation) and elevated to an explicit
platform-wide policy by ADR-0013. Contrasted with **Host-Fatal**.

### Lifecycle

The platform service orchestrating initialisation, startup, shutdown, and
disposal for every registered module, in deterministic order, with
per-module failure isolation (WP 2.3). See the Platform Service Map's
"Lifecycle" entry. Not to be confused with **Lifecycle Phase**, the Host's
own, separate sequencing concept.

### Lifecycle Phase

A named, ordered step in a sequence — Configuration Built, Module Discovery,
Module Initialisation, and the rest of *Host Lifecycle.md*'s phase table. A
phase answers "where are we in the sequence"; it exists for scheduling and
observability, and its boundaries are drawn by whoever designs the sequence.
A phase may itself be a single, indivisible call, or a *batch* of many
smaller **Atomic Operations** — the two are not synonyms. See ADR-0018's
Terminology section and Engineering Principle 11 for the full distinction.

### Logging

The platform service providing the `ILogger` abstraction every runtime
component depends on for structured, filtered, append-only diagnostic
output — no consumer of `ILogger` knows or can know where a message
ultimately goes (ADR-0010). See the Platform Service Map's "Logging" entry.

### Module

A unit of platform functionality implementing `IModule` (and, once
initialised, `IModuleLifecycle`), discovered by **Discovery**, tracked by
**Registration**, and driven through its lifecycle by **Lifecycle**. A
module has no domain knowledge of the platform's orchestration and no path
back into Discovery, Registration, or Lifecycle (ADR-0017) — it is a
**Consumer** of the platform, never a driver of it.

### Module Pipeline

The six already-implemented platform services taken together as a sequence —
Configuration, Logging, Discovery, Registration, Dependency Injection,
Lifecycle — that a module passes through from being discovered to being
disposed. Named and described in `docs/academy/02 Runtime Architecture/
01-the-module-pipeline.md`; the Runtime Host is a new, thin layer that calls
this pipeline, not a seventh member of it.

### Module State

The state of an individual module (`ModuleState`), tracked externally by
`ModuleLifecycleManager` (ADR-0002), independent of the Runtime Host's own
state (ADR-0012). Ten values: `Discovered`, `Registered`, `Initialising`,
`Initialised`, `Starting`, `Running`, `Stopping`, `Stopped`, `Disabled`,
`Disposed`, with `Failed` reachable from most non-terminal states. Not to be
confused with **Host State** — a Host can be `Running` while individual
modules sit in `Failed`, by design.

### Owner

The component that constructs an object (or is the only thing that ever
calls whatever constructs it), holds the authoritative reference to it for
as long as it exists, and decides when it is destroyed. Contrasted with
**Consumer**. See *Ownership Matrix.md*, "What 'Owner' Means," for the full
definition and the object-by-object table it governs.

### Partial Shutdown

Some modules or services fail to stop/dispose cleanly during `Stopping`.
Already fully handled by `ModuleLifecycleManager`'s existing per-module
isolation — no new Host-level policy exists or is needed. See *Failure
Behaviour.md*, "Partial Shutdown."

### Partial Startup

Any point where some, but not all, of Configuration Built through Module
Initialisation completed before a Host-fatal *failure* occurred — distinct
from a startup interrupted by cancellation or an early shutdown request
(which is not a failure at all, and is handled by **Controlled Shutdown**,
not this case). See *Failure Behaviour.md*, "Partial Startup," and its
"Post-Fault Teardown" path.

### Permissive Disposal

The design rule, established by ADR-0004 for individual modules and reused
by the Runtime Host for its own `Faulted → Disposed` transition, that
`Dispose` is legal from every state except an already-`Disposed` terminal
state — including states where no real resources were ever acquired (a
module that was `Registered` but never `Initialised`, or a Host that never
built anything past Configuration). This is what makes unconditional
shutdown sweeps possible: a caller never needs to reason about how far
startup progressed before disposing everything.

### Platform Service

One of the components the Runtime Host assembles and orchestrates —
Configuration, Logging, Discovery, Registration, Dependency Injection,
Lifecycle, and (once classified under ADR-0013) any future service such as a
Requirements Engine or Project Engine. A platform service's failure is
**Host-Fatal**; contrasted with a **Module**, whose failure is an **Isolated
Failure**. See *Failure Behaviour.md*'s Governing Principle.

### Plugin *(planned)*

An assembly loaded from disk (the still-empty `src/Plugins/` directory) to
extend the platform with additional modules at runtime. Not yet implemented;
would need to load *before* Module Discovery in the Host's sequence so that
Discovery's default assembly scan actually sees the loaded plugin assemblies.
Named in *Runtime Host Architecture.md*'s Future Extensibility section.

### Post-Fault Teardown

The teardown procedure used when a genuine platform-service failure aborts
startup (ADR-0013), going directly to `Faulted` rather than through
`Stopping`. Disposes whichever modules exist, in whatever state they
reached, then attempts Service Disposal — structurally similar to
**Controlled Shutdown** but a distinct path, since a fault is never routed
through `Stopping`. See *Shutdown Sequence.md*, "Post-Fault Teardown."

### Registration

The platform service maintaining the single authoritative runtime catalogue
of registered modules — rejecting duplicates, preserving registration order,
providing lookup. Owns runtime metadata only; never instantiates,
orchestrates, or injects. See the Platform Service Map's "Registration"
entry.

### Restartability (Single-Use Host)

The decided (ADR-0015) property that a `TempestHost` instance is single-use:
`Created → Running → Stopped/Faulted → Disposed`, with no path back to
`Starting` or `Running`. A second run always means a new
`TempestHostBuilder` producing a new `TempestHost`, backed by entirely fresh
collaborators — nothing is reused across runs.

### Runtime Host

The single entry point to TempestOS: the component whose job is to bring
every platform service up, in the right order, hold the platform in a
running state, and bring everything back down again, cleanly, whenever asked
or whenever something goes wrong. Deliberately thin — it does not implement
any of the six platform services, only calls them in order and owns the
questions none of them individually answers. See *Runtime Host
Architecture.md*, "Overview." Implemented (once built) as `TempestHost` /
`TempestHostBuilder` in the `Tempest.Core.Runtime` namespace (ADR-0016).

### Shutdown Request

The running-time signal (ADR-0014), distinct from **Startup Cancellation**,
that initiates the Host's own graceful shutdown sequence once `Running`. An
early shutdown request arriving during `Starting` (before `Running` is
reached) is handled identically to startup cancellation, both routing
through `Stopping` (ADR-0018).

### Single Responsibility

The Engineering Principle (10) that a component should have exactly one
reason to change. Cited throughout the module pipeline's separation of
concerns — Discovery discovers, Registration registers, Lifecycle
orchestrates, the Service Provider creates (ADR-0007) — and by ADR-0017 as
the reasoning behind keeping Discovery, Registration, and Lifecycle out of
the DI container's public surface.

### Startup Cancellation

The startup-time `CancellationToken` (ADR-0014), distinct from a **Shutdown
Request**, observed only during `Starting`. Means: abort immediately, without
treating it as a fault, and attempt to tear down whatever was already built —
routed, since ADR-0018, through the same `Stopping` state a graceful shutdown
uses, not a separate direct path to `Stopped`.

### Tempest.Core.Hosting

The namespace for environment and deployment adapters — how a `TempestHost`
is embedded into a specific deployment target (console application, Windows
Service, Linux daemon, container, embedded process). Pre-existing (its
original `HostingService` predates the module pipeline and creates workspace
directories on disk); reframed, not replaced, by ADR-0016. Contrasted with
**Tempest.Core.Runtime**. Governing rule: "Runtime = platform. Hosting =
environment."

### Tempest.Core.Runtime

The namespace the Runtime Host itself lives in — `TempestHost`,
`TempestHostBuilder`, `ITempestHost`, `ITempestHostBuilder` — established by
ADR-0016, distinct from the pre-existing **Tempest.Core.Hosting**.

---

## Related Documents

*Runtime Host Architecture.md* · *Host Lifecycle.md* · *Runtime State
Machine.md* · *Failure Behaviour.md* · *Shutdown Sequence.md* · *Startup
Sequence.md* · *Ownership Matrix.md* · *Platform Service Map.md* · ADR-0001
through ADR-0018 · `docs/academy/01 Engineering Principles/` · `docs/academy/
02 Runtime Architecture/`.
