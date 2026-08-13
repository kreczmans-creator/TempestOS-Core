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
- See also [Failure Isolation Across TempestOS](02%20Runtime%20Architecture/08-failure-isolation.md) (Case 5: propagate, don't isolate), [Navigation Architecture](02%20Runtime%20Architecture/09-navigation-architecture.md) (the closest structural precedent), and [Calculation Framework](02%20Runtime%20Architecture/13-calculation-framework.md) (`WP 7.1D`'s own worked comparison against this framework — a calculation is a pure function, a command is an imperative action).

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
- See also [Verification Framework](02%20Runtime%20Architecture/14-verification-framework.md) (`WP 7.1E`'s own worked comparison against this framework — Audit records who did what; Verification judges whether an engineering claim was demonstrated).

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

### Engineering Data Model

Implemented (`WP 7.1A`, `ADR-0053`) — `Tempest.Core.EngineeringData`:
`IEngineeringDocumentStore`, this platform's first data-modelling
abstraction beyond flat key-value storage — stable document identity,
explicit revision history (never overwritten in place), and typed,
directed references between documents. Built directly on
`IPersistenceStore` (`WP 6.4`), introducing no second storage mechanism.
Every other Engineering Foundation framework (Materials, Calculation,
Verification) builds on it rather than inventing its own storage shape.

- [Engineering Data Model](02%20Runtime%20Architecture/15-engineering-data-model.md) — the concept guide: the document/revision/reference pattern, why it is a layer above `IPersistenceStore` rather than a replacement for it, and common mistakes. Written by `WP 7.1F`, four Work Packages later than `WP7.0C Academy Plan.md` originally called for — a disclosed documentation-drift finding, not a silent gap.
- [WP 7.1A — Engineering Data Model](03%20Work%20Packages/WP7.1A-engineering-data-model-implementation.md) — the first implementation Work Package of this phase; implements `Tempest.Core.EngineeringData` (`ADR-0053`) exactly as `WP 7.0C` proposed. Standard 13-section implementation template.
- ADR-0053 (Engineering Data Model — Storage Substrate and Revision/Reference Persistence Model).

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

### Engineering Calculations

Implemented (`WP 7.1D`, `ADR-0056`) — `Tempest.Core.Calculations`:
`CalculationEngine`, a type-erased, Command-Framework-adjacent registry
that dispatches a registered `ICalculationDefinition<TInput, TResult>`
by Id and durably records every execution as an Engineering Data Model
document. Substantially extends `WP 7.0C`'s own illustrative contract
shape — `CalculationMetadata` (assumptions, constraints),
`CalculationContext` (intermediate results, constraint checks, material
references) — so a `CalculationRecord<TResult>` represents engineering
evidence, not merely a numerical answer. First Engineering Foundation
Work Package to include a dedicated Security Review.

- [Calculation Framework](02%20Runtime%20Architecture/13-calculation-framework.md) — the concept guide: why a calculation is not a command, the purity guarantee that makes concurrent execution safe, and common mistakes.
- [WP 7.1D — Engineering Calculation Framework](03%20Work%20Packages/WP7.1D-engineering-calculation-framework-implementation.md) — implemented exactly as `WP 7.0C` proposed, resolving both of `ADR-0056`'s own reserved questions (purity enforcement, Data Model integration) plus the `Calculate`-signature extension this Work Package's own evidentiary requirements demanded.
- ADR-0056 (Calculation Framework — Purity Enforcement and Dispatch Model).

### Verification

Implemented (`WP 7.1E`, `ADR-0057`) — `Tempest.Core.Verification`:
`VerificationService` answers "has this engineering claim been
demonstrated?" — distinct from Audit (who did what) and a Calculation
Record (what was computed). Verification history is queried through the
Engineering Data Model's own existing `LinkAsync`/`GetReferencesAsync`
mechanism, requiring no new index and no direct Persistence dependency
at all — the simplest dependency shape of any Engineering Foundation
framework. **Completes the entire Engineering Foundation programme** —
all five frameworks (`FCR-0029`–`FCR-0033`) are now Implemented.

- [Verification Framework](02%20Runtime%20Architecture/14-verification-framework.md) — the concept guide: distinguishing Verification from Audit and from a Calculation Record, three structurally similar "record what happened" types with genuinely different semantics.
- [WP 7.1E — Verification Framework](03%20Work%20Packages/WP7.1E-verification-framework-implementation.md) — implemented exactly as `WP 7.0C` proposed, resolving both of `ADR-0057`'s own reserved questions (Audit orthogonality, open method vocabulary) plus one genuine finding (verification history via the Data Model's own reference mechanism) `WP7.0C Required ADR Catalogue.md` did not anticipate.
- ADR-0057 (Verification Framework — Relationship to Audit and Method Vocabulary).

### Requirements Engine

Implemented (`WP 7.3A`, `ADR-0058`–`ADR-0061`) — `Tempest.Core.Requirements`:
`RequirementsService` is the first working Systems Engineering Foundation
capability — the canonical representation of an engineering requirement,
built entirely on the Engineering Core's own existing mechanisms. Every
requirement, collection, and group is an `IEngineeringDocument`; every
relationship (grouping, collection membership, allocation, traceability)
is a `DocumentReference`; `RequirementStatus` remains structurally
independent of `VerificationOutcome`, with zero code path connecting
them. `GetEvidenceAsync` composes Verification's own history with linked
references into a single digital-thread view, proving `WP7.2B Digital
Thread Architecture.md`'s own central claim in running code. **The first
implementation Work Package of the Systems Engineering Foundation
phase**, following the `WP 7.2A`/`WP 7.2B`/`WP 7.2C` architecture and
contract review sequence, with zero architectural rework.

- [Requirements Engine](02%20Runtime%20Architecture/16-requirements-engine.md) — the concept guide: the three-layer Requirement-as-Document pattern, and the relationship-kind/traceability vocabulary.
- [WP 7.3A — Requirements Engine](03%20Work%20Packages/WP7.3A-requirements-engine-implementation.md) — implemented exactly as `WP 7.2C` proposed, zero deviation; the sole disclosed narrowing (Guid-only allocation targets) originated at the `WP 7.2C` contract-review stage itself, not this Work Package. Resolves all four reserved ADRs (`0058`–`0061`); discloses new Technical Debt (`TD-25`).
- ADR-0058 (Requirements Engine Classification, Storage, and Relationship to the Engineering Data Model); ADR-0059 (Requirement Identity, Status, and Category Representation); ADR-0060 (Requirement Concurrency and Traceability Integrity Model); ADR-0061 (Requirements Engine — Internal vs. Calling-Layer Permission Enforcement).

### Engineering Workspace

