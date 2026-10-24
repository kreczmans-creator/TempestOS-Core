# 🔴 Red team — offensive review

**Scope** `v0.26.0` calculation release, branch `v0.26.0/phase-6-security`, reviewed without the design specification: only the built code under `src/Tempest.Calc/`, `src/Tempest.Desktop/CalcSheets/`, `src/Tempest.Desktop/Documents/`, `src/Tempest.Core/Evidence/` and the existing tests.

**Method** Every attack is a proof-of-concept xunit test that asserts the *secure* behaviour, so it fails while the hole is open and turns green when it is fixed. The PoCs are left in place in new `Red_*` files (no existing file was touched): `tests/Tempest.Calc.Tests.Sheets/Red_ImportAttacks.cs` (CSV/JSON imports, rendered text, Monte Carlo cap), `tests/Tempest.Calc.Tests.Engine/Red_EngineAttacks.cs` (expression-engine caps under the default limits), `tests/Tempest.Calc.Tests.Calculators/Red_SolverAttacks.cs` (hostile table rows through every calculator, frame and polygon solvers, hard-limit sweeps), `tests/Tempest.Calc.Tests.Library/Red_ConcurrencyAndTamper.cs` (a gating store wrapper reproduces interleavings and crashes between two writes deterministically; tampering with issued records) and `tests/Tempest.Desktop.Tests/CalcSheets/Red_PdfRendererAttacks.cs`. Run with `dotnet test <project> -c Release --filter "FullyQualifiedName~Red_"`. Budget for a hang: 5 s; for a rendered sheet: 400 KB.

**Result** 138 PoC test cases (42 Sheets, 33 Engine, 45 Calculators, 11 Library, 7 Desktop); **44 failed at the time of the review** (every one but R-22 passes after the Blue fixes; see `blue-team.md`) (open holes behind 23 findings, R-01…R-22 and R-24); 94 pass (attacks that found nothing, plus R-23 as information). Note: the orchestrator committed the four Calc `Red_*` files mid-review as `aef280c`; the later additions to two of them, the Desktop file and this report are left uncommitted as instructed. The three to fix first are R-17 (a pack issues inputs that were changed after the check), R-14 (an issued record can be rewritten under a Superseded mark) and R-15 (a Check racing an Update, or a crash between the two writes of Check, leaves an instance that can never be checked again).

## Findings

