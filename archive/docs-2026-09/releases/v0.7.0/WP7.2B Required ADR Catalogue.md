# WP 7.2B — Required ADR Catalogue

## Status

**A catalogue of anticipated ADRs, not finished ADR documents** — mirrors
`WP7.0C Required ADR Catalogue.md`'s own identical role for the original
five Engineering Foundation frameworks. None of the three entries below
is written as an `Accepted`-status file under `docs/adr/`; each is
deferred to the owning implementation Work Package's own dedicated
architecture-confirmation pass. Numbering begins at `ADR-0058` — the
highest existing ADR is `ADR-0057-verification-framework-relationship-
to-audit-and-method-vocabulary.md`, confirmed directly against
`docs/adr/` immediately before this document was written.

## The List

### ADR-0058 — Requirements Platform: Classification, Storage, and Relationship to the Engineering Data Model

**Context.** `WP7.2B Requirements Platform Architecture.md` §2 proposes
classifying the Requirements Engine as a Platform Service under
`ADR-0013`, following the identical precedent Materials, Calculations,
and Verification already established — nothing else strictly requires
it to boot, but it is shared infrastructure other future modules build
on. §4 proposes building it directly on `Tempest.Core.EngineeringData`
(`Kind = "Requirement"`), introducing no second storage mechanism.

**Anticipated decision.** Confirm (or revise) the Platform Service
classification; confirm (or revise) the direct `IEngineeringDocumentStore`
dependency, with no new storage abstraction.

**Alternative considered and rejected.** Classifying Requirements as a
Module (or set of modules) rather than a Platform Service was considered
and not adopted as this review's own default — `ADR-0013`'s own test
("does the rest of the platform need this to exist before it can
function at all?") answers "no" literally, the same answer that would
have classified Materials, Calculations, and Verification as modules
too, had the test been applied that literally. This review's own
finding — that "Platform Service" in this repository's actual practice
has always meant "shared cross-cutting infrastructure," not strictly
"boot-critical" — should be the owning Work Package's own starting
point, not re-litigated from first principles.

### ADR-0059 — Requirement Identity, Status, and Category Representation

**Context.** `WP7.2B Requirements Domain Model.md` §9, §11, §12
identifies three open representation questions this architecture
deliberately leaves unresolved: whether `Requirement Status` is a closed
enum (mirroring `VerificationOutcome`) or an open string (mirroring
`IMaterialSpecification.Category`); the exact shape of the business
identifier index (mirroring `MaterialCatalog`'s own direct
`IPersistenceStore` dependency, `ADR-0055` Decision 3); and whether
`Requirement Category` should remain a fully open string or gain any
structure at all.

**Anticipated decision.** Decide each of the three independently — this
domain model's own finding is that both existing Engineering Core
precedents (a closed enum for `VerificationOutcome`; an open string for
`Category`) are individually correct for their own concept, and the
owning Work Package should not assume uniformity is itself a virtue.

**Alternative considered and rejected.** Forcing all three (Status,
Identifier, Category) to a single, uniform representation style (all
closed enums, or all open strings) was considered, for internal
consistency's own sake, and not adopted as this review's own
recommendation — `Materials` and `Verification` already demonstrate that
this platform's own convention is "choose the representation the
specific concept's own maturity warrants," not "represent every
classification-like field identically."

### ADR-0060 — Requirement Concurrency and Traceability Integrity Model

**Context.** `WP7.2B Security Architecture.md` discloses a genuine,
new gap: `ReviseAsync`'s own per-document lock prevents two concurrent
revisions from colliding on revision number, but provides no
compare-and-swap or "expected prior revision" check — two authors
editing the same requirement concurrently could each succeed, with the
second silently becoming current. No such conflict-detection mechanism
exists anywhere in `Tempest.Core.EngineeringData` today, since no prior
Engineering Core consumer has needed one (Materials, Calculations, and
Verification are each dominated by single-author or system-generated
writes, not the multi-stakeholder collaborative editing a Requirements
Engine's own target users would plausibly need).

**Anticipated decision.** Confirm whether this gap must be resolved
before the Requirements Engine's own implementation ships (a real,
demonstrated multi-author collaborative-editing need), or whether it is
correctly deferred as disclosed, accepted debt — mirroring `TD-18`'s own
identical "no measured problem yet" disclosure discipline — until a real
need is demonstrated.

**Alternative considered and rejected.** Building an optimistic-
concurrency check (an expected-revision-number parameter on
`ReviseAsync`) speculatively, as part of this architecture phase itself,
was considered and not adopted — no real, demonstrated multi-author
editing scenario exists yet for *any* Engineering Core consumer, and
building the mechanism ahead of that evidence would violate Security
Principle 7's own "do not build ahead of demonstrated need" discipline,
applied here to a correctness mechanism rather than a security one.

## Cross-Reference Check

Every entry above cites a specific `WP7.2B` companion document and,
where applicable, an existing ADR or Technical Debt item its own
anticipated decision would extend (`ADR-0013`, `ADR-0055`, `TD-18`). No
open question disclosed anywhere else in this Work Package's own
deliverables is missing an entry here.

## Related Documents

`docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md` (the precedent
this catalogue's own structure follows); `WP7.2B Requirements Platform
Architecture.md`; `WP7.2B Requirements Domain Model.md`; `WP7.2B
Security Architecture.md`; `docs/adr/` (`ADR-0001`–`ADR-0057`, the
existing sequence this catalogue extends).
