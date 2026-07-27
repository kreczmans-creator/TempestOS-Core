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
| Platform Version | Implemented (WP 4.2A) | — | Any current or future platform service (ADR-0023) |
| Configuration | Implemented (WP 2.5) | — | Logging, any future config consumer |
| Logging | Implemented (WP 2.6) | Configuration | Discovery, Registration, Lifecycle, DI, Configuration |
| Dependency Injection | Implemented (WP 2.4) | — | Lifecycle, any registered service |
| Discovery | Implemented (WP 2.1) | Logging | Registration |
| Registration | Implemented (WP 2.2) | Discovery, Logging | Lifecycle |
| Lifecycle | Implemented (WP 2.3) | Registration, Dependency Injection, Logging | Host |
| Module SDK | Implemented (WP 4.1) — not Host-orchestrated; a developer-facing convenience layer, not a platform service in its own right | `IModule`, `IModuleLifecycle` | Any module author |
| Host | Implemented (WP 2.7B) | Configuration, Logging, Discovery, Registration, Lifecycle, Dependency Injection | Tempest.App |
| Event Bus | **Implemented — WP 4.4D** (`IEventBus`/`EventBus`, `Tempest.Core.Events`) — dispatch/subscription/failure model per ADR-0028; **consumed — WP 4.4E** | Dependency Injection | Any module — first real consumer: `ClockModule`/`ClockLifecycleObserverModule` (`WP 4.4E`) |
| Background Services | **Implemented — WP 4.5** (`IHostedServiceDiscoveryService`/`HostedServiceDiscoveryService`, `IHostedServiceManager`/`HostedServiceManager`, `Tempest.Core.BackgroundServices`) — discovery, ownership, orchestration, and Host Lifecycle placement per ADR-0029/ADR-0030; failure model per ADR-0021 | Host, Dependency Injection | Any module declaring a hosted service |
| Command Framework | Contract implemented (WP 4.0: `ICommand`); dispatcher planned (WP 4.7) — orthogonal to Navigation, ADR-0022 | Dependency Injection | Any module |
| Plugin Manifest | **Implemented — WP 4.2** (`Tempest.Core.Plugins`) | Host (Phases 3.1/3.2, ADR-0026 — a pre-Discovery step) | Module Discovery (unchanged), any real plugin |
| Project Engine | Planned | Undetermined | Undetermined |
| Requirements Engine | Planned | Undetermined | Undetermined |

Arrows in this table point from a service to what it *needs*; read the third
column as "the following depend on this row." "Depends on" and "Depended on
by" are deliberately kept as separate columns rather than merged into one
diagram, because — as *The Module Pipeline* explains — each of these
dependencies is on an *interface*, never a concrete implementation.

---

## Platform Version

**Responsibility.** Provides the single, authoritative version of the
running platform, queryable from anywhere via ordinary constructor
injection. Resolves its value exactly once, from the executing assembly's
own build metadata — never a hand-typed, duplicated constant.

**Key types.** `IPlatformVersionProvider`, `PlatformVersionProvider`,
`PlatformVersion` (`Tempest.Core.Versioning`).

**Dependencies.** None — deliberately a leaf. No current or future platform
service may sit "below" it in ADR-0023's layering; its only optional input
is `ILogger?` (diagnostics only, defaulting to `null`), matching every
other platform service's own convention.

**Consumers.** Any current or future platform service or module, resolved
via `IPlatformVersionProvider`. First real consumer beyond its own tests:
**implemented, WP 4.2** — Plugin Discovery's `MinimumPlatformVersion`
compatibility check (ADR-0025, category 4).

**Lifecycle.** **Update, ADR-0026 (WP 4.2C/4.2).** Constructed by
`TempestHost` immediately after Logging Built — moved earlier than its
original WP 4.2A placement (Platform Services Registered) specifically so
Plugin Discovery (Phase 3.1), which now runs before Module Discovery, can
depend on it. Its DI *registration* (`AddInstance`, ADR-0009) stays at the
original Platform Services Registered phase — construction and
registration are separable concerns, and nothing needs to resolve it via
DI before Module Initialisation regardless. No new `Host Lifecycle.md`
*phase* was needed for this move — only the existing "Platform Services
Registered" phase's own construction step relocated earlier in the method
body.

