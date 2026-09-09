# ADR-0134: An Estimate, a Supplier Quote, a Customer Quotation and an Actual Are Four Types, Not Four States

## Status

Accepted — `Group D` (P03 Commercial Intelligence), 2026-09-06.

## Context

Four things circulate around a job and are constantly confused:

| | Who asserted it | Who is bound |
|---|---|---|
| **Estimate** | The organisation, from its own records | Nobody |
| **Supplier quote** | A supplier | The supplier, if firm and current |
| **Customer quotation** | The organisation | The organisation, once issued |
| **Actual** | What happened | — |

The tempting design is one `Quotation` type with a `Kind` or a `Status`
enumeration. It is tempting because the fields overlap: all four have
lines, a total, a currency and a date.

It is wrong because the fields that do *not* overlap are the ones that
matter. Only an estimate has assumptions and source pins. Only a supplier
quote has a firmness and a supplier's own reference. Only a customer
quotation can be issued, and issuing binds the organisation. Only an
actual has evidence of payment. A single type either carries all of them
as nullable — in which case nothing enforces that an issued offer names
who issued it — or carries none, in which case the model records less
than the business knows.

And the failure mode is expensive. An estimate read as a quotation is a
price sent to a customer with the margin left out. A supplier's price read
as a selling price is a job sold at cost.

## Decision

Four types, in four libraries, with four document kinds.

- `CostEstimate` (`CommercialCostEstimates`) — derived from records the
  organisation holds. Every line carries `SourcePins`: the exact library,
  record and revision it came from. Assumptions are first-class, because
  an estimate that turns out wrong is usually an estimate whose assumption
  turned out wrong.
- `SupplierQuote` (`CommercialSupplierQuotes`) — what a third party
  offered. `QuoteFirmness` runs `Indicative → Budgetary → Firm →
  FirmAgainstSpecification`; `IsBindingAt(date)` is a method rather than a
  property, because whether a quote still stands depends on when you ask.
- `CustomerQuotation` (`CommercialCustomerQuotations`) — what the
  organisation offered. Carries `QuotationStatus` as a second axis from
  the record lifecycle, on `ADR-0129`'s reasoning: a released, validated
  record *of a draft quotation* must be expressible.
- `RealisedOutcome` — what it actually cost, recorded against the estimate
  it tests, so "how good are our estimates?" is answerable at all.

**Reproducibility is what the pins are for.** Re-reading a two-year-old
estimate resolves the figures that were actually used, not whatever the
cost library says today. `EstimatingService.ReproduceAsync` re-reads every
pin and reports where the libraries have moved; it never alters the
estimate. Superseding a source raises a warning about the library, not a
correction to history.

## Consequences

**Some duplication across the four types**, in lines, totals and
validity. Accepted deliberately: the alternative is a type whose
invariants cannot be stated because they differ per state.

**Converting an estimate into a quotation is an explicit act**, not a
status change. `CustomerQuotation.EstimatePin` records which estimate at
which revision the price rested on, and `MarginOver` computes the margin
from the estimate's *highest* figure so a ranged estimate yields the safe
margin rather than the flattering one.

**A comparison across the four is always possible and never implicit.**
`RealisedOutcome.VarianceFrom` needs both figures in the same currency
and returns `null` otherwise, rather than a percentage against nothing.