| ID | Severity | Location | Description | Evidence | Proposed fix | Status |
|---|---|---|---|---|---|---|
| R-01 | Medium | `src/Tempest.Calc/Tempest.Calc.Sheets/CalcCsv.cs:94` | A header with a repeated (or empty) column name makes `ToDictionary` throw `ArgumentException`; the importer's "never throws for bad content" contract is broken by `symbol,value,value` or `symbol,value,,,`. | `Csv_Duplicate_Header_Columns_Do_Not_Throw`, `Csv_Wide_Rows_And_Row_Cap_Are_Fast`: both throw. | Build the column map with `TryAdd` (first wins) or refuse a duplicate header with a block notice. | Fixed (Blue) — PoC passes |
| R-02 | Medium | `CalcCsv.cs:256` (quoted branch of `ParseRows`) | `MaxFieldLength` (1 000) is enforced only for unquoted cells; a quoted cell grows to the 1 MB file cap and a 500 KB text input is imported. | `Csv_Quoted_Field_Respects_Field_Length_Cap`: 500 000-char cell imported; `Csv_Huge_Quoted_Text_Does_Not_Blow_The_Sheet`: sheet 501 146 chars. | Apply the same `field.Length >= MaxFieldLength` guard inside the quoted branch. | Fixed (Blue) — PoC passes |
| R-03 | Medium | `CalcCsv.cs:199` (`IsFormulaLike`) | The negative-number exemption lets any cell starting with `-1+…`/`+1+…` through unprefixed on export and unrefused on import; Excel evaluates `-1+cmd\|' /C calc'!A0` as a formula (DDE). | `Csv_Export_Neutralises_Sign_Prefixed_Formulas` (3 payloads), `Csv_Import_Refuses_Sign_Prefixed_Formula_In_Text`. | Exempt only cells that `double.TryParse` as a whole; prefix everything else starting with `+`/`-`. | Fixed (Blue) — PoC passes |
| R-04 | Medium | `src/Tempest.Calc/Tempest.Calc.Sheets/CalcInputJson.cs:45` | A `null` element in the list (`[null]`) dereferences `v.Symbol` → `NullReferenceException` escapes `Read`, which promises "never an exception". | `Json_Null_Element_Does_Not_Throw` (2 documents). | `values.Any(v => v is null \|\| …)` → block notice. | Fixed (Blue) — PoC passes |
| R-05 | High | `CalcInputValidator.cs:17` (no cap on `Provenance.Reference/Revision`), `CalcInstanceService.cs:104`, `CalcInstanceStore.cs:114` | Provenance text has no length cap: a 1.5 MB reference passes validation, runs, and renders a 1.5 MB sheet; through the service a 2 MB reference is accepted by `UpdateInputsAsync`, and `IssueAsync` then throws `InvalidOperationException` from inside the issue transaction when the 4 MB document cap trips. | `Json_Huge_Provenance_Reference_Is_Blocked_Or_Contained`: sheet 1 501 453 chars; `Oversized_Provenance_Is_Refused_And_Issue_Never_Throws`: Issue threw "larger than 4 MB"; `Oversized_Text_Is_Bounded_In_The_Pdf`: a 200 KB title + reference gives a ~2.3 MB PDF. | Cap `Reference`/`Revision` (e.g. 200 chars) in the validator and in `CalcInputJson.Read`; make `UpdateInputsAsync` refuse (not throw) when the serialised instance exceeds the cap. | Fixed (Blue) — PoC passes |
| R-06 | Medium | `src/Tempest.Calc/Tempest.Calc.Sheets/CalcSheetBuilder.cs:29` | A text input longer than 200 chars is blocked (correct) but the builder prints the entered value anyway, so a blocked draft sheet carries the whole 1.5 MB. | `Json_Oversized_Text_Input_Does_Not_Blow_The_Sheet`: sheet 1 501 149 chars. | Truncate `ValueText` for the inputs table (e.g. 200 chars + "…"). | Fixed (Blue) — PoC passes |
| R-07 | Low | `src/Tempest.Calc/Tempest.Calc.Sheets/InputValues.cs:32` | `IsSufficientToIssue` is `Kind != Manual`, so an undefined kind (JSON `"Kind": 99`, accepted by the enum converter) passes the issue gate. | `Json_Unknown_Provenance_Kind_Is_Not_Sufficient_To_Issue`. | Whitelist the defined kinds; reject undefined enum values on read. | Fixed (Blue) — PoC passes |
| R-08 | Low | `src/Tempest.Calc/Tempest.Calc.Sheets/CalcSheetTextRenderer.cs:188` | `Clean` strips `char.IsControl` only; U+2028/U+2029 (line/paragraph separators) survive and can forge a line in the "show working" text and the FTS index. | `Clean_Strips_Every_Line_Breaking_Character` (U+2028, U+2029 fail; U+0085 passes). | Also replace `UnicodeCategory.LineSeparator/ParagraphSeparator` (and consider bidi overrides U+202A–E, U+2066–9). | Fixed (Blue) — PoC passes |
| R-09 | Medium | `CalcRunner.cs:102`, `MonteCarlo.cs:31` | The sample cap (1 000 000) is the only guard and each sample re-runs the whole calculator; at the cap one call exceeds the 5 s budget (cancelled at 5 s on the smallest calculator). | `Monte_Carlo_At_The_Sample_Cap_Finishes_In_Time_Or_Is_Refused`: OperationCanceledException at 5.0 s. | Cap the generic Monte Carlo at a per-run budget (samples × cost) or a much lower default cap (e.g. 100 000) at the runner. | Fixed (Blue) — PoC passes |
| R-10 | High | `src/Tempest.Calc/Tempest.Calc.Engine/Parsing/Evaluator.cs:689` (`Flatten`) | `sum/avg/min/max/hypot/vec` materialise every element of every argument as a `ScalarValue` object before any cap: 64 arguments × a 1 000 000-element vector (both at the default caps) is 64 M allocations, 3.6 GB and 11–28 s in one 200-character expression. | `Variadic_Reducers_Over_Maximal_Vectors_Stay_In_Budget` (sum 12.8 s, max 11.4 s, vec 28.5 s; 3 633 MB each). | Check Σ lengths against `MaxArrayElements` before flattening and reduce over `double` spans, not boxed scalars. | Fixed (Blue) — PoC passes |
| R-11 | High | `src/Tempest.Calc/Tempest.Calc.Sheets/CalcInputValidator.cs:218` | A table row without a `Cells` dictionary (JSON `{"Rows":[{}]}` or `[null]`) makes `row.Get` dereference null; `CalcRunner.Run` throws `NullReferenceException` instead of blocking, for all 7 calculators with a table input. | `Table_Rows_Without_Cells_Never_Throw`: NRE for elec.enclosure-heat, bs.duct-sizing, bs.pipe-pump, mech.bolt-group, mech.tolerance-stack, struct.frame-2d, struct.section-properties. | Treat a null row / null `Cells` as an empty row in `CalcRow` (constructor default) and in the validator; add `NullReferenceException` to the runner's catch. | Fixed (Blue) — PoC passes |
| R-12 | Medium | `src/Tempest.Calc/Tempest.Calc.Structural/Frame/FrameSolver.cs:53`, `MemberLoads.cs:24` | Every load row adds a station to its member and every station evaluates every load (O(n²) Macaulay sums, ×3 per station, ×50 bisection steps at sign changes); 20 000 point loads on one member (the row cap) run far past the budget. | `Frame_With_Maximal_Load_Rows_On_One_Member_Finishes_In_Budget`: still running at 15 s. | Cap breakpoints per member (`MaxStations`), merge coincident loads, or precompute cumulative load sums per station. | Fixed (Blue) — PoC passes |
| R-13 | Medium | `FrameSolver.cs:198` | The 20 M banded-entry cap admits a 2 000-node random mesh whose RCM bandwidth is O(n); the LDLᵀ factor is O(n·b²) ≈ 10¹⁰ flops. | `Frame_Random_Mesh_At_The_Node_Cap_Finishes_In_Budget`: still running at 15 s. | Cap `free × half²` (factor cost), or cap the half-bandwidth, and poll the cancellation token inside the factor. | Fixed (Blue) — PoC passes |
| R-14 | High | `src/Tempest.Calc/Tempest.Calc.Library/CalcInstanceStore.cs:76,188` | The issued-record immutability check admits a write whose status is Superseded when `SameIssuedContent` holds — but that compares only counts, `Issue`, `IssuedAt`, `EvidenceId`; name, revision, issue reference, every input value, every output value and the checks can be rewritten. | `Issued_Content_Cannot_Be_Rewritten_Under_A_Superseded_Mark`: name → "TAMPERED", outputs → 999 999 MPa stored. | Compare the full record except `Status`/`UpdatedAt` (record equality on a copy with those fields normalised), or supersede inside `ReviseAsync`'s transaction only. | Fixed (Blue) — PoC passes |
| R-15 | High | `CalcInstanceService.cs:104–122` (Update), `:156–183` (Check), `CalcInstanceStore.cs:76` | No optimistic concurrency on instance writes: Update and Check both read a draft; Check records on the Evidence and writes Checked; the slower Update then writes Draft with new inputs. Same split state after a crash between the Evidence check commit and the instance write. Evidence Checked → Checked is not permitted, so the instance can never be checked, issued or revised again. | `Update_Racing_A_Check_Cannot_Revert_A_Checked_Instance`: instance Draft, Evidence Checked; `Crash_After_The_Check_Is_Recorded_Leaves_A_Recoverable_Instance`: retry refused "cannot be checked from that status". | Version the instance (`UpdatedAt`/sequence) and refuse a write whose base changed; write the Evidence check and the instance in one transaction; let Check be idempotent when the Evidence is already Checked by the same checker. | Fixed (Blue) — PoC passes |
| R-16 | Medium | `CalcInstanceService.cs:302–303` (Revise) | Revise writes the new draft, then supersedes the old one, in two transactions. A crash between them leaves A Issued with an orphan B draft, and every later Revise throws `DuplicateBusinessIdentifierException` (Evidence title "… (rev B)" exists): the calculation can never be revised. A concurrent second Revise escapes as that exception, not a refusal. | `Crash_Mid_Revise_Leaves_No_Orphan_Draft`: A Issued + 1 orphan; `Two_Simultaneous_Revisions_Produce_One_Revision_B`: `DuplicateBusinessIdentifierException`. | Write both instances in one transaction; map `DuplicateBusinessIdentifierException` to a refusal; on retry adopt the existing draft. | Fixed (Blue) — PoC passes |
| R-17 | High | `CalcInstanceService.cs:375` (CheckPack), `:104` (UpdateInputs), `:410` (IssuePack) | A pack check leaves its members Draft, so a member's inputs can be replaced after the pack was checked (`UpdateInputsAsync` succeeds) and `IssuePackAsync` issues the new, unchecked inputs under the checked Evidence. | `Pack_Member_Changed_After_The_Pack_Check_Cannot_Be_Issued`: update succeeded, issued, σ = 12 000 MPa (checker saw 60 MPa). | Refuse `UpdateInputsAsync` for a member of a checked pack; or record each member's `OutputsHash`/`UpdatedAt` at pack check and refuse issue when any differs. | Fixed (Blue) — PoC passes |
| R-18 | Low | `CalcInstanceService.cs:348` (CreatePack), `:410` | A pack listing one instance twice is accepted; its issue then throws `CalcConcurrentIssueException` from inside the transaction (second write sees the first), not a refusal. | `Pack_With_Duplicate_Members_Is_Refused_Or_Issues_Cleanly`. | `Distinct()` the ids at CreatePack (refuse duplicates); catch `CalcConcurrentIssueException` in `IssuePackAsync` as `IssueAsync` does. | Fixed (Blue) — PoC passes |
| R-19 | Medium | `src/Tempest.Core/Evidence/EvidenceService.cs:237` (`RecordCheckAsync`), `CalcInstanceService.cs:172` | Several concurrent checks on one draft succeed: the Evidence status transition is checked on a stale read, so more than one check record lands (last writer wins). | `Two_Simultaneous_Checks_Leave_One_Check`: 2–4 of 4 accepted (run-dependent). | Re-read and verify the status inside the check transaction. | Fixed (Blue) — PoC passes |
| R-20 | Medium | `CalcInputValidator.cs:218–270` (`ValidateTable`) | One block per offending cell, unbounded: a 20 000-row load table with every cell out of range yields ~200 000 notices and a multi-megabyte sheet (the 500-row hard-limit sweep already gave a 511 KB sheet). | `Validator_Notice_Count_Is_Bounded`, `Extremes_In_Every_Numeric_Input_Never_Throw(struct.frame-2d)`: sheet 511 137 chars. | Stop after N notices per input ("… and 19 990 more rows") and cap the applicability section in the renderer. | Fixed (Blue) — PoC passes |
| R-21 | Medium | `src/Tempest.Calc/Tempest.Calc.Mechanical/ToleranceStackCalculator.cs:56` | `n_mc`'s hard maximum is the engine sample cap (1 000 000); with 200 contributors one sheet runs ~20 s. | `Tolerance_Stack_At_Its_Hard_Limits_Finishes_In_Budget` (still running at 15 s), `Extremes_In_Every_Numeric_Input_Never_Throw(mech.tolerance-stack)`: 20.3 s. | HardMax 100 000 (the soft max) or budget samples × contributors. | Fixed (Blue) — PoC passes |
| R-22 | Low | `CalcInstanceService.cs:81` (Create) | A crash on the instance write of Create leaves an Evidence and a Calculation-register entry that govern nothing (and a reserved title). | `Crash_Mid_Create_Leaves_No_Orphan_Evidence`: 2 orphan objects. | Create the instance in the same transaction as the Evidence, or create the Evidence last. | Accepted (D-32) — PoC skipped with the justification |
| R-24 | Low | `src/Tempest.Desktop/CalcSheets/CalcSheetPdfRenderer.cs:67` (`Layout`) | The PDF renderer draws every string raw; `CalcSheetTextRenderer.Clean` is not applied, so NUL/ESC/CR land in the PDF text (and in any text extracted from the attachment). PDF syntax and unbalanced parentheses in the payload are escaped correctly by Skia (no injection). | `Hostile_Strings_Are_Drawn_As_Text("line1\r\nline2\t\0\x1b[31m…")`: NUL/ESC found in the extracted text. | Pass every string through `Clean` (and collapse to one line) before drawing. | Fixed (Blue) — PoC passes |
| R-23 | Info | `CalcSession.cs` `Bind` | `Define` checks `MaxArrayElements` but not `MaxMatrixOrder` for a matrix (2 001 × 1 accepted); the order cap bites only at use (`*`, `det`, `inv`, `solve`), never for `transpose`/`+`. | `Matrix_Work_Is_Bounded_By_The_Token_Cap` (passes: elements cap still holds). | Check the order in `Bind` too. | No issue — PoC passes |

