# STATUS — 2026-10-23 22:55 UTC (v0.27.0)
Phase: 5/5 — all reviews done; rc1 gate green on a clean clone; awaiting the owner's tag
Done since last: Phase 4 merged (gate green); Phase 5 Green carried every legacy vector (G-05 bolted-joint separation fixed, G-06 bolt group, G-07 DPMO tail, D-42); Black on a clean clone (B-26 fixed); Red delta; Yellow final; CHANGELOG, release notes, handover
Now: release/v0.27.0 ready for review; tag v0.27.0-rc1 after the owner steps in docs/handover/v0.27.0-handover.md
Next: owner — tag v0.26.0-rc1 at 6afead3, run the real-shell journey on Windows (Y11-04), open the v0.27.0 PR and tag v0.27.0-rc1
Tests (rc1 gate, clean clone, Release, 0 warnings): Core 7,194/0 (+15 skip), Engine 386/0, Sheets 93/0, Library 60/0 (+1 skip), Calculators 560/0, Desktop 1,271/0
Build: green (Linux)   Branch: release/v0.27.0
Decisions taken: D-00…D-43 (docs/decisions/)
Risks: Windows CI not yet run on release/v0.27.0 (no PR open); tag pushes refused from this session (HTTP 403)
