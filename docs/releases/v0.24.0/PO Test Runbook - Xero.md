# Product Owner Test Runbook — v0.24.0 Xero integration

**Tester:** Steven (Product Owner). **Date:** ____________. **Build (title bar):** ____________.
**Data folder:** `C:\TempestOS-rc24-data` (from `scripts/install-test-build.ps1`). **Xero organisation:** the **Demo Company (UK)** for every section except XG4–XG7.

**How to score.** Fill *Result* with `PASS`, `FAIL` or `SKIP` (with why). A
FAIL is anything that differs from *Expect*; quote the wording you saw in
*Comments*. Fill the Digest at the bottom last.

**What v0.24.0 does** (`ADR-0162`, decisions D1–D7, build defaults Q1–Q10
in `Xero Build Decisions.md`): TempestOS stays the master for quotes,
purchase orders and expenses; Xero gets a copy of each with the same
number and the same PDF. Invoices go to Xero as **drafts only** — you
approve and send them from Xero. TempestOS never approves an invoice and
never emails anyone. Company details, VAT rates and account codes are read
from Xero. Each quote, invoice, purchase order and expense shows one
**Xero** badge.

**Badge words** (used throughout): *Not sent* · *Queued* · *In Xero
(draft)* · *Draft in Xero — review and send from Xero* (invoices) ·
*Sent in Xero* / *Accepted in Xero* / *Declined in Xero* / *Invoiced in
Xero* (quotes) · *In Xero* · *Awaiting payment* · *Paid* · *Voided* ·
*Deleted in Xero* · *Failed* (with the reason) · *Can't tell* ·
*Waiting for authorisation*. Buttons on a badge: **Retry**, **Send
again**, **Send to Xero**.

Every check "in Xero" means: open the Demo Company in your browser and
find the record by its number (Business → Invoices / Quotes / Purchase
orders / Bills to pay; Contacts).

---

## XA. Install, connect the Demo Company, smoke script

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XA1 | Run `scripts/install-test-build.ps1`; launch from the *TempestOS 0.24.0 (test)* shortcut | Title bar reads `TempestOS 0.24.0 (…)` | | |
| XA2 | In the browser: Xero → organisation menu → **My Xero** → open (or **Try**) the **Demo Company** | *Demo Company (UK)* opens | | |
| XA3 | Follow `docs/guides/Xero Setup - Step by Step.md` Parts 1–3, picking **Demo Company (UK)** on Xero's consent page | Consent page lists the six scopes; no *openid*, *profile* or *email*; Settings → *Connector authorisation* reads **"Authorised."** | | |
| XA4 | (Developer PC) `pwsh -NoProfile -File scripts/xero-demo-smoke.ps1 -DataFolder C:\TempestOS-rc24-data -Keep` | Ends **RESULT: PASSED**; report lists steps S01–S20 *pass*; open items F2, F3, F4 *CONFIRMED* (F4 *NOT RUN* on a second run is fine) | | |
| XA5 | Open each *[XERO]* link the script printed | Contact *TempestOS Smoke*; quote `SMOKE-Q-…` **Accepted** with one PDF; invoice `SMOKE-INV-…` **Draft**, PDF attached, never sent; purchase order `SMOKE-PO-…` **Draft** with PDF; bill `SMOKE-EXP-…` **Draft** with a receipt image | | |
| XA6 | (Optional, 8 minutes) run XA4 again with `-KeyWindow` | Report line **F1 … CONFIRMED** (Xero kept the idempotency key past 5½ minutes) | | |
| XA7 | Delete the `SMOKE-…` drafts and the accepted smoke quote in Xero by hand | Gone from Xero | | |

