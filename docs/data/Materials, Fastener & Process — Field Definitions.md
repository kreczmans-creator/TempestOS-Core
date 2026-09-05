# Materials, Fastener & Process — Field Definitions

## Register Metadata

| Field | Value |
|---|---|
| **Document Name** | Materials, Fastener & Process — Field Definitions |
| **Purpose** | The single authoritative **schema contract** every future Materials, Fastener and Process dataset is authored against — column names, types, units, controlled vocabularies, normalisation rules and provenance requirements — so that datasets produced outside this repository can later be validated and imported into TempestOS without re-keying. |
| **Scope** | Schema definition only. **This document defines no data.** It creates no dataset record, no seed file, no importer, no validator, and changes no application, schema or governance code. |
| **Status** | **Baseline v1.0**, 2026-09-05. Binding on every dataset authored from this date. Amendable only per §12. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | The repository itself, for everything marked **Repo-defined** below: `src/Tempest.Core/Materials/`, `src/Tempest.Core/UnitsAndQuantities/`, `src/Tempest.Core/EngineeringDomain/Contracts/`. This document is the source of truth only for what is marked **Dataset-defined**. |
| **Related Work** | `docs/roadmap/Parallel Programme A — Engineering Reference Data.md` (`A.1` Materials, `A.3` Fasteners, `A.7` Manufacturing Processes — the packages that produce data against this contract). |
| **Related ADRs** | `ADR-0053` (Engineering Data Model), `ADR-0054` (Units & Quantities), `ADR-0055` (Materials). Cited, not modified. |
| **Related Capabilities** | `FCR-0029`–`FCR-0033` (the five Engineering Foundation frameworks). |
| **Coverage Status** | **Complete for Materials** — every column maps onto a real repository type. **Partial by necessity for Fasteners and Processes** — no TempestOS domain type exists for either today (§3d); their schemas are defined at dataset level and their mapping is explicitly **Unknown**, recorded as such rather than invented. |

---

## 1. What This Document Is For

Datasets for `A.1` (Materials), `A.3` (Fasteners) and `A.7` (Manufacturing
Processes) are being authored **outside this repository**. Deferring the
import is deliberate and cheap. Deferring the *format* is neither: fifty
rows in the wrong shape is a re-keying job, not an import.

This document is therefore the contract fixed **before** authoring
begins. An author — human or assistant — should be able to work from this
file alone, without reading the source, and produce files that validate
on first import.

**A note on filename.** The commissioning instruction named the target
`docs/data/Materials/Fastener/Process - Field Definitions.md`. Read
literally that is three nested directories and one file; the intent is
one document covering all three subjects. A path separator cannot appear
in a filename, so the slashes are rendered as a list. This is a
disclosed deviation from the instruction's literal text, not a silent
rename.

## 2. What This Document Does Not Do

- It defines **no dataset records** and ships no CSV, JSON or YAML.
- It defines **no importer and no validator** — both are later, numbered
  technical Work Packages, and both are out of scope here.
- It changes **no application code, no persistence schema, and no
  governance decision.**
- It introduces **no field merely because it may be useful later.** Every
  column below exists because a repository type, a stated acceptance
  criterion in Programme A, or an unavoidable authoring need requires it.

## 3. Repository Evidence — What Already Exists

Everything in this section is **Verified** by direct reading of the
source at the commit this document was written against.

### 3a. The Materials framework (`ADR-0055`)

`src/Tempest.Core/Materials/` defines the shape a material actually takes:

| Type | Shape | Consequence for the dataset |
|---|---|---|
| `MaterialSpecification` | `MaterialId` (string, non-blank), `Name` (string, non-blank), `Category` (string, **nullable**), `Properties` (`IReadOnlyDictionary<string, MaterialProperty>`) | One entity row per material; properties are a keyed collection, not fixed columns |
| `MaterialProperty` | `Value` (a boxed `Quantity<TDimension>`) + `Provenance` — **mandatory by construction** | Every property value carries its own provenance; provenance is not a row-level afterthought |
| `MaterialPropertyProvenance` | `SourceReference` (string?), `SourceRevision` (**int?**), `ValidationStatus`, `ConfidenceLevel`, `ApplicableConditions` (string?), `Notes` (string?) | These exact six names become the dataset's provenance columns |
| `MaterialPropertyDto` | `DimensionKind`, `Value`, `UnitSymbol`, `UnitToBaseFactor`, `Provenance` | The stored wire shape; the dataset mirrors the first three |

