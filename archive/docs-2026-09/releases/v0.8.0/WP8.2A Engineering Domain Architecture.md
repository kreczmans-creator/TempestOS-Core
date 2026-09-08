# WP 8.2A — Engineering Domain Architecture

## Purpose

The complete canonical Engineering Domain Architecture for TempestOS —
the engineering language every current and future module consumes.
This Work Package is architecture-only: no implementation, no
persistence, no interfaces, no repositories, no storage technology, no
UI. It defines every Engineering Object TempestOS recognises — its
identity, behaviour, ownership, lifecycle, relationships, traceability,
governance, and validation — in a form technology-independent enough
that a new engineering team could implement the entire platform from
this specification alone.

This document is the master reference; eight companion deliverables go
deeper into one area each (`Canonical Object Catalogue`, `Relationship
Catalogue`, `Lifecycle Specification`, `Configuration Management
Specification`, `Digital Thread Specification`, `Metadata
Specification`, `Validation Specification`, `Engineering Object
Interaction Diagrams`), plus Academy documentation and three new ADRs.

## 0. Grounding: This Architecture Formalises, Not Replaces, What Already Ships

TempestOS already has a real, shipped Engineering Core
(`Tempest.Core.EngineeringData`, `WP 7.1A`) and two real Systems
Engineering Foundation frameworks built on it (`Tempest.Core.Requirements`,
`WP 7.3A`; `Tempest.Core.Verification`, `Tempest.Core.Materials`,
`Tempest.Core.Calculations`, all `WP 7.1x`). This Work Package's own
"Core Principle" — everything is an Engineering Object — is not a new
claim invented here; it is the **explicit generalisation** of a pattern
every one of those four frameworks already independently discovered and
converged on without coordination:

- Every framework's own primary entity **is a** `IEngineeringDocument`
  (`Guid Id`, `string Kind`, `int CurrentRevisionNumber`,
  `DateTimeOffset CreatedAt`) — never a second, framework-specific
  identity/storage shape.
- Every framework's own "revise" operation **is** a thin wrapper over
  the single shared `IEngineeringDocumentStore.ReviseAsync`, producing
  an immutable, append-only `IDocumentRevision` — never a second
  versioning scheme.
- Every framework's own relationships **are** `DocumentReference`
  records (`SourceDocumentId`, `TargetDocumentId`, `string
  RelationshipKind`) written via `LinkAsync` and read via
  `GetReferencesAsync` — an open, unvalidated `RelationshipKind`
  string, never a closed enum, and Kind-agnostic at the target end
  (`Engineering Principle 31`).
- Every framework's own lifecycle status (where one exists —
  Requirements' own seven-state `RequirementStatus`) is a **closed,
  explicit, caller-driven transition table**, never auto-derived from
  another framework's own outcome (`Engineering Principle 29`/`30`).

This Work Package's own job is to **name this pattern once, formally,
as platform architecture** (§3, below, and `ADR-0072`–`ADR-0074`), and
to **extend its vocabulary** — the canonical object catalogue, the
relationship catalogue, the common lifecycle vocabulary — to cover
every Engineering Object the platform will ever need, most of which
have no implementation yet. Nothing here contradicts or requires
changing `Requirement`, `IVerificationRecord`, `IMaterialSpecification`,
or `CalculationRecord<TResult>` — each is reconciled explicitly, as a
real, shipped **specialisation** of this canonical model, in `Canonical
Object Catalogue.md`.

## 1. Core Principle

**Everything inside TempestOS is an Engineering Object.** A Requirement,
a Calculation, an Assembly, a Part, a Document, a Risk, a Change, a
Verification Record, a Manufacturing Operation — every one of them is
an instance of the same canonical shape (§3), distinguished only by its
own `Kind` and the relationships, lifecycle vocabulary, and metadata
its own family of the Canonical Object Catalogue declares. **Every
Engineering Object participates in the Digital Thread** — reachable
by traversal from any other Engineering Object it is related to,
through the single, already-existing reference mechanism (§6,
`Digital Thread Specification.md`).

This is a discipline-neutral claim, not a Systems-Engineering-specific
one: a Supplier, a Purchase Item, and a Work Instruction are Engineering
Objects exactly as much as a Requirement is — the canonical shape does
not privilege any one discipline's own object family.

## 2. The Problem

1. **What is the one shape every Engineering Object shares**, such
   that a future module never needs to invent a new identity, revision,
   relationship, or lifecycle mechanism of its own — the same question
   `WP 7.0C`'s own Engineering Foundation Contract Review answered for
   five frameworks independently, now answered once, for all of them?
