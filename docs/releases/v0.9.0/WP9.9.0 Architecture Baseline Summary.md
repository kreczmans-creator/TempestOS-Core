# WP 9.9.0 — Release Preparation & Product Baseline — Architecture Baseline Summary

## Purpose

A snapshot of the platform's own architecture as `v0.9.0` stands ready
for Product Approval — what layers exist, how they depend on one
another, and what has changed since `v0.8.0`. No architecture was
redesigned to produce this summary; every claim below is a direct
observation of the existing, shipped structure.

## The Layer Model

`v0.9.0` adds **zero new layers** — the entire release is delivered by
populating two layers `v0.8.0` already introduced but left largely
empty (the Engineering Workspace, and consumption of the Engineering
Domain), never by adding a new one:

```
Engineering Discipline Modules      (SIX NOW REAL this release — Mechanical,
                                      Requirements, Calculations, Documents,
                                      Verification, Manufacturing — all
                                      built directly against WP 8.2C's own
                                      compiled Engineering Domain classes,
                                      wired into the Workspace, Cockpit,
                                      and Digital Thread)
        ↑ consumes
Tempest.App.Workspace               (POPULATED this release — real Project
                                      Explorer areas, Property Inspector
                                      facets, commands, and Cockpit KPIs
                                      for all six disciplines; was, at
                                      v0.8.0, still the fixed sample tree)
        ↑ consumes
Tempest.Core.EngineeringDomain      (WP 8.2A–8.2C, unchanged this release
                                      except three additive facets —
                                      shared canonical vocabulary: 168
                                      compiled contract types, 38 concrete
                                      object classes, now genuinely
                                      consumed by six real disciplines
                                      instead of zero)
        ↑ consumes
Systems Engineering Foundation      (Requirements — Tempest.Core.Requirements,
                                      +1 additive interface this release)
        ↑ consumes
Engineering Core                    (Data Model, Units & Quantities,
                                      Materials, Calculations, Verification
                                      — unchanged this release)
        ↑ consumes
Platform Services                   (Identity, Settings, Persistence,
                                      Audit, Notifications, Reporting,
                                      REST API, Export/Import, Licensing
                                      — unchanged this release)
        ↑ consumes
Runtime Foundation                  (Discovery, Registration, Lifecycle,
                                      DI, Configuration, Logging, Host,
                                      Event Bus, Background Services,
                                      Navigation, Command Framework,
                                      Diagnostics, Plugin Manifest
                                      — unchanged this release)
```

Each layer depends only downward. Confirmed directly, not assumed:

- `Tempest.App.Workspace.{Mechanical,Requirements,Calculations,Documents,
  Verification,Manufacturing}` each depend on `Tempest.Core.EngineeringDomain`
  directly, plus their own relevant Engineering Core framework
  (`ICalculationEngine`, `IVerificationService`) — zero dependency on
  `Tempest.Samples` from any of the six (the identical, already-established
  "Workspace-layer composition helper, no Domain-layer registry"
  boundary every discipline observes).
- `Tempest.App.Workspace.Manufacturing` additionally depends on
  `Tempest.App.Workspace.Documents`/`.Verification` directly — the one
  new intra-`Tempest.App` namespace dependency this release introduces
  (`WP 9.5A`'s own disclosed cross-Work-Package facet/view provider
  reuse), confirmed one-directional by direct `grep` (neither `.Documents`
  nor `.Verification` references `.Manufacturing` anywhere).
- `Tempest.Core.EngineeringDomain` continues to depend on
  `Tempest.Core.EngineeringData` and, at implementation time,
  `Tempest.Core.Identity` only — zero new dependency added this release.
- **One pre-existing, already-disclosed cross-framework dependency
  reconfirmed:** `Tempest.Core.Requirements` (`IRequirementValidationService`,
  `WP 9.1A`) references `Tempest.Core.EngineeringDomain` directly, reusing
  `IValidationResult`/`IValidationDiagnostic` — the one exception to
  "no discipline framework depends on the Engineering Domain," disclosed
  by `WP 9.1A` at the time, not newly found.

