# Group C — Business Governance & Scale: Completion Report

**Programme:** P07 — Business Governance & Scale
**Work Packages:** C1, C2, C3, C4, C5, C6, C7
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Programme status

**Framework complete. Template integration outstanding. Business data
empty. Operational workflow not started.**

Those are four separate facts and are reported separately throughout, per
§51 of the commissioning instruction.

| Gate | Result |
|---|---|
| Build, Debug | 0 errors, 0 warnings |
| Build, Release | 0 errors, 0 warnings |
| Tests, Debug | **4,123 / 4,123** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Tests, Release | **4,123 / 4,123** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Governance health check | **13 passed, 3 warned, 0 failed** of 16 |
| Working tree | Clean |

Test count before this programme: 3,904 Core / 474 Desktop. **+219 Core
tests, no Desktop test added, changed or deleted.** No existing test was
weakened, skipped or deleted.

The three governance warnings are pre-existing and environmental: two are
the tool's own disclosed "no git tags in a working clone" limitation, and
one is the informational note that two historic release folders predate
the `WorkPackages.md` convention. The health check reached **5 failures**
mid-programme, all register drift; all five are reconciled (§11).

---

## 1. Template integration — the access limitation, stated plainly

§0 of the commissioning instruction named an existing set of Tempest
business template documents as mandatory source material, held in a
Claude Design project, and instructed that they be inspected, mapped and
reused rather than recreated.

**They could not be reached.**

| What was tried | Result |
|---|---|
| Fetching the design-project URL | HTTP 403 Forbidden |
| Listing the account's artifacts (own and shared) | Three artifacts, none a business template |
| Searching `docs/` for contract, rate-card, insurance, pricing or financial material | No template, rate card, policy, business plan or financial model |
| Searching `src/` for existing business, commercial, financial or supplier structures | Only `ISupplier`/`IPurchaseItem` (two marker interfaces) and `IRisk`/`IDecision` (project-level), all in `EngineeringDomain` |

§0 also states: *"If the template project cannot be accessed directly
from the current Claude Code environment, do not fabricate its contents.
Clearly identify the access limitation and continue only with repository
evidence that is actually available."* That is what was done.

**`P07` therefore claims to have implemented no template requirement it
never saw.** The framework was designed from the repository and from the
engineering-consultancy domain the platform already models, and is built
to receive the templates when they become available. `FCR-0096` tracks
the integration as blocked on access rather than on effort.

### 1.1 Template → C-package mapping — anticipated, unverified

Every row below is **unverified**. It records where a template of that
kind would land given the implemented model, so that the mapping work has
a starting point; it is not a statement about any template that exists.

| Anticipated template | Owning package | Structured in TempestOS | Remains document-level | Verified? |
|---|---|---|---|---|
| Consultancy agreement / T&Cs | C1 | `ContractTemplate` — clause index by category, mandatory/negotiable policy, `LegalReviewState`, default `CommercialTerms` | The wording itself, via `SourceDocumentId` | **No** |
| Statement of work / proposal | C1 | `IssuedContract` — parties, term, `CommercialTerms`, `ContractDeliverable` with acceptance criteria, `ContractObligation` | Scope narrative | **No** |
| NDA | C1 | `ContractTemplate` with `ClauseCategory.Confidentiality`; obligations marked `SurvivesTermination` | Wording | **No** |
| Risk register | C2 | `BusinessRisk` — cause, consequence, inherent and residual bands, mitigations with owners, acceptance | Any narrative commentary | **No** |
| Insurance schedule | C2 | `InsurancePolicy` — insurer, number, period, `InsuranceCoverage` with limits, excess, exclusions | The policy document, via `PolicyDocumentId` | **No** |
| IP register / assignment | C3 | `IPAsset` — type, origin, ownership with evidence, `IPLicence` | Deeds and assignments | **No** |
| Data-protection / retention policy | C3 | `DataAsset` — purpose, category, `RetentionRule`, access and transfer requirements, `ComplianceReviewState` | The policy statement | **No** |
| Rate card | C4 | `RateCard` — effective period, one currency, `RateCardEntry` per service with basis and minimum | Published presentation | **No** |
| Financial model / forecast | C5 | `FinancialScenario` with `FinancialPeriod` and `FinancialLine`; `FinancialAssumption` as governed records | Any spreadsheet used to derive figures | **No** |
| Pipeline tracker | C6 | `Opportunity` — stage, value with `RevenueReality`, owner, next action, interactions | Meeting notes | **No** |
| Business plan / scale plan | C7 | `OperatingScenario` — capabilities, `ResourceCapacity`, constraints, assumptions, `DecisionGate` | The plan's narrative and argument | **No** |

