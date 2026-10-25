# M6 — Weld group, elastic line method (`mech.weld-group@1.1.1`)

**In plain terms.** Several fillet welds in a plane share a load, and if the load acts off-centre the group is also twisted, so the weld ends furthest from the centre carry the most. The worksheet treats each weld as a line, finds the centre of the group, and works out the force per unit length at the most loaded point and the stress in the throat against a strength you choose, and the leg size needed.

**Scope.** Straight fillet welds in a plane under in-plane forces with eccentricity, an out-of-plane force and out-of-plane moments: force per unit length at every weld end, critical point, throat stress against a design strength, leg required. Shares the group-geometry kernel (`GroupGeometry`) with M3 bolt group.

**Method.** Unit throat lines: `L_w = Σl`; `x_c = Σ l x_m/L_w`; `I_ux = Σ[l(y_m − y_c)² + l Δy²/12]`, `I_uy` likewise, product `I_uxy = Σ[l(x_m − x_c)(y_m − y_c) + l Δx Δy/12]`; `J_u = I_ux + I_uy` (Shigley 11th ed. Tables 9-1, 9-2; Blodgett 1966 §7.4). `T = F_y(e_x − x_c) − F_x(e_y − y_c) + M_z`. At each end: `F/L_w` + `T r/J_u` ⊥ r + `F_z/L_w + [(M_x I_uy − M_y I_uxy) y′ + (M_y I_ux − M_x I_uxy) x′]/(I_ux I_uy − I_uxy²)` (the unsymmetric-bending form; it is `M_x y′/I_ux + M_y x′/I_uy` only for a group symmetric about a centroidal axis, and the older symmetric form understated an L-shaped group by 14–20 %); `f_r` the vector sum. A group that lies on one line (a single weld) resists direct shear, in-plane torsion and the moment that bends the line, but not a moment about the line itself: that one component blocks (rule `collinear`), the rest is computed. `a = 0.707 s`, `τ = f_r,max/a`, `s_req = f_r,max/(0.707 f_w)`.

**Inputs.** `welds` table (x1, y1, x2, y2 −2000…2000 mm; ≤ 50), `s` 3–25 mm, `f_w` (from S4 or a user allowable with source), `F_x`, `F_y`, `F_z` kN, `e_x`, `e_y`, `M_x`, `M_y`, `M_z` kN·m.

**Outputs.** `L_w`, `x_c`, `y_c`, `I_ux`, `I_uy`, `I_uxy`, `J_u`, `T`, `f_r_max`, `critical`, `a`, `tau`, `s_req`, `u`. **Ports.** `f_r_max` → S4, `tau`, `s_req`. **Check.** throat stress against design strength.

**Limitations.** Elastic, static, fillet welds of one leg size; rigid parts.

**Test cases.** `ka-two-vertical` (J_u 291 666.7 mm³, f_r 721.11 N/mm), `ka-box-torsion` (106.07 N/mm), `ka-two-horizontal-bending` (502.49 N/mm) — Shigley Table 9-1/9-2 closed forms, Confirmed; `ka-l-group-bending` (150 × 50 L, M_x 1 kN·m: I_uxy −70 312.5 mm³, f_r 1500 N/mm, recomputed in python3 by solving the moment equilibrium) — analytic, Confirmed. Shigley Ch. 9 numbered examples and Blodgett's worked examples are not pinned (owner copies needed).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Bracket welded to a column flange on three sides ([sheet](../../worked-examples/mechanical/mech.weld-group/bracket-c-weld/sheet.txt)).

```text
L_w = 80 + 200 + 80 = 360 mm
x_c = 17.778 mm
y_c = 100 mm
I_ux = about the centroid = 2266700 mm³
I_uy = about the centroid = 227560 mm³
I_uxy = about the centroid = 0 mm³
J_u = 2266700 + 227560 = 2494200 mm³
T = -30 × 232.22 − 0 × 0 + 0e3 = -6.9667 kN·m
f_r_max = at (80, 0) mm: (-279.31, -257.13, 0) N/mm = 379.64 N/mm
critical = (80, 0) mm = "(80, 0) mm"
a = 0.707 × 8 mm = 5.656 mm
tau = 379.64 N/mm / 5.656 mm = 67.122 MPa
s_req = 379.64 N/mm / (0.707 × 220 MPa) = 2.4408 mm
u = 67.122 MPa / 220 MPa = 0.3051
Governing: PASS  Throat stress against design strength: u = 0.3051 vs u ≤ 1  utilisation 0.305  « EN 1993-1-8 cl. 4.5.3.3
```
<!-- hand-arithmetic:end -->
