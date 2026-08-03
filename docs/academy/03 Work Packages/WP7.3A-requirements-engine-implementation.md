# WP 7.3A — Requirements Engine — Implementation

## 1. Introduction

`WP 7.3A` is the first implementation Work Package of the Systems
Engineering Foundation phase, following `WP 7.2A` (Strategic Roadmap
Selection), `WP 7.2B` (Requirements & Verification Platform
Architecture), and `WP 7.2C` (Requirements & Verification Platform
Contract Review). It implements `Tempest.Core.Requirements` exactly as
`WP7.2C Requirements Platform Contracts.md` approved — the canonical
representation of engineering requirements throughout TempestOS,
discipline-neutral, consuming the Engineering Core. It is the third
Work Package overall to include a dedicated Security Review.

## 2. Purpose

To give every future engineering discipline module a single, canonical
way to represent, revise, relate, and track the lifecycle of an
engineering requirement — distinct from Verification's own outcome
judgement, and built entirely on the Engineering Core's own existing
mechanisms rather than a new storage or traversal layer.

## 3. Background

`WP 7.2A` selected the Requirements & Verification Platform as the next
strategic programme. `WP 7.2B` designed its complete architecture,
including a Digital Thread Architecture arguing no new mechanism was
required. `WP 7.2C` defined the complete public contracts — thirteen
concepts, each with seventeen defined attributes — and reserved four
ADRs (`0058`-`0061`) for genuine implementation decisions. This Work
Package resolves all four, implements every approved contract exactly,
and is the first Work Package to prove `WP7.2B`'s own central digital
thread claim in running code.

## 4. The Problem

A Requirements Engine needs a stable identity model, an enforced
lifecycle, a relationship vocabulary usable by every future discipline,
and a way to compose "what proves this requirement" from existing
Verification and Engineering Data mechanisms — without duplicating any
of them, and without accidentally coupling requirement status to
verification outcome, two related-sounding but genuinely independent
concepts.

## 5. The Design

Every requirement, collection, and group is an `IEngineeringDocument` of
its own `Kind`. Every relationship (`GroupedUnder`, `CollectedIn`,
`DependsOn`, `DerivesFrom`, `AllocatedTo`, `References`, `Satisfies`) is
a `DocumentReference` via `LinkAsync`/`GetReferencesAsync` — collections
and groups store no membership/parent field of their own at all, both
derived entirely from filtered reference reads.
`RequirementStatusTransitions` encodes the approved seven-state
lifecycle's own exact permitted-transition table, checked by
`SetStatusAsync`, with zero code path connecting it to
`VerificationOutcome`. `GetEvidenceAsync` composes
`IVerificationService.GetVerificationHistoryAsync` with
`GetReferencesAsync` into one read — the digital thread, demonstrated.
See `WP7.3A Implementation Report.md` for the complete file-by-file
account.

## 6. Alternatives Considered

**Deriving `RequirementStatus.Verified` automatically from a passing
`VerificationRecord`** — considered and rejected; would couple two
concepts this project has deliberately kept independent everywhere else
(`16-requirements-engine.md` §6).

**Storing collection membership and group parentage as DTO fields**
rather than deriving them via `GetReferencesAsync` — considered and
rejected; would duplicate a mechanism the Data Model already provides.

**Adding a compare-and-swap parameter to `ReviseAsync` speculatively** —
considered and rejected; would deviate from the approved contract
without a genuine implementation defect to justify it (`ADR-0060`).

## 7. Why This Solution Was Chosen

It implements the approved contracts with zero deviation, requires zero
new storage or traversal mechanism beyond what the Engineering Core
already provides, and is the fourth consecutive Engineering Core
framework to reach that same conclusion independently — the strongest
evidence yet that the reuse-of-existing-mechanism pattern generalises
across genuinely different domains (materials, calculations,
verification, and now requirements).

## 8. Architectural Principles

