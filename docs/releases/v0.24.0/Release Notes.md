# TempestOS v0.24.0 — Release Notes

**Status: draft.** Awaiting the Product Owner's run of
`docs/releases/v0.24.0/PO Test Runbook.md` against the Xero Demo
Company and the live smoke test (`scripts/xero-demo-smoke.ps1`), which
has not been run against the real Demo Company yet (its open items below
are still to be filled). The forward cash picture (task X7) is in this
build; runbook section XF walks it. Statements marked *(needs …)* describe
behaviour delivered by a review-board fix merged separately; the
integrator confirms each before release. Decision record: `ADR-0162`
(Accepted).
Design: `Xero Integration Plan.md` (D1–D7), `Xero Technical Design.md`,
`Xero Build Decisions.md` (Q1–Q10 defaults).

## Summary

`v0.24.0` connects TempestOS to Xero for every document that reaches the
accounts. TempestOS stays the place where quotes are written, approved and
accepted, and where purchase orders and expenses are recorded; Xero gets a
copy of each with the same number and the same PDF. Invoices go to Xero as
**drafts** for the Product Owner to approve and send from Xero. Company
details, VAT rates and account codes are read from Xero. Nothing is
approved, posted or emailed by TempestOS.

## What changed

- **Xero scopes (X0).** TempestOS now asks for `accounting.invoices`,
  `accounting.contacts`, `accounting.settings.read`,
  `accounting.attachments`, `accounting.reports.banksummary.read` and
  `offline_access`. `accounting.contacts.read` is replaced by
  `accounting.contacts` (so TempestOS can create a contact; fixes review
  item M21) and `openid profile email` are dropped (Q9). A connection made
  before v0.24.0 reads **Re-authorise needed** until it is re-authorised;
  nothing is lost meanwhile — writes wait.
- **Settings → Xero (X1, U1).** A new section shows the connection, the
  required, granted and missing scopes, **Re-authorise**, the company
  details *from Xero, read at …*, a tax type per VAT rate and an account
  code for sales and per expense category (chosen from what Xero holds;
  Q10), the *General expenses* contact (Q3), whether invoice PDFs show on
  Xero's online invoice (off, Q5), the sync counts with **Retry all**, and
  the **Allow live organisation** switch (off; audited).
- **Company details from Xero (X1, D6).** Quote, invoice and timesheet
  PDFs print the organisation's name, address, VAT and company numbers and
  bank details as Xero holds them; offline, the last reading is used.
  Every line's tax type and account code is checked against Xero before it
  is sent; one Xero lacks is held back with the reason.
- **Customers and suppliers linked to Xero contacts (X2, U2).** Customers
  & Suppliers → **Link to Xero…** offers Xero's contacts, strongest match
  first (VAT number, customer code, exact name, similar name); nothing is
  linked until you confirm, or **Create in Xero** makes one. Billing
  address, VAT number and payment terms are then read from Xero and shown
  read-only. TempestOS writes only its customer code into an empty
  contact number (Q7).
- **Quotes to Xero (X3, D2).** An approved quote, once exported or sent,
  becomes a Xero quote with the same number, lines and expiry, the
  revision in its reference and the PDF attached. It stays *Draft* in Xero
  until it is sent in TempestOS, so a later revision can still update it
  (Q1); Send, Accept and Decline set the Xero status. A quote invoiced in
  Xero is flagged, because raising it from TempestOS too would bill twice.
- **Invoices as Xero drafts (X4, D3, D4).** Send creates the Xero draft
  with TempestOS's own invoice number, linked to the contact by its Xero
  id, the PDF attached. The badge reads **Draft in Xero — review and send
  from Xero**, then Awaiting payment, Paid or Voided as Xero reports.
  Editing after send updates the draft only while Xero holds it as a
  draft; voiding deletes the Xero draft only.
- **Purchase orders and expense bills (X5, D5).** An issued purchase
  order becomes a Xero **draft** purchase order with its PDF (Q2);
  cancelling deletes it. A recorded expense becomes a Xero **draft bill**
  against its supplier (or the General expenses contact, Q3), numbered by
  the supplier's invoice number or `EXP-…` (Q4), with the receipt
  attached and the recorded VAT kept. Expenses recorded from a received
  purchase order are not billed again (Q6). Recharges are unchanged.
