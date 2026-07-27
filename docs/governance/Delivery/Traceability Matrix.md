# Traceability Matrix

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Traceability Matrix |
| **Purpose** | End-to-end traceability for every major TempestOS capability — Requirement → Work Package → ADR → Architecture → Implementation → Tests → Academy → Release — in one place, so no capability's own history requires reconstructing from six separate documents. |
| **Scope** | Every capability listed in `Feature Register.md` as Implemented. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Every register in this governance suite; this matrix is the synthesis, not a new source of raw fact. |
| **Review Frequency** | Updated whenever a new major capability completes its own Work Package. |
| **Last Reviewed** | 2026-07-27 (WP 5.0C, Shell & Composition Framework Architecture). |
| **Related Documents** | Every register in `docs/governance/`. |
| **Related ADRs** | All 35 (see per-row detail). |
| **Related Academy Articles** | See `Academy Register.md`. |
| **Coverage Status** | Complete for every Implemented capability, including Navigation as of `WP 5.0B`. Partial for the Shell (chain begun — Requirement through Architecture and Academy are complete; Implementation, Tests, and Release are pending `WP 5.0D`). Not Yet Applicable for planned-but-unstarted capabilities (Command Framework dispatcher, Diagnostics Improvements, Developer Experience) — see Reason/Review Trigger below. |

---

## Reason (Not Yet Applicable Capabilities)

