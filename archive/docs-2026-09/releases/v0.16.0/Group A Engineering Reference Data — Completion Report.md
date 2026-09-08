# Group A — Engineering Reference Data: Completion Report

**Programme:** P01 — Engineering Reference Data
**Work Packages:** A1, A2, A3, A5, A6, A7 (A4 completed previously)
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Programme summary

Six reference libraries completed to A4's standard, and one shared layer
extracted so the seventh did not become seven copies of itself.

| Gate | Result |
|---|---|
| Build, Debug | 0 errors, 0 warnings |
| Build, Release | 0 errors, 0 warnings |
| Tests, Debug | **3,788 / 3,788** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Tests, Release | **3,788 / 3,788** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Governance health check | **13 passed, 3 warned, 0 failed** of 16 |

Test count before this programme: 3,439 Core / 474 Desktop. **+349 Core
tests, no Desktop test added, changed or deleted.** No existing test was
weakened, skipped or deleted to make a new one pass; the A4 and A1 test
churn is behavioural equivalence through shared types, not reduced
coverage.

The three governance warnings are pre-existing and environmental: two are
the tool's own disclosed "no git tags in a working clone" limitation, and
one is two historical release folders that never had a `WorkPackages.md`.
All three failures the tool found — Interface, Exception and Namespace
Register drift — are fixed.

---

## 1. The architectural decision, stated plainly

A4 built a great deal that is not about bearings. Six more libraries
needed all of it, unchanged.

Two things were prohibited: duplicating the infrastructure seven times,
and collapsing seven domains into one generic
`EngineeringReferenceData` class. `ADR-0126` records where the line
falls — governance is shared, engineering semantics are not — and
`Group A Engineering Reference Data.md` documents the result.

**A4 and A1 migrated onto the shared layer rather than being left beside
it.** That churn is a real cost, paid once, disclosed rather than
avoided. The alternative was two libraries permanently on their own
copies of code six others share.

`ADR-0125` resolves affine units, which three libraries needed and which
`FCR-0034`/`TD-19` had carried since `WP 7.1B`.

---

## 2. A1 — Materials Database

**Status.** Implemented (uplift).
**Existing state.** `IMaterialCatalog`/`MaterialCatalog` with
`MaterialSpecification`, `MaterialProperty`, its own provenance,
confidence and validation-status types, its own codec and its own
exception family. No lifecycle, no supersession, no query, no comparison,
no validation.
**Implemented.** `MaterialDefinition`; `MaterialFamily` (closed) with
`MaterialFamilyTraits`; `MaterialPropertyNames`, a controlled vocabulary
of sixteen well-known names with expected dimensions, laid *alongside*
the deliberately open property set rather than closing it;
`MaterialQuery`/`MaterialQueryEvaluator`; `MaterialComparer`;
`IMaterialValidationService` with `TEMPEST-MAT-001`…`013`.
**Canonical model.** Name and family required; everything else optional
and null where the source gave nothing.
**Taxonomy.** Family became a closed enum because
`MaterialFamilyTraits` needs something to stand on; the source's own
wording survives verbatim in `SourceClassification`, and a material
classified `Other` must carry it.
**Data.** None. `FCR-0093`.
**Provenance.** Shared `ReferenceProvenance`; the old
confidence/validation-status types removed as duplicates.
**Validation.** Dimension checking, physics bounds, cross-property
relationships (yield above ultimate; inverted service-temperature range),
family applicability.
**Revision.** Full lifecycle and supersession, both new.
**Search.** Typed query including property-range bounds in base units; an
unrecorded property never matches a bound.
**Integration.** Document `Kind` unchanged (`MaterialSpecification`), so
pre-uplift records are still this library's own. A3, A5 and A7 link to
it.
**Testing.** `Materials/` 5 files, 47 attributes. Four files whose
subjects moved to the shared layer deleted, three added.
**Documentation.** `docs/architecture/A1 Materials Database.md`.
**Deferred.** Dataset population.
**FCR/TD.** `FCR-0093`.

