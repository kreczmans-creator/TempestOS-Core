# WP 7.3A — Requirements Engine — Future Capability Recommendations

## Purpose

Recommend Future Capability Register updates arising from this Work
Package's own implementation experience — new candidates identified, and
the disposition of `FCR-0027` (the Requirements Engine itself) now that
implementation is complete.

## FCR-0027 — Requirements Engine

**Status change: Contracts Defined → Implemented.** `Tempest.Core.Requirements`
is now a working, tested, DI-registered Platform Service satisfying the
contracts `WP7.2C` approved. See `docs/governance/Future Capability
Register.md` for the updated entry.

## New Candidate: String-Based Allocation Targets

**What.** `WP7.2B Requirements Domain Model.md`'s own architectural
vision described a Requirement Allocation target as either a real
document reference or an open, unvalidated string identifier (for
allocating to a future design element that does not yet exist as a
document). `WP7.2C`'s own approved `LinkAsync` contract carried forward
only the document-reference half; the open-string half was never given
its own contract method, and this Work Package implements the approved
contract exactly, with no string-based overload.

**Why this is a Future Capability, not Technical Debt.** No regression
occurred — the contract review stage itself never committed to building
the open-string half. This is a capability gap between one phase's
architectural aspiration and the next phase's own final, narrower
commitment, not a defect introduced during implementation.

**Recommended scope for a future Work Package.** A dedicated
`AllocateToPendingAsync(Guid requirementId, string pendingTargetDescription, ...)`
or equivalent, mirroring how `Tempest.Core.EngineeringData.DocumentReference`
itself already tolerates an open `RelationshipKind` string — the same
pattern, applied to allocation targets instead of relationship kinds.

**Revisit trigger.** A real, demonstrated need to allocate a requirement
to a design element that does not yet exist as a created
`IEngineeringDocument` (e.g., early-phase systems engineering, where
requirements are allocated to a still-conceptual subsystem before any
concrete design document for it exists).

## New Candidate: Requirement Baselining

**What.** A capability to freeze a named, dated set of requirement
revisions as a formal baseline, for later comparison against a current
working set — a standard systems engineering practice this Work
Package's own controlling instruction did not name and correctly did not
implement (baselining is adjacent to, but distinct from, the revision
history `Tempest.Core.Requirements` already provides per-requirement).

**Revisit trigger.** A real, demonstrated need to compare "what the
requirement set looked like at milestone X" against current state —
plausible for the first engineering discipline module that consumes the
Requirements Engine in earnest.

## New Candidate: Change Impact Analysis

**What.** Given a proposed change to one requirement, traverse its own
recorded relationships (`DependsOn`, `DerivesFrom`, `AllocatedTo`,
`Satisfies`) to surface what else may be affected — a read-only query
capability layered entirely on top of `GetRelationshipsAsync`'s own
existing primitive, requiring no new storage.

**Revisit trigger.** A real, demonstrated need, once a non-trivial
requirement set with real relationship depth exists to analyse.

## Not Recommended: Optimistic Concurrency for `ReviseAsync`

Deliberately not raised as a Future Capability here — `TD-25` already
tracks this as Technical Debt with its own explicit revisit trigger (a
real multi-author editing incident); raising a duplicate Future
Capability entry for the same underlying gap would fragment tracking
rather than clarify it.

## Verdict

Two genuine new Future Capability candidates identified (string-based
allocation targets, requirement baselining), one adjacent candidate
noted (change impact analysis) for a later Work Package's own
consideration, and `FCR-0027` itself closed out as Implemented.

## Related Documents

`docs/governance/Future Capability Register.md`; `WP7.2B Requirements
Domain Model.md`; `WP7.3A Implementation Report.md`; `WP7.3A Digital
Thread Assessment.md`; `WP7.3A Technical Debt Assessment.md`.
