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
- [WP 13.0A — Plugin & Registration Trust Isolation Architecture](03%20Work%20Packages/WP13.0A-plugin-and-registration-trust-isolation-architecture.md) — the `v0.13.0` architecture-only Work Package extending the plugin platform's shape (dependencies, a queryable `IPluginRegistry`, lifecycle, configurable conventions — `ADR-0107`–`ADR-0109`) and, composed with it, the trust boundary the concept guide above was extended in place to cover (`ADR-0110`–`ADR-0112`, see **Security**, below, for the trust/isolation half). Design only at the time — see `WP 13.2A`, below, for the implementation.
- `docs/architecture/Plugin Platform Architecture.md` — the full `v0.13.0` design document for manifest v2, dependency resolution, the Plugin Registry, lifecycle, and DI service-registration boundaries.
- [WP 13.1A — Plugin Runtime & Composition Root Implementation](03%20Work%20Packages/WP13.1A-plugin-runtime-and-composition-root-implementation.md) — builds the mechanical, non-trust half of `WP 13.0A`'s design (`ADR-0107`–`ADR-0109`): the fixed-point dependency-graph resolution, the Host-owned `PluginRegistry`, the configurable plugins root/manifest convention (closing `TD-06`/`FCR-0010`), and `IDiagnosticsProvider.Plugins`. Real, tested code — the trust/isolation half (`ADR-0110`–`ADR-0112`) was implemented next, `WP 13.2A`, below.
- [WP 13.2A — Plugin Trust & Capability Enforcement Implementation](03%20Work%20Packages/WP13.2A-plugin-trust-and-capability-enforcement-implementation.md) — builds the trust half `WP 13.1A` left untouched: real trust tier assignment and detached SHA-256/RSA-PSS signature verification at Plugin Discovery (`ADR-0112`), real capability enforcement at Plugin Loading (`PluginAssemblyLoader.EnforceTrust`, `ADR-0111`), and real trust-ordered registration across `NavigationService`/`CommandRegistry`/`CommandHandlerTable`/`IEventBus`. Closes `TD-09`/`TD-10`/`TD-11` — see **Security**, below.
- [WP 13.3A — Plugin Platform Integration & End-to-End Validation](03%20Work%20Packages/WP13.3A-plugin-platform-integration-and-end-to-end-validation.md) — the plugin platform's own closing integration/validation pass for `v0.13.0`: independently re-verifies `ADR-0107`–`ADR-0112` fully implemented against real, current code (not any prior Work Package's own citation), raises `TD-49`/`TD-50` (a Discovery-to-Loading TOCTOU window; filename-based first-party trust-store detection — both disclosed by `WP 13.2B`, never before formally registered) and `FCR-0085`–`FCR-0088` (certificate-chain validation/revocation checking, per-plugin hot/live reload and unload, process-separated marketplace isolation, a per-plugin unsigned-load allow-list — all named out of scope by the ADRs themselves), and fixes a stale `ADR-0107`–`ADR-0109` citation gap in `ADR Register.md`.
- [WP 13.3B — Plugin Platform Integration & End-to-End Validation Review](03%20Work%20Packages/WP13.3B-plugin-platform-integration-and-end-to-end-validation-review.md) — an independent review of `WP 13.3A` by four parallel sub-agents, mirroring `WP 13.0B`'s/`WP 13.1B`'s own review-before-baseline discipline; found and fixed a genuine registry-Id-spoofing defect in `WP 13.3A`'s own `InvalidPluginManifestException.PluginId` fix (a malformed, never-validated manifest could otherwise carry its own declared `Id` into `PluginRegistryEntry.Id`, spoofing a genuine, already-`Loaded` plugin's entry). Committed together with `WP 13.3A` as a single commit (`88e41a2`); this retrospective itself was reconstructed by `WP 13.9.1` directly from that commit message, closing the most-corroborated finding in `WP13.9.0 Engineering Release Report.md`.

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
- [WP 13.0A — Plugin & Registration Trust Isolation Architecture](03%20Work%20Packages/WP13.0A-plugin-and-registration-trust-isolation-architecture.md) — the direct answer to `Security Roadmap.md` items 1, 2, and 10: a four-tier plugin trust model, a capability model extending `IPermissionEvaluator` (`ADR-0044`) via a new `ICurrentComponentAccessor`, a detached signature verified at Plugin Discovery, and the isolation-boundary decision (capability-scoped enforcement, not `AssemblyLoadContext` or process separation — `ADR-0110`–`ADR-0112`). Closes `TD-09`/`TD-10`/`TD-11` at the design level; see **Plugins**, above, for the composed Plugin Platform half of this same Work Package, and `02 Runtime Architecture/07-plugin-architecture.md`'s own extended Trust Boundary section for the synthesised concept-guide treatment. Design only at the time — see `WP 13.2A`, below, for the implementation.
- `docs/security/Plugin Trust & Isolation Architecture.md` — the full `v0.13.0` design document for trust tiers, the capability model, the signing strategy, and the isolation-boundary decision.
- [WP 13.2A — Plugin Trust & Capability Enforcement Implementation](03%20Work%20Packages/WP13.2A-plugin-trust-and-capability-enforcement-implementation.md) — implements `ADR-0110`–`ADR-0112` exactly as designed: real trust tier assignment/signature verification at Plugin Discovery, real capability enforcement at Plugin Loading, real trust-ordered registration across `NavigationService`/`CommandRegistry`/`CommandHandlerTable`/`IEventBus`. **`TD-09`/`TD-10`/`TD-11` moved Open → Resolved**; `Security Roadmap.md` items 1, 2, and 10 updated from "resolved at the architecture level" to implemented.

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

### Product Convergence & Recovery

The 2026-08-28/29 programme that turned the working engineering platform
into the project-centric TempestOS product: an adversarial Product
Compliance Audit and its responsive-workspace remediation, the Product
Spine, a real persistence boundary for engineering objects, the
project/standalone convergence of the shell, and the replacement of the
compile-time docking grid with a data-driven layout tree.

- [Responsive Workspace & Ribbon Minimisation](02%20Runtime%20Architecture/32-responsive-workspace-and-ribbon-minimisation.md) — the Product Compliance Audit's own remediation: a width-driven responsive rule over the workspace and a genuinely minimisable Ribbon; closes `TD-70`/`TD-71`.
- [The Product Spine](02%20Runtime%20Architecture/33-the-product-spine.md) — `IProjectDirectory`/`IProjectContext`/`IShellNavigator`: the project-centric backbone the product was missing, established without rewriting the engineering platform beneath it.
- [Engineering Object Rehydration](02%20Runtime%20Architecture/34-engineering-object-rehydration.md) — `ADR-0113`: durable `EngineeringObjectState` plus factory-driven rehydration (`IRehydratable<TSelf>`), so a persisted object survives a process restart with its identity, lifecycle, revisions, relationships and provenance intact; removes `Projects.Index` rather than leaving two competing persistence mechanisms. Closes `TD-85`; opens `TD-86`/`TD-87`/`TD-88`.
- [Project-Centric Convergence](02%20Runtime%20Architecture/35-project-centric-convergence.md) — transitive `ProjectMembership`, `IEngineeringScope`, and the `ShellAreas`/`ProjectAreas` descriptor tables that make a not-yet-implemented module say so honestly instead of pretending. Standalone calculation sets remain a first-class, project-free workflow. Closes `TD-89` for the spine.
- [Workspace Layout & Docking](02%20Runtime%20Architecture/36-workspace-layout-and-docking.md) — `ADR-0095`: the immutable `WorkspaceLayoutTree` (splits, tab groups, floating windows) that replaces the fixed 5x3 `DockingGrid`, with drag-to-dock, collapse/auto-hide, resize, persistence and restoration. Closes `TD-72`; opens `TD-90`/`TD-91`.
- [Attachment Content Storage](02%20Runtime%20Architecture/37-attachment-content-storage.md) — `ADR-0114`: an attached file becomes a file this platform holds. The byte shape of the store it already had, with metadata and content deliberately separated so rehydrating a whole object graph loads no files, and reads that report `Available`/`Missing`/`Corrupt` rather than handing back damaged bytes. Closes `TD-31` and implements `FCR-0054`; opens `TD-95`/`TD-96`/`TD-97`.
- [Document & Drawing Viewer](02%20Runtime%20Architecture/38-document-and-drawing-viewer.md) — `ADR-0115`: a real viewer, not a metadata display. PDFium rasterises pages on demand at the current zoom, because a text-extraction viewer serves a specification and fails at the vector drawings mock-ups 2 and 3 are about. The viewport is a pure immutable value; the viewer is an ordinary `TD-72` panel. Closes `TD-80` for the scope delivered and visually accepts it against the mock-ups; opens `TD-98`–`TD-101`. Includes what a 287-green headless suite could not see: four user-visible defects — one of them a missing Open button that made the whole viewer unreachable — found the first time anyone rendered the window.
- [Project Documents & Requirements](02%20Runtime%20Architecture/39-project-documents-and-requirements.md) — `TD-102`: the two project areas that were marked **Implemented** and drew a declared-capability card with no badge. Documents join through `ProjectMembership` transitively; requirements join through the allocation link the platform already records, because a requirement is not an engineering object and a `ProjectId` field would be a second answer. Declared status and recorded verification are shown side by side because they disagree. Opens `TD-103` (the desktop shell establishes no principal).
- [Production Rehydration & the Principal Boundary](02%20Runtime%20Architecture/40-production-rehydration-and-the-principal-boundary.md) — `ADR-0116`: two defects with one shape — the product worked because the sample harness happened to ship. Twelve engineering Kinds rehydrated only because `Tempest.Samples` registered them, and nine more were registered nowhere at all and were silently discarded on every restart, found by reflecting over the domain rather than reading the registration list. The lesson worth carrying: when proving an absence, assert the dependency, not the symptom — a behavioural test passes either way when the assembly is loaded in the test process. Resolves `TD-103` and `TD-104`; closes `TD-75`'s rehydration half and says plainly which half is left. Includes the bug the fix introduced: publishing only a non-null principal left a sample's principal standing in a session that should have had none.
- [Project Tasks & Delivery Workflow](02%20Runtime%20Architecture/41-project-tasks-and-delivery-workflow.md) — `ADR-0117`: the task model already existed and nothing used it. Its central decision is a refusal — a task's status is not `LifecycleState`, because the canonical table forbids Released → Draft (correctly) while a finished task must reopen. Rather than weaken a rule protecting released engineering data, the task family implements `IFamilySpecificState`, a contract the platform had declared and never used: when a rule is in your way, check whether the codebase already predicted the exception. Also records the mutation that survived — a state change that never persisted, invisible because every assertion read the object it had just changed — and two test fragilities: a stale reference across `ReviseAsync`, and `Single()` failing on a shown window. Partially resolves `TD-81`, for Tasks only.

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

