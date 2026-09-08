# TempestOS v0.6.0 — Platform Service Contracts

## Status

**Contract Review — documentation only. No implementation.** This
document is the final pre-implementation review of every proposed
`v0.6.0` public interface and service contract, following the Platform
Services Architecture Package's own approval. It exists so each
contract can be checked for stability — internal consistency, no
overlap with an existing or sibling contract, a stated lifetime, a
stated failure model — *before* any Work Package writes code against
it, mirroring `docs/releases/v0.4.0/Platform Services Architecture
Review.md`'s own role for that release's own new surfaces.

This document does not restate the full interface source from `Public
Interface Catalogue.md` — it references those signatures and adds the
fifteen review dimensions the architecture package did not yet cover in
full: lifetime, thread safety, failure behaviour, logging, configuration,
event publication, persistence, security, versioning, performance,
testing, and future extension points, for each of the nine proposed
services.

## How to Read This Document

Each service section below answers the same fifteen questions, in the
same order, so a reviewer (or `WP 6.8`) can check any two services'
answers against each other directly. Where a question does not apply to
a given service (e.g., Event Publication Rules for Licensing, which
publishes no events), the section says so explicitly rather than being
omitted — an omitted section could be mistaken for an oversight; an
explicit "Not applicable" cannot.

---

## Persistence *(`Tempest.Core.Persistence`, established as part of `WP 6.4`)*

**Purpose.** A minimal, internal, platform-owned durable store, existing
solely so no other platform service invents an incompatible storage
mechanism of its own.

**Responsibilities.** Store, retrieve, delete, and enumerate string
values, scoped by a caller-supplied `collection` name and `key`. Nothing
beyond that — no querying, no schema, no transactions across multiple
keys.

**Public Interfaces.** `IPersistenceStore` (see `Public Interface
Catalogue.md`).

**Service Lifetime.** DI-public, container-constructed singleton,
registered Phase 6 (see `Service Lifecycle.md`).

**Thread Safety Expectations.** Every method must be safe for concurrent
invocation from multiple callers without external synchronization —
Settings and Audit may both be read/written from concurrent request
paths (particularly once the REST API exists). The underlying storage
implementation must serialize concurrent writes to the same
`collection`/`key` pair internally; it must never require a caller to
hold a lock.

**Failure Behaviour.** A read/write/delete/list failure (disk I/O error,
serialization fault) throws `PersistenceStoreUnavailableException` —
never returns a silently-empty or silently-stale result. This mirrors
`ADR-0013`'s existing platform-service-failure philosophy: fail loudly,
let the caller (or the Host, if the caller is itself a Platform Service
constructed at startup) decide what "unavailable persistence" means for
it, rather than masking the failure.

**Logging Requirements.** Optional `ILogger?` constructor parameter,
matching every existing platform service's convention (`ADR-0010`). Logs
a warning-level entry on any operation failure before the exception
propagates; does not log the *content* of any stored value (a stored
value could belong to Settings or Audit and should not be duplicated
into logs it has no announced relationship to).

**Configuration Requirements.** The storage backend's own location
(file path, connection string, or equivalent) is read from
`IConfigurationProvider` at construction — read-only, once, per
Configuration's own existing immutability contract (`ADR-0009`, Case
Study 05). No runtime-mutable setting governs Persistence itself (that
would be circular, since Settings depends on Persistence).

**Event Publication Rules.** Publishes no events. Persistence is a
low-level storage primitive; any consumer-visible "this changed" signal
is the *consuming* service's own responsibility (e.g., Settings' own
`ISettingsChangedEvent`), not something Persistence itself announces.

**Persistence Requirements.** This section is self-describing — this
*is* the Persistence service. Its own state must survive an ordinary
process restart; it is explicitly not required to survive a full
machine wipe, a corrupted disk, or any scenario beyond ordinary
process-lifetime durability.

**Security Considerations.** Persistence has no concept of *who* is
reading or writing a given collection/key — Identity & Permissions
authorization must happen in the *calling* service (Settings, Audit),
never inside Persistence itself. Persistence must not allow one
`collection` to be enumerated or read using a `key` intended for
another — collection scoping is a hard partition, not an advisory one.

**Versioning Policy.** `IPersistenceStore`'s own contract is expected to
be stable for the life of `v0.6.x` — additive-only changes (a new
method) are acceptable; no existing method's signature or behaviour may
change without a major-version-level ADR, since both current consumers
(Settings, Audit) would need simultaneous review.

