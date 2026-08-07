# WP 9.3A — Verification Management Workspace — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar situation should know going in.

## What Went Well

**The Kind-keyed Workspace extension model generalised a fifth time,
this time needing no execution-bridging adapter at all.** `WP 9.2A`
proved the model against a Domain object paired with a generic,
per-Template execution Framework requiring a real adapter
(`CalculationTemplateRegistry`). This Work Package proves the opposite
case cleanly: a Domain object (`VerificationActivity`) paired with a
Framework (`IVerificationService`) whose own single action needed no
bridge at all. Recognising this early — by reading `IVerificationService`'s
own actual shape before assuming an adapter was needed by analogy —
avoided building unnecessary structure.

**A category-based Explorer categorisation (`VerificationMethodCategory`)
reused `WP 9.4A`'s own `DocumentCategory` pattern directly, with no new
abstraction.** Where `WP 9.2A` needed a synthetic Kind for Templates,
this Work Package needed nothing beyond the identical "map a real
object's own field to a display category" function `WP 9.4A` had
already established one release cycle earlier (within the same session).
Reuse across two consecutive real-discipline Work Packages of the exact
same categorisation shape is a genuine, reusable pattern now, not
merely a coincidence.

## What Was Harder Than Expected

**Assuming `CalculationRecordReader`'s own read mechanism would transfer
directly to Verification — and being wrong, caught only by nine failing
tests.** The first implementation draft of `VerificationRecordReader`
copied `CalculationRecordReader.GetResultHistoryAsync`'s own shape
verbatim, reading `EngineeringDomainContext.RelationshipRepository`.
Every test that recorded a result and read it back failed, returning
zero records. Root-caused by comparing `VerificationService.RecordAsync`'s
own actual source against `CalculationTemplateRegistry.ExecuteAsync`'s
own — the two frameworks solve "how does a Workspace reader find a
record" in genuinely different ways, and only one of the two (Calculations,
via a Workspace-layer `.LinkAsync()` call `CalculationTemplateRegistry`
itself makes) happens to populate `RelationshipRepository`. Worth
remembering directly: two frameworks that look structurally similar from
the outside (both "a Domain object" plus "a separate record-producing
service") can differ in exactly which underlying store a given piece of
data lands in — a precedent's own *shape* transferring does not
guarantee its own *specific data-access call* transfers with it.
Verified, not merely trusted, by running the test suite and reading the
actual failure.

**Deciding whether to "fix" the gap by adding a second link, rather than
reading differently.** The instinctive first fix (once the root cause
was understood) was to have `RecordVerificationResultCommandHandler`
call `activity.LinkAsync(record.Id, "verifiedBy")` itself, after
`RecordAsync` returned, mirroring `CalculationTemplateRegistry`'s own
explicit extra step. This was considered and rejected on inspection: it
would create a genuine *duplicate* raw-store reference (the same
source/target/kind recorded twice — once by `RecordAsync` internally,
once by the added call), a new, disclosable defect of this Work
Package's own making, worse than the gap it would "fix." The chosen fix
— read the raw store directly, the same data `RecordAsync` already
correctly wrote once — introduces no duplication and needs no additional
write at all. Worth remembering: when a "fix" for a data-visibility gap
would itself require writing new data, checking first whether the
*existing* data is already correct and merely being read from the wrong
place is worth the extra minute — it was, here.

## Process Observations

This Work Package is the first to be commissioned with a controlling
instruction containing a disclosed error of its own (the "Await...
`WP 9.4A`" closing line, referring to a Work Package already complete
before this one began). The governing instruction ("disclose all
inconsistencies... do not silently modify historical records") was
applied to the controlling instruction itself, not only to the
repository's own prior state — recorded plainly in the Implementation
Report and `PROJECT_STATUS.md`, proceeding under the Work Package's own
real, intended number (`9.3A`) rather than either silently substituting
`9.5A` to make the instruction's own final line literally correct, or
refusing to proceed.

Like `WP 9.2A`'s and `WP 9.4A`'s own Lessons Learned, this Work Package's
own one genuine implementation-time finding (`TD-32`) is a pre-existing
platform characteristic, not a bug introduced here — but unlike either
of those two, it was found by a *failing test*, not by direct code
inspection alone. Worth recording as a data point in its own right: the
"build representative data, run the real integration suite" discipline
this project has followed since `WP 9.0B` continues to catch things a
purely static review would not.

## Recommendation for Future Work Packages

When connecting a Workspace-layer reader to two or more existing
Frameworks that look structurally similar from the outside, verify each
one's own actual relationship-writing mechanism by direct source
inspection before assuming a prior Work Package's own reader pattern
transfers unmodified — and if a test suite is available before the
pattern is trusted, let it run against the real, seeded data first. When
a data-visibility gap is found, check whether a read-side fix (reading
the existing, already-correct data from the right place) is sufficient
before reaching for a write-side one (adding a new link) — the latter
risks introducing genuine duplication a read-side fix cannot.

## Related Documents

`WP9.3A Implementation Report.md`; `WP9.3A Technical Debt Assessment.md`
(`TD-32`); `ADR-0089`; `ADR-0090`; `WP9.2A Lessons Learned.md`; `WP9.4A
Lessons Learned.md`.
