# TempestOS Academy — Index

**Purpose.** One navigable table of contents for the entire Academy, so a
reader can find the right document by *topic* rather than by knowing which
numbered folder it happens to live in. This index is itself Academy
material, subject to the same maintenance obligation as everything else it
indexes (Engineering Governance §6): a work package that adds or rewrites an
Academy document updates this index as part of the same change, not as a
separate, later pass.

Every document is listed exactly once, under the section where a new reader
would most naturally look for it first; cross-references to other relevant
sections are noted inline.

---

## Welcome

Start here if this is your first time in the Academy.

- [Welcome to the TempestOS Academy](00%20Introduction/00-welcome-to-the-academy.md) — what the Academy is, who it's for, and how it's organised. Read this before anything else.
- [`PROJECT_STATUS.md`](../../PROJECT_STATUS.md) — where the project stands *right now* (repo root). Read this alongside Welcome — it tells you what's currently true; the Academy tells you why.
- [Contributor Learning Path](Contributor%20Learning%20Path.md) — the full, repository-wide onboarding sequence (README → FOUNDATION → PROJECT_STATUS → Academy → Architecture → Governance → ADRs → a real module and hosted service → contribution workflow), of which the Academy-internal path below is one step.
- [Engineering Governance](06%20Engineering%20Standards/Engineering%20Governance.md) — the project's constitution: how a work package moves from brief to merge, what "Done" requires, when an ADR or a Rejected Design entry is mandatory. The Welcome article tells you to read this second; this index repeats that instruction because it is the single most important document in the Academy after the welcome page itself.
- [Engineering Lifecycle](06%20Engineering%20Standards/Engineering%20Lifecycle.md) — the concrete Idea → Investigation → Architecture → ADR → Rejected Designs → Implementation → Testing → Architecture Review → Academy → Governance → Release → Maintenance pipeline every Work Package follows, elaborating Engineering Governance §1.

## Learning Path (Academy-Internal)

Once you've followed `Contributor Learning Path.md`'s own repository-wide
sequence as far as "how the platform works," this is the reading order
*within* the Academy specifically, for a new engineer with strong general
software engineering ability but no prior TempestOS or modular-runtime
experience:

1. **Welcome**, above, and **Engineering Governance**.
2. **Engineering Principles** (below) — the vocabulary the rest of the
   Academy assumes you already have.
3. **Platform Architecture** (below) — how the whole platform fits
   together, before any one piece of it.
4. **Runtime** (below) — the module pipeline and the Host, in detail.
5. Whichever of **Dependency Injection**, **Modules**, **Plugins**,
   **Events** you're about to work on, in depth.
6. The specific **Work Package Walkthrough** for whatever you're about to
   change — always read this before changing existing, working code.
7. **Case Studies** and **Design Patterns** as they're referenced from the
   above — they reward reading in context, not cover to cover up front.

## Engineering Principles

General software-engineering principles, explained on their own terms
first, then connected explicitly to how TempestOS applies them. Read these
if you want the vocabulary the rest of the Academy assumes.

- [SOLID](01%20Engineering%20Principles/01-solid.md)
- [Separation of Concerns](01%20Engineering%20Principles/02-separation-of-concerns.md)
- [Immutability](01%20Engineering%20Principles/03-immutability.md)
- [Composition Over Inheritance](01%20Engineering%20Principles/04-composition-over-inheritance.md)
- [Dependency Injection](01%20Engineering%20Principles/05-dependency-injection.md) — see also **Dependency Injection**, below, for the platform-specific deep dive.
- [Fail Fast](01%20Engineering%20Principles/06-fail-fast.md)
- [Deterministic Systems](01%20Engineering%20Principles/07-deterministic-systems.md)
- [State Machines](01%20Engineering%20Principles/08-state-machines.md)
- [Defensive Programming](01%20Engineering%20Principles/09-defensive-programming.md)
- [Single Responsibility Principle](01%20Engineering%20Principles/10-single-responsibility.md)
- [Atomic Phase Principle](01%20Engineering%20Principles/11-atomic-phase-principle.md)

## Platform Architecture

How TempestOS's platform is put together as a whole — the concepts that
span every individual service.

- [Platform Layering: Designing a Platform Service](02%20Runtime%20Architecture/06-platform-layering.md) — the four-layer model (Modules → Platform APIs → Platform Services → Runtime Host, ADR-0023) and how to classify a new capability against it.
- [Failure Isolation Across TempestOS](02%20Runtime%20Architecture/08-failure-isolation.md) — the recurring platform-service/module/plugin/subscriber isolation question, all four worked examples side by side.
- `docs/architecture/Platform Service Map.md` — the living, service-by-service index of what exists, what depends on what, and where to read more (outside the Academy folder, but maintained under the same obligation).
- `docs/architecture/Engineering Glossary.md` — the project's own vocabulary, alphabetical, cross-referenced.
- `docs/architecture/Rejected Designs.md` — the permanent record of designs seriously considered and declined; the mirror image of the ADR catalogue.
- `docs/adr/` — the full Architecture Decision Record catalogue (ADR-0001 through ADR-0039 at time of writing).

## Runtime

The module pipeline and the Runtime Host, holistically.

- [The Module Pipeline](02%20Runtime%20Architecture/01-the-module-pipeline.md) — Discovery → Registration → Lifecycle → Dependency Injection, as one connected system.
- [The Startup Sequence](02%20Runtime%20Architecture/02-the-startup-sequence.md) — why configuration (and later, logging) must exist before dependency injection begins, and the ordering this forces.
- [Working with the TempestOS Host](02%20Runtime%20Architecture/05-the-runtime-host.md) — a first-read guide to `TempestHost`, synthesising the six reference documents below into one narrative.
- `docs/architecture/Runtime Host Architecture.md` — the Host's responsibilities and non-responsibilities.
- `docs/architecture/Host Lifecycle.md` — every startup/shutdown phase, in order, with entry/exit/failure criteria.
- `docs/architecture/Runtime State Machine.md` — the Host's own seven-state machine.
- `docs/architecture/Shutdown Sequence.md` — controlled shutdown and post-fault teardown, side by side.
- `docs/architecture/Failure Behaviour.md` — every named failure mode, classified.
- `docs/architecture/Ownership Matrix.md` — who owns every significant runtime object.

