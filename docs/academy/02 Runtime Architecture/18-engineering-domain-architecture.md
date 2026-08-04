# Engineering Domain Architecture

## 1. Introduction

The Engineering Domain Architecture (`WP 8.2A`, contracted `WP 8.2B`,
implemented `WP 8.2C`, `ADR-0072`–`ADR-0079`) is TempestOS's own
canonical statement of what an Engineering Object is — the shape every
one of roughly fifty named object families (Requirement, Assembly,
Risk, Supplier, Milestone, and so on) commits to, whether it ships
today or in a future release. `WP 8.2A` was architecture-only: no code,
no interfaces, no persistence, no UI. `WP 8.2B` then converted that
architecture into the complete public contract — proposed, uncompiled
C# for every one of the ~49 canonical objects plus their own supporting
facet/relationship/lifecycle/validation/traceability contracts — still
no implementation, no concrete classes, no persistence. `WP 8.2C` then
compiled every one of those contracts and gave 38 of the ~49 canonical
objects a real, tested concrete class, backed by a small, shared
implementation framework (`EngineeringObjectBase`, generic factories, a
new in-memory repository layer) — the first Engineering Domain code
that actually runs. This document teaches why the model looks the way
it does, why its own contracts and implementation are shaped the way
they are, and how all three relate to the four Engineering Core
frameworks (`Tempest.Core.Requirements`, `Verification`, `Materials`,
`Calculations`) that already ship and already, independently, converged
on the same shape.

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
5. **How does "composition over inheritance" become a checkable
   contract rule**, not merely a stated intention, across forty-nine
   interfaces that each need some, but not all, of about twenty common
   behaviours — `ADR-0075`'s own question?
6. **How do seventeen named relationship categories become real
   contracts without silently reopening `ADR-0073`'s own already-locked
   "open string, never a closed enum" decision** — `ADR-0076`'s own
   question, the most direct tension `WP 8.2B`'s own controlling
   instruction created against `WP 8.2A`'s own prior work?
7. **How does "no persistence" for a new implementation Work Package
   coexist with `ADR-0072`'s own mandate that every canonical object is
   `IEngineeringDocumentStore`-backed** — `ADR-0077`'s own question,
   `WP 8.2C`'s direct analogue of `ADR-0076`'s tension one layer down,
   at the implementation stage rather than the contract stage.
8. **Do the five canonical Kinds an existing framework already
   implements get a second, competing concrete realisation from the new
   shared framework, or not** — `ADR-0078`'s own question, forced by
   `WP 8.2C`'s own simultaneous instructions to "implement every
   canonical Engineering Object class" and to write no Requirements/
   Verification/Calculations logic.
9. **Does "one factory per Kind" (`WP8.2B Dependency Rules.md` §7) mean
   one hand-written factory type per Kind, or one instance** — `ADR-0079`'s
   own question, answered before it produced sixty near-identical
   classes.

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

`WP 8.2B` then gave every one of those facets a real, proposed
interface shape. `IEngineeringObject` mirrors `IEngineeringDocument`
directly (identity); ten small facet interfaces —
`IHasBusinessIdentifier`, `IHasMetadata`, `IHasLifecycle`,
`IHasRevisions`, `IHasRelationships`, `ITraceable`, `IValidatable`,
`IHasAttachments`, `ISearchable` — cover metadata/lifecycle/revision/
relationships/validation, composed into each of the ~49 canonical
object interfaces only as needed, never as one inherited monolith
(`ADR-0075`). Every relationship — all seventeen named categories — is
realised as one interface, `IEngineeringRelationship`, carrying an open
`RelationshipKind` string (unchanged from `ADR-0073`) and a
`RelationshipCategory` enum as *descriptive metadata only*, never
validated against the string at write time (`ADR-0076`) — resolving
the direct tension between "define contracts governing seventeen
relationship categories" and "relationships are open-string, never a
closed enum" by recognising governing a category can mean documenting
a convention, not defining a closed type.

