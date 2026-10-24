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
- [x] Black (clean clone; B-01…B-25; Highs + 5 Mediums fixed, ADR-0166), Yellow final
- [ ] Grey Windows CI verdict (two release runs failed; fixes on phase-7)
- [ ] Release notes, handover, STATUS gates, rc1 tag
- [x] 24 worked examples + 1 calc pack
- [x] CHANGELOG, release notes, THIRD-PARTY-NOTICES (unchanged, complete), handover
- [ ] All §10 gates; tag v0.26.0-rc1

## Resume from here (Phase 7 close-out, 2026-10-23 20:45 UTC) — exact steps, no judgement calls needed
1. Branch `v0.26.0/phase-7-reviews` at `42686a0` (pushed). Working tree clean except graphify hook cache files (`git checkout -- graphify-out/cache/hook_sessions/`; never commit them).
2. Final Linux gate logs are in the session scratchpad (`build-release3.log`, `desktop3.log`, `calc3-*.log`, `core3.log`). If any log is missing, run: `dotnet build src/TempestOS.slnx -c Release -p:TreatWarningsAsErrors=true`, then `dotnet test` on `tests/Tempest.Core.Tests`, `tests/Tempest.Desktop.Tests`, `tests/Tempest.Calc.Tests.{Engine,Sheets,Library,Calculators}`. Expected: 0 errors, 0 warnings; Core 7,221 pass; Desktop 1,193 pass; Engine 384, Sheets 93, Library 44 (+1 skipped, D-32), Calculators 328 pass.
3. If green: `git checkout release/v0.26.0 && git merge --no-ff v0.26.0/phase-7-reviews` (message "Merge branch 'v0.26.0/phase-7-reviews' into release/v0.26.0"), `git push origin release/v0.26.0`. Never touch `main`.
4. Read the CI run for the new release head (GitHub Actions, workflow `ci.yml`, branch `release/v0.26.0`, event push). Three Windows-only causes are already fixed: xUnit analyser rules (CI builds with `-p:TreatWarningsAsErrors=true`; build locally the same way), PDFs over 200 KB from the Windows default face (subsetter keeps only PDF tables, D-36), Red budgets under the slower runners (`TEMPEST_TEST_TIMEOUT_FACTOR`). A new failure: fetch the failed job's log, find `FAILED:` lines, fix on `v0.26.0/phase-7-reviews`, re-gate, repeat step 3. If PDFs are still over 200 KB on Windows only, record RR-01 as open in STATUS and the handover and do not tag.
5. If CI Gate is green: tick every §10 line in STATUS.md ("Tests: … Windows CI green on <sha>"), update PR #31's body (Phase 7 done, rc1 tagged), then `git tag -a v0.26.0-rc1 <release sha> -m "v0.26.0-rc1"` and `git push origin v0.26.0-rc1`. Never tag `v0.26.0`.
6. Append one LOG.md line and push STATUS/TASKS/LOG on `release/v0.26.0` (a docs-only commit is fine there; it is the dashboard).

## Addendum A — twelve more calculators (v0.26.0, no tag)
- [x] A1 orientation (`docs/build-log/addendum-A-orientation.md`)
- [x] A-0 L1 result linking + calc packs (`v0.26.0/addA-0-linking`); §A3 reference records and §A5 export lock (groundwork)
- [x] A-1 M5 bearing life, S5 EN 1990 combinations, B5 heat exchanger, E6 load budget, B6 sealed system; records in the form (`v0.26.0/addA-1-quick`)
- [x] A-2 M4 shaft, E5 fault protection, M6 weld group; drive train E1 → M4 → M5 → M2 (`v0.26.0/addA-2-machine`)
- [x] A-3 S4 EN 1993-1-8 bolts and welds; v0.25.2 bolt shear reconciled, ADR-0169 (`v0.26.0/addA-3-connections`)
- [ ] A-4 E4 conductor sizing, B4 U-value and room heat loss; motor-circuit pack; licensed records refuse export (`v0.26.0/addA-4-records`)
- [ ] A-5 S6 base plate (`v0.26.0/addA-5-baseplate`)
- [ ] A7 five linked chains: integration tests + issued pack worked examples
- [ ] Security + every colour team re-run on the delta (Addendum A sections)
- [ ] A8 owner test pack + handover Addendum A section
- [ ] A9 gates; STATUS final line

## Addendum B — fifteen calculators, Fluid Power, three workflows (v0.26.0, no tag; queued after Addendum A §A9, B0.2)
- [ ] B1 orientation (`docs/build-log/addendum-B-orientation.md`)
- [ ] B-0 foundations: ADR Fluid Power discipline (next free ADR); W3 material library; B6 gas-law kernel; M1 stack engine API (`v0.26.0/addB-0-foundations`)
- [ ] B-1 M7 springs, E7 PCB trace, B7 Glaser, S7 LTB + N+M, F2 accumulator (`v0.26.0/addB-1-core`)
- [ ] B-2 F1 cylinder circuit, F3 O-ring gland; HPU chain F1 → F2 → B1 → E1 → E4 (`v0.26.0/addB-2-fluid`)
- [ ] B-3 M8 snap-fits, B8 pipe expansion, S9 plate bending, E8 containment, B9 insulation (`v0.26.0/addB-3-reuse`)
- [ ] B-4 W1 CAD import, W2 scan import, M9 kinematics, E9 motor start/PFC; scan-to-seal W2 → M1 → F3 (`v0.26.0/addB-4-workflows`)
- [ ] B-5 S8 lifting lugs and spreader beams behind a feature flag (`v0.26.0/addB-5-gated`)
- [ ] §B7 ten chains: integration tests + issued pack worked examples
- [ ] Security + every colour team re-run on the Addendum B delta
- [ ] §B10 owner test pack additions
- [ ] §B9 gates; STATUS final line