2. **How does a canonical object catalogue of ~55 named objects stay
   consistent with four frameworks that already ship**, without either
   contradicting shipped code or freezing the canonical model to only
   what has been built?
3. **How are relationships between wildly different object families**
   (a Part manufactured by a Supplier, a Risk blocking a Milestone, a
   Drawing documenting an Assembly) **expressed uniformly**, without a
   combinatorial explosion of per-pair relationship types?
4. **How does lifecycle stay both universal and discipline-specific** —
   every object needs *a* lifecycle, but Requirements' own seven-state
   model is already real and already differs from a generic Draft →
   Released → Obsolete arc?
5. **Where does configuration management (baselines, revisions,
   releases) live**, given a revision mechanism already exists but no
   baseline/release concept has been built yet?

## 3. The Engineering Object — Canonical Shape

Every Engineering Object, real or future, is defined by five facets,
directly reusing what `Tempest.Core.EngineeringData` already provides
for the first two and naming the remaining three as canonical
architecture for the first time:

| Facet | Canonical Answer | Grounding |
|---|---|---|
| **Identity** | A permanent `Guid` (never reassigned) plus an optional, stable, human-readable business identifier (never the primary key) | `IEngineeringDocument.Id`; `Requirement.Identifier`/`MaterialSpecification.MaterialId` precedent |
| **Content/Behaviour** | Opaque to the platform — a `Kind` string names what an object *is*; the platform never interprets content, only stores and versions it | `IEngineeringDocument.Kind`; `Engineering Principle 3` |
| **Ownership/Metadata** | A common metadata envelope (`Metadata Specification.md`) — author, timestamps, revision, status, category, and family-specific fields, never conflated with content | `IDocumentRevision.AuthorPrincipalId`/`CreatedAt`; `ICurrentPrincipalAccessor` |
| **Lifecycle** | A closed, explicit, caller-driven state table drawn from the canonical lifecycle vocabulary (`Lifecycle Specification.md`), specialised per object family | `RequirementStatus`/`RequirementStatusTransitions` precedent |
| **Relationships** | Directed, typed `DocumentReference`s (`Relationship Catalogue.md`), open-string `RelationshipKind`, Kind-agnostic at both ends | `DocumentReference`/`LinkAsync`/`GetReferencesAsync`; `Engineering Principle 31` |

**An Engineering Object is not a new base class or interface.** It is
an architectural description of a shape every `Kind` value already
committed to, and every future `Kind` value must commit to — realised,
at implementation time, exactly as today: as a `Kind` string over the
existing `IEngineeringDocumentStore` (`ADR-0072`).

## 4. Governance

Every Engineering Object's own governance surface is composed from
already-existing, already-shipped mechanisms, never a new one:

- **Audit history** — every mutating action is recorded by the calling
  layer via the existing `IAuditRecorder.RecordAsync`, attributed to
  the current principal; never internal to an object's own framework
  (unchanged calling-layer-responsibility discipline, `WP7.2B
  Requirements Platform Architecture.md` §3/§4).
- **Change history** — the object's own `IDocumentRevision` sequence,
  already append-only and immutable.
- **Review/approval history** — modelled as Engineering Objects in
  their own right (`Review`, `Approval`, `Decision` — Canonical Object
  Catalogue), related to the object they concern via a typed
  relationship (`Relationship Catalogue.md`), never as fields on the
  reviewed object itself — the same "a relationship, never a field"
  discipline `IRequirement` already applies to what satisfies/verifies
  it.
- **Evidence requirements** — an `Evidence` Engineering Object family,
  composed by traversal (`Digital Thread Specification.md`), never a
  new stored aggregate — the same discipline `IRequirementEvidence`
  already applies.
- **Retention/Archive policy** — realised entirely through lifecycle
  (`Archived`/`Obsolete` states, `Lifecycle Specification.md`); no
  object is ever physically deleted, only transitioned (`Validation
  Specification.md` §Deletion Rules).

## 5. Security

Named at the architecture level only — no access model is designed or
built here, and none is required to exist before this Work Package's
own Definition of Done is satisfied:

- **Classification, Export Control, ITAR, Security Clearance** — each
  is a metadata field on the common envelope (`Metadata
  Specification.md`), an open, caller-defined value today — exactly as
  `Category` already is — never a closed, platform-enforced vocabulary
  until a real requirement demonstrates one is needed.
- **Access model** — deliberately not designed here. `Tempest.Core.Identity`
  already provides `IPermissionEvaluator`/`Permission`, used today by
  `IVerificationService.GetVerificationHistoryAsync`
  (`verification.read`) as the calling-layer enforcement precedent —
  a future permission model for the full canonical object set extends
  that same mechanism, not a new one.