**Performance Expectations.** No specific throughput target is set in
this review (see `Risk Register.md`'s "Performance risk" entry, not
separately registered). A single read/write should complete in
low-single-digit milliseconds under ordinary local-disk conditions;
`WP 6.4`'s own architecture phase should set a concrete target once a
storage backend is chosen.

**Testing Requirements.** Read/write/delete/list round-trip correctness;
concurrent-write isolation (two callers writing different keys in the
same collection do not corrupt each other); failure-path coverage
(simulated I/O failure surfaces `PersistenceStoreUnavailableException`,
never a swallowed error); collection-scoping isolation (a key in one
collection is never visible via another collection's `ListKeysAsync`).

**Future Extension Points.** A query/filter capability beyond key lookup
(anticipated need from Audit — see `Technical Debt Assessment.md`);
a typed (not string-only) value contract, if a future consumer needs
structured data rather than caller-serialized strings.

---

## Reporting Framework *(`Tempest.Core.Reporting`, `WP 6.0`)*

**Purpose.** Produces structured, formatted output from platform or
module data via a registered definition/renderer pair.

**Responsibilities.** Register report definitions and their renderers;
dispatch a render request by definition Id; enumerate registered
definitions. Does not persist generated output, does not schedule
recurring report generation, and does not itself provide a delivery
mechanism (a generated report reaching a user is Notifications' or the
REST API's own concern, not Reporting's).

**Public Interfaces.** `IReportDefinition`, `IReportRenderer<TDefinition>`,
`IReportingService` (see `Public Interface Catalogue.md`).

**Service Lifetime.** DI-public, container-constructed singleton,
Phase 6.

**Thread Safety Expectations.** `RegisterDefinition` is expected to be
called only during module initialisation (single-threaded, by
construction — Module Initialisation is sequential per `Host Lifecycle.
md`), so it need not itself be thread-safe against concurrent
registration. `GenerateAsync` must be safe for concurrent invocation
once registration is complete — multiple callers may request different
(or the same) report concurrently.

**Failure Behaviour.** `GenerateAsync` for an unregistered Id throws
`ReportDefinitionNotFoundException`. A renderer's own failure (an
exception thrown from `RenderAsync`) propagates to the caller
unmodified — Reporting does not swallow or wrap a renderer's own
exception, so the caller sees the real cause. This mirrors the Command
Framework's own dispatch failure model (`ADR-0038`) rather than
inventing a new one.

