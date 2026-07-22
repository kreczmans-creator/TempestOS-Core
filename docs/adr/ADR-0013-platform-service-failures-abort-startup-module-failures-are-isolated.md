# ADR-0013: Platform-Service Failures Abort Host Startup; Module Failures Remain Isolated

## Status

Accepted — WP 2.7 (Runtime Host Architecture), 2026-07-22. Architecture only;
no code changes accompany this decision.

## Context

WP 2.3 established, deliberately, that a single module's failure during
`InitialiseAllAsync`/`StartAllAsync`/etc. does not prevent other modules from
being processed — the module is marked `Failed`, logged, and the batch
continues. This is correct and already implemented.

The Host introduces a level above modules: platform *services* — Configuration,
Logging, Dependency Injection, Discovery, Registration. These are not modules;
they are the infrastructure every module (and the Lifecycle service itself)
depends on to exist at all. The question WP 2.7 had to answer explicitly:
if `ConfigurationBuilder.Build()` throws, or Discovery finds a duplicate
module ID, or the DI container fails to construct — does the Host apply the
same per-item isolation WP 2.3 established for modules, or does it treat this
differently?

## Decision

**Platform-service failures are Host-fatal.** If Configuration fails to
build, Logging fails to construct, Discovery throws, Registration throws, or
the DI container fails to construct, the Host aborts startup entirely and
transitions to `Faulted` (see *Runtime State Machine.md*). There is no
"partial platform" — a Host does not run with, say, Configuration available
but Discovery having silently failed.

**Module failures remain isolated, exactly as WP 2.3 already established.** A
module ending up in `ModuleState.Failed` during Module Initialisation does
**not** abort Host startup and does **not**, by itself, transition the Host to
`Faulted`. The Host can legitimately reach `Running` with one or more modules
`Failed`, visible for inspection via `IModuleLifecycleManager`.

The boundary is the platform/module distinction already implicit throughout
the Platform Service Map: services are infrastructure every module needs;
modules are the things running on top of that infrastructure. A failure in
the foundation is categorically different from a failure in one thing built
on it.

## Consequences

**Positive:**

- WP 2.3's per-module isolation, already implemented and already tested, is
  preserved exactly as-is — the Host does not need to (and must not) impose a
  stricter policy on modules than `ModuleLifecycleManager` already applies.
- The Host's own failure surface is narrow and well-defined: exactly five
  platform services can abort startup (Configuration, Logging, DI, Discovery,
  Registration), each with an already-established, dedicated exception
  hierarchy (`ConfigurationException`, no dedicated logging-construction
  exception yet, `ServiceResolutionException`, `ModuleDiscoveryException`,
  `ModuleRegistrationException`) the Host can catch and react to specifically.
- Operators get a single, reliable signal — Host state — for "is the platform
  itself sound," while still being able to drill into "which specific modules
  are unhealthy" separately, consistent with ADR-0012.

**Negative:**

- A Host reaching `Running` is not, by itself, proof that every module is
  working — a monitoring or health-check consumer must check module state
  separately, not assume `Running` implies "everything is fine."
- The boundary between "platform service" and "module" needs to stay
  intuitive as the platform grows — a future service that blurs the line
  (is it infrastructure every module needs, or itself a "module") would need
  this ADR revisited to decide which failure policy applies to it.

## Future Considerations

If a future platform service is introduced (see the Platform Service Map's
planned entries — Host itself aside, a Requirements Engine or Project Engine
could plausibly be either a platform service or a set of modules), its
classification should be decided explicitly and should determine which
failure policy applies, rather than being left ambiguous. The default
question to ask: "does the rest of the platform, including other modules,
need this to exist before it can function at all?" — if yes, it is a platform
service and its failure should be Host-fatal; if no, it is better modelled as
a module, isolated per WP 2.3's existing policy.
