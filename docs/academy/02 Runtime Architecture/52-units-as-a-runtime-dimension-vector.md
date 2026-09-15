# Units as a Runtime Dimension Vector

**Release:** `v0.17.0` · **Work Package(s):** `WP 17.3A` ·
**Decision:** `ADR-0147` (reverses part of `ADR-0054`; extends
`ADR-0125` from the Group A programme) ·
**Code:** `Tempest.Core.UnitsAndQuantities`

**In plain terms.** Every number an engineer enters into TempestOS —
a length, a load, a temperature — has to carry its unit with it, or a
calculation is meaningless. Two mistakes are possible: adding numbers
that are not the same *kind* of thing at all (a length plus a mass),
and adding numbers of the same kind written in different units (3
metres and 200 centimetres) without converting one first. TempestOS
stops the first kind before the software is even built, so it can
never reach a user in the finished product, and it now handles the
second kind by converting automatically and correctly instead of
making the engineer do it by hand. The same work taught the software
to handle temperature properly and to work out, on the fly, that a
force divided by an area is a pressure — and proved all of it with
tens of thousands of randomly generated numbers.

## Two ways to stop a wrong addition

"3 m + 2 kg" can be refused at exactly two points: when the code is
compiled, before it ever runs, or when the two numbers actually meet
at run time. TempestOS's original design (`WP 7.1B`, `v0.7.0`,
`ADR-0054`) chose the first, using a **phantom type** — a type that
exists purely so the compiler can tell two things apart, never to hold
data. `Length` and `Mass` are distinct C# types, so `Quantity<Length>`
and `Quantity<Mass>` are different types to the compiler, and no `+`
operator accepts one where the other is expected. This pattern is
covered in full in `04 Design Patterns/05-phantom-type-dimension-safety.md`,
which this chapter builds on rather than repeats. A phantom type buys
a guarantee no runtime check can match, but it cannot decide anything
only knowable *while the program runs* — and two such needs turned up
as this platform's calculations matured.

## What the original design honestly could not do

`ADR-0147`'s own Context names two walls, found by reading the
consuming calculation code directly. **No runtime dimensional
analysis:** `TDimension` is erased by the compiler once code compiles
— that is the whole point of the phantom type — so nothing at run time
can discover that a force divided by an area is a pressure; a
calculation deriving one quantity from two others had to already know,
by hand, which marker type the result belonged to. **The exact-unit
rule was ceremony, not safety:** `ADR-0054` required the *exact same*
`Unit<TDimension>` on both sides of `+`, `-` and every comparison — 5 m
and 500 cm could not be added without an explicit `ConvertTo` first,
and were not even equal by `==`. `TDimension` already guarantees the
two values are the same kind of thing at compile time; refusing to
combine 5 m and 500 cm anyway added typing without adding a safety net
a determined caller could not already sidestep by converting first.

`ADR-0054` was not wrong to defer this — it was a reasonable default
before anything had used the framework in anger. Real calculation code
showed the rule was a cost with nothing behind it, and that correction
is the most honest part of this chapter.

## A vector that discovers pressure, not a catalogue that names it

The fix for the first wall is a runtime `Dimension`: seven whole-number
exponents, one per SI base quantity (length, mass, time, temperature,
electric current, amount of substance, luminous intensity). Force is
`L¹ M¹ T⁻²`; multiplying two dimensions adds their exponents, dividing
subtracts them. A new, non-generic `Quantity` (the class lives in
`QuantityVector.cs`, kept apart from the generic `Quantity<TDimension>`
in `Quantity.cs` only by file name) carries one of these vectors
alongside its value, so `*`/`/` can compose a dimension no catalogue
ever named in advance:

```csharp
var unit = new UnitDefinition(
    ComposeSymbol(left.Unit.Symbol, right.Unit.Symbol, '.'),
    left.Unit.Dimension * right.Unit.Dimension,
    left.Unit.ToBaseFactor * right.Unit.ToBaseFactor);

return new Quantity(left.Value * right.Value, unit);
```

Three kilonewtons times two metres becomes six kilonewton-metres, with
the true SI-coherent value always recoverable through `BaseValue`.
Every marker type — the twenty-two that already existed, plus eight
new ones — now declares its own vector through one new member,
`static abstract Dimension Vector` on `IDimension`. That single fact
lets a value cross from the compile-time-checked generic world into
the runtime-checked one and back: `Quantity<TDimension>.ToQuantity()`
always succeeds, and `FromQuantity(quantity)` checks the vector matches
before admitting a value back into the typed facade, throwing
`IncompatibleUnitsException` otherwise.

## The rule ADR-0147 calls a reversal

`ADR-0054`'s own Status line now reads "superseded in part" — only its
exact-unit rule is gone; `double` values, no DI registration and
`IUnitConverter` stand untouched. `ADR-0147` is blunter about the one
decision it did change: it says outright that the rule "is
**reversed**". `+`, `-`, every comparison operator and equality now
convert automatically through the base unit — for both the new
`Quantity` and the existing `Quantity<TDimension>` — so 5 m and 500 cm
are equal and `5 m + 500 cm` returns `10 m` with no `ConvertTo` call.
`IncompatibleUnitsException` can no longer come from a unit mismatch at
all; it is now reserved for the one thing units genuinely cannot allow,
affine arithmetic.

