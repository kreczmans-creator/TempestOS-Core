# Security review — v0.26.0 calculation surface (Phase 6)

Scope: the `Tempest.Calc.*` assemblies (Engine, Sheets, Verification, Library, Mechanical, Structural, BuildingServices, Electrical), the Desktop worksheet form and PDF renderer, and the Core seams they use (one-transaction Issue, FTS indexing). Method: STRIDE over the trust boundaries, then the nine §7 scopes with evidence from the test suites. Severity: Critical/High fixed with regression tests; Medium fixed or justified; Low logged.

## Trust boundaries

| Boundary | What crosses it | Control |
|---|---|---|
| User → form / CSV / JSON | Input values, units, provenance text, table rows | `CalcInputValidator` (kinds, hard limits, row caps), `CalcCsv` (size, rows, formula cells), `CalcInputJson` (schema, caps) |
| Template author → engine | Expression text (compiled in, not user-entered in v0.26.0) | `CalcLimits` caps on length, tokens, nesting, symbols, literals, steps, variables, arrays, matrices, iterations |
| Sheet text → store / PDF / FTS | Rendered sheet, titles, names | `CalcSheetTextRenderer.Clean` strips control characters; PDF draws glyphs (no markup); FTS tokeniser splits to alphanumeric terms |
| Library → Core persistence | Instances, packs, Evidence attachments | Parameterised store API (`IPersistenceTransaction`), immutability enforced in `CalcInstanceStore`, Issue in one transaction |
| Composition → runtime | Calculator assemblies | Static project references and `CalcCalculatorComposition.All()`; no path-based loading |

## STRIDE

| Threat | Where | Finding | Status |
|---|---|---|---|
| Spoofing | Checker / issuer identity | Taken from the signed-in principal through Core's `CurrentPrincipalAccessor`; the checker-independence switch is Core's (unchanged). | No new surface |
| Tampering | Issued instances, packs, sheets | `Issued_Instance_Is_Immutable_In_The_Store`, `Pack_…`, `UpdateInputsAsync` refused after Issue; Evidence revision immutable in Core; attachment id chosen before commit so nothing is written after. | Closed |
| Repudiation | Who issued what, when | Evidence issue record (reference, client, revision, author, date) plus the instance snapshot (inputs, outputs, template text, hash, engine version, test results). | Closed |
| Information disclosure | Sheets, logs | No logging of inputs anywhere in `Tempest.Calc.*` (no logger dependency); worked examples use fictional project references. | Closed |
| Denial of service | Expressions, numerics, imports, solvers | Every cap tested (see scope 4–5); solver sizes capped (frame 2,000 nodes / 4,000 elements / 20 M banded entries; polygon 20,000 vertices / 200 loops; Monte Carlo 1,000,000; Colebrook 100 iterations; bisections 200). | Closed |
| Elevation of privilege | Expression engine, composition | No reflection, `eval`, file or network access from expressions; function table is fixed; calculators are compiled code. | Closed |

## The nine scopes

| # | Scope | Evidence | Result |
|---|---|---|---|
| 1 | Input handling (CSV/JSON/Excel) | `Csv_Import_Caps_Rows`, `Csv_Import_Caps_Size`, `Csv_Import_Refuses_Formula_Cells`, `Csv_Escapes_Quotes_And_Commas`; JSON reader returns notices, never throws; no archive handling; no path handling in Calc (the Desktop file dialogs own paths). Excel import/export not implemented (D-23: CSV opens in Excel; a managed MIT xlsx library is a later decision). | Pass |
| 2 | Template text and symbols | `Render_NeverInterpretsAHostileString`, `Hostile_Strings_Cannot_Forge_Lines` (Sheets); `HostileInputTests.Hostile_Text_Is_Contained` over all 16 templates (free-text inputs and table text columns: formula prefixes, markup, control characters, SQL text, 5,000-character strings, bidi override) — no exception, no control characters in the sheet, bounded size. PDF: glyph drawing only. FTS: `IndexTextAsync` receives `Clean`ed text and Core's tokeniser rewrites the MATCH expression term by term. | Pass |
| 3 | SQLite | Calc never builds SQL; it uses `IPersistenceTransaction` (parameterised in `SqlitePersistenceStore`). No new migrations: instances and packs are JSON documents in collections `Calc.Instances` / `Calc.Packs`. Immutability: `Issued_Instance_Is_Immutable_In_The_Store`; concurrency: SEC-05 found and fixed (one of four racing issues succeeds; one attachment). | Pass (after SEC-05) |
| 4 | Expression engine | Caps: `Expression_Length_Is_Capped`, `Token_Count_Is_Capped`, `Nesting_Depth_Is_Capped`, `Deep_Nesting_With_Default_Limits_Does_Not_Overflow_The_Stack`, `Huge_Exponent_Literal_Is_Refused`, `Exponents_Are_Capped_So_No_Expression_Overflows`, `Symbol_Length_Is_Capped`, `Step_Count_Is_Capped`, `Variable_Count_Is_Capped`, `Cancellation_Becomes_A_Block`. No reflection, file or network API is referenced by `Tempest.Calc.Engine` (System.IO only for `CalcCsv` text readers over strings). | Pass |
| 5 | Numeric DoS | `Monte_Carlo_Sample_Count_Is_Capped`, `Monte_Carlo_Bin_Count_Is_Capped`, `Monte_Carlo_Honours_Cancellation`, `Matrix_Order_Is_Capped`, `Array_Literal_Size_Is_Capped`, `Fixed_Point_Iterations_Are_Capped`, `Root_Finding_Iterations_Are_Capped`, `Tornado_Input_Count_Is_Capped`; `FrameTests.Caps_Are_Enforced`, `A_Long_Continuous_Beam_Solves_Within_The_Caps`; `SectionPropertiesTests.Vertex_Cap_Is_Enforced`; every table input has `MaxRows`. | Pass |
| 6 | Plugins / assembly loading | `CalcCalculatorComposition.All()` is a static list over project references; `grep Assembly.Load` finds only the frozen `Tempest.Core.Plugins` (unchanged, signed manifests). | Pass |
| 7 | Dependencies | `dotnet list package --vulnerable --include-transitive` on 2026-10-23: every project reports no vulnerable packages. No new `PackageReference` in any `Tempest.Calc.*` project (Engine/Sheets/Verification/Library/calculator assemblies have none). NetAnalyzers with `AnalysisModeSecurity=All`, `AnalysisLevel=latest`, `TreatWarningsAsErrors=true`, `InvariantGlobalization=true` in every new assembly; lock files committed (ADR-0160). | Pass |
| 8 | Secrets / PII | No secrets in the diff (no connection strings, tokens or keys); worked examples cite fictional drawings and specifications (`DRG-…`, `SPEC-…`) and public textbook values; no client data. No logging in Calc. | Pass |
| 9 | Supply chain | CalcpadCE is **not** vendored (D-01); nothing was copied from it. `THIRD-PARTY-NOTICES.md` unchanged and complete (no new direct packages). Fonts: existing brand fonts, now subset per document. | Pass |

