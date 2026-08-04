# WP 8.2A — Engineering Domain Architecture — Configuration Management Specification

## Purpose

The revision, version, baseline, and configuration model every
Engineering Object participates in — grounded entirely in the single,
already-shipped revision mechanism (`IDocumentRevision`/
`IEngineeringDocumentStore.ReviseAsync`), introducing no second
versioning scheme (`Engineering Principle 2`, unchanged).

## 1. Revision Model (Already Shipped, Reconciled)

Every Engineering Object's own content history is the existing,
immutable, append-only `IDocumentRevision` sequence: a monotonically
increasing `RevisionNumber` (starting at 1), each entry carrying its
own `Content`, `ChangeSummary`, `AuthorPrincipalId`, and `CreatedAt`.
**This is the entire revision model.** No canonical object in this
catalogue gets a second, family-specific revision mechanism — a Part's
own revision history and a Requirement's own revision history are the
identical mechanism, differing only in what `Content` opaquely holds.

## 2. Version Model

A **version** is a human-facing label an object's own family may
choose to expose over its own raw `RevisionNumber` (for example, a
Drawing's own "Rev C" convention) — always a **derived, display-layer
concept**, never a second stored sequence. The mapping from
`RevisionNumber` to a version label is a family-local, caller-defined
convention (mirrors `Requirement.Identifier`'s own status as a
caller-defined business key layered over the real `Guid` identity) —
this specification does not mandate one universal version-label
scheme, since different disciplines have genuinely different,
long-established conventions (numeric revisions, alphabetic revisions,
semantic versions) none of which should be privileged platform-wide.

## 3. Baselines

A **Baseline** is a named, frozen set of specific object **revisions**
— not a set of objects, a set of *(object, revision-number)* pairs,
frozen at the moment the Baseline is created. This is realised via the
exact same named-collection pattern `IRequirementCollection` already
ships (`WP 7.3A`) — a Baseline **is** the `Baseline` canonical object
(`Canonical Object Catalogue.md` §11), owning membership only, extended
with one additional fact `RequirementCollection` does not currently
need: each member is recorded **with the revision number frozen at
baseline-creation time**, not merely a reference to the live object.

- **Configuration Item** — any Engineering Object eligible for
  inclusion in a Baseline; in practice, every Engineering Object is a
  potential Configuration Item (no family is structurally excluded).
- **Snapshot** — a synonym for what a Baseline captures at creation
  time, not a separate mechanism; this specification uses "Baseline"
  as the one canonical term to avoid two names for one concept.
- **Released configuration** — a Baseline whose own lifecycle state
  (`Lifecycle Specification.md`) is `Released`.
- **Working configuration** — the live, unbaselined state of a set of
  Configuration Items — not itself a stored object, simply "whatever
  the current revision of each object is right now," the default state
  before any Baseline exists.
- **Frozen baseline** — a Baseline in the `Archived` or `Released`
  state; by construction, a Baseline's own member revision numbers
  never change after creation (append-only, `Engineering Principle 4`)
  — "frozen" describes every Baseline from the moment it exists, not a
  special sub-type of Baseline.

## 4. Release

A **Release** (`Canonical Object Catalogue.md` §11) is a Baseline that
has been made available for consumption downstream — realised as a
Baseline whose own lifecycle has reached `Released`
(`Lifecycle Specification.md` §2). No second storage concept is
introduced for Release; it is Baseline plus lifecycle state, exactly as
`Engineering Change` is Change Request plus approval (§5, below).

## 5. Change Integration

A `Change Request` (`Canonical Object Catalogue.md` §11) proposes a new
revision of one or more Configuration Items; once approved, it becomes
an `Engineering Change`, `Derived From` the originating Change Request
(`Relationship Catalogue.md` §4). The Engineering Change's own effect
is expressed entirely through ordinary revisions
(`IEngineeringDocumentStore.ReviseAsync`) to the affected objects, plus
a `Supersedes` relationship from each new revision's own owning object
to whichever Baseline it invalidates, if any — no separate
change-application mechanism is introduced.

## 6. Branching Philosophy

**Deliberately minimal, and deliberately not designed further here.**
TempestOS's own revision model is strictly linear per object (each
`ReviseAsync` call produces the next sequential `RevisionNumber` — no
concept of a "branch point" exists anywhere in `IEngineeringDocumentStore`
today). This specification does **not** introduce branching:

- A genuinely divergent line of development for one Configuration Item
  is represented, today, as a **new Engineering Object**,
  `Derived From` the original — never a branch of the same object's own
  linear revision sequence.
- This is a disclosed, deliberate scope boundary, not an oversight:
  branching (true divergent, later-mergeable revision lines) is a
  materially larger persistence-and-merge-semantics problem than this
  architecture-only Work Package is scoped to solve, and no real,
  demonstrated need for it has surfaced across any of the five
  Engineering Core frameworks shipped to date.
- A future Work Package proposing real branching semantics would need
  its own Contract Review — named here as a live gap, not resolved.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Canonical Object
Catalogue.md` §1 (Physical & Configuration), §11 (Change & Release);
`WP8.2A Lifecycle Specification.md`; `IEngineeringDocumentStore`/
`IDocumentRevision` (`src/Tempest.Core/EngineeringData/`);
`IRequirementCollection` (`src/Tempest.Core/Requirements/`).