Implemented (`WP 8.0A`–`WP 8.1C`, `ADR-0062`–`ADR-0071`) — the
Engineering Workspace: TempestOS's first user-facing engineering
product surface and, since `WP 8.1A`, the platform's own default
launch target (`ADR-0068`). Running `Tempest.App` presents the
Engineering Cockpit first (`ADR-0069`), then the five-region Workspace
shell (Areas, Project Explorer, Documents, Properties, Status Bar) —
additive to, not a replacement for, console `TempestShell`, which
remains fully intact and tested.

- [Engineering Workspace](02%20Runtime%20Architecture/17-engineering-workspace.md) — the concept guide: why the Workspace is a graphical evolution of the same composition root `TempestShell` already occupies, why its View layer is forbidden from calling a mutating service directly, and how the architecture/contracts/UX-specification/shell/navigation/cockpit sequence fits together.
- [WP 8.0A — Engineering Workspace Architecture](03%20Work%20Packages/WP8.0A-engineering-workspace-architecture.md), [WP 8.0B — Workspace Contracts](03%20Work%20Packages/WP8.0B-workspace-contracts.md), [WP 8.0C — Engineering Workspace UX Specification](03%20Work%20Packages/WP8.0C-engineering-workspace-ux-specification.md) — architecture, then frozen contracts (twelve interfaces), then the full target-experience specification — see Work Package Walkthroughs above for each, including the disclosed sequencing departure (`WP 8.0C` completed after `WP 8.1A`, not before it).
- [WP 8.1A — Workspace Shell — Implementation](03%20Work%20Packages/WP8.1A-workspace-shell-implementation.md), [WP 8.1B — Navigation & Project Explorer — Implementation](03%20Work%20Packages/WP8.1B-navigation-and-project-explorer-implementation.md), [WP 8.1C — Engineering Cockpit — Implementation](03%20Work%20Packages/WP8.1C-engineering-cockpit-implementation.md) — the shell, then Navigation/Project Explorer, then the Cockpit, each built and proven.
- ADR-0062 (additive to `TempestShell`, zero new Platform Service), ADR-0063 (Views read directly; mutations dispatch through Commands), ADR-0064 (layout/session state via `ISettingsProvider`), ADR-0065 (Digital Thread visualisation composes existing reads — later superseded by `ADR-0093`), ADR-0066/ADR-0067 (Terminal UI; Kind-keyed registration), ADR-0068 (`WorkspaceManager`/`WorkspaceShell` become the default launch target), ADR-0069/ADR-0070 (Cockpit as landing screen; Command Palette as global entry point), ADR-0071 (a `WP 8.0B` worked-example correction).
- See also [Shell & Application Composition](02%20Runtime%20Architecture/10-shell-and-application-composition.md) for the composition-root precedent this design extends, and [Desktop Application Framework](02%20Runtime%20Architecture/20-desktop-application-framework.md), below, for the second presentation layer later built over this identical Workspace.

### Engineering Domain Architecture

Implemented (`WP 8.2A`–`WP 8.2C`, `ADR-0072`–`ADR-0079`) — the
canonical Engineering Domain: TempestOS's own statement of what an
Engineering Object is, across ~49 named object families. Formalises a
pattern the four already-shipped Engineering Core frameworks
(Requirements/Verification/Materials/Calculations) had each
independently converged on, rather than inventing a new model.

- [Engineering Domain Architecture](02%20Runtime%20Architecture/18-engineering-domain-architecture.md) — the concept guide: why four separately-designed frameworks converging on the same shape became binding platform architecture, and how the architecture/contracts/implementation phases relate.
- [WP 8.2A — Engineering Domain Architecture](03%20Work%20Packages/WP8.2A-engineering-domain-architecture.md) — the architecture-only phase; five catalogue entries reconciled as already-Implemented, the remaining forty-plus honestly marked `Conceptual`.
- [WP 8.2B — Engineering Domain Contracts](03%20Work%20Packages/WP8.2B-engineering-domain-contracts.md) — the complete public contract, contracts only, zero `src/`/`tests/` change.
- [WP 8.2C — Engineering Domain Implementation](03%20Work%20Packages/WP8.2C-engineering-domain-implementation.md) — 38 of ~49 canonical objects given a real, tested concrete class over `EngineeringObjectBase`.
- ADR-0072 (every canonical object is an `IEngineeringDocumentStore`-backed `Kind`), ADR-0073 (relationships are open-string `DocumentReference`s, never a closed enum), ADR-0074 (lifecycle status is a common canonical vocabulary, specialised per family), ADR-0075 (composition, not inheritance — small facet interfaces), ADR-0076 (one generic `IEngineeringRelationship` interface, not seventeen), ADR-0077 (shared services reuse the existing document store in production), ADR-0078 (the five already-Implemented Kinds get no competing concrete class), ADR-0079 (two generic factory types, not dozens hand-written).
- See also [Engineering Data Model](02%20Runtime%20Architecture/15-engineering-data-model.md) for the storage substrate this architecture builds on, and `docs/architecture/Classification & Relationship Vocabulary Safety Net Architecture.md` (`WP 12.1A`/`WP 12.1B`, `ADR-0105`) for the later compile-time-adjacent safety net closing this architecture's own open-string vocabulary gap.

### Desktop Application

Realised (`WP 10.0A`–`WP 10.0B`, `ADR-0092`–`ADR-0094`) — the
Engineering Workspace's presentation moves from a Terminal UI to a
graphical desktop application, `Tempest.Desktop` (Avalonia 11.2.3),
built over the unchanged `v0.9.0` terminal-era Workspace contract
layer with zero contract change.

- [User Experience & Desktop Application](02%20Runtime%20Architecture/19-user-experience-and-desktop-application.md) — the concept guide, written at the architecture stage: why the presentation paradigm changes a second time (console → terminal, `v0.8.0`; terminal → graphical, `v0.10.0`), and what stays exactly the same underneath both changes.
- [Desktop Application Framework](02%20Runtime%20Architecture/20-desktop-application-framework.md) — the concept guide, written at implementation stage: how `EngineeringWorkspaceComposer` lets two presentation layers load the identical six-discipline Workspace without copying `Program.cs`'s own composition sequence.
- [WP 10.0A — User Experience Architecture](03%20Work%20Packages/WP10.0A-user-experience-architecture.md) — this project's first two ADR supersessions (`ADR-0092` supersedes `ADR-0066`; `ADR-0093` supersedes `ADR-0065`), architecture only.
- [WP 10.0B — Desktop Application Framework — Implementation](03%20Work%20Packages/WP10.0B-desktop-application-framework.md) — `ADR-0094` selects Avalonia; `Tempest.Desktop` built, zero engineering functionality changed.
- ADR-0092 (graphical desktop paradigm, superseding `ADR-0066`), ADR-0093 (progressively-expandable node-link Digital Thread graph, superseding `ADR-0065`), ADR-0094 (Avalonia 11.2.3, a real headless testing mode).
- See also [Navigation Architecture](02%20Runtime%20Architecture/09-navigation-architecture.md) and [Shell & Application Composition](02%20Runtime%20Architecture/10-shell-and-application-composition.md) for the platform/application rendering boundary this second presentation layer proves out under real, varied use for the first time.

