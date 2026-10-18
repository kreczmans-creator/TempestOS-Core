# Product Owner Test Runbook — v0.25.0 (release candidate 1)

**Tester:** ____________ (Product Owner). **Date:** ____________.
**Build (title bar):** ____________. **Commit (from the installer script or the release page):** ____________.
**Data folder:** `C:\TempestOS-rc25-data` (a fresh folder, so this run starts clean).
**Xero organisation:** the **Demo Company (UK)** only.

This is the first downloadable build since `v0.22.0`. It carries three
lines of work:

- **`v0.25.0`** — new Engineering navigation (section N). **Never tested by you.**
- **`v0.24.0`** — Xero (section X). **Never walked against the Demo Company.**
- **`v0.23.0`** — business and numbering (section B). Its first PO round's fixes are in.

The sections are in priority order, so the newest and riskiest come first.

## How to score

- Fill *Result* with `PASS`, `FAIL` or `SKIP (why)`.
- A FAIL is anything that differs from *Expect*. Quote the exact wording you saw in *Comments*, and save a screenshot (Win+Shift+S) named with the step id, e.g. `N3.png`.
- Each section ends in a **gate**. A gate is **GO** only when every **blocking** step in it passes.
- The **No-Go triggers** under each gate fail it on sight, whatever the step results.
- Fill the **Decision** table at the bottom last.

## Time plan

| Block | Sections | About | Needed for a decision? |
|---|---|---|---|
| Must | A, N, S, X (XA–XQ, XI) | 90 min | Yes: these gates decide whether rc.1 becomes `v0.25.0` |
| Should | B, X (the rest) | 60 min | Yes, if time allows; otherwise record SKIP and the gate stays open |
| Could | C, R | 30 min | No; findings feed the next round |

---

## A. Get the build and start clean

Use one of the two routes.

**Route 1 (recommended) — the developer PC with the source checkout:**

```powershell
git fetch; git checkout main; git pull
pwsh -NoProfile -File scripts/install-test-build.ps1
```

The script:

- builds the installer at `VERSION` 0.25.0 and installs it;
- records `C:\TempestOS-rc25-data` as the data folder;
- puts a **TempestOS 0.25.0 (test)** shortcut on the desktop;
- prints the version and commit.

**Route 2 — download:**

1. Open <https://github.com/kreczmans-creator/TempestOS-Core/releases>, then the release **TempestOS v0.25.0-rc.1**.
2. Download `TempestOS-v0.25.0-rc.1-Setup.exe` and `SHA256SUMS.txt`.
3. Check the hash: `Get-FileHash .\TempestOS-v0.25.0-rc.1-Setup.exe` must match the line in `SHA256SUMS.txt`.
4. Run the Setup.exe.
5. Before the first launch, make a desktop shortcut to `%LOCALAPPDATA%\TempestOS\current\Tempest.Desktop.exe` with the argument `--persistence-root "C:\TempestOS-rc25-data"`. This skips the first-run data-location dialog, which can hang (`C-01`).

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| A1 | Install by route 1 or 2 | Finishes without an error; route 1 prints `0.25.0` and a commit | Yes | | |
| A2 | Launch from the **TempestOS 0.25.0 (test)** shortcut (or your route-2 shortcut) | The window opens on **Home** within about 10 s; the title bar reads `TempestOS 0.25.0 (…)`; no data-location dialog | Yes | | |
| A3 | Look at the rail | Exactly **Home, Projects, Tasks, Engineering, Business** | Yes | | |
| A4 | Close the window (X) and relaunch | Opens again on the same data folder; no error dialog; nothing in `C:\TempestOS-rc25-data\logs\tempestos-crash.log` | Yes | | |

**Gate G0 — Installable: GO / NO-GO**

- **No-Go triggers:** an installer error; the title bar is not 0.25.0; any crash dialog; a crash log appears.

---

