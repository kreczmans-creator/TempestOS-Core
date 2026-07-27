# Host Lifecycle

**Status: implemented — WP 2.7B (`Tempest.Core.Runtime`).** Every phase below
is implemented by `TempestHost.RunAsync` exactly as described here.

**Update, WP 4.2:** Phases 3.1 and 3.2 (Plugin Discovery, Plugin Loading)
are now implemented (`Tempest.Core.Plugins`), exactly as ADR-0026
specified. Decimal phase numbers mean "between 3 and 4" — no existing
phase was renumbered; see ADR-0026 for why.

**Update, WP 4.4D:** Phase 6 (Platform Services Registered) gained one new
registration — `IEventBus` as an ordinary container-constructed singleton
— alongside the `IPlatformVersionProvider` registration WP 4.2A already
added there. No new phase; see Phase 6, below, and *Event Bus
Architecture.md*/ADR-0028.

**Update, WP 4.5:** Phases 8.1 and 10.1 (Hosted Services Started, Hosted
Services Stopped) are now implemented (`Tempest.Core.BackgroundServices`),
exactly as ADR-0029/ADR-0030 specified. Phase 6 also gained one new
registration step: every discovered hosted service type, registered as an
ordinary self-referential singleton (`AddDiscoveredHostedServices`).
Decimal phase numbers mean "between 8 and 9" / "between 10 and 11" — no
existing phase was renumbered; see ADR-0030 for why.

## Purpose

This document defines every phase the Runtime Host passes through, from
construction to disposal, in the order they actually occur — reconciled with
the real dependency graph of the six existing platform services (see
ADR-0011 for where, and why, this reorders the WP 2.7 brief's illustrative
phase list). For each phase: its purpose, what must already be true for it to
begin (entry criteria), what must be true for it to be considered complete
(exit criteria), and what happens if it fails.

Every phase maps onto the Host's own state machine — see *Runtime State
Machine.md* — but a phase is a finer-grained unit than a state: several
phases (Configuration Built through Module Initialisation) all occur while the
Host is in the single `Starting` state.

## Phase Table

| # | Phase | Host State |
|---|---|---|
| 1 | Host Created | `Created` |
| 2 | Configuration Built | `Starting` |
| 3 | Logging Built | `Starting` |
| 3.1 | Plugin Discovery *(ADR-0026, implemented — WP 4.2)* | `Starting` |
| 3.2 | Plugin Loading *(ADR-0026, implemented — WP 4.2)* | `Starting` |
| 4 | Module Discovery | `Starting` |
| 5 | Module Registration | `Starting` |
| 6 | Platform Services Registered | `Starting` |
| 7 | Dependency Injection Built | `Starting` |
| 8 | Module Initialisation | `Starting` |
| 8.1 | Hosted Services Started *(ADR-0029/ADR-0030, implemented — WP 4.5)* | `Starting` |
| 9 | Runtime Running | `Running` |
| 10 | Shutdown Requested | `Running` → `Stopping` |
| 10.1 | Hosted Services Stopped *(ADR-0029/ADR-0030, implemented — WP 4.5)* | `Stopping` |
| 11 | Module Disposal | `Stopping` |
| 12 | Service Disposal | `Stopping` |
| 13 | Host Disposed | `Disposed` |

---

### 1. Host Created

**Purpose.** The Host object exists; nothing else does yet. This is the
starting point every run begins from.

**Entry criteria.** None — this is the first phase.

**Exit criteria.** The Host instance exists, holding no platform service
references yet.

**Failure behaviour.** None specific to this phase — construction of the Host
itself is not expected to be able to fail in any interesting way (it holds no
resources yet to fail acquiring).

---

### 2. Configuration Built

**Purpose.** Assemble every configured `IConfigurationSource` into a single,
immutable, validated `IConfigurationProvider` — the first real platform
service to come into existence.

**Entry criteria.** Host Created has completed. Whatever configuration sources
the Host is to use (see *The Startup Sequence*, Academy) are known.