Two enums are **Repo-defined controlled vocabularies** and must be used
verbatim, spelling and casing included:

- `MaterialPropertyValidationStatus` = `Unvalidated` | `Validated` | `Superseded`
- `MaterialPropertyConfidenceLevel` = `Unknown` | `Low` | `Medium` | `High`

`MaterialPropertyProvenance.Unknown` is the framework's own honest
default — every field null or its "not assessed" member. The dataset
inherits that discipline: **absent is recorded as absent, never as a
plausible value.**

### 3b. Units & Quantities — seven dimensions, and only seven (`ADR-0054`)

`MaterialPropertyValueCodec` bounds every material property value to
seven dimensions, by exhaustive type-pattern match, with no reflection
and no type-name deserialisation:

> `Length, Mass, Duration, Force, Pressure, Area, Volume`

`Unit<TDimension>` carries a `Symbol` and a `ToBaseUnitFactor`;
`Quantity<TDimension>` formats as `"{Value} {Unit.Symbol}"` under the
invariant culture and parses back through `TryParse` against a known-unit
list. The published unit catalogue is a **deliberate starting set**, not
a claim of completeness, and extending it is purely additive.

### 3c. The consequence that shapes this whole schema

**There is no Temperature dimension. There is no dimensionless
dimension. There is no compound dimension (density, thermal
conductivity, expansion coefficient, specific heat).**

That is not an oversight to route around here. It means the familiar
material properties split into two tiers, and the dataset must say which
tier every property is in:

- **Tier 1 — importable today.** Expressible as one of the seven
  dimensions. Yield strength, tensile strength and elastic moduli are
  `Pressure`; masses are `Mass`; sizes are `Length`; stress areas are
  `Area`; loads are `Force`.
- **Tier 2 — carried, not importable today.** Density, Poisson's ratio,
  elongation, hardness, thermal conductivity, expansion coefficient,
  specific heat, service temperature, and every qualitative rating.

Tier 2 columns are still authored, because the data is worth having and
because re-gathering it later costs more than carrying it now. They are
authored in a **separate file** (§6c) so that no importer ever has to
decide what to do with a value the domain cannot represent. Promoting a
Tier 2 property to Tier 1 requires a Units & Quantities extension, which
is a numbered technical Work Package with its own ADR — **not** a change
an author of this dataset may make.

### 3d. Fasteners and Processes — no domain type exists

Searched and **Verified**: `src/Tempest.Core/` contains no fastener type,
no thread type, no property-class type, no process-family type and no
process-capability type. The nearest existing concepts are
`IPart`/`IComponent`/`IPurchaseItem` (`PhysicalConfiguration.cs`,
`SupplyChain.cs`) and `IManufacturingOperation` (`TestManufacturing.cs`)
— all of which are engineering *objects in a project*, not catalogue
reference data, and none of which is a fit today.

So the Fastener and Process schemas below are **Dataset-defined
throughout**, deliberately shaped like the Materials schema so that one
importer can serve all three if a later Work Package decides so. Their
target TempestOS type is **Unknown**, and stated as Unknown.

## 4. File Set, Format and Naming

### 4a. The entity/property split

The Materials domain stores properties as a **keyed collection**, not as
fixed columns. A wide "one row per material, one column per property"
sheet cannot carry per-property provenance without multiplying every
property by six provenance columns.

Each subject therefore has **two files**, and the property file is
long-form — one row per (entity, property):

| File | Grain | Maps to |
|---|---|---|
| `<Subject>.csv` | One row per entity | `MaterialSpecification`'s own identity and classification fields |
| `<Subject>Properties.csv` | One row per entity × property | The `Properties` dictionary — each row is one `MaterialProperty` |
| `<Subject>PropertiesTier2.csv` | One row per entity × property | Nothing yet — carried data, `Unknown` mapping (§3c) |

The canonical file names are:

