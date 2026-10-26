# ⚪ White team — governance

| Item | Evidence | Status |
|---|---|---|
| Licence audit of every dependency | `THIRD-PARTY-NOTICES.md` lists every direct `PackageReference` with its NuGet licence expression (MIT / Apache-2.0 / BSD; no GPL, LGPL or AGPL text appears in the file). No `Tempest.Calc.*` project adds a package; the Desktop, Core, Workspace and test projects' package sets are unchanged from v0.25. `dotnet list package --vulnerable --include-transitive`: no vulnerable packages (security review §7). | Pass |
| THIRD-PARTY-NOTICES complete | Unchanged and still complete: zero new direct packages in the v0.26.0 diff (`git diff origin/main -- '**/*.csproj'` shows only `ProjectReference` additions). CalcpadCE not vendored (D-01). | Pass |
| Standards-copyright audit | Every clause reference is a `CalcClauseRef(standard, clause, title)`; a template test asserts titles stay short (`Template_Is_Well_Formed`). Tabulated standard data is not embedded: ISO 286 deviations are derived from the published formulae (M2); EN 1993-1-1 imperfection and partial factors are editable inputs with textbook provenance (D-14); IEC TR 60890 surface factors are per-face exposure inputs (D-20); BS 7671 tables are not embedded (D-21); ASHRAE/CIBSE loss coefficients are user inputs (D-19); Blue Book values appear only as known-answer expectations with their source (D-15). Green team re-audited the sources (`green-team.md`). | Pass |
| ADR completeness | ADR-0165 "Tempest computes through a transparent worksheet engine; Evidence governs everything" (supersedes half of D-028); ADR Register row added; `docs/adr` count 165 = register count. The prompt's "ADR-0164" number was taken (D-02). ADR-0165 §6 amended for the Desktop → Calc reference direction (D-12) with the architecture test updated, not deleted. | Pass |
| DECISIONS | `docs/decisions/v0.26.0-DECISIONS.md` D-00 … D-24, each with a reason. | Pass |
| CHANGELOG | Root `CHANGELOG.md` created (D-03) with the v0.26.0 entries per phase. | Pass |
| Release notes | `docs/releases/v0.26.0/Release Notes.md` (repository convention: a folder per release, which the governance health check requires) — final text written in Phase 7; the prompt's path `docs/releases/v0.26.0.md` is satisfied by that folder's notes (D-25). | Pass |
| Docs index | No root docs index file exists in the repository; the calc specs live under `docs/calcs/<discipline>/`, the worked examples under `docs/worked-examples/<discipline>/<id>/<slug>/`, the reviews under `docs/reviews/`, the handover under `docs/handover/`. The orientation file `docs/build-log/00-orientation.md` and the handover list them. | Pass |
| Branch naming / CI gate | Work on `release/v0.26.0` with `v0.26.0/phase-*` sub-branches (CLAUDE.md rule); PR #31 title starts with `v0.26.0:`. CI `Branch Name` job applies. | Pass |
| Markdown budget | The PR adds ≈ 1.4 k Markdown lines against ≈ 19.6 k code lines (CONTRIBUTING rule: Markdown ≤ code). | Pass |

Findings: none open. W-01 (Info): the Blue Book and ASHRAE example values are cited from public mirrors or recomputed from published relations (Y3-05, Y4-05); the owner should confirm against the printed sources when convenient.

## Addendum A

Checked at `v0.26.0/addA-9-blue` (`efa30499`), 75 commits on `6afead3`. Addendum A extends v0.26.0; it does not make a new release (DA-01).