## Attacks that found nothing (PoCs pass)

- **Imports:** JSON nesting beyond `MaxDepth`, `1e400`, `NaN`/`Infinity` tokens, numbers as strings, duplicate keys, duplicate symbols, non-list documents, empty documents, control characters and formula text in symbols and units; a 1 000-input document with 10 000-element vectors (fast). CSV: BOM, NUL, lone surrogates, CR/CRLF mixes, unterminated quotes, formula-like unit cells, a 10 000-column row, the 10 000-row cap. Unit expressions are capped at 64 characters however they are reached.
- **Rendered text:** control characters, CRLF, ANSI escapes, `<script>`, DDE and forged-notice text in every header, footer, revision, provenance, choice and text field are contained (R-08 aside).
- **Engine (default limits):** non-finite results (`10^400`, `0^-1`, `inf − inf`, `exp(1000)·0`, `mod(x, NaN)`) are refused at the step; huge, negative, NaN and fractional indices and `round` decimals; 1 000-token flat chains of `+`, `*`, `and`; nested parentheses/unary/calls (existing tests); matrix `inv`/`det`/`solve` bounded by the token cap; element-wise vector arithmetic at 1 000 000 elements; `Record`/`Define` caps; reserved and malformed symbols; underscore-padded literals; 2 001-character text literals.
- **Solvers:** a mechanism beside a member 10¹² times stiffer and a released chain under transverse load are both reported as mechanisms (the pivot test is relative to the largest diagonal, so the error is in the safe direction); frame E/A/I/length/load at every hard limit; polygon sections: a 2 000-vertex star, a bow-tie, collinear and duplicate vertices, 200 loops, ±10⁵ mm coordinates, 10⁻⁹ mm slivers; every numeric input and table column of every calculator at its hard limits, zero and ±10⁻³⁰⁰ with 500 rows (only R-20/R-21 fell out).
- **Library:** two simultaneous Issues (existing test), crash mid-Issue then retry (one attachment, one Issued), UpdateInputs/Check/DeleteDraft/plain store write on an Issued instance are refused, two concurrent Revises are stopped by the Evidence title (albeit by exception, R-16).
- **PDF:** DDE, script, bidi overrides, PDF syntax and 64 unbalanced parentheses in every field are escaped and drawn as text with the document structure intact; a 50-sheet pack with hostile index and revision rows renders within budget (control characters and oversized text are R-24 and R-05).

## Addendum A

**Scope.** The Addendum A delta `6afead3..origin/v0.26.0/addA-8-integration` (22a3bbf): result linking and table-cell links, reference records and the licensed-source export lock, external evidence, the independent check, pack templates and the dry run, JSON/CSV import of links, and the Desktop saved-calculation and pack panels.

**Method.** Each attack is written as a proof-of-concept xUnit test that asserts the *secure* behaviour, so it fails while the hole is open and passes once it is fixed. The tests use the existing harness: `LibraryTestHost`, `RecordedCalculator` (RecordTests), `ForceSourceCalculator` (LinkingTests), `IndependentStressCalculator`, and `ChainKit`/`ChainInputs`. Per the review brief they are given here as text and are **not** in the repository, and nothing was run (the CPU was busy). Each finding names the code path that makes its PoC fail today. Where the test host has no signed-in principal, two people are simulated by rewriting `CreatedBy` in the store, as `IndependentCheckTests` and `ChainKit.AuthoredByAnotherAsync` already do.

**Result.** 16 findings: 3 High, 4 Medium, 9 Low. Fix these three first:
- R-A-01: the issued sheet and the pack sheet print the whole licensed table.
- R-A-03: an upstream stale through a record or template change does not make its downstream stale.
- R-A-02: a calculation issues on a draft upstream whose "rev A" can still change.

### Findings

