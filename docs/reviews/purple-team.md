# 🟣 Purple team — verification of Red/Green/security findings

Method: for each finding that Blue claims fixed, re-run the proof-of-concept against the fixed build and name the permanent regression test that now encodes it. A finding is Closed only when the PoC fails against the fixed code and the regression test passes in the suite.

| Finding | PoC (before) | After the fix | Permanent regression test | Status |
|---|---|---|---|---|
| SEC-05 | 4 parallel `IssueAsync` on one checked instance: 4 successes, one Evidence with 4 overwrites. | 1 success, 3 refusals (`TransitionNotPermitted` / `CalcConcurrentIssueException`), one attachment. | `ConcurrentIssueTests.Racing_Issues_On_One_Evidence_Leave_Exactly_One_Issued_Revision`, `Issue_After_An_Issue_Is_Refused_Without_A_Second_Attachment` (Core); `ConcurrencyTests.Two_Simultaneous_Issues_Leave_One_Issued_Revision` (Library) | Closed |
| G-01 | 600 × 300 duct at 0.8 m³/s: 0.471 Pa/m. | 0.556 Pa/m (Green's independent value 0.554–0.559). | `DuctSizingCalculator` case `ka-rectangular-equivalent` (`dpL_s1`) | Closed |
| G-02 | Bolt at v/V_R = t/T_R = 0.6 passed (0.85) under a clause that fails it (1.03). | Both checks reported; the linear EN 1993-1-8 check governs at 1.03 → FAIL. | golden `mech.bolt-group.txt`; `BoltTests` | Closed |
| G-03 | Negative NPSH_a → "NPSH_r ≤ NPSH_a" PASS. | Negative utilisation fails. | `CheckUtilisation` change covered by every calculator suite; `PipeFlowTests.Pump_Below_System_Warns_Without_A_Duty_Point` | Closed |
| G-04 | Sheet showed "9999.9 %" yield. | 99.999 %. | `ToleranceStackTests` (SI fraction) + golden `mech.tolerance-stack.txt` | Closed |
| Hostile text | — | No exception, no control characters, bounded sheet for 16 templates × 6 strings. | `HostileInputTests.Hostile_Text_Is_Contained` | Closed |
| PERF-1 | 3 × 5 grid frame reported "mechanism" (band overflow corrupted the matrix). | 510-element grid solves; overflow throws. | `PerformanceBudgetTests.Five_Hundred_Element_Frame_Under_One_Second` | Closed |
| CI-W1 / CI-W2 | Windows CI: PDFs 650 KB; CRLF breaks a sheet assertion. | Body face subset; LF everywhere. Verified on Linux; Windows verified by the CI run on `release/v0.26.0` (Grey). | `WorkedExamplePdfTests`, `SheetsTests.Working_Shows_Formula_Substitution_Result_And_Clause` | Closed on Linux; Windows pending CI |

Red-team findings are appended below as they are verified.

## Red-team findings (verified after the Blue fixes)

| Finding | PoC (before) | After the fix | Permanent regression test | Status |
|---|---|---|---|---|
| R-01…R-04, R-06…R-08 (imports, rendered text) | 12 Sheets PoCs threw or imported oversized/formula text. | All pass: notices, caps and `Clean` hold. | `Red_ImportAttacks` (42 cases) | Closed |
| R-05 (provenance size) | 1.5 MB reference rendered; Issue threw from inside the transaction; 2.3 MB PDF. | Blocked at 200/40 characters; Issue refuses; PDF 2,287 KB → < 400 KB with 200 KB fields. | `Red_ImportAttacks.Json_Huge_Provenance_Reference_Is_Blocked_Or_Contained`, `Red_ConcurrencyAndTamper.Oversized_Provenance_Is_Refused_And_Issue_Never_Throws`, `Red_PdfRendererAttacks.Oversized_Text_Is_Bounded_In_The_Pdf` | Closed |
| R-09, R-21 (Monte Carlo budget) | 5 s cancellation at the cap; 20 s tolerance stack. | 100,000-sample cap at the runner; both PoCs within budget. | `Red_ImportAttacks.Monte_Carlo_At_The_Sample_Cap_Finishes_In_Time_Or_Is_Refused`, `Red_SolverAttacks.Tolerance_Stack_At_Its_Hard_Limits_Finishes_In_Budget` | Closed |
| R-10 (variadic reducers) | 3.6 GB, 11–28 s. | Refused before allocation at the element cap; within budget. | `Red_EngineAttacks.Variadic_Reducers_Over_Maximal_Vectors_Stay_In_Budget` | Closed |
| R-11 (null table rows) | NRE from seven calculators. | Block notice, no exception. | `Red_SolverAttacks.Table_Rows_Without_Cells_Never_Throw` | Closed |
| R-12, R-13 (frame work) | Still running at 15 s. | Refused by the per-member load cap and the factor-work cap. | `Red_SolverAttacks.Frame_With_Maximal_Load_Rows_On_One_Member_Finishes_In_Budget`, `Frame_Random_Mesh_At_The_Node_Cap_Finishes_In_Budget` | Closed |
| R-14 (tamper under Superseded) | Name and outputs rewritten. | `CalcInstanceImmutableException`; stored record unchanged. | `Red_ConcurrencyAndTamper.Issued_Content_Cannot_Be_Rewritten_Under_A_Superseded_Mark` | Closed |
| R-15, R-19 (Check races and crashes) | Instance Draft with Evidence Checked; 2–4 of 4 checks accepted. | Update refused with a concurrency message; one check of four; Check atomic with the instance write. | `Update_Racing_A_Check_Cannot_Revert_A_Checked_Instance`, `Two_Simultaneous_Checks_Leave_One_Check`, `Crash_After_The_Check_Is_Recorded_Leaves_A_Recoverable_Instance` (re-targeted) | Closed |
| R-16 (Revise) | Orphan draft; `DuplicateBusinessIdentifierException` escaped. | One revision B; the loser gets a refusal; a retry after a crash succeeds by adopting the abandoned entry. | `Two_Simultaneous_Revisions_Produce_One_Revision_B`, `Crash_Mid_Revise_Leaves_No_Orphan_Draft` (both re-targeted to the single write transaction) | Closed |
| R-17, R-18 (packs) | Member changed after the pack check was issued; duplicate member threw. | Update refused; issue refused when a member moved; duplicates collapsed. | `Pack_Member_Changed_After_The_Pack_Check_Cannot_Be_Issued`, `Pack_With_Duplicate_Members_Is_Refused_Or_Issues_Cleanly` | Closed |
| R-20 (notice flood) | 511 KB sheet. | 20 notices per input plus a count. | `Red_SolverAttacks.Validator_Notice_Count_Is_Bounded` | Closed |
| R-22 (orphan Evidence on Create crash) | 2 orphan objects. | Unchanged — accepted (D-32). | `Crash_Mid_Create_Leaves_No_Orphan_Evidence` (skipped with the justification) | Accepted |
| R-24 (PDF control characters) | NUL/ESC in the extracted text. | None; document structure intact; 50-sheet hostile pack within budget. | `Red_PdfRendererAttacks` (7 cases; the helper keeps the hostile revision within the new 40-character cap so the run is not blocked before layout) | Closed |

## Black-team findings (verified after the fixes)

| Finding | Before | After the fix | Permanent regression test | Status |
|---|---|---|---|---|
| B-01 | Empty file ran on defaults with no notice. | One warning per defaulted input ("F (Axial load) was not entered; the template default 12 kN is used."). | `SheetsTests.Missing_Input_With_Default_Is_Fine` | Closed |
| B-12 | Rejection moved the Evidence to Checked; instance stuck. | Instance and Evidence Draft; re-check and issue succeed. | `CalcInstanceServiceTests.Rejected_Check_Leaves_Instance_And_Evidence_Draft_And_Can_Be_Checked_Again` | Closed |
| B-13 | Check succeeded without provenance; issue refused; no way back. | Check refused with the provenance message; after the source is supplied, check and issue succeed. | `CalcInstanceServiceTests.Check_And_Issue_Are_Refused_Without_Provenance` | Closed |
| B-14 | `ArgumentException` for empty strings. | Refusals in the service's voice. | `CalcInstanceServiceTests.Empty_Checker_Or_Issue_Fields_Are_Refused_Not_Thrown` | Closed |
| B-17, B-18, B-20 | "4 row(s)"; `3.99 vs 1`; ranges in mm for an inch input. | Rows on the sheet; `u = … vs u ≤ …`; ranges in the entered unit. | 16 goldens, 32 worked-example sheets, `SheetsTests.Checks_Show_Governing_First_With_Pass_Fail` | Closed |

Suites after the fixes: Engine 384/384, Sheets 93/93, Calculators 328/328, Library 42/42 (+1 skipped), Core Evidence 92/92, Desktop CalcSheets + Documents 157/157.
