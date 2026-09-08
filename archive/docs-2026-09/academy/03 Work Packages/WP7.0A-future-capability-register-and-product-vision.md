# WP 7.0A — Future Capability Register & Product Vision

## What This Document Is

This is not a standard 13-section implementation retrospective — `WP
7.0A` shipped no production code, no test, and no ADR. It mirrors `WP
6.8`'s own whole-review shape instead (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways), because this Work Package, like that one, is a
milestone activity rather than a feature implementation — the first
Work Package of the Engineering Foundation phase (`v0.7.0`), and the
first Work Package in this project's history whose deliverable is
product vision and future-roadmap governance rather than platform
capability.

## Introduction

`v0.6.0` shipped `CERTIFIED WITH ACCEPTED TECHNICAL DEBT` and was
released: merged to `main`, tagged, and pushed. Before any `v0.7.0`
implementation Work Package could be scoped, this project needed
something it had never formally produced: a single, permanent statement
of what TempestOS is *for*, and a single, permanent register of every
future capability identified so far — replacing the informal "Car Park"
discussions and scattered Technical Debt Register trade-offs that had,
until now, been the only record of TempestOS's own future.

## What Was Achieved

Four permanent documents: `VISION.md` (repository root), `docs/
governance/Future Capability Register.md` (28 entries, `FCR-0001`–
`FCR-0028`, every one traced to a specific, cited, pre-existing
document), `docs/governance/Capability Categories.md` (15 categories —
6 Platform-adjacent, 9 Engineering Discipline), and `docs/governance/
Product Roadmap.md` (8 phases, only the first four shipped or scoped).
Five completion deliverables under `docs/releases/v0.7.0/`, prefixed
`WP7.0A`: an Architecture Report (confirming no existing architectural
rule was changed or duplicated), a Roadmap Report, a Future Capability
Summary (a full prioritisation pass across all 28 entries), a
Recommended v0.7 Candidate Work Packages document (five capabilities —
`FCR-0001`, `FCR-0003`, `FCR-0004`, `FCR-0005`, `FCR-0006` — grouped
into three candidate Work Packages, none approved), and this
retrospective.

## Architectural Lessons

The Platform-vs-Engineering-Module boundary this Work Package names in
`VISION.md` is not a new architectural rule — it is `ADR-0013`'s
existing platform-service-vs-module classification test, applied one
level up, to whole future capabilities rather than individual services.
`ADR-0013` itself already named a Requirements Engine and a Project
Engine as open examples needing exactly this decision; this Work
Package's own contribution was recognising that these two aspirational
services, `Threat Model.md`'s own assumption 1 (engineering IP: CAD,
requirements, analysis, verification records), and the dormant
`ProjectModel`'s own field names (`Classification`, `SecurityLevel`,
`ExportControlled`) were all pointing at the same underlying product
ambition — never previously stated as one coherent vision because no
Work Package had read all three together before.

## Implementation Lessons

The genuinely hard part of this Work Package was not writing — it was
declining to write. Six of nine Engineering Discipline categories in
`Capability Categories.md` have zero identified capabilities, and it
would have been easy to invent a plausible-sounding candidate for each
(a "Mechanical Analysis Module," say). None was invented. This Work
Package's own instruction to "not invent implementation details" was
interpreted, deliberately, as extending to not inventing *capabilities*
either — every one of the 28 `FCR` entries is sourced from an existing,
cited document (a Technical Debt Register item, a Security Roadmap
item, a `WP6.x Future Capability Recommendations.md` document, an ADR,
or `PROJECT_STATUS.md`'s own Long-Term Vision section), and the register
says so explicitly where a category has nothing to cite.

## Repository Maturity

Every governance convention established across `v0.5.0` and `v0.6.0`
transferred to this new kind of register without modification: permanent
non-reused identifiers (`FCR-NNNN`, mirroring `ADR`/`RD` numbering),
explicit Coverage Status disclosure rather than false completeness, a
Cross-Reference Check verifying every `TD`/`AT`/`ADR`/`FCR` citation
against its source, and Verified/Inferred/Unknown marking on uncertain
claims (`FCR-0026` is marked Inferred, not Verified, since no Work
Package has confirmed defence-sector operation as a current, active
objective — only that dormant, bootstrap-era code once modelled toward
it). `docs/governance/Governance Index.md` gained a new "Product &
Roadmap" category for the three new registers; `Documentation
Register.md`'s own Directory Map and root-document row were updated to
include `VISION.md` and the new registers, consistent with (not a
repeat of) that register's own disclosed, still-partial staleness from
the `v0.6.0` Release Engineering pass.

## Recommendations

- **Identify real Engineering Discipline capabilities via a dedicated
  exercise engaging real domain stakeholders** — not further mining of
  this repository's own existing text, which would not produce genuine,
  non-invented candidates for the six currently-empty categories.
- **Review `Future Capability Register.md`'s own Coverage Status at
  every future release boundary**, so this new register does not
  itself become the next one to go stale for several Work Packages
  before a closing review catches it — the exact pattern `FCR-0005`
  exists to prevent.
- **Classify `FCR-0027` (Requirements Engine) and `FCR-0028` (Project
  Engine) under `ADR-0013` explicitly**, the first time either is
  seriously proposed for design.

## Key Takeaways

1. A product vision document benefits from the same evidentiary
   discipline as an ADR — every claim in `VISION.md` is either cited to
   an existing document or stated explicitly as an ambition, never
   blurred, so it can be checked the same way an architectural decision
   can.
2. A future-capability register's honesty is measured by what it
   refuses to invent, not by how complete it appears — disclosing six
   empty categories is more valuable than quietly filling them.
3. This project's governance model, built to track what exists,
   generalised cleanly to tracking what might exist next, without
   needing new conventions invented for the purpose — itself evidence
   the original model was well-designed, not merely well-suited to its
   original purpose.

## Related Documents

`VISION.md`; `docs/governance/Future Capability Register.md`;
`docs/governance/Capability Categories.md`; `docs/governance/Product
Roadmap.md`; `docs/releases/v0.7.0/WP7.0A Architecture Report.md` and
its four companion deliverables; `WP6.8-platform-services-integration-
review.md` (the whole-review format this retrospective mirrors).
