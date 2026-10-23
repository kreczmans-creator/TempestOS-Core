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
