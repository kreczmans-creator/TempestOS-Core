# ADR-0157: The Review Queue Is Exported, Read-Only, as reviews.json

## Status

Accepted — Product Owner approval (2026-10-01) to bring the lost
August 2026 Companion app's read-only review queue into `v0.23.0`, so it
appears on Tempest-Dashboard and therefore on the phone.

**Decision 5 superseded by `ADR-0162` (2026-10-02)**: a day on, the
Product Owner reversed "not wanted" for the write path, specifically to
build the architecture once rather than twice as staff are hired. The
read-only export this ADR defines (Decisions 1–4) is unchanged — only
"no approve/reject path" no longer holds.

Builds on `ADR-0154` (the Dashboard Export's per-file pattern) and
`ADR-0155` (project membership).

## Context

The Companion app (recovered ref
`refs/recovered/claude/tempestos-companion-mobile-ubznt3`) listed
"pending reviews": live Documents, Drawings and CAD Models in
`LifecycleState.InReview` (`CompanionQueryService.BuildPendingReviewsAsync`).
It had a write action too (set document status); that is not wanted.

Today's Core differs in two ways that matter. Repository lists are index
rows, materialised on demand (`TD-88`, `WP 21.5B`). And more disciplines
now have a review step: Calculations and Verification Activities have
"Request Review" commands to `InReview`; Evidence's `Checked` is the
canonical `InReview` (`ADR-0148`); Requirements' `Reviewed` is what the
Requirements Cockpit counts as "Review".

## Decision

**1. A fifth export file, `reviews.json`, not a new `engineering-status.json`
section.** Every existing file is one dashboard source with one
envelope (`contracts.json` → `contracts`, `quotes.json` → `quotes`), and
`engineering-status.json` is a set of counts and the Cockpit's own
surfaces at schema v2. The queue is a list of records with its own
consumers (the Engineering view's list, Home's attention count), so it
gets its own file, schema and ingest source (`reviews`), and
`engineering-status.json` is unchanged. `ReviewQueueExportAdapter`
writes it; `DashboardExportHostedService` writes it on the same tick
and the same atomic `.tmp` rename as the other four.

**2. "Awaiting review" is each family's own `InReview` equivalent.**
Documents/Drawings/CAD Models, Calculations and Verification Activities:
`InReview`, dated by the last transition into it, submitted by that
transition's actor. Evidence: `Checked` with a non-rejected check,
dated and named by the check. Requirements: `Reviewed`; a requirement
records no status-change time or actor, so it is dated by creation
(`sinceBasis: "created"`) and `submittedBy` is `null`.

**3. The index filters; only queued items are materialised.** For the
lifecycle Kinds, liveness and `InReview` are read from the index row.
Evidence keeps its own status beside the canonical one, so live
Evidence is materialised as `engineering-status.json` already does.

**4. Project is `ProjectMembership`'s.** The structural parent walk for
engineering objects; for a requirement, the project its links reach,
when they all reach the same one. Otherwise `null` (standalone work is
real, `TD-89`).

**5. Read-only.** No approve, reject or status-change path from the
dashboard or the phone.

### Schema (v1)

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-10-01T09:00:00Z",
  "total": 1,
  "byDiscipline": { "documents": 1, "calculations": 0, "verification": 0, "evidence": 0, "requirements": 0 },
  "items": [
    {
      "id": "…guid…",
      "kind": "Drawing",
      "discipline": "documents",
      "identifier": "DWG-100",
      "title": "GA Drawing",
      "project": { "id": "…guid…", "identifier": "PRJ-7", "name": "Harbour Bridge" },
      "status": "inReview",
      "since": "2026-09-12T14:03:00Z",
      "sinceBasis": "submitted",
      "ageDays": 18,
      "submittedBy": "principal-id"
    }
  ]
}
```

`kind` is Core's Kind string (`Document`, `Drawing`, `CadModel`,
`Calculation`, `VerificationActivity`, `Evidence`, `Requirement`).
`status` is the family's own word (`inReview`, `checked`, `reviewed`).
`sinceBasis` is `submitted`, `checked` or `created`. `ageDays` is whole
days from `since` to `generatedAt`, never negative. Items are sorted
oldest first. A requirement's `title` is its statement, truncated to
160 characters. `submittedBy` is a principal id, or the checker's name
for Evidence.

## Consequences

**Positive.** The queue the Companion had is back, read-only, on the
dashboard and phone, across every discipline with a review step, in
each discipline's own words.

**Negative.** Requirements are dated by creation, so a requirement's age
overstates how long it has waited. `submittedBy` is a principal id, not
a display name. Both are disclosed in the payload (`sinceBasis`, `null`)
rather than guessed.

## Alternatives Considered

**Extending `engineering-status.json`.** Rejected: see Decision 1.

**Documents only, as the Companion had.** Rejected: Calculations and
Verification Activities now have the same review step, and leaving them
out would make the queue look shorter than it is.

## Related Documents

`ADR-0148`; `ADR-0154`; `ADR-0155`; `TD-88`;
`src/Tempest.Workspace/Integration/DashboardExport/ReviewQueueExportAdapter.cs`;
Tempest-Dashboard `docs/DATA-CONTRACTS.md` §Reviews.