**Exit criteria.** `ConfigurationBuilder.Build()` has returned a valid
`IConfigurationProvider` with no exception thrown.

**Failure behaviour.** A `ConfigurationException` (duplicate key, invalid
entry, or any other `ConfigurationBuilder.Build()` failure) is Host-fatal per
ADR-0013 — the Host transitions to `Faulted`. Nothing has been built yet to
tear down; disposal at this point is a no-op, though still attempted for
consistency (ADR-0004's WP 2.7 update).

---

### 3. Logging Built

**Purpose.** Construct `ConsoleLogSink`, `LoggerFactory` (reading
`Runtime:Logging:MinimumLevel` from the now-available configuration), and a
default `ILogger`.

**Entry criteria.** Configuration Built has completed; a valid
`IConfigurationProvider` exists.

**Exit criteria.** A working `ILogger` exists and can be used by every
subsequent phase for diagnostics.

**Failure behaviour.** A `ConfigurationException` from `LoggerFactory`
(an invalid `Runtime:Logging:MinimumLevel` value) is Host-fatal per ADR-0013 —
`Faulted`. Disposal attempts to release whatever Configuration Built acquired
(currently nothing disposable).

---

### 3.1. Plugin Discovery

**Status: implemented — WP 4.2 (`PluginManifestDiscoveryService`,
`Tempest.Core.Plugins`).**

**Purpose.** Read and validate every plugin manifest found in the plugins
directory, producing a deterministic, ordered list of eligible plugins.
Loads no assembly — a pre-Discovery artifact describing a plugin, not yet
touching it. See *Plugin Manifest Architecture.md*.

**Entry criteria.** Logging Built has completed — a working `ILogger`
exists. `PlatformVersionProvider` has been constructed (moved earlier than
its original WP 4.2A position, per ADR-0026) so `IPlatformVersionProvider.Version`
is available for the `MinimumPlatformVersion` compatibility check.
Configuration Built has completed, though this phase has no hard
dependency on it. Module Discovery, Registration, the DI container, and
every module do not exist yet, and none is needed.

**Exit criteria.** A deterministic (sorted ordinally by candidate folder
name), possibly empty, list of valid, version-compatible plugin manifests
exists. Every candidate that failed validation has been isolated per
ADR-0025, logged at its assigned severity, and excluded.

**Failure behaviour.** Fully governed by ADR-0025. Every plugin-scoped
failure (malformed manifest, duplicate identity, incompatible version) is
isolated — logged, that candidate excluded, this phase continues with the
rest. Only a genuine defect in this phase's own orchestration (not
attributable to any specific plugin) is Host-fatal — `Faulted`, exactly
the same transition Configuration Built and Logging Built already use.

---

### 3.2. Plugin Loading

**Status: implemented — WP 4.2 (`PluginAssemblyLoader`,
`Tempest.Core.Plugins`).**

**Purpose.** Load each eligible plugin's declared assembly file into the
process, in the same deterministic order Plugin Discovery established.

**Entry criteria.** Plugin Discovery has completed with its (possibly
empty) list of validated manifests in hand.

**Exit criteria.** Every eligible plugin's assembly has either been loaded
into the process (now visible to
`AppDomain.CurrentDomain.GetAssemblies()`, exactly like any other loaded
assembly) or isolated per ADR-0025 (missing assembly file, load failure,
or dependency load failure) and excluded. **This is the guarantee Module
Discovery, entirely unchanged, depends on** — see Phase 4, below.

**Failure behaviour.** Fully governed by ADR-0025, identical in shape to
Plugin Discovery's own: plugin-scoped failures are isolated; a genuine
defect in this phase's own orchestration is Host-fatal — `Faulted`.

---

### 4. Module Discovery

**Purpose.** Find every `IModule` implementation across loaded assemblies.
**Requires no code change for plugin support** — any assembly Plugin
Loading (Phase 3.2) loaded is already visible to this phase's own,
unchanged `AppDomain.CurrentDomain.GetAssemblies()` default, exactly as any
other loaded assembly already is. A run with zero plugins present behaves
identically to today, byte-for-byte.

