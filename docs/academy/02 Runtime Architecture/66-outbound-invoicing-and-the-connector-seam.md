# Outbound Invoicing and the Connector Seam

**Release:** `v0.19.0` release candidate on `release/v0.19.0` (superseded
for testing by `v0.19.1`); `WP 19.10D` lands on the `v0.19.1` candidate and
`WP 20.1B` on the `v0.20.0` candidate — none released · **Work Package(s):**
`WP 19.1A` (parts 1–3), `WP 19.1A-R1`, `WP 19.9.0`, `WP 19.10D`, `WP 20.1B` ·
**Debt:** `TD-183` · **Decision:** `ADR-0151` (and its two addenda) · **Code:** `Tempest.Core.Invoicing`,
`Tempest.Core.Invoicing.OAuth`, `Tempest.Core.Invoicing.Xero`,
`Tempest.Core.Invoicing.QuickBooksOnline`, `Tempest.Core.Secrets`,
`Tempest.Desktop.Views.InvoicingView`

**In plain terms.** When an engineer marks a piece of client work as
done, TempestOS can ask an outside accounting package — Xero, or
QuickBooks Online — to raise a draft invoice for it, so nobody retypes
hours and prices into a second system. This chapter is about the safety
rules wrapped around that one outside call: it must never create the
same invoice twice, it must never pretend to know something got paid
when only the accounts package genuinely knows that, and the one piece
of code that talks to Xero must be the only place in the platform that
does. Everything else stays exactly as trustworthy whether Xero answers
instantly, refuses, or does not answer at all.

## Why this is not another engineering discipline

Every governed act built before this — Evidence's Check and Issue
(`ADR-0148`), a timesheet's frozen rate (`ADR-0150`) — decides something
from data the platform already holds. `ADR-0151` (`750b179`) names the
difference: *"a connector call is different — its answer comes from a
third party over a network, and fails in ways a rate-card lookup never
does."* A
**connector** is the one piece of code that speaks to a given outside
service, so those failure modes are handled in exactly one place, never
scattered wherever a button raises an invoice. The scope stays narrow
too: no ledger, tax computation or payment processing — one invoice
request from figures already frozen elsewhere, "not ERP, not PLM"
(`D-028`).

## A network call is a state, never a retry loop

`IInvoicingConnector`'s four connector calls each return a `ConnectorResult<T>`
with one of five outcomes — `Ok`, `Rejected`, `Reauthorise`,
`Unavailable`, `Unknown` — never a thrown exception; `ADR-0151` treats an
exception out of an implementation as a defect in it, not a normal
outcome to catch. In plain terms: instead of code that quietly retries
when something goes wrong, every call answers with one honest word for
what happened, and that word is what the user sees — never a spinner
hiding "refused" from "unreachable." `FakeInvoicingConnector` is the
only implementation `WP 19.1A` part 1 ships — scriptable per call or
pinned to one idempotency key — and every part-1 test runs against it;
parts 2/3 add real connectors without changing `InvoicingService`,
`InvoiceRequest`, or one line of that suite (`ed04ec3`, `2cd22e9`).

## The request's own id is the idempotency key

An **idempotency key** tells the receiving system "this is request
number X", so a message that arrives twice — a timeout that triggers a
retry — is still done once. `InvoicingService.SendAsync` never mints
one: it passes `InvoiceRequest.Id` itself, and every connector writes
that same string into the created invoice's own reference field:

```csharp
var result = await _connector
    .CreateDraftInvoiceAsync(snapshot, requestId.ToString(), cancellationToken)
    .ConfigureAwait(false);
```

`FindByReferenceAsync` is what makes this pay off: reconciliation looks
the invoice up by that reference *before* any retry, so a timeout after
Xero has already committed the write can never produce a second invoice.
Nothing new is minted, stored or lost — the object's own id, already
unique, does the whole job.

## Nine states, and one that is never stored

`InvoiceRequestStatus` runs `Draft → Sending → Sent → Accepted | Rejected
| Voided`, plus `Unknown` and `Reauthorise` — and a ninth, `Unavailable`,
declared but never actually written. When `SendAsync` cannot reach the
connector, the request reverts to `Draft` rather than moving to
`Unavailable`: *"not retried automatically"* is the row's own wording,
and a request nobody can see needs a human to notice it. The transition
table still lists `Unavailable` with no edge in or out, so a reader —
and `InvoiceRequestStatusTransitionsTests` (`4d349b1`), walking all 81
`(from, to)` pairs — finds the gap named rather than an undeclared
value.

## Paid is read, never set

