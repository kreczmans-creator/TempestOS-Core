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
| `WP 21.2A` Documents from the templates: invoice, purchase order, timesheet, technical report, drawing register, progress report | `IDocumentRenderer<TModel>` — one contract every document renderer (the six new ones, and `QuotationSheetRenderer`/`IssueSheetRenderer`, retrofitted) satisfies — and `DocumentExporter`, naming a file `<reference>-<template>.pdf` through the same file-picker path the Quote tab's own Export already uses. Six new renderers against the design system's own template folders (`docs/design/templates/README.md`'s own mapping, extended): the invoice (net only — `WP 21.3B`'s VAT fields are not in this Work Package's own base), the weekly timesheet, the technical report (a Document's own revisions and content, split into sections by leading `#` markers), the drawing register and the progress report (both A4 landscape — `DocumentTemplate` gained landscape page geometry, additive and backward compatible with every existing portrait caller), and the purchase order (model-less, against a fixture only — the real `PurchaseOrder` Kind, `WP 21.3B`, lives only under `src/Frozen/` in this Work Package's own base, so no live "Export PO" button exists). The design system's three type families (Chakra Petch, Inter, Space Mono) and the horizontal navy lockup are embedded as `Tempest.Desktop` resources and loaded through `SKTypeface.FromStream`/`SKBitmap.Decode` — no running Avalonia application needed — closing `WP 20.10G`'s own disclosed "still the platform default face, still no logo" gap; Chakra Petch and Space Mono draw real, correctly-extractable PDF text (verified — and a genuine bug found and fixed in the test-only `PdfTextExtractor` along the way: it merged every embedded font's own CID space into one dictionary, corrupting text extraction once more than one custom font could appear in the same document), Inter stays the platform default (SkiaSharp's PDF backend does not embed a variable-format `SKTypeface` as extractable text at all — verified empirically, not assumed). Settings → Organisation gained a **Bank details** section (sort code, account number, account name, IBAN) for the invoice's own "Payment details" section. Six buttons wired where a user expects them: Business → Invoices **Export invoice**, Business → Timesheets **Export week**, project → Documents **Export register**, a Document's editor **Export as report**, Projects Dashboard **Export progress report** (every open project now lists there, not only Blocked/At risk/Ready to invoice). `PHYSICAL_REVIEW.md` §7e. | *(pending — `wp/21.2A`, not yet merged)* |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
