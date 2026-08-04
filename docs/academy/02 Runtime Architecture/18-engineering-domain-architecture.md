# Engineering Domain Architecture

## 1. Introduction

The Engineering Domain Architecture (`WP 8.2A`, `ADR-0072`–`ADR-0074`)
is TempestOS's own canonical statement of what an Engineering Object
is — the shape every one of roughly fifty named object families
(Requirement, Assembly, Risk, Supplier, Milestone, and so on) commits
to, whether it ships today or in a future release. It is
architecture-only: no code, no interfaces, no persistence, no UI. This
document teaches why the model looks the way it does, and how it
relates to the four Engineering Core frameworks (`Tempest.Core.
Requirements`, `Verification`, `Materials`, `Calculations`) that
already ship and already, independently, converged on the same shape.

## 2. Purpose

To explain why TempestOS did not need to invent a new domain model —
only to notice that four separately-designed frameworks had already
converged on one, and to state that convergence once, formally, as
binding platform architecture, rather than leaving every future
framework to rediscover it independently a fifth, tenth, and fiftieth
time.

## 3. Background

By `v0.7.0`, TempestOS had shipped a working Engineering Data Model
(`Tempest.Core.EngineeringData`, `WP 7.1A`) and four frameworks built
on it — Units & Quantities, Materials, Calculations, Verification —
plus, in the Systems Engineering Foundation phase, a Requirements
Engine (`WP 7.3A`). Each of these frameworks independently discovered
the same three facts: their own primary entity is best expressed as an
`IEngineeringDocument`; their own "revise" operation is best expressed
as a thin wrapper over the one shared `IDocumentRevision` mechanism;
and their own relationships are best expressed as open-string
`DocumentReference`s, never a closed enum. No cross-framework
coordination produced this convergence — each framework's own Contract
Review reached it independently, the same way five frameworks
independently reached "reuse the Engineering Data Model, introduce no
new persistence" during `WP 7.0C`.

`WP 8.2A` is the Work Package that names this convergence once,
formally, and extends its vocabulary to cover every Engineering Object
the platform will ever need — most of which, as of this Work Package,
have no implementation at all.

## 4. The Problem

1. **Does every future Engineering Object family get its own storage
   shape, or do they all share one** — the question `ADR-0072`
   answers, having already been answered four times independently
   without anyone declaring it platform policy?
2. **How do fifty wildly different object families relate to each
   other** without a combinatorial explosion of per-pair relationship
   types — `ADR-0073`'s own question?
3. **Does lifecycle stay one rigid global enum, or fully ad hoc per
   family** — neither extreme survives contact with `RequirementStatus`,
   a real, shipped, seven-state model that is neither identical to a
   generic eight-state list nor unrelated to it — `ADR-0074`'s own
   question.
4. **How does a canonical catalogue of fifty objects stay honest about
   what is real and what is not**, so a reader never mistakes an
   architectural aspiration for shipped behaviour?

## 5. The Design

Every Engineering Object is defined by five facets (`WP8.2A Engineering
Domain Architecture.md` §3): identity (a permanent `Guid` plus an
optional business identifier), content (opaque, interpreted only by its
own owning framework), metadata (a common envelope, `Metadata
Specification.md`), lifecycle (a canonical eight-state vocabulary,
specialised per family, `ADR-0074`), and relationships (open-string
`DocumentReference`s, `ADR-0073`). None of these five facets is a new
mechanism — every one is a direct restatement of what
`Tempest.Core.EngineeringData` and its four dependent frameworks
already do. The Canonical Object Catalogue names ~49 objects across
thirteen families, honestly marking five as `Implemented` (reconciled
against real `Kind` constants) and the remaining forty-plus as
`Conceptual` — architecturally defined, not yet built, the explicitly
required state for almost this entire catalogue.

## 6. Alternatives Considered

**A distinct storage/type hierarchy per object family** — considered
and rejected; see `ADR-0072`. Would fragment the platform into
incompatible Engineering Core styles the moment a second one shipped.

**A closed `RelationshipKind` enum** — considered and rejected; see
`ADR-0073`. Cannot scale to fifty object families and their own future
extensions without becoming either impossibly large or a bottleneck
every new relationship must be reviewed through.

**One rigid, unspecialisable global lifecycle enum** — considered and
rejected; see `ADR-0074`. Would require redesigning the real, shipped
`RequirementStatus`, which this architecture-only Work Package has no
authority to do.

## 7. Why This Solution Was Chosen

It is the first Work Package to generalise "reuse what already exists"
from a per-framework discipline (proven four separate times) into a
stated platform architecture — the natural next step once four
independent conclusions are the same conclusion, and the one this
Work Package's own Definition of Done (a new team could implement the
whole platform from this specification alone) actually requires.

