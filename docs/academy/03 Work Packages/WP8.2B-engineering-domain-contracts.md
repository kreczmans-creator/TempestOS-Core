# WP 8.2B — Engineering Domain Contracts

## What This Document Is

A contract-review-only milestone Work Package, mirroring `WP7.0C
Engineering Foundation Contracts`/`WP7.2C Requirements Platform
Contracts`/`WP8.0B Workspace Contracts`'s own whole-review format — no
production code written, no implementation performed. This document
follows the same whole-review shape (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways) rather than the standard 13-section per-feature
template, since no code exists for that template's own "Files Added"/
"Trade-offs" sections to describe.

## Introduction

`WP 8.2B` is `v0.8.0`'s own eighth Work Package, following `WP 8.2A`
directly — the identical two-stage architecture-then-contracts sequence
every prior Engineering Core framework already proved out (`WP 7.0B` →
`WP 7.0C`; `WP 7.2B` → `WP 7.2C`; `WP 8.0A` → `WP 8.0B`). It converts
`WP 8.2A`'s own canonical Engineering Domain Architecture into the
complete public contract every current and future TempestOS module
implements against.

## What Was Achieved

Proposed, uncompiled C# for: `IEngineeringObject` (the base contract);
ten facet interfaces (`IHasBusinessIdentifier`, `IHasMetadata`,
`IHasLifecycle`, `IHasRevisions`, `IHasRelationships`, `ITraceable`,
`IValidatable`, `IHasAttachments`, `ISearchable`); all ~49 canonical
object interfaces across thirteen families, each composed from
`IEngineeringObject` plus its own relevant facets (`ADR-0075`); one
generic `IEngineeringRelationship` interface plus a
`RelationshipCategory` enum realising all seventeen named relationship
categories (`ADR-0076`); the full lifecycle contract set
(`LifecycleState`, `ILifecycleTransitionTable`, `IApprovalGate`,
`IReviewGate`, `IReleaseGate`); the full validation contract set
(`IValidationResult`, `IValidationRule`, `IValidationRuleSet`,
`IReferenceIntegrityChecker`); the full Digital Thread contract set
(`IRelationshipDiscovery`, `IDependencyTraversal`, `IEvidenceComposer`,
`IImpactAnalysis`); two factory contracts
(`IEngineeringObjectFactory`, `IEngineeringRelationshipFactory`);
eight sequence diagrams; and a complete dependency-rules/layering
analysis. Two new ADRs (`ADR-0075`, `ADR-0076`). Zero code compiled —
every signature remains proposed, documentation-only C#, exactly as
`WP7.2C Requirements Platform Contracts.md` established the precedent
for.

## Architectural Lessons

**A product brief's own literal request can conflict with a prior,
binding decision, and resolving that conflict explicitly is real
architectural work, not a formality.** The controlling instruction's
own "define contracts governing" seventeen relationship categories,
read literally, implies seventeen interface types — directly
contradicting `ADR-0073`'s own already-locked "open string, never a
closed enum" decision, made one Work Package earlier in the same
release. `ADR-0076` resolves this by distinguishing "governing a
category" (documenting a convention, via descriptive metadata on one
generic interface) from "defining a closed type" (seventeen structural
types) — the same tension, and the same resolution shape, `WP 8.2A`
itself already demonstrated once (reconciling `RequirementStatus`
against a canonical lifecycle list). This is now a proven, repeatable
pattern across two consecutive Work Packages: when a new instruction
seems to want something a prior ADR already forbade, look for the
narrower, honest reading before assuming a contradiction.

**"Composition over inheritance," stated as a principle, needed a
mechanical rule to actually be checkable.** The controlling instruction
names the principle; `ADR-0075` gives it a concrete shape (small facet
interfaces, composed; at most one level of object-to-object
specialisation) and `WP8.2B Dependency Rules.md` §6 states the rule
precisely enough that a future contribution could be checked against it
mechanically — "does this new interface inherit from more than one
other canonical object interface" is answerable by inspection, not
judgement.

