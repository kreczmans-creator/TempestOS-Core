# Independent Test Runbook — TempestOS v0.25.2 (from scratch)

**Tester:** ____________. **Date:** ____________. **PC / Windows version:** ____________.
**Build (title bar):** ____________.

You have never used TempestOS before, and that is the point: we want to know what a newcomer finds confusing or broken. No setup, accounts or Xero connection is needed. Allow **about 90 minutes**.

## What TempestOS is (30 seconds)

TempestOS is a desktop app for a small engineering consultancy. It covers:

- customers and projects;
- quotes, invoices and timesheets;
- the engineering work itself: requirements, calculations, documents and evidence.

The left-hand **rail** switches between **Home, Projects, Tasks, Engineering** and **Business**.

## How to score

- Fill *Result* with `PASS`, `FAIL` or `SKIP (why)`.
- A **FAIL** is anything that differs from *Expect*, crashes, freezes, or shows you gibberish (a raw error, a long code like `3f2a…-…`).
- In *Comments*, write what you did and what you saw, word for word. Also take a screenshot with **Win+Shift+S**, named with the step id (e.g. `P3.png`).
- **Confusing counts.** If something works but you had to hunt for it, say so in *Comments*.
- Don't fix your way round a problem silently: note it, then carry on if you can.

---

## 1. Install (10 min)

1. Open <https://github.com/kreczmans-creator/TempestOS-Core/releases/tag/v0.25.2>.
2. Download `TempestOS-v0.25.2-Setup.exe` and `SHA256SUMS.txt`.
3. Optional hash check. In PowerShell run `Get-FileHash .\TempestOS-v0.25.2-Setup.exe`; the result should match the line in `SHA256SUMS.txt`.
4. Run the Setup.exe. Windows SmartScreen may warn about an unknown publisher: **More info → Run anyway**.

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| I1 | Run the installer to the end | No error | | |
| I2 | Launch TempestOS from the Start menu | A **Where should your data live?** dialog. Pick a new empty folder, e.g. `C:\TempestOS-test`, and continue. The window opens on **Home** within about 10 s, and the title bar reads `TempestOS 0.25.2` | | |
| I3 | Close the window (X) and launch again | Opens straight onto Home with no dialog and no error | | |

---

## 2. Find your way around (10 min)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| F1 | Click each rail entry in turn: Home, Projects, Tasks, Engineering, Business | Each opens a page. Nothing is blank, cut off or overlapping | | |
| F2 | Press **Ctrl+K**, type *project*, then click anywhere outside the box | A command search box appears and lists matching commands. Clicking outside closes it | | |
| F3 | Click the ◐ button in the header | Switches light/dark theme. Both are readable | | |
| F4 | Make the window narrow (half the screen), then wide again | The layout adapts. Nothing becomes unusable | | |

---

## 3. A customer and a project (15 min)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| P1 | Business → **Customers & Suppliers** → add a customer: name *Testy Ltd*, code **TESTY** | Saved, and listed | | |
| P2 | Add a contact to Testy Ltd (any name and email) | Saved on the customer | | |
| P3 | Add a second customer with the **same** code TESTY | Refused, with a message saying why | | |
| P4 | Projects → **New Project…** → client *Testy Ltd* → name *Trial Bridge* | Created with a reference beginning `TESTY-` and opens | | |
| P5 | In the project, find its **project folder** on disk (the project page shows or opens it) | The folder exists and holds the numbered set: `01 Commercial`, `02 Customer Data`, `03 Requirements`, `04 Design`, `05 Documents`, `06 Evidence & Verification`, `07 Reports`, `08 Correspondence`, `09 Issued to Customer`, `10 Sign-off & Handover`, `99 Superseded` | | |

---

## 4. Quote, invoice, exports (20 min)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| Q1 | In *Trial Bridge*, create a quote with two lines (any description, quantity, rate) and save it | Numbered `TESTY-…-Q-001`. Totals add up, with VAT if shown | | |
| Q2 | Export the quote to PDF | The save dialog opens in the project's `01 Commercial\01 Quotes` folder. The suggested name is `TESTY-…-Q-001-R1-quote.pdf`. Open the PDF: it is readable and the figures match | | |
| Q3 | Export the same quote again | The name now ends `-R2-…`. Cancel instead of saving, then export again: still `-R2-` | | |
| Q4 | Close TempestOS, reopen, and export the quote again | Carries on from where it was (`-R2-`, or `-R3-` if you saved R2). It never goes back to R1 | | |
| Q5 | Business → **Invoices** → create an invoice for *Trial Bridge* and export it | Opens in `01 Commercial\04 Invoices`. The file is named by the invoice number with `-R1-` | | |
| Q6 | Business → **Timesheets** → record a few hours against *Trial Bridge* this week → export the week | Hours add up. The file name starts `TSHT-` | | |

---

## 5. Engineering (20 min)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| E1 | Rail → **Engineering** | A tree on the left: Dashboard, Tasks, **Disciplines** (Mechanical, Structural, Electrical, Building Services / HVAC, Manufacturing) and **Modules** (Calculations, Requirements, Verification, Documents, Evidence, Reference data) | | |
| E2 | Modules → **Requirements** → **Create Requirement** → name it | Created and opens. The tree stays on the left | | |
| E3 | Disciplines → **Mechanical** → **Create Mechanical Object** → Kind **Part** → name it | Created and listed in the Mechanical Explorer | | |
| E4 | Disciplines → **Structural** → **Create Structural object** → Kind *StructuralMember* → name *Beam B1*. With it selected → **Actions ▾ → Set member attributes** → fill every prompt | Properties shows every value you entered. Close and reopen TempestOS: they are still there | | |
| E5 | Modules → Calculations → **Calculators** → *Bolt shear capacity*. Bolt diameter dropdown **M20**, shear strength dropdown **Class 8.8**, shear planes 2, safety factor 1.5 → Calculate | Boxes fill with 20 and 480. Result **201062 N**. The dropdowns list sizes smallest to largest (M3, M4 … M36) | | |
| E6 | Same calculator: type `abc` as the diameter → Calculate | Says the diameter is not a number. Nothing crashes | | |
| E7 | Try to get back to the Engineering **Dashboard** from anywhere above | One click, from the tree | | |

---

## 6. Safety and general feel (10 min)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| G1 | Delete something you created (e.g. the requirement) | Asks you to confirm and names the item | | |
| G2 | Press **Ctrl+Z** straight after | It comes back, or the status bar says this delete can't be undone | | |
| G3 | Close TempestOS with things open, then relaunch | Everything you made is still there | | |
| G4 | Anywhere: did you see a crash, a freeze, a raw error or a long code? | No. List each one under *Comments* | | |

---

## 7. Your own exploration (as long as you like)

Poke at anything that looks interesting. For each problem, add a row:

| # | Where (screen / button) | What you did | What happened | What you expected | Screenshot |
|---|---|---|---|---|---|
| X1 | | | | | |
| X2 | | | | | |
| X3 | | | | | |

---

## Verdict

| Question | Answer |
|---|---|
| Steps that FAILED | |
| The three most confusing things | 1. 2. 3. |
| Would you trust it with real project data? (yes / not yet / no, and why) | |

**Send back:**

- this sheet, or the **Export results** file if you used the HTML version;
- your screenshots;
- the folder `C:\TempestOS-test\logs`, zipped, if anything crashed.
