# TempestOS v0.25.2 — Release Notes

A patch on `v0.25.1`, from the Product Owner's walk of the `v0.25.x` runbook (2026-10-21).
Re-walk: `docs/releases/v0.25.2/PO Test Runbook.md`.

## Fixed

- **N16: one command sets every attribute.**
  - Each discipline object kind now has **Set *kind* attributes**, e.g. *Set member attributes*.
  - It asks for each attribute in turn. A blank answer keeps the current value.
  - Code: `DisciplineWorkspaceRegistration.cs`, `DisciplineObjectCommands.cs`.
- **R1: the Ctrl+K palette closes on a click outside it.** Code: `CommandPaletteOverlay.cs`.
- **X6: an exported invoice uses its invoice number.** The file name and the Xero number now match.
- **N11: runbook wording.** The step now says *Create Mechanical Object → Kind Part*. The app was right.

## Changed

- **Every exported file carries its project context and a revision.**
  - The name is `<project>-<designator>-<number>-R<rev>-<template>.pdf`, e.g. `TESTY-TRIALX-Q-001-R1-quote.pdf`.
  - The same reference exported again gets the next revision: `R2`, `R3` and so on, and this survives a restart.
  - Cancelling the save dialog uses no revision.
  - A technical report takes its document's own revision.
- **New designators.**
  - Register `REG`, report `RPT`, evidence `EVI`, timesheet `TSHT`.
  - These join `Q`, `CO`, `PO`, `INV`, `CAD`, `DWG`, `CALC` and `DOC`.
- **Standard project folders.**
  - A new project folder gets the standard set: `01 Commercial` (Quotes, Customer PO & Contract, Purchase Orders, Invoices), `02 Customer Data`, `03 Requirements`, `04 Design` (CAD, Drawings, Calculations), `05 Documents`, `06 Evidence & Verification`, `07 Reports`, `08 Correspondence`, `09 Issued to Customer`, `10 Sign-off & Handover` and `99 Superseded`.
  - Each export's save dialog opens in its own folder. For example, an invoice opens in `01 Commercial\04 Invoices` and a progress report in `07 Reports`.
  - A configured quote subfolder still wins.
- **C1: calculators offer standard parts as dropdowns.**
  - **Bolt shear:**
    - Diameter M3–M36 (ISO 261).
    - Shear strength by property class 4.6–12.9, taken as 0.6 × fub (ISO 898-1).
  - **Bearing at a hole:**
    - Clearance hole M3–M36 (ISO 273 medium).
    - Plate thickness 3–50 mm.
  - Picking a value fills the box, and you can still type a value of your own.
- **Standard-part pickers list smallest to largest.** Fastener, material and bearing pickers, and the Libraries list, now sort numbers by value (M3, M4, M5 … M10, M12), not as text (M10, M12 … M3).

## Not changed

- X10 (the live-organisation demo guard) is still to be walked once a live organisation is connected.
