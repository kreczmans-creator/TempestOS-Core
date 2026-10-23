# STATUS — 2026-10-23 20:55 UTC (v0.27.0)
Phase: 3/5 — structural and thermal ports, built and tested; Phase 3 gate next
Done since last: Phase 1 foundations merged (record migration, reference fill, spec raise, column buckling, beam and bolted-joint raises); Phase 2 seven mechanical ports merged (gate green: Core 7,221, Calc Engine 384, Sheets 93, Library 53, Calculators 468, Desktop 1,212); Phase 3 struct.fillet-weld, bs.plane-wall, elec.thermal-chain (D-40) with legacy known answers, worked examples, PDFs, specs and behaviour tests
Now: Phase 3 Yellow pass (Y10); behaviour tests for the seven Phase 2 ports raised to the Phase 3 level; Phase 3 gate
Next: Phase 4 Desktop retirement (archive listing Y8-06, Compare over calc instances, legacy surface retired under ADR-0167)
Tests: Calculators 530/0, Library 53/0 (+1 skip), Desktop worksheets 80/0; full gate pending
Build: green (Linux, warnings as errors)   Branch: v0.27.0/phase-3-struct-thermal from release/v0.27.0 @ e7b4bcf
Decisions taken: D-00…D-40 (docs/decisions/)
Risks: v0.26.0-rc1 tag still waits on the owner (tag push refused, HTTP 403); legacy surface retirement must wait for the migration to pass Grey
