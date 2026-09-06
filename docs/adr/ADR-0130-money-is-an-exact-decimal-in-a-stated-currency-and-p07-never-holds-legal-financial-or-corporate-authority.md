# ADR-0130: Money Is an Exact Decimal in a Stated Currency, and P07 Never Holds Legal, Financial or Corporate Authority

## Status

Accepted — `Group C` (P07 Business Governance & Scale), 2026-09-06.

## Context

Two decisions are recorded together here because they are the same
decision seen from two ends: what `P07` is allowed to compute, and what it
is allowed to conclude.

**On money.** TempestOS already has a quantity system —
`Quantity<TDimension>`, dimensioned, unit-aware, with conversion factors.
Reaching for it to represent money is the obvious move and the wrong one.
Currency is not a physical dimension: there is no factor between pounds
and euros that is true independently of a date and a market, so a
"currency unit" would be a conversion waiting to silently produce a
number nobody can defend. Separately, `Quantity` carries a `double`, and
binary floating point cannot represent 0.10 exactly. An invoice line, a
rate and a forecast are exact to the minor unit.

**On authority.** Business governance software is where an ordinary
convenience becomes a claim of authority. A field called `IsApproved` that
anybody can set. A `Recommend()` that returns a supplier. A risk matrix
whose green cell reads as acceptance. A dashboard whose threshold, once
crossed, is treated as the decision. Each is a small step, and each ends
with software having approved a contract, accepted a risk, certified
compliance or committed the organisation — none of which it can be
accountable for.

## Decision

### Money

`Money` is a `readonly record struct` of a `decimal` amount and a
`CurrencyCode`, in `Tempest.Core.BusinessGovernance`. It is not a
`Quantity`, and `UnitsAndQuantities` is not extended to carry currency.

- Every operation is decimal arithmetic. No `double` appears anywhere in
  `P07`'s monetary path.
- Arithmetic and comparison across currencies throw
  `CurrencyMismatchException` rather than converting. Converting needs a
  rate and a date, neither of which this platform holds; refusing is the
  only honest answer, and it is the same discipline `ADR-0125` applied to
  affine units.
- `CurrencyCode` validates shape only — three ASCII letters — and is not
  checked against a registry, because no currency registry ships with
  TempestOS and pretending otherwise would assert something unverified.
- `Money.Sum` takes the currency explicitly, so an empty sequence still
  answers in a stated currency rather than throwing or guessing.
- Rounding is banker's rounding, the convention accounting systems use,
  and is never applied implicitly.

### Authority

**No `P07` type or service performs an act of business authority.** Every
such act is a `BusinessAuthorisation` — kind, person, capacity, date,
basis — that a caller acting for a named person constructs, and that
refuses construction without all of them. `P07` records these and reports
their absence; it creates none.

Concretely, and enforced by reflection tests:

| The organisation must | `P07` does |
|---|---|
| Execute a contract | Prepare a draft with the commitment it will need stated as an outstanding authority |
| Accept a risk | Record an acceptance a named person made, changing nothing else about the risk |
| Approve a rate card | Refuse to quote from one nobody approved |
| Determine IP ownership | Default to `NotDetermined` and report an unevidenced position as an assertion |
| Determine compliance | Record `ReviewRequired` and whose review it is |
| Decide to hire | Report that a threshold was crossed, naming who is asked to consider what |
| Confirm insurance responds | Report `PolicySupportsClaim` — a current, evidenced policy with a stated limit — and no stronger value exists |

`DeterminationState.ReviewRequired` is the mechanism that keeps this
platform out of legal and accounting practice. Where a determination
belongs to a solicitor, an accountant or an insurer, `P07` records whose
it is and that it has not been made.

## Consequences

**A `P07` result is often less than an answer**, and deliberately. "No
policy document is held, so the organisation could not demonstrate the
cover it believes it has" is less satisfying than "insured" and is the
only one of the two that is true of the records.

**Currency-mixed data throws rather than totalling.** A caller holding
two currencies must handle that explicitly — a second rate card, a second
scenario, or a conversion the caller performs with a rate it can defend.

**No monetary value can be silently lost or approximated.** A defect
found during testing proved the value of stating this: `Money` did not
round-trip through the reference-data serialiser, so every persisted
contract value came back as zero in an unspecified currency. A
`JsonConstructor` and a `CurrencyCode` converter close it, and a
round-trip test guards it.

**Adding an approval to `P07` is deliberately awkward.** Constructing a
`BusinessAuthorisation` requires naming a person and their capacity and
what they relied on. That friction is the point: an approval that is easy
to fabricate is an approval that means nothing.

## Alternatives considered

**A `Currency` dimension in `UnitsAndQuantities`.** Rejected: it would
put a conversion factor between GBP and EUR into the type system, and any
factor there is wrong on most days.

**`decimal` alone, with the currency held alongside by convention.**
Rejected: it is exactly the arrangement that lets a euro amount be added
to a sterling one, and the mistake is invisible until the total is wrong.

**An `IsApproved` boolean, with the approver in a note field.** Rejected:
it makes fabricating an approval a single keystroke and makes "who
approved this, in what capacity, on what basis?" unanswerable.

**Blocking a decision that departs from what the records support.**
Rejected, consistent with `ADR-0127`: an engineer or a director may
overrule the system, and one that forbids it will be worked around.
Recording and flagging the departure is what matters.
