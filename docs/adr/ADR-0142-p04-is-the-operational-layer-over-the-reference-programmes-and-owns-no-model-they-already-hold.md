# ADR-0142: P04 Is the Operational Layer Over the Reference Programmes, and Owns No Model They Already Hold

## Status

Accepted — `P04` (Business OS), 2026-09-06.

## Context

`P04` arrived last, after `P01`, `P02`, `P03`, `P05`, `P06` and `P07` were
complete. The 40-work-package audit found that three of its six packages
were **partial rather than missing** and one was **already built
elsewhere**:

| WP | What already existed |
|---|---|
| WP04.1 CRM | `P07`'s `Opportunity` carried organisation and contact as free text |
| WP04.2 Projects | `Tempest.App/Projects` — 17 files: directory, membership, tasks, milestones, deliverables, contributions, registers |
| WP04.3 Finance | `P07`'s `C5` — assumptions and scenarios, i.e. forecasting |
| WP04.6 Records | `EngineeringData` documents; `P05`'s `E5` technical documents |

The obvious way to build a "Business OS" is to build a business OS: a
project model, a money type, a document store, a supplier list. Every one
of those already exists in this repository, and building a second of any
of them would mean two answers to one question, diverging from the first
write.

There is also a hard boundary to keep. `ADR-0135` says `P03` compares
suppliers, recommends, and never places an order. `P04` is where ordering
lives — and the point of `ADR-0135` survives only if `P04` *records* what
a person did rather than doing it.

## Decision

**`P04` is an operational layer and owns no model another programme
holds.**

| `P04` needs | It uses | It does not build |
|---|---|---|
| Money | `P07`'s `Money`, `CurrencyCode`, `EffectivePeriod` | A second money type |
| Authority | `P07`'s `BusinessAuthorisation` | A second approval model |
| Projects, tasks, milestones | `Tempest.App/Projects` | Any project model at all |
| Suppliers, quotes, comparisons | `P03`, by record Id and `ReferencePin` | A second supplier database |
| Documents | `EngineeringData`; relationships via `P05`'s `DocumentRelationship` | A second document store |
| Failure causes | `P06`'s `FailureCause` | A second cause model |
| Evidence | `P05`'s `EngineeringEvidence` | A second evidence type |

Enforced by reflection tests: no type under
`Tempest.Core.BusinessOperations` may be named `Project*`, `Task`,
`Milestone`, `Deliverable`, `Money`, `Currency*` or `Amount`.

**WP04.2 adds one thing and no model.** The audit found exactly one gap in
the existing project architecture: task-to-task dependency. Closed by
adding `DependsOn` to the canonical `TaskRelationshipKinds` — where
`ADR-0105`'s one-value-one-owner rule puts it — plus a read-side
`ProjectDependencyRegister` following `ProjectMilestoneRegister`'s shape.
A dependency is a statement about sequence, not permission: nothing
prevents a task starting while its predecessor is open, and
`IsStartedOutOfSequence` reports it.

**What `P04` does own**, because nothing else did:

- **`Organisation`, `Contact`, `Interaction`** — the record `P07`'s
  opportunities had nothing to point at. `Interaction` is distinct from
  `OpportunityInteraction` because most contact is not about an
  opportunity, and a CRM that can only record pursuit misses most of the
  relationship.
- **`Budget` and `FinancialEntry`** — `C5` models a possible future, a
  budget is a decision about the present somebody is accountable for.
  `FinancialPosture` (budgeted, committed, accrued, actual) is one type at
  four moments in the money's life rather than four types an amount gets
  copied between, which is how totals stop agreeing.
- **`PurchaseRequisition` and `PurchaseOrder`** — the requisition carries
  the engineering need and pins the `P03` comparison a person acted on;
  the order records what was agreed with whom.
- **`NonConformance`, `Disposition`, `QualityAction`** — three separate
  things. The disposition decides what happens to *this* part; the
  corrective action decides what happens so there is no next one.
- **`BusinessRecord`** — invoices, certificates, correspondence, distinct
  from `E5`'s technical documents and sharing no document kind.

**Three refusals, each an error.**

1. **An order recorded as placed with nobody named as having placed it.**
   It commits the business's money and nobody is accountable. `P03` never
   orders (`ADR-0135`); `P04` records that a person did.
2. **A concession with no stated justification.** Accepting something out
   of specification is a deliberate decision, and one with no reason
   cannot be defended to a customer or an auditor.
3. **A non-conformance closed while the problem stands.** Closing a record
   and solving a problem are different acts, and a system that lets the
   first stand for the second produces a clean register and a recurring
   fault.

**Two things the platform will not compute.**

- **No accounting.** No chart of accounts, no tax rules, no VAT, no
  ledger, no double entry. A budget answers "how much is left?" and stops.
- **No retention law.** Statutory periods differ by jurisdiction, record
  type and year; shipping them would be legal advice the platform cannot
  stand behind. `RetentionTerms` records the period the *organisation*
  decided and its basis, reports where nothing has been decided, and
  deletes nothing.

## Consequences

**`P04` is thin, and that is the measure of the other programmes.** Most
of what a Business OS needs was already built; `P04` supplies the
operational records that sit on top and nothing beneath them.

**Reading a `P04` record fully needs the libraries it points at.** A
purchase order names a supplier by `P03` record Id and a quote by pin.
Accepted for the same reason `ADR-0137` accepted it for `P05`: a
self-contained record is a second copy of facts that will drift.

**Budget position is measured against commitments, not payments.** A
business measuring against what it has paid discovers the overspend when
the invoices arrive. Entries in another currency are excluded and counted,
never converted — dropping them understates the spend, converting them
invents a rate.

**`FinancialPosture.Committed` includes accruals and actuals.** Counting
only entries still marked `Committed` would show a budget refilling as
invoices arrive, which is the opposite of the truth.
