# ADR-0058: Requirements Engine — Classification, Storage, and Relationship to the Engineering Data Model

## Status

Accepted — `WP 7.3A` (Requirements Engine), 2026-07-30.

## Context

`WP7.2B Requirements Platform Architecture.md` §2 proposed classifying
the Requirements Engine as a Platform Service under `ADR-0013`, and
building it directly on `Tempest.Core.EngineeringData` with no new
storage abstraction. `WP7.2C Required ADR Catalogue.md` reserved this
decision, deliberately not ratifying it, for this Work Package's own
implementation phase to confirm.

## Decision

**1. The Requirements Engine is a Platform Service — confirmed.**
`IRequirementsService` is registered as an ordinary, container-
constructed DI singleton (`services.Singleton<IRequirementsService,
RequirementsService>()`), in Phase 6, immediately after
`IVerificationService` — the identical registration shape every
Engineering Core sibling (`IEngineeringDocumentStore`, `IMaterialCatalog`,
`ICalculationEngine`, `IVerificationService`) already uses. `ADR-0013`'s
own literal test ("does the rest of the platform need this to exist
before it can function at all?") answers "no," the same answer that
would apply to every one of those four siblings too — this project's
own established practice treats "Platform Service" as "shared,
cross-cutting infrastructure," not strictly "boot-critical," and this
decision continues that practice rather than re-litigating it.

**2. Every requirement, collection, and group is a first-class
Engineering Document — confirmed, no new storage mechanism.**
`RequirementsService` creates each as an `IEngineeringDocument` via
`IEngineeringDocumentStore.CreateAsync`, under `Kind = "Requirement"`,
`"RequirementCollection"`, or `"RequirementGroup"` respectively.
Identity, revisioning, and content storage are entirely delegated —
`Tempest.Core.Requirements` introduces zero new persistence code for any
of the three.

**3. Every relationship — group hierarchy, collection membership,
allocation, traceability — is a `DocumentReference`, created via
`IEngineeringDocumentStore.LinkAsync`, never a field stored on any
DTO.** Confirmed directly in the shipped implementation: `RequirementDto`,
`RequirementCollectionDto`, and `RequirementGroupDto` each carry no
relationship data of their own — `RequirementCollection.MemberRequirementIds`
and `RequirementGroup.ParentGroupId` are both derived, at read time, from
`GetReferencesAsync`, exactly as `WP7.2C Requirements Platform
Contracts.md` §3–§4 proposed.

## Consequences

**Positive:**

- Zero new storage abstraction — the Requirements Engine inherits every
  correctness guarantee (revision atomicity, append-only references)
  `Tempest.Core.EngineeringData` already provides and has already been
  proven by four prior Engineering Core frameworks.
- Classifying the Requirements Engine identically to its four siblings
  keeps this project's own Platform Service model internally consistent
  — no special-casing is introduced for the first Systems Engineering
  Foundation framework.

**Negative:**

- None disclosed beyond what `Tempest.Core.EngineeringData` itself
  already discloses (`TD-17`, `TD-18`) — this decision inherits those
  limitations, it does not introduce new ones.

## Alternatives Considered

**Classifying the Requirements Engine as a Module, or a set of
modules** — considered and rejected. `ADR-0013`'s own test, applied
literally, would have classified Materials, Calculations, and
Verification as modules too, since none is strictly required for the
platform to boot. This Work Package's own review (`WP7.2C Governance
Confirmation.md`) found no principled reason to answer this question
differently for the Requirements Engine than for its four siblings.

**A dedicated storage abstraction purpose-built for requirements** —
considered and rejected, mirroring `ADR-0053`'s own identical reasoning
for the Engineering Data Model itself: no real requirement exists yet
that `IEngineeringDocumentStore` cannot satisfy.

## Related Documents

`ADR-0013`; `ADR-0053` (the Engineering Data Model's own storage
decision this one builds on); `ADR-0055`, `ADR-0056`, `ADR-0057` (the
Platform Service classification precedent for Materials, Calculations,
and Verification); `WP7.2B Requirements Platform Architecture.md`;
`WP7.2C Requirements Platform Contracts.md`; `WP7.2C Required ADR
Catalogue.md`; `docs/releases/v0.7.0/WP7.3A Implementation Report.md`.