**Entry criteria.** Logging Built has completed — Discovery takes an optional
`ILogger` for diagnostics. Per ADR-0011 and ADR-0008, Discovery requires
**no** DI container — none exists yet at this point, and none is needed.
Plugin Loading (Phase 3.2) has completed, whether or not any plugin was
actually present or eligible.

**Exit criteria.** `IFrameworkDiscoveryService.DiscoverModules()` has returned
an ordered list of `ModuleDescriptor` values with no exception thrown.

**Failure behaviour.** `ModuleDiscoveryException` (or its
`DuplicateModuleIdException` subtype) is Host-fatal per ADR-0013 — `Faulted`.
Disposal attempts to release Configuration and Logging (currently nothing
disposable in either).

---

### 5. Module Registration

**Purpose.** Register every discovered `ModuleDescriptor` with a
`RuntimeModuleManager`, building the authoritative runtime catalogue.

**Entry criteria.** Module Discovery has completed with a set of descriptors
in hand.

**Exit criteria.** Every descriptor has been registered with no exception
thrown; `IRuntimeModuleManager.GetAll()` reflects the full set.

**Failure behaviour.** `DuplicateModuleRegistrationException` (two discovered
types somehow producing the same ID) is Host-fatal per ADR-0013 — `Faulted`.
This should be effectively unreachable in practice, since Discovery already
rejects duplicate IDs itself (`DuplicateModuleIdException`) before Registration
ever sees them — but Registration's own guard remains the authoritative
protection and is not bypassed.

---

### 6. Platform Services Registered

**Purpose.** Populate a `ServiceCollection` with everything the DI container
needs before it is built: the `IConfigurationProvider` and logging instances
and the already-constructed `IPlatformVersionProvider` (via `AddInstance`,
per ADR-0009 — see *Platform Version.md*), `IEventBus` as an ordinary
container-constructed singleton (`services.Singleton<IEventBus, EventBus>()`,
added WP 4.4D — requiring no Composition Root treatment, per ADR-0028, since
its own constructor needs nothing `AddInstance` provides), every
discovered module's concrete type (via `AddDiscoveredModules`, keyed by the
`ModuleDescriptor` values Registration just produced), and every discovered
hosted service type as an ordinary, self-referential singleton
(`services.Singleton(type, type)` via `AddDiscoveredHostedServices`, the
same Type-based overload `AddDiscoveredModules` already uses — implemented,
WP 4.5, ADR-0029).

**Entry criteria.** Module Registration has completed; Configuration,
Logging, and Platform Version instances already exist (Platform Version's
own construction happens earlier, immediately after Logging Built, per
ADR-0026 — only its DI registration happens here).

**Exit criteria.** The `ServiceCollection` contains every registration the
running instance needs — this phase adds no new capability to `ServiceCollection`
itself; it is the Host's own act of calling `AddInstance`/`Singleton`/
`AddDiscoveredModules` in sequence.

**Failure behaviour.** An `ArgumentException` from a malformed registration
(for example, a type that doesn't satisfy the service type it's registered
against) is Host-fatal per ADR-0013 — `Faulted`. This is not expected to occur
in practice, since the Host constructs every registration itself from
already-validated data.

---

### 7. Dependency Injection Built

**Purpose.** Construct the `TempestServiceProvider` from the now-fully-populated
`ServiceCollection` — see ADR-0011 for why this happens *after*, not before,
Discovery and Registration.

**Entry criteria.** Platform Services Registered has completed.

**Exit criteria.** A `TempestServiceProvider` exists and can resolve every
registered service, including every discovered module's concrete type.

**Failure behaviour.** Construction itself (`TempestServiceProvider`'s
constructor) is not expected to throw under normal conditions — it only
copies descriptors and pre-seeds its singleton cache. A resolution failure
(`ServiceNotRegisteredException`, `CircularServiceDependencyException`,
`AmbiguousConstructorException`) occurring *later*, during Module
Initialisation, is described under that phase, not this one.

