# S1 — Section properties (`struct.section-properties@1.0.0`)

**Scope.** Area, centroid, `I_xx`, `I_yy`, `I_xy`, principal values and angle, elastic and plastic moduli, radii of gyration, torsion constant and (I-sections) warping constant for: rectangle, circle, tube, hollow rectangle (EN 10210-2 corner idealisation `r_o = 1.5t`, `r_i = 1.0t` by default), I, channel, tee, angle (root fillets), an arbitrary polygon with holes, or a composite of placed primitives (add/subtract, rotation).

**Method.** Outline loops integrated by Green's theorem (`PolygonSection`); parallel-axis transfer to the centroid; principal axes from `tan 2θ = −2I_xy/(I_xx − I_yy)`; plastic moduli by bisection on the equal-area axis then `|Q_above| + |Q_below|`. Circle and tube in closed form. Torsion: Saint-Venant series (solid rectangle), `πd⁴/32` (round), Bredt `4A_m²t/p_m + p_m t³/3` (closed thin-walled, warned), `Σbt³/3` (open, warned) plus the El Darwish–Johnston fillet term for rolled I (AISC DG9). Warping `I_w = I_yy (h − t_f)²/4` (I only). Polygons and composites report no `J` (warning).

**Inputs.** `shape`; `h`, `b`, `t`, `t_f`, `d`, `r`, `r_i` as the shape needs; `vertices` table (`x`, `y`, `loop`); `parts` table (`shape`, dims, `x0`, `y0`, `rot`, `sign`). **Outputs.** `A`, `x_c`, `y_c`, `I_xx`, `I_yy`, `I_xy`, `I_1`, `I_2`, `theta`, `W_el_x`, `W_el_y`, `W_pl_x`, `W_pl_y`, `i_x`, `i_y`, `alpha_pl_x`, `J`, `I_w`. **Checks.** none (a properties sheet). Caps: 2,000 vertex rows, 100 parts, 200 loops, 20,000 vertices per loop.

**Test cases.** UB 457×191×98 and hot-finished SHS 100×100×5 against public Blue Book values (Confirmed, within 0.5 %; J within 1 %); rectangle and tube analytic; unequal angle as a polygon against the parallel-axis theorem.
