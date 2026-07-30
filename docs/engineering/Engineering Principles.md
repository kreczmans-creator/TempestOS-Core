# Engineering Principles

## Status

Established by `WP 7.1A` (Engineering Data Model) — the first Work
Package to implement a real Engineering Foundation framework rather than
plan or design one. This document is new: `docs/engineering/` did not
exist before this Work Package. Where `docs/academy/06 Engineering
Standards/Engineering Governance.md` governs how TempestOS is built,
and `VISION.md` states what TempestOS is for, this document states the
principles the engineering-domain content itself — not the platform
that hosts it — must uphold, derived from what `Tempest.Core.
EngineeringData` actually implements, not asserted in advance of it.

Extended by `WP 7.1B` (Units & Quantities Framework), 2026-07-30, adding
six further principles (7-12, below) derived from what
`Tempest.Core.UnitsAndQuantities` actually implements — the same
"derived from working code, not asserted in advance" discipline applied
to a second framework.

Extended by `WP 7.1C` (Materials Framework), 2026-07-30, adding four
further principles (13-16, below) derived from what
`Tempest.Core.Materials` actually implements — the same discipline
applied to a third framework, this one built directly on both of the
first two.

Extended by `WP 7.1D` (Engineering Calculation Framework), 2026-07-30,
adding seven further principles (17-23, below) derived from what
`Tempest.Core.Calculations` actually implements — the same discipline
applied to a fourth framework, this one consuming all three of the
first three.

## Purpose

Every future Engineering Foundation framework (`FCR-0030`–`FCR-0033`)
and every future Engineering Module builds on the Engineering Data
Model. This document exists so each of them inherits a consistent set of
principles about what "engineering information" means on this platform,
rather than each independently re-deriving the same ground rules — the
Academy/governance equivalent of `Future Work Package Guidelines.md`,
scoped specifically to engineering-domain content.

**Only principles the implemented architecture actually demonstrates
are listed below.** A principle that sounded reasonable in the abstract
but that `EngineeringDocumentStore`'s own real implementation does not
enforce is not included — this document is derived from working code,
not from aspiration.

## The Principles

### 1. Engineering entities have stable identities

An `IEngineeringDocument`'s own `Id` is assigned once, at creation
(`CreateAsync`), and never changes for the rest of that document's
existence — every subsequent operation (`ReviseAsync`, `LinkAsync`,
`GetRevisionHistoryAsync`) addresses the document by that same, permanent
Id. A document's `Kind` likewise never changes after creation. Only
`CurrentRevisionNumber` advances. This is enforced structurally, not by
convention: no method on `IEngineeringDocumentStore` accepts a new Id
for an existing document, and `EngineeringDocumentStore`'s own
implementation never rewrites a document's identity record's `Kind`.

### 2. Revision history is explicit

Every change to a document's content produces a new, separately
retrievable `IDocumentRevision` — there is no "update in place" operation
anywhere in the approved contract or its implementation.
`GetRevisionHistoryAsync` returns every revision a document has ever
had, oldest first, not merely the current one. This was proven, not
merely designed: `EngineeringDocumentStoreTests.
GetRevisionHistoryAsync_ReturnsEveryRevision_OldestFirst` creates three
revisions and confirms all three, in order, are independently readable.

### 3. Engineering data is independent of calculations

