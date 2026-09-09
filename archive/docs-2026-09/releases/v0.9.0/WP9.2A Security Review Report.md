# WP 9.2A — Engineering Calculations Workspace — Security Review Report

## Purpose

A proportionate security review of `CalculationTemplateRegistry`, the
Workspace layer's own ten commands, and the Engineering Cockpit's new
Calculations reads — reviewed across the same dimensions this project's
own established Security Review convention uses. Fourth consecutive
dedicated Security Review (after `WP 9.0A`/`WP 9.0B`/`WP 9.1A`).

## Review

| Dimension | Finding | Classification |
|---|---|---|
| **Authorisation boundaries** | Every new Calculations command performs no internal permission gating of its own — mirrors every `WP 9.0A`/`WP 9.0B`/`WP 9.1A` command's own identical, calling-layer-enforced posture (`ADR-0061`, unchanged). | Not Applicable — reviewed, design consistent with established precedent |
| **`ExecuteCalculationCommand`'s own JSON input surface** | `CalculationTemplateAdapter<TInput,TResult>.ExecuteAsync` deserializes caller-supplied JSON via `System.Text.Json.JsonSerializer.Deserialize<TInput>` into a closed, compile-time-known record type per Template — never a polymorphic or type-name-carrying deserialisation (`$type`-style), never `object`/`dynamic`. Malformed JSON is caught and converted to `CalculationInputInvalidException`, surfaced as an ordinary `CommandResult.Failure` — confirmed by a dedicated test (`Execute_InvalidInputJson_FailsWithoutThrowing`). | Not Applicable — reviewed, secure by construction |
| **`CalculationRecordReader`'s own generic JSON read of stored records** | Reads back only content `CalculationEngine` itself wrote into the shared `IEngineeringDocumentStore` moments (or executions) earlier — never externally-supplied, untrusted content; parsed with `JsonDocument` (a read-only DOM, no deserialisation into an executable type at all). | Not Applicable — reviewed, secure by construction |
| **Soft-delete integrity** | `DeleteCalculationObjectCommand` never erases a document, revision, or relationship — mirrors every other Domain mutation's own append-only ethos (`EngineeringObjectBase.DeleteAsync`, unchanged); `IsDeleted` is the only state that changes. | Not Applicable — reviewed, secure by construction |
| **`DeleteCalculationObjectCommand`'s has-children guard** | Correctly blocks deletion of a Calculation with live `IHasParent`-nested children, reusing `EngineeringObjectBase.DeleteAsync`'s own already-proven guard unmodified. Proven by a dedicated test. | Not Applicable — reviewed, guard proven effective |
| **Lock/Unlock/Review/Approve/Archive aliasing (`ADR-0087`)** | All five dispatch through the one `SetCalculationStatusCommand`/`IHasLifecycle.TransitionAsync`, which in turn defers entirely to the existing, unmodified `LifecycleTransitionTable` — an impermissible transition (e.g. Draft straight to Released) is rejected identically regardless of which of the five descriptive Command Palette entries a caller reaches it through. Proven by a dedicated test (`SetStatus_ImpermissibleTransition_Fails`) confirming no alias bypasses the table. | Not Applicable — reviewed, secure by construction |
| **`CalculationTemplateRegistry.ExecuteAsync`'s own relationship link** | Links the caller-supplied `targetObjectId` to the resulting record via `"calculatedBy"` only if the resolved target itself composes `IHasRelationships` (a type check, not a permission check) — an unrecognised/non-Domain target Id fails earlier, at `EngineeringDomainContext.Repository.FindAsync`, with a clear `ArgumentException`, never a silent partial link. | Not Applicable — reviewed, secure by construction |
| **`ICalculationResult`/`IVerificationResult` reachable only through `ITraceable.GetEvidenceAsync`, never called by this Work Package** | Confirmed by direct inspection: `CalculationsPropertyFacetProvider`/`CalculationRecordReader` never call `GetEvidenceAsync` on any Calculation — the same, now-thrice-established `WP 9.1A` avoidance pattern, here applied from the start rather than found and corrected mid-implementation. No permission-gating surprise is possible because the gated path is never reached at all. | Not Applicable — reviewed, avoided by construction |
| **Resource exhaustion** | `CalculationsNodeProvider`/`CalculationRecordReader`/`CalculationsKpiCards` are all O(n) in total Calculation-Kind-document count (and, for result history, O(m) in executions per Calculation) — the same already-tracked, disclosed characteristic `TD-22`/`TD-24`/`WP 9.0A`'s, `WP 9.0B`'s, and `WP 9.1A`'s own equivalent findings carry. | Technical Debt — mirrors the existing, already-tracked pattern; not separately re-registered |
| **Serialization safety** | `CalculationRecordDto<TResult>` (`Tempest.Core.Calculations`, unchanged), every Template's own `Input`/`Result` record, and `CalculationExecutionSummary` are all plain, closed-shape C# records — no polymorphic or type-name-carrying deserialisation anywhere this Work Package touches. | Not Applicable |
| **Dependency risk** | No new third-party dependency; `System.Text.Json` is already a `Tempest.Core.Calculations` dependency (`CalculationEngine` itself already serializes every record with it). | Not Applicable |
| **Backwards compatibility** | Every existing `ICalculationEngine`/`ICalculation`/`ICalculationSet`/`EngineeringCockpit` consumer is unaffected — every new member is additive; confirmed by the full, unmodified `WP 7.1D`/`WP 8.2C`/`WP 9.0A`–`WP 9.1A` test suites passing unchanged alongside the 57 new tests. | Not Applicable |

## New Debt Disclosed by This Review

No new Technical Debt item is registered by this review specifically —
the one finding above classified as debt (O(n)/O(n·m) list-and-filter
reads) mirrors an already-tracked, existing pattern across four
consecutive Work Packages now. Two further, pre-existing Domain-contract
gaps this review confirms but does not itself introduce
(`ICalculationResult`/`IVerificationResult`'s and `IApprovalGate`/
`IApproval`'s own total absence of any concrete implementation) are
registered in full in `WP9.2A Technical Debt Assessment.md` (`TD-30`),
not duplicated here.

## Verdict

**Zero Release Blocking findings.** No permission-gating availability
defect was introduced (the class of issue `WP 9.1A` found and fixed was
avoided here from the start, by never calling `GetEvidenceAsync` at
all). No new attack surface was introduced — the one new external input
boundary (`ExecuteCalculationCommand.InputJson`) deserializes into
closed, compile-time-known types only, with malformed input failing
safely as an ordinary command failure.

## Related Documents

`ADR-0086`; `ADR-0087`; `WP9.0A Security Review Report.md`; `WP9.0B
Security Review Report.md`; `WP9.1A Security Review Report.md`; `WP9.2A
Technical Debt Assessment.md`; `docs/governance/Quality/Technical Debt
Register.md`.
