# ADR-0017: Discovery, Registration, and Lifecycle Remain Host-Owned Collaborators, Not Public DI Services

## Status

Accepted — resolves WP 2.7's Open Question 4, 2026-07-22. Architecture only;
no code changes accompany this decision.

## Context

*Runtime Host Architecture.md* / *Host Lifecycle.md* (WP 2.7) left open
whether `IFrameworkDiscoveryService`, `IRuntimeModuleManager`, and
`IModuleLifecycleManager` should be registered into the DI container as
resolvable services — letting any module declare a constructor dependency on
them, exactly as it can already for `IConfigurationProvider` or `ILogger`.

## Decision

No. Discovery, Registration, and Lifecycle remain **Host-owned collaborators**:
constructed and held directly by `TempestHost`/`TempestHostBuilder`, never
added to the `ServiceCollection`, never resolvable by a module or any other
DI-registered service.

## Reasoning

Making these three services injectable would let any module, via ordinary
constructor injection, reach back into the machinery that is supposed to be
driving *it*: a module's constructor or `InitialiseAsync` could call
`IRuntimeModuleManager.Register(...)` to add a module the Host never
discovered, or call `IModuleLifecycleManager.StopAllAsync()` to shut down
*other* modules mid-startup, or hold a reference to
`IFrameworkDiscoveryService` and trigger a fresh, arbitrary discovery pass at
an arbitrary point in its own lifecycle. Every one of these is a module
acting as if it were the Host — exactly the kind of boundary violation the
platform-service/module distinction (ADR-0013) and the Single Responsibility
principle (see that Engineering Principle document) have consistently guarded
against throughout the Academy.

More concretely, it would directly undermine the deterministic startup model
WP 2.7 exists to establish (*Startup Sequence.md*, *Runtime State Machine.md*):
that model depends on discovery happening exactly once, in one place, at a
known point in the sequence, with registration and lifecycle orchestration
following deterministically from its output. A module able to call `Register`
or re-trigger discovery at will makes "what modules exist and in what order
were they brought up" no longer a question the Host alone can answer.

This decision is also the natural, stricter conclusion of a principle already
established for Discovery specifically: ADR-0008 kept
`ReflectionFrameworkDiscoveryService` entirely independent of the DI
container's own construction. Keeping it, and its two collaborators, fully
*out* of the container — not merely "in the container but conventionally
unused by modules" — is the enforced version of that same boundary, not a new
one invented for this ADR.

## Consequences

**Positive:**

- The deterministic startup/shutdown model this entire work package designed
  remains structurally guaranteed, not merely conventionally respected — a
  module has no *path* to Discovery, Registration, or Lifecycle, not just an
  instruction not to use one.
- Reinforces ADR-0013's platform-service/module boundary at the API surface
  itself: modules consume the platform; they do not drive it.
- Consistent with, and a direct generalisation of, ADR-0008's existing
  decision to keep Discovery independent of DI.

**Negative:**

- A legitimate future need — a diagnostics or health-check module wanting
  read-only visibility into "what modules exist and what state is each one
  in" — cannot be met by ordinary constructor injection of
  `IRuntimeModuleManager`/`IModuleLifecycleManager` under this decision. See
  Future Considerations.

## Future Considerations

If a genuine need arises for a module to *observe* (never drive) the module
pipeline's state, the correct answer is a new, narrow, read-only service —
for example, a health-report type constructed by the Host from
`IModuleLifecycleManager`'s own snapshot data (`Modules`/`GetState`) and
registered as a deliberately limited view — not registering
`IRuntimeModuleManager` or `IModuleLifecycleManager` themselves, which would
grant full orchestration authority (registration, initialisation, shutdown) to
whatever depended on them, not merely read access.