**Logging Requirements.** Optional `ILogger?`. Logs at
information level when a definition is registered (mirroring
`ICommandRegistry`'s own registration logging) and at warning level
when generation fails.

**Configuration Requirements.** None beyond what a specific renderer
implementation may itself require (e.g., a PDF renderer's own font/
template path) — that configuration belongs to the renderer, not to
`IReportingService` itself.

**Event Publication Rules.** Publishes no events of its own. A module
wanting to react to "a report was generated" should structure its own
renderer to do so, or (once Notifications exists) raise a notification
explicitly — Reporting itself stays uninvolved in that decision,
mirroring the deliberate orthogonality `ADR-0040` establishes against
Export/Import.

**Persistence Requirements.** None. Reporting is stateless beyond its
in-memory registration table; a generated report is handed back to the
caller and is not retained by the service itself.

**Security Considerations.** `GenerateAsync` does not itself check
permissions — a caller invoking it through the Command Framework
(`GenerateReportCommand`) inherits whatever authorization that command
handler enforces via `IPermissionEvaluator`; a direct DI consumer is
trusted at the same level as any other constructor-injected dependency.
This mirrors how Navigation and the Command Framework themselves impose
no authorization internally (`ADR-0032`, `ADR-0037`) — the enforcement
point is the caller, not the service.

**Versioning Policy.** `IReportDefinition`/`IReportRenderer<TDefinition>`
are expected to remain stable; a breaking change to either affects every
registered report definition simultaneously and should be treated with
the same weight as a Command Framework contract change (`ADR-0037`'s own
precedent).

**Performance Expectations.** Rendering time is renderer-specific and
explicitly out of this service's own control; `IReportingService` itself
adds negligible overhead (a dictionary lookup plus a delegated call).

**Testing Requirements.** Registration (success, duplicate-Id rejection);
dispatch (success, not-found, renderer-throws propagation); concurrent
`GenerateAsync` calls against distinct and identical definition Ids.

**Future Extension Points.** Report generation progress/streaming for a
long-running renderer; scheduled/recurring report generation (explicitly
out of scope for `WP 6.0` itself — see `WorkPackages.md`).

---

## Identity & Permissions *(`Tempest.Core.Identity`, `WP 6.1`)*

**Purpose.** Answers who is performing an action, and whether they are
allowed to.

**Responsibilities.** Resolve the current acting principal; evaluate
whether a principal holds a given permission; enforce that requirement
(throwing if not met). Does not itself perform authentication (verifying
a password, token, or credential) in this first iteration — see
`ADR-0043`'s local-only scope decision; `ICurrentPrincipalAccessor` is
expected to be populated by whatever mechanism establishes a local
session, which `WP 6.1`'s own architecture phase must still define.

**Public Interfaces.** `IIdentity`, `IPrincipal`,
`ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `Permission` (see
`Public Interface Catalogue.md`).

**Service Lifetime.** DI-public, container-constructed singleton,
Phase 6.

**Thread Safety Expectations.** `ICurrentPrincipalAccessor.Current` must
be safe to read concurrently and must reflect the calling context's own
current principal correctly under concurrent requests (a genuine
concern once the REST API exists, handling multiple simultaneous
requests each with a potentially different principal) — this likely
requires an `AsyncLocal<T>`-backed implementation, not a single mutable
field, a specific design point `WP 6.1`'s own architecture phase must
resolve explicitly, named here so it is not discovered late during
`WP 6.3`'s own implementation.

**Failure Behaviour.** `RequirePermission` throws
`PermissionDeniedException` when the check fails — never returns a
boolean the caller might forget to check. `HasPermission` (the
non-throwing form) is provided for call sites that need to branch rather
than fail (e.g., hiding a menu item a principal cannot use), mirroring
the throwing/non-throwing pairing already established by
`ICommandRegistry`.

**Logging Requirements.** Optional `ILogger?`. A `PermissionDeniedException`
should be logged at warning level with the principal Id, the permission
key, and the caller context — this is the platform's first
security-relevant log content and should be treated with corresponding
care (never log a credential or secret, only identity/permission
metadata).

**Configuration Requirements.** The initial permission grants for a
given local principal (until a full administration surface exists) are
expected to be configuration- or file-sourced — `WP 6.1`'s own
architecture phase must decide the exact source; this review does not
prescribe one, only that it must exist before `WP 6.3` can authorize
anything.

**Event Publication Rules.** Publishes no events in this first
iteration. A future "permission changed" notification is a plausible
extension point (see below), not part of this release's own scope.

**Persistence Requirements.** None required by the contracts themselves
in this release's minimal, local-only scope — if permission grants are
file-sourced (see Configuration Requirements, above), no `Persistence`
dependency is introduced; if a future iteration makes permissions
runtime-editable, it would then depend on `Settings`/`Persistence` like
any other mutable platform state.

**Security Considerations.** This entire service *is* a security
consideration — see `Required ADRs.md`'s `ADR-0043`/`ADR-0044` and
`Risk Register.md`'s `R1`. The single most load-bearing design decision
in the release: `IPermissionEvaluator` must be the *only* enforcement
point every other service calls (`ADR-0044`), or `TD-09`/`TD-10`/`TD-11`
remain open.

**Versioning Policy.** `Permission` (a simple string-keyed record) is
expected to be extended additively (new permission keys defined by
whichever service needs them) without ever changing its own shape.
`IPermissionEvaluator`'s two methods are expected to remain stable
indefinitely, mirroring `ICommandRegistry`'s own long-term contract
stability.

**Performance Expectations.** `HasPermission`/`RequirePermission` are
expected to be called on the hot path of every authorized operation
(especially every REST request) — must be O(1) or O(log n) against the
number of permissions a principal holds, never a linear scan proportional
to the total number of permissions defined platform-wide.

**Testing Requirements.** Grant/deny correctness for every permission
combination; `PermissionDeniedException` thrown correctly and exactly
once per failed check; concurrent-request principal isolation (two
concurrent callers with different principals never see each other's
`Current`); the specific `TD-09`/`TD-10`/`TD-11` scenarios each
explicitly re-tested as regression cases once `WP 6.1` implements the
fix each was named against.

**Future Extension Points.** External identity-provider federation
(explicitly deferred, `ADR-0043`); a "permission changed" notification;
a full administration UI/API for managing grants (a plausible `WP 6.3`
or later REST API consumer, not this release's own scope).

---

## Notification Framework *(`Tempest.Core.Notifications`, `WP 6.2`)*

**Purpose.** Tells a user, module, or external system that something
happened, built on top of the existing Event Bus.

**Responsibilities.** Accept a notification derived from (or raised
alongside) an event; dispatch it to every subscribed handler. Does not
itself define *how* a notification is presented (toast, email, webhook)
— that is each handler's own concern, mirroring how the Event Bus itself
never prescribes what a subscriber does with an event.

**Public Interfaces.** `INotification`, `INotificationHandler<TNotification>`,
`INotificationDispatcher` (see `Public Interface Catalogue.md`).

**Service Lifetime.** DI-public, container-constructed singleton,
Phase 6.

**Thread Safety Expectations.** `Subscribe`/`Unsubscribe`/`PublishAsync`
must be safe under the identical concurrency model `IEventBus` already
proved out (`ADR-0028`) — a per-call snapshot of subscribers, so a
handler subscribing or unsubscribing during dispatch never causes a
race or a missed/duplicate delivery within that one dispatch call.

**Failure Behaviour.** Every subscriber's failure is isolated
unconditionally — one handler throwing does not prevent any other
handler from receiving the same notification, and does not propagate to
the publisher — an exact reuse of `IEventBus`'s own unconditional
per-subscriber isolation (`ADR-0028`), not a new failure model requiring
its own justification.

**Logging Requirements.** Optional `ILogger?`. Logs a warning for each
isolated handler failure, including the notification type and handler
type — mirroring `EventBus`'s own existing failure-logging convention
exactly.

**Configuration Requirements.** None for the dispatcher itself. An
individual notification handler (e.g., an email-sending handler) may
have its own configuration (SMTP settings, and so on) — out of this
service's own scope.

**Event Publication Rules.** This service's *entire purpose* is
publication — see Responsibilities, above. It is built on `IEventBus`,
consuming it as an ordinary dependency, not replacing or wrapping its
public surface for other consumers.

**Persistence Requirements.** None. A notification is not retained after
dispatch — a future "notification history/inbox" feature is a plausible
extension (see below), not this release's own scope.

**Security Considerations.** A handler subscribing to a notification
type receives every instance of that type published platform-wide —
`WP 6.2`'s own architecture phase should confirm no notification type
carries data a subscriber shouldn't see without its own authorization
check (e.g., a "user X's report is ready" notification should not leak
report content to an unauthorized subscriber; the notification payload
should carry only what's safe for any subscriber of that type to see).

**Versioning Policy.** `INotification`/`INotificationHandler<T>` are
expected to remain as stable as `IEvent`/`IEventHandler<T>` have proven
to be — additive (new notification types) rather than breaking changes
to the two core interfaces.

**Performance Expectations.** Matches `IEventBus`'s own existing
performance characteristics (sequential, in-process dispatch) — no
new performance requirement beyond what Event Bus already satisfies.

**Testing Requirements.** Publish/subscribe basic behaviour; multiple
subscribers; subscriber failure isolation (mirroring the Event Bus's
own existing test suite almost directly); a notification correctly
derived from/alongside a real event, proven end-to-end against a real
consumer once one exists.

**Future Extension Points.** A durable notification history/inbox (would
depend on Persistence, if built); delivery-channel abstractions (email,
webhook, push) as first-party handler implementations rather than each
module writing its own.

---

## REST API *(`Tempest.Core.Api`, `WP 6.3`)*

**Purpose.** Lets an external HTTP client invoke platform capability
from outside the running process.

**Responsibilities.** Host an HTTP listener; map registered routes to
Command Framework invocations; authorize each request via Identity &
Permissions before dispatch; return a response reflecting the command's
own `CommandResult`. Does not itself contain business logic — every
route is a thin translation layer to an existing `ICommand`.

**Public Interfaces.** `IApiEndpointRegistry` (see `Public Interface
Catalogue.md`); the `IHostedService` scaffold type itself is not yet
drafted, pending `ADR-0049`'s ratification (see `Public Interface
Catalogue.md`'s own note).

**Service Lifetime.** Hosted Service — discovered and orchestrated by
`IHostedServiceManager`, started Phase 8.1, stopped Phase 10.1.
`IApiEndpointRegistry` itself is DI-public, container-constructed,
Phase 6 (routes may be registered by any module during initialisation,
before the hosted service itself starts listening).

**Thread Safety Expectations.** Must handle concurrent inbound HTTP
requests safely — this is the platform's first service where genuine
request-level concurrency is a first-class, expected condition rather
than an edge case. Every downstream dependency it calls
(`ICommandRegistry.InvokeAsync`, `ICurrentPrincipalAccessor`,
`IPermissionEvaluator`) must itself already be safe for concurrent
invocation — confirmed for each in this document's own corresponding
section.

**Failure Behaviour.** An authorization failure
(`PermissionDeniedException`) maps to an HTTP 403; a
`CommandNotFoundException` maps to an HTTP 404; any other unhandled
exception from command dispatch maps to an HTTP 500, logged at error
level, with no internal exception detail (stack trace, message) leaked
into the HTTP response body — a security-relevant behaviour named
explicitly here rather than left to whatever ASP.NET Core's own default
error page happens to do.

**Logging Requirements.** Every inbound request logged at information
level (method, path, response status, principal Id if authorized) —
this log is also the natural first input to Audit (`WP 6.5`), though the
REST API should call `IAuditRecorder` explicitly for that rather than
relying on log-scraping to reconstruct an audit trail.

**Configuration Requirements.** Listening port/address, TLS certificate
source, and any Kestrel-specific tuning are read from
`IConfigurationProvider` at startup — read-only, matching every existing
platform-service convention.

**Event Publication Rules.** Publishes no platform Event Bus events
itself for ordinary request handling (an inbound HTTP request is not,
in this platform's own terms, a domain event) — the *handled command*
may itself publish an event or notification, exactly as it would if
invoked from any other caller (menu, keyboard shortcut). The REST API
introduces no new publication path.

**Persistence Requirements.** None directly — any request whose command
handler needs persistence goes through that handler's own dependencies,
not through the REST API layer itself.

**Security Considerations.** The highest-security-sensitivity service in
this release, alongside Identity & Permissions itself: the platform's
first network-facing attack surface. Every route must be authorized
before dispatch, with no bypass path; TLS should be the default
expectation for anything beyond local development; request size/rate
limiting is a plausible requirement `WP 6.3`'s own architecture phase
should assess explicitly (not committed to in this review, since it may
be Kestrel's own built-in capability rather than something this
platform must build). See `Risk Register.md` `R2`/`R3`.

**Versioning Policy.** Route paths should carry an explicit version
segment (e.g., `/api/v1/...`) from their first release — retrofitting
versioning onto an already-shipped, unversioned API is a well-known,
avoidable source of debt this review flags proactively, not something
discovered during `WP 6.3`'s own implementation.

**Performance Expectations.** No specific throughput/latency target is
set in this review; `WP 6.3`'s own architecture phase should set one
once ASP.NET Core/Kestrel is prototyped (`Risk Register.md` `R3`),
informed by realistic expected concurrent-client counts for this
platform's own deployment model.

**Testing Requirements.** Route-to-command mapping correctness;
authorization enforcement (authorized succeeds, unauthorized returns
403, unauthenticated returns 401 if that distinction is modeled);
error-mapping correctness (404/403/500, no leaked internal detail);
integration tests driving real HTTP requests against a real, running
Host (mirroring `TempestHostTests`'s own "against the real, unmodified
Host" testing philosophy); a load/concurrency test proving the
thread-safety expectations above actually hold under concurrent
requests.

**Future Extension Points.** Rate limiting; request/response
compression; a machine-readable API description (OpenAPI/Swagger) once
the route surface stabilizes; webhook/callback support (a plausible
Notifications integration).

---

## Settings Framework *(`Tempest.Core.Settings`, `WP 6.4`)*

**Purpose.** User-changeable, runtime-mutable configuration, distinct
from the immutable, load-once Configuration service.

**Responsibilities.** Register setting definitions with defaults; read
and write current values; publish a change notification through the
Event Bus on every write. Establishes the shared Persistence abstraction
as part of its own scope.

**Public Interfaces.** `ISettingDefinition`, `ISettingsProvider`,
`ISettingsChangedEvent` (see `Public Interface Catalogue.md`).

**Service Lifetime.** DI-public, container-constructed singleton,
Phase 6. Depends on Persistence and the Event Bus, both already
registered earlier in the same phase's registration block (see
`Service Lifecycle.md`'s note on registration-order grouping).

**Thread Safety Expectations.** `GetValueAsync`/`SetValueAsync` must be
safe for concurrent invocation, including a read racing a concurrent
write to the same key — the read must return either the pre-write or
post-write value, never a partially-written or corrupted one. This is
delegated to `IPersistenceStore`'s own concurrency guarantee (see
Persistence's Thread Safety Expectations, above), not re-implemented
here.

**Failure Behaviour.** `GetValueAsync`/`SetValueAsync` for an
unregistered key throws `SettingNotFoundException`. A
`PersistenceStoreUnavailableException` from the underlying store
propagates unchanged — Settings does not mask a storage failure as "key
not found" or any other misleading result.

**Logging Requirements.** Optional `ILogger?`. Logs at information level
whenever a setting value actually changes (old and new value, unless
the setting is flagged sensitive — see Security Considerations, below,
in which case only the key and the fact of a change is logged, never
the values).

**Configuration Requirements.** None beyond what Persistence itself
requires (see above) — Settings does not read `IConfigurationProvider`
directly; a setting's *default* value is supplied by whichever module
registers its `ISettingDefinition`, in code, not from Configuration.

**Event Publication Rules.** Publishes exactly one event type,
`ISettingsChangedEvent`, through the existing `IEventBus`, on every
successful `SetValueAsync` call — reusing the Event Bus's own existing
dispatch/failure-isolation model (`ADR-0028`) rather than inventing a
parallel one. Never published for a read, only for a write that
actually changes the stored value (a write of the already-current value
should still publish, for simplicity and predictability, unless a
future ADR decides otherwise — `WP 6.4`'s own architecture phase should
settle this explicitly).

**Persistence Requirements.** Every setting value must survive a process
restart — this is Settings' entire reason for depending on
`IPersistenceStore` rather than holding values only in memory.

**Security Considerations.** A setting value may be sensitive (an API
key, a credential) — `ISettingDefinition` should carry (or a future
extension should add) a flag distinguishing a sensitive setting so
logging (see above) and any future REST-exposed settings-management
endpoint can redact it appropriately. Not fully specified in this
review; named as a required decision for `WP 6.4`'s own architecture
phase.

**Versioning Policy.** `ISettingDefinition`/`ISettingsProvider` are
expected to be stable; a module's own registered setting keys are that
module's own namespace to manage (mirroring how a module owns its own
`ModuleDescriptor.Id` namespace) — Settings itself does not version
individual keys.

**Performance Expectations.** `GetValueAsync` is a likely hot-path call
(potentially on every request in a REST-API-exposed setting); an
in-memory cache over the underlying `IPersistenceStore`, invalidated on
write, is a plausible implementation detail `WP 6.4`'s own architecture
phase should consider, rather than hitting the store on every read
unconditionally.

**Testing Requirements.** Read/write/default-value correctness;
`ISettingsChangedEvent` published correctly on change, with correct
old/new values; not-found handling; concurrent read/write correctness;
persistence-failure propagation.

**Future Extension Points.** Per-principal (as opposed to global)
settings, once Identity & Permissions exists in a mature enough form;
a settings-management REST surface; sensitive-value redaction (see
Security Considerations, above).

---

## Audit Framework *(`Tempest.Core.Audit`, `WP 6.5`)*

**Purpose.** A durable, queryable, append-only record of who did what,
when — distinct from Logging and Diagnostics.

**Responsibilities.** Record an attributable action, with the current
principal resolved automatically; answer filtered queries over
previously recorded actions. Never modifies or deletes an existing
record — append-only, by design.

**Public Interfaces.** `IAuditRecord`, `IAuditRecorder`, `IAuditQuery`
(see `Public Interface Catalogue.md`).

**Service Lifetime.** DI-public, container-constructed singleton,
Phase 6. Depends on Persistence and Identity & Permissions, both
registered earlier in the same phase.

**Thread Safety Expectations.** `RecordAsync` must be safe for
concurrent invocation from multiple callers (every REST request is a
plausible concurrent recorder) without losing or corrupting any
individual record — append-only semantics make this simpler than
Settings' read-modify-write concern, since there is no existing value to
race against.

**Failure Behaviour.** `RecordAsync` propagates
`PersistenceStoreUnavailableException` unchanged if the underlying store
fails — an audit record that silently fails to record is a worse
outcome than a loud failure the caller must handle, since a missing
audit record could itself be a security-relevant gap. `WP 6.5`'s own
architecture phase should explicitly decide whether a `RecordAsync`
failure should ever be allowed to abort the *original* action being
audited, or whether audit failure must be isolated from the primary
operation — this review flags the tension without resolving it.

**Logging Requirements.** Optional `ILogger?`, for the Audit service's
own operational health (e.g., "audit write failed") — never as a
substitute for the durable audit record itself; the two are
deliberately distinct (see `ADR-0045`).

**Configuration Requirements.** None beyond what Persistence itself
requires.

**Event Publication Rules.** Publishes no events. Audit is a read/write
sink, not a source other services react to — a future "new audit record
recorded" notification is conceivable but not part of this release's
own scope.

**Persistence Requirements.** Every audit record must be durable and
queryable indefinitely (no automatic expiry in this release's own
scope) — a retention/archival policy is a plausible future requirement,
not committed to here.

**Security Considerations.** Audit records may themselves contain
sensitive detail (what action, what parameters) — access to
`IAuditQuery` should itself be permission-gated via Identity &
Permissions (an audit trail that anyone can read is itself a
disclosure risk), named here as a requirement for `WP 6.5`'s own
architecture phase to design explicitly, not assumed.

**Versioning Policy.** `IAuditRecord`'s shape is expected to be stable;
the `Detail` dictionary's own per-action key/value content is each
calling service's own concern and may evolve without changing
`IAuditRecord` itself.

**Performance Expectations.** `RecordAsync` should not meaningfully slow
down the action it is recording — an asynchronous, fire-and-forget-safe
write path (with its own failure handling, per Failure Behaviour above)
is preferable to a synchronous write blocking the caller's own request/
response cycle, a design point `WP 6.5`'s own architecture phase should
resolve explicitly.

**Testing Requirements.** Record/query round-trip correctness; query
filter correctness (by actor, action, date range); concurrent
`RecordAsync` calls do not lose records; permission-gating on
`IAuditQuery` (an unauthorized caller cannot query).

**Future Extension Points.** Retention/archival policy; a richer query
language beyond the initial `AuditQueryCriteria` shape; export of audit
records through `WP 6.7` (Export/Import) for compliance reporting.

---

## Licensing Framework *(`Tempest.Core.Licensing`, `WP 6.6`)*

**Purpose.** What capability is enabled, for whom, until when.

**Responsibilities.** Validate a license at Host startup; expose the
current license's own entitlements read-only thereafter. Does not
itself implement any licensed feature's own gating logic beyond
answering "is this capability enabled" — a consuming module decides what
to do with that answer.

**Public Interfaces.** `ILicense`, `ILicenseValidator`,
`ILicenseProvider` (see `Public Interface Catalogue.md`).

**Service Lifetime.** `ILicenseValidator` — Composition-Root-constructed,
pre-container, before Logging Built. `ILicenseProvider` —
Composition-Root-constructed, `AddInstance`, Phase 6 (see `Service
Lifecycle.md`).

**Thread Safety Expectations.** `ILicenseProvider.HasCapability`/
`CurrentLicense` must be safe for concurrent reads (the underlying
`ILicense` is immutable once validated, so this is satisfied trivially
by construction, not by any explicit synchronization).

**Failure Behaviour.** An invalid license aborts Host startup entirely —
Host-fatal, per `ADR-0013`'s existing platform-service-failure
classification, applied to Licensing without modification (`ADR-0050`).
`LicenseValidationResult.FailureReason` must be human-readable and
specific enough for an operator to act on (expired vs. malformed vs.
missing file), not a generic "invalid license" message.

**Logging Requirements.** Optional `ILogger?`, though note Licensing
validates *before* Logging Built (see Service Lifetime) — its own
validation-time diagnostic output, if any, necessarily predates the
platform's own logging infrastructure and must use a minimal,
direct fallback (e.g., a bootstrap console write), mirroring how
Configuration itself has no logger available at its own construction
point.

**Configuration Requirements.** The license file's own location is a
fixed, documented convention (mirroring Plugin Manifest's own fixed
`Plugins/`/`plugin.manifest.json` convention, `TD-06`) rather than
sourced from `IConfigurationProvider` — Configuration itself is not yet
built at the point Licensing validates.

**Event Publication Rules.** Publishes no events — there is no Event Bus
yet at Licensing's own validation-time construction point, and no
plausible "license changed" event once the process is running, since
`ILicense` is immutable for the life of the running instance (a license
change requires a restart in this release's own scope).

**Persistence Requirements.** None — Licensing deliberately does not
depend on `IPersistenceStore` (`ADR-0050`'s own explicit rejection of
that dependency, to avoid recreating `WP 5.2`'s Composition-Root timing
problem).

**Security Considerations.** The license file's own integrity
(tamper-resistance, signature verification) is a genuine question this
review surfaces but does not resolve — `WP 6.6`'s own architecture
phase must decide whether license validation includes a cryptographic
signature check or trusts the file's own contents at face value, and
disclose whichever it chooses as a named trade-off if the answer is the
latter.

**Versioning Policy.** `ILicense`'s own shape (licensee name, expiry,
enabled capabilities) is expected to be stable; new capability strings
may be added additively by any future Work Package without changing
`ILicense` itself.

**Performance Expectations.** Validation happens exactly once, at
startup — no ongoing performance concern. `HasCapability` should be
O(1) (a set lookup), since it may be called on a hot path by any
capability-gated code.

**Testing Requirements.** Valid/expired/malformed/missing-file
validation outcomes; Host-fatal abort behaviour on invalid license
(mirroring existing `ADR-0013` platform-service-failure tests);
`HasCapability` correctness against a known license fixture.

**Future Extension Points.** Remote validation/activation (explicitly
deferred — see `Technical Debt Assessment.md`); floating/seat-based
licensing; a license-renewal/grace-period model.

---

## Export / Import *(`Tempest.Core.ExportImport`, `WP 6.7`)*

**Purpose.** User-facing, `Stream`-based, portable-artifact I/O,
distinct from the internal Persistence abstraction.

**Responsibilities.** Export one or more `IExportable` sources into a
single portable artifact; import a previously exported artifact back
into its owning service(s), checking schema-version compatibility first.

**Public Interfaces.** `IExportable`, `IExportService`, `IImportService`
(see `Public Interface Catalogue.md`).

**Service Lifetime.** DI-public, container-constructed singleton,
Phase 6.

**Thread Safety Expectations.** `ExportAsync`/`ImportAsync` operate on a
caller-supplied `Stream` for the duration of one call — no shared
mutable state within the service itself, so thread safety reduces to
"two concurrent calls with two distinct streams do not interfere,"
which holds trivially if the service holds no instance state beyond its
own registered `IExportable` sources' own thread-safety guarantees.

**Failure Behaviour.** `ImportAsync` throws
`IncompatibleExportSchemaException` for an artifact whose
`SchemaVersion` the current platform version does not support — never
attempts a best-effort partial import of an incompatible artifact. A
mid-export or mid-import I/O failure on the caller's own `Stream`
propagates as the underlying `IOException` unchanged — Export/Import
does not wrap or reinterpret a caller's own stream failure.

**Logging Requirements.** Optional `ILogger?`. Logs at information level
on successful export/import (what was exported/imported, schema
version); at warning level on a schema-incompatibility rejection.

**Configuration Requirements.** None.

**Event Publication Rules.** Publishes no events of its own — a
consuming module may choose to raise a notification once its own
`IExportable`/import-consuming logic completes, but Export/Import
itself stays uninvolved, mirroring Reporting's own orthogonal stance.

**Persistence Requirements.** None directly — Export/Import reads
*from* whatever service owns the exported data (via that service's own
public interface, per `ADR-0051`), and writes *to* the caller-supplied
`Stream`, never touching `IPersistenceStore` itself.

**Security Considerations.** An exported artifact may contain sensitive
data (e.g., a Settings export including a sensitive setting value, per
Settings' own Security Considerations, above) — `IExportable`
implementations are individually responsible for redacting or refusing
to export sensitive content; `IExportService`/`IImportService`
themselves impose no content-level policy, mirroring how Persistence
itself imposes no content-level policy on what Settings/Audit choose to
store.

**Versioning Policy.** `IExportable.SchemaVersion` is the explicit
mechanism for this — every exporter declares its own schema version,
and `IImportService` must reject (not silently downgrade or upgrade) an
incompatible version, per `ADR-0051`'s own "versioned, round-trip-safe
contract" requirement.

**Performance Expectations.** Bounded by the caller's own `Stream` and
by whatever `IExportable` source is being read/written — no
service-level performance requirement beyond not introducing its own
unnecessary buffering or copying beyond what streaming the data through
genuinely requires.

**Testing Requirements.** Export/import round-trip correctness for a
representative `IExportable` source; schema-incompatibility rejection;
multi-source export/import (more than one `IExportable` in a single
artifact); stream-failure propagation.

**Future Extension Points.** Compression of the exported artifact;
encryption of sensitive exported content; a migration path for a schema
version bump (currently: reject; a future release could add
best-effort upgrade logic explicitly, as its own ADR-worthy decision).

---

## Cross-Service Consistency Check

Every service above answers "Security Considerations" and "Failure
Behaviour" with a concrete, service-specific statement — none is
"Not applicable," confirming every proposed service has at least one
genuine security- or failure-relevant property worth stating before
implementation. "Event Publication Rules" and "Persistence
Requirements" are the two dimensions most often answered "none" or "not
applicable" (Reporting, REST API, Licensing, Export/Import for
persistence; Persistence, Reporting, REST API, Audit, Licensing,
Export/Import for events) — each such answer is stated explicitly above
with its own reasoning, not left blank.

## Related Documents

`Release Architecture.md`; `Platform Services Overview.md`; `Public
Interface Catalogue.md`; `Service Lifecycle.md`; `Required ADRs.md`;
`Risk Register.md`; `Technical Debt Assessment.md`; `Platform Service
Implementation Order.md`; `Service Registration Matrix.md`; `Testing
Strategy.md`.
