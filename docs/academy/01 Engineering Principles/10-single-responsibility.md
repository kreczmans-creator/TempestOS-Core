# Single Responsibility Principle

## What

The Single Responsibility Principle (SRP) states that a class should have one,
and only one, reason to change. "Responsibility," in Robert C. Martin's original
formulation, means "a reason to change" — not "one method" or "one line of code."
A class can have many methods and still satisfy SRP, as long as all of them serve
the same single reason the class exists, and none of them would need to change
for some *other*, unrelated reason.

SRP gets its own document, separate from the broader SOLID overview, because it
is the principle most directly responsible for TempestOS's overall shape: the
entire Discovery → Registration → Lifecycle → Dependency Injection pipeline is
SRP applied at the level of whole subsystems, not just individual classes.

## Why

A class with more than one responsibility has more than one reason to change —
which means a change motivated by one responsibility risks affecting, breaking,
or requiring retesting of code that serves an entirely different, unrelated
responsibility, purely because they happen to live in the same class. It also
makes the class harder to understand, since a reader has to hold multiple,
unrelated concerns in mind simultaneously to follow what any given method is
doing and why.

## Benefits

- Changes are localised: a change to *how modules are discovered* cannot break
  *how modules are registered*, because they are different classes with
  different, single responsibilities, and no code path allows one's change to
  silently affect the other's behaviour.
- Classes are individually easier to understand, test, and name well — a class
  that does one thing has an obvious, accurate name for what it does.
- Responsibilities can be recombined or replaced independently: a different
  discovery strategy can be swapped in without touching registration, lifecycle,
  or the service provider, because none of them depend on discovery's internals.

## Disadvantages

- Over-applying SRP can fragment genuinely cohesive logic into too many tiny
  pieces, each individually simple but collectively harder to follow as a whole
  — SRP is about *reasons to change*, not about minimising line count per class.
- Determining the "right" granularity of responsibility is a judgment call, not
  a mechanical rule — reasonable engineers can disagree about whether two
  behaviours are "one responsibility" or "two," and getting it wrong in either
  direction (too coarse, too fine) has real costs.

## When to Use

Whenever a class currently serves, or is at risk of growing into serving,
multiple genuinely independent concerns — especially when those concerns have
different *rates* of change, different owners, or different testing needs.

## When Not to Use

When a set of behaviours, while nominally "more than one thing," always changes
together for the same underlying reason and would gain nothing from being split
— artificially separating them just adds indirection between pieces that were
never actually independent.

## How TempestOS Applies It

SRP is the literal organising principle behind TempestOS's entire module
pipeline. WP 2.4's brief made this explicit, in writing, as a fixed set of
responsibilities before any code was written for that work package:

> Discovery Service — discovers modules. Runtime Module Manager — registers
> modules. Lifecycle Manager — orchestrates modules. Service Provider — creates
> module instances.

Each class in this pipeline has exactly one reason to change:

- `ReflectionFrameworkDiscoveryService` changes only if *how modules are found*
  changes.
- `RuntimeModuleManager` changes only if *how the runtime catalogues known
  modules* changes.
- `ModuleLifecycleManager` changes only if *how modules are started, stopped, and
  torn down* changes.
- `TempestServiceProvider` changes only if *how instances are constructed and
  wired together* changes.

This is why, across four separate work packages, none of WP 2.1 through WP 2.4
ever required modifying a *previous* work package's core class to add the next
piece of behaviour — WP 2.2 didn't need to change `ReflectionFrameworkDiscoveryService`;
WP 2.3 didn't need to change `RuntimeModuleManager`; WP 2.4's entire, sole
production-code change to prior work was one narrowly-scoped edit to
`ModuleLifecycleManager` (replacing its one `Activator.CreateInstance` call site
with a service-provider call) — precisely because construction was always that
class's stated responsibility, and DI is simply a different *mechanism* for
fulfilling the same, unchanged responsibility.

`IModule` and `IModuleLifecycle` being two separate interfaces (rather than one)
is SRP applied at the interface level: "describing what a module is"
(`Id`/`Name`/`Version`) is a different responsibility from "describing what a
module does when its lifecycle is driven" (`Initialise`/`Start`/`Stop`/`Dispose`),
and a module that has no lifecycle behaviour genuinely has no reason to depend on
the second responsibility at all.

## Key Takeaway

SRP, applied consistently across an entire pipeline rather than only within
individual classes, is what let four separate work packages each add
significant new capability to TempestOS without ever needing to reopen and
modify the work that came before them.
