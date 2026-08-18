# Failure Behaviour

**Status: implemented — WP 2.7B (`Tempest.Core.Runtime`).** Every rule below
is now backed by working, tested code, not only design intent.

**Update, WP 4.2:** the Plugin Discovery/Loading Failure section below
(ADR-0025, ADR-0026) is now implemented (`Tempest.Core.Plugins`) — every
rule in it is backed by working, tested code (`PluginManifestDiscoveryServiceTests`,
`PluginAssemblyLoaderTests`), not only design intent.

**Update, WP 4.5:** the Hosted Service Failure section below (ADR-0021,
ADR-0029, ADR-0030) is now implemented (`Tempest.Core.BackgroundServices`)
— every rule in it is backed by working, tested code
(`HostedServiceManagerTests`, `TempestHostHostedServiceTests`), not only
design intent.

**Update, WP 13.0A (architecture only):** the Plugin Discovery/Loading
Failure section below is extended with seven new isolated failure
categories, twelve through eighteen — three from dependency-graph
resolution (`ADR-0107`) and four from trust/signature/capability
enforcement (`ADR-0111`, `ADR-0112`). Architecture only; implementation
is `WP 13.0B`'s own scope.

## Governing Principle

The boundary established by ADR-0013 governs every failure mode below:
**platform-service failures are Host-fatal; module failures are isolated.**
Everything in this document is a specific application of that one rule,
plus two failure modes (shutdown exceptions, logging failures) that don't fit
neatly into "platform service" or "module" and are addressed on their own
terms.

## Configuration Failure

**Trigger.** `ConfigurationBuilder.Build()` throws `ConfigurationException`
(or a subtype: `InvalidConfigurationEntryException`,
`DuplicateConfigurationKeyException`) — a missing/empty key, a null value, or
a duplicate key within one source.

**Required behaviour.** Host-fatal. Transition directly `Starting → Faulted`.
Nothing else has been built yet; disposal is attempted for consistency but has
nothing to release.

## Plugin Discovery/Loading Failure *(ADR-0025, ADR-0026; implemented — WP 4.2. Extended — ADR-0107, ADR-0111, ADR-0112, WP 13.0A, architecture only)*

**Trigger.** Any of the eleven failure categories ADR-0025 classifies,
occurring during Plugin Discovery (Phase 3.1) or Plugin Loading
(Phase 3.2) — a malformed manifest, a duplicate plugin identity, an
incompatible platform version, a missing or corrupt assembly, a
dependency load failure, or a reflection/type load failure.

**Required behaviour.** **Not** Host-fatal, for every category above —
isolated to the one plugin, exactly like an individual module's failure
(ADR-0013's other half): logged at the severity ADR-0025 assigns, that
plugin excluded, the phase continues with every remaining candidate. The
Host proceeds to Module Discovery regardless, even if every plugin fails
or none is present at all — a zero-plugin run is indistinguishable from
today's behaviour.

**The one exception**: a genuine defect in Plugin Discovery's or Plugin
Loading's own orchestration, not attributable to any specific plugin — a
Host-level bug, not a plugin failure, and Host-fatal:
`Starting → Faulted`, exactly the same transition Configuration Built,
Logging Built, Module Discovery, and Module Registration already use for
their own Host-fatal failures. No new transition is introduced.

**Extended, `WP 13.0A` (architecture only).** Also any of seven further
categories:

- **Categories 12–14** (`ADR-0107`) — a missing plugin dependency, an
  incompatible plugin dependency version, or a circular plugin
  dependency.
- **Categories 15–16** (`ADR-0112`) — a manifest-carried signature
  present but failing to verify, or absent with unsigned loading not
  explicitly enabled.
- **Category 17** (`ADR-0111`) — a requested capability outside the
  plugin's assigned trust tier's ceiling, or a plugin module's
  constructor requiring an undeclared/ineligible service type.
- **Category 18** (`ADR-0111`) — a *running* plugin attempting a
  capability-gated operation it was not granted; unlike 12–17, this
  occurs after the plugin is already `Loaded`, not during Discovery/
  Loading, and blocks only the one call, not the plugin as a whole.

**Required behaviour (extended, `WP 13.0A`).** Identical treatment for
categories 12–17 — isolated, never Host-fatal, logged at the severity
`ADR-0107`/`ADR-0112` assign, that plugin (or, for a circular
dependency, every participating plugin) excluded, the phase continues.
Category 18 blocks only the one denied call, logged at Warning,
mirroring `PermissionEvaluator`'s own existing denied-permission
convention — the plugin itself remains `Loaded` and running. The
Host-fatal carve-out above is unchanged and ungrown by any of these
seven categories.

## Discovery Failure

**Trigger.** `IFrameworkDiscoveryService.DiscoverModules()` throws
`ModuleDiscoveryException` (invalid metadata) or `DuplicateModuleIdException`.

**Required behaviour.** Host-fatal. `Starting → Faulted`. Disposal attempts to
release Configuration and Logging (currently no-ops — see Architectural Debt
Assessment, WP 2.7 Academy review).

## Registration Failure

**Trigger.** `RuntimeModuleManager.Register` throws
`DuplicateModuleRegistrationException`.

**Required behaviour.** Host-fatal. `Starting → Faulted`. In practice this
should be unreachable, since Discovery already rejects duplicate IDs before
Registration ever sees them — Registration's own guard is not bypassed or
relied upon as the sole protection, but its failure mode is still Host-fatal
if somehow reached (for example, via a future path that registers descriptors
not sourced from Discovery).

## Hosted Service Failure *(ADR-0021, ADR-0029, ADR-0030; implemented — WP 4.5)*

**Trigger.** A hosted service (`IHostedService`) throws during
`StartAsync` (Phase 8.1, Hosted Services Started) or `StopAsync`
(Phase 10.1, Hosted Services Stopped).

**Required behaviour.** **Not** Host-fatal by default — isolated exactly
like an individual module's failure (ADR-0013's own module half, extended
by ADR-0021): logged at `Error`, that service's own status marked
`Failed`, the batch continues with the next service. The Host proceeds to
`Running` (from Phase 8.1) or to `Stopped` (from Phase 10.1) regardless.