## XS. Settings → Xero section

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XS1 | Settings → scroll to **Xero** | Section present (only when Xero is the connector). *Connection*: **"Connected to Xero."** Lines *Required scopes:*, *Granted:* (the six scopes) and *Missing: none* | | |
| XS2 | **Refresh from Xero** | **"Reading Xero…"** then **"Read Demo Company (UK) from Xero."**; *Organisation: Demo Company (UK)*; **"Demo Company: yes — Xero's Demo Company; TempestOS may write drafts to it."** | | |
| XS3 | Read *Company details* | **"Company details from Xero, read at <time>"** with the Demo Company's name, address, phone, VAT No., Company No. and bank account | | |
| XS4 | Export any quote PDF (XQ2) and look at its header and footer | The company details from XS3 (D6), not the ones typed under *Organisation identity* | | |
| XS5 | *Tax types (per VAT rate)* and *Account codes* | Each picker lists what Xero holds; **Default** selected; sales default `200`; each expense category (Travel, Subsistence, Materials, Subcontract, Other) has a 400-series default (Q10). Status: **"Every saved tax type and account code is active in Xero."** | | |
| XS6 | *Xero General expenses contact*: pick *TempestOS Smoke* (or any Demo contact) → **Save** | Saved; after a restart it reads back (Q3) | | |
| XS7 | *Show attached invoice PDFs to the client on Xero's online invoice* | **Off** by default; caption *Q5: off by default — a PDF attached to a Xero invoice is kept internal to Xero.* | | |
| XS8 | *Allow live organisation* | **Off**; caption **"Off: TempestOS writes only to Xero's Demo Company."** and **"Connected to the Demo Company: writes are allowed whatever this switch says."** | | |
| XS9 | Sync line under *Bills and invoices* | **"Sync: 0 queued · 0 failed · 0 waiting for authorisation."** (numbers change as you work); **Retry all** answers **"Nothing to retry."** when nothing failed | | |
| XS10 | Turn Wi-Fi off → restart TempestOS → Settings → Xero | Shows the last reading: **"Showing the last reading (from Xero, read at …)."**; PDFs still use it. Turn Wi-Fi back on | | |
| XS11 | (Only if the Demo Company was connected before v0.24.0) open Settings → Xero | **"Re-authorise needed."**, *Missing:* lists `accounting.contacts`, `accounting.settings.read`, `accounting.attachments` — **Re-authorise** → consent → **"Connected to Xero."** | | |

