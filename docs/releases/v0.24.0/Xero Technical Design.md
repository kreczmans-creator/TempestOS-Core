# TempestOS v0.24.0 — Xero Technical Design

**Status: design, 2026-10-02.** Builds the plan in `Xero Integration
Plan.md` (PO decisions D1–D7, work packages X0–X8). Decision record:
`ADR-0162`. Seams other tasks build against are committed as C# contracts
(§12); everything else here is specification for the build tasks in §11.

## 1. Scope and shape

```
 TempestOS record changes ──► IWorkspaceChanges ──► planners (X3/X4/X5) ──► outbox (durable)
         (Quotation, InvoiceRequest,                desired state,            Xero.Outbox
          PurchaseOrder, ProjectExpense)            no network                  │
                                                                                ▼
 Settings/PDFs ◄── X1 cache ◄─┐                                  drain (X6, 1 request in flight)
 badges (X6)   ◄── links  ◄───┤                                   │  push handlers (X3/X4/X5)
                              │                                   ▼
                       XeroAccountingApi (typed, B1) ─► XeroWriteSafetyHandler (D3/D4/D7)
                                                        ─► InvoicingHttpLoggingHandler ─► api.xero.com
                                                           (tests: ─► XeroApiSimulator)
```

- **TempestOS never calls Xero from a domain service.** Domain services
  (`QuotationService`, `PurchaseOrderService`, `ExpenseService`) stay
  unaware of Xero; the engine observes committed changes and plans writes
  from desired state. The one exception is the existing invoice send
  (`InvoicingService.SendAsync` → `IInvoicingConnector`, `ADR-0151`), kept
  because the invoice request already *is* a durable, reconciled send
  state machine.
- **Xero is read for D6** (organisation, tax rates, accounts, contact
  billing details, invoice/bill payment status) and **written only as
  drafts or copies** (D2, D3, D5).

## 2. Scopes (X0)

Exact strings requested (`Tempest.Core.Invoicing.Xero.XeroScopes.Required`):

| Scope | Why (endpoints) |
|---|---|
| `accounting.invoices` | `Invoices` (ACCREC drafts, ACCPAY draft bills, status read-back), `Quotes`, `PurchaseOrders`, `RepeatingInvoices` (dashboard, existing) |
| `accounting.contacts` | `Contacts` search, read, **create** (X2; fixes M21 — today's grant is `accounting.contacts.read`) |
| `accounting.settings.read` | `Organisation`, `TaxRates`, `Accounts` (X1). Read-only: TempestOS never changes Xero settings |
| `accounting.attachments` | `PUT/POST {Quotes,Invoices,PurchaseOrders}/{id}/Attachments/{FileName}` |
| `accounting.reports.banksummary.read` | `Reports/BankSummary` (existing cash position) |
| `offline_access` | refresh token |

Removed from today's profile: `accounting.contacts.read` (superseded by
`accounting.contacts`) and `openid profile email` (no code reads the
`id_token`; `GET https://api.xero.com/connections` needs only the access
token). Removing them is verified on the Demo Company in task B1 before
release; if `/connections` refuses without them, they go back (Q9).
Not requested: `accounting.payments`, `accounting.banktransactions`,
`accounting.manualjournals`, payroll, any other report.

**Sources.**
[S1] Xero changelog, "Broad scopes deprecation" (6 Aug 2026): *"Apps created
on or after 2 March 2026 already use granular scopes"*; `accounting.transactions`
is replaced by `accounting.invoices`, `accounting.payments`,
`accounting.banktransactions`, `accounting.manualjournals`; existing apps
migrate by 13 September 2027 — https://developer.xero.com/changelog.
[S2] Scopes reference — https://developer.xero.com/documentation/guides/oauth2/scopes/
(client-rendered; content read via [S3]/[S4]). [S3] Xero's own MCP server,
`src/clients/xero-client.ts` (commit `f24583c`, 2026-06-05), "Granular
scopes (required for new apps)": `accounting.invoices`, `accounting.payments`,
`accounting.banktransactions`, `accounting.manualjournals`,
`accounting.reports.*.read`, `accounting.contacts`, `accounting.settings`
— https://github.com/XeroAPI/xero-mcp-server. [S4] Apideck, "Xero Scopes":
`accounting.invoices` covers *"Credit notes, invoices, linked transactions,
purchase orders, quotes, repeating invoices, items"*; `accounting.contacts`,
`accounting.settings`, `accounting.attachments`, `accounting.budgets`
*"remain available to every app regardless of when it was created"*;
`accounting.reports.banksummary.read` — https://www.apideck.com/blog/xero-scopes.
[S5] Xero OpenAPI `xero_accounting.yaml` v19.1.0 (per-endpoint security
still lists the legacy `accounting.transactions`; used for schemas,
parameters and the settings/contacts/attachments scopes, which are
unchanged) — https://github.com/XeroAPI/Xero-OpenAPI.

**Ambiguity, designed for.** No first-party page stating the granular
mapping could be read as text (developer.xero.com renders client-side);
the mapping above agrees across [S1], [S3], [S4]. Whether
`accounting.settings.read` alone suffices for `TaxRates`/`Accounts` is
from [S5] (`accounting.settings` *or* `accounting.settings.read`). A 403
on any endpoint is classified `MissingScope` (§7.4) and surfaces as
"re-authorise", never as a retry loop; the live smoke test (§10.2) checks
every endpoint once.

**Re-authorisation when the grant lacks a scope.** `OAuthAuthoriser`
stores the granted scope set at `Invoicing:Xero:GrantedScopes` in
`ISecretStore` after each code exchange (from the token response's
`scope` field when present, else the access token's `scope` claim, else
the requested set). `XeroConnector.AuthorisationStateAsync` answers
`Expired` with detail *"Xero needs re-authorising to allow: {missing}"*
when `XeroScopes.Required` ⊄ granted; Settings shows Re-authorise. A
missing grant record (tokens stored before v0.24.0) counts as missing
`accounting.contacts`, `accounting.settings.read`, `accounting.attachments`.

## 3. Endpoints and payloads per document type

All calls: base `https://api.xero.com/api.xro/2.0/`, headers
`Authorization: Bearer`, `xero-tenant-id`, `Accept: application/json`;
every `PUT`/`POST` carries `Idempotency-Key` (§7.2) and
`?summarizeErrors=true`. One document per request.

