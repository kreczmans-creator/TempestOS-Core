# WP 7.1D — Engineering Calculation Framework — Calculation Integrity Assessment

## Purpose

This Work Package's own controlling instruction required every
calculation support nine specific integrity properties — a dedicated
deliverable no prior Engineering Foundation Work Package needed, since
none of them defined what "engineering evidence" itself must look like.
This report confirms each property is genuinely satisfied by the real
implementation, not merely asserted.

## Integrity Properties — What Is Guaranteed, and How It Is Proven

| Property | Guarantee | Proof |
|---|---|---|
| **Stable identity** | `CalculationRecord<TResult>.Id` is assigned once, at execution, and is the real `EngineeringData.IEngineeringDocument`'s own Id — never reassigned, never reused. | `ExecuteAsync_RecordId_IsDirectlyRetrievableThroughEngineeringDocumentStore`; `ExecuteAsync_CalledTwice_ProducesTwoDistinctRecordIds` |
| **Revision history** | Every execution's own record is genuinely revision-capable, inherited directly from `IEngineeringDocumentStore` — `RevisionNumber` reflects the real, current document state. | `ExecuteAsync_RevisionNumberIsOne`; the underlying document is independently retrievable and revisable through `IEngineeringDocumentStore` directly, exactly as `ADR-0056` records |
| **Explicit assumptions** | Every record carries a copy of its own producing definition's declared `Assumptions` — never omissible, never inferred after the fact. | `ExecuteAsync_RecordIncludesDefinitionsOwnAssumptions` |
| **Explicit inputs** | `TInput` is a required, non-optional parameter to `ExecuteAsync` and `Calculate` — no calculation may execute without one; the framework does not itself retain `TInput` (a disclosed, deliberate scope boundary, not a gap — the *definition* attaches whatever input-derived context matters via `CalculationContext`). | `ExecuteAsync_RegisteredCalculation_ReturnsExpectedResult` and every other execution test supplies an explicit input |
| **Explicit outputs** | `TResult` is a required, non-optional return value — `Calculate` cannot return without one, and `CalculationRecord.Result` always carries it. | Every execution test asserts a concrete, expected `Result` value |
| **Unit-safe quantities** | Where a calculation is dimensioned, `TInput`/`TResult` are `Quantity<TDimension>` — the same compile-time dimension-safety guarantee `WP 7.1B` established applies unchanged; a calculation cannot silently mix dimensions any more than `Quantity<TDimension>` itself can. | `DoubleLengthCalculationDefinition` uses `Quantity<Length>` for both `TInput` and `TResult`; `CalculationSampleModuleIntegrationTests.cs` exercises this end to end through the real Host |
| **Material references where applicable** | `CalculationContext.ReferenceMaterial` lets a definition record which materials it consulted; every reference appears on the resulting record. | `ExecuteAsync_RecordsReferencedMaterialIds`; `ExecuteAsync_NoMaterialReferenced_ReferencedMaterialIdsIsEmpty` proves the field is honestly empty, not a placeholder, when nothing was referenced |
| **Validation outcome** | Every record carries a `CalculationValidationResult` derived automatically from recorded constraint checks — `Valid` if every check passed, `Conditional` if any did not. | `ExecuteAsync_AllConstraintsSatisfied_OutcomeIsValid`; `ExecuteAsync_SoftConstraintUnsatisfied_OutcomeIsConditional_ResultStillReturned`; `ExecuteAsync_NoConstraintsRecorded_OutcomeIsValid` |
| **Provenance** | `CalculationId`, `ExecutedAt`, `ExecutedByPrincipalId`, `Assumptions`, and `ReferencedMaterialIds` together constitute complete provenance — who computed what, when, under what assumptions, referencing what materials — without duplicating `Materials.MaterialPropertyProvenance`, a genuinely different kind of evidence. | `ExecuteAsync_PrincipalEstablished_RecordsItsIdentity`; `ExecuteAsync_NoPrincipalEstablished_RecordsUnknownExecutor` |

## Hidden Assumptions Made Impossible — By Construction, Not Convention

This Work Package's own controlling instruction required the framework
"make hidden engineering assumptions impossible wherever practical."
Concretely:

- A `MaterialProperty`-style bare value with no attached assumptions is
  structurally impossible for a `CalculationRecord` — `Assumptions` is a
  non-nullable field, always populated from the producing definition's
  own `CalculationMetadata`, copied at execution time.
- A calculation cannot silently skip recording its own validation
  outcome — `CalculationValidationResult` is computed automatically by
  the engine itself from whatever constraint checks the definition
  recorded, never left for the definition to construct (and potentially
  omit) directly.
- A calculation cannot be executed without a `CalculationId` resolving
  to a real, registered definition — `CalculationDefinitionNotFoundException`
  is thrown otherwise, never a silent no-op.

## What Remains the Registering Definition's Own Responsibility

Consistent with "does not itself provide any concrete calculation," this
framework does **not** verify:

- That a definition's own declared `Assumptions`/`Constraints` are
  actually true of the real world — only that they are recorded and
  travel with the result.
- That a `ReferenceMaterial` call names a `materialId` that genuinely
  exists in `Tempest.Core.Materials` (`AT-16`).
- That `Calculate` is genuinely pure — enforced by convention and a
  concurrency test, not by the compiler (`ADR-0056` Decision 1).

These are disclosed, deliberate scope boundaries, not integrity gaps
this Work Package silently accepted.

## Verdict

**Every one of the nine integrity properties this Work Package's own
controlling instruction named is genuinely satisfied, each proven by a
specific, passing test — not merely asserted in documentation.**

## Related Documents

`WP7.1D Implementation Report.md`; `ADR-0056`; `tests/Tempest.Core.Tests/
Calculations/CalculationEngineTests.cs`; `docs/engineering/Engineering
Principles.md` (Principles 17-23).