- `Materials.csv`, `MaterialProperties.csv`, `MaterialPropertiesTier2.csv`
- `Fasteners.csv`, `FastenerProperties.csv`, `FastenerPropertiesTier2.csv`
- `Processes.csv`, `ProcessProperties.csv`, `ProcessPropertiesTier2.csv`

Long form has a second benefit the wide form cannot offer: **a property
that is unknown is simply absent.** There is no blank cell to
misinterpret, and no placeholder to mistake for a measurement.

### 4b. Format rules (all files)

| Rule | Requirement |
|---|---|
| Encoding | UTF-8, **no BOM**. Required — unit symbols `mm²`, `m³` are non-ASCII and must survive verbatim |
| Format | RFC 4180 CSV; comma delimiter; `LF` line endings |
| Header | Exactly one header row, column names verbatim from this document, in the order given, no extra columns |
| Quoting | Quote any field containing a comma, quote or newline; escape an embedded quote by doubling it |
| Decimals | `.` decimal point, invariant culture; no thousands separators; no `1,5` |
| Numbers | Digits, optional sign, optional `.`, optional `e`/`E` exponent. No `~`, `<`, `>`, `±`, no ranges in a numeric field, no unit suffix inside a numeric field |
| Empty cells | Permitted **only** where a column is marked Optional. Never as a stand-in for an unknown value in a mandatory field |
| Whitespace | Trimmed leading and trailing; no tab characters inside fields |
| Case | Controlled-vocabulary values are **case-sensitive** and must match the listed spelling exactly |

### 4c. The two rules that matter most

1. **A unit never travels inside a number.** `Value` and `UnitSymbol`
   are always separate columns. `"250 MPa"` in a `Value` cell is invalid.
2. **A row is never invented to be complete.** If a property is not
   known, omit its row. If an entity's classification is not known, write
   the explicit `Unknown` token where the column permits it. A plausible
   number is worse than no number, because it cannot be distinguished
   from a measured one afterwards.

## 5. The Provenance Block — Common To Every Property File

These six columns appear, identically, in every `*Properties.csv` and
`*PropertiesTier2.csv` file. Their names are taken verbatim from
`MaterialPropertyProvenance` so that mapping is one-to-one and needs no
translation table.

| Column | Meaning | Type | Req. | Allowed values / normalisation | Example |
|---|---|---|---|---|---|
| `SourceReference` | The engineering source the value was taken from — a standard, a datasheet, a test report | Text | **Mandatory** | Free text; repo-nullable but **mandatory here**: an uncited row fails Programme A's own acceptance criteria. Cite the document, not the website. Format: `<Body> <Number>:<Year>` where the source is a standard | `EN 10025-2:2019` |
| `SourceRevision` | The revision of that source, **as an integer** | Integer | Optional | **Repo type is `int?`** — a text edition like `2019+A1` cannot be stored. Use the four-digit year where the source is a dated standard, or the integer revision where one exists. Put the full edition string in `SourceReference`. Leave empty if the source has no integer revision | `2019` |
| `ValidationStatus` | Whether the value has been independently checked | Enum | **Mandatory** | **Repo-defined:** `Unvalidated` \| `Validated` \| `Superseded`. Default for newly authored rows is `Unvalidated` — it is not a claim the value is wrong, only that nothing has confirmed it | `Unvalidated` |
| `ConfidenceLevel` | How confidently the value is believed accurate | Enum | **Mandatory** | **Repo-defined:** `Unknown` \| `Low` \| `Medium` \| `High`. `Low` = typical/nominal value from a general reference; `Medium` = manufacturer datasheet; `High` = certified test result traceable to a specimen or batch. **A value recalled by a language model without a source in hand is `Unknown`, never `Low`** | `Medium` |
| `ApplicableConditions` | The conditions the value is valid under | Text | Optional | Free text. State temperature, temper, orientation, section thickness or loading rate where the value depends on them. **Not** a place for a second numeric value | `Room temperature, thickness ≤ 16 mm` |
| `Notes` | Anything not captured by another column | Text | Optional | Free text. Never used to carry a value, a unit or a source | `Value is the minimum specified, not typical` |

