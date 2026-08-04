# WP 8.9.0 — Release Preparation & Product Baseline — Architecture Baseline Summary

## Purpose

A snapshot of the platform's own architecture as `v0.8.0` stands ready
for Product Approval — what layers exist, how they depend on one
another, and what has changed since `v0.7.0`. No architecture was
redesigned to produce this summary; every claim below is a direct
observation of the existing, shipped structure.

## The Layer Model

TempestOS's own layer model gains two new members this release, each
occupying a distinct position, neither requiring any existing layer to
change:

```
Engineering Discipline Modules      (not yet built — first candidate:
                                      Physical/Configuration, now buildable
                                      directly against WP 8.2C's own
                                      compiled classes)
        ↑ consumes
Tempest.Core.EngineeringDomain      (NEW, WP 8.2A–8.2C — shared canonical
                                      vocabulary: 83 compiled contract types,
                                      38 concrete object classes, reused by
                                      every future discipline module)
        ↑ consumes
Systems Engineering Foundation      (Requirements — Tempest.Core.Requirements)
        ↑ consumes
Engineering Core                    (Data Model, Units & Quantities,
                                      Materials, Calculations, Verification)
        ↑ consumes
Platform Services                   (Identity, Settings, Persistence,
                                      Audit, Notifications, Reporting,
                                      REST API, Export/Import, Licensing)
        ↑ consumes
Runtime Foundation                  (Discovery, Registration, Lifecycle,
                                      DI, Configuration, Logging, Host,
                                      Event Bus, Background Services,
                                      Navigation, Command Framework,
                                      Diagnostics, Plugin Manifest)
```

`Tempest.App.Workspace` (the Engineering Workspace, `WP 8.0A`–`WP 8.1C`)
sits **beside** this stack, not inside it — a second, additive
presentation layer over `Tempest.App`'s own composition root, parallel
to console `TempestShell`, consuming Platform Services and Engineering
Core reads directly rather than introducing a new layer of its own
(`ADR-0062`).

Each layer depends only downward. Confirmed directly, not assumed:

- `Tempest.Core.EngineeringDomain` depends on `Tempest.Core.EngineeringData`
  and, indirectly at implementation time only, `Tempest.Core.Identity` —
  zero dependency on any discipline framework
  (`Requirements`/`Verification`/`Materials`/`Calculations`) and zero
  dependency on `Tempest.App` or `Tempest.App.Workspace`.
- None of `Tempest.Core.Requirements`/`Verification`/`Materials`/
  `Calculations` reference `Tempest.Core.EngineeringDomain` — correct;
  no discipline framework has yet been asked to consume the new shared
  vocabulary, and none was silently coupled to it either.
- `Tempest.App.Workspace` depends on Platform Services and Engineering
  Core reads via `ITempestHost.Services`, exactly as `ADR-0062`/`ADR-0063`
  specify — zero dependency on `Tempest.Core.EngineeringDomain` (the
  Workspace does not yet render any Engineering Domain object; its own
  Project Explorer content remains the fictional sample tree `WP 8.1B`
  introduced).

**Zero circular dependencies, zero layering violations, confirmed by
direct project- and namespace-reference inspection.**

## What Changed This Release

- **Engineering Domain (new).** A shared canonical vocabulary layer,
  sitting between the Engineering Data Model and every discipline
  framework — 83 compiled contract types (`WP 8.2B`), 38 concrete object
  classes over one shared `EngineeringObjectBase` (`WP 8.2C`), a new,
  purely in-memory repository layer, and two generic factory types. Every
  canonical object's own real storage reuses the existing, shared
  `IEngineeringDocumentStore` — zero new persistence mechanism
  introduced.
- **Engineering Workspace (new, first presentation-layer capability).**
  A five-region graphical-in-spirit, terminal-realised shell
  (`ADR-0066`), now `Tempest.App`'s own default launch target
  (`ADR-0068`), with working Navigation, a Project Explorer, and an
  Engineering Cockpit landing screen answering "where am I / what needs
  attention / is the project healthy / what next."
- **Platform Services (unchanged).** No existing Platform Service
  contract was modified; zero new Platform Service registered — the
  Workspace and the Engineering Domain are each, by design, outside this
  category (`ADR-0062`, `WP8.2A Engineering Domain Architecture.md` §1).
- **Runtime Foundation (unchanged).** Zero changes to Discovery,
  Registration, Lifecycle, DI, Configuration, Logging, the Host, Event
  Bus, Background Services, Navigation, Command Framework, Diagnostics,
  or Plugin Manifest.
