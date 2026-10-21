# Product Owner Test Runbook — v0.25.2 re-walk

**Tester:** ____________. **Date:** ____________. **Build (title bar):** ____________.
**Data folder:** keep the `v0.25.1` folder (`C:\TempestOS-rc25-data`). Only the failed steps and the new behaviour are walked.

## How to score

As before. Fill *Result* with `PASS`, `FAIL` or `SKIP (why)`. Quote what you saw in *Comments*. A gate is **GO** only when every blocking step in it passes.

---

## A. Install

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| A1 | Install `v0.25.2` over `v0.25.1`, then launch | Title bar reads **0.25.2**. Your `v0.25.1` data (projects, quotes, invoices) is all still there | Yes | | |

**Gate G0 — Installable: GO / NO-GO**

## N. Engineering (re-walk of N11, N16)

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| N11 | Open a project → **Structure** → pick **Mechanical** → **Create Mechanical Object** → Kind **Part** → name it | The Part is created under the open project, lists in the Mechanical Explorer and opens right up. (It is a Part, not a Requirement, so it does not show under Requirements.) | Yes | | |
| N16 | Structural → select the beam → **Actions ▾** → **Set member attributes** | You are asked for each attribute in turn (Member type, Section, Material, Grade, Length (mm)). Fill them all → Properties shows every value. Run it again and leave all but one blank: only that one changes. Close and relaunch: the values are still there | Yes | | |

**Gate G1 — Navigation: GO / NO-GO**

## E. Exports: names, revisions, folders (new)

Use a project with an identifier (e.g. *TESTY-TRIALX*) that has a project folder.

| # | Action | Expect | Blocking | Result | Comments |
|---|---|---|---|---|---|
| E1 | Create a **new** project with a folder → open the folder in File Explorer | The standard set: `01 Commercial` (01 Quotes, 02 Customer PO & Contract, 03 Purchase Orders, 04 Invoices), `02 Customer Data`, `03 Requirements`, `04 Design` (01 CAD, 02 Drawings, 03 Calculations), `05 Documents`, `06 Evidence & Verification`, `07 Reports`, `08 Correspondence`, `09 Issued to Customer`, `10 Sign-off & Handover`, `99 Superseded` | Yes | | |
| E2 | Export a quote PDF | The save dialog opens in `01 Commercial\01 Quotes`. File `<project>-Q-<n>-R1-quote.pdf` | Yes | | |
| E3 | Export the **same** quote again | Suggested name ends in **-R2-**. Cancel the dialog, export once more: still **-R2-** (a cancel uses no revision) | Yes | | |
| E4 | Invoicing → export an invoice (was X6) | The dialog opens in `01 Commercial\04 Invoices`. The file is named by the **invoice number** (the same as in Xero) plus `-R1-` | Yes | | |
| E5 | Close and relaunch TempestOS, then export the E3 quote again | **-R3-**: revisions survive a restart | Yes | | |
| E6 | Projects dashboard → export a progress report | Opens in `07 Reports`. File `<project>-RPT-001-R<n>-…pdf` | No | | |
| E7 | Project → drawing register export | Opens in `04 Design\02 Drawings`. File `<project>-REG-001-R<n>-…pdf` | No | | |
| E8 | Timesheet → export the week | File `TSHT-<you>-<week start>-R<n>-…pdf` | No | | |
| E9 | Issue evidence (an issue sheet attachment) | Attachment `<project>-EVI-<ref>-R<rev>-issue-sheet.pdf` | No | | |

**Gate G5 — Exports: GO / NO-GO**

- **No-Go triggers:**
  - An exported file overwrites an earlier revision.
  - A file is saved outside the project folder without you choosing to.

## C / R. Spot checks (Could)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| C1 | Calculators → *Bolt shear capacity* → **Bolt diameter** dropdown **M20**, **Ultimate shear strength** dropdown **Class 8.8 (fub 800)**, 2 planes, safety factor 1.5 → Calculate | The boxes fill with 20 and 480. Result **201062 N**. Typing your own value still works | | |
| C1a | Calculators → *Bearing load capacity at a hole* | **Hole diameter** offers M3 (3.4) … M36 (39), and **Plate thickness** offers 3 … 50 mm | | |
| C1b | Calculators → *Bolted joint preload* → open the fastener picker; also Engineering → Reference data → Fasteners | Listed smallest to largest: M3, M4, M5, M6, M8, M10, M12 … never M10 before M3 | | |
| R1 | Ctrl+K, then click anywhere outside the palette | The palette closes. A click inside it does not close it | | |

---

## Decision

| Gate | Blocking? | GO / NO-GO / OPEN | Failing steps |
|---|---|---|---|
| G0 Installable | Yes | | |
| G1 Navigation | Yes | | |
| G5 Exports | Yes | | |
| G4 Xero — X10 only | Yes, once a live organisation is connected; OPEN until then | | |

**Verdict:**

- If every blocking gate is GO, `v0.25.2` is accepted.
- Signed ____________ date ____________
