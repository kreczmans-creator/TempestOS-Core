# A1 Materials Database

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.Materials`
**Governing ADRs:** `ADR-0055` (original), `ADR-0126` (uplift)
**Status:** Implemented, `Group A`.

> The provenance model, lifecycle, catalogue mechanics, comparison
> semantics, data-quality principles and boundaries A1 shares with every
> other reference library are described once in
> `Group A Engineering Reference Data.md` and are not restated here.

---

## 1. Purpose

A1 is the authoritative catalogue of engineering materials: what a source
said about a material's identity, classification, condition and
properties, in a form other libraries can link to and engineering work can
cite.

It is the library the rest of Group A depends on most. A3, A5 and A7 all
link to it rather than describing materials of their own.

---

## 2. What changed in the uplift

A1 existed before Group A (`WP 7.1C`, `ADR-0055`) as
`IMaterialCatalog`/`MaterialCatalog` with `MaterialSpecification`,
`MaterialProperty`, its own provenance and confidence types and its own
exception family. It had no lifecycle, no supersession, no query, no
comparison and no validation.

The uplift kept what worked and replaced what had been duplicated:

| Before | After |
|---|---|
| `MaterialSpecification` (identity + content mixed) | `MaterialDefinition` (content only) in an `IReferenceRecord<T>` |
| `MaterialProperty` + its own provenance/confidence types | `ReferenceQuantityValue` + shared `ReferenceProvenance` |
| `MaterialPropertyValueCodec` | shared `ReferenceQuantityCodec`, extended from seven dimensions to twenty-two |
| `MaterialsException` + two subtypes | shared `ReferenceDataException` family |
| open-string `Category` | `MaterialFamily` enum + `SourceClassification` verbatim |
| — | lifecycle, supersession, query, comparison, validation |

**The document `Kind` is unchanged** (`MaterialSpecification`), so records
written before the uplift are still this library's own.

---

## 3. Canonical model

`MaterialDefinition` requires a `Name` and a `Family` and nothing else.
Everything further is optional and stays null where the source gave
nothing: `Designation`, `Grade`, `Condition`, `SourceClassification`,
`Supplier`, `SupplierDesignation`, `ProcessingNotes`,
`EnvironmentalNotes`, `Notes`, `EffectiveDate`.

`Properties` is an open dictionary of `ReferenceQuantityValue`, each
carrying its own origin and conditions.

### 3.1 Why the family became a closed enum

`ADR-0055` chose an open-string `Category` "since no real discipline
requirement has yet named a fixed taxonomy to validate one against".
Group A is that requirement: a material's family determines which
properties are meaningful — a ceramic has no yield point, a polymer no
heat-treatment condition — and `MaterialFamilyTraits` would have nothing
to stand on if the family were free text.

The source's own wording is not lost. `SourceClassification` keeps it
verbatim, exactly as the open string used to, and a material classified
`Other` **must** record it (`TEMPEST-MAT-002`).

### 3.2 Why the property set stayed open

`ADR-0055` made the property set deliberately open, and that decision is
not reversed: materials engineering does not have a closed property list,
and a library that rejected an unrecognised property name would refuse
real data.

What was added is a *controlled vocabulary alongside* it.
`MaterialPropertyNames` names sixteen well-known properties and maps each
to the dimension it must carry. A property recorded under a well-known
name in the wrong dimension is an error (`TEMPEST-MAT-004`); a property
recorded under any other name is legitimate and stored as given. This is
a middle ground that adds checking without closing the set.

### 3.3 Designation uniqueness

The key is supplier **and** designation, because a generic grade and a
named supplier's product legitimately share a designation and both must
be holdable.

---

## 4. Family traits

`MaterialFamilyTraits` answers, in one place:

- `IsMetal`, `IsPolymer` — group membership.
- `HasYieldStrength` — false for ceramics and glasses, which fail
  brittly without a yield point. A yield strength recorded against one is
  a modelling error (`TEMPEST-MAT-007`), not a data gap.
- `HasHeatTreatmentCondition` — metals only.
- `MayBeAnisotropic`.
- `IsApplicabilityKnown` — false for `Unspecified` and `Other`.

---

## 5. Validation

`TEMPEST-MAT-001`…`013`. The rules worth naming:

- **Dimension checking** (004) — a density recorded as a pressure is
  caught, and an unknown property name is still accepted.
- **Physics** (005, 006, 010) — properties that cannot be negative,
  properties that must exceed zero, and a Poisson's ratio outside the
  range a real isotropic material can occupy.
- **Relationships** (008, 009) — a yield strength above the ultimate
  tensile strength, and an inverted service-temperature range. Neither
  can be true of a real material.
- **Applicability** (007, 011) — a yield strength on a brittle family, a
  heat-treatment condition on a family that has none.

---

## 6. Boundaries

A1 records material properties. It does not derive allowable stresses,
does not apply safety factors, does not select a material for an
application, and holds no supplier commercial data.

The boundary most likely to be pushed: an allowable stress is not a
property, it is a design decision resting on a code, a load case and a
factor A1 knows nothing about.

---

## 7. Dataset

Empty. See `Group A Engineering Reference Data.md` §9. Population is
tracked as its own Future Capability Record.
