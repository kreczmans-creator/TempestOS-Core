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

Builder quality, 2026-10-25. Scope: the Addendum B branches merged up to `34f196cc` on `v0.26.0/addB-4-workflows` (100 non-merge commits since `510f337c`), the Opus review files of each item, `LOG.md`, `STATUS.md` and DB-01…DB-08. S8: reviewed separately in three Opus rounds (Security and Red sections, S8 lifting); no open Critical, High or Medium finding; the flag stays off by default.

### Per branch

| Branch | Content | Reviews run | Defects found by review | Defects found by tests or the gate | Re-work rounds | Warnings | Flaky tests |
|---|---|---|---|---|---|---|---|
| `addB-0-foundations` | Fluid Power discipline (ADR-0170), W3, gas-law kernel, stack-engine API | Opus | 6 Medium, 9 Low (`64342b37`) | Core module count | 1 | 0 | 0 |
| `addB-1-core` | M7, S7, B7, E7, F2 | Opus per calculator, every value recomputed | 2 High (S7 Annex A class 3, F2 bar·L basis), 12 Medium | 0 recorded | 1 | 0 | 0 |
| `addB-2-fluid` | F1, F3, HPU pack | Opus review plus three verification rounds | 1 High (F1 buckling), 10 Medium; F3 L5 accepted | HPU F1 line velocity FAIL after the fix (DB-03: 20 mm line) | 4 | 0 | Tolerance-stack timing test, twice under load (Y-A-05) |
| `addB-3-reuse` | M8, B8, S9, E8, B9 | Opus per calculator, two to three rounds each | 4 High, 14 Medium | A branch switch in the main checkout invalidated one Desktop run; gates moved to their own worktree | 2–3 | 0 | 0 |
| `addB-4-workflows` (in progress) | W1, W2, M9, E9, chains 1, 4, 5, 7–10, licence check | Opus (M9, E9, W2), security (W1), verification rounds, two chain reviews | W1 4 High, 6 Medium; W2 2 High, 7 Medium; M9 2 High, 3 Medium; E9 1 High, 4 Medium; chains 0 High, 10 Medium | Desktop `WorkedExamplePdfTests` 4 failures at `34f196cc` (Y-B-01); S9 Ritz timing test fails under load (Y-B-11) | 2–3 | 0 | Import timing tests moved to a non-parallel collection (DB-07) |
| `addB-5-gated` | S8 | — | S8: reviewed in three separate Opus rounds (see Security and Red, S8 lifting) | — | — | — | — |

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

S8: reviewed separately in three Opus rounds (Security and Red sections, S8 lifting); no open Critical, High or Medium finding; the flag stays off by default.

## Addendum C

Builder quality and wording, 2026-10-26. Scope: the Addendum C Track S delta on `release/v0.26.0` (`76935116..1474cd0a`, 128 non-merge commits): C-1 fixes, C-3 record import, C-4 dashboard, C-5 guided test, C-6 demo installer and the integration. Sources: the ADRs' review sections, `LOG.md`, DC-01 … DC-14, and every message and label the four new screens show (read from the source and the tests).

### Per branch

| Branch | Content | Reviews run | Defects found by review | Defects found by tests or the gate | Re-work rounds | Warnings | Flaky tests |
|---|---|---|---|---|---|---|---|
| `addC-1-fixes` | 130 of 145 Track P findings fixed; 35 templates moved version | Opus per batch (B to E, two Medium batches) and two verification rounds (N1–N4, then nine items) | N1 (S4 hole without d₀), N2–N4 (braking torque, full-size cpc, ideal gas above 300 bar), nine second-round items; eight owner items recorded (DC-03 … DC-09) | — | 4 | 0 | 0 |
| `addC-3-records` | Records screen, record import (ADR-0174) | Opus | 8 changes (ADR-0174 §11): the ordering of a lookup table is an error, text cells are one line, template 62 carries the material forward, titles never hold a table value, a curve may not repeat a current, a grid unit is one unit, a table added to a seed is the person's entry, no double hint | — | 1 | 0 | 0 |
| `addC-4-dashboard` | Verification dashboard | Opus | 9 changes (`LOG.md`): chains on a recorded run, Clean needs a re-derivation, the snapshot reader never throws, CI `--check`, export hardening, markdown escaping, usable records only, "unknown" defects | — | 1 | 0 | 0 |
| `addC-5-guided` | Guided test mode (ADR-0175) | Opus | 13 changes: 3 Medium (M1 wrong-step compare, M2 a stale compare, M3 unguarded I/O) and 10 Low (L4–L13) | — | 1 | 0 | 0 |
| `addC-6-demo` | Demo installer (ADR-0176) | Opus | 7 changes: exact profile versions, audit before the soft delete, issued Evidence refused, every failure a sentence, a failed clean-up said, stale writes refused, the published section cited as indicative | — | 1 | 0 | 0 |
| Integration | All five merged `--no-ff`; the dashboard wired to C-3 and C-5 | — | A stale pack write to a removed pack (`85c4b8d5`) | Two tests broke on the integrated tree: one name clash (`8f6fc524`, Green G-C-01) and one count hard-coded against the generated script (`ec11fa79`) | 1 | 0 | 0 |

