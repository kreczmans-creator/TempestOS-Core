# M3b — Bolt group, elastic method (`mech.bolt-group@1.4.0`)

**In plain terms.** When a load acts away from the centre of a bolt group, each bolt carries an equal share of the load directly plus a share of the twisting moment that grows with its distance from the centre. Adding the two as vectors gives the most heavily loaded bolt, which is then checked in shear, in tension where the load also pulls, and for the two acting together.

**Scope.** A planar bolt pattern under in-plane forces with eccentricity (torsion), an extra torque, axial load and out-of-plane moments: per-bolt shear and tension by the area-weighted elastic method, governing bolt, optional lever prying. Resistances per bolt are inputs with provenance (EN 1993-1-8 §3.6 / AISC Part 7 named for the method only).

**Method.** Centroid `x_c = ΣAx/ΣA`; `T = F_y(e_x − x_c) − F_x(e_y − y_c) + T_z`; `J = ΣA r²`; `v = A F/ΣA ⊕ T A r/J`; `t = A N/ΣA + A[(M_x I_y − M_y I_xy) y′ + (M_y I_x − M_x I_xy) x′]/(I_x I_y − I_xy²)` with `I_x = ΣA y′²`, `I_y = ΣA x′²`, `I_xy = ΣA x′y′` (the shared kernel's unsymmetric-bending form; it reduces to `M_x A y′/ΣA y′² + M_y A x′/ΣA x′²` for a pattern symmetric about a centroidal axis). Bolts on one line cannot resist a moment about that line: that component blocks (rule `collinear`) instead of being dropped; `t_p = t(1 + b/a)`; `u = √((v/V_R)² + (t_p/T_R)²)`.

**Inputs.** `bolts` table (x, y, A; ≤ 200), `F_x`, `F_y`, `e_x`, `e_y`, `T_z`, `N`, `M_x`, `M_y`, `V_R`, `T_R`, optional `a`, `b`.

**Outputs.** `T`, `J`, `v_max`, `t_max`, `u_max`, `governing`, `u_lin`, `governing_lin` (1.3.0); per-bolt rows `b1…bn` in the working; series: bolt pattern. **Checks.** circular interaction on the governing bolt; the linear interaction `v/V_R + t/(1.4 T_R)` of EN 1993-1-8 Table 3.4 at every bolt (`u_lin` is the highest and `governing_lin` names that bolt, which need not be the one that governs the circular form: 3 × 2 grid, F_x 25, F_y −75 kN at (300, 100), N 55, M_x −5, V_R 60, T_R 90: bolt 6 governs the circular form at 0.902 and bolt 3 the linear one at 1.049, a FAIL; before 1.3.0 the linear form was evaluated only at bolt 6 and passed at 0.902); shear alone; tension alone (both at the circular-governing bolt).

**Limitations.** Rigid parts, linear-elastic distribution, no bearing/slip resistance, prying as a simple lever. A single bolt takes direct shear only (the retired `calc.bolt-shear-capacity` case) and blocks under a torque; a moment about an axis all the bolts lie on blocks rather than being dropped.

**Changes in 1.4.0 (mech.bolt-group-04).** `v_max` and `t_max` are now the largest shear and the largest tension over all bolts, each at its own bolt, named by the new outputs `bolt_v` and `bolt_t`; they were the values at the bolt that governs the circular interaction, which can be a different bolt (3 × 2 grid with F_x 20, F_y −80 kN at (250, 30) mm, T_z 2 kN·m, N 30 kN, M_x 15, M_y −8 kN·m: `v_max` 19.226 kN at bolt 2 before, 39.327 kN at bolt 6 now, `t_max` 80 kN at bolt 2; "Shear alone" 0.320 before, 0.655 now). "Shear alone" and "Tension alone" are therefore checked on the most sheared and the most tensioned bolt. The governing bolt and the interaction checks are unchanged.

**Test cases.** `ka-four-bolt-eccentric` (v_max 36.44 kN, u 0.911 — analytic, Confirmed); `ka-moment-tension` (t_max 50 kN — analytic, Confirmed); `ka-unsymmetric-bending` (four bolts at (0,0), (120,0), (60,80), (0,160) mm, M_x 12 kN·m: t_max 62.5 kN, the symmetric-axes form gave 68.18 kN — recomputed in python3, Confirmed).
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
… 3 more step(s) on the sheet
Governing: PASS  Circular shear–tension interaction (governing bolt): u_max = 0.91109 vs u_max ≤ 1  utilisation 0.911
```
<!-- hand-arithmetic:end -->
