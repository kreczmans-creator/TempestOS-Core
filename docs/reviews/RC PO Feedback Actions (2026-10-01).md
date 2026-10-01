# RC Product Owner feedback — action register (2026-10-01)

Source: the PO's RC runbook results (claude.ai "TempestOS RC Runbook"
artifact, last saved 22 Sep; transcribed in `4cf47c1`,
`docs/reviews/outstanding-actions-2026-09-25.html`) plus the PO's
follow-up messages on 1 Oct. Score: 46/64 scored — 37 PASS, 2 FAIL
(D9, F8), 8 SKIP; F1–F7 not yet run.

## Done on `claude/focused-dirac-k0qilf`

| Ref | Item | Change |
|---|---|---|
| PO-1 | Home right rail (Continue/Recent/Favourite/Recently changed) were read-outs | Continue entries are buttons that open the project; the other three now navigate to Engineering before opening, so the click visibly does something |
| PO-3 | Module landing pages named inconsistently | "Dashboard + Reports" / "Dashboard & Reports" → **Dashboard** everywhere; Home's title reads Dashboard. Reports withdrawn from the shell (`ReportsView` kept for a later build) |
| D9 | Ctrl+K didn't open search; clicking the search bar "left it hanging" | Ctrl+K is a tunnelling handler that a focused control can't swallow; clicking the header search opens the palette with the cursor in it |
| F (Xero) | Xero sign-in would be refused for a new Xero app | Apps created since 2 Mar 2026 can't use `accounting.transactions`; TempestOS now asks for `accounting.invoices accounting.contacts.read accounting.reports.banksummary.read` (Bank Summary also needed a reports scope it never asked for) |
| F (Xero) | Needs a line-by-line guide | `docs/guides/Xero Setup - Step by Step.md` |
| PO-5 | "Create XXX" Kind was a typed, restricted field | Every fixed-list command parameter (Create Document's Kind and every other `Choice`/`EnumChoice`) is now a dropdown |

## Already in hand elsewhere (do not duplicate)

| Ref | Item | Where |
|---|---|---|
| F1 | Port 49301 is in Windows' dynamic range | `TD-183`, moved to 48131 on `claude/next-stretch-j1fqz2` |
| F8 | First-run data-location dialog hangs from the Start menu (desktop shortcut works) | `C-01` in the 25 Sep register (`App.ShowFirstRunDialogBlocking` blocks the UI thread before the main loop) |

## Needs a PO decision before building

| Ref | PO comment | Proposal | Decision needed |
|---|---|---|---|
| B2 | "Add a person — internal staff or quote contacts? Should be clients, with the company" | Split: **People** (internal staff, rate-card grades) stays in Reference data; **Contacts** belong to an Organisation under Business | Confirm the split |
| B3/B4 | "Client should be under Business — a customer/supplier management section" | New Business → **Customers & Suppliers** node over the existing Organisation catalogue (`OrganisationPicker`), with contacts per organisation; the New Project picker keeps working from it | Confirm name and whether suppliers share the list |
| B3 | "Project numbers derived from customer: 5-letter customer ID – 5-letter project name – Q – 001" | Organisation gets a unique 5-letter code; project identifier = `CUSTO-PROJN`; quote reference = `CUSTO-PROJN-Q-001`, sequence per project | Is the sequence per project or per customer? What happens to existing `P-…` numbers? |
| B7 | "Create a standard folder structure in Windows Explorer per project; export quotes straight into its Quote folder" | On project creation, create `<root>\<project id>\{01 Quote, 02 Engineering, 03 Drawings, 04 Correspondence, 05 Invoices}`; Quote Export saves there by default | Root folder location, and the folder list |
| B7 | "Quote template needs the light Tempest logo, not the dark one" | The PDF uses `tempest-logo-horizontal-navy.png` (navy on white). The only light variant is the app's `tempest-os-logo-light.png` | Confirm you mean the white/light artwork on a dark header band (needs a template change) — or the light-theme (navy) wordmark |
| C8 | "Need materials database" | S355J2 and five others are seeded but **Draft** until released in Reference data. Options: release the seed set by default, and/or grow the library (EN 10025 / 10088 / 573 grades) | Which grades, and whether seeded data is released automatically |

## Not yet run — rerun after this build

D10–D13 (move/undo/attachments), E5–E12, F1–F7 (follow the Xero guide), F8 after `C-01`.

## Calculator reference diagrams (PO-2) — scope

No earlier written record of this was found; this entry records it.

- **Before:** 16 calculations (`CalculationModuleDescriptors.All`), forms
  generated from descriptors in `CalculationModulesView`; nothing drawn.
- **Now (`ADR-0158`):** 19 calculations, **19 drawn, 0 without a
  diagram.** A declarative 2D spec per calculation in Core, drawn by one
  Desktop control beside the inputs: redrawn as inputs change, field and
  shape highlighted both ways, labels `L = 2000 mm` / `L = ?`, marked
  "not to scale · inputs only". "No diagram yet" remains only as a
  defensive fallback.
- **The PO's rule (2026-10-01):** "for future calculations the 2D diagram
  will be a necessary item to consider it complete" — Engineering
  Principle 33, `CONTRIBUTING.md` Definition of Done. The coverage test
  fails for any calculation without exactly one diagram; the shrink-only
  list is gone.

| WP | Content | Status |
|---|---|---|
| A | Spec model, variants, coverage test | Done |
| B | Diagram control: shapes, dimension arrows, labels, theming, fallback, screen-reader text | Done |
| C | Wire into the calculator page: live redraw, two-way highlight, layout | Done |
| D1 | Simple diagrams: beam bending/deflection, column buckling, bolt shear, bearing at a hole, thermal expansion, shaft, pressure vessel (plus plane wall, resistance chain, tolerance stack) | Done |
| D2 | Hard diagrams: bolt group, lifting lug, fillet weld, thick cylinder, bolted-joint preload | Done |
| D3 | Charts not geometry: bearing life, Miner S-N, material margin | Done |
| E | Same diagram on the recorded run and the calc-sheet export | Open (waits on Q5) |

**PO answers (2026-10-01):** (1) not to scale, everywhere — lugs
included; (2) a fixed panel beside the inputs; (3) charts are acceptable
for non-geometric calculations; (4) inputs only — no deflected shape, no
stress marker. **Still open:** (5) whether the diagram goes on the issued
calc sheet (WP E).
