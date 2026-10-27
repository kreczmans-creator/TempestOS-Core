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
- [x] A-4 E4 conductor sizing, B4 U-value and room heat loss; motor-circuit pack; licensed records refuse export (`v0.26.0/addA-4-records`)
- [x] A-5 S6 base plate (`v0.26.0/addA-5-baseplate`)
- [x] A7 five linked chains: integration tests + issued pack worked examples
  - [x] A7a table-cell links; wet-heating chain; E1 continuous duty (`v0.26.0/addA-7a-cell-links`)
  - [x] A7b cooling, steel support, drive train to M3; five pack worked examples; pack dry run (`v0.26.0/addA-7b2-chains`, `v0.26.0/addA-8b-pack-dryrun`)
- [x] A8-UI Desktop: open saved calculations; packs panel (create from template, check, issue) (`v0.26.0/addA-8-ui`)
- [x] Security + every colour team re-run on the delta (Addendum A sections): Security/Red/Green/Black found 9 High; Blue rounds 1 and 2 fixed them; Purple verified; no High open (`v0.26.0/addA-9-blue`)
- [x] A8 owner test pack + handover Addendum A section (`docs/testing/v0.26.0-owner-test-pack.md`)
- [x] A9 gates; STATUS final line (copyright audit DA-25)

## Addendum B — fifteen calculators, Fluid Power, three workflows (v0.26.0, no tag; queued after Addendum A §A9, B0.2)
- [x] B1 orientation (`docs/build-log/addendum-B-orientation.md`)
- [x] B-0 foundations: ADR Fluid Power discipline (next free ADR); W3 material library; B6 gas-law kernel; M1 stack engine API (`v0.26.0/addB-0-foundations`)
- [x] B-1 M7 springs, E7 PCB trace, B7 Glaser, S7 LTB + N+M, F2 accumulator (`v0.26.0/addB-1-core`)
- [x] B-2 F1 cylinder circuit, F3 O-ring gland; HPU chain F1 → F2 → B1 → E1 → E4 (`v0.26.0/addB-2-fluid`)
- [x] B-3 M8 snap-fits, B8 pipe expansion, S9 plate bending, E8 containment, B9 insulation (`v0.26.0/addB-3-reuse`)
- [x] B-4 W1 CAD import, W2 scan import, M9 kinematics, E9 motor start/PFC; scan-to-seal W2 → M1 → F3 (`v0.26.0/addB-4-workflows`)
- [x] B-5 S8 lifting lugs and spreader beams behind a feature flag (`v0.26.0/addB-5-gated`)
- [x] §B7 ten chains: integration tests + issued pack worked examples
- [x] Security + every colour team re-run on the Addendum B delta
- [x] §B10 owner test pack additions
- [x] §B9 gates; STATUS final line

