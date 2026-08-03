# WP 7.3A — Requirements Engine — Security Review Report

## Purpose

A proportionate security review of the shipped `Tempest.Core.Requirements`
implementation, reviewing the fourteen dimensions this Work Package's
own controlling instruction names, each classified **Not Applicable**,
**Accepted Risk**, **Technical Debt**, or **Release Blocking** — the
third consecutive Engineering Foundation/Systems Engineering Work
Package to perform a dedicated Security Review (after `WP 7.1D`,
`WP 7.1E`), and the first to confirm findings already anticipated by two
prior architecture/contract-review stages (`WP7.2B Security
Architecture.md`, `WP7.2C Security Review.md`) against real, shipped,
tested code rather than a proposed design.

## Review

| Dimension | Finding | Classification |
|---|---|---|
| **Authorisation boundaries** | `IRequirementsService` performs no internal permission gating (`ADR-0061`) — confirmed directly: its constructor takes no `IPermissionEvaluator`. Every method is calling-layer-enforced, mirroring `IMaterialCatalog`/`ICalculationEngine`. `GetEvidenceAsync` remains permission-gated transitively through `IVerificationService.GetVerificationHistoryAsync`, proven by `GetEvidenceAsync_InheritsVerificationReadPermissionGate`. `RequirementsSampleModule`'s own `GetSampleRequirementEvidenceCommandHandler` demonstrates explicit calling-layer enforcement, denied by default with no roles configured. | Not Applicable — reviewed, design confirmed sound |
| **Requirement ownership** | `CreatedByPrincipalId` is captured once, at `CreateAsync` time, and carried forward unchanged across every later revision (`ReviseAsync_PreservesIdentifierCategoryAndCreationAttribution`) — inherited attribution discipline, identical to every Engineering Core sibling. | Not Applicable — reviewed, no gap found |
| **Revision integrity** | Every requirement, collection, and group is an `IEngineeringDocument`; every content change is a new, immutable revision via `IEngineeringDocumentStore.ReviseAsync` — no update-in-place path exists anywhere in `Tempest.Core.Requirements`. | Not Applicable — inherited, already-reviewed guarantee |
| **Traceability integrity** | Every relationship (`LinkAsync`) is append-only, inherited from `IEngineeringDocumentStore.LinkAsync`'s own existing guarantee — confirmed unchanged from `WP7.2B Security Architecture.md`/`WP7.2C Security Review.md`. No framework-level check prevents a duplicate or contradictory relationship — the same disclosed, non-blocking limitation `TD-18` already carries, extended, not worsened. | Accepted Risk — mirrors `TD-18`'s own existing disposition |
| **Concurrent modification** | Confirmed, unresolved: `ReviseAsync`/`SetStatusAsync` carry no compare-and-swap or expected-prior-revision check, exactly as `ADR-0060` accepts. `ReviseAsync_ConcurrentRevisions_NeverProduceDuplicateRevisionNumbers` proves the store itself remains internally consistent (no two revisions ever claim the same number) even though editorial intent can still be silently overwritten. | **Technical Debt (`TD-25`, new — formally registered by this Work Package)** |
| **Exception disclosure** | Every custom exception (`DuplicateRequirementIdentifierException`, `RequirementNotFoundException`, `InvalidRequirementStatusTransitionException`) discloses only identifiers already known to the caller (an identifier, a Guid, a status pair) — no internal state, no stack detail, no sensitive content is echoed. | Not Applicable — reviewed, no gap found |
| **Serialization safety** | `RequirementDto`/`RequirementCollectionDto`/`RequirementGroupDto` are serialized via `System.Text.Json`, never deserialized from untrusted external input — every deserialization call reads back only this service's own previously-written content, mirroring every Engineering Core sibling's own identical, already-reviewed pattern. | Not Applicable — no untrusted deserialization occurs |
| **Data integrity** | Identifier-to-document-Id mapping is written atomically per-identifier via `AsyncKeyedLock`, proven by `CreateAsync_ConcurrentCallsWithSameIdentifier_OnlyOneSucceeds` (15 concurrent calls, exactly one succeeds) — mirrors `MaterialCatalog.RegisterAsync`'s own identical, already-proven concurrency guarantee. | Not Applicable — reviewed, proven correct |
| **Tamper resistance** | No cryptographic signing of any stored document exists anywhere in this platform — mirrors `TD-16`'s identical, already-accepted, platform-wide disclosure. Not a Requirements-specific gap. | Accepted Risk — inherited, platform-wide, already-disclosed posture |
| **Resource exhaustion** | `IRequirementsService` imposes no bound on the number of requirements, relationships, or collection members a caller may create — mirrors `TD-22`/`TD-24`'s own identical "no measured problem yet" disclosure discipline for Calculation/Verification. `ListAsync` and `GetRelationshipsAsync` each scale linearly with total count, the same characteristic `IAuditQuery`/`MaterialCatalog.ListAsync` already carry, disclosed, not newly introduced. | Technical Debt — mirrors `TD-22`/`TD-24`'s own existing disposition; not separately re-registered, since it is the identical, already-tracked pattern, not a new finding |
| **Dependency risk** | No new third-party dependency was introduced — only `System.Text.Json` and `Tempest.Core.Concurrency.AsyncKeyedLock`, both already used extensively elsewhere in `Tempest.Core`. | Not Applicable |
| **Supply-chain considerations** | No new dependency, therefore no new supply-chain surface. | Not Applicable |
| **Secure defaults** | `CreatedByPrincipalId` defaults to the honest `"unknown"` sentinel when no principal is established, mirroring `EngineeringDocumentStore`/`MaterialCatalog`/`CalculationEngine`/`VerificationService`'s own identical, already-reviewed pattern — never silently omitted, never spoofed. `RequirementStatusTransitions` defaults toward rejection (`InvalidRequirementStatusTransitionException`) for any transition not explicitly listed, never silently permitting an unreviewed one. | Not Applicable — reviewed, secure by construction |
| **Backwards compatibility** | `Tempest.Core.Requirements` is a brand-new namespace with zero existing consumers — every signature matches `WP7.2C Requirements Platform Contracts.md`'s own approved shape exactly, with zero deviation requiring its own compatibility consideration. | Not Applicable |

