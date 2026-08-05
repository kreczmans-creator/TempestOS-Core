# ADR-0083: A Bill of Materials Line Is a Fourth Additive Domain Facet (`IHasBomLine`); Unit of Measure Is a Plain String, Never `Quantity<TDimension>`

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.0B` (Product Configuration & BOM Management), 2026-08-05.

## Context

`WP 9.0B` requires Quantity, Find Numbers, Item Numbers, Reference
Designators, and Units on every Product Structure Kind that can appear
as a line under a parent (`Assembly`/`SubAssembly`/`Part`/`Component`,
the same four `ADR-0080` already extended). None of this data exists
anywhere in the frozen Domain — `ConfigurationMember` (`WP8.2B`) carries
only `ObjectId`/`RevisionNumber`, and no canonical object interface
carries a quantity of any kind.

Separately, this platform already has a real, mature unit-of-measure
system: `Tempest.Core.UnitsAndQuantities` (`WP7.1B`, `ADR-0054`) —
`Quantity<TDimension>`/`Unit<TDimension>`, phantom-typed over
`Length`/`Mass`/`Area`/`Volume`/`Duration`/`Force`/`Pressure`. Whether a
BOM line's own unit of measure should reuse it was a genuine, open
question this Work Package needed to answer before writing any code.

## Decision

**A fourth additive facet, `IHasBomLine`** (`Quantity`, `UnitOfMeasure`,
`FindNumber`, `ItemNumber`, `ReferenceDesignator`, `SetBomLineAsync`), is
added to `Tempest.Core.EngineeringDomain` and composed into
`IAssembly`/`IPart`/`IComponent` (`ISubAssembly` inherits it via
`IAssembly`) — the identical composition-over-reopening pattern
`ADR-0080` already established for `IRenamable`/`IHasParent`/`IDeletable`,
applied a fourth time. `EngineeringObjectBase` implements it
unconditionally, and `SetBomLineAsync` mutates the four fields in place
(the same `_structuralLock` pattern `RenameAsync` already uses) — a BOM
line is structural metadata, not document content, so no new revision is
created.

**`UnitOfMeasure` is a plain `string`, deliberately never
`Quantity<TDimension>`.** `Tempest.Core.UnitsAndQuantities` exists for
one specific purpose — compile-time-safe *calculation* dimensional
analysis, so that a Length can never be silently added to a Mass. A BOM
count (`"EA"`, "each") is not a physical dimension at all, and forcing it
through an `IDimension` type family built for
Length/Mass/Area/Volume/Duration/Force/Pressure would be a category
error, not a reuse. Even for BOM lines that *do* carry a physical unit
(`"M"` of wire stock, `"KG"` of adhesive), the two systems answer
different questions: `UnitsAndQuantities` guarantees *conversion*
correctness inside a calculation; a BOM line only ever needs to *display*
a unit code next to a quantity, never convert or arithmetically combine
it with another quantity. Reusing the dimensional system here would
import calculation-grade complexity for a display-only need, the same
"proportionate reuse, not reuse for its own sake" judgement `ADR-0055`
already made for Materials.

## Consequences

**Positive:**

- Every already-shipped, non-Product-Structure Kind (Requirement, Risk,
  and every other of the ~30 others) is completely unaffected — none
  composes `IHasBomLine`.
- `MechanicalObjectFactoryRegistry`/`MechanicalPropertyFacetProvider`/
  `MechanicalProductStructureNodeProvider` (`WP 9.0A`) needed only
  additive extension, never a rewrite, to surface the new facet —
  direct evidence the `ADR-0080` extension pattern generalises to a
  fourth facet as cleanly as it did to the second and third.
- `UnitsAndQuantities` remains exactly what `ADR-0054` scoped it to be —
  no pressure to grow it a "count" pseudo-dimension purely to satisfy a
  BOM display need that never wanted dimensional safety in the first
  place.

**Negative:**

- `UnitOfMeasure` is an unvalidated free-text string — `"EA"` and `"ea"`
  and `"Each"` are three different strings to this platform, with no
  canonicalisation. Disclosed as a Future Capability
  (a small closed vocabulary/lookup), not built speculatively now; no
  real BOM data in this Work Package's own representative graph
  triggers the inconsistency, since every unit string is written by
  the same sample module.
- A BOM line composed of a physical-dimension unit (`"M"`, `"KG"`) is
  never validated for consistency against `UnitsAndQuantities`'s own
  unit symbols — a caller could write `"XYZ"` and nothing would object.
  Same disposition as above.

## Alternatives Considered

**`UnitOfMeasure` as `Unit<TDimension>` for physical units, string for
everything else (a hybrid)** — considered and rejected. Two representations
for "the same field" is a worse API than one honest, simple string;
callers would need to know in advance whether a given BOM line's own
unit "counts" as a dimension before choosing which constructor overload
to use.

**A new `Count`/`Each` pseudo-dimension added to `UnitsAndQuantities`
purely to unify the type** — considered and rejected; would grow a
calculation-focused framework (`ADR-0054`) to serve a display-only need
that was never its own purpose, and "each" has no meaningful conversion
factor to define against any other unit, breaking `IUnitConverter`'s own
premise.

**Attaching BOM data to `IConfiguration`/`ConfigurationMember` instead of
the object itself** — considered and rejected; a BOM line describes an
object's own current structural usage (`IHasParent.ParentId`), not a
point-in-time configuration snapshot — the two are orthogonal
(`ADR-0081`'s own reasoning applies identically here).

## Related Documents

`ADR-0080`; `ADR-0054`; `ADR-0055`; `WP7.1B Units and Quantities
Framework Implementation.md`; `src/Tempest.Core/EngineeringDomain/Contracts/BillOfMaterials.cs`;
`src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectBase.cs`.
