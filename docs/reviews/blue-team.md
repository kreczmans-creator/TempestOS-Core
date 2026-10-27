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

## Addendum A

Inputs: `security-review.md` (SEC-A-01…SEC-A-10), `red-team.md` (R-A-01…R-A-16), `green-team.md` (G-A-01…G-A-15, G-B-01…G-B-18), `black-team.md` (B-A-01…B-A-23) and the Purple verification (`purple-team.md`, Addendum A). Blue round 1 is on `v0.26.0/addA-9-blue` (commits `64d570d1` Library, Sheets, records, linking and CSV; `874bc235` electrical; `3c637a3d` mechanical, structural and building services; `f3a1056d` motor-circuit example; `5510ad03` regenerated goldens, examples, pack examples and PDFs). The decision record is DA-23, with DA-17 to DA-22 for the earlier rounds.

Every High is fixed. Purple re-ran the reviewer's scenario against the fixed code for each one and it now fails. Statuses below are Purple's verdicts. "Open → Blue round 2" means the item is not fixed and is in the round 2 brief.

Paths: `Library/` is `src/Tempest.Calc/Tempest.Calc.Library/`; `Sheets/` is `src/Tempest.Calc/Tempest.Calc.Sheets/`; `Mech/`, `Struct/`, `BS/` and `Elec/` are the matching `src/Tempest.Calc/Tempest.Calc.*` projects; `Desktop/` is `src/Tempest.Desktop/CalcSheets/`. Tests are in `tests/Tempest.Calc.Tests.Library` (Library), `.Sheets` (Sheets) and `.Calculators` (Calculators). `MotorCircuitChainTests` is in `tests/Tempest.Calc.Tests.Library/Chains`. After the first name in a cell, further test names are in the same class.

### Red findings (R-A-01…R-A-16)

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| R-A-01 High — issued and pack sheets print a licensed table | One method, `FinishSheetAsync`, finishes every sheet (preview, issue, pack, PDF). It withholds the rows of a table pinned to a licensed record and prints the pin and citation. | `Library/CalcInstanceService.cs`, `Sheets/CalcSheetBuilder.cs` | Library `BlueRoundTests.R_A_01_An_Issued_Sheet_Withholds_The_Rows_Of_A_Licensed_Table_And_Shows_The_Value_Used`, `R_A_01_An_Issued_Pack_Sheet_And_A_Preview_Withhold_The_Licensed_Rows_Too`, `R_A_01_An_Unlicensed_Record_Table_Is_Still_Printed`; Sheets `BlueRoundTests.R_A_01_A_Withheld_Table_Prints_Its_Pin_And_No_Rows` | Fixed |
| R-A-02 High — Check and Issue accept a draft upstream | `DraftUpstreamAsync` runs in Check, Issue, CheckPack and IssuePack. A draft passes only when it is in the same pack. | `Library/CalcInstanceService.Linking.cs`, `CalcInstanceService.cs` | `BlueRoundTests.R_A_02_A_Calculation_Is_Not_Checked_Or_Issued_On_A_Draft_Upstream_Unless_They_Go_In_One_Pack`, `R_A_02_Both_Drafts_Are_Checked_And_Issued_Together_In_One_Pack` | Fixed |
| R-A-03 High — transitive staleness drops record and template changes | Template and record staleness are judged at every depth. Records are fetched once for the whole upstream closure. | `Library/CalcInstanceService.Linking.cs` | `BlueRoundTests.R_A_03_A_Record_Revision_Upstream_Makes_The_Downstream_Stale`, `R_A_03_A_Template_Change_Upstream_Makes_The_Downstream_Stale` | Fixed |
| R-A-04 Medium — a record revision strands checked work | A stale Checked calculation can be revised. `ReopenPackAsync` takes a checked pack back to a draft under a new pack revision and new Evidence; the Packs panel has a Reopen pack button. | `Library/CalcInstanceService.cs`, `Desktop/` | `BlueRoundTests.R_A_04_A_Checked_Calculation_Gone_Stale_Is_Revised_Not_Stranded`, `R_A_04_A_Checked_Pack_Gone_Stale_Is_Reopened_Corrected_And_Issued` | Fixed |
| R-A-05 Medium — the designer can check their own work | `CalcInstance.EditedBy` lists everyone who set up or changed the inputs, kept across revisions. The check is refused for the creator and all of them. | `Library/CalcInstance.cs`, `CalcInstanceService.cs` | `BlueRoundTests.R_A_05_Whoever_Changed_The_Inputs_Cannot_Check_Them_And_The_Set_Travels_With_Each_Revision`, `R_A_05_The_Pack_Check_Refuses_Whoever_Changed_A_Members_Inputs` | Fixed (side effect: P-A-03) |
| R-A-06 Medium — record provenance never verified | The held value (scalar, or table cell by cell in SI) is compared with the pinned revision. A bare `record:` prefix is not a pin. A pin to a missing revision is stale and withheld from every export. | `Library/CalcInstanceService.Records.cs`, `Sheets/CalcInputValidator.cs` | `BlueRoundTests.R_A_06_A_Value_That_Is_Not_The_Records_Cannot_Cite_It`, `R_A_06_The_Bare_Prefix_Record_Is_Not_A_Pin`, `R_A_06_A_Pin_To_A_Revision_That_Does_Not_Exist_Is_Stale_And_Withheld_From_Every_Export` | Fixed |
| R-A-07 Medium — staleness re-reads the whole collection | The relist asks `StalenessForProjectAsync` once per project. `ListInstancesAsync` lists by key prefix. A successor index is kept. `DescribeLinks` is memoised. | `Desktop/CalcSheetCoordinator.cs`, `Library/CalcInstanceStore.cs`, `CalcInstanceService.Linking.cs` | `BlueRoundTests.R_A_07_A_Relist_Reads_The_Projects_Instances_Not_The_Whole_Collection_And_Not_Once_Per_Row`, `R_A_07_The_Whole_Project_Answer_Agrees_With_Asking_Row_By_Row` | Fixed |
| R-A-08 Low — link revision label never checked | A link's revision label must equal the upstream's. The sheet prints the upstream's own revision. | `Library/CalcInstanceService.Linking.cs` | `BlueRoundTests.R_A_08_A_Link_Cannot_Name_A_Revision_The_Upstream_Does_Not_Have` | Fixed |
| R-A-09 Low — output ports are advisory | Where a template declares ports, only those are linkable (Bind, BindCell, `LinkProblem`, the Desktop target list). | `Library/CalcInstanceService.Linking.cs`, `Desktop/CalcSheetCoordinator.cs` | `BlueRoundTests.R_A_09_Only_Declared_Output_Ports_Can_Be_Linked` | Fixed |
| R-A-10 Low — AttachExternal ignores the pack freeze | It honours the pack freeze, checks the citing input before attaching, and passes the read's version (no lost update). | `Library/CalcInstanceService.ExternalEvidence.cs`, `CalcInstanceService.Records.cs` | `BlueRoundTests.R_A_10_Nothing_Is_Attached_To_A_Member_Of_A_Checked_Pack_Or_For_A_Refused_Citation` (the lost-update guard has no test) | Fixed |
| R-A-11 Low — "unknown" is an independent checker | The sentinel is not a person. Nobody signed in cannot check an independent-check calculation. | `Library/CalcInstanceService.cs` | `BlueRoundTests.R_A_11_Nobody_Signed_In_Is_Not_An_Independent_Checker` | Fixed |
| R-A-12 Low — packs not project-scoped | A pack holds the calculations of one project. The no-project list is its own. | `Library/CalcInstanceService.cs`, `CalcInstanceService.PackTemplates.cs`, `Desktop/CalcSheetCoordinator.cs` | `BlueRoundTests.R_A_12_A_Pack_Holds_The_Calculations_Of_One_Project_And_The_No_Project_List_Is_Its_Own` | Fixed |
| R-A-13 Low — Instantiate not atomic | A name clash is refused before anything is written. | `Library/CalcInstanceService.PackTemplates.cs` | `BlueRoundTests.R_A_13_Instantiating_A_Template_Twice_In_One_Project_Is_A_Refusal_That_Leaves_Nothing_Behind` | Fixed |
| R-A-14 Low — pack check in two transactions | The pack check commits with its Evidence in one transaction. IssuePack re-reads every member and every upstream it relied on inside the issue transaction. A record revised in the same instant is not covered. | `Library/CalcInstanceService.cs` | `BlueRoundTests.R_A_14_The_Pack_Check_Is_One_Transaction`, `R_A_14_An_Issue_Is_Refused_When_A_Calculation_It_Relied_On_Changed_Before_The_Commit` (tested through `AssertUnchangedAsync`, not `IssuePackAsync`) | Fixed; record race Accepted (DA-23) |
| R-A-15 Low — legacy `calc:` link skips the validator | A legacy reference meets the same cycle, project and existence rules and follows the upstream's successors. | `Library/CalcInstanceService.Linking.cs`, `CalcInstance.cs` | `BlueRoundTests.R_A_15_A_Legacy_Reference_Meets_The_Cycle_And_Project_Rules` | Fixed |
| R-A-16 Low — record API hardening | The citation is one quoted cell. `EnteredBy` is the signed-in person. Cells and texts are capped. A record over the document cap is refused. "List only the latest revision" is not done. | `Library/CalcInstanceService.Records.cs`, `CalcDataRecord.cs`, `CalcInstanceStore.cs` | `BlueRoundTests.R_A_16_A_Records_Citation_Is_Exported_As_One_Quoted_Cell_And_Is_Entered_By_The_Signed_In_Person` | Partly; the rest Accepted (DA-23) |