**Release Engineering & Architecture Governance (v0.11.0, Released 2026-08-11 — ACCEPT WITH OBSERVATIONS):**

- [WP 11.0A — Platform Architecture & Code Quality Review](03%20Work%20Packages/WP11.0A-platform-architecture-and-code-quality-review.md) — an independent, evidence-based review of the complete `v0.10.0` codebase (600 `.cs` files, ~66,000 lines): ten findings (`A-1`–`A-9`, `R-1`), zero Critical, zero ADR violations in the areas sampled. Independently confirms dependency direction empirically (zero `Tempest.Core`→`Tempest.App`/`Tempest.Desktop` references) rather than trusting the documentation's own claim. Finds no CI/CD pipeline anywhere in the repository (`R-1`) and two independently maintained presentation stacks with no documented decision about the console shell's future (`A-2`) — both drive the rest of this release directly. Review only, no code modified.
- [WP 11.0B — v1.0 Architecture Roadmap & Release Planning](03%20Work%20Packages/WP11.0B-architecture-roadmap-and-release-planning.md) — turns `WP 11.0A`'s ten findings into a scoped, sequenced Work Package roadmap from `v0.11.0` through `v1.0.0`. Proposes a v1.0 definition ("Definition 2": a locally-trusted desktop engineering platform, third-party plugins and non-local REST explicitly out) later cited verbatim as `D-021` in `WP16.0A v0.16.0 Scope Decision.md`, Proposed. Reprioritises `FCR-0005` (governance health-check tooling) from perpetually-deferred to this release, after its own seventh independent recurrence. Planning only, no code, ADR, or architecture modified.
- [WP 11.1A — Continuous Integration & Build Verification](03%20Work%20Packages/WP11.1A-continuous-integration-and-build-verification.md) — `.github/workflows/ci.yml`, TempestOS's first CI pipeline, closing finding `R-1`. Builds Debug and Release (warnings promoted to errors at the CI step only, local builds unchanged) and runs the complete test suite against both, on every push/PR/manual dispatch. Verified locally, command-for-command: 0 Warnings/0 Errors both configurations, 2,266/2,266 tests passing. Actual GitHub-hosted execution disclosed `Unknown`/unverified pending the first real push.
- [WP 11.1B — Branch Protection & Engineering Workflow Hardening](03%20Work%20Packages/WP11.1B-branch-protection-and-engineering-workflow-hardening.md) — the complete engineering workflow surrounding `WP 11.1A`'s pipeline: branching strategy, pull request expectations, merge/release requirements, a new hotfix workflow and rollback procedure, and a release-candidate process. Two genuine pre-existing defects fixed in `scripts/new-release.ps1`. One genuine governance deviation found and disclosed, not silently fixed: the `v0.10.0` tag points to the feature branch's own pre-merge commit, not `main` — a warning `v0.11.0`'s own tag would repeat regardless (see `WP 11.4B`).
- [WP 11.2A — Governance Health-Check Automation](03%20Work%20Packages/WP11.2A-governance-health-check-automation.md) — `scripts/governance-healthcheck.ps1`, delivering `FCR-0005`. Eight checks, Pass/Warn/Fail, no production-code dependency. Three genuine tool defects found and fixed during development. Its first live run finds two genuine, previously-undisclosed governance findings — the Academy Index's own "Work Package Walkthroughs" section stopping at `WP 7.3A` (the exact gap this entry, and the nine alongside it, are backfilling for `v0.11.0` itself) and four documented-but-untracked-by-git directories — both disclosed, neither fixed within this Work Package's own scope.
- [WP 11.3A — Presentation Strategy Review & Platform Consolidation](03%20Work%20Packages/WP11.3A-presentation-strategy-review-and-platform-consolidation.md) — finally commissions finding `A-2`, after being predicted for two roadmap slots without landing under either. Finds the real duplication confined to `TempestShell` (provably dead three releases, zero risk to retire), not the shared Workspace domain layer `Tempest.Desktop` depends on; `WorkspaceShell` has genuine, demonstrated ongoing harness value. Recommends (5-stage roadmap, Stages 1–4 approved, Stage 5 deferred): retire `TempestShell`; formally ratify `Tempest.App`/`WorkspaceShell` as an Internal Engineering Harness via a new ADR; correct documentation and release packaging. Review only.
- [WP 11.3B — Presentation Strategy Implementation](03%20Work%20Packages/WP11.3B-presentation-strategy-implementation.md) — executes `WP 11.3A`'s approved Stages 1–4: `TempestShell`/`IPage`/`PlaceholderPage` retired (`git rm`, history preserved); `ADR-0101` authored, classifying `Tempest.App`/`WorkspaceShell` as TempestOS's Internal Engineering Harness; `README.md`, `Contributor Learning Path.md`, `Platform Service Map.md`, `Shell & Composition Framework Architecture.md` corrected; `ci.yml`/`release.yml` now package `Tempest.Desktop` and `Tempest.App` as two separate, clearly-labelled artifacts. Full regression 2,228/2,228 both configurations; `governance-healthcheck.ps1` re-run, ADR Register check now correctly 100/100 automatically.
- [WP 11.4A — Release Engineering Corrections](03%20Work%20Packages/WP11.4A-release-engineering-corrections.md) — resolves the three release-engineering defects the first real GitHub-hosted CI execution diagnosed, none a regression in product code: a genuinely flaky Desktop test (fixed delay replaced with a bounded, condition-driven poll); `governance-healthcheck.ps1`'s absolute-path handling (`Join-Path` doubling an already-rooted `-SummaryPath`); `ci.yml`'s tag-triggered checkout conflict (`fetch-tags` colliding with an unpinned ref). All three independently re-verified on real infrastructure via two separate live runs; `Build & Test`/`CI Gate` genuinely pass on real infrastructure for the first time this release.
- [WP 11.4B — Merge to Main & Release Process Correction](03%20Work%20Packages/WP11.4B-merge-to-main-and-release-process-correction.md) — merges `feature/v0.11.0-v1-architecture` into `main` (`4b1fb16`) and closes the release, but first discovers, in its own required pre-merge verification, that `v0.11.0`'s own tag repeated verbatim the `v0.10.0` tag-position defect `WP 11.1B` had already disclosed and warned against — found *before* this release's branch closed, unlike `v0.10.0`'s case. Written as a genuine NCR/CAPA record. Governance §7 item 4 amended under explicit, narrow, one-time Product Owner authorisation; tag deleted and recreated on the merge commit, reusing its original annotation verbatim; Build Gate/Test Gate re-verified on `main` itself for the first time this release, satisfying §7.3 as literally written.
- [WP 11.9.0 — `v0.11.0` Release Preparation & Engineering Sign-Off](03%20Work%20Packages/WP11.9.0-v0.11.0-release-preparation-and-engineering-sign-off.md) — executed as a six-discipline TempestOS Engineering Programme, each performing an independent review before reconciliation. All hard gates independently re-verified from a clean build: Debug/Release both 0 Warnings/0 Errors; 2228/2228 tests both configurations (the 2266→2228 drop exactly reconciled against the two deleted `WP 11.3B` test files' own raw source). Five governance registers found stale and corrected, including one outright factual error (`FCR-0005` still read "Identified, not started" despite having shipped). Finds, but does not fix within this "Verification only" scope, `Contributor Learning Path.md`'s own stale closing pointer (`docs/releases/v0.5.0/WorkPackages.md`, six releases stale — not corrected until `WP 16.2B`) and this release's own absent Academy retrospective coverage (also not backfilled until `WP 16.2B`). **Recommendation: Accept with Observations.**

`docs/releases/v0.11.0/WorkPackages.md`'s own eight-Work-Package plan
(`WP 11.0A`–`WP 11.9.0`) is complete and `v0.11.0` is released (tag
`v0.11.0`, "Release Engineering & Architecture Governance," ACCEPT WITH
OBSERVATIONS, retagged on merge commit `4b1fb16` per `WP 11.4B`'s own
disclosed, Product-Owner-approved exception); see `WP11.9.0 Engineering
Release Report.md` for the full six-discipline Programme Review and
`WP11.5A Governance Currency & Documentation Integrity.md` for a later,
`v0.15.0`-era follow-up audit of this same release's own governance
documentation.

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
- [WP 12.9.3B — Governance Health Check Consistency Remediation](03%20Work%20Packages/WP12.9.3B-governance-health-check-consistency-remediation.md) — pushing `main` for the first time surfaces a real GitHub Actions `[FAIL]` invisible to every local run: `Test-ProjectStatusReferences` and `Test-ReleaseRegisterMatchesTags` both consume `Get-RepoTags`, but only the latter treated a shallow checkout's empty tag list as a disclosed environmental limitation (`Warn`) rather than a content defect — a checker-architecture inconsistency (`WP 12.9.3A`'s own architecture review), not a repository defect: `v0.3.0`–`v0.10.0` are all real, correctly-tagged releases. Fix scoped to exactly the affected sub-check — version-token validation now degrades to `Warn` when tags are unavailable; path-reference validation, which never depended on tags, continues unconditionally and can still `Fail` the check on its own. `Get-RepoTags` and Check 3 itself both left unmodified. Four behavioural cases verified against isolated fixtures; full Debug+Release regression 2,255/2,255 passing both, 0 Warnings/0 Errors both.

