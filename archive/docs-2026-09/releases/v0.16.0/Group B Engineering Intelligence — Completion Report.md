# Group B — Engineering Intelligence: Completion Report

**Programme:** P02 — Engineering Intelligence
**Work Packages:** B1, B2, B3, B4, B5
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Programme summary

`P01` established what is true. `P02` establishes what follows from it —
without ever crossing the line into deciding.

> *Facts live in `P01`. Reasoning lives in `P02`. Calculations live in
> the calculation layer. Human engineering authority remains explicit.*

| Gate | Result |
|---|---|
| Build, Debug | 0 errors, 0 warnings |
| Build, Release | 0 errors, 0 warnings |
| Tests, Debug | **3,904 / 3,904** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Tests, Release | **3,904 / 3,904** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Governance health check | **13 passed, 3 warned, 0 failed** of 16 |

Test count before this programme: 3,788 Core / 474 Desktop. **+116 Core
tests, no Desktop test added, changed or deleted.** No existing test was
weakened, skipped or deleted to make a new one pass.

The three governance warnings are pre-existing and environmental — two
are the tool's own disclosed "no git tags in a working clone" limitation,
and one is the informational note that two historic release folders
predate the `WorkPackages.md` convention. The health check began this
programme at **4 failures**; all four were register drift, three of it
pre-existing, and all four are now reconciled (§7).

---

## 1. Framework completion and knowledge population, reported separately

This distinction is the most important sentence in this report.

**Framework: complete.** Five work packages, four governed content
libraries, one shared reasoning core, 56 validation rules, full DI
registration in the real host, two ADRs, one architecture document, and
116 new tests.

**Knowledge: empty.** TempestOS ships with **zero** rules, **zero**
decision trees, **zero** review definitions and **zero** trade studies.

That is not an omission. Populating these libraries with
plausible-looking engineering guidance that no engineer had authored,
sourced, reviewed and released would be the single most damaging thing
this programme could do — it would produce a system that appeared to
know things it had invented. Every record must come from an engineering
organisation that can stand behind it. The libraries are ready for that
content and contain none of it.

---

## 2. What was built

### 2.1 The shared reasoning core

`Tempest.Core.EngineeringIntelligence` — 22 files.

| Concern | Types |
|---|---|
| Outcome | `AssessmentOutcome` (8 members), `AssessmentOutcomes` |
| Severity | `RuleSeverity` (7 members), `RuleSeverities` |
| Revision pinning | `ReferencePin` |
| Evidence | `EvidenceKind`, `EvidenceReference` |
| Subject bridge | `IAssessmentSubject`, `SubjectQuantity`, `SubjectText`, `AssessmentSubjectKinds`, `SubjectPropertyNames` |
| Rule model | `RuleDefinition`, `RuleExpression` + 8 forms, `RuleThreshold`, `QuantityComparator`, `RuleApplicability`, `RuleDomain` |
| Execution | `RuleEngine`, `ConditionResult`, `RuleEvaluation`, `ResolvedConstant`, `ConstantResolutionSet` |
| Result | `AssessmentRecord`, `CandidateStanding` |
| Governance | `IRuleCatalog`/`RuleCatalog`, `RuleQuery`, `IRuleValidationService`/`RuleValidationService`, `RuleValidationRules` |

Plus `…EngineeringIntelligence.Subjects` — five typed adapters, one per
`P01` library that can be assessed.

### 2.2 The five work packages

| WP | Capability | Namespace | Files |
|---|---|---|---|
| B1 | Material selection logic | `…MaterialSelection` | 5 |
| B2 | Manufacturing decision trees | `…Decisions` | 8 |
| B3 | Mechanical design rules | `…DesignRules` | 3 |
| B4 | Engineering review logic | `…Reviews` | 5 |
| B5 | Design trade-off framework | `…TradeStudies` | 9 |

---

## 3. How human authority is kept explicit

Not by documentation. By what the types make impossible.

