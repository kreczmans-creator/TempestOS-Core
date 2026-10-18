# TempestOS v0.24.0 — Xero Integration Plan

**Status: scoped, not started.** Product Owner decisions of 2026-10-02.
Starts once `v0.23.0` is accepted.

## Principle

TempestOS is the master for commercial work: quotes, deliverables,
milestones, purchase orders and expenses. Xero is the master for
anything that reaches the accounts. Every document that leaves
TempestOS gets an identical copy in Xero, with the same number and the
same PDF. Nothing is posted to the ledger, and nothing is sent to a
client, without the Product Owner acting in Xero.

## Product Owner decisions (2026-10-02)

| # | Decision |
|---|---|
| D1 | TempestOS stays the place where quotes are written, approved and accepted; acceptance generates deliverables for invoicing and milestone payments. |
| D2 | Xero holds a read-only copy of each quote, PDF attached, whose status follows TempestOS (sent, accepted, declined). |
| D3 | Invoices are created in Xero as **DRAFT** for the Product Owner to review. TempestOS never approves (`AUTHORISED`) an invoice. |
| D4 | The Product Owner **sends invoices manually from Xero**. TempestOS never emails a client. |
| D5 | Purchase orders and expenses also go to Xero, so the accounts hold everything. |
| D6 | Xero is the source of truth for the company's own details, VAT rates and account codes; TempestOS reads them. |
| D7 | Testing happens against the Xero Demo Company before the live organisation is connected. |

D3 and D4 can be revisited once the system has proved itself; any
later switch to approving or sending from TempestOS is a new decision,
not a setting added now.

## Facts this plan relies on

- Accepting a quote in Xero does not change Xero's cash flow or
  forecasts; only invoices, bills and bank transactions do. The forward
  cash picture ("accepted, not yet invoiced") therefore belongs in
  TempestOS (X7).
- Today's connector (`src/Tempest.Core/Invoicing/Xero/XeroConnector.cs`)
  already creates a **DRAFT** sales invoice (`ACCREC`), idempotent on
  the TempestOS reference, reads its status back, and reads bills,
  repeating bills and bank balances. Contacts are matched by **name
  only**, and the scope is `accounting.contacts.read`, so an invoice
  for a brand-new client may be refused (review board item M21).
- Nothing reaches Xero for quotes, purchase orders, expense bills, or
  attachments. Company details and VAT codes come from TempestOS's own
  settings and `VatRateTaxTypeMapping`, not from Xero.
- `InvoicingService.RaiseFromExpenseAsync` recharges an expense to a
  client; it does not record the expense as a bill. That is new in X5.

## Work packages

| WP | Content | Size |
|---|---|---|
| X0 | **Scopes and Demo Company.** Confirm against Xero's current developer documentation which granular scopes cover quotes, purchase orders, attachments, contact creation and organisation settings, and add only those to `TempestHost`'s profile. Re-authorisation prompt when the stored grant lacks a new scope. Runbook and setup guide steps for connecting to the Demo Company. | S |
| X1 | **Read company, VAT rates and account codes from Xero** (D6). Organisation name, address, VAT and company numbers feed quote, invoice and timesheet PDFs, with a "from Xero, read at …" note in Settings. Tax rates and account codes are read and cached; `VatRateTaxTypeMapping` checks against them, so a line can never post a code Xero lacks. Works offline from the last reading. | M |
| X2 | **Customer and supplier links.** Each organisation stores its Xero `ContactID`. First use either links to an existing Xero contact (offered by name and VAT number, confirmed by the PO) or creates one. Billing address, VAT number and payment terms are then read from Xero, never pushed. Fixes M21. | M |
| X3 | **Quotes to Xero** (D2). Exporting an approved quote creates or updates the Xero quote with the same number, lines and expiry, status SENT, PDF attached. Accept and decline in TempestOS set the Xero status; a new revision Rn updates the Xero quote and replaces the attachment. Never sent to the client by Xero. | M |
| X4 | **Invoices to Xero** (D3, D4). Exporting an invoice creates the Xero **DRAFT** with the same invoice number as TempestOS, linked to the contact (X2), PDF attached, the deliverable and project in the reference. TempestOS shows "Draft in Xero — review and send from Xero", then reads back Awaiting payment, Paid or Voided. Editing in TempestOS after the draft exists updates the draft only while Xero still holds it as a draft; otherwise it is refused with the reason. | M |
| X5 | **Purchase orders and expense bills** (D5). An approved PO creates a Xero purchase order (same number, supplier contact, PDF). A recorded expense creates a Xero **draft bill** (`ACCPAY`) with the receipt attached and the account code picked from X1. Recharges to clients (`RaiseFromExpenseAsync`) are unchanged. | M |
| X6 | **Sync status and failure handling.** One "Xero" column or badge on quotes, invoices, POs and expenses: Not sent, In Xero (draft), Awaiting payment, Paid, Failed (with the reason and a Retry). Every push is idempotent and audited; nothing is lost when Xero is unreachable — it queues and retries on the next refresh. | M |
| X7 | **Forward cash picture in TempestOS.** Business dashboard combines Xero actuals (bank, bills, invoices due) with accepted-but-not-invoiced milestones from TempestOS, by month. | S/M |
| X8 | **ADR, runbook and acceptance.** ADR recording D1–D7 and the source-of-truth table; a runbook section that walks quote → accept → invoice draft → PO → expense bill against the Demo Company and checks each record in Xero; Release Notes. | S |

Order: X0 → X1 and X2 in parallel → X3, X4, X5 in parallel → X6 → X7 →
X8. X4 depends on X2 (contacts); X5's bills depend on X1 (account codes).

## Source of truth

| Data | Master | Other side |
|---|---|---|
| Company details, VAT number, bank details | Xero | TempestOS reads |
| VAT rates, chart of accounts | Xero | TempestOS reads |
| Customers and suppliers (existence, customer code) | TempestOS | Linked to a Xero contact |
| Contact billing details and payment terms | Xero | TempestOS reads |
| Quotes, revisions, acceptance | TempestOS | Read-only copy in Xero |
| Invoices (content, number) | TempestOS creates | Xero draft; the PO approves and sends in Xero |
| Invoice payment status | Xero | TempestOS reads |
| Purchase orders | TempestOS | Copy in Xero |
| Expenses | TempestOS records | Draft bill in Xero |

## Out of scope for v0.24.0

- Approving or emailing invoices from TempestOS (D3, D4).
- Two-way editing of contacts beyond the X2 link.
- Payroll, bank reconciliation, and credit notes (a voided TempestOS
  invoice voids its Xero draft only).
- QuickBooks Online parity; the QuickBooks connector stays as it is.
