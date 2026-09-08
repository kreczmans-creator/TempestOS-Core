# WP 7.3A — Requirements Engine — Implementation Report

## Status

Complete. Implements the approved Requirements & Verification Platform
contracts (`WP7.2C Requirements Platform Contracts.md`) exactly, per
this Work Package's own controlling instruction — the first
implementation Work Package of the Systems Engineering Foundation,
continuing the Engineering Foundation programme.

## What Was Implemented

`Tempest.Core.Requirements` — 20 new production files under
`src/Tempest.Core/Requirements/`, plus a full living-reference sample
module (`RequirementsSampleModule`, 8 further production files under
`src/Samples/Tempest.Samples/`):

- **`IRequirementsService`/`RequirementsService`** — the Platform
  Service entry point: create, find (by Guid and by business
  identifier), revise, set lifecycle status, link relationships, list,
  manage collections and groups, and aggregate evidence.
- **`IRequirement`/`Requirement`** — the core entity, an
  `IEngineeringDocument` of `Kind = "Requirement"`.
- **`IRequirementCollection`/`RequirementCollection`** — a named,
  purpose-built set of requirements, membership derived entirely from
  `LinkAsync`/`GetReferencesAsync`, never stored on the collection's own
  content.
- **`IRequirementGroup`/`RequirementGroup`** — a hierarchical
  categorisation node, parent reference likewise derived, never stored.
- **`RequirementRelationshipKinds`** — six reserved relationship-kind
  constants (`GroupedUnder`, `CollectedIn`, `DependsOn`, `DerivesFrom`,
  `AllocatedTo`, `References`, `Satisfies`); a seventh ("Verified By")
  deliberately absent, since it already exists inside
  `Tempest.Core.Verification`.
- **`IRequirementEvidence`/`RequirementEvidence`** — a read-side
  aggregation of a requirement's own verification history and linked
  references, introducing no new storage.
- **`RequirementStatus`/`RequirementStatusTransitions`** — a closed,
  seven-state lifecycle enum and its own exhaustive, contractual
  transition table.
- **Three exception types** (`RequirementsException` abstract base,
  `DuplicateRequirementIdentifierException`,
  `RequirementNotFoundException`, `InvalidRequirementStatusTransitionException`)
  — the approved contract's own complete exception model, no more.

## Contract Fidelity

**Every method signature matches `WP7.2C Requirements Platform
Contracts.md` exactly — zero deviation.** The eleven methods the
approved contract explicitly named (`CreateAsync`, `FindAsync`,
`FindByIdentifierAsync`, `ReviseAsync`, `SetStatusAsync`, `LinkAsync`,
`GetRelationshipsAsync`, `ListAsync`) are implemented with the identical
signature the contract review proposed. The methods the contract
explicitly deferred to this Work Package's own signature choice
(`CreateCollectionAsync`, `FindCollectionAsync`, `AddToCollectionAsync`,
`CreateGroupAsync`, `FindGroupAsync`, `GetEvidenceAsync`) were designed
following the identical conventions (nullable-return lookups,
`CancellationToken cancellationToken = default` as the final parameter,
one exception hierarchy) every approved method already establishes.

## Four Reserved ADRs, Resolved

- **`ADR-0058`** — Platform Service classification and Engineering Data
  Model reuse, confirmed exactly as `WP7.2B`/`WP7.2C` proposed.
- **`ADR-0059`** — `RequirementStatus` a closed enum, `Category` an open
  string, `Identifier` a dedicated `IPersistenceStore` index mirroring
  `MaterialCatalog`'s own precedent — each representation decided
  independently, on its own merits.
- **`ADR-0060`** — No compare-and-swap concurrency protection on
  `ReviseAsync`/`SetStatusAsync`, accepted as disclosed Technical Debt
  (`TD-25`), not resolved, consistent with the approved contract's own
  signature.
- **`ADR-0061`** — `IRequirementsService` performs no internal
  permission gating anywhere, mirroring Materials'/Calculations' own
  majority precedent rather than Verification's own narrower exception;
  `GetEvidenceAsync` remains gated transitively through its own call to
  `IVerificationService.GetVerificationHistoryAsync`.