### Security findings (SEC-A-01…SEC-A-10)

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| SEC-A-01 High | As R-A-01. | As R-A-01 | As R-A-01 | Fixed |
| SEC-A-02 High | As R-A-02. | As R-A-02 | As R-A-02 | Fixed |
| SEC-A-03 High | As R-A-03. | As R-A-03 | As R-A-03 | Fixed |
| SEC-A-04 Medium | As R-A-04 (revise, reopen) and R-A-14 (one-transaction pack check and issue re-read). | `Library/CalcInstanceService.cs` | As R-A-04 and R-A-14 | Fixed; record race Accepted (DA-23) |
| SEC-A-05 Medium | As R-A-05 and R-A-11. | `Library/CalcInstance.cs`, `CalcInstanceService.cs` | As R-A-05 and R-A-11 | Fixed (see P-A-03) |
| SEC-A-06 Medium (a–e) | (a–c) as R-A-06; (d) as R-A-08; (e) as R-A-15. | `Library/CalcInstanceService.Records.cs`, `CalcInstanceService.Linking.cs` | As R-A-06, R-A-08, R-A-15 | Fixed |
| SEC-A-07 Medium | As R-A-07. | As R-A-07 | As R-A-07 | Fixed |
| SEC-A-08 Low | As R-A-09. | As R-A-09 | As R-A-09 | Fixed |
| SEC-A-09 Low | As R-A-10, R-A-12 and R-A-13. | As R-A-10, R-A-12, R-A-13 | As R-A-10, R-A-12, R-A-13 | Fixed |
| SEC-A-10 Low | As R-A-16. | As R-A-16 | As R-A-16 | Partly; the rest Accepted (DA-23) |

### Green findings — mechanical and structural (G-A-01…G-A-15)

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| G-A-01 Medium — out-of-plane bending ignores the product moment | The product moment of area enters bending for weld and bolt groups; a single inclined weld and bolts on one line resist only the moment that bends them. Known answers 1500 N/mm and 62.5 kN. | `Mech/GroupGeometry.cs`, `WeldGroupCalculator.cs`, `BoltGroupCalculator.cs` | `WeldGroupTests.An_Unsymmetric_L_Group_Uses_The_Product_Moment_In_Bending`, `BoltTests.An_Unsymmetric_Pattern_Uses_The_Product_Moment_In_Bending`, `WeldGroupTests.An_Inclined_Single_Weld_Resists_Only_The_Moment_That_Bends_It`, `BoltTests.Bolts_On_One_Line_Cannot_Resist_A_Moment_About_That_Line`; cases `ka-l-group-bending`, `ka-unsymmetric-bending` | Fixed (wording slip: P-A-06) |
| G-A-02 Medium — new sheets take the first test case | `NewSheetInputs` takes declared defaults only, never a test case. The Library service and the Desktop both use it. `DefaultInputs` stays for the template hash and known-answer runs. | `Sheets/CalcTemplateHasher.cs`, `Library/CalcInstanceService.cs`, `Desktop/CalcSheetView.cs` | Library `BlueRoundTests.G_A_02_A_New_Sheet_Opens_On_The_Declared_Defaults_Only`; Sheets `BlueRoundTests.G_A_02_NewSheetInputs_Takes_Declared_Defaults_Only_Never_A_Test_Case` | Fixed (side effect: P-A-05) |
| G-A-03 Medium — PROFIS condition cannot fail | Typed text only warns. An attached report passes the anchorage condition. | `Struct/BasePlateCalculator.cs` | `BasePlateTests.A_Typed_Profis_Reference_Is_Not_A_Passed_Check`, `An_Attached_Profis_Report_Passes_The_Anchorage_Condition` | Partly; P-A-02 → Blue round 2 |
| G-A-04 Low — bearing bore run as an H7 hole | The drive-train M2 step uses a bearing bore with its deviations from the bearing record (invented values 0 / −9 µm). | `Library/CalcPackExamples.cs`, `Mech/Iso286FitCalculator.cs` | `Iso286FitTests.A_Bearing_Bore_Below_Nominal_On_A_k6_Seat_Is_An_Interference_Fit_At_Every_Size_Combination`, `A_Bearing_Bore_Needs_Its_Deviations_From_The_Bearing_Record` | Fixed |
| G-A-05 Low — pinion forces typed beside a linked torque | The 60 mm pitch radius is stated; the forces carry a note to re-derive them when the torque changes. | `Library/CalcPackExamples.cs`, drive-train README | — | Accepted (DA-22, stated limit) |
| G-A-06 Low — `T_pk` described as the motor's torque | M4 gains a `T_pk` input that feeds yield, and the wording is corrected. The coupling and yield check still take the load's demand, not the motor's peak. | `Mech/ShaftCalculator.cs`, `Library/CalcPackTemplates.cs` | `ShaftTests.A_Peak_Torque_Governs_The_Yield_Check_Not_The_Fatigue_Check` | Partly (rationale in the template comment) |
| G-A-07 Low — Rayleigh ratio is the upper bound | The ratio and warning use the Dunkerley lower bound. | `Mech/ShaftCalculator.cs` | `ShaftTests.The_Critical_Speed_Ratio_And_Warning_Use_The_Dunkerley_Lower_Bound` | Fixed |
| G-A-08 Low — P forced to F_r when F_a/F_r ≤ e | X₁ and Y₁ give the equivalent load at or below e. | `Mech/BearingLifeCalculator.cs` | `BearingLifeTests.X1_And_Y1_Give_The_Equivalent_Load_At_Or_Below_E` | Fixed |
| G-A-09 Low — punching d_m from a sharp hexagon | d_m is the across-flats unless the across-corners is entered (and refused if below the across-flats). B_p,Rd 185.48 kN. | `Struct/ConnectionCalculator.cs` | `ConnectionTests.Punching_Takes_The_Across_Flats_As_d_m_Unless_The_Across_Corners_Is_Entered`, `An_Across_Corners_Below_The_Across_Flats_Is_Refused` | Fixed |
| G-A-10 Low — lever arm ignores a cut outstand | z_C follows the strip when the outstand is cut at the plate edge. z_C 126.06 mm, C_Ed 513.64 kN. | `Struct/BasePlateCalculator.cs` | `BasePlateTests.The_Compression_Lever_Arm_Follows_The_Strip_When_The_Outstand_Is_Cut_At_The_Plate_Edge` | Fixed |
| G-A-11 Low — unsafe silent defaults | `m` and `l_eff` are required; α defaults to 1. S6 enters l_eff = 150 mm. | `Struct/BasePlateCalculator.cs` | `BasePlateTests.The_Tension_T_Stub_Geometry_And_The_Concentration_Factor_Have_No_Silent_Unsafe_Defaults` | Fixed |
| G-A-12 Low — one ULS envelope in the steel-support chain | Stated as a limitation of the chain. | `Library/CalcPackTemplates.cs`, steel-support README | — | Accepted (DA-22, stated limit) |
| G-A-13 Info — Shigley example data | The book's data, converted exactly: S_e 201.4997 MPa, n_f 1.620988. | `Mech/ShaftCalculator.cs` | Known answer `ka-shigley-7-1` | Fixed |
| G-A-14 Info — §A5 data audit | Closed by the §A9 copyright audit (PASS with notes, DA-25): the SCI P363 row is a two-cell minimal citation equal to the EN 1993-1-8 formula output (Unconfirmed); the SKF 6206 ratings are the one attributed manufacturer set (owner to record the catalogue terms); the UC 203 section is a single user-input section and the example header now says so. | `CalcPackExamples.cs` header | — | Accepted (DA-25) |
| G-A-15 Info — coverage notes | No defect in what is computed. | — | — | No action |