| TempestOS | Create | Update | Lookup (reconcile) | Attachment | Payload (TempestOS → Xero) |
|---|---|---|---|---|---|
| Organisation (customer/supplier) | `PUT Contacts` | — (never pushed) | `GET Contacts/{ContactNumber}`; `GET Contacts?searchTerm=` | — | `Name`, `ContactNumber` = `Organisation.Reference` (≤50), `TaxNumber`, `CompanyNumber`, `EmailAddress` — **create only** |
| Quotation (approved Rn) | `PUT Quotes` (`Status: DRAFT`) | `POST Quotes/{QuoteID}` (content only while Xero `DRAFT`; status: §4.1) | `GET Quotes?QuoteNumber=` | `POST`/`PUT Quotes/{id}/Attachments/{Reference}.pdf` | `QuoteNumber` = `Reference`; `Reference` = `Rn`; `Title` = display name; `Summary` = project; `Contact.ContactID`; `Date` = `QuoteDate`; `ExpiryDate` = `QuoteDate + ValidityDays`; `Terms`; `CurrencyCode`; `LineAmountTypes: Exclusive`; lines: `Description`, `Quantity` (hours or 1), `UnitAmount`, `TaxType` (output, §8), `AccountCode` (sales default, §8) |
| InvoiceRequest (sent) | `PUT Invoices` (`Type: ACCREC`, `Status: DRAFT`) | `POST Invoices/{InvoiceID}` (lines only while `DRAFT`); void: `Status: DELETED` while `DRAFT`/`SUBMITTED` | `GET Invoices?InvoiceNumbers=`; fallback `where=Reference=="{requestId}"` (pre-v0.24 sends) | `PUT Invoices/{id}/Attachments/{Identifier}.pdf?IncludeOnline={Q5}` | `InvoiceNumber` = `InvoiceRequest.Identifier`; `Reference` = `{project code} · {deliverable or "time & expenses"}`; `Contact.ContactID`; `Date` = send date; `DueDate` = date + payment terms; `CurrencyCode`; `LineAmountTypes: Exclusive`; lines as above. **No `Status` other than `DRAFT`/`DELETED`, no `SentToContact`.** |
| PurchaseOrder (issued) | `PUT PurchaseOrders` (`Status: DRAFT`, Q2) | none after issue (TempestOS lines are fixed once Issued); cancel: `Status: DELETED` | `GET PurchaseOrders/{PurchaseOrderNumber}` | `PUT PurchaseOrders/{id}/Attachments/{Reference}.pdf` | `PurchaseOrderNumber` = `Reference`; `Reference` = project code; `Contact.ContactID` (supplier); `Date` = `IssuedDate`; `DeliveryDate` = `ExpectedDelivery`; `CurrencyCode`; lines with input `TaxType`, `AccountCode` (§8). Never `SentToContact`. |
| ProjectExpense (recorded) | `PUT Invoices` (`Type: ACCPAY`, `Status: DRAFT`) | `POST Invoices/{id}` while `DRAFT`; delete: `Status: DELETED` while `DRAFT` | `GET Invoices?InvoiceNumbers=&ContactIDs=` | `PUT Invoices/{id}/Attachments/{receipt name}` (receipt) | `InvoiceNumber` = `EXP-{expenseId:N}` (Q4); `Contact.ContactID` (Q3); `Date` = expense date; one line: `Description`, `Quantity: 1`, `UnitAmount` = net, `AccountCode` = category map (§8), `TaxType` = input type, `TaxAmount` = recorded VAT (receipt figure, not recomputed); `LineAmountTypes: Exclusive`. Recharges (`RaiseFromExpenseAsync`) unchanged. |
| Company (X1) | — | — | `GET Organisation`, `GET TaxRates`, `GET Accounts` (`If-Modified-Since`) | — | read only |
| Status read-back (X6) | — | — | `GET Invoices?IDs=a,b,…&page=n` (batched, `If-Modified-Since`), `GET Quotes/{id}`, `GET PurchaseOrders/{id}` | — | read only |

Wire facts used ([S5] unless noted): `QuoteNumber` *"Unique alpha numeric
code identifying a quote (Max Length = 255)"*; `InvoiceNumber` (ACCREC)
*"when missing will auto-generate"* (TempestOS always supplies it);
`Reference` on invoices is *"ACCREC only"* — hence bills reconcile by
`InvoiceNumber` + `ContactID`; `PurchaseOrderNumber` *"when missing will
auto-generate"*; Contacts `IsCustomer`/`IsSupplier` *"Cannot be set via PUT
or POST"* — Xero sets them when a sales invoice or bill exists;
`ContactNumber` is *"used to identify contacts in external systems (max
length = 50)"*; `GET Contacts?searchTerm=` searches *"Name, FirstName,
LastName, ContactNumber and EmailAddress"*; Attachment `IncludeOnline`
*"Allows an attachment to be seen by the end customer within their online
invoice"* (invoices only). Attachments: *up to 10 per document, each up to
25 MB* ([S9] invoices page, search extract) — TempestOS caps at 10 MB
(`XeroDocumentFile.MaximumSizeInBytes`). Xero has **no attachment delete**
endpoint: a new revision's PDF is uploaded under the **same file name**
with `POST …/Attachments/{FileName}` (update-by-name, [S5]); if Xero keeps
both, the Rn number in `Reference` disambiguates (verified in smoke test).

## 4. Status mapping

### 4.1 Quotes (D2: Xero follows TempestOS)

TempestOS → Xero:

| TempestOS event | Xero write | Notes |
|---|---|---|
| Draft, InReview | none | not issued |
| Approved Rn, then **Export** or **Send** (first issue of Rn) | `PushQuote`: create `DRAFT`, or update content while Xero `DRAFT`; `UploadAttachment` | Q1: Xero stays `DRAFT` until TempestOS sends, so a later Rn+1 can still update it |
| Approved → Draft (edit after approval) | none | next approval pushes Rn+1 |
| Sent | `SetQuoteStatus SENT` | from `DRAFT` |
| Accepted | `SetQuoteStatus ACCEPTED` | from `SENT` |
| Declined | `SetQuoteStatus DECLINED` | from `SENT` |

