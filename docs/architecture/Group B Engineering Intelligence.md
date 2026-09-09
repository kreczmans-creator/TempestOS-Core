# Group B — Engineering Intelligence

**Programme:** P02 — Engineering Intelligence
**Namespace:** `Tempest.Core.EngineeringIntelligence`
**Governing ADRs:** `ADR-0127`, `ADR-0128`
**Status:** Architecturally complete, `Group B`. Every rule, tree, review
and study library ships **empty** — see §10.

---

## 1. Purpose

`P01` established what is true. `P02` establishes what follows from it.

The governing sentence, and the boundary every decision here was tested
against:

> *Facts live in `P01`. Reasoning lives in `P02`. Calculations live in
> the calculation layer. Human engineering authority remains explicit.*

| Work Package | Capability | Namespace |
|---|---|---|
| B1 | Material selection logic | `…EngineeringIntelligence.MaterialSelection` |
| B2 | Manufacturing decision trees | `…EngineeringIntelligence.Decisions` |
| B3 | Mechanical design rules | `…EngineeringIntelligence.DesignRules` |
| B4 | Engineering review logic | `…EngineeringIntelligence.Reviews` |
| B5 | Design trade-off framework | `…EngineeringIntelligence.TradeStudies` |

All five sit on one shared core in the root namespace.

---

## 2. What P02 is not

Stated because the boundary is the design, not a caveat on it. `P02` is
not a generic AI system, an LLM orchestration layer, a chatbot, a
calculation engine, a CAD system, a PLM system, a replacement for
requirements management, a manufacturing execution system, a company-wide
business-rules engine, a generic workflow engine, or an autonomous
engineering decision-maker.

Two of those deserve their own sentence.

**Not a calculation engine.** `P02` compares recorded values against
thresholds. It does not compute stress, deflection, life or capacity.
Where a rule needs a computed quantity, the calculation layer computes it
and the result is assessed as an input.

**Not an autonomous decision-maker.** `ADR-0127` records this in full.
Nothing in `P02` chooses a material, a process or an option.

---

## 3. The shared core

| Concern | Type |
|---|---|
| Outcome | `AssessmentOutcome` (8 members), `AssessmentOutcomes` |
| Severity | `RuleSeverity` (7 members), `RuleSeverities` |
| Revision pinning | `ReferencePin` |
| Evidence | `EvidenceKind`, `EvidenceReference` |
| Subject bridge | `IAssessmentSubject`, `SubjectQuantity`, `SubjectText`, `AssessmentSubjectKinds`, `SubjectPropertyNames` |
| Rule model | `RuleDefinition`, `RuleExpression` (8 forms), `RuleThreshold`, `QuantityComparator`, `RuleApplicability`, `RuleDomain` |
| Execution | `RuleEngine`, `ConditionResult`, `RuleEvaluation`, `ResolvedConstant`, `ConstantResolutionSet` |
| Result | `AssessmentRecord`, `CandidateStanding` |
| Governance | `RuleCatalog`, `RuleQuery`, `RuleValidationService`, `RuleValidationRules` |

### 3.1 Outcomes are eight-valued

`NotEvaluated`, `Pass`, `Fail`, `Concern`, `NotApplicable`, `NotRecorded`,
`EvidenceRequired`, `Indeterminate`.

`AssessmentOutcomes.IsAffirmative` is true for `Pass` alone. Aggregation
is worst-wins over an explicit rank, never an average and never a count:
there is no number of passes that outweighs a failed requirement.

The four gap states are distinct on purpose:

| Outcome | Means |
|---|---|
| `NotRecorded` | The property exists for this kind of subject; this record does not have it. |
| `NotApplicable` | `P01`'s own traits table says this kind of subject has no such property. |
| `EvidenceRequired` | Only a person can settle this, and nobody has. |
| `Indeterminate` | The comparison itself could not be made — a dimension mismatch, which is a defect in the rule. |

### 3.2 Severity is not a weight

Binding (`Prohibition`, `Requirement`, `Constraint`) versus non-binding
(`Warning`, `Recommendation`, `Advisory`). An unsatisfied binding rule is
`Fail` and a defect; an unsatisfied non-binding rule is `Concern` and is
not. "Must not", "prefer" and "consider" are three different statements.