### Green findings — building services and electrical (G-B-01…G-B-18)

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| G-B-01 High (= B-A-03) — E4 type C, E5 type B | E4 1.1.0 outputs a `device_code` port and the pack binds it to E5. A mismatch blocks E5, and a blocked member stops the pack check. | `Elec/ConductorSizingCalculator.cs`, `Elec/FaultProtectionCalculator.cs`, `Library/CalcPackTemplates.cs` | `MotorCircuitChainTests.A_Fault_Check_On_A_Different_Device_Than_The_Cable_Step_Sized_Is_Blocked`, `Changing_The_Cable_Steps_Device_Re_Blocks_The_Fault_Check_Until_It_Follows`; `FaultProtectionTests.A_Device_Carried_From_The_Cable_Step_Must_Be_The_Device_Chosen`, `A_Fractional_Or_Out_Of_Range_Device_Code_Is_Refused`, `A_Type_C_Device_Halves_The_Allowed_Loop_Impedance_Of_The_Type_B_Default` | Fixed |
| G-B-02 Medium — gG deemed rule in either context | The gG deemed I₂ condition applies only under BS 7671; the machine context needs I₂. | `Elec/ConductorSizingCalculator.cs` | `ConductorSizingTests.A_Gg_Fuse_Is_Deemed_Only_Under_BS_7671_And_Needs_Its_I2_In_The_Machine_Context` | Fixed |
| G-B-03 Medium — line conductor against minimum fault energy | The line conductor is not checked against the end-of-line fault energy. A let-through energy replaces the estimate. | `Elec/FaultProtectionCalculator.cs` | `FaultProtectionTests.The_Line_Conductor_Is_Not_Checked_Against_The_End_Of_Line_Fault_Energy`, `A_Let_Through_Energy_Replaces_The_End_Of_Line_Estimate` | Fixed |
| G-B-04 Medium — linked motor loss counted as panel heat | A linked machine is outside the enclosure; a load releases its stated share of its input as panel heat. | `Elec/LoadBudgetCalculator.cs` | `LoadBudgetTests.A_Linked_Machine_Is_Outside_The_Enclosure_And_Adds_No_Panel_Heat`, `A_Load_Releases_Its_Stated_Share_Of_Its_Input_As_Panel_Heat` | Fixed |
| G-B-05 Medium — E3 runs as DC | The pack links `system` = AC three-phase. | `Library/CalcPackExamples.cs` | `MotorCircuitChainTests.Motor_Demand_Reaches_The_Cable_Voltage_Drop_Fault_Check_And_Enclosure` | Fixed |
| G-B-06 Medium — sheets seeded from a test case | As G-A-02. | As G-A-02 | As G-A-02 | Fixed (side effect: P-A-05) |
| G-B-07 Medium (= B-A-02) — C_r = 0 outlet | See B-A-02. | `BS/HeatExchangerCalculator.cs` | See B-A-02 | Fixed |
| G-B-08 Medium (= B-A-08) — vessel omits the water reserve | The temperatures are fixed (see B-A-08). The vessel states that it excludes the water reserve and the safety-valve margin, and cites CIBSE B1 instead of BS EN 12828. | `BS/SealedSystemCalculator.cs` | `SealedSystemTests.The_Vessel_States_That_It_Excludes_The_Water_Reserve_And_Cites_Only_What_It_Implements` | Accepted (stated omission; DA entry to be added in round 2) |
| G-B-09 Low (= B-A-04) — global U target on partitions | The target U is not applied to a partition to a heated neighbour. | `BS/RoomHeatLossCalculator.cs` | `RoomHeatLossTests.The_Global_Target_U_Is_Not_Applied_To_A_Partition_To_A_Heated_Neighbour` | Fixed |
| G-B-10 Low (= B-A-11) — static lift on closed loops | A closed circuit has no static lift and reports the water it holds. | `BS/PipeFlowCalculator.cs` | `PipeFlowTests.A_Closed_Circuit_Has_No_Static_Lift_And_Reports_The_Water_It_Holds` | Fixed |
| G-B-11 Low — peak power linked as continuous | E1 gives an RMS power (closed forms for both profiles) and E6 takes that. | `Elec/MotorSizingCalculator.cs`, `Library/CalcPackTemplates.cs` | `MotorRmsPowerTests` (6 tests, including `The_Closed_Form_Matches_Numerical_Integration_For_Both_Profiles`) | Fixed |
| G-B-12 Low — PSU input missing from AC bus | The PSU input is added to the bus it is fed from. | `Elec/LoadBudgetCalculator.cs` | `LoadBudgetTests.The_Power_Supply_Input_Is_Added_To_The_Bus_It_Is_Fed_From` | Fixed |
| G-B-13 Low — MCCB without I₂ only warns | The deemed condition is stated in the installation context. | `Elec/ConductorSizingCalculator.cs` | `ConductorSizingTests.An_Mccb_Without_I2_Is_A_Stated_Deemed_Condition_In_The_Installation_Context` | Fixed |
| G-B-14 Low — one R_si for every element | R_si follows the heat-flow direction. | `BS/RoomHeatLossCalculator.cs` | `RoomHeatLossTests.The_Conventional_Internal_Surface_Resistance_Follows_The_Heat_Flow_Direction`, `An_Internal_Floor_Takes_The_Same_Resistance_On_Both_Faces_For_Its_Direction` | Fixed |
| G-B-15 Medium (§A5) — ampacity set matches a published column | Replaced with invented values in tests and source. | `tests/…Calculators/Electrical/ConductorSizingTests.cs` | Known answers and `ConductorSizingTests` | Fixed |
| G-B-16 Low (§A5) — AWG, C_g, λ and a catalogue factor | Invented values now; the 0.51 case is removed. | As G-B-15, plus B4 and B6 tests | As G-B-15 | Fixed |
| G-B-17 Low — B6 volume not linked from B1 | The `V_pipe` port exists. In the wet-heating pack the link would be a cycle, so 3.14 L is entered by hand and the README says so. | `BS/PipeFlowCalculator.cs`, `Library/CalcPackTemplates.cs` | `PipeFlowTests.The_Water_Held_In_The_Pipes_Is_A_Port_For_The_Sealed_System` | Accepted (DA-22, stated limit) |
| G-B-18 Info — loose expected values | B5 known answer tightened to 1e-6; the 0.51 case dropped; the CPC is 1.5 mm². | `BS/HeatExchangerCalculator.cs`, `Elec/MotorSizingCalculator.cs` | `MotorRmsPowerTests.A_Direct_Coupled_Load_With_A_Leftover_Efficiency_Is_Questioned` | Fixed |

