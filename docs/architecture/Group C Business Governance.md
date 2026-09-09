# Group C — Business Governance & Scale

**Programme:** P07 — Business Governance & Scale
**Namespace:** `Tempest.Core.BusinessGovernance`
**Governing ADRs:** `ADR-0129`, `ADR-0130`
**Status:** Architecturally complete, `Group C`. Every library ships
**empty** — see §12.

---

## 1. Purpose

`P01` established what is true about engineering materials and standards.
`P02` established what follows from them. `P07` establishes what the
organisation is committed to, exposed to, permitted to use, charging,
forecasting, pursuing and limited by.

The system exists to make eight questions answerable, and a ninth about
every answer:

| Question | Work package |
|---|---|
| What are our obligations? | C1 |
| What risks do we carry, and what protects us? | C2 |
| What are we allowed to use, and on what terms? | C3 |
| What do we charge? | C4 |
| What is our financial position and forecast? | C5 |
| Where are our opportunities? | C6 |
| What capacity do we have, and what must change before we scale? | C7 |
| What evidence supports each answer, who owns it, when was it valid, and what decision authority applies? | The shared core |

| Work package | Capability | Namespace |
|---|---|---|
| C1 | Contract templates & commercial terms | `…BusinessGovernance.Contracts` |
| C2 | Insurance & risk register | `…BusinessGovernance.Risk` |
| C3 | IP & data protection framework | `…BusinessGovernance.Assets` |
| C4 | Pricing & rate card | `…BusinessGovernance.Pricing` |
| C5 | Financial controls & forecasting | `…BusinessGovernance.Finance` |
| C6 | Business development & sales pipeline | `…BusinessGovernance.Development` |
| C7 | Operating model & scale plan | `…BusinessGovernance.Operating` |

---

## 2. Source material and its limits

`P07` was commissioned with an existing set of Tempest business template
documents named as mandatory source material, held in an external Claude
Design project.

**That project could not be reached from this environment.** The URL
returns HTTP 403 to the session's own fetch tool, and no equivalent
content exists in the repository: a full search of `docs/` and `src/`
found no contract template, rate card, insurance schedule, business plan
or financial model. The organisation's own artifact listing holds three
artifacts, none of them a business template.

Rather than invent their contents, `P07` was designed from the second
authoritative source the brief names — the repository itself — and from
the engineering-consultancy domain the platform already models. The
consequence is recorded plainly in §11 and in the completion report:
**template integration is outstanding, and the framework is designed to
receive it.** Nothing in `P07` claims to have implemented a template
requirement it never saw.

---

## 3. What P07 is not

Stated because the boundary is the design.

**Not an accounting package.** C5 compares figures somebody else
recorded. It posts nothing, recognises nothing, depreciates nothing,
computes no tax and produces no statement anybody should file. Xero, a
bank and an accountant remain exactly as necessary as before.

**Not a CRM.** C6 holds the governed record — explicit stages, revenue
that knows how real it is, an owner, a history behind every stage change.
`P04` — Business OS — will own the operational surface: activity feeds,
reminders, email, contact management.

**Not legal practice.** No clause text ships with TempestOS, no wording is
interpreted, and no compliance conclusion is reached.

**Not a security or access-control system.** C3 records handling
requirements; `Tempest.Core.Identity` enforces access, as it already did.

**Not supplier or cost intelligence.** That is `P03`, and `P07` defines
governed structures it can later consume.

---

## 4. The shared core

| Concern | Type |
|---|---|
| Money | `Money`, `CurrencyCode`, `CurrencyMismatchException` |
| Effectivity | `EffectivePeriod` |
| Certainty | `DeterminationState` (6 members), `DeterminationStates` |
| Sensitivity | `ConfidentialityClassification` (7 members), `ConfidentialityClassifications` |
| Evidence | `BusinessEvidence`, `BusinessEvidenceKind` |
| Authority | `BusinessAuthorisation`, `AuthorityRequirement`, `BusinessAuthorityKind` |
| Stewardship | `BusinessOwnership`, `ReviewSchedule`, `BusinessGovernanceFacts` |
| Validation | `BusinessGovernanceValidator`, `BusinessGovernanceRules` |

### 4.1 Money

`ADR-0130` records the decision. `Money` is an exact `decimal` plus a
`CurrencyCode`, and is deliberately **not** a `Quantity<TDimension>`:
currency is not a physical dimension, and any conversion factor placed in
the type system is wrong on most days. Arithmetic and comparison across
currencies throw rather than converting.

`Money.Sum` takes its currency explicitly, so an empty pipeline totals to
zero pounds rather than throwing or guessing. Rounding is banker's
rounding and never implicit.

### 4.2 Determination states

`P07`'s counterpart to `P02`'s `AssessmentOutcome`, with its own
vocabulary because the states differ: `NotDetermined`, `Recorded`,
`Assumed`, `ReviewRequired`, `NotApplicable`, `Disputed`.