### Desktop Application — Workspace & Engineering Surfaces

The v0.10.0 Work Packages that built and hardened `Tempest.Desktop`'s
own real engineering surfaces over the foundation above: the graphical
Cockpit, a root-cause hardening pass, a modernised shell, docking, the
Object Editor Framework, the Ribbon, and the Digital Thread graph —
each additive to the twelve frozen `WP8.0B` Workspace contracts, with
at most one precedented, additive extension per Work Package.

- [Engineering Cockpit — Graphical Implementation](02%20Runtime%20Architecture/21-engineering-cockpit-graphical-implementation.md) / [WP 10.1A — Engineering Cockpit Implementation](03%20Work%20Packages/WP10.1A-engineering-cockpit-implementation.md) — systematically audits every disclosed Cockpit placeholder since `WP 8.1C`, upgrading six to real data; realises `ADR-0069` literally via `DocumentAreaView.SetHomeTab`.
- [Runtime Host — Restart Stability & Readiness Signalling](02%20Runtime%20Architecture/22-runtime-host-restart-stability.md) / [WP 10.1B — Runtime Host & Module Discovery Hardening](03%20Work%20Packages/WP10.1B-runtime-host-and-module-discovery-hardening.md) — genuinely resolves `TD-26` and fully root-causes `TD-37`, both previously only mitigated one layer up.
- [Workspace Modernisation — Real Dispatch Behind a Modern Shell](02%20Runtime%20Architecture/23-workspace-modernisation.md) / [WP 10.2A — Workspace Modernisation](03%20Work%20Packages/WP10.2A-workspace-modernisation.md) — `ADR-0096` adds Rename/Delete dispatch to `IWorkspaceManager`, closing a `WP 9.0A`-disclosed gap.
- [Docking & Workspace Layouts](02%20Runtime%20Architecture/24-docking-and-workspace-layouts.md) / [WP 10.2B — Docking & Workspace Layouts](03%20Work%20Packages/WP10.2B-docking-and-workspace-layouts.md) — realises `WorkspaceDockPosition.Bottom` for the first time; Collapse and Auto-Hide as two genuinely distinct mechanisms sharing one affordance; zero contract change of any kind.
- [Engineering Object Editors](02%20Runtime%20Architecture/25-engineering-object-editors.md) / [WP 10.3A — Engineering Object Editors](03%20Work%20Packages/WP10.3A-engineering-object-editors.md) — `ObjectEditorView`, one generic engine over `EngineeringObjectBase` (`ADR-0075`) rather than six bespoke editors; `ADR-0097` adds Revise dispatch.
- [Ribbon & Command Experience](02%20Runtime%20Architecture/26-ribbon-and-command-experience.md) / [WP 10.3B — Ribbon, Toolbar & Command Experience](03%20Work%20Packages/WP10.3B-ribbon-and-command-experience.md) — a disclosed reversal of an earlier "No ribbon" scope boundary; reuses `ADR-0096`/`ADR-0097`'s dispatch verbs rather than a second command framework.
- [Digital Thread Visualisation](02%20Runtime%20Architecture/27-digital-thread-visualisation.md) / [WP 10.4A — Digital Thread Visualisation](03%20Work%20Packages/WP10.4A-digital-thread-visualisation.md) — realises `ADR-0093` as a real, interactive, progressively-expandable node-link graph, never a precomputed traversal.
- ADR-0096 (`WP 10.2A`, Rename/Delete dispatch), ADR-0097 (`WP 10.3A`, Revise dispatch) — the two additive `IWorkspaceManager` extensions this whole arc relies on. `TD-26`/`TD-37` (closed, `WP 10.1B`); `TD-32` (closed for the graph view, `WP 10.4A`); `TD-39`/`TD-40` (opened here, closed by the next section).
- See also [Command Framework](02%20Runtime%20Architecture/11-command-framework.md) for `ICommandRegistry`/`ICommandDispatcher`, the mechanism every Ribbon/Object-Editor dispatch verb reuses rather than replaces.

### Desktop Application — Professional & Commercial Experience

The v0.10.0 Work Packages that took `Tempest.Desktop` from a working
shell to a professional, commercially-presentable application: a
theme-reactive visual/UX polish pass, a real workflow layer (dialogs,
window persistence, notifications), a productivity layer (Undo/Redo,
Macros, an External Controller abstraction), and a closing, audit-first
verification of every prior claim.

- [Workspace Visual Polish & Engineering User Experience](02%20Runtime%20Architecture/28-workspace-visual-polish.md) / [WP 10.5A — Workspace Visual Polish & Engineering User Experience](03%20Work%20Packages/WP10.5A-workspace-visual-polish.md) — `ApplicationPalette`/`ThemeReactiveBrush`, the platform's own first genuinely theme-reactive brushes; closes `TD-39`/`TD-40`.
- [Desktop Workflow & Professional Interaction](02%20Runtime%20Architecture/29-desktop-workflow-and-professional-interaction.md) / [WP 10.5B — Desktop Workflow & Professional Interaction](03%20Work%20Packages/WP10.5B-desktop-workflow-and-professional-interaction.md) — a unified, four-dialog Dialog Framework with no shared base class; the Notification Framework's own first real Desktop consumer.
- [Command Execution & Productivity Experience](02%20Runtime%20Architecture/30-command-execution-and-productivity-experience.md) / [WP 10.6A — Command Execution & Productivity Experience](03%20Work%20Packages/WP10.6A-command-execution-and-productivity-experience.md) — Undo/Redo as a Desktop-local delegate stack (`ADR-0098`); a Macro as a registered Command (`ADR-0099`); External Controller integration with no vendor SDK (`ADR-0100`).
- [Commercial User Experience & Application Completion](02%20Runtime%20Architecture/31-commercial-user-experience-and-application-completion.md) / [WP 10.5C — Commercial User Experience & Application Completion](03%20Work%20Packages/WP10.5C-commercial-user-experience-and-application-completion.md) — a required-first runtime UX audit of every `WP 10.0B`–`WP 10.5B` claim; `DisciplineColors`, the platform's own fifth colour-mapping class.
- ADR-0098 (Undo/Redo, a delegate stack not a new Command contract), ADR-0099 (a Macro is a registered Command, not a second execution path), ADR-0100 (External Controller integration — no vendor SDK). Disclosed numbering note: `WP 10.5C` was completed after `WP 10.6A` despite its lower number, mirroring `WP 9.3A`'s own precedent — see Work Package Walkthroughs, above.
- See also [Diagnostics & Composite Logging](02%20Runtime%20Architecture/12-diagnostics-and-composite-logging.md) for the `IDiagnosticsProvider` reads the Cockpit/Status Bar/Ribbon all consume, and Notifications, above, for the Event-Bus-derived framework `WP 10.5B` gives its first real Desktop consumer.

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
- [Engineering Standard: Continuous Integration](06%20Engineering%20Standards/04-continuous-integration.md) — CI philosophy, the `.github/workflows/ci.yml` build pipeline, release verification, and the engineering workflow around it (`WP 11.1A`).
- [Engineering Standard: Release Engineering](06%20Engineering%20Standards/05-release-engineering.md) — branching strategy, pull request workflow, release process, versioning policy, and the emergency hotfix process (`WP 11.1B`).
- [Engineering Standard: Governance Automation](06%20Engineering%20Standards/06-governance-automation.md) — the automated Governance Health-Check Tool (`FCR-0005`), what it validates, and what it deliberately does not fix (`WP 11.2A`).
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

