# P04 — Business OS: Completion Report

**Programme:** P04 — Business OS
**Work Packages:** WP04.1 – WP04.6
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Programme status — the honest facts

| | State |
|---|---|
| **Framework** | Foundation skeleton complete. Nine governed libraries, 17 services, all registered in the real host. |
| **Authoritative data** | **None.** Every library ships empty. |
| **Fictional test data** | Present, test-project only, marked throughout. |
| **Integration** | **Deliberately absent.** No UI, no external system, no workflow engine. |

| Gate | Result |
|---|---|
| Build, Debug | 0 errors, 0 warnings |
| Tests, Debug | **4,867 / 4,867** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Governance health check | **13 passed, 3 warned, 0 failed** of 16 |

`P04` added **78 tests** (4,789 → 4,867).

---

## 1. What the audit found, and how it changed the work

`P04` was commissioned as six missing packages. The 40-work-package audit
found something different:

| WP | Audit finding | What was built |
|---|---|---|
| WP04.1 | **Partial** — `P07` held organisation and contact as free text | `Organisation`, `Contact`, `Interaction` |
| WP04.2 | **Complete elsewhere** — `Tempest.App/Projects`, 17 files | One relationship kind and one register. **No project model.** |
| WP04.3 | **Partial** — `P07`'s `C5` held forecasting | `Budget`, `FinancialEntry`, `BudgetPosition` |
| WP04.4 | **Missing** — `P03` held the reference layer only | `PurchaseRequisition`, `PurchaseOrder` |
| WP04.5 | **Missing** | `NonConformance`, `Disposition`, `QualityAction` |
| WP04.6 | **Partial** — documents existed, business records did not | `BusinessRecord`, `RetentionTerms` |

**`P04` is thin, and that is the measure of the other six programmes.**
Most of what a Business OS needs was already built. `ADR-0142` records the
resulting rule: `P04` owns no model another programme already holds, and
three reflection tests enforce it.

---

## 2. WP04.2 — the package that was already built

`Tempest.App/Projects` implements the project directory, membership, tasks
with work state and assignment, milestones, deliverables, contributions,
and the requirement, document and governance registers.

The audit found **exactly one gap**: task-to-task dependency. Closed by:

- `TaskRelationshipKinds.DependsOn`, added to the canonical vocabulary
  class where `ADR-0105`'s one-value-one-owner rule puts it;
- `ProjectDependencyRegister`, a read side following
  `ProjectMilestoneRegister`'s shape exactly.

A dependency states sequence, not permission. `IsStartedOutOfSequence`
reports work begun before its predecessor finished rather than preventing
it, and a dependency on a task outside the project is left untouched
rather than shown with no name.

**No project model was built in `P04`**, and a reflection test fails if
one ever is.

---

## 3. The three refusals

Each is a validation **error**, not a warning.

| Refusal | Why |
|---|---|
| An order recorded as placed with nobody named as having placed it | It commits the business's money and nobody is accountable. `P03` never orders (`ADR-0135`); `P04` records that a person did. |
| A concession with no stated justification | Accepting something out of specification is a deliberate decision, and one with no reason cannot be defended to a customer or an auditor. |
| A non-conformance closed while the problem stands | Closing a record and solving a problem are different acts. A system letting the first stand for the second produces a clean register and a recurring fault. |

`INonConformanceCatalog.FindClosedButUnresolvedAsync` surfaces the third
across the whole library — the report a quality system exists to produce
and rarely does.

---

## 4. The two things the platform will not compute

**No accounting.** No chart of accounts, no tax rules, no VAT, no ledger,
no double entry. `TaxRegistration` is recorded because an invoice needs
it; nothing is computed from it. A budget answers "how much is left?" and
stops.

**No retention law.** Statutory periods differ by jurisdiction, record
type and year, and shipping them would be legal advice the platform cannot
stand behind. `RetentionTerms` records the period the *organisation*
decided and its basis, reports where nothing has been decided, and
**deletes nothing** — a test asserts a record past its retention date is
still there afterwards.

---

## 5. Two design decisions worth stating

**Budget position measures against commitments, not payments.** A business
measuring against what it has paid discovers its overspend when the
invoices arrive. `FinancialPosture.Committed` deliberately includes
accruals and actuals: counting only entries still marked `Committed` would
show a budget refilling as invoices arrive, which is the opposite of the
truth.

**Entries in another currency are excluded and counted, never converted.**
Dropping them silently would understate the spend; converting them would
invent a rate. The same refusal `ADR-0130` made for `P07`.

---

## 6. Tests

78 tests: operational core behaviour, per-package behaviour, adversarial
tests, the full create/revise/retrieve-historical/supersede cycle against
the real document-backed store, a blanket JSON round-trip guard over every
`P04` type, and host registration proving all 17 services compose in the
real unmodified `TempestHost`.

Four structural guards hold the audit's conclusions in place: no second
project model, no second money representation, no method that places an
order or disposes of a record, and no shared document kind between a
business record and a technical document.

No new persistence defect was found. The round-trip guard exists so one
cannot arrive later unnoticed.

---

## 7. Registers

| Register | Before | After | Change |
|---|---|---|---|
| ADR Register | 141 | 142 | `ADR-0142` |
| Architecture Document Register | 45 | 46 | `P04 Business OS.md` |
| Namespace Register | 87 | 93 | Six `P04` namespaces; `Tempest.App.Projects` 17 → 18 |
| Interface Register | 293 | 310 | Seventeen `P04` interfaces |
| Governance Index | 141 ADRs stated | 142 | Corrected; audit document linked |
| Exception Register | 99 | 99 | Unchanged — `P04` declares no new exception type |

**One ADR, and one is the right number.** `P04`'s six packages embody a
single architectural decision — that `P04` owns no model another programme
holds — together with the refusals and non-computations that follow from
it.

---

## 8. Known gaps and deferred work

**No operational data.** §0.

**No workflow.** Requisition → sourcing → authority → order is a sequence
of *states a person sets*, not a process the platform drives. Nothing
routes, notifies or escalates.

**No UI.** Deliberately.

**`IProjectDependencyRegister` is not in the Interface Register**, because
it lives in `Tempest.App` and that register's declared scope is
`src/Tempest.Core/`. Stated here rather than silently omitted.

**Budget position ignores incoming money.** `CashDirection.Incoming`
exists on the model and `BudgetPosition` totals outgoing only. Modelling
receivables properly needs invoicing, which is the accounting `P04`
declines to do.

**Party resolution is one-way.** A `PurchaseOrder` points at a `P03`
supplier; nothing indexes orders by supplier. A real purchasing view would
want that.

**The CRM does not link back to `P07` opportunities.**
`Opportunity.ExternalOrganisationId` now has something to point at, but
`P07` was not modified to point at it — that would be changing a completed
programme, which the remediation scope forbids. The link is available and
unused.

---

## 9. Git

| Commit | Subject |
|---|---|
| `3562333` | Audit: all 40 original work packages against the roadmap |
| `bc930c1` | P04 WP04.1 CRM structure, and WP04.2's one genuine gap closed |
| `e1df53f` | P04 WP04.3 finance structure and WP04.4 purchasing operations |
| `e1d915b` | P04 WP04.5 quality management and WP04.6 document and records management |
| `4f0fb13` | P04: tests and host registration |

Branch: `claude/tempestos-a4-bearing-library-unobtf`.