`v0.12.0`'s own roadmap-defined Work Packages are complete, its
Engineering Readiness Review exercised for real three times (`WP
12.9.0`/`WP 12.9.2`/`WP 12.9.4`), and the release itself tagged and
published — final verdict `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`;
see `docs/releases/v0.12.0/WP12.9.4 Engineering Release Report.md` for
the full account.

**Trust & Deployment Hardening (v0.13.0, in progress):**

- [WP 13.0.0 — v0.13.0 Branch Establishment](03%20Work%20Packages/WP13.0.0-v0.13.0-branch-establishment.md) — creates `feature/v0.13.0`, cut directly from the `v0.12.0` tag, as the sole integration branch for this release (stricter than every prior release's own multiple-parallel-branch convention). Two real conflicts found and resolved with the Product Owner rather than silently applied: `VERSION → 0.13.0-dev` would have broken `governance-healthcheck.ps1`'s own version-token parsing; `v0.13.0`'s own scope is roadmap-conditional, confirmed now explicitly triggered. Three parallel verification agents (Architecture/Repository/Governance) all Pass.
- [WP 13.0.0A — Release Register Reconciliation](03%20Work%20Packages/WP13.0.0A-release-register-reconciliation.md) — closes the one finding `WP 13.0.0` disclosed rather than fixed: `docs/governance/Delivery/Release Register.md` had no row for the `v0.12.0` tag, a pre-existing gap dating to that release's own close, independently confirmed to predate this Work Package. `governance-healthcheck.ps1` re-confirmed clean: 7 passed, 1 warned, 0 failed. Governance/documentation only; zero `src/`/`tests/` files touched.
- [WP 13.0A — Plugin & Registration Trust Isolation Architecture](03%20Work%20Packages/WP13.0A-plugin-and-registration-trust-isolation-architecture.md) — this release's own first roadmap-predicted Work Package (`A-3`/`FCR-0001`). Two composed architecture documents (`Plugin Platform Architecture.md`, `Plugin Trust & Isolation Architecture.md`, `ADR-0107`–`ADR-0112`) resolve, at the design level, `TD-09`/`TD-10`/`TD-11` and close `TD-06`/`FCR-0010`. See **Plugins** and **Security**, above, for the topical treatment. Architecture only; zero `src/`/`tests/` files touched. **Found and corrected by `WP 13.0B`'s own independent audit before this entry was added**: this list itself had never gained a `WP 13.0A` row at all, despite the topical sections above referencing it correctly — added here directly.
- [WP 13.0B — Plugin & Trust Isolation Architecture Review and Baseline](03%20Work%20Packages/WP13.0B-plugin-and-trust-isolation-architecture-review-and-baseline.md) — an independent review/verification Work Package auditing `WP 13.0A` before it becomes a commit. Three parallel, genuinely independent audit sub-agents (Architecture; Governance; Documentation) found and closed a small set of real defects `WP 13.0A` itself introduced (a stale ADR-count paragraph, a mis-cited failure-category range, a false "untouched" claim about two already-edited architecture documents, two missing ADR citations) — no new architecture, no broadened scope. `governance-healthcheck.ps1` re-confirmed clean: 7 passed, 1 warned, 0 failed. `WP 13.0A`'s and this Work Package's combined material committed as a single architecture baseline.
- [WP 13.1A — Plugin Runtime & Composition Root Implementation](03%20Work%20Packages/WP13.1A-plugin-runtime-and-composition-root-implementation.md) — the real Trust Isolation Implementation successor `WP 13.0B`'s own entry above disclosed as "not yet commissioned, and does not yet have a number," now commissioned and assigned the `WP 13.1A` label directly by the Product Owner — colliding with, and repurposing, that same label's own roadmap-predicted "REST API Authentication & TLS Architecture" row; disclosed plainly, not silently reconciled (see `WorkPackages.md`). Builds only the mechanical, non-trust half of `WP 13.0A`'s design (`ADR-0107`–`ADR-0109`); `TD-09`/`TD-10`/`TD-11` remain untouched. Debug/Release both 0 Warnings/0 Errors; full regression Debug 2,305/2,305 passing; zero genuine production-code defects found; closes `TD-06`/`FCR-0010`. See **Plugins**, above, for the topical treatment.
- [WP 13.1B — Plugin Runtime & Composition Root Implementation Review](03%20Work%20Packages/WP13.1B-plugin-runtime-and-composition-root-implementation-review.md) — an independent review of `WP 13.1A` before it becomes a commit, mirroring `WP 13.0B`'s own review-before-baseline discipline, applied to real code for the first time. Four parallel, genuinely independent audit sub-agents found and closed two real, empirically-confirmed production bugs (a duplicate-dependency-entry bug silently dropping a valid plugin from the topological sort with zero diagnostic trace; a blank configured value faulting the entire Host instead of falling back to default) plus two test-coverage gaps, adding eight new regression tests. `governance-healthcheck.ps1` re-confirmed clean; full regression, both configurations, 2,313/2,313 passing. No new architecture, no ADR changes, no broadened scope.
- [WP 13.2A — Plugin Trust & Capability Enforcement Implementation](03%20Work%20Packages/WP13.2A-plugin-trust-and-capability-enforcement-implementation.md) — the trust-half implementation successor to `WP 13.1A`/`WP 13.1B`'s own mechanical half, implementing `ADR-0110`–`ADR-0112` exactly as `WP 13.0A` designed and `WP 13.0B` independently audited them, zero architectural deviation. Real trust tier assignment/detached SHA-256 signature verification at Plugin Discovery; real capability enforcement (`PluginAssemblyLoader.EnforceTrust`) and trust-ordered registration (`NavigationService`/`CommandRegistry`/`CommandHandlerTable`/`IEventBus`) at Plugin Loading and each call site. **Closes `TD-09`/`TD-10`/`TD-11`** and implements `Security Roadmap.md` items 1, 2, 10. Not a roadmap-table numbering collision — outside the original prediction entirely, the same shape `WP 13.0.0`/`WP 13.0.0A` already established. See **Plugins**/**Security**, above, for the topical treatment.
- [WP 13.2B — Plugin Trust & Capability Enforcement Review](03%20Work%20Packages/WP13.2B-plugin-trust-and-capability-enforcement-review.md) — an independent review of `WP 13.2A` by four parallel read-only sub-agents, committed with it as `25df570`. Found and fixed a Critical privilege-escalation path (`NeverEligibleServiceResolveTypes`), a blocking missing component-scope push in `CommandHandlerTable.DispatchAsync`, and a Medium `IsFirstParty` exemption-guard inconsistency across four registries. Two findings disclosed but not fixed were later registered as `TD-49`/`TD-50` by `WP 13.3A`.
- [WP 13.3A — Plugin Platform Integration & End-to-End Validation](03%20Work%20Packages/WP13.3A-plugin-platform-integration-and-end-to-end-validation.md) — the plugin platform's own closing integration/validation pass, independently re-verifying `ADR-0107`–`ADR-0112` against real code and registering `TD-49`/`TD-50` and `FCR-0085`–`FCR-0088`.
- [WP 13.3B — Plugin Platform Integration & End-to-End Validation Review](03%20Work%20Packages/WP13.3B-plugin-platform-integration-and-end-to-end-validation-review.md) — an independent review of `WP 13.3A`, finding and fixing a genuine registry-Id-spoofing defect in that Work Package's own `InvalidPluginManifestException.PluginId` fix.
- [WP 13.9.0 — v0.13.0 Engineering Readiness Review](03%20Work%20Packages/WP13.9.0-v0.13.0-engineering-readiness-review.md) — the release-readiness review that opened the `WP 13.9.x` hardening chain, returning a **NOT READY** verdict. Its Security/Trust discipline empirically demonstrated, against the compiled binary, that `EnforceTrust` scanned only the one manifest-declared assembly — the finding `WP 13.9.1`–`WP 13.9.6` then remediated.
- [WP 13.9.1 — v0.13.0 Readiness Remediation](03%20Work%20Packages/WP13.9.1-v0.13.0-readiness-remediation.md) — governance and documentation remediation closing `WP 13.9.0`'s findings, including reconstructing `WP 13.3B`'s own missing Academy retrospective directly from commit `88e41a2`.
- [WP 13.9.2 — v0.13.0 Readiness Re-Execution](03%20Work%20Packages/WP13.9.2-v0.13.0-readiness-re-execution.md) — a fresh re-execution of the readiness review against the remediated state.
- [WP 13.9.3 — Multi-Assembly Trust-Boundary Remediation](03%20Work%20Packages/WP13.9.3-multi-assembly-trust-boundary-remediation.md) — closes the multi-assembly scan gap: constructor-parameter `ParameterType` resolution is forced inside the scan's own before/after `AppDomain` diff window, so an assembly reachable only through a constructor parameter can no longer escape trust checking.
- [WP 13.9.4 — Trust-Denial Execution Boundary Remediation](03%20Work%20Packages/WP13.9.4-trust-denial-execution-boundary-remediation.md) — makes trust denial an execution boundary rather than a bookkeeping outcome: a Host-owned `IPluginDeniedTypeRecorder`/`IPluginDeniedTypeRegistry` plus two `TempestHost` filters, at Module Registration and Hosted Service Registration. Broadened mid-Work-Package, by its own Adversarial Review, to cover `IHostedService` as well as `IModule`.
- [WP 13.9.5 — v0.13.0 Trust & Security Final Adversarial Review](03%20Work%20Packages/WP13.9.5-v0.13.0-trust-and-security-final-adversarial-review.md) — the adversarial review that found `WP 13.9.4`'s registration-time filters, though correct, act too late: an unattributed denied module was still being constructed during Module Discovery itself.
- [WP 13.9.6 — Module Discovery Trust Boundary Remediation](03%20Work%20Packages/WP13.9.6-module-discovery-trust-boundary-remediation.md) — closes that gap with one optional, generic `Func<Type, bool>? isTypeExcluded` predicate on `ReflectionFrameworkDiscoveryService`, wired by `TempestHost` to the unmodified `WP 13.9.4` registry — preserving `ADR-0110`'s deliberate plugin-unawareness at the type-reference level. Verified non-vacuous by mutation testing.
- [WP 13.9.7 — Trust Boundary Integration & Commit](03%20Work%20Packages/WP13.9.7-trust-boundary-integration-and-commit.md) — sole integrating agent, no parallel writers; reconciled, independently verified and committed the entire `WP 13.9.1`–`WP 13.9.6` chain as one commit, `d7d19d4`.
- [WP 13.10A — Plugin Platform Hardening Architecture](03%20Work%20Packages/WP13.10A-plugin-platform-hardening-architecture.md) — a read-only hardening review that found two genuine, live-PoC-confirmed gaps in `ADR-0111`'s own enforcement mechanism, raising `TD-51` and `TD-52`.
- [WP 13.10B — Plugin Trust Hardening Implementation](03%20Work%20Packages/WP13.10B-plugin-trust-hardening-implementation.md) — closes both: `HasCompliantConstructor` and the never-eligible denylist now run against `hostedServiceTypes` as well as `moduleTypes`, and `HostedServiceManager` gained an optional `componentScopeProvider` hook mirroring `ModuleLifecycleManager`'s established `ADR-0111` hook. Its own Adversarial Security review found and fixed a compounding regression mid-implementation, before commit.
- [WP 13.10C — Plugin Trust Hardening Review & Integration](03%20Work%20Packages/WP13.10C-plugin-trust-hardening-review-and-integration.md) — four fresh read-only reviewers, each with its own live proof-of-concept; non-vacuousness confirmed by reverting the fix and observing the targeted tests genuinely fail, then restoring. Committed the `WP 13.10A`/`WP 13.10B` chain as `79e453f`. Its characterisation of `TD-51`'s residual gap as safely isolated was later found false for the `IModule` axis by `WP 13.11A`.
- [WP 13.11A — Plugin Platform Final Hardening Architecture Review](03%20Work%20Packages/WP13.11A-plugin-platform-final-hardening-architecture-review.md) — read-only, six parallel disciplines. **Reopened `TD-51`**: the "unresolvable constructor parameter type" residual gap genuinely crashes the whole Host on the `IModule` axis. Also raised `TD-53` and `TD-54`.
- [WP 13.11B — TD-51 Trust-Denial Crash Remediation](03%20Work%20Packages/WP13.11B-td-51-trust-denial-crash-remediation.md) — closes reopened `TD-51` (`8341438`). The denial is surfaced to `EnforceTrust` through an `out PluginTrustDeniedException?` so `RecordDenied` runs before the throw, and the fixed-point scan is allowed to **complete** rather than abort — deliberately declining `WP 13.11A`'s own recommended partial-list shape, which would have traded a Host crash for a silent trust bypass. Added `TD-55`.
- [WP 13.11C — TD-51 Remediation Review & Trust-Boundary Verification](03%20Work%20Packages/WP13.11C-td-51-remediation-review-and-trust-boundary-verification.md) — found that `WP 13.11B`'s completed-fixed-point-scan decision, the security-critical half of that fix, had **no regression coverage at all**; reverting it left all 2,561 tests green. Closed with one new test and one new builder helper, proven non-vacuous in both directions, and corrected a factually wrong inherited "three other call sites" claim.
- [WP 13.11D — v0.13.0 Plugin Platform Exit Review](03%20Work%20Packages/WP13.11D-v0.13.0-plugin-platform-exit-review.md) — read-only exit review across six disciplines, declaring **READY FOR WP14** with zero `src/`/`tests/` files changed. Added `TD-56`: a plugin's own constructor executes outside its component scope, so plugin code runs during construction with a `null` ambient principal every capability gate treats as first-party — **Disclosed, Non-Blocking for `v0.13.0`; mandatory precondition to enabling third-party plugin support**.
- [WP 13.12.0 — v0.13.0 Academy Retrospective Completion](03%20Work%20Packages/WP13.12.0-v0.13.0-academy-retrospective-completion.md) — closes the release-blocking Academy completeness finding: sixteen of `v0.13.0`'s twenty-five delivered Work Packages had shipped with no retrospective, against Engineering Governance §6. All sixteen backfilled from primary evidence; the claimed exemption in `PROJECT_STATUS.md` shown to cite a rule that does not exist, and corrected.
- [WP 13.12.1 — v0.13.0 Engineering Readiness Re-Execution](03%20Work%20Packages/WP13.12.1-v0.13.0-engineering-readiness-re-execution.md) — re-ran the readiness review mechanically under `ADR-0106` §4 across six read-only disciplines, reporting committed `HEAD` and working tree separately (9 of 25 retrospectives versus 25 of 25). **NOT READY** for both, on two Release Blocking findings at `HEAD` and one in the working tree. Two disputed classifications adjudicated against the governing text. Changed no file.
- [WP 13.12.2 — v0.13.0 Release Documentation Closure](03%20Work%20Packages/WP13.12.2-v0.13.0-release-documentation-closure.md) — produced this release's authoritative Engineering Release Report (superseding `WP13.9.0`'s stale **NOT READY**) and its Release Notes, added the `WP 13.12.x` records, and corrected eight documentation-drift findings by annotation rather than replacement — while deliberately preserving genuinely pre-existing debt. Leaves CI-on-`main` as the sole remaining barrier to release.
- [WP 13.12.3 — v0.13.0 Commit Preparation & Final Branch Audit](03%20Work%20Packages/WP13.12.3-v0.13.0-commit-preparation-and-final-branch-audit.md) — six read-only audits over the uncommitted `WP 13.12.0`–`13.12.2` documentation set. Determined the `ADR-0110` amendment did not require architectural approval (Consequences-only; `ADR-0111`'s precedent is stronger still). Found and fixed **eight defects the documentation pass had itself introduced**, all before commit `f58b475`.
- [WP 13.12.4 — VERSION 0.12.0 → 0.13.0](03%20Work%20Packages/WP13.12.4-version-bump-0.12.0-to-0.13.0.md) — the release-time version bump, isolated in its own sequential commit (`30b0f45`) so the pre-tag release candidate is one minimal diff. `new-release.ps1` reads `VERSION` on `main` and refuses to tag on mismatch, so it had to land pre-merge.
- [WP 13.12.5 — PR & Main-CI Release Gate](03%20Work%20Packages/WP13.12.5-pr-and-main-ci-release-gate.md) — stopped at a tooling boundary rather than inventing CI evidence, then merged PR #1 under explicit Product Approval as `6089a218`. Real CI green on `main` at that exact pre-tag commit, clearing the release's sole `ADR-0106` §4 Release Blocking finding for the first time.
- [WP 13.12.6 — v0.13.0 Tag & Release](03%20Work%20Packages/WP13.12.6-v0.13.0-tag-and-release.md) — ran `new-release.ps1` without `-Push`, creating annotated tag `15aedec` → `6089a218`. Verified the tag independently rather than trusting the script's own success banner, since `TD-42` records that `git tag` runs there without an `$LASTEXITCODE` guard.
- [WP 13.12.7 — v0.13.0 Tag Push & Release Completion](03%20Work%20Packages/WP13.12.7-v0.13.0-tag-push-and-release-completion.md) — pushed only the tag; `release.yml` then **failed** at `Test (Release)` on one Desktop test, so packaging and publication were skipped and no GitHub Release or asset was ever produced. Stopped immediately without amending, retagging, or retrying.
- [WP 13.12.8 — v0.13.0 Release Test Failure Investigation](03%20Work%20Packages/WP13.12.8-v0.13.0-release-test-failure-investigation.md) — four parallel read-only agents converging on **a genuine intermittent test flake, not a product defect**: a fixed `Task.Delay(50)` racing a real disk-write chain. Decisive evidence — `ci.yml` passed and `release.yml` failed on the same SHA, image and minute. Local reproduction attempted and failed; disclosed rather than overstated.
- [WP 13.12.9 — Desktop Async Test Determinism Remediation](03%20Work%20Packages/WP13.12.9-desktop-async-test-determinism-remediation.md) — replaced that fixed delay with the bounded 2 s / 10 ms repository poll `TD-46` had already established for a sibling test in the same file. Test-only, one method, `+28/−2` (`7449756`); deliberately not generalised to the other 28 delays.
- [WP 13.12.10 — v0.13.0 Release Register Closure](03%20Work%20Packages/WP13.12.10-v0.13.0-release-register-closure.md) — added `v0.13.0`'s row, recorded deliberately as *tagged and merged — GitHub Release not published* rather than "Released, published", restoring `governance-healthcheck.ps1` to its pre-tag-push baseline. Documentation-only (`ea3fe07`).
- [WP 13.13.0 — v0.13.0 Release Failure Disposition & v0.13.1 Planning](03%20Work%20Packages/WP13.13.0-v0.13.0-release-failure-disposition-and-v0.13.1-planning.md) — analysis only, changing no file. Established that re-tagging is barred by Engineering Governance §7.4, that `WP 11.4B`'s tag-position exception cannot reach this case on two independent grounds, and that a new patch version is the only permitted remedy.
- [WP 13.13.1 — v0.13.1 Release Preparation](03%20Work%20Packages/WP13.13.1-v0.13.1-release-preparation.md) — prepares `v0.13.1`, the corrected and publishable form of `v0.13.0`'s content, on a branch cut from `ea3fe07` so the tagged commit `6089a218` stays in ancestry unchanged. `v0.13.0`'s tag is left immutable per Engineering Governance §7.4; `WP 11.4B`'s tag-position exception is deliberately not invoked, being bounded to a tag's mechanical position and to the window before the release branch closes. The project's first non-zero patch version.
- [WP 13.13.2 — v0.13.1 Release Closure](03%20Work%20Packages/WP13.13.2-v0.13.1-release-closure.md) — corrects the release records once `v0.13.1` was genuinely published: the Release Register row moves from "In preparation" to Released/published, carrying the merge commit, tag peel, CI run, workflow run and both asset names as independently checkable evidence rather than as a claim (`TD-42`). `v0.13.0`'s row and tag are left untouched — superseded in content, never in tag, per Engineering Governance §7.4. Documentation-only. Its own closure statement recorded 38/38 retrospectives while having none itself; that gap was found by `WP-Z4` and this article is the correction.
**Durability, Review Readiness & Command-Path Convergence — pre-programme engineering body of work (v0.14.0):**

- [TD-59 + TD-60 — Reserved-Name-Safe Persistence Boundary; Controlled Malformed-Value Reads](03%20Work%20Packages/v0.14.0-TD-59-TD-60-reserved-name-safe-persistence-boundary.md) — `PersistenceStore` now encodes reserved Win32 device stems and terminal dots into representable file names, and every store surfaces malformed values as a controlled exception rather than a raw BCL type or a silent absence (`dc46210`). A follow-up (`159d862`) fixed two of the closure tests themselves, which had asserted one file system's case-sensitivity as universal.
- [TD-58 — Outcome-Gated Refresh Architecture](03%20Work%20Packages/v0.14.0-TD-58-outcome-gated-refresh-architecture.md) — every Desktop `ActionCompleted` event now carries an `ActionOutcome`, so a refusal or failed command no longer triggers the same full rebuild as a real mutation, and macro/palette successes finally refresh the Explorer and Cockpit they previously left stale (`6135c7f`).
- [Governance — Independent Finding-Closure Verification, Register Closure, False-Claim Corrections](03%20Work%20Packages/v0.14.0-independent-finding-closure-verification.md) — a falsification report re-verifying `TD-58`–`TD-60`'s own closures and every previously-Resolved register row against the repository rather than a prior session's unrecoverable claim; catches two stale Risk Register entries and reopens two closure-test gaps as `TD-63`/`TD-64` (`ec81bf6`).
- [TD-70 + TD-71 — Responsive Workspace, Ribbon Minimisation, Drag as a Durable Preference](03%20Work%20Packages/v0.14.0-TD-70-TD-71-responsive-workspace-and-ribbon-minimisation.md) — the desktop workspace adapts to available space for the first time (measured empirically at seven widths before any change), and a `GridSplitter` drag finally updates the preference it silently used to lose (`e40a3d6`).
- [TD-84 — The Product Spine: Module → Project → Workspace as Persisted State](03%20Work%20Packages/v0.14.0-TD-84-the-product-spine.md) — implements the 2026-08-28 project-centric product decision: a project is a real `IProject` engineering object, `IProjectContext`/`IShellNavigator` make "what project is open" one immutable, invariant-enforced answer (`37788a0`). Its own restart journey exposed the durability boundary `TD-85` then fixed.
- [TD-85 — Durable Engineering Object State, Per-Type Rehydration, Removal of Projects.Index](03%20Work%20Packages/v0.14.0-TD-85-durable-engineering-object-state.md) — `ADR-0113`: `EngineeringObjectState` is written through the same persistence authority the document store already uses, and 30 canonical types rehydrate themselves through a Kind-keyed registry with no central switch (`e752368`). A closure audit (`fa01c63`) found `ReviseAsync` was silently dropping lifecycle history on a revision, harmless in memory and destructive once durable.
- [TD-89 — Project-Centric Convergence: One Definition of Project Membership](03%20Work%20Packages/v0.14.0-TD-89-project-centric-convergence.md) — corrects `TD-84`'s too-strong reading: standalone engineering work (no project) becomes a first-class scope, and `ProjectMembership` walks the durable parent chain transitively so a Part three levels down counts as project content (`cd26b8f`).
- [TD-72 — Data-Driven Workspace Layout Replacing the Compile-Time Docking Grid](03%20Work%20Packages/v0.14.0-TD-72-data-driven-workspace-layout.md) — `ADR-0095`: the compile-time three-dock grid is replaced by `WorkspaceLayoutTree`, an immutable tree of splits, tab groups and floating windows (`76a520b`). A closure verification pass (`6e38948`) found one surviving mutation — `TD-83`'s own smell, reintroduced one level up — and a real cross-platform defect (`TD-94`) hiding behind a carried-forward "environment failure" label.
- [CI — Close the Build Errors and Governance-Index Gaps That Made the Branch Red](03%20Work%20Packages/v0.14.0-ci-build-errors.md) — fixes two nullable-dereference build errors, one analyzer-flagged assertion, and five orphaned Academy/governance-index links, confirmed against real failing run `33240113523` (`7b53ce6`). The only CI workflow change this release makes.
- [TD-93 — Vocabulary Consistency Check Scanned Whatever Happened to Be Loaded](03%20Work%20Packages/v0.14.0-TD-93-vocabulary-consistency-check.md) — the check reflected over whatever assemblies the CLR happened to have loaded rather than a defined scope; now names and force-loads its three covered assemblies with a guard test, and discloses `Tempest.Samples` as an unenforceable layering exclusion rather than fixing it (`f2600d9`). `TD-93` itself stays Open.
- [CI — Make a Red Run Name the Tests That Failed](03%20Work%20Packages/v0.14.0-ci-red-run-names-failed-tests.md) — the "Publish build & test summary" step now writes every failed test's name and message to the job log and step summary, since a red run previously meant only "something failed" with the diagnosing `.trx` undownloadable under the same account-level storage quota `7b53ce6` disclosed (`416f2c8`).
- [Windows Delete-While-Locked Test Fix](03%20Work%20Packages/v0.14.0-windows-delete-while-locked-test-fix.md) — fixes a defect its own author introduced one commit earlier: the test's post-condition read ran while the file lock it depended on being released was still held. Test-only; discloses that the Windows branch still cannot be executed in this container (`44648ed`).
- [TD-31 — Attachment Content Is Durable Bytes This Platform Holds](03%20Work%20Packages/v0.14.0-TD-31-attachment-content-storage.md) — `ADR-0114`: `IBinaryPersistenceStore` is the byte shape of the same hardened `PersistenceStore`; content is written before metadata so a crash leaves unreferenced bytes rather than a lying record, and size/hash are derived from stored bytes, never accepted from a caller (`3715aa8`).
- [TD-80 — The Document and Drawing Viewer](03%20Work%20Packages/v0.14.0-TD-80-the-document-and-drawing-viewer.md) — `ADR-0115`: PDFium rasterises pages at the current zoom rather than extracting text, verified with a five-line probe before any viewer code was written (`0faf6ab`). A real-application closure pass (`3f68376`) found four defects — including no reachable Open button — that a headless test suite structurally could not see.
- [TD-102 — The Two Project Areas That Claimed to Be Implemented Now Are](03%20Work%20Packages/v0.14.0-TD-102-the-two-project-areas-that-claimed-to-be-implemented.md) — Documents and Requirements rendered a no-content `DeclaredCapabilityView` while marked `Implemented`, with no badge to say so; now real, project-scoped registers joined through `ProjectMembership` and the requirement-allocation link (`4bd6140`). Discloses `TD-103`, a permission-gating principal-boundary gap.
- [Production Rehydration and the Principal Boundary](03%20Work%20Packages/v0.14.0-ADR-0116-production-rehydration-and-the-principal-boundary.md) — `ADR-0116`: twelve engineering Kinds rehydrated only through `Tempest.Samples`, and the desktop shell established no session principal at all — both fixed at the production boundary, proven by asserting the absence of any sample-assembly dependency directly rather than merely running behaviour the dependency would also satisfy (`671a18b`). Closes `TD-103`, `TD-104`.
- [Project Tasks — the First Real Project-Management Surface](03%20Work%20Packages/v0.14.0-ADR-0117-project-tasks.md) — `ADR-0117`: `TaskWorkState` implements `IFamilySpecificState` so a finished task can reopen without weakening the canonical lifecycle's `Released → Draft` prohibition platform-wide (`007aec2`). The most useful test result was a mutation surviving because every assertion read the in-memory object it had just changed.
- [Product Gap Reconciliation Audit: Findings and Standing Evidence](03%20Work%20Packages/v0.14.0-product-gap-reconciliation-audit.md) — measuring `TD-75`'s removal directly found 70 compile errors, since real product navigation and the calculation catalogue were declared inside the sample assembly; re-scopes the row honestly and leaves `ProductGapReconciliationAuditTests` as a standing check rather than a one-time finding (`187180a`).
- [TD-75 — The Product No Longer Ships Inside Its Own Demo Harness](03%20Work%20Packages/v0.14.0-TD-75-the-product-no-longer-ships-inside-its-own-demo-harness.md) — phase 1 (`a5795bd`) moves misfiled product content out of `Tempest.Samples` and cuts the `Tempest.App` reference; phase 2 (`fdd2a2a`) removes the sample-supporting wiring phase 1 deliberately left, proven by deleting `src/Samples` outright and building clean. Twice found that `Assembly.GetReferencedAssemblies()` cannot prove a reference's absence.
- [Academy Guide — Sample Explorer Content's New Home](03%20Work%20Packages/v0.14.0-academy-guide-sample-explorer-relocation.md) — corrects the Engineering Workspace guide's stale pointer to the pre-move sample-tree namespace, keeping the historical `WP 8.1B`/`WP 8.1C` narrative intact and adding only a current-location note (`41a171a`).
- [Project Risks, Issues & Decisions — the Governance Families Get a Surface](03%20Work%20Packages/v0.14.0-project-risks-issues-and-decisions.md) — Risk, Hazard, Issue and Decision gain minimal but real mutable state (status, priority, ownership) through `IFamilySpecificState`, each preserving a distinction a shared vocabulary would flatten (Accepted-but-live, Resolved-but-open, Superseded-but-terminal) (`45d8a99`). `WorkPriority` replaces `TaskPriority` so "High" means one thing everywhere.
- [PROJECT_STATUS.md Dangling-Path Fix](03%20Work%20Packages/v0.14.0-project-status-dangling-path-fix.md) — the second stale pointer the same `TD-75` phase-2 move produced, caught by the Governance Health Check on the author's own commit rather than by re-reading (`e57f1aa`).
- [xUnit2029 Build Failure and the Vacuous Assertion Behind It](03%20Work%20Packages/v0.14.0-xunit2029-build-failure.md) — fixes the flagged assertion form, then mutation-tests the correction and finds it had been vacuous all along — the predicate could never fail for any reachable state (`562a563`).
- [Project Timeline — Milestones, Deliverables, and the Work Behind Each Date](03%20Work%20Packages/v0.14.0-project-timeline.md) — lists milestones in date order with contributing deliverables and tasks; deliberately invents no "achieved" state the domain model does not support, keeping "past with outstanding work" distinct from "past with nothing ever linked" (`f9fd9e0`).
- [TD-77 Stage 2 — Core Command Context and Binding Contract](03%20Work%20Packages/v0.14.0-TD-77-stage-2-core-command-context-and-binding-contract.md) — the additive Core contract `Command Framework Architecture.md` had already anticipated: `CommandContext`/`CommandBinding`/`CommandAvailability`, hand-written typed lambdas, no reflection (`6e3d6d5`). Binary compatibility with plugin assemblies (`ADR-0111`) forced `Binding` to be an init accessor rather than a constructor parameter.
- [TD-77 Stage 3 — Production Command Descriptors Are Bound](03%20Work%20Packages/v0.14.0-TD-77-stage-3-production-command-descriptors-are-bound.md) — all 74 production descriptors declare a real binding or a named unavailability reason; finds two commands (`requirements.delete-group`, `requirements.revise`) permanently unreachable through the old Ribbon Id-parser (`bb22983`).
- [TD-77 Stage 4 — Core Invocation Contract, Proven Against Real Bindings](03%20Work%20Packages/v0.14.0-TD-77-stage-4-core-invocation-contract.md) — adds no production code (Stage 2 had already built it); proves the registry exhaustively against all 74 real bindings instead of fixtures, and pins two `Evaluate` semantics as approved decision points rather than silently changing them (`1c38cb4`).
- [TD-77 Stage 5 — Three Surfaces Consume the Binding Contract](03%20Work%20Packages/v0.14.0-TD-77-stage-5-three-surfaces-consume-the-binding-contract.md) — the Ribbon, Command Palette and Macro Manager all ask the framework instead of guessing independently; the ~390-line `RibbonObjectActionHandlers.cs` workaround is deleted outright (`e72b933`). `TD-77`'s own register row stays Open — `WP-A1`/`WP-A2` later finish the migration.
- [Governance — The Architecture Audit Becomes Tracked Debt; TD-01's Trigger Fires](03%20Work%20Packages/v0.14.0-architecture-audit-becomes-tracked-debt.md) — eleven findings recorded as `TD-105`–`TD-115` with named dispositions rather than left in a conversation; fires `TD-01`'s own eleven-release-old revisit trigger, commissioning `WP-C` and the twelve-Work-Package remediation programme (`b796b9d`). States this backfill set's own file-naming rule.

- [WP-C — Delete the Retired v0.1 Architecture](03%20Work%20Packages/WP-C-delete-the-retired-v0.1-architecture.md) — deletes the pre-`TempestHost` architecture `Tempest.Core` had carried unreferenced since the runtime that replaced it — eight types in `dfa6ee1`, plus `ApplicationConfiguration` in `7e28f74`, a ninth orphan the deletion itself created rather than the audit found. Closes `TD-01` on its own recorded trigger, open since `WP 2.6`, and corrects the two registers that stated the deleted namespaces as current.
- [WP-B1 — Pin the Two Encodings of Per-Kind Command Eligibility](03%20Work%20Packages/WP-B1-pin-the-two-encodings-of-per-kind-command-eligibility.md) — one test class holding `AppliesToKinds` and the `WorkspaceManager` factory maps consistent where they overlap. The invariant is **directional, not equality** — Manufacturing deliberately registers Documents'/Verification's commands for its own Kinds, so a symmetric assertion would fail on correct code. Leaves the unification question genuinely open for `WP-B2`.
- [WP-D2 — One Settings Document, and Corruption That Leaves a Trace](03%20Work%20Packages/WP-D2-one-settings-document-and-corruption-that-leaves-a-trace.md) — consolidates nine hand-copied settings stores into `SettingsDocument<TDocument>` and separates degrading *safely* from degrading *silently* — six of the nine had no logger, so a torn write discarded a user's state with nothing recorded. `TD-60`'s recovery contract unchanged and now asserted. The audit said eight sites; the real count was nine.
- [WP-A1 — Close the Live Id-Only Command Path](03%20Work%20Packages/WP-A1-close-the-live-id-only-command-path.md) — migrates the last live callers of the obsolete Id-only overload and installs two guard rails. The guard found a **fourth** broken surface the audit had missed — `MainWindow`'s Macro Manager, which did not throw but ran every macro step against no context. Corrects audit finding `F-17` about the REST transport being uncomposed.
- [WP-H — Enforce the Architectural Invariants Nothing Was Holding](03%20Work%20Packages/WP-H-enforce-the-architectural-invariants-nothing-was-holding.md) — audits the enforcement surface, finds eight invariants already held, and adds five test classes for those genuinely unheld — dependency direction, the two allow-list premises (`AT-10`/`AT-23`), the `Copy`-delegation bound, and `TD-115`'s dormant commands. Consolidation into one location was rejected on the governing principle, not on taste.
- [WP-REVIEW — Clean-Machine and Physical-Review Readiness](03%20Work%20Packages/WP-REVIEW-clean-machine-and-physical-review-readiness.md) — verifies from a genuine clean clone — isolated package cache, bare `PATH` — that the product restores, builds, tests and governs cleanly, and produces `PHYSICAL_REVIEW.md`. Found `TD-116`: the Desktop cannot launch on Linux/X11 because of a deliberate security pin, isolated exactly and **deliberately not fixed**, since both remedies are somebody's decision.
- [WP-D1 — One Implementation of the Desktop Report-Then-Refresh Tail](03%20Work%20Packages/WP-D1-one-desktop-report-then-refresh-tail.md) — consolidates seven hand-written copies of status-bar/toast/history/gated-refresh into `ActionOutcomeReporter`, preserving two things a naive version would have broken: the refresh set is per-caller, and the gate reads `WorkspaceChanged`, never `Succeeded`. Corrects `TD-111`'s own wording — `ActionOutcome` never lacked a consumer; the tail lacked an implementation.
- [WP-F — Test-Suite Hygiene, and Two of the Four Findings Were Wrong](03%20Work%20Packages/WP-F-test-suite-hygiene-and-two-findings-that-were-wrong.md) — addresses `TD-114`'s four findings and records that two did not describe the repository: the repeated-host-boot premise was wrong (the cost is mandatory serialisation), and "84 weak assertions" was really 269 of which only 8 qualified. Exact counts became set assertions; one canonical `74 = 18 + 56` reconciliation was retained and completed.
- [WP-B2 — Kind Eligibility Is Two Mechanisms, One Invariant](03%20Work%20Packages/WP-B2-kind-eligibility-is-two-mechanisms-one-invariant.md) — documentary; produces `ADR-0118` and closes `TD-107` by deciding rather than changing. `F-03`'s premise does not survive contact with the code — the two encodings answer different questions, the overlap is 19 commands of 74, and the Manufacturing asymmetry is load-bearing. Three unification shapes examined and each rejected for a distinct reason.
- [WP-G — The Project CRUD Leaves MainWindow, Verbatim](03%20Work%20Packages/WP-G-the-project-crud-leaves-mainwindow-verbatim.md) — moves nineteen CRUD methods into two collaborators split along the domain service, byte-for-byte — all 19 bodies diffed against `HEAD` and identical after two mechanical edits. `MainWindow` 1,577 → 1,042 lines. Both event seams mutation-checked, because "it compiles" is not evidence the wiring still reaches the code. `TD-109` stays partially resolved, deliberately.
- [WP-A2 — The Keyboard Reaches the Canonical Path; REST Is Decided, Not Deferred](03%20Work%20Packages/WP-A2-the-keyboard-reaches-the-canonical-path.md) — takes `WP-H`'s trigger: `InputBindingRouter`'s dormancy was concealing a defect, not recording a decision, and binding any real command would have produced a key that silently did nothing. Establishes by audit that **no production command is reachable over HTTP at all**, and reclassifies `AT-10` from "not yet" to decided, including the authorship limitation.
- [WP-E — Async/Threading Hardening and the Cockpit Read Scope](03%20Work%20Packages/WP-E-async-threading-hardening-and-the-cockpit-read-scope.md) — triages every blocking call rather than sweeping them: 36 of 63 wait on an already-completed `Task`, and no deadlock is possible. Removes the two that mattered — one Cockpit refresh falls from **1,140 persistence reads to 104**. Async conversion was rejected because it would not have removed a single repeated read. Discovered `TD-117`.
- [WP-Z1 — Governance Correction](03%20Work%20Packages/WP-Z1-governance-correction.md) — closes the pre-release audit's findings: corrects a `TD-108` figure the author had written one commit earlier, normalises eight Status cells that led with "Open" while ending with their own closure, re-derives every count in two registers, and updates `PROJECT_STATUS.md` by its own retention convention. Discloses that `WP-F`'s Desktop attribute total had been wrong since it was written.
- [WP-Z2 — The Undo/Redo Toolbar Refresh Comes Back to the UI Thread](03%20Work%20Packages/WP-Z2-undo-redo-ui-thread-marshalling.md) — fixes `TD-117` in the layer that can see it — five lines in the Desktop subscriber, `Tempest.App` untouched. Produces `ADR-0119`. Corrects the defect's recorded age (`v0.10.0`, not `v0.13.1`) and its symptom: not an error dialog but a silently half-completed Undo, the data changed and every downstream refresh skipped.
- [WP-Z3 — Programme Academy Retrospective Completion](03%20Work%20Packages/WP-Z3-programme-academy-retrospective-completion.md) — creates the fifteen retrospectives the programme owed under Engineering Governance §6, including this one — §6 exempts no work package, and `WP 13.12.2` set the precedent for a work package writing its own. Resolves a count stated twice and wrong twice: "eleven" omitted `WP-REVIEW`, "thirteen" omitted it too. Zero `src/`/`tests/` files changed.
- [WP-Z4 (Stages 4–14) — TD-119 Desktop Test Synchronisation Remediation](03%20Work%20Packages/WP-Z4-td-119-desktop-test-synchronisation-remediation.md) — removes the fixed-delay synchronisation mechanism from `Tempest.Desktop.Tests`: 52 fixed waits become 1, test-only, zero `src/` changes, verified on both CI events at one identical SHA after four consecutive commits where the two events disagreed.
- [WP 11.5A — Governance Currency & Documentation Integrity](03%20Work%20Packages/WP11.5A-governance-currency-and-documentation-integrity.md) — audits `WP 11.2A`'s own disclosed gaps, finds them already remediated three releases earlier (`WP 12.9.1`/`WP 12.9.2`), fabricates no redundant fix, and instead corrects genuinely current `Governance Index.md`/`Documentation Register.md` drift it found instead. Discloses `main`'s own then-undocumented divergence from the `v0.14.0` tag, directly enabling `WP 15.1A`.
- [WP 15.0A — Desktop Shell Brand Recovery & Windows Startup Crash Fix](03%20Work%20Packages/WP15.0A-desktop-shell-brand-recovery-and-windows-startup-crash-fix.md) — recovers `Tempest.Companion`'s brand chrome into `Tempest.Desktop`; fixes a real Windows startup crash (`Dispatcher.VerifyAccess` off the UI thread after an awaited settings read resumed on a thread-pool thread). Discloses an `FCR-0092` citation this repository's own Future Capability Register does not resolve.
- [WP 15.0B — Desktop Productisation Phase 1](03%20Work%20Packages/WP15.0B-desktop-productisation-phase-1.md) — closes navigation dead-ends, placeholder data, and chrome inconsistencies found by driving the real running application, not source review alone. Discloses that its own three commits' embedded test pass-counts used a since-corrected, flawed local SDK-substitution technique and are not repeated as fact.
- [WP 15.0C — Desktop Productisation Phase 2](03%20Work%20Packages/WP15.0C-desktop-productisation-phase-2.md) — three genuinely independent fixes (Ribbon/Property Inspector hierarchy, dialog/Command Palette keyboard workflow, `DeclaredCapabilityView`/`DocumentAreaView`) dispatched as parallel, isolated-worktree background agents and merged sequentially by the lead session.
- [WP 15.0D — Ribbon Responsive Scrollbar Fix](03%20Work%20Packages/WP15.0D-ribbon-responsive-scrollbar-fix.md) — root-causes the Ribbon's horizontal scrollbar never rendering at compact widths to a `ScrollViewer`'s `Auto` visibility never growing its own container to make room for itself; fixes it with a content-driven height reservation, verified via real Xvfb interaction in both themes.
- [WP 15.1A — v0.15.0 Release Preparation & Governance Closure](03%20Work%20Packages/WP15.1A-v0.15.0-release-preparation-and-governance-closure.md) — derives `v0.15.0`'s entire scope from `git log`/`git diff` evidence alone, retroactively numbering and retrospecting `WP 15.0A`–`D` and re-filing `WP 11.5A`; bumps `VERSION` to `0.15.0`, adds `TD-121`/`TD-122`, and reviews the Future Capability Register for the first time since `v0.13.0`. Discloses, rather than fixes, that `Academy Register.md` itself was not backfilled with the five new retrospectives — the gap `WP 16.2B` closes.
- [WP 15.1B — v0.15.0 Release Readiness Review](03%20Work%20Packages/WP15.1B-v0.15.0-release-readiness-review.md) — an independent review of `WP 15.1A`'s deliverable against `main`, re-running every build, test, and governance check fresh rather than trusting the prior count. Finds one real gap — `WorkPackages.md` accounted for only 19 of the range's 20 commits, missing `b755685` — corrects it in place, and recommends `v0.15.0` **READY TO RELEASE**.
- [WP 15.2A — Desktop Test Suite Persistence Root Cleanup](03%20Work%20Packages/WP15.2A-desktop-test-suite-persistence-root-cleanup.md) — closes `TD-120`: every isolated persistence root now lives under one shared, per-run directory an `ICollectionFixture` deletes once the whole collection finishes, instead of accumulating one directory per test forever. Found and closed an adjacent gap in the same change: `ResponsiveWorkspaceTests` was the one call site among 40+ missing the collection attribute entirely. Verified empirically — 6,786 pre-existing stray directories in the session's own `/tmp` untouched, zero new ones after a 412-test run.
- [WP 16.0A — v0.16.0 Scope Decision](03%20Work%20Packages/WP16.0A-v0.16.0-scope-decision.md) — drafts and reserves six Decision Register rows (`D-021`–`D-026`), every one Proposed. Its own closing line said the programme would wait for approval; it proceeded anyway on later Product Owner direction, and the Decision Register still carries no `D-021`+ row.
- [WP 16.0B — Integrate Off-`main` Work](03%20Work%20Packages/WP16.0B-integrate-off-main-work.md) — merges `WP 15.2A`, folds the orphaned `v0.15.1` folder, and defers the Companion branch, applying two still-Proposed decisions — including an irreversible folder deletion on that basis.
- [WP 16.1A — Enforce the Release Gate](03%20Work%20Packages/WP16.1A-enforce-the-release-gate.md) — `CI Gate` now depends on the Governance Health Check, closing half of `TD-45`; the GitHub branch-protection half is handed to the Product Owner, since no session tool can configure it.
- [WP 16.1B — Health-Check Extension](03%20Work%20Packages/WP16.1B-health-check-extension.md) — the health check grows from 8 to 16 checks; its two brand-new checks immediately found a UTF-8 BOM defeating a register's own documented `grep` and a merge-artefact register drift, on their first real run.
- [WP 16.2A — Register and Status Currency](03%20Work%20Packages/WP16.2A-register-and-status-currency.md) — re-derives the governance registers directly from source and cuts `PROJECT_STATUS.md` from 565,445 to about 11,000 bytes via a diff-verified byte-identical archive, not a deletion. Closes `TD-57`.
- [WP 16.2B — Academy Retrospective Backfill](03%20Work%20Packages/WP16.2B-academy-retrospective-backfill.md) — 41 retrospectives across three parallel branches plus a closure commit reconciling counts none of the three could touch without colliding; makes release Definition of Done item 4 true for the first time since `v0.10.0`.
- [WP 16.3A — Durable State Schema Versioning: Architecture](03%20Work%20Packages/WP16.3A-durable-state-schema-versioning-architecture.md) — `ADR-0120`, closing `TD-87`'s concretely named risk with no migration required, by serialising enums as strings.
- [WP 16.3B — Durable State Schema Versioning: Implementation](03%20Work%20Packages/WP16.3B-durable-state-schema-versioning-implementation.md) — realises `ADR-0120`; rejected twice at Technical Review before landing, first for a gate that let an unmigrated record pass silently, then for a container-wide `int?` registration. Closes `TD-87`.
- [WP 16.4A — Test Determinism](03%20Work%20Packages/WP16.4A-test-determinism.md) — closes `TD-34`/`TD-83`/`TD-100`/`TD-119`; discloses that the plan's own five-run CI matrix was never obtained, because this account's runners have been unavailable since 2026-09-04.
- [WP 16.5A — Accessibility Baseline](03%20Work%20Packages/WP16.5A-accessibility-baseline.md) — six dialogs made genuinely modal, graph keyboard operability, live regions and Cockpit contrast; also records the two defects the baseline itself shipped, found by reading the shipped code.
- [WP 16.5B — Linux/X11 Avalonia Upgrade Spike](03%20Work%20Packages/WP16.5B-linux-x11-avalonia-upgrade-spike.md) — `TD-116` resolved by a timeboxed Avalonia/`Tmds.DBus.Protocol` upgrade; the result rests on one local `xvfb-run` launch, with zero real CI runs behind its advisory smoke job.

`WP 15.0A`–`D` and `WP 11.5A` were retroactively numbered and retrospected
by `WP 15.1A` (`v0.15.0` Release Preparation) — none carried a real Work
Package number, `WorkPackages.md` row, or retrospective at the time they
were merged. See `docs/releases/v0.15.0/WP15.1A v0.15.0 Release
Preparation Report.md` for the full account.

See `PROJECT_STATUS.md` for current status and
`docs/releases/v0.13.0/WorkPackages.md` for the full plan.

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
