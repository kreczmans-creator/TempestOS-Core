# TempestOS v0.6.0 — Release Architecture
## "Platform Services"

## Status

**Architecture — design phase.** No implementation exists. This
document is the release-wide architectural review required before any
of the nine Work Packages in `docs/releases/v0.6.0/WorkPackages.md`
begins implementation, mirroring the role `docs/releases/v0.4.0/
Architecture.md` played for the Platform Foundation and the role
`WP 5.0A`/`WP 5.0C`/`WP 5.1A`'s own individual architecture phases played
for `v0.5.0` — except scoped to an entire release's worth of services at
once, since `v0.6.0` is the first release to introduce this many
genuinely new platform services in parallel.

This document, together with `Platform Services Overview.md`, `Platform
Service Dependency Diagram.md`, `Public Interface Catalogue.md`,
`Service Lifecycle.md`, `Required ADRs.md`, `Risk Register.md`, and
`Technical Debt Assessment.md`, constitutes the complete v0.6.0
architecture package. Each individual Work Package is still expected to
run its own dedicated architecture phase where warranted (mirroring the
`WP 5.0A`/`WP 5.0B`, `WP 5.0C`/`WP 5.0D`, `WP 5.1A`/`WP 5.1B` precedent)
before its own implementation begins — this package establishes the
release-wide shape and cross-service integration those later phases must
build within, not a substitute for them.

## Objective

Produce the complete architectural design for `v0.6.0`: how nine new
Work Packages — Reporting, Permissions & Identity, Notifications, a REST
API, Settings, Audit, Licensing, Export/Import, and a closing Integration
Review — each fit the existing four-layer platform model (`ADR-0023`),
integrate with the Runtime Host, the Plugin Framework, and the Command
Framework without weakening any of them, and compose as one coherent
platform rather than eight independently-designed additions that happen
to coexist.

## Repository Investigation

Before designing anything, this review confirmed the actual state of
every system these nine Work Packages will touch, rather than assuming
it from `docs/releases/v0.6.0/WorkPackages.md`'s own provisional
language:

- **No platform service in this codebase currently persists anything.**
  The only persistence-capable code is the bootstrap-era
  `JsonProjectRepository`/`ProjectModel` (`Tempest.Core.Repositories`/
  `.Projects`), confirmed dead — unreferenced by `Program.cs` since
  `WP 5.0D`, and explicitly not revived by `WP 5.2`'s own `TD-01`
  decision. Every one of `WP 6.4` (Settings), `WP 6.5` (Audit), and
  `WP 6.7` (Export/Import) needs some form of durable storage, and none
  of them can reuse anything that exists today.
- **Zero real hosted services ship as of `v0.5.0`** (`AT-07`,
  `Technical Debt Register.md`) — the Background Services infrastructure
  (`WP 4.5`) is complete and tested, but has never had a real consumer.
  `WP 6.3` (REST API) is a long-running background process by its very
  nature (start after Module Initialisation, run until shutdown, stop
  before Module Disposal) — exactly the shape `IHostedService` was built
  for.
- **`TD-09`/`TD-10`/`TD-11` remain open** (plugin isolation; Navigation
  ownership; Command/Navigation registration-order squatting), each
  explicitly triggered on "real third-party plugin support" or "an
  ownership/priority/reservation model." `WP 6.1` (Permissions &
  Identity) is the first Work Package in this project's history with a
  genuine reason to build an authorization concept — the natural, and
  possibly only reasonable, place to finally resolve all three.
- **Four independent platform services already reached the identical
  "DI-public, Composition-Root or container-constructed, no
  orchestration authority" shape** — the Event Bus, Navigation, the
  Command Framework, and Diagnostics (`v0.4.0`/`v0.5.0`). This is not
  incidental; it is now the established default for any new service that
  carries no orchestration authority of its own, per every one of those
  four ADRs' own independent reasoning.