`ReviewRequired` is the one that keeps this platform out of legal and
accounting practice. Where a determination belongs to a solicitor, an
accountant or an insurer, TempestOS records whose it is and that it has
not been made.

### 4.3 Authority

Every act of business authority is a `BusinessAuthorisation` carrying a
kind, a person, the capacity they acted in, a date and a basis — and it
refuses construction without them. **No `P07` type or service constructs
one.** `AuthorityRequirement` states what a record still needs, so an
absence reports as an actionable requirement naming somebody, rather than
as a silence.

### 4.4 Governance facts

`BusinessGovernanceFacts` — owner, classification, review cycle,
authorisations, outstanding authorities, evidence — is **composed into**
each domain definition, never inherited from. Eleven unrelated domains
share governance and share nothing else.

---

## 5. Governance and storage

`ADR-0129` records the decision. Every `P07` library is a
`ReferenceDataCatalog<TDefinition>` over the Engineering Data Model, with
its own document `Kind` and secondary uniqueness index — the same shared
lifecycle `Group A` and `Group B` use, and no third one.

| Library | Document `Kind` | Secondary key |
|---|---|---|
| `BusinessContractTemplates` | `BusinessContractTemplate` | template code |
| `BusinessContracts` | `BusinessContract` | contract reference |
| `BusinessRisks` | `BusinessRisk` | risk reference |
| `BusinessInsurancePolicies` | `BusinessInsurancePolicy` | policy reference |
| `BusinessIPAssets` | `BusinessIPAsset` | asset reference |
| `BusinessDataAssets` | `BusinessDataAsset` | asset reference |
| `BusinessRateCards` | `BusinessRateCard` | card code |
| `BusinessFinancialAssumptions` | `BusinessFinancialAssumption` | assumption reference |
| `BusinessFinancialScenarios` | `BusinessFinancialScenario` | scenario reference |
| `BusinessOpportunities` | `BusinessOpportunity` | opportunity reference |
| `BusinessOperatingModels` | `BusinessOperatingModel` | model reference |

Inherited without a line of new infrastructure: provenance on every
record; a released record that cannot be edited in place, only
superseded; every revision permanently readable; `ReferencePin`
traceability; and history and audit from the document store.

### 5.1 Two states, not one

Governance state answers *may work rely on this record?*. Domain status
answers *what is the commercial position?*. They are separate axes, and a
contract record can be Released — accurate, checked, complete — while the
contract is still in negotiation.

| Axis | Type |
|---|---|
| Governance | `ReferenceValidationState` on the record |
| Contract | `ContractStatus` |
| Insurance | `PolicyStatus` |
| Pipeline | `PipelineStage`, `RevenueReality` |
| Risk | treatment, acceptance, closure |

### 5.2 Validation

| Series | Content | Count |
|---|---|---|
| `TEMPEST-BGV-001..010` | Shared governance facts | 10 |
| `TEMPEST-BGC-001..022` | Contracts and templates | 22 |
| `TEMPEST-BGR-001..020` | Risk and insurance | 20 |
| `TEMPEST-BGI-001..020` | IP and data assets | 20 |
| `TEMPEST-BGP-001..012` | Pricing | 12 |
| `TEMPEST-BGF-001..017` | Finance | 17 |
| `TEMPEST-BGD-001..011` | Business development | 11 |
| `TEMPEST-BGO-001..018` | Operating model | 18 |

130 rules, all on `ReferenceValidationService<TDefinition>`, with the
shared ten run once by `BusinessGovernanceValidator` rather than eleven
times.

---

## 6. C1 — Contract templates & commercial terms

A `ContractTemplate` holds clauses by category, the organisation's own
policy on each (mandatory, negotiable, needs a solicitor) and a
`LegalReviewState` defaulting to `NotDetermined` — the honest state of a
template somebody adapted from a previous engagement.

An `IssuedContract` **pins the exact template revision it was drawn
from**. That is C1's central guarantee, and it is mechanical: the shared
lifecycle refuses to edit a released template, so revising one cannot
alter a contract already issued. `TemplateDeparture` records where a
contract left its template, why, and whether a solicitor looked at it.

`CommercialTerms` separates what a contract charges from what it says, so
value, liability cap, change control and payment timing are reportable
without reading the wording. Obligations and deliverables are modelled
apart from clauses, because it is obligations that come due.

`IContractService` prepares a draft with its future commitment already
stated as an outstanding authority, resolves a pinned template, and
reports obligations. **It executes nothing.**

---

## 7. C2 — Insurance & risk register

Risk and insurance are kept explicitly separate. `RiskTreatment.Transfer`
moves who pays, not whether the event happens, so an insured risk stays
open with the policy recorded against it.

Assessment and acceptance are separate acts. `IsAccepted` is true only
where a named person exercised `RiskAcceptance` — never because the band
is low or the treatment field says Accept.

