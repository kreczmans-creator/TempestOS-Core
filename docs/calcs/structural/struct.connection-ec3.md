# S4 — Bolts and fillet welds, EN 1993-1-8 (`struct.connection-ec3@1.2.1`)

**In plain terms.** A bolted joint can fail by the bolt shearing, the plate tearing or crushing at the hole, or the bolt pulling through or stretching, and a fillet weld can fail along its throat. The worksheet takes the bolt, plate and weld you describe and the loads on them, works out how much each failure mode can carry under Eurocode 3, and divides the load by the smallest capacity. A ratio under 1 passes, and the mode with the highest ratio governs.

**Scope.** One bolt in a plate (category A shear, category D tension): shear, bearing, tension, punching, combined shear and tension; optional block tearing; spacing minima; a fillet weld by the simplified and directional methods. Every input is hard-limited (M12–M36, t 3–50 mm).

**Method.** f_ub from the property class; A_s from the ISO 898-1 formula with ISO 261 pitches. `F_v,Rd = α_v f_ub A n_s/γ_M2` (α_v 0.6 or 0.5 per Table 3.4); `F_b,Rd = k₁ α_b f_u d t/γ_M2` at the worst of end/inner and edge bolts; `F_t,Rd = 0.9 f_ub A_s/γ_M2`; `B_p,Rd = 0.6π d_m t f_u/γ_M2` with `d_m = (s + e)/2` from the across-flats `s` and the across-corners `e` of the smaller of head and nut (optional input `e_head`), or `d_m = s` when `e_head` is blank (conservative: the sharp-hexagon value overstated it by 8 %); `F_v/F_v,Rd + F_t/(1.4F_t,Rd) ≤ 1` (cl. 3.6.1). Block tearing `f_u A_nt/γ_M2 + f_y A_nv/(√3 γ_M0)` (cl. 3.10.2). Welds `f_vw,d = f_u/(√3 β_w γ_M2)` (cl. 4.5.3.3), directional `√(σ⊥² + 3(τ⊥² + τ∥²)) ≤ f_u/(β_w γ_M2)`, `σ⊥ ≤ 0.9 f_u/γ_M2` (cl. 4.5.3.2); β_w by grade (Table 4.1).

**Inputs.** `grade`, `size`, optional `d_0`, `n_s`, `threads`, `e_head`; `steel`, `t`, `f_y`, `f_u` (record `steel.strengths`); `e_1`, `e_2`, optional `p_1`, `p_2`; Table 3.3 minima `k_e1`, `k_e2`, `k_p1`, `k_p2` and `gamma_M2`, `gamma_M0` (record `ec3.national-annex`); `F_v_Ed`, `F_t_Ed`; optional block tearing `V_Ed`, `A_nt`, `A_nv`; optional weld `a`, `F_w_Ed`, `sigma_perp`, `tau_perp`, `tau_par`.

**Outputs.** `f_ub`, `A_s`, `alpha_v`, `F_v_Rd`, `alpha_b`, `k_1`, `F_b_Rd`, `F_t_Rd`, `B_p_Rd`, utilisations, `governing`, `V_eff_Rd`, `f_vw_d`, `F_w_Rd`. **Ports.** `F_v_Rd`, `F_b_Rd` (→ S6 `F_1_vb_Rd`), `F_t_Rd` (→ S6 `F_t_Rd`), `A_s` (→ S6 `A_s`), `f_ub`, `f_yb` (→ S6 `f_ub`, `f_yb`; ports added in 1.1.0; in the steel-support chain a second S4 run for the base plate's thickness and edge distances feeds S6), `f_vw_d` (→ M6). Links in: M3 bolt forces, M6 weld force per length, S3, S5.

**Limitations.** A spacing that leaves no bearing resistance (`e_2` or `p_2` so small that `k_1 ≤ 0`, or `p_1` so small that `α_d ≤ 0`) blocks naming the input and the Table 3.3 minimum, instead of failing with a division by zero. Static; categories A and D only; no slip-resistant joints, no moment end plates, no countersunk or long joints.

**v0.25.2 reconciliation (ADR-0169).** The legacy bolt-shear module is the shank-in-plane case (gross area, α_v 0.6 for every class) and now says so; threads in the shear plane are handled here (A_s, α_v 0.5 for 4.8–6.8 and 10.9).

**Test cases.** `ka-m20-88-shear-bearing` (F_v,Rd 94.00, F_t,Rd 141.00, F_b,Rd 99.39, B_p,Rd 185.48 kN with d_m = s), `ka-m20-punching-e-head` (e = 34 mm, illustrative: B_p,Rd 197.84 kN), `ka-m20-109-threads` (97.92 kN), `ka-weld-s275` (f_vw,d 222.79 MPa) — analytic, Confirmed; `ka-p363-m20-88` (SCI P363 94.1 / 141 kN, 1 %) and `ka-p358-fin-plate` (bearing 99.4 kN pattern) — **Unconfirmed** until checked against the owner's SCI copies.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Beam web splice: M20 8.8 in double shear through 10 mm S275 plates ([sheet](../../worked-examples/structural/struct.connection-ec3/beam-splice-m20/sheet.txt)).

```text
f_ub = class 8.8 = 800 MPa
f_yb = class 8.8 = 640 MPa
A_s = d = 20, P = 2.5 mm = 244.79 mm²
d_0 = 22 mm
alpha_v = class 8.8 = 0.6
F_v_Rd = 0.6 × 800 × 244.79 × 2 / 1.25 = 188 kN
alpha_b = min(0.60606, 0.81061, 1.9512, 1) = 0.60606
k_1 = e₂ = 35, p₂ = —, d₀ = 22 = 2.5
F_b_Rd = 2.5 × 0.60606 × 410 × 20 × 10 / 1.25 = 99.394 kN
F_t_Rd = 0.9 × 800 × 244.79 / 1.25 = 141 kN
B_p_Rd = 0.6π × 30 × 10 × 410 / 1.25 = 185.48 kN
u_v = 55 kN / 188 kN = 0.29255
u_b = 55 kN / 99.394 kN = 0.55335
u_t = 0 kN / min(141 kN, 185.48 kN) = 0
… 1 more step(s) on the sheet
Governing: PASS  Bolt bearing: u_b = 0.55335 vs u_b ≤ 1  utilisation 0.553  « EN 1993-1-8 Table 3.4
```
<!-- hand-arithmetic:end -->
