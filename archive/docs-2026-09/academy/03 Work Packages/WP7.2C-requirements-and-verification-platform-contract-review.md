# WP 7.2C — Requirements & Verification Platform Contract Review

## What This Document Is

Like `WP 7.0A`/`WP 7.0B`/`WP 7.0C`/`WP 7.2A`/`WP 7.2B` before it, this is
not a standard 13-section implementation retrospective — `WP 7.2C`
shipped no production code, no test, no compiled interface. It mirrors
the same whole-review shape (What Was Achieved, Architectural Lessons,
Implementation Lessons, Repository Maturity, Recommendations, Key
Takeaways), because this Work Package, like those before it, is a
contract-review milestone rather than a feature implementation.

## Introduction

`WP 7.2B` designed the complete architecture for the Requirements &
Verification Platform, Engineering Review APPROVED. `WP 7.2C`'s own
controlling instruction asked the natural next question: what would
each of the thirteen named domain concepts' own public contract
actually look like — mirroring `WP 7.0C`'s own identical role for the
original five Engineering Foundation frameworks, and `v0.6.0`'s own
Contract Review phase before that.

## What Was Achieved

Full proposed C# interface contracts for `IRequirementsService`,
`IRequirement`, `IRequirementCollection`, `IRequirementGroup`, the
relationship mechanism (Relationship/Allocation/Trace Link, sharing one
underlying `DocumentReference` shape), `IRequirementEvidence`,
`RequirementStatus`, and the remaining simpler concepts (Category,
Identifier, Revision), each answering the same seventeen review
questions this Work Package's own controlling instruction named.
Twelve completion deliverables under `docs/releases/v0.7.0/`, prefixed
`WP7.2C`: the Contracts document itself, a Requirement Lifecycle Model
(a full seven-state transition table, confirming `RequirementStatus`
is never automatically derived from a `VerificationRecord`'s own
`Outcome`), a Relationship Model (confirming six of seven named
relationship kinds belong in the initial implementation, the seventh —
"Verified By" — already existing, unmodified, inside
`Tempest.Core.Verification`), a Traceability Contract (confirming all
five traceability dimensions reuse existing Engineering Core capability
with zero new mechanism, while disclosing one genuine, structural
limitation in reverse allocation traceability), a Verification
Integration Contract (re-confirming `ADR-0057`'s own circular-dependency
avoidance holds unmodified at the contract level), a Platform
Integration Matrix, a Security Review (finding zero new issues beyond
`WP 7.2B`'s own architecture-level review, but one new open question:
`ADR-0061`), a Testing Strategy, an Academy Plan, a Governance
Confirmation, a Required ADR Catalogue (three questions carried forward
unchanged from `WP 7.2B`, one new question reserved), and this
retrospective.

## Architectural Lessons

**Writing concrete signatures surfaces questions an architecture review
cannot, by its own nature, surface.** `WP7.2B Security Architecture.md`
classified authorisation as "Implemented (inherited)" — a true, but
coarse-grained, finding. Only once this Work Package wrote
`IRequirementsService`'s own actual proposed method signatures did the
specific question — should any individual method gate permissions
internally, the way `IVerificationService.GetVerificationHistoryAsync`
does, or remain entirely calling-layer-enforced, the way
`IReportingService` does — become concrete enough to name and reserve
(`ADR-0061`). The lesson generalises across this project's own
three-phase pattern (Roadmap → Architecture → Contract Review): each
phase answers a genuinely different question, and a later phase finding
something an earlier phase could not is the pattern working as intended,
not a gap in the earlier phase's own review.

**Re-confirming a circular-dependency-avoidance decision at the contract
level, not merely citing it, is worth the repetition.** `WP7.2C
Verification Integration Contract.md` did not simply state "`ADR-0057`
already handled this" — it checked, directly against
`Tempest.Core.Verification`'s own real, shipped `RecordAsync` signature,
that nothing this Work Package proposes would require adding a
Requirements-specific type to that framework. The check cost little and
confirmed, rather than assumed, that a genuinely important prior
decision remains sound one contract-review phase later.

## Implementation Lessons

