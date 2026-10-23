# STATUS — 2026-10-23 20:05 UTC
Phase: 7/7 — Black review answered; final gate running; then merge, Windows CI, rc1
Done since last: Phase 6 merged; Red (23 findings → 22 fixed, R-22 accepted); Black black-box review on a clean clone (4 High, 7 Medium → 4 High + 5 Medium fixed under ADR-0166, 2 justified, Lows deferred); Windows CI failures diagnosed and fixed (xUnit analysers, 285 KB system-font PDFs → subsetter keeps PDF tables only, tolerance-stack Monte Carlo keys, Red budgets × CI factor); goldens + 32 worked examples + pack regenerated; README/worked-example index/release notes/handover updated; graph refreshed
Now: Linux gate green (Release build 0 warnings; Core 7,221 / Desktop 1,193 / Calc 849 pass, 0 fail, 1 skipped D-32) → phase-7 merged into release → Windows CI running
Next: Grey Windows verdict; §10 checklist; tag v0.26.0-rc1 only if every gate passes
Tests: Core 7,221 pass / 0 fail; Desktop 1,193 pass / 0 fail; Calc Engine 384, Sheets 93, Library 44 (+1 skipped D-32), Calculators 328 pass / 0 fail; 3 unconfirmed-reference known-answer cases (M1, S3, B2)
Build: green (Linux, Debug + Release, warnings as errors)   Branch: v0.26.0/phase-7-reviews (PR #31 release/v0.26.0 → main, draft)
Decisions taken: D-00…D-38; ADR-0165, ADR-0166; legacy-calculator replacement planned for v0.27.0 (D-38)
Risks: Windows CI not yet green on release (two runs failed on the fixed issues); R-22 orphan Draft Evidence after a crash mid-Create (accepted)
