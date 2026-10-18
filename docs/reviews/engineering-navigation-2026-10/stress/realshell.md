# Engineering navigation — real-shell stress test

- **Date:** 2026-10-18
- **Build under test:** `claude/nice-archimedes-airhlx` @ `9306eca` (window title `TempestOS 0.24.0 (9306eca)`)
- **Platform:** Linux, Xvfb (1600x1000 and 1180x760), real X11 input through xdotool, `Tempest.Desktop.RealShell --mode drive`
- **Persistence root:** the one left by the scripted journey (`/tmp/rs/run`, project `NWMAR-APOLLO Apollo Pump Redesign`)
- **Product code changed:** none

## Overall: **NO-GO** (one functional defect, three layout defects)

There were no crashes, hangs, unhandled exceptions or blank panes in roughly 1,100 driven actions. The tree stayed on screen in every scenario. The blocking defect: picking **Evidence** changes the command bar, but the Project Explorer and the status bar's AREA segment still show the discipline picked before it (F1). Fix that, and ideally F2 and F3, and this is a GO.

## Scripted acceptance journey (`scripts/run-realshell-linux.sh --out /tmp/rs`)

| Pass | Steps | Verified | Failed |
|---|---|---|---|
| journey | 36 | 13 | 23 |
| verify (relaunch) | 8 | 1 | 7 |

**Result: FAILED, but not because of this change.** I ran the same script on `origin/main` in a separate worktree. Both runs executed the same 45 steps in the same order, and each step got the same Verified/Failed outcome with the same message. Main's summary line counts the final `close` step in the journey pass (37 / 14 Verified); this branch's counts it separately (36 / 13). The journey script still expects pre-v0.24 identifiers. For example, it looks for `P-0001 Apollo Pump Redesign`, `Q-2026-001` and `mat-s355j2`, while the product now issues `NWMAR-APOLLO` and `NWMAR-APOLLO-Q-001`. The journey script therefore needs updating on `main`. The navigation change does not regress it.

## Findings

