# 🔵 Blue team — defensive mitigations and residual risk

Inputs: `security-review.md` (SEC-01…SEC-05), `green-team.md` (G-01…G-14), `red-team.md` (R-xx, appended as the Red findings land). Every Critical/High and every Medium is listed with its mitigation commit; Lows are logged with their justification.

## Mitigations shipped in Phase 6

| Finding | Mitigation | Where | Regression test |
|---|---|---|---|
| SEC-05 High — racing Issues both succeed | Transaction-time status re-check in Core (`verifyBeforeWrite` hook, `Evidence.RecordIssueAsync`, `ConcurrentIssueException` → `TransitionNotPermitted`) and in the Library (`CalcInstanceStore.WriteIssuedInstanceAsync`, `CalcConcurrentIssueException` → refusal). Packs use the same guarded write for every member. | `src/Tempest.Core/Evidence/Evidence.cs`, `EvidenceService.cs`, `EngineeringObjectBase.cs`, `EngineeringObjectStateStore.cs`; `src/Tempest.Calc/Tempest.Calc.Library/CalcInstanceStore.cs`, `CalcInstanceService.cs` | `ConcurrentIssueTests` (Core), `ConcurrencyTests` (Library) |
| G-01 High — duct friction at the actual velocity with D_e | Friction rate uses the equivalent-round velocity 4Q/πD_e²; fittings and limits keep the actual velocity; known-answer case now asserts the friction rate. | `DuctSizingCalculator.cs` | `ka-rectangular-equivalent` (dpL 0.556 Pa/m) |
| G-02 Medium — circular interaction labelled as EN 1993-1-8 | Circular check relabelled (elastic method); linear EN 1993-1-8 Table 3.4 form added as a second check; the governing check is the higher. | `BoltGroupCalculator.cs` | golden `mech.bolt-group.txt`, `BoltTests` |
| G-03 Medium — negative utilisation passed | `CheckUtilisation` passes only 0 ≤ u ≤ limit. | `RunResult.cs` | all calculator suites; NPSH case in `PipeFlowTests` |
| G-04 Medium — yield shown ×100 | Percent values built with `ScalarValue.Of(value, "%")`. | `ToleranceStackCalculator.cs` | `ToleranceStackTests` (SI fraction), golden |
| CI-W1 — Windows PDFs 650 KB | Body (platform default) face now subset like the brand faces, bytes read from the typeface. | `DocumentFonts.cs` | `WorkedExamplePdfTests` (< 200 KB), `TrueTypeSubsetterTests` |
| CI-W2 — CRLF sheets on Windows | Text renderer emits LF on every platform (goldens and sheet hashes are platform-independent). | `CalcSheetTextRenderer.cs` | `SheetsTests`, goldens |
| PERF-1 — frame band overflow | Free DOFs numbered in the reordered node sequence; band overflow now throws instead of corrupting; 510-element grid solves in < 1 s. | `FrameSolver.cs` | `PerformanceBudgetTests`, `FrameTests` |
| Hostile text in sheets | Every template run with formula prefixes, markup, control characters, SQL text, 5 k strings and bidi overrides in each free-text input and text column. | — | `HostileInputTests` (16 templates × 6 strings) |

## Red findings (R-01…R-24) — mitigations

Red's proof-of-concept tests stay in the suite as the regression tests (`Red_ImportAttacks`, `Red_EngineAttacks`, `Red_SolverAttacks`, `Red_ConcurrencyAndTamper`, `Red_PdfRendererAttacks`); the only change to a PoC is noted below.

