# P04 — Business OS

**Programme:** P04 — Business OS
**Namespace:** `Tempest.Core.BusinessOperations` (and `Tempest.Workspace/Projects` for `WP04.2`)
**Governing ADR:** `ADR-0142`
**Status:** Foundation skeleton complete. Every library ships **empty** —
see §9.

---

## 1. Purpose

Every other programme establishes what is *true*, what *follows*, what
things *cost*, what the business is *committed to*, what engineering work
*produces*, and what the organisation *knows*. `P04` establishes what the
business is **doing** — who it is dealing with, what it has promised to
spend, what it has ordered, what went wrong, and what it has to keep.

| WP | Question | Where it lives |
|---|---|---|
| WP04.1 | Who are we dealing with, and what has passed between us? | `.Crm` |
| WP04.2 | What work is planned, and what is waiting on what? | `Tempest.Workspace/Projects` |
| WP04.3 | What did we allow, and how much of it is gone? | `.Finance` |
| WP04.4 | What did we ask for, and what did we order? | `.Purchasing` |
| WP04.5 | What did not conform, and is it actually fixed? | `.Quality` |
| WP04.6 | What must we keep, and for how long? | `.Records` |

---

## 2. The shape of the programme

`P04` arrived last, and the audit found it was mostly **already built** —
in pieces, under other programmes. `ADR-0142` records the resulting rule:
**`P04` owns no model another programme holds.**

| It needs | It uses |
|---|---|
| Money, authority, effectivity | `P07` |
| Projects, tasks, milestones | `Tempest.Workspace/Projects` |
| Suppliers, quotes, comparisons | `P03`, by Id and `ReferencePin` |
| Documents, relationships, evidence | `EngineeringData` and `P05` |
| Failure causes | `P06`'s `FailureCause` |

Reflection tests enforce it: no type in the namespace may be named
`Project*`, `Task`, `Milestone`, `Deliverable`, `Money`, `Currency*` or
`Amount`.

---

## 3. The shared core

**`OperationalState`** — open, in progress, awaiting external, awaiting
internal, closed, cancelled. A second axis from
`ReferenceValidationState`, so a released validated record of an open
non-conformance is expressible.

**`PartyReference`** — the type that stops `P04` growing a second supplier
database. It points at a `P03` supplier or a `P04` organisation, and where
the party is neither, carries the name with `IsResolved` false. That case
is not a defect to prevent: a quotation arrives from a company nobody has
entered yet, and refusing to record it is how operational software becomes
the thing people work around.

**`OperationalFacts`** — state, owner, originator, dates, project,
classification, evidence, pins. `IsIncompletelyClosed` catches the same
failure in two shapes: closed with no date, cancelled with no reason.

---

## 4. WP04.1 — CRM

`Organisation` is the record `P07`'s `Opportunity.OrganisationName` had
nothing to point at. An organisation that is also a supplier links the
`P03` record; `Roles` is a list because one company can be both.

`Interaction` is deliberately distinct from `OpportunityInteraction`. Most
contact is not about an opportunity — a courtesy call, a technical
question, a complaint — and a CRM that can only record pursuit misses most
of the relationship.

Business contact details only. `P07`'s `C3` owns data protection; `P04`
holds a name, a role, a work address, a work number. 14 diagnostics,
`TEMPEST-BOC-001`–`014`.

---

## 5. WP04.2 — Project management

**Satisfied by `Tempest.Workspace/Projects`**, which already holds the
directory, membership, tasks with work state and assignment, milestones,
deliverables, contributions, and the requirement, document and governance
registers.

`P04` added exactly one thing the audit found missing: task-to-task
dependency, as `TaskRelationshipKinds.DependsOn` plus a read-side
`ProjectDependencyRegister`. A dependency states sequence, not permission
— `IsStartedOutOfSequence` reports work begun before its predecessor
finished rather than preventing it.

---

## 6. WP04.3 — Finance

`Budget` is the operational counterpart to `C5` forecasting: a scenario is
a view of a possible future, a budget is a decision about the present
somebody is accountable for. A budget nobody set is a validation
**error**.

`FinancialPosture` — budgeted, committed, accrued, actual — is one type at
four moments in the money's life, not four types an amount gets copied
between as it progresses.

`BudgetPosition` measures remaining against **commitments, not payments**.
A business measuring against what it has paid discovers its overspend when
the invoices arrive. Entries in another currency are excluded and counted,
never converted.

**Not accounting.** No chart of accounts, no tax rules, no ledger, no
double entry. 13 diagnostics, `TEMPEST-BOF-001`–`013`.

---

## 7. WP04.4 — Supplier and purchasing operations

The seam over `P03`. A `PurchaseRequisition` carries the engineering need
and pins the sourcing comparison a person acted on; a `PurchaseOrder`
records what was agreed with whom. Keeping them apart means the need
survives a change of supplier.

**TempestOS still does not place orders.** `P03` compares and recommends
(`ADR-0135`); `P04` records the order a person raised. An order recorded
as placed with nobody named as having placed it is an **error** — it
commits the business's money and nobody is accountable.

Over-delivery is recorded, not refused. A system that will not record it
produces a receipt note nobody can file. 15 diagnostics,
`TEMPEST-BOP-001`–`015`.

---

## 8. WP04.5 and WP04.6 — Quality and records

`WP04.5` keeps three things apart: the **non-conformance** (what was
wrong), the **disposition** (what happens to the affected item), and the
**corrective action** (what happens so there is no next one). A business
conflating the last two reworks the same fault for years.

Two errors: a concession with no justification, and a record closed while
the problem stands. `FindClosedButUnresolvedAsync` surfaces the second —
the report a quality system exists to produce and rarely does. Causes
reuse `P06`'s `FailureCause`. **No compliance claim is made.** 12
diagnostics, `TEMPEST-BOQ-001`–`012`.

`WP04.6` holds business records — invoices, certificates, correspondence —
distinct from `E5`'s technical documents and sharing no document kind.
**The platform holds no retention law**: statutory periods differ by
jurisdiction, record type and year. `RetentionTerms` records the period
the organisation decided and its basis, reports where nothing has been
decided, and **deletes nothing**. 10 diagnostics,
`TEMPEST-BOR-001`–`010`.

---

## 9. What ships

**Every library ships empty.** No customer, no supplier relationship, no
budget, no order, no non-conformance, no record. All of it is the
organisation's own operational data.

Test fixtures are fictional and marked: "Fictional Client Ltd", "Notional
Machining Ltd", registration number `00000000`, no monetary figure
describing any real transaction. They live only in the test project.

---

## 10. Dependencies

`P04` depends on `P03`, `P05`, `P06`, `P07`, the shared reference-data
layer, and `Tempest.Workspace/Projects`. Nothing depends on `P04`.

Every cross-library collaborator in validation is **optional**, so an
operational record is recordable and checkable before the thing it cites
is registered.
