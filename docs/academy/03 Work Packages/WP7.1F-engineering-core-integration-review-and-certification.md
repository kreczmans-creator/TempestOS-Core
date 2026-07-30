# WP 7.1F — Engineering Core Integration Review & Certification

## What This Document Is

`WP 7.1F` is a closing engineering certification, not a feature Work
Package — it produced no production code and introduced no new
capability. This retrospective is deliberately shaped around what was
verified, what was found, architectural and process lessons, repository
maturity, and recommendations for the next Work Package, mirroring
`WP 6.8`'s own precedent for a whole-programme verification pass rather
than the standard 13-section per-feature template a single framework's
own implementation retrospective uses.

## 1. Introduction

The Engineering Foundation programme (`v0.7.0`, `WP 7.0A`–`WP 7.1E`) is
TempestOS's second multi-Work-Package phase to reach a closing
certification review, following `v0.6.0`'s own `WP 6.8`. `WP 7.1F`
closes it: a full, evidence-based audit of everything the preceding
eight Work Packages shipped, following the identical discipline
`WP 4.2D`, `WP 5.0S`/`WP 5.4`, and `WP 6.8` each established for their own
release phases — confirm, don't assume; verify, don't trust a prior
claim; disclose, don't hide.

## 2. What Was Achieved

- **All five Engineering Foundation frameworks — Engineering Data
  Model, Units & Quantities, Materials, Calculation, Verification — were
  independently re-verified**, not merely re-read from a prior
  retrospective's own claim.
- **Three governance registers, stale since `WP 6.8` itself, were fully
  backfilled a second time.** `Interface Register.md`, `Dependency
  Injection Register.md`, and `Module Register.md` had each gone five
  Work Packages without an update — 11 interfaces, 4 registrations, and
  4 sample modules, all real, shipped, and tested, none recorded. This
  Work Package performed the full correction, closing the gap completely
  rather than leaving it as a growing caveat, and raised `FCR-0005`'s own
  priority from Medium to High as a direct consequence.
- **A genuine, previously-undisclosed Academy gap was found and closed.**
  `WP7.0C Academy Plan.md` named the Engineering Data Model's own concept
  guide as this programme's "highest-priority new Academy content" — it
  was never written by `WP 7.1A`, and the omission was never disclosed by
  any of the four Work Packages that subsequently built on the framework
  it would have explained. Written here (`02 Runtime Architecture/
  15-engineering-data-model.md`).
- **A cross-framework security observation was surfaced that neither
  individual Security Review could produce alone** — `TD-18`'s own
  quietly increased relevance now that `LinkAsync` is load-bearing for
  four real consumers, not the single consumer it had when first
  disclosed by `WP 7.1A`.
- **Ten completion deliverables were produced** — a Certification
  Report, an Architecture Conformance Report, a Consumption Matrix, a
  Definition of Done Audit, a Security Review Summary, a Technical Debt
  Disposition, a Future Capability Register Review, an Executive
  Summary, this Retrospective, and `ENGINEERING_CORE_COMPLETION_REPORT.md`
  — the programme's own permanent historical milestone document.

## 3. Architectural Lessons

**A dependency graph proposed before implementation (`WP7.0C
Cross-Framework Dependency Report.md`) held completely true once real
code existed to check it against.** Every proposed edge (Materials →
Data Model, Materials → Units & Quantities, Verification → Data Model)
matched the real, compiled `using` graph exactly; the one proposed "by
convention, not a hard constraint" relationship (Calculation → Units &
Quantities) is now proven, not merely proposed — no compile-time
dependency exists at all. This is a genuinely reusable confirmation:
contract-review-stage dependency analysis, done carefully, survives
contact with real implementation across five separate Work Packages
without correction.

**Reviewing five frameworks together surfaces findings a single
framework's own review structurally cannot.** Two independent
frameworks (`Calculations`, `Verification`) each, separately, chose not
to validate a material reference — reviewed in isolation, each looks
like an ordinary scope decision; reviewed together, the repetition
becomes corroborating evidence the boundary is principled. The identical
vantage point also surfaced `TD-18`'s own increased relevance as a fourth
consumer of `LinkAsync` emerged — visible only once all five frameworks'
own dependencies on `EngineeringData` are considered together.

