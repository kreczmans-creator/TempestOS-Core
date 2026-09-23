# ADR-0151: The Connector Seam Is a Result, Never an Exception; the Request's Own Id Is the Idempotency Key; Paid Is Read, Never Set

## Status

Accepted — `WP 19.1A` part 1 (Outbound invoicing connector, model and
substrate only — no HTTP, no UI), 2026-09-10.

## Context

`v0.19.0` closes the consultancy seam a third time: `WP 19.0A` (`ADR-0150`)
gave a project a client, a rate card and frozen time and delivery figures;
`WP 19.1A` turns those figures into a real invoice, in Xero or QuickBooks
Online, without TempestOS itself ever deciding what got paid. Every
existing "governed act decides, the Kind only persists" shape (`Evidence`,
`ADR-0148`; `TimesheetEntry`/`DeliverableCompletion`, `ADR-0150`) assumes
the decision comes from data this platform already holds; a connector
call is different — its answer comes from a third party over a network,
and fails in ways a rate-card lookup never does: refused, unauthorised,
unreachable, or answered but the answer lost in transit.

Not ERP, not PLM (`D-028`): this Work Package raises one invoice request
from figures already frozen elsewhere — no ledger, no tax computation, no
payment processing. `Paid` is a fact this platform reads, never computes.

## Decision

**1. `IInvoicingConnector`** (`CreateDraftInvoiceAsync`, `ReadStatusAsync`,
`FindByReferenceAsync`, `ListContactsAsync`, `AuthorisationStateAsync`)
answers every call with a `ConnectorResult<T>`
(`Ok`/`Rejected`/`Reauthorise`/`Unavailable`/`Unknown`), never a thrown
exception — `IEvidenceService`'s own refusal-as-result discipline, widened
to the five states a network call genuinely has; a thrown exception out
of an implementation is a defect in it, exactly as one out of a
`CommandBinding.Build` lambda is a defect in the binding.
`FakeInvoicingConnector` is the only implementation this part ships —
records every call, scriptable per call or pinned to one idempotency key
— bound as the default (`Invoicing:Connector`, unset or `"Fake"`); parts
2/3 add real `Xero`/`QuickBooksOnline` connectors over `HttpClient` and
OAuth 2.0, replacing the binding this key selects, never this interface.

**2. The request's own id is the idempotency key.** `InvoicingService.SendAsync`
passes `InvoiceRequest.Id` itself to `CreateDraftInvoiceAsync` — never a
value minted per attempt — and every implementation writes that same
string into the created invoice's own reference field.
`FindByReferenceAsync` resolves a lost response: reconciliation looks the
invoice up by that reference *before* any retry, so a timeout after the
accounting system already committed the write can never produce a second
invoice for one request.

**3. `InvoiceRequest : EngineeringObjectBase, IRehydratable<InvoiceRequest>`**
follows `Evidence`'s own shape — its own explorer area (project → status
group → request), facet provider and plain-data view, registered directly
rather than folded into `CanonicalObjectKinds`. State: client, PO
reference, `Currency` — resolved from the project's own pinned rate card,
**not** `Organisation.TradingCurrency` (may be unset, or disagree with
what every line is already priced in, which `Money.Sum` would then
refuse to total) — `Lines` (a `TimesheetEntry` or `DeliverableCompletion`
each, at the rate `ADR-0150` already froze, never recomputed here),
`Total`, `Status`, and every external field a connector can report
(id, invoice number, status, issued/paid dates, last error, connector
name, sent timestamp).

