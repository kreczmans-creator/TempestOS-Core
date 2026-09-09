# WP 7.1C — Materials Framework — Material Provenance Assessment

## Purpose

This Work Package's own controlling instruction required a dedicated
provenance assessment — a deliverable no prior Engineering Foundation
Work Package needed, since none of them modelled data whose own
trustworthiness depends on knowing where a value came from. This report
confirms every provenance field is genuinely captured, genuinely
preserved, and never invented.

## Provenance Fields — What Is Captured, and Why

| Field | Type | Captured Where Defined | Purpose |
|---|---|---|---|
| Source reference | `string?` | `MaterialPropertyProvenance.SourceReference` | The engineering source a value was taken from (a standard, a datasheet, a test report). Free text — no fixed vocabulary of sources exists yet, mirroring `Verification`'s own deliberately open `method` field precedent (`WP7.0C Engineering Foundation Contracts.md` §5). |
| Revision | `int?` | `MaterialPropertyProvenance.SourceRevision` | The revision or edition of the source the value came from, where the source itself has one. |
| Validation status | `MaterialPropertyValidationStatus` (`Unvalidated`/`Validated`/`Superseded`) | `MaterialPropertyProvenance.ValidationStatus` | Whether the value has been independently checked. Defaults to `Unvalidated` — an honest "not yet checked" state, never silently upgraded. |
| Confidence level | `MaterialPropertyConfidenceLevel` (`Unknown`/`Low`/`Medium`/`High`) | `MaterialPropertyProvenance.ConfidenceLevel` | How confidently the value is believed accurate. Defaults to `Unknown` — recorded honestly, never guessed at, mirroring this governance suite's own Verified/Inferred/Unknown discipline. |
| Applicable conditions | `string?` | `MaterialPropertyProvenance.ApplicableConditions` | The conditions a value is valid under (e.g. a temperature range). Free text, for the same reason as Source reference. |
| Notes | `string?` | `MaterialPropertyProvenance.Notes` | Anything not captured by the other five fields. |

Every field this Work Package's own controlling instruction named is
represented — none was dropped, and none was renamed beyond what plain
English required (e.g. "Revision" → `SourceRevision`, to disambiguate
from the material's own, separate `RevisionNumber`).

## Structural Guarantee, Not Convention

`MaterialProperty`'s own constructor throws `ArgumentNullException` if
`Provenance` is omitted — there is no code path anywhere in
`Tempest.Core.Materials` that constructs a property without one.
`MaterialPropertyProvenance.Unknown` (all fields `null` or their own
"not assessed" enum member) is the honest default when nothing is
known — a real, present value, never an omitted field. This is proven
directly: `MaterialProperty_NoProvenanceGiven_CannotBeConstructedWithNullProvenance`
confirms the constructor rejects a null provenance; every other test
confirms a real `MaterialPropertyProvenance` is always present on every
constructed property.

## Preservation Through Registration, Revision, and Lookup

`RegisterAsync_ThenFindAsync_PreservesEveryProvenanceField` constructs a
property with every one of the six fields set to a distinct,
non-default value, registers it, reads it back through
`FindAsync`, and asserts structural equality against the original
`MaterialPropertyProvenance` — proving the full round-trip through JSON
serialization (`MaterialPropertyDto`) preserves every field exactly, not
merely the ones a shallower test might have happened to check.

## No Invented Values

Every value used in this Work Package's own tests and living-reference
sample module (`MaterialsSampleModule`) is clearly fictional — a
"Fictional Test Alloy" with a `YieldStrength`/`ReferenceLength` whose
own `SourceReference` states explicitly "Fictional test fixture — not a
real material standard" and whose `Notes` field states explicitly the
value was invented for this Work Package's own demonstration. No real
material designation (e.g. a real steel or alloy grade) and no
real-looking published property value appears anywhere in this Work
Package's own code or tests — a deliberate, disclosed choice, per this
Work Package's own controlling instruction ("do not invent values").

## Verdict

**Provenance is structurally guaranteed, fully preserved through the
complete register/revise/find lifecycle, and never invented anywhere in
this Work Package's own deliverables.**

## Related Documents

`WP7.1C Implementation Report.md`; `ADR-0055`; `tests/Tempest.Core.Tests/
Materials/MaterialCatalogTests.cs`; `src/Samples/Tempest.Samples/
MaterialsSampleModule.cs`.