---

### 8. Module Initialisation

**Purpose.** Construct a `ModuleLifecycleManager` and drive every registered
module from `Registered` to `Running` — `InitialiseAllAsync` then
`StartAllAsync`.

**Entry criteria.** Dependency Injection Built has completed.

**Exit criteria.** `InitialiseAllAsync` and `StartAllAsync` have both returned.
This does **not** require every module to have succeeded — per ADR-0013, this
phase completes even if one or more modules ended up `Failed`, exactly as
`ModuleLifecycleManager`'s existing per-module isolation already guarantees.

**Failure behaviour.** An **individual module's** failure (caught internally
by `ModuleLifecycleManager`, marked `Failed`, logged) does **not** fail this
phase and does **not** fault the Host — see ADR-0013. A failure in
`ModuleLifecycleManager`'s own resolution path (a `ServiceResolutionException`
escaping because `ModuleLifecycleManager` re-throws after marking a module
`Failed`) is likewise a per-module failure, not a Host fault, *unless* it
represents a genuine defect in the Host's own construction of
`ModuleLifecycleManager` itself (for example, passing a service provider the
Host itself failed to build correctly) — such a defect is a Host-level bug,
not a module failure, and is Host-fatal.

---

### 8.1. Hosted Services Started

**Status: implemented — WP 4.5 (`Tempest.Core.BackgroundServices`).**

**Purpose.** Construct `IHostedServiceManager` from the hosted service
types discovered and registered during Platform Services Registered
(Phase 6), and start every one, in deterministic order — after every
module has been given the chance to initialise and start, never
interleaved with them.

**Entry criteria.** Module Initialisation has completed, regardless of
individual module outcomes (ADR-0013) — mirroring Plugin Loading's own
"completed, whether or not anything was actually eligible" entry
criterion.

**Exit criteria.** `IHostedServiceManager.StartAllAsync` has returned. Does
**not** require every hosted service to have started successfully — an
isolated (non-critical) service's failure does not prevent this phase from
completing, exactly as an individual module's failure does not prevent
Module Initialisation from completing.

**Failure behaviour.** Fully governed by ADR-0021/ADR-0029. An isolated
service's `StartAsync` failure: logged, that service marked `Failed`,
phase continues. A **critical** service's (`ICriticalBackgroundService`)
failure: Host-fatal — `Starting → Faulted`, the identical transition every
other startup phase already uses.

---

### 9. Runtime Running

**Purpose.** The steady state: the platform is up, every platform service
exists, every module has been given the chance to start, every hosted
service has been given the chance to start, and the Host waits for a
shutdown request.

**Entry criteria.** Module Initialisation and Hosted Services Started have
both completed.

**Exit criteria.** A shutdown request or a runtime exception is observed.