**Provenance requirement, stated once:** every row in every property file
carries this block. There is no property row without provenance —
`MaterialProperty` cannot be constructed without it, by design, and the
dataset does not get to be laxer than the type it feeds.

**Confidence requirement, stated once:** `ConfidenceLevel` is a claim
about evidence, not about how reasonable a number looks. If the author
does not have the cited source in front of them, the honest value is
`Unknown` and `ValidationStatus` is `Unvalidated`.

## 6. A. Materials

### 6a. `Materials.csv` — one row per material

| Column | Meaning | Type | Unit | Req. | Allowed values / normalisation | Example |
|---|---|---|---|---|---|---|
| `MaterialId` | The catalogue key; the `materialId` index key the framework uses | Text | — | **Mandatory** | Non-blank, unique, **case-sensitive**, stable for the life of the row. `A-Z a-z 0-9 - .` only; no spaces. Recommended form `<Family>-<Designation>`, punctuation stripped from the designation | `STEEL-S355J2` |
| `Name` | The human-readable name | Text | — | **Mandatory** | Non-blank free text. The name an engineer would say | `S355J2 structural steel` |
| `Category` | Classification | Text | — | Optional | Maps to the framework's own nullable `Category`. **Dataset-defined vocabulary**, §10a. Empty means genuinely unclassified — not "not yet decided" | `Steel` |
| `Designation` | The formal designation within its standard | Text | — | Optional | Verbatim from the standard, including spaces and punctuation as published | `S355J2` |
| `StandardReference` | The standard defining the material | Text | — | Optional | Same format as `SourceReference` (§5). Relates to the `A.2` Standards Library once that exists (§8) | `EN 10025-2:2019` |
| `Condition` | Temper, heat treatment or delivery condition | Text | — | Optional | **Dataset-defined vocabulary**, §10b. `Unknown` permitted explicitly | `Normalised` |
| `Form` | The product form the row describes | Text | — | Optional | **Dataset-defined vocabulary**, §10c | `Plate` |
| `Status` | Whether the row is live | Enum | — | **Mandatory** | **Dataset-defined:** `Active` \| `Deprecated` \| `Draft`. `Draft` means authored but not yet reviewed | `Draft` |
| `Notes` | Row-level notes | Text | — | Optional | Free text. Never carries a property value | — |

`MaterialId` is the join key for every other file. Nothing else joins.

### 6b. `MaterialProperties.csv` — Tier 1, one row per property

| Column | Meaning | Type | Unit | Req. | Allowed values / normalisation | Example |
|---|---|---|---|---|---|---|
| `MaterialId` | The material this property belongs to | Text | — | **Mandatory** | Must match a `MaterialId` in `Materials.csv` exactly. **Referential** | `STEEL-S355J2` |
| `PropertyKey` | The dictionary key the property is stored under | Text | — | **Mandatory** | **Dataset-defined vocabulary**, §10d. PascalCase, no spaces. Unique per `MaterialId` — one row per key, per material | `YieldStrength` |
| `DimensionKind` | Which of the seven dimensions the value is | Enum | — | **Mandatory** | **Repo-defined, exactly seven:** `Length` \| `Mass` \| `Duration` \| `Force` \| `Pressure` \| `Area` \| `Volume`. Must be the dimension §10d assigns to `PropertyKey` | `Pressure` |
| `Value` | The numeric magnitude | Decimal | Per `UnitSymbol` | **Mandatory** | Finite number, invariant culture. **As published** — see precision, below | `355` |
| `UnitSymbol` | The unit the magnitude is expressed in | Text | — | **Mandatory** | **Repo-defined per dimension**, §7b. Must be a symbol in the catalogue for `DimensionKind`, character-exact including `²`/`³` | `MPa` |
| `SourceReference`, `SourceRevision`, `ValidationStatus`, `ConfidenceLevel`, `ApplicableConditions`, `Notes` | Provenance | — | — | Per §5 | Per §5 | Per §5 |

**Precision.** Record the value exactly as the source publishes it, at
the source's own significant figures. Do not round, do not extend, and
**do not convert units** — `UnitSymbol` accepts whatever the source used,
and the framework converts (`Unit<TDimension>.ToBaseUnitFactor`,
`Quantity.ConvertTo`) far more reliably than an author with a
calculator. A converted value silently loses its traceability to the
printed page it came from.

