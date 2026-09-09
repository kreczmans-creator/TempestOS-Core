# WP 7.2A — Recommended Programme

## Purpose

States the one programme recommended to become TempestOS's next
implementation phase, supported by the evidence gathered in
`WP7.2A Strategic Roadmap Review.md`, `WP7.2A Programme Comparison
Matrix.md`, and `WP7.2A Capability Dependency Report.md`. This document
recommends; it does not approve — implementation begins only once
Engineering Review and Product Approval both occur, per this Work
Package's own explicit closing instruction.

## Recommendation

# Programme A — Requirements & Verification Platform

Score 46/55, the highest of all seven candidates (`WP7.2A Programme
Comparison Matrix.md`), and the only programme with both a completed
technical foundation (`FCR-0029`, `FCR-0033`, both Implemented and
certified) and a named platform-level hook predating this Work Package
by five releases (`ADR-0013`'s own Future Considerations, `WP 2.7`,
2026-07-22).

## Why Not Programme F (Platform Hardening) — the Only Other Serious Candidate

Programme F scored second-highest (36/55) and is not a weak candidate —
it is the only other programme this review found real, actionable
evidence for. It was not recommended, for three specific, evidence-based
reasons:

1. **Every named trigger for Programme F's own three components remains
   unfired.** `FCR-0001`'s trigger is a real third-party plugin —
   `AT-06` confirms `src/Plugins/` remains empty. `FCR-0003`/`FCR-0004`'s
   trigger is a concrete deployment scenario beyond a trusted local
   network — none exists. Building any of the three now would itself
   violate `Security Principles.md` Principle 7, the same "do not build
   ahead of demonstrated need" discipline this project applies
   everywhere else. Recommending Programme F now would be recommending
   exactly the anti-pattern this review declines to apply to Programmes
   B–E for an identical reason (no real, demonstrated need).
2. **Programme F has zero Engineering Core leverage and zero engineering
   value** (`WP7.2A Programme Comparison Matrix.md`) — it does not
   advance `VISION.md`'s own stated reason TempestOS exists. Two
   consecutive certifications (Platform Core, Engineering Core) have now
   built substantial engineering-domain infrastructure with no
   engineering-domain product to show for it yet; Programme F would
   extend that gap by one more phase, not close it.
3. **Programme F remains available at essentially the same cost whenever
   its own trigger fires** — the enforcement mechanism already exists
   (`ADR-0044`), and `WP7.2A Candidate Work Package Catalogue.md`
   recommends it explicitly as `v0.9`'s own likely scope, not
   abandoned, only sequenced second.

**This is a real, disclosed trade-off, not a dismissal.** `WP7.2A Risk
Assessment.md` names the residual risk directly: `FCR-0001`/`FCR-0003`/
`FCR-0004` remain open for at least one further release under this
recommendation, and `VISION.md`'s own Long-Term Objective 2 (read
literally) would have preferred them resolved first. This review's own
judgement, stated plainly: that objective's own literal reading is in
tension with Security Principle 7 applied to the identical facts, and
this review resolves the tension in Principle 7's favour, consistent
with `WP 7.0B`'s own actual, already-certified precedent of proceeding
past that same "readiness" language once before.

## Why Not Programme G, or Programmes B–E

Programme G (19/55) has no real design gap to close — `FCR-0024`'s own
register entry states the capability already works structurally.
Programmes B, C, D, and E (14/55 each) have zero identified capability
in `Future Capability Register.md`, and `WP7.0B Engineering Discipline
Assessment.md`'s own finding — re-confirmed unchanged by this review —
is that no sequencing recommendation among them is possible without
inventing content this repository has no evidence for. Recommending any
one of the four would be exactly the "speculative, non-evidence-based
claim" `Future Work Package Guidelines.md` §8 forbids.

## Engineering Workflow vs. Engineering Disciplines

**Recommendation: TempestOS should next focus on Engineering Workflow,
not Engineering Disciplines.** Programme A — Requirements & Verification
— is a workflow capability (how engineering claims are made,
demonstrated, and traced), not a discipline capability (a Mechanical,
Structural, or Electrical calculation). The evidence supporting this
split:

- **Every one of the five discipline categories with zero identified
  capability (Mechanical, Structural, Electrical, HVAC, Manufacturing)
  requires a real engineering-domain stakeholder to identify a first
  capability** — this repository's own, repeated, unchanged finding
  since `WP 7.0B`. No amount of further documentation review changes
  this; only a real stakeholder engagement or customer scenario does.
