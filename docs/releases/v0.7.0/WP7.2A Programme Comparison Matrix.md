# WP 7.2A — Programme Comparison Matrix

## Purpose

Scores all seven candidate programmes named in this Work Package's own
controlling instruction against the eleven required evaluation criteria,
using one consistent framework, so the resulting recommendation is
traceable to a scored comparison rather than a narrative preference.

## Scoring Methodology

Each criterion is scored 1 (weakest) to 5 (strongest) **in the direction
of "better for TempestOS to pursue this programme next,"** so a higher
score is always better regardless of what the criterion measures — for
**Technical risk** and **Security impact**, a high score means *low*
risk/impact (safer to build), not a high amount of either. Every score
is backed by a cited fact from `WP7.2A Strategic Roadmap Review.md` or a
directly-checked repository document, not asserted without evidence.
Maximum possible total: 55.

## Matrix

| Criterion | A — Requirements & Verification | B — Mechanical | C — Building Services/HVAC | D — Structural | E — Electrical | F — Platform Hardening | G — AI & Engineering Intelligence |
|---|---|---|---|---|---|---|---|
| Strategic value | 5 | 1 | 1 | 1 | 1 | 3 | 1 |
| Commercial value | 3 | 1 | 1 | 1 | 1 | 3 | 1 |
| Engineering value | 5 | 1 | 1 | 1 | 1 | 1 | 1 |
| Platform leverage | 4 | 1 | 1 | 1 | 1 | 5 | 2 |
| Engineering Core leverage | 5 | 2 | 2 | 2 | 2 | 1 | 1 |
| Future capability unlocked | 5 | 1 | 1 | 1 | 1 | 3 | 1 |
| Technical risk (5 = lowest risk) | 3 | 1 | 1 | 1 | 1 | 5 | 3 |
| Security impact (5 = most positive/least new exposure) | 4 | 3 | 3 | 3 | 3 | 5 | 3 |
| Academy impact | 5 | 1 | 1 | 1 | 1 | 2 | 2 |
| Estimated implementation effort (5 = lowest effort) | 3 | 1 | 1 | 1 | 1 | 4 | 1 |
| Long-term maintainability | 4 | 1 | 1 | 1 | 1 | 4 | 3 |
| **Total (of 55)** | **46** | **14** | **14** | **14** | **14** | **36** | **19** |

## Rationale Per Programme

### Programme A — Requirements & Verification Platform

Highest total. **Strategic value (5):** the only Engineering Discipline
category with a named platform-level hook (`ADR-0013`'s own Future
Considerations), directly named in `VISION.md`'s Long-Term Objective 3.
**Engineering value (5):** the first genuinely domain-facing capability
this platform would ship — realising `VISION.md`'s own stated reason for
existing, not further infrastructure. **Engineering Core leverage (5):**
directly consumes `IVerificationService` and `IEngineeringDocumentStore`
— `WP7.1E Future Capability Recommendations.md` Recommendation 1 names
this integration directly. **Future capability unlocked (5):** gives
Verification its first real consumer and establishes the traceability
backbone every future discipline module (once identified) would want.
**Technical risk (3, not 5):** `WP7.0B Candidate Work Package
Catalogue.md` itself discloses this as "the least architecturally
grounded candidate in this catalogue" — a real, honest unknown, not a
reason to score it lower than its evidence otherwise supports. **Security
impact (4, not 5):** introduces a new asset class (requirements/
traceability data) needing its own threat-model addendum, but no new
trust boundary — still trusted, first-party, in-process code.
**Estimated implementation effort (3):** genuinely unscoped at this
stage; Medium-High is the honest estimate, not Low.

### Programme F — Platform Hardening

Second-highest total, the only other programme scoring above 20.
**Platform leverage (5)** and **Technical risk (5):** the enforcement
mechanism (`IPermissionEvaluator`, `ADR-0044`) already exists — this
programme applies it, it does not build it, the lowest-risk, best-
understood work of any candidate. **Security impact (5):** the only
programme that actively closes existing, disclosed security debt
(`TD-09`, `TD-10`, `TD-11`, `TD-13`, `TD-14`) rather than adding a new
surface. **Engineering value (1):** zero net-new engineering-domain
capability — this is infrastructure completion, not the mission
`VISION.md` states TempestOS exists for. **Engineering Core leverage
(1):** no relationship to any Engineering Foundation framework
whatsoever. **Academy impact (2):** would update existing architecture
documents, not produce the genuinely new kind of content a first
Engineering Module would.

### Programme G — AI & Engineering Intelligence

**Every criterion capped by the same fact:** `Future Capability
Register.md`'s own Coverage Note states `FCR-0024` "describ[es] an
already-supported extension point rather than a gap" — `ICommandRegistry`
already lets a future AI/automation caller enumerate and invoke by Id,
filtered by permission. There is no real design gap to close. Scoring
this programme "next" would mean scoping a Work Package with no known
consumer and no known requirement to satisfy — the same evidence-free
pattern this review declines to apply to Programmes B–E, applied
consistently here too.

### Programmes B, C, D, E — Mechanical, Building Services/HVAC, Structural, Electrical

**Identical, near-floor scores across all four**, because the underlying
fact is identical across all four: `WP7.0B Engineering Discipline
Assessment.md` found each has **zero** identified capabilities in
`Future Capability Register.md`, and concluded directly — re-confirmed
unchanged by this review — that "no sequencing recommendation among
these five is possible today... the correct next step is a real
engineering-domain stakeholder engagement or a concrete customer
scenario naming one of them first, not a documentation-derived guess
dressed up as a recommendation." Every criterion for these four is
scored against that same finding: **Engineering value (1)** because no
defined capability exists to evaluate; **Technical risk (1)** because
building without a real requirement is speculative engineering, the
exact pattern `Future Work Package Guidelines.md` §8 and `Security
Principles.md` Principle 7 both warn against; **Long-term maintainability
(1)** because a module built on invented requirements risks the rework
`WP7.0B Roadmap Risk Register.md`'s own `RR-1` names as a real,
disclosed risk even for the Engineering Foundation frameworks that *do*
have real evidence behind them. **Engineering Core leverage (2, not 1)**
is the one criterion scored slightly above floor for all four, since
each would plausibly consume Units & Quantities, Materials, and
Calculation once a real capability is eventually identified — a
structural, not evidentiary, distinction.

**Manufacturing is not separately listed** — it is not among this Work
Package's own seven named candidate programmes, and `WP7.0B Engineering
Discipline Assessment.md`'s own "partial exception" note (its structural
dependency on `FCR-0031`, Materials) does not change its own identical
zero-capability starting point.

## Sensitivity Check

Even under the most generous plausible re-scoring of Programme F
(maximum 5 on every criterion this review scored below 5 — Strategic
value, Commercial value, Engineering value, Future capability unlocked,
Academy impact — a re-scoring this review does not consider justified
by the evidence), its total rises to 51, still below Programme A's 46
only if every one of Programme A's own sub-maximum scores is
simultaneously read at its most pessimistic. Under any evidence-consistent
scoring, **Programme A and Programme F remain the only two candidates in
contention**; no plausible re-scoring brings any of Programmes B, C, D,
E, or G within range of either.

## Related Documents

`WP7.2A Strategic Roadmap Review.md`; `WP7.2A Capability Dependency
Report.md`; `WP7.2A Recommended Programme.md`; `docs/governance/Future
Capability Register.md`; `WP7.0B Engineering Discipline Assessment.md`;
`WP7.0B Candidate Work Package Catalogue.md`; `WP7.0B Roadmap Risk
Register.md`.
