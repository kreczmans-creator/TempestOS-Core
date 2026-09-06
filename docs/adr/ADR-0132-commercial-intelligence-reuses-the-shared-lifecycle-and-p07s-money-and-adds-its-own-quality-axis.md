# ADR-0132: Commercial Intelligence Reuses the Shared Lifecycle and P07's Money, and Adds Its Own Quality Axis

## Status

Accepted — `Group D` (P03 Commercial Intelligence), 2026-09-06.

## Context

`P03` holds suppliers, process costs, lead times, estimates, quotes and
sourcing comparisons. Every one of them is authored by somebody, taken
from a source, checked, released, and eventually replaced — which is the
lifecycle `ADR-0126` already built for `Group A` and `ADR-0129` already
reused for `Group C`. Building a third would be the third place a bug in
supersession has to be fixed.

`P03` also holds money on almost every record. `ADR-0130` settled what
money is: an exact decimal in a stated currency, never a `Quantity`, never
converted. Introducing a second representation because commercial costs
"feel different" from contractual ones would give the platform two ways
to be wrong about a price.

But commercial data has a property engineering reference data does not.
A material's tensile strength does not go stale; a supplier's price from
eighteen months ago does, without anybody touching the record. The
lifecycle state says how far a record got through governance. It says
nothing about whether the figure is still true.

## Decision

**One lifecycle.** Every `P03` library derives from
`ReferenceDataCatalog<TDefinition>` and uses `ReferenceValidationState`
unchanged: Draft → Checked → Validated → Released → Superseded, with
released records immutable and superseded ones naming their replacement.

**One money.** `P03` uses `Money`, `CurrencyCode`, `EffectivePeriod` and
`CurrencyMismatchException` from `Tempest.Core.BusinessGovernance`
directly. It defines no monetary type of its own.

- `CostFigure` wraps `Money` rather than replacing it, adding only what
  `P03` needs: a certainty (`Unknown`, `Estimated`, `Ranged`, `Quoted`,
  `Exact`) and, for a range, both ends.
- Adding a known cost to an unknown one gives `Unknown`, because the total
  genuinely is unknown and returning the known part would report a number
  that is certainly too small.
- `CostFigure.Sum` of nothing is zero in the stated currency, not unknown:
  an estimate with no lines costs nothing, and that is a fact rather than
  an absence.
- Cross-currency arithmetic throws, exactly as in `P07`.

**A second, orthogonal axis: `CommercialQuality`.** `Invalid`,
`Incomplete`, `Unverified`, `Stale`, `Verified`, `NotApplicable`,
`Contradicted`.

- `IsDecisionGrade` is true only for `Verified`. Everything else,
  including a perfectly well-formed but unverified figure, is not
  something to price a job from without looking at it.
- Over a set, the weakest quality governs, and an empty set is
  `Incomplete` rather than `Verified` — the absence of contradicting
  evidence is not evidence.
- `Contradicted` exists because commercial data does something engineering
  data rarely does: two credible sources give different numbers, and
  neither is wrong to record.

## Consequences

**A released `P03` record can be worthless**, and the model can say so. A
Released, Verified-provenance cost record whose validity period ended last
year is `Stale`, and the two states do not contradict each other.

**Fixing supersession fixes it everywhere.** `P01`, `P02`, `P07` and `P03`
share one implementation.

**Currency-mixed commercial data throws rather than totalling**, which
surfaces at estimate level as a validation error naming both currencies
rather than a plausible wrong number.
