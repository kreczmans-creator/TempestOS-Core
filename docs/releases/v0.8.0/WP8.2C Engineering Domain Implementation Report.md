# WP 8.2C — Engineering Domain Implementation Report

## Status

Complete. `v0.8.0`'s own ninth Work Package, and its fourth
implementation — implements the complete Engineering Domain contracts
`WP 8.2A`/`WP 8.2B` established, exactly as frozen, with no discipline
logic (Requirements/Verification/Calculations/Manufacturing) written
anywhere inside it. 1631/1631 tests passing (1592 → 1631), both Debug
and Release, clean rebuild. Three new ADRs (`ADR-0077`–`ADR-0079`).

## What Was Implemented

A new namespace, `Tempest.Core.EngineeringDomain`, under
`src/Tempest.Core/EngineeringDomain/`, split into `Contracts/` (21
files — every WP8.2B interface/enum/record, compiled exactly as
frozen, plus one disclosed gap-fill, `IRevisionRecord`) and
`Implementation/` (24 files):

- **`EngineeringObjectBase`** — one shared concrete class implementing
  `IEngineeringObject` and all nine facet interfaces unconditionally.
  Every one of the 39 concrete canonical object classes derives from it
  (or from another concrete class that does), declaring only the
  specific canonical interface(s) its own Kind actually composes.
- **`InMemoryEngineeringDocumentStore`** — a second, fully in-memory
  `IEngineeringDocumentStore` implementation, used by this Work
  Package's own tests and never registered in `TempestHost.cs`
  (`ADR-0077`).
- **`IEngineeringObjectRepository`/`InMemoryEngineeringObjectRepository`**
  and **`IEngineeringRelationshipRepository`/
  `InMemoryEngineeringRelationshipRepository`** — the genuinely new,
  purely in-memory "in-memory repositories" deliverable: a Kind-queryable
  object index and a relationship-metadata side index, neither
  competing with `IEngineeringDocumentStore` (`ADR-0077`).
- **`LifecycleTransitionTable`** — the canonical eight-state permitted-
  transition table, mirroring `RequirementStatusTransitions`'s own
  dictionary shape.
- **`ValidationRuleSet`**, **`ReferenceIntegrityChecker`** — the
  validation framework; `ValidationRuleSet` starts with zero registered
  rules (only `StructuralValidationRules.NoSelfReference` is enforced
  today, structurally, in `EngineeringObjectBase.LinkAsync` itself).
- **`RelationshipDiscoveryService`** (`IRelationshipDiscovery`,
  `IDependencyTraversal`, `IImpactAnalysis`) and **`EvidenceComposer`**
  (`IEvidenceComposer`) — the Digital Thread framework, `IImpactAnalysis`
  realised exactly as `WP8.2B Digital Thread Contract Specification.md`
  names it: `IDependencyTraversal` run incoming, over Dependency/
  Allocation/Verification categories only.
- **`EngineeringObjectFactory<T>`/`EngineeringRelationshipFactory`** —
  two generic factory types serving every Kind/RelationshipKind, each
  instance permanently bound to the one Kind it was constructed for
  (`ADR-0079`).
- **39 concrete canonical object classes** across eleven family files
  (`ProgrammeHierarchy.cs`, `PhysicalConfiguration.cs`,
  `RequirementsVerification.cs`, `Calculations.cs`,
  `DocumentationDesign.cs`, `TestManufacturing.cs`, `SupplyChain.cs`,
  `GovernanceRisk.cs`, `ProcessApproval.cs`, `ChangeRelease.cs`,
  `EvidenceReference.cs`) — every `Conceptual` object `WP8.2A Canonical
  Object Catalogue.md` names, except the five already-Implemented Kinds
  (`ADR-0078`). `Task`/`Action` are named `EngineeringTask`/
  `EngineeringAction` in concrete form, to avoid colliding with
  `System.Threading.Tasks.Task`/`System.Action`.
