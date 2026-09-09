# Group A — Engineering Reference Data

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.ReferenceData`
**Governing ADRs:** `ADR-0125`, `ADR-0126`
**Status:** Architecturally complete, `Group A`. Every library ships with
no dataset — see §9.

---

## 1. Purpose

Group A is seven reference libraries and the one layer they share.

| Work Package | Library | Namespace | Document |
|---|---|---|---|
| A1 | Materials Database | `Tempest.Core.Materials` | `A1 Materials Database.md` |
| A2 | Standards Library | `Tempest.Core.Standards` | `A2 Standards Library.md` |
| A3 | Fastener Library | `Tempest.Core.Fasteners` | `A3 Fastener Library.md` |
| A4 | Bearing Library | `Tempest.Core.Bearings` | `A4 Bearing Library.md` |
| A5 | Mechanical Components | `Tempest.Core.Components` | `A5 Mechanical Components Library.md` |
| A6 | Engineering Constants | `Tempest.Core.Constants` | `A6 Engineering Constants.md` |
| A7 | Manufacturing Processes | `Tempest.Core.Manufacturing` | `A7 Manufacturing Process Library.md` |

This document describes what all seven have in common. Each library's own
document describes only what is specific to it.

The engineering principle every library serves is one sentence:
*reference data must be authoritative, structured, traceable and
reusable.*

---

## 2. The shared layer

`ADR-0126` records the decision and the reasoning. In outline: the
governance question a reference library answers — *where did this come
from, has a person checked it, and may engineering work rely on it?* — is
identical whether the record describes a bearing, a standard or a casting
process. The engineering content is not. So the governance is shared and
the content is not.

### 2.1 What is shared

| Concern | Type |
|---|---|
| Provenance | `ReferenceProvenance`, `ReferenceExtractionMethod`, `ReferenceVerificationStatus` |
| Lifecycle | `ReferenceValidationState`, `ReferenceValidationStates` |
| Catalogue | `IReferenceDataCatalog<T>`, `ReferenceDataCatalog<T>` |
| Record | `IReferenceRecord<T>`, `ReferenceRecord<T>` |
| Sourced values | `ReferenceValue<T>`, `ReferenceRange<T>`, `ReferenceQuantityValue`, `ReferenceQuantityCodec` |
| Comparison | `ReferenceComparer`, `ReferenceComparisonResult`, `ReferenceComparisonCell` |
| Validation | `IReferenceValidationService<T>`, `ReferenceValidationService<T>`, `ReferenceValidationRules` |
| Citation | `StandardReference` |
| Cross-library seams | `IStandardResolver`, `IReleasedConstantSource` |
| Errors | `ReferenceDataException` and six subtypes |

### 2.2 What each library owns

Its definition record, its family taxonomy, its family-traits table, its
query type and evaluator, its comparison property list, its rule series,
its document `Kind` and its index collection names. Nothing else.

A definition is a plain record with no base type. It carries no identity,
no provenance, no validation state and no revision number: those belong
to the registered record, because they are catalogue governance rather
than engineering description.

---

## 3. Storage

Every record in every library is one `IEngineeringDocument` of that
library's own `Kind`, with the record serialised as JSON into the
document's own revision content. Revision history, authorship and
document relationships all come from the shared store; no library invents
any of them.

Each library also holds two `IPersistenceStore` index collections: record
Id to document Id, and the library's own secondary uniqueness key to
record Id. This is a thin, typed index over the document store, never a
second storage mechanism — the pattern `ADR-0055` established for
Materials and `ADR-0058` repeated for Requirements. The indexes exist
because `IEngineeringDocumentStore` can neither look a document up by an
arbitrary caller-chosen string nor enumerate documents of a given `Kind`.

| Library | Document `Kind` | Secondary key |
|---|---|---|
| Materials | `MaterialSpecification` | supplier + designation |
| Standards | `StandardReference` | body + designation + edition |
| Fasteners | `FastenerReference` | manufacturer + part number, else manufacturer + designation |
| Bearings | `BearingReference` | manufacturer + part number |
| Components | `ComponentReference` | manufacturer + part number, else manufacturer + designation |
| Constants | `EngineeringConstant` | symbol (case-significant) |
| Manufacturing | `ManufacturingProcessReference` | family + name + variant |

Write atomicity is a per-record `AsyncKeyedLock` plus a second lock on the
secondary key, which is keyed differently and so cannot be protected by
the first.

---

## 4. Provenance

Every record carries a `ReferenceProvenance`, never optionally. Nothing
in it is fabricated and nothing is inferred: a field the source did not
supply stays null, and verification status stays `NotVerified` until a
person actually verifies the record. Importing data does not verify it.

Provenance gates the lifecycle, identically in every library:

- A record cannot leave `Draft` without a named source organisation **and**
  a named source document.
- A record cannot reach `Released` without `VerifiedAgainstSource`, a
  named reviewer principal, and a verification date.

---

## 5. Lifecycle

```
Draft ⇄ Checked ⇄ Validated → Released → Superseded
```

Down-transitions are permitted deliberately: a check that finds a defect
must be able to send a record back, or the only way to correct it would be
to abandon the record and its history with it.

`Released` is terminal but for supersession. A released record is never
edited and never demoted, because downstream engineering work has already
consumed it; a corrected value becomes a new record that supersedes it,
and both survive.

Supersession links the replacement to the record it replaces using the
platform's existing `supersedes` relationship kind
(`GovernanceRelationshipKinds.Supersedes`), in the direction
`Decision.SupersedesAsync` already established. Group A introduces no
relationship kind of its own.

`ReferenceValidationState` is a family-specific specialisation of the
canonical `LifecycleState` vocabulary (`ADR-0074`), not a competing state
model.

---

## 6. Data-quality principles

These are the rules the whole programme is built on, and each one shows
up in the type system rather than only in prose.

| Principle | How the model enforces it |
|---|---|
| Missing is not zero | Every optional value is nullable and stays null; no query bound ever matches an unrecorded value |
| Unknown is not false | Every enum's default member is `Unspecified`/`Unknown`/`NotRecorded`, never a substantive value |
| Unverified is not validated | The lifecycle's provenance gates, enforced by the catalogue on every transition |
| Not applicable is not missing | `ReferencePropertyAvailability` has three members, and every family-traits table drives which one a cell gets |
| Derived is not source | `ReferenceValueOrigin.DerivedByTempestOS` is a distinct member, and `TEMPEST-REF-004` flags it wherever it appears |
| A citation is not compliance | `StandardReference` records what a source cited; nothing in Group A asserts conformity |
| A recommendation is not data | No library holds a selection, a suitability judgement or a recommended value |

A family-traits table that cannot speak for a family says so: every one
exposes `IsApplicabilityKnown`, and an unclassified family's conservative
answers must be read as "not known to apply", never "known not to apply".

---

## 7. Validation

Each library has a validation service over its own catalogue. It stores
nothing, changes nothing and repairs nothing.

Errors and warnings are different claims. An **error** means the record
states something that cannot be true, or that the library requires. A
**warning** means the record is incomplete or needs a person to look at
it — never a claim that the data is wrong.

The shared `TEMPEST-REF-` series covers the rules that are about being
reference data at all; each library adds its own series for its own
engineering:

| Series | Library | Codes |
|---|---|---|
| `TEMPEST-REF-` | shared | 001–008 |
| `TEMPEST-MAT-` | A1 | 001–013 |
| `TEMPEST-STD-` | A2 | 001–014 |
| `TEMPEST-FST-` | A3 | 001–020 |
| `TEMPEST-BRG-` | A4 | 001–022 |
| `TEMPEST-CMP-` | A5 | 001–024 |
| `TEMPEST-CON-` | A6 | 001–014 |
| `TEMPEST-MFG-` | A7 | 001–017 |

A4's own twenty-two bearing rules were **not** copied into the other six
libraries. Each series says only what is true of its own domain.

---

## 8. Cross-library seams

Two narrow interfaces, both declared in the shared layer so no library
takes a compile-time dependency on another.

**`IStandardResolver`** — `ExistsAsync(standardId)`. Lets any library
confirm its own standard citations resolve. Deliberately narrow: a citing
library has no business reading a standard's title, scope or status, and
copying them would duplicate A2's own data.

**`IReleasedConstantSource`** — `FindReleasedAsync(symbol)`. Lets a
future calculation capability consume a constant. Returns null both for a
constant that does not exist and for one that has not been Released, so a
consumer cannot distinguish "there is a value here you may not use" from
"there is no value here". What it does return carries the record Id and
revision number, so a calculation can say afterwards exactly which number
it used.

Both are **optional** collaborators everywhere they are consumed: a
fastener must be recordable and checkable before the material it names
has been registered, and no library may become a hard prerequisite for
holding data in another.

Each seam resolves through a forwarder rather than a second container
mapping of the same implementation type, which would construct two
catalogues over one store with independent write locks.

---

## 9. Datasets — architecturally complete, deliberately empty

**No library in Group A ships with data.**

A full survey of the repository found no authoritative dataset for any
Group A domain: no material property tables, no standards index, no
fastener dimensions, no bearing catalogue, no component data, no
constants tabulation, no process capability study. Nothing was found that
could be imported, and nothing was invented to fill the gap.

Every test fixture in every library is explicitly fictional, says so in
its own remarks, and uses designations in an unusable "FX-" series with
fictional source organisations, so no fixture can be mistaken for real
reference data.

Populating each library is tracked as its own Future Capability Record.
Population requires an authoritative source, a licence permitting its use,
and a person to verify each record against it — none of which is an
implementation task.

---

## 10. Units

Every dimensioned value in Group A goes through
`Tempest.Core.UnitsAndQuantities`. No library has a unit system of its
own, and no library records a dimensioned value as a bare number.

`Group A` extended the framework additively (`ADR-0125`):

- An optional `ToBaseUnitOffset` on `Unit<TDimension>`, making affine
  units representable, and arithmetic on affine quantities refused —
  resolving `FCR-0034`.
- New dimensions: `Temperature`, `MassDensity`, `Stiffness`,
  `TorsionalStiffness`, `Torque`, `Power`, `ThermalConductivity`,
  `ThermalExpansion`, `SpecificHeatCapacity`, `Acceleration`, `Energy`,
  `Velocity`, `Dimensionless`, plus `Micrometre` and `Gigapascal`.

Two deliberate exceptions, each disclosed where it lives:

- **Hardness** (A3) is not a dimensioned quantity. Vickers, Rockwell and
  Brinell numbers are scale-specific ordinal readings with no exact
  conversion between them, so the scale travels with the number as text
  and no comparison offers a canonical value to sort by.
- **Production scale** (A7) is a named band, not a quantity. Sources
  describe volume in words and the boundaries differ by industry;
  attaching numbers would be TempestOS inventing thresholds nobody
  published.

---

## 11. Boundaries

Group A does not own, and no library in it may acquire:

- Selection or suitability judgement of any kind.
- Calculation, derivation or optimisation.
- Cost, price, availability, lead time or any other commercial data.
- Supplier capability.
- Conformity assessment or any claim of compliance.

Group A supplies the reference evidence those capabilities will consume.
Each library's own document restates the specific boundary that library
is most likely to be pushed across.

---

## 12. Related documents

- `ADR-0125`, `ADR-0126` — the governing decisions.
- `ADR-0053`, `ADR-0055`, `ADR-0058`, `ADR-0072`, `ADR-0073`, `ADR-0074`,
  `ADR-0084`, `ADR-0124` — the decisions Group A follows rather than
  re-takes.
- The seven per-library documents listed in §1.