| Finding | Mitigation | Where |
|---|---|---|
| R-01 Medium — duplicate CSV header throws | Column map built with `TryAdd` (first column wins). | `CalcCsv.cs` |
| R-02 Medium — quoted CSV cell escapes the field cap | `MaxFieldLength` enforced inside the quoted branch too. | `CalcCsv.cs` |
| R-03 Medium — sign-prefixed DDE formulas | Only a cell that `double.TryParse` accepts as a whole is exempt; everything else starting with `+`/`-`/`=`/`@` is prefixed on export and refused on import. | `CalcCsv.cs` |
| R-04 Medium — `[null]` JSON element throws | Read as `List<CalcInputValue?>`; a null element or a null table row is a block notice. | `CalcInputJson.cs` |
| R-05 High — unbounded provenance text | `CalcProvenance.MaxReferenceLength` 200 / `MaxRevisionLength` 40 (`IsWithinLimits`), blocked by the validator and refused by `UpdateInputsAsync`; `IssueAsync` maps the document-size cap to a refusal; the PDF renderer caps every field (400 chars) and paragraph (4,000 chars) (D-31). | `InputValues.cs`, `CalcInputValidator.cs`, `CalcInstanceService.cs`, `CalcSheetPdfRenderer.cs` |
| R-06 Medium — blocked oversized text printed whole | The sheet builder truncates the value and the provenance text in the inputs table. | `CalcSheetBuilder.cs` |
| R-07 Low — undefined provenance kind issues | `IsSufficientToIssue` requires `Enum.IsDefined(Kind)`. | `InputValues.cs` |
| R-08 Low — U+2028/U+2029 forge lines | `Clean` also replaces `LineSeparator`, `ParagraphSeparator` and `Format` (bidi overrides, zero-width marks). | `CalcSheetTextRenderer.cs` |
| R-09 Medium / R-21 Medium — Monte Carlo at the engine cap overruns | `CalcRunner.MaxCalculatorSamples` = 100,000 for calculator-level Monte Carlo (the engine's 1,000,000 cap stays for direct engine use); `n_mc` hard maximum follows it (D-29). | `CalcRunner.cs`, `ToleranceStackCalculator.cs` |
| R-10 High — variadic reducers allocate every element | `Flatten` checks the summed length against `MaxArrayElements` before copying and reduces over `double` spans, not boxed scalars. | `Evaluator.cs` |
| R-11 High — table row without cells throws | `CalcRow.Cells` defaults null to empty; the validator blocks a null row; the runner catches `NullReferenceException`. | `InputValues.cs`, `CalcInputValidator.cs`, `CalcRunner.cs` |
| R-12 Medium — 20,000 loads on one member | `FrameLimits.MaxLoadsPerMember` = 200 (a block names the member) (D-30). | `FrameModel.cs`, `FrameCalculator.cs` |
| R-13 Medium — O(n·b²) factor at the node cap | `FrameLimits.MaxFactorWork` = 1.5 × 10⁹ (free × half-bandwidth²) refused before factoring; the factor polls cancellation (D-30). | `FrameSolver.cs` |
| R-14 High — issued content rewritten under a Superseded mark | `SameIssuedContent` compares the full record except `Status`/`UpdatedAt`. | `CalcInstanceStore.cs` |
| R-15 High — Update racing a Check; crash mid-Check | Optimistic concurrency on every instance write (`SaveInstanceAsync(instance, expectedUpdatedAt)`, `WriteGuardedAsync`); Check records the Evidence check and writes the instance in one Core transaction (`IEvidenceService.RecordCheckInOneTransactionAsync`, `Evidence.RecordCheckAsync(check, alsoWrite)` with the transaction-time re-check) (D-33). PoC re-targeted: the gate can no longer split the two writes, so it asserts the atomic outcome. | `CalcInstanceService.cs`, `CalcInstanceStore.cs`, `Evidence.cs`, `EvidenceService.cs` |
| R-16 Medium — Revise in two transactions; duplicate escapes | New draft and Superseded mark written in one transaction (`SaveInstancesAsync`); a concurrent Revise is refused (`DuplicateBusinessIdentifierException` mapped, the register entry claimed before the draft); a retry after a crash adopts the abandoned register entry and its Draft Evidence record (`FindAbandonedRevisionAsync`) (D-33). PoCs re-targeted to the single write transaction. | `CalcInstanceService.cs` |
| R-17 High — pack member changed after the pack check | `UpdateInputsAsync` refuses a member of a checked pack; `IssuePackAsync` refuses when any member's `UpdatedAt` is later than the pack's. | `CalcInstanceService.cs` |
| R-18 Low — duplicate pack members | `CreatePackAsync` takes `Distinct()` ids; `IssuePackAsync` maps `CalcConcurrentIssueException` to a refusal. | `CalcInstanceService.cs` |
| R-19 Medium — concurrent checks all succeed | The check status transition is re-verified inside the transaction (`ConcurrentCheckException` → `TransitionNotPermitted`). | `Evidence.cs`, `EvidenceService.cs` |
| R-20 Medium — unbounded validator notices | `CalcInputValidator.MaxNoticesPerInput` = 20, then one "… and N more" notice. | `CalcInputValidator.cs` |
| R-22 Low — orphan Evidence after a crash mid-Create | Accepted (D-32): the orphan is a Draft Evidence record and register entry that govern nothing and cannot be issued; the PoC is skipped with the justification. | — |
| R-24 Low — PDF draws raw control characters | Every drawn string passes through `Clean` and the field/paragraph caps (`T(...)` in the renderer), including the header band, metadata, footers and every table cell. | `CalcSheetPdfRenderer.cs` |
| R-23 Info | No change (PoC passes). | — |

Worked-example `inputs.json` files are unchanged: `IsWithinLimits` is `[JsonIgnore]` so the exchange format did not move.

## Black findings (B-01…B-25) — mitigations

| Finding | Mitigation | Where |
|---|---|---|
| B-01 High — defaulted inputs pass silently | One Warning per non-optional input that takes its template default (ADR-0166). | `CalcInputValidator.cs` |
| B-02 / B-03 Medium — JSON wording, duplicate symbols | Plain-words malformed-document notice; duplicate symbol and ValueText-only values refused by name. | `CalcInputJson.cs` |
| B-12 High — rejection strands the calculation | A Rejected check is kept on the instance (`LastRejection`); the Evidence stays Draft. | `CalcInstanceService.cs`, `CalcInstance.cs` |
| B-13 High — check without provenance is a dead end | Check and CheckPack demand the provenance Issue demands. | `CalcInstanceService.cs` |
| B-14 Medium — empty strings throw | Refusals: "A checker name is needed." etc. | `CalcInstanceService.cs` |
| B-17 High — table rows absent from the sheet | Up to 100 rows per table written under the summary row; the rest counted. | `CalcSheetBuilder.cs` |
| B-18 Medium — ratios read as demands | `u = 0.375 vs u ≤ 1`. | `RunResult.cs` |
| B-20 Medium — ranges in the template unit | Range converted to the entered unit when it parses. | `CalcSheetBuilder.cs` |
| B-15, B-19 Medium | Justified (D-35). | — |
| B-24 Low — no way to load an example into the form | *Import inputs (CSV/JSON)* on the worksheet form re-seeds it from the clipboard, table and vector inputs carried through (D-37); `docs/worked-examples/README.md` says how. | `CalcSheetView.cs`, `CalcSheetCoordinator.cs` |
| Windows CI (Grey) | Subsetter keeps only PDF-relevant tables; Red budgets honour the CI timeout factor (D-36). | `TrueTypeSubsetter.cs`, `Red_*` |

## Validation layer, logging, secure defaults

- Validation: `CalcInputValidator` (kinds, limits, rows) → `CalcInputBinder` → calculator guards (`Block` with the symbol) → `CalcRunner` (never throws). Table text is cleaned at render and FTS index time.
- Logging: `Tempest.Calc.*` has no logger; nothing dumps inputs. Core's audit rows record who issued what, not the input values.
- Secure defaults: every cap on; static composition; CSV formula cells refused on import and neutralised on export; Manual provenance cannot issue; issued records immutable.

## Residual risk register

| ID | Risk | Likelihood / impact | Owner | Status |
|---|---|---|---|---|
| RR-01 | DirectWrite (Windows) may reject a subset font and fall back to the whole face; the PDF stays correct but may exceed 200 KB. | Low / Low | Grey (CI on windows-2022 decides) | Open until the Windows run is green |
| RR-02 | A calculator author bypasses `CheckUtilisation` with a hand-built `CalcCheck`; the Orange checklist and the DoD theories catch most cases. | Low / Medium | Yellow | Accepted |
| RR-03 | Mechanism detection by relative pivot (Y3-03 / SEC-02) could pass a pathological stiffness ratio. | Low / Low | Red | Open |
| RR-04 | Reference values from mirrors (Blue Book, ASHRAE) not re-read from the printed sources (Y3-05, Y4-05, G-14). | Low / Low | Owner | Open |