## XC. Customers & Suppliers — contact linking

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XC1 | Business → **Customers & Suppliers** → add *Basket Case Ltd* (a name the Demo Company already has as a contact), type **Customer** | Record shows **"Not linked to a Xero contact. Documents for this organisation wait until it is linked."** and **Link to Xero…** | | |
| XC2 | **Link to Xero…** | Prompt *Link Basket Case Ltd to Xero*: **"Searching Xero for matching contacts…"** then the candidates, strongest first, each with why (*Matched on VAT number*, *Xero's contact number is this customer code*, *Same name*, *Similar name*). Nothing linked yet | | |
| XC3 | Select the right contact → **Link to selected contact** | **"Linked 'Basket Case Ltd' to its Xero contact."**; the record shows *Xero contact …* with *(you chose it)* | | |
| XC4 | Read the Xero details on the record | *Xero billing address*, *Xero VAT number*, *Xero payment terms* read-only, with **"From Xero, read at <time>. Change these in Xero; TempestOS never sends them."** | | |
| XC5 | In Xero, the contact's *Contact number* field | Shows the TempestOS customer code if it was empty before (Q7); otherwise unchanged. Nothing else on the contact changed | | |
| XC6 | Add *Smoke Test Supplier Ltd* (no such contact in Xero), type **Supplier** → **Link to Xero…** | **"Xero has no contact that looks like this organisation…"** → **Create in Xero** → **"Created 'Smoke Test Supplier Ltd' in Xero and linked it."** In Xero: one new contact with the customer code as its contact number | | |
| XC7 | On the XC6 record, **Unlink from Xero**; then **Link to Xero…** again | **"Unlinked 'Smoke Test Supplier Ltd' from its Xero contact. Nothing was changed in Xero; …"** — the Xero contact still exists. The new search offers it first (*Xero's contact number is this customer code*); link it again. Xero still holds one such contact | | |
| XC8 | In Xero, archive the contact linked in XC3; in TempestOS **Refresh from Xero** on the record | The record says it is archived in Xero and documents for it are blocked: restore it in Xero, or unlink it and link another. Restore it in Xero afterwards | | |
| XC9 | Wi-Fi off → **Link to Xero…** | The prompt says Xero could not be reached; nothing is linked. Wi-Fi on | | |

## XQ. Quotes (D1, D2, Q1)

Use a project for *Basket Case Ltd* (XC3).

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XQ1 | Project → **Quote** tab: add two lines → **Submit for review** → **Approve** | Quote badge **Xero: Not sent** (nothing goes to Xero before issue) | | |
| XQ2 | **Export** (save the PDF) | Badge **Xero: Queued**, then within a minute **Xero: In Xero (draft)**. In Xero: a quote with the **same number**, *Reference* `R1`, the same lines and totals, status **Draft**, the exported PDF attached (same file) | | |
| XQ3 | Edit a line → resubmit → **Approve** (R2) → **Export** | Badge returns to **In Xero (draft)**; in Xero the same quote now has R2's lines, *Reference* `R2`, and the R2 PDF under the same file name (one file, or two if Xero kept both — note which) | | |
| XQ4 | **Send** | Badge **Xero: Sent in Xero**. In Xero: status **Sent**. Xero sent **no email** to the contact (check the quote's history) | | |
| XQ5 | **Accept** | Badge **Xero: Accepted in Xero**; in Xero **Accepted**; TempestOS creates the deliverables as before (D1) | | |
| XQ6 | A second quote: approve → Export → Send → **Decline** | Badge **Declined in Xero**; in Xero **Declined** | | |
| XQ7 | In Xero, on the XQ5 quote, **Create invoice** (Xero's own action); TempestOS → wait a minute or Settings → Xero → **Refresh from Xero** | Quote badge **Xero: Invoiced in Xero** (attention colour) with the note that raising it from TempestOS too would bill twice. Delete that Xero invoice afterwards | | |
| XQ8 | In Xero, delete the XQ6 quote | Its badge reads **Deleted in Xero** | | |
| XQ9 | Business → **Quotes** list | Each quote row has its Xero badge, the same words as on the Quote tab | | |
| XQ10 | A quote raised before v0.24.0 (if your data has one) | Badge **Not sent** with **Send to Xero** (*Raised before Xero sync began: send it to Xero now*, Q8). Click it → **"Queued for Xero."** → **In Xero (draft)** or the status matching TempestOS | | |

## XI. Invoices (D3, D4, Q5)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XI1 | Business → **Invoices** → raise an invoice for an XQ5 deliverable → **Send** | Status **Sending**, badge **Queued**, then the badge reads exactly **Xero: Draft in Xero — review and send from Xero** | | |
| XI2 | In Xero: Invoices → **Draft** tab | The invoice is there with the **same invoice number** as TempestOS, the contact from XC3, *Reference* `<project code> · <deliverable>`, the same lines, account `200`, VAT 20% (`OUTPUT2`), the PDF attached. It is **Draft** — not *Awaiting approval*, not *Awaiting payment* | | |
| XI3 | In Xero, open the attachment's settings | *Show on online invoice* is **off** (Q5) | | |
| XI4 | Check your mailbox and the contact's history in Xero | **No email** was sent by TempestOS or by Xero (D4) | | |
| XI5 | In TempestOS, edit a line of the sent invoice (while Xero still holds the draft) | The Xero draft updates to match; badge stays **Draft in Xero — …** | | |
| XI6 | In Xero, **Approve** the invoice; TempestOS → wait a minute | Badge **Awaiting payment**; the invoice reads *Accepted* with the issued date from Xero | | |
| XI7 | In TempestOS, try to edit that invoice again | Refused: Xero holds it as AUTHORISED; change it in Xero | | |
| XI8 | In Xero, record a full payment; TempestOS → wait a minute | Badge **Paid**; paid date read from Xero (paid is read, never set) | | |
| XI9 | A second invoice: Send → in TempestOS **Void** it while it is a Xero draft | Xero draft becomes **Deleted**; badge **Voided** | | |
| XI10 | A third invoice for an organisation that is **not linked** (XC7 unlinked state) → **Send** | Refused before sending, naming the missing Xero contact link; nothing in Xero | | |
| XI11 | Look for any button that approves or emails an invoice in TempestOS | There is none. Invoices never offer **Send to Xero** either (they go through Send) | | |

## XP. Purchase orders (D5, Q2)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XP1 | Business → **Purchase orders** → new order to *Smoke Test Supplier Ltd* (XC6), one line → **Issue** | The order keeps its PDF; badge **Queued**, then **Xero: In Xero (draft)** | | |
| XP2 | In Xero: Purchases → Purchase orders → **Draft** | Same number, the supplier, delivery date, line with input VAT (`INPUT2`) and the expense account, PDF attached. **Draft**, not approved; no email sent | | |
| XP3 | **Cancel** a second issued order (after it reached *In Xero (draft)*) | In Xero that order is **Deleted**; badge **Deleted in Xero** | | |
| XP4 | In Xero, delete the XP1 order by hand | Badge **Deleted in Xero** with **Send again** (*Send it to Xero again as a new draft (the deleted one stays deleted)*) | | |
| XP5 | Click **Send again** | **"Queued to be sent to Xero again as a new draft."** → **In Xero (draft)**; in Xero a new draft with the same number; the deleted one stays deleted | | |
| XP6 | In Xero, on a draft PO, **Copy to bill**; then in TempestOS **Receive** the order and record its lines as expenses | The expenses are **not** pushed as separate bills (Q6): their badge says why (the purchase order's own bill in Xero). Xero holds one bill, the copied one | | |

## XE. Expenses → draft bills (D5, Q3, Q4)

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XE1 | Business → **Timesheets** (or a project's Details) → **Record expense**: Travel, net 50, VAT 10; **Supplier…** → *Smoke Test Supplier Ltd*; *Supplier invoice number* `INV-7781`; **Receipt…** → a photo (JPEG/PNG) or PDF | Expense recorded; receipt attached | | |
| XE2 | Business → **Invoices** → expand **Expense bills in Xero** | The expense is listed with its badge: **Queued** then **In Xero (draft)** | | |
| XE3 | In Xero: Purchases → **Bills to pay → Draft** | Bill `INV-7781` for the supplier, one line: account for Travel, `INPUT2`, net 50, VAT **10.00** exactly as recorded, receipt attached. **Draft** | | |
| XE4 | Record a second expense with **No supplier (Xero: General expenses)** and no invoice number | Bill against the contact chosen in XS6, numbered `EXP-…` | | |
| XE5 | Amend the XE1 expense's amount while the bill is a draft | Xero draft updates | | |
| XE6 | In Xero, approve the XE1 bill; amend the expense again in TempestOS | Badge **Failed** — *approved in Xero; change it there*; **Retry** does not change Xero | | |
| XE7 | Delete the XE4 expense | Its Xero draft bill becomes **Deleted** | | |
| XE8 | Recharge an expense to a client (as before v0.24.0) | Unchanged: the recharge invoice is raised as before; the expense's own bill is separate | | |

## XR. Retry, Send again, Send to Xero, Draft-in-Xero wording

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XR1 | Unlink *Basket Case Ltd* (XC3); export a new quote for it | Badge **Failed** with the reason naming the missing contact link, and **Retry** | | |
| XR2 | Link it again (XC3) | Within a minute the quote goes out by itself, badge **In Xero (draft)** (a blocked push is re-planned when the link appears) | | |
| XR3 | Make a push fail, then fix the cause: Settings → Xero → pick sales account `260` (Other Revenue) → **Save**; archive account 260 in Xero → **Refresh from Xero**; export a quote (badge **Failed**, naming the account); pick **Default** → **Save** → **Retry** on the badge. Restore account 260 in Xero afterwards | **"Queued for Xero again."** → **In Xero (draft)** | | |
| XR4 | Settings → Xero → **Retry all** with two failed writes | **"Queued 2 failed write(s) to Xero again."** | | |
| XR5 | Hover each badge with a reason | The detail line under the badge shows the reason in words; automation name *Xero status for <number>* | | |
| XR6 | Every sent invoice in Business → Invoices | Reads **Draft in Xero — review and send from Xero** until you approve it in Xero; never "Sent to client" | | |
| XR7 | **Send again** appears only on a purchase order or bill deleted in Xero (XP4); **Send to Xero** only on a quote or purchase order raised before v0.24.0 (XQ10) | As stated; neither appears on invoices | | |

## XO. Offline behaviour

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XO1 | Wi-Fi off. Approve and export a quote, issue a purchase order, record an expense, send an invoice | Everything succeeds in TempestOS; each badge reads **Queued**; the invoice request goes back to Draft and its Xero draft is queued | | |
| XO2 | Close TempestOS, reopen it (still offline) | Badges still **Queued**; nothing lost | | |
| XO3 | Wi-Fi on; wait up to a minute | Each badge moves to its *In Xero* state; in Xero exactly **one** of each record (no duplicates) | | |
| XO4 | Settings → Xero while offline | Last reading shown with its time (XS10); tax and account checks use it | | |

## XG. The Demo guard (D7) and the live organisation

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XG1 | Settings → Xero, connected to the Demo Company | **"Demo Company: yes …"**; *Allow live organisation* off; writes work (sections XQ–XE) | | |
| XG2 | Run `scripts/xero-demo-smoke.ps1` while connected to the Demo Company | Passes (XA4) | | |
| XG3 | Look for the audit row of any switch change so far | None unless you changed the switch | | |
| XG4 | **Only after XA–XO pass.** Settings → Xero → **Re-authorise** → pick **your own organisation** → **Refresh from Xero** | **"Demo Company: no — a live organisation."** and **"Writes to <organisation> are blocked until this switch is on and saved."** | | |
| XG5 | Export a quote now | Badge **Failed** — *TempestOS blocked the request (D7.live-organisation)*; nothing appears in your live Xero | | |
| XG6 | Run the smoke script now | **REFUSED - not the Demo Company; nothing was written**; script exits FAILED; nothing in your live Xero | | |
| XG7 | Decide with the project chat whether to go live. If yes: tick **Allow live organisation** → read the warning → **Save** → **Retry** the XG5 quote | **"Allowed: TempestOS writes drafts to <organisation>, your live organisation."**; audit row `xero.live-organisation.allowed`; the quote reaches your live Xero as a **draft** copy. Customers must be linked again for the live organisation (links never cross organisations). If not going live: untick → **Save**, Re-authorise with the Demo Company | | |

## XF. Forward cash picture (X7) — PLACEHOLDER

> **Placeholder, to be completed when task X7 merges.** The Business
> dashboard's forward cash picture (Xero bank, bills and invoices due,
> plus accepted-but-not-invoiced milestones from TempestOS, by month) is
> built separately. Its steps (XF1…) will be added here with X7's merge:
> months sum correctly; offline uses the last reading; accepted work
> already invoiced in Xero is not counted twice.

| # | Action | Expect | Result | Comments |
|---|---|---|---|---|
| XF1 | *(added with X7)* | *(added with X7)* | | |

## XL. The smoke script, step by step (what XA4 checked)

| Script step | Checks | Runbook step it backs |
|---|---|---|
| S01–S02 | Connected; the grant has all six scopes | XA3, XS1 |
| S03–S06 | Organisation, tax rates, accounts, bank summary read; **IsDemoCompany**; codes exist | XS2–XS5, XG1 |
| S07, S07a | Contact *TempestOS Smoke* linked or created; a repeated create makes no second contact | XC2–XC6 |
| S08–S11a | Quote DRAFT + PDF, second PDF same name, SENT, ACCEPTED, read back | XQ2–XQ5 |
| S12–S14 | Invoice DRAFT + PDF (not online), read back still DRAFT, never sent to the contact | XI1–XI4 |
| S15–S16 | Purchase order DRAFT + PDF | XP1–XP2 |
| S17–S18 | Bill DRAFT + receipt, recorded VAT kept | XE1–XE3 |
| S19 | Drafts deleted (unless `-Keep`) | XI9, XP3, XE7 |
| S20 | Nothing that left the PC approved, emailed or wrote outside the allow-list | XI4, XI11 |
| F1–F4 | Key retention, attachment replace-by-name, scopes without openid, contact create | Release Notes "To confirm" |

---

## Digest

| Section | PASS | FAIL | SKIP | Notes |
|---|---|---|---|---|
| XA Install and smoke | | | | |
| XS Settings → Xero | | | | |
| XC Contact linking | | | | |
| XQ Quotes | | | | |
| XI Invoices | | | | |
| XP Purchase orders | | | | |
| XE Expense bills | | | | |
| XR Retry / Send again / Send to Xero | | | | |
| XO Offline | | | | |
| XG Demo guard | | | | |
| XF Forward cash (X7) | | | | placeholder |

**Smoke report file:** ____________ **Open items F1–F4:** ____________ **Go live (XG7)?** ____________