Applies `FOUNDATION.md`'s existing principles without modification: one
component, one reason to change; fail fast (a duplicate identifier, an
invalid status transition, or a link to a non-existent target fails
immediately, never silently). Extends `docs/engineering/Engineering
Principles.md` with four further principles (29-32) under a new
"Requirements Engine Extension" section. Adds a new Academy concept
guide, `16-requirements-engine.md`, giving the three-layer pattern and
the relationship-kind vocabulary a canonical reference point — the
required output `WP7.2C Academy Plan.md` itself named.

## 9. Files Added

20 new production files under `src/Tempest.Core/Requirements/`; 8 new
sample files under `src/Samples/Tempest.Samples/`; 1 file modified
(`TempestHost.cs`); 5 new test files under `tests/Tempest.Core.Tests/
Requirements/`, `Runtime/`, and `Samples/`; 1 test file modified
(`ClockModuleDiscoveryTests.cs`). Full list: `WP7.3A Implementation
Report.md`.

## 10. Trade-offs

`ReviseAsync`/`SetStatusAsync` carry no compare-and-swap or
expected-prior-revision check (`TD-25`) — disclosed in `WP7.3A
Technical Debt Assessment.md` and `WP7.3A Security Review Report.md`,
not Release Blocking. Allocation targets are Guid-only; `WP7.2B`'s own
broader open-string vision was never carried into the approved
contract, disclosed as a Future Capability, not debt (`WP7.3A Future
Capability Recommendations.md`).

## 11. Common Mistakes

A future consumer should **not** call `SetStatusAsync(id,
RequirementStatus.Verified)` automatically whenever a matching
`VerificationRecord` with a `Pass` outcome exists — the two mechanisms
are deliberately independent (Principle 29). A future consumer should
**not** assume `RequirementCollection`/`RequirementGroup` carry their
own membership/parent data directly — always derive it via
`GetRelationshipsAsync` or the dedicated lookup methods.

## 12. Future Evolution

String-based allocation targets, requirement baselining, and change
impact analysis are all named as Future Capability candidates in
`WP7.3A Future Capability Recommendations.md`. The first
discipline-specific engineering module (Mechanical, HVAC, Structural,
or Electrical) is the Requirements Engine's own most likely first real
consumer beyond its own sample module.

## 13. Key Takeaways

1. A concept that sounds like it might need its own storage mechanism
   often does not — Requirements is the fourth consecutive Engineering
   Core framework to need zero new storage beyond the Data Model's own
   existing document and reference primitives.
2. Keeping two related-sounding concepts (lifecycle status, verification
   outcome) genuinely independent in code, not only in documentation, is
   what makes the separation trustworthy.
3. Two full Work Packages of upstream architecture and contract review
   (`WP7.2B`, `WP7.2C`) can produce a design that survives
   implementation with zero rework — the strongest possible validation
   that this programme's own architecture-first discipline is working.

## Architectural Debt Assessment

`TD-25` (no concurrency-conflict detection on `ReviseAsync`/
`SetStatusAsync`) — newly disclosed, not Release Blocking. Full detail:
`WP7.3A Technical Debt Assessment.md`.

## Observations

This is the first implementation Work Package of the Systems
Engineering Foundation phase — validated by the same discipline as
every Engineering Foundation predecessor (clean Debug/Release builds,
1406/1406 tests, both configurations, clean rebuild, up from a 1275
baseline). Every approved contract was implemented exactly as written;
the sole disclosed gap (open-string allocation targets) originated at
the contract-review stage itself, not this Work Package's own
implementation.

## Related Documents

`docs/releases/v0.7.0/WP7.3A Implementation Report.md` and its seven
companion deliverables; `ADR-0058`; `ADR-0059`; `ADR-0060`; `ADR-0061`;
`docs/engineering/Engineering Principles.md`; `docs/academy/02 Runtime
Architecture/16-requirements-engine.md`; `docs/releases/v0.7.0/WP7.2C
Requirements Platform Contracts.md`.