Every non-merge commit in the delta carries both requested trailers (128 of 128). The final gate (`LOG.md`): Engine 465, Sheets 138, Calculators 2,320, FluidPower 118, Import 625, Library 937, Core 7,235, Desktop 1,506, with 0 warnings.

### Wording read
The new screens mostly follow the house style: one sentence, active, naming what to do. Examples: "Close the demo project 'Demo — Hydraulic Press Cell' before removing it."; "Open the worksheet for this step first; the form was opened for another example, so comparing it here would read the wrong numbers."; "The unit of theta_a holds a comma, a quote, a bracket or a line break. A unit is typed on its own (mm2, kN, degC)." Refusals name the line and the column. The problems are listed below.

### Findings

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y-C-01 | Low | `CalcRecordsView.cs:188, 357, 461`; `VerificationDashboardView.cs:74, 88`; `VerificationPdfRenderer.cs:57`; `VerificationMarkdown.cs:43, 112–113` | "Track P" is the name of a work stream, and it appears on the screens and in the exports: "Record template (Track P)", "Record CSV (Track P template)", "No Track P template fills this kind", the column "Track P re-derivation". An engineer who did not commission the work cannot know what it means. | Source strings. | "Record template", "Record CSV (template)", "No template fills this kind", "Independent re-derivation"; update `VerificationMarkdownTests` and the user page. | Fixed (second colour round): "Record template", "Record CSV (from a record template)", "No record template fills this kind", "Independent re-derivation" on the dashboard, its PDF and markdown, "the independent verification documents"; the user page and the runbook line the guided script shows (script regenerated, 0.26.0+c2fa6697) |
| Y-C-02 | Low | `DemoProjectViewModel.cs` (`RunAsync`) | "The demo project act was cancelled; whatever it had started was undone." and "The demo project act failed (…)". "Act" is not a word the panel uses, and "undone" can be untrue (Black B-C-04). | Source strings. | "Installing the demo project was cancelled; what it had made was removed." (with the partial case from B-C-04), and "Installing the demo project failed: …" naming the action. | Fixed (second colour round): "Installing the demo project was cancelled; what it had made was removed.", "Installing the demo project failed (…); press Install demo project again, …", and the panel's own guard "Could not install the demo project: …" |
| Y-C-03 | Low | `CalcRecordsView.cs` (`GuardedAsync`) | "Could not complete that: {message}" does not say what failed. The guided test panel says "Could not <what>: <reason>". | Source string. | Pass the action to the guard: "Could not import the file: …", "Could not read the provenance file: …". | Fixed (second colour round): each handler names its action ("Could not import the file: …", "Could not save the typed table: …", "Could not read the provenance file: …") |
| Y-C-04 | Low | Record import row cap (message from the shared `ImportBudget`) | A file one row over the cap reads "The file has more than 1001 rows." The person counts data rows, and the cap is 1,000 of them; the header is the 1,001st. | Probe: 1,002 lines refused with that text. | In the record import, say "The file has more than 1,000 data rows; split it into two files." | Fixed (second colour round): "The file has more than 1,000 data rows; split it into files of at most 1,000 rows, each with the header."; asserted by `Purple_P_C_01_…` |
| Y-C-05 | Low | `VerificationDashboardView.cs:88` | Column headers "Unconf." and "Second." are abbreviations, and "Second." reads as a time. | Source strings. | "Unconfirmed" and "Secondary" (the PDF already writes "Unconf. / Secondary"). | Fixed (second colour round): "Unconfirmed" and "Secondary" on screen, "Unconfirmed / Secondary" in the PDF |
| Y-C-06 | Info | Review timing | Every Addendum C item was reviewed before its merge, and each review's findings were fixed with named tests before the merge. Two integration breaks came from tests that tied themselves to another item's names or counts (`8f6fc524`, `ec11fa79`). | `LOG.md`, commits. | Keep tests off generated counts; read them from the artefact (as `ec11fa79` now does). | Closed |