- **One Xero badge per record (X6, U3).** Quotes, invoices, purchase
  orders and expenses (Invoices → *Expense bills in Xero*) show Not sent,
  Queued, In Xero (draft), In Xero, Awaiting payment, Paid, Voided,
  Deleted in Xero, Failed (with the reason), Can't tell or Waiting for
  authorisation, with **Retry**, **Send again** (a purchase order or bill
  deleted in Xero) and **Send to Xero** (a quote, purchase order or expense
  raised before Xero sync began, Q8). Every write of a quote, invoice
  draft, purchase order, bill or attachment goes through one durable
  outbox: it survives a restart, waits while offline, honours Xero's rate
  limits and is never made twice. Creating a contact and the Q7 contact
  number are written directly when you confirm them in **Link to Xero…**,
  looked up by the customer code first so they are never made twice
  (`ADR-0162` §4).
- **Forward cash picture (X7).** Business → Dashboard gains **Forward
  cash — next 6 months**: for each calendar month, opening cash, money in
  (invoices TempestOS raised in Xero and not yet paid, plus accepted
  quotes' work not yet invoiced, dated by its milestone), money out
  (approved bills and repeating bills from Xero) and closing cash. Every
  figure names its source (*Xero …, read at …*; *TempestOS*; *worked
  out*), amounts are gross including VAT, accepted work already invoiced
  in Xero is counted once, and with no accounts reading Xero's figures
  read *unavailable* with the reason, never zero. Offline it shows the
  last reading and says the refresh failed.
- **The safety rules are in code (ADR-0162).** One handler below every
  Xero call refuses any request that would approve an invoice, bill or
  purchase order, email anyone, set *sent to contact*, write outside
  contacts, quotes, invoices, purchase orders and their attachments, or
  write to an organisation that is not the Demo Company while *Allow live
  organisation* is off.
- **Demo Company smoke test (X8).** `scripts/xero-demo-smoke.ps1` runs the
  live tests (`Category=XeroLive`) against the Demo Company with the stored
  tokens: connect, read, contact, quote → sent → accepted, invoice draft,
  purchase order draft, bill draft with receipt, repeated creates, and the
  open items below. A second journey does the same through TempestOS's
  own production path (contact linker, quote, purchase order and bill
  planners, mappers and handlers, the invoicing service and the sync
  engine with its read-back), so the code the app runs meets the real
  Xero, not only hand-built requests. They refuse to write unless Xero
  reports `IsDemoCompany`, and are skipped (not failed) in CI and without
  the Demo Company's credentials. Both journeys run against the in-process
  Xero simulator on every CI run.
- **Documentation.** `docs/guides/Xero Setup - Step by Step.md` (new
  scopes, Demo Company first, Re-authorise, the live switch, the smoke
  script); `docs/releases/v0.24.0/PO Test Runbook.md` (the steps);
  `PHYSICAL_REVIEW.md` §7m (a short Xero walk pointing at the runbook).
- **Version.** `VERSION` reads `0.24.0`: the test build installs into
  `C:\TempestOS-rc24-data` and its title bar reads `TempestOS 0.24.0 (…)`.

## Product Owner decisions (D1–D7, 2026-10-02)

| # | Decision | Where it is enforced |
|---|---|---|
| D1 | TempestOS stays where quotes are written, approved and accepted; acceptance generates deliverables and milestones | Quote domain unchanged; Xero never changes a TempestOS quote |
| D2 | Xero holds a read-only copy of each quote, PDF attached, status following TempestOS | X3 planner; Xero quotes only move `DRAFT → SENT → ACCEPTED/DECLINED` |
| D3 | Invoices are created in Xero as **DRAFT**; TempestOS never approves | Write models allow only `DRAFT`/`DELETED`; the safety handler blocks anything else; simulator and architecture tests |
| D4 | The PO sends invoices manually from Xero; TempestOS never emails a client | No email method; the handler blocks `/Email` and `SentToContact` |
| D5 | Purchase orders and expenses also go to Xero | X5: draft purchase orders and draft bills |
| D6 | Xero is the source of truth for company details, VAT rates and account codes | X1 reader and cache; Settings shows them read-only |
| D7 | Testing against the Demo Company before the live organisation | Writes only to `IsDemoCompany` until **Allow live organisation** is on (audited); links are per organisation |

## Build defaults to confirm (Q1–Q10)

Chosen by the chief engineer so the build was not blocked; each is easy to
change. Please confirm or change each one.

| Q | Default in this build | Confirm |
|---|---|---|
| Q1 | A quote goes to Xero as **DRAFT** on export and becomes **SENT** only when sent in TempestOS; revisions update it while it is a draft | |
| Q2 | Purchase orders go to Xero as **DRAFT** | |
| Q3 | An expense may name a supplier; with none, its bill goes against one configured *General expenses* contact (none chosen → the bill waits) | |
| Q4 | Bill number: the supplier's invoice number when entered, else `EXP-{expense id}` | |
| Q5 | Attached PDFs are **not** shown on Xero's online invoice (Settings switch to change) | |
| Q6 | An expense recorded from a received purchase order's lines is not billed separately (use Xero's *Copy to bill*) | |
| Q7 | Linking an existing contact writes TempestOS's customer code into its contact number only when that is empty | |
| Q8 | Nothing raised before Xero sync began is pushed automatically: quotes, purchase orders and expenses are sent on demand (**Send to Xero**); invoices have no **Send to Xero** — one already in Xero from before v0.24.0 is imported as a link, and an unsent invoice goes through **Send** as usual | |
| Q9 | `openid profile email` dropped (confirmed by the smoke test, open item F3) | |
| Q10 | Account codes chosen in Settings from Xero's chart; defaults are the UK Demo Company's (200 Sales; 400-series expenses) | |