Xero's allowed quote transitions (simulator-enforced; [S8] Quotes page via
search extract, corroborated by Xero's MCP server PR #309 "observed to fail
against a live org"): `DRAFT→SENT|DELETED`, `SENT→ACCEPTED|DECLINED|DELETED`,
`ACCEPTED→INVOICED|SENT|DELETED`, `DECLINED→SENT|DELETED`,
`INVOICED→SENT|DELETED`; `INVOICED` is reached only by invoicing in Xero.
TempestOS only ever walks `DRAFT→SENT→ACCEPTED|DECLINED`. A status-only
`POST Quotes/{id}` carries `QuoteID`, `QuoteNumber`, `Contact`, `Date`,
`Status` and **omits `LineItems`** (Xero's MCP server carries contact and
date forward and omits lines; designed defensively the same way). Content
edits are sent only while Xero holds `DRAFT` (ambiguous in the docs;
Xero's MCP server refuses non-draft content edits).

Xero → TempestOS: **never changes a TempestOS quote.** Read-back records
`LastKnownXeroStatus`; drift is shown on the badge: `INVOICED` → *"Invoiced
in Xero — raising it from TempestOS too would bill twice"*; `DELETED` →
Failed *"Deleted in Xero"*; any other mismatch → note with a *Re-apply*
action that enqueues `SetQuoteStatus` (only along allowed transitions).

### 4.2 Invoices (D3, D4)

| TempestOS | Xero | Badge |
|---|---|---|
| InvoiceRequest Draft | — | Not sent |
| Send (connector Xero) → Sending | `PUT Invoices` `DRAFT` | Queued |
| Sent | `DRAFT` | In Xero (draft) — *"review and send from Xero"* |
| Unavailable on send | request back to Draft **and** `PushInvoiceDraft` queued (same idempotency key, request id) | Queued |
| Edit after Sent (X4) | `UpdateInvoiceDraft` iff Xero `DRAFT`; else refused *"Xero holds it as {status}; change it in Xero"* | |
| Void (X4 extends `VoidAsync` to Sent) | `DeleteInvoiceDraft` iff Xero `DRAFT`/`SUBMITTED`; else refused *"void it in Xero; TempestOS reads it back"* | Voided |

Xero → TempestOS (extends `InvoicingService.InterpretStatus`, `ADR-0151`
"paid is read, never set"):

| Xero `Status` | `InvoiceRequestStatus` | Badge |
|---|---|---|
| `DRAFT` | Sent | In Xero (draft) |
| `SUBMITTED` | Sent | In Xero (draft) — *"awaiting approval in Xero"* |
| `AUTHORISED` | Accepted (`IssuedDate` = `Date`) | Awaiting payment |
| `PAID` | Accepted (`PaidDate` = `FullyPaidOnDate`) | Paid |
| `VOIDED` | Voided | Voided |
| `DELETED` (new) | Voided, note *"deleted in Xero"* — frees its lines | Voided |

### 4.3 Purchase orders (D5)

| TempestOS | Xero |
|---|---|
| Draft | — |
| Issued (+ PDF on Issue/Export) | `PushPurchaseOrder` `DRAFT` (Q2), `UploadAttachment` |
| Received, Closed | none (Xero has no received state; billing is the PO's own *Copy to bill* in Xero) |
| Cancelled | `DeletePurchaseOrder` (`DELETED`); refused by Xero once `BILLED` → Failed with reason |

Xero → TempestOS: none changes TempestOS; `AUTHORISED`/`BILLED`/`DELETED`
shown as notes. Xero PO statuses: `DRAFT, SUBMITTED, AUTHORISED, BILLED,
DELETED` [S5].

### 4.4 Expense bills (D5)

| TempestOS | Xero |
|---|---|
| Recorded | `PushExpenseBill` `ACCPAY` `DRAFT`, `UploadAttachment` (receipt, when attached) |
| Amended | update iff Xero `DRAFT`; else Failed *"approved in Xero; change it there"* |
| Deleted | `DeleteExpenseBill` iff `DRAFT` |
| (from a PO's lines, `RecordLinesAsExpensesAsync`) | Q6 — default: **not** pushed as a bill when the source PO is linked in Xero (the PO's *Copy to bill* in Xero is the bill); needs an additive `SourcePurchaseOrderId` on `ProjectExpense` |

Xero → TempestOS: `AUTHORISED` → *"Approved in Xero"*, `PAID` → Paid,
`DELETED`/`VOIDED` → Voided note. Invoice statuses: `DRAFT, SUBMITTED,
DELETED, AUTHORISED, PAID, VOIDED` [S5].

## 5. Identity and linking model

- **Links** (`XeroLink`, `IXeroLinkStore`): one per *(tenant, kind,
  TempestOS key)*, JSON in `IPersistenceStore` collection `Xero.Links`,
  key `{tenantId}/{kind}/{tempestKey}`. Holds Xero's id (`ContactID`,
  `QuoteID`, `InvoiceID` — for bills too — or `PurchaseOrderID`), Xero's
  number, last pushed content hash, last known Xero status, last uploaded
  attachment name and SHA-256, timestamps, and how it was linked.
- **Tenant-scoped.** The Demo Company's links are never used against the
  live organisation (D7). Connecting another organisation starts with no
  links.
- **No change to any existing record's persisted shape.** Quotation,
  PurchaseOrder, ProjectExpense and Organisation gain no field; a record
  with no link reads *Not sent*. `InvoiceRequest.ExternalId` /
  `ExternalInvoiceNumber` / `ExternalStatus` remain the invoice's own
  `ADR-0151` fields; on first read the engine **imports** a link
  (`LinkedBy = "imported"`) for each request with `Connector == "Xero"`
  and an `ExternalId`, so v0.19–v0.23 sends keep reconciling.
- **Schema versioning.** `XeroLink`, `XeroOutboxEntry` and
  `XeroSettingsReading` carry `SchemaVersion` (`1`). Readers accept every
  version ≤ current and ignore unknown JSON properties; a record with a
  newer version than the build knows is left untouched and the record's
  badge says *"written by a newer TempestOS"* (never overwritten).
  Additive fields only within a version; a breaking change bumps the
  version with an upgrade step in the store.
- **Contacts (X2).** `XeroContactCandidate`s are offered by VAT number,
  `ContactNumber`, exact name, then similar name; the PO confirms
  (`LinkExistingAsync`) or creates (`CreateAsync`, which first looks up
  `ContactNumber` = `Organisation.Reference` so a lost response never
  makes two). Linking an existing contact writes only the customer code into an empty `ContactNumber` (Q7, Build Decisions); nothing else.
  Billing address, VAT number and payment terms are read with
  `ReadDetailsAsync` and shown read-only; never pushed.

## 6. Sync engine (X6)

### 6.1 Outbox

`XeroOutboxEntry` in `IPersistenceStore` collection `Xero.Outbox`:
operation, document, argument, idempotency key, content hash, state,
attempts, `NotBeforeUtc`, last error, who enqueued. States: `Pending →
InFlight → Succeeded | Failed | Unknown | WaitingForAuthorisation`,
`Superseded`. Per-document FIFO: nothing for a document is sent while an
earlier entry for it is Pending/Unknown/Failed/Waiting — a status change
never overtakes its create. A newer entry for the same document and
operation supersedes a pending one; an entry identical to one pending or
succeeded (same content hash) is not queued again.

### 6.2 Planning

`IXeroSyncPlanner` per kind computes desired writes from the record and
its link (pure, offline). Triggered by `IWorkspaceChanges` (commits to
`Quotation`, `InvoiceRequest`, `PurchaseOrder`, `ProjectExpense`,
including `AttachmentAdded`), handled off the commit thread through a
bounded channel; and a full scan at start-up and on Refresh — so a crash
between commit and enqueue loses nothing.

### 6.3 Drain

`XeroSyncService` (hosted, timer `Xero:SyncSeconds`, default 60; also
straight after an enqueue): claims the oldest due entry (`InFlight`,
persisted before the request), dispatches to the `IXeroPushHandler` for
its operation, records the result, audits, repeats. **One request in
flight per tenant** (Xero allows 5 concurrent). Outcomes:

| Push outcome | Entry | Queue |
|---|---|---|
| Succeeded / NothingToDo | Succeeded; link saved | continue |
| Rejected (Xero 400/404, or a safety rule) | Failed + reason (Xero `ValidationErrors` joined) | continue (other documents) |
| Blocked (no contact link, no PDF yet, no account mapping) | Failed + reason; re-planned automatically when the precondition appears | continue |
| Reauthorise (401/403, no token, missing scope) | WaitingForAuthorisation | **pause all**; resume after successful authorisation |
| RetryLater (transport, 5xx, 429) | Pending, `NotBeforeUtc` = backoff or `Retry-After` | 429: **pause all** until `Retry-After` |
| Unknown (response lost, unparseable 2xx) | Unknown | next drain reconciles by lookup first |

Backoff: `min(30 s × 2^(attempts−1), 30 min)` ± 20 % jitter. Entries found
`InFlight` at start-up become Unknown. An Unknown entry whose lookup is
inconclusive three times becomes Failed *"check Xero, then Retry or
Unlink"*.

### 6.4 Never double-create

1. A create is issued only when no link exists for *(tenant, document)*;
   `IXeroLinkStore.SaveAsync` refuses to change a link's Xero id.
2. Every create carries a fixed `Idempotency-Key`; Xero replays the
   cached response for a repeat [S7].
3. Before any resend of an Unknown/InFlight entry, the handler looks the
   record up by its natural key (`QuoteNumber`, `InvoiceNumber`,
   `PurchaseOrderNumber`, bill `InvoiceNumber`+`ContactID`, `ContactNumber`).
   Found → link (`LinkedBy = "reconciled"`), Succeeded; not found → resend
   with the same key.
4. Before a first create of an invoice, quote or PO the handler also
   looks the number up: found with our reference → link; found otherwise →
   Failed *"{number} is already used in Xero"* — never a silent duplicate.
5. Invoices keep `ADR-0151`'s reconcile-before-retry
   (`InvoicingService.ReconcileAsync`), now by `InvoiceNumbers=` first.

### 6.5 Rate limits and paging [S6]

Per tenant: **60 calls/minute, 5,000/day, 5 concurrent**; app-wide 10,000/
minute. Every response's `X-MinLimit-Remaining`, `X-DayLimit-Remaining`,
`X-AppMinLimit-Remaining` are recorded; at `X-MinLimit-Remaining ≤ 2` the
drain pauses to the next minute. A 429 carries `X-Rate-Limit-Problem` and
(minute/day) `Retry-After` seconds; the queue pauses for exactly that
(+1 s; 60 s when absent). A client-side limiter caps TempestOS at 50
calls/minute so the dashboard refresh and the drain cannot together
exceed 60. Paging: `page=n`, up to 100 items per page; loop until a short
page. Read-back batches invoice ids (`IDs=`, ≤ 50 per call) and sends
`If-Modified-Since` = last read time (supported on `Invoices`, `Quotes`,
`PurchaseOrders`, `Contacts`, `Accounts` [S5]). Request bodies stay far
below Xero's 3.5 MB limit (one document per request) [S6].

### 6.6 Errors

Xero's error body: `{ "ErrorNumber", "Type": "ValidationException",
"Message", "Elements": [ { "ValidationErrors": [ { "Message" } ] } ] }`
[S5]. A 200 response is also checked for per-element `HasErrors` /
`ValidationErrors` (`summarizeErrors` is always sent `true`, designed for
either default). 400 → Rejected; 401 → Reauthorise; 403 → Reauthorise
with `MissingScope` when the body names authorisation/scope; 404 →
Rejected + `NotFound` (linked record gone: badge *"Deleted in Xero"*,
offer Unlink); 429/5xx/transport → RetryLater; anything else → Unknown.
An `Idempotency-Key` reused with a different body answers 400 [S7] — by
construction impossible (the key embeds the content hash) and treated as
a defect if seen.

### 6.7 Audit

`IAuditRecorder` actions: `xero.outbox.enqueued`, `xero.push.succeeded`,
`xero.push.failed`, `xero.push.blocked-by-rule`, `xero.link.created`,
`xero.link.linked`, `xero.link.reconciled`, `xero.link.unlinked`,
`xero.settings.read`, `xero.reauthorisation.required`,
`xero.live-organisation.allowed`. Detail: document, operation, Xero id and
number, idempotency key, attempt, HTTP status, reason. Never a token.

### 6.8 Offline

Every enqueue, plan and badge works with no network: links, outbox and the
X1 cache are local. Writes wait in the outbox (badge *Queued*); PDFs and
VAT validation use the last X1 reading (badge in Settings: *"from Xero,
read at …"*); a document whose required data (contact link, tax type,
account code) is missing locally is Blocked with the reason, never sent
half-formed.

## 7. Safety rules enforced in code (D3, D4, D7)

### 7.1 Where

`XeroWriteSafetyHandler` — a `DelegatingHandler` in the Xero `HttpClient`
pipeline, below every Xero caller (the typed client and the existing
`XeroConnector` share one client). It inspects every outbound request and
answers a synthetic **400** (`X-Tempest-Blocked: {rule}`, Xero-shaped
`ValidationException` body) instead of sending, so callers report
Rejected through their ordinary path, and audits `xero.push.blocked-by-rule`.

| Rule | Blocks |
|---|---|
| D3.invoice-status | Any `Invoices` write whose body carries `Status` other than `DRAFT` (create/update) or `DELETED` (update of an existing id) — so never `SUBMITTED`, `AUTHORISED`, `PAID`, `VOIDED` |
| D3.po-status | Any `PurchaseOrders` write with `Status` other than `DRAFT`/`DELETED` (until Q2 says otherwise) |
| D4.email | Any request to a path ending `/Email` (e.g. `POST Invoices/{id}/Email`) |
| D4.sent-to-contact | Any body with `SentToContact: true` |
| quote-status | `Quotes` writes with `Status` outside `DRAFT, SENT, ACCEPTED, DECLINED` |
| write-allow-list | Any non-GET to a path other than `Contacts[/id]`, `Quotes[/id]`, `Invoices[/id]`, `PurchaseOrders[/id]`, `{Quotes,Invoices,PurchaseOrders}/{id}/Attachments/{file}` (so never `Payments`, `BankTransactions`, `ManualJournals`, `CreditNotes`, `Organisation`, `TaxRates`, `Accounts`) |
| D7.live-organisation | Any non-GET while the cached `Organisation.IsDemoCompany` is false and Settings `Xero.AllowLiveOrganisation` is off (default off; turning it on is audited); unknown organisation (no reading) → read first, still unknown → block |

### 7.2 By construction

Write-side wire models expose status as enums whose only members are the
allowed words (`XeroInvoiceWriteStatus { Draft, Deleted }`); no write model
has `SentToContact`; the typed client has no email method. The idempotency
key is `tos:{kind}:{key}:{operation}:{hash}` (≤ 128 chars, hashed when
longer; Xero answers 400 above 128 [S7]).

### 7.3 Guarded by tests

1. `XeroWriteSafetyHandlerTests`: each rule, positive and negative, at the
   HTTP level (a hand-built forbidden request is blocked, an allowed one
   passes, an audit row is written).
2. Simulator end-to-end: every test asserts `simulator.Violations` is
   empty; the simulator independently records `D3.*`, `D4.*`, `D7.*`
   violations even if the handler were removed — and a dedicated test runs
   the full journey **without** the safety handler to prove the
   simulator's own detectors fire on deliberately forbidden requests.
3. `XeroNeverApprovesOrSendsTests`: drives every outbox operation and the
   invoice send through randomised sequences (seeded) and asserts no
   simulated invoice, bill or PO ever reaches `AUTHORISED`/`SUBMITTED`
   except through the simulator's back-office `ApproveInXero`, and no
   `/Email` request exists.
4. Architecture test (`XeroSafetyArchitectureTests`, Architecture filter):
   no `src/**/*.cs` outside the read-side mapping files contains the
   literals `"AUTHORISED"`, `"SUBMITTED"`, `"SentToContact"` or `/Email`
   in a write path; `XeroWriteSafetyHandler` is in the Xero `HttpClient`
   built by `TempestHost`.
5. D7: the simulator with `IsDemoCompany = false` refuses every write via
   the handler unless the setting is on.

## 8. Company details, tax rates and accounts (X1, D6)

- `IXeroSettingsReader.RefreshAsync`: `GET Organisation`, `GET TaxRates`,
  `GET Accounts` (3 calls; `If-Modified-Since` on Accounts), cached as
  `XeroSettingsReading` at `{root}/accounts/xero-settings.json`
  (schema-versioned; corrupt or newer → "no reading", never an exception),
  beside the existing `last-reading.json`. Refreshed at connect, daily, on
  Settings → Refresh, and before the first write of a session.
- **PDF identity.** `OrganisationIdentity` gains `VatNumber` (additive,
  optional). When a reading exists, PDFs use `LegalName` (else `Name`),
  `RegistrationNumber`, `TaxNumber`, the `STREET` address, phone, website
  and the `BANK` account(s) from the reading; Settings shows them
  read-only with *"from Xero, read at {time}"*. Without a reading the
  existing Settings → Organisation values are used (offline first run).
- **Tax types.** `VatRateTaxTypeMapping` gains the input side
  (`Standard→INPUT2`, `Reduced→RRINPUT`, `Zero→ZERORATEDINPUT`,
  `Exempt→EXEMPTINPUT`, `OutOfScope→NONE`; UK codes, [S5] `ReportTaxType`
  list) and a check against the reading: the mapped `TaxType` must exist,
  be `ACTIVE`, and `CanApplyToRevenue` (sales) / `CanApplyToExpenses`
  (purchases) — otherwise the push is Blocked *"Xero has no active tax
  rate {code}"*. A different tax type per rate can be chosen in Settings
  from the reading's list (non-UK organisations).
- **Account codes.** Settings holds one sales account code (quotes,
  invoices) and one per `ExpenseCategory` (bills, PO lines), chosen from
  the reading's `ACTIVE` accounts of class `REVENUE` / `EXPENSE`; a code
  Xero lacks Blocks the push. No defaults are invented: the Demo Company
  runbook names the codes to pick.

## 9. Security

- Minimal scopes (§2); `accounting.settings.read`, not `accounting.settings`.
- Tokens and the granted-scope record only in `ISecretStore`
  (`WindowsDpapiSecretStore`/`FileSecretStore`, `ADR-0151`); never in the
  persistence store, logs, audit or the outbox. `InvoicingHttpLoggingHandler`
  already redacts; the simulator test asserts no token appears in any
  audit row or log line.
- Links and outbox hold Xero ids and numbers only; no billing details are
  persisted beyond the read-only contact detail cache.
- The live organisation is written only after the PO turns on
  `Xero.AllowLiveOrganisation` (D7).

## 10. Test strategy

### 10.1 In-process Xero API simulator (task S1)

`tests/Tempest.Core.Tests/Invoicing/Xero/Simulator/XeroApiSimulator`
(surface committed, §12): an `HttpMessageHandler` holding one tenant in
memory, seeded with the UK Demo Company's tax rates (`OUTPUT2`, `RROUTPUT`,
`ZERORATEDOUTPUT`, `EXEMPTOUTPUT`, `INPUT2`, `RRINPUT`, `ZERORATEDINPUT`,
`EXEMPTINPUT`, `NONE`) and a chart-of-accounts subset (`200` Sales, `310`
Cost of Goods Sold, `400`-series overheads incl. `493` Travel, `090`
Business Bank Account). It validates, per request:

- **Auth:** `Bearer` = configured token, `xero-tenant-id` = tenant, else 401/403.
- **Scope per endpoint** (granular table §2): ungranted → 403 `AuthorizationUnsuccessful`.
- **Routing** for every endpoint in §3, incl. id-or-number paths
  (`Contacts/{ContactNumber}`, `PurchaseOrders/{PurchaseOrderNumber}`).
- **Required fields:** invoice `Type`, `Contact` (`ContactID` must exist),
  `LineItems[].Description`; quote `Contact`, `Date`, `LineItems`; PO
  `Contact`, `LineItems`; contact `Name` (unique, case-insensitive);
  `TaxType` exists, ACTIVE, applicable; `AccountCode` exists, ACTIVE;
  unique `QuoteNumber`, ACCREC `InvoiceNumber`, `PurchaseOrderNumber`.
- **Status rules:** the quote transition table (§4.1); invoices created
  only `DRAFT`/`SUBMITTED`/`AUTHORISED` (the latter two recorded as D3
  violations), content updates only while `DRAFT`/`SUBMITTED`, `DELETED`
  only from `DRAFT`/`SUBMITTED`, `VOIDED` only from `AUTHORISED`; PO
  `DELETED` refused once `BILLED`.
- **Idempotency-Key:** replay the cached response for a repeat; 400 for
  reuse with a different body; 400 above 128 characters.
- **Rate limits** on the injected `TimeProvider`: 60/minute, 5,000/day,
  5 concurrent → 429 with `Retry-After`, `X-Rate-Limit-Problem`; limit
  headers on every response.
- **Paging** (100 per page), `If-Modified-Since` (`UpdatedDateUTC`),
  `where=`/`IDs=`/`InvoiceNumbers=`/`QuoteNumber=`/`searchTerm=` filters
  used by TempestOS, Microsoft JSON dates (`/Date(…)/`) in responses.
- **Errors** in Xero's `ValidationException` shape; per-element errors.
- **Faults:** 503, 429, 401, transport failure, and *drop response after
  commit* (the lost-response case).
- **Back-office acts** for read-back tests: approve, pay, void, delete,
  convert quote to invoice.
- **Violations** list (contract and D3/D4/D7) asserted empty by every test.

Every connector path — X1 read, X2 search/link/create, X3–X5 create /
update / status / delete / attachment, X4 send + reconcile + read-back, X6
drain under 429, 5xx, lost response, re-authorisation and restart — has an
end-to-end test over `XeroAccountingApi → XeroWriteSafetyHandler →
XeroApiSimulator`, with no network. The existing `StubHttpMessageHandler`
tests stay as they are.

### 10.2 Live smoke test against the Demo Company (task X8, D7)

`scripts/xero-demo-smoke.ps1` runs `dotnet test tests/Tempest.Core.Tests
--filter "Category=XeroLive"` with `TEMPEST_XERO_LIVE=1`; the tests skip
otherwise (never in CI). They use the operator's stored tokens
(`ISecretStore`, the same root as the app) and **refuse to write unless
`GET Organisation` answers `IsDemoCompany: true`**. Steps (~30 calls,
under the minute limit): read organisation/tax rates/accounts and check
every scope; create-or-link contact *TempestOS Smoke*; quote `SMOKE-{stamp}`
`DRAFT` + PDF → `SENT` → `ACCEPTED`; invoice `SMOKE-{stamp}` `DRAFT` + PDF
(assert `Status == DRAFT` after create and after read-back); PO `DRAFT` +
PDF; bill `DRAFT` + receipt; repeat each create with the same
`Idempotency-Key` and assert no second record; second PDF upload under the
same name (records whether Xero replaced or kept both — feeds §3); delete
the drafts unless `-Keep`. Prints each record's Xero link for the PO to
inspect. The runbook (X8) walks the same journey through the UI.

### 10.3 Other layers

Unit tests per planner (desired state from record + link), handler (over
the simulator), stores (round-trip, schema version, unknown properties,
newer-version guard), backoff calculator; Desktop tests (headless) for
badges, Settings Xero section, contact link prompt, using fakes of the
§12 interfaces.

## 11. Build plan

**T0** (this change: design, contracts §12, `ADR-0162`) is done.
Order: **S1 ∥ B1 ∥ B2 → X1 ∥ X2 → X3 ∥ X4 ∥ X5 → X6 → UI tasks (U1 ∥ U2 ∥
U3) → X7 → X8.** Each task owns exactly the files listed (create or edit);
anything else is read-only to it. "Registration" is conflict-free: B1
creates `XeroServiceRegistration.cs` declaring one `static partial void
Register{Area}(IServiceCollection services, …)` hook per task; each task
implements its own hook in `XeroServiceRegistration.{Area}.cs`.
Similarly `XeroAccountingApi` is a `partial` class: B1 owns the transport
file, each task owns its resource file. Paths below are relative to
`src/Tempest.Core/Invoicing/Xero/` (Core) and
`tests/Tempest.Core.Tests/Invoicing/Xero/` (tests) unless absolute.

| Task | Content | Owns (create/edit) | Depends | Acceptance |
|---|---|---|---|---|
| **S1** Simulator | §10.1 in full | tests `Simulator/**` (`XeroApiSimulator*.cs`, `SimulatorSeed.UkDemo.cs`, `XeroApiSimulatorTests.cs`) | T0 | Self-tests: one per validation rule (allowed and refused), quote transition table exhaustive, 429 after 60 calls/minute on fake time with `Retry-After`, day limit, idempotent replay / conflicting reuse / >128, paging, `If-Modified-Since`, every fault kind, every back-office act; `Violations` populated for D3/D4/D7 |
| **B1** X0 API foundation + scopes | typed client transport (auth headers, `Idempotency-Key`, `summarizeErrors`, error parsing → `XeroApiResult`, rate-limit headers, 429), attachments resource, `XeroWriteSafetyHandler`, client-side limiter, granted-scope storage + `AuthorisationStateAsync` scope check, `TempestHost` profile = `XeroScopes.Required`, handler in the Xero pipeline, registration hook file | `Api/XeroAccountingApi.cs`, `Api/XeroAccountingApi.Attachments.cs`, `Api/XeroWireCommon.cs`, `Api/XeroWriteSafetyHandler.cs`, `Api/XeroRateLimiter.cs`, `XeroServiceRegistration.cs`, `XeroConnector.cs` (scope check only), `src/Tempest.Core/Invoicing/OAuth/OAuthAuthoriser.cs`, `…/OAuth/OAuthTokenResponse.cs`, `src/Tempest.Core/Runtime/TempestHost.cs`; tests `Api/**`, `Safety/**`, `tests/Tempest.Core.Tests/Architecture/XeroSafetyArchitectureTests.cs`, `…/Invoicing/Connectors/OAuthAuthoriserTests.cs` | T0, S1 (e2e tests) | §7.3 items 1, 4; every result mapping (200, 200-with-errors, 400, 401, 403 scope, 404, 429, 5xx, transport, garbage); scopes string test; re-auth state when grant lacks a scope; existing connector tests green |
| **B2** Link store + outbox store | `PersistenceXeroLinkStore`, `PersistenceXeroOutbox` (§5, §6.1: FIFO per document, supersede, dedupe, retry), import of pre-v0.24 invoice links, idempotency-key builder | `Sync/PersistenceXeroLinkStore.cs`, `Sync/PersistenceXeroOutbox.cs`, `Sync/XeroIdempotencyKey.cs`, `Sync/XeroInvoiceLinkImporter.cs`, `XeroServiceRegistration.Stores.cs`; tests `Sync/Stores/**` | T0 | Round-trip; schema v1 read with unknown props; newer-version untouched; link Xero-id change refused; tenant isolation; FIFO + supersede + dedupe; key ≤128 and stable; import idempotent |
| **X1** Settings reader | §8: reader, cache file, input tax map + validation, account-code map (settings) | `Api/XeroAccountingApi.Settings.cs`, `Settings/XeroSettingsReader.cs`, `Settings/FileXeroSettingsCache.cs`, `Settings/XeroTaxTypeResolver.cs`, `Settings/XeroAccountCodeMap.cs`, `XeroServiceRegistration.Settings.cs`, `src/Tempest.Core/Invoicing/VatRateTaxTypeMapping.cs`; tests `Settings/**`, `…/Invoicing/VatRateTaxTypeMappingTests.cs` | B1, S1 | Simulator: reading cached, offline read returns last; corrupt/newer cache → null; missing/inactive/inapplicable tax type and unknown account code Blocked with reason; 3 calls per refresh |
| **X2** Contacts | `XeroContactLinker` (§5), M21 fix: invoices use `ContactID` | `Api/XeroAccountingApi.Contacts.cs`, `Contacts/XeroContactLinker.cs`, `Contacts/XeroContactMatcher.cs`, `XeroServiceRegistration.Contacts.cs`; tests `Contacts/**` | B1, B2, S1 | Candidates ranked VAT > ContactNumber > exact > similar; link existing writes nothing; create looks up `ContactNumber` first; lost-response create makes one contact; archived contact not linkable; details read-only |
| **X3** Quotes | planner + handlers `PushQuote`, `SetQuoteStatus`; drift notes | `Api/XeroAccountingApi.Quotes.cs`, `Sync/Quotes/XeroQuotePlanner.cs`, `Sync/Quotes/XeroQuotePushHandler.cs`, `Sync/Quotes/XeroQuoteMapper.cs`, `XeroServiceRegistration.Quotes.cs`; tests `Sync/Quotes/**` | X1, X2, B2 | §4.1 both directions over the simulator: approve+export→DRAFT; Rn+1 updates content; send→SENT; accept/decline; never an illegal transition; status-only update omits lines; INVOICED drift noted; same number; PDF uploaded once per content hash |
| **X4** Invoices | `XeroConnector` creates with `InvoiceNumber`, `ContactID`, account code, reference text; reconcile by number; `InterpretStatus` + DELETED; void of a Xero draft; draft update; `PushInvoiceDraft` on Unavailable; planner (attachment, update, delete) | `XeroConnector.cs` (all but B1's scope check), `XeroModels.cs`, `Api/XeroAccountingApi.Invoices.cs`, `Sync/Invoices/**`, `src/Tempest.Core/Invoicing/InvoicingService.cs`, `…/InvoiceRequest.cs`, `…/InvoiceRequestStatus.cs`, `XeroServiceRegistration.Invoices.cs`; tests `Sync/Invoices/**`, `…/Invoicing/Connectors/XeroConnectorTests.cs`, `…/Invoicing/InvoicingServiceJourneyTests.cs` | X1, X2, B2 | §4.2 tables over the simulator; D3: invoice never leaves DRAFT through TempestOS; unlinked client refused before Sending; lost response → one invoice; pre-v0.24 request still reconciles by Reference; existing invoicing tests green |
| **X5** POs and bills | planners + handlers for PO and expense bill (§4.3, §4.4) | `Api/XeroAccountingApi.PurchaseOrders.cs`, `Api/XeroAccountingApi.Bills.cs`, `Sync/Purchasing/**`, `XeroServiceRegistration.Purchasing.cs`; `src/Tempest.Core/Expenses/ProjectExpense.cs` (additive `SupplierOrganisationId`, `SourcePurchaseOrderId` — after Q3/Q6); tests `Sync/Purchasing/**` | X1, X2, B2 | Issued PO → DRAFT PO + PDF; cancel → DELETED; expense → ACCPAY DRAFT with category account, input tax, receipt; amend while DRAFT only; PO-sourced expense per Q6; recharges unchanged |
| **X6** Engine | `XeroSyncService` (drain, read-back, badges), change observer + start-up scan, backoff, audit, hosted service, `IXeroDocumentFileSource` over attachments | `Sync/XeroSyncService.cs`, `Sync/XeroChangeObserver.cs`, `Sync/XeroBackoff.cs`, `Sync/XeroReadBack.cs`, `Sync/AttachmentXeroDocumentFileSource.cs`, `Sync/XeroSyncHostedService.cs`, `XeroServiceRegistration.Sync.cs`; tests `Sync/Engine/**`, `XeroNeverApprovesOrSendsTests.cs` | X3, X4, X5 | §6 over the simulator: offline queue then drain; 429 pauses all for `Retry-After`; 5xx backoff; reauth pause/resume; crash with InFlight → reconcile, one record; restart scan enqueues a commit made before crash; per-document order; §7.3 item 3 |
| **U1** Settings UI (X0/X1) | Xero section: connection, granted vs required scopes, Re-authorise, company details *"from Xero, read at"*, tax/account mapping pickers, AllowLiveOrganisation switch; PDFs use Xero identity | `src/Tempest.Desktop/Views/SettingsView.cs`, `src/Tempest.Desktop/OrganisationIdentitySettings.cs`, `src/Tempest.Desktop/Documents/DocumentTemplate.cs` (`VatNumber`), new `src/Tempest.Desktop/Views/XeroSettingsSection.cs`; `tests/Tempest.Desktop.Tests/Xero/SettingsXero*Tests.cs` | X1 | Headless tests with fakes; offline shows last reading; switch audited |
| **U2** Contact link UI (X2) | link/create prompt from Customers & suppliers and from a Blocked badge | `src/Tempest.Desktop/Views/CustomersSuppliersView.cs`, new `src/Tempest.Desktop/Views/XeroContactLinkPrompt.cs`; `tests/Tempest.Desktop.Tests/Xero/ContactLink*Tests.cs` | X2 | PO confirms a candidate; create path; reason shown on failure |
| **U3** Badges + PDF attach on issue (X6) | Xero column/badge with Retry on quotes, invoices, POs, expenses; Export/Issue attach the rendered PDF to the record | `src/Tempest.Desktop/Views/QuotesView.cs`, `…/ProjectQuoteView.cs`, `…/InvoicingView.cs`, `…/PurchaseOrdersView.cs`, `…/ExpenseEntryPrompt.cs` (receipt), new `src/Tempest.Desktop/Views/XeroSyncBadge.cs`; `tests/Tempest.Desktop.Tests/Xero/Badge*Tests.cs` | X6 | Every badge state rendered; Retry calls `IXeroOutbox.RetryAsync`; export attaches the same bytes it saves |
| **X7** Forward cash | dashboard: Xero actuals (bank, bills, invoices due) + accepted-not-invoiced milestones by month | `src/Tempest.Core/Invoicing/AccountsReadModel.cs`, `…/AccountsReading.cs`, `…/AccountsRefreshService.cs`, `src/Tempest.Desktop/Views/Dashboards/BusinessDashboardView.cs`; tests `…/Invoicing/Accounts*Tests.cs`, Desktop dashboard tests | X6 | Months sum correctly; offline uses last reading; accepted-not-invoiced excludes anything already in Xero |
| **X8** Acceptance | live smoke test + script, runbook, setup guide, Release Notes, ADR to Accepted | `tests/Tempest.Core.Tests/Invoicing/Xero/Live/**`, `scripts/xero-demo-smoke.ps1`, `docs/guides/Xero Setup - Step by Step.md`, `docs/releases/v0.24.0/PO Test Runbook.md`, `docs/releases/v0.24.0/Release Notes.md`, `docs/adr/ADR-0162-*.md`, `docs/governance/Architecture/ADR Register.md` | all | §10.2 passes on the Demo Company; runbook walked by the PO |

Every task: `dotnet build` of both test projects with
`-p:TreatWarningsAsErrors=true`, its own tests plus the Invoicing and
Governance|Architecture filters green, `graphify update .`, no edits
outside its row.

## 12. Committed contracts (this change)

| File | Seam |
|---|---|
| `src/Tempest.Core/Invoicing/Xero/XeroScopes.cs` | exact scope strings (B1, S1, tests) |
| `src/Tempest.Core/Invoicing/Xero/Sync/XeroSyncContracts.cs` | `XeroDocumentKind`, `XeroDocumentRef`, `XeroLink`, `IXeroLinkStore`, `XeroOperation`, `XeroOutboxState`, `XeroOutboxEntry`, `IXeroOutbox`, `XeroPushOutcome`, `XeroPushResult`, `IXeroPushHandler`, `XeroPlannedOperation`, `IXeroSyncPlanner`, `XeroSyncBadge`, `XeroSyncStatus`, `XeroDrainReport`, `IXeroSyncService` |
| `src/Tempest.Core/Invoicing/Xero/Sync/XeroDocumentFile.cs` | `XeroDocumentFile`, `IXeroDocumentFileSource` |
| `src/Tempest.Core/Invoicing/Xero/Settings/XeroSettingsContracts.cs` | `XeroOrganisationProfile`, `XeroAddress`, `XeroBankAccount`, `XeroTaxRate`, `XeroAccount`, `XeroSettingsReading`, `IXeroSettingsReader` |
| `src/Tempest.Core/Invoicing/Xero/Contacts/XeroContactContracts.cs` | `XeroContactCandidate`, `XeroContactDetails`, `IXeroContactLinker` |
| `src/Tempest.Core/Invoicing/Xero/Api/XeroApiResult.cs` | `XeroApiResult<T>` |
| `tests/Tempest.Core.Tests/Invoicing/Xero/Simulator/XeroApiSimulator.cs` | simulator public surface (S1 implements) |

## 13. Open questions for the Product Owner

| # | Question | Default if unanswered |
|---|---|---|
| Q1 | The plan says an exported quote goes to Xero as **SENT**. Xero only allows content edits on a DRAFT quote, and a SENT quote can never return to DRAFT. May the Xero copy stay **DRAFT** until the quote is *Sent* in TempestOS, so an Rn+1 made after export can still update it? | DRAFT until Sent |
| Q2 | Xero purchase order status: **DRAFT** (consistent with D3, nothing approved by TempestOS) or **AUTHORISED** (approved; still no ledger posting)? | DRAFT |
| Q3 | A TempestOS expense records no supplier. Which Xero contact should its draft bill be against: an optional supplier on the expense, falling back to one configured contact (e.g. yourself, for reimbursement)? | optional supplier + configured fallback |
| Q4 | Bill number: TempestOS's `EXP-{id}`, or should the expense record the supplier's own invoice number? | `EXP-{id}`; supplier number later |
| Q5 | Invoice PDF attachment: visible on Xero's online invoice to the client (`IncludeOnline=true`), or kept internal? | internal (false) |
| Q6 | An expense recorded from a received PO's lines: also a separate draft bill, or left for Xero's own *Copy to bill* from the PO (avoids two bills)? | not pushed when the PO is in Xero |
| Q7 | When linking to an existing Xero contact, may TempestOS write its customer code into the contact's `ContactNumber` (an identifier, not billing details)? | yes, only when empty (Build Decisions Q7) |
| Q8 | Quotes and invoices raised before v0.24.0: push them on demand (a *Send to Xero* action), or leave them out? | on demand only |
| Q9 | Dropping `openid profile email` from the requested scopes (unused) — confirm, subject to the Demo Company check in B1. | drop if the check passes |
| Q10 | Which Xero accounts for sales and for each expense category (Travel, Subsistence, Materials, Subcontract, Other)? | none — pushes Blocked until chosen |

## 14. Sources

- [S1] Xero Developer changelog — https://developer.xero.com/changelog (read 2026-10-02)
- [S2] Xero, Scopes — https://developer.xero.com/documentation/guides/oauth2/scopes/
- [S3] XeroAPI/xero-mcp-server, `src/clients/xero-client.ts`, commit `f24583c` — https://github.com/XeroAPI/xero-mcp-server; PR #309 (quote transitions) — https://github.com/XeroAPI/xero-mcp-server/pull/309
- [S4] Apideck, "Xero Scopes: What Changed" — https://www.apideck.com/blog/xero-scopes; Databuzz, "Changes to Xero Accounting API Scopes" — https://support.databuzz.com.au/article/741-changes-to-xero-accounting-api-scopes
- [S5] XeroAPI/Xero-OpenAPI `xero_accounting.yaml` v19.1.0 — https://github.com/XeroAPI/Xero-OpenAPI
- [S6] Xero, OAuth 2.0 API limits — https://developer.xero.com/documentation/guides/oauth2/limits/; Limits FAQ — https://developer.xero.com/faq/limits; Rate limits — https://developer.xero.com/documentation/best-practices/api-call-efficiencies/rate-limits
- [S7] Xero, Idempotent requests — https://developer.xero.com/documentation/guides/idempotent-requests/idempotency/
- [S8] Xero, Quotes — https://developer.xero.com/documentation/api/accounting/quotes; Types and codes — https://developer.xero.com/documentation/api/accounting/types
- [S9] Xero, Invoices — https://developer.xero.com/documentation/api/accounting/invoices; Attachments — https://developer.xero.com/documentation/api/accounting/attachments
- [S10] Xero, HTTP response codes — https://developer.xero.com/documentation/api/http-response-codes

developer.xero.com renders client-side; [S2], [S6]–[S10] were read
through search-engine extracts and the Limits FAQ, cross-checked against
[S3] and [S5]. Every claim taken only from an extract is either enforced
defensively (lookup before resend, 10 MB cap, status-only updates omit
lines) or verified by the live smoke test (§10.2).