> The sections below were written on `release/v0.27.0` (23–24 October 2026) for the legacy calculator replacement (ADR-0167). The owner decided that nothing ships as 0.27.0: the work is part of v0.26.0 and is kept here, unchanged, as a marked part of this file.

## v0.27.0 Phase 1 — foundations (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y8-01 | Low | `CalcReferenceFill.FillAsync` | Reads each quantity through `MakeGenericMethod` over the Core dimension type, as the legacy `CalculationModuleService` did; reflection on a closed, compile-time list of dimension types. | `ReferenceFillTests` (5). | Accepted: the Core readers are generic over the dimension; a non-generic reader in Core is a v0.28 tidy. | Closed |
| Y8-02 | Low | `CalcInputSpec.ReferenceLibrary` | Library slots are strings (`"Materials:hub"`); a typo in a template would show an empty picker rather than fail to compile. | `Slots_Fill_Hub_And_Shaft_From_Different_Records`. | Accepted for v0.27.0: `CalcReferenceFill.Libraries` names the three catalogues and `CatalogueOf` rejects an unknown one with an exception in `ListReleasedAsync`, which the Desktop reports; a template-level unit test per calculator (Phase 5) closes it. | Open → Phase 5 |
| Y8-03 | Info | `LegacyCalculationMigration.MigrateAsync` | One instance read per legacy record on every start-up (idempotence check). | Upgrade test. | Accepted: record counts are hundreds, not millions; a migration marker is unnecessary until measured. | Closed |
| Y8-04 | Low | `ColumnBucklingCalculator.Run` | The slenderness block formats λ with a .NET format string rather than `CalcValue.FormatNumber`, the convention every other sheet message uses. | Code read. | Fixed after the Phase 1 gate (same output under invariant globalization). | Fixed |
| Y8-05 | Info | `SpecHandArithmeticTests` | Spec arithmetic is generated from stored sheets; a template change now needs three write passes (golden, worked examples, spec), in that order, because the spec reads the sheet the worked examples wrote. | Two drift failures during Phase 1, both caught by the test. | Documented in `docs/worked-examples/README.md` regeneration line. | Closed |
| Y8-06 | Info | Archives | A migrated record has no project and no register entry, so nothing in the Desktop lists it yet; the coordinator opens it by Id. | `ALegacyArchive_IsShownReadOnly_…`. | Phase 4: the worksheet catalogue lists archives under their own node, newest first, capped at 500 (`Archives_AreListedUnderTheirOwnNode…`). | Fixed |

## v0.27.0 Phase 2 — mechanical ports (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y9-01 | Medium | `BearingLifeCalculator` | A load of 1e-300 kN passed the hard limits and drove (C/P)^p to infinity; the engine blocked the step, but its message named NaN on the sheet. | `Hostile_Numbers_Never_Throw_And_Never_Reach_A_Sheet(mech.bearing-life)`. | `light-load` block below P/C = 10⁻⁶ (life effectively unlimited). | Fixed |
| Y9-02 | Low | `ThinVesselCalculator` | Keeps the legacy design factor `n` on the computed thickness, which UG-27 does not have. | Spec, input description. | Kept so a legacy record re-entered gives the same thickness; defaults to 1 and is labelled "legacy". | Closed |
| Y9-03 | Info | `MaterialMarginCalculator` | Adds `k_u` (ultimate factor relative to yield), which the legacy module did not have; with `f_u` empty the sheet reduces to the legacy yield margin. | `ka-margin-yield-only`. | None. | Closed |
| Y9-04 | Info | `FatigueMinerCalculator` | Two outputs per block (N_i, d_i): a 200-block spectrum gives 400 outputs on the results table. | Code read. | Accepted; the sheet's results are the record of the spectrum. | Closed |
| Y9-05 | Info | Known answers | Every legacy hand calculation of the five ported modules reproduces to five significant figures (17 cases); the two originals with property tests only gained two hand calculations each. | 468 Calculators tests. | None. | Closed |

