# Lifting lug and pin joint — `calc.lifting-lug-pin-joint`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.LiftingLugPinJointCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/LiftingLugPinJointVectors.cs`.

## Method

**In plain terms.** A lifting lug is a plate with a hole and a pin through
it, and it can fail in five separate ways: the plate can tear across the
narrowest section beside the hole, the pin can crush the hole, the plate
can shear out along two planes from the hole to its edge, the pin can
shear through, and the pin can bend between the plates that support it.
Each check is a load divided by an area (or a moment divided by a section
modulus) compared with what the material allows. The largest of the five
ratios governs, and it is often the pin in bending rather than the plate.

The classical hand check of a pinned lug — the checks ASME BTH-1
(*Design of Below-the-Hook Lifting Devices*, the pinned-connection
provisions of chapter 3) and every lifting-lug design guide perform:
tension on the net section through the hole, bearing of the pin on the
hole, shear tear-out of the lug beyond the hole, and the pin itself in
double shear and bending. The pin bending idealisation is the usual one
for a lug between two cheek plates with uniform bearing on each plate.
The allowable stresses are supplied by the caller from the material
record and the design category in force; this module applies no design
factor of its own. Nothing from the standard is reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `Load` P | Force | > 0 |
| `LugThickness` t | Length | > 0 |
| `LugWidth` W (at the hole) | Length | > hole diameter |
| `HoleDiameter` d_h | Length | > 0 |
| `PinDiameter` d_p | Length | > 0, ≤ d_h; ≥ 0.9·d_h (method limit) |
| `EdgeDistance` a (hole edge to lug end, in the load direction) | Length | > 0 |
| `CheekPlateThickness` t_s | Length | > 0 |
| `Clearance` g (lug face to cheek plate, each side) | Length | ≥ 0 |
| `AllowableTensileStress`, `AllowableBearingStress`, `AllowableShearStress` (lug) | Pressure | > 0 |
| `PinAllowableBendingStress`, `PinAllowableShearStress` | Pressure | > 0 |

## Formulae

1. Net-section tension: σ_t = P / ((W − d_h)·t).
2. Bearing: σ_br = P / (d_p·t).
3. Tear-out (two shear planes beyond the hole): τ = P / (2·a·t).
4. Pin double shear: τ_p = P / (2·π·d_p²/4).
5. Pin bending: M = (P/2)·(t/4 + g + t_s/2), from the lug's uniform load
   and each cheek plate's uniform reaction; σ_b = 32·M / (π·d_p³).
6. Each utilisation is the stress over its allowable; the governing check
   is the largest. Outcome meets criteria when every utilisation ≤ 1.

## Outputs and recorded intermediates

Results: the five stresses, the five utilisations, the governing check's
name and utilisation, outcome. Intermediates: the five areas and section
modulus, the pin moment, the pin-to-hole ratio.

## Limits of applicability (refusals, not exceptions)

- **Loose pin:** d_p / d_h < 0.9. The uniform-bearing idealisation assumes
  a close-fitting pin; a looser fit concentrates bearing over a narrow
  contact and this module refuses rather than under-predict. (This is the
  module's own applicability limit, stated as such.)

## Worked examples (hand calculations)

**Example 1 — a lug that passes.** P = 50 kN, t = 20 mm, W = 100 mm,
d_h = 32 mm, d_p = 30 mm, a = 40 mm, t_s = 12 mm, g = 2 mm. Allowables:
tension 150, bearing 200, shear 90 MPa (lug); bending 250, shear 150 MPa (pin).
- Net section (100 − 32) × 20 = 1 360 mm² → **36.765 MPa**, 0.24510.
- Bearing 30 × 20 = 600 mm² → **83.333 MPa**, 0.41667.
- Tear-out 2 × 40 × 20 = 1 600 mm² → **31.250 MPa**, 0.34722.
- Pin shear 2 × π × 30² / 4 = 1 413.72 mm² → **35.368 MPa**, 0.23579.
- Pin bending M = 25 000 × (5 + 2 + 6) = 325 000 N·mm; Z = π × 30³ / 32 =
  2 650.72 mm³ → **122.61 MPa**, **0.49043** — governing. Meets criteria.

**Example 2 — a lug that fails three ways.** P = 120 kN, t = 15 mm,
W = 90 mm, d_h = 26 mm, d_p = 25 mm, a = 30 mm, t_s = 10 mm, g = 1 mm,
same allowables.
- Net section 64 × 15 = 960 mm² → **125.00 MPa**, 0.83333.
- Bearing 25 × 15 = 375 mm² → **320.00 MPa**, 1.6000.
- Tear-out 2 × 30 × 15 = 900 mm² → **133.33 MPa**, 1.4815.
- Pin shear 981.75 mm² → **122.23 MPa**, 0.81487.
- Pin bending M = 60 000 × (3.75 + 1 + 5) = 585 000 N·mm; Z = 1 533.98 mm³
  → **381.36 MPa**, 1.5254. Governing: bearing, 1.6000. Does not meet criteria.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative:** any non-positive load, thickness, diameter, edge
  distance, cheek plate thickness or allowable → invalid; negative
  clearance → invalid; W ≤ d_h (no net section) → invalid; d_p > d_h (the
  pin does not fit) → invalid.
- **Out of range:** d_p = 25 mm in a 32 mm hole (ratio 0.78) → refused.
- **Unit mismatch:** Example 1 restated in inches, lbf and psi gives the
  same base-unit results.
