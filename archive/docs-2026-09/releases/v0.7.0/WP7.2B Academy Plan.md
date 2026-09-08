# WP 7.2B — Academy Plan

## Purpose

Identifies the existing Academy material an engineer should read before
starting the Requirements & Verification Platform's own future
implementation Work Package, and the new Academy material that Work
Package is expected to produce once it completes — mirroring `WP7.0C
Academy Plan.md`'s own role for the original five Engineering Foundation
frameworks.

This document does not itself add anything to `docs/academy/Academy
Index.md` — no new concept guide exists yet for the Requirements
Platform, since none is implemented. This Work Package's own retrospective
(a whole-review, architecture-only document, not a 13-section
implementation template) is this Work Package's own sole Academy
contribution — see `WP7.2B Lessons Learned.md`.

## Cross-Cutting Required Reading (Owning Implementation Work Package)

- `docs/academy/06 Engineering Standards/Engineering Governance.md` —
  the constitution the implementation Work Package still operates under.
- `VISION.md` — the product ambition this Platform exists to serve.
- `docs/engineering/Engineering Principles.md` — all 28 principles,
  none of which this Work Package extends (see "Engineering Principles
  Review," below).
- `WP7.2A Recommended Programme.md` — why this programme, over the six
  other candidates evaluated.
- `WP7.2B Requirements Platform Architecture.md`, `Systems Engineering
  Architecture.md`, `Digital Thread Architecture.md`, `Requirements
  Domain Model.md` — the specific, proposed design for this Platform and
  the open questions its own architecture-confirmation pass must
  resolve.

## Required Reading, By Analogy to the Closest Existing Pattern

- `13-calculation-framework.md` — the closest existing "why a second
  registry/service exists, distinguished from a structurally similar
  one" concept guide; the Requirements Platform's own future concept
  guide will need the same discipline, distinguishing a `Requirement`
  from an ordinary `IEngineeringDocument` generally (the same "worked
  example vs. genuinely new pattern" judgement `WP7.0C Academy Plan.md`
  applied to Materials).
- `14-verification-framework.md` — required reading specifically for
  the Verification Integration section (`WP7.2B Platform Integration
  Report.md` §1) — understanding why Verification and Requirements
  remain separate, composed mechanisms, never merged, is the same
  discipline this guide already teaches for Verification and Audit.
- `WP7.1C-materials-framework-implementation.md` — required reading for
  the business-identifier index pattern (`MaterialCatalog`'s own
  `materialId` lookup, `ADR-0055` Decision 3) the Requirements Platform's
  own `Requirement Identifier` concept directly reuses.

## Required Output (Owning Implementation Work Package)

- A 13-section implementation retrospective under `docs/academy/03 Work
  Packages/`, following the standard template every Engineering
  Foundation implementation Work Package used.
- **A new concept guide is required** — the Requirements Platform
  introduces a genuinely new pattern this platform has not taught
  before: a domain concept (`Requirement`) that is simultaneously an
  ordinary `IEngineeringDocument` *and* the subject of a three-layer
  architectural distinction (Engineering Core → Systems Engineering
  Foundation → Engineering Discipline Modules) no prior Academy content
  has needed to explain. Recommended title: "The Systems Engineering
  Foundation" or equivalent, teaching the three-layer model
  (`WP7.2B Systems Engineering Architecture.md`) as its own primary
  content, with the Requirement/Verification/Audit three-way
  distinction as a secondary, supporting section (extending, not
  duplicating, `14-verification-framework.md`'s own existing
  Verification-vs-Audit-vs-Calculation-Record comparison).
- Updates to `docs/architecture/Platform Service Map.md` and
  `Engineering Glossary.md`, following the identical pattern every prior
  Platform Service addition already established.

## Summary Table

| Deliverable | New Concept Guide? | Rationale |
|---|---|---|
| Requirements & Verification Platform (owning implementation Work Package) | **Yes** | Genuinely new pattern — the first Engineering Foundation-adjacent capability requiring its own `ADR-0013` classification decision, and the first to introduce a three-layer architectural model this Academy has not taught before |

## Engineering Principles Review

**Finding: no extension to `docs/engineering/Engineering Principles.md`
is warranted by this Work Package, and none is added.** Every one of
the document's own existing 28 principles was derived from real, shipped,
tested code — its own Status section states this discipline explicitly
for all five prior extensions ("derived from working code, not asserted
in advance of it"). This Work Package produces **architecture only, no
implementation** — there is no working code yet to derive a genuine
Systems Engineering principle from. Proposing a new principle now would
violate this document's own single most important discipline, stated in
its own Purpose section: "Only principles the implemented architecture
actually demonstrates are listed below."

**This is itself a confirming, not a negative, finding.** The correct
next update to `Engineering Principles.md` is deferred to the owning
implementation Work Package, exactly as every one of the five existing
extensions was made only once its own framework had real, tested code
behind it — this Work Package's own restraint here is consistent
application of the same rule, not an oversight.

## Related Documents

`docs/releases/v0.6.0/Academy Plan.md`; `WP7.0C Academy Plan.md` (the
precedent this document's own structure follows); `WP7.2B Requirements
Platform Architecture.md`; `docs/engineering/Engineering Principles.md`;
`docs/governance/Documentation/Academy Register.md`.
