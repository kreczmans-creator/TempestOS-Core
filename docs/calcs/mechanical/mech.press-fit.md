# M2b — Press and shrink fit (`mech.press-fit@1.0.0`)

**Scope.** Interface pressure, hub and shaft stresses, von Mises utilisation against each yield, assembly force, friction torque, shrink-fit temperature rise. Shigley §3-16/§3-17 and Roark Ch. 13 relations in this sheet's notation.

**Method.** `δ_r = δ/2`; `C_h = (R/E_h)((r_o² + R²)/(r_o² − R²) + ν_h)`, `C_s = (R/E_s)((R² + r_i²)/(R² − r_i²) − ν_s)`, `p = δ_r/(C_h + C_s)`; `σ_θ,hub = p (r_o² + R²)/(r_o² − R²)`, `σ_θ,shaft = −p (R² + r_i²)/(R² − r_i²)`, `σ_r = −p`; von Mises with σ_z = 0; `F = μ p 2πR L`, `T = μ p 2πR² L`; `ΔT = (δ + c_a)/(α d)`.

**Inputs.** `d`, `delta` (diametral, µm), `d_o`, `d_i` (0 solid), `L`, `E_h`, `nu_h`, `E_s`, `nu_s`, `mu` (with its source), `S_y_h`, `S_y_s`, `alpha_h`, `c_a`.

**Outputs.** `p`, `sigma_t_hub`, `sigma_t_shaft`, `sigma_vm_hub`, `sigma_vm_shaft`, `F_a`, `T`, `dT`. **Checks.** hub yield, shaft yield.

**Limitations.** Plane stress, uniform pressure, no end effects, no surface-finish loss of interference, friction as entered.

**Test cases.** `ka-steel-steel-solid` (p 62.1 MPa, hub hoop 103.5 MPa, F 87.8 kN, T 2195 N·m, ΔT 100 K — analytic); `ka-hollow-shaft` (p 49.6 MPa — analytic).
