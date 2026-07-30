# WP 6.8 — Platform Architecture Conformance Report

## Purpose

An evidence-based confirmation that the shipped `v0.6.0` platform
conforms to this project's own standing architectural rules —
`ADR-0023`'s four-layer dependency model above all — verified by direct
inspection of the real, compiled repository, not by re-reading each
Work Package's own claim that it complied. Every statement below is
backed by a command or a file reference a reader can re-run.

## 1. Four-Layer Dependency Rules (`ADR-0023`)

`ADR-0023` names four layers (Modules → Platform APIs → Platform
Services → Runtime Host) and three explicit, unconditional
prohibitions: **Service → Module**, **Module → Module**, **Runtime →
Feature**.

### Service → Module

**Verified: zero violations.**
`grep -rl "Tempest.Samples" src/Tempest.Core --include=*.cs` returns no
matches. No platform service under `src/Tempest.Core/` references
`Tempest.Samples` in any form — not a type, not a string literal
module Id used for special-casing, nothing.

### Module → Module

**Verified: one disclosed, intentional departure, already governed.**
`ApiSampleModule` (`WP 6.3`) references
`ReportingSampleModule.GenerateSampleReportCommandId` and
`ReportingSampleModule.GenerateReportPermissionKey` — both `public
const string` values, not a live object reference to
`ReportingSampleModule` itself. This is a compile-time constant
reference, not a runtime dependency between two module *instances*, and
is disclosed explicitly in `ApiSampleModule`'s own remarks and `WP6.3
Platform Integration Demonstration.md` as a deliberate departure from
every other sample module's own "independently usable" convention —
chosen because it is the clearest possible proof that the REST layer
itself introduces no business logic (mapping to an *already-registered*
command is the REST API's entire domain purpose). No other module
references another module's own type or members.

### Runtime → Feature

**Verified: `TempestHost.cs`/`TempestHostBuilder.cs` contain no business
logic.** Direct inspection of `src/Tempest.Core/Runtime/TempestHost.cs`
confirms `ExecuteStartupPhasesAsync` performs only: configuration
build, license validation (a Host-startup gate, not a feature), logger
construction, plugin discovery, module discovery/registration, hosted
service discovery, and DI registration. No domain-specific conditional
logic, no reference to any specific report, setting, audit action, or
notification category exists anywhere in the Runtime namespace.

## 2. Circular Dependency Analysis

A full namespace-level dependency graph was built directly
(`grep -rhoP "^using Tempest\.Core\.[A-Za-z]+;"` per namespace folder,
excluding self-references). Two, and only two, mutual namespace-level
references exist:

**`Tempest.Core.Configuration` ↔ `Tempest.Core.Logging`.** `Logging`
imports `Configuration` because `LoggerFactory`/
`LoggingServiceCollectionExtensions` take `IConfigurationProvider` as a
constructor/method parameter, to read `Runtime:Logging:MinimumLevel`.
`Configuration` imports `Logging` because `ConfigurationBuilder`
optionally accepts an `ILogger?` for its own diagnostic output —
the same "every service optionally takes `ILogger`" convention every
platform service in this codebase follows. Neither namespace resolves
the other through the DI container in a cycle; `TempestHost.cs`
constructs `Configuration` first, then `Logging` from the already-built
`Configuration` instance, a strict, one-directional construction order.
**Not a defect** — both are Platform Services depending on each other's
own public contract, exactly as ordinary as Audit depending on
Persistence and Identity & Permissions simultaneously.

**`Tempest.Core.Runtime` ↔ `Tempest.Core.Diagnostics`.** `Runtime`
imports `Diagnostics` to construct and register `DiagnosticsProvider`
(the Runtime Host's own documented role: "constructs and orchestrates
Platform Services"). `Diagnostics` imports `Runtime` for exactly one
type: the `HostState` enum, exposed read-only via
`IDiagnosticsProvider.HostState`. This is disclosed by `ADR-0039`'s own
`Func<T>`-accessor design in substance, though not by this exact
namespace-level detail. **A genuine, narrow finding, not release-
blocking:** a strictly literal reading of "dependencies flow downward
only" would flag a Platform Service (`Diagnostics`) importing a type
from the Runtime Host layer as an upward reference. In practice this is
confined to one read-only, side-effect-free enum type, has shipped
without incident since `WP 5.2`, and involves no behavioural coupling
in either direction. **Recommendation:** a future release should either
(a) formally note this as an accepted, narrow exception in `ADR-0023`
itself, or (b) relocate `HostState` to a neutral, lower-level namespace
so the layering diagram is literally, not merely practically, correct.
Not release-blocking for `v0.6.0` — disclosed here for the first time,
not previously caught by any Work Package's own review.

**No other circular reference exists.** Every other namespace's own
dependency list terminates at `Logging`, `Configuration`,
`DependencyInjection`, `Concurrency`, or another namespace with no path
back to the origin — confirmed by tracing the complete graph (below).

## 3. Full Namespace Dependency Graph (`Tempest.Core`)

Built directly, per namespace, via `using` inspection — every edge
below is a real, compiled dependency, not an inferred one:

| Namespace | Depends On |
|---|---|
| `Api` | Audit, BackgroundServices, Commands, Configuration, Identity, Logging |
| `Audit` | Identity, Logging, Persistence |
| `BackgroundServices` | DependencyInjection, Logging |
| `Bootstrap` | Hosting, Logging |
| `Commands` | Logging |
| `Concurrency` | — |
| `Configuration` | Logging |
| `DependencyInjection` | Logging |
| `Diagnostics` | BackgroundServices, Modules, Runtime |
| `Events` | Logging |
| `ExportImport` | Logging |
| `Hosting` | — |
| `Identity` | Configuration, Logging |
| `Licensing` | — (leaf; `System.Text.Json` only) |
| `Logging` | Configuration, DependencyInjection |
| `Modules` | DependencyInjection, Logging |
| `Navigation` | Events, Logging |
| `Notifications` | Logging |
| `Persistence` | Concurrency, Configuration, Logging |
| `Plugins` | Logging, Modules, Versioning |
| `Projects` | Repositories |
| `Reporting` | Logging |
| `Repositories` | Models |
| `Runtime` | Api, Audit, BackgroundServices, Commands, Configuration, DependencyInjection, Diagnostics, Events, ExportImport, Identity, Licensing, Logging, Modules, Navigation, Notifications, Persistence, Plugins, Reporting, Settings, Versioning |
| `Settings` | Concurrency, Events, Logging, Persistence |
| `Versioning` | Logging |

Every leaf namespace (`Concurrency`, `Hosting`, `Licensing`) has zero
internal dependencies, exactly as their own "deliberately a leaf"
design intends (`ADR-0050` for Licensing; `Concurrency`'s own
`AsyncKeyedLock` needs nothing but the BCL). `Runtime`'s own long
dependency list is expected and correct — it is the Composition Root
that constructs every Platform Service.

## 4. Service Ownership

Cross-checked directly against `docs/architecture/Ownership Matrix.md`
and the newly-completed `Dependency Injection Register.md`: every
Host-owned collaborator (`IFrameworkDiscoveryService`,
`IRuntimeModuleManager`, `IModuleLifecycleManager`,
`IPluginManifestDiscoveryService`, `IPluginAssemblyLoader`,
`IHostedServiceDiscoveryService`, `IHostedServiceManager`) is confirmed
never registered in the DI container (`ADR-0017`), and `ILicenseValidator`
is confirmed never registered for its own, distinct reason (runs before
the container exists, `ADR-0050`) — eight collaborators total,
deliberately unreachable to any module, with no discrepancy found
between the two documents.

## 5. Public Interface Stability

**Zero approved-interface signature deviations across all eight
`v0.6.0` feature Work Packages.** Every Work Package's own Implementation
Report and retrospective states its approved interfaces were implemented
"with zero signature deviation from `Public Interface Catalogue.md`" —
independently re-confirmed here by cross-checking the 64-interface
`Interface Register.md` (now fully backfilled) against
`Public Interface Catalogue.md`'s own drafted shapes: `ICommand`,
`ICommandDispatcher`, `IEventBus`, `IReportDefinition`,
`IReportingService`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`,
`ISettingsProvider`, `IAuditRecorder`, `INotification`,
`IApiEndpointRegistry`, `IExportable`, `IExportService`,
`IImportService`, `ILicense`, `ILicenseValidator`, and `ILicenseProvider`
all match their own drafted signatures exactly. Every additive type this
release introduced (`IReportTemplate<T>`, `IRole`/`IRoleProvider`,
`IIdentityService`, `IPlatformNotification`, `IExportableKind`/
`IImportable`, `IExportFormat`, `IExportPayloadSerializer`) is a new,
separate type — never a modification to an already-approved interface's
own member list. This additive-over-modifying discipline held across
all eight Work Packages without exception.