## One Disclosed Finding: The Open-String Allocation Target Was Not Carried Into the Final Contract

`WP7.2B Requirements Domain Model.md` §5 described a Requirement
Allocation target as "either a reference to any existing
`IEngineeringDocument`... or, when no such document exists yet, an open,
unvalidated string identifier." `WP7.2C Requirements Platform
Contracts.md`'s own `LinkAsync` signature, however, accepts only a
`Guid targetDocumentId` — no string-based overload was ever proposed at
the contract stage. This Work Package implements `WP7.2C`'s own
approved signature exactly, which is narrower than `WP7.2B`'s own
architectural aspiration. **This is not a deviation from the approved
contract** — the contract itself never included the string-based
overload — but it is disclosed here as a genuine finding: the
architecture phase's own broader vision and the contract phase's own
final, shipped signature diverged, and no Work Package caught this until
implementation. See `WP7.3A Future Capability Recommendations.md` for
the resulting recommendation.

## Engineering Core Integration

- **Engineering Data Model** — hard dependency; every requirement,
  collection, and group is an `IEngineeringDocument`.
- **Verification Framework** — hard dependency; `GetEvidenceAsync` calls
  `IVerificationService.GetVerificationHistoryAsync` directly, and the
  sample module calls `IVerificationService.RecordAsync` directly —
  zero duplicate verification mechanism exists anywhere in
  `Tempest.Core.Requirements`.
- **Calculation Framework, Units & Quantities** — no dependency; no
  concrete need was identified during implementation, consistent with
  both being named "where appropriate" rather than mandatory.

## Testing

131 new tests (1275 → 1406, both configurations, clean rebuild): 119 in
`Tempest.Core.Tests.Requirements` (unit, relationship, revision,
traceability, allocation, serialization, equality, concurrency, failure,
regression), 4 Host registration tests, 8 sample-module integration
tests. Every named test category from `WP7.2C Testing Strategy.md` is
represented, including the exhaustive lifecycle-transition table test
(42 cases: 16 permitted, 26 forbidden) and the reverse-allocation-
traceability limitation test.

## Platform Integration Demonstrated

`RequirementsSampleModule` demonstrates all four integrations this Work
Package's own controlling instruction names: **Identity** (establishes
its own principal, `GetSampleRequirementEvidenceCommandHandler` gates
explicitly at the calling layer, denied by default); **Audit**
(`requirements.sampleCreated` recorded on creation); **Reporting**
(`SampleRequirementReportDefinition`/`SampleRequirementReportRenderer`,
a real, generated summary report); **Export/Import**
(`RequirementExportAdapter`, a real export-then-import round trip
creating a new requirement). No existing Platform Service was extended
or modified.

## Repository Metrics

- Automated tests: 1275 → 1406 (+131), both configurations, clean
  rebuild, 0 warnings, 0 errors.
- New production files: 28 (20 in `Tempest.Core.Requirements`, 8 in
  `Tempest.Samples`).
- New sample module: 20th production module (`RequirementsSampleModule`).
- New ADRs: 4 (`ADR-0058`–`ADR-0061`), closing the entire reserved
  range.
- New exception types: 3 (`DuplicateRequirementIdentifierException`,
  `RequirementNotFoundException`, `InvalidRequirementStatusTransitionException`),
  plus one abstract base (`RequirementsException`).
- New Technical Debt item: 1 (`TD-25`).
- New Engineering Principles: 4 (29-32).

## Related Documents

`WP7.2B Requirements Platform Architecture.md`; `WP7.2C Requirements
Platform Contracts.md`; `ADR-0058`–`ADR-0061`; `WP7.3A Engineering
Review Report.md`; `WP7.3A Security Review Report.md`; `WP7.3A Systems
Engineering Impact Assessment.md`; `WP7.3A Digital Thread Assessment.md`;
`WP7.3A Technical Debt Assessment.md`; `WP7.3A Future Capability
Recommendations.md`; `WP7.3A Lessons Learned.md`.
