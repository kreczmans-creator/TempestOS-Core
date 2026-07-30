# WP 7.1F — Security Review Summary

## Purpose

Reviews both dedicated Security Reviews performed during the Engineering
Foundation programme (`WP7.1D Security Review Report.md`,
`WP7.1E Security Review Report.md`), confirms every finding's own
disclosed classification, and determines whether any additional
Engineering-Core-wide security finding has emerged from viewing all five
frameworks together that no single framework's own review could surface
in isolation — the cross-cutting check a per-framework review cannot
perform by construction.

## 1. Audit of `WP 7.1D`'s Own Security Review (Engineering Calculation Framework)

Fifteen-category checklist, each finding re-confirmed directly against
the review document and, where a Technical Debt item resulted, against
`Technical Debt Register.md` itself:

| Finding | Classification | Confirmed |
|---|---|---|
| `TInput` itself is not validated by the framework (each registered definition's own responsibility) | Accepted Risk | Confirmed — matches the Command Framework's own identical "does not validate a command's own payload" precedent |
| Exception messages may echo a rejected input value, at the registering definition's own discretion | Accepted Risk | Confirmed — mirrors `EngineeringDocumentNotFoundException`'s own existing convention |
| No cancellation reaches into `Calculate` once dispatched | Technical Debt (`TD-21`) | Confirmed in register, Open, revisit trigger named |
| No bound on `CalculationContext`-recorded data volume or type fidelity | Technical Debt (`TD-22`) | Confirmed in register, Open, revisit trigger named |
| No upper bound on registration count; no network-facing surface this Work Package's own scope | Accepted Risk | Confirmed — matches `ICommandRegistry`'s own identical registration-time trust model |
| Every remaining category (exception disclosure beyond the above, serialization, thread safety, concurrency, data integrity, tamper resistance, trust boundaries, unsafe assumptions, dependency risk, supply chain, secure defaults, backwards compatibility) | Not Applicable | Confirmed — each reviewed with a stated reason, no gap found |

**Zero Release Blocking findings.** Two Technical Debt items (`TD-21`,
`TD-22`), one new Future Capability Register entry raised directly from
this review (`FCR-0035`, Calculation Execution Timeout & Cancellation
Support).

## 2. Audit of `WP 7.1E`'s Own Security Review (Verification Framework)

Same fifteen-category checklist, applied a second time:

| Finding | Classification | Confirmed |
|---|---|---|
| `RecordAsync`'s own multi-step linking sequence is not transactional | Technical Debt (`TD-23`) | Confirmed in register, Open, revisit trigger named |
| No bound on `VerificationContext`-recorded criteria/evidence/links; `GetVerificationHistoryAsync` scales with total reference count | Technical Debt (`TD-24`) | Confirmed in register, Open, revisit trigger named |
| `VerificationContext.ReferenceMaterial` accepts any string, unvalidated | Accepted Trade-off (`AT-17`) | Confirmed in register, mirrors `AT-16` exactly |
| `RecordAsync` remains unrestricted-write (unlike `GetVerificationHistoryAsync`, which is permission-gated) | Accepted Risk | Confirmed — mirrors `IAuditRecorder.RecordAsync`'s own identical asymmetry |
| Every remaining category | Not Applicable | Confirmed — each reviewed with a stated reason, no gap found |

**Zero Release Blocking findings.** Two Technical Debt items (`TD-23`,
`TD-24`), one Accepted Trade-off (`AT-17`), one new Future Capability
Register entry raised directly from this review (`FCR-0036`,
Transactional Multi-Document Operations for the Engineering Data Model).

## 3. Cross-Framework Finding Not Visible From Either Individual Review

**The same unvalidated-material-reference pattern (`AT-16`, `AT-17`) now
exists identically in two independent frameworks (`Calculations`,
`Verification`), each having reached it independently rather than by
one shared design decision propagated to both.** Reviewed together, this
is judged **not** a defect and **not** worth consolidating into one
shared validation mechanism: both frameworks reached the identical,
independently-justified conclusion (no hard dependency on `Materials`
exists in either, so validating a reference would cost adding one purely
for validation) — two independent frameworks converging on the same
answer is corroborating evidence the design boundary is sound, not a
sign of accidental duplication needing correction. No new Technical Debt
or Future Capability Register entry is raised from this observation
alone; `AT-16`/`AT-17` already each carry their own, identical revisit
trigger.

**A second, genuine cross-framework observation: `EngineeringData`'s own
`TD-18` (`LinkAsync`'s own concurrency behaviour under many simultaneous
calls against the same source document is not tested at the same depth
as `ReviseAsync`) is now load-bearing for a fourth consumer.**
`Materials`, `Calculations`, and `Verification` all call `LinkAsync`
during their own write paths (`Verification.RecordAsync` most
directly — several `LinkAsync` calls per execution, disclosed as
non-transactional in `TD-23`). `TD-18` was disclosed by `WP 7.1A` before
any of these three consumers existed; this review confirms it remains
accurate and increasingly relevant now that a real, multi-call consumer
(`Verification.RecordAsync`) exists, but finds no evidence of an actual
correctness problem — each `LinkAsync` call writes an independent,
randomly-keyed reference entry, so concurrent calls against the same
source document do not contend for a single mutable value the way
`ReviseAsync`'s own revision-number atomicity requires. **Not Release
Blocking; recommend `TD-18`'s own revisit trigger be reassessed alongside
`FCR-0036`, since both concern the same underlying `LinkAsync` call
pattern.**

## 4. No Third Dedicated Security Review Was Required for `WP 7.1A`–`WP 7.1C`

`WP 7.1A` (Engineering Data Model), `WP 7.1B` (Units & Quantities), and
`WP 7.1C` (Materials) each completed only the general Engineering Review
checklist, not a dedicated Security Review — consistent with their own
controlling instructions, none of which required one. This Work
Package's own review of all three, informed by the fifteen-category
checklist `WP 7.1D` and `WP 7.1E` later applied, finds no
retroactively-missed finding in any of the three: `EngineeringData`'s own
trust model (first-party, in-process callers only, no network-facing
surface), `UnitsAndQuantities`' own pure-value-type shape (no I/O, no
shared mutable state of any kind — the framework has no attack surface
to review), and `Materials`' own calling-layer-enforced authorization
(`AT-15`, already disclosed) each hold up under the same scrutiny applied
to their two siblings.