The rule with no exception anywhere in this seam: nothing in TempestOS
ever marks an invoice paid. `RecordStatusReadingAsync` is the only
method that writes `InvoiceRequest.PaidDate`, called only from
`ReconcileAsync`, reading whatever the connector reports. `ADR-0151`
states why in one line — *"`Paid` is a fact this platform reads, never
computes"* — and the physical review proves it as a manual step: send
through the fake connector, reconcile, and confirm *"nothing in Tempest
can set Paid"* (`PHYSICAL_REVIEW.md` §7b, C8). Whether an invoice is
paid is a fact only the accounts package genuinely knows; asserting it
here would let TempestOS's own records disagree with the ledger that
actually governs the money — the same drift `59-evidence.md`'s
refusal-as-result discipline guards against everywhere else.

## Where the token lives, and why not the database

`ISecretStore` keeps a connector's tokens out of `tempest.db` entirely,
under `<persistence root>/secrets/`, one file per key, named by a
SHA-256 hash of the key itself. On Windows, `WindowsDpapiSecretStore`
encrypts each file with the Windows Data Protection API, scoped to the
signed-in user; `FileSecretStore` is a disclosed, unencrypted fallback
everywhere else, logging a `Warning` naming exactly what it is. A token
is like a spare key to the practice's own Xero account: it lives in a
locked drawer beside the filing cabinet, never inside it — because the
cabinet (`tempest.db`) is precisely the file this platform backs up,
exports and hands to support without a second thought.

## Browser signs in, the app receives a code on a local port

Connecting Xero or QuickBooks Online uses **OAuth 2.0 with PKCE** — a
standard way of signing in to one's own account without ever typing
that password into TempestOS. `OAuthAuthoriser` opens the operator's own
browser to the provider's real login page; a loopback listener
(`http://127.0.0.1:<port>/callback/`, on this machine only) waits for
the one-time code the browser is redirected back with once approval is
given. PKCE is the extra proof: a random secret generated before the
browser opens, checked again at the token exchange, so a code
intercepted in transit is useless without it. `WP 19.1A-R1` (`926be24`)
fixed the loopback port to `49301` because Xero's and Intuit's own
consoles require one exact redirect URI registered ahead of time — so
**the URI to register in a sandbox app today is
`http://127.0.0.1:49301/callback/`, exactly**.

## The platform's own `IHostedService`, finally consumed

Background work has had a contract since early in the project —
`IHostedService`, deliberately named apart from the identically-named
Microsoft interface TempestOS depends on nowhere (`ADR-0005`, `ADR-0021`,
`ADR-0024`) — and it sat declared, nothing implementing it, release
after release. `InvoiceReconciliationService` is its first real
consumer: every `Invoicing:PollMinutes` minutes (fifteen by default) it
re-reads every live request in `Unknown`, `Sent` or `Accepted` through
`ReconcileAsync`. A **poller** is a background clock: it wakes on its
own, checks in, goes back to sleep — so a paid invoice is noticed
without a person clicking Reconcile. Its timer comes from an injected
`TimeProvider`, not a bare `System.Threading.Timer`, so a test supplies
a fake clock it advances instantly; every failure is caught, logged and
skipped, since this poller is not critical to correctness, only to how
quickly a person finds out.

## Two connectors, and the differences their own APIs forced

`XeroConnector` and `QuickBooksOnlineConnector` (`WP 19.1A` part 2,
`3898793`) both implement `IInvoicingConnector` over `HttpClient`,
mapping every HTTP outcome through one shared table — 2xx → `Ok`,
400/422 → `Rejected`, 401/403 → `Reauthorise`, 429/5xx or a transport
failure → `Unavailable`.
Xero accepts a contact as a bare name and matches or creates it;
QuickBooks Online needs a real `CustomerRef` resolved first and carries
no single status word, so `DeriveExternalStatus` synthesises one from
`Balance`/`TotalAmt`/`EmailStatus` into the vocabulary
`InvoicingService.InterpretStatus` already matches. Both are proved
against **recorded responses**, not a live sandbox: a
`StubHttpMessageHandler` plays back the shapes each API actually
returns, deterministic and fast, refusing any request nobody scripted —
which is what lets the suite run in CI at all, *"158/158 Invoicing
tests green (Debug), parts 1 and 2 combined"* (`9f1bceb`). It cannot
prove the real API still behaves as documented, though: `ADR-0151` §11
names the QuickBooks Online item-reference gap as *"the physical
review's own first finding once a real sandbox is registered."*

Two further §11 shortcuts closed at `WP 19.1A-R1`, before the review
ran: QuickBooks Online's `DocNumber` field allows 21 characters against
`InvoiceRequest.Id`'s 36, so `DeriveDocNumber` derives a stable "TOS-"
plus seventeen hex digits — deterministic, but disclosed as explicitly
*not* a uniqueness claim across the full id space; and both connectors
had matched a contact by `ClientOrganisationId` — a catalogue id, never
a name any real contact list could hold — so `InvoicingService.ToSnapshotAsync`
now resolves the client's actual display name once, the only place in
this seam that reads the organisation catalogue.

