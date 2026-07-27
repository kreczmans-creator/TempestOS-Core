# Governance Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Governance Register |
| **Purpose** | Tracks, per Work Package, whether Engineering Governance's own obligations (§5 ADR criteria, §6 Academy maintenance, §10 Rejected Designs) were actually met — the compliance record, distinct from the Decision Register's record of *what* was decided. |
| **Scope** | Every Work Package from `7514b9d` (first Claude-authored commit) through `WP 5.0C`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Git history (`git log`); `docs/academy/06 Engineering Standards/Engineering Governance.md`; each Work Package's own retrospective. |
| **Review Frequency** | Updated at the end of every Work Package. |
| **Last Reviewed** | 2026-07-27 (WP 5.0C, Shell & Composition Framework Architecture). |
| **Related Documents** | `Decision Register.md`; `ADR Register.md`; `Rejected Designs Register.md`; `Academy Register.md`; `Feature Register.md`. |
| **Related ADRs** | All 35 — this register verifies each one's originating Work Package actually followed §5. |
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
| WP 5.0C — Shell & Composition Framework Architecture | *(this commit)* | ADR-0033, ADR-0034, ADR-0035 | RD-0034–RD-0037 | Yes |

**Total: 31 Work Packages tracked, plus `v0.4.0` Release Engineering,
100% Academy
retrospective compliance for every Work Package that required one
(housekeeping correctly excepted).**

## Findings

- **Zero governance gaps found.** Every Work Package that met §5's ADR
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
