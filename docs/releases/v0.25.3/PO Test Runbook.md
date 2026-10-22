# Product Owner Test Runbook — v0.25.3

**Tester:** ____________. **Date:** ____________. **Build (title bar):** ____________.

This is the one current runbook. It replaces the `v0.25.0` and `v0.25.2` runbooks. Everything those covered and you have already passed is not repeated here.

What changed since `v0.25.2`:

- **Data and Xero survive an update** (P1).
- **Settings are split:**
  - system settings at the foot of the rail;
  - account settings under your name in the header;
  - organisation details in Business.
- **A project has its own Invoices tab** (Q5).
- **Requirements number themselves** (E2).
- **Smaller fixes:**
  - a refused save shows in red beside the button (P3);
  - the Engineering Dashboard is left-aligned (E7);
  - pickers list part sizes smallest first.

## How to score

Fill *Result* with `PASS`, `FAIL` or `SKIP (why)`. Quote what you saw in *Comments*. A gate is **GO** only when every blocking step in it passes and none of its No-Go triggers happened.

---

## A. Install, and your data carries over (do this first)

**Before installing:** if your data lives in the *default* location, copy `%LOCALAPPDATA%\TempestOS\persistence-data` to `C:\TempestOS-data`. Earlier builds kept the default data inside the installer's own folder, and `Setup.exe` clears that folder. If you chose your own folder, for example `C:\TempestOS-rc25-data`, it is safe and needs no copy.

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| A1 | Install `v0.25.3` with `Setup.exe` (or the in-app update), then launch | Title bar reads **0.25.3** | Yes | | |
| A2 | If the data-location dialog appears, pick the folder that holds your data | It is the **last** time it asks. Your customers, projects, quotes and invoices are all there | Yes | | |
| A3 | Settings (foot of the rail) → **Xero** | Still connected to the same organisation. Nothing asks you to authorise again | Yes | | |
| A4 | Close TempestOS. Run the **same** `Setup.exe` again over the installed copy, then launch | **No** data-location dialog. Everything from A2 is still there, and Xero is still connected | Yes | | |
| A5 | Open `%LOCALAPPDATA%\TempestOS-Data` in File Explorer | It holds `first-run.json`, naming your data folder | No | | |

**Gate G0 — Installable and data survives an update: GO / NO-GO**

- **No-Go triggers:**
  - Any record missing after A1 or A4.
  - The data-location dialog appears again at A4.
  - Xero asks to be reconnected.

## S. Settings in their new places

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| S1 | Look at the foot of the left rail, above the **‹** chevron | A **Settings** entry. Click it: the page is titled **Settings**, with Persistence root, Backup and restore, Sign-off, Evidence, Connector authorisation, Xero, VAT, Workflow and Updates. **No** Job role, Appearance or Notifications | Yes | | |
| S2 | Click **your name** in the header | **Account settings**, with Job role, Appearance, Notifications and Timesheets (working pattern and export folder). **None** of the system sections | Yes | | |
| S3 | Account settings → change **Theme** → Save. Then Settings → untick **Confirm before delete** → Save. Close and relaunch | Both choices are kept. Put Confirm before delete back afterwards | Yes | | |
| S4 | Business → **Organisation** | Organisation identity and Bank details, with their own **Save**. They are no longer in Settings | Yes | | |
| S5 | Change the organisation's phone number → Save → export any invoice | The PDF shows the new number. If Xero supplies the company details, a note on the Organisation page says so | No | | |
| S6 | Make the window narrow (half the screen) | The rail folds to icons. The Settings icon is still at its foot and still works | No | | |

**Gate G7 — Settings: GO / NO-GO**

- **No-Go trigger:** a setting that no longer saves, or one you cannot find in either place.

## B. Business and projects

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| B1 | Open a project | An **Invoices** tab right after **Quote** | Yes | | |
| B2 | Invoices tab | Only **this** project's invoices, grouped New / Available to invoice / Sent / Outstanding / Closed | Yes | | |
| B3 | Raise or export an invoice from the tab | Works as in Business → Invoices. The export opens in `01 Commercial\04 Invoices` and is named by the invoice number with `-R1-` | Yes | | |
| B4 | Close the project → Business → Invoices | Every project's invoices, as before | No | | |
| B5 | Business → Customers & Suppliers → add a customer with a code already in use → **Save organisation** | Refused, in **red beside the button** (also at the top) | No | | |

**Gate G3 — Business: GO / NO-GO**

## E. Engineering

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| E1 | In a customer-coded project (e.g. `TESTY-TRIALX`): Requirements → **Create Requirement** → leave **Identifier** blank → enter a statement | Created as `TESTY-TRIALX-REQ-001` | Yes | | |
| E2 | Create another the same way | `TESTY-TRIALX-REQ-002` | Yes | | |
| E3 | Create one with a typed identifier (e.g. `TESTY-TRIALX-REQ-010`), then one with a blank identifier | The typed one stands. The blank one becomes `-011` | No | | |
| E4 | Rail → Engineering → **Dashboard** | Left-aligned, like the other pages | No | | |
| E5 | Calculators → *Bolted joint preload* → open the fastener picker | Smallest to largest: M3, M4 … M10, M12 …, never M10 before M3 | No | | |

**Gate G1 — Engineering: GO / NO-GO**

## R. Quick regression (Could)

| # | Check | Expect | Result | Comments |
|---|---|---|---|---|
| R1 | Ctrl+K, then click outside the palette | It closes | | |
| R2 | Calculators → *Bolt shear capacity*: M20, Class 8.8, 2 planes, safety factor 1.5 | 201062 N | | |
| R3 | Export a quote unchanged, then edit a line and export again | Unchanged it stays R1; after the edit it is R2 | | |

---

## Decision

| Gate | Blocking? | GO / NO-GO / OPEN | Failing steps |
|---|---|---|---|
| G0 Installable and data survives an update | Yes | | |
| G7 Settings | Yes | | |
| G3 Business | Yes | | |
| G1 Engineering | Yes | | |
| G4 Xero — X10 live guard | Yes, once a live organisation is connected; OPEN until then | | |

**Verdict:**

- If every blocking gate is GO, `v0.25.3` is accepted.
- A G0 No-Go trigger means: **stop using this build for real work** and report it at once.
- Signed ____________ date ____________