- **Engineering Core / Systems Engineering Foundation (unchanged,
  functionally).** `Requirements`/`Verification`/`Materials`/
  `Calculations` are untouched by this release — `WP 8.2C` deliberately
  gave their five already-Implemented canonical Kinds no competing
  concrete realisation (`ADR-0078`), leaving ownership exactly where
  `WP 8.2A` placed it.

## Key Architectural Decisions This Release

| ADR | Decision | Area |
|---|---|---|
| `ADR-0062`–`ADR-0065` | Zero new Platform Service; reads via `ITempestHost.Services`; mutations via the Command Framework; layout persists via `ISettingsProvider` | Workspace Architecture |
| `ADR-0066`/`ADR-0067` | Terminal-based presentation, not a graphical framework; Kind-keyed registration for views/explorer nodes | Workspace Contracts |
| `ADR-0068` | `Tempest.App`'s own default launch target is the Workspace | Workspace Shell |
| `ADR-0069`/`ADR-0070` | Engineering Cockpit is the default landing screen; Command Palette is a first-class global entry point | Workspace UX |
| `ADR-0071` | Corrects a worked example inside `ADR-0067` — a discovered module cannot reach `IWorkspaceManager` directly | Navigation & Project Explorer |
| `ADR-0072`–`ADR-0074` | Kind-backed identity over `IEngineeringDocumentStore`; open-string relationships; common, per-family-specialised lifecycle vocabulary | Engineering Domain Architecture |
| `ADR-0075`/`ADR-0076` | Facet-composed contracts, never a monolith; one generic relationship interface, never per-category types | Engineering Domain Contracts |
| `ADR-0077`–`ADR-0079` | Shared services reuse the existing document store; the five already-Implemented Kinds get no competing realisation; factories are generic types, instantiated once per Kind | Engineering Domain Implementation |

**A recurring, cross-Work-Package architectural pattern, now proven a
third time at a new scale**: every one of these fifteen ADRs
independently reaches "reuse what already exists, introduce nothing
new" as its own central decision — the identical pattern
`v0.7.0`'s own Architecture Baseline Summary first named for six
frameworks, now confirmed holding across presentation-layer and
shared-vocabulary-layer work too, not only discipline frameworks.

## Dependency Graph Integrity

Verified directly against `.csproj` project references and namespace
`using` statements:

- `Tempest.Core` — the foundation; depends on nothing else in this
  repository. Zero new `PackageReference`/`FrameworkReference` entries
  this release.
- `Tempest.Samples` — depends only on `Tempest.Core`.
- `Tempest.App` — depends on `Tempest.Core` and `Tempest.Samples`; zero
  `PackageReference` entries (confirming `ADR-0066` holds).
- `Tempest.Core.Tests` — depends on all three.

Zero circular project references. Zero namespace cycles within
`Tempest.Core` — `Tempest.Core.EngineeringDomain` forms a strict,
one-directional dependency onto `Tempest.Core.EngineeringData` only,
confirmed by direct inspection of every `using` statement.

## Security Architecture Posture

**Zero dedicated Security Reviews this release** — a genuine, disclosed
departure from `v0.7.0`'s own three-review standard (`WP 7.1D`,
`WP 7.1E`, `WP 7.3A`), and from that release's own explicit
recommendation to continue it for every future implementation Work
Package. Mitigating factors, disclosed rather than assumed: zero new
external attack surface was introduced this release (no new REST
endpoint, no new authentication path, no new persistence technology);
every new component either reuses already-security-reviewed
infrastructure (`IEngineeringDocumentStore`, `ICurrentPrincipalAccessor`)
unmodified, or is a terminal-rendered presentation layer with no network
exposure (`ADR-0066`). `IEngineeringObjectRepository`/
`IEngineeringRelationshipRepository` introduce no new authorization
surface — they carry no permission model of their own, mirroring
`Materials`'/`Calculations`' own established "calling-layer enforcement"
pattern (`ADR-0061`'s own deciding test, applied without a fresh
Security Review to re-derive it). Weighed explicitly, not silently
assumed sufficient, in the Product Approval Report.

## Verdict

The architecture baseline as of `v0.8.0` is sound: zero circular
dependencies, zero layering violations, a consistent and now
three-times-confirmed reuse pattern. One genuine, disclosed process gap
(zero dedicated Security Reviews) is named explicitly rather than
hidden, and weighed in the Product Approval Report rather than silently
treated as immaterial. No architectural change is recommended before
Product Approval.

## Related Documents

`docs/releases/v0.8.0/WP8.9.0 Release Readiness Report.md`;
`docs/releases/v0.8.0/WP8.9.0 Workspace Baseline Summary.md`;
`docs/releases/v0.8.0/WP8.9.0 Engineering Domain Baseline Summary.md`;
`ADR-0062`–`ADR-0079`; `docs/architecture/Platform Service Map.md`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`.
