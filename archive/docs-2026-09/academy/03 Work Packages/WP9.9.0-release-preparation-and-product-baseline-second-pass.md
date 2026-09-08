# WP 9.9.0 — Release Preparation & Product Baseline (Second Pass)

## What This Document Is

A second, independent release-readiness closing review for `v0.9.0`,
commissioned by the Product Owner with the identical controlling
instruction as `WP9.9.0-release-preparation-and-product-baseline.md`'s
own first pass, after `WP 9.8B` (Platform Service Register
Reconciliation) closed that first pass's own top standing
recommendation. This is the first "verify, remediate, re-verify" cycle
this project has performed in full — a first pass finds a gap, a
dedicated Work Package closes it, a second pass independently
re-confirms the closure. Mirrors the first pass's own whole-review
retrospective format; the first pass's own document is left exactly as
written.

## 1. Introduction

Five release-closing reviews preceded this one (`WP 5.4`, `WP 6.8`,
`WP 7.1F`, `WP 7.4.0`, `WP 8.9.0`), each performed once per release.
`WP 9.9.0`'s own first pass was the sixth. This document is the first
instance of a release-closing review being performed *twice* for the
same release — not because the first pass was wrong (it recommended
`v0.9.0` **APPROVED**, and this pass reaches the identical
recommendation), but because the Product Owner chose to close the first
pass's own standing recommendation before finalising the release,
rather than after.

## 2. What Was Achieved

Every verification step the controlling instruction names was performed
again, fresh, against the current repository state — not assumed
unchanged from the first pass. Two genuine findings resulted, one
positive and one new: **the four-Engineering-Foundation-framework
Platform Service gap, this project's own single most persistent
disclosed governance finding, is now closed** (by `WP 9.8B`, independently
re-confirmed by this pass); and **`TD-34`**, a test-infrastructure
flake informally disclosed by name since `WP 6.3` but never formally
registered, was actually observed for the first time and formally
captured. Five completion deliverables produced, each suffixed "(Second
Pass)" to keep the first pass's own five identically-purposed documents
intact; `ReleaseNotes.md`/`Retrospective.md` updated in place, since
both are living release-level documents, not Work-Package-dated
artifacts.

**Recommendation, reconfirmed: `v0.9.0` APPROVED.**

## 3. Architectural Lessons

**A second verification pass is not redundant with the first merely
because nothing in `src/` changed between them.** Every build and test
result this pass produced was independently re-derived, not copied
forward — and one of them (the `TD-34` finding) differed from the first
pass's own equivalent result, despite zero code changes in between. The
lesson generalises: "the code hasn't changed" is not the same claim as
"a fresh verification run will return the identical result" — test
suite behaviour under concurrent execution is not fully deterministic,
and only re-running reveals that.

**Closing a disclosed governance gap and *re-verifying* the closure are
two distinct activities, and both have independent value.** `WP 9.8B`'s
own Reconciliation Report already claimed the gap closed, with its own
supporting evidence. This pass did not take that claim on faith — it
re-derived the same five-document consistency check independently and
reached the same conclusion by its own separate route. The value is not
in doubting `WP 9.8B`'s own rigour; it is in demonstrating that the
closure holds up under a second, differently-timed, independently-motivated
check, which is a stronger form of confirmation than either check alone.

## 4. Implementation Lessons

**A long-narratively-disclosed characteristic is not the same as a
tracked one, and the gap between the two can persist for a surprising
number of release cycles before anyone happens to catch it "live."**
`TD-34`'s own flake had been mentioned, by name, in prose, across at
least four prior release-closing reviews (`WP 6.3` onward) — each one
correctly stating "zero instances observed" and moving on. This pass's
own fifth full-suite run (one more than the first pass performed)
happened to be the one that caught it. Worth remembering: a
characteristic disclosed only in prose, never given its own tracked
entry, can be real and can eventually surface — "we've never actually
seen it happen" is evidence of low frequency, not evidence of absence,
and is not a substitute for formally registering the characteristic
once a real instance exists to cite.

