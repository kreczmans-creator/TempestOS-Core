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