`WP 8.2C` then compiled all of this and gave it a working implementation.
`EngineeringObjectBase` is one shared, concrete class implementing every
facet interface unconditionally — a concrete Kind class inherits
whichever subset its own interface actually declares, at no extra
implementation cost, since the plumbing already exists once. Every
canonical object's own real storage still flows through
`IEngineeringDocumentStore`, exactly as `ADR-0072` requires — reused,
in production, from the same shared, already-registered instance every
other Engineering Core framework already resolves (`ADR-0077`); a new,
purely in-memory `IEngineeringObjectRepository`/
`IEngineeringRelationshipRepository` pair is the genuinely new
"in-memory repositories" layer, answering the one question
`IEngineeringDocumentStore` cannot ("list every object of Kind X").
Five canonical Kinds already owned by an existing framework
(`Requirement`, `RequirementCollection`/`Group`, `VerificationRecord`,
`CalculationRecord`, `MaterialSpecification`) compile as Domain
interfaces but receive no competing concrete class here — that
realisation remains exactly where `WP 8.2A` already placed it
(`ADR-0078`). The remaining 38 canonical objects each get a small
concrete class, constructed through one of two generic factory types
(`EngineeringObjectFactory<T>`, `EngineeringRelationshipFactory`),
instantiated once per Kind by whichever composition root needs it —
never a hand-written factory class per Kind (`ADR-0079`).

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

**One monolithic `IEngineeringObject` interface carrying every common
behaviour** — considered and rejected; see `ADR-0075`. Would force
objects with no natural need for a behaviour (an `IApproval`, a
point-in-time event, forced to carry `ReviseAsync`) to implement
meaningless members.

**Seventeen separate per-category relationship interface types**
(`IParentRelationship`, `IVerificationRelationship`, ...) — considered
and rejected; see `ADR-0076`. Would be a closed set by construction,
directly contradicting `ADR-0073`'s own already-locked decision one
Work Package earlier in the same release.

**Registering a second, in-memory `IEngineeringDocumentStore` as the
Host's own production store** — considered and rejected; see `ADR-0077`.
Would silently break every existing Engineering Core framework's own
persistence, or require a competing dual-registration scheme this
platform has no precedent for.

**Giving the five already-Implemented canonical Kinds their own new
concrete classes**, either under the same `Kind` strings (risking two
incompatible writers for one Kind) or under new, Domain-prefixed ones
(permanently forking "what a Requirement is" in two directions) —
considered and rejected; see `ADR-0078`.

**One hand-written factory class per Kind** (~38 object factories plus
~20 relationship factories) — considered and rejected; see `ADR-0079`.
Pure boilerplate proportional to how many Kinds a catalogue happens to
enumerate, the identical reasoning `ADR-0076` already applied to
relationship types one Work Package earlier.

## 7. Why This Solution Was Chosen

It is the first Work Package to generalise "reuse what already exists"
from a per-framework discipline (proven four separate times) into a
stated platform architecture — the natural next step once four
independent conclusions are the same conclusion, and the one this
Work Package's own Definition of Done (a new team could implement the
whole platform from this specification alone) actually requires. `WP
8.2B` extends the same discipline one layer further: every contract
decision it made was checked against `WP 8.2A`'s own prior decisions
first, resolving apparent tensions (relationship categories vs. open
strings) rather than silently picking whichever reading of the
controlling instruction was easiest.

## 8. Architectural Principles

- **Composition Over Inheritance** — the canonical shape is a set of
  facets an object's own `Kind` commits to, not a base class hierarchy.
  This governs *contracts*: no canonical object **interface** inherits
  from more than one other canonical object interface (`ADR-0075`).
  `WP 8.2C` does introduce a shared `EngineeringObjectBase` **class** —
  a deliberate, disclosed, and orthogonal choice: ordinary
  implementation reuse (mirroring `ModuleLifecycleBase`'s own identical
  role for modules), never inherited by more than one canonical object
  interface's own concrete class, and never itself part of any public
  contract a caller programs against.
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
  complete, load-bearing domain vocabulary — and now a complete,
  proposed contract — to build against, with no Contract Review of its
  own needed to invent common facets it can simply compose.