| ID | Severity | Location | Description | Status |
|---|---|---|---|---|
| R-A-01 | High | `CalcSheetBuilder.cs:35-41`; `CalcInstanceService.cs:372,656` | The issued sheet and the pack sheet print every row (up to 100) of a licensed record table | Open |
| R-A-02 | High | `CalcInstanceService.Linking.cs:78,146,498`; `CalcInstanceService.cs:235,354` | Check and Issue accept a link pinned to a draft upstream, whose revision content is still mutable | Open |
| R-A-03 | High | `CalcInstanceService.Linking.cs:257,272-282,329-338` | Transitive staleness drops an upstream's record and template staleness | Open |
| R-A-04 | Medium | `CalcInstanceService.cs:418,547,615,155`; `CalcInstanceService.Linking.cs:181`; `CalcInstanceService.Records.cs:103` | A record revision strands checked calculations without links, and checked packs | Open |
| R-A-05 | Medium | `CalcInstanceService.cs:246-249,566-569,423` | The independence gate compares against the creator only: the designer can check their own work | Open |
| R-A-06 | Medium | `CalcInstanceService.Records.cs:201-215,156`; `CalcInputValidator.cs:21`; `CalcSheetView.cs:597-601,891-905` | Record provenance is never verified: an edited value keeps its pin, a prefix-only "pin" counts, and a pin to a non-existent revision passes | Open |
| R-A-07 | Medium | `CalcSheetCoordinator.cs:332-365`; `CalcInstanceStore.cs:54-56`; `CalcInstanceService.Linking.cs:253,611,645,667,525` | Staleness and sheet building re-read the whole instance collection per question | Open |
| R-A-08 | Low | `CalcInstanceService.Linking.cs:460-514,646,668` | A link's revision label is never checked, so an import can print a false revision on the issued sheet | Open |
| R-A-09 | Low | `CalcInstanceService.Linking.cs:84,152,505`; `ConductorSizingCalculator.cs:352`; `CalcInstanceService.Records.cs:156` | Output ports are advisory: a licensed factor leaves through a downstream link and that downstream's CSV export | Open |
| R-A-10 | Low | `CalcInstanceService.ExternalEvidence.cs:20-53`; `CalcInstanceService.Records.cs:132` | AttachExternal ignores the pack freeze, leaves orphan attachments, and loses updates | Open |
| R-A-11 | Low | `CalcInstanceService.cs:247-248`; `EngineeringDomainContext.cs:203-204` | With no principal, `"unknown"` counts as an independent checker | Open |
| R-A-12 | Low | `CalcInstanceService.cs:508-534`; `CalcInstanceService.PackTemplates.cs:242-250`; `CalcSheetCoordinator.cs:176` | Pack and chain APIs and calculations with no project are not project-scoped | Open |
| R-A-13 | Low | `CalcInstanceService.PackTemplates.cs:126-164`; `CalcInstanceService.cs:112-113` | Instantiate is not atomic: a name clash throws and leaves the earlier steps behind | Open |
| R-A-14 | Low | `CalcInstanceService.cs:576-582,615,644` | The pack check writes in two transactions; the issue-pack stale check is outside the transaction | Open |
| R-A-15 | Low | `CalcInstance.cs:84-88`; `CalcInstanceService.Linking.cs:460` | The legacy `reference: calc:<id>` link skips the ADR-0168 amendment validator | Open |
| R-A-16 | Low | `CalcInstanceService.Records.cs:26,181`; `CalcDataRecord.cs:61-82`; `CalcInstanceStore.cs:229-263` | Record API: CSV formula in the citation line, caller-set `EnteredBy`, no size cap | Open |

---

#### R-A-01 — High — Licensed table rows leave on every issued sheet and pack sheet

**Location.**
- `src/Tempest.Calc/Tempest.Calc.Sheets/CalcSheetBuilder.cs:35-41`: every row up to `MaxTableRowsOnSheet` = 100 (`:67`) is written.
- Called from `CalcInstanceService.cs:372` (IssueAsync), `:656` (IssuePackAsync) and `:737` (BuildSheetAsync).
- `WithholdLicensedAsync` (`CalcInstanceService.Records.cs:149`) is called only from the clipboard CSV export (`CalcSheetCoordinator.cs:563`).

**PoC.** Add these lines to the end of `MotorCircuitChainTests.The_Chain_Issues_As_One_Bundle_And_Its_Licensed_Ampacity_Table_Never_Leaves_In_An_Export`, where `sheet` is the issued pack sheet attached to the Evidence:
```csharp
// §A5: the sheet may show the value used (the selected row), not the licensed table.
var shown = new[] { "12.3", "17.7", "24.1", "31.9", "44.6" }.Count(c => sheet.Contains($"I_t = {c} A", StringComparison.Ordinal));
Assert.True(shown <= 1, $"{shown} licensed ampacity rows printed on the issued pack sheet");
```
This fails today with 5 rows. The worked example `docs/worked-examples/electrical/elec.conductor-sizing/conveyor-motor-circuit/sheet.txt:49-54` shows the same: `ampacity[1..5]` are all printed under a `record:` source.

**Impact.** §A5 says licensed values are "excluded from every export path (… calc-pack export, report attachments …)". It allows only "the value used and its record ID/citation" on the issued sheet. Today the owner's BS 7671 Appendix 4 column (or a CIBSE λ table) is reproduced in every client PDF and in every calc-pack PDF. Those PDFs are listed under Reports and can be exported anywhere. This is exactly the copyright exposure the lock exists to prevent, and the §A9 gate "Licensed records proven non-exportable by tests" passes only because the tests check the CSV path alone.

**Fix.** In `CalcSheetBuilder` (or in `DescribeLinksAsync`, which already knows the record), take a table whose provenance pins a licensed record and print the pin and citation plus the selected row(s) only, for example "`ampacity` — 5 rows from record … (licensed: withheld); row used: CSA = 2.5 mm², I_t = 17.7 A". E4 already `Record`s the selected `I_t`. Add tests on both `IssueAsync` and `IssuePackAsync`.

---

#### R-A-02 — High — A calculation issues on a draft upstream whose "rev A" can still change

**Location.**
- `CalcInstanceService.Linking.cs:78` (BindAsync), `:146` (BindCellAsync) and `:498` (LinkProblemAsync) refuse only `Superseded`.
- `CheckAsync` (`CalcInstanceService.cs:235`) and `IssueAsync` (`:354`) ask only `StalenessAsync`.
- The Desktop offers draft upstreams as link targets (`CalcSheetCoordinator.cs:178`).

**PoC.**
```csharp
[Fact]
public async Task Red_A02_A_Calculation_Cannot_Be_Issued_On_A_Draft_Upstream()
{
    var host = new LibraryTestHost(calculators: [new ForceSourceCalculator()]);
    var project = await host.ProjectAsync();
    var motor = await host.Service.CreateAsync("test.force-source", project, "Motor torque", [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);
    var bracket = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());
    Assert.True((await host.Service.BindAsync(bracket.Id, "F", motor.Id, "F_out")).Succeeded);   // pins "Motor torque rev A" (a draft)

    var check = await host.Service.CheckAsync(bracket.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
    Assert.False(check.Succeeded);                                   // FAILS today: checked
    // Today Issue then succeeds; the upstream draft is then edited in place and issued as rev A with F_out = 100 kN,
    // while Bracket's immutable issued sheet still says "link: Motor torque rev A · F_out" with 10 kN.
}
```

