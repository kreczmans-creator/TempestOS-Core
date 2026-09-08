# A7 Manufacturing Process Library

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.Manufacturing`
**Governing ADR:** `ADR-0126`
**Status:** Implemented, `Group A`.

> The provenance model, lifecycle, catalogue mechanics, comparison
> semantics, data-quality principles and boundaries A7 shares with every
> other reference library are described once in
> `Group A Engineering Reference Data.md` and are not restated here.

---

## 1. Purpose

A7 is the register of manufacturing processes: what processes exist, what
sources say they can achieve, what materials they work on, and what
limits them.

---

## 2. Taxonomy

`ProcessFamily` names some fifty processes; `ProcessGroup` gives thirteen
broader groups, and `ProcessFamilyTraits.GroupOf` maps every family to
exactly one. A caller can ask for "every casting process" without
enumerating each, and without the library growing a second, drifting
list.

`ProcessFamilyTraits` decides which capabilities describe which process:

| Trait | Meaning |
|---|---|
| `IsShaping` | the process gives a part its shape, rather than changing its properties, surface or assembly |
| `UsesAMouldOrDie` | the process forms material against a mould or die, and so has a draft angle |
| `HasWallThicknessCapability` | the process produces a wall thickness |
| `HasSurfaceRoughnessCapability` | the process leaves a surface of its own |
| `HasProcessTemperature` | the process runs at a controlled temperature |
| `IsJoining` | the process joins parts rather than shaping one |

A draft angle on a turning operation is not a data gap — there is nothing
to record — and the comparison reports it as `NotApplicable`. Recording
one is an error (`TEMPEST-MFG-005`).

---

## 3. Capability is a band, and a band needs its conditions

Every entry in `ProcessCapabilities` is a `ReferenceRange`, not a point.
Process capability is published as a band — "this process holds
tolerances between these limits" — and recording only a midpoint would
invent a figure nobody published while losing the fact that the two ends
belong to one thing.

Each band carries its own `Origin` and `Conditions`. An achievable
tolerance depends on the feature, the material and the equipment, and a
band separated from those is a number rather than reference data — so
`TEMPEST-MFG-006` warns when the origin is missing and
`TEMPEST-MFG-007` warns when the conditions are.

An open end is genuinely open, and an absent field means nobody recorded
the capability, never that the process has none. A search asks "does the
source's own band cover this value?" and a process that published no band
is not a match: an unrecorded band is never read as unbounded.

**A published capability is not a promise.** Recording that a source says
a process reaches a tolerance says what the source said. It does not say
a particular supplier will reach it, that a particular feature can be
made that way, or that the process should be chosen.

The one capability whose ends may legitimately be zero or negative is
`ProcessTemperature`; every other is checked for positivity.

---

## 4. Material compatibility

`ProcessMaterialCompatibility` records a material a source associated
with a process and what it said about the pairing:
`Suitable`, `ConditionallySuitable`, `NotSuitable`, or `Unspecified`.

`NotSuitable` is recorded, not omitted: knowing a combination does not
work is as useful as knowing one does — and a search for processes that
handle a material must never return one a source explicitly ruled out.

The material family comes from **A1's own `MaterialFamily`**, not a
second parallel list of materials in A7. One concept, one owner. A source
that named a specific grade records it in `MaterialId` where the grade is
registered and in `MaterialDesignation` verbatim where it is not.

`Origin` says who made the claim. TempestOS never concludes that a
material can be processed a given way from the properties of either: that
is a manufacturing judgement resting on equipment, tooling, geometry and
experience the library does not hold, and a claim marked
`DerivedByTempestOS` is flagged.

`TEMPEST-MFG-011` catches a record saying two contradictory things about
the same material; `TEMPEST-MFG-012` catches it saying the same thing
twice.

---

## 5. Production scale and constraints

`ProductionScale` is a named band — prototype, low, medium, high,
continuous. Sources describe production volume in words, the boundaries
between the words differ by industry, and attaching quantities here would
be TempestOS inventing thresholds no source published. It is **recorded,
never recommended**: that a source says a process is used at high volume
is a fact about the source, not advice about what volume a job should
use.

`ProcessConstraint` carries the source's own wording verbatim, with a
`ProcessConstraintKind` so a reader can filter. The description is
deliberately free text: process constraints are stated in prose in every
real source, and forcing them into a structured form would either lose
what the source said or invent structure it did not have. The kind exists
so constraints can be filtered, not so the text can be interpreted.

---

## 6. Identity

The uniqueness key is family, name **and** variant. Two sources
legitimately publish different capability bands for the same named
process, and both are real reference data; the variant is part of the key
so both can be held rather than one silently displacing the other.

The document `Kind` is `ManufacturingProcessReference`, deliberately
**not** `ManufacturingOperation`: that Kind is the workspace's own
canonical object for an operation performed on a real part, and this is a
reference description of a process in general. One value, one meaning.

---

## 7. Validation

`TEMPEST-MFG-001`…`017`, plus the shared inverted-range rule against
every band.

---

## 8. Boundaries

No process planning, no route generation, no process selection, no cost
model, no cycle-time estimation, no supplier capability.

The boundary most likely to be pushed: A7 does not become able to choose
a process for a part by holding more capability bands. Choosing rests on
geometry, volume, cost, lead time and available suppliers, and A7 holds
none of them.

`TypicalApplications` records what a source said the process is used for.
It is not, and must never be read as, a TempestOS recommendation.

---

## 9. Dataset

Empty. See `Group A Engineering Reference Data.md` §9. The fixture bands
are fictional deliberately: a capability band read as real would steer a
manufacturing decision.
