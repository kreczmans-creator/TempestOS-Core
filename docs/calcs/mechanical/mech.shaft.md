# M4 — Shaft: static, fatigue, keys, critical speed (`mech.shaft@1.1.1`)

**In plain terms.** A shaft carries gears, pulleys and bearings, so it bends and twists, and every turn the bending stress reverses, which is why shafts fail by fatigue at shoulders and keyways. The worksheet takes the stepped shaft, its loads and torque, finds the bending moment, slope and deflection along it, the fatigue and yield safety factors at the sections you mark, the keys, and the speed at which the shaft would whirl.

**Scope.** Stepped (optionally hollow) shaft on two bearings with radial loads in two planes and a steady torque: M, slope and deflection along it; fatigue and yield factors at user-marked sections; slopes at the bearings; deflection at the loads; first lateral critical speed; parallel keys.

**Method.** The shaft is an S2 beam (`Tempest.Calc.Structural.Frame`, pin at A, roller at B) solved once per plane; M, slope and deflection combine as vectors. `K_f = 1 + q(K_t − 1)`; `σ_a = K_f 32 M d/(π(d⁴ − d_i⁴))`, `τ_m = K_fs 16 T d/(π(d⁴ − d_i⁴))` on the smaller side of a shoulder. `S_e = k_a k_b k_c k_d k_e k_f S_e1`, `S_e1` (the rotating-beam limit S_e′) `= 0.5 S_ut` (≤ 1400 MPa, else 700); `k_a = a S_ut^b` (ground, machined, hot-rolled, forged; 10th ed. Table 6-2 coefficients, overridable by a `shaft.marin` record — the 11th edition revised them), `k_b = 1.24 d^−0.107` (≤ 51 mm) or `1.51 d^−0.157` (to 254 mm, held above), `k_c = 1`, `k_e = 1 − 0.08 z_R` (normal quantile). DE-Goodman `1/n_f = σ_a/S_e + √3 τ_m/S_ut`; `n_y = S_y/√(σ_a² + 3τ_pk²)`, where `τ_pk` is built from the larger of `T` and the optional peak torque `T_pk` (blank: `T`), so the first-cycle yield check sees the peak torque while the fatigue check keeps the steady one (Shigley 10th ed. §6-9, §7-4). Rayleigh `ω = √(g Σm y/Σm y²)` with shaft self-mass and carried masses; Dunkerley `1/ω² = Σ m δ_ii` (§7-6); **the ratio `r_c` and the warning use the Dunkerley lower bound** (the Rayleigh value is an upper bound), warning when `n_c,D/n < 2`. When nothing deflects (ρ = 0 and every carried mass on a bearing, or no mass at all) no critical speed is found and a warning (`critical-speed-skipped`) says why. Keys `τ = 2T/(d w L)`, `σ = 4T/(d h L)`, `L_req = max(2T n_target/(d w 0.577 S_y), 4T n_target/(d h S_y))` at the target factor (§7-7). A key given only some of `x_key`, `w`, `h`, `L_key` warns (rule `key`) instead of dropping the check.

**Inputs.** `segments` (x_end 1–3000 mm, d 5–300 mm, bore), `x_A`, `x_B`; `loads` (x, F_y, F_z kN, mass kg); `T` 0–50 kN·m between `x_T1` and `x_T2`, optional `T_pk` (peak torque for the yield check); `n` 0–30 000 rpm; `sections` (x, K_t, K_ts, q, q_s); `S_ut` 300–2000 MPa, `S_y`, `E`, `rho` (record `shaft.material`); `finish`, `R` 50–99.9 %, `k_d`, `k_f`; `n_target` 1.2–4; `theta_max`, `y_max` (record `shaft.limits`); optional key `x_key`, `w`, `h`, `L_key`, `S_y_key`.

**Outputs.** `R_A`, `R_B`, `d_A`, `d_B`, `M_max`, `theta_A`, `theta_B`, `y_load`, `S_e`, `n_f_min`, `n_y_min`, per-section `X1…`, `n_c`, `n_c_D`, `r_c`, key `tau_key`, `sigma_key`, `L_key_req`. **Ports.** `R_A`, `R_B` → M5; `d_A`, `d_B` → M2; `n_f_min`, `n_c`. Links in: E1 torque and speed.

**Limitations.** Linear elastic; two simple supports (the bearings must be at least 1/200 of the shaft length apart, else the sheet blocks in its own words); no torsional vibration; first lateral mode only; K_t from records or fit equations, never a digitised chart.

**Test cases.** `ka-simply-supported` (slope 2.430 mrad, deflection 0.3240 mm, n_c 5305.2 rpm — analytic, Confirmed), `ka-key` (τ 36.99, σ 84.56 MPa — analytic, Confirmed), `ka-shigley-7-1` (S_e 201.50 MPa, n_f 1.621, n_y 4.489 with the example's data converted to SI exactly — **Unconfirmed** until compared with the book's printed answers). Mott 6th ed. shaft examples are not pinned (owner copy needed).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Gearbox input shaft: pinion between bearings, coupling overhung ([sheet](../../worked-examples/mechanical/mech.shaft/gearbox-input-shaft/sheet.txt)).

```text
R_A = √(1.1² + -3²) = 3.1953 kN
R_B = √(1.1² + -3²) = 3.1953 kN
d_A = x = 20 mm = 35 mm
d_B = x = 280 mm = 35 mm
M_max = 37 stations = 0.41539 kN·m
theta_A = x = 20 mm = 0.0008961 rad
theta_B = x = 280 mm = 0.0008961 rad
y_load = 0.071775 mm at 150 = 0.071775 mm
k_a = 4.51 × 700^-0.265 (machined or cold-drawn) = 0.79474
k_e = 1 − 0.08 × 2.3263 = 0.81389
S_e1 = 700 MPa = 350 MPa
X1 = x = 120 mm, d = 40 mm, k_b = 0.83561, S_e = 189.17 MPa, K_f = 1.56, K_fs = 1.45, M = 319.53 N·m, T = 0 N·m, σ_a = 79.334 MPa, τ_m = 0 MPa, n_y = 6.3025 = 2.3845
X2 = x = 150 mm, d = 45 mm, k_b = 0.82514, S_e = 186.8 MPa, K_f = 1.912, K_fs = 2.8, M = 415.39 N·m, T = 300 N·m, σ_a = 88.778 MPa, τ_m = 46.947 MPa, n_y = 4.1532 = 1.6909
X3 = x = 180 mm, d = 40 mm, k_b = 0.83561, S_e = 189.17 MPa, K_f = 1.56, K_fs = 1.45, M = 319.53 N·m, T = 300 N·m, σ_a = 79.334 MPa, τ_m = 34.616 MPa, n_y = 5.0281 = 1.9801
… 15 more step(s) on the sheet
Governing: PASS  Slope at the bearings against limit: u_slope = 0.8961 vs u_slope ≤ 1  utilisation 0.896
```
<!-- hand-arithmetic:end -->