| Guarantee | Mechanism |
|---|---|
| Nothing returns a recommendation | No `Recommend`/`Rank`/`Score`/`Choose`/`Select` method exists on any `P02` service; a reflection test enforces this for `ITradeStudyService` |
| A material selection never selects | `MaterialSelectionResult.RequiresHumanDecision` is unconditionally `true` |
| A decision tree never chooses a process | A terminal node names *candidate* families and carries `RequiresHumanDecision` |
| A trade study never computes a decision | `TradeStudyDecision` is constructed by a caller, and cannot exist without the selected option, a rationale and a deciding principal |
| A decision is never overwritten | `RecordDecision` throws if one is already recorded |
| Overruling is visible | `DecisionDepartsFromAssessment`, `ConsiderationOverride`, and the superseded rule evaluation retained as evidence |
| A review never passes by silence | A criterion no rule can answer is `EvidenceRequired`, and the review is not `IsComplete` |
| An assessment never overstates its scope | `AssessmentScopeStatement` states what applied, what ran, and what was skipped for not being released |

**Two honesty defects the tests found, both fixed in `src`:**

1. `MaterialSelectionResult.RequiresHumanDecision` returned `false` when
   exactly one candidate satisfied the constraints. That amounted to the
   system reporting that it had selected the material. Now
   unconditionally `true`, with the narrowing information moved to the
   honestly-named `HasOutstandingQuestions`.
2. `AssessmentSubjectKinds.Component` declared the value `"Component"`,
   canonically owned by the mechanical workspace's own object Kind. It
   now takes a third, distinct value, `"MechanicalComponent"` — it is
   neither the workspace's object Kind nor `A5`'s stored-record Kind.
   Caught by `EngineeringVocabularyConsistencyTests`.

---

## 4. How missing data is handled

`AssessmentOutcome` is eight-valued, and `AssessmentOutcomes.IsAffirmative`
is true for exactly one member.

| Outcome | Means |
|---|---|
| `Pass` | The condition was evaluated and satisfied |
| `Fail` | Evaluated, not satisfied, binding severity |
| `Concern` | Evaluated, not satisfied, non-binding severity |
| `NotApplicable` | `P01`'s own traits table says this kind of subject has no such property |
| `NotRecorded` | The property applies; this record does not have it |
| `EvidenceRequired` | Only a person can settle this, and nobody has |
| `Indeterminate` | The comparison could not be made — a dimension mismatch, a defect in the rule |
| `NotEvaluated` | Nothing was checked |

No code path substitutes a default, treats a missing property as zero, or
infers suitability from the absence of a failure. Applicability comes
from `P01`'s traits tables, so "there is none" is a fact a reference
library asserted, never one `P02` invented; where a library cannot say,
the answer is `NotRecorded`, never `NotApplicable`.

---

## 5. Governance and traceability

`ADR-0128`: rules, decision trees, review definitions and trade studies
are governed reference-data records on the `Group A` lifecycle, not code.

| Content | Catalogue | Document `Kind` |
|---|---|---|
| Design rules | `RuleCatalog` | `EngineeringRule` |
| Decision trees | `DecisionTreeCatalog` | `EngineeringDecisionTree` |
| Review definitions | `ReviewDefinitionCatalog` | `EngineeringReviewDefinition` |
| Trade studies | `TradeStudyCatalog` | `EngineeringTradeStudy` |

Consequences, all inherited rather than reimplemented: only Released
content governs work; a released record is superseded rather than edited;
every revision stays readable; provenance gates the lifecycle.

Every evaluation, walk, finding and judgement records a `ReferencePin`
for the rule *and* the subject, so a result reproduces against exactly
what it read. Tests prove a rule superseded after release stops governing
new work while an assessment pinned to its revision still gives the
answer it gave.

**Validation:** `TEMPEST-EIR-001..016` (rules, 16), `TEMPEST-EID-001..014`
(trees, 14), `TEMPEST-EIV-001..009` (reviews, 9), `TEMPEST-EIT-001..017`
(trade studies, 17) — 56 rules, all on
`ReferenceValidationService<TDefinition>`.

---

## 6. Boundaries held