**ADR references.** ADR-0009 (Composition Root pattern, reused a third
time); ADR-0023 (this service is a direct instance of "dependencies flow
downward only" — everything may depend on it, it depends on nothing).

**Academy references.** WP 4.2A retrospective (*Runtime Platform Version
Infrastructure*); *Platform Version.md*.

---

## Configuration

**Responsibility.** Provides read-only, immutable, case-insensitive key/value
configuration data to the rest of the runtime. Configuration is data, never
business logic, and is loaded exactly once per running instance.

**Key types.** `IConfigurationProvider`, `ConfigurationProvider`,
`IConfigurationSource`, `MemoryConfigurationSource`, `ConfigurationBuilder`,
`ConfigurationException` and subtypes.

**Dependencies.** None, functionally. `ConfigurationBuilder` accepts an
optional `ILogger?` constructor parameter, matching the same optional-
diagnostics convention every other platform service follows — but unlike
Discovery, Registration, Lifecycle, and Platform Version (each of which
*is* constructed by `TempestHost` with an already-built, real logger),
Configuration is the first service to exist during startup, before Logging
Built, so the Host's own real call site never actually has a logger to
pass — the parameter exists on the type for any other caller (tests, a
future standalone use) that does.

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
consumer of `ILogger` knows or can know where a message ultimately goes. A
sink failure is isolated inside `Logger` itself (fixed WP 2.7B) and never
propagates to the caller that was logging something.

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
Framework*); WP 2.7B retrospective (the sink-isolation fix); *The Startup
Sequence* (Runtime Architecture, updated for WP 2.6).

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
`DuplicateModuleIdException`. `ModuleBase` (Module SDK, WP 4.1) is a
convenience base implementation of `IModule` — see the Module SDK entry,
below. `ModuleMetadataAttribute` *(implemented — WP 4.4B, ADR-0027)* — an
optional, class-level alternative to instance-property metadata, letting
Discovery read a module's `Id`/`Name`/`Version` without instantiating it;
see `Module Dependency Injection Architecture.md`.

**Dependencies.** `ILogger` (optional, for diagnostics). Deliberately **not**
dependent on the DI container (see ADR-0008) or on Configuration.

**Consumers.** `RuntimeModuleManager` (registers whatever Discovery finds);
`TempestHost`, which invokes it during Module Discovery (Phase 4).

**Lifecycle.** Runs once (or whenever explicitly invoked); does not persist
any module instance for a module discovered the existing way — every such
candidate is instantiated transiently, purely to read metadata, then
discarded. This is why module constructors must be side-effect-free
(ADR-0003). **Update, WP 4.4B (ADR-0027, implemented):** a module carrying
`ModuleMetadataAttribute` is not instantiated by Discovery at all — its
metadata is read from the attribute directly, leaving constructor
injection reachable for such a module's own, later, real construction
(`TempestServiceProvider`, unchanged). Every module without the attribute
keeps today's exact behaviour — verified directly: every pre-existing
Discovery test passes completely unmodified.

**ADR references.** ADR-0003 (*Constructors Are Side-Effect-Free*); ADR-0008
(*Discovery Does Not Depend on DI*); ADR-0027 (*A Declarative
`ModuleMetadataAttribute` Decouples Discovery From Construction* —
implemented).

**Academy references.** WP 2.1 retrospective (*Module Discovery*); Case Study
04 (*Why Discovery Is Isolated*); Engineering Principles — Deterministic
Systems, Fail Fast, SOLID (Interface Segregation, Open/Closed); WP 4.4A
retrospective (*Dependency Injection for Discovered Modules — architecture*);
WP 4.4B retrospective (*ADR-0027 Implementation*).

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
`InvalidModuleLifecycleTransitionException`. `ModuleLifecycleBase` (Module
SDK, WP 4.1) is a convenience base implementation of `IModuleLifecycle` —
see the Module SDK entry, below.

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

