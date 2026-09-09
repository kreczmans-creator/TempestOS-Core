# WP 7.3A — Requirements Engine — Digital Thread Assessment

## Purpose

Assess how the shipped `Tempest.Core.Requirements` implementation
demonstrates — or fails to demonstrate — the Digital Thread this Work
Package's own controlling instruction required foundations for:
"explicit links to Engineering Documents, Verification Records,
Calculations, Evidence, Future Allocations."

## What Was Required

This Work Package's own controlling instruction was explicit: implement
the architectural *foundations* for a digital thread, not a dedicated
traversal or query mechanism, and explicitly not Compliance or Workflow.
`WP7.2B Digital Thread Architecture.md`'s own central finding was that
no new mechanism should be required — a digital thread should be
achievable purely by composing `IEngineeringDocumentStore.LinkAsync`/
`GetReferencesAsync` calls that already exist.

## What Was Demonstrated

**Zero new storage or traversal mechanism was introduced.** Every one of
the five named link targets is demonstrated using only the pre-existing
`DocumentReference`/`LinkAsync`/`GetReferencesAsync` primitives:

| Required Link | How It Is Demonstrated |
|---|---|
| **Engineering Documents** | A requirement's own `GroupedUnder` (to a `RequirementGroup`) and `CollectedIn` (to a `RequirementCollection`) relationships are both ordinary `DocumentReference` entries against other `IEngineeringDocument` instances. |
| **Verification Records** | `GetEvidenceAsync` composes `IVerificationService.GetVerificationHistoryAsync` directly — a requirement's own verification history is read through Verification's own existing mechanism, not duplicated. `RequirementsSampleModule` demonstrates a real `IVerificationService.RecordAsync` call linked back to the sample requirement. |
| **Calculations** | Reachable, not separately demonstrated: a `References` relationship targets any `IEngineeringDocument` by Guid, which a Calculation record already is (`Tempest.Core.Calculations`). No sample module scenario exercises this specific pairing, since neither this Work Package nor its sample module had a concrete calculation-to-requirement scenario to demonstrate; the mechanism is identical to the Verification pairing that is demonstrated. |
| **Evidence** | `GetEvidenceAsync`'s own `RequirementEvidence` aggregates both verification history and linked references into a single read — the requirement's own composed "what proves this" view. |
| **Future Allocations** | `AllocatedTo` relationships link a requirement to any `IEngineeringDocument` representing a future design element (`RequirementsSampleModule`'s own `SampleComponent`-kind document demonstrates this concretely). |

**The central architectural finding is confirmed, not merely repeated.**
`WP7.2B Digital Thread Architecture.md` argued, on paper, that a digital
thread requires no dedicated mechanism. This Work Package's own
`GetEvidenceAsync` is the first place in the entire codebase that
actually composes multiple existing read primitives (`GetVerificationHistoryAsync`
+ `GetReferencesAsync`) into a single digital-thread-shaped view, proving
the claim in running, tested code (`Requirements Engine Extension`
Principle 32, added to `Engineering Principles.md`).

## What Was Correctly Not Built

- **No dedicated traversal/query API** beyond `GetRelationshipsAsync`
  (a single requirement's own direct relationships) and `GetEvidenceAsync`
  (a single requirement's own composed evidence) — no multi-hop
  traversal, no thread visualisation, no impact-analysis query. This
  matches the controlling instruction's own explicit scope: "foundations,"
  not a complete thread capability.
- **No Compliance, no Workflow** — confirmed absent throughout, per
  `WP7.3A Engineering Review Report.md`'s own Scope Discipline Review.

## One Disclosed Limitation

The disclosed contract-stage narrowing of Allocation targets (`WP7.3A
Implementation Report.md`'s own finding: Guid-only, not also
open-string) means a digital thread link to a *future* engineering
element that does not yet exist as a document cannot currently be
represented — only a link to an *already-created* document. See `WP7.3A
Future Capability Recommendations.md`.

## Verdict

The digital thread foundation this Work Package's own controlling
instruction required is demonstrated for four of the five named link
targets directly (Engineering Documents, Verification Records, Evidence,
Future Allocations) and is mechanically identical, though not separately
exercised, for the fifth (Calculations). Zero new storage or traversal
mechanism was introduced anywhere — the central architectural claim from
`WP7.2B` is now proven in code, not merely documented.

## Related Documents

`WP7.2B Digital Thread Architecture.md`; `WP7.2C Traceability
Contract.md`; `WP7.2C Verification Integration Contract.md`; `WP7.3A
Implementation Report.md`; `WP7.3A Systems Engineering Impact
Assessment.md`; `WP7.3A Future Capability Recommendations.md`.
