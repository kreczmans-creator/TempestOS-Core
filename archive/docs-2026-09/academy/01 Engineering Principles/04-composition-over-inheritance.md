# Composition Over Inheritance

## What

Composition over inheritance is the preference for building behaviour by
assembling small, independent objects and delegating to them, rather than by
building a class hierarchy where subclasses inherit and extend a shared base
class's behaviour.

Inheritance ("is-a") models a fixed, single-parent relationship decided at
compile time. Composition ("has-a") assembles behaviour from parts, wired together
at construction time, that can be swapped independently of each other.

## Why

Inheritance couples a subclass to its base class's implementation, not just its
interface — a subclass can be broken by a change to its base class's internals
even if the base class's public contract didn't change (the classic "fragile base
class" problem). Deep inheritance hierarchies also force a single-dimensional
model of variation: a class can only extend *one* base class, so if a type needs
to vary along two independent dimensions (say, "how it logs" and "how it stores
data"), inheritance forces an awkward choice about which dimension becomes the
hierarchy and which has to be handled some other way. Composition sidesteps both
problems: each collaborator is independent, replaceable, and testable on its own.

## Benefits

- Behaviour can vary along multiple independent dimensions simultaneously, by
  combining different collaborators, without a combinatorial explosion of
  subclasses.
- A composed object's dependencies are visible and swappable (particularly when
  paired with dependency injection — see that principle's own document) rather
  than baked into an inheritance chain fixed at compile time.
- Testing is easier: a collaborator can be replaced with a test double without
  needing to subclass or override anything.

## Disadvantages

- Composition can require more upfront design: identifying the right
  collaborators and interfaces between them is real design work, whereas
  inheritance offers an easy, if often illusory, shortcut ("just extend the base
  class and override what's different").
- A system built entirely from composed, delegating objects can be harder to
  navigate than a shallow, well-designed inheritance hierarchy for genuinely
  "is-a" relationships that really do share a fixed, stable contract.
- Some genuine "is-a" relationships are better expressed as inheritance — forcing
  composition where inheritance is the natural fit produces awkward indirection
  for no benefit.

## When to Use

When behaviour needs to vary independently along more than one axis; when you
want to swap an implementation detail (how something logs, how something
resolves a dependency) without touching the class that uses it; when a "shared
base class" would really just be a bag of unrelated helper methods rather than a
true, stable, shared contract.

## When Not to Use

When a genuine, stable "is-a" relationship exists and the shared contract is
unlikely to fragment — TempestOS's own `Exception` hierarchies
(`ModuleDiscoveryException` → `DuplicateModuleIdException`, and the equivalent
patterns for registration and lifecycle exceptions) are inheritance used
correctly: a `DuplicateModuleIdException` genuinely *is a* `ModuleDiscoveryException`,
and the shared contract (an `Exception` with a message and, potentially, an inner
exception) is exactly as stable as `System.Exception`'s own contract.

## How TempestOS Applies It

`ModuleLifecycleManager` is composed from, not descended from, its collaborators:
it *has an* `IRuntimeModuleManager` and *has an* `ITempestServiceProvider`, both
supplied at construction and used through narrow interfaces. There is no
`ModuleLifecycleManager : RuntimeModuleManager` inheritance relationship, and there
never should be — lifecycle orchestration and module registration are not "the
same kind of thing specialised further," they are two different concerns wired
together (see the Separation of Concerns document).

`TempestServiceProvider` itself exists specifically to make composition
practical at scale: rather than every class in TempestOS manually constructing
and threading through its own collaborators (as every WP 2.1–2.3 class still does,
by design — no DI within `Tempest.Core` itself yet), WP 2.4's container lets
composition happen automatically via constructor injection, recursively, so
composed object graphs of arbitrary depth can be assembled without hand-writing
the wiring at every level.

The one place TempestOS *does* use inheritance — the exception hierarchies
(`ModuleDiscoveryException`, `ModuleRegistrationException`,
`ModuleLifecycleException`, `ServiceResolutionException`, each with focused
subtypes) — is a deliberate, correct use of it: these are genuine "is-a"
relationships over a contract (`System.Exception`) that is not going to change
shape, and callers benefit from being able to `catch` the base type to handle a
whole category of related failures at once.

## Key Takeaway

The question is never "inheritance or composition, always" — it's "does this
relationship actually need to vary along more than one dimension, and is the
shared contract genuinely stable?" TempestOS's exception hierarchies answer "no,
it's stable" and use inheritance; its runtime services answer "yes, it varies" and
use composition.