**4. `InvoiceRequestStatus`** is nine values:
`Draft → Sending → Sent (external id known) → Accepted | Rejected | Voided`,
plus `Unknown`, `Reauthorise`, and `Unavailable` — declared because the
row's own words name it as part of this Kind's vocabulary, but **never
actually stored**: `SendAsync`'s own connector-unreachable outcome reverts
the request straight to `Draft` (the row: "stays Draft and is not
retried automatically"). The transition table carries `Unavailable` with
no edge in or out, so a reader finds the gap named, not an undeclared
value.

**5. `InvoicingService`** decides permission and lifecycle before
`InvoiceRequest`'s own mutators run, reporting a refusal result rather
than an exception. `RaiseFromCompletionAsync` refuses a project with no
client, no Released rate-card pin, a completion already invoiced (naming
the first request), or nothing left to bill. `SendAsync` always succeeds
as an *act* once `Draft`; what the connector said is read off the
returned request's own `Status` afterward, never a second refusal axis.
**Every line's source gains its `InvoicedBy` link only after the request
reaches `Sent`, each in its own transaction, after the request's own
status write — not one atomic transaction**: `EngineeringDomainContext.ExecuteWriteAsync`
is `internal` and commits one object's own state and audit row per call
(`ADR-0145`); widening it to a multi-object primitive is a substrate
change outside this Work Package's own files — request first, lines
second, disclosed here. `ReconcileAsync` resolves `Unknown` by
reference (found → `Sent` with links; not found → `Draft`) and refreshes
`Sent`/`Accepted` from the connector's own status word, matched
case-insensitively by substring (`AUTHORISED`/`PAID` → `Accepted`;
`VOIDED` → `Voided`; unrecognised changes nothing). `VoidAsync` covers
`Draft`/`Rejected` only — a request that reached the provider is voided
there, and read back through `ReconcileAsync`. **`PaidDate` is read from
the connector alone**: no mutator, service method or command ever sets it
otherwise — the Product Owner guard the row states in as many words.

**6. `ISecretStore`** keeps a connector's own token out of `tempest.db`,
under `<persistence root>/secrets/`, one file per key, named by a SHA-256
hash of the key rather than the key itself. `WindowsDpapiSecretStore`
(`[SupportedOSPlatform("windows")]`, selected only inside an
`OperatingSystem.IsWindows()` guard) encrypts each file with
`ProtectedData`, scoped to the signed-in Windows user; `FileSecretStore`,
the non-Windows fallback, is plain, unencrypted files with a logged
`Warning` on construction naming exactly what it is — disclosed, not
permanent. `InvoiceReconciliationService : IHostedService` polls every
`Invoicing:PollMinutes` minutes (default 15), discovered like every other
hosted service, reconciling every live request in `Unknown`, `Sent` or
`Accepted`; a failure listing or reconciling is caught, logged, and the
loop moves on — non-critical. Its timer is built from an injected
`TimeProvider` rather than a bare `System.Threading.Timer`, so a test
supplies one whose own `CreateTimer` hands back a controllable timer it
ticks directly.

**7. `DeliverableService` gains an optional, best-effort completion
hook** (`SetCompletionHook`) so completing a deliverable can raise an
invoice request through the service, never the UI, with no compile-time
dependency the wrong way round (`InvoicingService` already depends on
`IDeliverableService`, so a direct reference back would be circular): a
plain `Func<Guid, CancellationToken, Task>`, wired once by
`EngineeringWorkspaceComposer.RegisterEngineeringDisciplines` after both
services exist. Any exception, or refusal, is swallowed — completion
succeeded the moment its write committed, regardless of the invoice.

## Update — `WP 19.1A` part 2: the real connectors

Accepted — `WP 19.1A` part 2 (OAuth 2.0, `XeroConnector`,
`QuickBooksOnlineConnector`, contract tests, connector selection),
2026-09-10.

**8. `OAuthAuthoriser`** (`Tempest.Core.Invoicing.OAuth`) drives the
authorisation-code flow with PKCE (`S256`) for both providers alike: an
`HttpListener` loopback on a fresh `http://127.0.0.1:<free port>/callback/`
every run — confirmed to bind without elevation or a URL ACL reservation
before this class was written the way it is — opened in the system browser
through an `IBrowserLauncher` seam (`SystemBrowserLauncher` in production;
a fake that calls the loopback itself in tests, never a real browser).
Client id and secret are resolved from `Invoicing:<Provider>:ClientId`/
`ClientSecret` in configuration first, the identical key in `ISecretStore`
second — where a Settings screen (`WP 19.2B`) writes what the operator
types — and never the database; a provider with neither configured answers
every call `Reauthorise("not configured")`, checked *before* "never
authorised" so the diagnosis names the actual gap. `EnsureAccessTokenAsync`
is what every connector call goes through first: a still-valid stored
token is returned untouched (no network call at all); an expired one is
refreshed silently; a refused refresh is `Reauthorise`, never a crash. The
provider's own tenant/company id — Xero's `tenantId`, QuickBooks Online's
`realmId` — is stored generically under one `Invoicing:<Provider>:TenantId`
key: QuickBooks Online's own redirect already carries it as a `realmId`
query parameter; Xero's does not, so `OAuthAuthoriser` reads it, once, from
`GET https://api.xero.com/connections` right after a fresh token exchange.

**9. `XeroConnector`** (`/api.xro/2.0/Invoices`, `/Contacts`) sends
`Authorization: Bearer`, `xero-tenant-id`, and `Idempotency-Key: <request
id>`; the request id also becomes the invoice's own `Reference`, read back
by `FindByReferenceAsync` via `where=Reference=="…"`. A contact is sent as
`{"Name": ClientOrganisationId}` only — Xero's own documented behaviour
matches an existing contact by that name or creates one when absent, so no
separate contact round trip is needed. Every HTTP outcome maps through one
shared table (`ConnectorHttpOutcome`, `Tempest.Core.Invoicing.OAuth`): 2xx
→ `Ok`; 400/422 → `Rejected` (Xero's own validation message, extracted from
the body); 401/403 → `Reauthorise`; 429/5xx → `Unavailable`; a
pre-response transport failure (DNS, refused connection, a client-side
timeout before a response starts arriving) → `Unavailable`; a 2xx response
whose own body cannot be parsed → `Unknown` (a response demonstrably
arrived; only its shape is not understood). Xero's own legacy
`/Date(<ms>+<tz>)/` wire format is parsed alongside plain ISO-8601.

**10. `QuickBooksOnlineConnector`** (`/v3/company/<realmId>/invoice`,
`/customer`, `/query`) differs from Xero in two ways its own API forces.
First, **a `CustomerRef` must already be resolved** before an invoice can
be created at all — a bare name in the invoice body is not enough, unlike
Xero — so this connector queries `Customer` by `DisplayName` and creates
one when absent, genuinely, not deferred. Second, **QuickBooks Online
carries no single status word**: `DeriveExternalStatus` synthesises one
from `Balance`/`TotalAmt`/`EmailStatus` (`PAID` when the balance reaches
zero against a positive total, `VOIDED` when both are zero, `SENT` from
`EmailStatus`, otherwise `SUBMITTED`) into the same vocabulary
`InvoicingService.InterpretStatus` already matches by substring, and a
paid invoice's own `PaidDate` is read, best-effort, from its linked
`Payment`'s own `TxnDate` via one further query. The request id is carried
three ways: the `requestid` query parameter on the create call (QuickBooks
Online's own idempotency mechanism), and both `DocNumber` and
`PrivateNote` on the invoice itself — `DocNumber` is also what
`FindByReferenceAsync` queries on. The same outcome table as Xero's own
applies throughout.

**11. Disclosed gaps, not hidden.** QuickBooks Online's real API requires
every invoice line to carry a valid `ItemRef` naming a product/service item
already defined in the company — a catalogue this platform's own object
model has no concept of. `Invoicing:QuickBooksOnline:DefaultItemId`, when
configured, is attached to every line; left unconfigured, a real sandbox
will likely answer 400/422 (mapped to `Rejected`, never a crash) — the
physical review's own first finding once a real sandbox is registered.
Separately, `InvoiceRequest.Id.ToString()` (36 characters) is longer than
QuickBooks Online's own documented 21-character `DocNumber` limit; a real
sandbox call may reject it, in which case a shorter, still-unique
QuickBooks-Online-specific reference is a follow-up this part does not
build. Both connectors treat `InvoiceRequestSnapshot.ClientOrganisationId`
— an organisation-catalogue id, not necessarily a human display name — as
the contact's own matched/created name; a later part threading the
organisation's own display name onto the snapshot would improve what is
actually matched.

**12. Selection.** `Invoicing:Connector` = `Fake` (default) | `Xero` |
`QuickBooksOnline` picks the connector at `TempestHost` composition; each
real connector's own `HttpClient` is wrapped in `InvoicingHttpLoggingHandler`
so its own request/response diagnostics flow into the exact platform log
sinks every other component already writes into
(`Tempest.Core.Logging.TempestLoggerProvider`'s own remarks name this
precisely). Construction never fails over a missing client id — resolution
is lazy, per call, inside `OAuthAuthoriser` — so an unconfigured real
provider is fully usable as a composition target from the very first run,
answering `Reauthorise("not configured")` until an operator registers a
sandbox app and connects it.

## Consequences

**Positive:** a caller of `SendAsync`/`ReconcileAsync` never writes a
`try`/`catch` around an ordinary network answer; a retried send after a
lost response can never double-invoice; parts 2/3 replace one binding
each, touching nothing else this ADR decided.

**Negative:** the request and its line links are not atomic — a crash
between the two leaves a `Sent` request whose lines still read unbilled,
recoverable only by re-running the link; `InvoiceRequestStatus.Unavailable`
is dead vocabulary, kept only because the row names it.

## Related Documents

`D-028`; `ADR-0148` (Evidence, the shape repeated again); `ADR-0150`
(frozen rates and `InvoicedBy`, read here, never re-resolved); `ADR-0145`
(one object, one transaction); `WorkPackages.md` (`WP 19.1A` row).

## Addendum (`WP 19.1A-R1`) — three of §11's own disclosed gaps closed before the physical review

**Fixed OAuth loopback port.** §9's own loopback listener used to bind a
fresh ephemeral port every run, which Xero's and Intuit's own app
consoles cannot accept — both require one exact redirect URI, registered
ahead of time. `OAuthLoopbackListener` now binds the port
`OAuthAuthoriser.ResolveLoopbackPort()` resolves from
`Invoicing:OAuth:LoopbackPort` — `49301` by default, `0` kept as the
original ephemeral behaviour (a test's own choice only). **The exact
redirect URI to register in a Xero or QuickBooks Online sandbox app is
`http://127.0.0.1:49301/callback/`** — unchanged unless an operator
configures a different port. When the configured port is already bound by
something else, `AuthoriseAsync` returns `OAuthResult.Failed`, naming both
the port attempted and the `Invoicing:OAuth:LoopbackPort` key — never a
crash, exactly as this ADR's own "a result, never an exception" runs
throughout.

**The QuickBooks Online `DocNumber` length limit, actually closed.** §11
disclosed that `InvoiceRequest.Id.ToString()` (36 characters) exceeds the
field's own 21-character limit and left it unbuilt. `QuickBooksOnlineConnector.DeriveDocNumber`
now derives a stable, exactly-21-character value —
`"TOS-"` plus the first 17 hex digits found in the request id (its own
dashes skipped), upper-cased, zero-padded if fewer than 17 are present —
and writes it as `DocNumber`; `PrivateNote` still carries the full
36-character id verbatim, unchanged. `FindByReferenceAsync` derives the
identical value from the reference it is given before querying, since
what it is handed is always the full id, never the already-shortened
form. The derivation is deterministic (the same id always derives the
same number, which is what reference lookup depends on) but explicitly
**not** a claim of uniqueness across the whole id space — 17 of a GUID's
own 32 hex digits is 68 bits, not the full 128 — disclosed in the
method's own remarks rather than assumed away, matching this ADR's own
established practice of naming a gap rather than hiding it.

**Contact matching by organisation name, not a bare catalogue id.** §11
also disclosed that both connectors matched/created a contact using
`InvoiceRequestSnapshot.ClientOrganisationId` directly — an organisation-
catalogue id, never a name any accounting system's own contact list could
plausibly already hold. `InvoiceRequestSnapshot` gains `ClientName`,
filled by `InvoicingService.ToSnapshotAsync` immediately before a
connector is ever called: the client organisation's own name, read from
`Tempest.Core.BusinessOperations.Crm.IOrganisationCatalog` by
`ClientOrganisationId` — the one and only place in this seam that reads
the catalogue at all, keeping every connector's own "plain data, no
catalogue dependency" shape (§2's own remarks) intact. Both `XeroConnector`
and `QuickBooksOnlineConnector` now match/create by `ClientName`; when it
is `null` or blank — the id did not resolve to any registered
organisation — `CreateDraftInvoiceAsync` returns
`ConnectorResult.Rejected("client organisation '<id>' is not in the
catalogue")` outright, never querying or creating a contact named after a
raw, meaningless id.

## Addendum (`WP 19.9.0`) — a source is billed on at most one live request

§5 writes `InvoicedBy` only once a request reaches `Sent`; §7 raises a
request the moment a deliverable is completed. Together they left a gap
the v0.19.0 Desktop journey found one run in two: the completion hook
raised a Draft, and Raise invoice on the same completion from the
Deliverables tab raised a second Draft carrying the same lines, because
nothing was yet marked invoiced. `RaiseFromCompletionAsync` now reads
every live request first (anything not `Rejected` or `Voided`, which free
their lines): a completion a live request already carries is refused
`AlreadyInvoiced`, naming that request and its status; a timesheet entry a
live request already carries is left off the new one; and when nothing is
left, the `NothingToBill` refusal names the request that already holds it.
The Deliverables tab's Raise invoice therefore stays what §7 implied it
was — the retry for a completion whose hook refused (no client yet, no
rate-card pin) — and never a way to bill the same work twice. The
`InvoiceRequestResult` a refusal carries is the existing request, so a
caller can open it.
