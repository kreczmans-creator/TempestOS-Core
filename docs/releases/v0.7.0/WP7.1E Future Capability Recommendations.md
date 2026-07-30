# WP 7.1E — Verification Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
this Work Package's own implementation and Security Review found —
mirroring `WP7.1A`/`WP7.1B`/`WP7.1C`/`WP7.1D Future Capability
Recommendations.md`'s own format. As the final Engineering Foundation
Work Package, this document also names the programme-level choice
Product Approval now faces.

## Recommendation 1 — A Future Requirements Engine Should Consume `IVerificationService` Directly, Never Duplicate Verification Recording

**What.** When `FCR-0027` (Requirements Engine) is eventually designed,
recording that a requirement has been verified should call
`IVerificationService.RecordAsync` against the requirement's own
document Id directly, rather than inventing a parallel "requirement
verification status" field or mechanism.

**Why this matters.** This Work Package's own implementation proves the
full round trip works correctly against any real `IEngineeringDocument`,
regardless of its own `Kind` — a future Requirements Engine can rely on
it directly.

## Recommendation 2 — `FCR-0036` (Transactional Multi-Document Operations) Should Be Resolved Against a Real, Demonstrated Multi-Consumer Need, Not Verification Alone

**What.** A future Work Package resolving `FCR-0036` should confirm at
least one other consumer (a future Requirements Engine, a future
Materials multi-link need) genuinely needs atomic multi-document writes
before designing the mechanism, rather than building it for
Verification's own `TD-23` in isolation.

**Why not build it now.** No real, demonstrated failure from `TD-23`'s
own non-transactional sequence has occurred; building transactional
infrastructure speculatively, for one consumer's own disclosed but
unrealised risk, would be premature.

## Recommendation 3 — Any Future Consumer Needing Bounded `VerificationContext` Recording Should Layer Its Own Limit, Not Request a Framework Change

**What.** If a future consumer needs to cap how many criteria or
evidence entries a single verification may record (e.g., to bound
storage or display), it should enforce that limit itself before calling
`RecordAsync`, rather than asking `VerificationContext` to enforce one
generically.

**Why not build it now.** No current consumer has this need (`TD-24`);
a generic limit chosen without a real requirement would likely be
wrong for at least one future consumer.

## Recommendation 4 (Programme-Level) — Product Approval Should Choose Among Three Genuinely Open Paths Now That the Engineering Foundation Is Complete

**What.** With all five Engineering Foundation frameworks now
Implemented, the next Work Package could reasonably be: (a) a real,
discipline-specific Engineering Module, proving the five foundation
frameworks compose correctly for an actual domain problem; (b) a
Platform Hardening candidate (`A`–`C`); or (c) design work toward
`FCR-0027` (Requirements Engine), Verification's own most natural next
consumer.

**Why this matters.** Every prior Work Package's own "Next Planned Work
Package" recommendation had a clear technical dependency to point to
(Candidate `F` needed `FCR-0030`; Candidate `G` needed `FCR-0029`/
`FCR-0030`). With the programme complete, no remaining candidate has an
unmet technical dependency — the choice is now genuinely a product
decision, not an engineering one, and this Work Package does not
recommend one path over the others.

## Not Recommended

- **Adding a hard dependency on `Tempest.Core.Materials` to validate
  material references.** `AT-17` already covers this — an open string
  reference is sufficient absent a real, demonstrated need.
- **Building transactional multi-document operations for Verification
  alone**, ahead of a second, independent consumer's own demonstrated
  need — see Recommendation 2.

## Related Documents

`WP7.1E Implementation Report.md`; `ADR-0057`; `docs/releases/v0.7.0/
WP7.0C Engineering Foundation Contracts.md`; `docs/governance/Quality/
Technical Debt Register.md` (`TD-23`, `TD-24`, `AT-17`);
`docs/governance/Future Capability Register.md` (`FCR-0036`).