## Dependency Injection

- [Dependency Injection](01%20Engineering%20Principles/05-dependency-injection.md) — the general principle and TempestOS's own container.
- [WP 2.4 — Dependency Injection](03%20Work%20Packages/WP2.4-dependency-injection.md) — the container's own implementation retrospective, including a real bug found and fixed during the work.
- [WP 4.4A — Dependency Injection for Discovered Modules](03%20Work%20Packages/WP4.4A-dependency-injection-for-discovered-modules.md) and [WP 4.4B — ADR-0027 Implementation](03%20Work%20Packages/WP4.4B-adr-0027-implementation.md) — how a discovered module gained the ability to request a DI-public service via constructor injection.
- `docs/architecture/Module Dependency Injection Architecture.md` — the design document behind ADR-0027.

## Modules

- [Building a Module](02%20Runtime%20Architecture/03-building-a-module.md) — the practical, module-author-facing guide, including the parameterless-constructor constraint and its attribute-based lift.
- [Building an Event-Driven Module](02%20Runtime%20Architecture/04-building-an-event-driven-module.md) — the same guide, extended for a module that publishes or subscribes to events.
- [WP 4.1 — Module SDK](03%20Work%20Packages/WP4.1-module-sdk.md) — `ModuleBase`/`ModuleLifecycleBase`.
- [WP 4.3 — Sample Module Architecture](03%20Work%20Packages/WP4.3-sample-module-architecture.md) and [Implementation](03%20Work%20Packages/WP4.3-sample-module-implementation.md) — `ClockModule`, the living reference module every later work package extends.
- [WP 4.4E — Sample Module Event Integration](03%20Work%20Packages/WP4.4E-sample-module-event-integration.md) — `ClockModule` extended to publish, and its companion module built to subscribe.
- [WP 5.3 — Developer Experience Improvements](03%20Work%20Packages/WP5.3-developer-experience-improvements.md) — the `dotnet new tempest-module` scaffolding template, and a Discovery pitfall closed with a clear error message instead of a raw runtime exception.
- `docs/architecture/Sample Module Architecture.md` — the full design document behind `ClockModule` and its companion.
- `src/Templates/README.md` — how to install and use the module template.

## Plugins

- [Plugin Architecture](02%20Runtime%20Architecture/07-plugin-architecture.md) — the concept guide: what a plugin manifest is, why it's a pre-discovery artifact, and how loading one requires zero change to Module Discovery.
- [WP 4.2 — Plugin Manifest Architecture](03%20Work%20Packages/WP4.2-plugin-manifest-architecture.md) and [Implementation](03%20Work%20Packages/WP4.2-plugin-manifest-implementation.md).
- [WP 4.2A — Runtime Platform Version](03%20Work%20Packages/WP4.2A-runtime-platform-version.md) — the platform-version prerequisite the Plugin Manifest design found and required.
- [WP 4.2B — Plugin Failure Classification](03%20Work%20Packages/WP4.2B-plugin-failure-classification.md) (ADR-0025).
- [WP 4.2C — Plugin Discovery Lifecycle Placement](03%20Work%20Packages/WP4.2C-plugin-discovery-lifecycle-placement.md) (ADR-0026).
- `docs/architecture/Plugin Manifest Architecture.md` and `docs/architecture/Platform Version.md` — the full design documents.

## Events

- [Building an Event-Driven Module](02%20Runtime%20Architecture/04-building-an-event-driven-module.md) — the practical guide to publishing and subscribing.
- [WP 4.4 — Event Bus Architecture](03%20Work%20Packages/WP4.4-event-bus-architecture.md) — the design phase, including the WP 4.4C discovery-and-redirect story.
- [WP 4.4D — Event Bus Implementation](03%20Work%20Packages/WP4.4D-event-bus-implementation.md) — `IEventBus`/`EventBus`, built and proven.
- [WP 4.4E — Sample Module Event Integration](03%20Work%20Packages/WP4.4E-sample-module-event-integration.md) — the Event Bus's first real consumer.
- `docs/architecture/Event Bus Architecture.md` — the full design document (ADR-0028).

## Background Services

Implemented (`WP 4.5`, ADR-0029/ADR-0030) — `Tempest.Core.BackgroundServices`.

- [WP 4.5 — Background Services Architecture](03%20Work%20Packages/WP4.5-background-services-architecture.md) — the design phase: classification, discovery, ownership, orchestration, ordering, failure model, and Host Lifecycle placement.
- [WP 4.5 — Background Services Implementation](03%20Work%20Packages/WP4.5-background-services-implementation.md) — `IHostedServiceDiscoveryService`/`HostedServiceDiscoveryService`, `IHostedServiceManager`/`HostedServiceManager`, wired into `TempestHost` at Phases 8.1/10.1, built and proven.
- `docs/architecture/Background Services Architecture.md` — the full design document.
- ADR-0021 (failure classification, decided during original v0.4.0 planning), ADR-0029 (discovery/ownership/orchestration model), ADR-0030 (Host Lifecycle placement).
- See also [Failure Isolation Across TempestOS](02%20Runtime%20Architecture/08-failure-isolation.md) for how ADR-0021 fits alongside the platform's other three isolation decisions, [Working with the TempestOS Host](02%20Runtime%20Architecture/05-the-runtime-host.md) for the two new phases (`8.1`, `10.1`) in context, and [Reflection-Based Discovery](04%20Design%20Patterns/04-reflection-based-discovery.md) for hosted service discovery as a third application of that pattern.

