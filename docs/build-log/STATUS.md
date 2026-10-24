# STATUS — 2026-10-24 (Addendum A)
Addendum A: 1/13 — orientation written; next A-0 L1 result linking
Phase: 7/7 — every §10 gate passed; v0.26.0-rc1 ready at 6afead3 (tag push blocked for this session: owner step below)
Done since last: Windows CI on release/v0.26.0 @ 6afead3 green (run 40014821084: all Build & Test legs Debug + Release, governance, dependency scan); earlier red runs were the xUnit analyser rules, system-font PDF size, a Monte Carlo budget and a missing ADR Register row, all fixed; Black review fixes (ADR-0166); legacy-calculator review (D-38) → v0.27.0
§10 gates: [x] CI green on Windows (Grey) and Linux gate green  [x] 12 calculators to DoD  [x] 47 known-answer cases, 0 failing, 3 unconfirmed-reference (M1, S3, B2)  [x] security review + 9 colour-team reports, no open Critical/High  [x] issued sheets open from Reports, PDFs < 200 KB, Issue in one transaction  [x] ADR-0165/0166, DECISIONS, CHANGELOG, release notes, THIRD-PARTY-NOTICES, handover
Owner step: tag the candidate — `git tag -a v0.26.0-rc1 6afead3 -m "v0.26.0-rc1" && git push origin v0.26.0-rc1` (or GitHub → Releases → new tag v0.26.0-rc1 on 6afead3). This session's Git access refuses tag pushes (HTTP 403).
Tests: Core 7,221 / Desktop 1,193 / Calc 849 pass, 0 fail, 1 skipped (D-32); 32 worked examples (4 unconfirmed) + 1 pack
Build: green (Linux + Windows CI)   Branch: release/v0.26.0 @ 6afead3 (PR #31 → main, draft)
Next: v0.27.0 on release/v0.27.0 — legacy calculator replacement (D-38, ADR-0167); Phase 1 in progress
Risks: R-22 orphan Draft Evidence after a crash mid-Create (accepted, D-32)