| Boundary | Held because |
|---|---|
| Not an LLM system | No part of the rule engine, decision walker or assessment path calls a model. Reasoning is deterministic, inspectable, reproducible. |
| Not a calculation engine | `P02` compares recorded values against thresholds; it computes no stress, deflection, life or capacity. |
| Not a CAD/PLM/MES/requirements replacement | Nothing here models geometry, product structure, shop-floor execution or requirements. |
| Not a generic rules or workflow engine | Every type is engineering-specific; there is no generic condition/action pair and no workflow state machine. |
| No copied reference values | Rules reference records and constant symbols; no `P01` value is duplicated into `P02`. |
| No copyrighted standards text | Standards are cited by `StandardReference` only, and citing one is never a claim of compliance. |
| No supplier-specific process data | `B2` holds no supplier data and invents no capability numbers. |
| `WP16` untouched | No compatibility requirement arose. |
| `P01` unchanged in substance | One field's *value* changed (`AssessmentSubjectKinds` is `P02`'s own type); no `Group A` semantics were altered to make `P02` easier. |

Dependency direction: `P02` reads `P01`; nothing in `P01` knows `P02`
exists. `TempestHost` registration order follows that direction, and a
test asserts every `Group A` registration is unchanged.

---

## 7. Register reconciliation

The health check began at 4 failures. All four are now reconciled, and
in three cases the drift **pre-dated this programme** and is disclosed
rather than quietly corrected.

| Register | Before | Action |
|---|---|---|
| Interface Register | 225 declared, 211 rows | 14 `P02` rows added; total re-derived 195 → 225 |
| Exception Register | 96 declared, 93 rows | 3 rows added. Its distribution table still carried `Materials \| 3` and `Bearings \| 7` from before `Group A` folded both into `Reference Data \| 7` — a stale 10-for-7 that is exactly why the register's own "96 rows" narrative disagreed with its actual 93. Corrected. |
| Namespace Register | 61 derived, 54 rows | 7 rows added; total re-derived 48 → 61, having drifted across `Group A` |
| Governance Index | 128 ADRs, 126 stated | Corrected to 128 |
| ADR Register | 128 files, 126 rows | 2 rows added; the "Total" narrative re-derived 104 → 128, disclosing that it had drifted across four Work Packages |
| Architecture Document Register | 40 rows for 41 documents | 1 row added for `Group B`, and 1 backfilled for `State Schema Versioning Architecture.md`, which `WP 16.3A`/`WP 16.3B` never gave one; 39 → 41 |
| Documentation Register | `docs/architecture/` stated 31 | Re-derived to 39, disclosing the eight-document `Group A` drift |
| Vocabulary Register | — | 4 Kind rows added; the `AssessmentSubjectKinds.Component` value change disclosed |

No register entry was manufactured to make the project look more
complete, and no count was adjusted without re-deriving it from the
repository.

---

## 8. Known gaps and honest limitations

1. **Every content library is empty.** §1. This is the intended state.
2. **`CalculationRecord.ReferencedMaterialIds` records ids but not
   revisions.** A pre-existing `P01`/platform gap, unchanged by this
   programme and reported again here: a calculation cannot yet say
   *which revision* of a material it used, where a `P02` assessment can.
   Closing it means widening that record, which is out of `P02`'s scope.
3. **No pin resolver.** Each result pins through the catalogue that
   produced it. A generic "resolve any pin" service would make `P02`
   depend on all seven `P01` libraries; deliberately not built.
4. **No rules DSL.** Rules are records, not text. A grammar can be added
   later over the same records if authoring demands it.
5. **`P02` has no UI.** Nothing in the desktop application surfaces a
   rule, an assessment, a review or a trade study yet. The layer is
   reachable through DI and nowhere else.
6. **Property names are strings.** `SubjectPropertyNames` checks them
   during validation, so a rule reading a property no library records is
   reported at authoring time — but it is a check, not a type.

---

## 9. What this programme did not touch

No `Group A` library's engineering semantics. No `WP16` work package. No
existing test's strength. No new database technology, no new persistence
mechanism, no new revision infrastructure, and no second lifecycle.

`P02` is 57 new source files, 13 new public interfaces, 3 new exceptions,
7 new namespaces and 4 new document Kinds — sitting entirely on
machinery that already existed.
