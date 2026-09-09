# Group D — Commercial Intelligence

**Programme:** P03 — Commercial Intelligence
**Namespace:** `Tempest.Core.CommercialIntelligence`
**Governing ADRs:** `ADR-0131`, `ADR-0132`, `ADR-0133`, `ADR-0134`, `ADR-0135`
**Status:** Architecturally complete, `Group D`. Every library ships
**empty** — see §11.

---

## 1. Purpose

`P01` established what is true about materials, standards and processes.
`P02` established what follows from them. `P07` established what the
organisation is committed to and exposed to. `P03` establishes what
things cost, who can make them, how long they take, and what the
organisation should therefore do about it.

Five questions, and a sixth about every answer:

| | Question | Package |
|---|---|---|
| 1 | Who could make this, and what has anybody established about them? | `D1` / `WP03.1` |
| 2 | What does this process cost, from whom, at what quantity, and when was that true? | `D2` / `WP03.2` |
| 3 | How long does it take, and how firmly is that stated? | `D3` / `WP03.3` |
| 4 | What will this job cost us, what were we offered, and what did we offer? | `D4` / `WP03.4` |
| 5 | Which supplier should we use, and what do we not know? | `D5` / `WP03.5` |
| 6 | *Where did that answer come from, and would we still give it today?* | all |

The sixth is the one that makes the other five worth holding. A price
with no source is a rumour; an estimate that silently changes when its
sources are revised is a record of nothing.

---

## 2. What P03 is not

`P03` is deliberately narrow, and several nearby things are explicitly
out of scope.

- **Not an ERP or purchasing system.** No purchase orders, no goods
  receipts, no purchase ledger, no approval workflow. `P03` informs
  procurement; it does not run it.
- **Not accounting.** Actual costs are recorded against the estimate they
  test, so estimating accuracy is measurable. They are not a general
  ledger.
- **Not a CRM.** Customer relationships, opportunities and the sales
  pipeline are `P04` and `P07`'s `C6`. `D4`'s `CustomerQuotation` records
  one document, not a relationship.
- **Not an authority.** `ADR-0135`: `P03` compares, ranks and recommends,
  and never places an order, awards business, approves a supplier or
  commits expenditure.

---

## 3. The shared core

Seven types in `Tempest.Core.CommercialIntelligence` that every package
uses, and that exist so the packages agree about the hard cases.

### `CommercialQuality`

`Invalid`, `Incomplete`, `Unverified`, `Stale`, `Verified`,
`NotApplicable`, `Contradicted`. A second axis from
`ReferenceValidationState` (`ADR-0132`): the lifecycle says how far a
record got through governance, this says whether the figure is still
worth using. `IsDecisionGrade` is true only for `Verified`; over a set the
weakest governs; an empty set is `Incomplete`, because the absence of
contradicting evidence is not evidence.

### `CostFigure`

Wraps `Money` (`ADR-0130`) and adds a certainty — `Unknown`, `Estimated`,
`Ranged`, `Quoted`, `Exact` — plus both ends of a range. Unknown plus
known is unknown. A sum of nothing is zero in the stated currency, not
unknown. Cross-currency arithmetic throws.

There is no averaging and no midpoint-of-range shortcut: choosing which
end of a range to believe is a commercial judgement, not an operation.

### `LeadTimeDuration` and `LeadTimeKind`

A working day is not a duration (`ADR-0133`). The type keeps its own unit,
refuses to convert working days to elapsed time, and throws rather than
ordering incomparable figures. `LeadTimeKind` records how firmly a figure
is stated, from `Estimated` to `Committed` to `Actual`.

### `QuantityBand`

`[Minimum, Maximum?]` with `Contains`, `Overlaps` and `Width`. Prices and
lead times are true at a quantity, and a band that says so can be checked
against an enquiry.

### `CommercialApplicability`, `CommercialEnquiry`, `GeographicScope`, `CommercialSource`

The context every commercial figure carries: which process, which
materials, which supplier, what quantities, where, and over what period.
Three deliberate asymmetries, all in the same direction:

- An unstated `GeographicScope` **covers nothing**, rather than
  everything.
- `AppliesTo` never matches on the *absence* of a stated dimension.
- An undated `CommercialSource` reads as **older than any threshold**.

Absence is never read as permission.

---

## 4. Governance and storage

Every `P03` library derives from `ReferenceDataCatalog<TDefinition>`
(`ADR-0126`, reused per `ADR-0132`) and inherits the whole lifecycle:
Draft → Checked → Validated → Released → Superseded, released records
immutable, supersession naming a replacement, a full revision history.

| Library | Document kind |
|---|---|
| `CommercialSuppliers` | `CommercialSupplierReference` |
| `CommercialProcessCosts` | `CommercialProcessCost` |
| `CommercialLeadTimes` | `CommercialLeadTime` |
| `CommercialCostEstimates` | `CommercialCostEstimate` |
| `CommercialSupplierQuotes` | `CommercialSupplierQuote` |
| `CommercialCustomerQuotations` | `CommercialCustomerQuotation` |
| `CommercialSourcingRequirements` | `CommercialSourcingRequirement` |
| `CommercialSourcingComparisons` | `CommercialSourcingComparison` |

Each has a validation service reporting diagnostics under its own prefix:
`TEMPEST-CID` (suppliers), `-CIP` (costs), `-CIL` (lead times), `-CIQ`
(quotes and estimates), `-CIS` (sourcing), over the shared `-CIC`
(context).

---

## 5. D1 / WP03.1 — Supplier database

`SupplierIdentity` separates who a business *is* from what it *does*.
Identity is recorded and never merged: only a registration-number match
is conclusive, everything else is a *possible* duplicate for a person to
look at (`ADR-0131`).

