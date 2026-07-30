# WP 7.1B — Units & Quantities Framework — Implementation

## 1. Introduction

`WP 7.1B` is the second implementation Work Package of the Engineering
Foundation phase (`v0.7.0`), following `WP 7.1A` (Engineering Data
Model). It implements `Tempest.Core.UnitsAndQuantities` — a pure,
dependency-free mathematical library for dimensioned physical
quantities and unit conversion — exactly as `WP7.0C Engineering
Foundation Contracts.md` proposed, extended with arithmetic, comparison,
formatting, parsing, and serialization support this Work Package's own
controlling instruction named explicitly.

## 2. Purpose

To give every future Engineering Foundation framework and Engineering
Module a single, canonical representation for a dimensioned physical
quantity — so no future Mechanical, Structural, Electrical, HVAC,
Materials, or Manufacturing capability reinvents unit conversion, or
falls victim to the unit-conversion defect class engineering software
has repeatedly demonstrated industry-wide.

## 3. Background

`WP 7.0B` identified Units & Quantities (`FCR-0030`) as a prerequisite
for the Engineering Calculation Framework (`FCR-0032`) and for every
future discipline module. `WP 7.0C` proposed its public contract —
`Quantity<TDimension>`, `Unit<TDimension>`, `IUnitConverter` — and
reserved `ADR-0054` for three open questions: `double` vs. `decimal`
representation, whether DI registration was needed, and whether
`IUnitConverter` was worth building at all. This Work Package resolves
all three and implements the contract.

## 4. The Problem

A physical quantity is meaningless without its unit, and combining two
quantities expressed in different units without an explicit conversion
is a well-known, recurring class of engineering software defect. No
existing `v0.6.0` or `v0.7.0` Platform Service or framework represents a
dimensioned quantity at all — every future discipline module would
otherwise invent its own, incompatible representation and its own
conversion logic, exactly the fragmentation `ADR-0041`'s own shared-
Persistence precedent already resolved once for Settings and Audit.

## 5. The Design

`Quantity<TDimension>` and `Unit<TDimension>` are immutable, allocation-
free `readonly record struct` value types, generic over a compile-time
`IDimension` marker (`Length`, `Mass`, `Duration`, `Force`, `Pressure`,
`Area`, `Volume` — seven dimensions, each with a small static unit
catalogue mixing SI and Imperial units). Neither type is DI-registered;
neither depends on any Platform Service (`ADR-0054`). Conversion
(`ConvertTo`) is pure multiplication against a fixed `ToBaseUnitFactor`.
Arithmetic (`+`, `-`) and comparison (`<`, `>`, and so on) require the
exact same `Unit` on both operands — not merely the same dimension —
throwing `IncompatibleUnitsException` otherwise, so no operation ever
performs an implicit conversion. Formatting and parsing are hard-coded
to `CultureInfo.InvariantCulture`, deterministic regardless of the
calling thread's own culture. `IUnitConverter`/`UnitConverter` is a
thin, stateless wrapper for callers holding an untyped value. See
`WP7.1B Implementation Report.md` for the complete file-by-file account.

## 6. Alternatives Considered

**`decimal`-backed representation** — considered and rejected in
`ADR-0054`; no identified engineering standard requires exact decimal
arithmetic beyond `double`'s own precision for any dimension in this
Work Package's own starting catalogue.

**Extending `Unit<TDimension>` with an optional offset term to support
Temperature (an affine, not purely multiplicative, dimension) now** —
considered and rejected in `ADR-0054`. Every other dimension's own
conversion arithmetic assumes pure multiplication; adding an offset term
used by exactly one dimension would be a disproportionate design
compromise. Temperature is deliberately deferred (`FCR-0034`, `TD-19`).

**An automated, Roslyn-scripting-based "does not compile" test** for the
compile-time dimension-safety guarantee — considered and rejected in
favour of direct-inspection verification, disclosed as `AT-14`, since
automating it would require a new test-only dependency this framework
does not otherwise need.

## 7. Why This Solution Was Chosen

It satisfies every Design Principle this Work Package's own controlling
instruction named (immutable, thread-safe by construction, no implicit
conversion, no DI, no logging, no Hosted Service) while remaining small
enough to review in full — seven dimensions, twenty files, no dimension-
specific special-casing anywhere in `Quantity<TDimension>`'s own
arithmetic or comparison logic.

