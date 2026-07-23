# Host Lifecycle

**Status: implemented — WP 2.7B (`Tempest.Core.Runtime`).** Every phase below
is implemented by `TempestHost.RunAsync` exactly as described here.

**Update, WP 4.2C:** Phases 3.1 and 3.2 (Plugin Discovery, Plugin Loading)
are architected — ADR-0026 — but not yet implemented; they will land with
Plugin Manifest (`WP 4.2`). Decimal phase numbers mean "between 3 and 4" —
no existing phase was renumbered; see ADR-0026 for why.

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
| 3.1 | Plugin Discovery *(architected, ADR-0026; not yet implemented)* | `Starting` |
| 3.2 | Plugin Loading *(architected, ADR-0026; not yet implemented)* | `Starting` |
| 4 | Module Discovery | `Starting` |
| 5 | Module Registration | `Starting` |
| 6 | Platform Services Registered | `Starting` |
| 7 | Dependency Injection Built | `Starting` |
| 8 | Module Initialisation | `Starting` |
| 9 | Runtime Running | `Running` |
| 10 | Shutdown Requested | `Running` → `Stopping` |
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

**Status: architected — ADR-0026. Not yet implemented; lands with Plugin
Manifest (WP 4.2).**

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

**Status: architected — ADR-0026. Not yet implemented; lands with Plugin
Manifest (WP 4.2).**

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
(via `AddInstance`), and every discovered module's concrete type (via
`AddDiscoveredModules`, keyed by the `ModuleDescriptor` values Registration
just produced).

**Entry criteria.** Module Registration has completed; Configuration and
Logging instances already exist.

**Exit criteria.** The `ServiceCollection` contains every registration the
running instance needs — this phase adds no new capability to `ServiceCollection`
itself; it is the Host's own act of calling `AddInstance`/`AddDiscoveredModules`
in sequence.

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

### 9. Runtime Running

**Purpose.** The steady state: the platform is up, every platform service
exists, every module has been given the chance to start, and the Host waits
for a shutdown request (or, in the future, hosts background work — see
*Runtime Host Architecture.md*'s Future Extensibility section).

**Entry criteria.** Module Initialisation has completed.

**Exit criteria.** A shutdown request or a runtime exception is observed.

**Failure behaviour.** Today, with no hosted services or background work yet
implemented, there is no code path that can fault the Host *during* Running —
this phase's failure behaviour is defined now so that a future hosted-service
implementation has an established policy to follow rather than needing to
invent one: an unhandled exception during Running is Host-fatal
(`Running → Faulted`), on the same reasoning as any other platform-level
failure under ADR-0013.

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

**Exit criteria.** The Host has transitioned to `Stopping` and begun Module
Disposal.

**Failure behaviour.** Not applicable — receiving a shutdown request, or
being cancelled during startup, is not itself a failure mode; see *Shutdown
Sequence.md*. This is explicitly distinct from a platform-service failure
during `Starting`, which goes directly to `Faulted` (ADR-0013), not through
this phase.

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
