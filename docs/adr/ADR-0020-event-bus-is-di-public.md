# ADR-0020: The Event Bus Is a DI-Public Platform Service

## Status

Accepted — v0.4.0 release planning (WP 4.0 / WP 4.4), 2026-07-23. Decided
before implementation begins, per this release's own instruction to resolve
architecture-significant questions ahead of code, not during it.

## Context

WP 4.4 (Event Bus) gives modules a way to communicate without depending on
each other directly. Two placements were considered:

**Option A — Host-owned collaborator**, mirroring Discovery, Registration,
and Lifecycle (ADR-0017): constructed and held directly by `TempestHost`,
never registered into the `ServiceCollection`, never resolvable by a module.

**Option B — DI-public platform service**, mirroring `IConfigurationProvider`
and `ILogger`: registered into the container and resolved by any module, or
any other DI-registered service, via ordinary constructor injection.

ADR-0017's reasoning for keeping Discovery, Registration, and Lifecycle out
of the container was specific: those three components *orchestrate the
module pipeline itself* (registering new modules, driving lifecycle
transitions, retriggering discovery). Granting a module access to them would
let it act as if it were the Host. An event bus does not do any of that — it
carries messages between modules; it does not register, initialise, start,
stop, or dispose anything. It is structurally identical in kind to
Configuration and Logging: a service modules *consume*, not a mechanism that
*drives* them.

## Decision

The Event Bus is DI-public. `IEventBus` is registered into the
`ServiceCollection` (during the existing Platform Services Registered phase
— see `Host Lifecycle.md`) and resolved by any module via ordinary
constructor injection, exactly like `IConfigurationProvider` and `ILogger`.

The governing shape:

```
Module
  ↓
IEventBus
  ↓
Runtime
```

Never:

```
Module A
  ↓
Module B
```

No module shall ever hold a direct reference to another module's type or
instance. A module publishes to `IEventBus`; a module subscribes through
`IEventBus`. Neither needs to know the other exists, let alone reference its
assembly.

## Consequences

**Positive:**

- No module ever needs a compile-time or run-time dependency on another
  module's assembly. This is the same principle an operating system kernel
  applies to inter-process communication: a process talks to the kernel's
  messaging primitives, never directly into another process's memory.
- Reuses the existing DI container and its established registration shape
  (ADR-0009's Composition Root pattern already covers services that need
  to exist before the container does, if `IEventBus`'s construction ever
  requires that) — no new registration mechanism is introduced.
- Consistent with, and does not weaken, ADR-0017: Discovery, Registration,
  and Lifecycle remain exactly as Host-owned and non-DI-public as before.
  This decision is about a different kind of component, not a reason to
  revisit that one.

**Negative:**

- `IEventBus` is a genuinely new, DI-public platform service — `TempestHost`'s
  Platform Services Registered step gains a new registration alongside
  Configuration, Logging, and discovered module types. This is an extension
  of the Composition Root's existing job, not a new phase, but it must be
  wired deliberately when WP 4.4 is implemented.
- `IEventBus` carries no orchestration authority whatsoever — it cannot
  register, initialise, start, stop, or dispose anything. Future
  contributors must not be tempted to grow it into a second path back into
  the module pipeline; if such a need ever arises, it is a new, separate
  decision, not an extension of this one.

## Future Considerations

If a future capability needs to restrict which modules may publish or
subscribe to which event types (a permissions or capability model), that is
an additive decision layered on top of this one, not a reason to reconsider
`IEventBus`'s DI-public status. WP 4.4's own implementation defines
`IEvent`/`IEventHandler<T>` (see WP 4.0, Platform Contracts) and per-subscriber
failure isolation; this ADR governs only where the bus lives, not its
dispatch semantics.