## Affine units, and the one thing worth adding to them

`ADR-0125` (Group A, the reference-data programme this release also
carries) had already given this framework degrees Celsius and
Fahrenheit, by adding an offset alongside the existing multiplicative
factor:

```csharp
public double ToBase(double value) => (value * ToBaseFactor) + ToBaseOffset;
public double FromBase(double baseValue) => (baseValue - ToBaseOffset) / ToBaseFactor;
```

It also refused all arithmetic on an affine value — twenty degrees
Celsius plus five degrees Celsius is not twenty-five degrees of
anything, since neither shares a common zero. That refusal stands.
`ADR-0147` adds the one operation that *is* physically meaningful:
subtracting two absolute temperatures now yields a genuine difference,
in a new `TemperatureDelta` dimension (kelvin- and degree-sized units,
each a pure scale factor, no offset), and adding that delta back is
permitted. Giving `TemperatureDelta` its own exponent vector was
considered and rejected as unprincipled special-casing — a temperature
interval and a temperature position are dimensionally identical, and
the two stay apart by name only, the way `Torque`/`Energy` and
`SectionModulus`/`Volume` already share one vector under two names.

## Eight new dimensions

`SecondMomentOfArea` (L⁴) and `SectionModulus` (L³) let a structural
calculation record two figures that previously had nowhere to go but a
bare `double`. `Frequency` (T⁻¹) joins `RotationalSpeed`, sharing its
vector but keeping a separate name, and `TemperatureDelta` completes
the temperature work above. An electrical set — `ElectricCurrent`
(this platform's first use of the seventh SI base quantity), `Voltage`,
`Resistance`, `ElectricCharge` — brings the total to thirty dimensions,
each with its own marker type and catalogue, none changing how any
existing dimension behaves.

## Proving it with random numbers, not a handful of examples

An ordinary test checks one example — 5 m converts to 500 cm, and no
more. A **property-based test** states a rule that must hold for
*every* input and lets the test framework generate hundreds of
different ones looking for a counter-example. `WP 17.3A` adopted
`CsCheck` for the property that matters most here and that no fixed
example can pin down: a calculation's answer must not depend on which
unit its inputs happened to be typed in.

`PropertyTests.cs` runs the conversion property against all thirty
dimensions' own catalogues — a value converted to a random unit and
back recovers the original within a relative tolerance of `1e-12`.
`CalculationPropertyTests.cs` takes the same idea to the six
calculation definitions themselves, building one physical value twice,
in two different randomly chosen units:

```csharp
private static Quantity<TDimension> At<TDimension>(double baseValue, Unit<TDimension> unit)
    where TDimension : IDimension =>
    new(unit.FromBase(baseValue), unit);
```

Running a calculation against both and checking the results agree
(within `1e-9`, the tolerance the bracket check already used for
floating-point rounding) is "the answer does not change when I change
the input units," made into code, for all six calculations this
platform has. A seventh property checks a qualitative fact instead: the
bracket check's stress margin must strictly decrease as the applied
load increases, at every input combination generated. These
thirty-seven tests took the Core suite from 5,159 to 5,196, with zero
failures and a clean, warnings-as-errors build.

## What changed for a developer, and what did not

Test projects now reference `CsCheck` (pinned to `4.8.0`); build and
test commands are otherwise unchanged. `Tempest.Core.Calculations`
needed no code changes at all — every calculation already converted to
a known unit immediately (`.ConvertTo`, `.BaseValue`), so the reversed
rule and the new dimensions are both invisible to it, which is exactly
what `CalculationPropertyTests.cs` exists to turn into evidence rather
than assertion. `Quantity<TDimension>`/`Unit<TDimension>` stay exactly
where they were, gaining only the `ToQuantity()`/`FromQuantity()`
bridge, and every existing consumer compiles unchanged. One disclosed
behavioural change: equality is now base-value equality rather than
field-by-field equality, so 5 m and 500 cm are now the same dictionary
key where they previously were not.

## What was deliberately not built

`UnitsNet`, the most widely used .NET units library, was evaluated and
rejected: its quantity types are sealed, with no extension point for a
`ReferencePin` (`ADR-0125`, `ADR-0143`) — the mechanism keeping a
stress figure tied to the exact reference-data revision it came from.
Wrapping every quantity in a second carrier type would have been a
larger, less coherent design than finishing the runtime vector this
platform already had half of. A canonical base-unit-symbol registry,
so a composed unit like `kN.m` always displayed as `N.m`, was rejected
as disproportionate — `BaseValue` already recovers the correct
magnitude unconditionally; only a cosmetic label is ever affected.

## What to take away

**A rule with no example behind it is a cost, not a safeguard** — the
exact-unit rule looked principled until real calculation code showed it
only added `ConvertTo` calls no one needed. **A compile-time guarantee
and a run-time one can serve the same feature without competing** —
dimensional safety still needs the phantom type; discovering a new
dimension from two others needs a runtime vector, and this Work Package
kept both. **State the property, not the example** — thirty-seven tests
asking "does the answer change if I change the units" caught what no
hand-picked set of conversions ever could.

Declared engineering figures (`59-evidence.md`, forthcoming) are
recorded as this same `Quantity` type; `13-calculation-framework.md`
named cross-dimension multiplication as a future capability back in
`WP 7.1B` — this is the Work Package where it arrived.