## 3. A2 — Standards Library

**Status.** Implemented (new).
**Existing state.** `IStandardResolver` and `StandardReference` existed
in the shared layer with no implementation behind them.
**Implemented.** `IStandardCatalog`/`StandardCatalog`,
`StandardDefinition`, `StandardsBody`/`StandardsBodyKind`,
`StandardClassification`/`StandardClassificationTraits`,
`StandardDiscipline`,
`StandardPublicationStatus`/`StandardPublicationStatuses`,
`StandardEquivalence`, query, comparer,
`TEMPEST-STD-001`…`014`, and `StandardCatalogResolver`.
**Canonical model.** Body and designation required.
**The two decisions.** Publisher status is a different axis from record
validation state, kept as separate fields, criteria and comparison rows.
An edition is a record, not a revision: the key is body + designation +
edition, so two editions are both holdable and lookup without an edition
finds the undated record rather than guessing the latest.
**Data.** None, and licence-constrained in a way the others are not.
`FCR-0093`.
**Validation.** Bibliographic identity, publisher-status contradictions,
four date orderings, self-reference (checked against the undated key too),
and a heuristic guard on a scope summary long enough to be reproduced
standard text.
**Search.** Including the two status axes as independent criteria.
**Integration.** Implements `IStandardResolver`; every other library's
validation service resolves citations through it.
**Testing.** `Standards/` 2 files, 45 attributes, 52 executed.
**Documentation.** `docs/architecture/A2 Standards Library.md`.
**Deferred.** Dataset population. No standard content, ever.
**FCR/TD.** `FCR-0093`.

## 4. A3 — Fastener Library

**Status.** Implemented (new).
**Existing state.** None.
**Implemented.** `IFastenerCatalog`/`FastenerCatalog`,
`FastenerDefinition`, `FastenerFamily`/`FastenerFamilyTraits`,
`FastenerHeadType`/`FastenerDriveType`, `ThreadSpecification`,
`FastenerDimensions`, `FastenerMechanicalProperties`,
`FastenerHardness`, `FastenerTorqueReference`, `FastenerFinish`, query,
comparer, `TEMPEST-FST-001`…`020`.
**Taxonomy.** Eleven families, with traits distinguishing external from
internal threading, headed from headless, and driven from undriven — a
set screw is headless but still driven, and the two questions are kept
apart.
**The three decisions.** Torque is transcribed, never computed, and a
figure without its friction conditions is warned about. Hardness is not a
dimensioned quantity and its comparison row offers no canonical value.
Pitch is recorded; threads per inch is a designation convention and gets
no field.
**Data.** None. `FCR-0093`.
**Validation.** Applicability, geometry (a pitch not smaller than the
diameter; a width across corners not greater than the flats; an item with
no wall), physics (strength above tensile; proof load above breaking
load), and unrecorded thread handedness.
**Integration.** Links to A1 by `materialId`; cites A2.
**Testing.** `Fasteners/` 2 files, 44 attributes, 49 executed.
**Documentation.** `docs/architecture/A3 Fastener Library.md`.
**Deferred.** Dataset population. No joint analysis.
**FCR/TD.** `FCR-0093`.

## 5. A5 — Springs, Gears and Mechanical Components

**Status.** Implemented (new).
**Existing state.** None.
**Implemented.** `IComponentCatalog`/`ComponentCatalog`,
`ComponentDefinition`, `ComponentFamily`/`ComponentGroup`/
`ComponentFamilyTraits`, three typed detail records
(`SpringDetail`, `GearDetail`, `DriveElementDetail`),
`ComponentDimensions`, `ComponentRatings`, query, comparer,
`TEMPEST-CMP-001`…`024`.
**Taxonomy.** One taxonomy, twenty-seven families, seven groups, with
traits deciding which typed detail a family may carry. Families with none
carry dimensions and ratings only — a fact, not a gap. Fasteners and
rolling bearings deliberately excluded: they are A3's and A4's.
**Units.** `TorsionalStiffness` added as a dimension of its own
(`ADR-0125`), because a torsion spring's rate is a torque per angle and
the radian being dimensionless means units alone cannot tell it from a
torque. `Power` added for drives rated in power.
**Data.** None. `FCR-0093`.
**Validation.** Detail applicability, rate form matching the family, and
geometry that cannot be otherwise — a spring with no travel; active coils
exceeding total; a wire diameter disagreeing with the coil diameters; a
pressure angle above 45°; an external gear whose tips do not stand
outside its pitch circle, with the rule deliberately restricted so
internal gears are not wrongly rejected.
**Testing.** `Components/` 2 files, 43 attributes, 53 executed.
**Documentation.** `docs/architecture/A5 Mechanical Components Library.md`.
**Deferred.** Dataset population. No spring design, no gear rating.
**FCR/TD.** `FCR-0093`, `FCR-0095`.

