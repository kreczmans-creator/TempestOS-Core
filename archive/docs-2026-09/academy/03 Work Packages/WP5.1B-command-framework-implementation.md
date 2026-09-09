# WP 5.1B — Command Framework Implementation

## 1. Introduction

WP 5.1B implements `ICommandDispatcher`, `ICommandRegistry`,
`CommandDescriptor`, `CommandResult`, and five exception types exactly as
`WP 5.1A` designed (`ADR-0036`–`ADR-0038`), with one small, disclosed
implementation nuance — a shared internal collaborator,
`CommandHandlerTable`, needed to make `ICommandRegistry.InvokeAsync`
actually dispatch through the same handler set `ICommandDispatcher.
RegisterHandler` populates. No architecture was redesigned; no STOP
condition was triggered.

## 2. Purpose

To realise `WP 5.1A`'s own design precisely, prove it against a real
sample module (`CommandSampleModule`) demonstrating successful execution,
expected failure, and navigation integration, and confirm — through a
comprehensive test suite, a mandatory security review, and manual
execution of the real application — that the implementation matches the
approved architecture exactly.

## 3. Background

`WP 5.1A`'s own architecture document left one thing deliberately
unresolved at the design level: exactly *how* `ICommandRegistry.
InvokeAsync` would share state with `ICommandDispatcher`, given that the
public contract is deliberately generic-only. This Work Package's own
brief was explicit: implement only the approved architecture; if it
proves insufficient, stop and recommend the minimum change, rather than
redesigning anything. Implementation found a genuine constraint the
architecture document had not anticipated — and resolved it within
existing architecture, not by escalating.

## 4. The Problem

`ICommandDispatcher.DispatchAsync<TCommand>` is generic; a caller must
supply `TCommand` at compile time. `ICommandRegistry.InvokeAsync(string
id)` only has a runtime-constructed `ICommand` (from `CommandDescriptor.
CreateDefault`) — its concrete type is not known until the method is
already running. There is no way to call a generic method with a
type parameter known only at runtime without either reflection or type
erasure. A second, related finding: registering the same concrete class
under two different service types (`services.Singleton<ICommandDispatcher,
CommandService>()` and `services.Singleton<ICommandRegistry,
CommandService>()`) does **not** produce one shared instance in this
container — confirmed by direct inspection of `TempestServiceProvider`'s
own per-`ServiceType` singleton cache, which constructs and caches a
*separate* instance for each `ServiceType` key, even when the
`ImplementationType` is identical. Both findings together ruled out the
two most obvious "quick fixes."

## 5. The Design

`CommandHandlerTable` — a type-erased handler store, registered as its
own ordinary container-constructed singleton and constructor-injected
into both `CommandDispatcher` and `CommandRegistry` — resolves both
findings at once. Because it is registered under exactly one
`ServiceType`, both consumers receive the identical, shared instance,
proven directly by a dedicated test
(`InvokeAsync_DispatchesThroughTheSameHandlerTableCommandDispatcherPopulates`)
and its container-resolved counterpart. Handlers are stored as
type-erased delegates, created as closures at registration time (when
`TCommand` is still known) — this needs no reflection at all, preserving
the architecture document's own "no reflection anywhere" claim exactly.
See `Command Framework Architecture.md`'s own "Implementation Note"
section for the complete reasoning, including the one small nuance this
finding produced: `CommandHandlerTable` had to be declared `public`
rather than `internal`, purely because C# does not allow a `public`
constructor to expose a less-visible parameter type (CS0051) — a
language-level constraint, not an architectural one, and not a capability
change for any caller.

## 6. Alternatives Considered

**Reflection-based generic method invocation** (`MethodInfo.
MakeGenericMethod(command.GetType()).Invoke(...)` from inside
`InvokeAsync`). Rejected — would have worked without changing any public
contract, but would have contradicted the architecture document's own,
explicit "no reflection anywhere" security claim, for no real benefit
over the type-erased closure approach, which achieves the identical
result with zero reflection.

**A downcast from `ICommandDispatcher` to the concrete `CommandDispatcher`
class inside `CommandRegistry`.** Considered and rejected — fragile
(breaks if the registered implementation type ever changes) and a known
code smell, where the shared-collaborator approach achieves the same
sharing cleanly, through ordinary constructor injection.

**Extending `ICommandDispatcher`'s own public interface with a
non-generic dispatch method.** Rejected — this Work Package's own brief
requires "confirm implementation conforms exactly to ADR-0036/037/038";
changing an already-Accepted ADR's own approved interface shape, even
additively, was judged a real deviation worth avoiding when an
equally-effective, purely-internal alternative existed.

## 7. Why This Solution Was Chosen

