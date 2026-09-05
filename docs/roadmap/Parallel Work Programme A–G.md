# Parallel Work Programme A–G

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Parallel Work Programme A–G |
| **Purpose** | The definition of a **non-code**, lettered work programme (A–G) that can be executed in parallel with, and independently of, TempestOS's own numbered technical Work Packages — so that reference data, specifications, templates, registers, rules and business content can be produced without consuming Claude Code capacity currently concentrated on `WP 16`. |
| **Scope** | Seven programmes (A–G), forty sub-packages (`A.1`–`G.7`). Definition only: purpose, scope, inputs, fields, outputs, acceptance criteria, dependencies, next action, Claude Code requirement, and TempestOS integration potential for each. **No sub-package below is scoped as an implementation Work Package, and none authorises a code, schema, dependency or architecture change.** |
| **Owner** | Product Owner (commissioning), Project Maintainer (upkeep). |
| **Source of Truth** | This document for the programme structure and the execution standard; each `Parallel Programme X — …md` file in this directory for its own sub-package definitions; `docs/governance/Product Roadmap.md` for phase sequencing of the *technical* product; `docs/releases/v0.16.0/WorkPackages.md` for the live technical Work Package status. |
| **Review Frequency** | Whenever a sub-package is started, completed, re-sequenced, or promoted into a numbered technical Work Package; and at every release boundary. |
| **Last Reviewed** | 2026-09-05 — **established**. Commissioned by the Product Owner as a parallel, non-code work programme; written as definition only, with every sub-package at status **Defined, not started**. No prior document in this repository defines A–G work of this kind; the letter collision with `WP 7.2A`'s own historical Programme A–G vocabulary is disclosed below rather than silently reused. |
| **Related Documents** | `docs/governance/Product Roadmap.md`; `docs/governance/Future Capability Register.md`; `docs/governance/Capability Categories.md`; `docs/governance/Future Work Package Guidelines.md`; `VISION.md`; `docs/governance/Documentation/Documentation Register.md`. |
| **Related ADRs** | None. This programme deliberately produces no architectural decision; any decision arising from a *later* integration of its output is that integration Work Package's own ADR to write. |
| **Coverage Status** | **Complete as a definition** for all forty sub-packages. **Empty as content** — no dataset, template, register or document defined below has been produced yet. Recorded honestly as such. |

---

## 1. Purpose of This Document

This document answers one question: **what work can be done on TempestOS
that does not need Claude Code, and in what order?**

`WP 16` (v1.0 Readiness Hygiene) is the active software-development
track and the current consumer of Claude Code capacity. Everything
defined here — reference datasets, decision rules, templates, business
registers, commercial structures and teaching material — is content and
specification work. It can be produced by hand, in a spreadsheet, in a
document, or by a general-purpose assistant, on a different day, by a
different person, without touching `src/`, `tests/`, the build, or any
schema.

That independence is the point. This programme is designed so that
**not one of its forty sub-packages blocks, or is blocked by, `WP 16`**.

## 2. Labelling Convention

- Existing technical work keeps its **numeric** identifiers (`WP 16`,
  `WP 16.2A`, and every predecessor). Nothing in this document renames,
  renumbers, supersedes or re-scopes any of them.
- This parallel programme uses **letters**: Programmes **A**–**G**,
  sub-packages **`A.1`**, **`A.2`** … **`G.7`**.
- A letter identifier in this repository therefore means *parallel,
  non-code content work*; a number means *technical Work Package*.

### 2a. Disclosed Collision — `WP 7.2A`'s own Programme A–G

`docs/releases/v0.7.0/WP7.2A Recommended Programme.md` (2026-07-30)
already used the labels **Programme A**–**Programme G** for seven
*candidate technical programmes* it scored against one another —
Programme A being "Requirements & Verification Platform", Programme F
"Platform Hardening". Those labels are historical, closed, and belong to
that Work Package's own comparison exercise; they are not a live
register.

This is a genuine collision of vocabulary and is disclosed rather than
papered over. Two rules keep it harmless:

1. In this repository, work defined here is always written as
   **"Parallel Programme A"** … **"Parallel Programme G"** in prose, or
   cited by sub-package identifier (`A.3`, `C.2`), never as a bare
   "Programme A".
2. `WP 7.2A`'s own text is left exactly as written. A closed historical
   document is not retro-edited to accommodate later vocabulary.

Sub-package identifiers themselves (`A.1`–`G.7`) collide with nothing:
no numeric Work Package, ADR, `FCR`, `TD` or `D-` record uses that form.

## 3. Programme Index

| Programme | Title | Sub-packages | Claude Code required? | Later TempestOS integration? |
|---|---|---|---|---|
| **A** | Engineering Reference Data | `A.1`–`A.7` (7) | No | Yes — as seed data for the Materials and Engineering Data frameworks |
| **B** | Engineering Intelligence | `B.1`–`B.5` (5) | No | Yes — as rule content behind selection, review and trade-off features |
| **C** | Commercial Intelligence | `C.1`–`C.5` (5) | No | Yes — as supplier, cost and lead-time reference data |
| **D** | Business OS | `D.1`–`D.6` (6) | No | Partly — structures first; only some become product features |
| **E** | Engineering Assets | `E.1`–`E.5` (5) | No | Yes — as document, calculation and review templates |
| **F** | AI Knowledge & Academy | `F.1`–`F.5` (5) | No | Yes — as Academy and assistant-facing content |
| **G** | Business Governance & Scale | `G.1`–`G.7` (7) | No | Mostly not — business operating content, not product content |

