# M2b — Press and shrink fit (`mech.press-fit@1.0.0`)

**In plain terms.** Pressing a shaft into a slightly smaller hole stretches the hub and squeezes the shaft until the two meet at a common radius. That interference sets up a contact pressure, found by treating hub and shaft as thick cylinders, and the pressure times friction over the contact area is the torque and axial force the joint can carry without slipping. The same pressure makes the hub's hoop stress highest at its bore, which is checked against yield, and heating or spinning the parts changes the interference.

**Scope.** Interface pressure, hub and shaft stresses, von Mises utilisation against each yield, assembly force, friction torque, shrink-fit temperature rise. Shigley §3-16/§3-17 and Roark Ch. 13 relations in this sheet's notation.

**Method.** `δ_r = δ/2`; `C_h = (R/E_h)((r_o² + R²)/(r_o² − R²) + ν_h)`, `C_s = (R/E_s)((R² + r_i²)/(R² − r_i²) − ν_s)`, `p = δ_r/(C_h + C_s)`; `σ_θ,hub = p (r_o² + R²)/(r_o² − R²)`, `σ_θ,shaft = −p (R² + r_i²)/(R² − r_i²)`, `σ_r = −p`; von Mises with σ_z = 0; `F = μ p 2πR L`, `T = μ p 2πR² L`; `ΔT = (δ + c_a)/(α d)`.

**Inputs.** `d`, `delta` (diametral, µm), `d_o`, `d_i` (0 solid), `L`, `E_h`, `nu_h`, `E_s`, `nu_s`, `mu` (with its source), `S_y_h`, `S_y_s`, `alpha_h`, `c_a`.

**Outputs.** `p`, `sigma_t_hub`, `sigma_t_shaft`, `sigma_vm_hub`, `sigma_vm_shaft`, `F_a`, `T`, `dT`. **Checks.** hub yield, shaft yield.

**Limitations.** Plane stress, uniform pressure, no end effects, no surface-finish loss of interference, friction as entered.

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
F_a = 0.15 × 62.1 MPa × 2 × π × 25 mm × 60 mm = 87.792 kN
… 2 more step(s) on the sheet
Governing: PASS  Hub yield: 144.9 MPa vs 355 MPa  utilisation 0.408
```
<!-- hand-arithmetic:end -->