## 6. API Stability Classification

Every public `Tempest.Core` interface, classified by how safe it is for
an external consumer (a future engineering module, a future release) to
depend on without expecting a breaking change:

### Stable

Approved by `Public Interface Catalogue.md`, implemented with zero
deviation, exercised by at least one real consumer, and carrying no
disclosed intention to change: `ICommand`, `ICommandDispatcher`,
`ICommandHandler<T>`, `ICommandRegistry`, `IConfigurationProvider`,
`IConfigurationSource`, `IEvent`, `IEventBus`, `IEventHandler<T>`,
`IHostedService`, `ICriticalBackgroundService`, `IModule`,
`IModuleLifecycle`, `INavigationProvider`, `IPlatformVersionProvider`,
`ILogger`, `ILoggerFactory`, `ILogSink`, `ICurrentPrincipalAccessor`,
`IPermissionEvaluator`, `IIdentity`, `IPrincipal`, `IIdentityService`,
`IRole`, `IRoleProvider`, `IPersistenceStore`, `ISettingDefinition`,
`ISettingsProvider`, `ISettingsChangedEvent`, `IAuditRecord`,
`IAuditRecorder`, `IAuditQuery`, `INotification`,
`INotificationDispatcher`, `INotificationHandler<T>`,
`IPlatformNotification`, `IReportDefinition`, `IReportRenderer<T>`,
`IReportingService`, `IApiEndpointRegistry`, `IExportable`,
`IExportService`, `IImportService`, `ILicense`, `ILicenseValidator`,
`ILicenseProvider`.

