# Weld group (elastic line method) (`mech.weld-group@1.3.0`)

**In plain terms.** Several fillet welds in a plane share a load, and if the load acts off-centre the group is also twisted, so the weld ends furthest from the centre carry the most. The worksheet treats each weld as a line, finds the centre of the group, and works out the force per unit length at the most loaded point and the stress in the throat against a strength you choose, and the leg size needed.

## Scope

Calculates the force per unit length at every end of a fillet weld group, the throat stress and the leg size required. It covers one leg size, with in-plane forces and torque, an out-of-plane force and moments.

The sheet shares the group-geometry kernel (`GroupGeometry`) with the bolt group (`mech.bolt-group`).

## Method

- Welds as lines of unit throat: `L_w = Σl`; `x_c = Σ l x_m/L_w`.
- Unit second moments: `I_ux = Σ[l(y_m − y_c)² + l Δy²/12]`; `I_uy` likewise; product `I_uxy = Σ[l(x_m − x_c)(y_m − y_c) + l Δx Δy/12]`; `J_u = I_ux + I_uy`.
- Torsion about the centroid: `T = F_y(e_x − x_c) − F_x(e_y − y_c) + M_z`.
- At each weld end, the direct part is `F/L_w` and the torsional part is `T r/J_u`, perpendicular to `r`.
- The out-of-plane part is `F_z/L_w + [(M_x I_uy − M_y I_uxy) y′ + (M_y I_ux − M_x I_uxy) x′]/(I_ux I_uy − I_uxy²)`. This is the unsymmetric-bending form. It is `M_x y′/I_ux + M_y x′/I_uy` only for a group symmetric about a centroidal axis, and the older symmetric form understated an L-shaped group by 14 to 20 %.
- The resultant `f_r` is the vector sum of the three parts.
- Throat and weld stress: `a = 0.707 s`; `τ = f_r,max/a`; `s_req = f_r,max/(0.707 f_w)`.

A group that lies on one line (a single weld) resists direct shear, in-plane torsion and the moment that bends the line. It does not resist a moment about the line itself. That one component blocks the sheet (rule `collinear`), and the rest is computed.

**Reference diagram.** The worksheet draws the weld group in plan, two lines standing for `welds`. The load point at `e_x`, `e_y` carries `F_x`, `F_y` and `M_z`. A weld section beside it shows the leg `s`. `F_z`, `M_x`, `M_y` and `f_w` are labels. The drawing is not to scale (ADR-0158, DI-08).

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `welds` | Weld lines: a table of x1, y1, x2, y2 (−2000 to 2000 mm, at most 50 rows) | mm | Setup |
| `s` | Fillet leg (3 to 25 mm, usually 3 to 20 mm) | mm | Geometry |
| `f_w` | Design strength of the throat (from EN 1993-1-8 or a user allowable with its source) | MPa | Material |
| `F_x` | In-plane force, x | kN | Loads |
| `F_y` | In-plane force, y | kN | Loads |
| `F_z` | Out-of-plane force (tension positive) | kN | Loads |
| `e_x` | Point of application, x | mm | Loads |
| `e_y` | Point of application, y | mm | Loads |
| `M_x` | Out-of-plane moment about x (tension on +y side) | kN·m | Loads |
| `M_y` | Out-of-plane moment about y (tension on +x side) | kN·m | Loads |
| `M_z` | Additional in-plane torque | kN·m | Loads |

## Assumptions

- The load is static, the weld is elastic and the connected parts are rigid.
- The weld stress varies linearly with distance from the centroid.
- The welds are fillet welds of one leg size, and the throat is 0.707·s.
- The design strength of the throat comes from EN 1993-1-8 (simplified method) or a user allowable with its source.

## Outputs

`L_w`, `x_c`, `y_c`, `I_ux`, `I_uy`, `I_uxy`, `J_u`, `T`, `f_r_max`, `critical`, `a`, `tau`, `s_req` and `u`.

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `weld.force_per_length_max` | `f_r_max` | N/mm | Maximum resultant force per unit length (→ connection-ec3 F_w_Ed) |
| `weld.stress_throat` | `tau` | MPa | Maximum throat stress |
| `weld.leg_required` | `s_req` | mm | Leg size required |

A port keeps its output symbol as an alias, so a link, a pack binding or an inputs file written before the port names still resolves.

## Checks

- Throat stress against design strength: the throat stress `tau` against `f_w`.
- Rules `few-welds` and `collinear` (blocks).

## Limitations

The sheet is elastic and static. It covers fillet welds of one leg size, with rigid connected parts.

## References

- Budynas, *Shigley's Mechanical Engineering Design*, Ch. 9, Tables 9-1 and 9-2, 11th ed.: welds treated as lines, torsional and bending properties.
- Blodgett, *Design of Welded Structures*, §7.4, 1st ed. (1966): treating the weld as a line.
- BS EN 1993-1-8:2005, cl. 4.5.3.3: simplified method for the design resistance of fillet welds (the resistance input).

## Test cases

- `ka-two-vertical` (`J_u` 291 666.7 mm³, `f_r` 721.11 N/mm), `ka-box-torsion` (106.07 N/mm) and `ka-two-horizontal-bending` (502.49 N/mm): Shigley Tables 9-1 and 9-2, closed forms, confirmed.
- `ka-l-group-bending` (150 × 50 L, `M_x` 1 kN·m: `I_uxy` −70 312.5 mm³, `f_r` 1500 N/mm): analytic, recomputed in python3 by solving the moment equilibrium, confirmed.
- Shigley's Ch. 9 numbered examples and Blodgett's worked examples are not pinned, because the owner's copies are needed.

**Changes in 1.3.0 (C-2).** The weld table is in the `Setup` group and the other inputs are in standard groups; every number has a usual range inside its hard range (soft limits); the check shows the stress and the design strength; messages and assumptions are rewritten. No result changes.

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
Governing: PASS  Throat stress against design strength: 67.12 MPa vs 220 MPa  utilisation 0.31  « EN 1993-1-8 cl. 4.5.3.3
```
<!-- hand-arithmetic:end -->