**Ranges.** The schema stores one number. Where a standard publishes a
range, author the row the design actually uses — normally the specified
minimum for a strength — and say which in `Notes`. Never split the
difference.

### 6c. `MaterialPropertiesTier2.csv` — carried, not importable

Identical columns to §6b, with two differences:

| Column | Difference |
|---|---|
| `DimensionKind` | **Replaced by** `UnitText` — free text (`kg/m³`, `W/m·K`, `µm/m·K`, `%`, `HB`, `°C`), because these dimensions do not exist in the framework. Mandatory; write `-` for a genuinely dimensionless ratio |
| `Value` | May be **numeric or text**, since hardness scales and qualitative ratings are not numbers. Text values use the vocabulary in §10e where one applies |

`PropertyKey` values for this file are listed in §10d, Tier 2. Everything
in §5 still applies unchanged: every row is cited, every row carries a
confidence level.

## 7. Units

### 7a. Stored versus display

| Concept | Rule |
|---|---|
| **Authored unit** | Whatever the cited source published. The dataset preserves it |
| **Canonical/base unit** | The SI base of each dimension: `m`, `kg`, `s`, `N`, `Pa`, `m²`, `m³`. The framework normalises via `ToBaseUnitFactor`; **the author never does this by hand** |
| **Display unit** | Not a dataset concern. Presentation belongs to the application, and no display preference is expressed in these files |

### 7b. Permitted unit symbols — Repo-defined, character-exact

| Dimension | Base | Permitted symbols |
|---|---|---|
| `Length` | `m` | `m`, `mm`, `cm`, `km`, `in`, `ft`, `yd`, `mi` |
| `Mass` | `kg` | `kg`, `g`, `mg`, `t`, `lb`, `oz` |
| `Duration` | `s` | `s`, `ms`, `min`, `h` |
| `Force` | `N` | `N`, `kN`, `lbf` |
| `Pressure` | `Pa` | `Pa`, `kPa`, `MPa`, `bar`, `psi` |
| `Area` | `m²` | `m²`, `mm²`, `ft²` |
| `Volume` | `m³` | `m³`, `L`, `ft³`, `gal` |

Any other symbol is invalid in a Tier 1 file — including `GPa`, `N/mm²`,
`kgf` and `ksi`, none of which is in the catalogue today. Express an
elastic modulus in `MPa` rather than `GPa`; `N/mm²` is numerically `MPa`
and is written as `MPa`.

This catalogue is a starting set and extending it is purely additive —
but extending it is a code change, therefore a numbered technical Work
Package, and **not** something a dataset author may assume.

## 8. B. Fasteners

**Mapping status: Unknown.** No TempestOS type models a fastener today
(§3d). These files are reference data with no import target yet, shaped
deliberately like §6 so one may serve later.

### 8a. `Fasteners.csv` — one row per fastener

