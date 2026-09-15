# Design system templates — which renderer maps to which template

The Product Owner's own Claude Design export (`WP 20.10`, commit
`88311649` on `release/v0.20.0`) carries one `.dc.html` template per
document under `docs/design/templates/<folder>/`, and the system itself
(tokens, components, brand assets, fonts) under `docs/design/system/`.
This Work Package (`WP 20.10G`, PO finding D4) built
`src/Tempest.Desktop/Documents/DocumentTemplate.cs` — the shared page
grammar every TempestOS-rendered document uses — from that export's own
page grammar, before this branch's own worktree carried the export
itself; see `DocumentTemplate`'s own remarks for the exact colours,
hairlines and header/footer shapes it reproduces.

At the time of writing, this worktree (`wp/20.10G`, based on
`release/v0.20.0` at `d1fef00b`) does not itself carry the export — the
merge that would bring `88311649` in was blocked by this session's own
permission system ("Modify Shared Resources"); see this Work Package's
own report for the full account. The mapping below is what
`DocumentTemplate`'s two current callers render against once this folder
is populated (already true in the release branch's own tip and in the
Product Owner's design system export, both read directly for this
mapping).

| TempestOS renderer | Template folder | Why |
|---|---|---|
| `Quotations/QuotationSheetRenderer.cs` (the quotation sheet, PO finding D4) | `templates/cost-estimate/CostEstimate.dc.html` | The closest shape to a quotation's own lines/total/terms: an itemised table with a "Rate"/"Amount" pair of right-aligned, mono-styled numeric columns and a shaded, indigo-ruled totals block — the same grammar `QuotationSheetRenderer`'s own Lines table and Total line now reproduce. The header band (wordmark, eyebrow document type, mono reference line under a 2px indigo rule) and the footer (organisation identity, left/right split) are common to every template in the export, `cost-estimate` included. |
| `IssueSheets/IssueSheetRenderer.cs` (the issue sheet) | `templates/letterhead/Letterhead.dc.html` | The plainest, most general template in the export — a running header and footer on every page, no document-specific body grammar of its own — matches the issue sheet's own shape (project/client/evidence identity, a Review section, two tables, a Signatures block) better than a template built around one specific body (an invoice's VAT summary, a change order's approval block): the issue sheet needed the shared page furniture, not another document's own body layout. |
| `WP 20.10E`'s change-order kind (quotation, rendered through the same renderer as above; see that Work Package's own report) | `templates/change-order/ChangeOrder.dc.html`, when this Work Package's own renderer is extended to read it | Named by the brief as the template to match once a `Kind` exists on the quotation model to render against it; this Work Package did not add that property (out of its own "files you own" list — quotation domain code) and rendered the reference and "QUOTATION" as today in that case, per its own kill switch. |
| `Documents/Invoicing/InvoiceDocumentRenderer.cs` (the invoice, `WP 21.2A`) | `templates/invoice/Invoice.dc.html` | The named mapping — an itemised table, a totals block and the design system's own payment-terms/bank-details layout are exactly this template's own shape. Net only: `WP 21.3B` (VAT) is not in this Work Package's own base. |
| `Documents/PurchaseOrders/PurchaseOrderDocumentRenderer.cs` (the purchase order, `WP 21.2A`) | `templates/purchase-order/PurchaseOrder.dc.html` | The named mapping. Model-less — rendered against a fixture only; the real `PurchaseOrder` Kind (`WP 21.3B`) is not in this Work Package's own base (`src/Frozen/` only), so no live "Export PO" button exists anywhere in the shell — disclosed in this Work Package's own report. |
| `Documents/Timesheets/TimesheetDocumentRenderer.cs` (the weekly timesheet, `WP 21.2A`) | `templates/timesheet/Timesheet.dc.html` | The named mapping — hours by project and day, totals, and an approval block; the approval signature rows are blank (`TimesheetEntry` carries no real approver to draw instead). |
| `Documents/TechnicalReports/TechnicalReportDocumentRenderer.cs` (the technical report, `WP 21.2A`) | `templates/technical-report/TechnicalReport.dc.html` | The named mapping — cover block, revision history (`IEngineeringDocumentStore.GetRevisionHistoryAsync`), numbered sections split from the current revision's own content by leading `#` markers. |
| `Documents/DrawingRegisters/DrawingRegisterDocumentRenderer.cs` (the drawing register, `WP 21.2A`) | `templates/drawing-register/DrawingRegister.dc.html` | The named mapping — A4 **landscape** (`DocumentTemplate.LayoutState.Landscape`, added this Work Package): number, title, revision, status, issue history, one row per project document. |
| `Documents/ProgressReports/ProgressReportDocumentRenderer.cs` (the progress report, `WP 21.2A`) | `templates/progress-report/ProgressReport.dc.html` | The named mapping — A4 **landscape**, one section per page (the design system's own "deck" grammar: RAG summary, cost position, risks, four-week look-ahead). Deliverable-level progress is stated as unavailable rather than invented — see the renderer's own remarks. |

## The fidelity test

`tests/Tempest.Desktop.Tests/Documents/DesignSystemTemplateFidelityTests.cs`
asserts, once `docs/design/system/tokens/colors.css` and
`tokens/typography.css` exist in the tree the test runs against, that
`DocumentTemplate.ReferenceColourTokens`' own hex values and
`ReferenceTypeNames`' own family names are genuinely present in those
files (not merely in the reference document `DocumentTemplate` was built
from), and that each renderer's own mapped template folder above really
exists. Until then, every assertion is a documented no-op (xunit 2.9.3 has
no runtime-conditional "Skipped" outcome available without a package this
Work Package's brief does not name; the reason is written to the test's
own output instead) — `WP 21.2A` found both token files, and every
template folder the table above names, genuinely present in this
worktree, so these assertions are live rather than no-ops here; the
no-op path stays for a future worktree that regresses to missing them.
`WP 21.2A` also added `EveryRenderer_TemplateNameProperty_MatchesTheMappingTable`,
a compile-time pin (unlike the disk-existence theory above) that each of
its own six new renderers' `IDocumentRenderer{TModel}.TemplateName`
property agrees with the row this table states for it.
