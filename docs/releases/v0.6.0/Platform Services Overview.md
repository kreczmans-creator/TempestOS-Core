# TempestOS v0.6.0 — Platform Services Overview

## Purpose

This document is the `v0.6.0`-scoped companion to `docs/architecture/
Platform Service Map.md`, describing every platform service this release
proposes to add — Reporting, a shared Persistence abstraction, Settings,
Identity & Permissions, Notifications, Audit, Licensing, Export/Import,
and the REST API — in the same "At a Glance" + per-service shape that
document already uses. **None of these services is implemented.** Once
a service is actually built, its entry is expected to migrate into
`Platform Service Map.md` itself (which is Academy material, maintained
continuously) rather than live here permanently — this document is a
release-scoped design artifact, not a second, competing index.

`WP 6.8` (Platform Services Integration Review) is not a service and has
no entry below — it is the closing Work Package that re-verifies this
entire release once the other eight substantially land.

## At a Glance

| Service | Owning WP | Depends on | Depended on by |
|---|---|---|---|
| Persistence | `WP 6.4` (established as part of Settings' own scope) | Dependency Injection | Settings, Audit |
| Reporting | `WP 6.0` | Dependency Injection, Command Framework (invocation only) | Any module producing report output |
| Identity & Permissions | `WP 6.1` | Dependency Injection | REST API, Audit, any module performing an authorization check |
| Notifications | `WP 6.2` | Dependency Injection, Event Bus | Any module or service raising a user/system-facing notice |
| REST API | `WP 6.3` | Background Services (`IHostedService`), Command Framework, Identity & Permissions | External HTTP clients |
| Settings | `WP 6.4` | Dependency Injection, Persistence | Any module with runtime-mutable configuration |
| Audit | `WP 6.5` | Dependency Injection, Persistence, Identity & Permissions | Any service recording an attributable action; REST API |
| Licensing | `WP 6.6` | None (deliberately a leaf, mirroring Platform Version) | Host (Composition Root, startup gate); any module checking entitlement |
| Export/Import | `WP 6.7` | Dependency Injection; reads from whatever service owns the exported data | Any module offering portable data exchange |

Arrows read exactly as in `Platform Service Map.md`: the third column
lists what *needs* the row; the second column lists what the row
*needs*. Every arrow points downward per `ADR-0023`; none of the nine
entries above introduces a cycle — verified explicitly in `Platform
Service Dependency Diagram.md`.

---

## Persistence *(proposed — established as part of `WP 6.4`'s own scope, `ADR-0041`)*

**Responsibility.** A minimal, internal, platform-owned key/value or
document-shaped durable store, existing solely so Settings and Audit (and
any future platform service with the same need) do not each invent an
incompatible storage mechanism. Not a general-purpose database
abstraction, an ORM, or a public application data layer — narrowly
scoped to what the platform's own services need to remember between
process runs.

**Key types (proposed).** `IPersistenceStore`, `PersistenceException` and
subtypes — see `Public Interface Catalogue.md`.

**Dependencies.** None beyond ordinary DI-public-service conventions
(`ILogger?`, optional). Explicitly **not** depended on by Licensing
(`ADR-0050`) or Export/Import (`ADR-0051`) — see `Release Architecture.md`'s
Cross-Service Orthogonality section for why each is excluded.

**Consumers.** Settings (`WP 6.4`, its own originating Work Package);
Audit (`WP 6.5`).

**Lifecycle.** Anticipated as an ordinary DI-public, container-constructed
singleton, registered during the existing Platform Services Registered
phase (Phase 6) — no new Host Lifecycle phase. See `Service Lifecycle.md`.

**ADR references (proposed).** `ADR-0041` (*A Shared Persistence
Abstraction Serves Settings and Audit*).

---

## Reporting Framework *(proposed — `WP 6.0`)*

**Responsibility.** Produces structured, formatted output — tabular,
document, or otherwise presentation-oriented — from platform or module
data, via a registered `IReportDefinition`/`IReportRenderer` pair,
invoked imperatively (never through open-generic DI resolution, per
`RD-0040`'s own already-established finding that this container cannot
do open-generic or keyed registration). Deliberately **not** a
replacement for Export/Import: a report may be lossy and is not
guaranteed round-trip-safe (`Release Architecture.md`'s Cross-Service
Orthogonality section).

**Key types (proposed).** `IReportDefinition`, `IReportRenderer`,
`IReportingService`, `ReportRequest`, `ReportResult`, `ReportingException`
and subtypes — see `Public Interface Catalogue.md`.

**Dependencies.** Dependency Injection. Optionally invoked through the
Command Framework (`GenerateReportCommand`), reusing `ICommandDispatcher`/
`ICommandRegistry` rather than inventing a second invocation mechanism.

**Consumers.** Any module registering a report definition; the REST API
(`WP 6.3`), as a plausible future consumer surfacing report generation
over HTTP, dispatching through the Command Framework exactly like any
other caller.

**Lifecycle.** Anticipated as an ordinary DI-public, container-constructed
singleton, Phase 6. See `Service Lifecycle.md`.

**ADR references (proposed).** `ADR-0040` (*Reporting Is DI-Public and
Orthogonal to Export/Import*).

---

## Identity & Permissions *(proposed — `WP 6.1`)*

**Responsibility.** Answers "who is performing this action, and are they
allowed to" — the platform's first genuine authorization concept.
Establishes an `IIdentity`/`IPrincipal` model and an
`ICurrentPrincipalAccessor` for resolving the acting principal, plus an
`IPermissionEvaluator` for checking whether that principal may perform a
given action. Positioned as the vehicle to finally resolve `TD-09`
(plugin isolation), `TD-10` (Navigation ownership), and `TD-11`
(Command/Navigation registration-order squatting) — see `Technical Debt
Assessment.md`. Explicitly scoped, per this review, as **local-only**
in its first iteration (no external identity-provider federation) — see
`Risk Register.md` and `ADR-0043`.

**Key types (proposed).** `IIdentity`, `IPrincipal`,
`ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `Permission`,
`PermissionDeniedException` and subtypes — see `Public Interface
Catalogue.md`.

**Dependencies.** Dependency Injection.

**Consumers.** The REST API (`WP 6.3`, a hard dependency — REST cannot
authorize requests without this service existing first); Audit
(`WP 6.5`, to attribute a recorded action to a principal); any module
performing its own authorization check.

**Lifecycle.** Anticipated as an ordinary DI-public, container-constructed
singleton, Phase 6. Given its stated likelihood of needing its own
architecture-then-implementation split (mirroring `WP 5.0A`/`WP 5.0B`),
this entry is expected to be superseded by a dedicated `docs/
architecture/Identity & Permissions Architecture.md` once that phase
runs.

**ADR references (proposed).** `ADR-0043` (*Identity Model Scope —
Local-Only, Extensible*), `ADR-0044` (*Authorization Enforcement Point*).

---

## Notification Framework *(proposed — `WP 6.2`)*

**Responsibility.** Tells a user, module, or external system that
something happened — built *on top of* the existing Event Bus, not a
replacement for it. An `INotification` is derived from (or raised
alongside) an `IEvent`; delivery and presentation are the Notification
Framework's own concern, distinct from the Event Bus's zero-guarantee
publish/subscribe semantics (`Release Architecture.md`'s Cross-Service
Orthogonality section). `INotificationHandler<TNotification>` is
subscribed imperatively at runtime, mirroring `IEventHandler<T>`'s own
already-proven shape — never resolved generically through the container
(`RD-0040`).

**Key types (proposed).** `INotification`, `INotificationDispatcher`,
`INotificationHandler<TNotification>`, `NotificationException` and
subtypes — see `Public Interface Catalogue.md`.

**Dependencies.** Dependency Injection, Event Bus (notifications are
derived from events; this service does not duplicate the Event Bus's
own dispatch machinery).

**Consumers.** Any module or platform service raising a user- or
system-facing notice; a plausible future Shell notification surface.

**Lifecycle.** Anticipated as an ordinary DI-public, container-constructed
singleton, Phase 6. See `Service Lifecycle.md`.

**ADR references (proposed).** `ADR-0046` (*Notifications Are Derived
From Events, Not a Replacement Pub/Sub*).

---

## REST API *(proposed — `WP 6.3`)*

**Responsibility.** Lets an external HTTP client invoke platform
capability from outside the running process — the first network-facing
surface this platform has ever exposed. Architecturally, this is a
long-running background process exactly as `IHostedService` (`WP 4.5`)
was designed for: start after Module Initialisation, run until
shutdown, stop before Module Disposal. Every REST endpoint dispatches
*through* the existing, unmodified Command Framework
(`ICommandRegistry.InvokeAsync`) and Diagnostics — introducing no second
invocation mechanism. Every request is authorized through Identity &
Permissions (`WP 6.1`) before dispatch; `WP 6.3` is explicitly blocked
on `WP 6.1` landing first (`WorkPackages.md`). Adopts ASP.NET Core/
Kestrel — part of the .NET SDK's own shared framework, not a third-party
NuGet dependency in the sense `ADR-0005` was written to avoid — as this
platform's first substantial reliance on a pre-built framework component
beyond the bare SDK; see `ADR-0048`/`ADR-0049`.

**Key types (proposed).** `IApiEndpointRegistry`, plus whatever minimal
ASP.NET Core hosting scaffold `WP 6.3`'s own architecture phase settles
on — deliberately not drafted further here, since the ASP.NET Core
adoption decision (`ADR-0049`) itself must be ratified first. See
`Public Interface Catalogue.md`.

**Dependencies.** Background Services (`IHostedService`), Command
Framework (dispatch), Identity & Permissions (authorization), Diagnostics
(status/health surfacing).

**Consumers.** Any external HTTP client.

**Lifecycle.** Anticipated as an `IHostedService` implementation,
discovered exactly like any other hosted service — start at Phase 8.1,
stop at Phase 10.1 (`ADR-0030`), retiring `AT-07`. No new Host Lifecycle
phase. See `Service Lifecycle.md`.

**ADR references (proposed).** `ADR-0047` (*REST API Is a Background
Hosted Service*), `ADR-0048` (*REST Endpoints Dispatch Through the
Existing Command Framework*), `ADR-0049` (*Adopting ASP.NET Core/Kestrel
for the REST API*).

---

## Settings Framework *(proposed — `WP 6.4`)*

**Responsibility.** User-changeable, runtime-mutable configuration —
explicitly distinct from Configuration (`WP 2.5`), which is read-only,
immutable, and loaded once at startup (`ADR-0009`, Case Study 05).
Settings values may change while the platform is running, and a change
is expected to be observable by interested consumers via an
`ISettingsChangedEvent : IEvent`, reusing the existing Event Bus contract
rather than inventing a new notification path. Establishes the shared
Persistence abstraction (`IPersistenceStore`) as part of its own scope.

**Key types (proposed).** `ISettingDefinition`, `ISettingsProvider`,
`ISettingsChangedEvent`, `SettingsException` and subtypes — see `Public
Interface Catalogue.md`.

**Dependencies.** Dependency Injection, Persistence (its own,
co-established abstraction), Event Bus (change notification).

**Consumers.** Any module with runtime-mutable configuration; the REST
API (a plausible future consumer for remote settings management,
authorized through Identity & Permissions like any other REST-exposed
capability).

**Lifecycle.** Anticipated as an ordinary DI-public, container-constructed
singleton, Phase 6. See `Service Lifecycle.md`.

**ADR references (proposed).** `ADR-0042` (*Settings Is DI-Public and
Distinct From Configuration*), `ADR-0041` (Persistence, shared with
Audit).

---

## Audit Framework *(proposed — `WP 6.5`)*

**Responsibility.** A durable, queryable, append-only record of who did
what, when — distinct from Logging (developer-facing, diagnostic, not
guaranteed durable) and Diagnostics (a live snapshot of *current* state,
not history). Depends on Identity & Permissions to attribute a recorded
action to an actual principal, and on the shared Persistence abstraction
(established by `WP 6.4`) for durable storage, rather than inventing a
second storage mechanism.

**Key types (proposed).** `IAuditRecord`, `IAuditRecorder`, `IAuditQuery`,
`AuditException` and subtypes — see `Public Interface Catalogue.md`.

**Dependencies.** Dependency Injection, Persistence, Identity &
Permissions (attribution).

**Consumers.** Any platform service or module recording an attributable
action; the REST API (recording inbound requests as audit records); a
plausible future compliance/reporting surface.

**Lifecycle.** Anticipated as an ordinary DI-public, container-constructed
singleton, Phase 6. See `Service Lifecycle.md`.

**ADR references (proposed).** `ADR-0045` (*Audit Is a Durable, Queryable
Record, Distinct From Logging and Diagnostics*), `ADR-0041` (Persistence,
shared with Settings).

---

## Licensing Framework *(proposed — `WP 6.6`)*

**Responsibility.** What capability is enabled, for whom, until when.
Deliberately kept as its own leaf dependency — reading its own
license-file source directly, mirroring Platform Version's own
"deliberately a leaf" position (`ADR-0023`) — specifically to avoid the
Composition-Root/container-timing chicken-and-egg problem `WP 5.2`/
`ADR-0039` had to solve with `Func<T>` accessors for Diagnostics.
License validation is a Host-startup, Composition-Root-level,
Host-fatal gate (`ADR-0013`'s existing platform-service-failure
classification, applied without modification) — an invalid license
aborts startup exactly as any other platform-service failure does.

**Key types (proposed).** `ILicense`, `ILicenseValidator`,
`ILicenseProvider`, `LicenseValidationException` and subtypes — see
`Public Interface Catalogue.md`.

**Dependencies.** None — deliberately a leaf, matching Platform
Version's own precedent.

**Consumers.** The Host (Composition Root, startup validation gate);
any module checking entitlement for a licensed capability via
`ILicenseProvider`.

**Lifecycle.** `ILicenseValidator` runs at the Composition Root, before
the DI container exists, mirroring Configuration's own pre-container
construction. `ILicenseProvider` (the read-only, post-validation view)
is registered via `AddInstance` (`ADR-0009`), Phase 6. See `Service
Lifecycle.md`.

**ADR references (proposed).** `ADR-0050` (*License Validation Is a
Host-Startup, Host-Fatal Gate*).

---

## Export / Import *(proposed — `WP 6.7`)*

**Responsibility.** User-facing, `Stream`-based, portable-artifact I/O —
explicitly distinct from the internal Persistence abstraction, which is
platform-owned key-value/document state, not a user-directed portable
file (`Release Architecture.md`'s Cross-Service Orthogonality section).
Export/Import reads *from* whatever service owns the data being
exported (Settings, a Reporting definition, and so on) — it does not
read `IPersistenceStore` directly. Establishes its own versioned,
round-trip-safe contract so an exported artifact can be re-imported
without data loss.

**Key types (proposed).** `IExportable`, `IExportService`,
`IImportService`, `ExportImportException` and subtypes — see `Public
Interface Catalogue.md`.

**Dependencies.** Dependency Injection; reads from whatever service owns
the data being exported, via that service's own public interface —
never a new, parallel data-access path.

**Consumers.** Any module offering portable data exchange (settings
backup/restore, report artifact download, and so on).

**Lifecycle.** Anticipated as an ordinary DI-public, container-constructed
singleton, Phase 6. See `Service Lifecycle.md`.

**ADR references (proposed).** `ADR-0051` (*Export/Import Is Orthogonal
to the Internal Persistence Abstraction*).

---

## Related Documents

`docs/architecture/Platform Service Map.md` (the living, cross-release
index this document is scoped alongside, not a replacement for);
`Release Architecture.md`; `Platform Service Dependency Diagram.md`;
`Public Interface Catalogue.md`; `Service Lifecycle.md`; `Required
ADRs.md`; `Risk Register.md`; `Technical Debt Assessment.md`;
`docs/releases/v0.6.0/WorkPackages.md`.
