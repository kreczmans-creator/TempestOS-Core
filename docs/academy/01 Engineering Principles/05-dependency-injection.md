# Dependency Injection

## What

Dependency Injection (DI) is the practice of supplying an object's dependencies
from outside, at construction time, rather than having the object create or look
up its own dependencies internally. A "dependency injection container" is a piece
of infrastructure that automates this: given a set of registrations (this
interface maps to this implementation, with this lifetime), it constructs an
object graph on request, resolving each dependency recursively.

DI is a specific technique for achieving Dependency Inversion (the "D" in SOLID)
in practice.

## Why

Without DI, a class that needs a collaborator typically either constructs it
directly (`new SomeConcreteClass()`) or reaches for it through a static/global
accessor (a service locator, a singleton instance). Both couple the class
directly to a specific implementation and make substituting a different
implementation — a test double, an alternative strategy, a different
configuration — require changing the class itself. DI breaks this coupling: the
class declares what it needs (typically as constructor parameters, typed as
interfaces), and something else — the composition root, or a container — decides
what concrete instance to actually supply.

## Benefits

- Classes become testable in isolation: a unit test can supply a fake/mock
  implementation of a dependency without needing the real one to exist or behave
  correctly.
- Swapping an implementation (a different logging backend, a different storage
  strategy) requires changing registration in one place, not every class that
  uses it.
- Object lifetimes (singleton vs. transient) become a registration-time decision,
  not something every class has to reason about and manage itself.
- Dependencies are visible: a class's constructor signature is a complete,
  honest list of what it needs to function (see Composition Over Inheritance).

## Disadvantages

- Indirection: understanding what concrete type actually gets used somewhere
  requires looking at the registration, not just the class itself — "where does
  this `IGreeter` actually come from?" is a question you can't answer by reading
  `GreeterConsumer` alone.
- A container introduces its own failure modes — missing registrations, circular
  dependencies, ambiguous constructors — that don't exist in code that constructs
  its dependencies directly. These need to be designed for deliberately (see the
  WP 2.4 retrospective for how TempestOS's container handles each).
- Overuse can lead to "everything is an interface with exactly one
  implementation, injected everywhere," adding ceremony without corresponding
  benefit — the same trap SOLID overuse falls into.

## When to Use

When a class's dependency genuinely needs to vary (across environments, across
tests, across configurations), or when the dependency graph is deep enough that
manual wiring becomes repetitive and error-prone. Also valuable simply for
testability, even if there is currently only one real implementation — being able
to substitute a test double is often reason enough.

## When Not to Use

For genuinely stable, parameterless value types and simple data structures that
will never need substituting — not every object needs to be resolved through a
container. Also unnecessary in code paths where manual construction is simple,
clear, and unlikely to ever need to vary (WP 2.1 through WP 2.3's own internal
code, notably, constructs its own collaborators manually and does not use the
WP 2.4 container internally — DI was introduced specifically to solve *module*
construction, not to retrofit itself into every existing class).

## How TempestOS Applies It

WP 2.4 is the direct answer to this principle: `ModuleLifecycleManager` no longer
constructs module instances itself via `Activator.CreateInstance` — it depends on
`ITempestServiceProvider` (an abstraction, injected via its own constructor) and
asks the provider for instances. The provider, in turn, resolves each module's own
constructor dependencies recursively, so a module can depend on other registered
services without `ModuleLifecycleManager` needing to know or care what those
dependencies are.

TempestOS built its own container rather than adopting
`Microsoft.Extensions.DependencyInjection` — see ADR-0005 for the full reasoning —
but the principle being applied is the same industry-standard one: singleton and
transient lifetimes, constructor injection, registration separated from
resolution (`IServiceCollection` vs. `ITempestServiceProvider`).

Notably, DI is applied narrowly and deliberately in TempestOS, not universally:
`RuntimeModuleManager`, `ReflectionFrameworkDiscoveryService`, and
`ModuleLifecycleManager` all still take their own collaborators as plain
constructor parameters, supplied manually by whoever constructs them (a test, or
eventually a composition root) — none of them are themselves resolved *through*
the container. Only module instances are. This is a conscious scope decision, not
an oversight: WP 2.4's brief was explicit that DI's job was module construction,
not a wholesale rewrite of how the rest of the runtime wires itself together.

## Key Takeaway

Dependency Injection is not "use a container everywhere" — it's "let something
external decide what concrete collaborator a class receives." TempestOS applies
this narrowly, to exactly the one problem it was introduced to solve (module
construction), rather than retrofitting it across a codebase that, for its other
components, has no actual need for it yet.