**Conflicts and gaps:** none can be identified, because no template was
seen. Where the templates use different terminology from the implemented
model, §37 requires the conflict be recorded and resolved through the
architecture mechanism rather than by silently renaming either side;
`FCR-0096` carries that instruction forward.

---

## 2. Shared architecture

`Tempest.Core.BusinessGovernance` — 8 files. Deliberately small:
governance all seven packages need, and nothing belonging to one.

| Concern | Types |
|---|---|
| Money | `Money`, `CurrencyCode`, `CurrencyMismatchException`, `CurrencyCodeJsonConverter` |
| Effectivity | `EffectivePeriod` |
| Certainty | `DeterminationState` (6), `DeterminationStates` |
| Sensitivity | `ConfidentialityClassification` (7), `ConfidentialityClassifications` |
| Evidence | `BusinessEvidence`, `BusinessEvidenceKind` |
| Authority | `BusinessAuthorisation`, `AuthorityRequirement`, `BusinessAuthorityKind` |
| Stewardship | `BusinessOwnership`, `ReviewSchedule`, `BusinessGovernanceFacts` |
| Validation | `BusinessGovernanceValidator`, `BusinessGovernanceRules` |

**What was shared and why.** Governance, because a contract template and
an insurance policy answer the same governance questions. **What was
not**: domain semantics. `BusinessGovernanceFacts` is composed into each
definition rather than inherited, so eleven unrelated domains are not
forced through one schema with a nullable field for each difference —
the same reasoning `ADR-0126` applied to `Group A`.

`ADR-0129` and `ADR-0130` record both decisions.

---

## 3. C1 — Contract templates & commercial terms

**Existing state:** none. No contract, template or commercial concept
existed anywhere in the repository.

**Implemented:** `ContractTemplate` with clauses by category and the
organisation's own policy on each; `CommercialTerms` separating what a
contract charges from what it says; `IssuedContract` with parties, term,
deliverables, obligations and `TemplateDeparture`; two catalogues, two
validation services, `IContractService`.

**Governance:** both libraries on the shared lifecycle.
`ContractStatus` is a second axis, so a Released record of a contract
still in negotiation is expressible.

**Revision:** an issued contract **pins the exact template revision it
was drawn from**. A test proves that superseding a template with
different liability terms leaves the contract resolving to the wording it
was drawn from.

**Human authority:** nothing executes a contract. A prepared draft
carries the commercial commitment it will need as an outstanding
authority against a named person. A reflection test forbids an `Execute`,
`Sign`, `Approve` or `Commit` method.

**Testing:** 25 tests. **Deferred:** clause-level diffing between a
contract and its template; renewal workflow (`P04`).

---

## 4. C2 — Insurance & risk register

**Existing state:** `EngineeringDomain.IRisk` — a project risk with
string likelihood and severity. Reused, not replaced:
`EscalatedFromProjectRiskId` links a business risk to the project risk it
came from.

**Implemented:** `BusinessRisk` with cause, consequence, inherent and
residual bands, mitigations with named owners, treatment and acceptance;
`InsurancePolicy` with coverages, limits, excess and exclusions;
`CoverageAssessment` and `IRiskAndInsuranceService`.

**Separation held:** risk is not insurance — `Transfer` moves who pays,
not whether the event happens, and an insured risk stays open with the
policy against it. Assessment is not acceptance — `IsAccepted` requires a
named person's `RiskAcceptance`, and `RecordAcceptance` changes the
exposure not at all.

