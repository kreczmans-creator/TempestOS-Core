# TempestOS — Product Owner Acceptance Pack (v1.0.0 release candidate, from v0.21.0)

**For:** Steven, Product Owner. **Prepared:** overnight, 2026-09-15 23:11 UTC → 2026-09-16 01:50 UTC, by the lead engineering agent with four package agents.
**Branch:** `claude/tempestos-v1-final-acceptance-19hka8` (`release/v0.21.0` @ `a4ab1915` plus tonight's work). **Last product-code commit:** `37264671` (the gate below ran on it); the real-shell runner's own rate-card step followed at `943e6f2e` (test-side code, build 0/0). **Documents commit (this file):** the branch head — `git log -1`.
**What this pack asks of you:** the last checkbox in §8. Everything before it is evidence, and none of it is acceptance.

---

## 1. Executive status

The overnight campaign produced a **credible v1.0 release candidate, ready for Product Owner acceptance — not accepted.** Every technically actionable item from the `v0.21.0` execution plan was either completed and proven (the Xero authorisation path, docking's keyboard closure with the real-screen checks of steps 1–2, a real-process real-input acceptance journey, documentation alignment, the release-quality evidence, the backlog audit) or shown to be gated on you (docking steps 3–4 behind your `ADR-0153` review, the live Xero sign-in, the first `Setup.exe`). Eleven defects were found by driving the real application or reading the code against the morning's journey; **all eight that blocked the journey were fixed with tests the same night**, three user-visible residues were filed. The full gate on the last code commit is green on Linux for everything that can run here and green on the Windows runners for every head pushed tonight up to `7fdc33df` (run 435) and `f7e2af66` (run 436); the run on the final head is **run 442 on `e825065b` — green in every gate job** (Build & Test Debug and Release, Core and the three Desktop shards each, Governance Health Check, Dependency Scan, Linux Launch Smoke with the advisory real-shell step, CI Gate), started 01:43:58 UTC, completed 01:57:23 UTC; `e825065b` carries exactly tonight's code, this commit adds only the verdict. The three things only you can do are in §7 and §5: sign in to Xero, produce and install a `Setup.exe`, and walk the journey on Windows with a second monitor.

## 2. Overnight work completed

| WP / Change | Result | Evidence |
|---|---|---|
| Branch base and the two unmerged branches | Designated branch reset from `main` (`v0.18.0`) to the real candidate `release/v0.21.0` @ `a4ab1915`; the CI cancel-in-progress fix and the Academy chapters merged (no conflicts). | `3d256792`, `fb0c8f8a` |
| `FileSecretStore` (Linux/macOS) | A pre-existing secrets directory stayed `0755`; now `0700`/`0600` are applied after the write. Found by the store's own test on Linux. Windows (DPAPI) unaffected. | `69668268`; `FileSecretStoreTests` 5/5 |
| **`WP 21.6P` — the Xero authorisation path** | **Verified from code before any change:** Authorise never authorised (the browser flow had no caller); the client id was stored under a key the authoriser never read; the connector chosen in Settings never reached the host. All fixed; a browser that cannot open is a result, not an exception; a chosen-but-not-running connector says *restart* instead of the Fake's "Authorised." (that last one caught only in the real application). 13 tests. | `709d01a4`; `WP21.6P Xero Authorisation Path Report.md`; `evidence/xero/01–04.png` |
| `WP 21.9.1` — documentation alignment | `VISION.md` provenance-dated with "Where the product stands"; `README.md` what-the-product-does-today; roadmap and capability register reviewed; Academy landing corrected; 166 links checked, 0 broken; `D-028` addendum for the calculators. | `aa91273c`, `7fdc33df`; `WP21.9.1 Documentation Alignment Report.md` |
| `WP 21.9.1` — release-quality evidence | Governance 5/5; 19/19 Actions SHA-pinned; Dependabot; notices; **backup through the real OS picker, restore guard, restart persistence — in the real application**; installer never yet built by the pipeline. | `2a2d1a33`; `WP21.9.1 Release Quality Evidence.md`; `evidence/quality/01–06.png` |
| **`WP 21.0K` — docking** | `ADR-0153` decision 8 (`Ctrl+Shift+,`/`.` reorder) delivered; keyboard gestures now reach the controller with a visible focus ring; **a critical pre-existing defect fixed**: closing one tab in a floating window discarded every other window's panels. K1/K2/K3/K6 + the new keys verified on a real X11 screen; a floating window restores to its exact place on one monitor. | `5c170f34`; `WP21.0K … Report.md`; `evidence/docking/` (6 PNGs) |
| **`WP 21.5C` (Linux) — the real-shell journey** | A runner starts the real application in-process and drives it with genuine X11 input: 35 steps (33 Verified, 2 pinned to reported defects) + 8-step relaunch verify reading the screen and `tempest.db`; identical across two clean runs; **a major defect fixed** (the organisation picker opened under its prompt). Wired into CI's Linux job as advisory. | `3ba0709f`; `WP21.5C Real-Shell Journey (Linux) Report.md`; `evidence/realshell/` (25 PNGs) |
| **Three journey defects fixed** | No way to create a rate card (so no priced time, no invoice on a clean install) → *Add a rate card* in Libraries; Accept left Requirements/Deliverables empty until reopen → refreshed in place; Home undercounted open projects → six buckets. Driven in the real application. | `37264671`; `evidence/fixes/01–06.png` |
| Backlog audit | 10 → 7 live rows (three stale rows closed on inspection) → 10 with the three residues filed tonight; every row carries an explicit classification. | `BACKLOG.md` |
| Release documents | Release Notes (rows, figures, warnings), `PHYSICAL_REVIEW.md` §7j corrected + K7/K8, §7k (Xero), §7c C2 and §7h R3 corrected, Execution Plan §6, `PROJECT_STATUS.md`, this pack, `OVERNIGHT_FINAL_ACCEPTANCE_REPORT.md`. | the branch head |

## 3. Current repository state

| Item | State | How verified |
|---|---|---|
| Branch / head | `claude/tempestos-v1-final-acceptance-19hka8`; last code commit `37264671`; documents on top (this file's commit is the head). | `git log` |
| Build | Debug **0 warnings / 0 errors**; Release **0 / 0**; `-p:TreatWarningsAsErrors=true`; 9 projects (the real-shell runner added). | `dotnet build` on `37264671` |
| Core tests | **5,314 passed of 5,324** in Debug and Release. The 10 failures are the known Windows-only tests (6 spawn `powershell`, 4 DPAPI) — green on the Windows runners. 7 tests added tonight. | `dotnet test` on `37264671`; CI runs 435/436 |
| Desktop tests | Debug: **901 passed of 902, 1 failed (the Linux-only status-bar test), 6 m 0 s**. Release: **901 passed of 902, 1 failed (the same Linux-only test), 5 m 55 s**. The one Linux-only failure is `StatusBarCollapseTests…` (font fallback, green on Windows). 24 tests added tonight (7 Settings, 8 docking, 1 overlay, 3 rate card, 1 dashboard extension, and assertions inside the quotation journey). | `dotnet test` on `37264671` |
| Architecture invariants | `DependencyDirectionTests` (6), `SampleSeparationTests`, `ModuleMetadataCoverageTests`, `FrozenLayersUnreachableTests` — green (inside the Core figure; run separately by `WP 21.5C`: 51 passed). | Core suite |
| Governance health check | **5 of 5** on the final head. | `pwsh scripts/governance-healthcheck.ps1` |
| Security / dependencies | `dotnet list package --vulnerable --include-transitive`: **all 9 projects clean**; every workflow `uses:` SHA-pinned; Dependabot weekly; `Security Posture.md` current. | scan on the final head; `WP21.9.1 Release Quality Evidence.md` |
| CI (Windows) | `a4ab1915` (the candidate): runs 430 and 433 green. Tonight's heads: 435 (`7fdc33df`) green, 436 (`f7e2af66`, with docking) green; the final head: **run 442 on `e825065b` — green in every gate job** (Build & Test Debug and Release, Core and the three Desktop shards each, Governance Health Check, Dependency Scan, Linux Launch Smoke with the advisory real-shell step, CI Gate), started 01:43:58 UTC, completed 01:57:23 UTC; `e825065b` carries exactly tonight's code, this commit adds only the verdict. | GitHub Actions |
| Real shell | The journey runner on the final code head: the `WP 21.5C` runner on the final head, display :105, 2026-09-16 01:38–01:42 UTC: **journey 37 steps — 37 Verified, 0 Failed; relaunch verify 8 of 8 — PASSED**, now including the rate card created and released through the new form, the New Project prompt offering it, the timesheet prompt offering its grade, and the invoice request raised on completion (`evidence/realshell/gate-run-*-on-the-final-head.md`). Two earlier runs tonight on the same product code: 34/35 (before the runner learned the rate-card form) and 35/36 (the stale Raise-invoice expectation). Plus the hand-driven checks in `evidence/xero/`, `evidence/docking/`, `evidence/fixes/`, `evidence/quality/`. | `scripts/run-realshell-linux.sh` |
| Installer | **Never produced by the pipeline** — `release.yml` runs on tags; newest tag `v0.18.0`. Script and 8 tests exist. Windows-only work for you (§4 step 19). | `WP21.9.1 Release Quality Evidence.md` §3 |
| Backup / restore | Verified in the real application: a valid `.db` (`integrity_check ok`), restore refused while a project is open, data survives close and relaunch. | `evidence/quality/` |
| Live backlog | 10 of 30, every row classified. | `BACKLOG.md` |

## 4. Product acceptance journey (about 90 minutes, in this order)

Every step names the `PHYSICAL_REVIEW.md` row it comes from; that file
carries the exact expected wording and the "counts as a failure if"
column. Launch per `PHYSICAL_REVIEW.md` §3 on a **fresh persistence
root** (`--persistence-root <empty folder>`), so every count starts at
zero and nothing from an earlier session masks a defect. Evidence
folders are under `docs/releases/v0.21.0/evidence/`.

| # | Action | Expected result | What to look for | Evidence from tonight |
|---|---|---|---|---|
| 1 | Launch. | Title bar `TempestOS 0.21.0 (<sha>)`; rail reads exactly Home, Projects, Tasks, Engineering, Business; Home shows five task tiles at 0, "Quotes: 0 open, £0.00", "Invoices: unavailable — No accounts reading yet." (§7c D1). | A rail entry that is empty; a tile showing a number on a fresh root. | `realshell/` step 1; `quality/04-…png` |
| 2 | **First, the consultancy's rates** (new tonight — a clean install had no way to create one): Engineering → **Reference data** → *Add a rate card*: name, grade `Engineer`, hourly rate `95` → **Add Rate Card** → the record opens right up → **Verify** → **Release**. Then Home → **New Project…**: name, **Add organisation…** *Client Ltd* (the picker now opens **above** the prompt — fixed tonight), **Rate card** → the released card is offered, PO `PO-1001`. Then Project → **Details**: budget, dates. | Rate cards (1) — Released; the New Project prompt's Rate card list shows it; the project opens right up; Details shows client by name, PO, budget, rate card (§7c D2, C2; §7d T1). A Draft card is refused with the reason. | The picker hidden behind the prompt; an id where a name belongs; a Draft card accepted. | `fixes/01–05.png`, `realshell/` steps 2–4 |
| 3 | **Quote** tab: add an hourly line and a fixed-price line with different VAT rates → **Export** → **Send** → **Accept**. | Each line's VAT is net × rate; Export saves `<reference>-quote.pdf` rendered through the shared template (wordmark, indigo rule); Send attaches the PDF to the quotation and moves it to Sent; Accept creates one Deliverable and one Requirement per line under a Milestone named after the quotation (§7c D3–D6; §7h R3). | Sent without a PDF; a Deliverable missing; a VAT amount not matching the rate. | `realshell/` steps 5–8 |
| 4 | Straight after Accept, without leaving the project: **Requirements** tab, **Deliverables** tab; add one Deliverable directly. | Both tabs already show what Accept created (fixed tonight — they used to stay empty until the project was reopened); Requirements lists them as "Not verified"; Deliverables lists the quote's plus the added one (§7c D7). | An empty state on either tab until the project is reopened. | `realshell/` |
| 5 | **Structure** tab: Calculations → create a calculation (Due date defaults today + 14); Mechanical → two Parts, the second with the same identifier. | The engineering surface renders inside the tab; the duplicate identifier is refused in the status bar (§7c D8; §7d T2, T3). | A menu bar above the ribbon; a duplicate accepted. | `realshell/` |
| 6 | Engineering → Modules → **Calculators** → *Bolt shear capacity*: 20 mm, 400 MPa, 2 planes, safety factor 1.5 → **Calculate** → **Re-run** → **Compare with previous**; then safety factor 2 → Calculate → Compare with previous; **Start a new calculation** → Calculate; type "abc" into Bolt diameter → Calculate. | 167552 N; Re-run records run 2 "Re-run of" run 1; the first Compare says nothing differs, the second shows only the Safety factor and Allowable shear capacity rows (167552 → 125664 N); the new calculation is run 1 with the old untouched; "abc" is refused beside the form with nothing recorded (§7i C8–C11). Then *Beam bending and deflection* with a released material (C2–C5): the material's E and allowable fill read-only; span 200 mm refuses with the span-to-depth reason, no dialog. | Stored JSON in the Working section; a renamed Calculate landing on the old calculation (both fixed on 2026-09-15); an exception dialog. | `realshell/` |
| 7 | Engineering → Modules → **Engineering Assets** → *Bracket verification*: a released material, load/area/length/mass limit with their own units → **Check** → **Record** into a pack and an artefact with the independent figures. | Applied stress, allowable, margin, mass with units; Record writes the artefact and the trace shows it (§7e E3, E4). | A fixed unit; Record silently succeeding with a blank independent basis. | `realshell/` |
| 8 | **Evidence** tab: record evidence (files, subject, the released material cited, key figures) → Settings → Evidence → **Independent check required** on → **Switch person…** to a second released person → **Check** → **Issue**. | Draft → Checked → Issued; the checker must differ from the author when the toggle is on; Issue attaches the issue sheet to the project (§7a; §7c D9; §7h R4). | The author allowed to check their own evidence with the toggle on. | `realshell/` (files may need the OS picker) |
| 9 | Business → **Timesheets** → **Record**: the project, 2 hours, grade **Engineer** (offered from the pinned card), a task → **Record**; then **Export week**. | The entry records and prices from the card (a blank task is refused with "A task is required."); a project with no card refuses with the reason; the exported week PDF has hours by project/day and blank signature blocks (§7c D12; §7f D2). | A rate silently zero; a fabricated approver; the grade list empty with a card pinned. | `fixes/05–06.png`, `realshell/` |
| 10 | **Deliverables** → **Complete** one (with the Issued evidence) → Business → **Invoices**. | Completion raises a Draft invoice request by itself (Fake connector); the row grows Open completion / Raise invoice; Invoices groups New / Available to invoice / Sent / Outstanding; **Export invoice** saves `<reference>-invoice.pdf` with bank details from Settings → Organisation (§7c D10, D11; §7f D1). | Completion failing because raising failed; an invoice without payment terms. | `realshell/` |
| 11 | Home, Projects → **Dashboard + Reports**, Business → **Dashboard & Reports**, **Tasks**. | Home's Project status now has six bars (On track, Ready to invoice, At risk, Overdue, Blocked, On hold) and its total counts every open project — including this one once a deliverable is complete (fixed tonight: it read 0 with one open project); Projects lists the project with a schedule bar; Business shows "unavailable" (no accounts reading) rather than zeros; Tasks shows the calculation and review buckets (§7c D1, D14–D17). | "0 open project(s)" with a project open; a zero where "unavailable" is honest; a stale count after navigating away and back. | `fixes/04.png`, `realshell/`, `quality/` |
| 12 | Search: **Ctrl+K**, type the project's name; the object picker: Structure → select an object → **Ctrl+Shift+M**; Undo: create a Document → **Ctrl+Z** → **Ctrl+Y**; rename → Ctrl+Z. | The palette finds the project and opens it; the picker moves the object and Ctrl+Z moves it back; Undo/Redo name the action in their tooltips (§7d T6, T7; §7g U1–U3). | Undo altering the original of a copy; the palette showing commands that do not apply. | `realshell/` |
| 13 | Attachments: Documents → attach a PDF and an `.svg` → **Open** each → rotate, annotate, **Save annotated copy…**; attach the same file to two objects → delete one. | The viewer shows the PDF and the SVG in-app; the annotated copy saves; the two attachments share one sha256 and deleting one leaves the other (§7d T5, T9). | A DWG opened in-app (it must hand off); a shared file deleted with its twin. | `quality/` (backup picker proves the OS picker works) |
| 14 | Libraries: Engineering → **Reference data** → open a record → **Revise** → **Release**. | A real editor, not a dialog; the revision history shows the new revision; a superseded record keeps resolving (§7c D15; §7a). | A release that loses the previous revision. | `realshell/` |
| 15 | Docking (§7j K1, K2, K3, K6, K7; K8 Windows-only): float the Inspector by dragging its tab past the workspace edge; dock the Explorer into it; close both tabs; keyboard-move a tab; `Ctrl+Shift+,`/`.`; Reset Layout; then close TempestOS with a floating window open and relaunch. | The floating window takes the second tab; closes with its last panel and the main window is untouched; focus ring stays after a keyboard move and reorder; Reset restores one window; **on relaunch the floating window is back where it was** (the save-on-close half is yours: it could not be driven under Xvfb). | Panels vanishing from the main window when a floating tab closes (fixed tonight — if you see it, it is a regression); a floating window left behind after the main window closes. | `docking/k1…k6, reorder…, persistence-…png` |
| 16 | Persistence and backup: Settings → **Back up now…** (pick a folder) → open the file's folder; **Restore from backup…** with a project open; close the project; restore; relaunch. | The backup is a valid `.db` (tonight: `integrity_check ok`, 9 tables); restore is refused while a project is open; after restore and relaunch the data is what the backup held (§7c C1; `WP21.9.1 Release Quality Evidence.md` §4–5). | A backup that is a copy of an open WAL file with no tables; restore allowed with a project open. | `quality/01–06.png` |
| 17 | Project lifecycle: **Sign off** with a blank statement, then a statement; a project closed ≥ 90 days ago (set the date back in the editor if you have one). | Blank statement refused before any service call; an archived project shows the read-only banner and every write is refused (§7c D18, D19). | A write accepted on an archived project. | `realshell/` |
| 18 | **Xero** (§7k X1–X5) — see §7 of this pack. | The status line at each step reads exactly as §7k says. | Anything else — record verbatim. | `xero/01–04.png` |
| 19 | Installer (Windows only): tag an `-rc` build or run `scripts/package-installer.ps1`; install `Setup.exe`; first run → the data-location dialog; **Check for updates now**. | A `TempestOS-0.21.0-Setup.exe`; the first-run dialog; Updates reports the feed state honestly (`WP 21.5A`; `PHYSICAL_REVIEW.md` §3, §4). | No `Setup.exe` is produced — the pipeline has never produced one (§6). | `WP21.9.1 Release Quality Evidence.md` §3 |

## 5. Highest-risk acceptance areas

1. **The installer has never been produced.** `package-installer.ps1` and
   its eight tests exist; `release.yml` runs only on a `v*.*.*` tag and
   the newest tag is `v0.18.0`, before `WP 21.5A`. No one has installed,
   first-run or updated TempestOS from a `Setup.exe`. This is the largest
   untested surface in the candidate and it is Windows-only work (§4 step 19).
2. **The live Xero sign-in.** The path exists and is proven up to the
   token exchange with a simulated redirect (`WP 21.6P`); the real
   exchange, Xero's consent page, the tenant resolution and the first
   accounts read against your organisation have never run (§7).
3. **Docking's save-on-close and two-monitor behaviour.** K1/K2/K3/K6/K7
   are verified on a real X11 screen; `MainWindow.Closing`'s five saves
   (layout, window state, recents, favourites) could not be driven under
   Xvfb, and K4/K5 need a second physical monitor. A floating window
   outliving the main window would mean the owner relationship is not
   honoured on your machine — watch for it (§4 step 15).
4. **The real-shell journey is a Linux/Xvfb proof, not a Windows one.**
   Real process, real renderer, real X11 input — but the shipped platform
   is Windows. Font metrics already differ (one status-bar test diverges
   on Linux only). Your walk on Windows is the first real-shell run on the
   supported platform for `v0.21.0`'s later packages.
5. **Persisted state versus what the screen shows.** Every step above
   that creates something should be checked after navigating away and
   back, and once after a relaunch (steps 11, 15, 16). Tonight's runs
   found no divergence; the sample is one machine.
6. **The Fake connector is the default.** Invoice requests, accounts
   readings and dashboards show the Fake's answers until Xero is
   authorised and TempestOS restarted; "Authorised." next to *Fake* is the
   Fake's own truth, not Xero's.

## 6. Known limitations

**Accepted v1.0 limitations (decided, documented, not defects)**
- Single user, local trust, Windows installer only; Linux advisory, macOS untested (`PHYSICAL_REVIEW.md` §2a; `D-021`, `D-025`).
- Not an ERP, not a PLM; no expression grammar, cell editor or run diff; calculations beyond the eleven modules are done in the engineer's own tools and recorded as evidence (`D-028` and its 2026-09-15 addendum).
- Requirements Create/Delete/Move/status changes are not undoable (`PHYSICAL_REVIEW.md` §7g, disclosed).
- Docking steps 3–4 (document tabs, project tabs and rail panes as dockable units; identity strips on torn-out views) are not built — gated on your `ADR-0153` review (`WP 21.0B`, `WP 21.0C`).
- The nested engineering tree stays a second docking level inside the Structure panel (`ADR-0153` decision 2).
- Seeded fasteners are geometry-only; a bolted-joint run on one refuses as `RecordIncomplete` until the library carries mechanical properties (Release Notes, Warnings).

**Unresolved defects (none open that block the journey; these are the honest residues)**
- Every blocking defect found tonight was fixed with a test: the Linux secrets-directory mode; the three Xero authorisation-path defects and the Fake "Authorised." misreport; the floating-window close that discarded other windows' panels; the organisation picker opening under its prompt; no way to create a rate card; the Requirements/Deliverables tabs not refreshing after Accept; Home's undercount of open projects.
- **Filed, user-visible, not fixed** (`BACKLOG.md`): raw GUIDs shown in three places (`TD-186`); a literal "(`TD-180`)" in an Invoices heading (`TD-187`); no per-line VAT control on the Quote tab — the default from Settings applies to every line (`TD-188`, a question for you). One `SIGABRT` on process exit seen once in nine runner runs, undiagnosed (Release Notes, Warnings).
- The one-off catalogue selection jump recorded on 2026-09-15 was not reproduced then or tonight.
- Linux only: `StatusBarCollapseTests` diverges under the Linux headless host (font fallback, Inferred); green on Windows CI; not a product defect on the supported platform.

**Future capability (not v1.0)**
- Dedicated UI for the frozen disciplines (Validation, Units, Profiles, Loads, Environments, Compare, Optimization, Sensitivity, Math Tools) — `TD-84`, Future Capability Register.
- Part attributes a drawing or calc sheet would cite — `TD-174`, `D-028`.
- Compare-and-swap on Requirements, a bound on `VerificationContext` — `TD-25`, `TD-24`, deferred beyond v1.0.
- The Windows UI-Automation real-shell run in CI (the `WP 21.5C` as originally written) — tonight's Linux runner is the actionable equivalent; the Windows one remains future work.

**External acceptance dependencies (only you can supply)**
- Your Xero app credentials and consent (§7).
- A Windows machine with a second monitor for K4/K5.
- A tag (or a manual run of `package-installer.ps1`) to produce the first `Setup.exe`.
- The `ADR-0153` review that gates docking steps 3–4.

## 7. Xero

| Question | Answer | Evidence |
|---|---|---|
| Do the connector tests pass? | **Yes** — `XeroConnectorTests`, `XeroConnectorAccountsTests`, `QuickBooksOnlineConnector*Tests`, `ConnectorSelectionTests` (now including "the connector chosen in Settings is bound at the next start") green in both configurations, here and on Windows CI run 435. | Core gate figures (§3) |
| Do the OAuth tests pass? | **Yes** — `OAuthAuthoriserTests` (round trips through the real loopback listener with a fake browser, PKCE, state check, consent denied, port in use, and tonight's "browser cannot open" case) and the new `AuthorisableConnectorTests` green. One fact, `AStoredToken_IsNeverWrittenToTempestDb`, constructs the Windows DPAPI store and fails on Linux only; green on Windows CI. | Core gate figures (§3) |
| Could a user authorise Xero from the product before tonight? | **No — Verified from the code.** The Authorise button only re-read the state; the browser sign-in had no caller; the client id typed in Settings was stored under a key the authoriser never read; the connector chosen in Settings never reached the host. All three fixed by `WP 21.6P`. | `docs/releases/v0.21.0/WP21.6P Xero Authorisation Path Report.md` |
| Has the path been driven in the real application? | **Yes, up to the token exchange** — Settings → Xero → client id → Authorise → restart wording → relaunch → Xero bound, "Not authorised." → Authorise → "Waiting for you to sign in to Xero in your browser (up to 5 minutes)…" with the correct Xero authorisation URL handed to the (stub) browser → a simulated redirect answered by the loopback listener → "Authorisation failed. The token endpoint refused the authorisation code." (a made-up code; honest). | `evidence/xero/01–04.png` |
| Has live authorisation been completed? | **No.** It needs your Xero app's client id (and secret if not PKCE-only), the redirect URI `http://127.0.0.1:49301/callback/` registered on that app, your sign-in and consent. Nobody has done this yet, and this pack does not pretend otherwise. | — |
| What must you do? | `PHYSICAL_REVIEW.md` §7k X1–X5, in order, recording every status line verbatim. Expect a **restart** between choosing Xero and authorising (the connector is bound at start-up; the status line tells you). | §7k |
| A decision before you start | The default loopback port 49301 is inside Windows' dynamic port range; a rare "Port 49301 is already in use" refusal is possible (retry answers it). Moving the default (`Invoicing:OAuth:LoopbackPort`) means registering a different redirect URI on the Xero app. Tonight left it as is — your call (`TD-183`). | Release Notes, Warnings |

## 8. Final acceptance checklist

Tick what you have seen with your own eyes on Windows. The last box is
yours alone; nothing tonight ticks it for you.

- [ ] Application launches
- [ ] Navigation feels coherent
- [ ] Project creation works
- [ ] Quote workflow works
- [ ] Engineering calculation workflow works
- [ ] Evidence workflow works
- [ ] Independent check works
- [ ] Deliverable workflow works
- [ ] Timesheet works
- [ ] Invoice workflow works
- [ ] Dashboards are credible
- [ ] Search/findability works
- [ ] Attachments work
- [ ] Libraries work
- [ ] Documents export correctly
- [ ] Viewer works
- [ ] Undo/Redo behaves correctly
- [ ] Docking behaves correctly
- [ ] Persistence survives restart
- [ ] Backup/restore behaves correctly
- [ ] No unacceptable defects remain
- [x] v1.0 release candidate accepted — *Product Owner's decision, recorded here by the Product Owner only*

## 9. Product Owner verdict (2026-09-23)

**Accepted, as `v0.22.0`, not `v1.0.0`.** `v0.22.0` = `main` (`v0.18.0`
plus the seven dashboard-export commits of 2026-09-20/21, `ADR-0154`/
`ADR-0155`) merged with this branch (`v0.19.0`–`v0.21.0` plus the
overnight campaign above) and `claude/academy-docs-review-completion-iqzgwv`
(10 Academy docs commits). `v1.0.0` is deferred until the live Xero and
QuickBooks connectors named in §7 above have had their first real run —
everything else this pack asked for is accepted as delivered. See
`docs/releases/v0.22.0/Release Notes.md`.
