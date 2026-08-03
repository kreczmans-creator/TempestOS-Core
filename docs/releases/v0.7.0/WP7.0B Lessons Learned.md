# WP 7.0B — Lessons Learned

## Status

Complete.

## 1. "Foundation before discipline" is a defensible architectural inference, distinct from inventing a capability

The hardest line to walk in this Work Package was between two things
that look similar but are not: inferring that a cross-cutting technical
substrate (a data model, a units system, a calculation engine) is
architecturally necessary before any discipline module can exist, versus
inventing what a discipline module itself would do. This Work Package
did the first and explicitly declined the second — `FCR-0029`–`FCR-0033`
describe *infrastructure a Mechanical Engineering module would need*,
never *what a Mechanical Engineering module would calculate*. Whether
this line was drawn in exactly the right place is itself a risk this
Work Package disclosed, not resolved (`RR-1`, `WP7.0B Roadmap Risk
Register.md`) — it will only be confirmed once a real second discipline
validates the foundation `WP 7.0A`'s own two named disciplines alone
could not.

## 2. A dependency graph revealed a real, previously-unnoticed asymmetry

Before this Work Package, `FCR-0027` (Requirements Engine) and a
verification concept were not clearly distinguished — `WP 7.0A`'s own
description of `FCR-0027` folded "verification and validation" into
Requirements generally. Building the dependency graph forced an explicit
question: does Verification depend on Requirements, or the reverse, or
neither? The answer — Verification depends on Requirements one-way, not
mutually — was not obvious until the graph was actually drawn, and
produced `FCR-0033` as its own, separate entry rather than leaving it
implicit inside `FCR-0027`.

## 3. Recommending release numbers without contradicting a prior Work Package's own non-commitment was a genuine tension

`Product Roadmap.md` (`WP 7.0A`) deliberately declined to assign release
numbers beyond Phase 4, for good reason (avoiding premature commitment).
This Work Package's own controlling instruction explicitly asked for a
`v0.7.x`–`v1.0` breakdown. Resolving this required treating the new
document as a genuinely separate, equally non-binding companion — not
an amendment overriding the earlier document's own stance — a
distinction worth naming explicitly so a future reader does not read
the existence of `WP7.0B Recommended Release Roadmap.md` as `Product
Roadmap.md` having quietly changed its mind.

## 4. Five disciplines with zero capabilities is not a problem this Work Package could solve by trying harder

`WP7.0B Engineering Discipline Assessment.md`'s own honest conclusion —
that Mechanical, Structural, Electrical, Building Services/HVAC, and
Manufacturing cannot be sequenced against each other from existing
evidence — was reached only after seriously attempting several
plausible-sounding justifications (alphabetical order, apparent
technical foundational-ness, apparent market size) and rejecting each as
unevidenced speculation, not because none occurred. Recording that this
was attempted and explicitly rejected, not simply skipped, is itself the
finding worth preserving.

## Recommendations

- **The next Work Package should not be a third consecutive
  planning-only pass** (`GR-2`, `WP7.0B Roadmap Risk Register.md`) —
  recommend that Engineering Review of `WP 7.0B` be followed by an
  actual Architecture/Planning/Contract Review phase that produces an
  approved implementation scope, not another architecture-only Work
  Package.
- **Validate the Engineering Foundation Programme against a second real
  discipline before treating it as stable** (`RR-1`) — the single
  highest-uncertainty output of this Work Package.
- **Extend `FCR-0005`'s own scope explicitly to cover `Future Capability
  Register.md` itself** (`GR-1`) — this register has already grown once
  since its own establishment, and will grow again.

## Related Documents

`WP7.0B Capability Dependency Report.md`; `WP7.0B Engineering Foundation
Architecture.md`; `WP7.0B Engineering Discipline Assessment.md`;
`WP7.0B Roadmap Risk Register.md`; `WP7.0A Lessons Learned.md` (the
precedent this document follows).
