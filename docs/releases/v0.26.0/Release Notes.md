# TempestOS v0.26.0 — Release Notes (release candidate)

**Theme.** Tempest computes. A transparent, unit-aware worksheet engine (`Tempest.Calc.*`) with twelve engineering calculators across four disciplines, every sheet showing formula → substitution → result with clause references, issued through the existing Evidence lifecycle in one transaction (ADR-0165).

## What is new

- **Engine** (`Tempest.Calc.Engine`): expressions over a runtime SI dimension vector, unit conversion (`in`), functions, vectors and matrices, a step log, hard caps on everything, seeded Monte Carlo and tornado sweeps.
- **Sheets** (`Tempest.Calc.Sheets`): the one calculation-sheet shape (header, scope and method, inputs with provenance and valid range, assumptions, working, checks governing first, applicability, verification footer); input validation; CSV and JSON exchange with formula-injection defences.
- **Library** (`Tempest.Calc.Library`): calculation instances and packs under Evidence (Draft → Checked → Issued → Superseded), one-transaction Issue, automatic Calculation-register link, staleness marker, concurrent-issue guard.
- **Desktop**: Worksheets under Modules → Calculations and under each discipline; a shared form with live recalculation, unit pickers, provenance per input, governing-check bar, Show working, read-only issued instances; Ctrl+K entries per calculator; calc-sheet PDFs with subset fonts (< 200 KB).
- **Calculators**: Mechanical — tolerance stack v2, ISO 286 fits, press fit, thick cylinder, bolted joint, bolt group. Structural — section properties, 2D frame solver, beam formulas, EN 1993-1-1 basic member checks. Building services — pipe pressure drop and pump duty, psychrometrics, duct sizing. Electrical — motor sizing, enclosure heat and fan sizing, voltage drop. Each ships with known-answer, property, robustness, golden and hostile-input tests, a spec under `docs/calcs/`, and two worked examples with PDFs under `docs/worked-examples/`; plus one calc pack example (M2 + M3 + S1).
- **Core**: `IEvidenceService.IssueInOneTransactionAsync`; transaction-time status re-check on Issue (fixes a double-issue race); TrueType glyph subsetting for every PDF.

## Known limitations

- EN 1993-1-1 checks cover classes 1–3 and no lateral-torsional buckling; interaction factors are user inputs.
- Fluids other than water are entered with their own density and viscosity; no glycol correlation is embedded.
- Excel (xlsx) import/export is not included; CSV and JSON are.
- Tabulated standard data is never embedded: imperfection factors, partial factors, loss coefficients and heat-transfer coefficients are editable inputs with provenance.
- Reference values for some known-answer cases are reproduced from public mirrors or from the published relations rather than the printed source (`Unconfirmed` where so).

## Upgrade

No schema change. Existing databases open unchanged; issued Evidence stays immutable; the Calculation register gains `documentedBy` links as calc instances are created.

See `CHANGELOG.md`, `docs/decisions/v0.26.0-DECISIONS.md`, `docs/reviews/` and `docs/handover/v0.26.0-handover.md`.
