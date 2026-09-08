# WP 7.2C — Testing Strategy

## Baseline

This document extends, and does not replace, `docs/academy/06
Engineering Standards/02-testing-strategy.md` and `WP7.0C Testing
Strategy.md`'s own precedent for a contract-review-stage testing
document, applied here to the Requirements & Verification Platform
before any implementation begins. Every convention already established
applies unchanged: the internal-test-seam pattern, minimal and
clearly-separated test fixtures, explicit test-category coverage matched
to the contract, regression tests named for the scenario they reproduce,
deterministic coordination for any concurrency-sensitive test, and a
full build/test run from a clean, committed tree before the owning
Work Package is reported done.

**This document proposes a test strategy for contracts that do not yet
have an implementation** — every row below is a plan, to be executed by
the owning implementation Work Package, not a claim that any test
exists today. Starting point: **1275 tests passing, 0 warnings, 0
errors** (the `v0.7.0` Engineering Foundation baseline, confirmed by
`WP 7.1F`) — the owning Work Package must leave that number undiminished
before its own new tests are even considered.

## Test Category Definitions (Reused, Unchanged)

The same six categories `WP7.0C Testing Strategy.md` defined apply here
without modification: Unit Tests, Integration Tests, Failure Injection
Tests, Regression Tests, Performance Tests, Documentation Validation.

## Per-Contract Test Strategy

### `IRequirementsService`

| Category | Plan |
|---|---|
| Unit | `CreateAsync`/`FindAsync`/`FindByIdentifierAsync`/`ReviseAsync`/`SetStatusAsync`/`LinkAsync`/`GetRelationshipsAsync`/`ListAsync` round-trip correctness, against a real (not faked) `IEngineeringDocumentStore`, mirroring `MaterialCatalogTests.cs`'s own philosophy. |
| Integration | A requirement created, revised, related, and status-transitioned through the real `IRequirementsService`, backed by a real `IEngineeringDocumentStore`. |
| Failure Injection | `DuplicateRequirementIdentifierException` for a re-used identifier; `RequirementNotFoundException` for every operation against a non-existent Id; `InvalidRequirementStatusTransitionException` for every forbidden transition in `WP7.2C Requirement Lifecycle Model.md`'s own table. |
| Regression | Identifier-uniqueness atomicity under concurrent `CreateAsync` calls with the same identifier — the single highest-risk correctness property this contract names, mirroring `MaterialCatalog`'s own identical regression concern. |
| Performance | None proposed at this stage, mirroring every Engineering Foundation framework's own identical position. |
| Documentation Validation | Every code example in the owning Work Package's own future Academy concept guide compiles and behaves as documented. |

### `IRequirement`, `IRequirementCollection`, `IRequirementGroup`

| Category | Plan |
|---|---|
| Unit | Data-contract shape correctness only — no independent behaviour to test beyond what `IRequirementsService`'s own tests already exercise. |
| Integration | Collection/Group membership round-trip, confirmed genuinely revisionable/relatable through `IEngineeringDocumentStore` directly — proving the "no second storage mechanism" claim, not merely asserting it, mirroring `WP7.0C Testing Strategy.md`'s own identical discipline for Materials. |
| Failure Injection | Adding a non-existent requirement as a collection/group member fails (`EngineeringDocumentNotFoundException`). |
| Regression | None anticipated at this stage. |
| Performance | None proposed. |
| Documentation Validation | None beyond what `IRequirementsService`'s own concept guide already validates. |

### Relationships (Requirement Relationship, Allocation, Trace Link)

