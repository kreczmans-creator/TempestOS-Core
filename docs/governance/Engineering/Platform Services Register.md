# Platform Services Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Platform Services Register |
| **Purpose** | The governance-level index of every platform service TempestOS provides — status, originating Work Package, and ADR backing — cross-referenced against the ADR and Test Registers. |
| **Scope** | Every service listed in `docs/architecture/Platform Service Map.md`'s own "At a Glance" table. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/architecture/Platform Service Map.md` — the full responsibility/dependency/consumer/lifecycle detail for each service lives there; this register does not repeat it, only indexes it against governance status. |
| **Review Frequency** | Updated whenever `Platform Service Map.md` itself is updated (Engineering Governance §6) — i.e., whenever a service is added, removed, or changes responsibility/dependencies/consumers. |
| **Last Reviewed** | 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline — Second Pass) — reviewed, zero new row added: a second, independent re-verification of `WP 9.8B`'s own backfill, not merely trusting that Work Package's own claim. All 30 entries re-confirmed directly, consistent across all five governance documents this project maintains for Platform Services — **the first release-closing review in this project's history to find this specific gap closed rather than open.** Previously reviewed 2026-08-07 (WP 9.8B, Platform Service Register Reconciliation) — **four rows added** (Engineering Data Model, Materials, Engineering Calculations, Verification), closing the disclosed four-Engineering-Foundation-framework gap `WP 7.3A` first found, confirmed open across three consecutive release-closing reviews (`WP 7.4.0`/`WP 8.9.0`/`WP 9.9.0`), never itself the direct subject of a Work Package until now; 26 → 30 total (also corrects this register's own stale "27" headline figure, which never matched its own bucket arithmetic). Four complete Responsibility/Key types/Dependencies/Consumers/Lifecycle/ADR/Academy-reference sections added to `Platform Service Map.md` in the same pass — verification and documentation only, no implementation change, no architectural redesign, no new service invented. Cross-checked service ownership/lifetime/registration/dependencies/consumers directly against source (`TempestHost.cs`, each service's own real constructor, a repository-wide consumer search) for all four, plus re-verified `Interface Register.md`/`Dependency Injection Register.md`/`Module Register.md` already correctly tracked all four (confirmed, not assumed) — the drift was confined to this register and `Platform Service Map.md` alone. **No outstanding Platform Service governance inconsistency remains.** See `WP9.8B Reconciliation Report.md`. Previously reviewed 2026-08-07 (WP 9.9.0, Release Preparation & Product Baseline) — reviewed, zero new row added: verification-only Work Package; all 27 catalogued entries re-verified directly, unchanged. **The four-Engineering-Foundation-framework gap `WP 7.3A` first disclosed is now confirmed open across three consecutive release-closing reviews** (`WP 7.4.0`, `WP 8.9.0`, `WP 9.9.0`) — escalated as this release's own top standing recommendation in `WP9.9.0 Product Approval Report.md`, since `WP 8.9.0`'s own identical recommendation was not acted on during `v0.9.0`. Every one of the seven `v0.9.0` Work Packages individually reconfirmed the gap open in their own review; none widened it (no real Engineering Discipline is a Platform Service by this platform's own established taxonomy). `v0.9.0` ("Mechanical Foundation") recommended **APPROVED** for Product Approval by this Work Package — see `WP9.9.0 Product Approval Report.md`. Previously reviewed 2026-08-07 (WP 9.5A, Manufacturing Workspace) — reviewed, no new row added: no new Platform Service was introduced; `ManufacturingObjectFactoryRegistry`/`ManufacturingNodeProvider`/`ManufacturingWorkspaceRegistration` are composition-root components (`ADR-0062`), never Host-resolved services, exactly as `WP 9.0A`–`WP 9.3A` already established for their own equivalents. The four-Engineering-Foundation-framework gap `WP 7.3A` first disclosed remains open, now confirmed open a seventh consecutive release cycle — disclosed again, not fixed, since resolving it remains outside this Work Package's own scope. This Work Package's own controlling instruction skips `WP 9.6A`–`WP 9.8A` and moves directly to `WP 9.9.0` Release Preparation — recorded here as a plain observation, not an inconsistency, per `PROJECT_STATUS.md`. Previously reviewed 2026-08-07 (WP 9.3A, Verification Management Workspace) — reviewed, no new row added: no new Platform Service was introduced; `VerificationActivityFactoryRegistry`/`VerificationActivityNodeProvider`/`VerificationWorkspaceRegistration` are composition-root components (`ADR-0062`), never Host-resolved services, exactly as `WP 9.0A`–`WP 9.4A` already established for their own equivalents. The four-Engineering-Foundation-framework gap `WP 7.3A` first disclosed remains open, now confirmed open a sixth consecutive release cycle — this Work Package directly touches the Verification framework specifically (`IVerificationService`), making its own continued absence from this register especially visible, identically to how `WP 9.2A` observed the same for Calculations; disclosed again, not fixed, since resolving it remains outside this Work Package's own scope. This Work Package closes the disclosed `WP 9.3A` numbering gap `WP 9.2A` left open and `WP 9.4A` recommended filling (`FCR-0055`); it completed, in real time, after `WP 9.4A` despite its own earlier number. Previously reviewed 2026-08-06 (WP 9.4A, Engineering Documents Workspace) — reviewed, no new row added: no new Platform Service was introduced; `DocumentObjectFactoryRegistry`/`DocumentsNodeProvider`/`DocumentsWorkspaceRegistration` are composition-root components (`ADR-0062`), never Host-resolved services, exactly as `WP 9.0A`–`WP 9.2A` already established for their own equivalents. The four-Engineering-Foundation-framework gap `WP 7.3A` first disclosed remains open, now confirmed open a fifth consecutive release cycle — disclosed again, not fixed, since resolving it remains outside this Work Package's own scope. This Work Package's own controlling instruction was received under a disclosed `WP 9.3A` numbering gap (see `PROJECT_STATUS.md`), not silently resolved by this register. Previously reviewed 2026-08-05 (WP 9.2A, Engineering Calculations Workspace) — reviewed, no new row added: no new Platform Service was introduced; `CalculationTemplateRegistry`/`CalculationObjectFactoryRegistry` are composition-root components (`ADR-0062`), never Host-resolved services, exactly as `WP 9.0A`/`WP 9.0B` already established for their own Mechanical-discipline equivalents. The four-Engineering-Foundation-framework gap `WP 7.3A` first disclosed remains open, now confirmed open a fourth consecutive release cycle — this Work Package directly touches the Calculations framework specifically (`ICalculationEngine`), making its own continued absence from this register especially visible; disclosed again, not fixed, since resolving it remains outside this Work Package's own scope. **Disclosed observation:** `WP 9.1A` (Requirements Management Workspace) does not appear to have reviewed this register at all — the immediately-prior entry below is `WP 9.0B`'s own — a gap in that Work Package's own governance closure this review surfaces but does not retroactively correct, per "never silently modify historical records." Previously reviewed 2026-08-05 (WP 9.0B, Product Configuration & BOM Management) — reviewed, no new row added: no new Platform Service was introduced; `IReferenceIntegrityChecker` (already registered, `WP 6.x`-era) gained its first real, non-test caller (`ValidateConfigurationCommandHandler`) but no new registration. The four-Engineering-Foundation-framework gap remains open — confirmed open again within the same `v0.9.0` release cycle `WP 9.0A` already confirmed it open in, not a new cycle; disclosed again, not fixed, since resolving it remains outside this Work Package's own scope. Previously reviewed 2026-08-05 (WP 9.0A, Mechanical Product Structure) — reviewed, no new row added: no new Platform Service was introduced. `Tempest.App.Workspace`/`Tempest.App.Workspace.Mechanical` remain composition-root components (`ADR-0062`), never Host-resolved services, exactly as `WP 8.1A` already established for `WorkspaceManager` itself. The four-Engineering-Foundation-framework gap `WP 7.3A` first disclosed remains open, now confirmed open a third consecutive release cycle (`WP 8.9.0` confirmed it open a second); disclosed again, not fixed, since resolving it is outside this Work Package's own Mechanical Product Structure scope. Previously reviewed 2026-07-30 (WP 7.3A, Requirements Engine) — Requirements Engine row updated Planned → Implemented; found and disclosed (not fixed) a pre-existing gap: `WP 7.1A`–`WP 7.1E`'s own four Engineering Foundation frameworks (Engineering Data Model, Materials, Calculations, Verification) were never added as rows here or in `Platform Service Map.md`. Previously reviewed 2026-07-29 (WP 6.6, Licensing). |
| **Related Documents** | `docs/architecture/Platform Service Map.md`; `Architecture Document Register.md`; `Module Register.md`; `Hosted Services Register.md`; `Event Catalogue.md`. |
| **Related ADRs** | ADR-0005 through ADR-0052 — nearly every ADR concerns one of these services directly or the boundary between them. Plus ADR-0053, ADR-0055–ADR-0061 (the Engineering Foundation and Requirements Engine frameworks) — a disclosed correction, `WP 9.8B`: this field's own range never extended past `ADR-0052` despite the register's own rows for Requirements Engine (`WP 7.3A`) and, now, the four Engineering Foundation frameworks each citing ADRs outside it. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/` (The Module Pipeline, The Startup Sequence, Working with the TempestOS Host, Platform Layering, Plugin Architecture, Failure Isolation Across TempestOS, `15-engineering-data-model.md`, `13-calculation-framework.md`, `14-verification-framework.md`, `16-requirements-engine.md`). |
| **Coverage Status** | **Complete** — the four Engineering Foundation frameworks (`WP 7.1A`–`WP 7.1E`) received rows here and complete detail sections in `Platform Service Map.md` by `WP 9.8B`, closing the gap `WP 7.3A` first disclosed; Requirements Engine's own row remains current (`WP 7.3A`). No known outstanding Platform Service governance inconsistency remains as of `WP 9.8B` — see `WP9.8B Reconciliation Report.md`. |

---

## Entries

| Service | Status | Originating Work Package | Key ADRs |
|---|---|---|---|
| Platform Version | Implemented | WP 4.2A | ADR-0009, ADR-0023 |
| Configuration | Implemented | WP 2.5 | ADR-0009 |
| Logging | Implemented | WP 2.6 | ADR-0009, ADR-0010 |
| Dependency Injection | Implemented | WP 2.4 | ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0009 |
| Discovery | Implemented | WP 2.1 | ADR-0003, ADR-0008 |
| Registration | Implemented | WP 2.2 | ADR-0001, ADR-0002 |
| Lifecycle | Implemented | WP 2.3 | ADR-0002, ADR-0003, ADR-0004, ADR-0007 |
| Module SDK | Implemented (developer convenience layer, not Host-orchestrated) | WP 4.1 | None new (applies ADR-0003) |
| Host | Implemented | WP 2.7 (design), WP 2.7B (implementation) | ADR-0004, ADR-0008, ADR-0009, ADR-0011–ADR-0019 |
| Event Bus | Implemented | WP 4.4 (design), WP 4.4D (implementation), WP 4.4E (first consumer) | ADR-0020, ADR-0028 |
| Background Services | Implemented | WP 4.5 (design), WP 4.5 (implementation) | ADR-0021, ADR-0029, ADR-0030 |
| Command Framework | Implemented | WP 4.0 (contract), WP 5.1A (design), WP 5.1B (implementation) | ADR-0022, ADR-0024, ADR-0036, ADR-0037, ADR-0038 |
| Plugin Manifest | Implemented | WP 4.2 (design and implementation), WP 4.2A, WP 4.2B, WP 4.2C | ADR-0025, ADR-0026 |
| Navigation | Implemented | WP 5.0A (design), WP 5.0B (implementation) | ADR-0022, ADR-0031, ADR-0032 |
| Diagnostics | Implemented | WP 5.2 (design and implementation) | ADR-0009, ADR-0017, ADR-0034, ADR-0039 |
| Identity & Permissions | Implemented | WP 6.1 (design and implementation, no separate architecture phase per the release-wide `v0.6.0` architecture/contract review packages) | ADR-0043, ADR-0044 |
| Persistence | Implemented | WP 6.4 (established as part of its own scope; design and implementation, no separate architecture phase) | ADR-0041 |
| Settings | Implemented | WP 6.4 (design and implementation, no separate architecture phase) | ADR-0041, ADR-0042 |
| Audit | Implemented | WP 6.5 (design and implementation, no separate architecture phase) | ADR-0041, ADR-0044, ADR-0045 |
| Notifications | Implemented | WP 6.2 (design and implementation, no separate architecture phase) | ADR-0028, ADR-0046 |
| Reporting | Implemented | WP 6.0 (design and implementation, no separate architecture phase) | ADR-0038, ADR-0040 |
| REST API | Implemented | WP 6.3 (design and implementation, no separate architecture phase) | ADR-0047, ADR-0048, ADR-0049, ADR-0052 |
| Export/Import | Implemented | WP 6.7 (design and implementation, no separate architecture phase) | ADR-0044, ADR-0051 |
| Licensing | Implemented | WP 6.6 (design and implementation, no separate architecture phase) | ADR-0009, ADR-0013, ADR-0023, ADR-0044, ADR-0050 |
| Engineering Data Model | Implemented | WP 7.1A (design and implementation, no separate architecture-phase row here — see `WP 7.0C` for the shared Engineering Foundation contract review) | ADR-0053 |
| Materials | Implemented | WP 7.1C (design and implementation, no separate architecture-phase row here — see `WP 7.0C`) | ADR-0055 |
| Engineering Calculations | Implemented | WP 7.1D (design and implementation, no separate architecture-phase row here — see `WP 7.0C`) | ADR-0056 |
| Verification | Implemented | WP 7.1E (design and implementation, no separate architecture-phase row here — see `WP 7.0C`) | ADR-0057 |
| Project Engine | Not implemented as a platform service — bootstrap-era code (`Tempest.Core.Projects`, `ProjectService`, `JsonProjectRepository`) predates and is independent of the module pipeline | Planned, no Work Package assigned | None |
| Requirements Engine | Implemented | WP 7.3A (design and implementation, no separate architecture-phase row here — see `WP 7.2B`/`WP 7.2C` for the architecture and contract review) | ADR-0058, ADR-0059, ADR-0060, ADR-0061 |

**Total: 30 entries — 28 Implemented, 1 planned with no code (Project
Engine), 1 developer-convenience layer (Module SDK). Four new rows added
by `WP 9.8B` (Engineering Data Model, Materials, Engineering Calculations,
Verification), closing the disclosed gap `WP 7.3A` first found — see the
"Disclosed pre-existing gap" note below, now marked Resolved.**

**A further, disclosed correction, `WP 9.8B`:** this line previously
read "Total: 27 entries" against its own stated bucket arithmetic ("24
Implemented, 1 planned..., 1 developer-convenience layer" — `24 + 1 + 1
= 26`, not 27) and against a direct row count of the table above (26
rows, before this Work Package's own four additions) — a genuine,
pre-existing arithmetic drift, distinct from the four-row omission
itself, found while re-deriving this total directly rather than
carrying it forward unchecked. Corrected here to the true baseline (26),
now 30 after this Work Package's own additions — not silently.

**Disclosed pre-existing gap, found during `WP 7.3A`'s own review — RESOLVED by `WP 9.8B`.** This register — and `Platform Service
Map.md`, its own cited Source of Truth — had never carried rows for the
four Engineering Foundation frameworks implemented by `WP 7.1A`–`WP
7.1E` (Engineering Data Model, Materials, Calculations, Verification).
`docs/governance/Future Capability Register.md` and `docs/governance/
Engineering/Interface Register.md`/`Dependency Injection Register.md`/
`Module Register.md` all correctly tracked these four as Implemented
throughout; only this register and `Platform Service Map.md` did not —
confirmed, not merely assumed, by `WP 9.8B`'s own direct cross-check of
all five governance documents (see `WP9.8B Reconciliation Report.md`).
This was the same class of governance-register drift `WP 7.1F` found
and closed for `Interface Register.md`/`Dependency Injection
Register.md`/`Module Register.md`, recurring here in a register `WP
7.1F`'s own review did not check — left open, disclosed but not fixed,
across `WP 7.4.0`, `WP 8.9.0`, and `WP 9.9.0` (three consecutive
release-closing reviews, each correctly declining to fix it as outside
their own scope), until `WP 9.8B` — a Work Package commissioned
specifically to close it. **Four rows now added above; four full
Responsibility/Key types/Dependencies/Consumers/Lifecycle/ADR-reference/
Academy-reference sections now added to `Platform Service Map.md`.**
Nothing was invented or redesigned — every fact backfilled was verified
directly against `TempestHost.cs`'s own real registration code, each
framework's own real constructor dependencies, and a direct,
repository-wide search for real consumers.

## Verification of "Implemented" Status

Each service marked Implemented above is **Verified** by direct
correspondence to a namespace under `src/Tempest.Core/` (or
`src/Samples/Tempest.Samples/` for the Event Bus's first consumer): the
service's key types exist in source, are exercised by at least one test in
the Test Register, and are described as implemented in
`Platform Service Map.md` itself. Project Engine and Requirements Engine
are marked "not implemented as a platform service" because the pre-module
bootstrap code they might relate to (`Tempest.Core.Projects`,
`Tempest.Core.Repositories`) was never integrated into, or classified
under, the module pipeline's own platform-service model (ADR-0013) — this
is **Verified** directly: no ADR classifies either, and no Work Package
claims to have implemented either as a platform service. Navigation is
marked Implemented as of `WP 5.0B`: `src/Tempest.Core/Navigation/`
contains `NavigationItem`, `INavigationProvider`/`NavigationService`,
`NavigationRequestedEvent`, and the `NavigationException` hierarchy,
exercised by 45 tests (`Test Register.md`) and registered as an ordinary
DI-public singleton in `TempestHost`'s existing Platform Services
Registered phase. Command Framework is marked Implemented as of `WP
5.1B`: `src/Tempest.Core/Commands/` contains `ICommand` (`WP 4.0`),
`ICommandHandler<TCommand>`, `ICommandDispatcher`/`CommandDispatcher`,
`ICommandRegistry`/`CommandRegistry`, `CommandDescriptor`, `CommandResult`,
and the `CommandException` hierarchy, exercised by 66 tests (`Test
Register.md`) and registered as ordinary DI-public singletons in
`TempestHost`'s existing Platform Services Registered phase. Diagnostics
is marked Implemented as of `WP 5.2`: `src/Tempest.Core/Diagnostics/`
contains `IDiagnosticsProvider`/`DiagnosticsProvider`, a read-only
projection over `IModuleLifecycleManager`/`IHostedServiceManager`'s own
existing snapshot data, registered via `AddInstance` (Composition Root
pattern, `ADR-0009`) rather than a container-constructed singleton, and
exercised by 17 tests (`Test Register.md`). The same Work Package also
resolved `TD-02` with `CompositeLogSink` (`src/Tempest.Core/Logging/`),
extending the existing Logging service rather than introducing a new one.
Notifications is marked Implemented as of `WP 6.2`:
`src/Tempest.Core/Notifications/` contains `INotification`,
`INotificationHandler<TNotification>`,
`INotificationDispatcher`/`NotificationDispatcher`,
`NotificationException`, and the additive
`IPlatformNotification`/`PlatformNotification`/`NotificationSeverity`
elaboration, registered as an ordinary DI-public singleton in
`TempestHost`'s existing Platform Services Registered phase (immediately
after `IEventBus`, mirroring `ADR-0046`'s own "built on the Event Bus's
own proven design" decision), and exercised by 54 tests (`Test
Register.md`).
Reporting is marked Implemented as of `WP 6.0`:
`src/Tempest.Core/Reporting/` contains `IReportDefinition`,
`IReportRenderer<TDefinition>`, `IReportingService`/`ReportingService`,
`ReportingException` and two subtypes, and the additive
`IReportTemplate<TDefinition>`/`PlainTextReportTemplate<TDefinition>`
elaboration, registered as an ordinary DI-public singleton in
`TempestHost`'s existing Platform Services Registered phase
(immediately after `IEventBus` and before Notifications, matching
`Service Registration Matrix.md`'s own recommended order), and
exercised by 39 tests (`Test Register.md`). Depends on nothing but
Dependency Injection itself — confirmed directly, consistent with
`Platform Service Implementation Order.md`'s own "no hard proposed-
service dependency" observation.
REST API is marked Implemented as of `WP 6.3`:
`src/Tempest.Core/Api/` contains `IApiEndpointRegistry`/`ApiEndpointRegistry`,
`ApiRouteDescriptor`, `ApiRequestHandler`, `RestApiHostedService`
(a real, hosted, Kestrel-backed HTTP listener — `ADR-0047`/`ADR-0049`),
`OpenApiDocumentGenerator`, and `ApiException` and one subtype.
`IApiEndpointRegistry` is registered as an ordinary DI-public singleton
in `TempestHost`'s existing Platform Services Registered phase;
`RestApiHostedService` is discovered and orchestrated identically to
any other hosted service, retiring `AT-07`. Exercised by 52 tests (`Test
Register.md`), including genuine, real-HTTP round trips (via
`HttpClient`) against a real, running `TempestHost`, and a genuinely
concurrent, per-request test proving `ADR-0052`'s own identity-
resolution design is safe under load.
Engineering Data Model is marked Implemented as of `WP 7.1A`:
`src/Tempest.Core/EngineeringData/` contains `IEngineeringDocument`,
`IDocumentRevision`, `IEngineeringDocumentStore`/`EngineeringDocumentStore`,
and `DocumentReference`, registered as an ordinary DI-public singleton
in `TempestHost`'s existing Platform Services Registered phase
(immediately after Persistence and Identity & Permissions, both of
which it depends on), and exercised by 25 tests. Four real,
non-test consumers confirmed by direct source inspection: Materials,
Engineering Calculations, Verification, and Requirements Engine each
build their own canonical Kind directly on it; the Engineering Domain's
own `EngineeringDomainContext` (`WP 8.2C`) is a fifth, later, cross-layer
consumer.
Materials is marked Implemented as of `WP 7.1C`:
`src/Tempest.Core/Materials/` contains `IMaterialCatalog`/`MaterialCatalog`
and `IMaterialSpecification`/`MaterialSpecification`, registered as an
ordinary DI-public singleton immediately after the Engineering Data
Model, and exercised by 55 tests. Real consumers confirmed directly:
`MaterialsSampleModule` and the base `EngineeringDomainSampleModule`
(`WP 8.2C`).
Engineering Calculations is marked Implemented as of `WP 7.1D`:
`src/Tempest.Core/Calculations/` contains `ICalculationDefinition<TInput,
TResult>`, `ICalculationEngine`/`CalculationEngine`, and
`CalculationRecord`, registered as an ordinary DI-public singleton
immediately after Materials, and exercised by 52 tests. Real consumers
confirmed directly: `CalculationSampleModule` and, at the Workspace
layer, `Tempest.App.Workspace.Calculations` (`WP 9.2A`).
Verification is marked Implemented as of `WP 7.1E`:
`src/Tempest.Core/Verification/` contains `IVerificationService`/
`VerificationService` and `IVerificationRecord`/`VerificationRecord`,
registered as an ordinary DI-public singleton immediately after
Engineering Calculations, and exercised by 49 tests. Real consumers
confirmed directly: `VerificationSampleModule`, Requirements Engine
(`GetEvidenceAsync`), and, at the Workspace layer,
`Tempest.App.Workspace.Verification` (`WP 9.3A`) and `.Manufacturing`
(`WP 9.5A`).

## Cross-Reference Check

Every service above appears in exactly one row of
`Platform Service Map.md`'s own "At a Glance" table — no service exists
in one document but not the other; re-verified directly by `WP 9.8B`
after adding the four new rows, not merely assumed consistent. Every
Implemented service has at least one corresponding entry in `Test
Register.md` and at least one Work Package retrospective in `Academy
Register.md` — for the four newly-added rows specifically: Engineering
Data Model (25 tests), Materials (55 tests), Engineering Calculations
(52 tests), Verification (49 tests), each independently re-run and
passing at the time of this cross-check.

**`WP 9.8B`'s own additional cross-checks**, per its own controlling
instruction: **Service ownership** — all four are `Tempest.Core`-owned,
DI-public (confirmed against `Interface Register.md`, unchanged by this
Work Package). **Lifetime** — all four are ordinary, container-constructed
singletons (confirmed against `Dependency Injection Register.md` and
directly against `TempestHost.cs`'s own `services.Singleton<...>()`
call sites, unchanged by this Work Package). **Registration** — all
four register in `TempestHost.cs`'s existing Phase 6 (Platform Services
Registered) block, in the order Engineering Data Model → Materials →
Engineering Calculations → Verification → Requirements Engine,
confirmed directly against source. **Dependencies** — each confirmed
directly against its own real constructor signature (Engineering Data
Model → Persistence, Identity & Permissions; Materials → Engineering
Data Model, Persistence; Engineering Calculations → Engineering Data
Model, Identity & Permissions; Verification → Engineering Data Model,
Identity & Permissions), not assumed from prior documentation.
**Consumers** — each confirmed by a direct, repository-wide search for
real (non-test) constructor usage, not carried forward from any prior
claim; every consumer named in `Platform Service Map.md`'s own four new
sections was found this way. All five cross-checked dimensions found
**fully consistent** across `Platform Services Register.md`,
`Platform Service Map.md`, `Dependency Injection Register.md`, `Module
Register.md`, and `Interface Register.md` — zero contradictions found.
