# WP 9.1A — Requirements Management Workspace — Security Review Report

## Purpose

A proportionate security review of the new `IRequirementsService`
methods, `IRequirementValidationService`, the Workspace layer's own 18
commands, and multi-selection — reviewed across the same dimensions this
project's own established Security Review convention uses. Third
consecutive dedicated Security Review (after `WP 9.0A`/`WP 9.0B`).

## Review

| Dimension | Finding | Classification |
|---|---|---|
| **Authorisation boundaries** | Every new Requirements command performs no internal permission gating of its own — mirrors every `WP 9.0A`/`WP 9.0B` Mechanical command's own identical, calling-layer-enforced posture (`ADR-0061`, unchanged). | Not Applicable — reviewed, design consistent with established precedent |
| **Permission-gated read reachable from a passive surface (found and fixed)** | `IRequirementsService.GetEvidenceAsync` is transitively gated on `VerificationService.ReadPermission`. `RequirementValidationService.ValidateAsync`, `RequirementsPropertyFacetProvider.GetFacetsAsync`, and `EngineeringCockpit`'s own Requirements KPI reads originally called it — a Property Inspector selection, a validation read, or a Cockpit KPI read could throw `PermissionDeniedException` for any principal lacking a narrower capability than "can view this at all," a genuine availability defect for a supposedly passive read surface. Fixed in all three call sites by reading `GetRelationshipsAsync` for the `verifiedBy` link directly — the identical fact, non-gated. | Not Applicable — reviewed, fix verified by test (`RequirementsWorkspaceIntegrationTests`, real unprivileged sample principal) |
| **Soft-delete integrity** | `DeleteAsync`/`DeleteGroupAsync`/`DeleteCollectionAsync` never erase a document, revision, or relationship — mirrors every other Domain mutation's own append-only ethos; `IsDeleted` is the only state that changes. | Not Applicable — reviewed, secure by construction |
| **`DeleteGroupAsync` has-children guard** | Correctly blocks deletion of a group with live grouped requirements or live sub-groups (the latter closed mid-implementation once `ListGroupsAsync` existed to make it possible) — prevents a class of silently-unreachable-but-not-deleted data. Proven by dedicated tests including the sub-group case. | Not Applicable — reviewed, guard proven effective |
| **`RequirementGroupDto` storage-model fix** | Reviewed directly as a correctness/integrity fix: the prior `.FirstOrDefault()`-over-relationships parent resolution would have become genuinely ambiguous the moment `MoveGroupAsync` recorded a second `groupedUnder` link for the same group — silently resolving to whichever link the (unordered) persistence store happened to return first. Fixed by storing `ParentGroupId` directly on the DTO; the relationship link remains recorded, for history, but is no longer the resolution mechanism. | Not Applicable — reviewed, fix verified by test (`MoveGroupAsync_FindGroupAsync_ReflectsTheMove`) |
| **Import/Export round-trip** | `RequirementCollectionExportAdapter` re-creates every imported requirement under a new, GUID-suffixed identifier, into a new collection — never overwrites or merges into existing data. A colliding re-created identifier (astronomically unlikely) is skipped, not fatal to the rest of the import. No deserialisation of untrusted executable content — plain JSON records only. | Not Applicable — reviewed, secure by construction |
| **Bulk commands (`BulkSetRequirementStatusCommand`/Owner/Priority)** | Each item is attempted independently; one item's own failure (not found, invalid transition) never blocks the remaining items, and the aggregate result reports exactly how many succeeded plus every individual failure message — no silent partial-success misreporting. | Not Applicable — reviewed, secure by construction |
| **`LinkRequirementCommand`** | A fully generic relationship-kind wrapper — accepts any non-blank string (`ADR-0073`'s own open-vocabulary design, unchanged). An unrecognised kind is flagged as an advisory-only warning by `IRequirementValidationService` (`TEMPEST-REQ-VAL-005`), never blocked — consistent with the platform-wide decision that relationship kinds are open by design. | Not Applicable — reviewed, design consistent with `ADR-0073` |
| **Multi-selection (`ADR-0085`)** | `WorkspaceContext`'s own backing selection-set storage is replaced wholesale on each mutation, never mutated in place — a caller holding an earlier `SelectedItems` reference cannot observe a later mutation through it, preventing a class of shared-mutable-state surprise. | Not Applicable — reviewed, secure by construction |
| **Resource exhaustion** | `ListCollectionsAsync`/`ListGroupsAsync`/`ListAsync`, and every Cockpit KPI/validation read built on them, are all O(n) in total Requirements-Kind-document count — the same already-tracked, disclosed characteristic `TD-22`/`TD-24`/`WP 9.0A`'s and `WP 9.0B`'s own equivalent findings carry. | Technical Debt — mirrors the existing, already-tracked pattern; not separately re-registered |
| **Serialization safety** | `RequirementDto`/`RequirementGroupDto`/`RequirementCollectionDto`/`RequirementCollectionExportAdapter`'s own payload records are plain, closed-shape C# records — no polymorphic or type-name-carrying deserialisation anywhere. | Not Applicable |
| **Dependency risk** | No new third-party dependency. | Not Applicable |
| **Backwards compatibility** | Every existing `IRequirementsService`/`ISelectionService`/`IWorkspaceContext` consumer is unaffected — every new member is additive; confirmed by the full, unmodified `WP 7.3A`/`WP8.0B`/`WP8.1x` test suites passing unchanged alongside the 70 new tests. | Not Applicable |

## New Debt Disclosed by This Review

No new Technical Debt item is registered by this review specifically —
the one finding above classified as debt (O(n) list-and-filter reads)
mirrors an already-tracked, existing pattern across three consecutive
Work Packages now.

## Verdict

**Zero Release Blocking findings.** One genuine availability defect
(the permission-gated read reachable from three passive surfaces) and
one genuine data-integrity defect (the `RequirementGroupDto` resolution
ambiguity) were found during this Work Package's own implementation,
reviewed here specifically for their security dimension, and confirmed
fixed with test evidence. No new attack surface was introduced.

## Related Documents

`ADR-0084`; `ADR-0085`; `WP9.0A Security Review Report.md`; `WP9.0B
Security Review Report.md`; `WP9.1A Technical Debt Assessment.md`;
`docs/governance/Quality/Technical Debt Register.md`.
