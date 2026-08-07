# WP 9.9.0 — Release Preparation & Product Baseline — Product Approval Report (Second Pass)

## Purpose

A second, independent formal recommendation for `v0.9.0`, superseding
none of `WP9.9.0 Product Approval Report.md`'s own first-pass
recommendation (which already read **APPROVED**) but re-confirming it
against the repository's own current, post-`WP 9.8B` state, and
disclosing what changed in between.

## Recommendation

# **APPROVED**

`v0.9.0` ("Mechanical Foundation") is recommended for Product Approval,
release, tagging, and merge to `main` by the Product Owner — the
identical recommendation the first pass already gave, now given a
second, independent basis.

## What Changed Since the First Pass's Own Recommendation

### Resolved: the four-Engineering-Foundation-framework Platform Service gap

The first pass's own single most important standing recommendation —
"make a firm decision about the four-Engineering-Foundation-framework
Platform Service Map/Register gap — this time, actually decide" — was
acted on. `WP 9.8B` closed it in full. This pass independently
re-verified the closure rather than trusting `WP 9.8B`'s own claim:
direct inspection of all five governance documents this project
maintains for Platform Services confirms complete, consistent coverage
for all 30 real services, including the four that were missing.

### New finding: `TD-34`, a genuine but non-blocking test flake

This pass's own fresh test-suite verification caught a live instance of
a test characteristic this project has informally described since `WP
6.3` but never formally registered — `CompositeLogSinkTests`'s own
intermittent, cross-test-class `Console.Error`-capture race. Fully
characterised (root cause identified by source inspection, non-
reproducibility confirmed by 5 isolated re-runs, resolution-on-re-run
confirmed), formally registered as `TD-34`, judged **not** Release
Blocking for the identical reason every other "disclosed, no
data-correctness issue" item in this register carries that
disposition: the underlying `CompositeLogSink.Write` behaviour is
correct; only a test's own incidental dependency on a shared, static
stream is at risk under parallel execution, and only intermittently.

### Unchanged: the "32 vs. 35 governance documents" drift

Still open, still outside the narrower scope of both `WP 9.8B` and this
pass.

## Evidence Supporting This Recommendation (Re-Confirmed, Not Assumed)

### Build and Test — Clean, With One Disclosed, Characterised Flake

4/4 projects, 0 warnings/0 errors, both configurations, from a fully
clean rebuild. 2026/2026 tests passing in 4 of 5 full-suite runs; the
one failure is `TD-34`, fully characterised, confirmed non-reproducible
in isolation and resolved on immediate re-run. Zero regression against
either `v0.8.0`'s own 1631 tests or the first pass's own 2026-test
baseline.

### Governance — Now Fully Consistent for Platform Services, One Drift Still Open

Every governance document this pass's own controlling instruction names
was re-audited directly. Platform Services governance is now, for the
first time in this project's history, fully consistent across all five
documents that describe it. The "32 vs. 35 governance documents" figure
remains a disclosed, open, non-blocking documentation-currency
question.

### Security — Unchanged, Zero Release Blocking

Eight dedicated Security Reviews across this release (seven
implementation Work Packages plus `WP 9.8B`), all independently
confirming zero Release Blocking findings, re-verified at the release
level by this pass's own Release Readiness Report §18.

## Why the One New Finding Does Not Block Approval

`TD-34` is a test-infrastructure characteristic, not a product defect —
it affects only the reliability of one diagnostic test's own assertion
under a specific, narrow concurrency condition (parallel execution
against another test class that redirects a shared, static console
stream), never the correctness of `CompositeLogSink`'s own real,
shipped behaviour, which the same test proves correct every time it
runs in isolation. It has, in effect, always been present since `WP
5.2`/`WP 9.0A` (the Work Packages that respectively introduced
`CompositeLogSink` and the first `[Collection("Console output
capture")]`-tagged test) — this pass is the first to observe and
formally document it, not the first to introduce it.

## Constraints Honoured

Per this Work Package's own explicit constraints: verification only, no
new functionality, no architectural changes. The one
implementation-adjacent action this pass took — registering `TD-34` —
is a governance-documentation entry, explicitly permitted by this Work
Package's own "no implementation changes unless correcting genuine
release-blocking defects or stale governance discovered during
verification" exception (a newly-discovered, previously-unregistered,
real test characteristic is exactly "stale governance discovered during
verification"). No Git merge, tag, `VERSION` change, or push was
performed. `VERSION` correctly remains `0.8.0`.

## What Happens Next

Per this Work Package's own explicit closing instruction: **STOP. Await
Product Owner release.** Two independent release-readiness passes now
recommend `v0.9.0` **APPROVED**, the second finding the release in a
materially more consistent governance state than the first left it.

**Standing recommendations, carried forward from the first pass, one
resolved:**

1. ~~Close the four-Engineering-Foundation-framework Platform Service
   Register/Map gap~~ — **Done, `WP 9.8B`.**
2. Reconstruct or formally retire the "32 governance documents" figure
   — still open.
3. Build `FCR-0005` (Governance Register Health-Check Tooling) — still
   open; `WP 9.8B`'s own existence, and this pass's own manual
   20-step re-verification, are each further evidence for it.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Product Approval Report.md` (first pass);
`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report (Second
Pass).md`; `docs/releases/v0.9.0/WP9.8B Reconciliation Report.md`;
`docs/governance/Quality/Technical Debt Register.md` (`TD-34`).
