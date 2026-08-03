# WP 7.3A — Requirements Engine — Technical Debt Assessment

## Purpose

Formally scope the one new Technical Debt item this Work Package's own
Security Review disclosed (`TD-25`), and confirm the disposition of
every existing Technical Debt item this Work Package's own
implementation touches or extends.

## New Item

### `TD-25` — No Concurrency-Conflict Detection on Requirement Revision/Status Changes

| Field | Value |
|---|---|
| **Raised by** | `WP 7.3A` (Requirements Engine), 2026-07-30 |
| **Area** | `Tempest.Core.Requirements.RequirementsService` |
| **Description** | `ReviseAsync` and `SetStatusAsync` carry no expected-prior-revision (compare-and-swap) parameter. Two concurrent editors of the same requirement can each succeed; the second call's content silently becomes current, with no conflict signalled to either caller. The underlying store's own per-document lock still guarantees no two revisions ever claim the same revision number — internal consistency is preserved — but editorial intent is not protected. |
| **Why accepted, not fixed now** | Implementing a compare-and-swap parameter now would deviate from the approved `WP7.2C Requirements Platform Contracts.md` `ReviseAsync`/`SetStatusAsync` signatures, which carry no such parameter — no genuine implementation defect justifies that deviation. See `ADR-0060`. |
| **Severity** | Moderate — the Requirements Engine is the first Engineering Core consumer whose own target users (a systems engineering team) are likely to edit the same artefact concurrently as ordinary practice, not an edge case, making this more consequential than the equivalent absence elsewhere in the platform. |
| **Revisit trigger** | A real, demonstrated multi-author collaborative-editing incident against a shipped requirement set. |
| **Related** | `ADR-0060`; `WP7.3A Security Review Report.md` ("Concurrent modification"); `TD-18` (traceability duplicate/contradiction detection — a related but distinct gap in `LinkAsync`, not `ReviseAsync`). |

## Existing Items Reviewed for Extension or Change

| Item | Disposition Under This Work Package |
|---|---|
| **`TD-16`** (no cryptographic signing/tamper-evidence for stored documents) | Extended, not worsened. Requirements, collections, and groups are ordinary `IEngineeringDocument` instances and inherit this existing, already-accepted, platform-wide posture. No Requirements-specific tamper-resistance gap exists beyond it. |
| **`TD-18`** (no duplicate/contradiction detection on `LinkAsync` relationships) | Extended, not worsened. Every Requirement relationship (`GroupedUnder`, `CollectedIn`, `DependsOn`, `DerivesFrom`, `AllocatedTo`, `References`, `Satisfies`) is recorded via the same `LinkAsync` this item already covers; this Work Package introduces no new relationship-integrity gap beyond the one already disclosed. |
| **`TD-22`/`TD-24`** (no bound on recorded volume for Calculation/Verification history) | Recognised as a recurring pattern, not separately re-registered. `IRequirementsService` imposes no bound on the number of requirements, relationships, or collection members a caller may create, and `ListAsync`/`GetRelationshipsAsync` scale linearly with total count — the identical, already-tracked "no measured problem yet" disclosure discipline, applied here rather than duplicated as a new item. |

## Items Considered and Not Raised

**A dedicated "open-string allocation target" gap.** `WP7.2B Requirements
Domain Model.md`'s own broader architectural vision for Allocation
(supporting an open string target, not only a document reference) was
never carried into `WP7.2C`'s own approved `LinkAsync` contract. This is
tracked as a **Future Capability**, not Technical Debt — it was never a
regression from an approved, working state, but a capability the
contract review stage itself never committed to building. See `WP7.3A
Future Capability Recommendations.md`.

## Verdict

One new Technical Debt item raised (`TD-25`), fully scoped, with an
explicit revisit trigger. Zero existing items worsened. Zero items
require re-classification. Total platform Technical Debt Register count:
24 → 25.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `ADR-0060`;
`WP7.3A Security Review Report.md`; `WP7.3A Implementation Report.md`;
`WP7.3A Future Capability Recommendations.md`.