**Failure behaviour.** No code path produced by `WP 4.5` can fault the Host
*during* Running — a hosted service's own failure is fully resolved at
Phase 8.1 (isolated) or 10.1 (isolated, or Host-fatal if critical); `WP 4.5`
implements no ongoing supervision, monitoring, or restart policy for a
hosted service once it reaches `Running` (deliberately out of scope — see
ADR-0029/ADR-0030's own stated exclusions). This phase's failure behaviour
remains defined regardless, so any future work package that *does*
introduce ongoing supervision has an established policy to follow rather
than needing to invent one: an unhandled exception during Running is
Host-fatal (`Running → Faulted`), on the same reasoning as any other
platform-level failure under ADR-0013.

---

### 10. Shutdown Requested

**Purpose.** The moment a stop signal is observed and the Host begins its
controlled teardown. Two distinct triggers lead here: a shutdown request
(ADR-0014's running-time signal) while `Running`, or **either** signal —
startup cancellation or an early shutdown request — while still `Starting`
(ADR-0018). Both are handled identically from this point on; there is no
separate "partial startup" variant of this phase.

**Entry criteria.** The Host is `Running`, **or** the Host is `Starting` and
either the startup cancellation token or a shutdown request has fired
(ADR-0018).

**Exit criteria.** The Host has transitioned to `Stopping` and begun
Hosted Services Stopped, then Module Disposal.

**Failure behaviour.** Not applicable — receiving a shutdown request, or
being cancelled during startup, is not itself a failure mode; see *Shutdown
Sequence.md*. This is explicitly distinct from a platform-service failure
during `Starting`, which goes directly to `Faulted` (ADR-0013), not through
this phase.

---

### 10.1. Hosted Services Stopped

**Status: implemented — WP 4.5 (`Tempest.Core.BackgroundServices`).**

**Purpose.** Stop every started hosted service, in the reverse of Phase
8.1's own order, before any module is stopped.

**Entry criteria.** Shutdown Requested has occurred. **If
`IHostedServiceManager` was never constructed** (the Host faulted, or was
cancelled, before Phase 8.1 ever ran), this phase is a no-op — mirroring
exactly how Module Disposal already tolerates `ModuleLifecycleManager`
never having been constructed.

**Exit criteria.** `IHostedServiceManager.StopAllAsync` has returned. Does
**not** require every hosted service to have stopped cleanly — an
individual service's isolated stop failure does not prevent this phase
from completing.

**Failure behaviour.** Fully governed by ADR-0021/ADR-0029. An isolated
service's `StopAsync` failure: logged, phase continues — mirroring Module
Disposal's own already-established policy for individual module stop
failures. A **critical** service's failure: Host-fatal —
`Stopping → Faulted`, the identical transition already defined for a
genuine Host-level defect during shutdown orchestration. Cleanup
guarantees are unaffected: `Faulted → Disposed` remains always legal and
disposal of every module, and every hosted service already stopped, is
still attempted afterward (ADR-0004, ADR-0019).

---

### 11. Module Disposal

**Purpose.** Stop and dispose every module, in reverse order, via
`StopAllAsync` then `DisposeAllAsync`.

**Entry criteria.** Shutdown Requested has occurred — whether triggered by a
graceful shutdown request from `Running`, or by cancellation/an early
shutdown request during `Starting` (ADR-0018; both now reach this phase via
the same `Stopping` state) — or the Host is tearing down after a genuine
startup fault (a separate path — see *Runtime State Machine.md*'s
`Faulted → Disposed`).

**Exit criteria.** `StopAllAsync` and `DisposeAllAsync` have both returned.
Per WP 2.3's existing, unmodified design, this does not require every module
to stop/dispose cleanly — individual failures are isolated exactly as they
already are for Initialise/Start.

**Failure behaviour.** Individual module Stop/Dispose failures are already
isolated by `ModuleLifecycleManager` (WP 2.3) — no new Host-level handling is
needed or introduced. A defect in the Host's own orchestration of this phase
(not a module failure) is treated as any other unexpected Host-level
exception — see *Failure Behaviour.md*.

---

### 12. Service Disposal

**Purpose.** Release whatever platform services (Configuration, Logging, the
DI container itself) hold resources needing release.

**Entry criteria.** Module Disposal has completed.

**Exit criteria.** Every disposable platform service has had disposal
attempted.

**Failure behaviour.** Per ADR-0004's WP 2.7 update, disposal is attempted
regardless of prior faults, and one service's disposal failure should not
prevent another's. **Today, none of Configuration, Logging, or the DI
container implement `IDisposable`/`IAsyncDisposable` at all** — this phase is
currently a no-op in practice, defined now so the ordering and policy exist
before there is anything to actually dispose. See the Architectural Debt
Assessment in the WP 2.7 Academy review.

---

### 13. Host Disposed

**Purpose.** The Host itself is now fully torn down; this is the terminal
state of a single run.

**Entry criteria.** Service Disposal has completed (or was attempted after an
earlier fault).

**Exit criteria.** None — this is terminal. See *Runtime State Machine.md*:
`Disposed` has no outgoing transitions.

**Failure behaviour.** Not applicable — this phase represents the end of the
Host's ability to fail in any way that matters, since nothing further is
expected to run.