`CommandHandlerTable` needed zero new Dependency Injection capability
(an ordinary `Singleton<T>()` registration, identical in kind to every
other stateful platform service in this codebase), needed zero
reflection, and left `ICommandDispatcher`/`ICommandRegistry`'s own
approved public shape completely untouched. Every alternative considered
would have either introduced reflection, introduced fragility, or
changed an approved public contract — this solution introduced none of
the three.

## 8. Architectural Principles

Reuse Before Invention (the closure-based handler storage technique
mirrors `IEventBus`'s own imperative-instance-registration shape exactly);
Single Responsibility (`CommandHandlerTable` does exactly one thing —
store and look up handlers by type — leaving registration-time validation
and logging to `CommandDispatcher`/`CommandRegistry` themselves); Fail
Fast (duplicate registration rejected immediately, at registration time,
in both `CommandDispatcher.RegisterHandler` and `CommandRegistry.
RegisterDescriptor`).

## 9. Benefits

Zero architecture drift: `ICommandDispatcher`/`ICommandRegistry` match
`WP 5.1A`'s own approved shape exactly, confirmed by direct comparison.
A real, working reference implementation (`CommandSampleModule`)
demonstrates every scenario the brief asked for — successful execution,
expected failure, registration, invocation by both type and Id, and
navigation integration — the first concrete realisation of ADR-0022's
own `OpenModuleCommand → NavigationService.Navigate(...)` illustration,
proven end to end through the real, unmodified Runtime Host.

## 10. Trade-offs

`CommandHandlerTable`'s necessary public visibility is a small, disclosed
cost — a caller could resolve it directly and bypass `CommandDispatcher`'s
own logging, though gaining no capability beyond what `ICommandDispatcher`
already grants identically. This Work Package's own security review
judged it immaterial, requiring no Technical Debt entry.

## 11. Common Mistakes

The mistake this Work Package's own investigation avoided: assuming two
independent `Singleton<TService, TImplementation>()` registrations
against the same concrete type share one instance, by analogy with how
richer dependency injection containers (which support forwarding or
multi-interface registration) behave. This container does not — each
`ServiceType` gets its own, independently-constructed and independently-
cached instance, confirmed by direct inspection before relying on it,
not assumed by convention.

## 12. Future Evolution

Wiring `Tempest.App`'s Shell input handling (keyboard shortcuts, a menu)
to `ICommandRegistry.InvokeAsync` is explicitly deferred to a later Work
Package — the Command Framework itself needs no further design or
implementation to support it; the seam already exists via
`ITempestHost.Services`. `CMD-1`/`TD-11` (registration-order squatting)
remains open, requiring a future Architecture Work Package before
third-party plugins are real.

## 13. Key Takeaways

1. A genuine implementation-time finding (two singleton registrations
   against one concrete type do not share an instance in this container)
   was resolved by introducing a small, shared, container-registered
   collaborator — not by reflection, not by a fragile downcast, and not
   by changing an already-approved public contract.
2. "No reflection anywhere," stated as a security property at the
   architecture phase, was preserved as a real implementation constraint
   during a genuine design choice, not merely asserted and then quietly
   abandoned when a shortcut (reflection) would have been easier.
3. A C# language-level accessibility rule (CS0051) can force a small,
   disclosed visibility change with zero architectural significance —
   worth distinguishing clearly from an actual deviation from approved
   architecture, and worth documenting either way.

## Architectural Debt Assessment

No new debt item is disclosed by this Work Package. `TD-09` (plugin trust
boundary) and `TD-11` (registration-order squatting), both disclosed at
`WP 5.1A`, are confirmed present in the real implementation exactly as
designed — neither worsened, neither newly introduced. `CommandHandlerTable`'s
necessary public visibility was assessed and judged immaterial (see
`Command Framework Architecture.md`'s Security Review Update) — no
Technical Debt entry was created for it.

## Observations

This Work Package is a small, concrete demonstration of "implement only
the approved architecture; stop and recommend rather than redesign if it
proves insufficient" actually working as intended: a real gap was found
between the architecture document's own prose and the DI container's
actual behaviour, and the correct response was neither to silently
paper over it with a shortcut (reflection) nor to halt the Work Package
and escalate, but to find a minimal, disclosed resolution fully within
the spirit of the approved decisions — then document it clearly enough
that a future reader never has to rediscover the same constraint the
hard way.

## Related Documents

`Command Framework Architecture.md` (Implementation Note, Security
Review Update); `ADR-0036`–`ADR-0038`; `docs/academy/
02 Runtime Architecture/11-command-framework.md`;
`docs/academy/03 Work Packages/WP5.1A-command-framework-architecture.md`;
`docs/governance/Quality/Technical Debt Register.md` (`TD-09`, `TD-11`).
