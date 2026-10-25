# 🟡 Yellow team — builder quality

Run at the end of each build phase (§4) and again in Phase 7. Findings: ID, severity, location, description, evidence, fix/justification, status.

## Phase 1 — engine, sheets, library, reports, form (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y1-01 | Low | `Tempest.Calc.Engine/Parsing/Lexer.cs` | Unit-after-number lexing is greedy; `2 m*x` reads `m*x` as a unit and fails with "Unknown unit" rather than multiplying by `x`. | `ExpressionTests.Unit_After_Number_Does_Not_Swallow_An_Operator` covers `2 mm *3`; the no-space form is documented as "write `2 m * x`". | Documented in the authoring guide (Orange); error message names the number. | Justified |
| Y1-02 | Low | `CalcInstanceService.IssueAsync` | After the one-transaction issue, `CompleteRegisterEntryAsync` is a second transaction (marks the register entry complete). A crash between leaves the instance Issued with an incomplete register flag. | `CalcInstanceServiceTests.Issue_Requires_Check_Then_Snapshots_Everything_In_One_Commit` asserts ≤ 3 commits. | Idempotent: the flag is set on the next `IssueAsync` retry path only; Grey team to confirm the register row still lists the calculation (it does — the entry exists from creation). Accepted. | Justified |
| Y1-03 | Info | `CalcSheetView` | `CommonUnits` list is a static array scanned per dimensioned input; fine for ≤ 30 inputs. | Form build for the M3 joint (22 inputs) < 50 ms headless. | None needed. | Closed |
| Y1-04 | Medium | `DocumentTemplate.RenderPdf` | Font subsetting added to the shared draw loop touches every document type. | 118 Documents/IssueSheet/Quotation tests and the full Desktop suite (1150) pass after the change; sheet 222 → 162 KB. | Covered by existing renderer tests (text extraction). | Closed |

## Phase 2 — M1–M3 (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y2-01 | Low | `Iso286.ShaftFundamentalDeviation` | `cd`, `ef`, `fg` intermediate letters are geometric means of their neighbours; `a`, `b`, `c`, `j` return null (override required). | Unit tests cover d–u; the fits sheet blocks with a plain message for an unsupported letter. | Documented in the spec; overrides from reference records. | Justified |
| Y2-02 | Low | `BoltedJointCalculator` | Frustum uses the hole diameter `d_h` inside the cone (VDI convention) with `0.5774 π E d` on the bolt diameter (Shigley eq. 8-20 form). | Known-answer case re-derived with the same convention. | Spec states the convention. | Justified |
| Y2-03 | Info | `ToleranceStackCalculator` | Monte Carlo runs on every recalculation in the form (10 000 samples ≈ 15 ms); fine. | `Sheet_Renders_Under_A_Second` for every template. | None. | Closed |
| Y2-04 | Low | Known-answer sources | Two cases cite textbook pages the build could not open (`Unconfirmed`): Shigley shouldered screw, Machinery's Handbook H7/g6. | STATUS lists the count. | Owner to confirm pages; values are analytic. | Open (owner) |

## Phase 3 — S1–S3 (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y3-01 | Low | `SectionPropertiesCalculator` | Torsion constants for channel, tee and angle are the bare Σbt³/3 (no fillet or junction term); rolled-I keeps the El Darwish–Johnston term. Blue Book J for a channel will read ~10–20 % higher. | Warning `thin-walled-torsion` on every open section; UB J within 1 % of the Blue Book. | Documented in the spec; a fillet term for channels is a later improvement. | Justified |
| Y3-02 | Low | `FrameSolver` | Distributed loads are per member (local normal or gravity per metre of member); a global projected load (per plan metre) must be converted by the user. | Load table convention stated in the template assumptions. | Documented. | Justified |
| Y3-03 | Medium | `FrameSolver.BandedSymmetric` | The mechanism test is a pivot ≤ 1e-10 × max diagonal. A near-mechanism with a very stiff and a very soft member in one model (ratio > 1e10) could pass as regular. | `Hinge_Mechanism_Is_Reported_Not_Thrown`, `Unsupported_Frame_Is_A_Mechanism` pass; 1,001-node beam solves. | Accepted for v0.26.0; Red team to probe with extreme stiffness ratios. | Open (Red) |
| Y3-04 | Low | `MemberChecksCalculator` | Lateral-torsional buckling is not checked; the sheet warns on every run. The simplified member interaction uses user k factors (default 1.0), which is not conservative for every case. | Known-answer `ka-uc-compression` is analytic (Unconfirmed against a published example). | Scope per §6 S3 "basic"; assumption A3 states it; worked example shows the FAIL path. | Justified |
| Y3-05 | Info | Known-answer sources | Blue Book values were read from public mirrors (engineeringtoolbox, beamdimensions) because steelforlifebluebook.co.uk renders its tables client-side. | Values agree across two mirrors; S1 reproduces them within 0.5 %. | Owner may re-confirm from the Blue Book itself. | Open (owner) |