**Impact.** ADR-0168 Decision 1 assumes "an instance is one revision, so the link is pinned to it". That holds for Checked and Issued instances, but a draft is edited in place under the same letter. Two issued documents can then disagree about what "Motor torque rev A" says, and the downstream's citation cannot be corrected, only superseded. The STALE marker appears only after the fact, and nothing warns at Check or Issue.

**Fix.** In `CheckAsync` and `IssueAsync`, refuse while any linked upstream is a `Draft` ("check and issue 'Motor torque' first, or issue both in one pack"). In `CheckPackAsync`/`IssuePackAsync`, allow drafts only when they are members of the same pack. Alternatively, record the upstream `OutputsHash` and status in `CalcIssueSnapshot` and print "draft — unchecked" against the link on the sheet.

---

#### R-A-03 — High — Upstream staleness from a record revision or a template change does not reach downstream

**Location.**
- `CalcInstanceService.Linking.cs:257`: `RecordStalenessAsync` runs for the top instance only.
- `:272-282`: the template-hash check runs only `if (depth == 0)`.
- `:329-338`: the inner `Stale(upstream, …, depth + 1, …)` therefore sees neither.

**PoC.**
```csharp
[Fact]
public async Task Red_A03_A_Record_Revision_Upstream_Makes_The_Downstream_Stale()
{
    var host = new LibraryTestHost(calculators: [new RecordedCalculator()]);
    var project = await host.ProjectAsync();
    var draft = new CalcDataRecord(Guid.Empty, "test.factors", "Grouping factors", 0, "Test Wiring Rules", "2018+A2:2022", "Table 4C1", "IET (own copy)", "s.kreczman",
        true, CalcDataOrigin.UserEntered, [new CalcDataValue("k_1", 0.8), new CalcDataValue("ratings", Rows: RecordedCalculator.Rated(27).Rows)], DateTimeOffset.UnixEpoch);
    var (record, _) = await host.Service.SaveRecordAsync(draft);
    var typed = CalcProvenance.Specification("typed");
    var cable = await host.Service.CreateAsync("test.recorded", project, "Cable", [CalcInputValue.Of("k_1", 1, typed), RecordedCalculator.Rated(20) with { Provenance = typed }]);
    Assert.True((await host.Service.ApplyRecordAsync(cable.Id, record!.Id)).Succeeded);            // Cable pins record rev 1
    var fault = await host.Service.CreateAsync("test.recorded", project, "Fault", [CalcInputValue.Of("k_1", 1, typed), RecordedCalculator.Rated(5) with { Provenance = typed }]);
    Assert.True((await host.Service.BindCellAsync(fault.Id, "ratings", 0, "I_t", cable.Id, "I")).Succeeded);

    await host.Service.SaveRecordAsync(draft with { Id = record.Id, Values = [new CalcDataValue("k_1", 0.5), draft.Values[1]] });   // rev 2

    Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(cable.Id))!)).IsStale);   // passes
    Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(fault.Id))!)).IsStale);   // FAILS today: fresh
    Assert.False((await host.Service.CheckAsync(fault.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded); // FAILS today
}
```
The same happens after an application upgrade changes the upstream template's content hash: the upstream is stale and its downstream is not.

**Impact.** ADR-0168 Decision 4 and §A4 say stale is transitive. Here E5 can be checked and issued using a CSA from an E4 that the system itself refuses to check or issue, because the licensed factor behind it was corrected. In the Desktop list, E4 shows STALE and E5 does not.

**Fix.** Move the template check and the record-pin check into `Stale()` so they run at every depth. Memoise the record lookups per call, since `RecordStalenessAsync` is async and the walk would need an async variant or pre-fetched record heads. Add a chain test that revises a record under E4 and expects E5 and E3 to be stale.

---

#### R-A-04 — Medium — A record revision strands checked calculations and checked packs

