# WP 7.0A — Roadmap Report

## Status

Complete. Summarises `docs/governance/Product Roadmap.md`'s own
phase-based sequencing for a reader who wants the sequencing rationale
without reading that register's full metadata block.

## Phase Summary

| Phase | Name | Status | Scoped? |
|---|---|---|---|
| 1 | Platform Foundation | Complete (`v0.1.0`–`v0.4.0`) | Shipped |
| 2 | Developer Experience | Complete (`v0.5.0`) | Shipped |
| 3 | Platform Services | Complete (`v0.6.0`, CERTIFIED WITH ACCEPTED TECHNICAL DEBT) | Shipped |
| 4 | Engineering Foundation | Current (`v0.7.0`) | Branch cut; `WP 7.0A` complete; implementation Work Packages not yet scoped |
| 5 | Engineering Modules | Not started | Not scoped — awaits a dedicated capability-identification exercise |
| 6 | Professional Features | Not started | Not scoped |
| 7 | Enterprise Features | Not started | Not scoped |
| 8 | Future Expansion | Not started | Not scoped |

## Why This Sequencing

1. **Platform before product.** Three releases (`v0.3.0`–`v0.6.0`) built
   and proved infrastructure and cross-service platform capability
   before any Engineering Module was designed — deliberately, per
   `FOUNDATION.md`'s own "architecture precedes implementation" rule
   applied at the release-phase level, not only the Work Package level.
2. **Close known platform gaps before building outward.** Phase 4
   (Engineering Foundation) is sequenced to resolve `Future Capability
   Register.md`'s own Platform- and Infrastructure-category gaps
   (`FCR-0001`, `FCR-0003`–`FCR-0006`, among others) before Phase 5
   begins — a platform with a known, disclosed authentication gap is a
   weaker foundation for a first real Engineering Module than one
   without it.
3. **Systems Engineering and Project Management first, once Phase 5
   begins**, because they are the only two Engineering Discipline
   categories with an existing, named platform-level hook (`ADR-0013`'s
   own Future Considerations name a Requirements Engine and a Project
   Engine directly) — not because they are judged more valuable than
   Mechanical, Structural, Electrical, or the other six categories,
   which simply have no identified candidate yet.
4. **Enterprise Features after Engineering Modules, never before.**
   Multi-user isolation, cloud synchronisation, and compliance readiness
   are all high-effort, high-consequence capabilities `Security
   Principles.md` Principle 7 explicitly cautions against building
   ahead of real need — sequenced last because an enterprise feature
   with no real Engineering Module for an enterprise customer to run is
   speculative by definition.

## What This Roadmap Does Not Do

It does not assign a release number to any phase beyond Phase 4, and it
does not scope a single Work Package within Phase 5 onward — each
requires its own Architecture, Planning, and Contract Review, per this
project's standing discipline. See `docs/governance/Product Roadmap.md`
§"Non-Commitments" for the explicit list of what this roadmap
deliberately withholds.

## Related Documents

`docs/governance/Product Roadmap.md` (full detail); `docs/governance/
Future Capability Register.md`; `docs/governance/Capability
Categories.md`; `VISION.md`.
