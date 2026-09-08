# SOLID

## What

SOLID is an acronym for five object-oriented design principles, popularised by
Robert C. Martin in the early 2000s:

- **S** — Single Responsibility Principle: a class should have one, and only one,
  reason to change.
- **O** — Open/Closed Principle: software entities should be open for extension
  but closed for modification.
- **L** — Liskov Substitution Principle: subtypes must be substitutable for their
  base types without altering the correctness of the program.
- **I** — Interface Segregation Principle: clients should not be forced to depend
  on interfaces they don't use.
- **D** — Dependency Inversion Principle: high-level modules should not depend on
  low-level modules; both should depend on abstractions.

This document gives a brief overview of each; Single Responsibility gets its own,
deeper treatment elsewhere in this section because of how central it is to
TempestOS's module/discovery/registration/lifecycle/DI separation.

## Why

SOLID exists because object-oriented codebases, left unguided, tend to accumulate
a specific set of pathologies: classes that do too much and are hard to change
safely; changes to one feature that ripple into unrelated code; substitutions that
silently break assumptions; interfaces so broad that implementing them means
implementing methods you don't need; and high-level policy code tangled directly
into low-level implementation detail. Each SOLID letter is a targeted response to
one of these specific failure modes, not a vague aspiration toward "good design."

## Benefits

- Each principle, applied, makes a specific category of future change cheaper and
  safer — SRP makes changes localised; OCP means adding behaviour doesn't require
  editing tested code; LSP means polymorphism is trustworthy; ISP keeps
  implementations honest about what they actually need; DIP makes high-level
  policy testable in isolation from low-level detail.
- The five principles compound. A codebase with small, single-purpose types (SRP),
  wired together through abstractions (DIP) rather than concrete references, is
  naturally easier to extend (OCP) and naturally produces small, focused
  interfaces (ISP).

## Disadvantages

- SOLID is frequently over-applied as ritual rather than reasoned engineering:
  interfaces created for types that will only ever have one implementation,
  abstraction layers introduced for extension points that will never actually be
  extended, single-method classes created in the name of "single responsibility"
  that fragment a cohesive piece of logic into pieces that are individually
  simple but collectively harder to follow.
- Liskov violations, in particular, are easy to introduce accidentally and
  genuinely hard to catch without deliberate attention — the type system does not
  enforce behavioural substitutability, only structural conformance.
- Applying all five principles maximally, everywhere, produces more indirection
  than most codebases need. SOLID is a set of tools for managing complexity that
  exists, not a target to maximise independent of whether the complexity is
  actually present.

## When to Use

When a piece of code has multiple, independent reasons to change over time; when
you're building an abstraction genuinely intended to have more than one
implementation, now or in a clearly foreseeable future; when a class's
responsibilities are already tangled enough that changes are risky and hard to
reason about.

## When Not to Use

When a type has exactly one implementation, no foreseeable need for a second, and
introducing an interface would only add a layer of indirection with no present
benefit. Not every class needs to be "SOLID" in isolation — the principles exist
to solve real problems, and applying them where the problem doesn't exist is pure
cost.

## How TempestOS Applies It

- **SRP** is the organising principle behind the entire module pipeline: Discovery
  discovers, Registration registers, Lifecycle orchestrates, the Service Provider
  constructs. Each of WP 2.1 through WP 2.4 exists specifically because a single,
  cohesive responsibility was carved out and given its own type and its own
  work package, rather than one component doing all four things.
- **OCP** shows up in `IModule`/`IModuleLifecycle`: new modules are added by
  implementing the interfaces, not by modifying `ReflectionFrameworkDiscoveryService`,
  `RuntimeModuleManager`, or `ModuleLifecycleManager`.
- **LSP** is why `IModuleLifecycle` implementations must all honour the same
  contract regardless of what they actually do internally — `ModuleLifecycleManager`
  treats every implementation identically, invoking the same four methods in the
  same order, and any implementation that behaves differently (blocking forever
  in `DisposeAsync`, say) would violate the substitutability the whole design
  depends on.
- **ISP** is why `IModule` (bare metadata: `Id`/`Name`/`Version`) and
  `IModuleLifecycle` (behaviour: `Initialise`/`Start`/`Stop`/`Dispose`) are two
  separate interfaces rather than one. A module with no lifecycle behaviour
  (see the `NoLifecycleModule` test fixture) implements only `IModule` — it is
  never forced to implement four methods it has nothing to do in.
- **DIP** is the entire subject of WP 2.4: `ModuleLifecycleManager` (high-level
  policy: when to construct, in what order) depends on `ITempestServiceProvider`
  (an abstraction), not on `Activator.CreateInstance` or any concrete construction
  mechanism (low-level detail) — see ADR-0007.

## Key Takeaway

SOLID is five separate, targeted answers to five separate, specific problems.
Knowing *which* problem each letter answers is what lets you recognise when a
principle actually applies to the code in front of you, versus when applying it
would just be adding structure the problem doesn't need.