## v0.27.0 Phase 3 — structural and thermal ports (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y10-01 | Medium | `FilletWeldCalculator` | A lap weld longer than 150·a carries less than the simplified method says (BS EN 1993-1-8 4.11 long-joint reduction β_Lw); the legacy module and the first port said nothing. | `Long_Welds_Warn_That_The_Long_Joint_Reduction_Is_Not_Applied`. | `long-joint` warning above 150·a; spec limitation updated. | Fixed |
| Y10-02 | Info | `PlaneWallCalculator` | No pass/fail check: the result is a heat flow. No condensation (dew-point) check on the cold surface; the single-pane example's −2.2 °C inner surface would frost. | Worked example `single-pane-window`. | Stated in the spec's limitations and the example's notes. | Closed |
| Y10-03 | Low | Phase 2 ports | Phase 3's templates have behaviour tests (properties, blocks, warnings); the seven Phase 2 ports have known answers and the shared hostile-input and template tests only. | `tests/Tempest.Calc.Tests.Calculators/Mechanical` has no port tests. | `MechanicalPortTests`: 26 tests over all seven (linearity, limit cases, Tresca/von Mises relations, gap and restraint limits, UG-27 inverse, Miner additivity, every block). | Fixed |
| Y10-04 | Info | Known answers | All nine legacy hand calculations of the three ported modules reproduce to five significant figures, including the exactly-on-limit chain (u = 1, margin 0). | 530 Calculators tests. | None. | Closed |

## v0.27.0 Phase 4 — Desktop retirement (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y11-01 | High | Worksheet form | The legacy calculators drew an input-bound reference diagram for every calculation (ADR-0158, Principle 33); the worksheet form drew none, so retiring the legacy surface would have lost it. | Found while retiring: `CalculationDiagramViewTests` exercised the diagram only through the legacy form. | D-41: retirement parked; `CalcDiagrams` (27 templates, 60 drawings) and `CalcDiagramReader`; the legacy form's behaviours (beside or above, focus both ways, row marking, theme) moved to the worksheet form and its 11 tests ported. Every drawing rendered and checked for overlapping labels. | Fixed |
| Y11-02 | Low | `CalcSheetView` | The governing-check "Go to step" button had no automation name; the coverage scan never reached the Worksheets node because Calculators stood first. | `AutomationNameCoverageTests` after the node change. | Named. | Fixed |
| Y11-03 | Info | Calculation register | Calculation objects created by the retired surface lose their Re-run command; their runs are archives (read-only). | ADR-0167 Amendment 1. | Accepted: re-enter the inputs in the worksheet template for a run with working. | Closed |
| Y11-04 | Info | Real-shell journey | `MainJourney.Calculators` now drives Worksheets; it needs a display and was not run in this container. | Code change only. | Run on the owner's Windows machine with the runbook. | Open → owner |


## v0.27.0 final (2026-10-23)

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y12-01 | Info | Open items | Y11-04 (the real-shell journey on Windows) is the only open Yellow item; it needs the owner's machine. | Handover step 3. | — | Open → owner |
| Y12-02 | Info | Results that change | Bolted-joint sheets saved under v0.26.0 with a failed separation check understated bolt stress (G-05); their template hash changes, so they show the stale marker until re-run. | `StalenessAsync` (template hash). | Release notes tell the owner to re-run them. | Closed |
| Y12-03 | Low | Lesson | B-26 was a regression in a suite not rerun after a template change. | Black on the clean clone found it. | Before a merge, run every Calc suite, not only the one changed. | Closed |

## v0.27.0 showcase journey (2026-10-24)

One worked example per discipline driven through the real window, a screenshot per stage, then each finding fixed and the journey re-run.

