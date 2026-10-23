# v0.26.0 tasks
## Phase 0 — setup
- [x] release/v0.26.0 from main; VERSION 0.26.0
- [ ] STATUS/TASKS/LOG/DECISIONS; orientation doc; draft PR
## Phase 1 — engine, sheets, library, reports (`v0.26.0/phase-1-engine`)
- [x] ADR-0165 (transparent worksheet engine; Evidence governs)
- [x] D-01 engine spike (CalcpadCE vs own engine), 8 h box
- [x] Tempest.Calc.Engine: parser, units, dimension checks, functions, vectors/matrices, step log, caps; 150+ tests
- [x] Tempest.Calc.Sheets: sheet model (§3.4), text + PDF render, golden normalisation
- [x] Tempest.Calc.Library: CalcTemplate/CalcInstance, hashing, versioning, catalogue, known-answer registry
- [x] Issue in one transaction + crash-mid-issue test; auto-link to Calculation register; calc packs; stale marker
- [x] Reports: issued calc sheet opens from shell; page < 200 KB
- [x] Generic: tornado sweep, Monte Carlo sampler, CSV import/export
- [x] Desktop: shared CalcSheetView per §3.8a (live recalc, unit pickers, validation, provenance, governing bar, Show working, read-only issued); Ctrl+K entries per calculator → Phases 2–5
- [x] 1D stack + thick-cylinder migrated onto the engine; old tests pass (with M1/M2, D-08)
- [x] Yellow pass (Y1-01…Y1-0x in docs/reviews/yellow-team.md)
## Phase 2 — mechanical (`v0.26.0/phase-2-mech`)
- [x] M1 tolerance stack v2
- [x] M2 ISO 286 fits + press fit
- [x] M3 bolted joint + bolt group
- [x] Yellow pass (docs/reviews/yellow-team.md, Y2-01…Y2-04)
## Phase 3 — structural (`v0.26.0/phase-3-struct`)
- [x] S1 section properties
- [x] S2 beam + 2D frame solver (+ beam formula library)
- [x] S3 EN 1993-1-1 member checks
- [x] Yellow pass (Y3-01…Y3-05)
## Phase 4 — building services (`v0.26.0/phase-4-bs`)
- [x] B1 pipe pressure drop / pump duty
- [x] B2 psychrometrics
- [x] B3 duct sizing
- [x] Yellow pass (Y4-01…Y4-05)
## Phase 5 — electrical (`v0.26.0/phase-5-elec`)
- [x] E1 motor sizing
- [x] E2 enclosure heat
- [x] E3 voltage drop
- [x] Yellow pass (Y5-01…Y5-04)
## Phase 6 — security (`v0.26.0/phase-6-security`)
- [x] STRIDE + tests + analyzers + vulnerable scan; docs/reviews/security-review.md (SEC-05 High fixed: concurrent Issue guard)
- [x] Hostile-input, concurrency, performance-budget tests; pack worked example (M2+M3+S1)
- [x] Phase 6 gate (full suites: Core 7,221, Desktop 1,191, Calc 847) and merge
## Phase 7 — reviews and gates (`v0.26.0/phase-7-reviews`)
- [x] Green (219/219 recomputed; G-01…G-04 fixed), White, Orange, Blue, Purple written
- [x] Red (R-01…R-24: 23 findings, 22 fixed, R-22 accepted D-32), Blue/Purple updated, Grey upgrade test passes
- [ ] Black (after merge), Grey Windows CI verdict, Yellow final
- [ ] Release notes, handover, STATUS gates, rc1 tag
- [x] 24 worked examples + 1 calc pack
- [ ] CHANGELOG, release notes, THIRD-PARTY-NOTICES, handover
- [ ] All §10 gates; tag v0.26.0-rc1
