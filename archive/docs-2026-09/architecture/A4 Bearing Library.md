# A4 Bearing Library

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.Bearings`
**Governing ADR:** `ADR-0124`
**Status:** Implemented, `A4`.

---

## 1. Purpose

A4 is the authoritative, structured, traceable bearing reference library.
It holds what a source said about a bearing — its identity,
classification, dimensions, load ratings, speeds, configuration,
construction, lubrication, standards and provenance — in a form that
downstream engineering work can rely on and cite.

It is **reference data**, not a product catalogue, not a selection engine
and not a calculation module. The engineering principle it serves is a
single sentence: *reference data must be authoritative, structured,
traceable and reusable.*

---

## 2. Architecture

A4 follows the pattern this platform already established twice, and
introduces no new storage, query, relationship or lifecycle mechanism of
its own.

```
IBearingCatalog  ──uses──>  IEngineeringDocumentStore   (Kind = "BearingReference")
       │                            └── revisions, authorship, document references
       └──uses──>  IPersistenceStore
                        ├── "Bearings.Index"            bearingId ──> documentId
                        └── "Bearings.PartNumberIndex"  manufacturer+part ──> bearingId

IBearingValidationService ──reads──> IBearingCatalog
                          ──reads──> IMaterialCatalog   (optional collaborator)

BearingComparer           ──pure──>  reads IBearing values only
```

- **Storage** is `IEngineeringDocumentStore` (`ADR-0053`, `ADR-0072`).
  Each bearing record is one document of `Kind = "BearingReference"`; the
  catalogue is an indexed, typed view over it, never a second store.
- **The two `IPersistenceStore` indexes** exist for the reason `ADR-0055`
  disclosed for Materials: the document store can neither look a document
  up by an arbitrary caller-chosen string nor enumerate documents of a
  `Kind`. The second index additionally enforces manufacturer-part-number
  uniqueness, which the first cannot.
- **Relationships** are open-string `DocumentReference`s (`ADR-0073`).
  A4 introduces **no new relationship vocabulary at all**: supersession
  reuses the platform's existing `supersedes`
  (`GovernanceRelationshipKinds.Supersedes`), in the same direction
  `Decision.SupersedesAsync` already uses — the replacement links to the
  record it supersedes. Inventing a bearing-specific second value for one
  concept is precisely the drift `ADR-0073` names as the cost of an open
  vocabulary.
- **Units** are `Tempest.Core.UnitsAndQuantities` throughout. A4 adds two
  dimensions (`RotationalSpeed`, `PlaneAngle`) and reimplements no
  conversion logic.
- **Materials** are referenced by `materialId`, never redefined.
- **Identity** supplies revision authorship; A4 records no principal of
  its own.
- **Validation results** reuse `EngineeringDomain`'s `IValidationResult`
  shape, exactly as `IRequirementValidationService` does.

### 2.1 Separation of concerns

A bearing **record** describes engineering characteristics. It does not
carry selection logic, calculation methodology, application suitability,
or supplier and commercial information. Those are separate concerns with
separate owners:

| Concern | Owner | A4's role |
|---|---|---|
| Bearing reference data | **A4** | owns it |
| Bearing selection | P02 Engineering Intelligence | supplies the evidence |
| Bearing calculations | the calculation modules | supplies the inputs |
| Bearing arrangement / application configuration | mechanical design | supplies the reference record |
| Supplier, price, availability | P03 Commercial Intelligence | supplies the identity to key off |

---

## 3. Canonical model

`IBearing` is the registered record; `BearingDefinition` is its
engineering content. The split is deliberate: the definition is what a
source said, the rest is catalogue governance.

```
IBearing
  BearingId                string   TempestOS identity, caller-assigned, stable
  Definition               BearingDefinition
  ValidationState          BearingValidationState
  SupersededByBearingId    string?
  UnderlyingDocumentId     Guid
  RevisionNumber           int

