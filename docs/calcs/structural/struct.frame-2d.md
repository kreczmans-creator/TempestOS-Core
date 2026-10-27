# Beam and 2D frame analysis (`struct.frame-2d@1.6.0`)

**In plain terms.** A frame is treated as members joined at nodes, each member a spring that resists stretching and bending. The stiffness method assembles those springs into one set of equations relating the forces at the nodes to how far they move, solves for the movements under each load case, and works back to the forces and moments along every member. Load combinations add the cases with their factors, and the envelope keeps the worst value at each point.

## Scope

Calculates reactions, member forces and deflections of a linear elastic plane frame or continuous beam by the direct stiffness method. Global x is right and y up; bending is about the section y-y axis, anticlockwise positive.

## Method

- Member stiffness: three freedoms per node, Euler–Bernoulli members with axial EA/L and bending EI/L³ terms.
- End releases: pins are taken out by static condensation; supports remove freedoms or add spring stiffness.
- Span loads: they become equivalent nodal loads, the exact fixed-end forces.
- Nodal displacements: K u = F, solved by banded LDLᵀ after a reverse Cuthill–McKee ordering; a non-positive pivot is reported as a mechanism.
- Internal forces: they follow from the member end actions and the Macaulay functions of the span loads.
- Member extremes: found at stations along each member, plus the zero-shear and zero-slope points found by bisection.

Direct stiffness (`FrameSolver`): 3 DOF per node, Euler–Bernoulli 6×6 member stiffness, releases by static condensation, exact fixed-end forces from Macaulay functions (`MemberLoads`), reverse Cuthill–McKee ordering, banded LDLᵀ (non-positive pivot ⇒ "mechanism" block). Internal functions: V = F_yi + Σloads, M = F_yi x + Σ − M_i (sagging +), EI v from the end state plus the load integrals; extremes at stations plus zero-shear / zero-slope points by bisection, and, for the moment, the values just before and just after every applied couple (the diagram jumps there).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `nodes` | Nodes | — | — | table | Setup | — |
| `elements` | Elements | — | — | table | Setup | — |
| `loads` | Loads | — | — | table | Setup | — |
| `n_div` | Stations per member | — | 20 | 4 to 100 | Setup | — |
| `combinations` | Load combinations (optional; blank = each case alone) | — | blank | table | Setup | — |
| `report` | Combination to report in detail (optional; blank = the first) | — | blank | — | Setup | — |
| `E` | Default Young's modulus | GPa | 210 | 1 to 500 | Material | — |

Columns of `nodes`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `id` | Node | — | yes |
| `x` | x | m | yes |
| `y` | y | m | yes |
| `support` | Support (free, pin, roller (vertical), roller (horizontal), fixed, spring) | — | no |
| `k_x` | Spring k_x | kN/m | no |
| `k_y` | Spring k_y | kN/m | no |
| `k_r` | Spring k_r (per radian) | kN·m | no |

Columns of `elements`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `id` | Element | — | yes |
| `i` | Node i | — | yes |
| `j` | Node j | — | yes |
| `A` | Area A | cm² | yes |
| `I` | Second moment of area about the section y-y axis | cm⁴ | yes |
| `E` | Young's modulus (blank = sheet default) | GPa | no |
| `rel_i` | Moment release at i | — | no |
| `rel_j` | Moment release at j | — | no |

Columns of `loads`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `case` | Load case | — | yes |
| `kind` | Kind (nodal, point, udl, trapezoid, moment) | — | yes |
| `target` | Node (nodal) or element | — | yes |
| `a` | a from end i | m | no |
| `b` | b from end i (distributed) | m | no |
| `w1` | w at a (+ towards local y, i.e. left of i→j; gravity rows: + downward) | kN/m | no |
| `w2` | w at b (trapezoid) | kN/m | no |
| `F_x` | F_x (global) | kN | no |
| `F_y` | F_y (global) | kN | no |
| `M` | Couple M (anticlockwise) | kN·m | no |
| `gravity` | Gravity load: w positive downward, per metre of member | — | no |