## Module SDK *(implemented — v0.4.0, WP 4.1)*

**Responsibility.** Reduces the repetitive boilerplate of writing a module —
not a new platform service the Host orchestrates, but a developer-facing
convenience layer over Discovery's and Lifecycle's existing contracts
(`IModule`, `IModuleLifecycle`). Introduces no new runtime behaviour: a
module built on the SDK is discovered, registered, and driven exactly like
a hand-written `IModule`/`IModuleLifecycle` implementation, with no
special-casing anywhere in the pipeline.

**Key types.** `ModuleBase` (identity only — `Id`/`Name`/`Version` via
constructor, for modules with no lifecycle), `ModuleLifecycleBase` (extends
`ModuleBase` with four `virtual` lifecycle methods, each defaulting to a
no-op, so a module overrides only the phase(s) it needs). Both in
`Tempest.Core.Modules` — no new namespace or project; see the WP 4.1
retrospective's Alternatives Considered for why.

**Dependencies.** None beyond what `IModule`/`IModuleLifecycle` already
require.

**Consumers.** Any module author, from `WP 4.1` onward. The Sample Module
(`WP 4.3`) is the SDK's first real, non-test consumer.

**A known, pre-existing constraint the SDK does not change.** Because
Discovery's metadata probe and `TempestServiceProvider`'s real construction
both operate on the same concrete module type, and Discovery requires a
public *parameterless* constructor while `TempestServiceProvider` requires
*exactly one* public constructor, a normally-discovered module cannot
currently receive constructor-injected dependencies — the two requirements
only both hold when that one constructor takes zero arguments. This was
identified during `WP 4.1`'s design review, not introduced by it; the SDK
works within this constraint (a concrete module still needs its own public
parameterless constructor) rather than attempting to lift it, which would
require changing Discovery — explicitly out of this work package's scope.

**ADR references.** None new — the SDK is a direct application of ADR-0003
(constructor side-effect-freedom) and the existing `IModule`/
`IModuleLifecycle` split; no new architectural decision was required.

**Academy references.** WP 4.1 retrospective (*Module SDK*); *Building a
Module* (Academy, new); WP 4.0 retrospective (*Platform Contracts*).

---

## Host *(implemented — WP 2.7B)*

**Responsibility.** The composition root: assembles Configuration, Logging,
Discovery, Registration, Dependency Injection, and Lifecycle into one running
instance, and owns orchestration, startup, shutdown, cancellation, and
disposal ordering. Does **not** own business logic, configuration parsing,
module implementation, or logging implementation. Implemented exactly as
designed — responsibilities, a 13-phase lifecycle, complete startup/shutdown
sequence diagrams, its own 7-state machine, and a full failure model; see
*Runtime Host Architecture.md* and its companion documents, all now marked
implemented.

**Status.** Implemented (WP 2.7B), as `TempestHost`/`TempestHostBuilder` in
`Tempest.Core.Runtime`. Previously flagged as a gap across the WP 2.4, WP
2.5, and WP 2.6 retrospectives, then designed by WP 2.7A — this entry updates
that gap from "designed, awaiting implementation" to implemented and tested.

**Dependencies.** Every implemented service above — Configuration and Logging
first (constructed directly, outside the container), then Discovery and
Registration (deliberately *before* the DI container is built — see
ADR-0011), then Dependency Injection, then Lifecycle.

**Key types.** `ITempestHost`, `TempestHost`, `ITempestHostBuilder`,
`TempestHostBuilder`, `HostState`, `HostException`,
`InvalidHostStateTransitionException`.

**Consumers (anticipated).** `Tempest.App` / the process entry point; future
hosted services, background workers, and — pending their own classification
under ADR-0013 — a Requirements Engine and/or Project Engine.

