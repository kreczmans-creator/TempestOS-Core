# S4 — Bolts and fillet welds, EN 1993-1-8 (`struct.connection-ec3@1.1.0`)

**Scope.** One bolt in a plate (category A shear, category D tension): shear, bearing, tension, punching, combined shear and tension; optional block tearing; spacing minima; a fillet weld by the simplified and directional methods. Every input is hard-limited (M12–M36, t 3–50 mm).

**Method.** f_ub from the property class; A_s from the ISO 898-1 formula with ISO 261 pitches. `F_v,Rd = α_v f_ub A n_s/γ_M2` (α_v 0.6 or 0.5 per Table 3.4); `F_b,Rd = k₁ α_b f_u d t/γ_M2` at the worst of end/inner and edge bolts; `F_t,Rd = 0.9 f_ub A_s/γ_M2`; `B_p,Rd = 0.6π d_m t f_u/γ_M2`; `F_v/F_v,Rd + F_t/(1.4F_t,Rd) ≤ 1` (cl. 3.6.1). Block tearing `f_u A_nt/γ_M2 + f_y A_nv/(√3 γ_M0)` (cl. 3.10.2). Welds `f_vw,d = f_u/(√3 β_w γ_M2)` (cl. 4.5.3.3), directional `√(σ⊥² + 3(τ⊥² + τ∥²)) ≤ f_u/(β_w γ_M2)`, `σ⊥ ≤ 0.9 f_u/γ_M2` (cl. 4.5.3.2); β_w by grade (Table 4.1).

**Inputs.** `grade`, `size`, optional `d_0`, `n_s`, `threads`; `steel`, `t`, `f_y`, `f_u` (record `steel.strengths`); `e_1`, `e_2`, optional `p_1`, `p_2`; Table 3.3 minima `k_e1`, `k_e2`, `k_p1`, `k_p2` and `gamma_M2`, `gamma_M0` (record `ec3.national-annex`); `F_v_Ed`, `F_t_Ed`; optional block tearing `V_Ed`, `A_nt`, `A_nv`; optional weld `a`, `F_w_Ed`, `sigma_perp`, `tau_perp`, `tau_par`.

**Outputs.** `f_ub`, `A_s`, `alpha_v`, `F_v_Rd`, `alpha_b`, `k_1`, `F_b_Rd`, `F_t_Rd`, `B_p_Rd`, utilisations, `governing`, `V_eff_Rd`, `f_vw_d`, `F_w_Rd`. **Ports.** `F_v_Rd`, `F_b_Rd` (→ S6 `F_1_vb_Rd`), `F_t_Rd` (→ S6 `F_t_Rd`), `A_s` (→ S6 `A_s`; port added in 1.1.0), `f_vw_d` (→ M6). Links in: M3 bolt forces, M6 weld force per length, S3, S5.

**Limitations.** Static; categories A and D only; no slip-resistant joints, no moment end plates, no countersunk or long joints.

**v0.25.2 reconciliation (ADR-0169).** The legacy bolt-shear module is the shank-in-plane case (gross area, α_v 0.6 for every class) and now says so; threads in the shear plane are handled here (A_s, α_v 0.5 for 4.8–6.8 and 10.9).

**Test cases.** `ka-m20-88-shear-bearing` (F_v,Rd 94.00, F_t,Rd 141.00, F_b,Rd 99.39, B_p,Rd 199.83 kN), `ka-m20-109-threads` (97.92 kN), `ka-weld-s275` (f_vw,d 222.79 MPa) — analytic, Confirmed; `ka-p363-m20-88` (SCI P363 94.1 / 141 kN, 1 %) and `ka-p358-fin-plate` (bearing 99.4 kN pattern) — **Unconfirmed** until checked against the owner's SCI copies.