## Phase 4 — B1–B3 (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y4-01 | Low | `PipeFlowCalculator` | The system curve scales friction with Q² from the design flow; in laminar flow the loss is ∝ Q, so the duty point is approximate for viscous fluids. | Laminar warning on the segment; pump curve use is a water-system feature. | Documented in assumption A3. | Justified |
| Y4-02 | Medium | `PipeFlowCalculator` | The first known-answer duty case passed once with wrong expectations before the Colebrook expectation fix, then failed; cause traced to the author's hand values, not the solver (hand values recomputed from the printed working). | Suite run twice after the fix: 190/190 both times; golden stable. | Expectations re-derived and the derivation written into the reference text. | Closed |
| Y4-03 | Low | `MoistAir.WetBulb` | Wet-bulb below 0 °C uses the ice form of the adiabatic-saturation relation with constant coefficients; sublimation enthalpy is treated as constant. | Property test `Every_Route_To_A_State_Agrees_With_The_Others` over −20 … 50 °C. | Acceptable for HVAC range; documented. | Justified |
| Y4-04 | Low | `DuctSizingCalculator` | Rectangular sizing picks side a = 1.5 × target D_e rounded when no side is fixed; aspect ratio is not optimised. | Known-answer with a fixed side (600 × 300) matches the ASHRAE relation. | Documented; user fixes a side in practice. | Justified |
| Y4-05 | Info | Known-answer sources | ASHRAE Ch. 1 Example 1 and the Ch. 21 friction-chart point are reproduced from the same published relations, not read from the printed example (Unconfirmed / chart tolerance). | STATUS lists unconfirmed count. | Owner may confirm against the handbook. | Open (owner) |

## Phase 5 — E1–E3 (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y5-01 | Low | `MotorSizingCalculator` | Efficiency is applied in the deceleration phase as in acceleration (the mechanism then helps the motor less than it really does), so T_dec is slightly pessimistic in magnitude; T_rms is conservative. | Analytic known answers use the same convention. | Assumption A1 states it. | Justified |
| Y5-02 | Low | `EnclosureHeatCalculator` | The forced-ventilation airflow neglects natural dissipation (conservative); `V_req_net` credits it for comparison. Solar gain uses one absorptivity and one sunlit area. | Analytic case. | Documented. | Justified |
| Y5-03 | Medium | `VoltageDropCalculator` | A mV/A/m figure is used as entered with its own convention; mixing a single-phase figure into a three-phase sheet is a user error the sheet cannot detect. | Known-answer `ka-mvam`. | Input description says "from the cable table"; Orange guide to note. | Justified |
| Y5-04 | Low | `EnclosureHeatCalculator` | First hand expectation for the fan duty point was wrong (quadratic solved by hand); the sheet's bisection was right. Re-derived and recorded in the reference text. | Suite green after the fix. | Closed. | Closed |