## Open items the Demo Company smoke test confirms

Run `scripts/xero-demo-smoke.ps1` (with `-KeyWindow` for F1) and copy the
report's *Open items* table here.

| Item | Design assumption | Result |
|---|---|---|
| F1 | Xero keeps an `Idempotency-Key` about 6 minutes; TempestOS relies on only 5 | *to be filled from the smoke report* |
| F2 | Uploading a PDF again under the same name replaces the earlier one | *to be filled* |
| F3 | The connection and every call work without `openid profile email` (Q9) | *to be filled* |
| F4 | A contact can be created under `accounting.contacts` | *to be filled* |

## Known limitations

- **5-minute key window for lost creates.** If Xero's answer to creating
  a purchase order or bill is lost and the sync cannot recover it within
  5 minutes (`IdempotencyKeyLifetime`, kept under Xero's documented 6), the
  badge reads **Can't tell** and TempestOS never re-sends it, so it never
  makes a second record. Someone must check Xero. The engine recovers lost
  creates first on every drain, so this needs TempestOS to be closed or
  offline for most of those 5 minutes straight after the create. Open item
  F1 confirms the window on the real Xero. A *Can't tell* badge offers no
  **Retry** *(needs m15)*.
- **Loopback port.** This build still listens on `49301` for the Xero
  sign-in (`TD-183`, the move to `48131`, has not merged); register the
  port the browser shows.
- **Going live.** Do not turn on *Allow live organisation* (runbook XG7)
  until the integrator confirms review-board items M1, M2 and M7 are
  merged: before them, switching from the Demo Company can copy Demo test
  records into the live books, an expired token offline reads as
  *Re-authorise*, and Xero rounds a unit price with more than two decimal
  places, so its totals can differ from TempestOS's.

Fixed in the build's clean-up pass (C1) and no longer limitations: an
expense's supplier and invoice number are saved in its first revision
(`b7c8530a`); Re-authorise keeps unsaved picker choices unless the
organisation changed (`ff0b9b64`); a credit-card account is never printed
as bank details (`958d7002`); a 429 on the invoice path pauses every write
and survives a restart (`5a706608`); the contact-link look-up and the
settings-cache edge cases (C1 U2, X1-1 to X1-6).

## ADR

- `ADR-0162` — *Xero Holds Drafts and Copies; TempestOS Never Approves or
  Sends; Every Push Goes Through One Durable, Idempotent Outbox* —
  **Accepted** (was Proposed at design time). It extends `ADR-0151` and
  amends its §9 (`XeroConnector`): invoices now name the contact by
  `ContactID` and are reconciled by invoice number. It records that
  contact create and the Q7 contact-number write are made directly, not
  through the outbox, and why.

## Upgrading from v0.23.0

1. Install with `scripts/install-test-build.ps1`.
2. Settings → **Xero** reads **Re-authorise needed**: click
   **Re-authorise** and pick the **Demo Company**.
3. Existing Xero invoices keep reconciling (they are imported as links on
   first read). Quotes, purchase orders and expenses raised earlier are
   sent only when you click **Send to Xero**.

## Gate

*To be filled at release:* build of both test projects with
`-p:TreatWarningsAsErrors=true`; Core and Desktop suites; the Demo
Company smoke report; the runbook digest.
