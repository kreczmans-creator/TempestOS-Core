# ADR-0034: `ITempestHost` Exposes a Read-Only Service Resolution Surface for External Consumers

## Status

Accepted — `v0.5.0` "Developer Experience" release, `WP 5.0C` (Shell &
Composition Framework Architecture), 2026-07-27. Depends on `ADR-0033`
(the Shell is a composition root layered above the Host) already being
decided; this ADR answers the mechanical question that decision leaves
open: once the Shell has a running `ITempestHost`, how does it actually
reach `INavigationProvider`/`IEventBus`?

## Context

`ITempestHost`'s current public surface (`State`, `RunAsync`, `StopAsync`,
`DisposeAsync`) exposes nothing that lets an external caller resolve a
DI-public platform service. The Host's own `ITempestServiceProvider` is
built entirely inside `TempestHost.ExecuteStartupPhasesAsync`, held in a
local variable, and never assigned to any field or returned by any public
member — confirmed directly. A module reaches `INavigationProvider`/
`IEventBus` today only because the container constructs the module itself
and resolves its constructor dependencies; the Shell is not, and per
`ADR-0033`, must not become, a module — so it has no comparable path in.

Three shapes were available: (1) expose the Host's own
`ITempestServiceProvider` through a new, read-only member on
`ITempestHost`; (2) have a module smuggle a resolved reference out to some
shared, Shell-visible location during its own `InitialiseAsync`; (3)
introduce an entirely new, Shell-specific resolution mechanism,
independent of the existing container.

## Decision

**`ITempestHost` gains one new, additive, read-only member: `Services`
(`ITempestServiceProvider?`), `null` until the Dependency Injection Built
phase completes, non-`null` from then until the Host is disposed.** The
Shell resolves `INavigationProvider`/`IEventBus` through it, using the
exact same `GetService(Type)` call any module's own resolution already
goes through internally — no new resolution mechanism is introduced.

**This does not weaken `ADR-0017`.** Discovery, Registration, Lifecycle,
and Hosted Service Discovery/Manager remain exactly as Host-owned as
before: none of them is ever added to the `ServiceCollection` in the
first place, so exposing read access to the container's own `GetService`
call cannot make any of them resolvable — there is nothing for `Services`
to hand out that a module could not already, in principle, resolve
itself. `ADR-0017`'s own protection is that these four components are
never *registered*, not that the container object is never *visible*;
this decision touches only the latter.

**Option (2) — a module smuggling a reference out through shared,
mutable state — was rejected** as fragile and order-dependent: it would
only work if that specific module happened to be discovered and
initialised, would introduce an unowned, ad hoc escape hatch outside this
project's established ownership conventions, and would solve a general
problem (the Shell needs access) with a special-cased, single-purpose
trick. **Option (3) — a wholly new resolution mechanism — was rejected**
as pure duplication: `ITempestServiceProvider` already does exactly what
is needed; inventing a second one would violate Reuse Before Invention
for no offsetting benefit.

## Consequences

**Positive:**

- Zero new resolution mechanism is introduced — the Shell uses the
  identical `ITempestServiceProvider`/`GetService` API a module already
  uses internally, just handed out one layer higher.
- `ADR-0017`'s own boundary is reaffirmed, not merely left alone: this
  decision is the direct, mechanical confirmation that "DI-public" and
  "Host-owned, never DI-public" remain the only two categories a service
  can fall into, regardless of who is asking.
- The Runtime Host remains fully UI-agnostic: `Services` does not know or
  care who resolves what through it, exactly as `TempestServiceProvider`
  never has.

**Negative:**

- A caller holding `Services` could, in principle, resolve a discovered
  module's own concrete type directly (module types are registered as
  ordinary singletons, per `AddDiscoveredModules`) — an unusual, if
  harmless, capability, since modules themselves carry no orchestration
  authority. The intended and expected use of `Services` is resolving
  DI-public *platform* services (`INavigationProvider`, `IEventBus`), not
  reaching into a specific module's own instance; this distinction is
  disclosed here rather than silently relied upon.
- `Services` being `null` before Dependency Injection Built means any
  caller must check for readiness — a small, explicit precondition the
  Shell's own composition model already accounts for (see `Shell &
  Composition Framework Architecture.md`'s "Composition Model" section).

## Future Considerations

If a future consumer needs `Services` to be available earlier than
Dependency Injection Built, or needs a push-based readiness signal rather
than a nullable-property check, that is a new, narrow extension to
`ITempestHost`'s own contract, revisiting this ADR explicitly — not a
reason to add a second, competing resolution surface alongside `Services`.