- Every one of the ~49 canonical object interfaces is a small,
  composed shape (`ADR-0075`) — an implementing class knows exactly
  which behaviours it must support by reading its own interface list,
  never by inheriting a large surface and discovering which members
  actually matter.
- Thirty-nine canonical objects are no longer merely named — they
  compile, are constructible through a working factory, are queryable
  through a real in-memory repository, and are exercised end to end by
  a sixteen-object representative graph (`EngineeringDomainSampleModule`),
  proving the architecture and its contracts actually hold together in
  running code, not only on paper.
- A future discipline module inherits identity, metadata, lifecycle,
  revision, relationship, traceability, validation, and search
  behaviour simply by deriving from `EngineeringObjectBase` and
  declaring which of the ~49 interfaces it realises — zero new
  plumbing code required, mirroring exactly how a new sample module
  inherits lifecycle no-ops from `ModuleLifecycleBase` today.

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
- `RelationshipCategory` (`ADR-0076`) carries no structural guarantee it
  matches the real `RelationshipKind` string on the same relationship —
  the same already-accepted trade-off `ADR-0073` made for
  `RelationshipKind` alone, now extended one field further.
- The ten facet interfaces (`ADR-0075`) are more surface area to learn
  at once than one large interface would be — accepted because each is
  individually small and the mapping from the twenty named "Common
  Behaviour" concerns to the ten facets is documented once
  (`WP8.2B Interface Catalogue.md` §1), not left for a reader to infer.
- Two `IEngineeringDocumentStore` implementations now exist in the
  repository (`ADR-0077`) — a reader unfamiliar with the ADR could
  reasonably ask why a second one exists; disclosed in both types' own
  code comments, not hidden.
- `IRequirement`/`IRequirementSet`/`IVerificationResult`/
  `ICalculationResult`/`IMaterial` compile but cannot currently be
  constructed by anything in `Tempest.Core.EngineeringDomain` —
  `ADR-0078`'s own disclosed cost of not duplicating an already-owned
  Kind.
- `WP8.2B Interface Catalogue.md`'s own `IRelease : IBaseline : IConfiguration`
  chain is three levels of canonical-object specialisation deep,
  directly contradicting `WP8.2B Dependency Rules.md` §6's own "at most
  one level" rule — found during implementation, compiled exactly as
  frozen (interfaces are not `WP 8.2C`'s to silently correct), and
  disclosed here as a genuine authoring inconsistency in `WP 8.2B`'s
  own deliverables, not corrected.

## 11. Common Mistakes

The mistake most worth naming: treating a `Conceptual` catalogue entry
as if naming it were the same as building it. Every one of the forty
`Conceptual` objects in `WP8.2A Canonical Object Catalogue.md` needs a
real implementation Work Package — most likely following the same
two-stage architecture-then-contracts discipline every shipped
framework already used — before any code can depend on it. This
document is a vocabulary and a shape, not a promise that the vocabulary
is already usable. `WP 8.2B`'s own contracts inherit this discipline
directly: a proposed, uncompiled `IPart` interface is not an
implemented `Part` class — nothing in `WP8.2B Interface Catalogue.md`
compiles, by design, and no future Work Package should assume otherwise
without checking.

A second, `WP 8.2B`-specific mistake worth naming: reading a product
brief's own literal structure ("define contracts governing" seventeen
named categories) as necessarily requiring seventeen separate types.
Resolving what a requirement actually needs (governance, in the sense
of documented rules) against what a prior, binding decision already
settled (`ADR-0073`) is real architectural work — accepting the
literal reading uncritically would have quietly undone a locked-in
decision one Work Package old.

A third, `WP 8.2C`-specific mistake worth naming: assuming "implement
every canonical Engineering Object class" must mean giving every one of
the ~49 a brand-new concrete class, including the five an existing
framework already owns. The five already-Implemented Kinds are just as
"implemented" after `WP 8.2C` as before it — their concrete realisation
was never this Work Package's own to duplicate (`ADR-0078`). A reader
should not expect `Tempest.Core.EngineeringDomain` to construct an
`IRequirement`; that remains `Tempest.Core.Requirements`'s own job,
permanently, unless a future Work Package deliberately decides
otherwise.

## 12. Future Evolution