Columns of `combinations`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `name` | Combination | — | yes |
| `case` | Load case | — | yes |
| `factor` | Factor | — | yes |

## Assumptions

1. The analysis is linear elastic and first order, with small displacements. Basis: Euler–Bernoulli direct stiffness.
2. Members have no shear deformation, and there are no P–Δ effects or buckling. Basis: Euler–Bernoulli direct stiffness.
3. Members are prismatic between nodes and connect rigidly unless a release is stated. Basis: Static condensation of the released rotation.
4. A released member end transmits no moment. Basis: Static condensation of the released rotation.
5. Distributed loads act perpendicular to the member unless marked as gravity loads. Basis: Load table convention.
6. Gravity loads act in global −y per metre of member length. Basis: Load table convention.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `R1_x` | R_x node 1 | kN |
| `R1_y` | R_y node 1 | kN |
| `R1_m` | M node 1 | kN·m |
| `R1_x_abs` | \|R_x\| node 1 | kN |
| `R1_m_abs` | \|M\| node 1 | kN·m |
| `R2_x` | R_x node 2 | kN |
| `R2_y` | R_y node 2 | kN |
| `R2_m` | M node 2 | kN·m |
| `R2_x_abs` | \|R_x\| node 2 | kN |
| `R2_m_abs` | \|M\| node 2 | kN·m |
| `sum_R_y` | Σ R_y | kN |
| `sum_R_x` | Σ R_x | kN |
| `M_max_e1` | M max, member 1 | kN·m |
| `M_min_e1` | M min, member 1 | kN·m |
| `M_abs_e1` | \|M\| max, member 1 | kN·m |
| `V_e1` | \|V\| max, member 1 | kN |
| `N_e1` | N, member 1 | kN |
| `d_e1` | δ max, member 1 | mm |
| `M_max` | M max | kN·m |
| `x_Mmax` | x of M max | m |
| `M_min` | M min | kN·m |
| `V_max` | \|V\| max | kN |
| `N_max` | N max (tension) | kN |
| `N_min` | N min (compression) | kN |
| `d_max` | δ max | mm |

## Ports

The ports are declared for node 1 and members 1 and 2; further nodes and members follow the pattern (`R<n>_x`, `N_e<e>`). Old output symbols stay as the ports' symbols.

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `frame.reaction_x_node1` | `R1_x` | Horizontal reaction at node 1, signed (node n follows the same pattern, R<n>_x) | kN | — |
| `frame.reaction_y_node1` | `R1_y` | Vertical reaction at node 1, up positive: compression into a column base (→ base-plate N_Ed) | kN | — |
| `frame.reaction_moment_node1` | `R1_m` | Moment reaction at node 1, signed | kN·m | — |
| `frame.reaction_x_magnitude_node1` | `R1_x_abs` | Magnitude of the horizontal reaction at node 1 (→ base-plate V_Ed) | kN | — |
| `frame.reaction_moment_magnitude_node1` | `R1_m_abs` | Magnitude of the moment reaction at node 1 (→ base-plate M_Ed) | kN·m | — |
| `frame.axial_force_member1` | `N_e1` | Axial force of greatest magnitude in member 1, tension positive (→ en1993-member N_Ed; member e follows the same pattern, N_e<e>) | kN | — |
| `frame.shear_member1` | `V_e1` | Greatest shear magnitude in member 1 (→ en1993-member V_Ed, connection-ec3 V_Ed) | kN | — |
| `frame.moment_magnitude_member1` | `M_abs_e1` | Greatest bending-moment magnitude in member 1, sagging or hogging (→ en1993-member M_y_Ed) | kN·m | — |
| `frame.moment_max_member1` | `M_max_e1` | Maximum sagging moment in member 1 | kN·m | — |
| `frame.moment_min_member1` | `M_min_e1` | Minimum (hogging) moment in member 1 | kN·m | — |
| `frame.shear_member2` | `V_e2` | Greatest shear magnitude in member 2 (V_e<e> for any member) | kN | — |
| `frame.moment_magnitude_member2` | `M_abs_e2` | Greatest bending-moment magnitude in member 2 (M_abs_e<e> for any member) | kN·m | — |
| `frame.reaction_y_total` | `sum_R_y` | Total vertical reaction | kN | — |
| `frame.reaction_x_total` | `sum_R_x` | Total horizontal reaction | kN | — |
| `frame.moment_max` | `M_max` | Maximum sagging moment, reported combination | kN·m | — |
| `frame.moment_min` | `M_min` | Minimum (hogging) moment, reported combination | kN·m | — |
| `frame.shear_max` | `V_max` | Greatest shear magnitude, reported combination | kN | — |
| `frame.axial_force_max` | `N_max` | Maximum axial tension, reported combination | kN | — |
| `frame.axial_force_min` | `N_min` | Maximum axial compression (negative), reported combination | kN | — |
| `frame.deflection_max` | `d_max` | Maximum transverse deflection, reported combination | mm | — |