## Implementation Lessons

Not applicable in the usual sense — no implementation was performed.
The closest analogue: designing `IEvidenceComposer`
(`WP8.2B Digital Thread Contract Specification.md` §3) surfaced that
its own three-step recipe is not a new algorithm at all — it is
`IRequirementsService.GetEvidenceAsync`'s own already-shipped
implementation, generalised. No contract in this Work Package was
designed without first checking whether a real, shipped method already
did the equivalent job for one framework — the same discipline
`WP 8.2A` established for the architecture stage, now confirmed to hold
at the contract stage too.

## Repository Maturity

**Every contract's own dependency was checked against
`WP8.2A Canonical Object Catalogue.md`/`Relationship Catalogue.md`
before being proposed**, and every facet interface's own member list
was checked against a real, shipped equivalent
(`IDocumentRevision.Content`/`AuthorPrincipalId` for `IHasRevisions`;
`DocumentReference` for `IHasRelationships`) rather than invented
independently. `WP8.2B Dependency Rules.md` confirms zero new Platform
Service, zero new persistence mechanism, and zero new relationship-
storage mechanism — the complete list of what is reused
(`IEngineeringDocument`, `IEngineeringDocumentStore`,
`IDocumentRevision`, `DocumentReference`, `Quantity<TDimension>`) was
verified directly against each type's own real, shipped shape. No
governance register required correction as part of this review.

## Recommendations for the Next Work Package

1. **An implementation Work Package should follow directly**, building
   a real Physical/Configuration Engineering Discipline Module
   (`IAssembly`/`ISubAssembly`/`IPart`/`IComponent`) against the frozen
   contracts exactly as specified — mirroring `WP 7.3A`'s own "implement
   the approved contracts exactly" discipline, and `WP8.2A`'s own
   Recommendations, unchanged.
2. **Close the Verification Activity/Verification Result gap** — a real
   `IVerificationActivity` implementation, distinct from its own
   eventual `IVerificationResult`, if a real discipline module surfaces
   a genuine need — not speculatively.
3. **Register the first real `IEngineeringObjectFactory`/
   `IEngineeringRelationshipFactory` pair** as part of that same
   implementation Work Package, proving `WP8.2B Dependency Rules.md`
   §7/§8's own factory/registration rules against real code, the same
   way `WP 8.1B` was the first real proof of `ADR-0067`'s own Workspace
   extensibility mechanism.
4. **Do not add an eleventh facet interface, or an eighteenth
   relationship category, speculatively.** Both sets (§3 of the master
   document; `RelationshipCategory`) were sized to the controlling
   instruction's own named concerns exactly — grow either only when a
   real implementation Work Package demonstrates a genuine gap, not in
   advance of one.

## Key Takeaways

1. Resolving an apparent conflict between a new instruction and a prior
   ADR is the same kind of judgement call this project has always
   required for disclosed implementation findings — now proven to
   scale to architecture/contract Work Packages too, not only
   implementation ones.
2. A principle ("composition over inheritance") is only as useful as
   the concrete, checkable rule it is turned into — stating the
   principle is the easy half; `ADR-0075`'s own "at most one level of
   object-to-object specialisation" rule is the harder, more valuable
   half.
3. Checking a new contract's own proposed shape against a real, shipped
   method (`GetEvidenceAsync`, `IDocumentRevision`) before finalising it
   is what turns "this seems like a reasonable interface" into "this
   interface is what the platform already proved works."

## Related Documents

`docs/releases/v0.8.0/WP8.2B Engineering Domain Contracts.md` and its
six companion deliverables; `ADR-0075`; `ADR-0076`; `docs/academy/
02 Runtime Architecture/18-engineering-domain-architecture.md`;
`docs/academy/03 Work Packages/WP8.2A-engineering-domain-architecture.md`;
`docs/academy/03 Work Packages/
WP7.2C-requirements-and-verification-platform-contract-review.md` (the
format precedent this document follows).
