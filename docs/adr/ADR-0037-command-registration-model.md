# ADR-0037: Commands Register Imperatively, in Two Parts — a Type-Keyed Handler and an Id-Keyed Descriptor

## Status

Accepted — `WP 5.1A` (Command Framework Architecture), 2026-07-28.

## Context

`WP 5.1A`'s brief requires determining whether commands should register
imperatively, declaratively, via reflection, via metadata, via the Event
Bus, or remain entirely independent — and requires the framework to
support invocation from a menu, a toolbar, a keyboard shortcut, and
future automation/AI callers, none of which can reference a concrete
`ICommand` type at compile time. `ICommand`'s own `WP 4.0` doc comment
already commits to "dispatched by its own concrete type," which serves a
typed caller well but does not, by itself, serve a caller that has only
a string.

## Decision

**Registration is imperative, and split into two independent
contracts:**

1. **`ICommandDispatcher.RegisterHandler<TCommand>(ICommandHandler<TCommand>
   handler)`** — a module constructor-injects `ICommandDispatcher` and
   registers an already-constructed handler *instance*, during its own
   `InitialiseAsync`/`StartAsync`, mirroring `IEventBus.Subscribe<TEvent>
   (IEventHandler<TEvent> handler)` exactly. Exactly one handler per
   command type is enforced by the dispatcher's own internal dictionary
   at registration time; a second registration for an already-claimed
   type throws `DuplicateCommandHandlerException`.
2. **`ICommandRegistry.RegisterDescriptor(CommandDescriptor descriptor)`**
   — the same module registers a `CommandDescriptor` (Id, display
   metadata, an optional `CanExecute` predicate, an optional
   parameterless `CreateDefault` factory), mirroring
   `INavigationProvider.Register(NavigationItem)` exactly. A second
   registration for an already-claimed Id throws
   `DuplicateCommandIdException`.

**Rejected: declarative/attribute-based registration**
(`ModuleMetadataAttribute`'s own shape, read by reflection). That
mechanism exists specifically to answer a metadata question *without
instantiating* the type in question — a real constraint only because
Discovery runs before the DI container exists. Command registration
happens *after* Dependency Injection Built, during Module
Initialisation, when the module is already constructed and already
running through its own lifecycle — there is no instantiation-avoidance
problem here for a declarative mechanism to solve. See `RD-0038`.

**Rejected: dispatching commands through the Event Bus.** An event has
zero or more subscribers and no expected result (`ADR-0028`); a command
has exactly one handler and an expected result (`Risks.md` R3, the
Engineering Glossary). Reusing `IEventBus.PublishAsync` would isolate a
command handler's own failure exactly like an isolated event-subscriber
failure — silently absorbing exactly the outcome information "an
expected result" requires the caller to receive. See `RD-0039`.

**Rejected: resolving `ICommandHandler<TCommand>` through the DI
container itself** (a generic-handler, reflection-discovered service, the
shape `ICommand`'s own doc comment might suggest). `TempestServiceProvider`
supports neither open-generic registration nor a mechanism for a module
to add a new registration after the `ServiceCollection` has already been
frozen into a provider. Achieving this would require either a new,
Discovery-shaped reflection pass scanning every module for
`ICommandHandler<T>` implementations before the container is built (a
structurally invasive new mechanism), or extending the container with
open-generic/keyed registration (a genuine container redesign, out of
this Work Package's own scope). Registering a handler *instance*
directly — needing zero new DI capability — achieves the identical
"exactly one handler per command type" invariant without either. See
`RD-0040`.

**Rejected: allowing a later registration to silently override an
earlier one.** Every existing registry in this platform
(`RuntimeModuleManager`, `NavigationService`) rejects, rather than
silently accepts, a duplicate — a silent override would make a command
Id or type collision (accidental or, per this Work Package's own
Security Review, `CMD-1`, potentially adversarial) invisible. First
registration wins; a colliding, later registration is rejected and
isolated by the platform's existing, unmodified per-module isolation
(`ADR-0013`). See `RD-0041`.

**No `Unregister`/`Deregister` is defined for either contract.**
`NavigationService.Unregister(string id)` was found, by this Work
Package's own Security Review, to have no ownership check
(`docs/security/Platform Security Review v0.5.0.md`, `NAV-1`) — any
caller holding an `INavigationProvider` reference can remove any other
component's item. The Command Framework does not repeat this shape for
`v0.5`; a registration, once accepted, persists for the Host's entire
remaining run.

## Consequences

**Positive:**

- Zero new Dependency Injection capability is required — the entire
  registration model reuses the imperative-instance-registration shape
  already proven by the Event Bus and Navigation.
- "Exactly one handler per command type" (`ICommand`'s own original,
  `WP 4.0` invariant) is preserved, enforced by the dispatcher's own
  registration-time check rather than by a container feature that does
  not exist.
- A caller with only a string Id (a keyboard shortcut, a menu, an
  automation script) is served by `ICommandRegistry`'s own Id-keyed
  catalogue without needing any reference to a concrete `ICommand` type
  — directly satisfying `WP 5.1A`'s own multi-form-factor and
  automation/AI-invocation requirements.

**Negative:**

- Two registration calls, not one, for a module that wants both typed
  dispatch and Id-based invocation of the same command — a small,
  deliberate cost of keeping the two concerns (dispatch, discovery)
  independently useful (a command usable only programmatically need not
  register a descriptor at all).
- The "first registration wins" rule is vulnerable to registration-*order*
  squatting, not merely name collision — see this Work Package's own
  Security Review, `CMD-1`, recorded as `TD-11`. Not fixed by this
  decision; requires a future ownership/priority model.

## Related Documents

`ADR-0027` (`ModuleMetadataAttribute` — reasoned from, not reused);
`ADR-0028` (Event Bus registration/dispatch shape — the direct
precedent this decision's registration half reuses); `ADR-0032`
(Navigation's own imperative registration decision — independently
re-derived, same conclusion); `RD-0038`–`RD-0041`; `Command Framework
Architecture.md`; `docs/security/Platform Security Review v0.5.0.md`
(`NAV-1`); `docs/governance/Quality/Technical Debt Register.md`
(`TD-11`).
