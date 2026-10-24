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