## Navigation

Implemented (`WP 5.0A` design, `WP 5.0B` implementation, ADR-0031/ADR-0032)
— `Tempest.Core.Navigation`.

- [Navigation Architecture](02%20Runtime%20Architecture/09-navigation-architecture.md) — the concept guide: why a UI-adjacent concept can still be architecturally UI-agnostic, the platform/application rendering boundary, the contribution model, and common mistakes.
- [WP 5.0A — Navigation Framework Architecture](03%20Work%20Packages/WP5.0A-navigation-framework-architecture.md) — the design phase: platform/application boundary, ownership, registration model, notification mechanism.
- [WP 5.0B — Navigation Framework Implementation](03%20Work%20Packages/WP5.0B-navigation-framework-implementation.md) — the implementation phase: `NavigationItem`/`NavigationService` built and proven against real modules, a real plugin assembly, and the real Host.
- `docs/architecture/Navigation Framework Architecture.md` — the full design document.
- ADR-0022 (Navigation/Command Framework orthogonality, decided during original v0.4.0 planning), ADR-0031 (Navigation belongs in `Tempest.Core`; rendering is an application responsibility), ADR-0032 (DI-public ownership, imperative registration, Event Bus reuse).
- See also [Platform Layering](02%20Runtime%20Architecture/06-platform-layering.md) for Navigation as a worked example of the four-layer model, and [Failure Isolation Across TempestOS](02%20Runtime%20Architecture/08-failure-isolation.md) for why Navigation needed no new failure model at all.

## Shell & Application Composition

Implemented (`WP 5.0C` design, `WP 5.0D` implementation,
ADR-0033/ADR-0034/ADR-0035) — the application shell, `Tempest.App`'s own
composition root.

- [Shell & Application Composition](02%20Runtime%20Architecture/10-shell-and-application-composition.md) — the concept guide: why "the thing that runs the app" is not the same component as "the thing the app runs," the composition-root relationship to the Runtime Host, and common mistakes.
- [WP 5.0C — Shell & Composition Framework Architecture](03%20Work%20Packages/WP5.0C-shell-and-composition-framework-architecture.md) — the design phase: platform/application boundary, composition model, `ITempestHost.Services`, page/view ownership.
- [WP 5.0D — Shell & Composition Framework Implementation](03%20Work%20Packages/WP5.0D-shell-and-composition-framework-implementation.md) — the implementation phase: `TempestShell` built and proven against the real Host and sample modules; `Tempest.App` runs the real platform for the first time.
- `docs/architecture/Shell & Composition Framework Architecture.md` — the full design document.
- ADR-0033 (the Shell is a composition root, not a module or hosted service), ADR-0034 (`ITempestHost` exposes a read-only service resolution surface), ADR-0035 (the Shell owns page/view construction, independent of the DI container).
- See also ADR-0009 for the forward reference this Work Package fulfils, and [Navigation Architecture](02%20Runtime%20Architecture/09-navigation-architecture.md) for the Rendering Boundary this design's own page ownership directly completes.

## Security

The v0.5.0 Security Baseline (`WP 5.0S`) — the platform's first
comprehensive security audit, and the standing reference every future
Work Package's Definition of Done is checked against.

- [WP 5.0S — Platform Security Baseline Audit](03%20Work%20Packages/WP5.0S-platform-security-baseline-audit.md) — the retrospective: threat modelling, secure platform design, secure plugin architecture, trust boundaries, least privilege, and secure engineering practice, taught from first principles.
- `docs/security/Threat Model.md` — assets, actors, trust boundaries, and threat scenarios.
- `docs/security/Security Principles.md` — the standing security principles the platform is designed against.
- `docs/security/Platform Security Review v0.5.0.md` — the full audit findings; establishes the Security Baseline.
- `docs/security/Security Roadmap.md` — prioritised future security work, sequenced against the Threat Model's own assumptions.

## Command Framework

Implemented — `WP 5.1A` design, `WP 5.1B` implementation
(ADR-0036–ADR-0038). `ICommand`'s own contract (`WP 4.0`) now has a real
handler contract and dispatcher, proven against a real sample module.

