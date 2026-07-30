# Phantom-Type Dimension Safety

## 1. Introduction

`Quantity<TDimension>`/`Unit<TDimension>` (`Tempest.Core.
UnitsAndQuantities`, `WP 7.1B`) are this platform's first use of a
phantom type — a generic type parameter that carries no runtime data of
its own, existing purely so the compiler can distinguish two otherwise
identical shapes. `TDimension` never appears as a field, is never
constructed, and is never inspected at runtime; its entire job is
letting `Quantity<Length>` and `Quantity<Mass>` be different types to
the compiler, so the compiler — not a runtime check — rejects mixing
them.

## 2. Purpose

To explain the phantom-type-marker pattern as a reusable technique —
distinct from the generic types this platform already uses for storage
or dispatch (`ICalculationDefinition<TInput, TResult>`, once it exists,
uses its type parameters for real data) — and to name why this
particular safety property is worth a compile-time guarantee rather than
a runtime check.

## 3. Background

A recurring, well-documented defect class in engineering software is
combining two numbers that represent different physical dimensions
without noticing — adding a length in metres to a mass in kilograms, or
converting a length into a unit that belongs to a different dimension
entirely. A runtime check (`if (unit.Dimension != otherUnit.Dimension)
throw`) catches this, but only when the offending code path actually
executes — a phantom type catches it the moment the offending code is
*written*, before it is ever run.

## 4. The Problem

1. **How does a generic type carry "which dimension" as information the
   compiler can check, without that information costing anything at
   runtime** — no extra field, no boxing, no runtime dimension lookup?
2. **How is a dimension marker prevented from ever being instantiated**,
   since an instance of `Length` would serve no purpose and would only
   invite confusion about whether `Length` is meant to hold data?
3. **How does this interact with value-type immutability and thread
   safety**, given `Quantity<TDimension>` is a `readonly record struct`
   with no DI registration at all?

## 5. The Design

**`IDimension` is an empty marker interface.** It declares no members —
its only purpose is to be the generic constraint (`where TDimension :
IDimension`) every dimension-carrying type shares.

**Each concrete dimension is a `sealed` class with a `private`
constructor.** `Length`, `Mass`, `Duration`, `Force`, `Pressure`, `Area`,
and `Volume` each implement `IDimension` and declare only a private
constructor:

```csharp
public sealed class Length : IDimension
{
    private Length() { }
}
```

No code anywhere — including this class's own static members — ever
calls that constructor. `Length` exists purely as a type-level tag,
used only as `Quantity<Length>`'s own generic type argument. The
`private` constructor is not a formality: it documents, structurally,
that instantiation was never intended, rather than merely never
happening to occur.

**Generic constraints do the enforcement, not runtime checks.**
`Quantity<TDimension>.ConvertTo(Unit<TDimension> targetUnit)` requires
`targetUnit` to share the exact same `TDimension` as the instance it is
called on — a `Quantity<Length>` simply has no `ConvertTo` overload that
accepts a `Unit<Mass>`. The same applies to every arithmetic and
comparison operator. `CompileTimeDimensionSafetyTests.cs` documents the
exact `CS1503`/`CS0019` compiler errors produced by attempting to
violate this, verified directly, not merely asserted.

## 6. Alternatives Considered

**A runtime `Dimension` enum or string tag on `Unit`, checked at the
start of every operation.** Considered and rejected — this is exactly
the "catches the mistake only if the code path runs" weakness a phantom
type avoids. It would also cost a real field and a real check on every
operation, where the phantom-type approach costs nothing at runtime at
all.

**A single, non-generic `Quantity` type carrying a runtime dimension
tag, used uniformly for every dimension.** Considered and rejected for
the same reason — it trades a compile-time guarantee for a runtime one,
the opposite direction of the safety property this framework's own
Design Principles require ("fail loudly on incompatible dimensions" is
satisfied more completely by never compiling than by failing loudly at
run time).

## 7. Why This Solution Was Chosen