### Black findings (B-A-01…B-A-23)

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| B-A-01 High — axial load discarded by Y = 0 | Defaults plus F_a above e block on `axial-factor`; pure thrust names Y. | `Mech/BearingLifeCalculator.cs` | `BearingLifeTests.An_Axial_Load_Above_E_Cannot_Be_Discarded_By_A_Zero_Y`, `Pure_Thrust_With_A_Zero_Y_Names_Y_Not_An_Empty_Load`, `A_Duty_Step_With_An_Axial_Load_Above_E_Needs_Y_Too` | Fixed |
| B-A-02 High — C_r = 0 gives the wrong outlet | The constant-temperature stream never sets C_min and leaves at its inlet temperature. New `lmtd-mismatch` warning above 2 %. | `BS/HeatExchangerCalculator.cs` | `HeatExchangerTests.A_Stream_At_Constant_Temperature_Never_Sets_C_Min_And_Leaves_At_Its_Inlet_Temperature`, `A_Cold_Side_At_Constant_Temperature_Leaves_At_Its_Inlet_And_The_Hot_Stream_Falls`, `The_Epsilon_Ntu_And_Lmtd_Duties_Agree_For_Every_Arrangement_And_Constant_Side`; `ka-isothermal-hot`, `ka-isothermal-cold` | Fixed |
| B-A-03 High (= G-B-01) | See G-B-01. | See G-B-01 | See G-B-01 | Fixed |
| B-A-04 High (= G-B-09) — FAIL in the issued wet-heating pack | See G-B-09. The Room B step has no failing check. | See G-B-09 | See G-B-09 | Fixed |
| B-A-05 High — CSV export drops tables, provenance and links | Tables, cell provenance and links round-trip (`name`, `name[r]`, `name[r].column`). A withheld value or table comes back withheld, with a warning. Proved over every worked example. | `Sheets/CalcCsv.cs`, `Desktop/CalcSheetView.cs` | `CsvRoundTripTests.A_Worked_Example_Survives_The_CSV_Export_And_Import_Unchanged` (44/44); Sheets `BlueRoundTests.B_A_05_*` (5); Library `B_A_05_A_Licensed_Table_Stays_Withheld_Through_The_CSV_Round_Trip_And_An_Unlicensed_One_Comes_Back` | Fixed (side effect: P-A-01) |
| B-A-06 Medium — division by zero at small spacing | The block is in the calculator's own words; a tiny positive bearing factor still computes and fails bearing. | `Struct/ConnectionCalculator.cs` | `ConnectionTests.A_Spacing_That_Leaves_No_Bearing_Resistance_Blocks_In_The_Calculators_Own_Words`, `A_Tiny_But_Positive_Bearing_Factor_Still_Computes_And_Fails_Bearing` | Fixed |
| B-A-07 Medium — shaft block messages | Zero density skips the critical speed with a warning; close bearings block in the shaft's words. | `Mech/ShaftCalculator.cs` | `ShaftTests.Every_Mass_On_A_Bearing_With_No_Density_Skips_The_Critical_Speed_With_A_Warning`, `A_Bare_Shaft_With_No_Density_Says_Why_There_Is_No_Critical_Speed`, `Bearings_Closer_Than_A_Two_Hundredth_Of_The_Length_Block_In_The_Shafts_Own_Words` | Fixed |
| B-A-08 Medium — negative expansion vessel | A fill temperature at or above the maximum blocks. The vessel is sized for the flow temperature when it is higher. | `BS/SealedSystemCalculator.cs` | `SealedSystemTests.A_Fill_Temperature_At_Or_Above_The_Maximum_Blocks_Instead_Of_A_Negative_Vessel`, `The_Vessel_Is_Sized_For_The_Flow_Temperature_When_It_Is_Above_The_Maximum` | Fixed |
| B-A-09 Medium — transformer minimum ignores the start | New outputs `S_T_req_start` and `S_T_min` name what each covers. | `Elec/LoadBudgetCalculator.cs` | `LoadBudgetTests.The_Minimum_Transformer_Rating_Names_What_It_Covers_And_The_Start_Is_Sized_Too` | Fixed |
| B-A-10 Medium — room heat loss opens blocked | The elements table is not record data; the block gives a next step; `U_target` is blank. The new sheet still opens blocked, and pre-filled defaults show Source "manual". | `BS/RoomHeatLossCalculator.cs` | `RoomHeatLossTests.The_Elements_Table_Is_Not_Record_Data_And_A_New_Sheet_Asks_For_It` | Partly; P-A-05 → Blue round 2 |
| B-A-11 Medium (= G-B-10) | See G-B-10. The pump is stated to be a deliberately tiny illustration. | See G-B-10 | See G-B-10 | Fixed |
| B-A-12 Medium — navigation docs | `elec.load-budget.md` says @1.2.0. The worked-examples index and the root README still give old counts and list none of the 12 new examples. Not in any brief. | `docs/calcs/electrical/elec.load-budget.md` | — | Partly; Open → Blue round 2 |
| B-A-13 Medium — step sheets lose checker and client | A pack member's own sheet shows the pack's checker and client. | `Library/CalcInstanceService.cs` | `BlueRoundTests.B_A_13_A_Pack_Members_Own_Sheet_Shows_The_Packs_Checker_And_Client` | Fixed |
| B-A-14 Low — utilisation printed as the demand | The E5 time check and the E4 deemed condition carry real ratios. Other checks still print the demand, and the PROFIS condition shows "utilisation 0". | `Elec/FaultProtectionCalculator.cs`, `Elec/ConductorSizingCalculator.cs` | `FaultProtectionTests.The_Disconnection_Time_Check_Carries_A_Real_Ratio` | Partly (residual risk) |
| B-A-15 Low — collinear block on a valid single weld | A single weld loaded along its axis through its centroid is direct shear. | `Mech/WeldGroupCalculator.cs` | `WeldGroupTests.A_Single_Weld_Loaded_Along_Its_Axis_Through_Its_Centroid_Is_Direct_Shear` | Fixed |
| B-A-16 Low — key check vanishes silently | A key missing some of its four dimensions says so; the key working names its symbols and units. | `Mech/ShaftCalculator.cs` | `ShaftTests.A_Key_Missing_Some_Of_Its_Four_Dimensions_Says_So`, `The_Key_Working_Names_Its_Symbols_And_Units` | Fixed |
| B-A-17 Low — unclear substitution lines | Only the key's torque unit was changed. Not in any brief. | — | — | Open |
| B-A-18 Low — review ids in every sheet | Repeated "not taken from a reference record" warnings print as one line listing the symbols (the run and form keep one per symbol). "§A5/§A6/§A3" remain in 28 stored sheets (DA-23 left them to the calculator builders). | `Sheets/CalcSheetBuilder.cs` | Sheets `BlueRoundTests.B_A_18_Record_Warnings_Collapse_On_The_Sheet_Only`; Library `B_A_18_Repeated_Unsourced_Table_Warnings_Are_One_Line_Listing_The_Symbols` | Partly; Open → Blue round 2 (see P-A-04) |
| B-A-19 Low — very light load blocks | A very light load gives a capped life with a warning. κ below 0.1 is refused by its stated range. | `Mech/BearingLifeCalculator.cs` | `BearingLifeTests.A_Very_Light_Load_Gives_A_Capped_Life_With_A_Warning_Not_A_Block`, `Kappa_Below_0_1_Is_Refused_By_Its_Stated_Range` | Fixed |
| B-A-20 Low — AWG rows and JSON flag text | (a) Ampacity rows in the other conductor system say so. (b) A JSON flag given as text `"no"` is still refused. | `Elec/ConductorSizingCalculator.cs`, `Sheets/CalcInputJson.cs` | `ConductorSizingTests.Ampacity_Rows_In_The_Other_Conductor_System_Say_So` | Partly; (b) Open → Blue round 2 |
| B-A-21 Low — zero-watt emitter | A selected emitter of 0 W is refused, not treated as "none selected". | `BS/SealedSystemCalculator.cs` | `SealedSystemTests.A_Selected_Emitter_Of_Zero_Watts_Is_Refused_Not_Treated_As_None_Selected` | Fixed |
| B-A-22 Low — pack `inputs.json` and sign convention | (b) The axial force sign is stated on S3, S6 and in the README. (a) The pack READMEs still call `inputs.json` the exchange form the Calc API reads. | `Struct/BasePlateCalculator.cs`, pack READMEs | `BasePlateTests.The_Axial_Force_Sign_Is_Stated_As_Opposite_To_The_Member_Check` | Partly; (a) Open → Blue round 2 |
| B-A-23 Info — positive results | None needed. | — | — | No action |

