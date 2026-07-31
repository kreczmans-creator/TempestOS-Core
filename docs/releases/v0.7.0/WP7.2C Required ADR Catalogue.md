# WP 7.2C — Required ADR Catalogue

## Status

**A catalogue of reserved ADR numbers, not finished ADR documents.**
Per this Work Package's own explicit instruction — "Reserve ADR numbers
only. Do not answer them." — none of the four entries below is written
as an `Accepted`-status file under `docs/adr/`; each remains deferred to
the owning implementation Work Package's own dedicated architecture-
confirmation pass. Numbering continues from `WP7.2B Required ADR
Catalogue.md` — the highest existing ADR remains `ADR-0057`; `ADR-0058`–
`ADR-0060` were reserved (not answered) by `WP 7.2B`, carried forward
unchanged here; `ADR-0061` is newly reserved by this Work Package's own
Contract Review.

## Carried Forward, Unchanged

### ADR-0058 — Requirements Platform: Classification, Storage, and Relationship to the Engineering Data Model

Unchanged from `WP7.2B Required ADR Catalogue.md`. This Contract
Review's own proposed contracts (`WP7.2C Requirements Platform
Contracts.md`) are built consistently with the Platform Service
classification and direct `IEngineeringDocumentStore` dependency
`WP 7.2B` proposed, but this Work Package does not ratify that proposal
as a written, Accepted ADR — per its own explicit instruction not to
answer reserved questions.

### ADR-0059 — Requirement Identity, Status, and Category Representation

Unchanged from `WP7.2B Required ADR Catalogue.md`, now informed by
concrete proposed shapes: `RequirementStatus` as a closed `enum`
(`WP7.2C Requirements Platform Contracts.md` §8), `Category` as an open,
nullable `string` (§9), `Identifier` as a `string` business key with a
dedicated index (§10). **This Contract Review proposes these three
shapes; it does not ratify them** — the owning implementation Work
Package's own architecture-confirmation pass remains where this
question is actually decided.

### ADR-0060 — Requirement Concurrency and Traceability Integrity Model

Unchanged from `WP7.2B Required ADR Catalogue.md`, now re-confirmed at
the contract level (`WP7.2C Security Review.md`'s own "Concurrent
modification" finding) — `IRequirementsService.ReviseAsync`'s own
proposed signature carries no expected-prior-revision parameter. Still
not answered.

## Newly Reserved by This Work Package

### ADR-0061 — Requirements Engine: Internal vs. Calling-Layer Permission Enforcement

**Context.** `WP7.2C Platform Integration Matrix.md` and `WP7.2C
Security Review.md` both disclose the same open question:
`IVerificationService.GetVerificationHistoryAsync` gates its own read
internally (`RequirePermission`, mirroring `IAuditQuery`'s own
precedent), while `IReportingService`/`IRequirementsService` (as
proposed) leave every permission check to the calling layer entirely
(mirroring `IReportingService`'s own explicit "the enforcement point is
the caller" precedent). Both patterns are real, already-proven
precedents within this platform — neither is a mistake — but this
Contract Review found no existing rule for *which* pattern a new
service should default to, and did not decide one itself.

**Anticipated decision.** Confirm whether any `IRequirementsService`
method (most plausibly `GetRelationshipsAsync` or a future
`GetEvidenceAsync`, given their evidentiary sensitivity, mirroring why
`GetVerificationHistoryAsync` itself is gated) should gate internally,
or whether every method remains calling-layer-enforced only, consistent
with `IReportingService`'s own precedent instead.

**Alternative considered and rejected.** Deciding this question within
the Contract Review itself, by simply picking one precedent, was
considered and rejected — per this Work Package's own explicit "reserve
ADR numbers only, do not answer them" instruction, and because a
real, principled distinction between the two existing precedents (does
the read expose evidence potentially sensitive beyond ordinary
operational data, as Verification's own history arguably does; or is it
ordinary state, as a Report's own generated content is) deserves its
own deliberate architecture-confirmation pass, not a default chosen
implicitly during contract drafting.

## Cross-Reference Check

Every entry above cites a specific `WP7.2C` (or, for the three carried
forward, `WP7.2B`) companion document. No open question disclosed
anywhere else in this Work Package's own deliverables (`Requirements
Platform Contracts.md`, `Security Review.md`, `Platform Integration
Matrix.md`, `Requirement Lifecycle Model.md`, `Relationship Model.md`,
`Traceability Contract.md`, `Verification Integration Contract.md`) is
missing an entry here.

## Related Documents

`WP7.2B Required ADR Catalogue.md` (the precedent this catalogue
extends); `WP7.2C Requirements Platform Contracts.md`; `WP7.2C Security
Review.md`; `WP7.2C Platform Integration Matrix.md`; `docs/adr/`
(`ADR-0001`–`ADR-0057`, the existing sequence this catalogue extends).
