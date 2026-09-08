# WP 7.2A — Strategic Roadmap Selection & Programme Architecture

## What This Document Is

Like `WP 7.0A`, `WP 7.0B`, `WP 6.8`, and `WP 7.1F` before it, this is not
a standard 13-section implementation retrospective — `WP 7.2A` shipped
no production code, no test, and no new public interface. It mirrors the
same whole-review shape (What Was Achieved, Architectural Lessons,
Implementation Lessons, Repository Maturity, Recommendations, Key
Takeaways), because this Work Package, like those four, is a planning
and governance milestone rather than a feature implementation.

## Introduction

With both the Platform Core (`v0.6.0`) and the Engineering Core
(`v0.7.0`, `WP 7.1A`–`WP 7.1F`) certified, TempestOS reached a point no
prior release faced: a genuinely open choice for what to build next,
with no outstanding technical dependency forcing any one answer.
`WP 7.2A`'s own controlling instruction asked this Work Package to
determine that choice from repository evidence alone, across seven
named candidate programmes, explicitly warned not to assume any one of
them (Mechanical Engineering, most pointedly) without evidence.

## What Was Achieved

All 36 `Future Capability Register.md` entries reviewed against all
seven candidate programmes; every prior planning document this project
has produced (`VISION.md`, `Product Roadmap.md`, `Capability
Categories.md`, both `WP 7.0A`/`WP 7.0B` deliverable sets, both
certification reports) re-read and cross-checked for what they actually
support, not merely summarised. Ten completion deliverables produced
under `docs/releases/v0.7.0/`, prefixed `WP7.2A`: a Strategic Roadmap
Review, an eleven-criterion Programme Comparison Matrix scoring all
seven candidates, a Capability Dependency Report, a Recommended
Programme document, a Candidate Work Package Catalogue (three new
candidates, `K`–`M`, extending `WP7.0B Candidate Work Package
Catalogue.md`'s own Candidate `I`), a Security Assessment, a Commercial
Assessment, an Engineering Assessment, an Executive Summary, and this
retrospective. **Recommendation: Programme A (Requirements & Verification
Platform), scoring 46 of 55 — the only candidate with both a completed
technical foundation and a five-release-old named platform-level hook
(`ADR-0013`).**

## Architectural Lessons

**A stated roadmap premise and an actual engineering decision can
diverge, and the correct response is to name the divergence explicitly,
not to silently follow whichever one is more convenient.**
`Product Roadmap.md`'s own Phase 4 "working premise" called for closing
Platform and Infrastructure gaps before building Engineering Modules;
`WP 7.0B`'s own Architecture Review chose to build the Engineering
Foundation frameworks instead, and that choice is now certified as
sound. This Work Package found the same kind of divergence a second
time, in `VISION.md`'s own "readiness" objective (authentication, trust
boundary closure, governance tooling resolved before the first
Engineering Module) — not yet met by this Work Package's own
recommendation. Both tensions are disclosed explicitly in
`WP7.2A Strategic Roadmap Review.md` and `WP7.2A Recommended
Programme.md`, with the reasoning for resolving each stated in full,
rather than assuming either document's own literal words still govern
unmodified.

**A dependency graph, once built, keeps answering questions it was not
originally built to answer.** `WP7.0C Cross-Framework Dependency
Report.md`'s own structural decision — `Verification` depends only on
`EngineeringData`'s generic document concept, never a concrete
Requirements type, specifically to avoid a future circular dependency —
was made before a Requirements Engine had any real design. This Work
Package's own Engineering Assessment re-confirmed that decision remains
sound, unmodified, five Work Packages later — proof the original
dependency-graph exercise anticipated correctly, not merely
coincidentally.

## Implementation Lessons

**"Cannot be sequenced from existing evidence" is a durable finding, not
a one-time observation that needs re-litigating.** `WP7.0B Engineering
Discipline Assessment.md` reached this conclusion for five Engineering
Discipline categories one Work Package (and five implementation Work
Packages) before this one. This Work Package re-checked it directly —
not merely cited it — and found it unchanged: no document produced
since named a Mechanical, Structural, Electrical, Building
Services/HVAC, or Manufacturing capability. The lesson generalises from
`WP 6.8`/`WP 7.1F`'s own governance-register-verification discipline to
roadmap findings specifically: re-verify a standing conclusion directly
before building on it, even when it seems unlikely to have changed.