### New findings from Purple (P-A-01…P-A-06)

| Finding | Mitigation planned | Where | Regression test | Status |
|---|---|---|---|---|
| P-A-01 Medium — a withheld licensed scalar re-imports as the template default | Block the import of a withheld value when its input has a declared default, and tell the user to apply the record again; or merge the import over the form's current values. | `Sheets/CalcCsv.cs` (`ImportInputs`), `Desktop/CalcSheetView.cs` (`ApplyImportedInputs`) | To add (Purple's scratch test `P_Withheld_Licensed_Scalar_Reimports_As_The_Default` is the model) | Open → Blue round 2 |
| P-A-02 Medium — any "evidence" provenance passes the PROFIS condition | Refuse or strip Evidence provenance that does not resolve to an attachment on the instance's own Evidence. Keep the condition out of the PASS count while the attachment is a placeholder. | `Library/CalcInstanceService.cs`, `Struct/BasePlateCalculator.cs`, steel-support example | To add | Open → Blue round 2 |
| P-A-03 Low — pressing Update makes the checker an author | Record a pure link re-pin separately from design authorship, or document that the designer presses Update. | `Library/CalcInstanceService.cs` (`ReviseCoreAsync`, `UpdateLinks`) | To add (model: `P_Two_Person_Team_Dead_End_After_The_Checker_Updates_Links`) | Open → Blue round 2 |
| P-A-04 Low — review ids in E5 and E6 assumptions | Use plain words, as for B-A-18. | `Elec/FaultProtectionCalculator.cs`, `Elec/LoadBudgetCalculator.cs` | Template hygiene test for "(Green" and "§A" text | Open → Blue round 2 |
| P-A-05 Low — 11 templates open blocked on a new sheet | Give each table block a next-step message, or seed labelled example rows; update the worked-examples README sentence. | `Sheets/CalcTemplateHasher.cs`, the calculators concerned | To add | Open → Blue round 2 |
| P-A-06 Info — wrong end of the weld in a case description | Correct the text of `ka-l-group-bending`. | `Mech/WeldGroupCalculator.cs` | — | Open → Blue round 2 |

### Residual risk

No Critical or High finding is open. The risks that remain are these.

- The licence lock follows provenance, not numbers. A user can retype a licensed value with another provenance and it will export (DA-18, accepted). P-A-01 is the same kind of gap on the import side and should be closed in round 2.
- "Evidence" provenance is not verified (P-A-02). Until it is, the anchorage PASS in S6 and the "6 of 6 PASS" on the shipped steel-support pack rest on a placeholder file. The PASS is not evidence of an anchorage design.
- A record revised in the same instant as a pack issue is not caught (DA-23). The window is one transaction. The lineage rule can also refuse a link to a calculation still pinned to a superseded revision, and chain depth is not counted through lineage (DA-18).
- The record scan reads every revision. The record store is small, so the cost is low (DA-23).
- G-A-06 and B-A-14 are Partly. M4's yield check uses the load's demand torque, not the motor's peak. Some checks still print the demand as the utilisation. Neither gives a wrong PASS on the shipped examples, but a user must read the sheet.
- G-A-14 and B-A-17 are not in any brief. Three published-looking cases remain (SCI P363, SKF 6206, UC 203), and several substitution lines read oddly.
- G-B-08 is accepted on the strength of the sheet's own statement. It needs a DA entry. The vessel excludes the water reserve and the safety-valve margin, so a user must add them.
- The shipped worked examples use invented data and placeholder files. They show the method. They are not designs.
- Purple ran filtered suites only. The full gate follows round 2.