- **Workflow capability (Requirements, Verification, and — per the
  Engineering Core's own design — eventually Traceability reporting)
  is discipline-agnostic** — every future discipline module, whichever
  is identified first, will want to trace its own artefacts to a
  requirement and a verification record. Building the workflow layer
  now means the first real discipline module, whenever it arrives, has
  a traceability mechanism to plug into immediately, rather than each
  discipline module inventing its own.
- **This mirrors the Engineering Foundation programme's own proven
  design discipline exactly**: `Verification`, `Materials`, and
  `Calculation` each reused the Engineering Data Model rather than
  inventing their own storage shape. A Requirements Engine reusing
  `IVerificationService` is the identical pattern, one layer up.

Building a discipline module before the workflow layer exists would
also repeat `WP7.0B Roadmap Risk Register.md`'s own disclosed risk
(`RR-1`): the Engineering Foundation itself was "designed from only two
disciplines' own aspirational descriptions... not validated against a
real Mechanical, Structural, Electrical, HVAC, or Manufacturing
requirement." Compounding that risk by building a *discipline* module on
top of an *unvalidated* foundation, before the workflow layer that would
let it trace its own claims exists, would be building on two open
questions simultaneously rather than closing one first.

## Release Planning

Per `Product Roadmap.md`'s own "Non-Commitments" discipline, no release
number below is a commitment — each still requires its own Architecture,
Planning, and Contract Review phase before any Work Package is approved.
This is sequencing guidance only.

### `v0.8.0` — Systems Engineering Foundation (Recommended)

Programme A's own architecture, planning, contract review, and
implementation — mirroring the exact `v0.7.0` pattern (`WP 8.0A`/
`WP 8.0B`/`WP 8.0C` planning, `WP 8.1x` implementation). `ADR-0013`'s own
classification question (platform service vs. module) is this
programme's own first required decision, exactly as `FCR-0027`'s own
register entry already names it as open. **Rationale:** the only
programme with a completed technical foundation and no unfired-trigger
objection; realises `VISION.md`'s own stated reason TempestOS exists,
two full releases after that vision was first stated.

### `v0.9.0` — Platform Hardening (Recommended, Second)

Programme F, if its own triggers still have not fired by the time
`v0.8.0` completes — or brought forward ahead of `v0.8.0` only if a
real third-party plugin author or a concrete networked deployment
scenario materialises first, per each item's own named trigger.
**Rationale:** the second-highest-scoring programme, cheapest to
execute (mechanism already exists), and the natural point to close
`FCR-0001`/`FCR-0003`/`FCR-0004` before a Systems Engineering capability
that has, by then, a real user base of its own might need them.

### `v1.0.0` — First Externally-Usable Engineering Milestone (Provisional)

Not a specific Work Package — a milestone marker for whenever a real
individual engineer or small professional practice (`VISION.md`'s own
named "once Engineering Modules ship" target user) could plausibly run
real Systems Engineering work end-to-end: a requirement recorded,
verified, and reported on, through the real, unmodified Host. This
review does not recommend a specific release number's worth of scope for
`v1.0` — consistent with `Product Roadmap.md`'s own discipline of not
committing release numbers beyond an approved phase — only that reaching
this milestone, not a specific feature count, is the meaningful marker
for it.

## Confidence and Caveats

This recommendation is **evidence-backed, not certain** — `WP7.0B
Roadmap Risk Register.md`'s own `RR-1` (the Engineering Foundation's own
design was validated against only two disciplines) applies with equal
force to Programme A itself, which is one of those same two disciplines.
`WP7.2A Risk Assessment.md` carries this forward as the single largest
residual risk this recommendation accepts, not resolves.

## Related Documents

`WP7.2A Strategic Roadmap Review.md`; `WP7.2A Programme Comparison
Matrix.md`; `WP7.2A Capability Dependency Report.md`; `WP7.2A Candidate
Work Package Catalogue.md`; `WP7.2A Risk Assessment.md` (folded into
`WP7.2A Executive Summary.md` and `Lessons Learned.md` per this Work
Package's own deliverable list); `docs/governance/Product Roadmap.md`;
`VISION.md`; `ADR-0013`; `docs/governance/Future Capability Register.md`
(`FCR-0027`).
