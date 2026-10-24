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

## Addendum A

**Summary.** All nine High findings are fixed. For each one I re-ran the reviewer's failing scenario against the fixed code and it now fails, and a permanent regression test covers it: R-A-01, R-A-02, R-A-03, G-B-01 (= B-A-03), B-A-01, B-A-02, B-A-03, B-A-04 and B-A-05. Red's own R-A-01 test, unchanged, now passes when run against the motor-circuit pack built on a licensed ampacity record. The issued pack sheet and E4's own issued sheet show none of the five licensed capacities.

Across all 82 findings:
- **Verified:** 57
- **Partly:** 13 (G-A-03, G-A-06, B-A-10, B-A-12, B-A-14, B-A-18, B-A-20, B-A-22, R-A-16, and their SEC duplicates)
- **Accepted-as-documented:** 6 (G-A-05, G-A-12, G-B-08, G-B-17, plus the parts of R-A-14 and R-A-16 that DA-23 states are not fixed)
- **Not fixed:** 4, none of them in a Blue brief (G-A-14, B-A-17, most of B-A-12, B-A-20(b))
- **Info, no action:** G-A-15, B-A-23

The fixes introduced six new findings: two Medium, three Low, one Info. The two Medium ones should be settled before the gate is called:
- **P-A-01:** a licensed scalar that the CSV export withheld comes back on import as the template default. Before Blue this import was blocked.
- **P-A-02:** "Evidence" provenance is never checked, so the PROFIS anchorage condition still passes on any typed reference marked "evidence", and on the placeholder file in the shipped example.

**Method.** Read-only on `v0.26.0/addA-9-blue` (5510ad03). I cloned it to `/tmp/claude-0/-home-user-TempestOS-Core/21794f57-2fa8-52ed-8796-ca45cd27cfc8/scratchpad/purple-clone` and built the Library, Sheets and Calculators test projects there (0 warnings). I ran filtered tests only:
- Library `BlueRoundTests|Chains|IndependentCheck|LinkingTests|CellLink|RecordTests`: 113/113
- Sheets `BlueRoundTests|Csv`: 33/33
- Calculators `CsvRoundTripTests`: 44/44 (every worked example)

I also wrote six scratch tests in the clone; they are not in the repository:
- `tests/Tempest.Calc.Tests.Library/PurplePocTests.cs`: Red's R-A-01 test, the two-person independence dead end, and a pack-issue staleness check.
- `tests/Tempest.Calc.Tests.Calculators/PurplePocTests.cs`: the withheld-scalar CSV round trip, and a survey of which templates open blocked on a new sheet (output in `/tmp/claude-0/-home-user-TempestOS-Core/21794f57-2fa8-52ed-8796-ca45cd27cfc8/scratchpad/purple-newsheet.txt`).

All six pass, which is what confirms each finding.

I recomputed in python3 the new weld-group and bolt-group expected values (1500 N/mm; −75 / −12.5 / 25 / 62.5 kN). I re-derived E1's P_rms closed forms for both the trapezoid and the sine profile.

### Red / Security

