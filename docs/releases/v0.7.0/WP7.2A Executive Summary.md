# WP 7.2A — Executive Summary

## What This Is

With both the Platform Core (`v0.6.0`) and Engineering Core (`v0.7.0`,
`WP 7.1A`–`WP 7.1F`) certified, TempestOS has zero Engineering Modules
and a genuinely open choice for what to build next. This Work Package
(`WP 7.2A`) is architecture, governance, and roadmap planning only — no
production code was written. It evaluates seven candidate programmes
against eleven criteria, using repository evidence exclusively, and
recommends exactly one.

## Recommendation

# Programme A — Requirements & Verification Platform

Score 46 of 55 (`WP7.2A Programme Comparison Matrix.md`), the highest of
all seven candidates evaluated. This programme designs and implements a
Requirements Engine (`FCR-0027`), consuming the just-certified
`Tempest.Core.Verification` and `Tempest.Core.EngineeringData`
frameworks directly — the first genuinely domain-facing, engineering-
discipline capability TempestOS will have shipped.

## Why This, and Not the Other Six

- **Programme F (Platform Hardening)** scored second (36/55) and remains
  a real, valid future candidate — recommended second, at `v0.9.0`, not
  rejected. It was not recommended first because every one of its own
  named triggers (a real third-party plugin; a concrete networked
  deployment scenario) remains unfired; building it now would itself
  violate the same "do not build ahead of demonstrated need" principle
  that governs everywhere else in this project.
- **Programme G (AI & Engineering Intelligence)** scored 19/55 — its
  own register entry confirms the underlying capability already works
  structurally; there is no design gap to close.
- **Programmes B, C, D, E (Mechanical, Building Services/HVAC,
  Structural, Electrical)** each scored 14/55 — `WP7.0B Engineering
  Discipline Assessment.md`'s own finding, re-confirmed unchanged by
  this review, is that zero capabilities are identified in any of the
  four, and no sequencing recommendation among them is possible without
  inventing content this repository has no evidence for.

## Engineering Workflow vs. Engineering Disciplines

**TempestOS should next focus on Engineering Workflow, not Engineering
Disciplines.** Requirements and Verification are discipline-agnostic —
every future discipline module, whichever is eventually identified,
would want to trace its own artefacts to a requirement and a
verification record. Building this workflow layer now means the first
real discipline module, whenever it arrives, has a traceability
mechanism already waiting for it, rather than inventing its own.

## Release Planning

- **`v0.8.0` (recommended):** Systems Engineering Foundation — Programme
  A's own architecture, planning, contract review, and implementation.
- **`v0.9.0` (recommended, second):** Platform Hardening — Programme F,
  unless a real third-party plugin or networked deployment scenario
  triggers it sooner.
- **`v1.0.0` (provisional milestone, not scoped):** the point at which a
  real individual engineer or small practice could run requirement →
  verification → report end-to-end through the real, unmodified Host.

No release number is a commitment — each still requires its own
Architecture, Planning, and Contract Review phase, per `Product
Roadmap.md`'s own standing "Non-Commitments" discipline.

## Risk Summary (Roadmap, Commercial, Architectural, Governance)

- **Roadmap risk:** `VISION.md`'s own "readiness" objective (auth/TLS
  resolved, plugin trust closed, governance tooling mature) is not fully
  met by this recommendation — disclosed, not hidden. Each unmet
  trigger is actively monitored, not deferred indefinitely.
- **Commercial risk:** No real, named customer validates Programme A's
  own scope yet — it remains provisional until a real engineering
  practice's own need confirms it, exactly as `WP7.0B Roadmap Risk
  Register.md`'s own `RR-1` already requires for the foundation it
  builds on.
- **Architectural risk:** The Engineering Foundation itself was
  validated against only two disciplines' own aspirational descriptions
  — Programme A is one of those two, carrying `RR-1`'s own residual risk
  forward, not resolving it.
- **Governance risk:** `FCR-0005` (Governance Register Health-Check
  Tooling), still unbuilt after three separate recurrences of the
  identical drift pattern it exists to prevent (`v0.5.0`-era, `v0.6.0`,
  the Engineering Foundation programme), remains a standing governance
  risk this review did not resolve — carried forward into
  `WP7.2A Candidate Work Package Catalogue.md`'s own explicit
  acknowledgement that Programme F, once scoped, should build it.

## What Was Verified

- All 36 `Future Capability Register.md` entries reviewed against all
  seven candidate programmes.
- `Product Roadmap.md`'s own Phase 4 "working premise" checked directly
  against what `WP 7.0B` actually built — found to differ, disclosed
  explicitly, not smoothed over.
- `VISION.md`'s own "readiness" objective checked against `Security
  Principles.md` Principle 7 and each named trigger's own current
  status — found in tension, resolved with reasoning stated plainly, not
  silently assumed.
- Ten completion deliverables produced, each independently citing the
  repository evidence behind its own conclusions.

## Recommendation to Product Approval

Approve Programme A (Requirements & Verification Platform) as
`v0.8.0`'s own scope, subject to its own dedicated Architecture,
Planning, and Contract Review phase — this Work Package does not
approve implementation itself. Retain Programme F (Platform Hardening)
as the next-sequenced programme after Programme A, not abandoned.

## Related Documents

`WP7.2A Strategic Roadmap Review.md`; `WP7.2A Programme Comparison
Matrix.md`; `WP7.2A Capability Dependency Report.md`; `WP7.2A
Recommended Programme.md`; `WP7.2A Candidate Work Package Catalogue.md`;
`WP7.2A Security Assessment.md`; `WP7.2A Commercial Assessment.md`;
`WP7.2A Engineering Assessment.md`; `WP7.2A Lessons Learned.md`.
