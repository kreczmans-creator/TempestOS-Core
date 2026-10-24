# Changelog

## v0.27.0 (release candidate in preparation)

### Added
- Eleven worksheet templates replacing the legacy calculation modules (ADR-0167): `struct.column-buckling` (Perry–Robertson), `mech.lifting-lug`, `mech.bearing-life` (ISO 281), `mech.fatigue-miner`, `mech.shaft-stress`, `mech.thermal-expansion`, `mech.thin-vessel`, `mech.material-margin`, `struct.fillet-weld` (EN 1993-1-8 simplified method, long-joint warning), `bs.plane-wall`, `elec.thermal-chain` (D-40). Every legacy test vector is carried as a known-answer case at 5 × 10⁻⁵; each template has behaviour tests, a spec and two worked examples with PDFs.
- Reference diagrams in the worksheet form for all 27 templates (ADR-0158, Principle 33, D-41): input-bound, not to scale, beside the inputs or above them on a laptop window, focus and pointer linked both ways.
- Legacy record migration: every stored legacy calculation record opens as a read-only archive worksheet (same Id), listed in the worksheet catalogue; verified on real v0.25.3 and v0.26.0-rc1 databases (Grey).
- Compare with previous revision over calc instances (inputs row by row, outputs with relative change, checks).
- Reference-library fill of worksheet inputs from released materials, fasteners and bearings, with the record's pin as the source.
- Issued calculation sheets: the reference diagram as Figure 1 (vectors, labels as text), the method as a step table (what each step calculates beside its relation), AMA citations with one numbered reference list at the end (D-44, D-45, D-46).
- Calculation register lists the project's worksheet calculations (status, revision, governing check) and opens them in the form.
- Every spec gains an "In plain terms" paragraph and the hand arithmetic of its first worked example, generated from the sheet and drift-guarded.

### Changed
- `mech.bolted-joint` 1.1.0: once the joint separates the clamp is zero and the bolt carries the whole working load (before, the bolt load was understated: the overloaded example showed yield at 89 % instead of 115 %); fatigue amplitude from the bolt-load range; separation-aware yield factor (D-42); a given-preload method with optional given stiffnesses and stress area; legacy joint-diagram load factors n_0, n_L.
- `mech.bolt-group` 1.1.0: a single bolt takes direct shear; a moment the bolts cannot resist is refused instead of dropped.
- `struct.beam-formulas`: the legacy stress and deflection-limit checks and cases.
- All 27 worksheet templates move up one patch version (e.g. `mech.bolted-joint` 1.1.1): the method is restated one step per line, each saying what it calculates. No formula, input or result changes; a saved calculation shows "newer version" until it is re-run.
- Worksheet form and sheets: record pickers keep their records after an import; the author, checker and header name the same person; Results uses the full width; units print as m², W/(m²·K); symbols the mono face lacks (Σ) print correctly.
- Statistics: the upper tail of the normal distribution keeps its precision deep in the tail (DPMO at 7σ was right to four digits only).

### Removed
- The legacy Engineering Calculators surface (Modules → Calculations → Calculators) and its module service and workbench (ADR-0167 Amendment 1). Worksheets replace it. The legacy engine, its records and the module definitions stay; records migrate to archives.

## v0.26.0 (release candidate)

