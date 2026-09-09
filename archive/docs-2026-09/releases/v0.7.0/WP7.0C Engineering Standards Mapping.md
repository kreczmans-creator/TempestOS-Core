# WP 7.0C — Engineering Standards Mapping

## Status

Complete. **Identifies architectural requirements only — implements no
standard.** Per this Work Package's own controlling instruction, this
document does not commit any framework to a specific engineering
standard (ISO, ASME, Eurocode, IEC, or otherwise). No existing
TempestOS document, ADR, or confirmed customer requirement names a
specific standard as a current obligation — asserting one here would be
exactly the kind of unsourced business claim this project's governance
discipline forbids (`Governance Philosophy.md`, "Unknown beats invented
data"). Instead, each framework below is assessed for the **architectural
requirement** that its contract must be standards-agnostic and
extensible enough to support whichever standard a real future discipline
need eventually names — never a commitment to one now.

## Per-Framework Assessment

### Engineering Data Model

- **Applicable engineering standards.** None named. `Content` is opaque
  by design (`WP7.0C Engineering Foundation Contracts.md`) — a future
  consumer may store data conforming to any standard (e.g., a
  STEP/ISO 10303 CAD reference, a requirements-management schema)
  without this framework itself needing to understand or validate it.
- **Unit systems.** Not applicable directly — this framework carries no
  numeric quantity itself. Architectural requirement: must not impose a
  unit system on `Content`, leaving that entirely to `Tempest.Core.
  UnitsAndQuantities` and the storing consumer.
- **Numerical precision considerations.** Not applicable — `Content` is
  `string`-typed and opaque.
- **Validation expectations.** Architectural requirement only: the
  framework validates its own contract (Id existence, revision
  sequencing) but explicitly does not validate `Content` against any
  external schema or standard — that responsibility belongs to the
  calling consumer, mirroring `IPersistenceStore`'s own identical
  position on stored value content.
- **Traceability expectations.** Full revision history and typed
  references are the framework's own core traceability contribution —
  sufficient to support whatever a future discipline-specific
  traceability standard (e.g., a requirements-verification matrix)
  needs to be built *on top of*, not built *into*, this framework.

### Units & Quantities Framework

- **Applicable engineering standards.** None named as a current
  obligation. Architectural requirement: the `Unit<TDimension>`/
  `IDimension` design must not hard-code a single unit system, so that
  SI units, Imperial/US customary units, and any standard-specific unit
  (a structural engineering standard's own preferred unit for a given
  quantity, for example) can all be represented as ordinary
  `Unit<TDimension>` values without a contract change — confirmed
  directly by the proposed contract's own Extension Points (`WP7.0C
  Engineering Foundation Contracts.md`).
- **Unit systems.** Architectural requirement: **must support both SI
  and Imperial/US customary units concurrently**, since real engineering
  practice worldwide uses both, and a future discipline module should
  never be forced to convert at its own boundary before calling this
  framework.
- **Numerical precision considerations.** Architectural requirement:
  the representation must use a precision-consistent numeric type across
  the entire framework (this proposal uses `double` throughout,
  `WP7.0C Engineering Foundation Contracts.md`) — whether `double` is
  sufficient for every future discipline's own precision needs, or
  whether a `decimal`-based variant is eventually needed for a
  standard requiring exact decimal arithmetic, is an open question (see
  `WP7.0C Required ADR Catalogue.md`), not resolved here.
- **Validation expectations.** Architectural requirement: a conversion
  between incompatible dimensions must fail loudly
  (`IncompatibleUnitsException`), never silently produce a
  dimensionally-meaningless result — the single most important
  correctness property this entire framework exists to guarantee.
- **Traceability expectations.** Not directly applicable — a
  `Quantity<TDimension>` value carries no provenance of its own; the
  *consumer* (a Calculation Record, a Material Specification) is
  responsible for its own traceability, mirroring the Data Model's
  identical position.

### Materials Framework

- **Applicable engineering standards.** None named. Architectural
  requirement: the open `IReadOnlyDictionary<string, object>` property
  shape (`WP7.0C Engineering Foundation Contracts.md`) must not assume
  any specific material-standard's own property taxonomy (e.g., a
  specific standard's own naming for "yield strength") — a future
  discipline module names its own property keys.
- **Unit systems.** Inherited entirely from Units & Quantities — every
  material property is expected to be a `Quantity<TDimension>`, so both
  SI and Imperial representations are available without a Materials-
  specific accommodation.
- **Numerical precision considerations.** Inherited from Units &
  Quantities — no additional precision consideration beyond what that
  framework already establishes.
- **Validation expectations.** Architectural requirement: duplicate
  `materialId` registration must fail (`DuplicateMaterialException`);
  no requirement is placed on validating a material's own property
  *values* against any external standard's own acceptable ranges — a
  future discipline-specific consumer's own responsibility.
- **Traceability expectations.** Inherited from Engineering Data Model
  — a material specification's own revision history is the Data Model's
  own contribution, not something Materials re-implements.

### Engineering Calculation Framework

- **Applicable engineering standards.** None named — and, architecturally,
  none *should* be, since this framework is deliberately formula-agnostic:
  it dispatches whatever calculation a future discipline module
  registers, never encoding a specific standard's own formula itself
  (`WP7.0C Engineering Foundation Contracts.md`'s own "supplied entirely
  by each registering consumer").
- **Unit systems.** Architectural requirement: `TInput`/`TResult` are
  expected, by convention, to carry `Quantity<TDimension>` values where
  dimensioned — the framework itself imposes no unit system, deferring
  entirely to Units & Quantities.
- **Numerical precision considerations.** Architectural requirement:
  since `ICalculationDefinition.Calculate` is required to be a pure
  function, floating-point precision and reproducibility become the
  registering consumer's own responsibility — this framework's own
  contract does not, and should not, mandate a specific numeric type
  for every possible calculation, only that whichever type is used
  produces a deterministic result for the same input (a testable
  property, not merely an aspiration — see `WP7.0C Testing Strategy.md`).
- **Validation expectations.** `CalculationInputInvalidException`,
  raised by the registered definition itself, not the framework —
  input validation against a specific engineering standard's own
  acceptable-input range is entirely the registering consumer's concern.
- **Traceability expectations.** `CalculationRecord<TResult>` is this
  framework's own traceability contribution — what was calculated, by
  whom, and when. Whether a calculation record should also capture
  *which version* of a registered definition performed the calculation
  (relevant if a formula is later corrected) is an open question (see
  `WP7.0C Required ADR Catalogue.md`).

### Verification & Validation Framework

- **Applicable engineering standards.** None named as a specific
  requirement. Architectural requirement: `method` (the verification
  method) is deliberately `string`-typed, not a closed enum, because
  Systems Engineering practice commonly recognises at least four
  verification methods (inspection, test, analysis, demonstration) and
  different standards/practices may name or subdivide them differently
  — fixing a specific enumeration now would encode one standard's own
  vocabulary as if it were universal, which this review declines to do
  without a named, confirmed requirement.
- **Unit systems.** Not directly applicable — a verification outcome
  itself carries no quantity. `evidence` (a `string`) may reference a
  dimensioned measurement recorded elsewhere (a Calculation Record, for
  example), inheriting that record's own unit-system properties rather
  than establishing its own.
- **Numerical precision considerations.** Not directly applicable — see
  above.
- **Validation expectations.** Architectural requirement: an outcome
  must be one of exactly three values (`Pass`/`Fail`/`Conditional`) —
  deliberately a closed enum, unlike `method`, because these three
  values are a structural property of what "verification" means, not a
  standard-specific vocabulary choice.
- **Traceability expectations.** Full verification history per subject
  document is this framework's own core contribution — sufficient
  traceability for a future Systems Engineering or Quality-discipline
  capability to build a requirements-verification matrix on top of,
  without this framework needing to build that matrix itself.

## Cross-Framework Standards Observation

**No framework in this set commits to a specific real-world engineering
standard**, and this is a deliberate, disclosed architectural choice,
not an oversight: every one of the five is designed to be standard-
agnostic infrastructure a future, real discipline module builds on,
consistent with `WP 7.0B`'s own finding that inventing discipline-
specific content without a real stakeholder need would repeat exactly
the kind of speculation this project's governance discipline forbids.
Identifying which real standards a future Mechanical, Structural,
Electrical, Building Services/HVAC, or Manufacturing module must
actually support remains an open question for that module's own future
Architecture Work Package, informed by a real stakeholder engagement —
not something this document, or any Engineering Foundation framework,
resolves in advance.

## Related Documents

`WP7.0C Engineering Foundation Contracts.md`; `WP7.0C Required ADR
Catalogue.md`; `WP7.0B Engineering Discipline Assessment.md`.