**Zero circular dependencies, zero layering violations, confirmed by
direct project- and namespace-reference inspection.**

## What Changed This Release

- **Engineering Workspace (populated, not redesigned).** Every one of
  the five Kind-keyed extension points `ADR-0067`/`ADR-0082` established
  (`RegisterExplorerArea`/`RegisterView`/`RegisterFacetProvider`/
  `RegisterHandler`/`RegisterDescriptor`) is now exercised by six real
  disciplines, none needing the frozen contract itself to change.
  `EngineeringCockpit`'s own placeholder cards (`WP 8.1C`) are now real,
  derived reads for five of six disciplines, plus Mechanical's own
  original `WP 8.1C`-era generic reads.
- **Engineering Domain (extended additively, never redesigned).** Three
  new facets this release (`IHasBomLine` — `WP 9.0B`; plus the three
  structural-mutation facets `IRenamable`/`IHasParent`/`IDeletable` —
  `WP 9.0A`) — the only `Tempest.Core.EngineeringDomain` changes across
  all seven Work Packages. The four Manufacturing-family classes
  (`ManufacturingOperation`/`WorkInstruction`/`Inspection`, `WP 8.2C`)
  and every other canonical Kind this release wires up were already
  compiled, untouched.
- **Requirements Framework (extended additively).** One new interface
  (`IRequirementValidationService`, `WP 9.1A`) — the only
  `Tempest.Core.Requirements` change this release.
- **Platform Services (unchanged).** Zero new Platform Service
  registered — every one of the six real disciplines is, by this
  platform's own established taxonomy, a presentation-layer/shared-object-
  model consumer, never a Platform Service (`ADR-0062`).
- **Runtime Foundation / Engineering Core (unchanged, functionally).**
  Zero changes to Discovery, Registration, Lifecycle, DI, Configuration,
  Logging, the Host, Event Bus, Background Services, Navigation, Command
  Framework, Diagnostics, Plugin Manifest, or the Materials/Calculations/
  Verification Frameworks' own contracts.

## Key Architectural Decisions This Release

| ADR | Decision | Area |
|---|---|---|
| `ADR-0080` | Structural mutation (Rename/Move/Delete) is three additive Domain facets, not a reopened `WP8.2B` contract | Mechanical Product Structure |
| `ADR-0081` | `MoveAsync` records a live `ParentId` plus an append-only relationship history; frozen `ChildIds` stay snapshots | Mechanical Product Structure |
| `ADR-0082` | Property Inspector facet sourcing is a third Kind-keyed provider category, extending the frozen `IWorkspaceManager` contract | Mechanical Product Structure |
| `ADR-0083` | A Bill of Materials line is a fourth additive Domain facet (`IHasBomLine`); Unit of Measure is a plain string, never `Quantity<TDimension>` | Product Configuration & BOM Management |
| `ADR-0084` | Requirements lifecycle/ownership/priority/enumeration are additive `IRequirementsService` methods, never a facet-composition retrofit | Requirements Management Workspace |
| `ADR-0085` | Multi-selection is additive members on the frozen `ISelectionService`/`IWorkspaceContext` contracts | Requirements Management Workspace |
| `ADR-0086` | `CalculationTemplateRegistry` is a Workspace-layer, JSON-marshalled type-erasure adapter over `ICalculationEngine` — never a Domain-layer registry | Engineering Calculations Workspace |
| `ADR-0087` | Calculation Management's Lock/Unlock/Review/Approve/Archive verbs are `CommandDescriptor` aliases over `TransitionAsync`; Approval State is `LifecycleState` alone | Engineering Calculations Workspace |
| `ADR-0088` | The Document Classification taxonomy is `Classification`-tagged `Document` objects, never five new concrete Domain classes | Engineering Documents Workspace |
| `ADR-0089` | "Execute"/"Record Result" are one command over `IVerificationService.RecordAsync` — no adapter needed | Verification Management Workspace |
| `ADR-0090` | "Verification Plan"/"Verification Activity" are one Domain Kind, distinguished only by `LifecycleState` | Verification Management Workspace |
| `ADR-0091` | Routings/Operations/Supplier Operations are `Classification`-tagged `ManufacturingOperation` objects, sequenced via the existing `IHasBomLine.ItemNumber` field | Manufacturing Workspace |

