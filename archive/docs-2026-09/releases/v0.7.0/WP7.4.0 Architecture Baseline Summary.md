# WP 7.4.0 — Release Preparation & Product Baseline — Architecture Baseline Summary

## Purpose

A snapshot of the platform's own architecture as `v0.7.0` stands ready
for Product Approval — what layers exist, how they depend on one
another, and what has changed since `v0.6.0`. No architecture was
redesigned to produce this summary; every claim below is a direct
observation of the existing, shipped structure.

## The Layer Model

TempestOS's own four-layer platform model (`ADR-0023`) gains a fifth
and sixth conceptual layer this release, both built entirely on top of
the existing four without requiring any of them to change:

```
Engineering Discipline Modules      (not yet built — Mechanical, HVAC,
                                      Structural, Electrical all remain
                                      at "no identified capability")
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

Each layer depends only downward. Confirmed directly, not assumed:

- `Tempest.Core.Requirements` depends on `Tempest.Core.EngineeringData`,
  `Tempest.Core.Verification`, `Tempest.Core.Identity`,
  `Tempest.Core.Persistence` — zero dependency on any Engineering
  Discipline Module (none exist yet) and zero circular reference back
  from any of its own dependencies.
- Each of the five Engineering Core frameworks depends on
  `Tempest.Core.EngineeringData` and, where needed,
  `Tempest.Core.UnitsAndQuantities` — none depends on Requirements, and
  none depends on a sibling Engineering Core framework except through
  the Data Model they all share.
- Zero Platform Service depends on any Engineering Core or Systems
  Engineering Foundation type — the dependency arrow points only one
  way.

**Zero circular dependencies, zero layering violations, confirmed by
direct project- and namespace-reference inspection.**

## What Changed This Release

- **Engineering Core (new).** Five frameworks, one shared storage
  substrate (`IEngineeringDocumentStore`, itself built directly on the
  pre-existing `IPersistenceStore` — no new storage abstraction was
  introduced at any point across all five).
- **Systems Engineering Foundation (new, first capability).** The
  Requirements Engine — the first Systems Engineering Foundation
  capability to reach running code, following a dedicated architecture
  (`WP 7.2B`) and contract review (`WP 7.2C`) phase.
- **Platform Services (unchanged).** No existing Platform Service
  contract was modified. `RequirementsService` is registered alongside
  its Engineering Core siblings as the newest Platform Service, in
  `TempestHost.cs`'s own Phase 6 block, following the identical
  registration convention every prior Platform Service uses.
- **Runtime Foundation (unchanged).** Zero changes to Discovery,
  Registration, Lifecycle, DI, Configuration, Logging, the Host, Event
  Bus, Background Services, Navigation, Command Framework, Diagnostics,
  or Plugin Manifest.

## Key Architectural Decisions This Release

| ADR | Decision | Framework |
|---|---|---|
| ADR-0053 | Built directly on `IPersistenceStore`; no new storage abstraction | Engineering Data Model |
| ADR-0054 | `double`-backed quantities, no DI registration, stateless converter | Units & Quantities |
| ADR-0055 | Structured, provenance-carrying property type; direct `IPersistenceStore` dependency for identifier index | Materials |
| ADR-0056 | `Calculate` signature extended with `CalculationContext`; convention-only purity enforcement | Calculations |
| ADR-0057 | Verification history queried via existing `LinkAsync`/`GetReferencesAsync`; no new index | Verification |
| ADR-0058 | Platform Service classification; Engineering Data Model reuse | Requirements |
| ADR-0059 | Independent representation decisions: closed-enum status, string identifier + index, open-string category | Requirements |
| ADR-0060 | No compare-and-swap concurrency mechanism; accepted as `TD-25` | Requirements |
| ADR-0061 | No internal permission gating; calling-layer enforcement, mirroring Materials/Calculations | Requirements |

**A recurring, cross-framework architectural pattern**: every one of
these nine ADRs independently reaches "reuse the Data Model's existing
mechanism, introduce nothing new" as its own central decision — the
single most consistent architectural finding across the entire release.

## Dependency Graph Integrity

Verified directly against `.csproj` project references and namespace
`using` statements — no tool-assisted cycle detection was needed,
since the dependency count remains small enough for direct inspection:

- `Tempest.Core` — the foundation; depends on nothing else in this
  repository.
- `Tempest.Samples` — depends only on `Tempest.Core`.
- `Tempest.App` — depends on `Tempest.Core` and `Tempest.Samples`.
- `Tempest.Core.Tests` — depends on all three.
- `TempestSampleModule` (the `dotnet new` template source) — depends
  only on `Tempest.Core`, independent of `Tempest.Samples`.

Zero circular project references. Zero namespace cycles within
`Tempest.Core` — the Engineering Core and Systems Engineering namespaces
form a strict, one-directional dependency chain, confirmed by direct
inspection of every `using` statement in `Tempest.Core.Requirements`
against `Tempest.Core.EngineeringData` and `Tempest.Core.Verification`.

## Security Architecture Posture

Three dedicated Security Reviews this release (`WP 7.1D`, `WP 7.1E`,
`WP 7.3A`), each reviewing 14 named dimensions against real, shipped
code — zero Release Blocking findings across all three. The platform's
own authorization enforcement point (`IPermissionEvaluator`, `ADR-0044`)
remains the single point of truth; every Engineering Core framework and
the Requirements Engine either leaves enforcement to the calling layer
(Materials, Calculations, Requirements — `ADR-0055`, `ADR-0056`,
`ADR-0061`) or gates internally only where the data itself is
evidentiary (Verification's own history, `ADR-0057`) — a now-explicit,
reusable deciding test (`ADR-0061`) rather than an ad hoc choice per
framework.

## Verdict

The architecture baseline as of `v0.7.0` is sound: zero circular
dependencies, zero layering violations, a consistent and now
well-evidenced reuse pattern across six independent frameworks, and
three clean dedicated Security Reviews. No architectural change is
recommended before Product Approval.

## Related Documents

`docs/releases/v0.7.0/WP7.4.0 Release Readiness Report.md`;
`ADR-0023` (the four-layer platform model); `ADR-0053`–`ADR-0061`;
`docs/architecture/Platform Service Map.md`;
`docs/academy/02 Runtime Architecture/15-engineering-data-model.md`;
`docs/academy/02 Runtime Architecture/16-requirements-engine.md`.