### Added
- `Tempest.Calc.*` module family: transparent worksheet engine, sheets, library, verification (ADR-0165). Engine: unit-aware expressions over a runtime SI dimension vector, step log (formula → substitution → result), caps, seeded Monte Carlo, tornado sweeps. Sheets: the one §3.4 sheet shape, validation, CSV/JSON exchange. Library: calculation instances with provenance, Evidence-governed Check/Issue in one transaction, Calculation-register link, calc packs, stale marker.
- `IEvidenceService.IssueInOneTransactionAsync`: the issue sheet bytes, attachment, issue record and status commit together; the Workspace issue command uses it.
- Desktop: **Worksheets** node under Modules → Calculations with the shared worksheet form (live recalculation, unit pickers, provenance per input, governing-check bar, Show working, read-only issued instances); calc-sheet PDF renderer; TrueType glyph subsetting for every PDF (issue sheet 222 → 162 KB).
- Mechanical worksheets: M1 `mech.tolerance-stack` v2 (WC, RSS, modified RSS, distributions, C_p/C_pk/DPMO/yield, Pareto, Monte Carlo, GD&T contributors, BS 8888/ASME notation); M2 `mech.iso286-fit` (ISO 286-1 formulae), `mech.press-fit`, `mech.thick-cylinder` (migrated); M3 `mech.bolted-joint` (simplified VDI 2230), `mech.bolt-group` (elastic method). Each with known-answer, property, robustness and golden tests, a spec under `docs/calcs/`, and two worked examples under `docs/worked-examples/`.
- Structural worksheets: S1 `struct.section-properties` (parametric shapes with fillets, polygon with holes, composites; Green's theorem, plastic moduli, torsion, warping; Blue Book UB/SHS within 0.5 %); S2 `struct.frame-2d` (direct stiffness 2D frames: releases, springs, cases and combinations, envelope, caps 2,000 nodes / 4,000 elements / 50 cases) and `struct.beam-formulas` (Roark single-span library); S3 `struct.en1993-member` (EN 1993-1-1 tension, compression, flexural buckling, bending, shear, simplified interaction; clause numbers only, editable imperfection and partial factors). Same DoD as the mechanical set.
- Building-services worksheets: B1 `bs.pipe-pump` (Darcy–Weisbach, Colebrook with Swamee–Jain check, K and equivalent-length fittings, system curve, pump duty point, affinity laws, NPSH; water correlations Kell/Vogel/Hyland–Wexler); B2 `bs.psychrometrics` (state from any second property, sensible/cooling-dehumidification/steam/evaporative/mixing processes, chart series); B3 `bs.duct-sizing` (equal-friction and velocity methods, round/rectangular/flat-oval equivalent diameters, fittings by user coefficients, index-run totals, limit checks). Same DoD.
- Electrical worksheets: E1 `elec.motor-sizing` (linear and rotary loads through screw / belt / rack / gearbox, trapezoidal and S-curve profiles, peak and RMS torque, inertia ratio, candidate-motor checks); E2 `elec.enclosure-heat` (heat-load schedule, effective surface by face exposure, editable heat-transfer coefficient, natural rise, forced-ventilation airflow with altitude correction, fan duty point, solar gain, cooling-unit capacity); E3 `elec.voltage-drop` (DC, single- and three-phase from resistivity, R/X per km or mV/A/m; parallel conductors; loss; conductor temperature estimate; user limits, BS 7671 cited not embedded). Same DoD.

### Changed (Phase 7 — Black/Blue, ADR-0166)
- Desktop worksheet form: *Import inputs (CSV/JSON)* re-seeds the form from the clipboard (a worked example's `inputs.json` or the exported CSV), carrying table and vector inputs the form has no field for; every import notice is shown and a blocking one imports nothing.
- Check and CheckPack refuse a draft whose inputs lack provenance; a Rejected check stays on the instance and leaves the Evidence Draft; empty checker, organisation, issue reference and client are refusals.
- Sheets: table inputs are written row by row (up to 100 rows); utilisation checks read `u = 0.375 vs u ≤ 1`; valid ranges follow the entered unit; a non-optional input that takes its template default raises a warning naming the default.
- JSON import: duplicate symbols and ValueText-only values are refused by name; a malformed document is described in plain words.
- PDFs: the font subsetter keeps only the tables a PDF needs, so Windows's default face embeds at the same size as the brand faces.

### Security (Phase 6 — Red/Blue)
- Imports: duplicate CSV headers, quoted-cell length, sign-prefixed DDE formulas, null JSON entries and null table rows are refused with notices (never exceptions); provenance reference/revision capped at 200/40 characters; validator notices capped per input.
- Engine and solvers: variadic reducers bounded before allocation; calculator Monte Carlo capped at 100,000 samples; frame solver bounded per member (200 loads) and by factor work.
- Library and Core: optimistic concurrency on every instance write; Check and Issue commit with the instance in one transaction with a transaction-time status re-check; issued records compared in full before a Superseded mark is accepted; Revise atomic and recoverable; pack members frozen after the pack check.
- PDF: every drawn string cleaned of control, separator and format characters and capped per field and paragraph.