## Checks

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `model` | Block | Conservative | The model is incomplete or inconsistent (a missing node, a zero-length member, a load off the member). |
| `report-name` | Warning | Conservative | The combination named in report does not exist: the first combination is reported instead. |
| `mechanism` | Block | Conservative | The structure is a mechanism: a non-positive stiffness pivot. |
| `size` | Block | Conservative | The model exceeds a cap (1,200 nodes, 2,400 elements, 2,400 load rows, 50 cases, 200 load rows per member, factorisation work); a table past its row cap is refused at entry. |
| `summary-only` | Warning | Conservative | More than 50 members or supports: per-member and per-support lines are omitted from the sheet; the extremes and envelope remain. |
| `large-deflection` | Warning | Unconservative | A deflection exceeds 1/50 of its member length: the small-displacement assumption is doubtful. |

## Limitations

The caps are 1,200 nodes, 2,400 elements, 2,400 load rows, 50 cases, 50 combinations (1,000 combination rows), 100 stations and 20 million banded entries. A table past its row cap is refused at entry by the input validator, in a sentence that names the table and the cap (`nodes: enter at most 1200 rows; 1201 were given.`), before the model is built. With more than 50 members or supports the sheet omits the per-member and per-support lines (rule `summary-only`).

**Why these caps (DC-15, DC-21).** A calculation is stored as one document of at most 4 MB (`CalcInstanceStore.MaxDocumentBytes`, 4,000,000 bytes), and the frame at the old caps (2,000 nodes, 4,000 elements, 20,000 loads) could not be saved: the C-7 soak's grid frame at those caps was refused at about 7.3 MB. The caps are now what the document holds with margin, computed from the stored size of a row. The store writes a table row as JSON with every filled cell as `{"Number":…,"Unit":"…"}`, `{"Text":"…"}` or `{"Flag":…}`; the longest number the serialiser writes is 24 characters (17 significant figures, a sign, a point and an exponent). With every column filled a node row (`id`, `x`, `y`, `support`, `k_x`, `k_y`, `k_r`) stores in 364 bytes, an element row (`id`, `i`, `j`, `A`, `I`, `E`, `rel_i`, `rel_j`) in 340 and a load row (`case`, `kind`, `target`, `a`, `b`, `w1`, `w2`, `F_x`, `F_y`, `M`, `gravity`) in 526. Tables at the caps: 1,200 × 364 + 2,400 × 340 + 2,400 × 526 = 436,800 + 816,000 + 1,262,400 = 2,515,200 bytes; the combinations table at its 1,000 rows adds 159,000; a `large-deflection` warning on every member adds 2,400 × 190 = 456,000; the detailed outputs (50 members and 50 supports) add 567 × 140 = 79,380; the header, the provenance and the checks a few kilobytes. Total about 3.21 MB, 80 % of the cap, so a frame within the caps always saves when its cells carry no provenance of their own, with a fifth of the cap to spare for links and notes; a table whose every cell carries its own scan-import provenance (a reference of up to 200 characters and a revision on each cell, about 290 bytes) can pass 4 MB at the caps, and the store then refuses it by name with its typed refusal, without a crash; a frame of ordinary numbers (`12.5`) is about 1.4 MB. The Library test `FrameSizeCapTests` serialises that worst-case frame and holds it under 85 % of the cap; `FrameTests` holds the caps and the refusal at entry.


