# ADR-0125: Affine Units Are Represented by an Offset on `Unit<TDimension>`, and Arithmetic on Them Is Refused

## Status

Accepted — `Group A` (P01 Engineering Reference Data), 2026-09-06.

## Context

`ADR-0054` established `Quantity<TDimension>`/`Unit<TDimension>` with a
single conversion parameter: a multiplicative `ToBaseUnitFactor`. Every
unit the framework held converted to its own base unit by multiplication
alone, and for length, mass, area, volume, force, pressure and duration
that is exactly right.

Three of the reference libraries this programme adds need temperature.
A1 records a material's own service-temperature limits and its
thermal-expansion coefficient's reference temperature; A5 records the
operating-temperature range a component's own source states; A7 records
the temperature band a process runs at. Every real source quotes these in
degrees Celsius or degrees Fahrenheit.

Neither converts to kelvin by multiplication. Both are **affine**: the
conversion needs an offset as well as a factor, and a framework holding
only a factor cannot represent either. `ADR-0055` already named this as a
known limitation, and `FCR-0034` has carried it since.

The options were to defer temperature in three libraries, to record
temperature as a bare `double` with a unit name in text, or to make the
framework able to hold the units engineers actually use.

There is a second question the first one drags in. Affine units break
arithmetic in a way multiplicative units do not. Adding 20 °C to 5 °C is
meaningless — the sum of two absolute temperatures is not a temperature —
while adding 20 mm to 5 mm is exactly what it appears to be. A framework
that represented affine units but let them be added would produce
plausible-looking wrong answers, which is worse than refusing to
represent them at all.

## Decision

**1. `Unit<TDimension>` gains an optional `ToBaseUnitOffset`, defaulting
to zero.** Conversion becomes `(value × factor) + offset` in one
direction and `(baseValue − offset) ÷ factor` in the other. Every
existing unit keeps an offset of zero and behaves exactly as before; the
change is purely additive, and no existing unit, quantity or conversion
changes value.

`Unit<TDimension>.IsAffine` reports whether a unit carries a non-zero
offset, so a caller can ask rather than infer.

**2. `Quantity<TDimension>` converts through the base unit.**
`ConvertTo` now reads `targetUnit.FromBase(Unit.ToBase(Value))` rather
than dividing factors, which is the only formulation correct for both
affine and multiplicative units. `BaseValue` exposes the base-unit
magnitude, and every comparison and ordering in `Group A` is written
against it.

**3. Arithmetic on an affine quantity throws.** `+`, `−`, `×` and `÷`
each require both operands to be non-affine and throw otherwise. A
caller that genuinely wants to add temperatures converts to kelvin
first, which is the operation they actually mean, and which the
framework then performs correctly.

**4. `TemperatureUnits` holds kelvin (base), degree Celsius, degree
Rankine and degree Fahrenheit**, the last two carrying both a factor and
an offset.

## Consequences

### Positive

- Temperature is a first-class dimension, recorded in the unit the
  source quoted, and converted correctly — the three libraries that need
  it are not deferred and do not invent local workarounds.
- `FCR-0034` is resolved rather than carried forward again.
- The change is additive at the type level: no existing call site
  changes, and the offset parameter is optional on every constructor.
- A category error that units alone cannot catch — adding two absolute
  temperatures — becomes an exception at the point of the mistake rather
  than a wrong number downstream.

### Negative

- Affine arithmetic throwing is a behavioural asymmetry between
  dimensions: `Quantity<Length>` supports `+` and `Quantity<Temperature>`
  in degrees Celsius does not. This is inherent to affine units and is
  disclosed on the operators themselves; the alternative is a silently
  wrong result.
- A *temperature difference* is a distinct quantity from an absolute
  temperature, and this decision does not model it. A source quoting a
  temperature difference in degrees Celsius records it in kelvin, where
  the magnitude is identical and the arithmetic is correct. Modelling
  `TemperatureDifference` as its own dimension is a legitimate future
  extension and is deliberately not made here, because no `Group A`
  library needs one.
- `ReferenceQuantityCodec` must round-trip the offset as well as the
  factor. It does, and `EncodedQuantity` carries it.

### Neutral

- The base unit of temperature is kelvin, which is not the unit any
  engineering source quotes. That is the correct choice — it is the
  non-affine one — and callers never see it unless they ask for
  `BaseValue`.

## Alternatives Considered

**Defer temperature; record it as text.** Rejected. Three libraries need
it, a temperature in text cannot be compared or converted, and the
libraries would each have invented a workaround — exactly the
domain-local unit system `Group A`'s own charter forbids.

**A separate `AffineUnit<TDimension>` type.** Rejected. It would split
every unit-consuming API in two, and `Quantity<TDimension>` would have to
accept both. The offset costs one field and one default argument;
a parallel type costs a fork of the framework.

**Allow arithmetic on affine quantities, converting to base first.**
Rejected, and this is the decision most worth stating. `20 °C + 5 °C`
would then return `298.15 K`, which is arithmetically what the code did
and engineering nonsense. A framework whose job is preventing unit
mistakes must not manufacture this one.

**A `TemperatureDifference` dimension now.** Rejected as speculative. No
`Group A` library records a temperature difference, and adding a
dimension nothing uses is the kind of unused generality this platform
avoids. The kelvin route is available and correct in the meantime.

## Related Documents

- `ADR-0054` — the Units & Quantities framework this extends.
- `ADR-0055` — named the affine-unit limitation as a known boundary.
- `ADR-0124` — added `RotationalSpeed` and `PlaneAngle` under the same
  additive discipline.
- `ADR-0126` — the shared reference-data layer whose libraries need
  temperature.
- `docs/governance/Future Capability Register.md` — `FCR-0034`.
