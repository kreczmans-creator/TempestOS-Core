# STATUS — 2026-10-24 (Addendum A)
Addendum A: 5/13 — A-0 linking + records, A-1 (M5, S5, B5, E6, B6), A-2 (M4, E5, M6, drive train), A-3 (S4; bolt shear ADR-0169) merged; next A-4 (E4, B4; motor-circuit pack; export lock tests)
Addendum B: 0/19 — queued after Addendum A (B0.2); tasks listed
Addendum C (Track S): 0/8 — queued after Addendum B (C0); Track P runs in a separate session
Unconfirmed (Addendum A, DA-11): M5 `ka-skf-6206` (ratings via a distributor listing); S5 `ka-designers-guide`, worked example `office-floor-beam-uk-na` (UK NA values); B5 `ka-incropera-11-1`; E6 `ka-peukert` (Linden), `ka-psu-derating` (Phoenix Contact curve); B6 `ka-manufacturer-dt30`; E5 `ka-wiring-matters-b32`, `ka-schneider-k`; M4 `ka-shigley-7-1`; S4 `ka-p363-m20-88`, `ka-p358-fin-plate`. Not pinned (owner copies needed): NTN Bearing Wizard, Shigley Ch. 11, IStructE EC3 manual, Çengel & Ghajar Ch. 11, CIBSE Guide B1, vessel-manufacturer example, Mott shaft examples, Shigley Ch. 9 / Blodgett weld examples
Phase: 7/7 — every §10 gate passed at 6afead3; Addendum A extends v0.26.0 (no tag, DA-01)
Done since last: Windows CI on release/v0.26.0 @ 6afead3 green (run 40014821084: all Build & Test legs Debug + Release, governance, dependency scan); earlier red runs were the xUnit analyser rules, system-font PDF size, a Monte Carlo budget and a missing ADR Register row, all fixed; Black review fixes (ADR-0166); legacy-calculator review (D-38) → v0.27.0
§10 gates: [x] CI green on Windows (Grey) and Linux gate green  [x] 12 calculators to DoD  [x] 47 known-answer cases, 0 failing, 3 unconfirmed-reference (M1, S3, B2)  [x] security review + 9 colour-team reports, no open Critical/High  [x] issued sheets open from Reports, PDFs < 200 KB, Issue in one transaction  [x] ADR-0165/0166, DECISIONS, CHANGELOG, release notes, THIRD-PARTY-NOTICES, handover
No tag: Addendum A forbids tagging or releasing (DA-01); the local rc1 tag from an earlier session is left alone.
Tests: Core 7,221 / Desktop 1,193 / Calc 849 pass, 0 fail, 1 skipped (D-32); 32 worked examples (4 unconfirmed) + 1 pack
Build: green (Linux + Windows CI)   Branch: release/v0.26.0 @ 6afead3 (PR #31 → main, draft)
Next: v0.27.0 on release/v0.27.0 — legacy calculator replacement (D-38, ADR-0167); Phase 1 in progress
Risks: R-22 orphan Draft Evidence after a crash mid-Create (accepted, D-32)
