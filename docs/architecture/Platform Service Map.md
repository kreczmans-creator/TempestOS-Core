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
| Command Framework | **Implemented — WP 5.1A (design), WP 5.1B (implementation)** (`ICommandDispatcher`/`ICommandRegistry`, `Tempest.Core.Commands`) — orthogonal to Navigation, ADR-0022 | Dependency Injection | `CommandSampleModule` (real contributor); `Tempest.App` (invocation, not yet wired into the Shell's own input handling) |
| Navigation | **Implemented — WP 5.0A (design), WP 5.0B (implementation)** (`INavigationProvider`/`NavigationService`, `Tempest.Core.Navigation`) — model, ownership, and rendering boundary per ADR-0031/ADR-0032 | Dependency Injection, Event Bus | Any module contributing a navigation item; `Tempest.App` (rendering, not yet built) |
| Diagnostics | **Implemented — WP 5.2** (`IDiagnosticsProvider`/`DiagnosticsProvider`, `Tempest.Core.Diagnostics`) — read-only projection over Host/module/hosted-service lifecycle state per ADR-0039 | Dependency Injection (constructed directly by `TempestHost`, ADR-0009); reads live data from `IModuleLifecycleManager`/`IHostedServiceManager` via `Func<T>` accessors, never resolves either through the container (ADR-0017) | `DiagnosticsSampleModule` (real contributor); any future Shell status page or health-check command |
| Identity & Permissions | **Implemented — WP 6.1** (`IIdentity`/`PlatformIdentity`, `IPrincipal`/`PlatformPrincipal`, `Permission`, `IRole`/`Role`, `IRoleProvider`/`RoleProvider`, `ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor`, `IPermissionEvaluator`/`PermissionEvaluator`, `IIdentityService`/`IdentityService`, `Tempest.Core.Identity`) — local-only identity model per ADR-0043; single authorization enforcement point per ADR-0044 | Dependency Injection | `IdentitySampleModule` (real contributor); `TD-09`/`TD-10`/`TD-11` are now resolvable through this enforcement point, though none is retired by this Work Package itself; a plausible future `WP 6.3` (REST API) and `WP 6.5` (Audit) consumer |
| Persistence | **Implemented — WP 6.4** (`IPersistenceStore`/`PersistenceStore`, `Tempest.Core.Persistence`) — established as part of Settings' own scope per ADR-0041; file-backed, one file per `collection`/`key`, percent-encoded paths, per-key async locking | Dependency Injection, Configuration (root path) | Settings (real contributor via `SettingsProvider`); Audit (real contributor via `AuditRecorder`/`AuditQuery`, `WP 6.5`) — the reuse `ADR-0041` recommended, now confirmed in practice |
| Settings | **Implemented — WP 6.4** (`ISettingDefinition`/`SettingDefinition`, `ISettingsProvider`/`SettingsProvider`, `ISettingsChangedEvent`/`SettingsChangedEvent`, `Tempest.Core.Settings`) — DI-public, distinct from Configuration per ADR-0042; in-memory cache over Persistence, invalidated on write | Dependency Injection, Persistence, Event Bus | `SettingsSampleModule` (real contributor); a plausible future `WP 6.3` (REST API) settings-management surface |
| Audit | **Implemented — WP 6.5** (`IAuditRecord`/`AuditRecord`, `IAuditRecorder`/`AuditRecorder`, `IAuditQuery`/`AuditQuery`, `AuditQueryCriteria`, `Tempest.Core.Audit`) — durable, queryable, append-only history distinct from Logging/Diagnostics per ADR-0045; reuses Persistence, never a second storage mechanism; `IAuditQuery` permission-gated via `ADR-0044` | Dependency Injection, Persistence, Identity & Permissions | `AuditSampleModule` (real contributor); also a real dependency of `ApiRequestHandler` (`Tempest.Core.Api`), `ReportingSampleModule`, `ExportImportSampleModule`, and `LicensingSampleModule` |
| Notifications | **Implemented — WP 6.2** (`INotification`, `INotificationHandler<T>`, `INotificationDispatcher`/`NotificationDispatcher`, `Tempest.Core.Notifications`) — derived from, not a replacement for, the Event Bus per ADR-0046; transient only, no persistence this release; additive `IPlatformNotification`/`PlatformNotification`/`NotificationSeverity` general-purpose shape | Dependency Injection | `NotificationSampleModule` (real contributor); `NotificationSampleHostedService` (the platform's first real, non-infrastructure hosted service); also a real dependency of `ReportingSampleModule`, `ExportImportSampleModule`, and `LicensingSampleModule`; a future UI Shell remains a plausible future consumer |
| Reporting | **Implemented — WP 6.0** (`IReportDefinition`, `IReportRenderer<T>`, `IReportingService`/`ReportingService`, `Tempest.Core.Reporting`) — orthogonal to Export/Import per ADR-0040; no permission-gating of its own (caller enforces, mirroring Navigation/Command Framework); additive `IReportTemplate<T>`/`PlainTextReportTemplate<T>` general-purpose template shape | Dependency Injection | `ReportingSampleModule` (real contributor, also demonstrating Identity/Settings/Audit/Notifications integration at the calling layer); a plausible future consumer for the REST API and any engineering module |
| REST API | **Implemented — WP 6.3** (`IApiEndpointRegistry`/`ApiEndpointRegistry`, `ApiRequestHandler`, `RestApiHostedService`, `Tempest.Core.Api`) — hosted on ASP.NET Core/Kestrel per ADR-0049, orchestrated as an ordinary hosted service per ADR-0047, dispatches every route through the existing, unmodified Command Framework per ADR-0048; identity resolved per-request without touching the shared ambient current principal per ADR-0052 | Dependency Injection, Identity & Permissions, Audit | `ApiSampleModule` (real contributor, exposing `ReportingSampleModule`'s own command with zero business logic of its own); any future engineering module wanting an HTTP-reachable route |
| Export/Import | **Implemented — WP 6.7** (`IExportable`/`IExportService`/`ExportService`, `IImportService`/`ImportService`, `Tempest.Core.ExportImport`) — orthogonal to Persistence per ADR-0051; additive `IExportableKind`/`IImportable` Kind-routing, `IExportFormat`/`JsonExportFormat` artifact framing, and optional `IExportPayloadSerializer`/`JsonExportPayloadSerializer` general-purpose shapes | Dependency Injection | `ExportImportSampleModule` (real contributor, round-tripping two Settings values as a single multi-source artifact, also demonstrating Identity/Audit/Notifications integration at the calling layer); a plausible future consumer for Licensing and any engineering module |
| Licensing | **Implemented — WP 6.6** (`ILicense`/`ILicenseValidator`/`LicenseValidator`, `ILicenseProvider`/`LicenseProvider`, `Tempest.Core.Licensing`) — pre-container, Host-fatal validation gate per ADR-0050, except a missing license file, which is a valid, unrestricted-but-uncapable default (resolving Risk Register R5) | `System.Text.Json` (BCL) only | `LicensingSampleModule` (real contributor, also demonstrating Identity/Settings/Audit/Notifications/REST API integration at the calling layer); a plausible future consumer for any commercially licensed engineering module |
| Plugin Manifest | **Implemented — WP 4.2** (`Tempest.Core.Plugins`) | Host (Phases 3.1/3.2, ADR-0026 — a pre-Discovery step) | Module Discovery (unchanged), any real plugin |
| Project Engine | Planned | Undetermined | Undetermined |
| Requirements Engine | **Implemented — WP 7.3A** (`IRequirementsService`/`RequirementsService`, `Tempest.Core.Requirements`) — the canonical, discipline-neutral requirement representation per `ADR-0058`; requirements/collections/groups are `IEngineeringDocument`s, relationships are `DocumentReference`s, zero new storage mechanism | Dependency Injection, Engineering Data Model, Verification | `RequirementsSampleModule` (real contributor, also demonstrating Identity/Audit/Reporting/Export-Import integration at the calling layer); a plausible future consumer for any discipline-specific engineering module |

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
`CompositeLogSink` (`WP 5.2` — fans a log entry out to any number of
child `ILogSink`s, isolating one child's own write failure from every
other; closes `TD-02`), `Logger`, `LoggerFactory`, `LogEntry`, `LogLevel`,
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

**Responsibility.** Assembles Configuration, Logging, Discovery,
Registration, Dependency Injection, and Lifecycle into one running
instance, and owns orchestration, startup, shutdown, cancellation, and
disposal ordering. Does **not** own business logic, configuration parsing,
module implementation, or logging implementation. Implemented exactly as
designed — responsibilities, a 13-phase lifecycle, complete startup/shutdown
sequence diagrams, its own 7-state machine, and a full failure model; see
*Runtime Host Architecture.md* and its companion documents, all now marked
implemented.

**A naming clarification, disclosed rather than left to collide
silently** (`WP 5.0C`): earlier text here called the Host itself "the
composition root," informally. `ADR-0009`'s own, authoritative definition
is narrower and different in kind — "whatever code assembles a *running*
TempestOS instance... eventually `Program.cs`" — which describes whatever
*constructs* `ITempestHost` (test setup, and, since `WP 5.0D`,
`Tempest.App`'s own Shell), not the Host's own internal wiring of its six
constituent services. Both uses were accurate to what they described;
only the shared label was ambiguous. See `Shell & Composition Framework
Architecture.md` and `ADR-0033` for the Shell's own composition-root role
in `ADR-0009`'s sense, and `ADR-0034` for the read-only `Services`
property, implemented `WP 5.0D`, that lets it reach
`INavigationProvider`/`IEventBus`.

**Status.** Implemented (WP 2.7B), as `TempestHost`/`TempestHostBuilder` in
`Tempest.Core.Runtime`. Previously flagged as a gap across the WP 2.4, WP
2.5, and WP 2.6 retrospectives, then designed by WP 2.7A — this entry updates
that gap from "designed, awaiting implementation" to implemented and tested.

**Dependencies.** Every implemented service above — Configuration and Logging
first (constructed directly, outside the container), then Discovery and
Registration (deliberately *before* the DI container is built — see
ADR-0011), then Dependency Injection, then Lifecycle.

**Key types.** `ITempestHost` (including `Services`, ADR-0034), `TempestHost`,
`ITempestHostBuilder`, `TempestHostBuilder`, `HostState`, `HostException`,
`InvalidHostStateTransitionException`.

**Consumers.** `Tempest.App`'s own Shell (`TempestShell`, implemented
`WP 5.0D`) — the process entry point's own composition root, per
`ADR-0033`, constructing and running the Host, then resolving
`INavigationProvider`/`IEventBus` through `Services`; future hosted
services, background workers, and — pending their own classification
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
ADR-0019 (*Host Disposal Is Always an Explicit, Idempotent Call*),
ADR-0033 (*The Shell Is a Composition Root Layered Above the Runtime
Host*, `WP 5.0C` design, `WP 5.0D` implementation), ADR-0034
(*`ITempestHost` Exposes a Read-Only Service Resolution Surface*,
`WP 5.0C` design, `WP 5.0D` implementation).

**Academy references.** WP 2.7 retrospective (*Runtime Host Architecture
Review*); WP 2.7B retrospective (*Runtime Host Implementation*, including its
Alternatives Considered and Architectural Debt Assessment); Engineering
Principle 11 (*Atomic Phase Principle*); *The Startup Sequence* (Runtime
Architecture); WP 5.0C retrospective (*Shell & Composition Framework
Architecture*); WP 5.0D retrospective (*Shell & Composition Framework
Implementation*); *Shell & Application Composition* (Academy concept
guide); *Runtime Host Architecture.md*, *Host Lifecycle.md*, *Startup
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

## Command Framework *(implemented — WP 5.1A design, WP 5.1B implementation, ADR-0036–ADR-0038)*

**Responsibility.** A uniform, UI-agnostic way to request a discrete unit
of application logic, invokable by a typed caller (`ICommandDispatcher.
DispatchAsync<TCommand>`) or by a caller with only a string Id
(`ICommandRegistry.InvokeAsync`) — a menu, a toolbar, a keyboard
shortcut, a future touch gesture, or a future automation/AI service.
`ICommand` marks a concrete command type, which carries its own
parameters as ordinary data; exactly one `ICommandHandler<TCommand>`
handles it, and the caller receives a `CommandResult` (or a propagated
exception) so it genuinely knows whether the command succeeded.

**Key types.** `ICommand` (`Tempest.Core.Commands`, implemented WP 4.0,
unchanged). `ICommandHandler<TCommand>`, `ICommandDispatcher`/
`CommandDispatcher`, `CommandDescriptor`, `ICommandRegistry`/
`CommandRegistry`, `CommandResult`, `CommandHandlerTable` (an internal-in-
spirit, DI-registered collaborator shared by the dispatcher and the
registry), and five exception types (`CommandException`,
`DuplicateCommandHandlerException`, `DuplicateCommandIdException`,
`CommandHandlerNotRegisteredException`, `CommandNotFoundException`) —
designed WP 5.1A, implemented WP 5.1B with zero deviation from the
approved public shape.

**Dependencies.** None module-specific — depends on nothing but the
handler/descriptor instances registered into it. **Explicitly orthogonal
to Navigation** (ADR-0022) — neither this nor `NavigationService`
depends on the other. **Never dispatched through the Event Bus**
(ADR-0037, RD-0039) — a command handler may use `IEventBus` as an
ordinary peer dependency, exactly as it may use `INavigationProvider`.

**Consumers.** `CommandSampleModule` (`Tempest.Samples`, WP 5.1B) — the
real, first consumer, registering `IncrementCounterCommand` (success/
failure) and `NavigateToSampleHomeCommand` (the first concrete
realisation of ADR-0022's own `OpenModuleCommand → NavigationService.
Navigate(...)` illustration). `Tempest.App`'s Shell can resolve both
`ICommandDispatcher`/`ICommandRegistry` via `ITempestHost.Services`
today; wiring the Shell's own input handling (menus, keyboard shortcuts)
to them is a later Work Package's own scope.

**ADR references.** ADR-0022 (*Navigation and Commands Are Orthogonal
Platform Services*), ADR-0023, ADR-0024, ADR-0036 (*Command Framework Is
a DI-Public Platform Service*), ADR-0037 (*Command Registration Model*),
ADR-0038 (*Command Dispatch Failure Model*).

**Academy references.** WP 4.0 retrospective (*Platform Contracts*); WP
5.1A retrospective (*Command Framework Architecture*); WP 5.1B
retrospective (*Command Framework Implementation*);
`docs/releases/v0.5.0/WorkPackages.md` (`WP 5.1B`).

---

## Navigation *(implemented — WP 5.0A design, WP 5.0B implementation, ADR-0031/ADR-0032)*

**Responsibility.** The primary mechanism by which a user navigates the
application — built-in platform pages, future engineering modules, and
future plugins each contribute a `NavigationItem` (identity, title, an
optional symbolic icon key, ordering, grouping, hierarchy via a parent
reference, an optional visibility predicate) to one coherent catalogue.
`INavigationProvider`/`NavigationService` holds that catalogue and
exposes `Navigate(id)`, which publishes a `NavigationRequestedEvent`
through the existing Event Bus. **The model is UI-agnostic by design** —
`Tempest.Core.Navigation` contains no rendering type, delegate, or UI
framework reference of any kind; resolving a navigated-to item into an
actual screen is entirely `Tempest.App`'s (or any future UI shell's) own
responsibility. See `Navigation Framework Architecture.md` for the
complete design.

**Key types.** `NavigationItem`, `INavigationProvider`/`NavigationService`,
`NavigationRequestedEvent`, `NavigationException` and two subtypes
(`DuplicateNavigationItemException`, `NavigationItemNotFoundException`) —
designed in full (`ADR-0031`, `ADR-0032`) and implemented with zero
deviation in `WP 5.0B`, in a new `Tempest.Core.Navigation` namespace
(`ADR-0024`'s established capability-packaging pattern). Registered as an
ordinary DI-public singleton in `TempestHost`'s existing Platform Services
Registered phase, alongside `IEventBus`.

**Dependencies.** `IEventBus` (to publish `NavigationRequestedEvent`) —
a platform-service-to-platform-service dependency with direct precedent
(`LoggerFactory` → `IConfigurationProvider`), introducing no cycle.
**Explicitly orthogonal to Command Framework** (ADR-0022) — neither
depends on the other; application logic wires the two together, exactly
as ADR-0022's own illustrative shapes show.

**Consumers.** Any module or plugin-loaded module contributing a
navigation item, via ordinary constructor injection — no special-casing
for either (`ADR-0032`). `Tempest.App` (or a future UI shell) is a
consumer of a different kind: it enumerates `Items` to render a menu and
subscribes to `NavigationRequestedEvent` to perform the actual view swap,
using its own, entirely private mapping from `Id` to rendering — a
mapping `Tempest.Core.Navigation` never sees.

**ADR references.** ADR-0022 (orthogonality with Command Framework,
decided during original v0.4.0 planning), ADR-0023, ADR-0024, ADR-0031
(*Navigation Contracts Belong in Tempest.Core; Rendering Remains an
Application Responsibility*), ADR-0032 (*Navigation Is a DI-Public
Platform Service, Registered Imperatively, Reusing the Event Bus*).

**Academy references.** WP 4.0 retrospective (*Platform Contracts* —
`ICommand`/`IEvent` as the precedent this design's own UI-agnosticism
reasoning draws on); WP 5.0A retrospective (*Navigation Framework
Architecture*); WP 5.0B retrospective (*Navigation Framework
Implementation*); `Navigation Framework Architecture.md`; *Navigation
Architecture* (Academy concept guide); Rejected Designs RD-0030 through
RD-0033; `docs/releases/v0.5.0/WorkPackages.md` (`WP 5.0A`/`WP 5.0B`).

---

## Diagnostics *(implemented — WP 5.2, ADR-0039)*

**Responsibility.** A read-only projection over the Host's own current
lifecycle state — `HostState`, every registered module's
`ModuleLifecycleStatus`, and every hosted service's `HostedServiceStatus`
— exposed to any DI-resolving consumer, without granting that consumer
write access to `IModuleLifecycleManager`/`IHostedServiceManager`
themselves (both remain Host-owned, never DI-public, per `ADR-0017`). See
`Diagnostics Architecture.md` for the complete design.

**Key types.** `IDiagnosticsProvider`/`DiagnosticsProvider`
(`Tempest.Core.Diagnostics`). Reuses `ModuleLifecycleStatus`
(`Tempest.Core.Modules`) and `HostedServiceStatus`
(`Tempest.Core.BackgroundServices`) exactly as they already exist —
neither is duplicated or wrapped in a new type.

**Dependencies.** None as ordinary constructor parameters — instead, three
`Func<T>` accessors supplied by `TempestHost` at construction, closing
over its own `State` property and `_lifecycleManager`/
`_hostedServiceManager` private fields. This is deliberate: neither
manager exists yet at Phase 6 (Platform Services Registered), where
`DiagnosticsProvider` itself is registered, so a direct constructor
reference would not compile. Before a referenced manager is actually
constructed, its own accessor reports an empty collection — never an
exception — mirroring `ITempestHost.Services`'s own "not yet available"
convention (`ADR-0034`).

**A genuine, disclosed architectural note (`WP 6.8`), not a defect.**
`Tempest.Core.Diagnostics` imports `Tempest.Core.Runtime` for exactly
one type — the `HostState` enum, exposed via `IDiagnosticsProvider.HostState`
— a mutual namespace reference with `Runtime` (which imports
`Diagnostics` to construct `DiagnosticsProvider`). A strictly literal
reading of `ADR-0023`'s "dependencies flow downward only" would flag
this as an upward reference from a Platform Service to the Runtime Host
layer. In practice this is confined to one read-only, side-effect-free
enum type, has shipped without incident since this Work Package
introduced it, and involves no behavioural coupling. `WP 6.8`'s own
`Platform Architecture Conformance Report.md` recommends a future
release either formally accept this as a named `ADR-0023` exception or
relocate `HostState` to a neutral namespace.

**Consumers.** `DiagnosticsSampleModule` (real contributor and consumer);
`GetDiagnosticsSummaryCommandHandler` (`Tempest.Samples`, demonstrating
the Command Framework and Diagnostics interacting); any future Shell
status page or health-check command.

**Lifecycle.** Constructed directly by `TempestHost` and registered via
`AddInstance` — the Composition Root pattern (`ADR-0009`) — immediately
after the Command Framework's own three registrations, still within
Phase 6 (Platform Services Registered). No new Host Lifecycle phase.

**ADR references.** ADR-0009 (Composition Root, reused a fourth time);
ADR-0017 (Host-owned collaborators never DI-public — the boundary this
design's entire shape exists to respect); ADR-0034 (the
`null`/empty-before-ready convention this design reuses); ADR-0039
(*Diagnostics Is a DI-Public, Lazily-Projected Read-Only Service Over
Host-Owned Lifecycle State*).

**Academy references.** WP 5.2 retrospective (*Diagnostics
Improvements*); *Diagnostics & Composite Logging* (Academy concept
guide); Rejected Designs RD-0042 through RD-0044;
`docs/releases/v0.5.0/WorkPackages.md` (`WP 5.2`).

---

## Identity & Permissions *(implemented — WP 6.1, ADR-0043/ADR-0044)*

**Responsibility.** Answers who is performing an action, and whether
they are allowed to. `IIdentity`/`IPrincipal` model a local-only actor
(no authentication step, ADR-0043 — a caller-supplied identity id is
trusted outright); `IRole`/`IRoleProvider` resolve config-sourced role
definitions (`Identity:Roles:{RoleName}:Permissions`);
`IIdentityService` resolves a principal by identity id (flattening its
configured roles into permissions, fail-closed to zero permissions for
an unrecognised id) and establishes it as current;
`ICurrentPrincipalAccessor` exposes that current principal read-only;
`IPermissionEvaluator` is the single, uniform authorization enforcement
point (`RequirePermission` throws `PermissionDeniedException`;
`HasPermission` is the non-throwing form) every future consumer is
expected to call (ADR-0044). See `docs/releases/v0.6.0/Release
Architecture.md` and companions for the full design, and `ADR-0043`/
`ADR-0044` for what implementation confirmed, elaborated, or departed
from in that design.

**Key types.** `IIdentity`/`PlatformIdentity`, `IPrincipal`/
`PlatformPrincipal`, `Permission`, `IRole`/`Role`, `IRoleProvider`/
`RoleProvider`, `ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor`,
`IPermissionEvaluator`/`PermissionEvaluator`, `IIdentityService`/
`IdentityService`, `IdentityException` and two subtypes
(`PermissionDeniedException`, `RoleNotFoundException`) — all
`Tempest.Core.Identity`. `IRole`/`IRoleProvider` and `IIdentityService`
are additive elaborations the original architecture package deferred to
this Work Package's own implementation phase, not part of its original
`Public Interface Catalogue.md` draft; `IIdentity`, `IPrincipal`,
`ICurrentPrincipalAccessor`, `IPermissionEvaluator`, and `Permission`
are implemented with zero signature deviation from that draft.

**Dependencies.** None beyond Dependency Injection and (for `RoleProvider`/
`IdentityService`) `IConfigurationProvider`, read the same way every
other config-sourced platform service reads it.

**Consumers.** `IdentitySampleModule` (real contributor and consumer,
the eighth production sample module) — establishes a default local
principal during its own `InitialiseAsync` and registers a command
(`CheckSamplePermissionCommand`) demonstrating both the granted and
fail-closed-denied paths against the same, unmodified module, depending
on configuration. `TD-09` (plugin isolation), `TD-10` (Navigation
ownership), and `TD-11` (Command/Navigation registration-order
squatting) are now *resolvable* through `IPermissionEvaluator` — **none
is retired by this Work Package**: retrofitting an enforcement call into
`NavigationService`, Command/Navigation registration, or plugin loading
was explicitly out of this Work Package's own scope (see `ADR-0044`).
Future, explicitly-scoped consumers: `WP 6.3` (REST API, a hard
dependency per `docs/releases/v0.6.0/WorkPackages.md`) and `WP 6.5`
(Audit, for attribution).

**Lifecycle.** `CurrentPrincipalAccessor` is constructed directly by
`TempestHost` (a plain `new` — it has no constructor dependencies) and
registered via `AddInstance` under *both* `ICurrentPrincipalAccessor`
and its own concrete type — the same already-built instance under two
service-type keys, so `IdentityService` (which needs write access via
the concrete type) and every ordinary consumer (which resolves only the
read-only interface) share one object rather than two independently-
constructed ones. `IRoleProvider`, `IPermissionEvaluator`, and
`IIdentityService` are ordinary, container-constructed singletons,
registered in `TempestHost`'s existing Platform Services Registered
block (Phase 6) — no new Host Lifecycle phase.

**A genuine implementation-phase departure from the architecture
package, disclosed rather than absorbed silently:**
`CurrentPrincipalAccessor` is backed by a single, `lock`-protected
mutable field, not `AsyncLocal<T>` as `Platform Service Contracts.md`
tentatively suggested — `AsyncLocal<T>` would make a principal
established during Module Initialisation invisible to any later,
unrelated caller (a dispatched command, a test), which does not fit
this release's own local-only, single-ambient-principal need. See
`ADR-0044` for the full reasoning and the regression test that proves
it.

**ADR references.** ADR-0043 (*Identity Model Scope Is Local-Only,
Extensible*); ADR-0044 (*`IPermissionEvaluator` Is the Single
Authorization Enforcement Point; `CurrentPrincipalAccessor` Is Ambient,
Not Request-Scoped*).

**Academy references.** `WP 6.1` retrospective (*Permissions & Identity
Implementation*); `docs/releases/v0.6.0/Release Architecture.md`,
`Platform Services Overview.md`, `Public Interface Catalogue.md`,
`Service Lifecycle.md`, `Required ADRs.md` (the architecture package
this Work Package implemented); `Platform Service Contracts.md`,
`Platform Service Implementation Order.md`, `Service Registration
Matrix.md`, `Testing Strategy.md` (the Contract Review package);
`docs/governance/Quality/Technical Debt Register.md` (`TD-09`, `TD-10`,
`TD-11`); `docs/security/Platform Security Review v0.5.0.md` (Findings
SEC-01, NAV-1); `docs/architecture/Command Framework Architecture.md`
(Finding CMD-1).

---

## Persistence *(implemented — WP 6.4, ADR-0041)*

**Responsibility.** A minimal, internal, platform-owned durable store —
store, retrieve, delete, and enumerate string values, scoped by a
caller-supplied `collection` name and `key`. No schema, no querying
beyond key lookup and full-collection key enumeration, no transactions
across multiple keys. Established as part of `WP 6.4`'s own scope
specifically so no other platform service invents an incompatible
storage mechanism of its own (`ADR-0041`).

**Key types.** `IPersistenceStore`/`PersistenceStore`,
`PersistenceException` and one subtype
(`PersistenceStoreUnavailableException`) — all `Tempest.Core.Persistence`.
Reuses `Tempest.Core.Concurrency.AsyncKeyedLock` (internal, shared with
Settings) for per-`collection`/`key` concurrency control.

**Dependencies.** Dependency Injection; `IConfigurationProvider`, read
once at construction for the storage root path
(`Persistence:RootPath`, defaulting to `persistence-data`).

**Consumers.** Settings (`WP 6.4`, its own originating Work Package),
via `SettingsProvider`. Audit (`WP 6.5`), via `AuditRecorder`/
`AuditQuery` — the reuse `ADR-0041`'s own title anticipated, now
implemented and verified: each service owns its own, distinct
collection name (`"Settings"`, `"Audit"`), proving collection-scoping
isolation in practice, not merely in design.

**Lifecycle.** Ordinary DI-public, container-constructed singleton,
registered in `TempestHost`'s existing Platform Services Registered
block (Phase 6) — no new Host Lifecycle phase.

**Storage.** One file per `collection`/`key` pair, under the configured
root directory; both `collection` and `key` are percent-encoded
(`Uri.EscapeDataString`) before becoming a path segment, so an arbitrary
caller-supplied name can never produce an invalid or unintended
file-system path. Every operation acquires a per-`collection`/`key`
`AsyncKeyedLock` before touching the file system.

**ADR references.** ADR-0041 (*A Shared Persistence Abstraction Serves
Settings and Audit*).

**Academy references.** `WP 6.4` retrospective (*Settings Framework
Implementation*); `docs/releases/v0.6.0/Release Architecture.md` and
companions (the architecture package this Work Package implemented);
`Platform Service Contracts.md` and companions (the Contract Review
package).

---

## Settings *(implemented — WP 6.4, ADR-0042)*

**Responsibility.** User-changeable, runtime-mutable configuration,
explicitly distinct from Configuration (`WP 2.5`), which is read-only,
immutable, and loaded once at startup (`ADR-0009`, Case Study 05).
Registers setting definitions with defaults; reads and writes current
values; publishes `ISettingsChangedEvent` through the existing Event Bus
on every successful write, including a write of the already-current
value (`ADR-0042`'s own explicit default).

**Key types.** `ISettingDefinition`/`SettingDefinition`,
`ISettingsProvider`/`SettingsProvider`, `ISettingsChangedEvent`/
`SettingsChangedEvent`, `SettingsException` and two subtypes
(`DuplicateSettingDefinitionException`, `SettingNotFoundException`) —
all `Tempest.Core.Settings`.

**Dependencies.** Dependency Injection, Persistence (durable storage),
Event Bus (change notification).

**Consumers.** `SettingsSampleModule` (real contributor and consumer,
the ninth production sample module) — registers a setting definition,
subscribes to `ISettingsChangedEvent`, and registers two commands
(get/set) demonstrating the Command Framework and Settings interacting.
Also a real dependency of `ReportingSampleModule` (`WP 6.0`),
`ExportImportSampleModule` (`WP 6.7`), and `LicensingSampleModule`
(`WP 6.6`), each reading a customisable message at the calling layer.

**Lifecycle.** Ordinary DI-public, container-constructed singleton,
registered in `TempestHost`'s existing Platform Services Registered
block (Phase 6), after Persistence and the Event Bus — no new Host
Lifecycle phase.

**Performance.** An in-memory cache sits over `IPersistenceStore`,
invalidated only by this instance's own writes — `GetValueAsync` is a
likely hot-path call; a cache hit never touches the file system. A
per-key `AsyncKeyedLock` (shared implementation with Persistence)
serialises the cache-populate-on-miss sequence against the
write-then-cache-update sequence, for the same key, so a slow concurrent
read can never overwrite a newer write's own cache entry with a stale
value.

**A disclosed, deliberate limitation.** No sensitive-value flag exists
on `ISettingDefinition` in this release — every setting change is
logged at Information level with both old and new values, unredacted.
Named as a Future Extension Point, not a defect (`ADR-0042`).

**ADR references.** ADR-0041 (Persistence, shared with Audit's future
need), ADR-0042 (*Settings Is DI-Public and Distinct From
Configuration*).

**Academy references.** `WP 6.4` retrospective (*Settings Framework
Implementation*); `docs/releases/v0.6.0/Release Architecture.md` and
companions; `Platform Service Contracts.md` and companions;
`docs/academy/05 Case Studies/` Case Study 05 (Configuration
immutability, the distinction Settings exists to complement).

---

## Audit *(implemented — WP 6.5, ADR-0045)*

**Responsibility.** A durable, queryable, append-only record of who did
what, when — explicitly distinct from Logging (developer-facing, not
guaranteed durable) and Diagnostics (a live snapshot of *current*
state). Records an attributable action with the current principal
resolved automatically; answers filtered queries over previously
recorded actions. Never modifies or deletes an existing record.

**Key types.** `IAuditRecord`/`AuditRecord`, `IAuditRecorder`/
`AuditRecorder`, `IAuditQuery`/`AuditQuery`, `AuditQueryCriteria`,
`AuditException` — all `Tempest.Core.Audit`.

**Dependencies.** Dependency Injection, Persistence (durable storage,
reused from `WP 6.4`, never a second mechanism), Identity & Permissions
(`ICurrentPrincipalAccessor` for attribution; `IPermissionEvaluator` for
query-gating).

**Consumers.** `AuditSampleModule` (real contributor and consumer, the
tenth production sample module) — establishes its own principal, records
an action during its own initialisation, and registers two commands
(record/query) demonstrating both the recording path and the
permission-gated query path. Also a real, since-confirmed dependency of
`ApiRequestHandler` itself (`Tempest.Core.Api`, `WP 6.3`), and of four
further sample-module command handlers built on top of already-shipped
platform services: `ReportingSampleModule` (`WP 6.0`),
`ExportImportSampleModule` (`WP 6.7`), and `LicensingSampleModule`
(`WP 6.6`). *(This entry previously read "none yet implemented" —
corrected `WP 6.7`, as a genuine, pre-existing drift found during that
Work Package's own repository review, unrelated to Export/Import's own
scope: it had gone stale since `WP 6.0` first shipped a real consumer.)*

**Lifecycle.** Ordinary DI-public, container-constructed singletons
(`IAuditRecorder`, `IAuditQuery`), registered in `TempestHost`'s
existing Platform Services Registered block (Phase 6), after
Persistence and Identity & Permissions — no new Host Lifecycle phase.

**Storage.** Every record is serialised to JSON (`System.Text.Json`,
already used elsewhere in this codebase — `PluginManifestDiscoveryService`
— introducing no new dependency) and stored in its own
`IPersistenceStore` collection (`AuditRecorder.AuditCollectionName`,
`"Audit"`), distinct from Settings' own `"Settings"` collection —
proving Persistence's own collection-scoping isolation in practice.
`IAuditQuery.QueryAsync` filters client-side, over
`ListKeysAsync` plus a per-key `ReadAsync` — `IPersistenceStore` has no
native query capability (`ADR-0041`, confirmed again here, `ADR-0045`);
see `Technical Debt Register.md`'s `TD-12`.

**A genuine implementation-phase finding, disclosed rather than
absorbed silently:** `RecordAsync` is awaited, not literally
fire-and-forget, so a storage failure always propagates — the
Contract Review's own performance goal is met by keeping the write
itself minimal (a single, append-only file write), not by discarding
the returned `Task`. See `ADR-0045`'s own reasoning.

**ADR references.** ADR-0041 (Persistence, reused not reinvented);
ADR-0044 (the enforcement point Audit's own query-gating reuses);
ADR-0045 (*Audit Is a Durable, Queryable, Append-Only Record, Distinct
From Logging and Diagnostics — Recording Model, Permission Gating, and
Persistence Sufficiency*).

**Academy references.** `WP 6.5` retrospective (*Audit Framework
Implementation*); `docs/releases/v0.6.0/Release Architecture.md` and
companions; `Platform Service Contracts.md` and companions;
`docs/governance/Quality/Technical Debt Register.md` (`TD-12`);
`docs/releases/v0.6.0/Risk Register.md` (`R8`).

---

## Notifications *(implemented — WP 6.2, ADR-0046)*

**Responsibility.** The standard platform mechanism for publishing
user-facing and platform-generated notifications — `INotification`
marks a published fact (mirroring `IEvent`'s own marker shape);
`INotificationHandler<TNotification>` is the consumer-facing
subscription contract; `INotificationDispatcher` subscribes and
publishes, sequentially, in subscription order, isolating and logging
(at `Warning`) every subscriber's own exception, never rethrowing it.
Deliberately **not** a second, independent publish/subscribe
implementation — built to mirror the Event Bus's own proven dispatch
model exactly (`ADR-0028`/`ADR-0046`), since the two types' own,
independently-approved generic constraints (`where TNotification :
INotification` vs. `where TEvent : IEvent`) rule out literal
delegation. Transient only this release — a notification is not
retained after dispatch; no history or inbox capability exists yet.

**Key types.** `INotification`, `INotificationHandler<TNotification>`,
`INotificationDispatcher`/`NotificationDispatcher`,
`NotificationException` — all `Tempest.Core.Notifications`, implemented
with zero signature deviation from `Public Interface Catalogue.md`.
`IPlatformNotification`/`PlatformNotification`/`NotificationSeverity`
(`Information`, `Success`, `Warning`, `Error`) are additive elaborations
this Work Package's own implementation phase introduced — "Notification
severity" and "Notification categories" were named in this Work
Package's own brief but never drafted as interface members;
`IPlatformNotification` extends both `INotification` and `Events.IEvent`,
concretely realising `INotification`'s own doc comment ("typically
derived from... an `IEvent`") for this one general-purpose shape.

**Dependencies.** Dependency Injection; `Tempest.Core.Events` (for
`IPlatformNotification`'s own `IEvent` extension — a type-level
relationship only, no runtime call into `IEventBus`); `Tempest.Core.Logging`
(optional `ILogger`, the same convention every other platform service
follows).

**Consumers.** `NotificationSampleModule` (real contributor and
consumer, the eleventh production sample module) — subscribes to
`IPlatformNotification` during its own initialisation, registers a
command (`PublishSampleNotificationCommand`) that publishes one on
demand, and observes `NotificationSampleHostedService`'s own
`StartAsync`/`StopAsync` notifications end-to-end, proving "Background
notifications" concretely. Also a real, since-confirmed dependency of
three further sample-module command handlers built on top of
already-shipped platform services: `ReportingSampleModule` (`WP 6.0`),
`ExportImportSampleModule` (`WP 6.7`), and `LicensingSampleModule`
(`WP 6.6`) — the REST API itself (`Tempest.Core.Api`) does not consume
Notifications directly; only the commands it happens to expose do. A
future UI Shell remains a plausible future consumer not yet implemented.
*(This entry previously read "none yet implemented" — corrected `WP
6.7`, as a genuine, pre-existing drift found during that Work Package's
own repository review, unrelated to Export/Import's own scope: it had
gone stale since `WP 6.0` first shipped a real consumer.)*

**Lifecycle.** Ordinary DI-public, container-constructed singleton
(`INotificationDispatcher`), registered in `TempestHost`'s existing
Platform Services Registered block (Phase 6), immediately after
`IEventBus` — no new Host Lifecycle phase.

**A genuine, first-of-its-kind hosted service, disclosed rather than
overclaimed.** `NotificationSampleHostedService` is the codebase's
first real, non-infrastructure `IHostedService` — every prior Work
Package's own Background Services coverage (`WP 4.5`) proved the
infrastructure itself but shipped with zero real consumers (`AT-07`).
`AT-07`'s own revisit trigger names `WP 6.3` (REST API) as its intended
retiree; this Work Package does not claim that milestone — see its own
Platform Impact Assessment.

**A genuine implementation-phase finding, disclosed rather than
absorbed silently:** `INotificationDispatcher` dispatches by exact
static generic type, the same design `IEventBus` already uses — a
caller that publishes a notification typed as the concrete
`PlatformNotification` will never be observed by a subscriber that
subscribed against `IPlatformNotification`, since the two are different
dictionary keys. Found and fixed against this Work Package's own sample
consumers while writing their integration tests; documented directly on
`IPlatformNotification`'s own remarks as calling guidance. See
`ADR-0046`.

**ADR references.** ADR-0028 (Event Bus dispatch/failure model, the
design reused here); ADR-0046 (*Notifications Are Derived From Events,
Not a Replacement Pub/Sub — Dispatch Model, Severity/Category
Elaboration, and Logging Level*).

**Academy references.** `WP 6.2` retrospective (*Notification
Framework Implementation*); `docs/releases/v0.6.0/Release
Architecture.md` and companions; `Platform Service Contracts.md` and
companions; `docs/governance/Quality/Technical Debt Register.md`
(`AT-07`).

---

## Reporting *(implemented — WP 6.0, ADR-0040)*

**Responsibility.** Produces structured, formatted output from
platform or module data via a registered definition/renderer pair.
Registers report definitions and their renderers; dispatches a render
request by definition Id; enumerates registered definitions. Does not
persist generated output, does not schedule recurring generation, and
does not itself provide a delivery mechanism — a generated report
reaching a user is Notifications' or the REST API's own concern, not
Reporting's (`ADR-0040`).

**Key types.** `IReportDefinition`, `IReportRenderer<TDefinition>`,
`IReportingService`/`ReportingService`, `ReportRequest`, `ReportResult`,
`ReportingException` and two subtypes
(`DuplicateReportDefinitionException`, `ReportDefinitionNotFoundException`)
— all `Tempest.Core.Reporting`, implemented with zero signature
deviation from `Public Interface Catalogue.md`.
`IReportTemplate<TDefinition>`/`PlainTextReportTemplate<TDefinition>`
are additive elaborations this Work Package's own implementation phase
introduced — "Template abstraction" was named in this Work Package's
own brief but never drafted as an interface member; entirely optional,
`IReportingService` has no awareness of templates at all.

**Dependencies.** Dependency Injection only — confirmed directly, and
consistent with `Platform Service Implementation Order.md`'s own
observation that "Reporting has no hard proposed-service dependency."

**Consumers.** `ReportingSampleModule` (real contributor and consumer,
the twelfth production sample module) — registers
`SampleSummaryReportDefinition` and its own renderer, then registers a
command (`GenerateSampleReportCommand`) whose handler checks a
permission (Identity), generates the report (Reporting), records the
action (Audit), and publishes a completion notice (Notifications) — see
this Work Package's own Platform Integration Demonstration for the
complete, per-service account. Named as a plausible future consumer for
the REST API and any engineering module — none yet implemented.

**Lifecycle.** Ordinary DI-public, container-constructed singleton,
registered in `TempestHost`'s existing Platform Services Registered
block (Phase 6), immediately after the Event Bus and before
Notifications — matching `Service Registration Matrix.md`'s own
recommended order. No new Host Lifecycle phase.

**Security.** `GenerateAsync` does not itself check permissions — the
enforcement point is the caller, mirroring how Navigation and the
Command Framework themselves impose no authorization internally
(`ADR-0032`, `ADR-0037`). `ReportingSampleModule`'s own command handler
is that enforcement point, and its own published notification carries
only a fixed, non-identifying success message — never report content —
per Notifications' own Security Considerations for exactly this
scenario.

**A genuine implementation-phase decision, disclosed rather than
absorbed silently:** "Export abstraction" was named in this Work
Package's own brief but is explicitly **not** built — a dedicated
export interface inside `Tempest.Core.Reporting` would duplicate `WP
6.7` (Export/Import)'s own future scope and contradict this very ADR's
own orthogonality decision. `ReportResult`'s own `ContentType`/`Content`
shape is Reporting's own output mechanism, explicitly not guaranteed
round-trip-safe or re-importable. See `ADR-0040`.

**ADR references.** ADR-0038 (Command dispatch failure model, mirrored
by `GenerateAsync`'s own renderer-failure propagation); ADR-0040
(*Reporting Is DI-Public and Orthogonal to Export/Import — Template
Abstraction, Cross-Service Integration, and Scope Boundaries*).

**Academy references.** `WP 6.0` retrospective (*Reporting Framework
Implementation*); `docs/releases/v0.6.0/Release Architecture.md` and
companions; `Platform Service Contracts.md` and companions;
`docs/governance/Quality/Technical Debt Register.md` (`AT-09`).

---

## REST API *(implemented — WP 6.3, ADR-0047/ADR-0048/ADR-0049/ADR-0052)*

**Responsibility.** Lets an external HTTP client invoke platform
capability from outside the running process. Hosts an HTTP listener;
maps registered routes to Command Framework invocations; authorizes
each request via Identity & Permissions before dispatch; returns a
response reflecting the command's own `CommandResult`. Contains no
business logic of its own — every route is a thin translation layer to
an existing `ICommand`, per this Work Package's own Design Principles.

**Key types.** `IApiEndpointRegistry`/`ApiEndpointRegistry`,
`ApiRouteDescriptor`, `ApiException` and one subtype
(`DuplicateApiRouteException`) — all `Tempest.Core.Api`, implemented
with zero signature deviation from `Public Interface Catalogue.md`.
`ApiRequestHandler` (the thin, Kestrel-independent request pipeline),
`RestApiHostedService` (the Kestrel-backed hosted-service scaffold),
`ApiResponse`, and `OpenApiDocumentGenerator` are additive
implementation-phase types — the hosted-service scaffold itself was
deliberately left undrafted in the architecture package, "pending
`ADR-0049`'s ratification."

**Dependencies.** Dependency Injection; Identity & Permissions
(`IIdentityService`/`IPermissionEvaluator`, for per-request
authorization); Audit (`IAuditRecorder`, for the Logging Requirement's
own "the REST API should call `IAuditRecorder` explicitly"). Does
**not** depend on Settings, Notifications, or Reporting directly — those
three are consumed only at the sample-module calling layer
(`ApiSampleModule` exposing `ReportingSampleModule`'s own command),
exactly mirroring Reporting's own precedent of keeping cross-service
integration outside the core service itself.

**Consumers.** `ApiSampleModule` (real contributor and consumer, the
thirteenth production sample module) — maps one route
(`POST /api/v1/sample-report`) directly to
`ReportingSampleModule.GenerateSampleReportCommandId`, containing zero
business logic of its own whatsoever, the purest possible proof of this
Work Package's own "no business logic inside controllers/endpoints"
design principle. Also a real, since-confirmed second consumer:
`LicensingSampleModule` (`WP 6.6`) independently maps its own route
(`POST /api/v1/sample-capability`) to
`CheckSampleCapabilityCommandId` — confirmed by `WP 6.8`'s own
Consumption Matrix as the strongest available evidence that
`IApiEndpointRegistry`'s own "any module can map a route" design
genuinely generalises, not merely works once.

**Lifecycle.** `IApiEndpointRegistry` is an ordinary DI-public,
container-constructed Phase 6 singleton, registered immediately after
Audit; `RestApiHostedService` is discovered and orchestrated identically
to any other hosted service — started Phase 8.1, stopped Phase 10.1
(`ADR-0030`), isolated by default, not critical (`ADR-0021`) — no new
Host Lifecycle phase. Retires `AT-07` ("Zero real hosted services exist
beyond the infrastructure") — the Work Package that trade-off's own
revisit trigger explicitly named in advance.

**Hosting.** ASP.NET Core/Kestrel, adopted via a `FrameworkReference` to
the already-installed shared framework, confined entirely to
`RestApiHostedService` — this platform's own DI container, Command
Framework, and every other platform service remain entirely unchanged
and unreplaced (`ADR-0049`). Binds to the loopback address only by
default (`Api:Port` configuration key, default port `5080`); no TLS is
configured this release (`TD-14`).

**Security — a genuine, disclosed limitation, not a hidden one.** The
platform's first network-facing attack surface. Identity is carried in
an `X-Identity-Id` request header, trusted outright with no
cryptographic verification — a mechanical extension of this release's
own local-only identity model (`ADR-0043`) over HTTP, not a real
authentication mechanism (`TD-13`). `ApiRequestHandler` never
establishes the shared, ambient `ICurrentPrincipalAccessor` — a
per-request `IPrincipal` is resolved via the pure, non-mutating
`IIdentityService.GetPrincipal` and passed explicitly to
`IPermissionEvaluator.HasPermission`, safe for concurrent requests by
construction. This was empirically verified, not merely reasoned about:
an `AsyncLocal<T>`-backed `CurrentPrincipalAccessor` was built and
tested directly, and regressed 17 pre-existing tests — see `ADR-0052`.

**A genuine implementation-phase finding, disclosed rather than
absorbed silently:** because the REST pipeline never establishes the
ambient current principal, a command handler relying on
`IAuditRecorder`'s own ambient-attribution convention will record
`"unknown"` when invoked via REST — the real caller identity is instead
carried explicitly in the REST API's own `api.request` audit entry's
own `Detail[CallerIdentityId]` (`TD-15`), mirroring `WP 6.5`'s own
`Detail`-carried-attribute convention.

**ADR references.** ADR-0038 (Command dispatch failure model, reused
for renderer/handler-failure mapping); ADR-0044 (the enforcement point
this Work Package's own permission checks reuse); ADR-0045 (the
`Detail`-carried-attribute convention `TD-15`'s own resolution mirrors);
ADR-0047 (*The REST API Is a Background Hosted Service*); ADR-0048
(*REST Endpoints Dispatch Through the Existing Command Framework*);
ADR-0049 (*Adopting ASP.NET Core/Kestrel for the REST API*); ADR-0052
(*The REST API Resolves Identity Per-Request Without Touching the
Ambient Current Principal*).

**Academy references.** `WP 6.3` retrospective (*REST API
Implementation*); `docs/releases/v0.6.0/Release Architecture.md` and
companions; `Platform Service Contracts.md` and companions;
`docs/releases/v0.6.0/Risk Register.md` (`R1`, `R2`, `R3`);
`docs/governance/Quality/Technical Debt Register.md` (`AT-07`, retired;
`AT-10`; `TD-04`; `TD-13`; `TD-14`; `TD-15`).

---

## Export/Import *(implemented — WP 6.7, ADR-0051)*

**Responsibility.** The platform's own user-facing, `Stream`-based,
portable-artifact I/O layer — exports one or more `IExportable` sources
into a single artifact, and reads a previously exported artifact back
into its owning service(s), rejecting an incompatible schema version
outright rather than attempting a best-effort partial import.
Explicitly distinct from `IPersistenceStore`, which is internal,
platform-owned state never directly exposed to a user (`ADR-0051`).
Does not duplicate Reporting — a `ReportResult`'s own bytes are not
guaranteed round-trip-safe (`ADR-0040`), so Reporting output is never
wrapped as export data.

**Key types.** `IExportable`, `IExportService`/`ExportService`,
`IImportService`/`ImportService`, `ExportImportException` and one
approved subtype (`IncompatibleExportSchemaException`) — all
`Tempest.Core.ExportImport`, implemented with zero signature deviation
from `Public Interface Catalogue.md`. `IExportableKind`, `IImportable`,
`ExportSection`, `IExportFormat`/`JsonExportFormat`,
`IExportPayloadSerializer`/`JsonExportPayloadSerializer`, and two further
concrete exception subtypes (`CorruptedExportArtifactException`,
`DuplicateImportableKindException`) are additive elaborations this Work
Package's own implementation phase introduced — "Serialization
abstraction" and "Format abstraction" were named in this Work Package's
own brief but never drafted as interface members; entirely optional or
internal-only, `IExportable`/`IExportService`/`IImportService` remain
unaware of all of them.

**Dependencies.** Dependency Injection only — confirmed directly by
`using` inspection, and consistent with `Platform Service Implementation
Order.md`'s own observation that Export/Import has no hard
proposed-service dependency, only a practical one (a real `IExportable`
source worth integrating against).

**Consumers.** `ExportImportSampleModule` (real contributor and
consumer, the fourteenth production sample module) — registers two
Settings-backed `SettingExportImportAdapter` instances (each a single
class implementing `IExportable`, `IExportableKind`, and `IImportable`
together) and two commands (`ExportSampleDataCommand`,
`ImportSampleDataCommand`) whose handlers check a permission (Identity),
export or import (Export/Import), record the action (Audit), and
publish a completion notice (Notifications) — see this Work Package's
own Platform Integration Demonstration for the complete, per-service
account. Named as a plausible future consumer for Licensing and any
engineering module.

**Lifecycle.** `IExportService`/`ExportService` is an ordinary DI-public,
container-constructed singleton, registered in `TempestHost`'s existing
Platform Services Registered block (Phase 6), immediately after the
REST API's own `IApiEndpointRegistry`. `ImportService` is constructed
directly, once, and registered under both its own concrete type and
`IImportService` — the same already-built instance under two
service-type keys — mirroring `ADR-0044`'s own dual-registration
precedent for `CurrentPrincipalAccessor`: a module needing
`RegisterImportable` resolves the concrete type, while every ordinary
consumer resolves only the read-only `IImportService` interface. No new
Host Lifecycle phase.

**Security.** `ExportAsync`/`ImportAsync` do not themselves check
permissions — the enforcement point is the caller, mirroring how
Reporting and the REST API themselves impose no authorization
internally. `ExportImportSampleModule`'s own command handlers are that
enforcement point. An exported artifact may contain sensitive data (a
Settings export including a sensitive value) — `IExportable`
implementations are individually responsible for redacting or refusing
to export sensitive content; `IExportService`/`IImportService` impose no
content-level policy of their own, mirroring how Persistence imposes
none on what Settings/Audit choose to store.

**A genuine implementation-phase decision, disclosed rather than
absorbed silently:** `IImportService.ImportAsync`'s own approved,
single-method shape carries no destination parameter, yet must "read...
back into the owning service(s)" — plural — with no registration
mechanism drafted for it. Resolved by a concrete-type-only
`RegisterImportable` method (not part of `IImportService` itself),
routing each artifact section to its own registered `IImportable` by
`Kind`, validating every section's compatibility before importing any
of them. See `ADR-0051`.

**ADR references.** ADR-0044 (`CurrentPrincipalAccessor`'s own
dual-registration precedent, reused for `ImportService`); ADR-0051
(*Export/Import Is Orthogonal to the Internal Persistence Abstraction —
Kind Routing, Format/Serialization Abstractions, and Scope Boundaries*).

**Academy references.** `WP 6.7` retrospective (*Export/Import
Framework Implementation*); `docs/releases/v0.6.0/Release
Architecture.md` and companions; `Platform Service Contracts.md` and
companions.

---

## Licensing *(implemented — WP 6.6, ADR-0050)*

**Responsibility.** What capability is enabled, for whom, until when.
Validates a license at Host startup, before the DI container exists;
exposes the current license's own entitlements read-only thereafter.
Does not itself implement any licensed feature's own gating logic
beyond answering "is this capability enabled" — a consuming module
decides what to do with that answer. Does not implement commercial
policy, billing, or subscriptions — those remain outside the platform
entirely.

**Key types.** `ILicense`/`License`, `ILicenseValidator`/`LicenseValidator`,
`LicenseValidationResult`, `ILicenseProvider`/`LicenseProvider`,
`LicensingException` and one approved subtype
(`LicenseValidationException`) — all `Tempest.Core.Licensing`,
implemented with zero signature deviation from `Public Interface
Catalogue.md`. `LicenseDto` is an additive, internal-only JSON
deserialization shape, mirroring `PluginManifestDto`'s own precedent.

**Dependencies.** `System.Text.Json` (BCL) only — confirmed directly by
`using` inspection. `ILicenseValidator` has no constructor dependencies
at all, deliberately a leaf, mirroring `IPlatformVersionProvider`'s own
position — it cannot depend on anything container-constructed, since it
runs before the container exists.

**Consumers.** `LicensingSampleModule` (real contributor and consumer,
the fifteenth production sample module) — registers a sample setting
and a command (`CheckSampleCapabilityCommand`) whose handler checks a
permission (Identity), checks a sample capability
(`ILicenseProvider.HasCapability`), reads a Settings-provided message on
success, records the outcome (Audit), and publishes a completion notice
(Notifications) — then maps that same command to an HTTP route (REST
API), proven by a real HTTP round trip. See this Work Package's own
Platform Integration Demonstration for the complete, per-service
account. Named as a plausible future consumer for any commercially
licensed engineering module.

**Lifecycle.** `ILicenseValidator` is Composition-Root-constructed,
pre-container — `TempestHost` constructs it directly, immediately after
`ConfigurationBuilder.Build()` returns and before the logger/sink are
built, mirroring `PlatformVersionProvider`'s own construction-time
placement. `ILicenseProvider` is Composition-Root-constructed from the
already-validated `ILicense` and registered via `AddInstance` at Phase
6, immediately after Identity & Permissions — the only proposed
`v0.6.0` service with a non-container-registered contract. No new Host
Lifecycle phase — both placements resolve to phases that already exist.

**Failure behaviour — the one genuine architectural decision this Work
Package resolved, not merely implemented.** `Risk Register.md`'s own
`R5` named an open question: does every "invalid" category (missing,
expired, malformed) warrant Host-fatal treatment? Resolved: a missing
license file is a valid, unrestricted-but-uncapable default
(`LicenseValidator.UnlicensedLicenseeName`, zero enabled capabilities) —
this platform's own normal, open-source-friendly state, never
Host-fatal. A license file that exists but is unreadable, not valid
JSON, missing its own required `LicenseeName` field, or already
expired, aborts Host startup entirely — Host-fatal, per `ADR-0013`'s
existing classification, applied without modification. Proven directly:
every one of the 24 pre-existing test files that build a real
`TempestHost` continues to pass completely unmodified, since none of
them has ever supplied a license file. See `ADR-0050`.

**Security — a genuine, disclosed limitation, not a hidden one.** The
license file's own contents are trusted at face value — no
cryptographic signature or tamper-resistance verification of any kind
(`TD-16`), extending this release's own local-trust posture (`ADR-0043`)
to a second surface.

**ADR references.** ADR-0009 (Composition Root pattern, confirmed to
extend to a leaf validator and a wrapped provider); ADR-0013
(platform-service-failure classification, applied here without
modification); ADR-0023 (`PlatformVersionProvider`'s own "deliberately a
leaf" precedent, mirrored here); ADR-0044 (the fail-closed-by-default
precedent `HasCapability`'s own default state mirrors); ADR-0050
(*License Validation Is a Host-Startup, Host-Fatal Gate — Except a
Missing License File, Which Is a Valid, Unrestricted Default*).

**Academy references.** `WP 6.6` retrospective (*Licensing Framework
Implementation*); `docs/releases/v0.6.0/Release Architecture.md` and
companions; `Platform Service Contracts.md` and companions;
`docs/releases/v0.6.0/Risk Register.md` (`R5`);
`docs/governance/Quality/Technical Debt Register.md` (`TD-16`, `AT-13`).

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

## Requirements Engine *(implemented — WP 7.3A, ADR-0058–ADR-0061)*

**Responsibility.** The canonical, discipline-neutral representation of an
engineering requirement — identity, statement, category, lifecycle status,
revision history, relationships (grouping, collection membership,
allocation, traceability), and composed evidence — for every future
engineering discipline module to consume without inventing its own shape.

**Key types.** `IRequirementsService`/`RequirementsService`, `IRequirement`/
`Requirement`, `IRequirementCollection`/`RequirementCollection`,
`IRequirementGroup`/`RequirementGroup`, `IRequirementEvidence`/
`RequirementEvidence`, `RequirementStatus`, `RequirementStatusTransitions`,
`RequirementRelationshipKinds` (`Tempest.Core.Requirements`).

**Dependencies.** Dependency Injection; `IEngineeringDocumentStore`
(Engineering Data Model) — every requirement/collection/group is an
`IEngineeringDocument`, every relationship a `DocumentReference`; direct
`IPersistenceStore` access for its own identifier index, mirroring
`MaterialCatalog`'s own precedent; `IVerificationService` — `GetEvidenceAsync`
composes verification history with linked references, introducing no new
digital-thread traversal mechanism.

**Consumers.** `RequirementsSampleModule` (real contributor, also
demonstrating Identity/Audit/Reporting/Export-Import integration at the
calling layer); a plausible future consumer for any discipline-specific
engineering module (Mechanical, HVAC, Structural, Electrical).

**ADR references.** ADR-0058 (classification, storage, Engineering Data
Model relationship), ADR-0059 (identity/status/category representation),
ADR-0060 (concurrency and traceability integrity — `TD-25`), ADR-0061
(internal vs. calling-layer permission enforcement).

**Academy references.** `02 Runtime Architecture/16-requirements-engine.md`;
`03 Work Packages/WP7.3A-requirements-engine-implementation.md`.