## The bug the Desktop journey found one run in two

`InvoicedBy` is written onto a source only once a request reaches
`Sent` (`ADR-0151` §5), so before that, a deliverable's own completion
hook could raise a Draft, and Raise invoice on the same completion could
raise a *second* Draft with identical lines — nothing yet marked them
billed. `WP 19.9.0` (`333fad6`) found this intermittently: whether the
hook's own request already existed depended on timing, so the Desktop
acceptance journey caught it one run in two, not because the test was
flaky but because the defect itself only sometimes had a window to land
in. The fix reads every live (not `Rejected`/`Voided`) request first and
refuses a second one for the same source, naming the one that already
carries it — a source is billed on at most one live request, proved
directly now rather than by luck, and recorded in `ADR-0151`'s own
addendum and `PHYSICAL_REVIEW.md` §7b (`d75aaba`).

## Raise it from the work, review it from the ledger

`WP 19.1A` part 3 gave all of this a Desktop face: an Invoicing rail
area listing every request by status with Review/Send/Reconcile/Void, a
Raise invoice action on a completed deliverable's own row, and connector
authorisation — Connect, the poll interval — added to Settings
(`1eb57f9`, `fbc65dc`, `5a4a4bd`). `WP 19.10D` then replaced that
by-status grouping — nine `InvoiceRequestStatus` values nobody but an
engineer would recognise — with the five groups the Product Owner had
actually sketched: **New**, **Available to invoice**, **Sent**,
**Outstanding / Overdue**, **Closed**; `InvoicesGroupingTests`
(`cfb13eb`) proves every status and completion lands in exactly one.
`WP 20.1B` then replaced the thirty-day-since-sent guess that last group
used with a real due date: a client's `PaymentTerms` (Up front / 30 / 60
days) is frozen onto the request at Send, and `DueOn` computed once from
that — see `65-the-project-commercial-core.md` for where `PaymentTerms`
itself lives on the client organisation.

## Xero only, for this release

The Product Owner decided on 2026-09-15: *"only Xero for this release;
QuickBooks can be added later"* (Product Owner Decisions 2026-09-15 §4).
QuickBooks Online's item-reference gap is deferred as P4 for `v1.0.0`;
the connector stays in the code and selectable in Settings with its
one-setting workaround, but no `v1.0.0` Work Package carries it further,
and the platform's own security posture statement names Xero alone as
the supported live connector — following a comment the Product Owner
had already given naming Xero as the accounting package actually used.

## What was deliberately not built

No ledger, tax computation, payment processing, or invoice numbering
scheme of TempestOS's own — the accounting package assigns that. No
automatic retry of a failed send: `Unavailable` reverts to `Draft` and
stops, because retrying blind is what an idempotency key exists to make
unnecessary. And no single transaction spanning a request's status and
its lines' `InvoicedBy` link: `ExecuteWriteAsync` commits one object's
state per call (`ADR-0145`), and widening it to span objects was a
substrate change outside this Work Package's own files — a real,
disclosed cost, not an oversight.

## Warnings carried forward

- **Sending is sequential, not one transaction** (`ADR-0151` §5): a
  crash between marking `Sent` and linking lines leaves them reading
  unbilled, recoverable only by re-running the link.
- **QuickBooks Online needs an item reference** this platform has no
  catalogue for; an unconfigured real sandbox will likely answer
  400/422 — `Rejected`, never a crash.
- **The OAuth redirect URI is now fixed**: a sandbox app must register
  `http://127.0.0.1:49301/callback/` exactly, or a busy port names
  itself and the configuration key rather than failing silently.
- **Accounts reads are written to the documented API shape, not proven
  live** — the first real authorisation against a live account is the
  actual test.
- **`TD-183`**: `OAuthAuthoriserTests` binds a real loopback port and
  collided once per night under ten concurrent CI runs — test hardening,
  unowned, not a product defect.

## What to take away

- **A connector is a seam, not a feature**: isolating every outside
  dependency behind one result-only interface means the rest of the
  platform never writes a `try`/`catch` around somebody else's outage.
- **An idempotency key is cheapest when it already exists**: reusing the
  request's own id needs nothing minted, stored or lost.
- **A fact only another system can attest is read, never asserted**:
  Paid belongs to the accounts package by definition, and the discipline
  keeping `InvoiceRequest.PaidDate` write-only-from-the-connector is the
  same one that keeps every other governed record on this platform
  honest about what it actually knows.
