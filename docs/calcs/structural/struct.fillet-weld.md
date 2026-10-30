# Fillet weld (EN 1993-1-8) (`struct.fillet-weld@1.2.0`)

**In plain terms.** A fillet weld fails by shearing through its throat, the shortest line from the root to the face. The simplified method adds every force on the weld into one resultant, spreads it along the weld length, and compares the stress on the throat area with one design shear strength. The weld is adequate when the throat carries the resultant; the sheet also gives the smallest throat that would do.

## Scope

Calculates the utilisation of a fillet weld loaded parallel, transverse and normal to its axis, with the required throat and leg. The simplified method compares the resultant over the throat with the design shear strength.

## Method

- Resultant force on the weld: F = √(F_∥² + F_⊥² + F_n²).
- Design shear strength of the weld, simplified method: f_vw,d = f_u/(√3·β_w·γ_M2).
- Long-joint reduction of a lap joint longer than 150·a (4.11(3)): β_Lw = min(1, 1.2 − 0.2·L/(150·a)); 1 for a shorter lap or another joint type.
- Shear stress on the throat: τ_w = F/(a·L).
- Weld resistance and utilisation: F_w,Rd = β_Lw·f_vw,d·a·L; u = F/F_w,Rd.
- Throat needed (not less than 3 mm, with the β_Lw of the throat provided) and the matching leg: a_req = max(F/(β_Lw·f_vw,d·L), 3 mm); leg = √2·a.

`F = √(F_∥² + F_⊥² + F_n²)`; `f_vw,d = f_u/(√3·β_w·γ_M2)`; `τ_w = F/(a·L)`; `F_w,Rd = f_vw,d·a·L`; `u = F/F_w,Rd`; `a_req = max(F/(f_vw,d·L), 3 mm)`; leg `= √2·a`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `joint` | Joint type: a lap joint is reduced beyond 150 throats, and Other is for a weld whose stress follows the base metal | — | Lap joint | Lap joint, Other (stress follows the base metal) | Setup | — |
| `L` | Effective weld length | mm | 200 | 30 to 10000 | Geometry | — |
| `a` | Throat thickness | mm | 5 | 3 to 50 | Geometry | — |
| `f_u` | Ultimate tensile strength of the weaker part joined | MPa | 490 | 300 to 1000 | Material | — |
| `F_par` | Force parallel to the weld axis | kN | 150 | -100000 to 100000 | Loads | — |
| `F_tr` | Force transverse to the weld axis, in the plane of the parts | kN | 0 | -100000 to 100000 | Loads | — |
| `F_n` | Force normal to the plane of the parts | kN | 0 | -100000 to 100000 | Loads | — |
| `beta_w` | Correlation factor (0.8 S235, 0.85 S275, 0.9 S355, 1.0 S420 and S460) | — | 0.9 | 0.8 to 1 | Factors | — |
| `gamma_M2` | Partial factor for the weld resistance | — | 1.25 | 1 to 2 | Factors | — |

## Assumptions

1. The resultant force is shared uniformly along the effective length. Basis: Simplified method.
2. Equal-leg fillet: leg = √2 × throat. Basis: Geometry.
3. A lap joint longer than 150·a is reduced by β_Lw = 1.2 − 0.2·L/(150·a), not above 1. Basis: BS EN 1993-1-8 4.11(3).
4. The weld length L is the length of the lap in the direction of the force. Basis: BS EN 1993-1-8 4.11(3).

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `F` | Resultant force | kN |
| `f_vw_d` | Design shear strength | MPa |
| `tau_w` | Throat stress | MPa |
| `beta_Lw` | Long-joint factor | — |
| `F_w_Rd` | Weld resistance | kN |
| `u` | Utilisation | — |
| `a_calc` | Throat needed | mm |
| `a_req` | Required throat | mm |
| `s_leg` | Leg length provided | mm |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `weld.resistance_design` | `F_w_Rd` | Design resistance of the weld | kN | — |
| `weld.utilisation` | `u` | Utilisation of the weld | — | — |
| `weld.strength_shear_design` | `f_vw_d` | Design shear strength of the weld | MPa | — |
| `weld.stress_throat` | `tau_w` | Stress on the throat | MPa | — |
| `weld.throat_required` | `a_req` | Required throat | mm | — |
| `weld.leg_provided` | `s_leg` | Leg length of the throat provided | mm | — |

## Checks

Checks the sheet makes:

- Weld force against weld resistance

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `throat` | Block | Conservative | Throats below 3 mm are outside the method. |
| `length` | Block | Conservative | Effective lengths below max(30 mm, 6·a) should not be designed to carry load. |
| `no-load` | Block | Conservative | At least one force must be non-zero. |
| `long-joint` | Warning | Conservative | A lap-joint weld longer than 150·a is reduced by the long-joint factor β_Lw = 1.2 − 0.2·L/(150·a) (BS EN 1993-1-8 4.11(3)); the reduced resistance is the one checked. |
| `long-joint-range` | Block | Conservative | The long-joint factor 1.2 − 0.2·L/(150·a) is not positive for a lap longer than 900·a: the weld is outside the formula. |

## Limitations

Simplified method only (no directional method). Throats below 3 mm, effective lengths below max(30 mm, 6a) and an all-zero load block. Uniform share along the length; the long-joint reduction β_Lw (4.11(3)) is applied to a lap joint longer than 150·a (1.1.0).

### Changes by version

**1.2.0 (C-2 consistency).** The check shows the resultant force and the weld resistance with units. The sheet declares six ports (`weld.*`). The inputs are grouped Setup (the joint type), Geometry, Material, Loads and Factors. The ultimate strength is called the ultimate tensile strength, and the standard is cited as BS EN 1993-1-8:2005 with `cl.` locators. The scope and two messages follow the sheet standard. No calculated value changes.

**Changes in 1.1.0 (C-2 fix, F2).** The long-joint reduction of BS EN 1993-1-8 4.11(3) is applied (it was a warning that said it was not, and the sheet passed what the code reduces). New input `joint` (default Lap joint; Other = a weld whose stress follows the base metal's, as a flange-to-web weld, for which 4.11(2) gives no reduction): a lap joint longer than 150·a has β_Lw = 1.2 − 0.2·L/(150·a), not above 1, `F_w,Rd = β_Lw·f_vw,d·a·L`, `u = F/F_w,Rd`, and the throat needed `a_calc = F/(β_Lw·f_vw,d·L)` with the β_Lw of the throat provided; new output `beta_Lw` (1 when not reduced). python3, a = 5 mm, f_u 490 MPa, β_w 0.9, γ_M2 1.25 (f_vw,d 251.468 MPa): L = 800 mm gives β_Lw 0.986667 and F_w,Rd 992.461 kN (1005.872 kN unreduced); L = 1500 mm β_Lw 0.8, F_w,Rd 1508.809 kN; L = 3000 mm β_Lw 0.4, F_w,Rd 1508.809 kN (the reduced resistance stops growing with the length). A lap longer than 900·a, where the formula reaches zero, blocks (`long-joint-range`). A saved instance without `joint` reads as a lap joint; the three known-answer cases are shorter than 150·a and are unchanged.

## References

- BS EN 1993-1-8:2005, cl. 4.5.3.3: Simplified method for design resistance of fillet weld.
- BS EN 1993-1-8:2005, cl. 4.5.2(2): Effective throat thickness: minimum 3 mm.
- BS EN 1993-1-8:2005, cl. 4.5.1(2): Effective length: welds shorter than 30 mm or 6 throats not to carry load.
- BS EN 1993-1-8:2005, cl. 4.11: Long joints.

## Test cases

The legacy module's three hand calculations (`ka-legacy-s355-shear`, `ka-legacy-s235-combined`, `ka-legacy-overloaded`).

- `ka-legacy-s355-shear`: Legacy example 1: S355 lap weld in longitudinal shear. Source: TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation.
- `ka-legacy-s235-combined`: Legacy example 2: S235, combined parallel and transverse load. Source: TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation.
- `ka-legacy-overloaded`: Legacy example 3: overloaded normal weld. Source: TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** S355 lap weld, 150 kN in longitudinal shear ([sheet](../../worked-examples/structural/struct.fillet-weld/s355-lap-shear/sheet.txt)).

```text
beta_Lw = lap not longer than 150·a = 1
F = sqrt((150 kN)^2 + (0 kN)^2 + (0 kN)^2) = 150 kN
f_vw_d = 490 MPa / (sqrt(3) × 0.9 × 1.25) = 251.47 MPa
tau_w = 150 kN / (5 mm × 200 mm) = 150 MPa
F_w_Rd = 1 × 251.47 MPa × 5 mm × 200 mm = 251.47 kN
u = 150 kN / 251.47 kN = 0.5965
a_calc = 150 kN / (1 × 251.47 MPa × 200 mm) = 2.9825 mm
a_req = max(2.9825 mm, 3 mm) = 3 mm
s_leg = sqrt(2) × 5 mm = 7.0711 mm
Governing: PASS  Weld force against weld resistance: 150 kN vs 251.47 kN  utilisation 0.60  « BS EN 1993-1-8 4.5.3.3
```
<!-- hand-arithmetic:end -->
