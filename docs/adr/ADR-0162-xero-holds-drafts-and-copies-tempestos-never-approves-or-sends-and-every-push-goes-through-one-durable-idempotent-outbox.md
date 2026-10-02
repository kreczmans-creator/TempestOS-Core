# ADR-0162: Xero Holds Drafts and Copies; TempestOS Never Approves or Sends; Every Push Goes Through One Durable, Idempotent Outbox

## Status

Accepted — 2026-10-02, at the end of work package X8. Product Owner
decisions D1–D7 of 2026-10-02 (`docs/releases/v0.24.0/Xero Integration Plan.md`);
proposed before the build, and every rule below is now built and guarded
by tests (the safety handler's own tests, the in-process Xero simulator's
`Violations` log on every end-to-end test, `XeroNeverApprovesOrSendsTests`
and `XeroSafetyArchitectureTests`). The live confirmation against the Xero
Demo Company is the operator-run smoke test
(`scripts/xero-demo-smoke.ps1`, `Category=XeroLive`) and the Product
Owner's runbook (`docs/releases/v0.24.0/PO Test Runbook - Xero.md`); their
results are recorded in `docs/releases/v0.24.0/Release Notes.md`. A
finding there that contradicts this decision reopens it as a new ADR.
Technical detail: `docs/releases/v0.24.0/Xero Technical Design.md`;
build defaults Q1–Q10: `docs/releases/v0.24.0/Xero Build Decisions.md`.

Extends `ADR-0151` (the connector seam: a result, never an exception; the
request's own id is the idempotency key; paid is read, never set) and
`ADR-0152`/`ADR-0154` (a quotation is a governed record whose acceptance
creates deliverables). Uses `ADR-0156` (project-centric numbering) for the
numbers Xero is given.

## Context

`WP 19.1A` connected Xero for one act: a draft sales invoice, matched to
its contact by name under a read-only contacts scope (review item M21),
plus a read of bills, repeating bills and bank balances for the Business
dashboard. Quotes, purchase orders, expense bills, attachments, the
company's own details, VAT codes and account codes never touched Xero.

The Product Owner decided (D1–D7) that TempestOS stays the master for
commercial work and Xero for everything that reaches the accounts, that
every document leaving TempestOS has an identical copy in Xero with the
same number and PDF, and that nothing is approved or sent to a client
without the Product Owner acting in Xero. Xero apps created from 2 March
2026 can only use granular OAuth scopes. Xero limits each organisation to
60 calls a minute and 5,000 a day, accepts an `Idempotency-Key` header,
and may be unreachable when TempestOS is used offline.

## Decision

**1. Source of truth.** Company details, VAT number, bank details, tax
rates, chart of accounts, contact billing details and invoice/bill payment
status are mastered in Xero and only read by TempestOS (cached, with the
time read, so PDFs and validation work offline). Quotes, revisions and
acceptance, invoice content and number, purchase orders and expenses are
mastered in TempestOS and copied to Xero. Customers and suppliers exist in
TempestOS and are linked to a Xero contact by `ContactID`, confirmed by
the Product Owner or created explicitly — never matched silently by name.

**2. Drafts and copies only (D3, D4).** TempestOS creates invoices and
bills in Xero as `DRAFT` and purchase orders as `DRAFT`; it moves a Xero
quote only along `DRAFT → SENT → ACCEPTED | DECLINED`, following
TempestOS; it may delete its own draft when the TempestOS record is
voided or cancelled. It never sets `SUBMITTED` or `AUTHORISED`, never sets
`SentToContact`, never calls an email endpoint, and never writes payments,
bank transactions, journals, credit notes, the organisation, tax rates or
accounts. These rules are enforced in one place below every Xero caller —
a `DelegatingHandler` in the Xero `HttpClient` that refuses a forbidden
request before it leaves the machine — and by construction in the write
models, and are guarded by tests at three levels (the handler's own
tests, an in-process Xero simulator that independently records any
violation, and an architecture test). Revisiting D3 or D4 is a new
decision and a new ADR, not a setting.

**3. Demo Company first (D7).** TempestOS writes only to an organisation
Xero reports as `IsDemoCompany` until the Product Owner turns on
*Allow the live organisation* in Settings (audited). Links between
TempestOS records and Xero records are scoped to one organisation, so the
Demo Company's ids are never used against the live one.

**4. One durable, idempotent outbox.** Every write to Xero except the
existing invoice send is a queued entry in a persisted outbox, planned
from each record's desired state after every committed change (and at
start-up), drained one request at a time, honouring 429 `Retry-After` and
backoff, pausing for re-authorisation, and audited. A record is created
in Xero at most once: a link is written only after Xero confirms the
record, a create is never issued while a link exists, every create
carries a fixed `Idempotency-Key`, and a write whose response was lost is
reconciled by looking the record up by its number before anything is
resent. The invoice send keeps `ADR-0151`'s own state machine and
reconcile-before-retry, now matching on the invoice number, and re-queues
through the outbox when Xero is unreachable.

**5. Minimal granular scopes.** TempestOS requests `accounting.invoices`,
`accounting.contacts`, `accounting.settings.read`,
`accounting.attachments`, `accounting.reports.banksummary.read` and
`offline_access`, and asks the Product Owner to re-authorise when the
stored grant lacks one. Tokens stay in the secret store.

**6. Backward compatible state.** No existing record's persisted shape
changes. Links, outbox entries and the cached settings reading are new,
schema-versioned stores; an invoice request sent to Xero before
`v0.24.0` is imported as a link on first read.

## Consequences

- The Product Owner reviews, approves and sends every invoice in Xero;
  TempestOS shows *Draft in Xero — review and send from Xero* and then
  reads back Awaiting payment, Paid or Voided.
- Work done offline reaches Xero later without loss; each record shows one
  Xero badge (Not sent, Queued, In Xero (draft), In Xero, Awaiting
  payment, Paid, Voided, Failed with reason and Retry, Needs
  re-authorising).
- A push can be Blocked until a customer or supplier is linked, a tax
  type or account code is chosen, or a PDF is issued; the reason is shown.
- Every Xero path is tested end to end without network against the
  simulator; a live smoke test runs only on the operator's request and
  only against the Demo Company.
- Quotes invoiced directly in Xero are flagged, because raising the same
  work from TempestOS would bill it twice.

## Related

`ADR-0151`, `ADR-0152`, `ADR-0154`, `ADR-0156`;
`docs/releases/v0.24.0/Xero Integration Plan.md`;
`docs/releases/v0.24.0/Xero Technical Design.md`;
`docs/releases/v0.24.0/Xero Build Decisions.md`;
`docs/releases/v0.24.0/Release Notes.md`;
`docs/releases/v0.24.0/PO Test Runbook - Xero.md`;
`docs/guides/Xero Setup - Step by Step.md`.
