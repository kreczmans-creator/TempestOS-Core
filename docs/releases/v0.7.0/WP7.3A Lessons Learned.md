# WP 7.3A — Requirements Engine — Lessons Learned

## Purpose

Capture what this Work Package's own implementation experience revealed
— both about the Requirements Engine specifically and about this
programme's own three-phase (architecture → contracts → implementation)
governance discipline more generally.

## What Went Well

**Zero architectural rework.** This is the strongest possible validation
of the programme's own architecture-first discipline: two full Work
Packages of upstream design (`WP7.2B` architecture, `WP7.2C` contracts)
produced a specification that survived implementation completely
unchanged. Every concept, every method signature, every exception type
matched the approved contracts exactly. See `WP7.3A Systems Engineering
Impact Assessment.md`.

**The reuse-of-existing-mechanism pattern held for a fourth consecutive
framework.** Materials, Calculations, and Verification each reused
`IEngineeringDocumentStore` rather than inventing new storage; the
Requirements Engine is the fourth framework to do so, and the first to
also demonstrate that relationships (`LinkAsync`) alone are sufficient
to build a hierarchy (Groups), a membership model (Collections), and a
digital thread (`GetEvidenceAsync`) — no new storage or traversal
primitive was needed anywhere.

**The "deciding test" from `ADR-0061` is a genuinely reusable
artefact.** Rather than re-deriving from scratch whether a new service
should gate permissions internally or leave it to the calling layer,
this Work Package articulated an explicit, generalisable test
(evidentiary/audit-adjacent data gates internally; ordinary operational
content leaves it to the caller) that the next Work Package facing the
identical question can apply directly instead of re-litigating.

## What Was Harder Than Expected

**The architecture-to-contract narrowing on Allocation targets went
uncaught for two full Work Packages.** `WP7.2B`'s own broader vision
(open-string allocation targets) was never carried into `WP7.2C`'s own
approved contract, and neither `WP7.2C`'s own review nor this Work
Package's own early implementation planning caught the gap until the
actual `LinkAsync` signature was being written against the approved
contract. This suggests a process improvement: a contract review stage
should explicitly cross-check every architectural capability named in
the prior stage against the contract being finalised, rather than
relying on the contract's own internal consistency alone.

**Revision-number off-by-one errors recurred across multiple tests.**
Three separate test assertions were initially written assuming
`CreateAsync` produces revision number 0; the actual
`EngineeringDocumentStore.CreateAsync` behaviour starts at revision 1.
This is now the second Work Package in this project's history
(`Tempest.Core.EngineeringData` consumers generally) where this exact
assumption has had to be corrected during test-writing rather than
verified against source first — a minor, recurring friction point
future Work Packages consuming `IEngineeringDocumentStore` should check
proactively.

## Process Observations

**A dedicated Security Review scoped to real, shipped code (not a
proposed design) is more valuable than reviewing the design alone.**
Every finding in `WP7.2B Security Architecture.md`/`WP7.2C Security
Review.md` was confirmed exactly as anticipated by `WP7.3A Security
Review Report.md` — a genuinely reassuring outcome, but one that could
only be established by reviewing the actual implementation, not the
proposal. This is the third consecutive Work Package (`WP7.1D`, `WP7.1E`,
now `WP7.3A`) to perform this kind of review, reinforcing that it should
remain standard practice for every implementation Work Package going
forward, not only ones judged to carry unusual security risk.

**Disclosing a contract-stage narrowing as a Future Capability, rather
than silently absorbing it or misclassifying it as Technical Debt,
proved to be the right call.** The distinction articulated in `WP7.3A
Technical Debt Assessment.md` (debt = a regression from a working state;
future capability = a design phase's own aspiration never committed to
by the next phase) is a useful one for future Work Packages to apply
when they encounter a similar gap between their own approved contract
and an earlier architecture document's broader vision.

## Recommendation for Future Work Packages

When a contract review stage (a "WP7.xC"-shaped Work Package) narrows an
architecture stage's own broader vision, that narrowing should be
explicitly named and disclosed in the contract review's own deliverables
— not discovered for the first time during implementation, as happened
here. This is itself a candidate governance-process improvement, not
merely a one-off observation limited to the Requirements Engine.

## Related Documents

`WP7.3A Implementation Report.md`; `WP7.3A Engineering Review Report.md`;
`WP7.3A Security Review Report.md`; `WP7.3A Technical Debt Assessment.md`;
`WP7.3A Future Capability Recommendations.md`; `ADR-0061`.