`SupplierRecord` describes and never qualifies. `CapabilityAssurance` runs
`NotAssessed → Offered → Verified → Proven`, with `Declined` and
`Disproven`; every value names who established what. Nothing approves a
supplier or admits it to an approved list.

---

## 6. D2 / WP03.2 — Process and cost library

`ProcessCostRecord` says what a process costs *from a supplier, at a
quantity, on a date* — linked to `P01`'s `A7` by `ProcessRecordId`. `A7`
says what a process is; `D2` says what it costs. The boundary is exact
and neither library duplicates the other.

`CostBasis` (`PerPart`, `PerBatch`, `PerHour`, `PerKilogram`, …) decides
whether `TotalFor(quantity)` can scale a figure at all; unscalable bases
return `null` rather than a plausible wrong number. `CostComponent` breaks
a figure down where the source did, and validation reports components
that do not sum to the whole.

---

## 7. D3 / WP03.3 — Lead-time intelligence

`LeadTimeRecord` carries a typical figure, optional bounds, a kind, and —
for historical figures — the number of orders observed. Validation flags a
"historical average" drawn from two orders, a quoted figure with no
quotation behind it, and bounds that contradict each other or cannot be
compared.

`FindApplicableAsync` returns applicable records **strongest claim
first**, so a caller taking the head gets the firmest thing anybody said.
It returns the rest too, because the weaker figures are frequently the
more realistic ones.

`LeadTimePerformance` records promised against actual, and reports
`Overrun` and `WasLate` as `null` where the two are stated in units that
cannot be compared.

---

## 8. D4 / WP03.4 — Quote and estimate structure

Four things, four types, four libraries (`ADR-0134`):

| | Who asserted it | Who is bound |
|---|---|---|
| `CostEstimate` | The organisation, from its own records | Nobody |
| `SupplierQuote` | A supplier | The supplier, if firm and current |
| `CustomerQuotation` | The organisation | The organisation, once issued |
| `RealisedOutcome` | What happened | — |

**Reproducibility.** Every `EstimateLine` carries `SourcePins` — library,
record, revision — so re-reading a historical estimate resolves the
figures actually used. `EstimatingService.ReproduceAsync` re-reads those
pins and reports where the libraries have moved; it never alters the
estimate. `ICostEstimateCatalog.FindCitingAsync` asks the same question
backwards: which estimates rested on this record?

**Unknowns propagate.** An estimate with one unpriced line has no total,
rather than a total that is certainly too small.

**Authority.** A `CustomerQuotation` recorded as issued must name the
person who issued it and the authority they held. Validation raises an
**error**, not a warning, where it does not.

---

## 9. D5 / WP03.5 — Procurement decision support

`SourcingRequirement` states the criteria *before* the candidates are
assessed, and is governed in its own right so the weights cannot be
quietly reshaped around the answer somebody wanted. Criteria are
`Mandatory` (eliminate), `Weighted` (rank) or `Informational`.

`SourcingComparisonService` is pure and deterministic — no I/O, no clock,
no randomness, tie-broken on ordinal code rather than input order. Two
behaviours are worth stating because their opposites are the usual
failure (`ADR-0135`):

- **Absent information is never scored as zero.** It reduces the
  candidate's established weight and becomes an outstanding question, so
  the supplier nobody researched is not silently ranked below one
  researched and found wanting.
- **Excluded candidates stay in the record**, with the failed criterion
  and its reason attached. A comparison that drops a candidate leaves
  nobody able to see it was considered.

`RecommendationStrength` — `Insufficient`, `Provisional`, `Marginal`,
`Clear` — is a statement about the comparison, not a confidence
percentage. `RequiresHumanDecision` is unconditionally `true`, and
`AlternativeChosen` makes disagreeing with the recommendation a
first-class outcome.

---

## 10. What P03 will not do

| The organisation must | `P03` does |
|---|---|
| Award the work | Rank the candidates and say what it could not establish |
| Approve a supplier | Record what somebody established about its capability, and who |
| Accept a quote | Record what was offered, how firm, and until when |
| Issue a quotation | Refuse to call it issued without naming who issued it |
| Commit expenditure | Total an estimate and name the lines nobody has priced |
| Convert a currency | Throw, naming both currencies |
| Convert working days to elapsed time | Return `null` |

Enforced by a reflection test over every type in the namespace.

---

## 11. What ships

**Every library ships empty.** `P03` is structure, governance and
reasoning; it contains no supplier, no price and no lead time.

This is not an omission. Real commercial intelligence — who the
organisation actually buys from, what it actually pays, how long
suppliers actually take — is business data belonging to the organisation,
not content that can be shipped in a platform. Fabricating it would
produce records indistinguishable in shape from real ones and wrong in
every particular.

Test fixtures are fictional throughout and clearly marked: "Notional
Machining Ltd", "Fictional Castings Ltd", "Imaginary Finishing Ltd",
"Fictional Client Ltd". They live only in the test project, backed by
in-memory stores that die with the test, and are registered nowhere at
run time.

---

## 12. Dependencies

`P03` depends on:

- **`P07`** for `Money`, `CurrencyCode`, `EffectivePeriod`,
  `BusinessEvidence` and `BusinessAuthorisation`. It introduces no second
  money representation (`ADR-0132`).
- **`P01`** for process and material record identities, cited by Id and
  never duplicated. Both are *optional* collaborators in validation: a
  cost must be recordable before the process it names is registered.
- **The shared reference-data layer** (`ADR-0126`) for the lifecycle.

`P03` does not depend on `P02`, and nothing in `P02` depends on `P03`.
Engineering reasoning and commercial reasoning are independent
programmes, and the container reflects that.