**No score:** `RiskExposures.From` publishes a deterministic matrix
returning a band. A severe impact is never merely Low however rare, and
`NotAssessed` propagates. `ResidualIsUnearned` catches a residual rating
crediting mitigations nobody implemented.

**Coverage never asserted:** the strongest value is
`PolicySupportsClaim`, and no enum value is named Covered or Insured.

**Testing:** 34 tests. **Deferred:** claims history; broker workflow;
premium forecasting into C5.

---

## 5. C3 — IP & data protection framework

**Existing state:** none.

**Implemented:** `IPAsset` separating origin (a fact about history) from
ownership (a legal conclusion), with `IPLicence`, restrictions and
registration renewal; `DataAsset` with purpose, category, `RetentionRule`,
access and transfer requirements and `ComplianceReviewState`.

**Nothing determined:** ownership defaults to `NotDetermined`; an
evidenced-position-with-no-evidence is an error, as is a disputed
position or third-party IP with no licence. No compliance conclusion is
reached anywhere — validation says "nobody qualified has said so", and a
test asserts the message never says "not compliant".

**Existing infrastructure used:** `Identity` for access enforcement, the
document store for evidence. No second access-control system.

**Testing:** 19 tests. **Deferred:** open-source licence obligation
tracking; DPIA and records-of-processing document generation; automatic
linkage from IP assets to the engineering objects that embody them.

---

## 6. C4 — Pricing & rate card

**Existing state:** none.

**Implemented:** `RateCard` (one currency, effective-dated, separately
approved), `RateCardEntry` with basis and minimum charge, `RateKind`
distinguishing list, quoted, negotiated and realised, `QuotedRate` pinned
to the card revision, and `IPricingService`.

**Revision:** `ReproduceAsync` deliberately skips the
released-and-approved check, so a two-year-old quotation reproduces the
price it gave after the card has been superseded at a higher rate. Tested
directly.

**Governance:** `FindApplicableAsync` returns every candidate rather than
resolving an overlap; validation reports two cards claiming one day.

**Testing:** 21 tests. **Deferred:** cost-based price derivation (`P03`);
client-specific negotiated-rate libraries; multi-currency cards, which
are refused by design rather than deferred.

---

## 7. C5 — Financial controls & forecasting

**Existing state:** none.

**Implemented:** `FinancialFigureKind` (actual, budget, forecast,
committed, baseline), `FinancialCategory`, `FinancialPeriod`,
`FinancialLine`, `FinancialAssumption` as a governed record,
`FinancialScenario`, `FinancialVariance` and `IFinancialControlService`.

**Actual, forecast and assumption kept apart:** `IsUnsupportedActual`
catches a forecast relabelled as an actual. Assumptions are records, so
revising one produces a revision rather than altering every forecast that
used it.

**Deterministic:** totals and variances are exact decimal arithmetic and
give the same answer twice; a variance report pins the scenario revision
it read.

**Boundary held:** no posting, recognition, depreciation or tax. A
reflection test forbids those methods. `IndicativeMargin` is named for
what it is.

**Testing:** 24 tests. **Deferred:** cash-flow timing modelling; import
from an accounting system; tax provisioning, which is an accountant's
determination.

---

## 8. C6 — Business development & sales pipeline

**Existing state:** none.

**Implemented:** `Opportunity` with explicit `PipelineStage`,
`RevenueReality`, owner, next action and interactions;
`IPipelineService` reporting the position.

**Revenue reality enforced:** `OverstatesRevenue` is an error; revenue is
contracted only when an opportunity is Won and names a contract — the one
seam between C6 and C1.

**No weighted pipeline:** `PipelinePosition` carries contracted and
potential separately and has no total, weighted or expected value. A
reflection test forbids adding one. `WinRate` returns null over no
decisions.

**P04 boundary:** no contact management, no activity feed, no reminders,
no email. `ExternalOrganisationId` is the seam.

**Testing:** 18 tests (within the 38-test C6/C7 file). **Deferred:**
proposal generation from C1 and C4; campaign and lead-source analytics
(`P04`).

