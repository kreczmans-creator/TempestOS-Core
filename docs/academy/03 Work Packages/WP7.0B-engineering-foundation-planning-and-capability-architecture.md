# WP 7.0B — Engineering Foundation Planning & Capability Architecture

## What This Document Is

Like `WP 7.0A` and `WP 6.8` before it, this is not a standard 13-section
implementation retrospective — `WP 7.0B` shipped no production code, no
test, and no new public interface. It mirrors the same whole-review
shape (What Was Achieved, Architectural Lessons, Implementation Lessons,
Repository Maturity, Recommendations, Key Takeaways), because this Work
Package, like those two, is a planning milestone rather than a feature
implementation.

## Introduction

`WP 7.0A` established `Future Capability Register.md` (28 entries) and
`VISION.md` as this project's authoritative future-planning artefacts,
approved by Engineering Review. `WP 7.0B`'s own controlling instruction
asked a harder question than `WP 7.0A` had to answer: not merely *what*
future capabilities exist, but *how do they relate to each other, what
minimum foundation do they share, and in what order should any of it be
attempted* — transforming a register into a coherent engineering
programme.

## What Was Achieved

Five new `Future Capability Register.md` entries (`FCR-0029`–`FCR-0033`,
the Engineering Foundation Programme), each marked Inferred and each
justified by explicit architectural-necessity reasoning rather than a
sourced prior document — a new register-maintenance pattern this Work
Package introduced and disclosed as such. Eight completion deliverables
under `docs/releases/v0.7.0/`, prefixed `WP7.0B`: a Capability Dependency
Report (a full 8-dimension assessment of all 33 register entries, plus a
dependency graph and six-programme grouping), an Engineering Foundation
Architecture document (justifying each of the five new entries
individually), an Engineering Discipline Assessment (honestly concluding
that five of nine Engineering Discipline categories cannot be sequenced
against each other from existing evidence), a Recommended Release
Roadmap (`v0.7.x`–`v1.0`, explicitly non-binding), a Candidate Work
Package Catalogue (ten candidates, `A`–`J`, none approved), a Platform
Consumption Matrix (demonstrating every one of the eleven `v0.6.0`
Platform Services is a plausible consumer of at least one candidate), a
Roadmap Risk Register (eleven risks across four categories), and this
retrospective.

## Architectural Lessons

Building the dependency graph surfaced a genuine, previously-implicit
asymmetry between `FCR-0027` (Requirements Engine) and what became
`FCR-0033` (Verification & Validation Framework) — `WP 7.0A`'s own
description of Requirements had folded verification into it without
examining whether the relationship was mutual or one-directional. It is
one-directional: Verification requires Requirements to exist first;
Requirements only benefits from, never strictly requires, Verification.
This is the kind of finding a dependency graph exercise produces that a
flat list does not — the graph itself was the analytical tool, not
merely a presentation format for conclusions already reached another
way.

## Implementation Lessons

There is no implementation to report — by design. The closest analogue
is the discipline required to keep the five new Engineering Foundation
entries at the level of "shared technical substrate" (a data model, a
units system) rather than drifting into "what a Mechanical Engineering
capability would actually calculate," which would have repeated the
exact invention `WP 7.0A` declined. This required deliberately stopping
short of a natural-feeling next step (naming an example calculation, an
example material property) more than once during drafting.

## Repository Maturity

`Future Capability Register.md`'s own governing rules (permanent
identifiers, explicit Coverage Note disclosure, Cross-Reference Check)
were exercised for the first time by a *second* Work Package extending a
register a *different* Work Package established — proving the pattern
generalises across Work Package boundaries, not only within the
Work Package that invented it. This Work Package's own Roadmap Risk
Register (`GR-1`) explicitly flags that this same register could itself
drift stale exactly as `Governance Register.md` did twice before it, and
recommends `FCR-0005`'s own scope be widened to prevent a third
instance of that pattern — a genuine, disclosed governance risk, not
merely a formality.

## Recommendations

- **Do not run a third consecutive planning-only Work Package.**
  `WP7.0B Roadmap Risk Register.md`'s own `GR-2` names this directly —
  two Work Packages of architecture and planning without any shipped
  capability is a reasonable investment; a third would risk momentum
  and credibility for no additional planning value.
- **Validate the Engineering Foundation Programme against a second real
  discipline** before treating `FCR-0029`/`FCR-0030`/`FCR-0032` as
  stable — the single highest-uncertainty output of this Work Package,
  named explicitly rather than presented with false confidence.
- **Engage real engineering-domain stakeholders** to close the
  five-discipline sequencing gap `WP7.0B Engineering Discipline
  Assessment.md` found impossible to close through documentation review
  alone.

## Key Takeaways

1. A dependency graph is an analytical tool, not just a diagram — it
   found a real asymmetry (`FCR-0027`/`FCR-0033`) that a flat capability
   list had left implicit.
2. Inferring shared technical foundation from a category list is
   architecturally defensible; inferring what a discipline module would
   specifically do is not — the line between them requires deliberate,
   repeated discipline to hold, not a one-time decision.
3. Honestly concluding "this cannot be sequenced from existing
   evidence" is more valuable than producing a plausible-sounding
   sequencing recommendation with no real basis — the same evidentiary
   discipline `VISION.md` and `Future Capability Register.md` already
   established, now proven to hold under a harder question than either
   first faced.

## Related Documents

`docs/governance/Future Capability Register.md`; `docs/governance/
Capability Categories.md`; `docs/releases/v0.7.0/WP7.0B Capability
Dependency Report.md` and its seven companion deliverables;
`WP7.0A-future-capability-register-and-product-vision.md` (the
immediately preceding Work Package this one builds on).