**Engineering Foundation (v0.7.0, Released 2026-08-03 — APPROVED):**

- [WP 7.0A — Future Capability Register & Product Vision](03%20Work%20Packages/WP7.0A-future-capability-register-and-product-vision.md) — architecture-and-governance milestone Work Package, not a feature implementation; established `VISION.md`, `docs/governance/Future Capability Register.md`, `Capability Categories.md`, and `Product Roadmap.md`. Mirrors `WP 6.8`'s own whole-review retrospective format.
- [WP 7.0B — Engineering Foundation Planning & Capability Architecture](03%20Work%20Packages/WP7.0B-engineering-foundation-planning-and-capability-architecture.md) — architecture-and-planning milestone Work Package, not a feature implementation; added `FCR-0029`–`FCR-0033` (the Engineering Foundation Programme) and eight planning deliverables analysing all 33 Future Capability Register entries. Mirrors the same whole-review retrospective format.
- [WP 7.0C — Engineering Foundation Contract Review](03%20Work%20Packages/WP7.0C-engineering-foundation-contract-review.md) — contract-review milestone Work Package, not a feature implementation; proposed public C# contracts for all five Engineering Foundation frameworks and reserved `ADR-0053`–`ADR-0057`. Mirrors the same whole-review retrospective format.
- [WP 7.1A — Engineering Data Model](03%20Work%20Packages/WP7.1A-engineering-data-model-implementation.md) — the first implementation Work Package of this phase; implements `Tempest.Core.EngineeringData` (`ADR-0053`) exactly as `WP 7.0C` proposed. Standard 13-section implementation template.
- [WP 7.1B — Units & Quantities Framework](03%20Work%20Packages/WP7.1B-units-and-quantities-framework-implementation.md) — the second implementation Work Package of this phase; implements `Tempest.Core.UnitsAndQuantities` (`ADR-0054`) exactly as `WP 7.0C` proposed, extended with arithmetic/comparison/formatting/parsing/serialization. Standard 13-section implementation template.
- [WP 7.1C — Materials Framework](03%20Work%20Packages/WP7.1C-materials-framework-implementation.md) — the third implementation Work Package of this phase; implements `Tempest.Core.Materials` (`ADR-0055`) exactly as `WP 7.0C` proposed, extended with a structured, provenance-carrying property type. Standard 13-section implementation template.
- [WP 7.1D — Engineering Calculation Framework](03%20Work%20Packages/WP7.1D-engineering-calculation-framework-implementation.md) — the fourth implementation Work Package of this phase, and the first to include a dedicated Security Review; implements `Tempest.Core.Calculations` (`ADR-0056`) exactly as `WP 7.0C` proposed, substantially extended to satisfy an "engineering evidence, not merely a numerical answer" requirement. Standard 13-section implementation template.
- [WP 7.1E — Verification Framework](03%20Work%20Packages/WP7.1E-verification-framework-implementation.md) — the fifth and final implementation Work Package of this phase, completing the entire Engineering Foundation programme; implements `Tempest.Core.Verification` (`ADR-0057`) exactly as `WP 7.0C` proposed, extended with a structured `VerificationContext`. Standard 13-section implementation template.
- [WP 7.1F — Engineering Core Integration Review & Certification](03%20Work%20Packages/WP7.1F-engineering-core-integration-review-and-certification.md) — closing certification review of the complete Engineering Core, not a feature implementation; mirrors `WP 6.8`'s own whole-release retrospective format. Found and closed a repeat of `WP 6.8`'s own exact governance-drift pattern (`Interface Register.md`/`Dependency Injection Register.md`/`Module Register.md` stale since `WP 6.8`) and wrote the Engineering Data Model's own missing concept guide.
- [WP 7.2A — Strategic Roadmap Selection & Programme Architecture](03%20Work%20Packages/WP7.2A-strategic-roadmap-selection-and-programme-architecture.md) — architecture, governance, and roadmap-planning milestone Work Package, not a feature implementation; mirrors `WP 7.0A`/`WP 7.0B`'s own whole-review format. Evaluated seven candidate programmes against eleven criteria; recommended Programme A (Requirements & Verification Platform, `FCR-0027`) over Programme F (Platform Hardening, sequenced second) and five programmes with no identified capability.
- [WP 7.2B — Requirements & Verification Platform Architecture](03%20Work%20Packages/WP7.2B-requirements-and-verification-platform-architecture.md) — architecture-only milestone Work Package continuing the Engineering Foundation into Systems Engineering, not a feature implementation; mirrors `WP7.0C Engineering Foundation Contracts.md`'s own format. Designed the complete Requirements & Verification Platform architecture — twelve domain concepts, a three-layer Engineering Core/Systems Engineering Foundation/Engineering Discipline Modules model, a digital thread design, and an industry-neutral standards mapping. Reserved `ADR-0058`–`ADR-0060`.
- [WP 7.2C — Requirements & Verification Platform Contract Review](03%20Work%20Packages/WP7.2C-requirements-and-verification-platform-contract-review.md) — contract-review-only milestone Work Package, not a feature implementation; mirrors `WP7.0C Engineering Foundation Contracts.md`'s own format, extended to seventeen questions per concept. Defined full proposed C# contracts for all thirteen named domain concepts, a Requirement Lifecycle Model, a Relationship Model, a Traceability Contract, and a Verification Integration Contract. Reserved `ADR-0061`.
- [WP 7.3A — Requirements Engine](03%20Work%20Packages/WP7.3A-requirements-engine-implementation.md) — the first implementation Work Package of the Systems Engineering Foundation phase; implements `Tempest.Core.Requirements` (`ADR-0058`–`ADR-0061`) exactly as `WP 7.2C` proposed, zero architectural deviation. Standard 13-section implementation template, third Work Package overall with a dedicated Security Review.
- [WP 7.4.0 — Release Preparation & Product Baseline](03%20Work%20Packages/WP7.4.0-release-preparation-and-product-baseline.md) — the release-closing verification pass across seventeen named areas (build, test, documentation, Academy, governance registers, ADR consistency, version consistency, and more); found and corrected five governance/documentation staleness findings (the Documentation Register's own stale Directory Map; the Governance Register's own Compliance Matrix, stale since `WP 6.8`, missing all twelve `v0.7.0` Work Packages). Disclosed, but did not fix: the Platform Services Register/Map gap `WP 7.3A` found. Recommendation: `v0.7.0` APPROVED.