- **`EngineeringDomainSampleModule`** (`src/Samples/Tempest.Samples/`)
  — a new, twenty-second sample module building a sixteen-object,
  eight-family representative graph, including a real cross-framework
  reference to a `Tempest.Core.Materials`-registered
  `IMaterialSpecification`.

## Contract Fidelity

Every interface/enum/record `WP8.2B Interface Catalogue.md`,
`Engineering Domain Contracts.md`, `Lifecycle Contract
Specification.md`, `Relationship Contract Specification.md`,
`Validation Contract Specification.md`, and `Digital Thread Contract
Specification.md` proposed compiles exactly as frozen — zero signature
changed. `IRelease : IBaseline : IConfiguration` (`WP8.2B Interface
Catalogue.md` §12) is a three-level canonical-object specialisation
chain, directly contradicting `WP8.2B Dependency Rules.md` §6's own "at
most one level" rule — compiled exactly as frozen anyway (interfaces
are not this Work Package's to silently correct) and disclosed below,
not corrected.

## Three New ADRs, Resolved

- **`ADR-0077`** — Engineering Domain shared services reuse the
  existing, real `IEngineeringDocumentStore` in production (via
  `EngineeringDomainContext`, DI-resolved); the new in-memory repository
  layer, not a second document store, is the "in-memory repositories"
  deliverable. Resolves a direct tension between this Work Package's own
  "no persistence" constraint and `ADR-0072`'s own prior, binding
  decision.
- **`ADR-0078`** — the five already-Implemented canonical Kinds
  (`Requirement`, `RequirementCollection`/`Group`, `VerificationRecord`,
  `CalculationRecord`, `MaterialSpecification`) are not given a
  competing concrete realisation here — their Domain interfaces compile;
  their concrete ownership stays exactly where `WP 8.2A` already placed
  it.
- **`ADR-0079`** — object and relationship factories are two generic
  types (`EngineeringObjectFactory<T>`, `EngineeringRelationshipFactory`),
  instantiated once per Kind by the composition root, never dozens of
  hand-written per-Kind factory classes.

## Disclosed Implementation-Phase Findings