## Addendum B

Inputs: `security-review.md` (SEC-B-01…SEC-B-04), `red-team.md` (R-B-01…R-B-04) and the Purple verification (`purple-team.md`, Addendum B, including P-B-01…P-B-04). The fixes are on `v0.26.0/addB-4-workflows`:

| Round | Merged at | Commit | Finding | Change |
|---|---|---|---|---|
| 1 | 0ecf1749 | 67af5ac7 | SEC-B-01 | W1 CSV split-decimal rules |
| 1 | 0ecf1749 | 051647fb | SEC-B-02 | Licence lock per record |
| 1 | 0ecf1749 | 75a2d66d | SEC-B-03 | 64 KB start-tag cap and budget on every chunk |
| 1 | 0ecf1749 | 1dd76d2f | SEC-B-04 | Underflow refused in `LocaleNumber` |
| 2 | 134ca7bc | eb02ffb4 | P-B-01, P-B-03 | Unquoted numbers refused under a decimal comma with a comma delimiter; digit-led neighbours checked; columns named |
| 2 | 134ca7bc | 3a143b15 | P-B-02 | Underflow refused for workbook cells and JSON numbers |

Statuses are Purple's final verdicts at 134ca7bc.

Paths: `Import/` is `src/Tempest.Calc/Tempest.Calc.Import/`; `Library/` is `src/Tempest.Calc/Tempest.Calc.Library/`. Tests are in `tests/Tempest.Calc.Tests.Import` (`AddendumBSecurityRegressionTests`) and `tests/Tempest.Calc.Tests.Library/Materials` (`MaterialLibraryLicenceRevisionTests.cs`, class `MaterialLibraryTests`).

### Red and Security findings (R-B-01…R-B-04 = SEC-B-01…SEC-B-04)

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| R-B-01 / SEC-B-01 Medium — W1 CSV split decimal read as a whole number | **Round 1 (67af5ac7):** a value under a blank header column is refused, and a whole number followed by bare digits in an unread column is refused or blocked. **Round 2 (eb02ffb4):** under a declared decimal comma with a comma delimiter, any unquoted number in the value or expression column is refused (quote it or use `;`; ADR-0171). Under a decimal point, both columns are checked against any neighbour that starts with digits (plus an optional unit), with no column exempt. | `Import/Cad/TableParsers.cs`, `Import/Csv.cs` (`CsvTable.WasQuoted`) | `AddendumBSecurityRegressionTests.SEC_B_01_A_Split_Decimal_Is_Refused_Not_Read_As_A_Whole_Number`, `SEC_B_01_An_Ambiguous_Whole_Number_File_Is_Blocked_Until_Corrected`, `SEC_B_01_Honest_Files_Are_Still_Read_Without_A_Block`, and the P-B-01 tests | Fixed |
| R-B-02 / SEC-B-02 Medium — licensing a later revision left the earlier one exportable | A record is licensed when any of its revisions is (`CalcInstanceStore.IsLicensedInAnyRevisionAsync`, `CalcInstanceService.IsLicensedRecordAsync`). This is used by `LicensedRecordPinAsync` (clipboard CSV, sheets, links, save-from-inputs), `ExportRecordAsync` and the material CSV/JSON export. | `Library/CalcInstanceService.Records.cs`, `CalcInstanceStore.cs`, `MaterialLibrary.cs` | `MaterialLibraryTests.SEC_B_02_A_Material_Licensed_In_A_Later_Revision_Releases_No_Value_Through_An_Earlier_One`, `SEC_B_02_A_Material_That_Was_Never_Licensed_Still_Exports_Every_Revision` | Fixed |
| R-B-03 / SEC-B-03 Medium — xlsx attribute flood | `CountingLimitStream` follows start tags byte by byte (aware of quotes, skipping `<!` and `<?`) and refuses one over 64 KB. It checks the time budget and Cancel on every chunk. The macro-declaration attribute loop ticks the budget. | `Import/XlsxReader.cs` | `AddendumBSecurityRegressionTests.SEC_B_03_A_Start_Tag_With_Millions_Of_Attributes_Is_Refused_Fast_Not_Parsed_In_One_Uninterruptible_Read`, `SEC_B_03_A_Tag_Just_Over_The_Cap_Is_Refused_And_One_Just_Under_It_Is_Read`, `SEC_B_03_Long_Text_Comments_And_Attribute_Values_Are_Not_Mistaken_For_Tags`, `SEC_B_03_Cancel_Is_Honoured_While_The_Reader_Pulls_Bytes_Of_One_Tag` | Fixed |
| R-B-04 / SEC-B-04 Low — underflow read as 0 | **Round 1 (1dd76d2f):** `LocaleNumber` refuses a literal with a non-zero digit that parses to 0. **Round 2 (3a143b15):** the same check, `LocaleNumber.UnderflowedToZero`, is applied to every `.xlsx` numeric cell and every W1 JSON number. | `Import/LocaleNumber.cs`, `Import/XlsxReader.cs`, `Import/Cad/TableParsers.cs` | `AddendumBSecurityRegressionTests.SEC_B_04_A_Non_Zero_Literal_That_Underflows_To_Zero_Is_Refused_Like_An_Overflow`, `SEC_B_04_Zero_And_Representable_Small_Numbers_Are_Still_Read`, `SEC_B_04_An_Underflowing_Value_In_A_Cad_Csv_Is_Not_Imported_As_A_Number`, and the P-B-02 tests | Fixed |

### Purple findings (P-B-01…P-B-04)

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| P-B-01 Medium — three split shapes still read silently after round 1 | As R-B-01, round 2. | `Import/Cad/TableParsers.cs`, `Import/Csv.cs` | `P_B_01_Under_A_Decimal_Comma_An_Unquoted_Number_With_A_Comma_Delimiter_Is_Refused`, `P_B_01_Under_A_Decimal_Point_The_Same_Shapes_Are_Blocked_Not_Read_Silently`, `P_B_01_Quoted_Semicolon_And_Tab_Files_Are_Still_Read_Under_A_Decimal_Comma` | Fixed |
| P-B-02 Low — W1 xlsx and JSON numbers underflowed to 0 after round 1 | As R-B-04, round 2. | `Import/XlsxReader.cs`, `Import/Cad/TableParsers.cs`, `Import/LocaleNumber.cs` | `P_B_02_A_Workbook_Cell_That_Underflows_To_Zero_Is_Refused`, `P_B_02_Zero_And_Representable_Workbook_Cells_Are_Still_Read`, `P_B_02_A_Json_Number_That_Underflows_To_Zero_Is_Refused`, `P_B_02_Zero_And_Representable_Json_Numbers_Are_Still_Read` | Fixed |
| P-B-03 Info — "unnamed column" wording | The messages name the column the remainder landed in. | `Import/Cad/TableParsers.cs` | `P_B_03_The_Message_Names_The_Column_The_Remainder_Landed_In` | Fixed |
| P-B-04 Info — exponent-only decimal-point file warns instead of blocking | None. It is contrived, and the person is warned. | `Import/Cad/TableParsers.cs` | — | Accepted (RR-B-01) |

