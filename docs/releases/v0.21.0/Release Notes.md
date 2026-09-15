# TempestOS v0.21.0 — Release Notes

**Status: in preparation on `release/v0.21.0`, cut from the `v0.20.0`
candidate (`88311649`) on 2026-09-15 at the Product Owner's instruction
to close every technical weakness named in the lead's assessment of that
afternoon.** Nothing in this document is certification. `v0.20.0` stays
under the Product Owner's manual test as the candidate, receiving the
`WP 20.10A`–`20.10G` fixes; every one of those is merged here too.

## Summary

**v0.21.0 is the recovery tranche** — the docking rewrite to `ADR-0153`,
Undo across commands, the editor split, documents from every template,
the Engineering Assets surfaces, typed calculation results with retained
inputs, the commercial edges, the viewer's remaining formats and markup,
the installer with upgrade, backup and restore, lazy rehydration, a
real-shell run in CI, and the mutation threshold met. See
`Execution Plan.md` for the packages and their waves.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 21.3B` Commercial edges: expenses, purchase orders, VAT on lines, a second principal signs in to check | `ProjectExpense` — date, description, a closed category (Travel/Subsistence/Materials/Subcontract/Other), net and VAT amounts, a receipt attachment, billable, `InvoicedBy` set once — recorded from Business → Timesheets ("Record expense…" beside Record) and the project's own Details tab; a billable expense becomes an invoice request line exactly as a timesheet entry does, and shows in Business → Invoices' own "Available to invoice". `PurchaseOrder` — `PO-<yyyy>-<nnn>` (the identical scan-the-store discipline as `Q-`/`CO-`), a supplier, lines with net and VAT, Draft → Issued → Received → Closed \| Cancelled, one act ("Record as expenses") turning a received order's own lines into project expenses with no ledger of its own; Business gains a **Purchase orders** entry, grouped New/Issued/Received/Closed, with New Purchase Order…/Add line…/Issue/Receive/Close/Cancel/Record as expenses. `VatRate` (Standard 20%, Reduced 5%, Zero, Exempt, Out of scope — the enum's own default, so every line recorded before this Work Package reads unchanged) on `QuotationLine`/`InvoiceRequestLine`, each with a computed VAT amount; `Quotation`/`InvoiceRequest` gain net/VAT/gross totals; Settings → Organisation identity carries the consultant's own default rate for a new line. The Xero/Fake connector seam maps the five rates to Xero's own tax types (`OUTPUT2`/`RROUTPUT`/`ZERORATEDOUTPUT`/`EXEMPTOUTPUT`/`NONE`) and refuses a send whose rate cannot be expressed, naming the line. Settings → Principal becomes a real sign-in: **Switch person…** lists every released person the People directory (the `IPeopleDirectory` seam, standing in for `WP 20.10F` where it has not yet merged) carries a known identity for, confirms by name with no password, and publishes them as the session's own principal from that moment on; the Evidence Check refusal for a checker who is also the recorder now names the fix directly ("An independent check needs a second person; switch person first."). `ADR-0150` addendum. | `wp/21.3B` → `release/v0.21.0` |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
