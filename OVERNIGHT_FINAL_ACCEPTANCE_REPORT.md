# TempestOS — Overnight Final Acceptance Report (v0.21.0 → v1.0.0 release candidate)

**Campaign:** 2026-09-15 23:11 UTC → 2026-09-16 «END_UTC» UTC, on `claude/tempestos-v1-final-acceptance-19hka8`, by the lead engineering agent with four package agents (docking, real shell, documentation, quality) in their own worktrees.
**Last code commit:** `37264671`. **Head:** the commit carrying this report (`git log -1`).
**Companion for the Product Owner:** `PRODUCT_OWNER_ACCEPTANCE.md` (the morning test pack). This report is the engineering record behind it.

## 1. Executive summary

The candidate `release/v0.21.0` @ `a4ab1915` was taken through every remaining technically actionable item and left as a **v1.0 release candidate ready for Product Owner acceptance**. Eight packages of work landed behind their own gates: the two unmerged branches, a Linux secrets-mode fix, the Xero authorisation path (`WP 21.6P`, three defects that made the owed live authorisation impossible from the product), docking's keyboard closure and real-screen checks (`WP 21.0K`, one critical pre-existing defect fixed), a real-process real-input acceptance journey on Linux/Xvfb (`WP 21.5C`, one major defect fixed, four reported, three of those fixed the same night), documentation alignment and release-quality evidence (`WP 21.9.1`), and the release documents with the backlog audited row by row. Nothing deliberately deferred was reopened: docking steps 3–4 stay gated on the Product Owner's `ADR-0153` review; the live Xero sign-in, the first `Setup.exe` and the two-monitor checks stay with the Product Owner. The gate on the last code commit: both builds 0/0 with warnings as errors, Core 5,314 of 5,324 in both configurations (the ten failures Windows-only tests, green on the Windows runners), Desktop «DESKTOP_SHORT», governance 5/5, dependencies clean, CI green on every pushed head up to `f7e2af66` and «CI_FINAL» on the final head. **This is a technical readiness statement. Acceptance is the Product Owner's.**

## 2. Starting state (2026-09-15 23:11 UTC, before any change)

