# Academy Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Academy Register |
| **Purpose** | The complete index of every Academy article — its category, subject, and originating Work Package — verifying the Academy's own maintenance obligation (Engineering Governance §6) is being met in practice, not just in principle. |
| **Scope** | Every file under `docs/academy/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/academy/` itself; `docs/academy/Academy Index.md` (the reader-facing navigation index this register cross-checks against). |
| **Review Frequency** | Updated whenever a new Academy article is created — in practice, every Work Package (Engineering Governance §6). |
| **Last Reviewed** | 2026-07-27 (WP 5.0D). |
| **Related Documents** | `docs/academy/Academy Index.md`; `docs/academy/Academy Audit Report.md`; `Engineering Standards Register.md`; `Feature Register.md`. |
| **Related ADRs** | None directly — the Academy documents ADRs, it is not itself governed by one. |
| **Related Academy Articles** | This register's entire scope. |
| **Coverage Status** | Complete. |

---

## 00 Introduction (1 article)

| Article |
|---|
| Welcome to the TempestOS Academy |

## 01 Engineering Principles (11 articles)

| # | Article |
|---|---|
| 01 | SOLID |
| 02 | Separation of Concerns |
| 03 | Immutability |
| 04 | Composition Over Inheritance |
| 05 | Dependency Injection |
| 06 | Fail Fast |
| 07 | Deterministic Systems |
| 08 | State Machines |
| 09 | Defensive Programming |
| 10 | Single Responsibility Principle |
| 11 | Atomic Phase Principle |

## 02 Runtime Architecture (10 articles)

