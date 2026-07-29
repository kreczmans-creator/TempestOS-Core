# ADR-0036: The Command Framework Is a DI-Public Platform Service

## Status

Accepted — `WP 5.1A` (Command Framework Architecture), 2026-07-28.

## Context

`ICommand` has existed since `WP 4.0` as a plain data contract, with no
handler and no dispatcher. `WP 5.1A`'s brief requires designing both,
and integrating cleanly with the Runtime Host, Event Bus, Navigation,
and Application Shell without architectural drift. The first question,
mirroring the one Navigation and the Event Bus each already answered for
themselves, is ownership: does a command dispatcher carry orchestration
authority over the module pipeline — the property that would make it
Host-owned and deliberately excluded from the DI container (`ADR-0017`)
— or does it carry no such authority, making it an ordinary,
constructor-injectable Platform Service, exactly like `IEventBus`
(`ADR-0020`) and `INavigationProvider` (`ADR-0032`)?

## Decision

**The Command Framework (`ICommandDispatcher`, `ICommandRegistry`, and
their concrete implementations) is a DI-public Platform Service,
registered as an ordinary container-constructed singleton — not
Host-owned.**

Applying `ADR-0017`'s own test directly: a command dispatcher cannot
register a module, retrigger Discovery, initialise, start, stop, or
dispose anything. It only accepts a request to run one already-
registered piece of application logic and reports the outcome. This is
precisely the same non-authority `IEventBus` and `INavigationProvider`
already have, and the same reasoning that placed each of them in the DI
container rather than as a `TempestHost`-owned collaborator applies here
without modification.

`ICommandDispatcher`/`ICommandRegistry` are registered during the
existing Platform Services Registered phase (Phase 6 of `Host
Lifecycle.md`), alongside `services.Singleton<IEventBus, EventBus>()`
and `services.Singleton<INavigationProvider, NavigationService>()`. No
new Host Lifecycle phase, `HostState`, or transition is introduced.

## Consequences

**Positive:**

- A module or plugin-loaded module resolves the Command Framework
  through ordinary constructor injection, exactly as it already resolves
  every other DI-public service — no special-casing, no new resolution
  mechanism.
- No change to `Runtime State Machine.md`, `Host Lifecycle.md`'s phase
  table, or `Failure Behaviour.md`'s Host-fatal/isolated boundary — the
  third platform service in a row (after the Event Bus and Navigation)
  to require zero Host Lifecycle change, reinforcing that this is a
  robust, general pattern rather than a coincidence specific to either
  prior case.
- Consistent with `ADR-0023`'s four-layer model: the Command Framework
  sits in the Platform Services layer, depended on downward by Modules,
  depending on nothing module-specific itself.

**Negative:**

- Like `IEventBus`/`INavigationProvider`, the Command Framework is a
  process-wide singleton with no per-tenant scope — a pre-existing,
  already-disclosed future-readiness observation (`docs/security/
  Platform Security Review v0.5.0.md`, `FR-1`), not reopened or worsened
  by this decision, but not resolved by it either.

## Alternatives Considered

**Host-owned, mirroring Discovery/Registration/Lifecycle.** Rejected —
the Command Framework carries no orchestration authority over the
module pipeline; applying `ADR-0017`'s exclusion here would misclassify
it, and would additionally prevent the exact "invoke from anywhere"
requirement (`WP 5.1A`'s own brief) this design exists to satisfy, since
a Host-owned collaborator is deliberately unreachable from outside the
module pipeline.

## Related Documents

`ADR-0017` (Discovery/Registration/Lifecycle Host-owned); `ADR-0020`
(Event Bus DI-public — direct precedent); `ADR-0032` (Navigation
DI-public — second precedent, same reasoning independently re-derived);
`Command Framework Architecture.md`.
