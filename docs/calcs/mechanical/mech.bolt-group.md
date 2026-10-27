# Bolt group (elastic method) (`mech.bolt-group@1.5.0`)

**In plain terms.** When a load acts away from the centre of a bolt group, each bolt carries an equal share of the load directly plus a share of the twisting moment that grows with its distance from the centre. Adding the two as vectors gives the most heavily loaded bolt, which is then checked in shear, in tension where the load also pulls, and for the two acting together.

## Scope

Calculates the shear and tension in every bolt of a planar group under eccentric in-plane forces, torque, axial load and out-of-plane moments, by the elastic method. It also finds the governing bolt and an optional lever-model prying addition.

## Method

- Centroid of the bolt group, each bolt weighted by its area: x_c = Σ A x / Σ A (y_c likewise).
- Torsion about the centroid from the eccentric load: T = F_y (e_x − x_c) − F_x (e_y − y_c) + M_z.
- Polar moment of the bolt areas about the centroid: J = Σ A r².
- Shear on each bolt, its direct share plus the torsion (vector sum): v = A (F/ΣA) + T·A·r/J.
- Tension on each bolt, its direct share plus the moments, with the product of inertia so that an unsymmetric pattern is right: t = A N/ΣA + A·[(M_x I_y − M_y I_xy) y' + (M_y I_x − M_x I_xy) x']/(I_x I_y − I_xy²), where I_x = Σ A y'², I_y = Σ A x'², I_xy = Σ A x'y' (this reduces to M_x A y'/Σ A y'² + M_y A x'/Σ A x'² only for a pattern symmetric about a centroidal axis).
- Tension with prying, when the distances a and b are given: t_p = t (1 + b/a).
- Combined shear and tension, circular interaction (elastic method, Shigley-style): (v/F_v_Rd)² + (t_p/F_t_Rd)² ≤ 1.
- Combined shear and tension, EN 1993-1-8 linear form (each is also checked alone; the highest governs): v/F_v_Rd + t_p/(1.4 F_t_Rd) ≤ 1.

The shear on a bolt is `v = A F/ΣA ⊕ T A r/J` (a vector sum). The tension uses the product of inertia, so an unsymmetric pattern is right. It reduces to `M_x A y′/ΣA y′² + M_y A x′/ΣA x′²` only for a pattern symmetric about a centroidal axis. Bolts on one line cannot resist a moment about that line: that component blocks (rule `collinear`) instead of being dropped. The resistances are inputs with their source; EN 1993-1-8 cl. 3.6 and 3.8 and the AISC manual are named for the method only. A single bolt takes direct shear only (the retired `calc.bolt-shear-capacity` case) and blocks under a torque.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `bolts` | Bolt positions and areas |  |  |  | Setup |
| `bolts.x` | x (column) | mm |  | | Setup |
| `bolts.y` | y (column) | mm |  | | Setup |
| `bolts.A` | Shear/stress area (column) | mm² |  | | Setup |
| `F_x` | In-plane force, x | kN | 0 | -100000 to 100000 | Loads |
| `F_y` | In-plane force, y | kN | -50 | -100000 to 100000 | Loads |
| `e_x` | Point of application, x | mm | 150 | -10000 to 10000 | Loads |
| `e_y` | Point of application, y | mm | 0 | -10000 to 10000 | Loads |
| `M_z` (was `T_z`) | Additional in-plane torque about the centroid | kN·m | 0 | -100000 to 100000 | Loads |
| `N` | Axial (out-of-plane) load, tension positive | kN | 0 | -100000 to 100000 | Loads |
| `M_x` | Out-of-plane moment about x (tension on +y side) | kN·m | 0 | -100000 to 100000 | Loads |
| `M_y` | Out-of-plane moment about y (tension on +x side) | kN·m | 0 | -100000 to 100000 | Loads |
| `F_v_Rd` (was `V_R`) | Shear resistance per bolt | kN | 30 | 0.1 to 10000 | Resistance |
| `F_t_Rd` (was `T_R`) | Tension resistance per bolt | kN | 45 | 0.1 to 10000 | Resistance |
| `a` | Prying lever: bolt to flange edge (optional; blank = no prying) | mm | optional | 1 to 1000 | Prying |
| `b` | Prying lever: bolt to the loaded line (optional; blank = no prying) | mm | optional | 0 to 1000 | Prying |

## Assumptions

1. The connected parts are rigid, and bolt forces vary linearly with distance from the centroid. Basis: The elastic method is conservative compared with plastic and instantaneous-centre methods.
2. Resistances are per bolt as entered, with their source (a design-code capacity or a manufacturer value). Basis: No code capacity is computed here.
3. Prying uses a simple lever: the bolt tension rises by the factor (1 + b/a). Basis: An optional, conservative first estimate.

## Outputs

| Symbol | Description |
|---|---|
| `T` | Torque about the centroid |
| `J` | Polar second moment of bolt areas |
| `F_v_Ed_max` | Maximum shear on any bolt |
| `F_t_Ed_max` | Maximum tension on any bolt |
| `bolt_v` | Bolt with the largest shear |
| `bolt_t` | Bolt with the largest tension |
| `u_max` | Governing interaction utilisation |
| `governing` | Governing bolt |
| `u_lin` | Linear interaction utilisation (highest over all bolts) |
| `governing_lin` | Bolt governing the linear interaction |