`RiskExposures.From` publishes a deterministic matrix returning a band,
never a score, with two deliberate properties: a severe impact is never
merely Low however rare, and `NotAssessed` propagates rather than
defaulting to safe. `ResidualIsUnearned` catches a residual rating better
than the inherent one with nothing implemented to explain it.

`CoverageAssessment` stops one step short of "covered" by construction.
Its strongest value is `PolicySupportsClaim`: a current, evidenced policy
of a relevant type with a stated limit. Whether it would respond is the
insurer's determination.

C2 does not replace `EngineeringDomain.IRisk`, which models a project
risk; `EscalatedFromProjectRiskId` links the two.

---

## 8. C3 — IP & data protection

Origin is a fact about history; ownership is a legal conclusion.
`IPOwnership` defaults to `NotDetermined`, because holding an asset in
TempestOS establishes nothing about owning it. `IsOwnershipAsserted` — a
determined position with no evidence — is an error, as is a disputed
position and use of a third party's IP with no recorded licence.
Background and foreground are modelled explicitly: losing track of which
is which is how a consultancy signs away its own methods.

`DataAsset` records what exists, why it is held, who owns it, who may see
it, how long it is kept and what happens then, and concludes nothing
about compliance. `ComplianceReviewState` names whose determination it is
and whether it has happened; validation says "nobody qualified has said
so", never "not compliant".

---

## 9. C4 — Pricing & rate card

`RateKind` keeps list, quoted, negotiated and realised as four separate
facts. A business keeping only one cannot say where the margin goes.

A rate card carries one currency, is effective-dated, and is approved
separately from being released: Released says the record is accurate,
approval says the prices are the ones the organisation intends to charge.
`PricingService` refuses to quote from a card lacking either.
`FindApplicableAsync` returns every candidate rather than resolving an
overlap, because two cards claiming one day is a governance failure the
caller must see.

`ReproduceAsync` deliberately skips the released-and-approved check: a
two-year-old quotation must reproduce the price it gave, even after the
card has been superseded.

---

## 10. C5 — Financial controls & forecasting

`FinancialFigureKind` — actual, budget, forecast, committed, baseline —
is the distinction the package rests on. `Actual` is the one kind `P07`
cannot originate, and `IsUnsupportedActual` catches a forecast that has
been relabelled.

Assumptions are governed records rather than spreadsheet cells, so
revising one produces a revision rather than quietly altering every
forecast that used it. Scenarios name the future they describe;
`IsPlanningCase` requires a person's approval.

`FinancialVariance` is computed, never stored, is direction-aware (less
revenue is adverse, less cost is not), and returns `null` against a zero
expectation rather than reporting infinity. `IndicativeMargin` is named
for what it is: a planning figure that applies no accounting standard.

---

## 11. C6 & C7 — Pipeline and operating model

**C6.** `RevenueReality` separates potential from contracted from
invoiced from realised, and `OverstatesRevenue` is an error.
`PipelinePosition` carries **no single pipeline value**: adding potential
to contracted produces a number that is neither, and multiplying
estimates by probabilities produces a weighted figure describing no
possible future.

**C7.** `DecisionGate` has no field for a decision. It holds a question,
a measure, a threshold, a recorded value, a review date and the name of
whoever decides. `GateStatus` has four values and none is a decision;
`Describe()` states outright that a crossed threshold is not itself one.
A measurement older than 90 days reports as stale rather than firing, and
the value is recorded rather than reached for — which keeps the gate
deterministic and keeps C7 from depending on every other package.

Capacity and capability are kept apart, and `CommittedProductiveDays`
counts only secured resources: a plan sized on people nobody has hired is
a plan, not a capacity.

---

## 12. What ships

Reported as four separate facts, deliberately.

**Framework: complete.** Seven work packages, eleven governed libraries,
one shared core, 130 validation rules, 27 registered services, and 219
tests.

**Template integration: outstanding.** The source templates could not be
reached (§2). The mapping in the completion report records what is known
about where each anticipated template would land, and marks every row
unverified.

**Business data: empty.** No contract, template, policy, rate, forecast,
opportunity, IP asset or operating model ships with TempestOS. Not an
omission: populating these libraries with plausible-looking business,
legal or financial facts would be the most damaging thing this programme
could do, and §36 of the commissioning instruction forbids it outright.

**Operational workflow: not started.** `P04` owns it.

---

## 13. Dependencies

`P07` reads the platform's own document store, persistence and identity,
and nothing else. It does not read `P01` or `P02`, and neither reads it.
`WP16` was not touched. Registration order in `TempestHost` places `P07`
last and dependent on nothing above it.

One shared type moved during this programme: `ReferencePin` was promoted
from `Tempest.Core.EngineeringIntelligence` to
`Tempest.Core.ReferenceData`, because pinning a reference record at a
revision is reference-data vocabulary and `P07` needs the same guarantee
`P02` does. Namespace change only — no member, behaviour or test changed.