**ADR references.** ADR-0004 (disposal reused at Host level, and its WP 2.7B
update), ADR-0008 (why Discovery/Registration precede DI — see ADR-0011),
ADR-0009 (composition root pattern), ADR-0011 (*Discovery and Registration
Precede DI Container Construction*), ADR-0012 (*The Runtime Host Owns an
Independent State Machine*), ADR-0013 (*Platform-Service Failures Abort
Startup; Module Failures Remain Isolated*), ADR-0014 (*Cancellation and
Shutdown-Request Are Distinct Signals*), ADR-0015 (*Runtime Hosts Are Not
Restartable*), ADR-0016 (*The Host Lives in Tempest.Core.Runtime, Distinct
From Tempest.Core.Hosting*), ADR-0017 (*Discovery, Registration, and
Lifecycle Remain Host-Owned Collaborators, Not Public DI Services*),
ADR-0018 (*Startup Cancellation Transitions to Controlled Shutdown*),
ADR-0019 (*Host Disposal Is Always an Explicit, Idempotent Call*).

**Academy references.** WP 2.7 retrospective (*Runtime Host Architecture
Review*); WP 2.7B retrospective (*Runtime Host Implementation*, including its
Alternatives Considered and Architectural Debt Assessment); Engineering
Principle 11 (*Atomic Phase Principle*); *The Startup Sequence* (Runtime
Architecture); *Runtime Host Architecture.md*, *Host Lifecycle.md*, *Startup
Sequence.md*, *Shutdown Sequence.md*, *Runtime State Machine.md*, *Failure
Behaviour.md*, *Ownership Matrix.md* (all `docs/architecture/`).

---

## Event Bus *(contract implemented — WP 4.0; implemented — WP 4.4D, ADR-0028; consumed — WP 4.4E)*

**Responsibility.** Lets modules publish and subscribe to events without
depending on each other directly. `IEvent` marks a published fact; a
concrete event type carries whatever data its subscribers need.
`IEventHandler<T>` is the consumer-facing subscription contract. Publish
is imperative (`Subscribe`/`Unsubscribe`/`PublishAsync`), dispatched
sequentially in subscription order over a per-call snapshot, with every
subscriber failure isolated unconditionally — see ADR-0028 and `Event Bus
Architecture.md` for the complete design. Built and tested; no module
consumes it yet — `ClockModule`'s own extension is a separate, later work
package.

**Key types.** `IEvent`, `IEventHandler<T>` (`Tempest.Core.Events`,
implemented WP 4.0). `IEventBus`/`EventBus` (`Tempest.Core.Events`) —
implemented WP 4.4D, per ADR-0028's design in full.

**Dependencies.** None for the contracts themselves. `IEventBus` is
DI-public (ADR-0020), resolved like `IConfigurationProvider`/`ILogger` —
registered as an ordinary container-constructed singleton
(`services.Singleton<IEventBus, EventBus>()` in `TempestHost.cs`'s
existing Platform Services Registered block), requiring no Composition
Root treatment and no new Dependency Injection capability (ADR-0028).

**Consumers.** Any module — including a plugin-loaded module
(`Tempest.Core.Plugins`, `WP 4.2`) and a future `IHostedService`
(`WP 4.5`), neither of which requires any special-casing (ADR-0028). First
real consumer, `WP 4.4E`: `ClockModule` publishes a
`ClockModuleLifecycleEvent` from each lifecycle method;
`ClockLifecycleObserverModule` (a new companion module) subscribes —
proven end-to-end, including through the real, unmodified `TempestHost`.

**ADR references.** ADR-0020 (*The Event Bus Is a DI-Public Platform
Service*), ADR-0023 (*Platform Layering*), ADR-0024 (*Platform Contracts
Are Packaged by Capability*), ADR-0028 (*Event Bus Dispatch, Subscription,
and Failure Model* — fully realised, WP 4.4D).

