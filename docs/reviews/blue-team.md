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