## 8. Architectural Principles

- **Composition Over Inheritance** — the canonical shape is a set of
  facets an object's own `Kind` commits to, not a base class hierarchy;
  nothing in this architecture proposes an `EngineeringObjectBase` type.
- **Open/Closed** — every extensibility point (`ADR-0072`'s new Kinds,
  `ADR-0073`'s new relationship kinds, `ADR-0074`'s per-family lifecycle
  specialisation) is additive; nothing requires modifying existing,
  shipped code to add a new canonical object.
- **Honesty over completeness-theatre** — the same principle `WP 8.1C`
  named for the Engineering Cockpit's own placeholder cards, applied
  here to an entire catalogue: a `Conceptual` object is marked as such,
  never presented as if it already existed.

## 9. Benefits

- Roughly forty canonical objects with no implementation yet
  automatically inherit identity stability, revision history, and the
  relationship mechanism the moment they are built — zero new
  persistence engineering required per object family.
- `RequirementStatus`, `IVerificationRecord`, `IMaterialSpecification`,
  and `CalculationRecord<TResult>` all remain exactly as shipped —
  reconciled, not redesigned, against the canonical model.
- A future Engineering Discipline Module (Assembly/Part, most likely
  next, per `WP7.2A Recommended Programme.md`'s own roadmap) has a
  complete, load-bearing domain vocabulary to build against before its
  own Contract Review begins.

## 10. Trade-offs

- No structural guarantee a `verifiedBy` link actually targets a
  Verification Result rather than something else — convention and
  review are the only enforcement, mirroring `RequirementRelationshipKinds`'
  own already-accepted risk at a larger scale (`ADR-0073`).
- Two future families can specialise the canonical lifecycle vocabulary
  inconsistently, with no platform-level check that a family's own
  `Approved` means the same thing another family's own `Approved` means
  (`ADR-0074`).
- Several rules in this architecture (approval gates structurally
  requiring an `Approved By` link; lifecycle-blocking `Depends On`
  constraints) are named but **not yet enforced by any shipped code** —
  disclosed explicitly in `Validation Specification.md`, not silently
  assumed.

## 11. Common Mistakes

The mistake most worth naming: treating a `Conceptual` catalogue entry
as if naming it were the same as building it. Every one of the forty
`Conceptual` objects in `WP8.2A Canonical Object Catalogue.md` needs a
real implementation Work Package — most likely following the same
two-stage architecture-then-contracts discipline every shipped
framework already used — before any code can depend on it. This
document is a vocabulary and a shape, not a promise that the vocabulary
is already usable.

## 12. Future Evolution

- **A real Physical/Configuration Engineering Discipline Module**
  (Assembly, Sub-Assembly, Part, Component) — the most natural first
  proof of this canonical model against a genuinely new discipline,
  mirroring Requirements' own role as the first proof of the Engineering
  Data Model.
- **Closing the Verification Activity/Verification Result gap**
  (`WP8.2A Canonical Object Catalogue.md` §3's own disclosed note) —
  a real, separately-persisted, revisable Verification Activity, distinct
  from its own eventual Result.
- **A real Baseline/Release implementation**, proving `Configuration
  Management Specification.md` §3's own reuse of the
  `RequirementCollection` pattern against a genuinely frozen,
  revision-pinned membership model.
- **Structural enforcement of the approval-gate and lifecycle-blocking
  rules** `Validation Specification.md` names but does not yet require
  any shipped code to enforce.

## 13. Key Takeaways

1. A pattern independently discovered by four separate teams (or four
   separate Work Packages) solving four separate problems is strong
   evidence it is the right platform-wide answer — the job left is
   naming it once, not inventing a fifth alternative.
2. An architecture-only Work Package can responsibly cover fifty named
   concepts without building any of them, provided every entry is
   honest about its own real/conceptual status — a catalogue is not
   weakened by disclosing how much of it is not yet real; it is
   weakened by hiding that fact.
3. Reconciling a new canonical model against real, shipped code
   (`RequirementStatus`, `RequirementRelationshipKinds`) is stronger
   evidence the model is sound than designing it in isolation and hoping
   it fits later.

## Related Documents

`15-engineering-data-model.md`; `16-requirements-engine.md`;
`14-verification-framework.md`; `13-calculation-framework.md`;
`ADR-0053`, `ADR-0058`, `ADR-0072`–`ADR-0074`; `docs/releases/v0.8.0/
WP8.2A Engineering Domain Architecture.md` and its eight companion
deliverables; `docs/engineering/Engineering Principles.md`;
`docs/academy/03 Work Packages/WP8.2A-engineering-domain-architecture.md`.
