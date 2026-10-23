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