| ID | Sev | Location | Description | Evidence | Fix / justification | Status |
|---|---|---|---|---|---|---|
| Y13-01 | High | `CalcSheetView` | An optional input an import left out came back at its default with no source, so Check was refused on the beam example. | `An_Optional_Input_An_Import_Leaves_Out_Stays_Blank_So_The_Inputs_Remain_Sourced`. | A stated seed (import or saved instance) leaves absent optional inputs blank. | Fixed |
| Y13-02 | Medium | `CalcSheetView` | An import, save or revision rebuilt the form and left the record pickers on "Loading released records…". | `Released_Record_Options_Survive_An_Import_And_Are_Dropped_For_Another_Template`. | The view keeps the listed records per template and shows them on a rebuild. | Fixed |
| Y13-03 | Medium | Worksheet checker, shell header | The checker was read from `host.SessionPrincipal`, the author from `ICurrentPrincipalAccessor`; the header was refreshed only by Settings. | `WorksheetJourneyTests` (switch person, issue, author = checker = header). | Checker read from the accessor; `WorkspaceHost.PrincipalChanged` refreshes the header. | Fixed |
| Y13-04 | Medium | Calculation register | The node showed only the bracket-check records; issued worksheets were not listed anywhere in the register. | `CalcRegisterViewTests`, `WorksheetJourneyTests`. | `CalcRegisterView` above the bracket surface: every worksheet calculation in the project, status, governing check, Open into the form. | Fixed |
| Y13-05 | Low | `CalcSheetView` | Results shrank to its values; each check wrapped in a 220 px column. | `Results_Fill_The_Column_And_A_Check_Reads_Across_It`. | Sections stretch; a check row reads across the column with its clause at the right. | Fixed |
| Y13-06 | Low | Sheet PDF | Table input rows wrapped a word per line in the Value column; check demands wrapped; status fields ran together. | `WorksheetJourneyTests` (row text on one line). | Table rows in a full-width table under the inputs; wider Demand column; fields parted by "·". | Fixed |
| Y13-07 | Medium | PDF fonts | Space Mono has no Σ: "R_tot = Σ R_i" printed a box. | `A_Symbol_The_Mono_Face_Lacks_Is_Drawn_In_The_Body_Face_In_Place`. | A missing glyph is drawn in the body face, in place. | Fixed |
| Y13-08 | Medium | Sheet PDF | The issued sheet carried only text and maths; the annotated reference diagram the form shows was not exported (owner's ask, 2026-10-24). | `An_Issued_Sheet_Carries_Its_Reference_Diagram_Read_Against_Its_Inputs`, `WorksheetJourneyTests`. | `CalcSheet.InputValues`; `CalcDiagramPainter` draws the diagram as vectors, its labels as text, under Scope and method as Figure 1 with the variant's caption. | Fixed |
| Y13-09 | Low | Sheet PDF | References were listed as "Ref:" lines under the method and as free text in the working margin and checks table; no numbered list (owner's ask: AMA, at the very end). | `CalcBibliographyTests`, `An_Issued_Sheet_Carries_Its_Reference_Diagram_Read_Against_Its_Inputs`. | D-44: AMA superscript citations with locators throughout; "9. References" closes the sheet. | Fixed |
| Y13-10 | Low | Templates, sheet PDF | The method printed as one paragraph of formulas with no words for what each one calculates. | `Method_Is_One_Step_Per_Line_Each_Saying_What_It_Calculates` (27 templates); PDF table assertion. | D-46: one step per line with its narrative; a two-column method table on the sheet; golden sheets and worked examples regenerated (only method, version and hash lines change). | Fixed |
| Y13-11 | Info | `Red_SolverAttacks` | `Extremes_In_Every_Numeric_Input_Never_Throw(mech.tolerance-stack)` failed once (5 s) right after a golden-file regeneration; it passed in three full-suite runs and five isolated runs after. | Not reproduced. | Watch: if it recurs, capture the message (the run kept no detail). | Open |
| Y13-12 | Low | Form, diagram, sheet PDF | Symbols read as identifiers (D_1, sigma_A, F_KR_req). | `SymbolTextTests` (15). | D-47: typeset subscripts and Greek letters. On screen a subscript run's baseline setting barely lowered it at form sizes; it is drawn as a small lowered block instead (checked on the showcase screenshots). | Fixed |