## Findings

| ID | Sev | Location | Description | Fix / justification | Status |
|---|---|---|---|---|---|
| SEC-05 | High | `EvidenceService.IssueInOneTransactionAsync`, `CalcInstanceService.IssueAsync` | Two Issues raced on one checked record both succeeded: the status check ran before the transaction opened and the mutation wrote the captured in-memory state. Found by `ConcurrencyTests` (Phase 6 PoC); not reachable from one UI click, reachable from two sessions or a double submit. | Core: `MutateAndPersistAsync` gained a `verifyBeforeWrite` hook; `Evidence.RecordIssueAsync` re-reads the stored state under the Issue transaction (`EngineeringObjectStateStore.ReadInTransactionAsync`) and throws `ConcurrentIssueException` (rolled back, reported as `TransitionNotPermitted`). Library: `CalcInstanceStore.WriteIssuedInstanceAsync` re-reads the instance under the same transaction and throws `CalcConcurrentIssueException` (refused). Regression tests: `ConcurrentIssueTests` (Core, 4-way race and issue-after-issue), `ConcurrencyTests` (Library). | Fixed |
| SEC-01 | Medium | `CalcInputJson` / Desktop import | A JSON file with a huge single string value (e.g. 50 MB provenance text) is read fully before the text-length cap applies. | Desktop import reads files through the existing size-limited reader (same path as CSV, `Csv_Import_Caps_Size` sizes); Calc-level cap on provenance and text inputs (`MaxTextLength` 2,000) applies on bind. Residual: memory of one file read, bounded by the dialog's size limit. | Justified |
| SEC-02 | Low | `FrameSolver` | Mechanism detection is a relative pivot threshold (1e-10 × max diagonal); a pathological stiffness ratio could pass (Y3-03). Not a security issue (no unbounded work; result is a numeric answer with a warning-free sheet). | Red probed: a mechanism beside a member 10¹² times stiffer and a released chain under transverse load are both reported as mechanisms (the error is in the safe direction). | Closed (Red, no PoC) |
| SEC-03 | Low | Worked-example writer | `TEMPEST_WRITE_WORKED_EXAMPLES=1` writes under the repository root found by walking up to `VERSION`; test-only, never in the product. | Test-only code path; documented. | Logged |
| SEC-04 | Info | Issued pack sheet | The pack issue sheet is text (the Library renders text when no PDF renderer is injected); the Desktop injects the PDF renderer for the product path. | By design (D-24). | Logged |

| R-01…R-24 | 6 High, 12 Medium, 5 Low, 1 Info | See `red-team.md` | Red team's offensive review (138 PoC tests; 44 failed): import caps, provenance size, reducer allocation, null table rows, solver work bounds, issued-record tamper under a Superseded mark, Check/Update/Revise races and crashes, pack member drift, notice floods, PDF control characters. | Every High, Medium and Low fixed (`blue-team.md`) with the PoCs kept as regression tests and verified (`purple-team.md`); R-22 Low accepted (D-32). | Closed (R-22 accepted) |

One High finding (SEC-05) fixed with regression tests in Core and Library; Medium SEC-01 justified above. Red's six High and twelve Medium findings are fixed and verified; the gate (no open Critical/High, every Medium fixed or justified) holds.
