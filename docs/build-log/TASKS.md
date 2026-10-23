# v0.27.0 tasks — legacy calculator replacement (D-38)

v0.26.0's task list is in git history at `release/v0.26.0` (`docs/build-log/TASKS.md`); its close-out checklist still applies to tagging `v0.26.0-rc1`.

## Phase 0 — setup (`release/v0.27.0`)
- [x] Branch from release/v0.26.0; VERSION 0.27.0; CHANGELOG section; v0.27.0-DECISIONS (D-39)
- [x] ADR-0167: the legacy calculation modules are superseded by Tempest.Calc templates; records migrate; tests carried as known-answer cases
- [ ] Draft PR release/v0.27.0 → main (dashboard)
## Phase 1 — foundations (`v0.27.0/phase-1-foundations`)
- [x] Record migration: every stored `CalculationRecord` readable as a `CalcInstance` archive snapshot (`LegacyCalculationMigration`, `LegacyArchiveSheet`, `ICalculationEngine.FindRecordDocumentAsync`); lifecycle refusals on archives; Grey upgrade test over a real SQLite database with a legacy bolted-joint record
- [x] Reference-library fill: `CalcReferenceFill` (released records only; `FromReference` pin per value; library slots such as `Materials:hub`); declared on struct.en1993-member, struct.beam-formulas, struct.frame-2d, mech.press-fit (hub/shaft), mech.bolted-joint (fastener + members); one picker per library in the worksheet form
- [x] Spec raise: "In plain terms" paragraph (hand-written) + hand arithmetic of the first worked example (generated from its sheet, drift-guarded by `SpecHandArithmeticTests`) in all 16 specs
- [x] Bolted-joint joint-diagram load factors (F_A_sep, n_0, n_L) with consistency tests; golden, worked examples, PDFs, pack and spec regenerated
- [x] Beam formulas: legacy stress (M·c/I) and deflection-limit checks, deep-beam warning, four legacy hand calculations as known-answer cases (8/8)
- [x] `struct.column-buckling` (Perry–Robertson, BS 5950-1 Annex C — the legacy method; three legacy hand calculations as known-answer cases, two worked examples, spec)
- [x] Yellow pass (Y8-01…Y8-06; Y8-02 and Y8-06 carried to Phases 5 and 4)
## Phase 2 — mechanical ports (`v0.27.0/phase-2-mech`)
- [ ] mech.lifting-lug (5 checks, governing)
- [ ] mech.bearing-life (ISO 281 L10 with a_ISO as inputs; covers bearing-load-capacity)
- [ ] mech.fatigue-miner
- [ ] mech.shaft-stress
- [ ] mech.thermal-expansion
- [ ] mech.thin-vessel; mech.material-margin (referenced material record)
- [ ] Legacy vectors carried as known-answer cases (same figures, 5 s.f.); two worked examples each; Yellow pass
## Phase 3 — structural and thermal ports (`v0.27.0/phase-3-struct-thermal`)
- [ ] struct.fillet-weld
- [ ] bs.plane-wall; bs.thermal-chain
- [ ] Legacy vectors carried; worked examples; Yellow pass
## Phase 4 — Desktop retirement (`v0.27.0/phase-4-desktop`)
- [ ] Legacy Calculators surface (Engineering → Modules → Calculators, Re-run/Compare) replaced by Worksheets; Compare reimplemented over calc instances
- [ ] Legacy definitions, descriptors, diagrams and their tests retired under ADR-0167 (after the migration passes)
- [ ] Desktop full suite green
## Phase 5 — reviews and gates (`v0.27.0/phase-5-reviews`)
- [ ] Green: every carried vector recomputed; Grey: upgrade from v0.25 and v0.26 databases; Black on a clean clone; Red delta on the new templates and the migration; Yellow final
- [ ] CHANGELOG, release notes, handover; rc1 tag when every gate passes