BearingDefinition
  Identity                 BearingIdentity        required
  Family                   BearingFamily          required
  Geometry                 BearingGeometry        required
  Provenance               BearingProvenance      required
  LoadRatings              BearingLoadRatings?
  SpeedRatings             IReadOnlyList<BearingSpeedRating>
  Configuration            BearingConfiguration?
  Construction             BearingConstruction?
  Lubrication              BearingLubrication?
  Mass                     Quantity<Mass>?
  Standards                IReadOnlyList<BearingStandardReference>
  ApplicationClassification string?
  ManufacturerAttributes   IReadOnlyDictionary<string,string>
  Notes                    string?
  EffectiveDate            DateOnly?
```

**Missing is never zero.** Every optional field is nullable, and a field
the source did not supply stays `null`. Nothing in this library
substitutes a default for an unknown engineering value.

---

## 4. Schema

Fields are grouped into the five categories §19 of A4's charter requires;
the categories are structural, not documentary.

### 4.1 Core — identity and classification

| Field | Type | Unit | Required | Provenance | Notes |
|---|---|---|---|---|---|
| `BearingId` | string | — | yes | catalogue | TempestOS identity; never derived from a part number |
| `Identity.Manufacturer` | string | — | yes | source | rejected if blank |
| `Identity.ManufacturerPartNumber` | string | — | yes | source | unique per manufacturer |
| `Identity.Designation` | string? | — | no (warned) | source | `TEMPEST-BRG-009` if absent |
| `Identity.Series` | string? | — | no | source | |
| `Identity.Variant` | string? | — | no | source | |
| `Identity.FamilyDesignation` | string? | — | required when `Family = Other` | source | the source's own wording, verbatim |
| `Identity.EquivalentReferences` | list | — | no | claimant | each records who claims the equivalence |
| `Family` | enum | — | yes | source | `TEMPEST-BRG-007` if `Unspecified` |
| `ApplicationClassification` | string? | — | no | source | the source's own grouping, never a judgement |

### 4.2 Dimensional

All lengths are `Quantity<Length>`, stored in the unit the source quoted.

| Field | Applicability | Notes |
|---|---|---|
| `Geometry.Bore` | all | `> 0` (`TEMPEST-BRG-001`) |
| `Geometry.OutsideDiameter` | all | `> Bore` (`TEMPEST-BRG-002`) |
| `Geometry.Width` | all | `> 0` (`TEMPEST-BRG-003`) |
| `Geometry.OverallWidth` | families where it differs from `Width` | `>= Width` (`TEMPEST-BRG-021`) |
| `Geometry.ChamferMinimum` | all | |
| `Geometry.AdditionalDimensions` | family- and source-specific | keyed by the source's own symbol (`"da min"`, `"Da max"`, cone/cup widths, raceway dimensions) |

### 4.3 Load ratings — `BearingRatedValue<Force>`, each carrying its own origin

| Field | Conventional symbol | Applicability |
|---|---|---|
| `BasicDynamicRadial` | C | radial families |
| `BasicStaticRadial` | C0 | radial families |
| `BasicDynamicAxial` | Ca | thrust and combined-load families |
| `BasicStaticAxial` | C0a | thrust and combined-load families |
| `FatigueLoadLimit` | Pu | where the source supplies one |
| `ManufacturerRatings` | source's own labels | manufacturer-specific, kept verbatim |

### 4.4 Speed — a list, never one number

`BearingSpeedRating(Kind, BearingRatedValue<RotationalSpeed>, ManufacturerDesignation?)`,
with `Kind` one of `ReferenceSpeed`, `LimitingSpeed`,
`GreaseLubricatedSpeed`, `OilLubricatedSpeed`, `SealLimitedSpeed`,
`ManufacturerSpecified`. Each rating keeps its own origin and its own
`Conditions` text. Collapsing these into a single "max RPM" would destroy
the engineering meaning; the model refuses to.

### 4.5 Type-specific — applicability from `BearingFamilyTraits`

| Field | Applicable when |
|---|---|
| `Configuration.ContactAngle` | `HasContactAngle(family)` — angular-contact ball, tapered roller, spherical roller, thrust roller |
| `Configuration.InternalClearanceClass` / `PreloadClass` / clearance range | `HasInternalClearance(family)` — every rolling-element family |
| `Configuration.Rows` | `HasRowConfiguration(family)` |
| `Construction.RollingElementMaterialId` | `HasRollingElements(family)` |
| `Construction.CageMaterialId` / `CageDesignation` | `HasCage(family)` |

Clearance, preload and precision classes are held as the source's own
designation plus the standard that defines it, not as an enum: `C3` means
something only against the standard defining it, and no
manufacturer-neutral numeric scale exists to normalise onto.

### 4.6 Manufacturer-specific

`ManufacturerAttributes` (free key/value, verbatim),
`LoadRatings.ManufacturerRatings`,
`Sealing.ManufacturerDesignation`, `Construction.ManufacturerDesignation`,
`Configuration.ArrangementDesignation`, `Identity.FamilyDesignation`.
Data that cannot be normalised without losing meaning is kept as written.
Discarding it would be silent data loss; forcing it into a normalised
field would be silent data corruption.

### 4.7 Derived

A4 computes no engineering values. `BearingValueOrigin.DerivedByTempestOS`
exists so that a value another module computes and stores here can never
be mistaken for manufacturer reference data —
`BearingValidationService` warns (`TEMPEST-BRG-020`) whenever one is
present.

### 4.8 Provenance

| Field | Notes |
|---|---|
| `SourceOrganisation`, `SourceDocument` | required to leave `Draft` |
| `SourceRevision`, `SourceDate` | as the source states them |
| `SourceLocation` | page, section, table |
| `ExtractionMethod` | `ManualTranscription`, `StructuredImport`, `AutomatedExtraction`, `Unknown` |
| `VerificationStatus` | `NotVerified` by default — importing is not verifying |
| `ReviewerPrincipalId`, `VerificationDate` | required to reach `Released` |
| `Notes` | |

---

## 5. Taxonomy

`BearingFamily`: `DeepGrooveBall`, `AngularContactBall`, `SelfAligningBall`,
`CylindricalRoller`, `TaperedRoller`, `SphericalRoller`, `NeedleRoller`,
`ThrustBall`, `ThrustRoller`, `Plain`, plus `Unspecified` (not recorded)
and `Other` (recognised but unnamed by this taxonomy, paired with the
source's own wording).

Extension is two purely additive edits — an enum member and a
`BearingFamilyTraits` row. Nothing switches exhaustively on the enum, so
no architectural change is needed when another family is introduced. The
list above is a starting taxonomy, not a claim of completeness.

---

## 6. Validation lifecycle

```
Draft ──> Checked ──> Validated ──> Released ──> Superseded
  ^          │            │
  └──────────┴────────────┘        (a defect found sends a record back)
