# Failure Behaviour

**Status: architecture only. No production code exists yet.**

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
produces this today (no hosted services or background work exist yet); this
policy is defined now specifically so a future hosted-service implementation
has an established rule to follow rather than needing to invent one at that
point.

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

**This is currently not true of the implemented code.** `Logger.Log()` calls
`_sink.Write(entry)` with no exception handling — a sink failure propagates
directly to whatever code just tried to log something. This is a genuine gap
between WP 2.6's own stated principle and its shipped implementation,
discovered during this architecture work, **not fixed here** (WP 2.7 is
architecture-only and modifies no production code). It is flagged prominently
in the WP 2.7 Academy review's Architectural Debt Assessment and in the
completion report's Risks section, with a recommendation that a small, scoped
fix (wrapping `_sink.Write(entry)` in a try/catch inside `Logger.Log()`,
logging the sink failure's occurrence somewhere durable — even just to the
console directly, bypassing the failed sink — without ever letting it
propagate) be made before, or as part of, the Host's own implementation, since
the Host's orchestration will call logging extensively and is exactly the kind
of caller this principle exists to protect.

## Required Behaviour Summary

| Failure | Host-fatal? | State transition |
|---|---|---|
| Configuration failure | Yes | `Starting → Faulted` |
| Discovery failure | Yes | `Starting → Faulted` |
| Registration failure | Yes | `Starting → Faulted` |
| Individual module initialisation failure | No | (none — Host proceeds to `Running`) |
| Host-level defect during Module Initialisation | Yes | `Starting → Faulted` |
| Runtime exception (Running) | Yes | `Running → Faulted` |
| Individual module shutdown failure | No | (none — `Stopping` proceeds to `Stopped`) |
| Host-level defect during shutdown | Yes, but disposal still proceeds | `Stopping → Faulted → Disposed` |
| Logging failure | **Must never be** (currently is — see above) | (none, once fixed) |
| Startup cancellation, or an early shutdown request | No (not a fault) | `Starting → Stopping → Stopped` (ADR-0018 — same controlled shutdown procedure as a graceful, post-`Running` stop) |