- **No existing platform service depends on a NuGet package or an
  external framework beyond the bare .NET SDK.** `ADR-0005`'s own
  "build a custom, minimal container" philosophy has held for every
  Runtime Foundation and Platform Foundation service. `WP 6.3` (REST API)
  is the first Work Package with a plausible, well-justified reason to
  depart from this — hand-rolling an HTTP/1.1 listener, TLS, and request
  routing from raw sockets would be an enormous, unjustified undertaking
  compared to the ASP.NET Core hosting stack already bundled with the
  .NET SDK (Kestrel is part of the shared framework, not a third-party
  NuGet dependency in the sense `ADR-0005` was written to avoid).

## The Single Most Consequential Finding: A Shared Persistence Abstraction

**None of the nine Work Packages named in `WorkPackages.md` is
"Persistence."** Yet `WP 6.4`, `WP 6.5`, and (partially) `WP 6.6` each
need durable storage, and nothing in the current plan says whether each
builds its own, incompatible mechanism or shares one.

**Recommendation, not a new Work Package.** Mirroring `D-016`'s own
precedent (`WP 5.0C`'s scope growing organically once Repository
Investigation found `Tempest.App` still did not consume the platform,
without inventing a new Work Package number or renumbering anything),
this review recommends that **`WP 6.4` (Settings) establish a minimal,
shared persistence abstraction — `Tempest.Core.Persistence`,
`IPersistenceStore` — as part of its own scope**, since Settings is the
first Work Package in dependency order that needs one. `WP 6.5` (Audit)
then depends on it explicitly, rather than inventing a second storage
mechanism. This is recorded as a required ADR (`ADR-0041`, see `Required
ADRs.md`) precisely because it is a genuine architectural decision with
a real alternative (each service builds its own storage) that this
review explicitly rejects.

**`WP 6.6` (Licensing) deliberately does not depend on this
abstraction.** License validation must be capable of running before the
DI container exists (see Service Lifecycle, and `ADR-0050`) — the
identical timing constraint `WP 5.2`'s own `Func<T>` accessor pattern
solved for Diagnostics, except here the cleaner answer is for Licensing
to own a single, simple file-based source directly, mirroring Platform
Version's own "deliberately a leaf" position (`ADR-0023`), rather than
depend on a container-resolved service that cannot yet exist at the
point Licensing itself must run.

**`WP 6.7` (Export/Import) also does not depend on this abstraction.**
`IPersistenceStore` is *internal* platform state the platform itself
owns and manages (settings values, audit records) — not a Stream-based,
user-directed artifact a person explicitly chooses where to save. Export/
Import's own job is producing and consuming portable files; it reads
*from* whatever service owns the data being exported (Settings, a
Reporting definition, and so on), not from the internal store directly.
This distinction — internal managed state vs. user-directed portable
artifacts — is genuine and is recorded in `ADR-0051`.

## Layer Classification (ADR-0023)

Every new service below sits at the **Platform Services** layer —
consumed by Modules and Platform APIs, consuming Dependency Injection and
(where named) other Platform Services, never containing module-specific
business logic, and never sitting below the Runtime Host. None
introduces a fifth layer or blurs the existing three-layer-plus-Host
model.

| Service | New Namespace | DI Classification | Orchestration Authority |
|---|---|---|---|
| Reporting | `Tempest.Core.Reporting` | DI-public, container-constructed | None |
| Persistence | `Tempest.Core.Persistence` | DI-public, container-constructed | None |
| Settings | `Tempest.Core.Settings` | DI-public, container-constructed | None |
| Identity & Permissions | `Tempest.Core.Identity` | DI-public, container-constructed | None |
| Notifications | `Tempest.Core.Notifications` | DI-public, container-constructed | None |
| Audit | `Tempest.Core.Audit` | DI-public, container-constructed | None |
| Licensing | `Tempest.Core.Licensing` | DI-public (`ILicenseProvider`), Composition-Root-constructed (`ILicenseValidator`) | None — read-only after startup validation |
| Export/Import | `Tempest.Core.ExportImport` | DI-public, container-constructed | None |
| REST API | `Tempest.Core.Api` | Host-owned Hosted Service (`IHostedService`), discovered exactly like any other hosted service | None beyond its own request/response lifecycle |

No service in this table gains a path back into Discovery, Registration,
Lifecycle, Plugin Discovery/Loading, or Hosted Service orchestration —
`ADR-0017`'s boundary is preserved by every one of the nine Work
Packages without exception, confirmed service by service in `Service
Lifecycle.md`.

