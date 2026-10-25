# TempestOS v0.26.0 — Release Notes (release candidate)

**Theme.** Tempest computes. A transparent, unit-aware worksheet engine (`Tempest.Calc.*`) with twelve engineering calculators (sixteen worksheet templates) across four disciplines, every sheet showing formula → substitution → result with clause references, issued through the existing Evidence lifecycle in one transaction (ADR-0165).

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

---

## Legacy calculator replacement (formerly planned as v0.27.0)

The work done on `release/v0.27.0` ships in v0.26.0; nothing ships as 0.27.0 (decision DI-01). What follows is the v0.27.0 release note as written, with its counts and names corrected for the merged line.

**Theme.** One calculation surface. The legacy calculation modules are replaced by worksheet templates that show their working, carry every legacy test figure, and draw a reference diagram beside the form (ADR-0167).

### What is new

- **Eleven more worksheets** (54 in all, with Addenda A and B): column buckling, lifting lug (`mech.lifting-lug`, behind the same off-by-default `lifting` feature as S8, with an attestation and a second check), basic bearing life (`mech.bearing-life-basic`; Addendum A's full ISO 281 sheet `mech.bearing-life` is unchanged), fatigue (Palmgren–Miner), shaft stress, thermal expansion, thin-walled vessel, material margin, fillet weld, plane wall heat transfer, heat sink thermal chain. Every hand calculation and test vector of the legacy modules runs as a known-answer case.
- **Reference diagrams** for the 27 worksheets of this replacement (the 27 added by Addenda A and B have none yet), bound to the inputs: focus a field and its shapes light; point at a shape and its row is marked; click it and the field takes focus.
- **Your old calculations stay readable.** At start-up every stored legacy calculation becomes a read-only archive worksheet, listed under *Archived legacy calculations* in the worksheet catalogue.
- **Compare with previous revision** sets two revisions of a calculation side by side.
- **Fill from a reference record**: material, fastener and bearing properties come from released records, with the record named as the source.

### Corrections that change results

- **Bolted joint, separated joints.** When the working load opens the joint, the bolt now carries the whole working load and the clamp reads zero. Before, a separated joint understated bolt stress and fatigue amplitude; re-check any bolted-joint calculation whose separation check failed.
- **Tolerance stack, very small reject rates.** Predicted DPMO beyond about 6σ is now right to five figures.

### Removed

- Engineering → Modules → Calculations → **Calculators**. Use **Worksheets**. Calculation-register entries made by the old surface remain; their runs open as archives.

### Upgrade

No schema change. On first start the migration copies each legacy calculation record into an archive worksheet with the same Id; the record itself is untouched and the copy is made once.

See `CHANGELOG.md`, `docs/decisions/v0.26.0-DECISIONS.md` (D-39…D-47, DI-01…DI-09), `docs/reviews/` and the "Legacy calculator replacement" section of `docs/handover/v0.26.0-handover.md`.