| Column | Meaning | Type | Unit | Req. | Allowed values / normalisation | Example |
|---|---|---|---|---|---|---|
| `FastenerId` | Catalogue key | Text | — | **Mandatory** | Unique, case-sensitive, `A-Z a-z 0-9 - .`, no spaces. Recommended `<Type>-<Thread>-<Length>-<Class>` | `HEXBOLT-M10x1.5-40-8.8` |
| `Name` | Human-readable name | Text | — | **Mandatory** | Free text | `M10 × 40 hex bolt, class 8.8` |
| `FastenerType` | What kind of fastener | Text | — | **Mandatory** | **Dataset-defined vocabulary**, §10f | `HexBolt` |
| `StandardReference` | The standard defining it | Text | — | Optional | As §5 | `ISO 4014:2022` |
| `ThreadStandard` | The thread system | Text | — | **Mandatory** | **Dataset-defined:** `ISO-Metric` \| `Unified` \| `Other`. The thread *designation* is `ThreadDesignation`; the standard's own dimensional tables are not reproduced here | `ISO-Metric` |
| `ThreadForm` | Coarse or fine | Text | — | **Mandatory** | **Dataset-defined:** `Coarse` \| `Fine` \| `ExtraFine` \| `Unknown` | `Coarse` |
| `ThreadDesignation` | The designation as published | Text | — | **Mandatory** | Verbatim, including `×`/`x` as the source writes it | `M10x1.5` |
| `HeadType` | Head style | Text | — | Optional | **Dataset-defined vocabulary**, §10g | `Hexagon` |
| `DriveType` | Drive style | Text | — | Optional | **Dataset-defined vocabulary**, §10h | `ExternalHex` |
| `PropertyClass` | Strength class or grade | Text | — | Optional | Verbatim as designated (`8.8`, `10.9`, `A2-70`, `A4-80`). **Not** a controlled list — the designation systems are the standards', and enumerating them here would create a competing vocabulary | `8.8` |
| `MaterialId` | The material it is made from | Text | — | Optional | **Referential** — must match a `MaterialId` in `Materials.csv`, or be empty. Never a free-text material name | `STEEL-8.8` |
| `Finish` | Coating or surface treatment | Text | — | Optional | **Dataset-defined vocabulary**, §10i | `ZincPlated` |
| `Status` | Whether the row is live | Enum | — | **Mandatory** | `Active` \| `Deprecated` \| `Draft` | `Active` |
| `Preferred` | Whether this is a stocked, preferred item | Boolean | — | Optional | `true` \| `false` only, lowercase. Empty means not assessed | `true` |
| `Notes` | Row-level notes | Text | — | Optional | Free text | — |

### 8b. `FastenerProperties.csv` — Tier 1

Columns exactly as §6b, with `MaterialId` replaced by `FastenerId`
(referential to `Fasteners.csv`). Dimensional and load properties map
cleanly onto the seven dimensions: `NominalDiameter` and `Length` are
`Length`; `TensileStressArea` is `Area`; `ProofLoad` and
`RecommendedPreload` are `Force`; `Mass` is `Mass`.

**Torque has no home in Tier 1** — the framework has no torque
dimension. `TighteningTorque` is a Tier 2 property, in `N·m`, and its
`ApplicableConditions` **must** state the assumed friction coefficient,
per `A.3`'s own acceptance criterion that no torque figure travels
without its assumption.

### 8c. `FastenerPropertiesTier2.csv`

Columns exactly as §6c, keyed by `FastenerId`.

## 9. C. Processes

**Mapping status: Unknown**, per §3d.

### 9a. `Processes.csv` — one row per process

| Column | Meaning | Type | Unit | Req. | Allowed values / normalisation | Example |
|---|---|---|---|---|---|---|
| `ProcessId` | Catalogue key | Text | — | **Mandatory** | Unique, case-sensitive, `A-Z a-z 0-9 - .`, no spaces | `MACH-MILL-3AXIS` |
| `Name` | Human-readable name | Text | — | **Mandatory** | Free text | `3-axis CNC milling` |
| `ProcessFamily` | Top-level classification | Text | — | **Mandatory** | **Dataset-defined vocabulary**, §10j | `Machining` |
| `ProcessSubtype` | Second-level classification | Text | — | **Mandatory** | **Dataset-defined vocabulary**, §10k, and must be valid for its `ProcessFamily` | `Milling` |
| `Description` | What the process does | Text | — | Optional | Free text, one or two sentences | — |
| `ApplicableMaterialCategories` | Which material categories it applies to | Text | — | Optional | Semicolon-separated list of §10a values, no spaces around the separator. **Referential to the vocabulary, not to `Materials.csv`** — a process applies to a class, not to a catalogue row | `Steel;Aluminium` |
| `DesignConstraints` | The DFM constraints it imposes | Text | — | Optional | Free text. Constraints only; no costs, no lead times — those belong to `C.2`/`C.3` and are deliberately kept out | `No internal sharp corners; min corner radius set by cutter` |
| `Status` | Whether the row is live | Enum | — | **Mandatory** | `Active` \| `Deprecated` \| `Draft` | `Active` |
| `Notes` | Row-level notes | Text | — | Optional | Free text | — |

**Explicitly out of scope for this file:** cost, rate, lead time and
supplier. Programme A defines engineering capability; Programme C prices
it. Keeping them in separate files keeps a capability statement from
quietly ageing into a price list.

