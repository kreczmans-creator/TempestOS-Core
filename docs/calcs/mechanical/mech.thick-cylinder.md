# M2c — Thick-walled cylinder (`mech.thick-cylinder@2.1.1`)

**In plain terms.** In a thin pipe the wall stress is nearly uniform, but in a thick wall it is not: the hoop stress is highest at the bore and falls towards the outside. Lamé's solution gives the radial and hoop stresses at every radius for any inner and outer pressure, and the axial stress for closed ends. The largest equivalent stress, at the bore for internal pressure, is compared with the allowable.

**Scope.** Lamé stresses at bore and outer surface under internal and external pressure, open or closed ends; von Mises and Tresca; utilisation; thin-wall comparison. Migrated from `calc.thick-walled-cylinder` (v0.16) onto the worksheet engine; the old calculator and its vectors stay and this sheet reproduces them.

**Method.** `A = (a² p_i − b² p_o)/(b² − a²)`, `B = a² b² (p_i − p_o)/(b² − a²)`, `σ_r = A − B/r²`, `σ_θ = A + B/r²`, `σ_z = A` (closed) or 0. von Mises from the three principals; Tresca = max − min. Thin wall: `(p_i − p_o)(a + b)/(2(b − a))`.

**Inputs.** `a`, `b`, `p_i`, `p_o`, `closed`, `sigma_allow`, `E` and `nu` (2.1.0, used only for the collapse comparison below). **Outputs.** stresses at a and b, `sigma_z`, `sigma_vm_*`, `sigma_tr_*`, `sigma_vm_max`, `u`, `sigma_thin`, `k_wall`; for an external pressure above the internal on a wall thinner than 10 % of the inner radius, `p_cr` and `u_cr`. **Check.** von Mises against allowable. **Stability (2.1.0).** The stress check is not a stability check: under external pressure a thin wall collapses elastically before the stress allowable is reached. When `p_o > p_i` and `(b − a)/a < 0.1` the sheet warns (`stability`), reports the long-tube elastic collapse pressure `p_cr = E/(4(1 − ν²))·(t/r_m)³` (Roark, ch. 15; an upper bound, as out-of-roundness lowers it) and checks the net external pressure against it (`u_cr`); a 95/100 mm tube under 7.5 MPa passes the stress check at 0.666 but loads 98 % of its 7.67 MPa collapse pressure.

**Test cases.** three v0.16 vectors (internal/closed, external/closed, internal/open) — analytic, Confirmed.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Internal pressure, closed ends ([sheet](../../worked-examples/mechanical/mech.thick-cylinder/internal-pressure-closed/sheet.txt)).

```text
A = ((50 mm)^2 × 50 MPa − (100 mm)^2 × 0 MPa) / ((100 mm)^2 − (50 mm)^2) = 16.667 MPa
B = (50 mm)^2 × (100 mm)^2 × (50 MPa − 0 MPa) / ((100 mm)^2 − (50 mm)^2) = 166670 MPa·mm²
sigma_r_a = 16.667 MPa − 166670 MPa·mm² / (50 mm)^2 = -50 MPa
sigma_t_a = 16.667 MPa + 166670 MPa·mm² / (50 mm)^2 = 83.333 MPa
sigma_r_b = 16.667 MPa − 166670 MPa·mm² / (100 mm)^2 = 1.8626e-15 MPa
sigma_t_b = 16.667 MPa + 166670 MPa·mm² / (100 mm)^2 = 33.333 MPa
sigma_z = 16.667 MPa
sigma_vm_a = sqrt(((83.333 MPa − (-50 MPa))^2 + ((-50 MPa) − 16.667 MPa)^2 + (16.667 MPa − 83.333 MPa)^2) / 2) = 115.47 MPa
sigma_vm_b = sqrt(((33.333 MPa − 1.8626e-15 MPa)^2 + (1.8626e-15 MPa − 16.667 MPa)^2 + (16.667 MPa − 33.333 MPa)^2) / 2) = 28.868 MPa
sigma_tr_a = max(83.333 MPa, -50 MPa, 16.667 MPa) − min(83.333 MPa, -50 MPa, 16.667 MPa) = 133.33 MPa
sigma_tr_b = max(33.333 MPa, 1.8626e-15 MPa, 16.667 MPa) − min(33.333 MPa, 1.8626e-15 MPa, 16.667 MPa) = 33.333 MPa
sigma_vm_max = max(115.47 MPa, 28.868 MPa) = 115.47 MPa
u = 115.47 MPa / 200 MPa = 0.57735
sigma_thin = (50 MPa − 0 MPa) × (50 mm + 100 mm) / 2 / (100 mm − 50 mm) = 75 MPa
… 1 more step(s) on the sheet
Governing: PASS  von Mises against allowable: u = 0.58 vs u ≤ 1  utilisation 0.58
```
<!-- hand-arithmetic:end -->