## 6. A6 — Engineering Constants and Fundamentals

**Status.** Implemented (new).
**Existing state.** None.
**Implemented.** `IConstantCatalog`/`ConstantCatalog`,
`ConstantDefinition`, `ConstantCategory`/`ConstantCategories`,
`ConstantUncertainty`, query, comparer,
`TEMPEST-CON-001`…`014`, plus `IReleasedConstantSource`,
`ReleasedConstant` and `ConstantCatalogReleasedSource`.
**The consumption seam.** Declared in the shared layer so a calculation
can consume constants without depending on A6. It hands back nothing
until a record is Released, reports an unreleased constant exactly as it
reports a missing one, and carries the record Id and revision number back
with the value so a calculation can say afterwards which number it used.
**Canonical model.** Symbol and name required; the value is always a
dimensioned quantity, mathematical constants included.
**Uncertainty.** Not recorded, zero, and exact by definition are three
distinct states. Absolute and relative figures are both recordable and
neither is ever computed from the other.
**Data.** None, and the fixtures use fictional digits deliberately.
`FCR-0093`.
**Validation.** A constant with no value; an exact constant carrying an
uncertainty; an uncertainty in the wrong dimension; a relative
uncertainty of one or more; a mathematical constant with a dimension; a
conventional value with no applicability; a symbol shadowed by another
record's alternative symbol.
**Testing.** `Constants/` 2 files, 33 attributes, 35 executed.
**Documentation.** `docs/architecture/A6 Engineering Constants.md`.
**Deferred.** Dataset population. No uncertainty propagation.
**FCR/TD.** `FCR-0093`.

## 7. A7 — Manufacturing Process Library

**Status.** Implemented (new).
**Existing state.** None. `Tempest.App.Workspace.Manufacturing` exists
and is a different thing — an operation on a real part.
**Implemented.** `IProcessCatalog`/`ProcessCatalog`,
`ProcessDefinition`, `ProcessFamily`/`ProcessGroup`/
`ProcessFamilyTraits`, `ProcessCapabilities`,
`ProcessMaterialCompatibility`, `ProcessConstraint`, `ProductionScale`,
query, comparer, `TEMPEST-MFG-001`…`017`.
**Taxonomy.** Some fifty families across thirteen groups, with traits
deciding which capabilities describe which process.
**Capabilities.** Every one a `ReferenceRange` with its own origin and
conditions. A search asks whether a source's own band covers a value; an
unpublished band is never read as unbounded.
**Material compatibility.** Over A1's own `MaterialFamily`, not a second
list. `NotSuitable` is recorded, and a search never returns a process a
source ruled out.
**Data.** None, and the fixture bands are fictional deliberately: a
capability band read as real would steer a manufacturing decision.
`FCR-0093`.
**Integration.** Document `Kind` is `ManufacturingProcessReference`,
deliberately distinct from the workspace's `ManufacturingOperation`.
**Testing.** `Manufacturing/` 2 files, 40 attributes, 64 executed.
**Documentation.** `docs/architecture/A7 Manufacturing Process Library.md`.
**Deferred.** Dataset population. No process planning, no selection, no
supplier capability.
**FCR/TD.** `FCR-0093`.

---

## 8. Shared layer and A4 migration