| # | Article | Last Materially Updated |
|---|---|---|
| 01 | The Module Pipeline | WP 2.x era |
| 02 | The Startup Sequence | WP 2.6/2.7 era |
| 03 | Building a Module | WP 4.1/4.4B |
| 04 | Building an Event-Driven Module | WP 4.4E |
| 05 | Working with the TempestOS Host | WP 4.5 (expanded) |
| 06 | Platform Layering | WP 5.0A (Navigation worked example added) |
| 07 | Plugin Architecture | WP 4.2 |
| 08 | Failure Isolation Across TempestOS | WP 5.0A (Navigation's "no new case needed" finding added) |
| 09 | Navigation Architecture | WP 5.0A (new), WP 5.0B (implementation confirmed) |
| 10 | Shell & Application Composition | WP 5.0C (new), WP 5.0D (implementation confirmed; `const`-field/assembly-loading finding added) |

## 03 Work Packages (29 retrospectives)

| Retrospective | Type |
|---|---|
| WP 2.1 — Module Discovery | Implementation |
| WP 2.2 — Runtime Registration | Implementation |
| WP 2.3 — Runtime Lifecycle | Implementation |
| WP 2.4 — Dependency Injection | Implementation |
| WP 2.5 — Configuration Framework | Implementation |
| WP 2.6 — Logging & Diagnostics Framework | Implementation |
| WP 2.7 — Runtime Host Architecture Review | Architecture |
| WP 2.7B — Runtime Host Implementation | Implementation |
| WP 4.0 — Platform Contracts | Implementation (contracts only) |
| WP 4.1 — Module SDK | Implementation |
| WP 4.2 — Plugin Manifest Architecture | Architecture |
| WP 4.2A — Runtime Platform Version | Implementation |
| WP 4.2B — Plugin Failure Classification | Architecture (ADR-0025) |
| WP 4.2C — Plugin Discovery Lifecycle Placement | Architecture (ADR-0026) |
| WP 4.2 — Plugin Manifest Implementation | Implementation |
| WP 4.2D — Platform Services Architecture Review | Review/Audit |
| WP 4.3 — Sample Module Architecture | Architecture |
| WP 4.3 — Sample Module Implementation | Implementation |
| WP 4.4A — Dependency Injection for Discovered Modules | Architecture (ADR-0027 design) |
| WP 4.4B — ADR-0027 Implementation | Implementation |
| WP 4.4 — Event Bus Architecture | Architecture (ADR-0028; also covers WP 4.4C) |
| WP 4.4D — Event Bus Implementation | Implementation |
| WP 4.4E — Sample Module Event Integration | Implementation |
| WP 4.5 — Background Services Architecture | Architecture (ADR-0029/0030) |
| WP 4.5 — Background Services Implementation | Implementation |
| WP 5.0A — Navigation Framework Architecture | Architecture (ADR-0031/0032) |
| WP 5.0B — Navigation Framework Implementation | Implementation |
| WP 5.0C — Shell & Composition Framework Architecture | Architecture (ADR-0033/0034/0035) |
| WP 5.0D — Shell & Composition Framework Implementation | Implementation |

**Note.** `WP 4.4C` produced no code and no separate retrospective — its
story is told inside the `WP 4.4` architecture retrospective's own
Background section (Verified — this is stated explicitly in that
document and in `Academy Index.md`). `WP 4.4F` (Academy & Documentation
Baseline Audit) is tracked as `docs/academy/Academy Audit Report.md`
itself, below, rather than as a `03 Work Packages/` retrospective — its
own deliverable *is* the audit report.

## 04 Design Patterns (4 articles)

| # | Article |
|---|---|
| 01 | The Registry Pattern |
| 02 | Descriptor and Snapshot Types |
| 03 | Minimal Interface, Extension-Method Sugar |
| 04 | Reflection-Based Discovery (expanded WP 4.5 — third real-world application) |

## 05 Case Studies (5 articles)

| # | Article | Paired ADR |
|---|---|---|
| 01 | Why RuntimeModule Is Immutable | ADR-0001 |
| 02 | Why Lifecycle State Lives Externally | ADR-0002 |
| 03 | Why Dispose Is Always Legal | ADR-0004 |
| 04 | Why Discovery Is Isolated | ADR-0008 |
| 05 | Why Isn't Configuration Mutable? | (general principle, no single paired ADR) |

## 06 Engineering Standards (5 documents)

See `Engineering Standards Register.md` for the detailed index — not
duplicated here. Grew from 3 to 4 documents during `WP 4.5A` (which added
`03-governance-registers.md`), and from 4 to 5 during `WP 4.5B` (which
added `Engineering Lifecycle.md`).

## Top-Level Academy Meta-Documents (4)

| Document | Purpose |
|---|---|
| `Academy Index.md` | Reader-facing navigable table of contents |
| `Academy Masterclass Roadmap.md` | Candidate long-form synthesis subjects, none yet written |
| `Academy Audit Report.md` | `WP 4.4F`'s own audit deliverable |
| `Contributor Learning Path.md` | Repository-wide onboarding sequence for a new contributor, added `WP 4.5B` |

**Total: 1 (Introduction) + 11 (Engineering Principles) + 10 (Runtime
Architecture) + 29 (Work Packages) + 4 (Design Patterns) + 5 (Case
Studies) + 5 (Engineering Standards) + 4 (top-level meta) = 69 files
under `docs/academy/` (Verified by direct file count).**

## Governance Maintenance Check (Engineering Governance §6)

Every Work Package from `WP 2.1` onward produced or updated Academy
documentation as part of its own delivery — **Verified** by direct
correspondence between `Feature Register.md`'s Work Package list and this
register's own "03 Work Packages" table: no Work Package that shipped
code or an ADR is missing a retrospective, and no retrospective exists
for a Work Package that never happened. No stale "Future Evolution"
prediction was found still describing a since-resolved gap as open — the
most recent example checked directly, Background Services' own
"designed, not yet implemented" markers across `02 Runtime Architecture/
05-the-runtime-host.md`, `06-platform-layering.md`, and
`08-failure-isolation.md`, were updated to reflect implementation as part
of `WP 4.5`'s own delivery.

## Cross-Reference Check

Every article above is cross-checked directly against
`docs/academy/Academy Index.md`'s own listing — no article exists in one
but not the other, and `Academy Index.md`'s own section headings
(Welcome, Learning Path, Engineering Principles, Platform Architecture,
Runtime, Dependency Injection, Modules, Plugins, Events, Background
Services, Design Patterns, Engineering Governance, Case Studies, Work
Package Walkthroughs, Reference Material) match this register's own
category grouping one-for-one.