## 4. Implementation Lessons

**A designated closing-review Work Package genuinely needs to be
scheduled, or the gap it exists to close keeps growing.** `WP 6.8` built
the exact tooling recommendation (`FCR-0005`) meant to prevent this
class of drift from recurring undetected; it recurred anyway, because no
closing review of the Engineering Foundation phase existed until this
Work Package's own explicit authorization. The lesson generalises
beyond this one register: "defer to the closing Work Package" is only a
safe strategy if a closing Work Package is actually scheduled, not
merely theoretically available as a governance pattern.

**A concept guide named in a plan is not evidence a concept guide
exists.** `WP7.0C Academy Plan.md`'s own explicit "highest-priority"
framing did not, by itself, cause the guide to be written — only a
closing review that checked the file system directly, rather than
trusting `WP 7.1A`'s own retrospective, caught the four-Work-Package-old
gap.

## 5. Repository Maturity

The Engineering Core now stands at 5 frameworks, 11 public interfaces,
4 DI registrations, 4 production sample modules, 8 tracked Technical
Debt items (`TD-17`–`TD-24`), 4 disclosed trade-offs (`AT-14`–`AT-17`), 5
ADRs (`ADR-0053`–`ADR-0057`), 28 Engineering Principles, and 99 Academy
files total (repository-wide) — every one of these figures re-derived
directly against the file system during this Work Package's own review,
not carried forward from any prior register's own arithmetic. This is
the fifth consecutive release-or-programme-scale review (`WP 4.2D`,
`WP 5.0S`/`WP 5.4`, `WP 6.8`, now `WP 7.1F`) to find genuine, disclosed
governance or documentation drift during its own closing pass — a
pattern consistent enough across four separate programmes that it is now
better understood as an expected, structural cost of multi-Work-Package
delivery than a surprising anomaly each time it recurs.

## 6. Recommendations for the Next Work Package

- **Build `FCR-0005`** before the next multi-Work-Package programme
  begins — three recurrences of the identical drift pattern across three
  separate release phases is sufficient evidence to justify the tooling
  investment.
- **Product Approval should choose among three genuinely open paths**,
  none technically blocked: a real, discipline-specific Engineering
  Module; a Platform Hardening candidate (`A`–`C`); or design work toward
  `FCR-0027` (Requirements Engine). This Work Package does not recommend
  one over the others.
- **Reassess `TD-18` alongside `FCR-0036`** when either is next revisited
  — both concern the same underlying `LinkAsync` call pattern, and
  tracking them together avoids re-deriving the same analysis twice.

## Key Takeaways

1. A closing, whole-programme review Work Package is not a formality —
   this one found and fully closed a governance-register gap a prior
   closing review's own tooling recommendation was meant to prevent, and
   a documentation gap four Work Packages old that no individual
   implementation Work Package's own scope was positioned to catch.
2. Cross-framework review produces findings neither corroboration nor
   contradiction can be visible from a single framework's own vantage
   point — two independent, correct decisions converging on the same
   answer, and one debt item's own quietly increasing relevance, both
   required seeing all five frameworks at once.
3. "Certified With Accepted Technical Debt" remains the honest
   certification outcome whenever a programme ships disclosed,
   deliberate limitations — naming the qualification explicitly is
   itself part of the certification's own evidentiary integrity, exactly
   as `WP 6.8` established for `v0.6.0`.

## Related Documents

`WP7.1F Engineering Core Certification Report.md`; `WP7.1F Engineering
Core Architecture Conformance Report.md`; `WP7.1F Engineering Core
Consumption Matrix.md`; `WP7.1F Definition of Done Audit.md`; `WP7.1F
Security Review Summary.md`; `WP7.1F Technical Debt Disposition.md`;
`WP7.1F Future Capability Register Review.md`; `WP7.1F Executive
Summary.md`; `WP7.1F Lessons Learned.md`;
`ENGINEERING_CORE_COMPLETION_REPORT.md`; `docs/governance/Future
Capability Register.md`; `docs/governance/Quality/Technical Debt
Register.md`; `ADR-0023`; `WP6.8-platform-services-integration-review.md`
(the whole-release-review precedent this document follows).