**The one exception**: a service implementing `ICriticalBackgroundService`
has explicitly opted out of isolation. Its failure is Host-fatal —
`Starting → Faulted` (from `StartAsync`) or `Stopping → Faulted` (from
`StopAsync`) — exactly the same transitions already used for a
platform-service failure and a genuine shutdown-time Host-level defect,
respectively. No new transition is introduced. Cleanup guarantees hold
regardless: `Faulted → Disposed` remains always legal, and disposal of
every module and every hosted service that already started is still
attempted afterward (ADR-0004, ADR-0019).

## Initialisation Failure

**Trigger.** A module throws during `InitialiseAllAsync`/`StartAllAsync`.

**Required behaviour.** **Not** Host-fatal — see ADR-0013. Already isolated by
`ModuleLifecycleManager` (WP 2.3): the module is marked `Failed`, logged, and
the batch continues. The Host reaches `Running` regardless. The Host must not
introduce any additional handling here beyond trusting the existing isolation
— duplicating it at the Host level would be redundant and risks the two layers
disagreeing about what "failed" means.

**The one exception**: a failure in the Host's own construction of
`ModuleLifecycleManager`, or in the Host's own call sites around it (not a
module's own code) — this is a Host-level defect, not a module failure, and is
Host-fatal.

## Runtime Exception

**Trigger.** An unhandled exception during the `Running` state.

**Required behaviour.** Host-fatal — `Running → Faulted`. No code path
produces this today — `WP 4.5`'s hosted service orchestration is fully
resolved by Phase 8.1/10.1 before or as `Running` is entered or left, and
introduces no ongoing supervision of a hosted service once it is
`Running`; this policy remains defined so any future work package that
does introduce ongoing supervision has an established rule to follow
rather than needing to invent one at that point.

## Shutdown Exception

**Trigger.** An exception during `Stopping` — either an individual module's
Stop/Dispose failure, or a genuine defect in the Host's own shutdown
orchestration.

**Required behaviour.** Individual module failures: already isolated by
`ModuleLifecycleManager` (WP 2.3) — no Host-level change needed, `Stopping`
still proceeds to `Stopped`. A genuine Host-level defect (not a module
failure): logged, and `Stopping → Faulted` is permitted — but disposal must
still be attempted afterward (`Faulted → Disposed`, per ADR-0004's Host-level
reuse). **Under no circumstance does a shutdown-time failure prevent `Host
Disposed` from eventually being reached** — every step in *Shutdown
Sequence.md* is attempted regardless of what failed before it.

## Partial Startup