| Item | State found | Verified how |
|---|---|---|
| Designated branch | `claude/tempestos-v1-final-acceptance-19hka8` existed at `main` (`36802578`, the `v0.18.0` merge) — 468 commits behind the real candidate. Reset to `origin/release/v0.21.0` @ `a4ab1915` before any work. | `git log`, `git rev-list --count` |
| Candidate | `release/v0.21.0` @ `a4ab1915` = `b033651d` (the last code commit, the second `WP 21.7C` follow-up merge) + `WP 21.9.0`'s release documents. `VERSION` 0.21.0. | `git`, `VERSION` |
| Release lines | `release/v0.19.0`, `v0.19.1`, `v0.20.0` all contained in `v0.21.0`; `main` = `v0.18.0` (released 2026-09-14). | `git rev-list --left-right` |
| Unmerged work | `claude/ci-failures-investigation-stohyz` (1 commit: `cancel-in-progress` off for `release/*`); `claude/academy-docs-review-completion-iqzgwv` (11 docs commits on the `v0.18.0` base, no file overlap with the tranche). No open pull requests. | `git`, GitHub API |
| Outstanding `v0.21` packages | `WP 21.0B`/`21.0C` (docking steps 3–4, gated on the Product Owner's `ADR-0153` review — the ADR is *Proposed*), `WP 21.5C` (the real-shell CI run, gated on `21.0C`), `WP 21.6` (the first live Xero authorisation, gated on the Product Owner). | `Execution Plan.md` §5 |
| CI on the candidate | Run 430 (push) green; run 431 (dispatch) cancelled by 433; run 433 (dispatch) in progress, later green in every gate job. | GitHub Actions |
| Build (this container, .NET SDK 10.0.401 installed tonight) | Debug and Release: 0 warnings, 0 errors, `TreatWarningsAsErrors`. | `dotnet build` |
| Core tests (Linux) | 5,317 total: 5,306 passed, 11 failed — 10 Windows-only (6 spawn `powershell`, 4 DPAPI), 1 genuine Linux finding (`FileSecretStore` left a pre-existing secrets directory `0755`). | `dotnet test` |
| Desktop tests (Linux) | 884 total: 883 passed, 1 failed (`StatusBarCollapseTests…`, deterministic on Linux, green on Windows CI). | `dotnet test` |
| Real application | Launches under Xvfb; `Host -> Running.` and `Desktop -> Composed.`; Home renders. | `xvfb-run`, screenshot |
| Backlog | 10 live rows of 30; `TD-182` listed live while its own row said closed; `TD-78` contradicted by the shipped design system; `TD-42` contradicted by the script. | `BACKLOG.md` read against the tree |
| Documentation drift | `VISION.md` "zero Engineering Modules" in the present tense; `README.md` Avalonia 11.2.3; roadmap last reviewed 2026-09-04; Academy landing said `v0.21.0` "held a plan and no code". | `WP 21.9.1` |
| Xero path (found in reconnaissance, before any change) | **Authorise never authorised**: `OAuthAuthoriser.AuthoriseAsync` had no caller in `src/`; Settings stored the client id under `Invoicing:ClientId`, the authoriser reads `Invoicing:Xero:ClientId`; the connector chosen in Settings never reached the host. | code reading, `grep` |



## 3. Work completed

| Package | Commit | What changed | Proven by |
|---|---|---|---|
| Merge `claude/ci-failures-investigation-stohyz` | `3d256792` | `cancel-in-progress` off for `release/*` | CI runs 435/436 not cancelling each other on the release-style cadence |
| Merge `claude/academy-docs-review-completion-iqzgwv` | `fb0c8f8a` | Academy chapters 42–64, glossary, index | link check 166/0 (`WP 21.9.1`) |
| `FileSecretStore` | `69668268` | `SetUnixFileMode` after create/write | `FileSecretStoreTests` 5/5 on Linux |
| `WP 21.6P` | `709d01a4` | `IAuthorisableConnector`; provider-keyed credentials with legacy migration; host honours the Settings choice; Authorise runs the sign-in; restart wording; browser-open guard | 13 tests; real app `evidence/xero/` |
| `WP 21.9.1` quality | `2a2d1a33` | evidence report, 6 screenshots | real app `evidence/quality/` |
| `WP 21.9.1` docs | `aa91273c` | VISION/README/roadmap/register/Academy | drift table (21 rows), link check |
| `WP 21.0K` | `5c170f34` | decision 8 keys; gestures through the controller; visible focus; `AdoptSecondaryWindowSubtree` | 8 tests + 1 rebuilt; real app `evidence/docking/` |
| `WP 21.5C` (Linux) | `3ba0709f` | runner project, script, CI advisory step; overlay z-order fix | 35+8 steps, two clean runs; `NestedOverlayZOrderTests` |
| Three journey defects | `37264671` | Add a rate card; Accept refreshes sibling tabs; Home six buckets | 4 tests + assertions; real app `evidence/fixes/` |
| Release documents | the later commits | backlog audit, `PHYSICAL_REVIEW.md` §7j/§7k/C2/R3, `D-028` addendum, release notes, Execution Plan §6, `PROJECT_STATUS.md`, the two acceptance documents | governance 5/5 |

## 4. Work not required / already complete

| Listed as outstanding | Finding | Proof |
|---|---|---|
| `TD-42` (`new-release.ps1` never checks `$LASTEXITCODE`) | Already fixed: checked after `git tag`, `git push origin main`, `git push origin v<Version>`, each throw naming `TD-42`. Row closed on inspection. | `scripts/new-release.ps1` lines 203–232 |
| `TD-78` (design system absent from the Desktop) | Shipped by `WP 20.10G`/`WP 21.2A`: `Theming/DesignTokens.cs`, `BrandPalette.cs`, `TempestTheme.cs`, embedded Inter/Chakra Petch/Space Mono. Row closed. | tree; every screenshot tonight |
| `TD-182` | Its own row already said "Closed by `WP 20.10G`". Moved out of the live table. | `BACKLOG.md` |
| Docking steps 1–2 (`WP 21.0A`) | Present and working on a real screen (K1/K2/K3/K6) — after `WP 21.0K` fixed the floating-window close defect that `WP 20.10D` introduced and `21.0A` carried. | `evidence/docking/` |
| Docking steps 3–4 (`WP 21.0B`/`21.0C`) | **Not built, deliberately.** Gated on the Product Owner's review of `ADR-0153` (status *Proposed*); building six days of shell-level docking unreviewed overnight would be exactly the over-engineering the brief forbids. Only decision 8 (two key bindings the ADR specifies) and decision 7's visible-focus refinement were delivered. | `ADR-0153` status |
| Governance health check | 5 of 5 on every head tonight. | `pwsh scripts/governance-healthcheck.ps1` |
| Dependency vulnerability scan | All eight projects "no vulnerable packages"; CI's `dependency-scan` green. | `dotnet list package --vulnerable --include-transitive` |
| Actions pinning, Dependabot, third-party notices | 19 of 19 `uses:` pinned to a full SHA; `dependabot.yml` covers NuGet and Actions weekly; six notices present. | `WP21.9.1 Release Quality Evidence.md` |
| Backup/restore, restart persistence | Verified in the real application (the OS picker works under Xvfb+GTK); `sqlite3 integrity_check ok`; restore refused with a project open; a project survives close and relaunch. | `WP21.9.1 Release Quality Evidence.md`, `evidence/quality/` |


## 5. Defects discovered (eleven)

| # | Where found | Defect | Severity | Outcome |
|---|---|---|---|---|
| 1 | Core tests on Linux | `FileSecretStore` left a pre-existing secrets directory `0755` | Low (Linux/macOS only) | Fixed `69668268` |
| 2 | Code reading (`WP 21.6P`) | Authorise never ran the sign-in (`AuthoriseAsync` had no caller) | **Blocking for `WP 21.6`** | Fixed `709d01a4` |
| 3 | Code reading | Client id stored under `Invoicing:ClientId`, read from `Invoicing:Xero:ClientId` | **Blocking** | Fixed `709d01a4` |
| 4 | Code reading | Connector chosen in Settings never reached the host (configuration only) | **Blocking** | Fixed `709d01a4` |
| 5 | Real app (Xero drive) | With the Fake running and Xero chosen, the status read "Authorised." | Major (misleading) | Fixed `709d01a4` (in the same package, before commit) |
| 6 | Real app (docking K2) | Closing one tab in a floating window discarded every other window's panels | **Critical** (data on screen lost; pre-existing since `WP 20.10D`) | Fixed `5c170f34` |
| 7 | Real app (docking K3) | Keyboard gestures never reached the controller; focus restore not visible | Medium | Fixed `5c170f34` |
| 8 | Real app (journey) | Organisation picker opened underneath the New Project prompt | Major | Fixed `3ba0709f` |
| 9 | Real app (journey) | No way to create a rate card → no priced time, no invoice on a clean install | **Blocking for the value chain** | Fixed `37264671` |
| 10 | Real app (journey) | Accept left Requirements/Deliverables tabs empty until reopen | Medium | Fixed `37264671` |
| 11 | Real app (journey) | Home counted four of six health buckets ("0 open project(s)") | Medium | Fixed `37264671` |
| — | Real app (journey) | Raw GUIDs in three places; a literal "(`TD-180`)" in a heading; no per-line VAT on the Quote tab | Low / question | Filed `TD-186`–`TD-188` |
| — | Runner | One `SIGABRT` on exit in 1 of 9 runs, after orderly shutdown | Unknown | Recorded (Release Notes) |
| — | Linux only | `StatusBarCollapseTests…` diverges under the Linux headless host | n/a (Windows green) | Recorded (Release Notes) |

## 6. Defects fixed

Eleven of eleven that were fixable tonight (rows 1–11 above), each with a regression test that fails on the previous code where a headless proof was possible, and each user-facing one re-driven in the real application afterwards (`evidence/xero/`, `evidence/docking/`, `evidence/realshell/`, `evidence/fixes/`). No fix widened scope: no new framework, abstraction, registry or renamed concept.

## 7. Remaining defects

- **Filed** (`BACKLOG.md`, live table): `TD-186` raw GUIDs shown (three places); `TD-187` a code reference in an Invoices heading; `TD-188` no per-line VAT control on the Quote tab — a question for the Product Owner.
- **Recorded, undiagnosed:** one `SIGABRT` at exit in nine runner runs.
- **Environment, not product:** the Linux-only status-bar test divergence; the ten Windows-only Core tests failing on Linux by construction.
- **Not defects, owed:** docking steps 3–4 (gated), K4/K5 and save-on-close on Windows hardware, the live Xero sign-in, the first `Setup.exe`.

## 8. Build/test evidence (last code commit `37264671`, this container, .NET SDK 10.0.401)

| Gate | Debug | Release |
|---|---|---|
| Build, `TreatWarningsAsErrors` | 0 warnings / 0 errors | 0 warnings / 0 errors |
| Core tests | 5,314 passed, 10 failed (Windows-only), 5,324 total | 5,314 passed, 10 failed (Windows-only), 5,324 total |
| Desktop tests | «DESKTOP_DEBUG» | «DESKTOP_RELEASE» |
| Governance health check | 5 of 5 | — |
| Dependency scan | 9 projects, none vulnerable | — |
| CI (Windows, sharded, both configurations) | 435 green on `7fdc33df`; 436 green on `f7e2af66`; final head «CI_FINAL` | |

Interim runs on earlier heads tonight agreed with these figures at every step (Core 5,306 → 5,314 passed as tests were added; Desktop 883 → 898 → «DESKTOP_PASSED» passed with the one Linux-only failure constant).

## 9. Real-shell evidence

- `WP 21.5C` runner on the final code commit: «REALSHELL_GATE».
- Hand-driven under Xvfb with `xdotool`: the Xero path (`evidence/xero/`, four screenshots, up to the token exchange), docking K1/K2/K3/K6 and the new keys (`evidence/docking/`), the rate-card form → release → New Project picker → timesheet grade (`evidence/fixes/`), backup/restore and restart persistence (`evidence/quality/`).
- Two defects (#5, #6) were invisible to every headless test and found only this way; the campaign's own rule — actual application behaviour over tests — was decisive.

## 10. Security evidence

Dependency scan clean (9 projects, transitive included); all 19 workflow `uses:` SHA-pinned; Dependabot configured; `Security Posture.md` current; the OAuth seam still never throws (a new guard for a browser that cannot open); the secrets directory mode now enforced on existing directories; DPAPI on Windows unchanged. No new listener, no new outbound destination, no new secret.

## 11. Installer/persistence evidence

Installer: the packaging script and its 8 tests exist; **no `Setup.exe` has ever been produced** (tags stop at `v0.18.0`). Persistence: a project survives close (the OS close message) and relaunch; a backup made through the real OS picker is a valid SQLite file (`integrity_check ok`, 9 tables); restore is refused while a project is open; the pre-migration backup path is covered by Core tests. Docking layout restore verified; the save-on-close half owed to a Windows desktop session (no window manager under Xvfb).

## 12. Documentation reconciliation

`VISION.md`, `README.md`, `Product Roadmap.md`, `Future Capability Register.md`, the Academy landing pages (`WP 21.9.1`, 21 drift rows, provenance kept, history not rewritten); `D-028` and the programme file carry the 2026-09-15 calculators amendment; `PHYSICAL_REVIEW.md` §7j corrected (how a panel floats, reach a header with Tab) with K7/K8, §7k added (Xero), §7c C2 (first rate card) and §7h R3 (VAT) corrected; Release Notes carry every row and warning; Execution Plan §6; `PROJECT_STATUS.md` current. Governance health check 5/5 after every change.

## 13. Backlog reconciliation

10 → 7 (three stale rows closed on inspection: `TD-42`, `TD-78`, `TD-182`) → 10 (three residues filed: `TD-186`–`TD-188`). Every remaining row carries an explicit classification: deferred beyond v1.0 (`TD-24`, `TD-25`, `TD-179`), future capability (`TD-84`), gated on the `ADR-0153` review (`TD-91`), intentionally deferred by `D-028` (`TD-174`), a Product Owner decision (`TD-183`, `TD-188`), v1.0.x polish (`TD-186`, `TD-187`). Nothing found tonight was filed to avoid fixing it.

## 14. Current release-candidate status

**Ready for Product Owner acceptance.** Technically: builds clean, suites green on the supported platform, governance and dependencies clean, the value chain drivable end to end on a clean install (now including the first rate card), the Xero path drivable up to the live sign-in, docking steps 1–2 verified on a real screen. Not yet done by anyone: the live Xero sign-in, the first installer, the Windows walk with a second monitor.

## 15. Product Owner acceptance instructions

`PRODUCT_OWNER_ACCEPTANCE.md` §4 (the 19-step journey, about 90 minutes), §5 (where to look hardest), §7 (Xero, X1–X5), §8 (the checklist; the last box is yours).

## 16. Exact remaining actions

1. **You:** walk `PRODUCT_OWNER_ACCEPTANCE.md` §4 on Windows from a fresh persistence root; record every deviation verbatim.
2. **You:** `PHYSICAL_REVIEW.md` §7k — the live Xero authorisation (needs your Xero app credentials; decide the loopback-port question first, `TD-183`).
3. **You (or a tag):** produce the first `Setup.exe` (`scripts/package-installer.ps1` on Windows, or push an `-rc` tag), install it, see the first-run dialog, check for updates.
4. **You:** docking K4/K5 with a second monitor and K8 (close with a floating window open, relaunch).
5. **You:** review `ADR-0153` — steps 3–4 (`WP 21.0B`/`21.0C`, 13 days) start or stay parked on that decision; answer `TD-188` (per-line VAT).
6. **Lead, on your findings:** fix or file each; then the PR of this branch to `main`, tag `v0.21.0`, and the `RC.0F` release steps toward `v1.0.0`.

## 17. Final recommendation (technical readiness only)

On the evidence above the candidate is **technically ready for Product Owner acceptance**. The engineering gate is green where it can run; every defect found overnight that blocked the journey is fixed and re-verified in the running application; what remains is either gated on a Product Owner decision or requires a physical action only the Product Owner can perform. Whether it is **accepted** as `v1.0.0` is not stated here.