## Phase 7 — final pass (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y7-01 | Info | Performance | Budgets met: every sheet renders < 1 s (16 templates), 10,000-sample Monte Carlo < 2 s, 510-element frame 47 ms. | `PerformanceBudgetTests` (×3 CI multiplier). | None. | Closed |
| Y7-02 | Low | `EnclosureHeatCalculator.PressureAtAltitude`, `MoistAir.PressureAtAltitude` | The standard-atmosphere formula is written twice (Electrical and BuildingServices cannot share without a reference between calculator assemblies). | Both tested. | Accepted: a shared `Tempest.Calc.Physics` helper is a v0.26.1 refactor. | Justified |
| Y7-03 | Low | `FrameSolver` | Band overflow was silent until Phase 6 (DOF numbering in model order); now numbered in the reordered sequence and guarded. | `PerformanceBudgetTests`, `FrameTests`. | Fixed. | Closed |
| Y7-04 | Info | API consistency | Every calculator exposes a static table/vector helper for tests and worked examples (`Table`, `Segments`, `Nodes`, `Loads`, …) and a `*Calculators.All` list; ids follow `discipline.topic`; outputs use `u_*` for utilisations. Sheet text is LF on every platform. | Template hygiene theory over 16 templates. | None. | Closed |
| Y7-05 | Info | Documentation | 16 specs under `docs/calcs/`, 24 worked examples + 1 pack, 9 colour-team reports, security review, handover. Markdown lines added ≈ 1.6 k against ≈ 20 k code lines. | `git diff --stat`. | None. | Closed |