| Item | Evidence | Status |
|---|---|---|
| No tag | No tag was created for Addendum A. `v0.26.0-rc1` points at `6afead3` and exists only in this clone: `git ls-remote --tags origin` lists no `v0.26` tag. It was made by an earlier session, the push was refused, and it is left alone (DA-01). | Pass |
| No release | No GitHub Release was created. `STATUS.md` says "No tag: Addendum A forbids tagging or releasing (DA-01)". | Pass |
| No merge to `main` | `origin/v0.26.0/addA-9-blue` is not an ancestor of `origin/main`. Every Addendum A branch merged into `release/v0.26.0` with `--no-ff` (12 merge commits). | Pass |
| Draft PR | PR #31, `release/v0.26.0` → `main`, is open and a draft (REST API). Title: "v0.26.0: Engineering calculation surface (Tempest.Calc.*)". | Pass |
| Version stays 0.26.0 | `VERSION` reads `0.26.0`. `CHANGELOG.md` has an "Addendum A — twelve more calculators (no tag)" subsection under v0.26.0. Seven templates that already shipped had their template versions raised (see `grey-team.md`); the product version did not move. | Pass |
| ADR-0168 and amendments 1–3 | `docs/adr/ADR-0168-calculations-link-to-a-pinned-revision-…md` has the amendment after the Opus review (one validator on every path), amendment 2 (table-cell links, 2026-10-24) and amendment 3 (blue round: draft upstreams, transitive staleness for every reason, enforced ports, no dead ends, CSV tables). Amendment 3 replaces the last paragraph of amendment 2, and says so. | Pass |
| ADR-0169 | `docs/adr/ADR-0169-bolt-shear-strengths-follow-the-en-1993-1-8-shear-factor-per-property-class.md`. Revised the same day after the Opus review (DA-17, finding 1). Scopes the v0.25.2 module as the shank-in-plane case; no earlier test is superseded. | Pass |
| ADR Register complete | `docs/adr` holds 168 ADR files and the register has 168 rows; ADR-0168 and ADR-0169 are in both. The register's last-reviewed line names both. | Pass |
| DECISIONS | `docs/decisions/v0.26.0-DECISIONS.md` has DA-01 … DA-23, in order and with no gaps, each with its reason. DA-23 records the Blue round 1 and what it did not fix. | Pass |
| Licence: no new dependency | `git diff 6afead3..HEAD` over `*.csproj` shows three lines added, all project references: `Tempest.Calc.Mechanical` → `Tempest.Calc.Structural` for M4 (DA-13), and two in the Library test project (Electrical and BuildingServices, for the chain tests). No `PackageReference` changed. `THIRD-PARTY-NOTICES.md` (MIT / BSD / Apache-2.0 only) is still complete. | Pass |
| Licence: no copyleft code | OpenHeatLoss (AGPL v3) is not named in `src/`. No code was read or ported from it. B4 is written from first principles, and it agrees with `calc.plane-wall-heat-transfer` to 1e-9 (`RoomHeatLossAgreementTests`). The owner test pack may name OpenHeatLoss as a black-box comparison only. | Pass |
| §A5: no licensed tables shipped | The ampacity and device-rating records ship empty (DA-19). E4 tests, golden and examples use invented values (G-B-15 and G-B-16 fixed). BS 7671 Appendix 4 appears in `src/` only as a clause reference and a "formula only" label. Tabulated data is a user record with a source, edition and licence flag. A spot check of `src/Tempest.Calc` for `Table 4A`–`4E` and `Appendix 4` found those labels and no values. The full manual audit that §A9 asks for is still owed. | Pass (spot check); audit owed |
| §A5: licensed records stay out of exports | The CSV export withholds licensed values (DA-06). Since Blue round 1 every sheet, including the issued pack sheet, is built by one path that withholds licensed table rows (R-A-01). Purple confirmed that no licensed capacity appears on the pack sheet or on E4's own sheet. Gap: P-A-01 (a withheld scalar re-imports as the template default) and the retyping limit (DA-18). | Pass with two open items |
| Standards cited by clause only | Each calculator cites `CalcClauseRef(standard, clause, title)`. Clause numbers added in Addendum A (S6, B4, E4, E5) are flagged "to be checked" by the owner in DA-19 and DA-20. | Pass |
| Branch naming | All 15 Addendum A branches are `v0.26.0/addA-…`. Other branches on origin are `claude/…` cloud branches, `dependabot/…` or `release/v…`. The draft PR title starts with `v0.26.0:`. The stale `v0.26.0/addA-7b-chains` (superseded by `addA-7b2-chains`) is still on origin (Y-A-03). | Pass; one stale branch |
| CHANGELOG | The Addendum A subsection lists every calculator, ADR-0168 linking, cell links, packs, the bolt shear scope (ADR-0169) and the panels. It does not yet record the Blue round 1 changes (licence lock on sheets, drafts, independence, CSV tables, new-sheet defaults). | Partly: update after Blue round 2 |
| Markdown budget | Since `6afead3`: 4,755 Markdown lines added against 17,222 C# lines added. These six reviews add more Markdown and stay far below the code. | Pass |
| Colour-team reports | Security, Red, Green, Black and Purple carry an Addendum A section. Blue, Yellow, White, Grey and Orange are added in this change. | Pass once merged |
| §A9 final gates | Open: Blue round 2 (P-A-01 to P-A-06 and the Partly items), the A8 owner test pack, the handover section, the full test gate after round 2, and the `STATUS.md` final line "READY FOR OWNER TESTING — not released, not tagged." No Critical or High finding is open. | Open |