It achieves a correctness guarantee — two different physical dimensions
can never be confused — at zero runtime cost, using only C#'s existing
generic type system, with no new language feature, no source generator,
and no analyzer required. The private-constructor discipline keeps the
pattern from being misused as a place to accidentally store real data,
which would defeat its own purpose.

## 8. Architectural Principles

- **Fail Fast** — a dimension mismatch is caught at compile time, the
  earliest possible point, rather than at first run or first production
  incident.
- **Immutability** — `Quantity<TDimension>`/`Unit<TDimension>` are
  immutable value types; the phantom type itself is never instantiated,
  the strongest form of immutability available.
- **Composition Over Inheritance** — each dimension is a distinct,
  unrelated `sealed` class, not a subclass of a shared "dimension" base
  carrying real behaviour; the only shared surface is the empty
  `IDimension` marker.

## 9. Benefits

- Zero runtime cost — no field, no boxing, no dictionary lookup, no
  branch — the entire safety guarantee is erased by the compiler once
  the code compiles successfully.
- A new dimension (a future `Angle`, or a discipline-specific dimension
  once a real Mechanical/Structural/Electrical/HVAC capability is
  designed) is purely additive: one new `sealed class` and one new
  static unit catalogue, with no change to `Quantity<TDimension>`,
  `Unit<TDimension>`, or any existing dimension's own code.
- Generalises beyond units: any future framework needing "the compiler,
  not a runtime check, must prevent mixing two categories of the same
  underlying shape" can reuse this exact technique.

## 10. Trade-offs

- The guarantee is real but its *test* is unconventional: xUnit has no
  built-in "assert this does not compile" facility, so the compile-time
  guarantee is proven by direct inspection and a permanently disabled,
  documented code sample (`CompileTimeDimensionSafetyTests.cs`), not an
  automated assertion — disclosed as `AT-14` in `Technical Debt
  Register.md`, not silently glossed over.
- A phantom type carries no runtime-inspectable metadata — code cannot
  ask a `Quantity<Length>` instance "what dimension are you" beyond
  `typeof(TDimension)` reflection, which this framework never needs and
  therefore never optimises for.

## 11. Common Mistakes

The mistake most worth naming: giving a dimension marker class real
members "just in case," which immediately breaks the pattern's own
guarantee that dimension markers cost nothing and hold no state — if
`Length` ever needs to carry information, that information belongs on
`Unit<Length>` or `Quantity<Length>` instead, never on the marker type
itself.

A second, related mistake: assuming the private constructor prevents
`default(Length)` — it does not, for a reference type, `default` is
simply `null`; the guarantee this pattern provides is that `Length` is
never *meaningfully* instantiated and never carries data, not that the
type itself is unreachable by every possible reflection or default-value
mechanism.

## 12. Future Evolution

- **Dimensional algebra** (`Quantity<Length> * Quantity<Length> =>
  Quantity<Area>`) is a natural extension of this pattern but was
  deliberately out of `WP 7.1B`'s own scope — the approved contract
  only requires same-dimension arithmetic, not cross-dimension
  multiplication/division. A future Work Package could extend this
  pattern with dimension-composition operators once a real calculation
  need demonstrates it.
- **A `.NET` generic-math-based numeric backing** (rather than a fixed
  `double`), if a future engineering standard demonstrates a genuine
  need for `decimal` or another numeric representation — see
  `ADR-0054`'s own Decision 1.

## 13. Key Takeaways

1. A phantom type moves a correctness guarantee from runtime to compile
   time at zero runtime cost — worth reaching for whenever "these two
   things must never be confused" is knowable entirely from context that
   already exists at compile time.
2. The pattern is only as safe as its own discipline: a marker type must
   genuinely carry no data and never be instantiated, or it stops being
   a phantom type and starts being an ordinary, confusing class.
3. Not every guarantee this pattern produces is testable the
   conventional way — proving "this does not compile" honestly, by
   direct inspection rather than a fabricated automated check, is more
   trustworthy than pretending a shallow runtime test covers the same
   ground.