**Academy references.** WP 4.0 retrospective (*Platform Contracts*); WP 4.4
architecture retrospective (*Event Bus Architecture*); WP 4.4D
implementation retrospective; WP 4.4E retrospective (*Sample Module Event
Integration*); *Building an Event-Driven Module* (Academy); `Event Bus
Architecture.md`; Rejected Designs RD-0019 through RD-0022;
`docs/releases/v0.4.0/WorkPackages.md` (`WP 4.4`).

---

## Background Services *(implemented — WP 4.5, ADR-0029/ADR-0030; contracts WP 4.0)*

**Responsibility.** Background work that starts after Module Initialisation
and stops before Module Disposal. `IHostedService` defines Start/Stop;
`ICriticalBackgroundService` is the opt-in marker for a service whose
failure should be Host-fatal rather than isolated (ADR-0021). A hosted
service is discovered via reflection (mirroring Module/Plugin Discovery),
never instantiated during discovery (it carries no metadata to read),
registered as an ordinary self-referential singleton during the existing
Platform Services Registered phase, and started/stopped by a new,
Host-owned `IHostedServiceManager` in deterministic, sequential order
(reverse order for stop) — see ADR-0029 and `Background Services
Architecture.md` for the complete design. Wired into the Runtime Host's
startup/shutdown sequence as decimal-numbered phases `8.1`/`10.1` — see
*Host Lifecycle.md*.

**Key types.** `IHostedService`, `ICriticalBackgroundService`
(`Tempest.Core.BackgroundServices`, implemented WP 4.0).
`IHostedServiceDiscoveryService`/`HostedServiceDiscoveryService`,
`IHostedServiceManager`/`HostedServiceManager`, `HostedServiceState`,
`HostedServiceStatus` — implemented, WP 4.5, exactly per ADR-0029's
design (the discovery service's implemented name,
`HostedServiceDiscoveryService`, is a cosmetic rename from the design
phase's working name, `ReflectionHostedServiceDiscoveryService` — no
behavioural change).

**Dependencies.** None for the contracts themselves. `IHostedServiceManager`
and `IHostedServiceDiscoveryService` are Host-owned (ADR-0017, applied
to a new component), constructed directly by `TempestHost`, never
DI-public — a deliberate contrast with the Event Bus, immediately above:
individual hosted service *instances* may consume `IEventBus` and any
other DI-public service, but the *manager that starts and stops them* is
kept as Host-owned as Discovery/Registration/Lifecycle are.

**Consumers.** Any module declaring a hosted service. A hosted service
instance may itself consume any DI-public Platform Service, including
`IEventBus`, via ordinary constructor injection.

**ADR references.** ADR-0021 (*Background Service Failures Are Isolated by
Default; Criticality Is Opt-In*), ADR-0023, ADR-0024, ADR-0029 (*Background
Service Discovery, Ownership, and Orchestration Model*), ADR-0030
(*Background Service Host Lifecycle Placement*).

**Academy references.** WP 4.0 retrospective (*Platform Contracts*); WP 4.5
architecture retrospective (*Background Services Design*); WP 4.5
implementation retrospective (*Background Services Implementation*);
`Background Services Architecture.md`; Rejected Designs RD-0023 through
RD-0029; `docs/releases/v0.4.0/WorkPackages.md` (`WP 4.5`).

---

## Command Framework *(contract implemented — WP 4.0; dispatcher planned — WP 4.7)*

**Responsibility.** A uniform way to request a discrete unit of application
logic. `ICommand` marks a concrete command type, which carries its own
parameters as ordinary data. No dispatcher exists yet — a command type
implementing this interface cannot currently be invoked by anything.

**Key types.** `ICommand` (`Tempest.Core.Commands`, implemented WP 4.0). A
handler contract and dispatcher — not yet defined; `WP 4.7`'s own design
work, deliberately not speculated on ahead of it.

**Dependencies.** None for the contract itself. **Explicitly orthogonal to
Navigation** (ADR-0022) — neither this nor the future Navigation service
depends on the other.