**Definition.** Any point where some, but not all, of Configuration Built
through Module Initialisation completed before a Host-fatal *failure*
occurred. This section is specifically about the **fault** case — a genuine
platform-service exception. A startup interrupted by *cancellation* or an
early shutdown request instead (not a failure at all) is the separate case
ADR-0018 covers, routed through `Stopping`, not through this section's
"Post-Fault Teardown" path — see *Shutdown Sequence.md* for both, side by
side.

**Required behaviour.** Whatever *was* built must have disposal attempted
against it — see *Shutdown Sequence.md*'s "Post-Fault Teardown" diagram. Today
this is largely a no-op in practice (Configuration, Logging, and the DI
container implement no disposal), but the *policy* — attempt disposal of
everything that exists, regardless of how far startup got — is established
now, consistent with ADR-0004's reasoning, so that when any of these services
does become disposable, the Host does not need to be redesigned to honour it.

## Partial Shutdown

**Definition.** Some modules or services fail to stop/dispose cleanly during
`Stopping`.

**Required behaviour.** Already fully handled by `ModuleLifecycleManager`'s
existing per-module isolation (WP 2.3) — no new Host-level policy is
introduced or needed. The Host's only obligation is to keep calling the
remaining steps in *Shutdown Sequence.md* regardless of what failed, which
`ModuleLifecycleManager`'s own design already guarantees will not itself
throw and abort the batch for an isolated module failure.

## Logging Failure

**Trigger.** An `ILogSink` implementation (today, `ConsoleLogSink`) throws —
for example, a closed or redirected console stream.

**Required behaviour, per WP 2.6's own stated architectural principle:**
"logging failures must never terminate the runtime." A logging failure must
never propagate out of a logging call and affect the operation that happened
to be logging something — configuration building, module registration,
lifecycle transitions, and the Host's own orchestration must all be able to
proceed exactly as if the log call had succeeded, even if it didn't.

**Fixed — WP 2.7B.** This was a genuine gap between WP 2.6's own stated
principle and its shipped implementation, discovered during WP 2.7A's
architecture-only review (which flagged it but, per its own scope, could
not fix it) and closed as WP 2.7B's own first step, before the Host
implementation that would become logging's heaviest caller. `Logger.Log()`
now wraps `_sink.Write(entry)` in a `try`/`catch`; a sink failure is
reported directly to `Console.Error` — bypassing the failed sink entirely
— and never propagates to whatever code was logging something.

## Required Behaviour Summary

| Failure | Host-fatal? | State transition |
|---|---|---|
| Configuration failure | Yes | `Starting → Faulted` |
| Plugin Discovery/Loading — per-plugin failure *(ADR-0025/0026, implemented — WP 4.2; ADR-0107/0111/0112 categories 12–17, WP 13.0A, architecture only)* | No | (none — that plugin isolated, phase continues) |
| Plugin Discovery/Loading — Host-level defect *(ADR-0025/0026, implemented — WP 4.2)* | Yes | `Starting → Faulted` |
| Plugin — running-plugin capability denial *(ADR-0111 category 18, WP 13.0A, architecture only)* | No | (none — that call blocked, plugin remains `Loaded`) |
| Discovery failure | Yes | `Starting → Faulted` |
| Registration failure | Yes | `Starting → Faulted` |
| Individual module initialisation failure | No | (none — Host proceeds to `Running`) |
| Host-level defect during Module Initialisation | Yes | `Starting → Faulted` |
| Hosted service — isolated start failure *(ADR-0021/0029, implemented — WP 4.5)* | No | (none — that service isolated, phase continues) |
| Hosted service — critical start failure *(ADR-0021/0029, implemented — WP 4.5)* | Yes | `Starting → Faulted` |
| Runtime exception (Running) | Yes | `Running → Faulted` |
| Individual module shutdown failure | No | (none — `Stopping` proceeds to `Stopped`) |
| Hosted service — isolated stop failure *(ADR-0021/0029, implemented — WP 4.5)* | No | (none — that service isolated, phase continues) |
| Hosted service — critical stop failure *(ADR-0021/0029, implemented — WP 4.5)* | Yes, but disposal still proceeds | `Stopping → Faulted → Disposed` |
| Host-level defect during shutdown | Yes, but disposal still proceeds | `Stopping → Faulted → Disposed` |
| Logging failure | **Fixed — WP 2.7B.** A sink failure is caught inside `Logger` itself and never propagates. | (none) |
| Startup cancellation, or an early shutdown request | No (not a fault) | `Starting → Stopping → Stopped` (ADR-0018 — same controlled shutdown procedure as a graceful, post-`Running` stop) |