**`Tempest.Core.ReferenceData`** — 31 files: provenance, lifecycle and
its transition table, `ReferenceDataCatalog<T>`, `IReferenceRecord<T>`,
sourced value and range types, the quantity codec, comparison, the
validation service shape, `StandardReference`, both cross-library seams,
and `ReferenceDataException` with six subtypes.

**A4** lost `IBearing`, `BearingValidationState`, its provenance and
serialisation types and its seven exception types, keeping its document
`Kind` and its behaviour. Its rule series kept only the codes genuinely
about bearings; five retired to the shared `TEMPEST-REF-` series and are
deliberately not reused.

**Tested once.** `ReferenceData/` — 3 files, 64 attributes, 84 executed —
covers the lifecycle gates, catalogue mechanics, index maintenance and
hostile data for all seven libraries. No library restates them.

---

## 9. Units & Quantities

`ADR-0125`. Purely additive: an optional `ToBaseUnitOffset` on
`Unit<TDimension>` (every existing unit unchanged at zero), conversion
through the base unit, and arithmetic refused on affine quantities.
Thirteen new dimensions plus `TorsionalStiffness`, `Power`, `Micrometre`
and `Gigapascal`. **`FCR-0034` and `TD-19` are Resolved.**

Two disclosed exceptions to recording engineering values as quantities,
each because the value genuinely is not one: hardness (A3) and production
scale (A7).

---

## 10. Governance

Registers reconciled against source, not against each other.

| Register | Change |
|---|---|
| Interface | 197 → 211 rows; 2 stale removed, 16 added |
| Exception | 10 bearing/material rows → 7 shared rows |
| Namespace | 6 namespaces added; 3 counts corrected |
| ADR | `ADR-0125`, `ADR-0126` |
| Governance Index | ADR count 124 → 126 |
| Engineering Vocabulary | 5 document Kinds; **no** new relationship kind; one comparison row key corrected |
| Dependency Injection | 13 rows |
| Platform Services | 12 rows |
| Platform Service Map | 12 at-a-glance rows, 1 detail section, A4's own key-types list corrected |
| Architecture Document | 7 documents; total 32 → 39 |
| Test | 6 directory rows, 3 re-derived; total 319/2957 → 332/3241 |
| Validation | A directly-executed Current State section |
| Future Capability | `FCR-0034` Resolved; `FCR-0093`–`FCR-0095` added |
| Technical Debt | `TD-19` Resolved; **no row added** |

**One vocabulary defect found and fixed:**
`MaterialComparisonProperties.Supplier` declared the bare value
`"Supplier"`, which `CanonicalObjectKinds` canonically owns. Caught by
`EngineeringVocabularyConsistencyTests` on the first full run after A1's
uplift; the row key is now `"SupplierOfRecord"`.

No new technical debt was recorded, because none was created: every
deferral is a capability, tracked as an FCR.

---

## 11. What was deliberately not done

- **No dataset, anywhere.** No authoritative source exists in this
  repository for any of the seven domains. Nothing was invented.
- **No import pipeline** — speculative against a hypothetical file shape.
  `FCR-0094`.
- **No `TemperatureDifference` dimension** — nothing uses one.
  `FCR-0095`.
- **No WP16 change** beyond what registration required, and no unrelated
  refactor, release-state change or product-status claim.
- **No selection, calculation, cost or supplier capability** in any
  library.

---

## 12. Git

| Commit | Subject |
|---|---|
| `429efdf` | Add shared reference-data layer; migrate A4 and uplift A1 Materials |
| `d268c01` | Add A2 Standards Library |
| `4534966` | Add A3 Fastener Library |
| `2fc28d3` | Add A5 Mechanical Components Library |
| `4d4ef7c` | Add A6 Engineering Constants Library |
| `6d03cf2` | Add A7 Manufacturing Process Library |
| `4bd1e28` | Wire Group A into the host, and reconcile the governance registers |

Branch `claude/tempestos-a4-bearing-library-unobtf`, pushed. No pull
request opened.
