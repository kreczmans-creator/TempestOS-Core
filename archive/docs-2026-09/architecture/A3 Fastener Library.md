# A3 Fastener Library

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.Fasteners`
**Governing ADR:** `ADR-0126`
**Status:** Implemented, `Group A`.

> The provenance model, lifecycle, catalogue mechanics, comparison
> semantics, data-quality principles and boundaries A3 shares with every
> other reference library are described once in
> `Group A Engineering Reference Data.md` and are not restated here.

---

## 1. Purpose

A3 is the authoritative catalogue of fasteners: bolts, screws, set
screws, studs, nuts, washers, threaded inserts, retaining rings, rivets
and pins, under one taxonomy.

---

## 2. Taxonomy

`FastenerFamily` classifies **what the item is** — not how it is driven
or what head it carries, which are the orthogonal `FastenerDriveType` and
`FastenerHeadType`.

`FastenerFamilyTraits` is what makes one taxonomy over eleven different
things safe:

| Trait | True for |
|---|---|
| `IsExternallyThreaded` | Bolt, Screw, SetScrew, Stud |
| `IsInternallyThreaded` | Nut, ThreadedInsert |
| `HasHead` | Bolt, Screw — a stud and a set screw are headless, which is what distinguishes them |
| `HasDriveFeature` | Bolt, Screw, SetScrew, Nut — a set screw is headless but still driven |
| `HasNominalLength` | everything but Washer, RetainingRing, Nut |
| `TakesPropertyClass` | every threaded family |
| `TakesProofLoad` | Bolt, Screw, Stud, Nut |
| `TakesTighteningTorque` | Bolt, Screw, SetScrew, Nut |

A thread on a washer is an error (`TEMPEST-FST-004`); a missing thread on
a bolt is a gap (`TEMPEST-FST-003`). The two are never confused, and
`Unspecified` is never treated as `None`: "not recorded" and "the family
has none" are different claims, and only the second can contradict the
family.

---

## 3. The three decisions worth stating

### 3.1 Torque is transcribed, never computed

`FastenerTorqueReference` records a torque **a source published**, under
the conditions the source stated.

Tightening torque depends on friction at the thread and under the head,
on lubrication, plating, reuse, joint stiffness and the preload actually
wanted — none of which A3 knows. Deciding a tightening torque for a real
joint is a calculation and a judgement belonging to a future capability
that will consume this as evidence.

`Conditions` is therefore not decoration: a torque figure separated from
the friction condition it was published for is a number, not reference
data, and one recorded without conditions is warned about
(`TEMPEST-FST-014`). A figure marked `DerivedByTempestOS` is flagged as
calculation output wearing reference data's clothes.

`TorqueReferences` is a list, because a source legitimately publishes
different figures for different friction conditions and property classes,
and collapsing them to one would discard the conditions that make any of
them meaningful.

### 3.2 Hardness is not a dimensioned quantity

A Vickers number, a Rockwell C number and a Brinell number are not one
quantity in three units — they are scale-specific ordinal readings
produced by different test methods, with no exact conversion between
them. Recording hardness as a `Quantity` would let the units framework
convert between scales that cannot be converted, which is precisely the
plausible-looking wrong answer P01 exists to prevent.

`FastenerHardness` therefore carries the scale as text, inseparably from
the number, and the comparison offers **no canonical value** for the
hardness row, so nothing can sort across scales. A deliberate, disclosed
exception to Group A recording engineering values as dimensioned
quantities — made because hardness genuinely is not one.

### 3.3 Pitch, not threads per inch

Pitch is the physical quantity and is recorded as a `Length` through the
units framework. A threads-per-inch count is a designation convention,
fully determined by the pitch, and a field for it would be a second,
silently inconsistent answer to one question. A source quoting only a
thread count has it preserved verbatim in
`ThreadSpecification.Designation`.

`ThreadSpecification` requires only that designation: a source quoting a
designation without breaking out diameter and pitch has still said
something exact, and inventing the numbers from it would be deriving data
and presenting it as source data.

---

## 4. Canonical model

`FastenerDefinition` requires a `Family` and a `Designation`. Everything
else is optional: manufacturer and part number, `Thread`, `HeadType`,
`DriveType`, `StyleDesignation` (the source's own nut style or washer
form wording), `Dimensions`, `Mechanical`, `MaterialId` and
`MaterialDesignation`, `Finish`, `TorqueReferences`, `Standards`,
`SourceClassification`, `Notes`, `EffectiveDate`.

`FastenerDimensions` is one record spanning every family rather than a
type per family: the dimensions genuinely overlap — a nut and a bolt head
are both measured across flats — and the traits table already says which
are meaningful, so a per-family split would restate that knowledge in a
second place.

`FastenerMechanicalProperties` holds published figures only. A3 does not
derive a proof load from a proof strength and a stress area, does not
infer a class from a strength, and does not fill a missing figure from a
related one. `PropertyClass` is the source's own designation kept
verbatim, never parsed: a class designation encodes strengths by
convention, and reconstructing those numbers would be deriving data the
source may not have intended.

The material is a typed link into A1 (`MaterialId`), never a copy. Where
the material is not registered, `MaterialDesignation` records what the
source said and validation warns (`TEMPEST-FST-017`).

Coating designations are kept as the source wrote them. A3 does not
classify coatings: they are governed by the standards that define them,
and a TempestOS-invented classification laid over them would be an
invented vocabulary presented as a universal one.

---

## 5. Validation

`TEMPEST-FST-001`…`020`. Beyond applicability and positivity:

- **Geometry** (009, 010) — a pitch not smaller than the nominal
  diameter forms no thread; a width across corners not greater than the
  width across flats is a polygon that does not exist; an outside
  diameter not greater than an inside diameter is an item with no wall.
- **Physics** (011, 012) — a yield or proof strength above the tensile
  strength; a proof load above the minimum breaking load.
- **Handedness** (020) — a thread with no recorded handedness is warned
  about, because a left-hand thread fitted as a right-hand one fails and
  the default is never safe to assume.

---

## 6. Boundaries

A3 records what fasteners exist and what their sources published. No
joint analysis, no preload or clamp-load calculation, no thread
engagement check, no torque TempestOS worked out, no selection, no
commercial data.

---

## 7. Dataset

Empty. See `Group A Engineering Reference Data.md` §9.
