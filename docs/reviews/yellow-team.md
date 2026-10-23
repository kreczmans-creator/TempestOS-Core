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
| Y7-08 | Info | `CalcSheetPdfRenderer.T` | One helper now cleans and caps every drawn string; the text renderer's `Clean` is the single source of the character rules. | `Red_PdfRendererAttacks`. | None. | Closed |