```

A family-specific specialisation of the canonical `LifecycleState`
vocabulary (`ADR-0074`), mapped by
`BearingValidationStates.CanonicalEquivalent`: Draft→Draft,
Checked→InReview, Validated→Approved, Released→Released,
Superseded→Superseded.

**Provenance gates the lifecycle**, in `BearingCatalog.SetValidationStateAsync`:

- leaving `Draft` requires a named source organisation and document;
- reaching `Released` additionally requires
  `VerifiedAgainstSource`, a named reviewer and a verification date.

Either refusal is a `BearingProvenanceIncompleteException`. Reference data
earns its status from its source, never from a caller asserting one.

**Released records are immutable.** `ReviseAsync` throws
`ReleasedBearingImmutableException` for a `Released` or `Superseded`
record. The supported path is `SupersedeAsync`: register the corrected
record, supersede the old one, and both survive with a
`supersedes` document reference recorded from the replacement to the
record it replaces. Downstream calculations
that already consumed a released value must still be able to read exactly
what they used.

---

## 7. Revision and history

Every catalogue write is a new document revision. `GetHistoryAsync`
returns every revision oldest-first with its author, timestamp and change
summary; `GetRevisionAsync(bearingId, n)` reconstructs the whole record as
it stood at revision *n*. Nothing is ever overwritten, and a state
transition is a revision too, so the reason a record was released — or
sent back to Draft — is in the same history as the values.

---

## 8. Data quality rules

`IBearingValidationService` reports `TEMPEST-BRG-001`…`022` (catalogued in
`BearingValidationRules`). Errors are claims that a record states
something impossible or that this library requires; warnings are claims
that a record is incomplete or needs a human to look at it. `ValidateCatalogueAsync`
returns a `BearingDataQualityReport` across the whole catalogue —
what a reviewer reads before deciding a dataset is fit to release. It
reports; it never repairs.

Note that a rule fires only where a value is actually recorded. An
unrecorded dimension is never checked as if it were zero.

---

## 9. Search contract

`BearingQuery` is a deterministic filter, not a search engine: no ranking,
no relevance, no free-text index. Unset criteria match everything;
criteria combine with AND; results come back in ascending ordinal
`BearingId` order, so the same query always returns the same set in the
same order.

Filterable: manufacturer (exact), part number (contains), designation
(contains), series, family, validation state, bore / outside diameter /
width ranges, C and C0 minima, mass maximum, speed minimum (optionally of
a named kind), sealing type, clearance class, precision class, referenced
material, construction class.

Dimensional ranges convert to the dimension's base unit before comparing,
so a range in inches correctly matches a record held in millimetres. **A
record that does not record the dimension a range filters on does not
match** — an unrecorded value is never assumed to fall inside a range.

---

## 10. Comparison contract

`BearingComparer.Compare` is pure and synchronous, and produces a
`BearingComparisonResult`: one row per property in
`BearingComparisonProperties.All`, one cell per bearing. Each cell is
`Recorded` (with display text in the source's own unit and a canonical
value for ordering), `NotRecorded`, or `NotApplicable`.

That third state is the point. Comparing a tapered roller bearing against
a deep-groove ball bearing produces a contact-angle row where one cell
holds a value and the other says the property does not apply — not a
blank that reads as a missing measurement. Cross-family comparison is
supported and correct.

The result carries structure and states no verdict: nothing here says
which bearing is better or which should be chosen.

---

## 11. Calculation boundary

A4 supplies reference data. It is not, and must not become, the bearing
calculation module.

A future calculation may consume `C`, `C0`, `Ca`, `C0a`, `Pu`, the
geometry, the speed ratings, the configuration and the construction from
here. **Calculation methodology — life equations, equivalent load, static
safety, lubrication regime — belongs to the calculation modules**
(`Tempest.Core.Calculations`, `ICalculationEngine`), which already have
their own purity and dispatch model (`ADR-0056`).

The only concession A4 makes to derived values is
`BearingValueOrigin.DerivedByTempestOS`, which exists purely so a computed
value stored alongside reference data can never be mistaken for it.

---

## 12. Selection boundary

A4 does not decide suitability. No bearing record declares that it is
"suitable for this application", and no rule in
`BearingValidationService` asks whether it is.

A future selection capability (P02) will consume requirements, loads,
speeds, life, environment, mounting, duty, reliability, lubrication,
temperature and application constraints, and will read A4 for the verified
reference evidence it needs. `BearingFamilyTraits` states what a property
*means* for a family; it never states whether a family suits a job.

The same boundary governs equivalence: `BearingEquivalentReference`
records that *someone* claims two designations describe the same bearing,
and requires naming who. A4 never derives an equivalence from matching
dimensions and never presents a recorded one as interchangeability.

---

## 13. Data vs knowledge

| | Owner |
|---|---|
| "This record states this bore, OD, width and C, according to this source, at this revision, verified by this reviewer." | **A4** |
| "For this load, speed, duty and environment, this bearing may be an appropriate candidate." | P02 Engineering Intelligence |

A4 owns the first and must never contaminate itself with the second.

---

## 14. Data import and the population requirement

**Assessed at implementation time: the repository contains no bearing
dataset.** A direct search of `src/`, `tests/`, `docs/` and `archive/`
found no bearing catalogue, datasheet extract, CSV, JSON dataset, or
schema/planning material for one — the only prior occurrences of the word
"bearing" in the repository are unrelated English usage ("status-bearing",
"load-bearing" in other contexts).

Accordingly, **no bearing data has been populated, and none has been
invented.** Fabricating manufacturer specifications, load ratings, speeds,
dimensions or standards compliance to make the library look complete is
prohibited by A4's own charter and would defeat its purpose. The library
ships empty, with the architecture, rules and provenance model in place.

**Population requirement.** Before A4 can serve engineering work, a real
dataset must be loaded from authoritative sources, in this order of
preference: recognised international or national standards for boundary
dimensions and designation systems; manufacturer technical catalogues;
manufacturer datasheets; authoritative engineering references. Arbitrary
web pages are not authoritative and must not be treated as such. Each
record's own `BearingProvenance` must name the organisation, document,
revision and location it came from, and must stay `NotVerified` until a
named reviewer has actually checked it.

**No importer has been built.** Writing one against a hypothetical file
shape, with no dataset to validate it against, would be speculative work;
`IBearingValidationService.ValidateDefinitionAsync` already provides the
pre-write check an importer needs, and `ValidateCatalogueAsync` provides
the data-quality report it would be judged by. See §16.

---

## 15. Application boundary

`IBearingCatalog` and `IBearingValidationService` are ordinary Platform
Services, registered as singletons in `TempestHost` alongside
`IMaterialCatalog`. A4 introduces no API framework, no new hosted service,
and no standalone application. No workspace surface is added: A4's
priority order is model → validation → persistence → query → provenance →
tests, and no existing surface required a bearing view to function.

---

## 16. Deferred

Each of these is a deliberate, disclosed omission, not an oversight.

| Deferred | Why |
|---|---|
| Bearing dataset population | no authoritative source is present in the repository; inventing data is prohibited (§14) |
| A dataset importer | nothing to import and no real file shape to write against; the validation seam an importer needs already exists |
| Temperature limits on `BearingLubrication` | `Unit<TDimension>` cannot express an affine scale such as °C (`ADR-0124`, `FCR-0034`) |
| Schema versioning of bearing documents | `ADR-0120`'s machinery is scoped to `EngineeringObjectState`; extending it to catalogue documents is its own decision |
| A search index | `SearchAsync` is a deterministic filtered enumeration; `BearingQuery` would not change if an index were added later |
| A reconciliation service | Materials and Requirements each have one (`TD-67`/`TD-97`); A4's equivalents are the stale-index guards in `ListAsync`/`FindAsync` plus `ValidateCatalogueAsync` |
| Workspace / UI surface | not required to integrate A4; UI is explicitly not this work package's deliverable |

---

## 17. Future integration

- **P02 Engineering Intelligence** — reads `SearchAsync` for candidates
  and `BearingComparer` for structured comparison; owns every suitability
  judgement A4 refuses to make.
- **P05 Engineering Assets** — bearing calculation and verification
  templates consume `IBearing.Definition` as verified input, and cite
  `UnderlyingDocumentId` plus `RevisionNumber` so a calculation records
  exactly which revision of which record it used.
- **P06 AI Knowledge & Academy** — worked examples and bearing-selection
  teaching material cite released records and their provenance, so a
  worked example can name its source.
- **P03 Commercial Intelligence** — keys supplier, price and availability
  off `Manufacturer` + `ManufacturerPartNumber`; none of that data belongs
  on a bearing record.
- **P07 Business Governance & Scale** — consumes the validation lifecycle
  and the data-quality report as governance evidence.

The seam in every case is the same: read `IBearingCatalog`, cite the
document Id and revision, and add nothing to A4.