## Addendum C — test readiness, Track S (v0.26.0, no tag; queued after Addendum B §B9, C0)
Track P (parallel verification on `v0.26.0/addC-parallel`) is a separate session; this session never starts or edits it.
- [x] C4 start (on hold at the owner's request; Track P already merged into release/v0.26.0 by the integration, DI-01): Addendum B gates met; record the Track S start in DECISIONS; merge `v0.26.0/addC-parallel` (`--no-ff`); STATUS "Addendum C: n/8" — started 2026-10-26 (DC-02).
- [x] C-1 fix every Track P Critical/High with regression tests (`v0.26.0/addC-1-fixes`)
- [x] C-2 consistency audit applied; `docs/calcs/glossary.md` (phase A `v0.26.0/addC-2-consistency`; phases B and C `v0.26.0/addC-2b-*`, `addC-2c-*`, integrated on `c2-int`, merged 7dbbf5fc)
- [x] C-3 record entry tooling: records screen, CSV import from Track P templates, missing-record prompts (`v0.26.0/addC-3-records`)
- [x] C-4 verification dashboard (`v0.26.0/addC-4-dashboard`)
- [x] C-5 guided test mode (`v0.26.0/addC-5-guided`)
- [x] C-6 seeded demo project "Demo — Hydraulic Press Cell" (`v0.26.0/addC-6-demo`)
- [x] C-7 performance and stability soak (`v0.26.0/addC-7-soak`)
- [x] C-8 final owner test pack and handover (`v0.26.0/addC-8-pack`)
- [x] §C7 gates; Security + colour teams Addendum C section; STATUS final line

## Legacy calculator replacement (written on `release/v0.27.0`; ships in v0.26.0 — nothing ships as 0.27.0)
The phases below are as they stood on that branch (D-38; ADR-0167), kept unchanged except where the integration into v0.26.0 changed a fact (see the integration list at the end).

## Phase 0 — setup (`release/v0.27.0`)
- [x] Branch from release/v0.26.0; VERSION 0.27.0; CHANGELOG section; v0.27.0-DECISIONS (D-39)
- [x] ADR-0167: the legacy calculation modules are superseded by Tempest.Calc templates; records migrate; tests carried as known-answer cases
- [ ] Draft PR release/v0.27.0 → main (dashboard)
## Phase 1 — foundations (`v0.27.0/phase-1-foundations`)
- [x] Record migration: every stored `CalculationRecord` readable as a `CalcInstance` archive snapshot (`LegacyCalculationMigration`, `LegacyArchiveSheet`, `ICalculationEngine.FindRecordDocumentAsync`); lifecycle refusals on archives; Grey upgrade test over a real SQLite database with a legacy bolted-joint record
- [x] Reference-library fill: `CalcReferenceFill` (released records only; `FromReference` pin per value; library slots such as `Materials:hub`); declared on struct.en1993-member, struct.beam-formulas, struct.frame-2d, mech.press-fit (hub/shaft), mech.bolted-joint (fastener + members); one picker per library in the worksheet form
- [x] Spec raise: "In plain terms" paragraph (hand-written) + hand arithmetic of the first worked example (generated from its sheet, drift-guarded by `SpecHandArithmeticTests`) in all 16 specs
- [x] Bolted-joint joint-diagram load factors (F_A_sep, n_0, n_L) with consistency tests; golden, worked examples, PDFs, pack and spec regenerated
- [x] Beam formulas: legacy stress (M·c/I) and deflection-limit checks, deep-beam warning, four legacy hand calculations as known-answer cases (8/8)
- [x] `struct.column-buckling` (Perry–Robertson, BS 5950-1 Annex C — the legacy method; three legacy hand calculations as known-answer cases, two worked examples, spec)
- [x] Yellow pass (Y8-01…Y8-06; Y8-02 and Y8-06 carried to Phases 5 and 4)
## Phase 2 — mechanical ports (`v0.27.0/phase-2-mech`)
- [x] mech.lifting-lug (5 checks, governing)
- [x] mech.bearing-life (ISO 281 L10 with a_ISO as inputs; covers bearing-load-capacity)
- [x] mech.fatigue-miner
- [x] mech.shaft-stress
- [x] mech.thermal-expansion
- [x] mech.thin-vessel; mech.material-margin (referenced material record)
- [x] Legacy vectors carried as known-answer cases (same figures, 5 s.f.); two worked examples each; Yellow pass (Y9-01…Y9-05)
## Phase 3 — structural and thermal ports (`v0.27.0/phase-3-struct-thermal`)
- [x] struct.fillet-weld (simplified method; long-joint warning above 150·a)
- [x] bs.plane-wall; elec.thermal-chain (filed under Electrical, D-40)
- [x] Legacy vectors carried (9 hand calculations, 5 s.f.); two worked examples each; behaviour tests; Yellow pass (Y10)
- [x] Behaviour tests for the seven Phase 2 ports, to the Phase 3 level (Y10-03: 26 tests)
- [ ] Phase 3 gate; merge to release
## Phase 4 — Desktop retirement (`v0.27.0/phase-4-desktop`)
- [x] Compare reimplemented over calc instances (`CalcInstanceComparer`, Compare with previous revision); archives listed in the worksheet catalogue (Y8-06)
- [x] Grey: real v0.25.3 and v0.26.0-rc1 databases (46 records, 19 calculations) migrate with nothing lost (`GreyLegacyDatabaseTests`)
- [x] Reference diagram in the worksheet form for every template (ADR-0158, Principle 33; D-41): 27 diagrams, 60 drawings; coverage, reading and focus tests; every drawing rendered and reviewed for overlaps
- [x] Legacy Calculators surface (Engineering → Modules → Calculators) retired (ADR-0167 Amendment 1): view, coordinator, workbench, module service; tests superseded as listed there; real-shell journey moved to Worksheets
- [ ] Legacy definitions, descriptors, diagrams and their tests retired under ADR-0167 (after the migration passes)
- [ ] Desktop full suite green
## Phase 5 — reviews and gates (`v0.27.0/phase-5-reviews`)
- [x] Green: every legacy vector traced and carried at 5e-5 (G-05…G-08 fixed; D-42)
- [x] Grey: real v0.25.3 and v0.26.0-rc1 databases (Phase 4)
- [x] Black on a clean clone (B-26 found and fixed; rc1 gate green on a fresh clone)
- [x] Red delta on the new templates, migration, compare, archives and diagrams
- [x] Yellow final (Y12; Y11-04 open to the owner)
- [x] CHANGELOG, release notes, handover
- [x] rc1 gates (clean clone, Release, every suite)
- [x] Showcase journey: one worked example per discipline, screenshot per stage; findings Y13-01…09 fixed (sheet diagram D-45, AMA references D-44)
- [ ] Owner: confirm the book editions in D-44
- [ ] Draft PR to main and tag `v0.27.0-rc1` (owner)

## Integration into v0.26.0 (2026-10-25)
- [x] Addendum C Track P merged as is (the C4 merge for `docs/verification/` and `tools/verification/`; Track S has not started and C4's other items are untouched)
- [x] `release/v0.27.0` merged; VERSION 0.26.0; CHANGELOG subsection, release notes, handover and decisions folded in as marked sections (DI-01, DI-02)
- [x] `mech.bearing-life` stays M5; legacy port kept as `mech.bearing-life-basic` (DI-03); every port test re-targeted
- [x] `mech.lifting-lug` gated like S8 (DI-04); legacy surface stays retired, engine gate stays (DI-05)
- [x] Templates changed on both lines merged and moved up a minor version (DI-06); method labels and "In plain terms" brought up to D-46 for Addenda A and B (DI-07)
- [x] Generated files regenerated (golden, worked examples, PDFs, spec hand-arithmetic); worked-examples index counts updated
- [x] Full gate: Engine 465/0, Sheets 135/0, Calculators 2,076/0, FluidPower 97/0, Import 625/0, Library 474/0 (+1 skip), Core 7,235/0 (+15 skip), Desktop 1,389/0; `scripts/check-licences.py` passes
- [x] Draw the reference diagrams for the 27 Addendum A and B templates and empty `CalcDiagramsTests.WithoutDiagramYet` (Principle 33; DI-08): 27 diagrams, 100 drawings, reviewed (DC-25, `v0.26.0/di08-all`)
- [ ] Regenerate graphify-out and its zip (integrator)
- [ ] Windows CI on the merged head; the RealShell journey on Windows (Y11-04)
- [ ] Owner: confirm the book editions in D-44 (DI-03, DI-04 and DI-08 decided in DC-21)
- [x] Track S: C-1 fixes for Track P's open Critical and High defects
- [x] Refine the test suite: timing tests apart from the coverage collector, shared-state tests in their own collections, race and GUID flakes fixed at their cause, Desktop shards rebalanced (DC-28)
- [x] Find and fix what kept failing the CI runs: CRLF checkout, the Calc loop that stopped at the first failing project, the SVG probe start-up, Windows handle counting, round-off digits and glossary line ends on Windows (DC-28, DC-29); first all-green push run 693, then 695