| # | Scenario | Result | What I saw |
|---|---|---|---|
| a | 7 disciplines × 10, 100 ms sleeps (once through `act`, once as raw X clicks 100 ms apart) | **PASS** (F2 noted) | Ended on Verification in both runs. The command bar, highlight and AREA all matched. No crash, no hang. After the first discipline pick, the tree narrows from 259 to 159 px but keeps 201 px rows. It gains a horizontal scrollbar, "Engineering Calculations" and "Engineering Assets" clip, and after rapid clicking the tree stays scrolled ~17 px. That pushes the expander chevrons off the left edge (they read as ✓) — **F2**. `a-disciplines-x10-act.png`, `a-disciplines-x10-raw.png` |
| b | Dashboard ↔ Evidence × 20 (raw clicks, 100 ms) and Dashboard ↔ Mechanical × 20 (`act`, 50 ms) | **PASS** (F1 seen) | The tree stayed visible every time. Ending on Dashboard showed the Engineering dashboard and nothing else. Ending on Mechanical showed the Mechanical command bar and explorer. Ending on Evidence showed the Evidence command bar, but the explorer still listed Verification's Inspection/Analysis/Test/… (F1). `b-alternate-end-evidence.png`, `b-alternate-end-dashboard.png` |
| c | Every selector in every discipline | **PASS** (F4 noted) | 19/19 selectors opened. Only one group showed at a time, and the active selector was highlighted. Evidence and Mechanical have no Lifecycle selector (Organize + Actions only), which matches their command sets. Requirements → Actions (13) wraps to **three rows** at 1280 px wide, so the bar is no longer "one row" there (F4). `c-requirements-actions.png`, `c-calculations-lifecycle.png` |
| d | Hide/Show chevron; 10 rapid hide/show pairs; switch discipline while hidden | **PASS** | Hide collapses the bar to its title line and Show restores it. Ten rapid toggles left it in a consistent state. Switching to Documents while hidden kept it hidden and updated the title, explorer and AREA. The hidden state persists across restarts (seen in e). `d-commands-hidden.png`, `d-commands-shown.png`, `d-hidden-then-switch.png` |
| e | Projects → Open → `NWMAR-APOLLO Apollo Pump Redesign` → Structure → each discipline, then 5 × 7 rapid clicks | **FAIL** (F1) | The Structure tab shows the Disciplines list (159 px) with the command bar, Project Explorer, Documents and Properties beside it. Six of the seven disciplines switched correctly. **Evidence** kept the previous discipline's explorer (Documents' Drawings/CAD Models/…) and AREA "Documents". The rapid clicks ended correctly on Verification. `e-structure-requirements.png`, `e-structure-evidence-stale.png`, `e-structure-rapid.png` |
| e′ | Slow control for F1: Verification, wait 2.5 s, Evidence, wait 3 s | **FAIL** (F1) | Same result: Evidence command bar, but Verification's explorer content and AREA "Verification". So this is not a race; Evidence never takes over the workspace area. `control-verification.png`, `control-evidence-stale-explorer.png` |
| f | Rail → Home → Engineering (outside and inside a project) | **PASS** (F5 noted) | Both times the landing page was the Engineering Dashboard with the Dashboard node highlighted, not the last discipline. The status bar's AREA still reads the last discipline ("Verification") on the dashboard and on other modules (F5). `f-home-back-to-engineering.png` |
| — | Tree → Tasks | **PASS** | Leaves the surface. The rail highlights Tasks and LOCATION reads "Tasks". `tree-tasks-leaves-surface.png` |
| g | a)–c) on a 1180x760 screen | **FAIL** (F3), then **PASS** once the window fitted | The window opens at its persisted 1280x800 and is **not clamped to the 1180x760 screen**, so the Hide/Show chevron, the Properties pane and the right of the status bar are off-screen (F3). Xvfb has no window manager, so a real desktop may constrain this; the app does not. After resizing the window to 1180x760: the rail collapses to icons, a) ended correctly on Verification, b) ended correctly on Dashboard/Evidence, and all 19 selectors opened. In Requirements → Actions the secondary commands drop to icon-only buttons, which reads as clean but unlabelled. `g-window-overflows-1180x760.png`, `g-1180-disciplines-x10.png`, `g-1180-alternate-end-dashboard.png`, `g-1180-requirements-actions.png` |
| h | `/tmp/rs/run/logs/` after every scenario | **PASS** | One log file (`tempest-20261018.log`). New lines after each scenario contained no `Error`, `Fatal`, `Exception` or `Unhandled`, and there was no crash dump. The only warnings are the startup DI-default and plain-text-secrets warnings, repeated once per launch. |

## Defects

- **F1 (blocking): Evidence discipline does not switch the Project Explorer or the AREA.** This happens from both the Engineering tree and the project's Structure → Disciplines list. The command bar switches to "Evidence commands", but the explorer keeps the previous discipline's content. The cockpit itself reports "6 discipline area(s) registered" for 7 disciplines, which suggests Evidence has no workspace area to activate and the old one is left in place. Expected: Evidence's own explorer, or an explicit empty state.
- **F2 (minor, clipping): the Engineering tree overflows when it narrows beside the surface.** Rows stay 201 px inside a 159 px tree. This adds a horizontal scrollbar, clips "Engineering Calcul…" and "Engineering Asset…", and the tree can stay horizontally scrolled after clicking, which clips the expander chevrons. The Project Explorer header also clips to "PROJECT EXPL" at 1280 px.
- **F3 (minor, small screens): the window size is not clamped to the screen.** On a 1180x760 display the 1280x800 window hides the Hide/Show chevron and the Properties pane.
- **F4 (cosmetic): the command bar is not one row for large groups.** Requirements → Actions (13) wraps to three rows at 1280 px. The active selector is also 7 px wider than the inactive ones (117 vs 110 px), so the neighbouring selectors shift sideways when the selection changes.
- **F5 (cosmetic): the status bar's AREA is stale off-surface.** It keeps the last discipline on the Engineering dashboard, Tasks and other modules.

## Screenshots

All PNGs in this folder are cropped to the application window. The 1180x760 window-overflow shot is the full screen, which is the point of that image.