**Root-causing a flake by direct source inspection, rather than merely
re-running until it stops reproducing, produces a disclosure a future
reader can actually act on.** This pass traced the failure to a
specific line (`CompositeLogSink.Write`'s own `Console.Error.WriteLine`
call) and a specific mechanism (a race against any concurrently-running
`[Collection("Console output capture")]`-tagged test's own console
redirection) — not merely "sometimes this test fails, we don't know
why." The resulting Technical Debt Register entry names two concrete,
actionable revisit triggers as a direct consequence.

## 5. Repository Maturity

**The single most persistent disclosed governance finding in this
project's history is now closed.** Three consecutive release-closing
reviews (`WP 7.4.0`, `WP 8.9.0`, `WP 9.9.0` first pass) found the
four-Engineering-Foundation-framework Platform Service gap open; a
fourth, `WP 9.9.0`'s own second pass, is the first to find it closed —
not because this pass fixed it (it is verification-only), but because a
dedicated Work Package, commissioned specifically in response to this
pass's own predecessor, did.

**A new governance-completeness finding was closed within the same pass
that found it**, unlike the Platform Service gap's own three-review
history. `TD-34` was found and formally registered in one continuous
pass — a data point in favour of "register what you find immediately,
rather than only disclosing it narratively and hoping a future review
gets around to tracking it," the same lesson this project's own
Technical Debt Register discipline has reinforced several times before
(`WP 9.3A`'s TD-count correction, `WP 9.5A`'s re-verification) applied
here to a test characteristic rather than a governance-document
arithmetic error.

**`FCR-0005` (Governance Register Health-Check Tooling) now has its
strongest evidentiary case in this project's history.** Two
governance-completeness gaps found and closed within one release
cycle's own closing sequence (the Platform Service gap, `TD-34`), both
requiring a human to manually re-derive facts a tool could check in
seconds, is direct, repeated, first-hand evidence for the exact
capability that Future Capability candidate names.

## 6. Recommendations for the Next Work Package

1. **Continue treating "verify, remediate, re-verify" as a legitimate,
   repeatable release-closing pattern** when a first pass finds a
   genuine, actionable gap — not only for Platform Service governance,
   but for any future disclosed finding a dedicated Work Package could
   close before the release is finalised.
2. **Build `FCR-0005`** — see Repository Maturity, above; this pass adds
   a second, independent data point to `WP 9.8B`'s own.
3. **Reconstruct or formally retire the "32 governance documents"
   figure** — the one standing recommendation from the first pass still
   fully open.
4. **Consider whether `TD-34`'s own revisit triggers are worth acting
   on now rather than deferring further** — a low-cost fix (serialising
   `CompositeLogSinkTests` against the `Console`-redirecting collection)
   would retire a flake this project has now formally tracked for the
   first time, closing it before it recurs and gets re-disclosed a
   second time.

## Key Takeaways

1. A second, independent verification pass can find a release in a
   materially more consistent state than the first pass left it — proof
   that closing a disclosed recommendation, rather than merely
   escalating it again, is achievable within the same release cycle
   that found it.
2. "Zero instances observed across N prior reviews" is evidence of low
   frequency, not evidence of absence — a long-narratively-disclosed
   characteristic deserves a tracked entry once real evidence exists,
   not indefinite re-disclosure in prose.
3. Re-verifying another Work Package's own claimed resolution
   independently, rather than trusting it, is a distinct and valuable
   activity even when the claim turns out to be correct — the
   confirmation itself has value, not only the original fix.
4. This project's own governance-completeness gaps keep following the
   same shape: real, disclosed, correctly out-of-scope for the Work
   Package that finds them, and requiring either a dedicated Work
   Package or automated tooling to actually close — `FCR-0005` remains
   the structural answer to that repeated shape.

## Related Documents

`docs/academy/03 Work Packages/WP9.9.0-release-preparation-and-product-baseline.md`
(first pass); `docs/academy/03 Work Packages/WP9.8B-platform-service-register-reconciliation.md`;
`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report (Second
Pass).md`; `docs/releases/v0.9.0/WP9.9.0 Product Approval Report
(Second Pass).md`; `docs/governance/Quality/Technical Debt
Register.md` (`TD-34`); `docs/governance/Future Capability
Register.md` (`FCR-0005`).
