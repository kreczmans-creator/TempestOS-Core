# ADR-0147: Units Are a Runtime Dimension Vector, and the Generic `Quantity<TDimension>` Is a Typed Facade Over It

## Status

Accepted — `WP 17.3A` (Units as a dimension vector), 2026-09-08.

## Context

`ADR-0054` gave this platform a compile-time-safe unit system:
`Quantity<TDimension>`/`Unit<TDimension>`, where `TDimension` is a
non-instantiable marker type (`Length`, `Mass`, `Force`, and so on). It
works exactly as designed for every calculation this platform has written
so far — `EngineeringCalculationDefinitions.cs` and
`BracketSectionCheck.cs` both convert every input to a known unit
immediately (`.ConvertTo(...)`, `.BaseValue`) and compute in plain
`double` from there.

Three gaps remained, named by `WP 17.3A`'s own controlling Work Package
row and confirmed by reading that consuming code directly:

1. **No runtime dimensional analysis.** `TDimension` is erased at compile
   time by design — that is the whole point of `ADR-0054`'s phantom-typed
   safety — but it means nothing in this framework can discover at run
   time that a force divided by an area is a pressure. A calculation
   wanting to derive a new physical quantity from two others must already
   know, at the call site, which marker type the result belongs to; there
   is no `Quantity<Force> / Quantity<Area> -> Quantity<Pressure>`, because
   the generic operators are defined only within one `TDimension`.
2. **The exact-same-unit rule was ceremony without safety.**
   `Quantity<TDimension>`'s own arithmetic and comparison operators
   require the exact same `Unit<TDimension>` on both operands — 5 m and
   500 cm cannot be added without an explicit `ConvertTo` first, and are
   not even equal by `==`. `TDimension` already guarantees dimensional
   compatibility at compile time; refusing to combine two quantities that
   are unambiguously the same *kind* of thing, merely because a caller
   wrote one in metres and the other in centimetres, forces
   ceremony (`ConvertTo` calls scattered through otherwise-simple
   arithmetic) that adds no safety a determined caller could not already
   bypass by converting first.
3. **Missing derived dimensions and no affine-delta path.** This
   platform's structural calculations need a second moment of area and a
   section modulus and have never been able to record either as anything
   but a bare `double`; nothing records frequency, or an electrical
   quantity, as a `Quantity` at all; and `ADR-0125`'s own affine-unit
   design (Celsius/Fahrenheit) has no way to express *subtracting* two
   absolute temperatures, which is the one operation on them that
   genuinely has a physical meaning (a temperature difference).

`UnitsNet`, the most widely used .NET units library, was evaluated as an
alternative to building any of this. It was not adopted: `UnitsNet`'s own
quantity types cannot carry a `ReferencePin` (`ADR-0125`, `ADR-0143`) —
they are sealed value types with no extension point for this platform's
own reproducibility requirement, that a stress figure derived from a
pinned reference-data revision carries that pin with it rather than
losing it at the first arithmetic operation. Adopting `UnitsNet` would
have meant wrapping every quantity in a second type to carry the pin
back, which is a larger and less coherent design than building the
runtime dimension vector this platform already had half of.

## Decision

**1. `Dimension` is a runtime vector of seven `sbyte` exponents**
(`Length`, `Mass`, `Time`, `Temperature`, `ElectricCurrent`,
`AmountOfSubstance`, `LuminousIntensity`, in ISO 80000/SI order), with
`*`/`/` combining two dimensions the way multiplying/dividing their
quantities does, `Pow(int)`, `IsDimensionless`, record-structure equality,
and a stable `ToString` (`"L^1 M^1 T^-2"` for force). The named vector for
every dimension this platform holds — existing and new — lives in a
companion static class, `Dimensions`, rather than as static members on
`Dimension` itself: C# does not allow an instance member and a static
member to share a name on one type, and three of the vectors
(`Length`, `Mass`, `Temperature`) would collide with the identically-named
instance exponent otherwise. This mirrors the marker-type/catalogue split
`ADR-0054` already established (`Length` the marker type,
`LengthUnits` the catalogue).

Two names can share the exact same vector deliberately.
`Dimensions.Stress` is a pure alias of `Dimensions.Pressure`;
`Dimensions.Torque` and `Dimensions.TorsionalStiffness` share
`Dimensions.Energy`'s own vector; `Dimensions.RotationalSpeed` and the new
`Dimensions.Frequency` are both T⁻¹. The vector alone cannot, and is not
meant to, keep a torque from a torsional stiffness — that separation is
what the compile-time generic facade is still for.

**2. `IDimension` gains `static abstract Dimension Vector { get; }`.**
Every marker type (all twenty-two existing ones, plus the eight this Work
Package adds) now declares its own runtime vector. This is the one fact
that lets a value cross from the compile-time-checked generic world to
the runtime-checked non-generic one and back — see Decision 4.

**3. A non-generic `Quantity` (readonly record struct: `double Value`,
`UnitDefinition Unit`) carries its own `Dimension` at run time**, via a
new `UnitDefinition` type (`Symbol`, `Dimension`, `ToBaseFactor`,
`ToBaseOffset` — the runtime-dimensioned counterpart to
`Unit<TDimension>`). Its own semantics:

- **Same-dimension automatic conversion.** `+`, `-`, every comparison
  operator, and equality between two quantities of the *same* dimension
  convert automatically — through the base unit, and for `+`/`-` back to
  the left operand's own unit. Two quantities of *different* dimensions
  throw `IncompatibleUnitsException` for every one of these, including
  equality — a deliberate departure from the ordinary `Equals` contract
  (see Consequences).
- **`*`/`/` combine dimensions.** The result's own unit is the literal
  product/quotient of the two operand units, factor and symbol both
  composed (`3 kN` times `2 m` is `6 kN.m`). When both operands are
  already expressed in their own dimension's base unit — the common case
  — the composed unit already *is* the derived base unit, and the
  composed symbol names it exactly (a force in newtons over an area in
  square metres composes to `N/m^2`). `BaseValue` always recovers the
  true SI-coherent magnitude regardless of which case applied.
- **Affine temperature, extended.** An absolute temperature (Celsius,
  Fahrenheit) still cannot be added, subtracted, or scaled directly
  (`ADR-0125`'s own refusal, unchanged) — except: subtracting two
  temperatures where at least one is affine now yields a genuine
  temperature *difference*, expressed in a new `TemperatureDelta`
  dimension (same vector as `Temperature`, Θ) via `TemperatureDeltaUnits`
  (`Kelvin`, `CelsiusDegree`, `FahrenheitDegree` — every one a pure scale
  factor, no offset, because an interval has no zero point to place); and
  adding that delta back onto an absolute temperature is permitted. Where
  neither operand is affine (kelvin, degrees Rankine, or a delta unit —
  every one zero-offset), ordinary arithmetic applies with no special
  casing, exactly matching `Quantity<TDimension>`'s own long-standing
  "kelvin arithmetic is permitted" behaviour.
- JSON round-trips through System.Text.Json (`UnitDefinition` serialises
  symbol, dimension exponents, and both factors — deterministic).
  `ToString` is culture-invariant. `Parse`/`TryParse` work over a supplied
  known-units list, exactly as `Quantity<TDimension>`'s own already do.

**4. `Quantity<TDimension>`/`Unit<TDimension>` remain, as a compile-time
typed facade.** `Unit<TDimension>` gains a `UnitDefinition` property
(symbol and factors, plus `TDimension.Vector`). `Quantity<TDimension>`
gains `ToQuantity()` (always succeeds) and a static `FromQuantity(Quantity)`
that throws `IncompatibleUnitsException` if the runtime dimension does not
equal `TDimension.Vector`. Every existing `XxxUnits` catalogue is
unchanged. `Quantity<TDimension>`'s own arithmetic and comparison
operators adopt the identical same-dimension-automatic-conversion rule as
the non-generic type (Decision 5), and every existing consumer — the six
calculation definitions included — compiles unchanged, because neither
converts explicitly today in a way this widening breaks.

**5. `ADR-0054`'s exact-same-unit rule is reversed for
`Quantity<TDimension>` too.** `+`, `-`, `<`, `>`, `<=`, `>=`, `==`, and
`!=` now convert automatically between units of the same
`TDimension` — 5 m and 500 cm are equal, and `5 m + 500 cm` returns `10 m`
with no explicit `ConvertTo` first. `IncompatibleUnitsException` is no
longer reachable from these operators for a unit mismatch — `TDimension`
itself makes that case impossible at compile time — and now guards only
the affine-arithmetic refusal `ADR-0125` already established. The three
tests that pinned the old rule (`Addition_DifferentUnits_ThrowsIncompatibleUnitsException`,
`Subtraction_DifferentUnits_ThrowsIncompatibleUnitsException`,
`CompareTo_DifferentUnits_ThrowsIncompatibleUnitsException`, and
`Equality_PhysicallyEquivalentButDifferentUnit_AreNotEqual`) are rewritten
to assert the new one; one further test making the identical assertion
for `RotationalSpeed` (`BothNewDimensions_RefuseImplicitConversionInArithmeticLikeEveryOther`)
is rewritten the same way.

**6. Eight new dimensions, each with its own marker type, catalogue, and
`Dimensions` vector:** `SecondMomentOfArea` (L⁴), `SectionModulus` (L³,
deliberately distinct from `Volume` despite the identical vector, in the
same spirit as `Torque`/`Energy`), `Frequency` (T⁻¹, distinct from
`RotationalSpeed`), `TemperatureDelta` (Θ, distinct from `Temperature`),
and an electrical set — `ElectricCurrent` (I, the seventh SI base
quantity this platform had not yet needed), `Voltage`, `Resistance`,
`ElectricCharge`.

**7. Property-based tests (CsCheck) replace hand-picked examples for the
claims that matter most.** `PropertyTests.cs` asserts, for every unit pair
within every dimension this platform holds, that converting from one to
the other and back recovers the original value within a relative
tolerance of 1e-12. `CalculationPropertyTests.cs` asserts, for each of the
six calculation definitions, that its result is invariant under an input
being re-expressed in a different unit of the same dimension (relative
tolerance 1e-9, matching `BracketSectionCheckCalculationDefinition.AcceptanceRelativeTolerance`'s
own already-established figure for this exact class of floating-point
concern), and that the bracket check's own stress margin is strictly
monotonically decreasing as the applied load increases.

## Consequences

### Positive

- A calculation can now derive a genuinely new physical quantity at run
  time (`force / area`) without a human having pre-declared, in a
  catalogue, that force-over-area is a pressure — the runtime `Dimension`
  vector discovers it from first principles every time.
- The exact-same-unit ceremony is gone from both the generic facade and
  the new non-generic type, for every dimension, with no per-dimension
  special-casing.
- A structural calculation can record a second moment of area or a
  section modulus as a `Quantity` rather than a bare `double` for the
  first time.
- Subtracting two temperatures now produces a value with a name
  (`TemperatureDelta`) instead of either throwing or silently returning a
  number in a dimension that does not describe what it is.
- Every existing consumer of `Quantity<TDimension>`/`Unit<TDimension>`,
  including all six calculation definitions, compiles and passes
  unchanged.

### Negative

- The non-generic `Quantity`'s own `==`/`!=`/`Equals` can throw
  `IncompatibleUnitsException` for a cross-dimension comparison — a
  deliberate continuation of this framework's "fail loudly rather than
  return a value that looks like an answer" discipline (`ADR-0125`), but
  a genuine departure from the ordinary `object.Equals` contract, and it
  makes `Quantity` unsafe as an unconstrained `Dictionary`/`HashSet` key
  across mixed dimensions. `GetHashCode` includes the dimension precisely
  so a hash collision cannot silently paper over this.
- `Quantity` `*`/`/`'s composed symbol names the units the operands were
  actually expressed in, not necessarily each dimension's own canonical
  base unit — `3 kN` times `2 m` reports `kN.m`, not `N.m`. `BaseValue`
  is always correct regardless; only the display symbol can look
  unfamiliar for a non-base input. Building a canonical base-unit-name
  registry for arbitrary derived dimensions was considered and rejected
  (Alternatives Considered) as disproportionate to the problem.
- `Quantity<TDimension>` and `Unit<TDimension>` equality is now
  base-value equality rather than field-by-field structural equality —
  5 m and 500 cm are now the same key in a dictionary keyed on
  `Quantity<Length>`, where they previously were not. No production code
  in this repository relies on the old distinction; this is disclosed as
  a genuine, if narrow, behavioural change to already-shipped types.

### Neutral

- `Tempest.Core.Calculations` needed no changes at all: every existing
  calculation already converts to a known unit immediately
  (`.ConvertTo`, `.BaseValue`) rather than relying on the exact-same-unit
  rule, so the reversed rule and the new dimensions are both invisible
  to it. `CalculationPropertyTests.cs` exists precisely to make this
  claim evidence rather than assertion.

## Alternatives Considered

**Adopting `UnitsNet`.** Rejected — see Context. Its quantity types have
no extension point for a `ReferencePin`, and wrapping every quantity in a
second carrier type to hold the pin back is a larger, less coherent
design than the runtime vector this platform already had the compile-time
half of.

**Keeping the exact-same-unit rule and adding an explicit `Quantity.Add`
helper that converts.** Rejected. This is what `ConvertTo` already is;
adding a second spelling of the same operation under a different name
solves nothing `ConvertTo` did not already solve, and leaves `+`/`-`/`==`
exactly as ceremonious as they were.

**A canonical base-unit-symbol registry, so `*`/`/`'s composed symbol
always names the true SI-coherent unit regardless of the operands'
own units.** Rejected as disproportionate. It would require maintaining a
lookup from every possible `Dimension` vector to a "canonical" display
name — effectively re-deriving `Dimensions`' own named catalogue in
reverse, for every dimension a caller might ever construct by
multiplying or dividing two others, most of which this platform will
never name. `BaseValue` already recovers the correct magnitude
unconditionally; only the cosmetic label is affected, and it is affected
only when an operand was not already in its own base unit.

**Giving `TemperatureDelta` a distinct `Dimension` vector from
`Temperature`.** Rejected. A temperature interval and a temperature
position are dimensionally identical (both Θ¹) under any physically
honest analysis; inventing a fictional eighth exponent to keep them
apart at the vector level would be exactly the kind of unprincipled
special-casing this framework's phantom-typed dimensions exist to avoid.
The separation belongs at the marker-type/unit-family level, which is
where `Torque`/`Energy` and `SectionModulus`/`Volume` already keep their
own dimensionally-identical pairs apart.

## Related Documents

`ADR-0054` (superseded in part by this decision — see its own Status
line); `ADR-0125` (affine units; extended, not superseded, by this
decision's `TemperatureDelta`); `ADR-0143` (`ReferencePin` — the
requirement `UnitsNet` could not satisfy); `docs/releases/v1.0.0/WorkPackages.md`
(`WP 17.3A` row); `docs/governance/Architecture/ADR Register.md`.
