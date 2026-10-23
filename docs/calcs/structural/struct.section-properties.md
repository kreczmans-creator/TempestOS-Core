# S1 — Section properties (`struct.section-properties@1.0.0`)

**In plain terms.** Every bending, stress and buckling check needs the shape's geometric properties: its area, where its centroid is, how its area is spread about each axis (second moments), and the section moduli that turn a bending moment into a stress. This worksheet computes them from the outline itself, adding up the contribution of each edge of the polygon, so any shape, with holes or built from plates, gives the same properties a handbook table would for a standard section.

**Scope.** Area, centroid, `I_xx`, `I_yy`, `I_xy`, principal values and angle, elastic and plastic moduli, radii of gyration, torsion constant and (I-sections) warping constant for: rectangle, circle, tube, hollow rectangle (EN 10210-2 corner idealisation `r_o = 1.5t`, `r_i = 1.0t` by default), I, channel, tee, angle (root fillets), an arbitrary polygon with holes, or a composite of placed primitives (add/subtract, rotation).

**Method.** Outline loops integrated by Green's theorem (`PolygonSection`); parallel-axis transfer to the centroid; principal axes from `tan 2θ = −2I_xy/(I_xx − I_yy)`; plastic moduli by bisection on the equal-area axis then `|Q_above| + |Q_below|`. Circle and tube in closed form. Torsion: Saint-Venant series (solid rectangle), `πd⁴/32` (round), Bredt `4A_m²t/p_m + p_m t³/3` (closed thin-walled, warned), `Σbt³/3` (open, warned) plus the El Darwish–Johnston fillet term for rolled I (AISC DG9). Warping `I_w = I_yy (h − t_f)²/4` (I only). Polygons and composites report no `J` (warning).

**Inputs.** `shape`; `h`, `b`, `t`, `t_f`, `d`, `r`, `r_i` as the shape needs; `vertices` table (`x`, `y`, `loop`); `parts` table (`shape`, dims, `x0`, `y0`, `rot`, `sign`). **Outputs.** `A`, `x_c`, `y_c`, `I_xx`, `I_yy`, `I_xy`, `I_1`, `I_2`, `theta`, `W_el_x`, `W_el_y`, `W_pl_x`, `W_pl_y`, `i_x`, `i_y`, `alpha_pl_x`, `J`, `I_w`. **Checks.** none (a properties sheet). Caps: 2,000 vertex rows, 100 parts, 200 loops, 20,000 vertices per loop.

**Test cases.** UB 457×191×98 and hot-finished SHS 100×100×5 against public Blue Book values (Confirmed, within 0.5 %; J within 1 %); rectangle and tube analytic; unequal angle as a polygon against the parallel-axis theorem.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** UB 457×191×98 section properties (Blue Book) ([sheet](../../worked-examples/structural/struct.section-properties/ub-457x191x98/sheet.txt)).

```text
A = Green's theorem over 108 vertices in 1 loop(s) = 125.27 cm²
x_c = Green's theorem over 108 vertices in 1 loop(s) = 0 mm
y_c = Green's theorem over 108 vertices in 1 loop(s) = 0 mm
I_xx = Green's theorem over 108 vertices in 1 loop(s) = 45728 cm⁴
I_yy = Green's theorem over 108 vertices in 1 loop(s) = 2347 cm⁴
I_xy = Green's theorem over 108 vertices in 1 loop(s) = 0 cm⁴
I_1 = (45728 cm⁴ + 2347 cm⁴) / 2 + sqrt(((45728 cm⁴ − 2347 cm⁴) / 2)^2 + (0 cm⁴)^2) = 45728 cm⁴
I_2 = (45728 cm⁴ + 2347 cm⁴) / 2 − sqrt(((45728 cm⁴ − 2347 cm⁴) / 2)^2 + (0 cm⁴)^2) = 2347 cm⁴
theta = ½ atan2(0, 43381) = 0 deg
y_top = Green's theorem over 108 vertices in 1 loop(s) = 233.6 mm
y_bot = Green's theorem over 108 vertices in 1 loop(s) = 233.6 mm
x_ext = Green's theorem over 108 vertices in 1 loop(s) = 96.4 mm
W_el_x = 45728 cm⁴ / max(233.6 mm, 233.6 mm) = 1957.5 cm³
W_el_y = 2347 cm⁴ / 96.4 mm = 243.47 cm³
… 7 more step(s) on the sheet
```
<!-- hand-arithmetic:end -->
