# v0.26.0 tasks
## Phase 0 — setup
- [x] release/v0.26.0 from main; VERSION 0.26.0
- [ ] STATUS/TASKS/LOG/DECISIONS; orientation doc; draft PR
## Phase 1 — engine, sheets, library, reports (`v0.26.0/phase-1-engine`)
- [ ] ADR-0165 (transparent worksheet engine; Evidence governs)
- [ ] D-01 engine spike (CalcpadCE vs own engine), 8 h box
- [ ] Tempest.Calc.Engine: parser, units, dimension checks, functions, vectors/matrices, step log, caps; 150+ tests
- [ ] Tempest.Calc.Sheets: sheet model (§3.4), text + PDF render, golden normalisation
- [ ] Tempest.Calc.Library: CalcTemplate/CalcInstance, hashing, versioning, catalogue, known-answer registry
- [ ] Issue in one transaction + crash-mid-issue test; auto-link to Calculation register; calc packs; stale marker
- [ ] Reports: issued calc sheet opens from shell; page < 200 KB
- [ ] Generic: tornado sweep, Monte Carlo sampler, CSV import/export
- [ ] Desktop: shared CalcSheetView per §3.8a (live recalc, unit pickers, validation, provenance, governing bar, Ctrl+K, Show working, read-only issued)
- [ ] 1D stack + thick-cylinder migrated onto the engine; old tests pass
- [ ] Yellow pass
## Phase 2 — mechanical (`v0.26.0/phase-2-mech`)
- [ ] M1 tolerance stack v2
- [ ] M2 ISO 286 fits + press fit
- [ ] M3 bolted joint + bolt group
- [ ] Yellow pass
## Phase 3 — structural (`v0.26.0/phase-3-struct`)
- [ ] S1 section properties
- [ ] S2 beam + 2D frame solver
- [ ] S3 EN 1993-1-1 member checks
- [ ] Yellow pass
## Phase 4 — building services (`v0.26.0/phase-4-bs`)
- [ ] B1 pipe pressure drop / pump duty
- [ ] B2 psychrometrics
- [ ] B3 duct sizing
- [ ] Yellow pass
## Phase 5 — electrical (`v0.26.0/phase-5-elec`)
- [ ] E1 motor sizing
- [ ] E2 enclosure heat
- [ ] E3 voltage drop
- [ ] Yellow pass
## Phase 6 — security (`v0.26.0/phase-6-security`)
- [ ] STRIDE + tests + analyzers + vulnerable scan; docs/reviews/security-review.md
## Phase 7 — reviews and gates (`v0.26.0/phase-7-reviews`)
- [ ] Red, Blue, Purple, Green, Yellow, Black, Grey, White, Orange reports
- [ ] 24 worked examples + 1 calc pack
- [ ] CHANGELOG, release notes, THIRD-PARTY-NOTICES, handover
- [ ] All §10 gates; tag v0.26.0-rc1