Findings:

- W-A-01 (Low, Open): the local `v0.26.0-rc1` tag at `6afead3` predates Addendum A. It must not be pushed. The owner should delete or move it, since the release will contain Addendum A.
- W-A-02 (Low, Open): `v0.26.0/addA-7b-chains` is stale on origin. The owner should delete it (Y-A-03).
- W-A-03 (Info, Open): ADR-0167 is not in this branch (it belongs to v0.27.0), so the ADR numbers on `release/v0.26.0` skip it. The forward merge of `release/v0.26.0` into `release/v0.27.0` (DA-02) must keep both registers.
- W-A-04 (Info, Open): clause numbers flagged "to be checked" in DA-19 and DA-20, and the Unconfirmed known answers listed in `STATUS.md`, need the owner's copies of the standards (DA-11).

## Addendum B

Checked at `v0.26.0/addB-4-workflows` (`3e4ca602`), the delta `a06e2ec2..3e4ca602` (469 files under `src/`, `tests/`, `docs/`), and the S8 work at `065fe64c` before its merge. Addendum B extends v0.26.0; it does not make a new release.

| Item | Evidence | Status |
|---|---|---|
| §B5: no prohibited table data | Grep audit and manual review of every hit, recorded in the White review notes. Families searched by name and by tell-tale numbers: EN 10270 Rm(d), EN 13906 diagrams, IPC-2152, IET factors, NA values and C1/C2, ISO 13788 climate and humidity classes, BS 5422 rows, B31.3 Sa and K1, BTH-1 and EN 13155, AS568 and ISO 3601, extrusion charts, ISO/TS 13725. No table from any of them is in source, tests or docs. | Pass |
| §B5: what ships is formulae | IPC-2221 k and exponents are the `pcb.method` record with provenance. Magnus, δ0, the gas-law exponent, the capstan equation, Freudenstein and the Navier series are computed. C₁ comes from the ψ closed form; there is no C₁ table. The EN 13906 and BTH-1 relations are written in own notation and cited by clause. | Pass |
| §B5: licensed values are user records | Rm(d), permissible fractions, NA factors, C₁/C₂, climate, humidity class, Sa, K1, ring sizes, extrusion limits, space factor, line limits and the S8 factors are inputs with a record kind. Each prohibited one has no default; the sheet blocks or does not pass without it. | Pass |
| §B5: test and example data | Climate, extrusion points, Sa, K1, space factors and S8 factors in tests and worked examples are labelled invented or illustrative. Small factor sets (α, β, λ̄_LT,0, γ_M1, R_si,f, f_Δp, end-type factors) are editable records (D-14). | Pass |
| §B5: no standards text | No "shall" in the delta or in S8. Standards are cited by clause number and title. | Pass |
| W3: export redaction | A licensed material is never exported and cannot be re-imported. Values applied from it are withheld from CSV and JSON, through links too. Sheets withhold licensed table rows. Tests: `Licensed_Values_Never_Leave_Through_A_Material_Export_And_Cannot_Be_Re_Imported`, `Values_Applied_From_A_Licensed_Material_Are_Withheld_From_Exports_And_Issued_Sheets`. Seeds are four generic materials, flagged indicative. | Pass |
| Licence: no new dependency | `*.csproj` gains project references and, in the two new test projects, the test packages already listed. No new package ID. `THIRD-PARTY-NOTICES.md` is still complete. W1 reads xlsx in-house; PdfPig was not added. | Pass |
| §B5: licence check in CI | `scripts/check-licences.py` and the required Licence Check job (0cf936cc). `Svg.Custom` (MS-PL, since v0.23.0) is a flagged exception for the owner (DB-08). | Pass |
| Owner pack: licensed records to populate | Owner test pack §5.4 maps each licensed item to its record kind (6ae57e87). | Pass |