---

## 9. C7 — Operating model & scale plan

**Existing state:** none.

**Implemented:** `OperatingCapability`, `ResourceCapacity` with an
explicit utilisation assumption, `OperatingConstraint`,
`OperatingAssumption`, `DecisionGate` and `OperatingScenario`.

**The gate reports; the person decides.** `DecisionGate` has no field for
a decision — a reflection test asserts it. `GateStatus` has four values
and none is one. `Describe()` states outright that a crossed threshold is
not itself a decision. A measurement older than 90 days reports as stale
rather than firing.

**Deliberate independence:** the gate's value is recorded, never reached
for. C7 does not read C5 or C6; a caller reads those and records the
figure with its date, which keeps the gate deterministic and keeps C7
from depending on every other package.

**Capacity vs capability:** kept apart, with `IsSinglePointOfFailure` and
`IsSoldButNotHeld` as the findings that follow.
`CommittedProductiveDays` counts only secured resources.

**Testing:** 20 tests (within the 38-test C6/C7 file). **Deferred:** HR,
payroll and resource scheduling, all explicitly out of scope; automated
measurement feeds into gates.

---

## 10. Boundary decisions

| Boundary | Held because |
|---|---|
| `P04` — Business OS | C6 holds the governed record; the operational CRM surface is not built. `ExternalOrganisationId` and `ContractParty` are the seams. |
| `P03` — Commercial Intelligence | No supplier intelligence, cost intelligence, lead-time data or procurement support. C4 and C5 define structures it can consume. |
| `P05` / `P06` | Nothing about engineering assets or Academy knowledge. |
| Accounting systems | C5 compares; Xero, a bank and an accountant remain necessary. |
| Legal practice | No clause text ships, no wording is interpreted, no compliance concluded. |
| Identity | Classification labels handling; `IPermissionEvaluator` enforces access. |
| `WP16` | Untouched. |
| `P01` / `P02` | `P07` reads neither, and neither reads `P07`. A host test asserts both remain registered unchanged. |

**One shared type moved:** `ReferencePin` was promoted from
`Tempest.Core.EngineeringIntelligence` to `Tempest.Core.ReferenceData`,
because pinning a reference record at a revision is reference-data
vocabulary and `P07` needs the same guarantee `P02` does. Namespace
change only — no member, behaviour or test changed, and the full suite
passed before and after.

---

## 11. Registers

The health check reached 5 failures during this programme. All are
reconciled, and where the drift **pre-dated** `P07` it is disclosed
rather than quietly corrected.

| Register | Action |
|---|---|
| ADR Register | 2 rows (`ADR-0129`, `ADR-0130`); total re-derived 128 → 130 |
| Interface Register | 27 `P07` rows; total re-derived 225 → 252 |
| Exception Register | 3 rows + a Business Governance distribution row; total 96 → 99 |
| Namespace Register | 8 rows; total 61 → 69; `ReferenceData` and `EngineeringIntelligence` file counts corrected for the `ReferencePin` promotion |
| Vocabulary Register | 11 document Kinds, all `Business`-prefixed; no Classification or `RelationshipKind` added, supersession reusing `supersedes` |
| Governance Index | ADR count 128 → 130 |
| Architecture Document Register | 1 row; 41 → 42 |
| Documentation Register | `docs/architecture/` 39 → 40 |
| Future Capability Register | `FCR-0096`, `FCR-0097` in a new Business Governance category; 94 → 96 |

No entry was manufactured, and no count adjusted without re-deriving it
from the repository.

**Technical Debt Register: no new entries.** `P07` disclosed no shortcut
or deliberate compromise that belongs there. Its two open items are
capability gaps, which is what the Future Capability Register is for.

---

## 12. Defects found and fixed

**`Money` did not survive a round trip through the reference-data
serialiser.** Every persisted contract value, rate-card entry and
forecast came back as zero in an unspecified currency. Two causes:
`CurrencyCode` holds its code in a private field so a malformed value
cannot be constructed, leaving no public property to serialise; and
`Money`'s properties are get-only, so without `[JsonConstructor]` the
serialiser fell back to the implicit parameterless struct constructor.

