# WP 7.1B — Units & Quantities Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
this Work Package's own implementation found — mirroring `WP7.1A Future
Capability Recommendations.md`'s own format.

## Recommendation 1 — Candidate `G` (Materials) Should Represent Dimensioned Properties as `Quantity<TDimension>` Directly, Boxed Only at the Dictionary Level

**What.** When Candidate `G` begins, `IMaterialSpecification.Properties`
(proposed as `IReadOnlyDictionary<string, object>`) should box real
`Quantity<TDimension>` values — e.g. `Quantity<Mass>`/`Quantity<Force>`
for density/yield-strength-adjacent properties, or `Quantity<Pressure>`
directly for yield strength itself — never a bare `double` with the
unit tracked separately in a parallel structure.

**Why this matters.** This Work Package's own `Quantity<TDimension>`
implementation proves boxing works correctly for JSON round-tripping
(`QuantitySerializationTests.cs`) and equality (`QuantityTests.
Equality_SameValueAndUnit_AreEqual`), so Materials can rely on it
directly rather than re-deriving the same guarantee.

## Recommendation 2 — Candidate `F` (Calculation) Should Treat `Quantity<TDimension>` as Its Default `TInput`/`TResult` Shape for Any Dimensioned Calculation

**What.** When Candidate `F` begins, any calculation whose input or
output represents a physical quantity should use `Quantity<TDimension>`
directly as `TInput`/`TResult`, not a bare `double` paired with a
separately-documented unit convention.

**Why this matters.** `WP7.0C Engineering Foundation Contracts.md`
already named this as a by-convention (not hard-constrained)
expectation; this Work Package's own implementation makes it concretely
possible, with real arithmetic and comparison operators a calculation
definition can use internally without reinventing them.

## Recommendation 3 — `FCR-0034` (Affine Unit Conversion) Should Be Designed as an Additive Extension, Not a Retrofit

**What.** When a future Work Package resolves `FCR-0034`, it should
introduce Temperature support in a way that leaves every existing
dimension's own conversion arithmetic unchanged — either a second,
parallel type (e.g. `AffineUnit<TDimension>`) used only where a
dimension genuinely needs an offset, or an optional offset field on
`Unit<TDimension>` defaulting to zero, re-verified against every
existing catalogued unit to confirm behaviour is unchanged.

**Why not build it now.** No current consumer needs Temperature; this
Work Package's own starting catalogue of seven purely-multiplicative
dimensions is internally consistent and complete for its own scope.
Building affine support speculatively, without a real discipline module
naming the requirement, would risk exactly the kind of premature design
this project's governance discipline discourages.

## Recommendation 4 — A Future Dimensional-Algebra Extension (Length × Length = Area) Should Be Scoped as Its Own Work Package, Not Folded Into a Future Framework's Own Brief

**What.** If a future calculation genuinely needs to derive `Quantity<Area>`
from two `Quantity<Length>` values (or similar cross-dimension
multiplication/division), this should be designed as a deliberate
extension to `Tempest.Core.UnitsAndQuantities` itself — new operators
with their own generic constraints relating the three dimensions
involved — not implemented ad hoc inside a calculation definition that
would otherwise need to reach into `Unit<TDimension>`'s own internals.

**Why not build it now.** No calculation exists yet to demonstrate a
concrete need; `WP7.0C`'s own contract scoped Units & Quantities to
same-dimension conversion only, and this Work Package's own arithmetic
operators are deliberately limited to the same scope.

## Not Recommended

- **Adding a `decimal`-backed `Quantity<TDimension>` variant speculatively.**
  `ADR-0054` already confirms `double` is sufficient for every dimension
  in this Work Package's own starting catalogue; a `decimal` variant
  should wait for a real engineering standard demonstrating a genuine
  need, not be built ahead of one.
- **Building the Roslyn-scripting infrastructure `AT-14` names**, ahead
  of a second, independent need for the same kind of compile-time-
  rejection test — a single use does not justify the new dependency.

## Related Documents

`WP7.1B Implementation Report.md`; `ADR-0054`; `docs/releases/v0.7.0/
WP7.0C Engineering Foundation Contracts.md`; `docs/governance/Quality/
Technical Debt Register.md` (`TD-19`, `AT-14`).