Findings:

- W-B-01 (Medium, Fixed in 0cf936cc; DB-08): no licence check in CI, which §B5 asks for. Add a pinned `nuget-license` job with an MIT / Apache-2.0 / BSD allow-list to `ci.yml` and to the gate.
- W-B-02 (Low, Fixed in f8d675a2: wording corrected, defaults kept): M7 says its permissible fractions have no default, but `f_tau`, `f_sigma` and `f_su` default to 0.45, 0.75 and 0.67. B9 says no `h_se` value ships, but it defaults to 10, and `q_max_m` to 25 W/m. They are generic values, not table data. Remove the defaults or correct the text.
- W-B-03 (Low, Fixed in 6ae57e87): the owner test pack lacks the Addendum B licensed records to populate (§B10 item 4).
- W-B-04 (Info, Open): Secondary sources (Shigley 10-3, PDHonline S106, Timoshenko α, the Parker relation and stretch rule, EN 13906 q and S_a) need the owner's printed copies.
- W-B-05 (Info, Open): label every example value "invented", so one grep can confirm them.
- S8 merged after this audit must be grepped again before the gate is ticked.

## Addendum C

Checked at `v0.26.0/addC-reviews-colour` on `1474cd0a` (`release/v0.26.0` after the Track S integration), the delta `76935116..1474cd0a`: C-1 calculation fixes, C-3 record import (ADR-0174), C-4 verification dashboard, C-5 guided test mode (ADR-0175) and C-6 demo installer (ADR-0176). Each feature had an Opus review whose findings were fixed before its merge (ADR review sections, `LOG.md`); those are not reported again. Security and Red report separately.

