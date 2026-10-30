# Thermal expansion of a restrained bar (`mech.thermal-expansion@1.1.1`)

**In plain terms.** A bar that is heated wants to grow by its expansion coefficient times its length times the temperature rise. If walls stop it, the growth it is denied becomes a squeeze: the force is the denied movement times the bar's stiffness, and the stress is that force over the area. A gap lets the bar grow freely until it closes; a flexible restraint gives a little, so the force is shared by the bar and the restraint in series. Cooling does the same in reverse and puts the bar in tension.

## Scope

Calculates the free movement, restraint force, axial stress and actual movement of a bar heated or cooled between two restraints. A gap and a rigid or elastic restraint are allowed, and the stress is compared with an allowable.

The sheet is ported from the legacy `calc.thermal-expansion-stress` module (ADR-0167). `E` and `alpha` fill from a released material record.

## Method

- Free expansion of the bar: `δ_free = α·L·ΔT`.
- Expansion actually restrained, once any gap `g` has closed: `δ_e = sign(δ_free)·max(|δ_free| − g, 0)`.
- Axial stiffness of the bar: `k_bar = E·A/L`.
- Restraint force, from rigid supports or an elastic restraint `k_s`: `F = δ_e·k_bar` (rigid); `F = δ_e/(1/k_bar + 1/k_s)` (elastic).
- Thermal stress in the bar, compressive on heating: `σ = −F/A`.
- Net movement of the bar end: `δ = δ_free − F/k_bar`.

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `L` | Bar length | m | Geometry |
| `A` | Cross-section area | mm² | Geometry |
| `g` | Gap closed before the restraint engages | mm | Geometry |
| `E` | Young's modulus | GPa | Material |
| `alpha` | Coefficient of thermal expansion | 1/K | Material |
| `dT` | Temperature change (positive heating) | K | Conditions |
| `k_s` | Restraint stiffness (optional; blank = a rigid restraint) | kN/m | Conditions |
| `sigma_allow` | Allowable stress | MPa | Requirements |

## Assumptions

- The expansion coefficient is constant over the temperature change and the bar stays elastic.
- The bar is uniformly heated and does not buckle under the compressive force.

## Outputs

`delta_free`, `delta_e`, `k_bar`, `F`, `sigma`, `sigma_abs` (the stress magnitude), `delta` and `u` (the stress utilisation).

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `expansion.movement_free` | `delta_free` | mm | Free thermal movement of the bar (positive on heating) |
| `expansion.force_restraint` | `F` | kN | Restraint force |
| `expansion.stress_axial` | `sigma` | MPa | Axial stress in the bar (negative is compressive) |
| `expansion.movement_actual` | `delta` | mm | Actual movement of the bar end |

## Checks

- Thermal stress against allowable: the stress magnitude `|σ|` against `sigma_allow`.

## Limitations

The sheet assumes a constant expansion coefficient and an elastic bar. It does not check buckling of the compressed bar; use `struct.column-buckling`.

**1.1.1.** Declares the unit change of 1.1.0 (ADR-0177 review): `k_s` was in kN/mm before 1.1.0, so a bare stored number from an earlier version is read in kN/mm, never kN/m. No calculated value changes.

## References

- Young, *Roark's Formulas for Stress and Strain*, Ch. 15, 8th ed.: thermal stresses.
- Budynas, *Shigley's Mechanical Engineering Design*, §3-17, 10th ed.: thermal stresses and strains.

## Test cases

The legacy module's five hand calculations (rigid, gap, elastic, cooling, gap not closed).

**Changes in 1.1.0 (C-2).** `k_s` is entered in kN/m, and `k_bar` and `F` are shown in kN/m and kN (the same values as before in kN/mm and N); the inputs are in groups; the check shows the stress and the allowable; four ports are declared. No result changes.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Steel bar heated 60 K against a 0.5 mm gap ([sheet](../../worked-examples/mechanical/mech.thermal-expansion/bar-with-gap/sheet.txt)).

```text
delta_free = 0.000012 1/K × 2 m × 60 K = 1.44 mm
delta_e = 1.44 mm − 0.5 mm = 0.94 mm
k_bar = 200 GPa × 500 mm² / 2 m = 50000 kN/m
F = 0.94 mm × 50000 kN/m = 47 kN
sigma = −47 kN / 500 mm² = -94 MPa
delta = 1.44 mm − 47 kN / 50000 kN/m = 0.5 mm
sigma_abs = abs(-94 MPa) = 94 MPa
u = 94 MPa / 150 MPa = 0.62667
Governing: PASS  Thermal stress against allowable: 94 MPa vs 150 MPa  utilisation 0.63
```
<!-- hand-arithmetic:end -->
