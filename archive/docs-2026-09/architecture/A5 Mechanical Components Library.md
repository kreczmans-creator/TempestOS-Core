# A5 Mechanical Components Library

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.Components`
**Governing ADRs:** `ADR-0125`, `ADR-0126`
**Status:** Implemented, `Group A`.

> The provenance model, lifecycle, catalogue mechanics, comparison
> semantics, data-quality principles and boundaries A5 shares with every
> other reference library are described once in
> `Group A Engineering Reference Data.md` and are not restated here.

---

## 1. Purpose

A5 is the authoritative catalogue of springs, gears, drive elements and
standard machine components.

---

## 2. One taxonomy, three typed details

A spring, a gear and a shaft coupling are described by entirely different
engineering content, but governed identically: same lifecycle, same
provenance, same supersession, same search. Splitting them into three
libraries would triple the infrastructure to express a difference that
lives entirely in the engineering detail.

So the record around them is shared and the detail is not:

| Detail | Group | Families |
|---|---|---|
| `SpringDetail` | Spring | compression, extension, torsion, disc, constant-force, gas |
| `GearDetail` | Gear | spur, helical, bevel, worm, worm wheel, internal, rack |
| `DriveElementDetail` | DriveElement | timing pulley/belt, vee belt/pulley, roller chain, sprocket |
| *(none)* | ShaftElement, MotionElement, Sealing | coupling, collar, key, plain bearing, linear guide, ball screw, radial shaft seal |

`ComponentFamilyTraits.GroupOf` maps every family to exactly one group,
and `HasSpringDetail`/`HasGearDetail`/`HasDriveElementDetail` decide which
detail a family may carry. A gear detail on a spring is a modelling error
(`TEMPEST-CMP-003`), two details at once is an error
(`TEMPEST-CMP-004`), and a family that should have one but records none
is a gap (`TEMPEST-CMP-005`).

The families with no typed detail carry only `ComponentDimensions` and
`ComponentRatings`, and the traits table says so — that is a fact, not a
gap, and the comparison reports it as `NotApplicable`.

Retaining rings, washers and threaded inserts are deliberately absent:
they are fasteners and belong to A3. Rolling bearings are equally absent:
they are A4's. One component has one home.

---

## 3. A torsion spring's rate is not a torque

The radian is dimensionless in SI, so torque and torque-per-radian share
the same base dimensions and a purely dimensional model cannot tell them
apart. They are nonetheless entirely different engineering quantities.

`ADR-0125` therefore adds `TorsionalStiffness` as a dimension of its own.
Separating them is exactly what the framework's phantom-typed dimensions
exist to make possible: the compiler refuses a mistake the units alone
could not catch.

`SpringDetail` has two separate rate fields — `Rate` (a `Stiffness`,
force per deflection) and `TorsionalRate` (a `TorsionalStiffness`, torque
per angle) — and `ComponentFamilyTraits.HasTorsionalRate` says which a
family may carry. A rate recorded in the wrong form is an error
(`TEMPEST-CMP-007`), and the comparison keeps them as two separate rows
so neither can be read as the other.

`Power` was added in the same pass, for drive elements and couplings
whose sources rate them in power rather than torque.

---

## 4. Canonical model

`ComponentDefinition` requires a `Family` and a `Designation`. Beyond the
typed detail it carries `ComponentDimensions` (bore, outside diameter,
overall length/width/height, mass — the dimensions a caller asks for
without knowing what the component is) and `ComponentRatings` (maximum
speed, rated and maximum torque, rated power, axial and radial load,
operating temperature range).

Each rating is a `ReferenceValue` carrying its own conditions, because a
rating separated from the conditions it was published under says less
than it appears to. **A published limit is not a permission**: that a
source rated a coupling to a torque says what the source said, not that
the coupling suits any particular drive.

---

## 5. Validation

`TEMPEST-CMP-001`…`024`. The geometry rules are the interesting ones,
and each is a fact that cannot be otherwise:

- **Springs** — a solid length not shorter than the free length is a
  spring with no travel (008); active coils exceeding total coils (009);
  an inside diameter not smaller than the outside (010); and a wire
  diameter that disagrees with the recorded coil diameters (011), since
  outside minus inside is exactly two wire diameters and a mismatch means
  one of the three was mis-transcribed.
- **Gears** — a pressure angle outside the range a real involute gear is
  cut at (013); a helix angle or hand on a spur gear (014); and an
  external gear whose tip diameter does not exceed its pitch diameter
  (015). That last rule is deliberately **restricted to external gears**:
  an internal gear's teeth stand inside its reference cylinder, and
  applying the rule universally would reject correct data.
- **Handedness** (021) — a helical spring with no winding direction, or a
  helical gear with no helix hand. A spring wound the wrong way unwinds
  under load and a meshing external pair needs opposite hands; neither
  default is safe to assume.
- **Ratings** (018, 019, 020) — a speed rating on a family that does not
  rotate, a torque rating on one that transmits none, and a rated torque
  above a maximum torque.

---

## 6. Boundaries

No spring design or optimisation, no gear rating for contact or bending
stress, no interference check, no drive selection, no ratio or
centre-distance calculation, no life prediction.

A5 does not compute a pitch diameter from module and tooth count even
though it could: a computed value presented alongside published ones is
derived data wearing source data's clothes, and `PitchDiameter` stays
null unless a source published one.

---

## 7. Dataset

Empty. See `Group A Engineering Reference Data.md` §9.