| Item | Evidence | Status |
|---|---|---|
| §A5/§B5: no prohibited table data in the C-3 templates | The 82 Track P files in `docs/verification/record-templates/*.csv` are header-only: a loop over them found no file with a second line, and `RecordTemplateContractTests.The_Template_Files_Hold_A_Header_And_No_Data_Rows` holds it. `RecordTemplates.cs` holds the header rows, one note per template, the check rules and generic warning and hard ranges (for example `theta_a` −100 to 500 °C). A grep of `Library/RecordImport/`, `Library/Dashboard/`, `Library/ManualTesting/`, `CalcDemoProject*.cs` and the new Desktop files for tell-tale table numbers (cable resistances 12.1, 7.41, 4.61, 3.08, 1.83; k 115, 143, 176; Z_s 1.37, 2.73; γ 1.35, ξ 0.925 and 0.85, ψ 0.94 and 0.87) found only `Opacity = 0.85` in view code. | Pass |
| §A5: the import ships no record and adds no way out | Nothing is seeded by C-3. An imported record is `licensed_source` by default. The importer's public surface has no export method, and a licensed import is withheld from a calculation's export (`RecordImportTests.An_Imported_Record_Is_Licensed_By_Default_And_Its_Values_Never_Leave_Through_An_Export`, `A_Record_Imported_As_Unlicensed_Exports_As_Before_And_The_Import_Adds_No_Export_Path`, `A_Licensed_Material_Added_To_By_An_Import_Never_Exports_In_Any_Revision`). The only file the import writes is a template's header row (`BlankTemplateCsv`). | Pass |
| §C6 C-6: the demo seed is invented | The demo uses the pack templates' own example inputs, which were audited in Addenda A and B. It also uses the calculator's synthetic cable tables (`ConductorSizingCalculator.SyntheticAmpacity`: 1.5 → 12.3 … 10 → 44.6 A, labelled "synthetic test values invented for these tests"), re-tagged "DEMO SYNTHETIC … not a regulation or manufacturer table" and given as plain inputs, so no reference record is stored. The 30-part scan table is generated from a fixed seed (`CalcDemoProject.SealScanCsv`). The one published figure set is the UC 203×203×46 section: five product dimensions (h 203.2, b 203.6, t_w 7.2, t_f 11, r 10.2 mm), cited as "published … (indicative)" (DC-13). `DemoProjectTests.No_Licensed_Record_Is_Referenced_And_No_Record_Is_Stored_Beyond_The_Indicative_Or_Synthetic` holds it. | Pass |
| §C6 C-1: defaults and new tables | C-1 adds no tabulated data. A scan of the C-1 diff for lines with six or more decimals found only method text and known-answer inputs. The new or changed defaults are generic textbook values: E 207 GPa, ν 0.3, K 0.2, μ_G and μ_K 0.12, ν_perm 1.0, L_add 100 mm, f_rg 1. Two defaults are recalled from a licensed cable table: `k_R` 1.125 (E5) and `rho_20` 0.0194 Ω·mm²/m (E3), both recorded for the owner in DC-05 (W-C-02). The new S4 hole factors (0.8 oversize, 0.6 slotted) are coefficients of the clause formula, like k₁'s 2.8 and 1.7 already in S4, and are cited as "EN 1993-1-8 cl. 3.6.1, Table 3.4 hole factor". | Pass; one item for the owner |
| Embedded data | `Dashboard/verification-snapshot.json` (63 KB) carries ids, severities and status words only. There is no defect text and no "recalled" or "Table". `ManualTesting/guided-test-script.json` (0.5 MB) carries the worked examples' inputs and expected values, which were audited with the examples. The hits for "NA." are provenance citations such as "UK NA to BS EN 1993-1-1, NA.2.15"; the one hit for "shall" is "shallows". | Pass |
| Standards cited by clause only | The new known-answer references cite a clause number and title ("EN 1993-1-8 cl. 3.6.1 single-lap limit"). No standards text was added. | Pass |
| Licence: no new dependency | `git diff 76935116..HEAD -- '*.csproj'` adds two `EmbeddedResource` items to `Tempest.Calc.Library.csproj` and no `PackageReference`. `THIRD-PARTY-NOTICES.md` is unchanged and complete. | Pass |
| ADRs and register | ADR-0174, ADR-0175 and ADR-0176 are in `docs/adr/` and Accepted. The ADR Register had no rows for them, so the governance health check (Checks 1 and 14, required by the CI Gate) would fail on this release: 176 files against 173 rows (W-C-01). Fixed in this change: 176 files, 176 rows, no gaps and no orphans (re-derived by script). | Fixed |
| DECISIONS | DC-01 … DC-14 are in order with no gaps, each with its reason. DC-03, DC-05, DC-07, DC-08 and DC-09 list the C-1 items that need the owner's licensed copy. | Pass |
| CHANGELOG | Has a C-3 entry. It has no entry for C-1, C-4, C-5 or C-6 (W-C-03). | Partly |
| Branch naming | `v0.26.0/addC-1-fixes`, `-2-consistency`, `-3-records`, `-4-dashboard`, `-5-guided`, `-6-demo`, `-7-soak` on origin, and this branch, `v0.26.0/addC-reviews-colour`. | Pass |
| Markdown budget | Since `76935116`: 1,109 Markdown lines added against 26,765 lines of C#, Python and PowerShell. | Pass |

Findings:

- W-C-01 (High, Fixed in this change): ADR-0174 to ADR-0176 had no ADR Register rows. The required Governance Health Check fails on 176 files against 173 rows. The three rows were added and the "Last Reviewed" line was updated.
- W-C-02 (Low, Open, owner, DC-05): `k_R` 1.125 and the E3 `rho_20` default of 0.0194 Ω·mm²/m are "the cable standard's maximum conductor resistance as recalled". They reproduce no row of a table, but the input text describes them as the standard's own figure. Proposed fix: confirm both against the owner's copy, or reword them as a stated conservative envelope ("about 12 % above pure copper") that cites no table.
- W-C-03 (Low, Open): CHANGELOG has no entry for C-1, C-4, C-5 or C-6. Proposed fix: one line each under v0.26.0, Addendum C, in the C-3 form.
- W-C-04 (Info): Addendum B's W-B-04 and W-B-05 still apply to the C-1 known answers. The seven new C-1 cases are analytic, and Green recomputed all seven independently (`green-team.md`, Addendum C).