## Integration With the Runtime, Host, Plugin System, and Command Framework

**Runtime & Host.** Every DI-public service above registers during the
existing Platform Services Registered phase (Phase 6) — no new `Host
Lifecycle.md` phase is required for any of Reporting, Persistence,
Settings, Identity, Notifications, Audit, or Export/Import. Licensing's
own validation runs within Phase 6 as well, Host-fatal if invalid
(`ADR-0013`'s existing platform-service-failure classification, applied
without modification). The REST API is the sole exception, and is not a
genuine exception at all — `IHostedService` implementations already have
two named phases (`8.1`/`10.1`, `ADR-0030`), and the REST API simply
becomes that infrastructure's first real consumer, exactly as `AT-07`
anticipated.

**Plugin System.** Every DI-public service above is reachable by a
plugin-loaded module through ordinary constructor injection, identically
to a first-party module — no special-casing, mirroring the Event Bus/
Navigation/Command Framework/Diagnostics precedent exactly. This is
where `WP 6.1`'s own stakes are highest: a plugin-loaded module gains the
identical ability to check (and, if `WP 6.1` does not resolve `TD-09`
first, potentially bypass) permissions that a first-party module has.
`Required ADRs.md`'s `ADR-0044` addresses this directly.

**Command Framework.** Reporting, Notifications, and the REST API each
have a natural Command Framework integration point: a
`GenerateReportCommand` (Reporting), and REST endpoints that dispatch
*through* `ICommandRegistry.InvokeAsync` rather than inventing a second
invocation mechanism (`ADR-0048`) — directly realising the Command
Framework's own original design intent (`Command Framework
Architecture.md`: "a menu, a toolbar, a keyboard shortcut, ... or a
future automation/AI service" — the REST API is that future caller,
arriving exactly as anticipated). Neither the Command Framework nor any
of these new services is modified to make this work; the Command
Framework's own Id-keyed registry already supports exactly this.

## Cross-Service Orthogonality Decisions

Every pair of new/existing services that could plausibly be confused
with one another has an explicit, written boundary — the same
discipline `ADR-0022` (Navigation/Commands) and `ADR-0037`/`RD-0039`
(Commands/Event Bus) already established:

| Pair | Boundary |
|---|---|
| Settings vs. Configuration | Configuration is read-only, immutable, loaded once at startup (`ADR-0009`, Case Study 05). Settings is read-write, at runtime, by design. Never the same concept. |
| Notifications vs. Event Bus | An event has zero-or-more subscribers and no delivery/presentation guarantee. A notification is *produced from* an event and has an intended recipient and presentation. Notifications are built on the Event Bus, not a replacement for it. |
| Audit vs. Logging vs. Diagnostics | Logging is developer-facing, diagnostic, not guaranteed durable. Diagnostics is a live snapshot of *current* state. Audit is a durable, queryable *history* of actions, attributable to an actor. All three may describe the same underlying event; each answers a different question. |
| Reporting vs. Export/Import | Reporting produces presentation-oriented output (may be lossy, e.g. rendered PDF/HTML). Export/Import produces round-trip-safe, versioned data. A report is not guaranteed re-importable; an export is. |
| Persistence vs. Export/Import | Persistence is internal, platform-managed state. Export/Import is user-directed, portable artifact I/O. Export/Import reads *from* whatever service owns the data (including, indirectly, Persistence-backed services), never from `IPersistenceStore` directly. |

## Required ADRs — Summary

Twelve new ADRs are required (`ADR-0040` through `ADR-0051`) — see
`Required ADRs.md` for the complete list with context and anticipated
decision for each. None is written as Accepted here: each is a
release-wide anticipated decision, to be formally authored (and, where
warranted, challenged under Technical Review) during its own owning Work
Package's dedicated architecture phase, exactly as `WP 5.1A`'s own
`ADR-0036`–`ADR-0038` were authored during a dedicated phase rather than
during `v0.5.0`'s own original release planning.

## Risks — Summary

See `Risk Register.md` for the complete register. The two highest-risk
items mirror `v0.5.0`'s own experience directly: `WP 6.1` (Permissions &
Identity) has no existing architectural grounding, exactly as Navigation
did not before `WP 5.0A`; and `WP 6.3` (REST API) is this platform's
first network-facing surface and first substantial external framework
dependency, together representing more architectural novelty in one Work
Package than any single Work Package in `v0.4.0` or `v0.5.0` carried.

## Technical Debt Implications — Summary

See `Technical Debt Assessment.md` for the complete assessment. In
summary: `WP 6.3` retires `AT-07` (zero real hosted services); `WP 6.1`
is positioned to retire `TD-09`, `TD-10`, and `TD-11` together, for the
first time since they were disclosed; and this release is expected to
disclose new, accepted trade-offs of its own (an initially local-only
Licensing model; an initially coarse-grained REST API authorization
model), each named explicitly in advance rather than discovered
mid-implementation.

## Validation Against Governing Documents

- **`FOUNDATION.md`.** Every one of the nine non-negotiable principles
  holds: each new service has exactly one reason to change (②); no
  service introduces mutable, externally-writable state without a
  dedicated owner (③ — Persistence and Settings both name a single
  owning service for their own state, never granting write access
  directly); the platform-service/module failure boundary is exercised,
  not reopened (④ — Licensing's Host-fatal classification is `ADR-0013`
  applied, not a new category); disposal guarantees are unaffected (⑤);
  no new interruption/atomicity question is introduced beyond what the
  Atomic Phase Principle already governs (⑥); every genuine architectural
  decision is named for its own ADR, not left implicit (⑦); no tier of
  authority is bypassed — this review recommends, it does not itself
  accept, any of the twelve required ADRs (⑧); every dependency named in
  `Platform Service Dependency Diagram.md` points downward (⑨).
- **Engineering Governance §5.** Every one of the twelve required ADRs
  meets at least one criterion: a genuine alternative was seriously
  considered and rejected for each (see `Required ADRs.md`'s own
  "Alternatives Considered" note per entry).
- **`docs/security/Security Roadmap.md`.** Items 6 (Permissions &
  Identity), 7 (API and networking exposure), and 8 (Licensing) are each
  addressed by a dedicated Work Package in this release, exactly as that
  document's own "How to Use This Roadmap" instructs: "check this
  roadmap first... the corresponding design work is a prerequisite for
  that Work Package, not an afterthought."

## Documentation Impact

**New**: this document; `Platform Services Overview.md`; `Platform
Service Dependency Diagram.md`; `Public Interface Catalogue.md`;
`Service Lifecycle.md`; `Required ADRs.md`; `Risk Register.md`;
`Technical Debt Assessment.md` (all `docs/releases/v0.6.0/`). **Not
created in this pass**: any file under `docs/adr/` (each required ADR is
authored formally during its own owning Work Package's dedicated
architecture phase, not here); any per-service `docs/architecture/`
document (each is created the same way, once that service's own
architecture phase runs — mirroring `Navigation Framework
Architecture.md`/`Command Framework Architecture.md`'s own origin).
**Update expected once implementation begins**: `docs/architecture/
Platform Service Map.md`, `Ownership Matrix.md`, and `Engineering
Glossary.md`, exactly as every previous new platform service required —
not performed here, since no service is implemented yet.

## Related Documents

`docs/releases/v0.6.0/WorkPackages.md`; `Platform Services Overview.md`;
`Platform Service Dependency Diagram.md`; `Public Interface Catalogue.md`;
`Service Lifecycle.md`; `Required ADRs.md`; `Risk Register.md`;
`Technical Debt Assessment.md`; `docs/releases/FOUNDATION.md`;
`docs/academy/06 Engineering Standards/Engineering Governance.md`;
`docs/security/Security Roadmap.md`; `docs/security/Threat Model.md`;
`docs/governance/Quality/Technical Debt Register.md`; `ADR-0005`,
`ADR-0009`, `ADR-0013`, `ADR-0017`, `ADR-0022`, `ADR-0023`, `ADR-0030`,
`ADR-0034`, `ADR-0037`, `D-016`.