- **Sensitive engineering data** — handled by the same calling-layer
  permission-gating precedent, never a field-level encryption or
  redaction mechanism designed speculatively here.

## 6. Search

Named at the architecture level only, consuming existing mechanisms:

- **Global search / Object indexing** — every Engineering Object is
  already uniquely addressable by `Guid` and `Kind`; a future search
  capability indexes the existing `IEngineeringDocumentStore` content
  and metadata envelope, introducing no second object registry.
- **Filtering, Grouping, Sorting** — operate over the common metadata
  envelope (`Metadata Specification.md`) fields every object already
  carries, uniformly, regardless of family.
- **Saved searches, Engineering queries** — a future capability, not
  designed here; named so a future Work Package does not need to
  re-derive that the metadata envelope is the correct foundation for
  it.

## 7. Extensibility

- **Future object registration** — a new canonical object is added to
  the Catalogue by naming a new `Kind` value and, if it needs one, a
  specialised lifecycle table — never a new storage mechanism. This
  mirrors `ADR-0067`'s own Kind-keyed extensibility precedent from the
  Workspace, applied here to the Engineering Core itself.
- **Custom engineering objects / Module-defined objects** — the
  Canonical Object Catalogue's own final entry, `Custom Object
  extension mechanism`, exists precisely for this: a module may declare
  its own `Kind` without platform review, provided it honours the
  canonical shape (§3) — the same discipline every sample module's own
  scoped `Kind` constant (`SampleComponent`, `SampleEngineeringDocument`)
  already demonstrates informally, today.
- **Module-defined relationships** — likewise an open `RelationshipKind`
  string a module may mint without platform review — `ADR-0073` names
  this explicitly as platform architecture, not merely Requirements'
  own convention.
- **Compatibility rules / Versioning expectations** — a `Kind` value,
  once shipped, is never redefined incompatibly (`Engineering Principle
  1`'s own "identity never changes" discipline extended to `Kind`
  itself); a genuinely new shape gets a genuinely new `Kind`, never a
  silent redefinition.

## 8. ADR Summary

Three genuine, platform-wide architectural decisions, formalising
patterns every existing framework already independently converged on,
now stated once as binding on every current and future module:

| ADR | Decision |
|---|---|
| `ADR-0072` | Every canonical Engineering Object is an `IEngineeringDocumentStore`-backed `Kind`, never a new storage/type hierarchy |
| `ADR-0073` | Relationships between Engineering Objects are open-string, unvalidated-vocabulary `DocumentReference`s, platform-wide — never a closed relationship-type enum |
| `ADR-0074` | Lifecycle status is a common canonical vocabulary, specialised per object family — never one rigid global enum, never fully ad hoc per module |

## 9. Summary of Companion Deliverables

| Deliverable | Covers |
|---|---|
| `WP8.2A Canonical Object Catalogue.md` | All ~55 named Engineering Objects, grouped into families, each reconciled against shipped code where it exists |
| `WP8.2A Relationship Catalogue.md` | Every named relationship kind — direction, ownership, lifecycle implications |
| `WP8.2A Lifecycle Specification.md` | The canonical lifecycle vocabulary, per-family specialisation, transition tables, approval gates |
| `WP8.2A Configuration Management Specification.md` | Revision, version, baseline, snapshot, configuration item, branching philosophy |
| `WP8.2A Digital Thread Specification.md` | The full traceability chain, allowable/forbidden links, traversal rules |
| `WP8.2A Metadata Specification.md` | The common metadata envelope every Engineering Object carries |
| `WP8.2A Validation Specification.md` | Required/optional fields, relationship/lifecycle/approval constraints, deletion rules, reference integrity |
| `WP8.2A Engineering Object Interaction Diagrams.md` | Mermaid diagrams — family relationships, digital thread flow, lifecycle states, worked traversal examples |

## Related Documents

`docs/engineering/Engineering Principles.md`; `docs/releases/v0.7.0/
WP7.0C Engineering Foundation Contracts.md`; `docs/releases/v0.7.0/
WP7.2B Requirements Platform Architecture.md` and its own Digital
Thread Architecture/Domain Model companions; `docs/releases/v0.7.0/
WP7.2C Requirements Platform Contracts.md`; `ADR-0053`, `ADR-0058`;
`src/Tempest.Core/EngineeringData/`; `src/Tempest.Core/Requirements/`;
`src/Tempest.Core/Verification/`; `src/Tempest.Core/Materials/`;
`src/Tempest.Core/Calculations/`.