**A recurring, cross-Work-Package architectural pattern, now proven a
fourth time at a new scale**: every one of these twelve ADRs
independently reaches "reuse what already exists, introduce nothing
new" as its own central decision — the identical pattern `v0.7.0`'s own
Architecture Baseline Summary first named, `v0.8.0`'s own confirmed a
third time for presentation- and shared-vocabulary-layer work, now
confirmed a fourth time across six genuinely different Engineering
disciplines. `ADR-0091` additionally proves the pattern generalises to a
*sequencing* concept (Routings), not only a *classification* one
(`ADR-0088`) — reusing an existing sibling-order field rather than
inventing a container type.

## Dependency Graph Integrity

Verified directly against `.csproj` project references and namespace
`using` statements:

- `Tempest.Core` — the foundation; depends on nothing else in this
  repository. Zero new `PackageReference`/`FrameworkReference` entries
  this release.
- `Tempest.Samples` — depends only on `Tempest.Core`. Fourteen new
  modules this release, all following the identical shape.
- `Tempest.App` — depends on `Tempest.Core` and `Tempest.Samples`; zero
  `PackageReference` entries (confirming `ADR-0066` continues to hold).
  Gains one new intra-namespace dependency this release
  (`.Manufacturing` → `.Documents`/`.Verification`, disclosed above),
  confirmed one-directional.
- `Tempest.Core.Tests` — depends on all three.

Zero circular project references. Zero namespace cycles within
`Tempest.Core` — `Tempest.Core.EngineeringDomain` continues to form a
strict, one-directional dependency onto `Tempest.Core.EngineeringData`
only, with the single, already-disclosed exception (`Tempest.Core
.Requirements` → `Tempest.Core.EngineeringDomain`) confirmed, again,
one-directional (`Tempest.Core.EngineeringDomain` has zero reference
back to `Tempest.Core.Requirements`).

## Security Architecture Posture

**Seven dedicated Security Reviews this release** — one per Work
Package, closing `v0.8.0`'s own disclosed "zero dedicated Security
Reviews" gap in full, and restoring `v0.7.0`'s own three-review-minimum
standard to every single implementation Work Package this cycle. No new
external attack surface was introduced across the entire release (no
new REST endpoint, no new authentication path, no new persistence
technology, no new deserialisation surface beyond closed, non-polymorphic
command parameters); every new Workspace-layer component either reuses
already-security-reviewed infrastructure (`IEngineeringDocumentStore`,
`ICurrentPrincipalAccessor`, `LifecycleTransitionTable`) unmodified, or
performs no permission gating of its own by calling-layer-enforced
design (`ADR-0061`'s own established pattern, applied consistently
across all six disciplines' own commands). The one disclosed
cross-Work-Package reuse (`WP 9.5A`'s own `Inspection`/`WorkInstruction`
facet-provider reuse) was verified, by dedicated tests, to introduce no
new authorisation path.

## Verdict

The architecture baseline as of `v0.9.0` is sound: zero circular
dependencies, zero layering violations, a consistent and now
four-times-confirmed reuse pattern, and a full recovery of the dedicated
Security Review discipline `v0.8.0` had disclosed lapsing. Two
governance-completeness findings remain open (the four-framework
Platform Service gap, the governance-document count drift), both
already disclosed by prior Work Packages, neither a functional or
architectural defect. No architectural change is recommended before
Product Approval.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report.md`;
`docs/releases/v0.9.0/WP9.9.0 Engineering Capability Summary.md`;
`ADR-0080`–`ADR-0091`; `docs/architecture/Platform Service Map.md`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`.