**Location.**
- Single calculation: `CalcInstanceService.cs:418` (Revise allows only Issued, or Checked when called from UpdateLinks); `CalcInstanceService.Linking.cs:181` (UpdateLinks needs links); `CalcInstanceService.Records.cs:103` (ApplyRecord needs a draft); `CalcInstanceService.cs:357` (Issue refuses stale).
- Pack: `CalcInstanceService.cs:615` (IssuePack refuses a stale member); `:155-156` (a member's inputs are frozen); `:547` (CheckPack needs a draft pack). The Desktop can create packs only by instantiating templates (`CalcSheetCoordinator.cs:410-426`).

**PoC.**
```csharp
[Fact]
public async Task Red_A04_A_Record_Revision_Never_Strands_A_Checked_Calculation()
{
    var host = new LibraryTestHost(calculators: [new RecordedCalculator()]);
    var project = await host.ProjectAsync();
    var (record, _) = await host.Service.SaveRecordAsync(/* licensed draft as in R-A-03 */);
    var typed = CalcProvenance.Specification("typed");
    var calc = await host.Service.CreateAsync("test.recorded", project, "Feeder", [CalcInputValue.Of("k_1", 1, typed), RecordedCalculator.Rated(20) with { Provenance = typed }]);
    await host.Service.ApplyRecordAsync(calc.Id, record!.Id);
    Assert.True((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
    await host.Service.SaveRecordAsync(/* same record, next revision */);

    var routes = new[]
    {
        await host.Service.IssueAsync(calc.Id, "ISS-1", "Client Co"),       // refused: stale
        await host.Service.ReviseAsync(calc.Id),                            // refused: only an issued calculation can be revised
        await host.Service.UpdateLinksAsync(calc.Id),                       // refused: no linked inputs
        await host.Service.ApplyRecordAsync(calc.Id, record.Id),            // refused: only a draft
    };
    Assert.Contains(routes, r => r.Succeeded);                             // FAILS today: every route refused
}
```
The pack variant uses `CreatePackAsync(project, "P", [calc.Id])` on the draft, then `CheckPackAsync`, then a record revision. After that, `IssuePackAsync`, `ApplyRecordAsync(member)`, `UpdateLinksAsync(member)` and `CheckPackAsync` are all refused.

**Impact.** Records are global and anyone can revise one. A single correction to a shared record (a typo in a grouping factor) permanently strands every checked-but-not-issued calculation without links, and every checked pack that pins the old revision, in every project. An application upgrade that changes a template hash does the same. This contradicts ADR-0166 ("a checked calculation is never a dead end").

**Fix.** Let `ReviseCoreAsync` take a Checked instance whenever it is stale (not only from UpdateLinks), and offer "Apply record rev n" on a Checked instance as a new revision. Add `ReopenPackAsync`, which takes a Checked pack back to a new draft revision with a new Evidence revision, and expose it in the pack panel.

---

#### R-A-05 — Medium — The independence gate compares against the creator only, so the designer can check their own E4/E5/S6

**Location.**
- `CalcInstanceService.cs:246-249` (CheckAsync) and `:566-569` (CheckPackAsync) compare against `CreatedBy` only.
- `:92` and `:423`: the author of a new revision is whoever revises it, including through `UpdateLinksAsync` (`CalcInstanceService.Linking.cs:243`).

**PoC.**
```csharp
[Fact]
public async Task Red_A05_Whoever_Designed_The_Inputs_Cannot_Check_Them()
{
    var host = new LibraryTestHost(calculators: [new IndependentStressCalculator()]);
    var project = await host.ProjectAsync();
    // Bob sets the calculation up (he presses "Create pack"); the session (Alice) then designs it.
    var draft = await host.Service.CreateAsync("test.independent", project, "E4 circuit 1", LibraryTestHost.ProvenancedInputs());
    await host.Store.SaveInstanceAsync(draft with { CreatedBy = "bob" }, draft.UpdatedAt);
    Assert.True((await host.Service.UpdateInputsAsync(draft.Id, LibraryTestHost.ProvenancedInputs(f: 30))).Succeeded);

    var check = await host.Service.CheckAsync(draft.Id, "Alice", "Client Co", "OK", CheckOutcome.Accepted);
    Assert.False(check.Succeeded);   // FAILS today: Alice checks the design she entered
}
```
**Desktop route with no Switch-person trickery.**
1. Alice authors E4 and Bob checks it.
2. E1 upstream changes, and Bob presses "Update to revision B". `UpdateLinksAsync` calls `ReviseCoreAsync`, and the author becomes Bob.
3. Alice, signed in as herself, checks rev B, which is her design with only the linked values changed.

**Impact.** §A3 asks for the independent check to default on for E4, E5 and S6. The control is defeated in two normal team workflows: a lead creates the packs and designers fill them in, and a checker presses the stale "Update" button. The Evidence then records an independent check that never happened.

**Fix.**
- Keep an `EditedBy` set on the instance: add the principal on Create, UpdateInputs, Bind, BindCell, ApplyRecord and AttachExternal.
- Carry the set into revisions made by Revise and UpdateLinks.
- Refuse a checker in that set, in both the single check and the pack check.

---

#### R-A-06 — Medium — Record provenance is never verified

**Location.**
- `CalcInstanceService.Records.cs:201-215`: staleness compares revision numbers only, never values.
- `CalcInputValidator.cs:21` (`IsRecordPin`, a prefix test) and `:37`.
- `CalcSheetView.cs:597-601,891-905`: the form keeps kind and reference text, so an edited value keeps its pin.
- `CalcInstanceService.Records.cs:156-157`: withholding looks the record up by the *pinned* revision.

**PoC.**
```csharp
[Fact]
public async Task Red_A06_A_Value_That_Is_Not_The_Records_Cannot_Cite_It()
{
    var host = new LibraryTestHost(calculators: [new RecordedCalculator()]);
    var project = await host.ProjectAsync();
    var (record, _) = await host.Service.SaveRecordAsync(/* licensed draft, k_1 = 0.8 */);
    var typed = CalcProvenance.Specification("typed");
    var calc = await host.Service.CreateAsync("test.recorded", project, "Feeder", [CalcInputValue.Of("k_1", 1, typed), RecordedCalculator.Rated(20) with { Provenance = typed }]);
    var applied = (await host.Service.ApplyRecordAsync(calc.Id, record!.Id)).Instance!;

    // (a) the user edits k_1 in the form; the source column still reads the pin
    var edited = applied.Inputs.Select(i => i.Symbol == "k_1" ? i with { Number = 1.9 } : i).ToList();
    Assert.True((await host.Service.UpdateInputsAsync(calc.Id, edited)).Succeeded);
    var sheet = (await host.Service.RunAsync(calc.Id))!.SheetText;
    Assert.Contains("Table 4C1 (licensed source) rev 1", sheet, StringComparison.Ordinal);   // cites the record for 1.9
    Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);   // FAILS today

    // (b) "record:" alone silences the §A3 warning
    var fake = await host.Service.CreateAsync("test.recorded", project, "Fake", [new CalcInputValue("k_1", Number: 1.9, Provenance: CalcProvenance.FromReference("record: BS 7671 Table 4C1"))]);
    Assert.Contains(fake.Warnings, w => w.RuleId == "record.k_1");   // FAILS today

    // (c) a pin to a revision that does not exist is neither stale nor withheld
    var future = new CalcInputValue("k_1", Number: 0.8, Provenance: CalcProvenance.FromReference($"record:{record.Id:N}@9 · Table 4C1"));
    Assert.Null((await host.Service.WithholdLicensedAsync(new CalcInputSet([future]))).Find("k_1")!.Number);   // FAILS today: 0.8 kept
}
```
The worked example `conveyor-motor-circuit/inputs.json` ships provenance `record: owner's licensed table (not shipped)…`, which is case (b).

**Impact.** The issued sheet attests that a value comes from a named, licensed edition and table when it does not, and both the checker and the export lock rely on that attestation. Case (a) happens through ordinary editing, with no attacker. Cases (b) and (c) arrive through a pasted JSON import.

**Fix.**
- When a pin is present, compare the input's value (scalar or table) with the pinned record's value. Mark a mismatch stale ("value differs from record … rev 1"), or drop the pin when the form value changes.
- Make `IsRecordPin` require a successful `PinOf`.
- Treat a pin whose revision does not exist as stale, and as withheld when any revision of that record is licensed.

---

#### R-A-07 — Medium — Staleness and sheet building re-read the whole instance collection per question

**Location.**
- `CalcSheetCoordinator.cs:332-365`: `RefreshListsAsync` calls `DescribeAsync`, which calls `StalenessAsync` for each of up to 200 listed calculations and pack members.
- `CalcInstanceService.Linking.cs:253,611-620` (`AllInstancesAsync`) calls `CalcInstanceStore.cs:54-56` (`ReadAllAsync` of `Calc.Instances`), which reads every instance in every project, including superseded ones and documents up to 4 MB.
- `RefreshLinkStateAsync` (`:154-188`) repeats this, and adds `ListRecordsAsync` over every record revision.
- Per link: `CalcInstanceService.Linking.cs:645,667` (`FindInstanceAsync` → `FindKeyAsync` → `ListKeysAsync` of all keys, for each linked cell) and `:317,525-540` (`Latest` scans every instance for each link and each hop).

**PoC.**
```csharp
[Fact]
public async Task Red_A07_One_Relist_Reads_The_Collection_A_Bounded_Number_Of_Times()
{
    // CountingStore wraps InMemoryQueryablePersistenceStore and counts ReadAllAsync / ListKeysAsync (as Red_ConcurrencyAndTamper's gating store does).
    var host = new LibraryTestHost(/* over CountingStore */);
    var project = await host.ProjectAsync();
    for (var i = 0; i < 200; i++)
        await host.Service.CreateAsync("test.direct-stress", project, $"Calc {i}", LibraryTestHost.ProvenancedInputs());

    host.Counting.Reset();
    foreach (var instance in (await host.Store.ListInstancesAsync(project)).Take(200))
        await host.Service.StalenessAsync(instance);                     // what one RefreshListsAsync does
    Assert.True(host.Counting.ReadAll <= 2, $"{host.Counting.ReadAll} full collection reads");   // FAILS today: 200

    // Sheet build: a 50-row table with every I_t cell linked to one upstream lists all keys 50 times.
}
```

**Impact.** A relist follows every Save, Check, Issue, Link, Apply and Attach (`ReportAsync` → `RefreshLinkStateAsync` → `RefreshListsAsync`) and every coalesced staleness event. With 200 calculations in a project and 1,000 in the database at about 20 KB each, one click reads and deserialises about 4 GB of JSON. A sheet built from a JSON import with thousands of cell links (the 4 MB cap allows about 25,000) issues one key listing per cell. The form becomes unusable as projects grow, with no attacker needed.

**Fix.**
- Add `StalenessForProjectAsync(projectId)`, which builds the link graph once from one listing and returns every instance's staleness. Use it in `RefreshListsAsync`.
- Make `ListInstancesAsync(projectId)` list by key prefix instead of `ReadAllAsync`.
- In `DescribeLinksAsync`, memoise upstream lookups per call.
- Index successors by `PredecessorId` for `Latest`.

---

#### R-A-08 — Low — A link's revision label is never checked, so a forged revision appears on the issued sheet

**Location.** `CalcInstanceService.Linking.cs:460-514`: `LinkProblemAsync` never compares `link.Revision` with `upstream.Revision`, and neither does `Stale()` (`:309-324`). The sheet prints `rev {link.Revision}` (`:646,668`). `CalcSheetView.cs:449-450` keeps an imported link's revision through the form.

**PoC.**
```csharp
var link = new CalcProvenance(CalcProvenanceKind.Link, $"calc:{motor.Id:N}#F_out", "D");          // motor is rev A, F_out = 10 kN
var inputs = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "F" ? CalcInputValue.Quantity("F", 10, "kN", link) : i);
var bracket = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", inputs);
Assert.DoesNotContain("rev D", (await host.Service.RunAsync(bracket.Id))!.SheetText);              // FAILS today: "link: Motor torque rev D · F_out"
```

**Impact.** The issued sheet names a revision of the upstream that does not exist, or the wrong one. The value is correct, because staleness checks it, but the audit trail is not. The forged link arrives through a pasted JSON import.

**Fix.** Refuse in `LinkProblemAsync` when `link.Revision != upstream.Revision`. Print `upstream.Revision` from the store rather than the label.

---

#### R-A-09 — Low — A licensed factor leaves through a downstream link and the downstream's CSV export

**Location.**
- `CalcInstanceService.Linking.cs:84,152,505`: any numeric output in `upstream.Outputs` can be bound, not only the declared `OutputPorts`.
- `CalcSheetCoordinator.cs:181`: the link target list offers every numeric output.
- `ConductorSizingCalculator.cs:352`: E4 outputs `C_a`, which is the input pinned to the licensed `cable.correction-factors` record when `derive_Ca` is off. E4 declares only I_b, I_n, CSA, I_z and L as ports (`:141-147`).
- `CalcInstanceService.Records.cs:156`: `WithholdLicensedAsync` withholds record pins only.

**PoC steps.**
1. Apply a licensed `cable.correction-factors` record (C_a = 0.91) to an E4 draft.
2. In a second calculation, link a dimensionless input to E4's `C_a`. `BindAsync` succeeds even though `C_a` is not a declared port.
3. Export that second calculation's inputs. `CalcCsv.ExportInputs(template, await WithholdLicensedAsync(inputs))` contains `0.91` with source `link: …`.

**Impact.** §A5 says the lock covers every export path. This route leaks one tabulated factor per link, not a table, so the severity is Low. It also shows that declared ports are not enforced.

**Fix.** When a template declares `OutputPorts`, bind only to those (in `BindAsync`, `BindCellAsync`, `LinkProblemAsync` and the Desktop target list). Alternatively, have `WithholdLicensedAsync` follow a link to its upstream and withhold the value when the upstream input of the same symbol is pinned to a licensed record.

---

#### R-A-10 — Low — AttachExternal ignores the pack freeze, leaves orphan attachments, and loses updates

**Location.**
- `CalcInstanceService.ExternalEvidence.cs`: `:20` reads the instance; `:25` checks Draft only, so a member of a checked pack passes; `:40` attaches up to 25 MB in its own transaction; `:53` calls `UpdateInputsAsync` without `expectedUpdatedAt`.
- `CalcInstanceService.Records.cs:132`: ApplyRecord also omits `expectedUpdatedAt`.

**PoC.**
```csharp
var calc = await host.Service.CreateAsync("test.direct-stress", project, "Base", LibraryTestHost.ProvenancedInputs());
var pack = await host.Service.CreatePackAsync(project, "P", [calc.Id]);
Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Co", "OK", CheckOutcome.Accepted)).Succeeded);
var attach = await host.Service.AttachExternalEvidenceAsync(calc.Id, "profis.pdf", "application/pdf", new byte[] { 1 });
Assert.False(attach.Succeeded);   // FAILS today: a file lands on a member's Evidence after the pack check
```
Lost update: hold the store gate on the attachment write, run `UpdateInputsAsync(calc, F = 30)`, then release. AttachExternal writes back its stale copy of the inputs and F reverts to 12.

**Impact.** Documents are added to a checked pack's members after the check. A refused citation leaves the file attached anyway (the "is attached but not cited" path). An edit saved while a large report uploads is silently lost.

**Fix.** Refuse when the member's pack is not Draft, and check that before attaching. Validate `inputSymbol` before attaching. Pass the read's `UpdatedAt` to `UpdateInputsAsync`, or attach and update in one transaction.

---

#### R-A-11 — Low — With no principal, `"unknown"` counts as an independent checker

**Location.** `EngineeringDomainContext.cs:203-204` returns `"unknown"` when no principal is set. `CalcInstanceService.cs:248`: `IsNullOrWhiteSpace(principal)` is never true for that value.

**PoC.** Existing `IndependentCheckTests.The_Author_Cannot_Check_…` second half: with `CreatedBy = "another.engineer"`, the host (no principal) checks successfully. Inverting that assertion is the PoC.

**Impact.** Any host without a signed-in principal (a library consumer, a background service) passes the E4/E5/S6 independence gate on anyone's calculation. The Desktop always sets a principal.

**Fix.** Refuse when `principal == EngineeringDocumentStore.UnknownAuthorPrincipalId`, and when `CreatedBy` is that value.

---

#### R-A-12 — Low — Pack and chain APIs, and calculations with no project, are not project-scoped

**Location.**
- `CalcInstanceService.cs:508-534`: CreatePack accepts any IDs.
- `:537-707`: CheckPack and IssuePack have no project check.
- `CalcInstanceService.PackTemplates.cs:242-250`: PackTemplateFromChain accepts any IDs and stores their names in a global template.
- `CalcInstanceStore.cs:54-58`: `ListInstancesAsync(null)` returns every project's instances. `CalcSheetCoordinator.cs:176` lists them as link targets for a calculation with no project.

**PoC.** `CreatePackAsync(project, "P", [unprojected.Id])`, then CheckPack, then IssuePack. The project-less calculation is issued under another project's pack Evidence and reference.

**Impact.** A library caller can pull a calculation from one project into another project's pack. Names of calculations in other projects leak into saved templates and into the link list. This is a single-user desktop, so the impact is low.

**Fix.** Refuse members whose `ProjectId` differs from the pack's. Scope `ListInstancesAsync(null)` to "no project" rather than "all".

---

#### R-A-13 — Low — Instantiate is not atomic: a name clash throws and leaves the earlier steps behind

**Location.**
- `CalcInstanceService.PackTemplates.cs:126-143`: the dry run does not check names, and only `CalcLinkRefusedException` is caught.
- `CalcInstanceService.cs:112-113`: the Evidence title and the register name are business identifiers, unique per project (TD-38, the same mechanism as R-16).

**PoC.** In a project that already holds a calculation named "Enclosure heat", instantiate `CalcPackTemplates.Cooling`. Step `e6` ("Panel load budget") is created. Step `e2` then throws `DuplicateBusinessIdentifierException` out of `InstantiatePackTemplateAsync`, leaving a stray draft with Evidence and a register entry and no pack. Instantiating any template a second time in the same project throws at step 1.

**Impact.** Stray Evidence and register entries are left behind, contradicting the comment "a refusal leaves no stray drafts behind". The user sees an exception instead of a refusal, and one project cannot hold two packs from the same template (two drive trains, for example).

**Fix.** Check all step names against `BusinessIdentifierIndex.FindConflict` in the dry run (or suffix them uniquely), and catch the exception as a refusal. Long term, create every step in one transaction.

---

#### R-A-14 — Low — The pack check writes in two transactions; the issue-pack stale check is outside the transaction

**Location.**
- `CalcInstanceService.cs:576` (`RecordCheckAsync`) then `:582` (`SavePackAsync`) are separate transactions.
- `:615`: IssuePack checks staleness before the transaction at `:644` opens.

**PoC.** Fail the `SavePackAsync` commit with `host.Failing`. A retried `CheckPackAsync` is then refused with "Evidence … is Checked; it cannot be checked from that status", which is the pack equivalent of R-15. Separately, a gate on the transaction start that lets an upstream `UpdateInputsAsync` land in between causes IssuePack to issue a member that is now stale.

**Impact.** A crash leaves a pack that can never be checked. The race window issues a stale member. Both need a crash or concurrency to trigger.

**Fix.** Use `RecordCheckInOneTransactionAsync` with `alsoWrite` set to a guarded pack write. Re-verify member `OutputsHash` and upstream heads inside `alsoWrite` in IssuePack.

---

#### R-A-15 — Low — The legacy `reference: calc:<id>` link skips the ADR-0168 amendment validator

**Location.** `CalcInstance.cs:84-88`: `UpstreamIds` includes legacy references. `CalcInstanceService.Linking.cs:460`: `LinkProblemAsync` checks only `Link`-kind provenance.

**PoC.**
1. Bind `B.F` to `A.F_out` (a Link).
2. Run `UpdateInputsAsync(A, P with Provenance = FromReference($"calc:{B.Id:N}"))`. It succeeds, creating the cycle A↔B that the amendment says is "refused when it is entered".
3. A legacy reference to an instance in another project is also accepted. That instance is then permanently "no longer exists" in `Stale()` (`:290-295`), so the calculation can never be checked.

**Impact.** Cycles and cross-project references can be entered through the form's "reference" source or an import. The graph walks are bounded, so the harm is self-inflicted staleness, not a hang.

**Fix.** Run the same validator on legacy references, or migrate the form to `Link` and refuse new legacy references.

---

#### R-A-16 — Low — Record API: CSV formula in the citation line, caller-set `EnteredBy`, no size cap

**Location.**
- `CalcInstanceService.Records.cs:181`: `# {Citation}` is written raw.
- `:26`: `EnteredBy` comes from the caller.
- `CalcDataRecord.cs:61-82`: there is no cap on cells per row or on cell text.
- `CalcInstanceStore.cs:229-240`: there is no `MaxDocumentBytes` check on record writes.
- `:257-263`: `ListRecordsAsync` deserialises every revision.

**PoC.** Save an unlicensed record with `Edition = "2018,=cmd|' /C calc'!A0"`, then call `ExportRecordAsync`. Split the first line on `,`: one field is `=cmd|' /C calc'!A0`, and `CalcCsv.IsFormulaLike` returns true for it. Separately, `SaveRecordAsync(draft with { EnteredBy = "someone.else" })` keeps that name.

**Impact.** The Desktop does not call `ExportRecordAsync` and always passes an empty `EnteredBy`, so these are latent library holes (R-03 class).

**Fix.** Write the citation through `Csv()` as a quoted cell, or drop the header. Always set `EnteredBy` from the principal. Cap the serialised record and cell text. List the latest revision only.

### Attacks that found nothing (the code held)

- **Cycles and depth.** Cycles through cell links, through any revision (lineage), and through forged JSON links are refused on entry. The 20-link cap holds when chains are joined end to end (`DepthAbove + 1 + DepthBelow`). Staleness walks, `Downstream` and `Depth` are bounded (visiting set, memo, 10,000 cap, path cap) even over a forged loop. 200 cells linked to one upstream are walked once (memo); see R-A-07 for the lookup cost.
- **Dimensions.** A link across dimensions, between an affine temperature and a temperature difference, between a Number input and a dimensioned output, into a text or choice column, or into a row that does not exist is refused. So is a cell link inside a record-pinned table.
- **Cross-project links.** Refused by `BindAsync`, `BindCellAsync` and `LinkProblemAsync`. Superseded upstreams are refused too.
- **Forged link values.** A link whose value differs from the upstream is stale and cannot be checked.
- **Export lock on the CSV path.**
  - A licensed record cannot be revised to unlicensed.
  - `SaveRecordFromInputsAsync` makes a copy licensed when any of its source inputs pins a licensed record.
  - `ExportRecordAsync` refuses licensed records.
  - The clipboard CSV withholds pinned scalars and whole pinned tables.
  - `PackTemplateFromChainAsync` stores no input values.
  - `CalcPackExamples` ships no ampacity or ratings table, so the motor-circuit pack stays refused until the user supplies them.
- **Attachments.**
  - A Windows path and `..` reduce to a final segment.
  - Control, bidi and format characters are stripped (`Clean`).
  - The 25 MB cap holds in the library, with a pre-check for seekable streams.
  - Opening goes through `AttachmentViewerLauncher`: a sanitised name in a fresh random temp directory, dangerous extensions refused. A `report.pdf.exe` or `..` name cannot escape or execute.
- **Immutability.** Bind, BindCell, ApplyRecord, AttachExternal and UpdateInputs on Checked, Issued or Superseded instances are refused. Members of a checked pack are frozen for input changes (R-17 holds). Issued records cannot be rewritten under a Superseded mark (R-14 holds).
- **Concurrency.** `BindAsync`, `BindCellAsync` and `UpdateLinksAsync` pass `expectedUpdatedAt`. Concurrent record saves get distinct revisions. A double click on Open, Create pack, Check pack or Issue pack is dropped (`ExclusiveAsync`). Two "Update to revision" presses produce one revision B.
- **Injection.** Link names, record citations, pack titles and step names reach the sheet, PDF and FTS through `Clean` with length caps.
- **Pack templates.** A stored template naming an unregistered calculator, a binding upstream of its source, or a cell binding without a row is refused by `ValidatePackTemplate` before anything is written. The dry run runs every step in memory first.
