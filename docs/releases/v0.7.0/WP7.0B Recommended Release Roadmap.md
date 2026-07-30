# WP 7.0B — Recommended Release Roadmap

## Status

Complete. **This is a recommendation, not a commitment.** `docs/
governance/Product Roadmap.md`'s own "Non-Commitments" section
(established `WP 7.0A`) deliberately withheld release numbers beyond
Phase 4; this document is the release-number mapping that Work Package
explicitly deferred, produced now because this Work Package's own
controlling instruction asks for it directly. It does not amend
`Product Roadmap.md`'s own non-commitment stance — it is a separate,
equally non-binding companion document, cited from that register.

## Mapping: Product Roadmap Phases → Release Numbers

| Product Roadmap Phase | Recommended Release | Rationale |
|---|---|---|
| Phase 4 — Engineering Foundation | **`v0.7.x`** | Already the branch name (`feature/v0.7.0-engineering-foundation`) and the current phase; `v0.7.0` itself is this phase's own first release once Candidate WPs A/B/C (`WP7.0A Recommended v0.7 Candidate Work Packages.md`) and the Engineering Foundation Programme (`FCR-0029`–`FCR-0033`) both land. If the Platform Hardening and Engineering Foundation Programmes are large enough to warrant splitting, `v0.7.1`/`v0.7.2` would carry the remainder — not committed here, a scope decision for Architecture/Planning. |
| Phase 5 — Engineering Modules | **`v0.8.x`** | The first release to ship a real, discipline-facing capability — most plausibly Systems Engineering and/or Project Management (`FCR-0027`/`FCR-0028`), the only two disciplines with a named platform-level hook. Whether Mechanical/Structural/Electrical/HVAC/Manufacturing join this release or a later one depends entirely on when a real stakeholder need for any of them is identified — not predictable today. |
| Phase 6 — Professional Features | **`v0.9.x`** | Capability that makes an existing Engineering Module (shipped `v0.8.x`) genuinely usable day-to-day — logically cannot precede having a real module to make usable. |
| Phase 7 — Enterprise Features | **`v1.0`** | Multi-user, cloud sync, commercial licensing, and compliance readiness are the capabilities that turn a working product into a sellable, scalable one — a defensible candidate for what "`v1.0`" means for TempestOS, though this is itself a product decision, not an engineering one, and not made by this document. |
| Phase 8 — Future Expansion | **Beyond `v1.0`** | Everything not yet phased — third-party plugins, offline/mobile, AI/automation, and whatever a future capability-identification exercise adds to the five still-empty Engineering Discipline categories. |

## Why This Mapping, Not Another

1. **One phase, one release-minor-version family, as a default — not a
   rule.** `v0.7.x` maps to Phase 4 because that is already this
   project's current branch and scope; each subsequent phase gets the
   next minor version family by the same logic. This is the simplest
   mapping consistent with existing evidence, not a discovered
   necessity — a future Architecture/Planning phase could legitimately
   split a phase across two release families or combine two phases into
   one, and this document does not forbid that.
2. **`v1.0` is assigned to Enterprise Features, not to "whenever
   Engineering Modules feel complete."** Seven of nine Engineering
   Discipline categories have zero identified capability; waiting for
   all nine to mature before calling anything "`v1.0`" would tie this
   project's own major-version milestone to a five-plus-discipline gap
   this document cannot close. Tying `v1.0` to Enterprise readiness
   instead is a coherent, achievable definition that does not depend on
   discipline coverage this repository cannot currently predict.
3. **No release is assigned a calendar date.** Consistent with `Product
   Roadmap.md`'s own precedent and `Security Principles.md` Principle 7
   — sequencing, not scheduling.

## What This Document Does Not Do

- It does not commit `v0.7.0` itself to any specific Work Package list
  — `WP7.0A Recommended v0.7 Candidate Work Packages.md` and this Work
  Package's own `WP7.0B Candidate Work Package Catalogue.md` are
  recommendations, not an approved scope.
- It does not predict a date for any release.
- It does not resolve whether Phase 4 needs one release (`v0.7.0`) or
  several (`v0.7.0`–`v0.7.2`) — that depends entirely on how large
  Architecture/Planning eventually scopes the Platform Hardening and
  Engineering Foundation Programmes.

## Related Documents

`docs/governance/Product Roadmap.md` (the phase model this document
maps release numbers onto); `WP7.0B Capability Dependency Report.md`;
`WP7.0A Recommended v0.7 Candidate Work Packages.md`; `WP7.0B Candidate
Work Package Catalogue.md`.
