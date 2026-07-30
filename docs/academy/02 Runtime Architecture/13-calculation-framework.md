# Calculation Framework

## 1. Introduction

`ICalculationEngine`/`ICalculationDefinition<TInput, TResult>`
(`Tempest.Core.Calculations`, `WP 7.1D`) is the Command Framework's own
closest structural relative in this platform — both register something
by Id during module initialisation and dispatch it later by that same
Id — and also its clearest counter-example. A command is an imperative
action; a calculation is a pure function producing engineering evidence.
This document exists specifically to keep those two ideas from being
conflated, mirroring this project's own repeated practice of explicitly
distinguishing structurally similar pairs (Command Framework vs. Event
Bus; Notifications vs. Event Bus, both in `08-failure-isolation.md`).

## 2. Purpose

To explain why TempestOS has two registries that look alike from a
distance — `ICommandRegistry` and `ICalculationEngine` — and to name,
precisely, the one property that makes them fundamentally different
abstractions rather than two names for the same idea.

## 3. Background — Why a Second Registry, Not a Reused One

`11-command-framework.md` already explains why a command is not simply
"a function call" — it carries a `CommandResult` (succeeded or failed,
with a message), and its own handler is permitted to have side effects
(write to a file, call a Platform Service, mutate state). A calculation
needs the opposite guarantee: given the same input, `Calculate` must
always produce the same output, with no side effect at all beyond what
it explicitly records through its own `CalculationContext`. Reusing
`ICommandRegistry` for calculations would have meant either weakening
its own contract (permitting non-deterministic, side-effecting
"commands") or silently trusting authors to keep a subset of commands
pure by convention alone, with nothing in the type system saying so.

## 4. The Problem

1. **How does a caller know, from the type system alone, that a
   registered thing is safe to call concurrently, retry, or reason about
   without side effects** — a guarantee `ICommand`/`ICommandHandler`
   deliberately does not offer, since a command's very purpose is to
   perform an action?
2. **How does an engineering calculation carry its own assumptions,
   constraints, and intermediate steps** — a `CommandResult`'s own
   succeeded/failed-plus-message shape has nowhere to put any of these?
3. **How does a calculation's own result become durable, traceable
   evidence** rather than a value that exists only for the duration of
   one dispatch call?

## 5. The Design

`ICalculationDefinition<TInput, TResult>.Calculate(TInput, CalculationContext)`
is documented, and tested, as a pure function — no I/O, no shared
mutable state. `CalculationContext` is the one side channel available,
itself pure in effect (a fresh, non-shared recorder the engine
constructs once per execution and reads back immediately after). Every
execution is durably recorded as an `EngineeringData.IEngineeringDocument`
— giving the resulting `CalculationRecord<TResult>` a stable identity,
genuine revision capability, and a self-contained copy of the producing
definition's own assumptions, so the record remains meaningful evidence
even if the original definition is no longer registered. See
`ADR-0056` for the complete design.

## 6. Alternatives Considered

**Reusing `ICommand`/`ICommandHandler` for calculations, distinguishing
them only by naming convention** — considered and rejected; see
Background, above. Nothing in the type system would have prevented a
"calculation command" from performing I/O, and nothing would have
attached assumptions, constraints, or a validation outcome to a
`CommandResult`.

**A calculation as merely a typed function delegate
(`Func<TInput, TResult>`), registered directly** — considered and
rejected. A bare delegate carries no `CalculationId`, no
`CalculationMetadata`, and no way to receive a `CalculationContext` —
every capability this framework's own "engineering evidence, not merely
a numerical answer" requirement demands would need to be bolted on
separately, effectively reinventing `ICalculationDefinition` anyway.

## 7. Why This Solution Was Chosen

It gives calculations exactly the guarantee commands deliberately do
not offer — safe, interference-free concurrent execution — while
reusing every proven mechanism already available (the Engineering Data
Model's own storage and revisioning, the type-erased registration
pattern the Command Framework already established) rather than
inventing new infrastructure for problems this platform has already
solved once.

## 8. Architectural Principles

- **Single Responsibility Principle** — a calculation computes; it does
  not dispatch commands, format reports, or perform I/O.
- **Deterministic Systems** — the same input always produces the same
  output, proven directly by a concurrency test, not merely documented.
- **Composition Over Inheritance** — `CalculationContext` is composed
  into `Calculate`'s own signature as a parameter, not inherited
  behaviour from a base class.

## 9. Benefits

- A calculation's own purity is what makes concurrent execution of the
  same Id, with different inputs, safe without any additional
  synchronization — a genuine, tested architectural benefit, not merely
  an assumption.
- Every calculation's own assumptions and constraints are explicit and
  attached to its own result automatically — a future reader of a
  calculation record never needs to trust that "the formula used was
  probably reasonable," since the record states what was assumed
  directly.
- Reusing the Engineering Data Model for storage means Calculation
  needed no new persistence mechanism, no new revision model, and no
  new identity scheme.

## 10. Trade-offs

- `Calculate` carries no `CancellationToken` — a long-running
  calculation cannot be cancelled once dispatched (`TD-21`), a cost
  accepted because calculation definitions remain trusted, first-party,
  in-process code today.
- `CalculationContext`'s own recorded intermediate values are not
  guaranteed to survive a durable round-trip back to their exact
  original CLR type (`TD-22`) — fully inspectable from the in-memory
  record returned immediately, not necessarily from storage read back
  later.

## 11. Common Mistakes

The mistake most worth naming: registering a calculation whose
`Calculate` method performs a side effect (writes a file, calls another
Platform Service, mutates a field outside `CalculationContext`) because
it "only runs once." Nothing prevents this at compile time — the purity
requirement is a documented convention, not a compiler-enforced
guarantee (`ADR-0056`) — and violating it silently reintroduces exactly
the concurrent-execution risk the whole design exists to avoid.

A second mistake: treating `CalculationInputInvalidException` and a
`Conditional` validation outcome as interchangeable. They are not — an
input invalid enough to make the result meaningless should be a thrown
exception (no record created at all); a `Conditional` outcome is for a
real, returned result that still carries an unmet advisory constraint.

## 12. Future Evolution

- **Execution cancellation** (`FCR-0035`, `TD-21`), once a real,
  long-running calculation or an externally-facing caller demonstrates
  the need.
- **Dimensional algebra** (`Quantity<Length> * Quantity<Length> =>
  Quantity<Area>`), named as a `WP 7.1B` Future Capability
  Recommendation, would let a calculation derive a dimensioned
  intermediate result directly rather than constructing one by hand.

## 13. Key Takeaways

1. Two registries that look alike from their own public shape
   (register-by-Id, dispatch-by-Id) can still be fundamentally
   different abstractions — the deciding property here is purity, not
   registration mechanics.
2. A framework's own evidentiary requirements ("represents engineering
   evidence, not merely a numerical answer") can justify extending an
   approved contract's own illustrative shape substantially, provided
   every extension is additive to what was shown, not a silent change
   to it.
3. Reusing already-proven infrastructure (the Engineering Data Model,
   the Command Framework's own type-erased dispatch pattern) for a new
   framework's own storage and registration needs is worth attempting
   before inventing anything new — both were reused here successfully.

## Related Documents

`11-command-framework.md` (the closest structural precedent, and this
guide's own primary point of comparison); `08-failure-isolation.md`
(this project's own repeated practice of distinguishing structurally
similar pairs); `ADR-0056`; `docs/academy/03 Work Packages/
WP7.1D-engineering-calculation-framework-implementation.md`.
