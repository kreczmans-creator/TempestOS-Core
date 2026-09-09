# WP 6.5 — Audit Framework — Lessons Learned

## 1. A Validation Mandate Requires an Actual Test Suite, Not a Restated Opinion

This Work Package was explicitly tasked with validating `WP 6.4`'s own
Persistence abstraction, not merely re-asserting the architecture
package's own prediction. Writing `AuditQueryTests`' own dedicated
filter-correctness suite (by actor, by action, by date range, combined,
ordering, empty results) turned "we believe client-side filtering is
adequate" into "this test suite proves client-side filtering is
adequate" — a categorically stronger and more useful claim for whoever
reads this Work Package's own conclusion later. **Lesson: when a brief
says "validate," produce evidence, not restated confidence.**

## 2. Not Every Shared Utility Needs to Be Reused by Every New Consumer

`Tempest.Core.Concurrency.AsyncKeyedLock` was built for Persistence and
Settings, both of which have genuine same-key-collision risk. Audit's
own record keys are always unique by construction, so the identical
race that utility prevents cannot occur here — and using it anyway
would have added complexity solving a problem Audit doesn't have.
**Lesson: "reuse before invention" means reusing what actually applies,
not reflexively adopting every existing pattern a new service could
technically use.**

## 3. Two Different Failure Philosophies Can Coexist Correctly in One Service

`AuditRecorder` fails loudly for storage failures (propagates
unchanged) but fails softly for attribution (records `"unknown"` rather
than refusing to write). These look inconsistent at first glance, but
each is the right answer for what's actually at stake: a storage
failure means the record genuinely didn't happen, which callers must
know; a missing principal means the record happened but its actor is
uncertain, which is still useful information. **Lesson: "be consistent"
should mean "apply the right principle consistently to each situation,"
not "use the same literal behaviour everywhere regardless of what's at
stake."**

## 4. A Deep, Multi-Step Test Is What Finds Bugs a Shallow One Hides

The premature-resource-disposal bug in `WP 6.4`'s own
`SettingsHostRegistrationTests.cs` existed from the moment that file
was written, but nothing in that Work Package's own test suite happened
to need the temp directory to survive long enough to expose it. This
Work Package's own round-trip test (establish principal → record →
query → assert, several awaited steps deep) had enough intervening
asynchronous work between the resource's creation and its last use to
make the bug fire deterministically, every time. **Lesson: a test that
only checks "does this resolve" or "does this return the right type" is
cheap but shallow; a test that exercises a full, realistic, multi-step
operation is what actually catches this class of bug — write at least
one of the latter for anything that touches a disposable resource
across an `await`.**

## 5. Confirming a Risk a Second Time Is Not the Same as Retiring It

`docs/releases/v0.6.0/Risk Register.md`'s `R8` was already "confirmed,
not retired" once, at `WP 6.4`. This Work Package confirmed it again,
with stronger evidence (a real filter-correctness test suite, not just
"the shape shipped as designed"). It would have been tempting to treat
two confirmations as a de facto retirement — they are not the same
thing, and saying so explicitly (rather than letting the register's own
status quietly drift toward "probably fine now") is what keeps the
Risk Register trustworthy. **Lesson: repetition of a finding is not
progress toward resolving it; only an actual change in the underlying
fact is.**

## Related Documents

`WP6.5 Implementation Report.md`; `WP6.5 Engineering Review Report.md`;
`WP6.5 Platform Impact Assessment.md`; `WP6.5 Technical Debt
Assessment.md`; `WP6.5 Future Capability Recommendations.md`;
`docs/academy/03 Work Packages/WP6.5-audit-framework-
implementation.md`; `ADR-0045`.