**Consumers.** Any module, once `WP 4.7` implements the dispatcher.

**ADR references.** ADR-0022 (*Navigation and Commands Are Orthogonal
Platform Services*), ADR-0023, ADR-0024.

**Academy references.** WP 4.0 retrospective (*Platform Contracts*);
`docs/releases/v0.4.0/WorkPackages.md` (`WP 4.7`).

---

## Plugin Manifest *(implemented — WP 4.2, `Tempest.Core.Plugins`)*

**Responsibility.** Describes a module before it is loaded — a
pre-Discovery artifact, distinct from `ModuleDescriptor`, which describes a
module already loaded and reflectable. The Manifest describes; the Runtime
decides. `PluginManifestDiscoveryService` (Phase 3.1) scans a plugins
directory for `plugin.manifest.json` files, parses, validates, and checks
platform-version compatibility, producing a deterministic, ordered list of
`PluginManifest` values; `PluginAssemblyLoader` (Phase 3.2) loads each
eligible plugin's declared assembly. See *Plugin Manifest Architecture.md*
for full detail, including its "Public API — As Implemented" section.

**Key types.** `PluginManifest`, `PluginException` and five subtypes
(`InvalidPluginManifestException`, `IncompatiblePluginVersionException`,
`DuplicatePluginIdException`, `PluginAssemblyNotFoundException`,
`PluginAssemblyLoadException`), `IPluginManifestDiscoveryService` /
`PluginManifestDiscoveryService`, `IPluginAssemblyLoader` /
`PluginAssemblyLoader` (`Tempest.Core.Plugins`).

**Status.** Implemented — WP 4.2. Plugin failure classification
(ADR-0025, WP 4.2B) — isolated for every failure category except a
genuine Host-level defect in plugin-loading orchestration itself.
Lifecycle placement (ADR-0026, WP 4.2C) — two new phases, `3.1` Plugin
Discovery and `3.2` Plugin Loading, between Logging Built and Module
Discovery, no renumbering of the existing thirteen phases, no change to
`Runtime State Machine.md`. The cross-cutting platform-version gap this
design originally surfaced is also resolved (WP 4.2A, see the Platform
Version entry, above). 27 tests (unit-level `PluginManifestDiscoveryService`/
`PluginAssemblyLoader` coverage, plus Host-level integration tests)
verify every ADR-0025 failure category and ADR-0026 ordering guarantee.

**Dependencies.** Logging Built and `PlatformVersionProvider`
(construction moved earlier per ADR-0026) both exist before Plugin
Discovery (Phase 3.1) begins. Plugin Loading (Phase 3.2) precedes Module
Discovery (Phase 4), analogous to how Configuration and Logging already
precede it today.

**Consumers.** Module Discovery — unchanged (zero code touched), since any
assembly Plugin Loading loads becomes visible to
`AppDomain.CurrentDomain.GetAssemblies()` exactly like any other loaded
assembly — proven directly by
`PluginAssemblyLoaderTests.LoadPlugins_LoadedAssembly_IsVisibleToUnchangedModuleDiscovery`,
which loads a real, dynamically-built assembly and confirms
`ReflectionFrameworkDiscoveryService` finds its module unaided.
`IFrameworkDiscoveryService`, `RuntimeModuleManager`, and
`ModuleLifecycleManager` remain untouched.

**ADR references.** ADR-0025 (*Plugin Failure Classification*) — decided,
implemented. ADR-0026 (*Plugin Discovery Lifecycle Placement*) — decided,
implemented.

**Academy references.** WP 4.2 retrospective (*Plugin Manifest
Architecture*); WP 4.2A retrospective (*Runtime Platform Version
Infrastructure*); WP 4.2B retrospective (*ADR: Plugin Failure
Classification*); WP 4.2C retrospective (*ADR: Plugin Discovery Lifecycle
Placement*); WP 4.2 implementation retrospective; *Plugin Manifest
Architecture.md*; Rejected Designs RD-0008 through RD-0014.

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