### 3.3 The subject bridge

`IAssessmentSubject` is the narrow seam between a rule and a `P01`
record. It is not a string bag: each library has a typed adapter
(`MaterialSubject`, `FastenerSubject`, `BearingSubject`,
`ComponentSubject`, `ProcessSubject`) that exposes recorded properties
along with their `ReferencePropertyAvailability`, and takes applicability
from that library's own family-traits table.

This is what makes "not applicable" honest: a ceramic has no yield point
because `A1` says so, not because `P02` decided. Where a library cannot
say — an unclassified family — every absent property is `NotRecorded`.
*Not known to apply* is never reported as *known not to apply*.

### 3.4 Execution is pure

`RuleEngine.Evaluate` is a static function of (rule, pin, subject,
constants). No clock, no principal, no catalogue read, no I/O. The same
inputs always give the same result, which is what makes a reproduced
assessment meaningful. Who ran it and when belongs to `AssessmentRecord`,
one layer out.

`AllOf` and `AnyOf` evaluate every operand rather than short-circuiting,
so a reviewer sees every reason and not only the first. `Not` preserves a
gap rather than inverting it into a pass.

---

## 4. Governance

`ADR-0128` records the decision. Rules, decision trees, review
definitions and trade studies are governed reference-data records on the
`Group A` shared lifecycle, not code.

| Content | Catalogue | Document `Kind` | Secondary key |
|---|---|---|---|
| Design rules | `RuleCatalog` | `EngineeringRule` | rule code |
| Decision trees | `DecisionTreeCatalog` | `EngineeringDecisionTree` | tree code |
| Review definitions | `ReviewDefinitionCatalog` | `EngineeringReviewDefinition` | review code |
| Trade studies | `TradeStudyCatalog` | `EngineeringTradeStudy` | study code |

Consequences, all inherited rather than reimplemented: only released
content governs work; released content cannot be edited in place, only
superseded; every revision remains readable; provenance gates the
lifecycle.

### 4.1 Revision traceability

Every evaluation, walk, finding and judgement records a `ReferencePin`
(library, record Id, revision number) for the rule *and* for the subject.
`AllPins` on any result gives the complete set. A study or assessment can
therefore be reproduced against exactly the revisions it read
(`ReproduceAsync` on each service), or re-run against current data to see
what has changed underneath it.

There is deliberately no generic pin resolver: each result pins through
the catalogue that produced it, so `P02` does not depend on all seven
`P01` libraries at once.

### 4.2 Validation

| Series | Content | Count |
|---|---|---|
| `TEMPEST-EIR-001..016` | Rules | 16 |
| `TEMPEST-EID-001..014` | Decision trees | 14 |
| `TEMPEST-EIV-001..009` | Review definitions | 9 |
| `TEMPEST-EIT-001..017` | Trade studies | 17 |

All four extend `ReferenceValidationService<TDefinition>`, so provenance
and standard-reference checks come for free.

---

## 5. B1 — Material selection

A caller supplies a `MaterialRequirementSet`: an application description,
`MaterialCriterion` values, and `MaterialEvidenceCriterion` values for
what no recorded property answers.

`MaterialCriterionRole` separates `Constraint` (violating it eliminates)
from `Preference` (missing it does not) from `Informational`. Criteria
run through `RuleEngine` as probe rules, so a project criterion and a
library rule cannot drift apart in how they treat a missing value.

Every candidate is reported — satisfying, unresolved and eliminated —
because "not considered" and "considered and ruled out for this reason"
are different answers and the second is often the more useful.
`RequiresHumanDecision` is unconditionally true; the narrowing
information is in `HasOutstandingQuestions`.

---

## 6. B2 — Manufacturing decision trees

`DecisionTree` is nodes, branches and terminal outcomes.
`DecisionTreeWalker.Walk` is pure and bounded (`MaximumSteps = 256`).

`DecisionWalkTermination` says why a walk ended: `ReachedOutcome`,
`NoBranchApplied`, `InformationMissing`, `TreeIsBroken`, `CycleDetected`.

