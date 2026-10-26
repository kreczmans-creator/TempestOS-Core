# S2 — Beam and 2D frame analysis (`struct.frame-2d@1.3.0`)

**In plain terms.** A frame is treated as members joined at nodes, each member a spring that resists stretching and bending. The stiffness method assembles those springs into one set of equations relating the forces at the nodes to how far they move, solves for the movements under each load case, and works back to the forces and moments along every member. Load combinations add the cases with their factors, and the envelope keeps the worst value at each point.

**Scope.** Linear elastic plane frames and continuous beams: pin / roller / fixed / spring supports, member end releases, nodal, point, uniform, trapezoidal and couple loads in cases and user combinations. Reactions, member end forces, N/V/M and deflection along every member with extremes and positions, envelope over combinations.

**Method.** Direct stiffness (`FrameSolver`): 3 DOF per node, Euler–Bernoulli 6×6 member stiffness, releases by static condensation, exact fixed-end forces from Macaulay functions (`MemberLoads`), reverse Cuthill–McKee ordering, banded LDLᵀ (non-positive pivot ⇒ "mechanism" block). Internal functions: V = F_yi + Σloads, M = F_yi x + Σ − M_i (sagging +), EI v from the end state plus the load integrals; extremes at stations plus zero-shear / zero-slope points by bisection, and, for the moment, the values just before and just after every applied couple (the diagram jumps there).

**Inputs.** `E`; tables `nodes` (id, x, y, support, k_x, k_y, k_r), `elements` (id, i, j, A, I, E?, rel_i, rel_j), `loads` (case, kind, target, a, b, w1, w2, F_x, F_y, M, gravity), `combinations` (name, case, factor); `report`; `n_div`. **Outputs.** `R<n>_x/y/m` and, from 1.1.0, their magnitudes `R<n>_x_abs`, `R<n>_m_abs`, `sum_R_x/y`, per member `M_max_e<id>`, `M_min_e<id>`, `V_e<id>`, `N_e<id>`, `d_e<id>` and the greatest moment magnitude `M_abs_e<id>` (≤ 50 members), `M_max`, `x_Mmax`, `M_min`, `V_max`, `N_max`, `N_min`, `d_max`, envelope `*_env`. **Ports (1.1.0).** declared for node 1 and members 1 and 2 (further nodes and members follow the pattern): `R1_x`, `R1_y` (up positive; compression into a column base → S6 `N_Ed`), `R1_m`, `R1_x_abs` (→ S6 `V_Ed`), `R1_m_abs` (→ S6 `M_Ed`), `N_e1` (tension positive → S3 `N_Ed`), `V_e1`, `V_e2` (→ S3 `V_Ed`, S4 `V_Ed`), `M_abs_e1`, `M_abs_e2` (→ S3 `M_y_Ed`), `M_max_e1`, `M_min_e1`, `sum_R_x/y`, `M_max`, `M_min`, `V_max`, `N_max`, `N_min`, `d_max`. The magnitude outputs are snapped to zero below 1e-9 of the largest reaction, so solver noise on an unloaded support does not print as 3e-14. The magnitude outputs exist because the design checks downstream (S3, S6) take shear and moment unsigned, and a reaction's sign follows the load direction. A load-table cell (a `loads` row's `F_y`, `F_x`, `w1`…) can be linked to another calculation's output (a table-cell link, ADR-0168 amendment 2), e.g. S5's `E_d_min` in the steel-support chain. Series: M/V/N/δ per member, geometry. Caps: 2,000 nodes, 4,000 elements, 50 cases, 50 combinations, 20,000 load rows, 100 stations, 20 M banded entries.

A point or couple load row needs its position `a`: a blank `a` blocks (rule `model`, "enter a") instead of placing the load at the start of the member; type 0 for the start. **Test cases.** Roark 8.1 simply supported UDL, cantilever tip load, propped cantilever UDL, concentrated couple at midspan (`ka-ss-couple`: M_max +15, M_min −15 kN·m); pinned portal sway (Hibbeler, equal columns, axially stiff beam); two-span beam with a hinge (statics). All analytic, Confirmed.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Two-span continuous beam, ULS and SLS combinations ([sheet](../../worked-examples/structural/struct.frame-2d/two-span-continuous-beam/sheet.txt)).

```text
n_nodes = 3
n_elements = 2
n_comb = ULS, SLS = 2
comb = ULS = "ULS"
R1_x = Σ member end forces − applied nodal loads = 0 kN
R1_y = Σ member end forces − applied nodal loads = 64.8 kN
R1_m = Σ member end moments − applied nodal couple = 0 kN·m
R1_x_abs = |0| kN = 0 kN
R1_m_abs = |0| kN·m = 0 kN·m
R2_x = Σ member end forces − applied nodal loads = 0 kN
R2_y = Σ member end forces − applied nodal loads = 216 kN
R2_m = Σ member end moments − applied nodal couple = 0 kN·m
R2_x_abs = |0| kN = 0 kN
R2_m_abs = |0| kN·m = 0 kN·m
… 30 more step(s) on the sheet
```
<!-- hand-arithmetic:end -->
