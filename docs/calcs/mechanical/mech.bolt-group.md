# M3b — Bolt group, elastic method (`mech.bolt-group@1.0.0`)

**Scope.** A planar bolt pattern under in-plane forces with eccentricity (torsion), an extra torque, axial load and out-of-plane moments: per-bolt shear and tension by the area-weighted elastic method, governing bolt, optional lever prying. Resistances per bolt are inputs with provenance (EN 1993-1-8 §3.6 / AISC Part 7 named for the method only).

**Method.** Centroid `x_c = ΣAx/ΣA`; `T = F_y(e_x − x_c) − F_x(e_y − y_c) + T_z`; `J = ΣA r²`; `v = A F/ΣA ⊕ T A r/J`; `t = A N/ΣA + M_x A y′/ΣA y′² + M_y A x′/ΣA x′²`; `t_p = t(1 + b/a)`; `u = √((v/V_R)² + (t_p/T_R)²)`.

**Inputs.** `bolts` table (x, y, A; ≤ 200), `F_x`, `F_y`, `e_x`, `e_y`, `T_z`, `N`, `M_x`, `M_y`, `V_R`, `T_R`, optional `a`, `b`.

**Outputs.** `T`, `J`, `v_max`, `t_max`, `u_max`, `governing`; per-bolt rows `b1…bn` in the working; series: bolt pattern. **Checks.** interaction on the governing bolt; shear alone; tension alone.

**Limitations.** Rigid parts, linear-elastic distribution, no bearing/slip resistance, prying as a simple lever.

**Test cases.** `ka-four-bolt-eccentric` (v_max 36.44 kN, u 0.911 — analytic, Confirmed); `ka-moment-tension` (t_max 50 kN — analytic, Confirmed).
