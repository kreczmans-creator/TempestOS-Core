# WP 7.1B — Units & Quantities Framework — Mathematical Validation Report

## Purpose

This Work Package's own controlling instruction requires a "Mathematical
Consistency Review" as part of Validation — a deliverable no prior
Engineering Foundation Work Package needed, since none of them was a
pure mathematical library. This report independently verifies every
conversion factor this Work Package introduces against its own published
definition, and confirms the automated test suite's own round-trip
evidence is mathematically sound, not merely passing by coincidence of
tolerance.

## Conversion Factor Verification

Every `ToBaseUnitFactor` below is checked directly against its own
internationally published definition (SI Brochure, NIST Special
Publication 811, or the exact statutory definitions given below), not
merely transcribed from memory.

| Dimension | Unit | Factor Used | Verified Against |
|---|---|---|---|
| Length | Millimetre | 0.001 | SI prefix, exact by definition |
| Length | Centimetre | 0.01 | SI prefix, exact by definition |
| Length | Kilometre | 1000.0 | SI prefix, exact by definition |
| Length | Inch | 0.0254 | International yard and pound agreement, 1959 — exact |
| Length | Foot | 0.3048 | 12 × 0.0254 — exact |
| Length | Yard | 0.9144 | 3 × 0.3048 — exact |
| Length | Mile | 1609.344 | 5280 × 0.3048 — exact |
| Mass | Gram, Milligram, Tonne | 0.001, 0.000001, 1000.0 | SI prefixes, exact by definition |
| Mass | Pound | 0.45359237 | International avoirdupois pound, 1959 — exact |
| Mass | Ounce | 0.028349523125 | 0.45359237 / 16 — exact |
| Duration | Millisecond | 0.001 | SI prefix, exact by definition |
| Duration | Minute | 60.0 | Exact by definition |
| Duration | Hour | 3600.0 | 60 × 60 — exact by definition |
| Force | Newton | 1.0 (base) | SI derived unit, kg·m/s², exact by definition |
| Force | Kilonewton | 1000.0 | SI prefix, exact by definition |
| Force | Pound-force | 4.4482216152605 | Defined as 1 lb × standard gravity (9.80665 m/s²) — exact to the precision shown |
| Pressure | Pascal | 1.0 (base) | SI derived unit, N/m², exact by definition |
| Pressure | Kilopascal, Megapascal | 1000.0, 1,000,000.0 | SI prefixes, exact by definition |
| Pressure | Bar | 100,000.0 | Exact by definition (100 kPa) |
| Pressure | Psi | 6894.757293168 | Pound-force per square inch — derived from `PoundForce / (0.0254²)`, independently recomputed and confirmed to match |
| Area | Square millimetre | 0.000001 | 0.001² — exact |
| Area | Square foot | 0.09290304 | 0.3048² — exact |
| Volume | Litre | 0.001 | Exact by definition (1 dm³) |
| Volume | Cubic foot | 0.028316846592 | 0.3048³ — exact |
| Volume | US gallon | 0.003785411784 | US statutory gallon, exact by definition |

**No factor above was found to be incorrect.** Every non-obvious factor
(Pound-force, Psi in particular) was independently recomputed from its
own constituent definition (mass × standard gravity; force ÷ area)
rather than trusted from a single source, and the recomputation matched
the value used to at least nine significant figures.

## Round-Trip Correctness — Statistical Confirmation

`DimensionCatalogueTests.cs` exercises every one of the 25 catalogued
units (across all seven dimensions) with a round-trip
(`unit → base → unit`) at a representative value (3.0), asserting
agreement to 6 decimal digits of precision. `QuantityTests.cs`
additionally exercises Length's own Metre↔Foot round-trip at five values
spanning fourteen orders of magnitude (`-5.0`, `0.0`, `5.0`, `1e-9`,
`1e12`), each asserted to 9 decimal digits of precision. No round-trip
test failed at any precision level attempted — `double`'s own ~15-17
significant decimal digits comfortably exceed both thresholds for every
value tested.

## Floating-Point Tolerance Discussion

Every conversion in this framework is a single multiplication followed,
on the return trip, by a single division by the same factor — the
minimum possible number of floating-point operations for a round-trip,
and therefore the minimum possible accumulated rounding error. No
conversion in this framework chains through an intermediate unit other
than each dimension's own base unit (Metre, Kilogram, Second, Newton,
Pascal, Square Metre, Cubic Metre) — `ConvertTo` always converts through
exactly one multiplication and one division, never a chain of several,
keeping accumulated floating-point error to the theoretical minimum for
any base-10 floating-point representation of a non-power-of-two factor
(Inch, Pound, and similar factors are not exactly representable in
binary floating point, so a small, expected representational error,
well within the 6-9 digit tolerances above, is inherent to `double`
itself, not a defect in this framework's own arithmetic).

## Edge-Case Verification

- **Zero:** `Quantity<TDimension>(0.0, unit)` is accepted and round-trips
  exactly (multiplying and dividing zero by any positive factor remains
  exactly zero, no floating-point error possible).
- **Negative values:** accepted and round-trip identically to positive
  values, confirmed by `ConvertTo_ThenBack_RecoversOriginalValue_
  WithinFloatingPointTolerance(-5.0)`.
- **Extremely small (`1e-9`) and extremely large (`1e12`) values:**
  both round-trip within the same 9-digit tolerance as ordinary
  magnitudes — `double`'s own dynamic range (±1.8×10^308) is far wider
  than any physical quantity this framework's own starting catalogue
  needs to represent.
- **Non-finite values (`NaN`, `+Infinity`, `-Infinity`):** rejected at
  construction for both `Quantity<TDimension>.Value` and
  `Unit<TDimension>.ToBaseUnitFactor` — confirmed by
  `[Theory]`-driven tests covering all three non-finite cases for each.
- **Division by zero:** `quantity / 0.0` produces an infinite
  intermediate value, rejected by the constructor before it can ever be
  observed by a caller — confirmed by
  `ScalarDivision_ByZero_ThrowsArgumentOutOfRangeException`.

## Verdict

**Mathematically sound.** Every conversion factor is independently
verified against its own published definition; every round-trip test
passes within a tolerance far tighter than any physical measurement this
framework will realistically carry; every edge case named by this Work
Package's own controlling instruction (incompatible dimensions,
negative values, zero, extremely small/large values, floating-point
tolerance, SI↔Imperial conversion) is covered by a specific, passing
test.

## Related Documents

`WP7.1B Implementation Report.md`; `ADR-0054`; `tests/Tempest.Core.Tests/
UnitsAndQuantities/DimensionCatalogueTests.cs`; `tests/Tempest.Core.Tests/
UnitsAndQuantities/QuantityTests.cs`.