### 9b. `ProcessProperties.csv` and `ProcessPropertiesTier2.csv`

Columns exactly as §6b and §6c, keyed by `ProcessId`.

Tier 1 holds what the seven dimensions can carry — `AchievableTolerance`,
`BestCaseTolerance`, `MinFeatureSize`, `MinWallThickness`, `MaxPartX/Y/Z`
are all `Length`; `MaxPartMass` is `Mass`; `TypicalSetupTime` is
`Duration`. Tier 2 holds `SurfaceFinishRa` (`µm`), batch-size ranges and
every qualitative rating.

A tolerance is a **capability, not a promise**: `ApplicableConditions`
should record the size range or feature type the figure applies to, and
`ConfidenceLevel` should be `Medium` only where a named supplier's own
published capability statement backs it.

## 10. Controlled Vocabularies

Each vocabulary below is labelled by where its authority comes from.
**Repo-defined** vocabularies must match the source exactly and cannot be
extended here. **Dataset-defined** vocabularies are this document's own,
deliberately minimal, and extended only by amendment (§12) — never
ad hoc by an author mid-file. Where a designation system already belongs
to a standards body (thread designations, property classes, bearing
designations), no vocabulary is defined at all: the standard's own
designation is recorded verbatim, because inventing a parallel
classification is exactly what §1 exists to prevent.

**Repo-defined (verbatim, no extension):**

| Vocabulary | Values | Source |
|---|---|---|
| `ValidationStatus` | `Unvalidated`, `Validated`, `Superseded` | `MaterialPropertyValidationStatus` |
| `ConfidenceLevel` | `Unknown`, `Low`, `Medium`, `High` | `MaterialPropertyConfidenceLevel` |
| `DimensionKind` | `Length`, `Mass`, `Duration`, `Force`, `Pressure`, `Area`, `Volume` | `MaterialPropertyValueCodec` |
| `UnitSymbol` | §7b | `*Units` catalogues |

**Dataset-defined (minimal; extend by amendment):**

- **§10a `Category`** — `Steel`, `StainlessSteel`, `Aluminium`, `Copper`,
  `CopperAlloy`, `Titanium`, `Nickel`, `Polymer`, `Elastomer`,
  `Ceramic`, `Composite`, `Other`.
- **§10b `Condition`** — `AsRolled`, `Normalised`, `Annealed`,
  `QuenchedAndTempered`, `ColdDrawn`, `SolutionTreated`, `Aged`,
  `WorkHardened`, `AsCast`, `Unknown`.
- **§10c `Form`** — `Plate`, `Sheet`, `Bar`, `Rod`, `Tube`, `Pipe`,
  `Section`, `Casting`, `Forging`, `Extrusion`, `Wire`, `Powder`.
- **§10d `PropertyKey`** — Tier 1: `YieldStrength`, `TensileStrength`,
  `ElasticModulus`, `ShearModulus`, `CompressiveStrength`,
  `FatigueStrength` (all `Pressure`); `NominalDiameter`, `Length`,
  `Thickness`, `MinFeatureSize`, `MinWallThickness`,
  `AchievableTolerance`, `BestCaseTolerance` (all `Length`);
  `TensileStressArea` (`Area`); `ProofLoad`, `RecommendedPreload`
  (`Force`); `Mass`, `MaxPartMass` (`Mass`); `TypicalSetupTime`
  (`Duration`). Tier 2: `Density`, `PoissonsRatio`, `Elongation`,
  `Hardness`, `ThermalConductivity`, `ThermalExpansionCoefficient`,
  `SpecificHeat`, `MaxServiceTemperature`, `MinServiceTemperature`,
  `CorrosionResistance`, `Machinability`, `Weldability`,
  `RelativeCostIndex`, `TighteningTorque`, `FrictionCoefficient`,
  `SurfaceFinishRa`, `TypicalBatchSizeMin`, `TypicalBatchSizeMax`.
  A key not on this list requires an amendment.
- **§10e Qualitative scale** — for `CorrosionResistance`,
  `Machinability`, `Weldability` and any rating like them:
  `VeryPoor`, `Poor`, `Fair`, `Good`, `Excellent`, `Unknown`. One scale,
  used everywhere, so ratings remain comparable across rows.