## 8. Architectural Principles

Applies `FOUNDATION.md`'s existing principles without modification: one
component, one reason to change (no dimension's own unit catalogue
depends on another's); immutability by construction, not by convention.
Extends `docs/engineering/Engineering Principles.md` with six further
principles (7-12), each demonstrated by working code — the same
"derived from working code, not asserted in advance" discipline
`WP 7.1A` established, applied here to a second framework.

## 9. Files Added

20 new production files under `src/Tempest.Core/UnitsAndQuantities/`
(no file modified — this Work Package registers nothing with
`TempestHost.cs` or any other existing file); 9 new test files under
`tests/Tempest.Core.Tests/UnitsAndQuantities/`. Full list: `WP7.1B
Implementation Report.md`.

## 10. Trade-offs

Temperature (Celsius/Fahrenheit, an affine conversion) is deliberately
absent from the starting catalogue (`TD-19`, `FCR-0034`) — the single
multiplicative `ToBaseUnitFactor` this Work Package's own approved
contract shows cannot represent it correctly without a design change
affecting every other dimension. The compile-time dimension-safety
guarantee is verified by direct inspection, not an automated compiler-
error test (`AT-14`) — both disclosed in `WP7.1B Technical Debt
Assessment.md`, neither believed to be a current correctness risk.

## 11. Common Mistakes

A future consumer should **not** assume two quantities of the same
dimension but different units can be added or compared directly — call
`ConvertTo` first, exactly as `IncompatibleUnitsException`'s own
documentation states. A future consumer should **not** attempt to
represent Temperature using this Work Package's own `Unit<TDimension>`
shape — it will silently produce wrong results for any conversion
requiring an offset (there is no runtime guard against this, since
`ToBaseUnitFactor` has no way to express one); wait for `FCR-0034`'s own
resolution, or raise it if a real Temperature need arrives sooner.

## 12. Future Evolution

Candidate `F` (Engineering Calculation Framework, `FCR-0032`) depends on
this framework directly and is now unblocked with real, tested behaviour
to build against. Materials (`FCR-0031`) likewise depends on this
framework for dimensioned property values. See `WP7.1B Engineering
Foundation Impact Assessment.md` for the complete account.

## 13. Key Takeaways

1. A framework with zero Platform Service dependency is a genuinely
   different implementation shape from every prior Engineering
   Foundation Work Package — no DI registration, no `ILogger?`
   parameter, no `TempestHost.cs` change at all, confirmed by this
   Work Package's own diff touching nothing under `src/Tempest.Core/
   Runtime/`.
2. An approved contract's shown public shape (`ConvertTo` alone) is a
   floor, not a ceiling — extending `Quantity<TDimension>` with
   arithmetic, comparison, formatting, and parsing did not require
   changing anything the contract specified, only adding to it.
3. A genuine architectural gap (Temperature's affine conversion) can
   surface during implementation of a framework that looked, at
   contract-review stage, like the simplest of the five — disclosing it
   immediately and deferring it cleanly (`ADR-0054`, `TD-19`,
   `FCR-0034`) was judged better than forcing a compromise into six
   working dimensions to accommodate a seventh.

## Architectural Debt Assessment

`TD-19` (no affine/offset unit conversion — Temperature deferred) and
`AT-14` (compile-time dimension safety verified by inspection, not an
automated test) — both newly disclosed, neither Release Blocking. Full
detail: `WP7.1B Technical Debt Assessment.md`.

## Observations

This is the second implementation Work Package of the Engineering
Foundation phase, and the first with genuinely zero DI/Platform Service
footprint — a useful, disclosed contrast to `WP 7.1A`'s own
Persistence-backed design, both validated by the same discipline (clean
Debug/Release builds, 1119/1119 tests, both configurations).

## Related Documents

`docs/releases/v0.7.0/WP7.1B Implementation Report.md` and its six
companion deliverables; `ADR-0054`; `docs/engineering/Engineering
Principles.md`; `docs/releases/v0.7.0/WP7.0C Engineering Foundation
Contracts.md`; `docs/academy/04 Design Patterns/
05-phantom-type-dimension-safety.md`.