`Tempest.Core.EngineeringData` contains no calculation logic, no
formula, and no numeric computation of any kind — `Content` is an
opaque `string`, uninterpreted by this namespace. This Work Package's
own controlling instruction required this separation explicitly ("shall
not implement engineering calculations"), and `WP7.0C Cross-Framework
Dependency Report.md` already confirmed, at contract level, that the
Engineering Calculation Framework (`FCR-0032`) depends on Units &
Quantities, never on the Data Model directly for its own core dispatch
mechanism — this Work Package's implementation introduces nothing that
would create such a dependency.

### 4. Engineering entities are immutable where practical

An `IDocumentRevision`, once written, is never modified or deleted —
confirmed directly by `EngineeringDocumentStore`'s own implementation,
which only ever writes a new revision key, never overwrites an existing
one. An `IEngineeringDocument`'s own identity record is the one
necessary exception (`CurrentRevisionNumber` must advance for the
document to have a "current" revision at all), and this exception is
itself narrow and structural, not a general licence to mutate — no
other field of a document's own identity record ever changes.

### 5. Engineering information is reproducible

Reading the same document Id and revision number always returns the
same, unchanged content — a direct consequence of Principle 2
(revisions are never modified) combined with Principle 4 (revisions are
immutable once written). This is a narrower claim than "a calculation
is reproducible" (which `Tempest.Core.EngineeringData` does not attempt,
per Principle 3) — it is specifically about the data layer: the record
of what engineering information existed at a point in time does not
drift or decay on repeated reads.

### 6. Engineering correctness takes precedence over convenience

`ReviseAsync`'s own atomicity guarantee (no two concurrent revisions of
the same document can ever claim the same revision number,
`EngineeringDocumentStoreTests.
ReviseAsync_CalledConcurrently_NeverProducesTwoRevisionsWithTheSameNumber`)
was implemented via a per-document lock, at the cost of serialising
concurrent revisions to the same document — a real, deliberate
performance trade-off, accepted because a document with an ambiguous or
colliding revision number would be a worse outcome than a slower write
path. Likewise, requiring a full new revision for any content change
(Principle 2), rather than offering a cheaper "patch" operation, is a
deliberate correctness-over-convenience choice, not an oversight.

## Units & Quantities Extension (`WP 7.1B`)

### 7. Units are explicit

A bare `double` never represents a physical quantity anywhere in
`Tempest.Core.UnitsAndQuantities` — every numeric value is paired with a
`Unit<TDimension>` inside a `Quantity<TDimension>`, and no method
anywhere in this framework accepts or returns an un-unit'd number. This
is enforced structurally: `Quantity<TDimension>`'s own constructor
requires both a value and a unit; there is no overload that defaults
the unit.

### 8. Dimensions are enforced

A `Quantity<Length>` cannot be added to, compared against, or converted
into a `Quantity<Mass>` — the compiler rejects it. This is proven, not
merely asserted: `CompileTimeDimensionSafetyTests.cs` documents the exact
`CS1503`/`CS0019` errors reproduced by attempting it, verified directly
against this repository's own compiler (see `ADR-0054`'s own note on why
this is verified by inspection rather than an automated compiler-error
test).

### 9. Conversion is deterministic

`Quantity<TDimension>.ConvertTo` is pure multiplication/division against
a fixed `ToBaseUnitFactor` — no randomness, no ambient state, no
thread-culture dependency. `DimensionCatalogueTests` proves every
catalogued unit round-trips through its own dimension's base unit to
within floating-point tolerance, for the same input, every time.
Formatting and parsing are equally deterministic: both are hard-coded to
`CultureInfo.InvariantCulture` regardless of the calling thread's own
culture — `QuantityTests.ToString_IsCultureInvariant` proves a `de-DE`
format provider does not change the decimal separator produced.

### 10. Physical impossibilities fail loudly

A `Unit<TDimension>` cannot be constructed with a zero, negative,
infinite, or `NaN` conversion factor — no unit's scale can be
physically zero or negative. A `Quantity<TDimension>` cannot be
constructed with a `NaN` or infinite value — no physical quantity is
"not a number." Both are enforced in each type's own constructor, proven
by `UnitTests.Constructor_NonPositiveOrNonFiniteFactor_ThrowsArgumentOutOfRangeException`
and `QuantityTests.Constructor_NonFiniteValue_ThrowsArgumentOutOfRangeException`
— neither silently clamps or coerces the invalid input.

### 11. Precision loss is never silent

`Quantity<TDimension>.ToString()` (no format specified) uses `double`'s
own full round-trippable representation — it does not truncate decimal
places unless the caller explicitly requests a reduced format (e.g.
`"F2"`). Dividing a quantity by zero does not silently produce an
`Infinity`-valued quantity: the resulting non-finite value is rejected by
the constructor (Principle 10), converting a silent precision/validity
loss into a loud, immediate failure —
`QuantityTests.ScalarDivision_ByZero_ThrowsArgumentOutOfRangeException`
proves this.

### 12. Mathematical correctness takes precedence over convenience

Every arithmetic and comparison operator requires both operands to share
the exact same `Unit<TDimension>` — not merely the same dimension —
throwing `IncompatibleUnitsException` otherwise
(`QuantityTests.Addition_DifferentUnits_ThrowsIncompatibleUnitsException`).
A more convenient design would silently convert 500 cm to 5 m before
adding; this framework deliberately requires the caller to call
`ConvertTo` explicitly first, exactly as `ADR-0054`'s own Decision 4
records, because an implicit conversion the caller did not ask for is a
correctness risk this framework's own controlling Work Package named
directly ("never perform implicit unit conversions").

## Materials Extension (`WP 7.1C`)

### 13. Every engineering property has provenance

`MaterialProperty` cannot be constructed without a
`MaterialPropertyProvenance` — the constructor throws
`ArgumentNullException` if one is omitted
(`MaterialProperty_NullValue_ThrowsArgumentNullException`'s own sibling
test for provenance). There is no "provenance-free" property anywhere in
this framework; the honest default when nothing is known
(`MaterialPropertyProvenance.Unknown`) is still a real, present value —
`SourceReference = null`, `ValidationStatus = Unvalidated`,
`ConfidenceLevel = Unknown` — never an omitted field.

### 14. Engineering data is revision controlled

A material's own properties are never updated in place — `ReviseAsync`
records an entirely new revision of the underlying
`IEngineeringDocument`, exactly as Principle 2 (Engineering Data Model)
already established for documents generally, now proven for Materials
specifically: `ReviseAsync_ExistingMaterial_UpdatesPropertiesAndIncrementsRevisionNumber`
confirms the revision number advances and the prior revision remains
independently readable through `IEngineeringDocumentStore` directly.

### 15. Engineering values remain independent of design methodology

`Tempest.Core.Materials` contains no safety factor, no design allowable,
no calculation, and no design-code-specific assumption — a
`MaterialProperty`'s own value is the engineering fact as sourced
(`MaterialPropertyProvenance.SourceReference`), never a value adjusted
for a particular design methodology's own margin or allowable. This
Work Package's own controlling instruction required this separation
explicitly ("shall not implement design allowables beyond the approved
contracts"), and `grep` of `src/Tempest.Core/Materials/` for any
calculation or safety-factor logic finds none.

### 16. Material identity is stable

A material's own `MaterialId`, `Name`, and `Category` never change once
registered — `ReviseAsync`'s own signature accepts only new properties,
never a new Id, name, or category, mirroring Principle 1's identical
claim for `IEngineeringDocument.Kind`.
`ReviseAsync_PreservesNameAndCategory` proves this directly: revising a
material's own properties leaves its name and category exactly as
registered.

## Calculation Framework Extension (`WP 7.1D`)

### 17. Every calculation is reproducible

`ICalculationDefinition<TInput, TResult>.Calculate` is required to be a
pure function of its own input — no I/O, no shared mutable state, no
ambient dependency. `ExecuteAsync_SameInputMultipleTimes_
AlwaysProducesTheSameResult` proves this directly: the same input,
executed five times, produces the identical result every time. This is
the same "deterministic systems" principle Units & Quantities already
established (Principle 9), now demonstrated for calculations
specifically.

### 18. Every assumption is explicit

`CalculationMetadata.Assumptions` is fixed per definition and copied
directly into every `CalculationRecord<TResult>` at execution time —
there is no code path anywhere in `Tempest.Core.Calculations` that
produces a record without its own producing definition's assumptions
attached. `ExecuteAsync_RecordIncludesDefinitionsOwnAssumptions` proves
a recorded assumption's own description and justification both survive
into the resulting record unchanged.

### 19. Every engineering input is traceable

A calculation's own input is not itself stored (only the definition's
declared metadata and computed outputs are), but every execution's own
identity (`CalculationRecord<TResult>.Id`) is the real, durable
`EngineeringData.IEngineeringDocument` Id backing it —
`ExecuteAsync_RecordId_IsDirectlyRetrievableThroughEngineeringDocumentStore`
proves this Id is genuinely usable with `IEngineeringDocumentStore`
directly, the same traceability guarantee Materials already established
(Principle 16) for its own `UnderlyingDocumentId`.

### 20. Every engineering output records provenance

A `CalculationRecord<TResult>`'s own provenance is not a separate,
bolted-on field — it *is* the record itself: `CalculationId`,
`ExecutedAt`, `ExecutedByPrincipalId`, `Assumptions`, and
`ReferencedMaterialIds` together answer exactly what
`Materials.MaterialPropertyProvenance` answers for a material property
(where a value came from, under what conditions, by whom), without
duplicating that record type for a genuinely different kind of evidence.

### 21. Intermediate results are inspectable

`CalculationContext.RecordIntermediate` lets a definition disclose a
named value it computed on the way to its own final result — never
hidden inside the calculation's own internal logic.
`ExecuteAsync_RecordsIntermediateResults` proves a recorded intermediate
value is present, by name, on the resulting `CalculationRecord<TResult>`,
immediately inspectable by the caller that requested the execution.

### 22. Calculations remain deterministic

Concurrent execution of the *same* registered, genuinely pure
calculation, with *different* inputs, produces correct, non-interfering
results — proven directly by
`ExecuteAsync_ConcurrentDifferentInputs_SamePureCalculation_
AllProduceCorrectResults` (thirty concurrent executions, each producing
exactly the result its own input implies, no cross-contamination). This
is the concrete architectural benefit `WP7.0C Engineering Foundation
Contracts.md` itself named as following directly from the purity
requirement — demonstrated here, not merely asserted.

### 23. Engineering judgement is never hidden inside algorithms

`Tempest.Core.Calculations` provides dispatch, metadata, context, and
recording infrastructure only — it supplies no calculation of its own,
mirroring exactly how `Commands.ICommandRegistry` supplies no command
logic of its own. Every engineering judgement (an assumption, a
constraint, a formula) belongs to the registering consumer's own
definition, explicit in its own declared `CalculationMetadata` and its
own `Calculate` method — never implicit inside this framework's own
dispatch mechanism.

## What This Document Does Not Cover

- **Verification** — the one remaining Engineering Foundation framework
  (`FCR-0033`) will assess which of these principles apply to it
  directly and which it extends with its own, once implemented; this
  document is not amended in advance of that work.
- **Affine unit conversion (Temperature)** — deliberately deferred, not
  covered by Principle 9's "pure multiplication" claim; see `ADR-0054`'s
  own "Temperature Deliberately Deferred" section. Materials properties
  and calculation inputs/outputs built on `Quantity<TDimension>` are
  correspondingly bounded to the same seven dimensions.
- **Discipline-specific engineering principles** (a structural
  engineering design principle, an electrical safety margin
  principle) — deliberately out of scope, per this Work Package's own
  controlling instruction not to introduce Mechanical, HVAC, Structural,
  Electrical, or Manufacturing mathematics, design-code logic, or
  safety-factor policy.

## Related Documents

`docs/academy/06 Engineering Standards/Engineering Governance.md`;
`VISION.md`; `docs/releases/FOUNDATION.md`; `docs/governance/Future
Capability Register.md`; `ADR-0053`; `ADR-0054`; `ADR-0055`; `ADR-0056`;
`docs/academy/03 Work Packages/WP7.1A-engineering-data-model-implementation.md`;
`docs/academy/03 Work Packages/WP7.1B-units-and-quantities-framework-implementation.md`;
`docs/academy/03 Work Packages/WP7.1C-materials-framework-implementation.md`;
`docs/academy/03 Work Packages/WP7.1D-engineering-calculation-framework-implementation.md`.