- [Command Framework](02%20Runtime%20Architecture/11-command-framework.md) — the concept guide: why commands exist, the Command/Mediator pattern, why TempestOS didn't adopt CQRS, and how `ICommandDispatcher`/`ICommandRegistry` answer two genuinely different callers' needs.
- [WP 5.1A — Command Framework Architecture](03%20Work%20Packages/WP5.1A-command-framework-architecture.md) — the design phase: registration model, dispatch model, the registration-order-squatting finding (`CMD-1`/`TD-11`), and why an implied prior direction (DI-resolved handlers) was resolved by reuse rather than a container redesign.
- [WP 5.1B — Command Framework Implementation](03%20Work%20Packages/WP5.1B-command-framework-implementation.md) — the implementation phase: `CommandDispatcher`/`CommandRegistry` built and proven against `CommandSampleModule`; the `CommandHandlerTable` sharing finding, resolved without reflection or a container redesign.
- `docs/architecture/Command Framework Architecture.md` — the full design document, including its own Implementation Note and Security Review Update.
- ADR-0022 (Navigation/Command orthogonality, decided during original v0.4.0 planning), ADR-0036 (Command Framework is DI-public), ADR-0037 (registration model), ADR-0038 (dispatch failure model — Case 5 of *Failure Isolation Across TempestOS*).
- See also [Failure Isolation Across TempestOS](02%20Runtime%20Architecture/08-failure-isolation.md) (Case 5: propagate, don't isolate) and [Navigation Architecture](02%20Runtime%20Architecture/09-navigation-architecture.md) (the closest structural precedent).

## Diagnostics

Implemented (`WP 5.2`, `ADR-0039`) — `Tempest.Core.Diagnostics`, and the
`CompositeLogSink` extension to `Tempest.Core.Logging`. Closes `TD-02`
outright and re-scopes `TD-01` forward again.

- [Diagnostics & Composite Logging](02%20Runtime%20Architecture/12-diagnostics-and-composite-logging.md) — the concept guide: why composite logging and read-only lifecycle-state visibility are the same underlying need, the `Func<T>` lazy-accessor pattern, and common mistakes.
- [WP 5.2 — Diagnostics Improvements](03%20Work%20Packages/WP5.2-diagnostics-improvements.md) — the combined design-and-implementation retrospective, including the opening "Event Framework" premise mismatch and its redirect (`D-019`), and the `TD-01` re-scoping decision (`D-020`).
- `docs/architecture/Diagnostics Architecture.md` — the full design document.
- ADR-0009 (Composition Root, reused), ADR-0017 (Host-owned collaborators never DI-public, the boundary this design respects), ADR-0034 (the `null`/empty-before-ready convention this design reuses), ADR-0039 (this Work Package's own decision).
- See also [Navigation Architecture](02%20Runtime%20Architecture/09-navigation-architecture.md) and [Shell & Application Composition](02%20Runtime%20Architecture/10-shell-and-application-composition.md) for `ITempestHost.Services`'s own precedent for the "not yet available" convention this design reuses.

### Identity & Permissions

Implemented (`WP 6.1`, `ADR-0043`, `ADR-0044`) — `Tempest.Core.Identity`:
a local-only identity model (`IIdentity`/`IPrincipal`), config-sourced
roles (`IRole`/`IRoleProvider`), an identity-resolution service
(`IIdentityService`), and a single authorization enforcement point
(`IPermissionEvaluator`). The platform's first authorization concept —
`TD-09`/`TD-10`/`TD-11` are now resolvable through it, though none is
retired by this Work Package itself.

- [WP 6.1 — Permissions & Identity Implementation](03%20Work%20Packages/WP6.1-permissions-and-identity-implementation.md) — implemented directly against the already-approved `v0.6.0` architecture and Contract Review packages, including the `AsyncLocal<T>`-vs-ambient-field finding (`ADR-0044`) and the honest disclosure that `TD-09`/`TD-10`/`TD-11` remain Open.
- `docs/releases/v0.6.0/Release Architecture.md` and companions — the architecture package this Work Package implemented.
- ADR-0043 (Identity Model Scope Is Local-Only, Extensible), ADR-0044 (`IPermissionEvaluator` Is the Single Authorization Enforcement Point; `CurrentPrincipalAccessor` Is Ambient, Not Request-Scoped).

### Persistence and Settings

Implemented (`WP 6.4`, `ADR-0041`, `ADR-0042`) — `Tempest.Core.Persistence`:
a minimal, file-backed key/value store established as part of Settings'
own scope; `Tempest.Core.Settings`: user-changeable, runtime-mutable
configuration, explicitly distinct from Configuration, with an
in-memory cache and `ISettingsChangedEvent` published through the
existing Event Bus.

- [WP 6.4 — Settings Framework Implementation](03%20Work%20Packages/WP6.4-settings-framework-implementation.md) — implemented directly against the already-approved `v0.6.0` architecture and Contract Review packages, including the shared-Persistence-abstraction ratification (`ADR-0041`) and the deliberate choice not to add a sensitive-value flag to an approved interface (`ADR-0042`).
- ADR-0041 (A Shared Persistence Abstraction Serves Settings and Audit), ADR-0042 (Settings Is DI-Public and Distinct From Configuration).

### Audit

Implemented (`WP 6.5`, `ADR-0045`) — `Tempest.Core.Audit`: a durable,
queryable, append-only record of who did what, when, explicitly
distinct from Logging and Diagnostics. Reuses the Persistence
abstraction `WP 6.4` established rather than introducing a second
storage mechanism; `IAuditQuery` is permission-gated through the same
enforcement point Identity & Permissions established (`ADR-0044`).

- [WP 6.5 — Audit Framework Implementation](03%20Work%20Packages/WP6.5-audit-framework-implementation.md) — implemented directly against the already-approved architecture and Contract Review packages, including the recording-model/permission-gating/Persistence-sufficiency decisions (`ADR-0045`) and a genuine, disclosed engineering-review finding: a premature-resource-disposal bug in two prior Work Packages' own Host-registration tests.
- ADR-0045 (Audit Is a Durable, Queryable, Append-Only Record, Distinct From Logging and Diagnostics — Recording Model, Permission Gating, and Persistence Sufficiency).

### Notifications

Implemented (`WP 6.2`, `ADR-0046`) — `Tempest.Core.Notifications`: the
standard platform mechanism for publishing user-facing and
platform-generated notifications, built on top of the existing Event
Bus's own proven dispatch model rather than a second, parallel
publish/subscribe implementation. Additive
`IPlatformNotification`/`NotificationSeverity`/`Category` fill the
severity/category gap the original interface draft never gave members.
Transient only this release; an isolated subscriber failure is logged
at `Warning`, a deliberate departure from the Event Bus's own `Error`
convention.

- [WP 6.2 — Notification Framework Implementation](03%20Work%20Packages/WP6.2-notification-framework-implementation.md) — implemented directly against the already-approved architecture and Contract Review packages, including the genuine C# generic-constraint impossibility that prevented literal delegation to `IEventBus` (`ADR-0046`) and a genuine, disclosed engineering-review finding: an exact-static-type-dispatch defect in this Work Package's own sample consumers.
- ADR-0046 (Notifications Are Derived From Events, Not a Replacement Pub/Sub — Dispatch Model, Severity/Category Elaboration, and Logging Level).

### Reporting

Implemented (`WP 6.0`, `ADR-0040`) — `Tempest.Core.Reporting`: the
single reporting engine every future module can depend on, registered
as an ordinary DI-public singleton with no permission-gating of its own
(the caller enforces, mirroring Navigation/Command Framework). Additive
`IReportTemplate<TDefinition>`/`PlainTextReportTemplate<TDefinition>`
separate a renderer's own data-gathering from layout/rendering.
Deliberately orthogonal to Export/Import (`WP 6.7`, now implemented,
`ADR-0051`) — no export interface was built inside Reporting.
Cross-service
integration (Identity, Settings, Audit, Notifications) is demonstrated
entirely at the sample module's own calling layer.

- [WP 6.0 — Reporting Framework Implementation](03%20Work%20Packages/WP6.0-reporting-framework-implementation.md) — implemented directly against the already-approved architecture and Contract Review packages, including the additive Template Strategy elaboration (`ADR-0040`) and a dedicated Platform Integration Demonstration assessing interactions with Identity, Settings, Persistence, Audit, and Notifications.
- ADR-0040 (Reporting Is DI-Public and Orthogonal to Export/Import — Template Abstraction, Cross-Service Integration, and Scope Boundaries).

### REST API

Implemented (`WP 6.3`, `ADR-0047`/`ADR-0048`/`ADR-0049`/`ADR-0052`) —
`Tempest.Core.Api`: lets an external HTTP client invoke platform
capability, hosted on ASP.NET Core/Kestrel confined to one type,
dispatching every route through the existing, unmodified Command
Framework with zero business logic of its own. This platform's first
genuinely concurrent, per-request scenario is resolved without touching
`CurrentPrincipalAccessor`'s own already-shipped ambient design — a
decision verified empirically (an `AsyncLocal<T>` alternative was built
and tested, and regressed 17 pre-existing tests) rather than reasoned
about alone. No real authentication exists this release — a disclosed,
deliberate limitation.

- [WP 6.3 — REST API Implementation](03%20Work%20Packages/WP6.3-rest-api-implementation.md) — implemented directly against the already-approved architecture and Contract Review packages, including the empirically-verified identity-resolution decision (`ADR-0052`) and a dedicated Platform Integration Demonstration.
- ADR-0047 (The REST API Is a Background Hosted Service), ADR-0048 (REST Endpoints Dispatch Through the Existing Command Framework), ADR-0049 (Adopting ASP.NET Core/Kestrel for the REST API), ADR-0052 (The REST API Resolves Identity Per-Request Without Touching the Ambient Current Principal — Empirically Verified).

### Export/Import

Implemented (`WP 6.7`, `ADR-0051`) — `Tempest.Core.ExportImport`: a
user-facing, `Stream`-based, portable-artifact I/O layer, explicitly
distinct from the internal `IPersistenceStore` abstraction. Additive
`IExportableKind`/`IImportable` route a multi-section artifact back to
the correct owning service by `Kind`, registered with `ImportService`'s
own concrete type — dual-registered under both that type and
`IImportService`, mirroring `ADR-0044`'s own `CurrentPrincipalAccessor`
precedent. Separate, optional `IExportFormat`/`JsonExportFormat`
(artifact framing) and `IExportPayloadSerializer`/
`JsonExportPayloadSerializer` (payload serialization) abstractions fill
the brief's own named scope without touching any approved interface.
Every section's compatibility is validated before any section is
imported — no best-effort partial import. Cross-service integration
(Identity, Settings, Audit, Notifications) is demonstrated entirely at
the sample module's own calling layer; Persistence and Reporting are
both deliberately not consumed.

- [WP 6.7 — Export/Import Framework Implementation](03%20Work%20Packages/WP6.7-export-import-implementation.md) — implemented directly against the already-approved architecture and Contract Review packages, including the additive Kind-routing/Format/Serialization elaborations (`ADR-0051`) and a dedicated Platform Integration Demonstration.
- ADR-0051 (Export/Import Is Orthogonal to the Internal Persistence Abstraction — Kind Routing, Format/Serialization Abstractions, and Scope Boundaries).

### Licensing

Implemented (`WP 6.6`, `ADR-0050`) — `Tempest.Core.Licensing`: what
capability is enabled, for whom, until when — exposes capability only,
never commercial policy. `ILicenseValidator` runs before the DI
container exists, deliberately a leaf with no constructor dependencies,
mirroring `PlatformVersionProvider`'s own position; an invalid license
(unreadable, malformed, missing its own required field, or expired)
aborts Host startup, Host-fatal, per `ADR-0013`. A **missing** license
file is explicitly not invalid — it resolves to a valid,
unrestricted-but-uncapable default, this platform's own normal,
open-source-friendly state, proven not to regress any of the 24
pre-existing tests that build a real `TempestHost`. `ILicenseProvider`
wraps the already-validated license and is registered via `AddInstance`
at Phase 6. License file contents are trusted at face value — no
cryptographic signature verification, a disclosed limitation.

- [WP 6.6 — Licensing Framework Implementation](03%20Work%20Packages/WP6.6-licensing-framework-implementation.md) — implemented directly against the already-approved architecture and Contract Review packages, including the missing-file-vs-broken-file Host-fatal resolution (`ADR-0050`) and a dedicated Platform Integration Demonstration.
- ADR-0050 (License Validation Is a Host-Startup, Host-Fatal Gate — Except a Missing License File, Which Is a Valid, Unrestricted Default).

### Units & Quantities

Implemented (`WP 7.1B`, `ADR-0054`) — `Tempest.Core.UnitsAndQuantities`:
`Quantity<TDimension>`/`Unit<TDimension>`, a pure, dependency-free
mathematical library for dimensioned physical quantities — this
platform's first Engineering Foundation framework with zero Platform
Service dependency and no DI registration of any kind. Seven starting
dimensions (Length, Mass, Duration, Force, Pressure, Area, Volume), each
purely multiplicative; Temperature (an affine dimension) deliberately
deferred (`TD-19`, `FCR-0034`). Arithmetic, comparison, formatting, and
parsing all require the exact same `Unit`, never an implicit conversion.

- [WP 7.1B — Units & Quantities Framework](03%20Work%20Packages/WP7.1B-units-and-quantities-framework-implementation.md) — implemented exactly as `WP 7.0C` proposed, extended with arithmetic/comparison/formatting/parsing/serialization, resolving all three `ADR-0054` questions plus one genuine finding (affine conversion) `WP7.0C Required ADR Catalogue.md` did not anticipate.
- [Phantom-Type Dimension Safety](04%20Design%20Patterns/05-phantom-type-dimension-safety.md) — the compile-time-safety pattern this framework introduces to TempestOS for the first time.
- ADR-0054 (Units & Quantities — Representation, Precision, and Registration Model).

### Materials

Implemented (`WP 7.1C`, `ADR-0055`) — `Tempest.Core.Materials`:
`MaterialCatalog`, a thin, typed index over the Engineering Data Model
(`Kind = "MaterialSpecification"`), consuming Units & Quantities
directly for every dimensioned property. Every property carries
structural provenance (`MaterialPropertyProvenance`) — source
reference, revision, validation status, confidence level, applicable
conditions, notes — never optional. No new concept guide: Materials is
presented as a worked example of the Data Model, per `WP7.0C Academy
Plan.md`'s own finding, not a new pattern in its own right.

- [WP 7.1C — Materials Framework](03%20Work%20Packages/WP7.1C-materials-framework-implementation.md) — implemented exactly as `WP 7.0C` proposed, extended with a structured, provenance-carrying property type resolving `ADR-0055`'s own reserved property-typing question, plus one genuine finding (a direct `IPersistenceStore` dependency for its own `materialId` index) `WP7.0C Required ADR Catalogue.md` did not anticipate.
- ADR-0055 (Materials Framework — Property Typing and Platform-Service Classification).

## Design Patterns

Recurring structural patterns TempestOS actually uses, explained in terms
of the real code that uses them — not a generic patterns catalogue.

- [The Registry Pattern](04%20Design%20Patterns/01-the-registry-pattern.md)
- [Descriptor and Snapshot Types](04%20Design%20Patterns/02-descriptor-and-snapshot-types.md)
- [Minimal Interface, Extension-Method Sugar](04%20Design%20Patterns/03-minimal-interface-with-extension-sugar.md)
- [Reflection-Based Discovery](04%20Design%20Patterns/04-reflection-based-discovery.md)
- [Phantom-Type Dimension Safety](04%20Design%20Patterns/05-phantom-type-dimension-safety.md)

## Engineering Governance

- [Engineering Governance](06%20Engineering%20Standards/Engineering%20Governance.md) — the project's constitution.
- [Engineering Standard: Exception Design](06%20Engineering%20Standards/01-exception-design.md)
- [Engineering Standard: Testing Strategy](06%20Engineering%20Standards/02-testing-strategy.md)
- [Working with TempestOS's Governance Registers](06%20Engineering%20Standards/03-governance-registers.md) — why the governance register suite exists, how to maintain one, common mistakes.
- `docs/architecture/Rejected Designs.md` — the Rejected Designs Log (Governance §10).
- `docs/adr/` — the ADR catalogue (Governance §5).
- `docs/governance/Governance Index.md` — the full governance register suite (`WP 4.5A`): ADR, Rejected Designs, Architecture Document, Decision, Platform Services, Module, Hosted Services, Plugin, Event, Dependency Injection, Namespace, Interface, Exception, Architectural Dependency, Risk, Technical Debt, Validation, Test, Repository Metrics, Documentation, Academy, Engineering Standards, Governance, Feature, Release, Engineering Evolution, and Traceability Matrix registers, plus `Governance Philosophy.md`, `Governance Audit Report.md`, and `Repository Maturity Report.md`.

## Case Studies

Narrative deep-dives into individually significant decisions — shorter and
more focused than a work package retrospective, longer and more narrative
than an ADR. Each has a matching ADR; not every ADR has a matching case
study.

- [Why RuntimeModule Is Immutable](05%20Case%20Studies/01-why-runtimemodule-is-immutable.md) (ADR-0001)
- [Why Lifecycle State Lives Externally](05%20Case%20Studies/02-why-lifecycle-state-lives-externally.md) (ADR-0002)
- [Why Dispose Is Always Legal](05%20Case%20Studies/03-why-dispose-is-always-legal.md) (ADR-0004) — a preserved, real architectural review exchange.
- [Why Discovery Is Isolated](05%20Case%20Studies/04-why-discovery-is-isolated.md) (ADR-0008)
- [Why Isn't Configuration Mutable?](05%20Case%20Studies/05-why-isnt-configuration-mutable.md)

## Work Package Walkthroughs

Every retrospective, in chronological order. Read the retrospective for
whatever you're about to change, before you change it.

**Runtime Foundation (v0.3.0):**

- [WP 2.1 — Module Discovery](03%20Work%20Packages/WP2.1-module-discovery.md)
- [WP 2.2 — Runtime Registration](03%20Work%20Packages/WP2.2-runtime-registration.md)
- [WP 2.3 — Runtime Lifecycle](03%20Work%20Packages/WP2.3-runtime-lifecycle.md)
- [WP 2.4 — Dependency Injection](03%20Work%20Packages/WP2.4-dependency-injection.md)
- [WP 2.5 — Configuration Framework](03%20Work%20Packages/WP2.5-configuration-framework.md)
- [WP 2.6 — Logging & Diagnostics Framework](03%20Work%20Packages/WP2.6-logging-and-diagnostics-framework.md)
- [WP 2.7 — Runtime Host Architecture Review](03%20Work%20Packages/WP2.7-runtime-host-architecture-review.md) (design phase, plus four ADRs and a real bug found in prior work)
- [WP 2.7B — Runtime Host Implementation](03%20Work%20Packages/WP2.7B-runtime-host-implementation.md)

**Platform Foundation (v0.4.0, Released 2026-07-27):**

- [WP 4.0 — Platform Contracts](03%20Work%20Packages/WP4.0-platform-contracts.md)
- [WP 4.1 — Module SDK](03%20Work%20Packages/WP4.1-module-sdk.md)
- [WP 4.2 — Plugin Manifest Architecture](03%20Work%20Packages/WP4.2-plugin-manifest-architecture.md)
- [WP 4.2A — Runtime Platform Version](03%20Work%20Packages/WP4.2A-runtime-platform-version.md)
- [WP 4.2B — Plugin Failure Classification](03%20Work%20Packages/WP4.2B-plugin-failure-classification.md)
- [WP 4.2C — Plugin Discovery Lifecycle Placement](03%20Work%20Packages/WP4.2C-plugin-discovery-lifecycle-placement.md)
- [WP 4.2 — Plugin Manifest Implementation](03%20Work%20Packages/WP4.2-plugin-manifest-implementation.md)
- [WP 4.2D — Platform Services Architecture Review](03%20Work%20Packages/WP4.2D-platform-services-architecture-review.md)
- [WP 4.3 — Sample Module Architecture](03%20Work%20Packages/WP4.3-sample-module-architecture.md)
- [WP 4.3 — Sample Module Implementation](03%20Work%20Packages/WP4.3-sample-module-implementation.md)
- [WP 4.4A — Dependency Injection for Discovered Modules](03%20Work%20Packages/WP4.4A-dependency-injection-for-discovered-modules.md)
- [WP 4.4B — ADR-0027 Implementation](03%20Work%20Packages/WP4.4B-adr-0027-implementation.md)
- [WP 4.4 — Event Bus Architecture](03%20Work%20Packages/WP4.4-event-bus-architecture.md) *(also covers WP 4.4C, which produced no code and no separate retrospective — see that document's own Background section)*
- [WP 4.4D — Event Bus Implementation](03%20Work%20Packages/WP4.4D-event-bus-implementation.md)
- [WP 4.4E — Sample Module Event Integration](03%20Work%20Packages/WP4.4E-sample-module-event-integration.md)
- [WP 4.5 — Background Services Architecture](03%20Work%20Packages/WP4.5-background-services-architecture.md)
- [WP 4.5 — Background Services Implementation](03%20Work%20Packages/WP4.5-background-services-implementation.md)
- WP 4.5A — Governance Register Baseline *(no dedicated retrospective — its own deliverable is the governance suite itself; see `docs/governance/Governance Index.md`)*
- WP 4.5B — Platform Foundation Closeout *(no dedicated retrospective — its own deliverable is `docs/releases/Platform Foundation Completion Report.md`)*

**Developer Experience (v0.5.0, Release Candidate):**

- [WP 5.0A — Navigation Framework Architecture](03%20Work%20Packages/WP5.0A-navigation-framework-architecture.md)
- [WP 5.0B — Navigation Framework Implementation](03%20Work%20Packages/WP5.0B-navigation-framework-implementation.md)
- [WP 5.0C — Shell & Composition Framework Architecture](03%20Work%20Packages/WP5.0C-shell-and-composition-framework-architecture.md)
- [WP 5.0D — Shell & Composition Framework Implementation](03%20Work%20Packages/WP5.0D-shell-and-composition-framework-implementation.md)
- [WP 5.0S — Platform Security Baseline Audit](03%20Work%20Packages/WP5.0S-platform-security-baseline-audit.md)
- [WP 5.1A — Command Framework Architecture](03%20Work%20Packages/WP5.1A-command-framework-architecture.md)
- [WP 5.1B — Command Framework Implementation](03%20Work%20Packages/WP5.1B-command-framework-implementation.md)
- [WP 5.2 — Diagnostics Improvements](03%20Work%20Packages/WP5.2-diagnostics-improvements.md)
- [WP 5.3 — Developer Experience Improvements](03%20Work%20Packages/WP5.3-developer-experience-improvements.md)
- [WP 5.4 — v0.5.0 Release Candidate & Engineering Sign-Off](03%20Work%20Packages/WP5.4-v0.5.0-release-candidate-and-engineering-sign-off.md) — the release-closing verification pass; not a feature Work Package. See also `docs/releases/v0.5.0.md`, `docs/releases/v0.5.0/CHANGELOG.md`, and `docs/releases/v0.5.0/Release Notes.md`.

`docs/releases/v0.5.0/WorkPackages.md`'s own Developer Experience phase
is complete and `v0.5.0` is released.

**Platform Services (v0.6.0, complete — CERTIFIED WITH ACCEPTED TECHNICAL DEBT):**

- [WP 6.1 — Permissions & Identity Implementation](03%20Work%20Packages/WP6.1-permissions-and-identity-implementation.md) — implemented directly against the already-approved architecture and Contract Review packages; no separate architecture-phase retrospective, per direct instruction.
- [WP 6.4 — Settings Framework Implementation](03%20Work%20Packages/WP6.4-settings-framework-implementation.md) — implemented ahead of `WP 6.0`–`WP 6.3` per `Platform Service Implementation Order.md`'s own recommendation; establishes the shared Persistence abstraction as part of its own scope.
- [WP 6.5 — Audit Framework Implementation](03%20Work%20Packages/WP6.5-audit-framework-implementation.md) — implemented per the same recommendation; reuses `WP 6.4`'s own Persistence abstraction and validates it as sufficient, without extending it speculatively.
- [WP 6.2 — Notification Framework Implementation](03%20Work%20Packages/WP6.2-notification-framework-implementation.md) — implemented per the same recommendation; builds on the existing Event Bus's own proven dispatch model rather than a second, parallel publish/subscribe implementation.
- [WP 6.0 — Reporting Framework Implementation](03%20Work%20Packages/WP6.0-reporting-framework-implementation.md) — the first of `v0.6.0`'s implemented Work Packages to match its own nominal numeric position; orthogonal to `WP 6.7` (Export/Import).
- [WP 6.3 — REST API Implementation](03%20Work%20Packages/WP6.3-rest-api-implementation.md) — this platform's first substantial dependency on a pre-built framework component (ASP.NET Core/Kestrel) and first genuinely concurrent, per-request scenario, resolved without modifying `WP 6.1`'s own already-shipped `CurrentPrincipalAccessor`.
- [WP 6.7 — Export/Import Framework Implementation](03%20Work%20Packages/WP6.7-export-import-implementation.md) — completes the orthogonality `WP 6.0` anticipated; resolves the approved contract's own multi-destination-import gap via a `Kind`-routed, dual-registered `ImportService`, reusing `WP 6.1`'s own `CurrentPrincipalAccessor` registration pattern.
- [WP 6.6 — Licensing Framework Implementation](03%20Work%20Packages/WP6.6-licensing-framework-implementation.md) — the release's final production implementation Work Package; resolves `Risk Register.md`'s own `R5` (a missing license file is a valid, unrestricted default, never Host-fatal; a broken one is), proven not to regress any of the 24 pre-existing tests that build a real `TempestHost`.
- [WP 6.8 — Platform Services Integration Review & Release Certification](03%20Work%20Packages/WP6.8-platform-services-integration-review.md) — the release's closing certification review, not a feature Work Package; fully backfilled three governance registers stale since `WP 5.2`, closed two silently-stale risks with fresh evidence, and recommended `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`.

See `PROJECT_STATUS.md` for current status and `docs/releases/v0.6.0/
WorkPackages.md` for the full, nine-Work-Package plan.

**Engineering Foundation (v0.7.0, in progress — not yet scoped):**

- [WP 7.0A — Future Capability Register & Product Vision](03%20Work%20Packages/WP7.0A-future-capability-register-and-product-vision.md) — architecture-and-governance milestone Work Package, not a feature implementation; established `VISION.md`, `docs/governance/Future Capability Register.md`, `Capability Categories.md`, and `Product Roadmap.md`. Mirrors `WP 6.8`'s own whole-review retrospective format.
- [WP 7.0B — Engineering Foundation Planning & Capability Architecture](03%20Work%20Packages/WP7.0B-engineering-foundation-planning-and-capability-architecture.md) — architecture-and-planning milestone Work Package, not a feature implementation; added `FCR-0029`–`FCR-0033` (the Engineering Foundation Programme) and eight planning deliverables analysing all 33 Future Capability Register entries. Mirrors the same whole-review retrospective format.
- [WP 7.0C — Engineering Foundation Contract Review](03%20Work%20Packages/WP7.0C-engineering-foundation-contract-review.md) — contract-review milestone Work Package, not a feature implementation; proposed public C# contracts for all five Engineering Foundation frameworks and reserved `ADR-0053`–`ADR-0057`. Mirrors the same whole-review retrospective format.
- [WP 7.1A — Engineering Data Model](03%20Work%20Packages/WP7.1A-engineering-data-model-implementation.md) — the first implementation Work Package of this phase; implements `Tempest.Core.EngineeringData` (`ADR-0053`) exactly as `WP 7.0C` proposed. Standard 13-section implementation template.
- [WP 7.1B — Units & Quantities Framework](03%20Work%20Packages/WP7.1B-units-and-quantities-framework-implementation.md) — the second implementation Work Package of this phase; implements `Tempest.Core.UnitsAndQuantities` (`ADR-0054`) exactly as `WP 7.0C` proposed, extended with arithmetic/comparison/formatting/parsing/serialization. Standard 13-section implementation template.
- [WP 7.1C — Materials Framework](03%20Work%20Packages/WP7.1C-materials-framework-implementation.md) — the third implementation Work Package of this phase; implements `Tempest.Core.Materials` (`ADR-0055`) exactly as `WP 7.0C` proposed, extended with a structured, provenance-carrying property type. Standard 13-section implementation template.

See `PROJECT_STATUS.md` for current status, `docs/governance/Future
Capability Register.md` for the authoritative future-capability list,
and `docs/releases/v0.7.0/WorkPackages.md` for candidate implementation
items (none yet approved).

## Reference Material

Documents outside `docs/academy/` maintained under the same obligation:

- `docs/releases/FOUNDATION.md` — what must never change, regardless of who's building.
- `docs/releases/v0.4.0/Architecture.md` — the v0.4.0 release's own architecture review, decisions, and reuse map.
- `docs/releases/v0.4.0/WorkPackages.md` — the authoritative, current work-package plan and status.
- `docs/releases/v0.4.0/CHANGELOG.md` — the running record of what has actually landed.
- `docs/releases/v0.4.0/Risks.md` — the release's own risk register.
- `docs/releases/v0.4.0/Platform Services Architecture Review.md` — the WP 4.2D milestone review.
- `docs/architecture/Platform Service Map.md`, `Engineering Glossary.md`, `Rejected Designs.md` — see Platform Architecture, above.
- **This Academy audit's own deliverables**: [Academy Masterclass Roadmap](Academy%20Masterclass%20Roadmap.md), [Academy Audit Report](Academy%20Audit%20Report.md).
- **The Governance Register suite's own deliverables (`WP 4.5A`)**: `docs/governance/Governance Index.md`, `Governance Philosophy.md`, `Governance Audit Report.md`, `Repository Maturity Report.md`.
- **The v0.5.0 Security Baseline's own deliverables (`WP 5.0S`)**: `docs/security/Threat Model.md`, `Security Principles.md`, `Platform Security Review v0.5.0.md`, `Security Roadmap.md`.