`docs/releases/v0.7.0/WorkPackages.md`'s own Engineering Foundation
phase is complete and `v0.7.0` is released (tag `v0.7.0`, "Engineering
Foundation," APPROVED).

**Engineering Workspace (v0.8.0, Released 2026-08-04 — APPROVED):**

- [WP 8.0A — Engineering Workspace Architecture](03%20Work%20Packages/WP8.0A-engineering-workspace-architecture.md) — designs the complete Engineering Workspace across twelve named areas, additive to console `TempestShell` with zero new Platform Service (`ADR-0062`); Views read Engineering Core/Platform services directly, mutations dispatch through the existing Command Framework (`ADR-0063`); layout/session state persists via the existing `ISettingsProvider` (`ADR-0064`); Digital Thread visualisation composes existing reads (`ADR-0065`). `ADR-0066`/`ADR-0067` reserved, resolved by `WP 8.0B` below.
- [WP 8.0B — Workspace Contracts](03%20Work%20Packages/WP8.0B-workspace-contracts.md) — contract-review only; defines the complete public contract for all twelve named Workspace interfaces, each fully specified in proposed C#. Resolves both reserved ADRs: `ADR-0066` (Terminal UI, not a graphical desktop framework, at this stage) and `ADR-0067` (Kind-keyed registration for both object views and Project Explorer nodes, mirroring `IReportDefinition`/`IReportRenderer<T>`).
- [WP 8.1A — Workspace Shell — Implementation](03%20Work%20Packages/WP8.1A-workspace-shell-implementation.md) — `v0.8.0`'s first implementation Work Package, completed third overall, ahead of `WP 8.0C`'s own UX Specification below — a disclosed sequencing departure. Implements all twelve `WP 8.0B`-approved contracts with zero signature change; `WorkspaceManager`/`WorkspaceShell` become `Tempest.App`'s own default launch target (`ADR-0068`), console `TempestShell` remaining intact but no longer default.
- [WP 8.0C — Engineering Workspace UX Specification](03%20Work%20Packages/WP8.0C-engineering-workspace-ux-specification.md) — completed fourth, after `WP 8.1A`'s own shell implementation rather than before it — a genuine, disclosed sequencing departure from the architecture-then-contracts-then-implementation order. Fully specifies the target Workspace experience across 28 named areas as nine deliverables. Two new ADRs: `ADR-0069` (the Engineering Cockpit is the Workspace's own default landing screen) and `ADR-0070` (the Command Palette is a first-class, global entry point).
- [WP 8.1B — Navigation & Project Explorer — Implementation](03%20Work%20Packages/WP8.1B-navigation-and-project-explorer-implementation.md) — implements the Navigation system (history, breadcrumbs, Areas panel) and the Project Explorer against a real, fixed, fictional object tree, proving `ADR-0067`'s own Kind-keyed extensibility mechanism end to end. One new ADR (`ADR-0071`), correcting a worked example inside `ADR-0067` that a discovered module cannot actually reach `IWorkspaceManager` directly.
- [WP 8.1C — Engineering Cockpit — Implementation](03%20Work%20Packages/WP8.1C-engineering-cockpit-implementation.md) — implements the Engineering Cockpit as the Workspace's own default landing experience (`ADR-0069`), answering four questions on every visit (where am I, what needs attention, is the project healthy, what should I do next); every card with a real Workspace-service backing is a live read, every other card an honestly disclosed placeholder. Zero new ADRs.
- [WP 8.2A — Engineering Domain Architecture](03%20Work%20Packages/WP8.2A-engineering-domain-architecture.md) — defines the canonical Engineering Domain across ~49 named objects in thirteen families, formalising a pattern the four already-shipped Engineering Core frameworks (Requirements/Verification/Materials/Calculations) had each independently converged on. Three new ADRs: `ADR-0072` (every canonical object is an `IEngineeringDocumentStore`-backed `Kind`), `ADR-0073` (relationships are open-string `DocumentReference`s, never a closed enum), `ADR-0074` (lifecycle status is a common canonical vocabulary, specialised per object family).
- [WP 8.2B — Engineering Domain Contracts](03%20Work%20Packages/WP8.2B-engineering-domain-contracts.md) — converts `WP 8.2A`'s architecture into the complete public contract: `IEngineeringObject` plus ten facet interfaces, composed never inherited, per object (`ADR-0075`); one generic `IEngineeringRelationship` interface realising all seventeen named relationship categories without reopening `ADR-0073` (`ADR-0076`). Zero `src/`/`tests/` change of any kind.
- [WP 8.2C — Engineering Domain Implementation](03%20Work%20Packages/WP8.2C-engineering-domain-implementation.md) — compiles all 21 `WP 8.2B` contract files (83 interfaces/enums/records) and gives 38 of the ~49 canonical objects a real, tested concrete class over a shared `EngineeringObjectBase`. Three new ADRs: `ADR-0077` (shared services reuse the existing `IEngineeringDocumentStore` in production, not a second document store), `ADR-0078` (the five already-Implemented Kinds receive no competing concrete class), `ADR-0079` (object/relationship factories are two generic types, not dozens of hand-written ones).
- [WP 8.9.0 — Release Preparation & Product Baseline](03%20Work%20Packages/WP8.9.0-release-preparation-and-product-baseline.md) — the release's tenth and closing Work Package: a release-readiness review across all nine prior `v0.8.0` Work Packages. Found and corrected one genuine arithmetic error (39 → 38 concrete canonical object classes). Disclosed, not fixed: the four-Engineering-Foundation-framework Platform Service Register/Map gap (first found by `WP 7.3A`) and zero dedicated Security Reviews this release. Recommendation: `v0.8.0` APPROVED.

