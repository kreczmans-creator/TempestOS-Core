# ADR-0006: Constructor Injection Only

## Status

Accepted — WP 2.4 (Dependency Injection), 2026-07-22.

## Context

Dependency injection containers commonly support more than one injection style:
constructor injection (dependencies passed as constructor parameters), property/
setter injection (a public property is populated after construction), and method
injection (dependencies passed as parameters to specific methods, sometimes called
at call time). `TempestServiceProvider` needed to choose which of these to
support.

## Decision

`TempestServiceProvider` supports constructor injection only. A type's public
constructor's parameters are resolved recursively and passed to it; no property or
field on a resolved instance is ever inspected or populated by the container.

Further, the constructor itself must be unambiguous: exactly one public
constructor is required. Zero public constructors and more than one public
constructor are both treated as resolution failures (`ServiceResolutionException`
and `AmbiguousConstructorException` respectively) rather than the container
guessing which one to use, or falling back to some default rule (such as "pick the
constructor with the most parameters," which `Microsoft.Extensions.DependencyInjection`
itself supports as an option).

## Consequences

**Positive:**

- **A type's dependencies are fully visible in one place: its constructor
  signature.** Reading `class Foo(IBar bar, IBaz baz)` tells a reader everything
  `Foo` needs to function, completely. Property injection hides this — a class
  could have all its real dependencies set via properties that a reader has no
  reason to look at.
- **No partially-constructed, not-yet-fully-wired objects exist.** Once a
  constructor returns, the object is complete and safe to use. Property injection
  creates a window between "object exists" and "object is actually usable" that
  every consumer has to be careful not to fall into.
- **The "exactly one public constructor" rule removes an entire category of
  non-determinism.** A container that picks "the constructor with the most
  parameters it can satisfy" behaves differently depending on what happens to be
  registered at the time — the same type can be constructed via a different
  constructor as unrelated registrations change elsewhere in the composition
  root. TempestOS's container never has to make that judgment call, because it's
  made a compile-time property of the type instead: exactly one public
  constructor, or a build-visible failure.

**Negative:**

- A type legitimately wanting two different valid ways to be constructed (for
  example, a default constructor for simple cases and a parameterised one for
  advanced configuration) cannot have both be public if it wants to participate in
  DI resolution — the second constructor would need to be `private`/`internal`
  and called from the public one, which is a real, if minor, design constraint on
  module authors.
- No support for optional constructor parameters with defaults being treated
  specially (a parameter with a default value is still resolved like any other;
  the container does not special-case "resolve if possible, otherwise use the
  default").

## Future Considerations

If a genuine need arises for a type to expose multiple valid public construction
paths for DI purposes, the correct extension is likely a way to mark one
constructor as the DI-eligible one (an attribute, for example) rather than
loosening the "exactly one" rule — which would reintroduce the non-determinism
this decision was made specifically to avoid.