1. **`IRevisionRecord` was referenced by `IHasRevisions.GetRevisionHistoryAsync`
   in `WP8.2B Interface Catalogue.md` but never itself defined anywhere
   in that Work Package's own deliverables.** Closed here, as a small,
   new interface mirroring `EngineeringData.IDocumentRevision` (minus
   the redundant `DocumentId`, since it is already scoped to one
   object's own history) — see `src/Tempest.Core/EngineeringDomain/
   Contracts/Facets.cs`.
2. **`IEngineeringRelationship` requires `Category`/`CreatedByPrincipalId`/
   `CreatedAt` — none of which `EngineeringData.DocumentReference`
   carries.** Closed by `IEngineeringRelationshipRepository`, a new
   side index recording the full shape whenever a relationship is
   created through the Domain framework's own `LinkAsync` path;
   `IEngineeringDocumentStore.LinkAsync` remains the sole authority on
   whether a link exists (`ADR-0073` unaffected).
3. **`IRelease : IBaseline : IConfiguration`** — see Contract Fidelity,
   above.
4. **`WP8.2B Engineering Domain Contracts.md`'s own §0 miscounted its
   companion deliverables** ("six companion deliverables," listing
   seven) — corrected in place; the true count (`WP 8.2B` shipped eight
   deliverables total, not nine) was also corrected across every
   governance register that had inherited the miscount.

## Engineering Core Integration

`EngineeringDomainSampleModule` registers a real
`IMaterialSpecification` through `Tempest.Core.Materials.IMaterialCatalog`
directly, then constructs a Domain-level `Part` referencing it
(`Part.MaterialId`) and links to its own underlying document
(`Part.LinkAsync(materialSpecification.UnderlyingDocumentId, "references")`)
— proving genuine, working, cross-framework traceability between the
new Engineering Domain implementation and an existing Engineering Core
framework, through the one shared `IEngineeringDocumentStore` both
frameworks resolve via DI.

## Testing

39 new tests directly exercising the framework (`EngineeringDomainFrameworkTests.cs`
— factory, lifecycle, revision, relationship, validation, evidence,
attachments, dependency traversal, impact analysis, reference
integrity), 10 host-registration tests (`EngineeringDomainHostRegistrationTests.cs`
— every shared service resolvable, singleton, and genuinely sharing the
real `IEngineeringDocumentStore`), and 7 full-pipeline sample-module
integration tests (`EngineeringDomainSampleModuleIntegrationTests.cs`
— including one running through the real, unmodified `TempestHost` end
to end). One pre-existing test, `ClockModuleDiscoveryTests.
DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule`,
updated to expect twenty-two sample modules (21 → 22), the same,
disclosed, expected update every prior new sample module has required.
1631/1631 tests passing, both Debug and Release, clean rebuild.

## Platform Integration Demonstrated

`EngineeringDomainSampleModule` runs through the real, unmodified
`TempestHost`, constructor-injecting `IIdentityService`,
`EngineeringDomainContext`, `IMaterialCatalog`, `IDependencyTraversal`,
`ICommandDispatcher`, and `ICommandRegistry` — all real, DI-resolved
collaborators, none private or hand-assembled. Its own
`GetSampleEngineeringDomainGraphSummaryCommand` demonstrates
`IDependencyTraversal` against the real sixteen-object graph, dispatched
through `ICommandRegistry` exactly as every prior sample module's own
commands are.

## Governance Note: Interface/DI/Module Registers

`Interface Register.md` gains roughly 70 new public interface entries
(21 Contracts files' worth, plus `IEngineeringObjectRepository`/
`IEngineeringRelationshipRepository`, both new to this Work Package).
`Dependency Injection Register.md` gains ten new named registrations
(`IEngineeringObjectRepository`, `IEngineeringRelationshipRepository`,
`ILifecycleTransitionTable`, `IValidationRuleSet`,
`IReferenceIntegrityChecker`, `IRelationshipDiscovery`,
`IDependencyTraversal`, `IImpactAnalysis`, `IEvidenceComposer`,
`EngineeringDomainContext`), all added to `TempestHost.cs` directly
after `IEngineeringDocumentStore`, before Materials. `Module
Register.md` gains one row (`EngineeringDomainSampleModule`, 21 → 22
production modules).

## Technical Debt Assessment

**Zero new Technical Debt items raised.** Every genuine limitation
found during this Work Package is disclosed above as a finding, an ADR
consequence, or a Future Evolution item in `18-engineering-domain-
architecture.md`, not silently absorbed: the in-memory repository does
not rebuild itself from the real store on Host restart (`ADR-0077`);
the five already-Implemented Kinds cannot currently be constructed
through this framework (`ADR-0078`); `ValidationRuleSet` enforces zero
Kind-specific rules by design, pending a real consumer.

## Repository Metrics

- 1631 tests (0 failures) — **+39, `WP 8.2C`**.
- Public interfaces (`src/Tempest.Core/`) — **+~70, `WP 8.2C`**: the
  complete `Tempest.Core.EngineeringDomain.Contracts` surface, plus two
  new implementation-owned interfaces.
- ADRs — **+3, `WP 8.2C`**: `ADR-0077`–`ADR-0079` (76 → 79).
- Modules (production) — **+1, `WP 8.2C`**: `EngineeringDomainSampleModule`
  (21 → 22).
- DI registrations (`TempestHost.cs` Phase 6) — **+10, `WP 8.2C`**.

## Related Documents

`docs/releases/v0.8.0/WP8.2A Engineering Domain Architecture.md` and
companions; `docs/releases/v0.8.0/WP8.2B Engineering Domain
Contracts.md` and companions; `ADR-0072`–`ADR-0079`; `docs/academy/
02 Runtime Architecture/18-engineering-domain-architecture.md`;
`docs/academy/03 Work Packages/WP8.2C-engineering-domain-implementation.md`.
