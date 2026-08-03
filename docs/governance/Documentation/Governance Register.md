# Governance Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Governance Register |
| **Purpose** | Tracks, per Work Package, whether Engineering Governance's own obligations (§5 ADR criteria, §6 Academy maintenance, §10 Rejected Designs) were actually met — the compliance record, distinct from the Decision Register's record of *what* was decided. |
| **Scope** | Every Work Package from `7514b9d` (first Claude-authored commit) through `WP 7.3A`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Git history (`git log`); `docs/academy/06 Engineering Standards/Engineering Governance.md`; each Work Package's own retrospective. |
| **Review Frequency** | Updated at the end of every Work Package. |
| **Last Reviewed** | 2026-07-30 (WP 7.4.0, Release Preparation & Product Baseline) — backfilled all twelve `v0.7.0` Work Packages so far (`WP 7.0A` through `WP 7.3A`) plus `v0.6.0` Release Engineering, missing from this register's own Compliance Matrix since `WP 6.8` — the identical recurring governance-drift pattern found and closed for `Interface Register.md`/`Dependency Injection Register.md`/`Module Register.md` (`WP 7.1F`) and `Platform Services Register.md`/`Platform Service Map.md` (`WP 7.3A`), now found in this register too; see Findings. Also corrected the `WP 6.8` row's own commit hash, which had been left as a stale self-reference (`*(this commit)*`) rather than the real, resolved hash (`6344204`) once the commit actually existed. Previously reviewed 2026-07-29 (WP 6.8, Platform Services Integration Review) — backfilled all nine `v0.6.0` Work Packages (`WP 6.0` through `WP 6.8`), missing from this register's own Compliance Matrix since `WP 5.3` — a genuine, previously-undisclosed governance-documentation drift found during this Work Package's own closing review, not flagged by any prior Work Package; see Findings. |
| **Related Documents** | `Decision Register.md`; `ADR Register.md`; `Rejected Designs Register.md`; `Academy Register.md`; `Feature Register.md`. |
| **Related ADRs** | All 61 — this register verifies each one's originating Work Package actually followed §5. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/Engineering Governance.md`. |
| **Coverage Status** | Complete. |

---

## Compliance Matrix

"ADR?" is marked **—** where no ADR was required (§5's own criteria not
met by that Work Package) rather than left blank, so the column always
records an explicit judgement, never an omission.

