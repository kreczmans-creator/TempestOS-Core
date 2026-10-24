# M3b — Bolt group, elastic method (`mech.bolt-group@1.1.1`)

**In plain terms.** When a load acts away from the centre of a bolt group, each bolt carries an equal share of the load directly plus a share of the twisting moment that grows with its distance from the centre. Adding the two as vectors gives the most heavily loaded bolt, which is then checked in shear, in tension where the load also pulls, and for the two acting together.

**Scope.** A planar bolt pattern under in-plane forces with eccentricity (torsion), an extra torque, axial load and out-of-plane moments: per-bolt shear and tension by the area-weighted elastic method, governing bolt, optional lever prying. Resistances per bolt are inputs with provenance (EN 1993-1-8 §3.6 / AISC Part 7 named for the method only).

**Method.** Centroid `x_c = ΣAx/ΣA`; `T = F_y(e_x − x_c) − F_x(e_y − y_c) + T_z`; `J = ΣA r²`; `v = A F/ΣA ⊕ T A r/J`; `t = A N/ΣA + M_x A y′/ΣA y′² + M_y A x′/ΣA x′²`; `t_p = t(1 + b/a)`; `u = √((v/V_R)² + (t_p/T_R)²)`.

**Inputs.** `bolts` table (x, y, A; ≤ 200), `F_x`, `F_y`, `e_x`, `e_y`, `T_z`, `N`, `M_x`, `M_y`, `V_R`, `T_R`, optional `a`, `b`.

**Outputs.** `T`, `J`, `v_max`, `t_max`, `u_max`, `governing`; per-bolt rows `b1…bn` in the working; series: bolt pattern. **Checks.** interaction on the governing bolt; shear alone; tension alone.

**Limitations.** Rigid parts, linear-elastic distribution, no bearing/slip resistance, prying as a simple lever. A single bolt takes direct shear only (the retired `calc.bolt-shear-capacity` case) and blocks under a torque; a moment about an axis all the bolts lie on blocks rather than being dropped.

**Test cases.** `ka-four-bolt-eccentric` (v_max 36.44 kN, u 0.911 — analytic, Confirmed); `ka-moment-tension` (t_max 50 kN — analytic, Confirmed); the legacy module's four hand calculations `ka-legacy-four-bolts`, `-line`, `-inclined`, `-centroid`, and `ka-legacy-single-bolt`.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Four-bolt bracket, 50 kN at 150 mm eccentricity ([sheet](../../worked-examples/mechanical/mech.bolt-group/four-bolt-bracket/sheet.txt)).

```text
n_b = 4
x_c = 84.3×0 + 84.3×100 + 84.3×100 + 84.3×0 / 337.2 = 50 mm
y_c = 84.3×0 + 84.3×0 + 84.3×100 + 84.3×100 / 337.2 = 50 mm
T = -50 × 150 − 0 × 0 + 0 = -7.5 kN·m
J = about the centroid = 1686000 mm⁴
b1 = v = (-18.75, 6.25) kN, t = 0 kN = 0.49411
b2 = v = (-18.75, -31.25) kN, t = 0 kN = 0.91109
b3 = v = (18.75, -31.25) kN, t = 0 kN = 0.91109
b4 = v = (18.75, 6.25) kN, t = 0 kN = 0.49411
v_max = bolt 2 = 36.443 kN
t_max = bolt 2 = 0 kN
u_max = √((36.443/40)² + (0/45)²) = 0.91109
governing = bolt 2 = "bolt 2"
u_v = 36.443 kN / 40 kN = 0.91109
… 2 more step(s) on the sheet
Governing: PASS  Circular shear–tension interaction (governing bolt): u_max = 0.91109 vs u_max ≤ 1  utilisation 0.911
```
<!-- hand-arithmetic:end -->