`docs/releases/v0.8.0/WorkPackages.md`'s own Engineering Workspace
phase is complete and `v0.8.0` is released (tag `v0.8.0`, "Engineering
Workspace," APPROVED).

**Mechanical Foundation (v0.9.0, Released 2026-08-07 — APPROVED):**

- [WP 9.0A — Mechanical Product Structure](03%20Work%20Packages/WP9.0A-mechanical-product-structure.md) — the first Work Package to wire a real Engineering Discipline into the real Engineering Workspace. Three new additive Domain facets (`IRenamable`, `IHasParent`, `IDeletable`) composed into the five Product Structure Kinds, mirroring `ADR-0075`'s composition model (`ADR-0080`); `MoveAsync` records a new relationship link rather than mutating the frozen `WP8.2C` snapshot fields (`ADR-0081`); the Property Inspector gains a third Kind-keyed provider category, `IPropertyFacetProvider` (`ADR-0082`). Discloses `TD-26` (`WorkspaceManager.StartAsync` does not await Runtime Host module initialisation).
- [WP 9.0B — Product Configuration & BOM Management](03%20Work%20Packages/WP9.0B-product-configuration-and-bom-management.md) — Bill of Materials and Configuration Management over the Mechanical Product Structure `WP 9.0A` delivered. One new additive Domain facet, `IHasBomLine` (`ADR-0083`), the fourth in the `ADR-0080` composition family; first real use of `ValidationRuleSet.Register` and `IReferenceIntegrityChecker.CheckBaselineMembersAsync`; a real Compare Baselines diff, not a placeholder.
- [WP 9.1A — Requirements Management Workspace](03%20Work%20Packages/WP9.1A-requirements-management-workspace.md) — the complete Requirements Management experience over the already-real `WP 7.3A` Requirements Framework. Seven new additive `IRequirementsService` methods plus a `RequirementGroupDto` parent-resolution fix (`ADR-0084`); multi-selection added to `ISelectionService`/`IWorkspaceContext` (`ADR-0085`), resolving `FCR-0039`. First sample module ever to depend on another sample module's own instance.
- [WP 9.2A — Engineering Calculations Workspace](03%20Work%20Packages/WP9.2A-engineering-calculations-workspace.md) — the complete Engineering Calculations experience over the already-real `WP 7.1D` Calculation Framework; the first real-discipline Work Package to require zero Domain-layer changes. `CalculationTemplateRegistry` (`ADR-0086`) is a JSON-marshalling type-erasure adapter connecting the generic `ICalculationEngine` to one non-generic Execute/Recalculate command; five status verbs realised as descriptive `CommandDescriptor`s over `IHasLifecycle.TransitionAsync` (`ADR-0087`), disclosing `TD-30` (no `IApprovalGate`/`IApproval` implementation exists anywhere in the platform).
- [WP 9.4A — Engineering Documents Workspace](03%20Work%20Packages/WP9.4A-engineering-documents-workspace.md) — completed fifth, before `WP 9.3A` below despite the later number, closing a numbering gap `WP 9.2A` left open. The complete Documents experience over `Document`/`Drawing`/`CadModel`; six of eight named Document types realised as plain `"Document"` objects distinguished only by the existing `Classification` facet (`ADR-0088`), never five new concrete Domain classes. Discloses `TD-31` (no file/URL attachment storage service exists anywhere in the platform).
- [WP 9.3A — Verification Management Workspace](03%20Work%20Packages/WP9.3A-verification-management-workspace.md) — completed sixth, after `WP 9.4A` above despite the earlier number, a disclosed out-of-sequence commissioning (mirrored later by `WP 10.5C`). The complete Verification Management experience over the already-real `WP 7.1E` Framework; "Verification Plan" and "Verification Activity" are one Domain Kind, distinguished only by `LifecycleState` (`ADR-0090`); `RecordVerificationResultCommand` realises Execute/Record Result/Attach Evidence together (`ADR-0089`). Discloses `TD-32`, a genuine implementation-time finding caught by nine failing tests before commit.
- [WP 9.5A — Manufacturing Workspace](03%20Work%20Packages/WP9.5A-manufacturing-workspace.md) — completed seventh, skipping `WP 9.6A`–`WP 9.8A` entirely per its own closing instruction. The complete Manufacturing Workspace over `ManufacturingOperation`/`WorkInstruction`/`Inspection` (`WP 8.2C`); the first genuine cross-Work-Package read-side reuse — Work Instructions/Inspections register Documents'/Verification's own already-shipped Property Facet Provider and Workspace View types directly, under a different Kind string. One new ADR (`ADR-0091`): a `Classification`-tagged `ManufacturingOperation` used as a Routing's own structural container.
- [WP 9.9.0 — Release Preparation & Product Baseline](03%20Work%20Packages/WP9.9.0-release-preparation-and-product-baseline.md) — the first release-readiness pass, covering all seven `v0.9.0` implementation Work Packages (`WP 9.0A`–`WP 9.5A`); recommends `v0.9.0` APPROVED. Reconfirms two governance-completeness findings open at the time — the four-Engineering-Foundation-framework Platform Service Register/Map gap (closed next, by `WP 9.8B`) and the "32 vs. 35 governance documents" count drift (left open).
- [WP 9.8B — Platform Service Register Reconciliation](03%20Work%20Packages/WP9.8B-platform-service-register-reconciliation.md) — a dedicated, narrow-scope governance Work Package, commissioned after `WP 9.9.0`'s own first pass above despite carrying an earlier number, closing the single most persistent disclosed governance finding in the project's history: the Platform Service Register/Map gap first found by `WP 7.3A`. Verification and documentation only: backfills four rows (Engineering Data Model, Materials, Engineering Calculations, Verification), and corrects two further, previously-undisclosed arithmetic/staleness findings in the same registers.
- [WP 9.9.0 — Release Preparation & Product Baseline (Second Pass)](03%20Work%20Packages/WP9.9.0-release-preparation-and-product-baseline-second-pass.md) — a second, independent release-readiness pass, the first full "verify, remediate, re-verify" sequence this project performed; independently re-derives and reconfirms the Platform Service Register/Map gap closed. New finding: `TD-34`, a `CompositeLogSinkTests` console-capture race, confirmed non-reproducible in isolation, not Release Blocking. Recommendation, reconfirmed: `v0.9.0` APPROVED.

`docs/releases/v0.9.0/WorkPackages.md`'s own Mechanical Foundation
phase is complete and `v0.9.0` is released (tag `v0.9.0`, "Mechanical
Foundation," APPROVED).

**User Experience & Desktop Application (v0.10.0, Released 2026-08-11 — APPROVED):**

- [WP 10.0A — User Experience Architecture](03%20Work%20Packages/WP10.0A-user-experience-architecture.md) — architecture and specification only. This project's first two ADR supersessions: `ADR-0092` supersedes `ADR-0066` — the Workspace's presentation moves from a Terminal UI to a graphical desktop application; `ADR-0093` supersedes `ADR-0065` — Digital Thread visualisation moves from a flat list to a progressively-expandable node-link graph. Zero Workspace contract change required for either decision, independently re-verified against every contract directly.
- [WP 10.0B — Desktop Application Framework — Implementation](03%20Work%20Packages/WP10.0B-desktop-application-framework.md) — builds `Tempest.Desktop`, TempestOS's first graphical desktop application, over the unchanged `WP 10.0A` architecture and the unchanged six-discipline Workspace. `ADR-0094` selects Avalonia 11.2.3. `EngineeringWorkspaceComposer`, extracted from console `Program.cs`, is the single, shared composition sequence both presentation layers call. Discloses `TD-35` (found and fixed) and directly, reproducibly hits `TD-26` for the first time.
- [WP 10.1A — Engineering Cockpit Implementation](03%20Work%20Packages/WP10.1A-engineering-cockpit-implementation.md) — builds the complete graphical Engineering Cockpit, auditing every disclosed Cockpit placeholder since `WP 8.1C` and upgrading six to real data (`OpenDecisions`/`BlockedItems`/`Health`/`RiskSummary`/`DigitalThreadSummary`/`UpcomingMilestones`). Realises `ADR-0069` literally for the first time via `DocumentAreaView.SetHomeTab`. Finds `TD-37`, a genuine, pre-existing sample-module registration defect affecting four `Tempest.Samples` modules — root-caused and fixed by `WP 10.1B`, next.
- [WP 10.1B — Runtime Host & Module Discovery Hardening](03%20Work%20Packages/WP10.1B-runtime-host-and-module-discovery-hardening.md) — `v0.10.0`'s first pure hardening/root-cause pass. Genuinely resolves `TD-26` (`WorkspaceManager.StartAsync` now waits for `HostState.Running`) and fully root-causes `TD-37` (a durable, cross-launch uniqueness index colliding with fixed sample-module identifiers on a second real launch, never a double-invocation as previously speculated) — both previously only mitigated one layer up. Discloses `TD-38`, deliberately unfixed.
- [WP 10.2A — Workspace Modernisation](03%20Work%20Packages/WP10.2A-workspace-modernisation.md) — transforms the working-but-minimal shell into a modern, professional UI across six named areas. `ADR-0096` gives `IWorkspaceManager` five new additive members (Rename/Delete factories and dispatch), closing a `WP 9.0A`-disclosed gap ("a future context-menu action"). Real context menus, multi-select, inline rename, text filtering, and an honest Validation-summary placeholder.
- [WP 10.2B — Docking & Workspace Layouts](03%20Work%20Packages/WP10.2B-docking-and-workspace-layouts.md) — a professional dockable workspace built entirely without touching any of the twelve frozen `WP8.0B` Workspace contracts. Realises `WorkspaceDockPosition.Bottom` for the first time via a new `OutputPanel`; Collapse and Auto-Hide are one shared visual affordance, two genuinely distinct behaviours. Discloses `TD-39` (a fixed, non-theme-reactive overlay background, shared with `CommandPaletteOverlay`).
- [WP 10.3A — Engineering Object Editors](03%20Work%20Packages/WP10.3A-engineering-object-editors.md) — replaces the Document Area's own three-line placeholder with `ObjectEditorView`, one generic editor engine across all six disciplines, reading the identical `EngineeringObjectBase` facet set (`ADR-0075`). `ADR-0097` adds a third, precedented `IWorkspaceManager` extension (Revise factory/dispatch); closes Mechanical's own missing Revise command. Discloses `TD-40` and `FCR-0068`.
- [WP 10.3B — Ribbon, Toolbar & Command Experience](03%20Work%20Packages/WP10.3B-ribbon-and-command-experience.md) — a genuine, disclosed reversal of direction ("No ribbon" in every prior `v0.10.0` Work Package); builds a real, tabbed Engineering Ribbon over `ICommandRegistry.Items`, discovering zero `CommandDescriptor` anywhere in this platform's history had ever set `CreateDefault`, reusing the `ADR-0096`/`ADR-0097` Kind-keyed dispatch verbs instead of a second command framework. Finds and fixes a six-Work-Package-latent Command Palette defect.
- [WP 10.4A — Digital Thread Visualisation](03%20Work%20Packages/WP10.4A-digital-thread-visualisation.md) — a second genuine, disclosed reversal ("No Digital Thread graph"), this time already governed by `ADR-0093`. `DigitalThreadGraphModel` is an interactive, progressively-expandable node-link graph, reusing `ObjectEditorView`'s own bidirectional relationship read over `IEvidenceComposer`, independently assessed superior. Closes `TD-32` for this view.
- [WP 10.5A — Workspace Visual Polish & Engineering User Experience](03%20Work%20Packages/WP10.5A-workspace-visual-polish.md) — a professional visual/UX polish pass; `ApplicationPalette`/`ThemeReactiveBrush` are the platform's own first genuinely theme-reactive custom brushes, closing `TD-39`. Four new reusable controls (`ToastHost`, `BusyOverlay`, `ConfirmationDialog`, `EmptyStateView`); closes `TD-40`. Finds and fixes a same-session regression in `WP 10.4A`'s own `DigitalThreadGraphView`.
- [WP 10.5B — Desktop Workflow & Professional Interaction](03%20Work%20Packages/WP10.5B-desktop-workflow-and-professional-interaction.md) — a unified, four-dialog Dialog Framework, real window-geometry persistence with a graceful-shutdown gate, a real end-to-end Mechanical object-creation workflow, and the Notification Framework's own first real Desktop consumer (`PlatformNotificationToastBridge`). Two genuine implementation-time findings, both fixed before commit.
- [WP 10.6A — Command Execution & Productivity Experience](03%20Work%20Packages/WP10.6A-command-execution-and-productivity-experience.md) — the professional engineering workflow layer: a real Undo/Redo architecture as a Desktop-local delegate stack, not a new Command contract (`ADR-0098`); a Macro is a registered Command, not a second execution path (`ADR-0099`); External Controller integration via one real Keyboard provider and one test-only stub, no vendor SDK (`ADR-0100`).
- [WP 10.5C — Commercial User Experience & Application Completion](03%20Work%20Packages/WP10.5C-commercial-user-experience-and-application-completion.md) — completed thirteenth, after `WP 10.6A` above despite the earlier number (disclosed numbering note, mirroring `WP 9.3A`'s own precedent). A required-first runtime UX audit of every `WP 10.0B`–`WP 10.5B` claimed feature, finding no genuinely unreachable feature anywhere; two genuine theme-reactivity defects found and fixed; `DisciplineColors`, the platform's own fifth colour-mapping class, applied to real, coloured Ribbon tab accents.

`docs/releases/v0.10.0/WorkPackages.md`'s own sixteen-Work-Package plan
(`WP 10.0A`–`WP 10.9A`) is complete and `v0.10.0` is released (tag
`v0.10.0`, "User Experience & Desktop Application," APPROVED); see `WP10.9A
Engineering Release Report.md` for the full gate-by-gate evidence.

**Desktop Composition & Domain Vocabulary Hardening (v0.12.0, in progress):**

- [WP 12.3A — Fault Injection & Validation Framework Architecture](03%20Work%20Packages/WP12.3A-fault-injection-validation-framework-architecture.md) — `v0.12.0`'s own first completed Work Package, a directly-commissioned pair not named in the roadmap's own predicted `12.0`–`12.2` slots. Diagnoses `Tempest.Samples.DuplicateNavigationSampleModule` — a deliberately-always-failing module proving per-module isolation (`ADR-0013`) — being discovered and initialised by every real `Tempest.App`/`Tempest.Desktop` launch, permanently leaving one module `ModuleState.Failed` in real use.
- [WP 12.3B — Fault Injection & Validation Framework Implementation](03%20Work%20Packages/WP12.3B-fault-injection-validation-framework-implementation.md) — realises `ADR-0102`: the module moved to a new project, `Tempest.Validation`, plus a new, default-excluded discovery-time marker (`IFaultInjectionModule`, `ITempestHostBuilder.EnableFaultInjectionModules()`). Verified directly, not merely asserted — a real `WorkspaceHost` now genuinely reaches `Running` with zero modules `Failed`.
- [WP 12.0A — Desktop Composition Root Decomposition Architecture](03%20Work%20Packages/WP12.0A-desktop-composition-root-decomposition-architecture.md) — the roadmap's own first predicted `v0.12.0` Work Package. Realises `WP11.0A Platform Architecture Review.md` Finding `A-1` (Composition-root God Objects in the Desktop layer — `MainWindow.cs` 1,556 lines, a ~1,000-line constructor; `EngineeringCockpit.cs` 1,398 lines). Designs `ADR-0103`, "Composition Roots Own Collaborators," extending `ADR-0009`'s own composition-root principle one layer down.
- [WP 12.0B — Desktop Composition Root Decomposition Implementation](03%20Work%20Packages/WP12.0B-desktop-composition-root-decomposition-implementation.md) — implements `ADR-0103` exactly, closing Finding `A-1` in full: `MainWindow.cs` (1,556 → 544 lines) decomposes into nine `Tempest.Desktop.Composition` collaborators; `EngineeringCockpit.cs` (1,398 → 575 lines) into six per-discipline collaborators. Zero public API changes, zero behavioural changes, zero new Platform Services.
- [WP 12.4A — Desktop Command & Event Wiring Architecture](03%20Work%20Packages/WP12.4A-desktop-command-and-event-wiring-architecture.md) — a second directly-commissioned Work Package, reviewing the Desktop layer's own command/event/UI-wiring architecture after `WP 12.0B`'s decomposition. Catalogues 25 events (all plain delegates, zero unsubscription) and four already-ADR-governed command mechanisms; quantifies a "report via StatusBar/Toast, then refresh" tail repeated up to 42 times. Produces `ADR-0104`: direct delegates remain the default; typed callback interfaces sanctioned only at three or more bundled callbacks.
- [WP 12.4B — Desktop Command & Event Wiring Implementation](03%20Work%20Packages/WP12.4B-desktop-command-and-event-wiring-implementation.md) — realises `ADR-0104` exactly: consolidates `RibbonObjectActionHandlers`'s own 16 duplicated report-then-refresh tails into one local function, `ReportAsync`; replaces two two-phase-constructed `CockpitView` references with a plain `Action refreshCockpit` delegate, closing `WP 12.0B`'s own architecture review Finding 5.
- [WP 12.1A — Classification & Relationship Vocabulary Safety Net Architecture](03%20Work%20Packages/WP12.1A-classification-and-relationship-vocabulary-safety-net-architecture.md) — realises `WP11.0A Platform Architecture Review.md` Finding `A-6` (domain classification/relationship vocabulary entirely stringly-typed, no compile-time safety net). Quantifies the gap: the literal Kind string `"Part"` written at 14 separate sites across 5 files; a confirmed cross-layer duplicate constant in `DigitalThreadGraphModel`. Produces `ADR-0105`: every value declared once as a named constant, tracked in a new Engineering Vocabulary Register, checked by one additive xUnit test — no enum, no runtime validation.
- [WP 12.1B — Classification & Relationship Vocabulary Safety Net Implementation](03%20Work%20Packages/WP12.1B-classification-and-relationship-vocabulary-safety-net-implementation.md) — realises `ADR-0105` exactly. Every `FactoryRegistry` retrofitted with named Kind constants; `DigitalThreadGraphModel`'s own confirmed duplicate closed (three local constants, not one). New Engineering Vocabulary Register populated from a full repository scan — 46 declared values across 11 declaring classes. New `EngineeringVocabularyConsistencyTests`, verified to actually catch a deliberately reintroduced rogue duplicate before being reverted.
- [WP 12.2A — Presentation Strategy Execution Architecture](03%20Work%20Packages/WP12.2A-presentation-strategy-execution-architecture.md) — discovers its own roadmap-predicted scope (realising `WP 11.2A`'s decision) was never commissioned as predicted; the real Desktop & Console Presentation Strategy Decision was ratified and executed under `WP 11.3A`/`WP 11.3B` in `v0.11.0`, before `v0.12.0` even branched. Every claim independently re-verified against live source; concludes neither a new ADR nor architecture document is justified.
- [WP 12.9.0 — Release Preparation & Engineering Sign-Off Architecture](03%20Work%20Packages/WP12.9.0-release-preparation-and-engineering-sign-off-architecture.md) — the final roadmap-defined `v0.12.0` Work Package. Designs the permanent TempestOS Engineering Readiness Review (ERR): five readiness categories, a written blocking taxonomy, and a fixed four-value verdict vocabulary (`ADR-0106`). Phase A repository assessment finds `main` carries four unpushed commits — real, GitHub-hosted CI has not run against the commit this release would tag. First real ERR execution: four of five categories Pass; Verification is `NOT READY` pending exactly one action (pushing `main`).
- [WP 12.9.1 — Governance Health Check Remediation](03%20Work%20Packages/WP12.9.1-governance-health-check-remediation.md) — repairs the four genuine `[FAIL]` results `governance-healthcheck.ps1` reports against `main`, per `WP 12.9.0`'s own Phase A finding above; this Academy Index entry, and the 58 others added alongside it, are this Work Package's own principal deliverable. Documentation/governance only; `scripts/governance-healthcheck.ps1` left unmodified throughout — every finding traced to genuine repository/documentation drift or an already-documented checker scope limit, never a checker defect.
- [WP 12.9.2 — Engineering Readiness Review Re-Execution](03%20Work%20Packages/WP12.9.2-engineering-readiness-review-re-execution.md) — re-executes the complete ERR (`ADR-0106`) against `main` after `WP 12.9.1`'s remediation, independently re-deriving every Phase A item from source. Finds `WP 12.9.1`'s own changes had never been committed; commits them (`955badb`, local only, no push) before evaluating anything else — a repository-state change necessary to assess "the current state after `WP 12.9.1`" at all. `governance-healthcheck.ps1` re-run clean: 7 passed, 1 warned, 0 failed. Architecture/Implementation/Governance/Release readiness all Pass; Verification readiness Not Ready — the identical class of finding `WP 12.9.0`'s own first execution raised, now against the newly-committed, not-yet-pushed tip. **Verdict: `NOT READY`**, pending one action (push, then real CI confirmation); no tag or push created or proposed.

`v0.12.0`'s own roadmap-defined Work Packages are complete; the
Engineering Readiness Review itself (`ADR-0106`) remains a distinct,
later, separately-commissioned Work Package, gated on `WP 12.2A`'s own
disposition being committed and merged first. See `PROJECT_STATUS.md`
for current status and `docs/releases/v0.12.0/WorkPackages.md` for the
full plan.

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