| Work Package | Commit | ADR? | RD Entries? | Academy Retrospective? |
|---|---|---|---|---|
| Repository Stabilisation (housekeeping) | `7514b9d` | — | — | Acknowledged by name in the Academy Welcome article, no dedicated retrospective (correctly — not an architectural Work Package) |
| WP 2.1 — Module Discovery | `407e109` | ADR-0003, ADR-0008 | — (Log did not yet exist) | Yes |
| WP 2.2 — Runtime Module Manager | `a0ede0a` | ADR-0001 | — | Yes |
| WP 2.3 — Runtime Lifecycle | `b228e74` | ADR-0002, ADR-0004 | — | Yes |
| WP 2.4 — Dependency Injection | `1eca2c3` | ADR-0005, ADR-0006, ADR-0007 | — | Yes |
| WP 2.5 — Configuration Framework | `ab84a81` | ADR-0009 | — | Yes |
| WP 2.6 — Logging & Diagnostics Framework | `ca92ed2` | ADR-0010 | — | Yes |
| WP 2.7 — Runtime Host Architecture (design) | `615d1ab` | ADR-0011–ADR-0018 | — | Yes |
| WP 2.7B — Runtime Host Implementation | `b6916b4` | ADR-0019 | — | Yes |
| WP 4.0 — Platform Contracts | `cf58c7e` | ADR-0024 | RD-0001, RD-0002 | Yes |
| WP 4.1 — Module SDK | `1f84994` | — | RD-0003–RD-0007 | Yes |
| WP 4.2 — Plugin Manifest Architecture | `a1ac4de` | — (prerequisites spawned instead) | RD-0008, RD-0009 | Yes |
| WP 4.2A — Runtime Platform Version | `81c71a7` | — | — | Yes |
| WP 4.2B — Plugin Failure Classification | `e445a7a` | ADR-0025 | RD-0010, RD-0011 | Yes |
| WP 4.2C — Plugin Discovery Lifecycle Placement | `c63d5b6` | ADR-0026 | RD-0012–RD-0014 | Yes |
| WP 4.2 — Plugin Manifest Implementation | `3dfec95` | — | — | Yes |
| WP 4.2D — Platform Services Architecture Review | `1d39044` | — | — | Yes |
| WP 4.3 — Sample Module Architecture | `b17ed56` | — | RD-0015 | Yes |
| WP 4.3 — Sample Module Implementation | `0b97141` | — | — | Yes |
| WP 4.4A — Dependency Injection for Discovered Modules | `7897071` | ADR-0027 | RD-0016–RD-0018 | Yes |
| WP 4.4B — ADR-0027 Implementation | `e2096db` | — | — | Yes |
| WP 4.4 — Event Bus Design | `6dfd498` | ADR-0020, ADR-0028 | RD-0019–RD-0022 | Yes (also covers WP 4.4C) |
| WP 4.4D — Event Bus Implementation | `f60ac8c` | — | — | Yes |
| WP 4.4E — Sample Module Event Integration | `2d5f32d` | — | — | Yes |
| WP 4.4F — Academy & Documentation Baseline Audit | `c9aa96e` | — | — | Yes (the audit report itself) |
| WP 4.5 — Background Services Design | `d903eed` | ADR-0021 (reaffirmed), ADR-0029, ADR-0030 | RD-0023–RD-0029 | Yes |
| WP 4.5 — Background Services Implementation | `c460aaf` | — | — | Yes |
| WP 4.5A — Governance Register Baseline | `256afc8` | — | — | This Work Package's own governance material (see `Governance Philosophy.md`) |
| WP 4.5B — Platform Foundation Closeout | `eb19605` | — | — | This Work Package's own closeout material (see `docs/releases/Platform Foundation Completion Report.md`) |
| v0.4.0 Release Engineering | `2c88c07`/`5802b92` | — | — | Not a Work Package in the numbered sequence — a Release Engineering activity, per Engineering Governance §7. `docs/releases/v0.4.0/Release Notes.md` and `docs/releases/v0.4.0.md` serve the retrospective role for this activity. |
| WP 5.0A — Navigation Framework Architecture | `c3f9246` | ADR-0031, ADR-0032 | RD-0030–RD-0033 | Yes |
| WP 5.0B — Navigation Framework Implementation | `df4cb45` | — (implements ADR-0031/ADR-0032 exactly; no new ADR required) | — | Yes |
| WP 5.0C — Shell & Composition Framework Architecture | `6ce9173` | ADR-0033, ADR-0034, ADR-0035 | RD-0034–RD-0037 | Yes |
| WP 5.0D — Shell & Composition Framework Implementation | `8d268a7` | — (implements ADR-0033–ADR-0035 exactly; no new ADR required) | — | Yes |
| WP 5.0S — Platform Security Baseline Audit | `6d7def5` | — (audit; no architecture redesigned, per its own brief) | — | Yes |
| WP 5.1A — Command Framework Architecture | `8aad1f0` | ADR-0036, ADR-0037, ADR-0038 | RD-0038–RD-0041 | Yes |
| WP 5.1B — Command Framework Implementation | `3ef23a9` | — (implements ADR-0036–ADR-0038 exactly; no new ADR required) | — | Yes |
| WP 5.2 — Diagnostics Improvements | `a0520d5` | ADR-0039 | RD-0042–RD-0044 | Yes |
| WP 5.3 — Developer Experience Improvements | `10c5b14` | — (RD-0045 only; no ADR met §5's criteria) | RD-0045 | Yes |
| WP 5.4 — v0.5.0 Release Candidate & Engineering Sign-Off | `d30e286` | — (verification only; no new ADR) | — | Yes — deliberately shaped around what a release-verification retrospective actually needs, not the standard 13-section template (disclosed in that document's own "What This Document Is") |
| WP 6.1 — Permissions & Identity | `c8c9ced` | ADR-0043, ADR-0044 | — (alternatives recorded within the ADRs themselves) | Yes |
| WP 6.4 — Settings Framework | `7e13af7` | ADR-0041, ADR-0042 | — | Yes |
| WP 6.5 — Audit Framework | `66b1cf1` | ADR-0045 | — | Yes |
| WP 6.2 — Notification Framework | `f5db8d6` | ADR-0046 | — | Yes |
| WP 6.0 — Reporting Framework | `2178207` | ADR-0040 | — | Yes |
| WP 6.3 — REST API | `08cb844` | ADR-0047, ADR-0048, ADR-0049, ADR-0052 | — | Yes |
| WP 6.7 — Export/Import Framework | `4283469` | ADR-0051 | — | Yes |
| WP 6.6 — Licensing Framework | `a940e0f` | ADR-0050 | — | Yes |
| WP 6.8 — Platform Services Integration Review & Release Certification | `6344204` | — (certification review; no architectural decision made, none required) | — | Yes — deliberately shaped around a whole-release verification pass, not the standard 13-section template, mirroring `WP 5.4`'s own precedent |
| `v0.6.0` Release Engineering | `18e61d5`/`7709ccb` | — | — | Not a Work Package in the numbered sequence — a Release Engineering activity, per Engineering Governance §7. `docs/releases/v0.6.0/ReleaseNotes.md` serves the retrospective role for this activity. |
| WP 7.0A — Future Capability Register & Product Vision | `6a11ae3` | — (governance/vision milestone; no architectural decision met §5's criteria) | — | Yes — whole-review format, mirroring `WP 6.8`'s own precedent |
| WP 7.0B — Engineering Foundation Planning & Capability Architecture | `2f8d1ef` | — (planning milestone; no architectural decision met §5's criteria) | — | Yes — whole-review format |
| WP 7.0C — Engineering Foundation Contract Review | `36cbc88` | — (contract review only; `ADR-0053`–`ADR-0057` reserved, not yet written) | — | Yes — whole-review format |
| WP 7.1A — Engineering Data Model | `4dee45d` | ADR-0053 | — | Yes |
| WP 7.1B — Units & Quantities Framework | `5769901` | ADR-0054 | — | Yes |
| WP 7.1C — Materials Framework | `d9b1ff7` | ADR-0055 | — | Yes |
| WP 7.1D — Engineering Calculation Framework | `91b6714` | ADR-0056 | — | Yes |
| WP 7.1E — Verification Framework | `9d0a65c` | ADR-0057 | — | Yes |
| WP 7.1F — Engineering Core Integration Review & Certification | `59db844` | — (certification review; no architectural decision made, none required) | — | Yes — whole-review format, mirroring `WP 6.8`'s own precedent |
| WP 7.2A — Strategic Roadmap Selection & Programme Architecture | `31adcfd` | — (roadmap/governance milestone; no architectural decision met §5's criteria) | — | Yes — whole-review format |
| WP 7.2B — Requirements & Verification Platform Architecture | `0e069e8` | — (architecture only; `ADR-0058`–`ADR-0060` reserved, not yet written) | — | Yes — whole-review format |
| WP 7.2C — Requirements & Verification Platform Contract Review | `d532648` | — (contract review only; `ADR-0061` newly reserved, `ADR-0058`–`ADR-0060` carried forward unanswered) | — | Yes — whole-review format |
| WP 7.3A — Requirements Engine | `ab43ccd` | ADR-0058, ADR-0059, ADR-0060, ADR-0061 | — | Yes |

**Total: 60 Work Packages tracked, plus `v0.4.0` and `v0.6.0` Release
Engineering, 100% Academy retrospective compliance for every Work
Package that required one (housekeeping correctly excepted). Backfilled
by `WP 7.4.0` (Release Preparation & Product Baseline) — this Compliance
Matrix had gone stale since `WP 6.8`, missing all twelve Work Packages
of the Engineering Foundation and Systems Engineering Foundation
programmes (`WP 7.0A` through `WP 7.3A`), the same recurring
governance-drift pattern `WP 6.8` and `WP 7.1F` each found and closed
for other registers — this is the first time it has been found in this
specific register.**

## Findings

- **Repository review correction (`WP 7.4.0`).** This register's own
  Compliance Matrix had not been updated since `WP 6.8` — all twelve
  Work Packages of the Engineering Foundation and Systems Engineering
  Foundation programmes (`WP 7.0A` through `WP 7.3A`) were missing
  entirely, plus `v0.6.0` Release Engineering. This is the third time
  this exact register has gone stale for several consecutive Work
  Packages before a later, dedicated review caught it (`WP 5.3`,
  `WP 6.8`, now `WP 7.4.0`) — a standing, recurring pattern, not a
  one-off. All twelve rows plus the Release Engineering row backfilled
  here, verified directly against `git log` and each Work Package's own
  ADR Register entry; no discrepancy found once complete. The `WP 6.8`
  row's own commit hash was also found still reading its original
  self-reference placeholder (`*(this commit)*`), never resolved to the
  real hash (`6344204`) once the commit existed — corrected here.
- **Repository review correction (`WP 6.8`).** This register's own
  Compliance Matrix had not been updated since `WP 5.3` — all nine
  `v0.6.0` Work Packages (`WP 6.0` through `WP 6.8`) were missing
  entirely, a nine-Work-Package gap larger than the four-Work-Package
  gap `WP 5.3` itself found and fixed in this same register. None of
  the eight feature Work Packages' own repository reviews caught this,
  despite six of them finding and fixing other governance drift
  elsewhere during this same release (`Platform Service Map.md`,
  `Hosted Services Register.md`, `Interface Register.md`/`Dependency
  Injection Register.md`/`Module Register.md`) — this specific register
  was never re-opened by any of them. All nine rows backfilled here,
  verified directly against `git log` and each Work Package's own ADR
  Register entry; no discrepancy found once complete. This is now the
  second time this exact register has gone stale for several
  consecutive Work Packages before a later, dedicated review caught it
  — see `WP6.8 Platform Certification Report.md`'s own discussion of
  `Risk Register.md`'s `R6` for the standing pattern this confirms.
- **Repository review correction (`WP 5.3`).** This register's own
  Compliance Matrix had not been updated since `WP 5.0D` — four
  completed Work Packages (`WP 5.0S`, `WP 5.1A`, `WP 5.1B`, `WP 5.2`)
  were missing entirely, and `WP 5.0D`'s own row still carried a
  `*(this commit)*` placeholder never backfilled with its real commit
  hash once that commit landed. None of the four intervening Work
  Packages' own repository reviews caught this, despite each fixing
  other, similar drift elsewhere (`Feature Register.md`, `Traceability
  Matrix.md`, `Architecture Document Register.md`) — this register
  itself was simply never re-opened. All five rows backfilled here,
  verified directly against `git log` and each Work Package's own
  retrospective; no discrepancy found once complete.
- **Zero governance gaps found**, backfill aside. Every Work Package that met §5's ADR
  criteria produced an ADR; every Work Package that met §10's rejected-
  alternative criteria produced a Rejected Designs entry; every Work
  Package produced an Academy retrospective, per §6, with the single,
  correctly-reasoned exception of the housekeeping commit that predates
  the Rejected Designs Log's own existence and carries no architectural
  content to retrospect.
- ADR-0021 (Background Service failure classification) is listed against
  `WP 4.5 — Background Services Design` above as "reaffirmed" because it
  was originally decided during v0.4.0 planning, before `WP 4.5` existed
  as a named Work Package (see `ADR Register.md`'s own note) — its
  Work Package attribution is deliberately not forced into a single row.

## Cross-Reference Check

Every ADR/RD count above is cross-checked against `ADR Register.md` and
`Rejected Designs Register.md` directly — no mismatch found. Every
"Academy Retrospective? Yes" is cross-checked against `Academy
Register.md`'s own "03 Work Packages" table — no Work Package is marked
compliant here without a corresponding retrospective actually existing.
