# Thick-walled cylinder (Lamé) (`mech.thick-cylinder@2.2.1`)

**In plain terms.** In a thin pipe the wall stress is nearly uniform, but in a thick wall it is not: the hoop stress is highest at the bore and falls towards the outside. Lamé's solution gives the radial and hoop stresses at every radius for any inner and outer pressure, and the axial stress for closed ends. The largest equivalent stress, at the bore for internal pressure, is compared with the allowable.

## Scope

Calculates the radial, hoop and axial stresses at the bore and outer surface of a thick-walled cylinder under internal and external pressure. It also gives von Mises and Tresca stresses, the utilisation and a thin-wall estimate.

The sheet is migrated from `calc.thick-walled-cylinder` (v0.16) onto the worksheet engine. The old calculator and its vectors stay, and this sheet reproduces them.

## Method

- Lamé constants from the bore and outside pressures: `A = (a² p_i − b² p_o)/(b² − a²)`; `B = a² b² (p_i − p_o)/(b² − a²)`.
- Radial and hoop stress at radius `r`: `σ_r = A − B/r²`; `σ_θ = A + B/r²`.
- Axial stress, closed or open ends: `σ_z = A` (closed); `σ_z = 0` (open).
- Equivalent stresses from the three principal stresses: von Mises; Tresca is the largest principal difference.
- Thin-wall estimate: `(p_i − p_o)(a + b)/(2(b − a))`.

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `closed` | Closed ends (axial stress carried by the wall) | yes or no | Setup |
| `a` | Inner radius | mm | Geometry |
| `b` | Outer radius | mm | Geometry |
| `E` | Young's modulus (used only for the collapse comparison) | GPa | Material |
| `nu` | Poisson's ratio (used only for the collapse comparison) | none | Material |
| `p_i` | Internal pressure | MPa | Loads |
| `p_o` | External pressure | MPa | Loads |
| `sigma_allow` | Allowable stress | MPa | Requirements |

## Assumptions

- The cylinder is long, elastic and isotropic, and its stresses vary with radius only.
- Closed ends carry the pressure end load as a uniform axial stress.

## Outputs

`sigma_r_a`, `sigma_t_a`, `sigma_r_b`, `sigma_t_b`, `sigma_z`, `sigma_vm_a`, `sigma_vm_b`, `sigma_tr_a`, `sigma_tr_b`, `sigma_vm_max`, `u`, `sigma_thin` and `k_wall`. For an external pressure above the internal on a wall thinner than 10 % of the inner radius, the sheet also gives `p_cr` and `u_cr`.

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `cylinder.stress_hoop_bore` | `sigma_t_a` | MPa | Hoop stress at the bore |
| `cylinder.stress_von_mises_max` | `sigma_vm_max` | MPa | Largest von Mises stress, bore or outer surface |

## Checks

- von Mises against allowable: the largest von Mises stress against `sigma_allow`.
- Net external pressure against the elastic collapse pressure (an upper bound): made only when `p_o > p_i` on a wall thinner than 10 % of the inner radius.
- Rules `geometry` and `no-pressure` (blocks); `stability` and `thin-wall` (warnings).

## Limitations

The stress check is not a stability check. Under external pressure a thin wall collapses elastically before the stress allowable is reached. When `p_o > p_i` and `(b − a)/a < 0.1`, the sheet warns (`stability`) and reports the long-tube elastic collapse pressure `p_cr = E/(4(1 − ν²))·(t/r_m)³` (Roark, ch. 15). This is an upper bound, because out-of-roundness lowers it. The sheet checks the net external pressure against it (`u_cr`). A 95/100 mm tube under 7.5 MPa passes the stress check at 0.666 but loads 98 % of its 7.67 MPa collapse pressure.

## References

- Young, *Roark's Formulas for Stress and Strain*, Ch. 13, Table 13.5, 8th ed.: thick-walled cylinders.
- Budynas, *Shigley's Mechanical Engineering Design*, §3-14, 10th ed.: stresses in pressurised cylinders.

## Test cases

Three v0.16 vectors (internal pressure with closed ends, external pressure with closed ends, internal pressure with open ends). Each is analytic and confirmed.

**Changes in 2.2.1 (round-off on the sheet, C2-20).** A radial stress at a free surface that comes out as round-off is shown as 0 MPa in its working line and in the substitutions that use it, as its Results row already was; no digit of round-off is printed. No result changes.

**Changes in 2.2.0 (C-2).** The inputs are in groups with `closed` first; the checks show the stress and the allowable, and the pressure and the collapse pressure; two ports are declared; messages are rewritten. No result changes.

**Changes in 2.1.0.** `E` and `nu` are inputs for the collapse comparison above.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Internal pressure, closed ends ([sheet](../../worked-examples/mechanical/mech.thick-cylinder/internal-pressure-closed/sheet.txt)).

```text
A = ((50 mm)^2 × 50 MPa − (100 mm)^2 × 0 MPa) / ((100 mm)^2 − (50 mm)^2) = 16.667 MPa
B = (50 mm)^2 × (100 mm)^2 × (50 MPa − 0 MPa) / ((100 mm)^2 − (50 mm)^2) = 166670 MPa·mm²
sigma_r_a = 16.667 MPa − 166670 MPa·mm² / (50 mm)^2 = -50 MPa
sigma_t_a = 16.667 MPa + 166670 MPa·mm² / (50 mm)^2 = 83.333 MPa
sigma_r_b = 16.667 MPa − 166670 MPa·mm² / (100 mm)^2 = 0 MPa
sigma_t_b = 16.667 MPa + 166670 MPa·mm² / (100 mm)^2 = 33.333 MPa
sigma_z = 16.667 MPa
sigma_vm_a = sqrt(((83.333 MPa − (-50 MPa))^2 + ((-50 MPa) − 16.667 MPa)^2 + (16.667 MPa − 83.333 MPa)^2) / 2) = 115.47 MPa
sigma_vm_b = sqrt(((33.333 MPa − 0 MPa)^2 + (0 MPa − 16.667 MPa)^2 + (16.667 MPa − 33.333 MPa)^2) / 2) = 28.868 MPa
sigma_tr_a = max(83.333 MPa, -50 MPa, 16.667 MPa) − min(83.333 MPa, -50 MPa, 16.667 MPa) = 133.33 MPa
sigma_tr_b = max(33.333 MPa, 0 MPa, 16.667 MPa) − min(33.333 MPa, 0 MPa, 16.667 MPa) = 33.333 MPa
sigma_vm_max = max(115.47 MPa, 28.868 MPa) = 115.47 MPa
u = 115.47 MPa / 200 MPa = 0.57735
sigma_thin = (50 MPa − 0 MPa) × (50 mm + 100 mm) / 2 / (100 mm − 50 mm) = 75 MPa
… 1 more step(s) on the sheet
Governing: PASS  von Mises against allowable: 115.5 MPa vs 200 MPa  utilisation 0.58
```
<!-- hand-arithmetic:end -->
