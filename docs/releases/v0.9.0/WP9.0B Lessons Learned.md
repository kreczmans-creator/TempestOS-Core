# WP 9.0B — Product Configuration & BOM Management — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar situation should know going in.

## What Went Well

**Half the Configuration Management scope was already done.**
`Configuration`/`Baseline`/`Release` existed as real, tested `WP8.2C`
classes before this Work Package began — "working" vs. "released" needed
no new concept at all, just reading `IHasLifecycle.Status`, which every
one of them already carries. Checking a Kind's own `Implementation/`
folder directly, before assuming a scope item needs new code, paid off
a second consecutive Work Package running.

**Two never-used extension points were exactly ready.**
`ValidationRuleSet.Register` and `IReferenceIntegrityChecker.CheckBaselineMembersAsync`
had existed, untouched by any caller outside a test, since `WP8.2C`. Both
slotted in with zero contract friction — strong evidence that `WP8.2B`'s
own contract-design discipline (build the extension point before the
first real user needs it) was worth the up-front cost.

## What Was Harder Than Expected

**Finding the `ReviseAsync` bug required building real data, not just
unit tests.** Every `WP 9.0A` unit test exercised Rename/Move/Delete in
isolation; none happened to revise an object afterward. It was only
while writing the representative data's own deliberate "revise after the
baseline was frozen" scenario — a genuinely realistic sequence a real
user would hit — that the bug became observable. Worth remembering:
representative/demo data that tells a real story sometimes finds defects
targeted unit tests, written before the bug existed to think about,
never will.

**A flaky test from an unexamined assumption about dictionary
ordering.** `ConcurrentDictionary.Values` iteration order is not
insertion order, a fact easy to assume otherwise from casual, single-run
testing (it usually *looks* stable for a small dictionary). Running the
full suite multiple times, not just the new test file in isolation,
caught it before it became a standing source of confusion.

**Deciding what NOT to build.** The instinct to give `UnitOfMeasure` real
type safety via `Quantity<TDimension>` was strong — the machinery
already exists and looked reusable at first glance. Working through
*why* it doesn't actually fit (a count is not a dimension; BOM display
never needs conversion) took more deliberate reasoning than reusing it
outright would have, but produced a materially simpler, more honest
design.

## Process Observations

Two genuine implementation-defect findings (`TEMPEST-VAL` collision,
`ReviseAsync` state loss) surfaced during this Work Package's own
forward work, not a dedicated audit — both were in code written earlier
in this same, not-yet-committed session. Fixing them immediately, with
regression tests, rather than only disclosing them, was the right call
specifically because neither had yet become part of any commit or
tagged release; the same finding discovered after a release ships would
warrant the disclose-not-silently-fix treatment this project applies
everywhere else.

## Recommendation for Future Work Packages

When adding a mutable field to any class with a `ReviseAsync`-shaped
"produce a new instance carrying old identity forward" operation, check
explicitly whether that operation actually carries the new field
forward — a freshly-reconstructed instance defaulting a field is an easy
gap to introduce silently, and unit tests that never chain the two
operations together will not catch it.

## Related Documents

`WP9.0B Implementation Report.md`; `WP9.0B Technical Debt Assessment.md`
(`TD-27`); `ADR-0083`; `WP9.0A Lessons Learned.md`.