| Category | Plan |
|---|---|
| Unit | One test per reserved relationship kind (`WP7.2C Relationship Model.md`), confirming it is recorded and retrievable via `GetRelationshipsAsync`/`GetReferencesAsync`. |
| Integration | A full traceability chain (Derived From → Allocated To → Satisfies) traversed end to end through the real store, proving `WP7.2C Traceability Contract.md`'s own forward/backward traceability claims empirically, not merely by design. |
| Failure Injection | A link to a non-existent target document fails, inherited from `IEngineeringDocumentStore.LinkAsync`'s own existing test coverage. |
| Regression | None anticipated at this stage. |
| Performance | None proposed. |
| Documentation Validation | The relationship-kind vocabulary table (`WP7.2C Relationship Model.md`) matches the real, implemented constant values exactly — a direct cross-check, not merely a design-time assertion. |
| **Additional: reverse-allocation-traceability limitation test.** | A dedicated test proving that an open-string allocation target (no backing document) correctly yields no result from a reverse-traceability query — confirming `WP7.2C Traceability Contract.md` §3's own disclosed limitation is a real, observed behaviour, not merely a documented caveat. |

### Requirement Verification Link

| Category | Plan |
|---|---|
| Unit, Integration | **None proposed by this Platform** — this concept is not a new contract (`WP7.2C Requirements Platform Contracts.md` §6); its own correctness is entirely `Tempest.Core.Verification`'s own existing, already-passing test suite's responsibility. |
| Integration (Requirements-side) | A single, dedicated test confirming `IVerificationService.RecordAsync` accepts a `Requirement`'s own document Id as `subjectDocumentId` without any Requirements-specific accommodation — proving the "no wrapper, no duplication" claim (`WP7.2C Verification Integration Contract.md`) empirically. |

### `IRequirementEvidence`

| Category | Plan |
|---|---|
| Unit | Aggregation correctness against a requirement with a known, fixture-constructed set of verifications, linked calculation records, and linked documents. |
| Integration | End-to-end: create a requirement, record two verifications (one `Fail`, one `Pass`), link a calculation record, request evidence, confirm every fact appears correctly aggregated. |
| Failure Injection | `RequirementNotFoundException` for a non-existent requirement; `PermissionDeniedException` inherited from `GetVerificationHistoryAsync`'s own existing gate. |
| Regression | None anticipated at this stage. |
| Performance | None proposed — inherits `GetVerificationHistoryAsync`'s own existing, disclosed scaling characteristic (`TD-24`), not a new concern. |
| Documentation Validation | The digital thread worked example (`WP7.2B Digital Thread Architecture.md`) compiles and produces the documented aggregation shape. |

### `RequirementStatus` / Lifecycle

| Category | Plan |
|---|---|
| Unit | Every permitted transition in `WP7.2C Requirement Lifecycle Model.md`'s own table succeeds; every transition not in the table throws `InvalidRequirementStatusTransitionException` — an exhaustive, table-driven test, one case per matrix cell. |
| Integration | A full lifecycle walk (`Draft` → `Reviewed` → `Approved` → `Allocated` → `Verified` → `Satisfied`) through the real service. |
| Failure Injection | Covered by the exhaustive transition-table test above — no separate category needed. |
| Regression | None anticipated at this stage. |
| Performance | None proposed. |
| Documentation Validation | The state diagram (`WP7.2C Requirement Lifecycle Model.md`) matches the real, implemented transition table exactly. |

## Cross-Contract Testing Observations

- **Every contract's own Failure Injection plan proves a specific,
  named exception from `WP7.2C Requirements Platform Contracts.md` —
  none is left undocumented at this stage**, mirroring `WP7.0C Testing
  Strategy.md`'s own identical discipline.
- **No contract has a Regression Tests entry yet**, expected and
  correct — none of these contracts has been implemented yet to have a
  real, historical defect to regress against.
- **Relationships and the Lifecycle model each have one additional,
  contract-specific test category beyond the six standard ones** — the
  reverse-allocation-traceability limitation test and the exhaustive
  transition-table test respectively — mirroring `WP7.0C Testing
  Strategy.md`'s own precedent that a framework's own core correctness
  guarantee sometimes is not fully captured by the six generic
  categories alone.

## Related Documents

`docs/academy/06 Engineering Standards/02-testing-strategy.md`; `WP7.0C
Testing Strategy.md`; `WP7.2C Requirements Platform Contracts.md`;
`WP7.2C Relationship Model.md`; `WP7.2C Traceability Contract.md`;
`WP7.2C Requirement Lifecycle Model.md`; `WP7.2C Verification
Integration Contract.md`.