### Residual risk register (Addendum B)

| ID | Risk | Likelihood / impact | Owner | Status |
|---|---|---|---|---|
| RR-B-01 | A decimal-point W1 CSV whose thousands separator splits a whole number, while every other non-whole value is written in exponent form with no decimal point (P-B-04), imports with a warning rather than a block. Under a declared decimal comma with a comma delimiter, unquoted numbers are refused outright. The panel shows every value read, and the issued sheet cites the file hash. | Very low / Medium | Blue | Accepted |
| RR-B-02 | Desktop Cancel abandons the preview task (`WaitAsync`). The abandoned parse sees the same token on its next chunk, and one `Read()` is capped at a 64 KB start tag, so it stops within milliseconds. Apply runs with no token, bounded by the 10 s budget. | Low / Low | Blue | Accepted |
| RR-B-03 | The start-tag scanner works on bytes. A UTF-16 part whose text holds a character with a 0x3C byte, followed by more than 64 KB without a 0x3C or 0x3E byte, is refused. Cell text is capped at 8 KB in UTF-16, and Excel and LibreOffice write UTF-8. The failure is a loud refusal, never a wrong value. | Very low / Low | Blue | Accepted |
| RR-B-04 | `IsLicensedInAnyRevisionAsync` reads every revision of a record for each pinned input on export or sheet build. This is linear in the revisions and fine at desktop scale. | Low / Low | Blue | Accepted |
| RR-B-05 | A comma-delimited W1 CSV under a declared decimal comma must quote every number (ADR-0171). Honest unquoted files of that shape are refused with instructions (quote the numbers or save with `;`). A decimal-point file with a numeric description after a whole number is refused or blocked. Both are loud and never read a wrong value. | Low / Low (usability) | Blue | Accepted |

## Addendum C

Inputs: the colour-team findings on Addendum C in `white-team.md`, `black-team.md`, `orange-team.md`, `purple-team.md`, `green-team.md`, `yellow-team.md` and `grey-team.md` (W-C, B-C, O-C, P-C, G-C, Y-C, GR-C), and the review sections of ADR-0174, ADR-0175 and ADR-0176. `security-review.md` and `red-team.md` report Addendum C separately, in parallel; their findings and mitigations are not in this table. Scope: the delta `76935116..1474cd0a` on `release/v0.26.0` (C-1 fixes, C-3 record import, C-4 dashboard, C-5 guided test, C-6 demo installer). Every Opus review finding of those features was fixed before its merge and is not repeated.

Paths: `Library/` is `src/Tempest.Calc/Tempest.Calc.Library/`. The regression tests are in `tests/Tempest.Calc.Tests.Library/AddendumCColourRegressionTests.cs`.

### Mitigations shipped in this round

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| W-C-01 High: the ADR Register lacked ADR-0174 to ADR-0176, so the required Governance Health Check (Checks 1 and 14) fails | Three rows added and the "Last Reviewed" line updated; re-derived parity 176 files, 176 rows | `docs/governance/Architecture/ADR Register.md` | The Governance Health Check job on CI | Fixed |
| B-C-01 Medium: one damaged result in a stored guided run crashed the run readers and emptied the dashboard; an undefined outcome counted as a Pass | `Deserialise` leaves out a null result, a result with no step and an undefined outcome; it reads a missing note, values text or tester id as empty | `Library/ManualTesting/ManualTestRunService.cs` | `Black_B_C_01_A_Damaged_Result_In_A_Run_Is_Left_Out_And_Stops_No_List_Sheet_Or_Dashboard` | Fixed |
| O-C-01 Medium: the records screen read the whole record collection once per record kind (68), and twice more per unlicensed record; 2.2 s at 1,000 records | One read: `CalcInstanceStore.ListRecordsWithLicenceAsync` returns each latest record with whether any revision is licensed (the SEC-B-02 rule), grouped by kind; 0.18 s at 1,000 records | `Library/CalcInstanceStore.cs`, `Library/RecordImport/RecordImportService.cs` (`ListTypesAsync`) | `Orange_O_C_01_The_Records_Screen_List_Reads_The_Records_Once_Not_Once_Per_Kind_Or_Per_Record` (store calls counted; a record licensed only in revision 1 still counts as licensed) | Fixed |
| O-C-02 Low: the worksheet's missing-records panel read the collection once per record kind each time a form opened | One read per call, grouped by kind | `Library/RecordImport/RecordImportService.cs` (`RequirementsAsync`) | Same test, second part | Fixed |
| B-C-03 Medium: the removed demo project stayed in the project lists, twice after a re-install | `IProjectDirectory.ListAsync` leaves deleted projects out; `ListIncludingDeletedAsync` keeps them for the project number (`CreateAsync`, the next free number, the new-project prompt) | `Tempest.Workspace/Projects/IProjectDirectory.cs`, `ProjectDirectory.cs`; `Desktop/Views/ProjectBrowserView.cs`, `NewProjectPrompt.cs` | `Black_B_C_03_A_Removed_Demo_Project_Is_Not_Listed_For_A_Person_But_Still_Holds_Its_Number` | Fixed (round 2) |
| B-C-02 Low: Remove deleted a demo scan profile a real calculation used | `CalcInstanceStore.DeleteDemonstrationAsync` keeps a cited version, in the same transaction; the message names it | `Library/CalcInstanceStore.cs`, `Library/CalcDemoProjectService.cs` | `Black_B_C_02_Remove_Keeps_A_Demo_Scan_Profile_A_Calculation_In_Another_Project_Uses_And_Says_So` | Fixed (round 2) |
| B-C-04 Low: a cancelled install whose clean-up failed said it was undone | `PartlyDone` with "part of it is left" | `Library/CalcDemoProjectService.cs`, `Desktop/CalcSheets/DemoProjectViewModel.cs` | `Black_B_C_04_A_Cancelled_Install_Whose_Clean_Up_Fails_Says_Part_Of_It_Is_Left`, `…_Clean_Up_Works_Is_Still_A_Cancellation_And_Leaves_Nothing` | Fixed (round 2) |
| B-C-05 Low: a store failure while the records screen opened was lost | The view model turns it into a sentence on the screen and logs it | `Desktop/CalcSheets/CalcRecordsViewModel.cs` | `AddendumCColourDesktopTests.Black_B_C_05_…` | Fixed (round 2) |
| Y-C-01 … Y-C-05, W-C-03, G-C-01: wording, CHANGELOG and duplicated type names | No work-stream names on screens or exports; plain demo panel sentences; every records-screen failure names its action; the row cap counts data rows; "Unconfirmed" and "Secondary" spelled out; CHANGELOG lines for C-1, C-4, C-5 and C-6; the dashboard's types renamed (`IDashboardManualTestSource`, `DashboardManualTestStatus`, `DashboardManualTestOutcome`, `DashboardRecordRequirement`, …) | `Desktop/CalcSheets/`, `Library/Dashboard/`, `Library/RecordImport/RecordImportService.cs`, `CHANGELOG.md`, `docs/guides/verification-dashboard.md`, `docs/testing/v0.26.0-runbook.md` (and the regenerated guided script) | `Purple_P_C_01_…` (the row-cap sentence), `Green_G_C_01_The_Dashboard_And_The_Guided_Test_Types_Can_Be_Named_In_One_File`, `VerificationMarkdownTests` | Fixed (round 2) |
| P-C-01 Low: the record import's row and column caps were not exercised | Test added | — | `Purple_P_C_01_A_Record_File_At_The_Row_And_Column_Caps_Is_Read_And_One_Over_Either_Is_Refused_Whole` | Fixed |

