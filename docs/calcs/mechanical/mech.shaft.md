# M4 — Shaft: static, fatigue, keys, critical speed (`mech.shaft@1.0.0`)

**Scope.** Stepped (optionally hollow) shaft on two bearings with radial loads in two planes and a steady torque: M, slope and deflection along it; fatigue and yield factors at user-marked sections; slopes at the bearings; deflection at the loads; first lateral critical speed; parallel keys.

**Method.** The shaft is an S2 beam (`Tempest.Calc.Structural.Frame`, pin at A, roller at B) solved once per plane; M, slope and deflection combine as vectors. `K_f = 1 + q(K_t − 1)`; `σ_a = K_f 32 M d/(π(d⁴ − d_i⁴))`, `τ_m = K_fs 16 T d/(π(d⁴ − d_i⁴))` on the smaller side of a shoulder. `S_e = k_a k_b k_c k_d k_e k_f S_e′`, `S_e′ = 0.5 S_ut` (≤ 1400 MPa, else 700); `k_a = a S_ut^b` (ground, machined, hot-rolled, forged; 10th ed. Table 6-2 coefficients, overridable by a `shaft.marin` record — the 11th edition revised them), `k_b = 1.24 d^−0.107` (≤ 51 mm) or `1.51 d^−0.157` (to 254 mm, held above), `k_c = 1`, `k_e = 1 − 0.08 z_R` (normal quantile). DE-Goodman `1/n_f = σ_a/S_e + √3 τ_m/S_ut`; `n_y = S_y/√(σ_a² + 3τ_m²)` (Shigley 10th ed. §6-9, §7-4). Rayleigh `ω = √(g Σm y/Σm y²)` with shaft self-mass and carried masses; Dunkerley `1/ω² = Σ m δ_ii` (§7-6); warn when n_c/n < 2. Keys `τ = 2T/(d w L)`, `σ = 4T/(d h L)`, `L_req` at the target factor (§7-7).

**Inputs.** `segments` (x_end 1–3000 mm, d 5–300 mm, bore), `x_A`, `x_B`; `loads` (x, F_y, F_z kN, mass kg); `T` 0–50 kN·m between `x_T1` and `x_T2`; `n` 0–30 000 rpm; `sections` (x, K_t, K_ts, q, q_s); `S_ut` 300–2000 MPa, `S_y`, `E`, `rho` (record `shaft.material`); `finish`, `R` 50–99.9 %, `k_d`, `k_f`; `n_target` 1.2–4; `theta_max`, `y_max` (record `shaft.limits`); optional key `x_key`, `w`, `h`, `L_key`, `S_y_key`.

**Outputs.** `R_A`, `R_B`, `d_A`, `d_B`, `M_max`, `theta_A`, `theta_B`, `y_load`, `S_e`, `n_f_min`, `n_y_min`, per-section `X1…`, `n_c`, `n_c_D`, `r_c`, key `tau_key`, `sigma_key`, `L_key_req`. **Ports.** `R_A`, `R_B` → M5; `d_A`, `d_B` → M2; `n_f_min`, `n_c`. Links in: E1 torque and speed.

**Limitations.** Linear elastic; two simple supports; no torsional vibration; first lateral mode only; K_t from records or fit equations, never a digitised chart.

**Test cases.** `ka-simply-supported` (slope 2.430 mrad, deflection 0.3240 mm, n_c 5305.2 rpm — analytic, Confirmed), `ka-key` (τ 36.99, σ 84.56 MPa — analytic, Confirmed), `ka-shigley-7-1` (S_e 203.7 MPa, n_f 1.650, n_y 4.586 with the example's data — **Unconfirmed** until compared with the book's printed answers). Mott 6th ed. shaft examples are not pinned (owner copy needed).