**Forty sub-packages. Zero of them require Claude Code to produce.**
Where a sub-package's *output* is later imported into TempestOS, that
import is a separate, numbered technical Work Package, scoped under
`docs/governance/Future Work Package Guidelines.md` like any other — it
is never smuggled in as part of the content work itself.

## 4. Work-Package Execution Standard

Every sub-package in Programmes A–G is defined against the same ten
headings, in this order, with no heading omitted:

1. **Purpose** — the one question this sub-package answers.
2. **Scope** — what is in, and explicitly what is out.
3. **Required inputs** — what must exist or be gathered before starting.
4. **Data / content fields** — the actual columns, fields or sections
   the deliverable carries.
5. **Outputs / artefacts** — the named files or records produced.
6. **Acceptance criteria** — the objective test for "done".
7. **Dependencies** — other sub-packages or documents this one needs.
8. **Recommended next action** — the single next step, small enough to
   start the same day.
9. **Claude Code required?** — Yes / No, with the reason.
10. **TempestOS integration** — whether, and how, the output can later be
    imported into the product.

A sub-package that cannot state all ten honestly is not ready to start.
Where a field is genuinely not yet knowable, it is marked **Unknown**,
per `docs/governance/Governance Philosophy.md`'s own discipline — never
filled with a plausible guess.

## 5. Parallelisation Rule

1. **Prioritise work that needs no Claude Code**: reference datasets,
   specifications, templates, registers, business documents, rules and
   structured content.
2. **Do not introduce code changes, schema changes, migrations, new
   dependencies, or architecture changes into `WP 16`** as a side effect
   of this programme. Not one sub-package below authorises any of those.
3. **Build content and specifications first; integrate deliberately
   later.** A dataset that exists as a clean, complete CSV or table is
   more valuable — and cheaper to import once — than a half-imported
   dataset behind a half-built importer.
4. **Content correctness is the acceptance test, not schema fit.** If a
   dataset is right, an importer can always be written for it. The
   reverse is not true.

## 6. Recommended Order

**A → G → C → E → F → B**, with **D** run concurrently wherever it is
useful.

| Position | Programme | Why here |
|---|---|---|
| 1 | **A** — Engineering Reference Data | The foundation everything else cites. Materials, standards, fasteners, bearings and processes are referenced by Programmes B, C, E and F; producing them first stops the later programmes inventing their own incompatible vocabularies. |
| 2 | **G** — Business Governance & Scale | Highest commercial risk reduction per hour spent, and entirely independent of engineering content. Contracts, insurance, IP, rates and financial controls protect the business while the rest of the programme is still being written. |
| 3 | **C** — Commercial Intelligence | Needs `A.7` (Manufacturing Process Library) and `G.4` (Rate Card) to be meaningful; supplies the cost and lead-time reality that Programme B's trade-off logic depends on. |
| 4 | **E** — Engineering Assets | Templates and calculation packs are the first *visible* deliverable to a client; they consume Programme A's data and Programme G's commercial terms. |
| 5 | **F** — AI Knowledge & Academy | Teaching material and prompt libraries are best written once the data (A), the commercial reality (C) and the artefacts (E) they teach against exist. |
| 6 | **B** — Engineering Intelligence | Deliberately last. Selection logic, decision trees and trade-off frameworks are only trustworthy once they can cite real reference data (A), real cost and lead-time data (C), and real worked examples (F). Writing rules before data invites confident, unsourced rules. |
| Concurrent | **D** — Business OS | Structural work (CRM, project, finance, purchasing, quality, records) that neither blocks nor is blocked by the others. Run it whenever capacity exists, especially before the first real client engagement. |

**No date is committed for any sub-package.** This document sequences
work; it does not schedule it.

## 7. Status Discipline

Every sub-package carries one of four statuses, recorded in its own
programme file:

- **Defined, not started** — as written today, all forty.
- **In progress** — content actively being produced.
- **Complete** — acceptance criteria met, artefact exists.
- **Integrated** — output imported into TempestOS by a numbered
  technical Work Package, which is cited by identifier.

No sub-package is marked Complete on the strength of a plan. It is
marked Complete when the artefact its own "Outputs / artefacts" section
names actually exists and can be opened.

## 8. Programme Files

- [Parallel Programme A — Engineering Reference Data](Parallel%20Programme%20A%20—%20Engineering%20Reference%20Data.md)
- [Parallel Programme B — Engineering Intelligence](Parallel%20Programme%20B%20—%20Engineering%20Intelligence.md)
- [Parallel Programme C — Commercial Intelligence](Parallel%20Programme%20C%20—%20Commercial%20Intelligence.md)
- [Parallel Programme D — Business OS](Parallel%20Programme%20D%20—%20Business%20OS.md)
- [Parallel Programme E — Engineering Assets](Parallel%20Programme%20E%20—%20Engineering%20Assets.md)
- [Parallel Programme F — AI Knowledge & Academy](Parallel%20Programme%20F%20—%20AI%20Knowledge%20&%20Academy.md)
- [Parallel Programme G — Business Governance & Scale](Parallel%20Programme%20G%20—%20Business%20Governance%20&%20Scale.md)