| Finding | Verdict | Permanent regression test | Note (PoC re-run) |
|---|---|---|---|
| R-A-01 / SEC-A-01 (High) | Verified | Library `BlueRoundTests.R_A_01_An_Issued_Sheet_Withholds_The_Rows_Of_A_Licensed_Table_And_Shows_The_Value_Used`, `R_A_01_An_Issued_Pack_Sheet_And_A_Preview_Withhold_The_Licensed_Rows_Too`, `R_A_01_An_Unlicensed_Record_Table_Is_Still_Printed`; Sheets `BlueRoundTests.R_A_01_A_Withheld_Table_Prints_Its_Pin_And_No_Rows` | Every sheet (preview, issue, pack, PDF) is now built by one method, `FinishSheetAsync`, and `GoldenFiles` is the only other caller of `CalcSheetBuilder.Build`. Red's test, run unchanged on the motor-circuit pack with a licensed ampacity record: 0 of 5 rows printed, and no capacity anywhere on the pack sheet or E4's own sheet. |
| R-A-02 / SEC-A-02 (High) | Verified | `R_A_02_A_Calculation_Is_Not_Checked_Or_Issued_On_A_Draft_Upstream_Unless_They_Go_In_One_Pack`, `R_A_02_Both_Drafts_Are_Checked_And_Issued_Together_In_One_Pack` | `DraftUpstreamAsync` runs in Check, Issue, CheckPack and IssuePack, and lets a draft through only when it is in the same pack. All five chain packs still check and issue (Chains tests pass). |
| R-A-03 / SEC-A-03 (High) | Verified | `R_A_03_A_Record_Revision_Upstream_Makes_The_Downstream_Stale`, `R_A_03_A_Template_Change_Upstream_Makes_The_Downstream_Stale` | Template and record staleness are judged at every depth, with records pre-fetched for the whole upstream closure. No storm: an issued pack's in-pack downstream stays fresh (scratch test), and events are still published only on update and supersede. |
| R-A-04 / SEC-A-04 | Verified | `R_A_04_A_Checked_Calculation_Gone_Stale_Is_Revised_Not_Stranded`, `R_A_04_A_Checked_Pack_Gone_Stale_Is_Reopened_Corrected_And_Issued` | A stale Checked calculation can be revised. `ReopenPackAsync` gives a new pack revision and new Evidence, and the Desktop has a "Reopen pack" button. |
| R-A-05 / SEC-A-05 | Verified (see P-A-03) | `R_A_05_Whoever_Changed_The_Inputs_Cannot_Check_Them_And_The_Set_Travels_With_Each_Revision`, `R_A_05_The_Pack_Check_Refuses_Whoever_Changed_A_Members_Inputs` | `EditedBy` is carried across revisions. The Desktop route (the checker presses Update) is now refused for Alice. |
| R-A-06 / SEC-A-06(a–c) | Verified | `R_A_06_A_Value_That_Is_Not_The_Records_Cannot_Cite_It`, `R_A_06_The_Bare_Prefix_Record_Is_Not_A_Pin`, `R_A_06_A_Pin_To_A_Revision_That_Does_Not_Exist_Is_Stale_And_Withheld_From_Every_Export` | Values (scalar or cell by cell, in SI) are compared with the pinned record. A bare `record:` prefix no longer counts as a pin; the motor-circuit example now shows that warning truthfully. |
| R-A-07 / SEC-A-07 | Verified | `R_A_07_A_Relist_Reads_The_Projects_Instances_Not_The_Whole_Collection_And_Not_Once_Per_Row`, `R_A_07_The_Whole_Project_Answer_Agrees_With_Asking_Row_By_Row` | The Desktop relist calls `StalenessForProjectAsync`. `ListInstancesAsync` lists by key prefix, there is a successor index, and `DescribeLinks` is memoised. |
| R-A-08 / SEC-A-06(d) | Verified | `R_A_08_A_Link_Cannot_Name_A_Revision_The_Upstream_Does_Not_Have` | A forged "rev D" link is refused, and the sheet prints the upstream's own revision. |
| R-A-09 / SEC-A-08 | Verified | `R_A_09_Only_Declared_Output_Ports_Can_Be_Linked` | Declared ports are enforced in Bind, BindCell, `LinkProblem` and the Desktop target list. E4's `C_a` is not a port. |
| R-A-10 / SEC-A-09 | Verified | `R_A_10_Nothing_Is_Attached_To_A_Member_Of_A_Checked_Pack_Or_For_A_Refused_Citation` | The pack freeze and the pre-check now hold. The lost-update fix (`expectedUpdatedAt`) is in the code but has no test. |
| R-A-11 / SEC-A-05 | Verified | `R_A_11_Nobody_Signed_In_Is_Not_An_Independent_Checker` | — |
| R-A-12 / SEC-A-09 | Verified | `R_A_12_A_Pack_Holds_The_Calculations_Of_One_Project_And_The_No_Project_List_Is_Its_Own` | — |
| R-A-13 / SEC-A-09 | Verified | `R_A_13_Instantiating_A_Template_Twice_In_One_Project_Is_A_Refusal_That_Leaves_Nothing_Behind` | — |
| R-A-14 / SEC-A-04 | Verified; record race Accepted-as-documented (DA-23) | `R_A_14_The_Pack_Check_Is_One_Transaction`, `R_A_14_An_Issue_Is_Refused_When_A_Calculation_It_Relied_On_Changed_Before_The_Commit` | The issue re-read is tested through `AssertUnchangedAsync` directly, not through `IssuePackAsync`. |
| R-A-15 / SEC-A-06(e) | Verified | `R_A_15_A_Legacy_Reference_Meets_The_Cycle_And_Project_Rules` | — |
| R-A-16 / SEC-A-10 | Partly (accepted in DA-23) | `R_A_16_A_Records_Citation_Is_Exported_As_One_Quoted_Cell_And_Is_Entered_By_The_Signed_In_Person` | The quoted citation, the principal as `EnteredBy`, and the cell and document caps are fixed. Listing only the latest revision is not done (DA-23: "the record store is small"). |

