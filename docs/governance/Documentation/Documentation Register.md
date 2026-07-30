# Documentation Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Documentation Register |
| **Purpose** | The master map of every documentation directory in the repository — what lives where, and which more detailed register indexes its contents. Deliberately a map of maps: it does not re-list every individual document (the ADR Register, Architecture Document Register, and Academy Register already do that in full), only where to look. |
| **Scope** | Every directory under `docs/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | The `docs/` tree itself. |
| **Review Frequency** | Updated whenever a new top-level documentation directory or document type is introduced. |
| **Last Reviewed** | 2026-07-30 (`WP 7.1B`, Units & Quantities Framework) — **Partial update only**: the `docs/releases/v0.7.0/` row's own description updated to mention `WP 7.1B`'s seven deliverables; no other row re-verified this pass, same disclosed staleness as below. Previously reviewed 2026-07-30 (`v0.6.0` Release Engineering) — **Partial update only**: the `docs/releases/` rows in the Directory Map were corrected (v0.5.0 relabelled released, v0.6.0 and v0.7.0 rows added); no other row was re-verified this pass, so the ADR/Academy/architecture-document counts below are carried forward from 2026-07-28 (`WP 5.3`) unchanged and are known to be stale (`docs/adr/` alone has grown from 39 to 54 since). A full re-derivation of every row is recommended as a candidate item for `v0.7.0` (see `docs/releases/v0.7.0/WorkPackages.md` C2, the governance-register health check candidate), not attempted here since neither pass was the dedicated health-check Work Package. |
| **Related Documents** | `ADR Register.md`; `Architecture Document Register.md`; `Academy Register.md`; `Release Register.md`; `Governance Index.md`. |
| **Related ADRs** | None directly. |
| **Related Academy Articles** | None directly. |
| **Coverage Status** | Complete. |

---

## Directory Map

| Directory | Contents | Detailed Index |
|---|---|---|
| `docs/adr/` | 39 Architecture Decision Records | `ADR Register.md` |
| `docs/architecture/` | 20 standing architecture documents, including the Rejected Designs Log and Engineering Glossary | `Architecture Document Register.md`; `Rejected Designs Register.md` |
| `docs/academy/00 Introduction/` | Academy welcome/orientation | `Academy Register.md` |
| `docs/academy/01 Engineering Principles/` | 11 general software-engineering principles | `Academy Register.md` |
| `docs/academy/02 Runtime Architecture/` | 11 concept guides synthesising the runtime | `Academy Register.md` |
| `docs/academy/03 Work Packages/` | 32 Work Package retrospectives | `Academy Register.md`; `Feature Register.md` |
| `docs/academy/04 Design Patterns/` | 4 recurring structural pattern guides | `Academy Register.md` |
| `docs/academy/05 Case Studies/` | 5 narrative decision deep-dives | `Academy Register.md` |
| `docs/academy/06 Engineering Standards/` | Engineering Governance (the constitution, now 12 sections) plus 4 coding/process standards | `Engineering Standards Register.md` |
| `docs/academy/Academy Index.md`, `Academy Masterclass Roadmap.md`, `Academy Audit Report.md`, `Contributor Learning Path.md` | Academy's own meta-documents | `Academy Register.md` |
| `docs/releases/FOUNDATION.md` | Permanent, cross-release engineering constitution | `Architecture Document Register.md`; `Release Register.md` |
| `docs/releases/Platform Foundation Completion Report.md` | Foundation phase closeout narrative (`WP 4.5B`) | `Release Register.md` |
| `docs/releases/v0.2.0/` | Empty directory — **Unknown** why no content exists; no retrospective or CHANGELOG entry explains this gap | `Release Register.md` |
| `docs/releases/v0.3.0.md` | v0.3.0 release notes (Runtime Foundation Complete) | `Release Register.md` |
| `docs/releases/v0.4.0/` | The released ("Platform Foundation") v0.4.0 release's own planning, architecture review, risk register, work packages, changelog, testing strategy, release checklist | `Release Register.md`; `Risk Register.md`; `Feature Register.md` |
| `docs/releases/v0.5.0/` | The released ("Developer Experience") v0.5.0 release's own plan, work packages, changelog, and release checklist | `Release Register.md`; `Feature Register.md` |
| `docs/releases/v0.6.0/` | The released ("Platform Services") v0.6.0 release's own architecture package, contract review package, work packages, risk register, and nine `WP 6.8` certification deliverables (`CERTIFIED WITH ACCEPTED TECHNICAL DEBT`) | `Release Register.md`; `Risk Register.md`; `Feature Register.md`; `Governance Register.md` |
| `docs/releases/v0.7.0/` | The in-progress ("Engineering Foundation") v0.7.0 release's own prepared-in-advance skeleton documents (`WorkPackages.md`, `ReleaseNotes.md`, `Retrospective.md`), `WP 7.0A`'s five, `WP 7.0B`'s eight, and `WP 7.0C`'s eight planning/review deliverables, plus `WP 7.1A`'s seven, `WP 7.1B`'s seven, `WP 7.1C`'s seven, and `WP 7.1D`'s eight implementation deliverables (the first to include a dedicated Security Review Report) — the first four real Work Package implementations of this release | `Release Register.md`; `Future Capability Register.md` |
| `docs/roadmap/` | Empty directory — **Unknown** intended purpose; no document references it | Not applicable — see Coverage Note below |
| `docs/diagrams/` | Empty directory — **Unknown** intended purpose; no document references it | Not applicable — see Coverage Note below |
| `docs/governance/` | This governance suite (introduced by `WP 4.5A`), plus `Future Work Package Guidelines.md` (added `WP 4.5B`); `Future Capability Register.md`, `Capability Categories.md`, `Product Roadmap.md` (added `WP 7.0A`, the new "Product & Roadmap" category) | `Governance Index.md` |
| `docs/security/` | The v0.5.0 Security Baseline: `Threat Model.md`, `Security Principles.md`, `Platform Security Review v0.5.0.md`, `Security Roadmap.md` (all introduced by `WP 5.0S`) | `Governance Index.md` (Security section) |
| `docs/engineering/` | `Engineering Principles.md` — the principles engineering-domain content itself must uphold, distinct from `docs/academy/06 Engineering Standards/` (which governs how TempestOS is built as software); introduced `WP 7.1A` | `Governance Index.md` |
| `src/Templates/` | `dotnet new` template sources (`WP 5.3`) — not under `docs/`, but documentation/tooling content rather than compiled platform code, and indexed here for the same reason `docs/security/` earns its own row | `src/Templates/README.md` is its own source of truth; see `Engineering Governance.md` §11 |
| Repository root (`PROJECT_STATUS.md`, `VISION.md`) | Primary status dashboard (`WP 4.5B`); permanent product vision document (`WP 7.0A`) | This register; each document is its own source of truth |

## Coverage Note — Two Empty, Unreferenced Directories

`docs/roadmap/` and `docs/diagrams/` both exist and are both empty
(**Verified** by direct `ls`). Neither is referenced by any ADR, Work
Package retrospective, `WorkPackages.md`, or `CHANGELOG.md` entry
reviewed during this baseline. Their intended purpose is **Unknown** —
recorded honestly as such rather than guessed at. `docs/releases/v0.2.0/`
is similarly empty and similarly unexplained by any document reviewed; it
is **Inferred** to be a placeholder from before the Claude-developed
history began (the earliest Claude-authored commit, `7514b9d`, already
finds `docs/releases/v0.3.0.md` as the active release document, with no
v0.2.0 content ever populated in between), but this is an inference, not
a Verified fact — no commit was found that explains why v0.2.0 has no
content.

## Cross-Reference Check

Every directory listed above is cross-checked against a `find docs -type
d` listing taken at time of review — no directory exists that is not
represented in this map, and no entry here refers to a directory that
does not exist.
