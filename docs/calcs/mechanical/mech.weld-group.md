# M6 — Weld group, elastic line method (`mech.weld-group@1.0.0`)

**Scope.** Straight fillet welds in a plane under in-plane forces with eccentricity, an out-of-plane force and out-of-plane moments: force per unit length at every weld end, critical point, throat stress against a design strength, leg required. Shares the group-geometry kernel (`GroupGeometry`) with M3 bolt group.

**Method.** Unit throat lines: `L_w = Σl`; `x_c = Σ l x_m/L_w`; `I_ux = Σ[l(y_m − y_c)² + l Δy²/12]`, `I_uy` likewise; `J_u = I_ux + I_uy` (Shigley 11th ed. Tables 9-1, 9-2; Blodgett 1966 §7.4). `T = F_y(e_x − x_c) − F_x(e_y − y_c) + M_z`. At each end: `F/L_w` + `T r/J_u` ⊥ r + `F_z/L_w + M_x y′/I_ux + M_y x′/I_uy`; `f_r` the vector sum. `a = 0.707 s`, `τ = f_r,max/a`, `s_req = f_r,max/(0.707 f_w)`.

**Inputs.** `welds` table (x1, y1, x2, y2 −2000…2000 mm; ≤ 50), `s` 3–25 mm, `f_w` (from S4 or a user allowable with source), `F_x`, `F_y`, `F_z` kN, `e_x`, `e_y`, `M_x`, `M_y`, `M_z` kN·m.

**Outputs.** `L_w`, `x_c`, `y_c`, `I_ux`, `I_uy`, `J_u`, `T`, `f_r_max`, `critical`, `a`, `tau`, `s_req`, `u`. **Ports.** `f_r_max` → S4, `tau`, `s_req`. **Check.** throat stress against design strength.

**Limitations.** Elastic, static, fillet welds of one leg size; rigid parts.

**Test cases.** `ka-two-vertical` (J_u 291 666.7 mm³, f_r 721.11 N/mm), `ka-box-torsion` (106.07 N/mm), `ka-two-horizontal-bending` (502.49 N/mm) — Shigley Table 9-1/9-2 closed forms, Confirmed. Shigley Ch. 9 numbered examples and Blodgett's worked examples are not pinned (owner copies needed).