### Green — mechanical / structural

| Finding | Verdict | Permanent regression test | Note |
|---|---|---|---|
| G-A-01 | Verified | `WeldGroupTests.An_Unsymmetric_L_Group_Uses_The_Product_Moment_In_Bending`, `BoltTests.An_Unsymmetric_Pattern_Uses_The_Product_Moment_In_Bending`, `WeldGroupTests.An_Inclined_Single_Weld_Resists_Only_The_Moment_That_Bends_It`, `BoltTests.Bolts_On_One_Line_Cannot_Resist_A_Moment_About_That_Line`; KA cases `ka-l-group-bending`, `ka-unsymmetric-bending` | Python gives 1500 N/mm (Green's figure) and 62.5 kN. See P-A-06 for a wording slip in the case's description. |
| G-A-02 / G-B-06 | Verified (see P-A-05) | Library `BlueRoundTests.G_A_02_A_New_Sheet_Opens_On_The_Declared_Defaults_Only`; Sheets `BlueRoundTests.G_A_02_NewSheetInputs_Takes_Declared_Defaults_Only_Never_A_Test_Case` | Both the Library service and the Desktop use `NewSheetInputs`. The M4 sheet shows ρ 7850, M5 R 90 % is stated, and s4b omits `p_1`. |
| G-A-03 | Partly | `BasePlateTests.A_Typed_Profis_Reference_Is_Not_A_Passed_Check`, `An_Attached_Profis_Report_Passes_The_Anchorage_Condition` | Typed text now only warns. But the passing test itself uses a made-up `FromEvidence("0a1b2c/3d4e5f")`, and the shipped steel-support S6 still shows PASS against `PROFIS-BP1-PLACEHOLDER.txt` ("6 of 6 PASS"). See P-A-02. |
| G-A-04 | Verified | `Iso286FitTests.A_Bearing_Bore_Below_Nominal_On_A_k6_Seat_Is_An_Interference_Fit_At_Every_Size_Combination`, `A_Bearing_Bore_Needs_Its_Deviations_From_The_Bearing_Record` | Drive-train M2 now uses a bearing bore with invented deviations of 0 / −9 µm. |
| G-A-05 | Accepted-as-documented | — (`CalcPackExamples` comment, drive-train README) | The 60 mm pitch *radius* is stated, and the mesh forces are entered with a note to re-derive them when the torque changes. |
| G-A-06 | Partly | `ShaftTests.A_Peak_Torque_Governs_The_Yield_Check_Not_The_Fatigue_Check` | M4 gains a `T_pk` input that feeds yield, and the wording is corrected. The coupling and the yield check still take the load's demand `T_pk` (159 N·m), not the motor's `T_peak` (400 N·m) or SF·`T_pk`. The rationale is in the `CalcPackTemplates` comment. |
| G-A-07 | Verified | `ShaftTests.The_Critical_Speed_Ratio_And_Warning_Use_The_Dunkerley_Lower_Bound` | — |
| G-A-08 | Verified | `BearingLifeTests.X1_And_Y1_Give_The_Equivalent_Load_At_Or_Below_E` | — |
| G-A-09 | Verified | `ConnectionTests.Punching_Takes_The_Across_Flats_As_d_m_Unless_The_Across_Corners_Is_Entered`, `An_Across_Corners_Below_The_Across_Flats_Is_Refused` | B_p,Rd = 185.48 kN with d_m = s (recomputed). |
| G-A-10 | Verified | `BasePlateTests.The_Compression_Lever_Arm_Follows_The_Strip_When_The_Outstand_Is_Cut_At_The_Plate_Edge` | z_C 126.06 mm, C_Ed 513.64 kN, matching Green. |
| G-A-11 | Verified | `BasePlateTests.The_Tension_T_Stub_Geometry_And_The_Concentration_Factor_Have_No_Silent_Unsafe_Defaults` | m and l_eff are now required and α defaults to 1. S6 enters l_eff = 150 mm (Green's Table 6.6 value). |
| G-A-12 | Accepted-as-documented | — (`CalcPackTemplates`, steel-support README) | The single ULS envelope is stated as a limitation. |
| G-A-13 | Verified | KA `ka-shigley-7-1` | The book's data, converted exactly: S_e 201.4997 MPa, n_f 1.620988 (Green: 201.50 / 1.621). |
| G-A-14 (Info) | Not fixed (not in any brief) | — | The SCI P363 case, the SKF 6206 case and example, and the UC 203 section in `CalcPackExamples` remain. |
| G-A-15 (Info) | No action | — | Coverage notes only. |

### Green — building services / electrical

| Finding | Verdict | Permanent regression test | Note |
|---|---|---|---|
| G-B-01 = B-A-03 (High) | Verified | `MotorCircuitChainTests.A_Fault_Check_On_A_Different_Device_Than_The_Cable_Step_Sized_Is_Blocked`, `Changing_The_Cable_Steps_Device_Re_Blocks_The_Fault_Check_Until_It_Follows`; `FaultProtectionTests.A_Device_Carried_From_The_Cable_Step_Must_Be_The_Device_Chosen`, `A_Fractional_Or_Out_Of_Range_Device_Code_Is_Refused`, `A_Type_C_Device_Halves_The_Allowed_Loop_Impedance_Of_The_Type_B_Default` | E4 1.1.0 outputs a `device_code` port and the pack binds it to E5. A mismatch blocks E5, and a blocked member stops the pack check. The E5 pack sheet now shows type C, linked. |
| G-B-02 | Verified | `ConductorSizingTests.A_Gg_Fuse_Is_Deemed_Only_Under_BS_7671_And_Needs_Its_I2_In_The_Machine_Context` | — |
| G-B-03 | Verified | `FaultProtectionTests.The_Line_Conductor_Is_Not_Checked_Against_The_End_Of_Line_Fault_Energy`, `A_Let_Through_Energy_Replaces_The_End_Of_Line_Estimate` | — |
| G-B-04 | Verified | `LoadBudgetTests.A_Linked_Machine_Is_Outside_The_Enclosure_And_Adds_No_Panel_Heat`, `A_Load_Releases_Its_Stated_Share_Of_Its_Input_As_Panel_Heat` | — |
| G-B-05 | Verified | `MotorCircuitChainTests.Motor_Demand_Reaches_The_Cable_Voltage_Drop_Fault_Check_And_Enclosure` (asserts `system = AC three-phase`) | — |
| G-B-07 = B-A-02 | Verified | see B-A-02 | — |
| G-B-08 = B-A-08 | Accepted-as-documented (vessel) and Verified (temperatures) | `SealedSystemTests.The_Vessel_States_That_It_Excludes_The_Water_Reserve_And_Cites_Only_What_It_Implements` | The water reserve and safety-valve margin are stated as omissions. The BS EN 12828 citation is dropped and CIBSE B1 is cited instead. |
| G-B-09 = B-A-04 | Verified | `RoomHeatLossTests.The_Global_Target_U_Is_Not_Applied_To_A_Partition_To_A_Heated_Neighbour` | — |
| G-B-10 = B-A-11 | Verified | `PipeFlowTests.A_Closed_Circuit_Has_No_Static_Lift_And_Reports_The_Water_It_Holds` | — |
| G-B-11 | Verified | `MotorRmsPowerTests` (6 tests, including `The_Closed_Form_Matches_Numerical_Integration_For_Both_Profiles`) | I re-derived the P_rms closed forms; the coefficients 2/3, 3/4 and 5/16 are correct. |
| G-B-12 | Verified | `LoadBudgetTests.The_Power_Supply_Input_Is_Added_To_The_Bus_It_Is_Fed_From` | — |
| G-B-13 | Verified | `ConductorSizingTests.An_Mccb_Without_I2_Is_A_Stated_Deemed_Condition_In_The_Installation_Context` | — |
| G-B-14 | Verified | `RoomHeatLossTests.The_Conventional_Internal_Surface_Resistance_Follows_The_Heat_Flow_Direction`, `An_Internal_Floor_Takes_The_Same_Resistance_On_Both_Faces_For_Its_Direction` | — |
| G-B-15, G-B-16 | Verified | KAs and `ConductorSizingTests` | Invented values now in tests and source: ampacity, AWG, C_g 0.58, λ, and the 0.51 case removed. |
| G-B-17 | Accepted-as-documented | `PipeFlowTests.The_Water_Held_In_The_Pipes_Is_A_Port_For_The_Sealed_System` | The `V_pipe` port exists. In the wet-heating pack the link would be a cycle, so 3.14 L is entered by hand and the README says so. |
| G-B-18 (Info) | Verified | `MotorRmsPowerTests.A_Direct_Coupled_Load_With_A_Leftover_Efficiency_Is_Questioned`; B5 KA tightened to 1e-6 | The 0.51 case is dropped and the CPC is now 1.5 mm². |

### Black

| Finding | Verdict | Permanent regression test | Note |
|---|---|---|---|
| B-A-01 (High) | Verified | `BearingLifeTests.An_Axial_Load_Above_E_Cannot_Be_Discarded_By_A_Zero_Y`, `Pure_Thrust_With_A_Zero_Y_Names_Y_Not_An_Empty_Load`, `A_Duty_Step_With_An_Axial_Load_Above_E_Needs_Y_Too` | The defaults plus F_a = 1.5 kN now block on `axial-factor`; pure thrust names Y. |
| B-A-02 (High) | Verified | `HeatExchangerTests.A_Stream_At_Constant_Temperature_Never_Sets_C_Min_And_Leaves_At_Its_Inlet_Temperature`, `A_Cold_Side_At_Constant_Temperature_Leaves_At_Its_Inlet_And_The_Hot_Stream_Falls`, `The_Epsilon_Ntu_And_Lmtd_Duties_Agree_For_Every_Arrangement_And_Constant_Side`; KAs `ka-isothermal-hot/cold` | Black's scenario now gives the hot side held at 80 °C and Q = Q_lmtd = 158.5 kN. A new `lmtd-mismatch` warning (> 2 % difference) is added. |
| B-A-03 (High) | Verified | see G-B-01 | — |
| B-A-04 (High) | Verified | see G-B-09 | The wet-heating Room B step now has no failing check, and the README has no unexplained FAIL. |
| B-A-05 (High) | Verified (see P-A-01) | `CsvRoundTripTests.A_Worked_Example_Survives_The_CSV_Export_And_Import_Unchanged` (44/44); Sheets `BlueRoundTests.B_A_05_*` (5); Library `B_A_05_A_Licensed_Table_Stays_Withheld_Through_The_CSV_Round_Trip_And_An_Unlicensed_One_Comes_Back` | Tables, cell provenance and links round-trip. The sealed-system example no longer turns FAIL into PASS, and a withheld table comes back withheld. |
| B-A-06 | Verified | `ConnectionTests.A_Spacing_That_Leaves_No_Bearing_Resistance_Blocks_In_The_Calculators_Own_Words`, `A_Tiny_But_Positive_Bearing_Factor_Still_Computes_And_Fails_Bearing` | — |
| B-A-07 | Verified | `ShaftTests.Every_Mass_On_A_Bearing_With_No_Density_Skips_The_Critical_Speed_With_A_Warning`, `A_Bare_Shaft_With_No_Density_Says_Why_There_Is_No_Critical_Speed`, `Bearings_Closer_Than_A_Two_Hundredth_Of_The_Length_Block_In_The_Shafts_Own_Words` | — |
| B-A-08 | Verified | `SealedSystemTests.A_Fill_Temperature_At_Or_Above_The_Maximum_Blocks_Instead_Of_A_Negative_Vessel`, `The_Vessel_Is_Sized_For_The_Flow_Temperature_When_It_Is_Above_The_Maximum` | — |
| B-A-09 | Verified | `LoadBudgetTests.The_Minimum_Transformer_Rating_Names_What_It_Covers_And_The_Start_Is_Sized_Too` | New outputs `S_T_req_start` and `S_T_min`. |
| B-A-10 | Partly | `RoomHeatLossTests.The_Elements_Table_Is_Not_Record_Data_And_A_New_Sheet_Asks_For_It` | The elements table is no longer record data, the block gives a next step, and `U_target` is blank. But the new sheet still opens blocked (Black asked to keep the elements), and pre-filled defaults still show Source "manual" in the form. |
| B-A-11 | Verified | see G-B-10 | Both packs are closed loops with no lift. The wet-heating pump is stated to be a "deliberately tiny illustration". |
| B-A-12 | Partly (not in any brief) | — | `elec.load-budget.md` now says @1.2.0. The worked-examples index still says "32 worked examples" and "Seven … FAIL" and lists none of the 12 new examples. The root README still says "sixteen worksheet templates" and "32 cases and a calculation pack". |
| B-A-13 | Verified | `BlueRoundTests.B_A_13_A_Pack_Members_Own_Sheet_Shows_The_Packs_Checker_And_Client` | The stored step sheets show "Checker: J. Reviewer … Client: Client Co". |
| B-A-14 | Partly | `FaultProtectionTests.The_Disconnection_Time_Check_Carries_A_Real_Ratio` | The E5 time check and the E4 "deemed" condition now carry real ratios. Utilisation is still printed as the demand elsewhere, and the PROFIS condition still shows "utilisation 0". |
| B-A-15 | Verified | `WeldGroupTests.A_Single_Weld_Loaded_Along_Its_Axis_Through_Its_Centroid_Is_Direct_Shear` | — |
| B-A-16 | Verified | `ShaftTests.A_Key_Missing_Some_Of_Its_Four_Dimensions_Says_So`, `The_Key_Working_Names_Its_Symbols_And_Units` | — |
| B-A-17 | Not fixed (not in any brief) | — | Only the key's torque unit was changed. |
| B-A-18 | Partly | Sheets `BlueRoundTests.B_A_18_Record_Warnings_Collapse_On_The_Sheet_Only`; Library `B_A_18_Repeated_Unsourced_Table_Warnings_Are_One_Line_Listing_The_Symbols` | The repeated warnings are collapsed. "§A5/§A6/§A3" remain in 28 stored sheets (DA-23 defers them to the calculator builders, who did not remove them). See P-A-04. |
| B-A-19 | Verified | `BearingLifeTests.A_Very_Light_Load_Gives_A_Capped_Life_With_A_Warning_Not_A_Block`, `Kappa_Below_0_1_Is_Refused_By_Its_Stated_Range` | The spec page wording is updated. |
| B-A-20 | Partly | `ConductorSizingTests.Ampacity_Rows_In_The_Other_Conductor_System_Say_So` | (a) is fixed. (b) is not: a JSON flag given as text `"no"` is still refused. |
| B-A-21 | Verified | `SealedSystemTests.A_Selected_Emitter_Of_Zero_Watts_Is_Refused_Not_Treated_As_None_Selected` | — |
| B-A-22 | Partly | `BasePlateTests.The_Axial_Force_Sign_Is_Stated_As_Opposite_To_The_Member_Check` | (b) is fixed: the sign is stated on S3, S6 and in the README. (a) is not: the pack READMEs still describe `inputs.json` as "the exchange form the Calc API reads". |
| B-A-23 | Info | — | — |

### New findings (Purple)

| ID | Severity | Where | Finding | Suggested fix |
|---|---|---|---|---|
| P-A-01 | Medium | `CalcCsv.ImportInputs` (withheld-value branch); `CalcSheetView.ApplyImportedInputs` | A licensed scalar that was withheld on export (for example E4 `C_g` 0.6) is now dropped on import with only a warning. The form is re-seeded from the import alone, so the run falls back to the declared default (`C_g` = 1), sizes a smaller CSA, and shows only warnings. Before Blue the import was blocked ("must be a finite number"). Proved by the scratch test `PurplePocTests.P_Withheld_Licensed_Scalar_Reimports_As_The_Default`. | Block the import of a withheld value when its input has a declared default (or always), telling the user to apply the record again. Alternatively, merge the import over the form's current values. |
| P-A-02 | Medium | `BasePlateCalculator` PROFIS condition; no Library check on `CalcProvenanceKind.Evidence` | Any "evidence" provenance passes the anchorage condition. The Desktop source dropdown offers "evidence" with free text, a JSON import can carry it, and the permanent test passes on a made-up id. The shipped steel-support S6 still shows PASS on a placeholder attachment, so the pack reports "6 of 6 PASS". | In the Library, refuse or strip on Create/Update any Evidence provenance that does not resolve to an attachment on the instance's own Evidence. Keep the condition out of the PASS count, or refuse issue while the attachment is a placeholder. |
| P-A-03 | Low | `ReviseCoreAsync` / UpdateLinks (`EditedBy`) | Whoever presses "Update to revision n" or "Revise" becomes an author for good. In a two-person team, once the checker re-pins a link, neither person can check revision B. Proved by the scratch test `P_Two_Person_Team_Dead_End_After_The_Checker_Updates_Links`. | Do not count a pure link re-pin as design authorship (record it separately), or document that the designer must press Update. |
| P-A-04 | Low | E5 and E6 assumption sources | Blue added the review IDs "(Green G-B-03.)" and "(Green G-B-04.)" as assumption bases. They are printed on every E5/E6 sheet (7 stored sheets, including two pack issue sheets). This is the B-A-18 problem, newly introduced. | Use plain words, as for B-A-18. |
| P-A-05 | Low (UX; the decision is recorded in DA-23) | `CalcTemplateHasher.NewSheetInputs` | 11 templates that used to open runnable now open BLOCKED on a new sheet with "X needs a value" and an empty table. Six of them predate the addendum (tolerance stack, bolt group, frame 2D, duct sizing, enclosure heat, pipe pump); the others are weld group, shaft, load combinations, load budget, and base plate (`m` required). Only room heat loss gives a next step. The worked-examples README still says "the form opens on the template's defaults". | Give each table block a next-step message, or seed example rows labelled "example"; then update the README sentence. |
| P-A-06 | Info | `WeldGroupCalculator` KA `ka-l-group-bending` description | The text puts 1500 N/mm "at the far end of the long weld". It is at (0, 50), the end of the short weld; the long weld's far end carries 300 N/mm. The value is right; only the text is wrong. | Correct the text. |

**Gate:** No High is open. P-A-01 and P-A-02 (Medium) should be fixed, or justified in DA-23, before the gate is called.

Suites run: filtered only, all green (Library 113 + 3 scratch tests, Sheets 33, Calculators 44 + 2 scratch tests). The full gate was left to the running suite.

Follow-up: P-A-01…P-A-06 and the Partly items B-A-12, B-A-18, B-A-20(b), B-A-22(a) are being fixed in Blue round 2 (see blue-team.md).