**Disclosing a real, structural limitation is stronger governance than
presenting an elegant design as uniformly complete.** `WP7.2C
Traceability Contract.md` names, explicitly, that reverse allocation
traceability does not work when an allocation target is an open string
rather than a real document — a direct consequence of the
discipline-neutrality design this same Work Package otherwise endorses
throughout. Naming the one place a design is incomplete is what makes
the remainder of the same document trustworthy, mirroring
`WP6.8`/`WP7.1F`'s own identical certification-report discipline,
applied here one stage earlier, to a contract rather than a shipped
implementation.

**Reserving a relationship-kind constant is nearly free; reserving a
domain concept is not — and the two should not be evaluated by the same
standard.** All six relationship kinds this Work Package reviewed were
included in the initial implementation, a different outcome from the
Engineering Foundation's own repeated "leave it open" pattern for
material properties or calculation input shapes. `WP7.2C Relationship
Model.md` states directly why: a `string` constant costs nothing and is
directly load-bearing for another required contract, while a property
taxonomy would require inventing unvalidated domain content. The lesson:
"prefer openness over premature invention" is not a blanket rule — it
applies specifically where the alternative is inventing domain content,
not wherever a decision merely looks similar in shape to one that
warranted openness before.

## Repository Maturity

TempestOS now has a complete, evidence-grounded contract proposal for
its own first Engineering Discipline capability — every one of thirteen
domain concepts with a full public interface, four reserved ADRs
(`ADR-0058`–`ADR-0061`), and twelve completion deliverables, produced
with zero production code. This is the sixth consecutive
architecture-or-contract-only Work Package in this project's history to
advance the project materially without shipping code (`WP 7.0A`,
`WP 7.0B`, `WP 7.0C`, `WP 7.2A`, `WP 7.2B`, now `WP 7.2C`) — the deepest,
most contract-detailed of the six, reflecting this Work Package's own
seventeen-question-per-concept review depth, a genuine increase in
rigor over `WP 7.0C`'s own twelve-question format for the original five
frameworks.

## Recommendations for the Next Work Package

- **Resolve all four reserved decisions (`ADR-0058`–`ADR-0061`) before
  writing any production code** — mirroring every Engineering Foundation
  implementation Work Package's own identical discipline of confirming
  its own reserved ADRs before implementation begins.
- **Decide `ADR-0061` using the test this review implicitly applied**:
  gate internally when data exposed is itself evidentiary and
  permission-sensitive on its own terms; leave to the calling layer when
  it is ordinary operational state — stated explicitly here so the
  owning Work Package does not need to re-derive it.
- **Re-examine the reverse-allocation-traceability limitation once a
  real discipline module exists to allocate against** — only real usage
  will show whether the disclosed limitation is an ongoing cost or a
  theoretical one.

## Key Takeaways

1. Each phase of a Roadmap → Architecture → Contract Review sequence
   answers a genuinely different question — a later phase surfacing
   something an earlier phase could not is the pattern working as
   designed, not a gap in the earlier review.
2. "Prefer openness over premature invention" applies specifically where
   the alternative is inventing unvalidated domain content — not as a
   blanket rule for every decision that superficially resembles one
   where openness was previously correct.
3. A contract review that confirms "nothing new beyond the architecture
   review" is itself informative when the reasoning is stated —
   evidence the preceding review was thorough, not merely an absence of
   findings.

## Related Documents

`WP7.2C Requirements Platform Contracts.md`; `WP7.2C Requirement
Lifecycle Model.md`; `WP7.2C Relationship Model.md`; `WP7.2C
Traceability Contract.md`; `WP7.2C Verification Integration Contract.md`;
`WP7.2C Platform Integration Matrix.md`; `WP7.2C Security Review.md`;
`WP7.2C Testing Strategy.md`; `WP7.2C Academy Plan.md`; `WP7.2C
Governance Confirmation.md`; `WP7.2C Required ADR Catalogue.md`; `WP7.2C
Lessons Learned.md`; `WP7.0C Engineering Foundation Contracts.md` (the
format precedent this document follows); `WP7.2B Requirements Platform
Architecture.md`.
