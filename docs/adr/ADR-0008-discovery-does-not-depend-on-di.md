# ADR-0008: Discovery Does Not Depend on the Dependency Injection Container

## Status

Accepted — WP 2.1 (Module Discovery), reaffirmed WP 2.4 (Dependency Injection),
2026-07-22.

## Context

WP 2.4 introduced `ITempestServiceProvider` and its stated objective was that "the
runtime shall no longer construct module instances directly using
`Activator.CreateInstance()`." Read literally and applied everywhere, this could
have been taken to mean `ReflectionFrameworkDiscoveryService`'s own
`Activator.CreateInstance` call — used to transiently instantiate a candidate type
purely to read its `Id`/`Name`/`Version` metadata during discovery — should also be
routed through the new service provider.

This was considered and rejected during WP 2.4's implementation.

## Decision

Discovery continues to call `Activator.CreateInstance` directly, exactly as it did
before WP 2.4. `ReflectionFrameworkDiscoveryService` has no dependency on
`Tempest.Core.DependencyInjection` at all — not a reference, not a using
statement, nothing.

## Consequences

**Positive:**

- **No circularity.** The intended pipeline is Discovery → Registration →
  Lifecycle → Dependency Injection. For discovery to resolve instances through the
  container, the container would need to already contain registrations for the
  very types discovery hasn't found yet — a chicken-and-egg problem with no clean
  resolution order.
- **Discovery remains genuinely independent of and prior to composition.**
  Nothing about how TempestOS assembles its dependency graph needs to be decided,
  or even exist, before discovery can run. This keeps "what modules exist" a
  question discovery can answer entirely on its own.
- **The distinction between the two `Activator.CreateInstance` call sites is a
  meaningful one, not an oversight.** Discovery's instantiation is transient,
  metadata-only, and immediately discarded — it depends on ADR-0003 (side-effect-
  free constructors) precisely because that instance is never meant to do anything
  beyond expose three string properties. `ModuleLifecycleManager`'s instantiation
  (ADR-0007) produces the one, real, persistent instance a module's lifecycle
  methods are actually invoked on.

**Negative:**

- The two-instantiation-sites-per-module reality (one transient, one persistent)
  is a subtlety a new engineer needs to learn: a module class gets constructed at
  least twice over its life if it's ever discovered and later initialised — once
  by discovery (thrown away), once by the lifecycle manager via DI (kept). If
  either constructor call has any side effect (see ADR-0003), that effect happens
  twice, which is exactly why ADR-0003 exists and is enforced by convention.
  Anyone reading only the lifecycle code, without also knowing about discovery's
  metadata probe, could be surprised that "construction" happens more than once
  per module across a full application run.

## Future Considerations

If module metadata ever needs to come from something the container should
legitimately own (for example, if `Id`/`Name`/`Version` were themselves injected
rather than being plain properties on the module type), that would represent a
genuine architectural change to how discovery works and would need its own ADR —
it is out of scope for, and not implied by, this decision.