**Scoring formalises a decision; it does not substitute for the
evidence the decision rests on.** The eleven-criterion Programme
Comparison Matrix produced a clean, defensible ranking (46 vs. 36 vs. 19
vs. 14 four times over) only because every individual score traced to a
specific, cited fact — not because scoring itself is persuasive. The
matrix's own Sensitivity Check section exists to demonstrate the
ranking survives a deliberately generous re-scoring of the runner-up,
proof the underlying evidence carries the recommendation, not the
arithmetic.

**Recommending a strong candidate "second" is only honest if its own
evidence and scope are preserved for whenever its turn comes.** Programme
F (Platform Hardening) was not rejected — `WP7.2A Candidate Work Package
Catalogue.md` explicitly carries `WP7.0B Candidate Work Package
Catalogue.md`'s own Candidates A–C forward unmodified, and
`WP7.2A Recommended Programme.md` names each of its own unfired triggers
as actively monitored, not deferred indefinitely.

## Repository Maturity

TempestOS now stands at two certified programmes (Platform Core,
Engineering Core), 57 ADRs, 36 Future Capability Register entries, 24
tracked Technical Debt items, 17 disclosed trade-offs, and — as of this
Work Package — a scored, evidence-backed answer to "what comes next,"
the first time this project has produced one rather than a list of
uncommitted candidates. This is the third consecutive planning-only
Work Package in this project's history to produce zero production code
while still advancing the project materially (`WP 7.0A`, `WP 7.0B`, now
`WP 7.2A`) — a pattern this project's own governance has never treated
as wasted effort, provided each produces a genuinely decision-useful
artefact, which this Work Package's own scored comparison and named
recommendation are intended to be.

## Recommendations for the Next Work Package

- **Approve Programme A (Requirements & Verification Platform) as
  `v0.8.0`'s own scope**, subject to its own dedicated Architecture,
  Planning, and Contract Review phase (Candidate K, `WP7.2A Candidate
  Work Package Catalogue.md`) — this Work Package does not approve
  implementation itself.
- **Retain Programme F (Platform Hardening) as the explicitly-sequenced
  next programme after Programme A**, not abandoned — recommended for
  `v0.9.0`, with each of its own triggers actively monitored.
- **Schedule a real engineering-domain stakeholder engagement, as its
  own separate initiative**, to identify a first capability within the
  five currently-empty Engineering Discipline categories — this remains
  outside what any documentation-only Work Package can resolve, per
  `WP7.0B Engineering Discipline Assessment.md`'s own unchanged finding.

## Key Takeaways

1. A repository that already discloses its own gaps honestly (empty
   discipline categories, unfired security triggers, an unbuilt
   governance tool) makes a strategic roadmap review's own job
   materially easier — most of the answer was already written down,
   scattered across documents that were never asked this exact question
   directly.
2. Naming a real tension between a stated vision and an actual
   recommendation, with reasoning, is stronger governance than silently
   picking whichever reading is convenient — a future reader can see
   exactly where to start a disagreement.
3. Sequencing a strong candidate second, with its own evidence and scope
   explicitly preserved, is a genuine commitment to build it later — not
   a polite way of declining it now.

## Related Documents

`WP7.2A Executive Summary.md`; `WP7.2A Strategic Roadmap Review.md`;
`WP7.2A Programme Comparison Matrix.md`; `WP7.2A Capability Dependency
Report.md`; `WP7.2A Recommended Programme.md`; `WP7.2A Candidate Work
Package Catalogue.md`; `WP7.2A Security Assessment.md`; `WP7.2A
Commercial Assessment.md`; `WP7.2A Engineering Assessment.md`;
`WP7.2A Lessons Learned.md`; `WP7.0A`/`WP7.0B`'s own retrospectives (the
whole-review-format precedent this document follows).