### Defensive controls of the four features (as shipped, verified by Purple)

| Feature | Controls |
|---|---|
| C-3 record import | Untrusted bytes pass only through the ADR-0171 primitives (5 MB, strict UTF-8, RFC 4180, 1,000 data rows, 64 columns, declared number format, formula-like text refused). Units are checked per column. The calculators' hard limits apply, and every stored-but-unapplied number has its own hard range. A lookup table that runs the wrong way is an error. Text cells are one line. The commit re-judges the file and writes one transaction. Provenance is required, and the signed-in person and the file hash are stamped. The record is licensed by default and no export path is added. |
| C-4 dashboard | Reads only, through read-only interfaces. The snapshot reader never throws and caps everything. 500 rows. Exports escape markdown, force the extension, never overwrite unasked, write through a temporary file and stay under 8 MB. Any failure is a sentence. |
| C-5 guided test | The run service holds no calculation, Evidence or issue service, and a full run changes no other collection. A signed-in tester is required and only the starter records. A finished run is immutable. The caps: 1,000 runs, a 2,000-character note refused rather than cut, 4,000 characters of values. The script is size-capped and staleness-tested. Every handler is guarded. Deletion takes only finished runs beyond 50, never a status holder, after the app's dialog. |
| C-6 demo installer | Never at start-up, and only after the dialog. Identity is the flag and the number. The single delete is narrow: one project's key prefix, two named profile versions with the `demo-press-` prefix, one transaction, refused on any issued row, audited before the soft delete. A stale write to a removed row is refused. The watermark comes only from the project flag. |

### Residual risk register (Addendum C)

| ID | Risk | Likelihood / impact | Owner | Status |
|---|---|---|---|---|
| RR-C-01 | The removed demo project stays in the project lists as a deleted tombstone (Black B-C-03; ADR-0176 follow-up). After a re-install two `DEMO0-PRESS1` entries are listed. | High / Low (confusing, no data at risk) | Blue | Closed: fixed with B-C-03 (second colour round) |
| RR-C-02 | Removing the demo deletes its two scan-profile versions even when a calculation in a real project used them (Black B-C-02). The calculation keeps its import record but cannot repeat the import from the list. | Low / Low | Blue | Closed: fixed with B-C-02 (second colour round) |
| RR-C-03 | A cancelled demo install whose clean-up fails is reported as undone (Black B-C-04). Remove clears what is left. | Very low / Low | Blue | Closed: fixed with B-C-04 (second colour round) |
| RR-C-04 | A store failure while the records screen opens from the tree is not said (Black B-C-05). The next open retries. | Low / Low | Blue | Closed: fixed with B-C-05 (second colour round) |
| RR-C-05 | The run store at its cap is about 53 MB with notes of 300 characters, and the dashboard reads every run (0.8–1.5 s at 1,000 runs, Orange O-C-03). "Delete older finished runs" brings it back. | Low / Low | Blue | Accepted |
| RR-C-06 | Two app instances on one database could each install the demo (the gate is per process; Black B-C-06). | Very low / Low | Blue | Accepted |
| RR-C-07 | Templates moved in C-1 cannot re-run earlier checked or issued owner-test calculations (DC-14, Grey GR-C-01). | Certain for owner-test data / Low (nothing is released) | Owner | Open: decide before the first release |
| RR-C-08 | Two cable resistance defaults are recalled from a licensed table (DC-05, White W-C-02). | — / Low | Owner | Open |

### 2026-10-27: C-2, DC-21 builds and DI-08

Inputs: this round's findings in `security-review.md` (SEC-C-11 to SEC-C-16), `red-team.md` (R-C-06, R-C-07) and the colour reports' 2026-10-27 sub-sections. Scope: the work merged after the sections above, up to `08315a20`. It covers C-2 phases A to C, the SVG reader and the self-check (DC-22), ADR-0177, the DC-21 builds and DI-08.

#### Mitigations shipped in this round

| Finding | Mitigation | Where | Regression test | Status |
|---|---|---|---|---|
| SEC-C-11 / R-C-06 High: one class named many times exhausted the SVG reader's memory | The class list is a set of distinct names, read as spans. A class test is a lookup. | `src/Tempest.Desktop/Viewing/SvgPictureBuilder.cs` (`ClassSet`, `Context.RulesFor`) | `SvgHostileInputTests.A_Class_Named_Many_Times_On_One_Element_Applies_Its_Rule_Once`; bounded probe `css-class-named-4M-times` | Fixed in b34020c9 |

#### Defensive controls of the new surfaces (as shipped)

| Surface | Controls |
|---|---|
| SVG reader (DC-22) | The caps of DC-22, now with a distinct class set. A 15 s deadline off the UI thread. 250 ms regex timeouts. The sanitiser runs before the reader. No reference is followed. A part left out is named on the page. |
| Aliases (C-2) | One canonical name per input, column and port. A conflict is refused, naming both names. The catalogue refuses an alias that clashes. |
| Unit and meaning changes (C-2) | `UnitChangedAt` and `MeaningChangedAt` declare each change. A link, import or pack binding across a meaning change is refused. Contract tests pin the baselines. |
| Superseded versions (ADR-0177) | Rerun writes nothing and says "reproduced" only on an exact match. Otherwise the record opens read-only, with Revise as the way on. |
| Record kinds (DC-10) | Header-only templates. Column maps in code. Licensed by default. The export lock covers every kind. |
| Frame caps (DC-15) | 1,200 nodes, 2,400 members and 2,400 loads, refused at entry. The worst frame stores at 80 % of the 4 MB cap. |
| Self-check (DA-12) | Authors only, with a statement. Stored on the Evidence check. Printed only on the members its checker prepared. |

#### Residual risk register (2026-10-27)

| ID | Risk | Likelihood / impact | Owner | Status |
|---|---|---|---|---|
| RR-C-09 | The SVG probe job aborts at child start-up on CI, so only the scaled-down tests prove the caps (SEC-C-12). | Certain / Medium | Integrator | Open: make the job start, then rerun it |
| RR-C-10 | A long text run in an SVG is replayed on the UI thread at each zoom step (SEC-C-13, O-C-06). | Low / Low | Blue | Open |
| RR-C-11 | About 20 % of the superseded versions never replay, so their calculations open read-only (DC-23). | Certain for those versions / Low (nothing is released) | Owner | Open: decide before the first release |
| RR-C-12 | A draft can claim a National Annex record that does not exist until Check (SEC-C-14). | Low / Low | Blue | Accepted: Check and Issue refuse it |
| RR-C-13 | After the read deadline, a PDF or image read runs on in the background (SEC-C-16). | Low / Low | Blue | Accepted |
| RR-C-14 | A frame whose every cell carries scan provenance can pass 4 MB at the caps. The store refuses it at save (B-C-07). | Low / Low | Blue | Accepted |