## New Debt Disclosed by This Review

### `TD-25` — No Concurrency-Conflict Detection on `ReviseAsync`/`SetStatusAsync`

**What.** Two concurrent editors of the same requirement can each
successfully call `ReviseAsync` or `SetStatusAsync`; the second call's
own content silently becomes current, with no conflict signalled to
either caller. `IEngineeringDocumentStore.ReviseAsync`'s own per-document
lock guarantees the store itself never produces two revisions claiming
the same revision number — the store remains internally consistent —
but editorial intent is not protected.

**Why this is debt, not merely a limitation.** The Requirements
Engine's own target users (a systems engineering team) are more likely
than any prior Engineering Core consumer to edit the same artefact
concurrently as ordinary practice, not an edge case.

**Revisit trigger.** A real, demonstrated multi-author collaborative-
editing incident.

**Disposition.** Accepted, per `ADR-0060` — implementing a fix now would
deviate from the approved contract (`ReviseAsync`'s own signature carries
no expected-prior-revision parameter) without a genuine implementation
defect to justify the deviation.

## Verdict

**Zero Release Blocking findings.** One new Technical Debt item (`TD-25`)
disclosed and formally registered; one existing Technical Debt
disposition (`TD-18`) extended, not worsened; one existing Technical
Debt pattern (`TD-22`/`TD-24`'s own "no bound on recorded volume"
disclosure) recognised as recurring, not separately re-registered. No
finding from either prior review stage (`WP7.2B`, `WP7.2C`) was
contradicted by what the real, shipped implementation actually does —
every anticipated finding was confirmed exactly as anticipated.

## Related Documents

`WP7.2B Security Architecture.md`; `WP7.2C Security Review.md`;
`ADR-0060`; `ADR-0061`; `docs/governance/Quality/Technical Debt
Register.md` (`TD-16`, `TD-18`, `TD-22`, `TD-24`, and this Work
Package's own new `TD-25`); `docs/releases/v0.7.0/WP7.3A Implementation
Report.md`; `docs/releases/v0.7.0/WP7.3A Technical Debt Assessment.md`.