## N. New in v0.25.0 — Engineering navigation

The Engineering tree now lists the disciplines, and picking one keeps the
tree beside the work. The command bar above the work is one row. Use a
window of at least 1280 px wide, then repeat N9 narrow.

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| N1 | Rail → **Engineering** (no project open: use the header's project chip → **Close Project** first if one is open) | The tree reads **Dashboard, Tasks, Disciplines** (expanded: Calculations, Documents, Evidence, Manufacturing, Mechanical, Requirements, Verification), **Modules** (Engineering Calculations, Calculators, Engineering Assets), **Reference data**. There is **no Mechanical** under Modules. The right pane shows the Dashboard | Yes | | |
| N2 | Disciplines → **Requirements** | The right pane shows **"Requirements commands"** above the Project Explorer, Documents/Cockpit and Properties. **The tree stays on the left** (narrower), and the rail highlights **Engineering**. There is **no row of discipline tabs** (Calculations / Documents / Evidence…) anywhere | Yes | | |
| N3 | Look at the command bar | One row: the **Create Requirement** tile, then **Organize (3) ▾**, **Lifecycle (1) ▾**, **Actions (13) ▾**. One group's buttons show under the selectors. Nothing is cut off at the right edge, and there is no horizontal scroll bar | Yes | | |
| N4 | Click **Actions (13) ▾**, then **Lifecycle (1) ▾**, then **Organize (3) ▾** | Each click shows that group's buttons (Actions: Create Requirement Group, Link Requirement, Set Requirement Owner …) and hides the others. The chosen selector is highlighted | Yes | | |
| N5 | Disciplines → **Calculations** → **Create Calculation** → give it a name | The bar switches to *Calculations commands* and the Explorer to Calculations, without leaving the screen. The new calculation opens right up in an editor tab, and the status bar says it was created | Yes | | |
| N6 | Select that calculation in the Explorer → **Organize ▾** → **Rename Calculation** | The Object Editor opens; rename and save; the Explorer shows the new name | Yes | | |
| N7 | Click **Dashboard** in the tree | One click returns you to the Engineering Dashboard. Then click **Mechanical**: you are back in the surface with *Mechanical commands* | Yes | | |
| N8 | The **^** chevron at the right of the command bar | The commands fold away and the *"… commands"* header stays. The chevron (now **Show commands**) brings them back. Fold it, close TempestOS, relaunch, go to a discipline: it is still folded, and the chevron restores it | Yes | | |
| N9 | Make the window narrow (under 1,200 px) and repeat N2–N4 | The rail folds to icons, the buttons compact to icons with tooltips, and nothing overlaps or is clipped | No | | |
| N10 | Projects → open a project → **Structure** tab | A narrow **Disciplines** list on the left, then the command bar, Explorer, Documents and Properties. The rail highlights **Projects** | Yes | | |
| N11 | In the Structure tab's list pick **Verification**, then **Mechanical** → **Create** a Part | The bar and Explorer follow each pick. The Part is created under the open project and opens right up | Yes | | |
| N12 | Ctrl+K → type *requirement* → pick **Create Requirement** | The palette lists every Requirements command, and running one behaves as from the bar. The Structure list's highlight moves to **Requirements** if it was not already | No | | |
| N13 | Rail → **Home** → rail → **Engineering** | The landing page shows **Dashboard**, not the surface you left | No | | |
| N14 | Your own judgement: find **Approve** for a Document, then **Record Verification Result**, without help | Each takes at most 3 clicks from the Engineering tree, and you can always see how to get back | Yes | | |

**Gate G1 — Navigation: GO / NO-GO**

- **No-Go triggers:**
  - The tree disappears when a discipline is open.
  - A command is unreachable from the bar (Ctrl+K alone is not enough).
  - Leaving a discipline needs the rail.
  - A crash, or a frozen pane, when switching disciplines quickly.

---

## S. Core smoke and data safety (every release)

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| S1 | Projects → **New Project…** → no client → name *Smoke 25* | The project is created as `P-0001` (or the next `P-` number) and opens right up | Yes | | |
| S2 | Structure → Requirements → **Create Requirement**; Documents → **Create Document** (pick a Kind from the dropdown) | Both are created under the project; the project's **Requirements** and **Documents** tabs list them | Yes | | |
| S3 | Select the document → **Organize ▾ → Delete Document** | Asks for confirmation naming the document; confirm; it goes and the selection clears | Yes | | |
| S4 | Ctrl+Z | The delete is undone and the document is back — or, if this delete is not undoable, the status bar says so in words | No | | |
| S5 | Close TempestOS with work open; relaunch | Everything from S1–S4 is still there; the last project and area are restored | Yes | | |
| S6 | The header's theme button (◐) | Light and dark both readable on Home, Engineering and a project | No | | |

**Gate G2 — Data safety: GO / NO-GO**

- **No-Go triggers:**
  - Anything created is missing after a relaunch.
  - A delete with no confirmation.
  - A raw exception or Guid shown to you.

---

## X. Xero on the Demo Company (v0.24.0)

Detailed steps live in `docs/releases/v0.24.0/PO Test Runbook.md`;
the ids below are its section ids.

- **Every *(needs …)* marker in that runbook is merged in this build.** Walk those steps rather than skipping them.
- Use the data folder above. Connect the **Demo Company (UK)** only, following `docs/guides/Xero Setup - Step by Step.md` Parts 1–3.
- Register the redirect `http://127.0.0.1:48131/callback/` in your Xero app first.

| # | Action (runbook ids) | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| X1 | Connect (XA2–XA3) | Consent lists six scopes; Settings → *Connector authorisation* reads **Authorised.** | Yes | | |
| X2 | (Developer PC) smoke script (XA4): `pwsh -NoProfile -File scripts/xero-demo-smoke.ps1 -DataFolder C:\TempestOS-rc25-data -Keep` | **RESULT: PASSED**, with S01–S20 and P01–P10 *pass*. Copy F1–F4 into the v0.24.0 Release Notes' *Open items* table | Yes | | |
| X3 | Settings → Xero (XS1–XS9) | Connected, *Missing: none*, *Demo Company: yes*, company details *from Xero, read at …*; pickers populated; *Allow live organisation* **off** | Yes | | |
| X4 | Link a customer to its Xero contact (XC1–XC3) | Nothing is linked until you confirm; no duplicate contact in Xero | Yes | | |
| X5 | Quote journey (XQ1–XQ8) | Badge **Queued → In Xero (draft) → Sent in Xero → Accepted in Xero**; same number and PDF in Xero; no email sent; *Refresh from Xero* reads back Xero's changes | Yes | | |
| X6 | Invoices (XI1–XI11) | Every invoice is a **Draft** in Xero; there is no approve or email action in TempestOS; *Edit lines* and **Void** behave as the runbook says | Yes | | |
| X7 | Purchase orders and expenses (XP, XE) | Each is a **Draft** in Xero with its PDF or receipt; VAT as recorded | No | | |
| X8 | Retry and offline (XR, XO) | **Queued** survives a restart offline and goes out once; never a duplicate | No | | |
| X9 | Forward cash (XF1–XF5) | Six months; every figure names its source; nothing counted twice | No | | |
| X10 | Demo guard (XG1–XG6) | Connected to a live organisation, writes are refused and say why | Yes | | |

**Gate G4 — Xero (Demo Company): GO / NO-GO**

- **No-Go triggers:**
  - **Anything written to a live organisation.**
  - TempestOS approves or emails anything in Xero.
  - A duplicate record in Xero.
  - A lost record after a restart.

**Gate G6 — Go live with Xero: YOUR CALL, not part of the release decision.**

Turn on *Allow live organisation* (XG7) only after G4 is GO **and** X2
passed. Until you decide, leave it off.

---

## B. Business and numbering (v0.23.0)

The rows are `PHYSICAL_REVIEW.md` §7l, N1–N10. Walk them there and copy
the results here.

| # | Area (§7l row) | Blocking | Result | Comments |
|---|---|---|---|---|
| B1 | Business tree: Dashboard, Customers & Suppliers, Staff, Rate cards, Quotes, Invoices, Purchase orders, Timesheets, Subscriptions (N1) | Yes | | |
| B2 | Customer with a 5-character code; a contact on it (N2) | Yes | | |
| B3 | New Project with a coded client → `CODE-PROJREF`; its first quote `…-Q-001` (N3) | Yes | | |
| B4 | Project folder on disk, created once (N4) | No | | |
| B5 | Second-person sign-off on: self-approval refused (N5) | Yes | | |
| B6 | Return to draft, R1/R2 revisions, PDF shows R1 (N6) | No | | |
| B7 | Timesheet export folder remembered (N7) | No | | |
| B8 | Home → Continue and Recent open what they name (N8) | Yes | | |
| B9 | Sign-off switch off by default; self-approval says so (N10) | Yes | | |

**Gate G3 — Business: GO / NO-GO**

- **No-Go triggers:**
  - A duplicate customer code or project reference is accepted.
  - A self-approval succeeds while second-person sign-off is on.
  - A Home link opens nothing.

---

## C. Engineering spot checks (Could)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| C1 | Engineering → Modules → **Calculators** → *Bolt shear capacity*: 20 mm, 400 MPa, 2 planes, safety factor 1.5 → Calculate → **Re-run** | 167552 N; the re-run makes a second record on the same calculation; the diagram is live | | |
| C2 | Engineering → **Reference data** → Materials | 77 materials, each with a cited source | | |
| C3 | Engineering → **Tasks** | Calculations, Reviews and Approvals listed; **Open** opens the object in the surface with the tree still visible | | |
| C4 | Engineering → Modules → **Engineering Assets** | Renders; nothing clipped | | |

## R. Regression watch-list: things that failed in earlier rounds (Could)

| # | Was | Check | Result | Comments |
|---|---|---|---|---|
| R1 | v0.23 D9: header search box inert | Click the header search box: the palette opens with the cursor in it | | |
| R2 | v0.23: fixed-list parameters typed | Create Document → Kind is a dropdown | | |
| R3 | v0.23: Home Recent opened into a hidden pane | Home → Recent → an object opens visibly in Engineering | | |
| R4 | v0.22: the old shortcut opened an old data folder | Only the *TempestOS 0.25.0 (test)* shortcut is on the desktop | | |

---

## Decision

| Gate | Blocking for v0.25.0 final? | GO / NO-GO / OPEN | Failing steps |
|---|---|---|---|
| G0 Installable | Yes | | |
| G1 Navigation | Yes | | |
| G2 Data safety | Yes | | |
| G4 Xero (Demo Company) | Yes | | |
| G3 Business | Yes, if walked; OPEN if skipped | | |
| G6 Go live with Xero | No (your separate call) | | |

**Rules:**

- **All blocking gates GO** → tag `v0.25.0` final from the same commit. No code changes.
- **Any blocking gate NO-GO** → send the failing step ids, screenshots and comments back. The fixes ship as `v0.25.0-rc.2`, and only the failed sections are re-walked.
- **G3 OPEN (not walked)** → `v0.25.0` may still be tagged if G0, G1, G2 and G4 are GO. G3 is then walked first next time.
- **A No-Go trigger in G2 or G4** (data loss, a live-organisation write, TempestOS approving or emailing in Xero) → **stop using this build for real work.** Report it at once.

**Verdict:** GO / NO-GO for `v0.25.0` — signed ____________ date ____________

**Top three things to change next (free text):**

1.
2.
3.
