# WP 9.0B — Product Configuration & BOM Management — Security Review Report

## Purpose

A proportionate security review of the new `IHasBomLine` facet, the five
new validation rules, and the three new Workspace commands, reviewed
across the same dimensions this project's own established Security
Review convention uses. Second consecutive dedicated Security Review
(after `WP 9.0A`, which itself closed `WP8.9.0`'s own disclosed
"zero dedicated Security Reviews" gap).

## Review

| Dimension | Finding | Classification |
|---|---|---|
| **Authorisation boundaries** | `SetBomLineAsync`/`CompareBaselinesCommand`/`ValidateConfigurationCommand` perform no internal permission gating of their own — mirrors every `WP 9.0A` command's own identical calling-layer-enforced posture. | Not Applicable — reviewed, design consistent with established precedent |
| **Input validation — quantity** | `SetBomLineAsync` rejects non-positive `Quantity` immediately (`ArgumentOutOfRangeException`), before any state is mutated — validate-then-commit, never commit-then-validate. `InvalidQuantityValidationRule` provides a second, `ValidateAsync`-reachable confirmation. | Not Applicable — reviewed, secure by construction |
| **Input validation — Unit of Measure/Find/Item Number/Reference Designator** | All four remain unvalidated free-text strings (`ADR-0083`'s own disclosed trade-off) — no injection surface exists anywhere these strings are consumed (never interpolated into a query, a shell command, or rendered as markup); worst case is a confusing but harmless display value. | Accepted Risk — disclosed in `ADR-0083`, not a new gap |
| **Duplicate/consistency validation** | `DuplicateItemNumberValidationRule`/`DuplicateFindNumberValidationRule` correctly scope to live siblings only (excluding soft-deleted objects and objects under a different parent) — proven by dedicated tests. Prevents a class of silently-ambiguous BOM data from passing validation undetected. | Not Applicable — reviewed, guard proven effective |
| **`ReviseAsync` structural-state-copy fix** | Reviewed directly as a security-adjacent correctness fix: before the fix, revising an object's content could silently un-delete it (a soft-deleted object's `IsDeleted` would revert to `false` on the revised instance) — a genuine, if narrow, data-integrity concern, not merely a display inconvenience. Fixed; four regression tests confirm `IsDeleted`/`ParentId`/`DisplayName` all now survive a revision correctly. | Not Applicable — reviewed, fix verified by test |
| **`TEMPEST-VAL` code collision** | Reviewed directly: before the fix, `CircularParentAssignmentException`/`EngineeringObjectHasChildrenException` carried the same `Code` value as `IReferenceIntegrityChecker.CheckAsync`'s own relationship-existence failures — a caller filtering or logging by validation code could have silently conflated two unrelated failure classes. Fixed; codes are now disjoint and named. | Not Applicable — reviewed, fix verified |
| **Configuration/Baseline/Release consistency** | `ValidateConfigurationCommand` wraps the already-reviewed `CheckBaselineMembersAsync` (`WP8.2C`) — no new read path, no new trust boundary. | Not Applicable — inherited, already-reviewed guarantee |
| **Resource exhaustion** | `DuplicateItemNumberValidationRule`/`DuplicateFindNumberValidationRule`/`CircularHierarchyValidationRule`/`MissingParentValidationRule` all scan `ListAllAsync()` — the same O(n)-in-total-object-count characteristic `TD-22`/`TD-24`/`WP 9.0A`'s own equivalent finding already carry, disclosed, not newly introduced. | Technical Debt — mirrors the existing, already-tracked pattern; not separately re-registered |
| **Serialization safety** | No new serialization surface — `IHasBomLine`'s own fields are plain in-memory C# values, never (de)serialized from untrusted input. | Not Applicable |
| **Dependency risk** | No new third-party dependency. Confirmed: `Tempest.Core.UnitsAndQuantities` was deliberately *not* taken as a dependency for `UnitOfMeasure` (`ADR-0083`), so no new coupling was introduced even internally. | Not Applicable |
| **Secure defaults** | `Quantity` defaults to `1` (a safe, common BOM default); `UnitOfMeasure`/`FindNumber`/`ItemNumber`/`ReferenceDesignator` all default to `null` (honestly unset, never a fabricated placeholder value). | Not Applicable — reviewed, secure by construction |
| **Backwards compatibility** | Every already-shipped Kind not composing `IHasBomLine` is unaffected; `MechanicalObjectFactoryRegistry`'s own three new Kinds are additive, existing callers unaffected (confirmed by the two call-site fixes needed for the new optional parameter, both mechanical, neither behavioural). | Not Applicable |

## New Debt Disclosed by This Review

No new Technical Debt item is registered by this review specifically —
the one finding above classified as debt (`ListAllAsync` scan cost)
mirrors an already-tracked, existing pattern. `TD-27` (the flaky-test
finding) is disclosed separately in `WP9.0B Technical Debt
Assessment.md` — a test-infrastructure finding, not a security one.

## Verdict

**Zero Release Blocking findings.** Two genuine correctness defects
found during this Work Package's own implementation were reviewed here
specifically for their security dimension (data-integrity/failure-
classification impact) and confirmed fixed, with test evidence. No new
attack surface was introduced.

## Related Documents

`ADR-0083`; `WP9.0A Security Review Report.md`; `WP9.0B Technical Debt
Assessment.md`; `docs/governance/Quality/Technical Debt Register.md`.