The port `boltgroup.shear_max` is `F_v_Ed_max` (it was `v_max`, a symbol the standard keeps for a velocity limit) and `boltgroup.tension_max` is `F_t_Ed_max` (it was `t_max`). The old symbols are aliases. `bolt_v` and `bolt_t` name the bolt that carries the largest shear and the largest tension. `governing` names the bolt with the highest circular interaction, and `governing_lin` the bolt with the highest linear interaction. The per-bolt rows `b1` to `bn` are in the working, and the series is the bolt pattern.

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `boltgroup.shear_max` | `F_v_Ed_max` (also `v_max`) | kN | Largest shear on any bolt |
| `boltgroup.tension_max` | `F_t_Ed_max` (also `t_max`) | kN | Largest tension on any bolt, with prying |
| `boltgroup.torque_centroid` | `T` | kN·m | In-plane torque about the centroid of the bolts |
| `boltgroup.utilisation_governing` | `u_max` |  | Governing interaction utilisation |

## Checks

- Circular shear–tension interaction against its limit, at the governing bolt (`u_max` against `u_lim` = 1).
- Linear shear–tension interaction against its limit, at every bolt (`u_lin` against 1, EN 1993-1-8 Table 3.4); `governing_lin` names that bolt, which need not be the one that governs the circular form (3 × 2 grid, F_x 25, F_y −75 kN at (300, 100), N 55, M_x −5, F_v_Rd 60, F_t_Rd 90: bolt 6 governs the circular form at 0.902 and bolt 3 the linear one at 1.049, a FAIL; before 1.3.0 the linear form was evaluated only at bolt 6 and passed at 0.902).
- Largest bolt shear against shear resistance (`F_v_Ed_max` against `F_v_Rd`).
- Largest bolt tension against tension resistance (`F_t_Ed_max` against `F_t_Rd`).

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `few-bolts` | Block | Conservative | No bolts, or a single bolt under an in-plane torque it cannot resist. |
| `coincident` | Block | Conservative | Two bolts share a position. |
| `collinear` | Block | Conservative | All bolts on one line: an out-of-plane moment about that line cannot be resisted. |

## Limitations

Rigid parts, a linear-elastic distribution, no bearing or slip resistance, and prying as a simple lever. A moment about an axis that all the bolts lie on blocks rather than being dropped.

## Changes

**1.5.0 (C-2, phase B).** Three inputs and two outputs are renamed, and the old symbols stay as aliases (an inputs file, a CSV, a saved draft, a pack example, a pack binding and a link that use them still load): `T_z` is `M_z` (the additional in-plane torque), `V_R` is `F_v_Rd`, `T_R` is `F_t_Rd`, `v_max` is `F_v_Ed_max` and `t_max` is `F_t_Ed_max`. The inputs are grouped (`Setup`, `Loads`, `Resistance`, `Prying`) and have usual ranges. The sheet declares four ports. The four checks show a value against a value (the two interactions against their limit of 1, the shear and the tension against their resistances), and the shear and tension utilisations are the same numbers as before. The scope, the assumption, the messages and the references follow the standard (BS EN 1993-1-8:2005; the AISC manual with its authors and edition). No calculated value changes.

**1.4.0 (mech.bolt-group-04).** `F_v_Ed_max` and `F_t_Ed_max` (then `v_max` and `t_max`) are the largest shear and the largest tension over all bolts, each at its own bolt, named by the outputs `bolt_v` and `bolt_t`; they were the values at the bolt that governs the circular interaction, which can be a different bolt (3 × 2 grid with F_x 20, F_y −80 kN at (250, 30) mm, M_z 2 kN·m, N 30 kN, M_x 15, M_y −8 kN·m: shear 19.226 kN at bolt 2 before, 39.327 kN at bolt 6 now, tension 80 kN at bolt 2; "shear alone" 0.320 before, 0.655 now). "Shear alone" and "tension alone" are therefore checked on the most sheared and the most tensioned bolt. The governing bolt and the interaction checks are unchanged.

## References

- BS EN 1993-1-8:2005, cl. 3.6, 3.8: Design resistance of individual fasteners; elastic distribution in a bolt group.
- American Institute of Steel Construction, Steel Construction Manual, §7, 15th ed.: Eccentrically loaded bolt groups — elastic method.

## Test cases

`ka-four-bolt-eccentric` (`F_v_Ed_max` 36.44 kN, u 0.911, analytic, Confirmed); `ka-moment-tension` (`F_t_Ed_max` 50 kN, analytic, Confirmed); `ka-unsymmetric-bending` (four bolts at (0,0), (120,0), (60,80), (0,160) mm, M_x 12 kN·m: tension 62.5 kN, where the symmetric-axes form gave 68.18 kN, recomputed in python3, Confirmed); the four legacy cases `ka-legacy-four-bolts`, `ka-legacy-line`, `ka-legacy-inclined` and `ka-legacy-centroid`, and the single-bolt case `ka-legacy-single-bolt`.

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
F_v_Ed_max = bolt 2 = 36.443 kN
F_t_Ed_max = bolt 1 = 0 kN
bolt_v = bolt 2 = "bolt 2"
bolt_t = bolt 1 = "bolt 1"
u_max = √((36.443/40)² + (0/45)²) = 0.91109
… 6 more step(s) on the sheet
Governing: PASS  Circular shear–tension interaction against its limit (governing bolt): 0.91109 vs 1  utilisation 0.91
```
<!-- hand-arithmetic:end -->
