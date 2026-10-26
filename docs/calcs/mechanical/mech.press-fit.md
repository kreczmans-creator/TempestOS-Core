# M2b — Press and shrink fit (`mech.press-fit@1.2.0`)

**In plain terms.** Pressing a shaft into a slightly smaller hole stretches the hub and squeezes the shaft until the two meet at a common radius. That interference sets up a contact pressure, found by treating hub and shaft as thick cylinders, and the pressure times friction over the contact area is the torque and axial force the joint can carry without slipping. The same pressure makes the hub's hoop stress highest at its bore, which is checked against yield, and heating or spinning the parts changes the interference.

**Scope.** Interface pressure, hub and shaft stresses, von Mises utilisation against each yield, assembly force, friction torque, shrink-fit temperature rise. Shigley §3-16/§3-17 and Roark Ch. 13 relations in this sheet's notation.

**Method.** `δ_r = δ/2`; `C_h = (R/E_h)((r_o² + R²)/(r_o² − R²) + ν_h)`, `C_s = (R/E_s)((R² + r_i²)/(R² − r_i²) − ν_s)`, `p = δ_r/(C_h + C_s)`; `σ_θ,hub = p (r_o² + R²)/(r_o² − R²)`, `σ_θ,shaft = −p (R² + r_i²)/(R² − r_i²)`, `σ_r = −p`; von Mises with σ_z = 0; `F = μ p 2πR L`, `T = μ p 2πR² L`; `ΔT = (δ + c_a)/(α d)`.

**Inputs.** `d`, `delta` (diametral, µm), `d_o`, `d_i` (0 solid), `L`, `E_h`, `nu_h`, `E_s`, `nu_s`, `mu` (with its source), `S_y_h`, `S_y_s`, `alpha_h`, `c_a`.

**Outputs.** `p`, `sigma_t_hub`, `sigma_t_shaft`, `sigma_vm_hub`, `sigma_vm_shaft` (at the shaft surface), `sigma_vm_shaft_bore` and `sigma_vm_shaft_max` (1.1.0: the hollow shaft's bore, where the hoop stress `2pR²/(R² − r_i²)` is largest and the radial stress zero, and the larger of the two), `F_a`, `T`, `dT`. **Checks.** hub yield, shaft yield at the surface and the bore (d 50, bore 25, hub 100 mm, δ 120 µm: surface 216.6 MPa, bore 397.4 MPa, u 1.136 where the check read 0.619 and passed; a solid shaft is unchanged).

**Limitations.** Plane stress, uniform pressure, no end effects, no surface-finish loss of interference, friction as entered.

**Changes in 1.2.0 (mech.press-fit-R1).** New optional inputs `Rz_h` and `Rz_s` (µm). With a roughness entered the interference that grips is `δ_eff = max(δ − 0.8·(Rz_h + Rz_s), 0)` (DIN 7190-1: the roughness peaks flatten on assembly), the gripping pressure is `p_eff = p·δ_eff/δ`, and the axial force `F_a` and the torque `T` use `p_eff`; the stresses and the yield checks stay at the nominal `p` (the larger interference of the fit). Default case (δ 40 µm, Rz 4 µm on each part): `δ_eff` 33.6 µm, `T` 2194.8 N·m before, 1843.6 N·m now (−16 %). With no roughness entered the numbers are unchanged and every run carries the assumption that the smoothing is not applied, that the torque needs the minimum interference of the fit and the stresses the maximum. New outputs `delta_eff` and `p_eff`.

**Test cases.** `ka-steel-steel-solid` (p 62.1 MPa, hub hoop 103.5 MPa, F 87.8 kN, T 2195 N·m, ΔT 100 K — analytic); `ka-hollow-shaft` (p 49.6 MPa — analytic).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 50 mm solid steel shaft in a 100 mm steel hub ([sheet](../../worked-examples/mechanical/mech.press-fit/solid-steel-shaft/sheet.txt)).

```text
R = 50 mm / 2 = 25 mm
r_o = 100 mm / 2 = 50 mm
r_i = 0 mm / 2 = 0 mm
delta_r = 40 µm / 2 = 20 µm
C_h = 25 mm / 207 GPa × (((50 mm)^2 + (25 mm)^2) / ((50 mm)^2 − (25 mm)^2) + 0.292) = 0.23655 mm/GPa
C_s = 25 mm / 207 GPa × (((25 mm)^2 + (0 mm)^2) / ((25 mm)^2 − (0 mm)^2) − 0.292) = 0.085507 mm/GPa
p = 20 µm / (0.23655 mm/GPa + 0.085507 mm/GPa) = 62.1 MPa
sigma_t_hub = 62.1 MPa × ((50 mm)^2 + (25 mm)^2) / ((50 mm)^2 − (25 mm)^2) = 103.5 MPa
sigma_r_hub = −62.1 MPa = -62.1 MPa
sigma_t_shaft = −62.1 MPa × ((25 mm)^2 + (0 mm)^2) / ((25 mm)^2 − (0 mm)^2) = -62.1 MPa
sigma_r_shaft = −62.1 MPa = -62.1 MPa
sigma_vm_hub = sqrt((103.5 MPa)^2 − 103.5 MPa × (-62.1 MPa) + (-62.1 MPa)^2) = 144.9 MPa
sigma_vm_shaft = sqrt((-62.1 MPa)^2 − (-62.1 MPa) × (-62.1 MPa) + (-62.1 MPa)^2) = 62.1 MPa
sigma_vm_shaft_bore = 62.1 MPa
… 6 more step(s) on the sheet
Governing: PASS  Hub yield: 144.9 MPa vs 355 MPa  utilisation 0.408
```
<!-- hand-arithmetic:end -->