- **A real Physical/Configuration Engineering Discipline Module**
  (Assembly, Sub-Assembly, Part, Component) — now able to build directly
  on the already-compiled `IAssembly`/`ISubAssembly`/`IPart`/`IComponent`
  concrete classes `WP 8.2C` shipped, rather than starting from proposed
  contracts alone, mirroring Requirements' own role as the first proof
  of the Engineering Data Model.
- **Closing the Verification Activity/Verification Result gap**
  (`WP8.2A Canonical Object Catalogue.md` §3's own disclosed note) —
  `VerificationActivity`/`Test`/`Inspection` now exist as real, if
  generic, concrete classes; a genuine discipline-specific need should
  drive any further specialisation, not speculation (`WP8.2B`'s own
  Recommendations, unchanged).
- **A real Baseline/Release implementation**, proving `Configuration
  Management Specification.md` §3's own reuse of the
  `RequirementCollection` pattern against a genuinely frozen,
  revision-pinned membership model — `Baseline`/`Release` concrete
  classes exist; `ReferenceIntegrityChecker.CheckBaselineMembersAsync`
  is implemented and tested, but nothing yet calls it automatically on
  a lifecycle transition into `Released`.
- **Structural enforcement of the approval-gate and lifecycle-blocking
  rules** `Validation Specification.md` names but does not yet require
  any shipped code to enforce — `IValidationRuleSet` exists and is
  tested empty; registering real rules against it is a genuine next
  step, not attempted speculatively here.
- **Reconciling the five already-Implemented canonical Kinds**
  (`ADR-0078`) — retrofitting `Requirement`/`VerificationRecord`/
  `CalculationRecord`/`MaterialSpecification` to additionally implement
  their own Domain facet interfaces, once a real consumer needs it, not
  before.
- **Rebuilding the in-memory repository from the real store on Host
  startup** (`ADR-0077`'s own disclosed gap) — today, restarting the
  Host loses the repository's own by-Kind index even though the
  underlying documents themselves survive.

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
4. "Composition over inheritance," applied literally to interface
   design (many small facets, composed per object, never one shared
   monolith or a deep inheritance chain), is a checkable rule — `WP
   8.2B Dependency Rules.md` §6 states it as one a future reviewer can
   verify mechanically, not merely aspire to.
5. When a new Work Package's own controlling instruction appears to
   conflict with a prior, binding decision, the right response is
   resolving the tension explicitly (`ADR-0076`, `ADR-0077`), not
   silently following whichever reading is more literal — the same
   "disclose, don't hide" discipline this project has applied to
   implementation findings since `WP 8.1A`, now proven to apply equally
   well across three consecutive architecture/contract/implementation
   Work Packages.
6. "Composition over inheritance" is a rule about **contracts**; a
   shared concrete base class for **implementation reuse** is an
   orthogonal, ordinary technique, not a violation of it — conflating
   the two would have meant reinventing every facet's own plumbing 38
   separate times for no architectural benefit.
7. Not every named canonical object needs a new concrete class from a
   new shared framework — five of them already have a perfectly good
   one, and building a second would have been the exact discipline-specific
   duplication this Work Package's own controlling instruction explicitly
   forbade.

## Related Documents

`15-engineering-data-model.md`; `16-requirements-engine.md`;
`14-verification-framework.md`; `13-calculation-framework.md`;
`ADR-0053`, `ADR-0058`, `ADR-0072`–`ADR-0079`; `docs/releases/v0.8.0/
WP8.2A Engineering Domain Architecture.md` and its eight companion
deliverables; `docs/releases/v0.8.0/WP8.2B Engineering Domain
Contracts.md` and its seven companion deliverables; `docs/releases/v0.8.0/
WP8.2C Engineering Domain Implementation Report.md` and its companion
deliverables; `docs/engineering/Engineering Principles.md`;
`docs/academy/03 Work Packages/WP8.2A-engineering-domain-architecture.md`;
`docs/academy/03 Work Packages/WP8.2B-engineering-domain-contracts.md`;
`docs/academy/03 Work Packages/WP8.2C-engineering-domain-implementation.md`.
