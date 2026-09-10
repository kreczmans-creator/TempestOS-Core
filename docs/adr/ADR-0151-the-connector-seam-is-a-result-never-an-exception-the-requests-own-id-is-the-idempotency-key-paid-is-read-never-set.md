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
