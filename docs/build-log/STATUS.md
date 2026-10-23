# STATUS — 2026-10-23 21:10 UTC (v0.27.0)
Phase: 0/5 — setup; v0.26.0-rc1 close-out running in parallel on release/v0.26.0
Done since last: v0.26.0 Phase 7 merged (Linux gate green); legacy-calculator review and D-38 plan; release/v0.27.0 branched; VERSION 0.27.0; task list for five phases
Now: Windows CI on release/v0.26.0 @ 2523221 (rc1 tag when green); ADR-0167; v0.27.0 draft PR; Phase 1 foundations (record migration, reference-library fill, spec raise)
Next: Phase 2 mechanical ports (lifting lug, bearing life, fatigue, shaft, thermal expansion, thin vessel, material margin)
Tests: v0.26.0 gate — Core 7,221 / Desktop 1,193 / Calc 849 pass, 0 fail, 1 skipped (D-32)
Build: green (Linux)   Branch: release/v0.27.0 @ 2523221 + setup
Decisions taken: D-00…D-39 (docs/decisions/)
Risks: Windows CI on the v0.26.0 candidate not yet green; record migration must keep every stored legacy run readable (non-negotiable #3)