## Overall Classification Summary (Engineering Core, All Five Frameworks)

| Classification | Count | Items |
|---|---|---|
| Release Blocking | **0** | None |
| Technical Debt | 4 | `TD-21`, `TD-22`, `TD-23`, `TD-24` |
| Accepted Risk / Accepted Trade-off | 3 (of the Engineering-Foundation-specific set) | `AT-16`, `AT-17`, plus the `TInput`/registration-trust Accepted Risk findings folded into `WP 7.1D`'s own review |
| Not Applicable | Remainder of both fifteen-category reviews | Reviewed, no gap found |

## Verdict

**Zero Release Blocking security findings anywhere in the Engineering
Core.** Every disclosed Technical Debt item and Accepted Trade-off
carries a named, concrete revisit trigger, not an open-ended "someday."
The one cross-framework observation this review adds beyond what either
individual Security Review could see (`TD-18`'s own growing relevance as
`LinkAsync`'s own multi-consumer load increases) is itself not Release
Blocking, and is folded into this Work Package's own Future Capability
Register Review as a recommendation to reassess `TD-18` alongside
`FCR-0036`, not a new finding requiring immediate action.

## Related Documents

`WP7.1D Security Review Report.md`; `WP7.1E Security Review Report.md`;
`docs/governance/Quality/Technical Debt Register.md`; `docs/governance/
Future Capability Register.md`; `WP7.1F Technical Debt Disposition.md`;
`WP7.1F Future Capability Register Review.md`; `WP7.1F Engineering Core
Certification Report.md`.
