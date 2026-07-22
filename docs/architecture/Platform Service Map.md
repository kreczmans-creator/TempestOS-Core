# TempestOS Platform Service Map

## Purpose

This is a living index of every platform service TempestOS is built from — what
each one is responsible for, what it depends on, what depends on it, how it
comes to exist during startup, and where to go for the full reasoning behind
it. It exists so a reader can answer "what is X, what does it need, and what
needs it" in one place, without reconstructing the picture from six work
package retrospectives and ten ADRs each time.

**This document must be updated whenever a service is added, removed, or has
its responsibility, dependencies, or consumers change** — it is Academy
material, subject to the same maintenance obligation as everything else under
`docs/academy/` (see Engineering Governance, §6). A service map that drifts out
of date is worse than no map at all, because it will be trusted.

## At a Glance

| Service | Status | Depends on | Depended on by |
|---|---|---|---|
| Configuration | Implemented (WP 2.5) | — | Logging, any future config consumer |
| Logging | Implemented (WP 2.6) | Configuration | Discovery, Registration, Lifecycle, DI, Configuration |
| Dependency Injection | Implemented (WP 2.4) | — | Lifecycle, any registered service |
| Discovery | Implemented (WP 2.1) | Logging | Registration |
| Registration | Implemented (WP 2.2) | Discovery, Logging | Lifecycle |
| Lifecycle | Implemented (WP 2.3) | Registration, Dependency Injection, Logging | Host |
| Host | Architected (WP 2.7), not implemented | Configuration, Logging, Discovery, Registration, Lifecycle, Dependency Injection | Tempest.App |
| Project Engine | Planned | Undetermined | Undetermined |
| Requirements Engine | Planned | Undetermined | Undetermined |

Arrows in this table point from a service to what it *needs*; read the third
column as "the following depend on this row." "Depends on" and "Depended on
by" are deliberately kept as separate columns rather than merged into one
diagram, because — as *The Module Pipeline* explains — each of these
dependencies is on an *interface*, never a concrete implementation.

---

## Configuration

**Responsibility.** Provides read-only, immutable, case-insensitive key/value
configuration data to the rest of the runtime. Configuration is data, never
business logic, and is loaded exactly once per running instance.

**Key types.** `IConfigurationProvider`, `ConfigurationProvider`,
`IConfigurationSource`, `MemoryConfigurationSource`, `ConfigurationBuilder`,
`ConfigurationException` and subtypes.

**Dependencies.** None. Configuration is the first service to exist during
startup — nothing else needs to be built before it.

**Consumers.** `LoggerFactory` (reads `Runtime:Logging:MinimumLevel`); any
future runtime service depending on `IConfigurationProvider` via constructor
injection, once the service provider is built.

**Lifecycle.** Built once, directly, via `ConfigurationBuilder.AddSource(...)`
+ `Build()`, *before* the DI container exists. Registered into the container
via `AddInstance<IConfigurationProvider>` (see ADR-0009). Never rebuilt or
mutated for the life of the running instance — see the first two steps of
*The Startup Sequence*.

**ADR references.** ADR-0009 (*Composition Root Owns Externally-Created
Services* — governs how configuration reaches the container).

