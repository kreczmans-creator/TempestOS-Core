# STATUS — 2026-10-23 16:40 UTC
Phase: 6→7 — security review done; colour teams in progress
Done since last: Phase 5 merged (E1–E3); 12/12 calculators built; security review (SEC-05 High: racing Issues both succeeded — fixed in Core and Library with regression tests); Green team recomputed 219/219 expected values independently, G-01 (duct friction) and G-02…G-04 fixed; hostile-input, concurrency and performance tests; pack worked example; Windows CI fixes (LF sheets, Body-font subsetting)
Now: Red team (fresh subagent) attacking the build; Grey upgrade test over a real SQLite file; Phase 6 gate then merge
Next: Black team on a clean clone; Yellow final; release notes; handover; §10 gates; rc1 tag
Tests: Core 7,221 + Calc 669 pass / 0 fail / 4 unconfirmed-reference; Desktop 1,184 pass (Phase 5 gate); Windows CI: two failures on release (CRLF sheet assertion, PDFs > 200 KB) fixed on phase-6, awaiting the next release push
Build: green (Linux)   Branch: v0.26.0/phase-6-security (PR #31 release/v0.26.0 → main, draft)
Decisions taken: D-00…D-28 (see docs/decisions/v0.26.0-DECISIONS.md)
Risks: Windows PDF size depends on DirectWrite accepting subset fonts (RR-01); reference values from mirrors (RR-04)
