# ADR-0054: Units & Quantities — Representation, Precision, and Registration Model

## Status

Accepted — `WP 7.1B` (Units & Quantities Framework), 2026-07-30.

## Context

`WP7.0C Required ADR Catalogue.md` reserved three questions for this
Work Package: whether `Quantity<TDimension>`/`Unit<TDimension>` should be
`double`-backed or `decimal`-backed; whether the no-DI-registration design
should be confirmed; and whether `IUnitConverter` should be built at all,
given its own proposed triviality. `WP7.0C Engineering Foundation
Contracts.md` proposed `double` and no DI registration as its own working
defaults, without committing to either.

A fourth question arose during implementation, not anticipated by the
catalogue: `WP7.0C Engineering Foundation Contracts.md`'s own shown
`Unit<TDimension>` shape supports only a single multiplicative
`ToBaseUnitFactor` — no offset term. This is sufficient for every SI base
and derived unit, and every Imperial unit, in this Work Package's own
starting catalogue, but it cannot correctly represent an affine
conversion (Celsius↔Fahrenheit, for example, requires both a scale and an
offset). This is disclosed below as a scoping decision, not a defect —
see "Temperature Deliberately Deferred."

## Decision

**1. `double`-backed representation, confirmed.** `Unit<TDimension>.ToBaseUnitFactor`
and `Quantity<TDimension>.Value` remain `double`. No engineering standard
identified by this Work Package's own research requires exact decimal
arithmetic for a physical measurement — `double`'s own ~15-17 significant
decimal digits exceed the precision any physical instrument or material
property this platform has so far modelled could supply. `decimal`
remains available as a future, additive alternative (a
`DecimalQuantity<TDimension>`, or a generic numeric-type parameter once
.NET's generic math interfaces are judged mature enough for this
codebase) should a future engineering standard demonstrate a genuine need
— not assumed here.

**2. No DI registration, confirmed.** `Tempest.Core.UnitsAndQuantities`
registers nothing with `TempestHost.cs`. `Quantity<TDimension>` and
`Unit<TDimension>` are constructed directly by their own consumers.
Verified directly against `TempestHost.cs`: this Work Package's own diff
touches no file under `src/Tempest.Core/Runtime/`.

**3. `IUnitConverter` is built, as a stateless, non-DI-registered class.**
`WP7.0C Engineering Foundation Contracts.md`'s own disclosed triviality
concern is real — `UnitConverter.Convert` is a one-line delegation to
`Quantity<TDimension>.ConvertTo` — but the interface itself is retained
because a caller holding an untyped value (a REST request body, a
configuration value) genuinely cannot call a generic instance method
without first knowing `TDimension`, and `IUnitConverter`'s own generic
method signature is exactly the shape such a caller needs. It is not
registered with DI, and nothing in this platform requires it to be
constructed more than once — `new UnitConverter()` is as valid as sharing
a single instance.

**4. Arithmetic, comparison, formatting, and parsing require the exact
same `Unit<TDimension>` on both operands, never an implicit conversion.**
This is an extension of `WP7.0C Engineering Foundation Contracts.md`'s
own shown `Quantity<TDimension>` shape (which showed only `ConvertTo`),
not a change to it — every member the contract specified remains exactly
as specified. `+`, `-`, and every comparison operator throw
`IncompatibleUnitsException` (the one exception type the contract itself
names) when two operands share `TDimension` but not `Unit` — 5 m and 500
cm cannot be added without an explicit `ConvertTo` first. Record
structure equality (`==`) follows the identical rule: 5 m and 500 cm are
not equal, since silently normalising during equality comparison would
itself be the implicit conversion this framework's own Design Principles
forbid.

**5. Temperature deliberately deferred — not a defect, a disclosed scope
boundary.** This Work Package's own starting catalogue (`Length`, `Mass`,
`Duration`, `Force`, `Pressure`, `Area`, `Volume` — seven dimensions,
purely multiplicative) deliberately excludes Temperature. Celsius↔Kelvin
is offset-only (scale = 1); Fahrenheit↔Celsius is a genuine affine
transform (both scale and offset). Neither fits
`Unit<TDimension>.ToBaseUnitFactor`'s single-multiplicative-factor shape
without extending it — and extending the approved contract's own shown
`Unit<TDimension>` shape to add an offset term is exactly the kind of
change this Work Package's own controlling instruction requires be
treated as a genuine implementation defect, not a routine addition, since
every existing dimension's conversion arithmetic
(`ConvertTo`/`+`/`-`/comparison) is written assuming pure multiplication.
Since choosing which dimensions belong to a "starting set... extensible"
catalogue is this Work Package's own discretion (`WP7.0C Engineering
Foundation Contracts.md`'s own words), the simplest, least invasive
resolution is to defer Temperature entirely rather than force an affine
special case into a framework every other dimension uses correctly as
pure multiplication. Recorded as a Future Capability Recommendation
(affine unit support), not silently dropped.

## Consequences

**Positive:**

- `double`, no-DI, and `IUnitConverter`'s existence are now settled,
  closing all three questions `WP7.0C Required ADR Catalogue.md` reserved.
- The "same-`Unit`-only" arithmetic/comparison rule is simple, uniform,
  and requires no per-dimension special-casing — every dimension's
  operators behave identically.
- Deferring Temperature avoids a real design compromise (an offset field
  used by exactly one dimension, ignored by the other six) while keeping
  every implemented dimension's contract exact and uncompromised.

**Negative:**

- A caller wanting to add 5 m and 500 cm must call `ConvertTo` explicitly
  first — a deliberate ergonomic cost in exchange for "never perform
  implicit unit conversions."
- Temperature — one of the seven canonical SI base quantities — is absent
  from this Work Package's own starting catalogue. Any future consumer
  needing Celsius/Fahrenheit must wait for a follow-on Work Package that
  extends `Unit<TDimension>` (or introduces a parallel affine-unit type)
  to support it correctly.
- `IUnitConverter` remains, by this Work Package's own admission, a thin
  wrapper whose only justification is untyped-caller convenience — a
  future review may reasonably conclude it is not worth its own
  maintenance cost if no untyped caller materialises.

## Alternatives Considered

**`decimal`-backed representation** — considered and rejected for this
starting catalogue, for the reason given in Decision 1. Not ruled out
permanently.

**Extending `Unit<TDimension>` with an optional offset term to support
Temperature now** — considered and rejected. This would change the
approved contract's own shown shape (not merely add to it), and every
other dimension's conversion/arithmetic logic would need to account for
an offset term that is always zero for six of seven dimensions — a
disproportionate design compromise to support one dimension in this
Work Package's own starting set, when Temperature can simply wait for
its own dedicated design.

**A Roslyn-scripting-based automated "does not compile" test** —
considered and rejected for the compile-time dimension-safety guarantee.
`WP7.0C Testing Strategy.md`'s own "Additional: compile-time rejection
test" category names this guarantee, but automating it would require a
new test-only dependency this framework does not otherwise need. The
guarantee is verified instead by direct inspection, disclosed as such —
see `CompileTimeDimensionSafetyTests.cs`'s own remarks.

## Related Documents

`ADR-0041` (the "not every public type is a DI-registered service"
precedent this decision's `IUnitConverter` design continues);
`docs/releases/v0.7.0/WP7.0C Engineering Foundation Contracts.md`;
`docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md`;
`docs/releases/v0.7.0/WP7.1B Implementation Report.md`.