- **§10f `FastenerType`** — `HexBolt`, `HexScrew`, `SocketHeadCapScrew`,
  `CountersunkScrew`, `ButtonHeadScrew`, `SetScrew`, `Stud`,
  `ThreadedRod`, `HexNut`, `NylocNut`, `FlangeNut`, `PlainWasher`,
  `SpringWasher`, `Other`.
- **§10g `HeadType`** — `Hexagon`, `HexagonFlange`, `SocketCap`,
  `Countersunk`, `ButtonHead`, `Pan`, `Cheese`, `None`.
- **§10h `DriveType`** — `ExternalHex`, `InternalHex`, `Torx`,
  `Phillips`, `Pozidriv`, `SlottedDrive`, `None`.
- **§10i `Finish`** — `Plain`, `ZincPlated`, `HotDipGalvanised`,
  `Passivated`, `Anodised`, `BlackOxide`, `PTFECoated`, `Other`.
- **§10j `ProcessFamily`** — `Machining`, `SheetMetal`, `Fabrication`,
  `Welding`, `Casting`, `Moulding`, `Additive`, `SurfaceTreatment`,
  `HeatTreatment`, `Assembly`, `Finishing`.
- **§10k `ProcessSubtype`** — `Turning`, `Milling`, `Drilling`,
  `Grinding`, `Boring`, `Broaching`, `LaserCutting`, `PlasmaCutting`,
  `WaterjetCutting`, `Punching`, `Bending`, `Rolling`, `MIG`, `TIG`,
  `SpotWelding`, `SandCasting`, `InvestmentCasting`, `DieCasting`,
  `InjectionMoulding`, `FDM`, `SLS`, `SLA`, `Painting`, `PowderCoating`,
  `Plating`, `Anodising`.

## 11. Disclosed Limitations

Recorded as **Unknown** rather than resolved, because resolving any of
them is a code change and this document changes no code.

1. **No Temperature dimension.** Service temperatures are Tier 2 and
   cannot be imported as `MaterialProperty` values today (§3c).
2. **No dimensionless or compound dimensions.** Density, Poisson's
   ratio, elongation, thermal conductivity and expansion coefficient are
   Tier 2 for the same reason.
3. **`SourceRevision` is an integer.** Edition strings such as
   `2019+A1` cannot round-trip; the mitigation (§5) is to carry the full
   edition in `SourceReference`. Whether the type should widen is a
   product question, not a dataset one.
4. **`Category` is free text in the framework.** §10a's vocabulary is
   dataset-level only and is not enforced by any code today.
5. **`PropertyKey` is an unconstrained dictionary key.** §10d is a
   dataset convention. Nothing in the framework rejects an unlisted key.
6. **Fasteners and Processes have no target type.** Their import path is
   genuinely undecided (§3d), and this document does not decide it.
7. **`GPa`, `N/mm²`, `ksi`, `kgf`, `N·m`, `°C`, `µm` are not in the unit
   catalogue.** Tier 1 authoring must use a catalogued symbol; Tier 2
   carries the rest as text.

None of the above blocks authoring. Every one of them is a reason the
entity/property split and the Tier 1/Tier 2 split exist.

## 12. Change Control

This document is the contract. A dataset that does not match it is
non-conforming, and the correct response is to amend the document
deliberately, not to bend the data.

- **Amendment** = a new version of this document, with a dated entry in
  the table below stating what changed and why.
- Adding a value to a **Dataset-defined** vocabulary is an amendment.
- Adding a unit symbol, a dimension, or promoting a property from Tier 2
  to Tier 1 is **not** an amendment to this document alone — it requires
  the corresponding code change, as a numbered technical Work Package
  with its own ADR, and this document then follows.
- An author who needs a value that does not exist here stops and asks.
  They do not invent one, and they do not silently widen a column.

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-09-05 | Established. Schema baseline for `A.1` Materials, `A.3` Fasteners, `A.7` Manufacturing Processes, derived from the Materials framework (`ADR-0055`) and Units & Quantities (`ADR-0054`) as implemented at this date |