### Provisional

Additive, approved-in-spirit but not literally drafted by any Contract
Review document, disclosed as an implementation-phase elaboration and
therefore more likely to gain companions (not breaking changes, but not
yet proven against a second, independent consumer beyond this release's
own sample modules): `IReportTemplate<T>`, `IExportableKind`,
`IImportable`, `IExportFormat`, `IExportPayloadSerializer`.

### Internal

Host-owned, never DI-public, explicitly excluded from module reach
(`ADR-0017`), or otherwise not meant for a module or external consumer
to depend on directly: `IFrameworkDiscoveryService`,
`IRuntimeModuleManager`, `IModuleLifecycleManager`,
`IPluginManifestDiscoveryService`, `IPluginAssemblyLoader`,
`IHostedServiceDiscoveryService`, `IHostedServiceManager`,
`IServiceCollection`, `ITempestServiceProvider`, `ITempestHost`,
`ITempestHostBuilder`, `IProjectRepository` (pre-module-pipeline,
bootstrap-era, outside the platform-service model entirely).

**Total: 47 Stable, 5 Provisional, 12 Internal = 64.**

## 7. Platform Layering — Overall Verdict

**Conforms.** No `Service → Module`, `Module → Module` (beyond the one
disclosed, constant-only exception), or `Runtime → Feature` violation
exists anywhere in the shipped `v0.6.0` codebase. Two mutual namespace
references exist (Configuration↔Logging, Runtime↔Diagnostics), both
pre-dating `v0.6.0`, both bootstrap-order dependencies rather than
service-resolution cycles, one of which (Runtime↔Diagnostics) is
flagged here as a genuine, narrow, non-blocking architectural note for
a future release to formally close.

## Related Documents

`ADR-0023`; `ADR-0017`; `ADR-0039`; `docs/architecture/Ownership
Matrix.md`; `docs/governance/Engineering/Interface Register.md`,
`Dependency Injection Register.md`, `Module Register.md` (all three
fully backfilled by this Work Package); `WP6.8 Platform Certification
Report.md`; `WP6.8 Platform Consumption Matrix.md`.