The defect was invisible in memory and would have stayed invisible until
somebody reopened a saved contract. `CurrencyCodeJsonConverter` and
`[JsonConstructor]` close it, and a round-trip test guards it directly.

---

## 13. Cross-package review

Checked across all seven, per §50:

| Checked for | Finding |
|---|---|
| Duplicate business entities | None. Parties and organisations are names plus an external id in both C1 and C6, deliberately not a second organisation model. |
| Duplicate risk models | None. `EngineeringDomain.IRisk` remains the project risk; C2 links rather than duplicates. |
| Duplicate approval models | None. One `BusinessAuthorisation`, used by all seven. |
| Duplicate revision systems | None. One shared reference-data lifecycle across `P01`, `P02` and `P07`. |
| Inconsistent status semantics | Resolved by design: governance state on the record, domain status on the definition, everywhere. |
| Inconsistent effective dates | One `EffectivePeriod`, used by contracts, policies, rate cards, licences and assumptions. |
| Inconsistent ownership | One `BusinessOwnership`, required on every record. |
| Inconsistent evidence | One `BusinessEvidence`, with `IsLocatable` meaning the same thing everywhere. |
| Inconsistent confidentiality | One `ConfidentialityClassification`; `Unclassified` ranks restrictive in every package. |
| Financial values | One `Money`. No package holds a bare decimal amount. |
| Accidental legal or accounting authority | None found. Every act is a `BusinessAuthorisation`; six reflection tests guard the boundaries. |
| Dependencies on `P04`/`P03` | None. |

One genuine near-duplicate is disclosed: `Finance.FinancialPeriod` and
`Operating.FinancialPeriodLabel` are structurally similar. They are kept
separate deliberately, so that C7 does not depend on C5 for a label; the
duplication is two properties, and coupling the packages would cost more
than it saves.

---

## 14. Known gaps and honest limitations

1. **The source templates were never seen** (§1). The mapping is
   anticipated and marked unverified throughout. `FCR-0096`.
2. **Every library is empty.** No business, legal or financial fact ships.
   `FCR-0097`.
3. **No UI.** Nothing in the desktop application surfaces a contract, a
   risk, a rate card or a gate. The layer is reachable through DI only,
   which §44 explicitly permits.
4. **No cross-package reporting surface.** Answering all eight business
   questions at once needs a caller that reads seven services; `P04`
   is where that belongs.
5. **Decision-gate values are recorded, not measured.** Feeding a gate
   from a C5 scenario or a C6 pipeline report is a caller's job today.
   Deliberate (§9), and a candidate future capability.
6. **Currency conversion is refused, not deferred.** An organisation
   trading in two currencies must hold two rate cards and two scenarios.
   This is a design position, not a gap.
7. **`CalculationRecord.ReferencedMaterialIds` records ids but not
   revisions** — a pre-existing platform gap, unchanged by `P07` and
   reported again for continuity with the `P02` report.

---

## 15. Git

| Item | State |
|---|---|
| Branch | `claude/tempestos-a4-bearing-library-unobtf` |
| Commits | 11 for `P07`: shared core; C1; C2; C3; C4; C5; C6; C7; DI, core and C1 tests with the serialisation fix; all-package tests; documentation and registers; this report |
| Working tree | Clean |
| Push state | Pushed |
| Out-of-scope changes | None. `WP16` untouched; no release tag or claim altered; the only change outside `BusinessGovernance` is the `ReferencePin` namespace promotion (§10) and the `TempestHost` registration block. |

---

## 16. What P07 did not touch

No `Group A` library's engineering semantics. No `Group B` reasoning
behaviour. No `WP16` work package. No existing test's strength. No new
database technology, no new persistence mechanism, no new revision
infrastructure, no new access-control system, and no second audit trail.

`P07` is 38 new source files, 27 new public interfaces, 3 new exceptions,
8 new namespaces, 11 new document Kinds and 130 validation rules —
sitting entirely on machinery that already existed.