A point or couple load row needs its position `a`: a blank `a` blocks (rule `model`) instead of placing the load at the start of the member. Type 0 for the start.

The magnitude outputs are set to zero below 1e-9 of the largest reaction, so solver noise on an unloaded support does not print as 3e-14. They exist because the design checks downstream take shear and moment unsigned, and a reaction's sign follows the load direction. A load-table cell can be linked to another calculation's output (a table-cell link), for example the governing effect of the load combinations sheet in the steel-support pack.

### Changes by version

**1.6.0 (DC-15 frame caps, owner decision DC-21).** The table caps are lowered from 2,000 nodes, 4,000 elements and 20,000 load rows to 1,200, 2,400 and 2,400, so that the largest frame within the caps stores under the 4 MB document cap with margin (the arithmetic is under Limitations), and a table past its cap is refused at entry by the input validator, naming the table and the cap. The other caps (50 cases, 50 combinations, 200 load rows per member, 100 stations, the factorisation work and the banded entries) do not change. Every shipped example, known-answer case and test frame is within the new caps. No calculated value changes.

**1.5.0 (C-2 consistency).** The sheet gives its ports public names `frame.<quantity>`; the output symbols are unchanged, so links and pack bindings that name them keep working. Port descriptions name their target sheets by template id and input. The inputs are grouped (the model tables and the analysis settings in Setup, the default Young's modulus in Material). The new rule `report-name` carries the warning for an unknown combination in `report`, which used the blocking rule `model`. The scope, method, assumptions and references follow the sheet standard. No calculated value changes.

**Changes in 1.4.0.** A load row with no magnitude is blocked instead of running as zero: a `udl` row with `w1` blank, a `trapezoid` row with `w1` or `w2` blank, a `moment` row with `M` blank, a `point` row with both `F_x` and `F_y` blank, and a `nodal` row with `F_x`, `F_y` and `M` all blank (a typed 0 is accepted; a trapezoid with `w2` blank used to become a triangle carrying half the load). The large-deflection warning (a deflection above 1/50 of the length) compares the deflection with the length of the straight chain of members joined end to end through unsupported two-member nodes, so a long beam cut into short elements no longer gets one false warning per element; a single member is still judged against its own length. The greatest-moment port `M_abs_e<n>` already reports the full jump of a couple at a member end (1.3.0).

## References

- Hibbeler, Structural Analysis, Ch. 16, 9th ed.: Plane frame analysis using the stiffness method.
- Young and Budynas, Roark's Formulas for Stress and Strain, Table 8.1, 8th ed.: Shear, moment, slope and deflection formulas for elastic straight beams.
- BS EN 1990:2002+A1:2005, cl. 6.4.3: Combinations of actions (user-entered factors).

## Test cases

- `ka-ss-udl`: Simply supported beam, UDL (Roark Table 8.1 case 2e). Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-cantilever-point`: Cantilever, point load at the tip (Roark Table 8.1 case 1a). Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-propped-cantilever`: Propped cantilever, UDL (Roark Table 8.1 case 2c). Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-portal-sway`: Pinned-base portal frame with a lateral load (Hibbeler, portal frame symmetry). Source: Hibbeler, Structural Analysis, 9th ed..
- `ka-two-span-release`: Two-span beam with a hinge (release) at the interior support side, point load. Source: Statics (hinge makes the right span simply supported).
- `ka-ss-couple`: Simply supported beam, concentrated couple at midspan (the moment diagram jumps). Source: Statics (closed form).

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