The Command Framework dispatcher, Diagnostics Improvements, and
Developer Experience Improvements have no Work Package started yet
(`WP 5.1` onward) — there is no Requirement→...→Release chain to trace
because no chain has begun. **Review Trigger.** The Work Package that
begins each one. Navigation is no longer in this category — its own
chain is now fully traced; see the Matrix's own Navigation row, below.
The Shell is also no longer in this category — its own chain began with
`WP 5.0C`, though Implementation/Tests/Release remain pending `WP 5.0D`
(see the Shell's own row, below, and the Coverage Status field above).

## Matrix

| Capability | Requirement | Work Package | ADR | Architecture | Implementation | Tests | Academy | Release |
|---|---|---|---|---|---|---|---|---|
| Module Discovery | Find every `IModule` without a hand-written list | WP 2.1 | ADR-0003, ADR-0008 | `Platform Service Map.md` (Discovery) | `ReflectionFrameworkDiscoveryService` | `Modules/` test suite | WP2.1 retrospective; Case Study 04 | v0.3.0 |
| Runtime Registration | An authoritative runtime catalogue of modules | WP 2.2 | ADR-0001 | `Platform Service Map.md` (Registration) | `RuntimeModuleManager` | `Modules/` test suite | WP2.2 retrospective; Case Study 01 | v0.3.0 |
| Runtime Lifecycle | Orchestrate init/start/stop/dispose with per-module isolation | WP 2.3 | ADR-0002, ADR-0004 | `Platform Service Map.md` (Lifecycle) | `ModuleLifecycleManager` | `Modules/` test suite | WP2.3 retrospective; Case Study 02, 03 | v0.3.0 |
| Dependency Injection | Constructor injection, singleton/transient lifetimes | WP 2.4 | ADR-0005, ADR-0006, ADR-0007 | `Platform Service Map.md` (DI) | `TempestServiceProvider`, `ServiceCollection` | `DependencyInjection/` test suite | WP2.4 retrospective; Design Pattern 03 | v0.3.0 |
| Configuration Framework | Read-only, immutable, case-insensitive configuration | WP 2.5 | ADR-0009 | `Platform Service Map.md` (Configuration) | `ConfigurationBuilder`, `ConfigurationProvider` | `Configuration/` test suite | WP2.5 retrospective; Case Study 05 | v0.3.0 |
| Logging & Diagnostics | `ILogger` abstraction, sink isolation | WP 2.6 | ADR-0010 | `Platform Service Map.md` (Logging) | `Logger`, `LoggerFactory`, `ConsoleLogSink` | `Logging/` test suite | WP2.6 retrospective | v0.3.0 |
| Runtime Host & Composition Root | Orchestrate startup/shutdown of all platform services | WP 2.7, WP 2.7B | ADR-0011–ADR-0019 | `Runtime Host Architecture.md` + 5 companion docs | `TempestHost`, `TempestHostBuilder` | `Runtime/` test suite | WP2.7, WP2.7B retrospectives; *The Startup Sequence*, *Working with the TempestOS Host* | v0.3.0 |
| Platform Contracts | Settled contract surface before implementation | WP 4.0 | ADR-0024 | `Engineering Glossary.md` (Platform API) | `IHostedService`, `ICriticalBackgroundService`, `ICommand`, `IEvent`, `IEventHandler<T>` | `Commands/`, `Events/` (contract tests) | WP4.0 retrospective | v0.4.0 (Released) |
| Module SDK | Reduce module-authoring boilerplate | WP 4.1 | — (RD-0003–RD-0007) | `Platform Service Map.md` (Module SDK) | `ModuleBase`, `ModuleLifecycleBase` | `Modules/` test suite | WP4.1 retrospective; *Building a Module* | v0.4.0 (Released) |
| Plugin Manifest | Load modules from disk without code changes to Discovery | WP 4.2, 4.2A, 4.2B, 4.2C | ADR-0025, ADR-0026 | `Plugin Manifest Architecture.md`, `Host Lifecycle.md` (3.1/3.2) | `PluginManifestDiscoveryService`, `PluginAssemblyLoader` | `Plugins/` test suite | WP4.2 family retrospectives; *Plugin Architecture* | v0.4.0 (Released) |
| Sample Module | A living reference module every later WP extends | WP 4.3 | — (RD-0015) | `Sample Module Architecture.md` | `ClockModule` | `Samples/` test suite | WP4.3 retrospectives | v0.4.0 (Released) |
| Dependency Injection for Discovered Modules | Let a discovered module accept constructor-injected dependencies | WP 4.4A, WP 4.4B | ADR-0027 | `Module Dependency Injection Architecture.md` | `ModuleMetadataAttribute` | `Modules/` test suite | WP4.4A, WP4.4B retrospectives | v0.4.0 (Released) |
| Event Bus | Publish/subscribe without direct module-to-module coupling | WP 4.4, WP 4.4D, WP 4.4E | ADR-0020, ADR-0028 | `Event Bus Architecture.md` | `IEventBus`, `EventBus`, `ClockModuleLifecycleEvent` | `Events/`, `Samples/` test suites | WP4.4, WP4.4D, WP4.4E retrospectives; *Building an Event-Driven Module* | v0.4.0 (Released) |
| Background Services | Host-orchestrated background work, isolated/critical failure model | WP 4.5 (×2) | ADR-0021, ADR-0029, ADR-0030 | `Background Services Architecture.md`, `Host Lifecycle.md` (8.1/10.1) | `HostedServiceDiscoveryService`, `HostedServiceManager` | `BackgroundServices/`, `Runtime/` test suites | WP4.5 (×2) retrospectives; *Reflection-Based Discovery* (expanded), *Failure Isolation Across TempestOS* (Case 2) | v0.4.0 (Released) |
| Navigation | Coherent navigation between built-in pages, modules, and plugins, without touching the Runtime Host | WP 5.0A, WP 5.0B | ADR-0022, ADR-0031, ADR-0032 | `Navigation Framework Architecture.md` | `NavigationItem`, `INavigationProvider`/`NavigationService`, `NavigationRequestedEvent`, `NavigationSampleModule` and companions | `Navigation/`, `Samples/` test suites | WP5.0A, WP5.0B retrospectives; *Navigation Architecture* | v0.5.0 (in progress) |
| Shell & Composition Framework | Let `Tempest.App` consume the platform via its own composition root, presenting Navigation and the Event Bus to a user | WP 5.0C | ADR-0033, ADR-0034, ADR-0035 | `Shell & Composition Framework Architecture.md` | *Pending — WP 5.0D* (`ITempestHost.Services`, the Shell itself, designed, not yet coded) | *Pending — WP 5.0D* | WP5.0C retrospective; *Shell & Application Composition* | v0.5.0 (in progress) |

**Total: 14 fully-traced (Implemented) capabilities, plus 1
partially-traced capability (Shell & Composition Framework — chain begun,
Implementation/Tests/Release pending).**

## Traceability Gaps Found

**None** for any capability marked Implemented in `Feature Register.md`.
Every capability above has at least one entry in every column — no
Implemented capability lacks a test, an Academy retrospective, or a
release association. The Shell's own Implementation/Tests/Release cells
are explicitly marked "Pending — WP 5.0D," not left blank — a disclosed,
expected gap for a capability whose architecture phase just completed,
not a silently missing link. The three remaining Not-Yet-Applicable
capabilities (see Reason, above) correctly have no chain, because no
chain has begun.

## Cross-Reference Check

Every cell in this matrix is a pointer to a register or document already
verified independently elsewhere in this governance suite (`ADR
Register.md`, `Test Register.md`, `Academy Register.md`, `Release
Register.md`) — this matrix introduces no new raw fact of its own, only
the synthesis connecting them.
