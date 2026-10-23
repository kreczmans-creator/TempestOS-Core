# S2 — Beam and 2D frame analysis (`struct.frame-2d@1.0.0`)

**Scope.** Linear elastic plane frames and continuous beams: pin / roller / fixed / spring supports, member end releases, nodal, point, uniform, trapezoidal and couple loads in cases and user combinations. Reactions, member end forces, N/V/M and deflection along every member with extremes and positions, envelope over combinations.

**Method.** Direct stiffness (`FrameSolver`): 3 DOF per node, Euler–Bernoulli 6×6 member stiffness, releases by static condensation, exact fixed-end forces from Macaulay functions (`MemberLoads`), reverse Cuthill–McKee ordering, banded LDLᵀ (non-positive pivot ⇒ "mechanism" block). Internal functions: V = F_yi + Σloads, M = F_yi x + Σ − M_i (sagging +), EI v from the end state plus the load integrals; extremes at stations plus zero-shear / zero-slope points by bisection.

**Inputs.** `E`; tables `nodes` (id, x, y, support, k_x, k_y, k_r), `elements` (id, i, j, A, I, E?, rel_i, rel_j), `loads` (case, kind, target, a, b, w1, w2, F_x, F_y, M, gravity), `combinations` (name, case, factor); `report`; `n_div`. **Outputs.** `R<n>_x/y/m`, `sum_R_x/y`, per member `M_max_e<id>`, `M_min_e<id>`, `V_e<id>`, `N_e<id>`, `d_e<id>` (≤ 50 members), `M_max`, `x_Mmax`, `M_min`, `V_max`, `N_max`, `N_min`, `d_max`, envelope `*_env`. Series: M/V/N/δ per member, geometry. Caps: 2,000 nodes, 4,000 elements, 50 cases, 50 combinations, 20,000 load rows, 100 stations, 20 M banded entries.

**Test cases.** Roark 8.1 simply supported UDL, cantilever tip load, propped cantilever UDL; pinned portal sway (Hibbeler, equal columns, axially stiff beam); two-span beam with a hinge (statics). All analytic, Confirmed.
