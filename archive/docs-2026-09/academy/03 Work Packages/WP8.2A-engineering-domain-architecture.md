# WP 8.2A — Engineering Domain Architecture

## What This Document Is

An architecture-only milestone Work Package, mirroring `WP7.2B
Requirements & Verification Platform Architecture`/`WP8.0A Engineering
Workspace Architecture`'s own whole-review format — no production code
written, no implementation performed. This document follows the same
whole-review shape (What Was Achieved, Architectural Lessons,
Implementation Lessons, Repository Maturity, Recommendations, Key
Takeaways) rather than the standard 13-section per-feature template,
since no code exists for that template's own "Files Added"/"Trade-offs"
sections to describe.

## Introduction

`WP 8.2A` is `v0.8.0`'s own seventh Work Package, and the first
Engineering-Core-facing architecture Work Package since `WP 7.2B`. It
defines the complete canonical Engineering Domain Architecture — every
Engineering Object TempestOS recognises, across roughly fifty named
families, with identity, lifecycle, relationships, traceability,
governance, and validation rules — grounded explicitly in the four
Engineering Core frameworks that already ship, rather than designed in
isolation from them.

## What Was Achieved

Nine documents produced under `docs/releases/v0.8.0/`: the master
`Engineering Domain Architecture.md` (the canonical Engineering
Object shape, Governance/Security/Search/Extensibility sections, ADR
summary), `Canonical Object Catalogue.md` (49 named objects across
thirteen families, five reconciled against real shipped `Kind`
constants, forty-plus honestly marked `Conceptual`), `Relationship
Catalogue.md` (twenty named relationship kinds, direction/ownership/
lifecycle for each, reconciled against `RequirementRelationshipKinds`/
`VerificationService`'s own shipped constants), `Lifecycle
Specification.md` (the canonical eight-state vocabulary and default
transition table, `RequirementStatus` reconciled as a real
specialisation), `Configuration Management Specification.md` (revision/
version/baseline model, reusing `IRequirementCollection`'s own shipped
pattern for Baseline, branching deliberately out of scope),
`Digital Thread Specification.md` (the full Requirements→Release chain
extended from `WP7.2B Digital Thread Architecture.md`'s own proven
three-stage chain), `Metadata Specification.md` (the common metadata
envelope, reconciling `MaterialPropertyConfidenceLevel` as a
platform-wide vocabulary), `Validation Specification.md` (required/
optional fields, relationship/lifecycle/approval constraints, deletion
rules, reference integrity — each disclosing whether shipped code
already enforces it), and `Engineering Object Interaction
Diagrams.md` (five mermaid diagrams). Three new ADRs
(`ADR-0072`–`ADR-0074`). Zero `src/`/`tests/` change of any kind.

## Architectural Lessons

**Four independently-designed frameworks converging on the identical
shape, without coordination, is the strongest possible evidence for
what platform architecture should say — stronger than designing from
first principles.** Every fact this Work Package's own canonical model
states (Kind-backed identity, open-string relationships, closed-table
lifecycle) was independently discovered by `Tempest.Core.Requirements`,
`Verification`, `Materials`, and `Calculations`, each solving its own
narrower problem. `WP 8.2A`'s own genuine contribution is not
invention — it is *noticing* the convergence and stating it once as
binding architecture, extending its reach to forty-plus objects that
do not exist yet. This is a repeatable lesson: when several
independently-built frameworks keep reaching the same design, that
convergence is itself architectural evidence, worth formalising
explicitly rather than leaving as an unstated coincidence.

**Reconciling a new canonical model against real, shipped code found
genuine, disclosable gaps rather than confirming a clean fit
everywhere.** `RequirementStatus` omits four of the eight canonical
lifecycle states entirely; `RequirementStatusTransitions` permits
`Reviewed → Approved` with no linked-evidence check, while this Work
Package's own canonical rule requires one; the shipped
`VerificationRecord` combines "Verification Activity" and "Verification
Result" into one object where the controlling instruction names two.
None of these are treated as defects in the shipped code — each is
named as a disclosed, deliberate reconciliation, exactly the same
"disclose, don't hide, don't silently redesign" discipline
`WP 8.1A`/`WP 8.1B`/`WP 8.1C` already established for implementation
findings, now proven to apply equally well to a pure architecture
Work Package reconciling against prior work.

## Implementation Lessons

Not applicable in the usual sense — no implementation was performed.
The closest analogue: designing the Relationship Catalogue's own
Parent/Child convention (source = parent, target = child) surfaced
that it runs opposite to `RequirementGroup`'s own shipped
`groupedUnder` direction (child → parent). Rather than either quietly
picking a convention that happened to contradict shipped code, or
forcing this catalogue to adopt Requirements' own historical direction
platform-wide, this Work Package disclosed the divergence explicitly
and reasoned about *why* each direction was chosen (dominant query
pattern per family) — a genuine, non-obvious design decision a purely
architecture-only Work Package still had to make carefully, without
being able to verify it against a compiler.

## Repository Maturity

**Every existing framework's own relationship/lifecycle/revision
mechanism was checked directly against its real, shipped source before
being cited as precedent** — `RequirementStatus`,
`RequirementStatusTransitions`, `RequirementRelationshipKinds`,
`VerificationService`'s own relationship constants,
`MaterialPropertyConfidenceLevel`, and `IDocumentRevision` were all
read directly from `src/Tempest.Core/`, not recalled from memory or
assumed from prior Work Package summaries. This confirmed the
"convergence" this Work Package's own central claim depends on is real,
not an assumption — the same verification discipline `WP 8.1B` already
established for ADR worked examples (`ADR-0071`), applied here to an
entire architecture's own grounding claim before any of the nine
deliverables were written.

## Recommendations for the Next Work Package

1. **A real Physical/Configuration Engineering Discipline Module**
   (Assembly, Sub-Assembly, Part, Component) is the natural next proof
   of this canonical model against a genuinely new discipline, mirroring
   Requirements' own historical role as the first proof of the
   Engineering Data Model (`WP 7.0C` → `WP 7.3A`). It should follow the
   same two-stage architecture-then-contracts discipline every prior
   framework used, grounded directly in this Work Package's own
   Canonical Object Catalogue/Relationship Catalogue/Lifecycle
   Specification rather than re-deriving them.
2. **Close the Verification Activity/Verification Result gap**
   (`Canonical Object Catalogue.md` §3's own disclosed note) if a real
   discipline module surfaces a genuine need for a separately-persisted,
   pre-outcome verification activity — not speculatively, absent that
   need.
3. **A real Baseline implementation**, proving `Configuration
   Management Specification.md` §3's own reuse of the
   `RequirementCollection` pattern, extended with frozen
   revision-number pinning, against real code.
4. **Do not build branching.** `Configuration Management
   Specification.md` §6 names this as a deliberate, disclosed scope
   boundary — no real, demonstrated need has surfaced across five
   shipped frameworks; this remains true until one does.

## Key Takeaways

1. Naming a convergence four independent frameworks already reached,
   once, as binding architecture, is a legitimate and valuable
   architectural contribution — it does not require inventing a fifth,
   different answer to feel like real architectural work.
2. A canonical catalogue of fifty objects is strengthened, not
   weakened, by honestly marking forty of them `Conceptual` — the
   alternative (implying they are further along than they are) would
   mislead every future Work Package that reads this catalogue as its
   own starting point.
3. Reconciling new architecture against real, shipped code is where
   the genuine, non-obvious findings surface (the Requirement lifecycle
   gap, the Verification Activity/Result gap, the Parent/Child
   direction question) — designing in isolation from what already ships
   would have missed all three.

## Related Documents

`docs/releases/v0.8.0/WP8.2A Engineering Domain Architecture.md` and
its eight companion deliverables; `ADR-0072`; `ADR-0073`; `ADR-0074`;
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`;
`docs/releases/v0.7.0/WP7.2B Requirements Platform Architecture.md`
(the format precedent this document follows); `docs/engineering/
Engineering Principles.md`.