The rule that matters: **a branch that could not be evaluated stops the
walk.** Continuing past it would mean deciding that it did not apply,
which nobody knows. A terminal node names *candidate* process families
and always carries `RequiresHumanDecision` — cost, lead time, tooling
already owned and supplier relationships are not in the tree.

This is not process planning, and it holds no supplier-specific data or
invented capability numbers.

---

## 7. B3 — Mechanical design rules

`DesignRuleService` assesses a subject against every applicable released
rule, narrowed by an optional `DesignRuleScope` (domains, severities,
safety-critical only, explicit rule codes).

`AssessmentScopeStatement` is the part that matters: it states how many
rules applied, how many ran, and how many applicable rules were skipped
for not being released. An assessment of a subject nothing applies to
says in words that it established nothing — the alternative is a clean
result for a design nobody checked.

---

## 8. B4 — Engineering review logic

A `ReviewDefinition` is criteria across 14 `ReviewArea` values. A
criterion naming a rule is answered by that rule; a criterion no rule can
answer becomes an `EvidenceRequired` finding naming what would settle it.

The system answers the criteria a rule can answer, and says so about the
rest. A review with no defects and an outstanding criterion is not
complete, and reports as such.

`RecordFinding` lets an engineer answer any criterion, including one a
rule already answered — and keeps the superseded rule evaluation as
evidence. Overruling a rule is legitimate; doing it invisibly is not.

---

## 9. B5 — Design trade-off framework

Not a weighted-score spreadsheet. The concepts a scoring matrix destroys
are kept structurally distinct:

| Concept | Type | Can it eliminate? |
|---|---|---|
| Requirement | `ConsiderationKind.Requirement` | Yes |
| Constraint | `ConsiderationKind.Constraint` | Yes |
| Criterion | `ConsiderationKind.Criterion` | No |
| Preference | `ConsiderationKind.Preference` | No |
| Evidence | `EvidenceReference` | — |
| Assumption | `TradeStudyAssumption` | — |
| Risk | `TradeStudyRisk` | — |
| Decision | `TradeStudyDecision` | — |
| Rationale | Required field on the decision | — |

- **Judgements are outcomes with reasons, not scores.**
  `AssessmentOutcome` has no arithmetic, so options are never summed or
  ranked. `TradeStudyJudgement.Comparison` holds the genuine relative
  statement — "stiffer than option B, at three times the cost" — in the
  engineer's own words.
- **Risks are described, not scored.** No likelihood digit, no severity
  digit, no product of the two. An accepted risk must name who accepted
  it.
- **Assumptions are separate from evidence**, with a confidence and an
  owner, so a later reader can see what the decision rests on.
- **The decision is a person's.** `TradeStudyDecision` cannot be
  constructed without the selected option, a rationale and a deciding
  principal. It is never computed, never overwritten, and a decision that
  departs from the assessment is flagged rather than blocked —
  `ConsiderationOverride` records the consideration, the reason and the
  authoriser.

An option need not be a catalogued record: comparing two architectures is
a real trade study, and the framework does not force an option into a
library to be considered.

---

## 10. What ships

Framework and knowledge are reported separately, deliberately.

**Framework:** complete. Five work packages, four governed libraries, one
shared reasoning core, 56 validation rules, full DI registration, and a
test suite covering rule execution, governance, decision walking,
selection, review and trade studies.

**Knowledge: empty.** No rule, decision tree, review definition or trade
study ships with TempestOS. Not an omission — populating these libraries
with plausible-looking engineering guidance nobody had reviewed would be
the single most damaging thing this programme could do. Every record must
be authored, sourced, checked and released by an engineering
organisation that can stand behind it.

The libraries are ready for that content. They contain none of it.

---

## 11. Standards and compliance

A rule may cite a standard through `StandardReference`, resolved against
`A2`. No copyrighted standards text is reproduced anywhere in `P02`, and
citing a standard is never a claim of compliance with it. A rule that
references a clause is an interpretation somebody wrote down; the
standard itself remains the authority.

---

## 12. Dependencies

`P02` reads `P01`. Nothing in `P01` knows `P02` exists, and no `Group A`
library was modified to accommodate the reasoning layer. `WP16` was not
touched.

Registration order in `TempestHost` follows the dependency direction:
every reference library first, then the four reasoning catalogues, then
the five services.
