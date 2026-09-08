# Original Roadmap — 40 Work Package Audit

**Date:** 2026-09-06
**Method:** evidence from source, interfaces, persistence, tests, DI
registration, documentation and registers. Not from filenames, and not
from completion reports.

---

## 1. Why this audit exists

Work has been delivered under **group letters** (`Group A`–`Group F`) that
do not match the **programme identifiers** (`P01`–`P07`). The audit exists
to establish what is actually built, independent of what it is called.

**The mapping, confirmed:**

| Group | Programme | Packages |
|---|---|---|
| A | P01 | A1–A7 = WP01.1–WP01.7 |
| **B** | **P02** | **B1–B5 = WP02.1–WP02.5** |
| C | P07 | C1–C7 = WP07.1–WP07.7 |
| D | P03 | D1–D5 = WP03.1–WP03.5 |
| E | P05 | E1–E5 = WP05.1–WP05.5 |
| F | P06 | F1–F5 = WP06.1–WP06.5 |
| — | P04 | WP04.1–WP04.6 |

`Group B` is `P02`. A reader looking for "P02" in the source finds
`Tempest.Core.EngineeringIntelligence` and no letter G.

---

## 2. Matrix

### P01 — Engineering Reference Data (Group A)

| WP | Subject | Namespace | Status |
|---|---|---|---|
| WP01.1 | Materials Database | `Materials` (16 files, tests) | **Complete** |
| WP01.2 | Standards Library | `Standards` (17) | **Complete** |
| WP01.3 | Fastener Library | `Fasteners` (19) | **Complete** |
| WP01.4 | Bearing Library | `Bearings` (25) | **Complete** |
| WP01.5 | Springs, Gears & Components | `Components` (16) | **Complete** |
| WP01.6 | Constants & Fundamentals | `Constants` (12) | **Complete** |
| WP01.7 | Manufacturing Process Library | `Manufacturing` (15) | **Complete** |

All seven on `ReferenceDataCatalog<T>`, all registered in `TempestHost`.

### P02 — Engineering Intelligence (Group B)

| WP | Group | Namespace | Status |
|---|---|---|---|
| WP02.1 | B1 | `EngineeringIntelligence.MaterialSelection` | **Complete** |
| WP02.2 | B2 | `.Decisions` | **Complete** |
| WP02.3 | B3 | `.DesignRules` | **Complete** |
| WP02.4 | B4 | `.Reviews` | **Complete** |
| WP02.5 | B5 | `.TradeStudies` | **Complete** |

56 files, 14 interfaces, 9 test files. Verified against the G1–G5
requirements: material selection consumes `P01` Materials and never
duplicates it; decision trees reference the `A7` process library;
`RuleSeverity` distinguishes prohibition, requirement, constraint,
warning, recommendation and advisory; review logic reports findings
without deciding; `RequiresHumanDecision` is present throughout and
unconditional on `MaterialAssessment` and `ProcessScreening`.

**One observation, not a defect.** `P02`'s trade-off framework carries no
numerical weighting at all — `TradeStudyJudgement` records a per-criterion
outcome with a mandatory reason, and standing derives from eliminating
judgements rather than a score. This is a stronger reading of "no magical
best-design score" than `P03`'s weighted comparison, and the two
programmes therefore differ in approach. Sensitivity analysis is absent.
Raised to the review board as an observation.

### P03 — Commercial Intelligence (Group D)

| WP | Group | Status |
|---|---|---|
| WP03.1–WP03.5 | D1–D5 | **Complete** |

29 files, 19 interfaces. `CostFigure` persistence defect found and fixed
during delivery.

### P04 — Business OS

| WP | Subject | Evidence | Status |
|---|---|---|---|
| WP04.1 | CRM Structure | `P07` `Opportunity` carries organisation *name*, contact *name* and interactions as free text on the opportunity. No governed organisation or contact record exists. | **Partial** |
| WP04.2 | Project Management | `Tempest.App/Projects` — directory, membership, tasks with work state and assignment, milestones, deliverables, contributions, requirements register, document register, governance register. 17 files. | **Complete (elsewhere)** |
| WP04.3 | Finance Structure | `P07` `C5` has assumptions and scenarios (forecasting). No budget, actual or commitment record exists. | **Partial** |
| WP04.4 | Supplier & Purchasing Ops | `P03` provides the full reference layer. No operational purchasing seam exists. | **Missing** |
| WP04.5 | Quality Management | `P05` `E3` covers verification evidence. No non-conformance, corrective or preventive action record exists. | **Missing** |
| WP04.6 | Document & Records Mgmt | `EngineeringData` holds documents; `P05` `E5` governs technical documents. No business record with classification and retention exists. | **Partial** |

### P05 — Engineering Assets (Group E)

| WP | Group | Status |
|---|---|---|
| WP05.1–WP05.5 | E1–E5 | **Complete** |

15 files, 11 interfaces, 215 tests. Two defects found and fixed during
delivery.

### P06 — AI Knowledge & Academy (Group F)

| WP | Group | Status |
|---|---|---|
| WP06.1–WP06.5 | F1–F5 | **Complete** |

13 files, 10 interfaces, 177 tests.

### P07 — Business Governance & Scale (Group C)

| WP | Group | Status |
|---|---|---|
| WP07.1–WP07.7 | C1–C7 | **Complete** |

38 files, 27 interfaces. `Money` persistence defect found and fixed during
delivery.

---

## 3. Two findings that change the remediation scope

**WP04.2 is already built, and must not be rebuilt.** `Tempest.App/Projects`
implements projects, tasks, milestones, deliverables, contributions,
membership and the requirement and document registers. The remediation
instruction says "reuse existing project architecture; do not create a
competing project system", and the correct action is therefore **no new
project model**. The one genuine gap is task-to-task dependency, which is
recorded below.

**Three of the six P04 packages are partial rather than missing.** `P07`
and `P05` already own the governance and reference layers for CRM,
finance and documents. What is absent in every case is the **operational**
layer: the organisation record behind an opportunity's free-text name, the
budget an actual is measured against, the business record with a retention
period. `P04`'s job is that layer and nothing beneath it.

---

## 4. Remediation scope agreed from this audit

| WP | Action |
|---|---|
| WP04.1 | Build governed `Organisation`, `Contact` and `Interaction`; link `P07`'s opportunity by its existing `ExternalOrganisationId`. |
| WP04.2 | **No new model.** Verify and record; add task dependency only if it proves genuinely absent. |
| WP04.3 | Build `Budget`, commitment and actual, reusing `P07` `Money`. Do not touch `C5` forecasting. |
| WP04.4 | Build the purchasing seam referencing `P03` by identity. |
| WP04.5 | Build non-conformance, corrective and preventive action, inspection and disposition. |
| WP04.6 | Build the business record with classification and retention over the existing document infrastructure. |

Everything else in the 40 is complete and is not to be touched.

---

## 5. Position before remediation

**34 of 40 work packages complete.** Six `P04` packages outstanding, of
which one is complete elsewhere and three are partial.
