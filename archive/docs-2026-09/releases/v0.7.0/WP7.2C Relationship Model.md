# WP 7.2C — Relationship Model

## Status

Contract review only. No implementation.

## Purpose

Reviews and defines the seven relationship kinds this Work Package's
own controlling instruction names, confirming for each whether it
belongs in the Requirements Platform's own initial implementation or is
correctly deferred as a future extension point — per this Work
Package's own explicit instruction.

## The Underlying Mechanism (Stated Once)

Every relationship below is a `Tempest.Core.EngineeringData.
DocumentReference`, created via `IRequirementsService.LinkAsync`
(itself a thin wrapper over `IEngineeringDocumentStore.LinkAsync`),
distinguished only by its own `RelationshipKind` string value —
confirmed in full in `WP7.2C Requirements Platform Contracts.md` §5.
No relationship kind introduces its own storage, its own validation
mechanism, or its own service. The only question each relationship kind
answers is: **what string value does it use, and does a real, named
need justify including it in the initial implementation now?**

## Review

| Relationship | Reserved Constant | Direction | Initial Implementation or Future Extension Point? | Rationale |
|---|---|---|---|---|
| **Parent / Child** | `RequirementRelationshipKinds.GroupedUnder` | Child → Parent Group | **Initial implementation.** | Requirement Group (`WP7.2C Requirements Platform Contracts.md` §4) is explicitly named as one of the thirteen contracts this Work Package must define — its own hierarchy is not optional. |
| **Depends On** | `RequirementRelationshipKinds.DependsOn` | Requirement → Requirement | **Initial implementation.** | A general-purpose Requirement Relationship (§5 of the Contracts document) is explicitly named as a required contract; "depends on" is its own most basic, universally-applicable instance — a requirement referencing a prerequisite requirement is a genuinely foundational systems-engineering need, not a speculative one. |
| **Derived From** | `RequirementRelationshipKinds.DerivesFrom` | Requirement → Source (another requirement, or an external reference) | **Initial implementation.** | This is one half of bidirectional traceability, named explicitly by this Work Package's own Traceability section (`WP7.2C Traceability Contract.md`) as backward traceability's own defining relationship — every standard family reviewed in `WP7.2B Standards Mapping.md` requires it. |
| **Allocated To** | `RequirementRelationshipKinds.AllocatedTo` | Requirement → Allocation Target (any document, or an open string) | **Initial implementation.** | Requirement Allocation is one of the thirteen required contracts, and `RequirementStatus.Allocated` (`WP7.2C Requirement Lifecycle Model.md`) depends on it existing — the lifecycle model itself would be incomplete without this relationship. |
| **Verified By** | *(not reserved by this Platform — owned by `Tempest.Core.Verification`)* | Requirement → Verification Record | **Already implemented, by a different framework.** | `Tempest.Core.Verification`'s own `VerifiedByRelationshipKind` constant already exists and already creates this exact relationship as part of `RecordAsync`'s own existing implementation (`WP 7.1E`). This Platform reserves **no** constant for it and creates it **never** directly — see `WP7.2C Verification Integration Contract.md` for the complete confirmation. |
| **References** | `RequirementRelationshipKinds.References` | Requirement → any document | **Initial implementation.** | A general, non-owning cross-reference (a requirement citing a supporting calculation, a related but non-dependent requirement) is the same open-reference pattern `AT-16`/`AT-17` already establish — cheap to include now since it reuses the identical mechanism every other relationship kind already requires. |
| **Satisfies** | `RequirementRelationshipKinds.Satisfies` | Design/Delivered Target → Requirement | **Initial implementation.** | This is traceability's other half (forward traceability, `WP7.2C Traceability Contract.md`) and is the relationship `RequirementStatus.Satisfied` (`WP7.2C Requirement Lifecycle Model.md`) depends on — the same reasoning as Allocated To. |

## Confirmation

**Six of the seven named relationships belong in the initial
implementation; the seventh ("Verified By") is not this Platform's own
relationship to reserve at all — it already exists, unmodified, inside
`Tempest.Core.Verification`.** No relationship named by this Work
Package's own controlling instruction is deferred as a future extension
point — every one is either load-bearing for a contract this Work
Package must define (the Lifecycle Model, the Requirement Group
hierarchy) or costs nothing extra to include now, since all six reuse
the identical `LinkAsync` mechanism regardless of how many relationship
kinds are reserved.

**This is a different outcome than `WP7.0C Engineering Foundation
Contracts.md`'s own precedent for calculation input/output types or
material property names**, where an open, extensible design was
preferred *specifically because* a closed set would have required
inventing content ahead of real discipline evidence. Relationship kinds
are different in kind: reserving a constant costs nothing (it is a
`string` literal, not a design commitment to a specific engineering
domain), and every one of the six is directly load-bearing for a
contract already required elsewhere in this same Work Package — there is
no equivalent "premature invention" risk to guard against here.

## Extension Points, Beyond the Seven Named

Additional relationship kinds may be added to
`RequirementRelationshipKinds` purely additively in the future — a
`"conflictsWith"` or `"duplicates"` kind, for instance, named as
plausible examples in `WP7.2B Requirements Domain Model.md` §4 but not
required by any contract this Work Package must define, and therefore
correctly left as a genuine future extension point, unlike the six
above.

## Related Documents

`WP7.2C Requirements Platform Contracts.md` §1, §5; `WP7.2C Requirement
Lifecycle Model.md`; `WP7.2C Traceability Contract.md`; `WP7.2C
Verification Integration Contract.md`; `WP7.2B Requirements Domain
Model.md`.
