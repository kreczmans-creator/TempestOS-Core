# WP 7.0C — Testing Strategy

## Baseline

This document extends, and does not replace, `docs/academy/06
Engineering Standards/02-testing-strategy.md` and `docs/releases/
v0.6.0/Testing Strategy.md`'s own precedent for a release-scoped testing
document, applied here to the five proposed Engineering Foundation
frameworks before any implementation begins. Every convention already
established applies unchanged: the internal-test-seam pattern for
ambient/broad contracts, minimal and clearly-separated test fixtures,
explicit test-category coverage matched to each framework's own
contract, regression tests named for the scenario they reproduce,
deterministic coordination for any concurrency-sensitive test, and a
full build/test run from a clean, committed tree before any owning Work
Package is reported done.

**This document proposes a test strategy for contracts that do not yet
have an implementation** — every row below is a plan, to be executed by
each framework's own owning Work Package (`WP7.0B Candidate Work Package
Catalogue.md`, Candidates `D`–`H`), not a claim that any test exists
today. Starting point: **1016 tests passing, 0 warnings, 0 errors** (the
`v0.6.0` baseline) — every owning Work Package must leave that number
undiminished before its own new tests are even considered.

## Test Category Definitions (Reused, Unchanged)

The same six categories `v0.6.0`'s own Testing Strategy defined apply
here without modification: Unit Tests, Integration Tests, Failure
Injection Tests, Regression Tests, Performance Tests, Documentation
Validation.

## Per-Framework Test Strategy

### Engineering Data Model

| Category | Plan |
|---|---|
| Unit | `CreateAsync`/`FindAsync`/`ReviseAsync`/`GetRevisionHistoryAsync`/`LinkAsync`/`GetReferencesAsync` round-trip correctness, against a real (not faked) store implementation, mirroring `IPersistenceStore`'s own existing test philosophy. |
| Integration | A document created, revised, and linked through the real `IEngineeringDocumentStore`, backed by whatever storage substrate the owning Work Package's own architecture phase selects. |
| Failure Injection | `EngineeringDocumentNotFoundException` for every operation against a non-existent Id; a simulated storage-substrate failure propagates unmodified. |
| Regression | Revision-number atomicity under concurrent `ReviseAsync` calls against the same document — the single highest-risk correctness property this framework's own contract names. |
| Performance | None proposed at this stage — no throughput target is set in this review, mirroring `v0.6.0`'s own identical position for Persistence. |
| Documentation Validation | Every code example in this framework's own future Academy concept guide compiles and behaves as documented. |

### Units & Quantities Framework

| Category | Plan |
|---|---|
| Unit | Conversion round-trip correctness (`ConvertTo` then convert back recovers the original value within floating-point tolerance) across every defined `Unit<TDimension>` pair. |
| Integration | Not applicable in the usual sense — this framework has no Platform Service dependency to integrate against (`WP7.0C Platform Integration Matrix.md`). |
| Failure Injection | `IncompatibleUnitsException` for a malformed `Unit<TDimension>` with a mismatched conversion factor. |
| Regression | None anticipated at this stage — no prior defect exists to regress against, since nothing has been implemented yet. |
| Performance | A conversion operation should complete without measurable overhead beyond ordinary floating-point arithmetic — a candidate for a documented manual benchmark rather than an automated performance test, mirroring `v0.6.0`'s own "not every Work Package has one" precedent. |
| Documentation Validation | Every code example in this framework's own future Academy concept guide compiles. |
| **Additional: compile-time rejection test.** | A dedicated test category unique to this framework: a test asserting that code attempting to convert between two different `IDimension` types **does not compile** — proving the generic-constraint safety guarantee is real, not merely documented. Mirrors how this platform already verifies certain generic-constraint guarantees elsewhere. |

### Materials Framework

| Category | Plan |
|---|---|
| Unit | `RegisterAsync`/`FindAsync`/`ListAsync` round-trip correctness. |
| Integration | A registered material's own underlying document is confirmed genuinely revisionable through `IEngineeringDocumentStore` directly — proving the "no second storage mechanism" claim in `WP7.0C Engineering Foundation Contracts.md`, not merely asserting it. |
| Failure Injection | `DuplicateMaterialException` for a re-used `materialId`. |
| Regression | None anticipated at this stage. |
| Performance | None proposed. |
| Documentation Validation | None beyond what the Data Model's own concept guide already validates — Materials is a worked example, not new documented content (`WP7.0C Academy Plan.md`). |

### Engineering Calculation Framework

| Category | Plan |
|---|---|
| Unit | `RegisterDefinition`/`ExecuteAsync` round-trip correctness for a simple, deterministic test calculation. |
| Integration | A registered calculation consuming a real `Quantity<TDimension>` input, confirming the by-convention Units & Quantities relationship actually works end to end. |
| Failure Injection | `CalculationDefinitionNotFoundException` for an unregistered Id; `CalculationInputInvalidException` propagates unmodified from a definition that deliberately rejects its input. |
| Regression | None anticipated at this stage. |
| Performance | None proposed at contract-review stage — a genuine per-calculation performance target depends entirely on what a real, future calculation actually does. |
| Documentation Validation | Every code example in this framework's own future Academy concept guide compiles. |
| **Additional: purity/concurrency test.** | A dedicated test proving that concurrent `ExecuteAsync` calls against the *same* registered, genuinely pure calculation Id, with different inputs, produce correct, non-interfering results — the concrete test this framework's own "purity enables lock-free concurrency" architectural claim requires to be more than an assertion. |

### Verification & Validation Framework

| Category | Plan |
|---|---|
| Unit | `RecordAsync`/`GetVerificationHistoryAsync` round-trip correctness across all three `VerificationOutcome` values. |
| Integration | A verification recorded against a real document created through `IEngineeringDocumentStore`, confirming the cross-framework dependency actually works end to end. |
| Failure Injection | `EngineeringDocumentNotFoundException` (the Data Model's own, reused type) for a non-existent `subjectDocumentId`. |
| Regression | None anticipated at this stage. |
| Performance | None proposed. |
| Documentation Validation | Every code example in this framework's own future Academy concept guide compiles, including the worked comparison against Audit and Calculation Records. |

## Cross-Framework Testing Observations

- **Every framework's own Failure Injection plan proves a specific,
  named exception from `WP7.0C Engineering Foundation Contracts.md` —
  none is left undocumented at this stage**, mirroring `v0.6.0`'s own
  discipline of tying every planned failure-injection test to a
  specific contract clause.
- **No framework has a Regression Tests entry yet** — expected and
  correct, since regression tests exist to reproduce a real, historical
  defect, and none of these five frameworks has been implemented yet to
  have one. Each owning Work Package's own testing practice will add
  these as real defects are found during implementation, exactly as
  every `v0.6.0` Work Package's own testing did.
- **Units & Quantities and Engineering Calculation Framework each have
  one additional, framework-specific test category** beyond the six
  standard ones — a genuine, disclosed departure from strict uniformity,
  because each framework's own core correctness guarantee (dimensional
  safety; calculation purity) is not fully captured by the six generic
  categories alone.

## Related Documents

`docs/academy/06 Engineering Standards/02-testing-strategy.md`;
`docs/releases/v0.6.0/Testing Strategy.md`; `WP7.0C Engineering
Foundation Contracts.md`.
