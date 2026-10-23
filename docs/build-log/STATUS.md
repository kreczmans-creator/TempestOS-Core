# STATUS — 2026-10-23 12:30 UTC
Phase: 4/7 Building services — B1, B2 built on the phase branch; B3 next
Done since last: Phase 3 merged (S1 section properties, S2 frame solver + beam formulas, S3 EN 1993-1-1 checks; yellow pass); B1 pipe/pump (Colebrook, duty point, affinity, NPSH) and B2 psychrometrics (Hyland–Wexler, five processes) with tests and worked examples
Now: B3 duct sizing; then Desktop composition + PDFs for B1–B3, yellow pass, merge phase-4
Next: Phase 5 E1–E3; Phase 6 security review; Phase 7 colour teams, handover, rc1 tag
Tests: Core 7,221 + Calc 627 pass / 0 fail / 3 unconfirmed-reference (S3 UC column; B2 ASHRAE example; M-phase 2); Desktop 1,172 pass (Phase 3 gate)
Build: green (Linux)   Branch: v0.26.0/phase-4-bs (PR #31 release/v0.26.0 → main, draft)
Decisions taken: D-00…D-16 (see docs/decisions/v0.26.0-DECISIONS.md); D-17 pending (glycol as user-entered fluid)
Risks: Windows build unverified until Grey; Markdown budget watched; Blue Book/ASHRAE values from mirrors or formulas, owner to confirm (Y3-05)