**Academy references.** WP 2.5 retrospective (*Configuration Framework*);
Case Study 05 (*Why Isn't Configuration Mutable?*); *The Startup Sequence*
(Runtime Architecture); Engineering Principles — Immutability, Fail Fast.

---

## Logging

**Responsibility.** Provides the `ILogger` abstraction every runtime component
depends on for structured, filtered, append-only diagnostic output. No
consumer of `ILogger` knows or can know where a message ultimately goes.

**Key types.** `ILogger`, `ILoggerFactory`, `ILogSink`, `ConsoleLogSink`,
`Logger`, `LoggerFactory`, `LogEntry`, `LogLevel`,
`LoggingServiceCollectionExtensions`.

**Dependencies.** `IConfigurationProvider` (read once, at `LoggerFactory`
construction, for the minimum log level).

**Consumers.** Discovery, Registration, Lifecycle, Dependency Injection, and
Configuration itself all depend on `ILogger` (optionally) for their own
diagnostic output — see ADR-0010. Any future runtime service should do the
same.

**Lifecycle.** `ConsoleLogSink`, `LoggerFactory`, and a default `ILogger` are
built directly at the composition root (`AddLogging`) — not resolved via the
container's reflection-based construction, since producing the default logger
requires *calling* `CreateLogger` — and registered via `AddInstance`. This
happens immediately after configuration is registered, and before the service
provider is built — see *The Startup Sequence*.

**ADR references.** ADR-0009 (its principle applied a second time); ADR-0010
(*The Module Pipeline Depends on the Logging Abstraction, Not a Concrete
Logger*).

**Academy references.** WP 2.6 retrospective (*Logging & Diagnostics
Framework*); *The Startup Sequence* (Runtime Architecture, updated for WP 2.6).

---

## Dependency Injection

**Responsibility.** Constructs and resolves service instances via constructor
injection, with singleton and transient lifetimes. Owns *how* things are
built; never owns *what* they do.

**Key types.** `IServiceCollection`, `ServiceCollection`,
`ITempestServiceProvider`, `TempestServiceProvider`, `ServiceDescriptor`,
`ServiceLifetime`, `ServiceResolutionException` and subtypes.

**Dependencies.** None intrinsically — the container itself has no
dependencies. Specific registrations (Configuration, Logging, discovered
modules) depend on their own upstream services being ready before they can be
registered.

**Consumers.** `ModuleLifecycleManager` (resolves module instances through
it); any service registered into it with constructor dependencies of its own.

**Lifecycle.** `ServiceCollection` accumulates registrations throughout the
early part of startup (configuration, logging, discovered modules, and so on);
`TempestServiceProvider` is built once, after every registration the running
instance needs has been added — the last step before "runtime starts" in *The
Startup Sequence*.

**ADR references.** ADR-0005 (*Custom Dependency Injection Container*);
ADR-0006 (*Constructor Injection Only*); ADR-0007 (*Service Provider Owns
Construction*); ADR-0008 (*Discovery Does Not Depend on DI*); ADR-0009
(*Composition Root Owns Externally-Created Services*).

**Academy references.** WP 2.4 retrospective (*Dependency Injection*); Design
Pattern 03 (*Minimal Interface, Extension-Method Sugar*); Engineering
Principles — Dependency Injection, Composition Over Inheritance, SOLID
(Dependency Inversion).

---

## Discovery

**Responsibility.** Finds `IModule` implementations across loaded assemblies
via reflection, validates their metadata, and returns them in deterministic,
alphabetical order. Answers exactly one question: what modules exist.

**Key types.** `IModule`, `ModuleDescriptor`, `IFrameworkDiscoveryService`,
`ReflectionFrameworkDiscoveryService`, `ModuleDiscoveryException`,
`DuplicateModuleIdException`.

**Dependencies.** `ILogger` (optional, for diagnostics). Deliberately **not**
dependent on the DI container (see ADR-0008) or on Configuration.

**Consumers.** `RuntimeModuleManager` (registers whatever Discovery finds); the
future Host, which will need to invoke it during startup.

**Lifecycle.** Runs once (or whenever explicitly invoked); does not persist
any module instance — every candidate is instantiated transiently, purely to
read metadata, then discarded. This is why module constructors must be
side-effect-free (ADR-0003).

**ADR references.** ADR-0003 (*Constructors Are Side-Effect-Free*); ADR-0008
(*Discovery Does Not Depend on DI*).

**Academy references.** WP 2.1 retrospective (*Module Discovery*); Case Study
04 (*Why Discovery Is Isolated*); Engineering Principles — Deterministic
Systems, Fail Fast, SOLID (Interface Segregation, Open/Closed).

---

## Registration

**Responsibility.** The single authoritative runtime catalogue of registered
modules. Rejects duplicates, preserves registration order, provides lookup.
Owns runtime metadata only — never instantiates, orchestrates, or injects.

**Key types.** `RuntimeModule`, `ModuleState` (shared with Lifecycle),
`IRuntimeModuleManager`, `RuntimeModuleManager`, `ModuleRegistrationException`
and subtypes.

**Dependencies.** `ModuleDescriptor` values (from Discovery, or constructed
directly); `ILogger` (optional, for diagnostics).

**Consumers.** `ModuleLifecycleManager` sources its entire ordered snapshot of
modules from here at construction.

**Lifecycle.** Populated once per running instance, typically immediately
after Discovery runs. Every `RuntimeModule` it produces is immutable from the
moment it's created (ADR-0001) — registration order is preserved, but nothing
about an already-registered module can change afterward except via the
separate Lifecycle service's own state tracking (ADR-0002).

**ADR references.** ADR-0001 (*RuntimeModule Is Immutable*); ADR-0002
(*Lifecycle State Is Managed Externally, Not on the Module*).

**Academy references.** WP 2.2 retrospective (*Runtime Registration*); Case
Study 01 (*Why RuntimeModule Is Immutable*); Design Pattern 01 (*The Registry
Pattern*); Design Pattern 02 (*Descriptor and Snapshot Types*); Engineering
Principles — Immutability, Single Responsibility.

---

## Lifecycle

**Responsibility.** Orchestrates initialisation, startup, shutdown, and
disposal for every registered module, in deterministic order, with per-module
failure isolation — "the single orchestration point for module execution."

**Key types.** `IModuleLifecycle`, `ModuleLifecycleStatus`,
`IModuleLifecycleManager`, `ModuleLifecycleManager`, `ModuleLifecycleException`,
`InvalidModuleLifecycleTransitionException`.

**Dependencies.** `IRuntimeModuleManager` (the modules to orchestrate);
`ITempestServiceProvider` (constructs module instances — see ADR-0007);
`ILogger` (optional, for diagnostics).

**Consumers.** The future Host, which will drive `InitialiseAllAsync` /
`StartAllAsync` / `StopAllAsync` / `DisposeAllAsync` as part of the startup and
shutdown sequence.

**Lifecycle.** Constructed once, after the service provider is built (it
depends on both `IRuntimeModuleManager` and `ITempestServiceProvider`, so it is
necessarily the last of the six implemented services to come into existence
during startup). Drives every module through `Registered → Initialising →
Initialised → Starting → Running → Stopping → Stopped → Disposed`, with
`Failed` reachable from any non-terminal state.

**ADR references.** ADR-0002 (*Lifecycle State Is Managed Externally*);
ADR-0003 (*Constructors Are Side-Effect-Free* — underpins ADR-0004); ADR-0004
(*Dispose Permitted From Every Non-Terminal State*); ADR-0007 (*Service
Provider Owns Construction*).

**Academy references.** WP 2.3 retrospective (*Runtime Lifecycle*); Case
Study 02 (*Why Lifecycle State Lives Externally*); Case Study 03 (*Why Dispose
Is Always Legal*); Engineering Principle — State Machines.

---

## Host *(architected, not yet implemented — WP 2.7)*

**Responsibility.** The composition root: assembles Configuration, Logging,
Discovery, Registration, Dependency Injection, and Lifecycle into one running
instance, and owns orchestration, startup, shutdown, cancellation, and
disposal ordering. Does **not** own business logic, configuration parsing,
module implementation, or logging implementation. Fully designed —
responsibilities, a 13-phase lifecycle, complete startup/shutdown sequence
diagrams, its own 7-state machine, and a full failure model — but not yet
implemented; see *Runtime Host Architecture.md* and its companion documents.

**Status.** Architecture complete (WP 2.7); implementation not started.
Previously flagged as a gap across the WP 2.4, WP 2.5, and WP 2.6
retrospectives, and in *The Startup Sequence*'s own Trade-offs section — this
entry updates that gap from "not designed" to "designed, awaiting
implementation."

**Dependencies.** Every implemented service above — Configuration and Logging
first (constructed directly, outside the container), then Discovery and
Registration (deliberately *before* the DI container is built — see
ADR-0011), then Dependency Injection, then Lifecycle.

**Consumers (anticipated).** `Tempest.App` / the process entry point; future
hosted services, background workers, and — pending their own classification
under ADR-0013 — a Requirements Engine and/or Project Engine.

**ADR references.** ADR-0004 (disposal reused at Host level), ADR-0008
(why Discovery/Registration precede DI — see ADR-0011), ADR-0009 (composition
root pattern), ADR-0011 (*Discovery and Registration Precede DI Container
Construction*), ADR-0012 (*The Runtime Host Owns an Independent State
Machine*), ADR-0013 (*Platform-Service Failures Abort Startup; Module
Failures Remain Isolated*), ADR-0014 (*Cancellation and Shutdown-Request Are
Distinct Signals*), ADR-0015 (*Runtime Hosts Are Not Restartable*), ADR-0016
(*The Host Lives in Tempest.Core.Runtime, Distinct From Tempest.Core.Hosting*),
ADR-0017 (*Discovery, Registration, and Lifecycle Remain Host-Owned
Collaborators, Not Public DI Services*), ADR-0018 (*Startup Cancellation
Transitions to Controlled Shutdown*).

**Academy references.** WP 2.7 retrospective (*Runtime Host Architecture
Review*, including its Open Questions, Risks, and Architectural Debt
Assessment); *The Startup Sequence* (Runtime Architecture); *Runtime Host
Architecture.md*, *Host Lifecycle.md*, *Startup Sequence.md*, *Shutdown
Sequence.md*, *Runtime State Machine.md*, *Failure Behaviour.md*, *Ownership
Matrix.md* (all `docs/architecture/`).

---

## Project Engine *(planned)*

**Responsibility (anticipated).** Not yet designed as a platform service.
Likely successor to, or integration point for, the existing pre-module-
pipeline project management code (`Tempest.Core.Projects`,
`ProjectService`, `ProjectModel`, `JsonProjectRepository`) — bootstrap-era
functionality that predates and is currently independent of the module
pipeline entirely.

**Status.** Not implemented as a platform service. The bootstrap-era code it
would likely relate to already exists but has not been touched, migrated, or
integrated by any module-pipeline work package to date.

**Dependencies / Consumers.** Undetermined.

**ADR references.** None yet.

**Academy references.** None yet.

---

## Requirements Engine *(planned)*

**Responsibility (anticipated).** Not yet designed. No code exists.

**Status.** Purely aspirational — named as a future platform service, with no
implementation, no design discussion, and no code to reference.

**Dependencies / Consumers.** Undetermined.

**ADR references.** None yet.

**Academy references.** None yet.