## Phase 6/7 — Red/Blue pass (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y7-06 | Info | Red PoCs | 138 Red proof-of-concept tests kept as regression tests; three re-targeted to the one-transaction Check/Revise design (the gate they used can no longer split the writes) and one skipped with the D-32 justification. | `Red_*` files, `blue-team.md`, `purple-team.md`. | None. | Closed |
| Y7-07 | Low | `CalcInstanceService.ReviseAsync` | Recovery after a crash mid-Revise relies on the register entry's business identifier (`"<name> (rev B)"`) being unique per project; a project-less instance (no register entry) falls back to the Evidence-title refusal. | `Crash_Mid_Revise_Leaves_No_Orphan_Draft`. | Accepted: project-less instances are a test convenience; the Desktop always creates inside a project. | Closed |
| Y7-09 | Info | ADR-0166 | Three Phase 1 tests amended under an ADR (non-negotiable #3), with new tests for the rejection path and the empty-field refusals; every golden and worked example regenerated and the Calculators suite confirms the stored sheets. | `git diff --stat docs/worked-examples` (32 sheets, 32 PDFs, pack). | None. | Closed |
| Y7-10 | Low | `CalcSheetBuilder.RowText` | Table rows are written as `key = value; …` text; a wide table (frame loads: 11 columns) gives a long line that the PDF wraps. | Frame worked examples. | Accepted for v0.26.0; a column table per input is a v0.26.1 layout item. | Closed |
| Y7-08 | Info | `CalcSheetPdfRenderer.T` | One helper now cleans and caps every drawn string; the text renderer's `Clean` is the single source of the character rules. | `Red_PdfRendererAttacks`. | None. | Closed |

## Addendum A

Builder quality, 2026-10-24. Scope: the 13 merged build branches from `v0.26.0/addA-orientation` to `v0.26.0/addA-9-blue`, 75 commits (62 without merges) on top of `6afead3`. Sources: `LOG.md`, `STATUS.md`, `v0.26.0-DECISIONS.md` (DA-01…DA-23) and the Addendum A sections of the other reviews. Counts that the log does not record are marked "not recorded".

### Per branch

| Branch | Content | Reviews run | Defects found by review | Defects found by tests or the gate | Re-work rounds | Warnings | Flaky tests |
|---|---|---|---|---|---|---|---|
| `addA-orientation` | Orientation, tasks, DA-01…03 | None (docs only) | 0 | 0 | 0 | n/a | 0 |
| `addA-0-linking` | L1 linking (ADR-0168), records (DA-05), export lock (DA-06) | Opus, retrospective with A-1…A-3 (DA-16, after A-3) | Link validator, licensed revisions, concurrent record saves (DA-17 findings 4, 5, 15); lineage cycles and `CreateAsync` validation (DA-18) | One record test flaky, found at A-2 | 2 (DA-17, DA-18) | 0 | 1 (A-0 record test, fixed at A-2) |
| `addA-1-quick` | M5, S5, B5, E6, B6, records in the form | Opus, retrospective | K_FI on unfavourable actions, E6 starting peak, S5 push to S2, B6 Φ50 unit, DA-11 wording (DA-17 findings 3, 6, 8, 9, 11) | 0 recorded | 1 (DA-17) | 0 | 0 |
| `addA-2-machine` | M4, E5, M6, drive-train template | Opus, retrospective | M4 k_a source and override, M6 F_z eccentricity, M4 example text, E5 adiabatic claim, E5 Z_e label (findings 2, 7, 10, 12, 13) | The flaky A-0 record test | 1 (DA-17) | 0 | 0 new |
| `addA-3-connections` | S4, bolt-shear picker (ADR-0169) | Opus, retrospective | 1 High: the legacy bolt shear mixed the shank and thread cases (finding 1) | M4 conditioning fix (LOG A-3) | 1 (ADR-0169 revised the same day) | 0 | 0 |
| `addA-3b-review-fixes` | 15 review fixes (DA-17, DA-18) | Opus verification of the fixes: 9 verified, 6 incomplete, 1 new Medium | 6 incomplete fixes and 1 new Medium | 0 recorded | 1 (closed on `addA-4-records`) | 0 | 0 |
| `addA-4-records` | E4, B4, motor-circuit pack, export lock | Opus on E4 and on B4 (DA-19) | E4 illustrative values matched published tables (§A5); B4 external detection, `f` on bridges, MVHR; counts not recorded | Gate: 0 | 1 (E4 and B4 review fixes) | 0 | 0 |
| `addA-5-baseplate` | S6, PROFIS report attach | Opus on S6 (DA-20) | 2 High and 5 Medium: Table 6.7 statics, anchor shear, t_req, geometry | 0 recorded | 1 | 0 | 0 |
| `addA-7a-cell-links` | Cell links, wet-heating pack, E1 continuous duty | Opus | 1 High, 2 Medium, 7 Low (E1 continuous duty, B1/B6 ports, cell-link hardening) | 0 recorded | 1 | 0 | 0 |
| `addA-7b-chains` | Drive train to M3, cooling and steel-support chains | Opus | See `addA-7b2-chains` | — | Superseded (force-push refused; see Y-A-03) | n/a | — |
| `addA-7b2-chains` | The same, reworked on a new branch | Opus (A7b and A8-UI together: 2 + 2 High) | Steel-support S4 split, arm-joint bolt group, air-side cooling coil, example pack inputs | Stale worked-example PDFs regenerated | 1 | 0 | 0 |
| `addA-8-ui` | Saved-calculations and packs panels | Opus (with A7b) | Signed-in checker, busy guard, unsaved-edit warning, coalesced relists | Gate: packs panel list-row bug (a control item could not be re-hosted) | 1 | 0 | 0 |
| `addA-8b-pack-dryrun` | Pack dry run in memory | None separate | 0 | A refused pack left stray drafts | 0 | 0 | 0 |
| `addA-8-integration` | Merge of A7b, A8-UI and dry run; five pack worked examples | Gate | 0 | Stale PDFs, packs panel row | 0 | 0 | 0 |
| `addA-9-blue` | Blue round 1 | Security, Red, Green, Black, then Purple | 82 findings, 9 High (Purple's count) | Room heat-loss test followed the blank new-sheet tables (DA-23); Purple's scratch tests found 2 Medium defects the fixes introduced | Blue round 2 pending | 0 | 0 new (see Y-A-05) |

Test totals at the last gate on the build branches (LOG, 2026-10-24): Engine 384, Sheets 93, Calculators 721, Library 132 (+1 skipped, D-32), Core 7,230 (+15 skipped), Desktop 1,221; 0 failures; 0 warnings.

### Findings

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y-A-01 | Medium | Process: builder worktrees | Worktrees were created from an old base. Each builder had to fetch the branch and reset hard before starting. A reset hard discards uncommitted work. | Every builder brief opens with `git fetch origin <branch> && git reset --hard origin/<branch>`. No lost work is known. | Create the worktree from the target branch tip, and say the base commit in the brief. Until then, keep the reset as the first step. | Open (process) |
| Y-A-02 | Low | Process: commit trailers | Builder commits carried the wrong attribution trailers. They were corrected at integration. | All 62 non-merge commits since `6afead3` end with `Co-Authored-By: Claude Opus 5.5` and `Claude-Session` (checked with `git log`). The 13 merge commits carry none. | Corrected. State the exact trailer text in each brief. | Closed |
| Y-A-03 | Medium | Process: `v0.26.0/addA-7b-chains` | A force-push to the A7b branch was refused. The rework went to `v0.26.0/addA-7b2-chains`, which was merged (`43753500`). The old branch is still on origin. | Both branches in `git branch -r`; the merge log names only `addA-7b2-chains`. | The branch-name rule finds stale branches by version, and this one carries the right version but holds superseded work. The owner should delete it. A builder must never need a force-push: branch, then rebase before the first push. | Open (owner) |
| Y-A-04 | Low | Process: reports | Subagents cannot write report files. Reviews and this addendum came back as messages and the orchestrator wrote them, which costs a hand-off and risks drift between message and file. | Review text in the scratchpad was copied into `docs/reviews/` verbatim. | Justified. Keep copies verbatim and record the source path. | Justified |
| Y-A-05 | Medium | `tests/Tempest.Calc.Tests.Calculators/Red_SolverAttacks.cs` — `Tolerance_Stack_At_Its_Hard_Limits_Finishes_In_Budget` | The `mech.tolerance-stack` 5 s timing test fails under CPU load, for example when two builders or a builder and a gate run together. It measures wall-clock time. | DA-16 forbids builders from running full suites during a gate for this reason. The budget already honours the CI timeout factor (D-36). | Measure CPU time of the run, or retry once with the load noted. Until then, run it alone when it fails and do not treat a single failure as a regression. | Open |
| Y-A-06 | Low | An A-0 record test | A record test was flaky and was fixed at A-2. | LOG, A-2 row. | Fixed. | Closed |
| Y-A-07 | Medium | Builder tests | Most of the nine Highs passed every builder test. The tests pinned what the builder had just made: E4 type C beside E5 type B, a FAIL in the issued wet-heating pack, a CSV export that dropped every table, and sheets opened on a test case's inputs. A test written beside the code shares its blind spots. | Purple's verification of G-B-01, B-A-04, B-A-05, G-A-02. The suites were green when each was merged. | Chain tests now assert cross-step consistency; the CSV round trip runs over every worked example; pack READMEs are read for unexplained FAILs; new sheets open on declared defaults only. Keep one reader who did not write the example. | Closed |
| Y-A-08 | Medium | Review timing | The Opus review (DA-16) came after A-3, so A-0…A-3 were reviewed together. It found 15 issues, one High. At verification six fixes were incomplete and one new Medium appeared (DA-18). | DA-16, DA-17, DA-18. | Every later phase was reviewed before its merge. The late round cost one extra branch (`addA-3b-review-fixes`). | Closed |
| Y-A-09 | Medium | Blue round 1 | The fixes introduced six new findings: two Medium (P-A-01, P-A-02), three Low, one Info. | `purple-team.md`, Addendum A. | Blue round 2 (see `blue-team.md`). A fix that changes a boundary (export, import, provenance) needs a test from the other side of that boundary. | Open |
| Y-A-10 | Low | Builder data | The first "illustrative" E4 ampacity values matched a published table, which breaks §A5. The review caught it before merge, and a second set matching another published column (G-B-15) came later. | DA-19; Green G-B-15, G-B-16. | Every value in tests and source is now invented. The A9 gate asks the White team for a grep audit of table data. | Closed |
| Y-A-11 | Low | Documentation | The worked-examples index, the root README counts and 28 stored sheets still carry stale counts or internal section references. DA-23 left the sheet text to the calculator builders, and they did not remove it. | B-A-12, B-A-18, P-A-04. | Blue round 2. Add a template hygiene test for "§A" and review ids in printed text. | Open |
| Y-A-12 | Info | Gates | Every gate built with 0 warnings. Suites are green. Purple ran filtered suites only; the full gate follows round 2. | `LOG.md`, `purple-team.md`. | None. | Closed |

## Addendum B

Builder quality, 2026-10-25. Scope: the Addendum B branches merged up to `34f196cc` on `v0.26.0/addB-4-workflows` (100 non-merge commits since `510f337c`), the Opus review files of each item, `LOG.md`, `STATUS.md` and DB-01…DB-08. S8: reviewed separately before B-5 merges.

### Per branch

| Branch | Content | Reviews run | Defects found by review | Defects found by tests or the gate | Re-work rounds | Warnings | Flaky tests |
|---|---|---|---|---|---|---|---|
| `addB-0-foundations` | Fluid Power discipline (ADR-0170), W3, gas-law kernel, stack-engine API | Opus | 6 Medium, 9 Low (`64342b37`) | Core module count | 1 | 0 | 0 |
| `addB-1-core` | M7, S7, B7, E7, F2 | Opus per calculator, every value recomputed | 2 High (S7 Annex A class 3, F2 bar·L basis), 12 Medium | 0 recorded | 1 | 0 | 0 |
| `addB-2-fluid` | F1, F3, HPU pack | Opus review plus three verification rounds | 1 High (F1 buckling), 10 Medium; F3 L5 accepted | HPU F1 line velocity FAIL after the fix (DB-03: 20 mm line) | 4 | 0 | Tolerance-stack timing test, twice under load (Y-A-05) |
| `addB-3-reuse` | M8, B8, S9, E8, B9 | Opus per calculator, two to three rounds each | 4 High, 14 Medium | A branch switch in the main checkout invalidated one Desktop run; gates moved to their own worktree | 2–3 | 0 | 0 |
| `addB-4-workflows` (in progress) | W1, W2, M9, E9, chains 1, 4, 5, 7–10, licence check | Opus (M9, E9, W2), security (W1), verification rounds, two chain reviews | W1 4 High, 6 Medium; W2 2 High, 7 Medium; M9 2 High, 3 Medium; E9 1 High, 4 Medium; chains 0 High, 10 Medium | Desktop `WorkedExamplePdfTests` 4 failures at `34f196cc` (Y-B-01); S9 Ritz timing test fails under load (Y-B-11) | 2–3 | 0 | Import timing tests moved to a non-parallel collection (DB-07) |
| `addB-5-gated` | S8 | — | S8: reviewed separately before B-5 merges | — | — | — | — |

Every non-merge commit carries the requested trailers (100 of 100). The W2 PDF path was dropped under the §B2 drop order (ADR-0172). E9 and M9, both on the drop list, were built.

### Sheets read
I read the issued text of every Addendum B worked example and the HPU, mechanism-review, cold-pipe and electronics-enclosure packs. The working is mostly clear: formula, substitution, result, and a description on each step. Block messages start with the input and say what to enter. The problems are listed below.

### Findings

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y-B-01 | Medium | `docs/worked-examples/{mechanical/mech.mechanism,electrical/elec.motor-start}/*/` | Four worked examples have no `sheet.pdf`. The Desktop gate fails, and the READMEs promise the file. | `WorkedExamplePdfTests`: 4 failed, 60 passed at `34f196cc`; the size check passed. | Write the PDFs with `TEMPEST_WRITE_WORKED_EXAMPLES=1` and commit before B-4 merges. | Closed (`16caceb7`); 8 PDFs stale again (Y-B-12) |
| Y-B-02 | Low | `ORingGlandCalculator.cs:732-…`; M8, M9, B7 condition checks | Band checks print "utilisation 0", so the governing order is arbitrary; a 0.8 % squeeze margin reads 0 (B-A-14). | F3 worked example §6. | Use limit/value or value/limit; a condition gives its own ratio. | Open: accept for v0.26.0, fix with B-A-14 in v0.26.1 |
| Y-B-03 | Low | `MechanismCalculator.cs:248` | "u_mu = 1000000000" at a toggle. | Probe: L3 50 on the crank-rocker. | Print μ_min 0° (toggle) and "—". | Open: fix now (small) |
| Y-B-04 | Low | `SnapFitCalculator.cs:97`, `AccumulatorCalculator.cs:74`, `ORingGlandCalculator.cs:143`, input texts | "Addendum B §B5/§B6" is printed on 8 issued sheets (14 lines); no hygiene test exists (Y-A-11). | `grep "Addendum B" docs/worked-examples/*/*/*/sheet.txt`. | Plain words; a template hygiene test for "§", "Addendum", "DA-", "DB-". | Open: fix now (text + hygiene test) |
| Y-B-05 | Low | `CalcInputValidator.cs:64` | "was not entered; the template default … is used" for inputs the mode never reads: 34 lines on six Addendum B sheets (F3 12, E9 13, S9 5). | Worked-example sheets. | Warn only for inputs the run read (B-21). | Open: accept for v0.26.0 (B-21, v0.26.1) |
| Y-B-06 | Low | `fp.accumulator` sheet | Pressures print "bar" with no basis; the example mixes absolute p and gauge PS. | `clamp-circuit-bladder-accumulator/sheet.txt` §2, §4. | "bar a"/"bar g" everywhere. | Open: fix now with B-B-05 |
| Y-B-07 | Low | `packs/hydraulic-power-unit/README.md:5` | A single 900-word paragraph. | — | One paragraph per step; an "invented" list. | Open: the narrative moved to line 7 and is now 1074 words; fix now (docs only) or accept |
| Y-B-08 | Low | Worked examples | Eight Addendum B calculators have one example (build prompt §5 asks for two); Addendum A was the same. | `docs/worked-examples/*/`. | Add the second, or a DECISIONS row. | Open: record as accepted (DECISIONS row) |
| Y-B-09 | Info | W2 | PDF table path dropped (§B2 order), recorded in ADR-0172 and the spec. | — | Add it to STATUS and the owner test pack. | Closed (owner test pack, `6ae57e87`) |
| Y-B-11 | Medium | `PlateBendingTests.cs:285-289` (`Review_Ritz_Speed_…_Honours_Cancellation`) | A wall-clock budget (1500 ms) fails in full parallel runs of the Calculators suite; it passes alone. | 1537 ms and 1972 ms in two full runs on 4 cores, 1503/1506 and 1505/1506 passed. | Measure CPU time or move it into the non-parallel timing collection (as Y-A-05). | Closed (`9f3fd919`; Calculators 1515/1515 at `6a189078`) |
| Y-B-10 | Info | Review timing | Every Addendum B item was reviewed before its merge, and every High was fixed with a named regression test (`Review_H1_*`, `H1_*`). The F2 emergency-sizing defect (G-AB-01) passed four review rounds because every reviewer checked the formula as briefed. | Green G-AB-01. | Keep one reviewer who checks the model against the duty, not the formula against the brief. | Closed |

### Verification at `6a189078`

| ID | Fix | Verdict |
|---|---|---|
| Y-B-01 | `16caceb7`: the four M9 and E9 PDFs added; PDFs left stale by version bumps refreshed. | Verified: present. Later fix commits made 8 PDFs stale again (Y-B-12). |
| Y-B-11 | `9f3fd919`: the S9 Ritz speed tests run in the Timing collection and take the fastest of 3. | Verified: Calculators 1515 of 1515 in a full parallel run. Closed. |
| Y-B-07 | `0b6e19cd` rewrote the narrative. | Not closed: the narrative paragraph (README line 7) is now 1074 words. |
| Y-B-09 | `6ae57e87`: the owner test pack records the dropped W2 PDF path (lines 201, 3476). | Closed. |
| Y-B-02…06, Y-B-08 | — | Unchanged. F2 1.3.0 adds one more unused-default warning (`process`) to the energy-storage example (Y-B-05). |

New finding in this round:

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y-B-12 | Medium | 8 worked-example `sheet.pdf` files | The PDFs predate the fixes that changed their `sheet.txt`, so the issued PDF shows the old template version and text. Affected: `fp.accumulator/clamp-circuit-bladder-accumulator` (1.2.0, no gas-process line), `elec.pcb-trace/motor-driver-supply-rail` (1.0.1), all three `bs.pipe-expansion` examples, both `bs.insulation` examples (the chilled one lacks the new warning) and `struct.ltb-interaction/ub-457x191x98-beam-column`. `WorkedExamplePdfTests` checks only existence and size, so the gate cannot see it. | In each folder `sheet.txt` was last changed by `4a19d7b8`, `34044c2d`, `dda97f60`, `5a818356` or `100e8a65`, after the PDF's last commit. | Regenerate after the last fix (`TEMPEST_WRITE_WORKED_EXAMPLES=1 … --filter WorkedExamplePdfTests`). Let the test compare the PDF's extracted text with `sheet.txt` (template line and governing check). | Open: fix now |

S8: reviewed separately before B-5 merges.
