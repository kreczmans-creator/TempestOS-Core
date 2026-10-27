# Section properties (`struct.section-properties@1.2.1`)

**In plain terms.** Every bending, stress and buckling check needs the shape's geometric properties: its area, where its centroid is, how its area is spread about each axis (second moments), and the section moduli that turn a bending moment into a stress. This worksheet computes them from the outline itself, adding up the contribution of each edge of the polygon, so any shape, with holes or built from plates, gives the same properties a handbook table would for a standard section.

## Scope

Calculates area, centroid, second moments, moduli, radii of gyration, and torsion and warping constants of a parametric, polygon or composite section. The outline has x horizontal and y vertical; properties use EN 1993 axes, y-y horizontal and z-z vertical.

## Method

- Area and centroid: A = ½∮(x dy − y dx); x_c = ∫x dA / A; y_c = ∫y dA / A.
- Second moment about y-y: I_y = ∫y² dA − A y_c², by Green's theorem and the parallel-axis theorem.
- Second moment about z-z: I_z = ∫x² dA − A x_c²; product I_yz = ∫xy dA − A x_c y_c.
- Principal axes: tan 2θ = −2 I_yz / (I_y − I_z).
- Elastic section modulus: W_el_y = I_y / y_max.
- Plastic section modulus about the equal-area axis, found by bisection: W_pl_y = |Q_above| + |Q_below|.
- Torsion constant J: Saint-Venant series (solid rectangle), πd⁴/32 (circle), Bredt (closed thin-walled), Σbt³/3 (open).
- Warping constant of an I-section: I_w = I_z (h − t_f)²/4.

Circle and tube are in closed form. Polygons and composites report no torsion constant `J` (a warning). The hollow rectangle uses the EN 10210-2 corner idealisation by default, and rolled I-sections add the El Darwish and Johnston fillet term to `J` (AISC Design Guide 9).

**Axes.** The outline is drawn with `x` horizontal and `y` vertical, and the centroid `x_c`, `y_c` and the fibre distances `y_top`, `y_bot` and `x_ext` are in those drawing axes. The section properties use the EN 1993 axes: `I_y`, `W_el_y`, `W_pl_y` and `r_g_y` are about the horizontal y-y axis, which is the major axis of an I-section drawn upright, and `I_z`, `W_el_z`, `W_pl_z` and `r_g_z` are about the vertical z-z axis. The member check, the buckling check and the frame use the same names, so a value moves from this sheet to them without a change of axis.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `shape` | Shape | — | I-section | rectangle, circle, tube, hollow rectangle, I-section, channel, tee, angle, polygon, composite | Setup | — |
| `h` | Depth or vertical leg (optional) | mm | 467.2 | 1 to 5000 | Geometry | — |
| `b` | Width or horizontal leg (optional) | mm | 192.8 | 1 to 5000 | Geometry | — |
| `t_w` | Wall, web or leg thickness (optional) | mm | 11.4 | 0.1 to 500 | Geometry | `t` |
| `t_f` | Flange thickness (optional) | mm | 19.6 | 0.1 to 500 | Geometry | — |
| `d` | Diameter of a circle or tube (optional) | mm | blank | 1 to 5000 | Geometry | — |
| `r` | Root radius of an I-section, channel, tee or angle, or outer corner radius of a hollow rectangle (optional; blank = 0 for the rolled shapes, 1.5 t_w for a hollow rectangle) | mm | 10.2 | 0 to 500 | Geometry | — |
| `r_i` | Inner corner radius of a hollow rectangle (optional; blank = 1.0 t_w, at most the outer radius) | mm | blank | 0 to 500 | Geometry | — |
| `vertices` | Polygon vertices (polygon shape) | — | blank | table | Polygon | — |
| `parts` | Parts (composite shape) | — | blank | table | Composite | — |

Columns of `vertices`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `x` | x | mm | yes |
| `y` | y | mm | yes |
| `loop` | Loop (1 = outline, 2+ = holes) | — | no |

Columns of `parts`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `shape` | Shape (rectangle, circle, tube, hollow rectangle, I-section, channel, tee, angle) | — | yes |
| `b` | Width or horizontal leg | mm | no |
| `h` | Depth, vertical leg, or diameter of a circle or tube | mm | no |
| `t_w` | Wall, web or leg thickness | mm | no |
| `t_f` | Flange thickness | mm | no |
| `r` | Root or corner radius | mm | no |
| `x0` | Centroid x | mm | yes |
| `y0` | Centroid y | mm | yes |
| `rot` | Rotation (° anticlockwise) | — | no |
| `sign` | Add or subtract (add, subtract) | — | no |

## Assumptions

1. A polygon outline does not cross itself. Basis: Green's theorem integrates the signed loops as given.
2. Holes lie inside the outline and do not overlap one another. Basis: Green's theorem integrates the signed loops as given.
3. Rolled-section root fillets are quarter circles of radius r and flange tips are square. Basis: Published section tables use the same idealisation.
4. Hollow-section corners are quarter circles of radius 1.5 t_w outside and 1.0 t_w inside by default. Basis: BS EN 10210-2 hot-finished idealisation.
5. Thin-walled torsion constants neglect the junction and tip effects except the fillet term kept for rolled I-sections. Basis: Open sections: Σbt³/3; closed: Bredt.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `A` | Area | cm² |
| `x_c` | Centroid x | mm |
| `y_c` | Centroid y | mm |
| `I_y` | Second moment of area about the y-y axis | cm⁴ |
| `I_z` | Second moment of area about the z-z axis | cm⁴ |
| `I_yz` | Product of inertia about the y-y and z-z axes | cm⁴ |
| `I_1` | Major principal second moment | cm⁴ |
| `I_2` | Minor principal second moment | cm⁴ |
| `theta` | Principal axis angle | deg |
| `W_el_y` | Elastic section modulus about the y-y axis | cm³ |
| `W_el_z` | Elastic section modulus about the z-z axis | cm³ |
| `W_pl_y` | Plastic section modulus about the y-y axis | cm³ |
| `W_pl_z` | Plastic section modulus about the z-z axis | cm³ |
| `r_g_y` | Radius of gyration about the y-y axis | mm |
| `r_g_z` | Radius of gyration about the z-z axis | mm |
| `alpha_pl_y` | Shape factor about the y-y axis | — |
| `J` | Torsion constant | cm⁴ |
| `I_w` | Warping constant | cm⁶ |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `section.area` | `A` | Area of the section | cm² | — |
| `section.second_moment_y` | `I_y` | Second moment of area about the y-y axis | cm⁴ | `I_xx` |
| `section.second_moment_z` | `I_z` | Second moment of area about the z-z axis | cm⁴ | `I_yy` |
| `section.product_inertia` | `I_yz` | Product of inertia about the y-y and z-z axes | cm⁴ | `I_xy` |
| `section.modulus_elastic_y` | `W_el_y` | Elastic section modulus about the y-y axis | cm³ | `W_el_x` |
| `section.modulus_elastic_z` | `W_el_z` | Elastic section modulus about the z-z axis | cm³ | — |
| `section.modulus_plastic_y` | `W_pl_y` | Plastic section modulus about the y-y axis | cm³ | `W_pl_x` |
| `section.modulus_plastic_z` | `W_pl_z` | Plastic section modulus about the z-z axis | cm³ | — |
| `section.radius_gyration_y` | `r_g_y` | Radius of gyration about the y-y axis | mm | `i_x` |
| `section.radius_gyration_z` | `r_g_z` | Radius of gyration about the z-z axis | mm | `i_y` |
| `section.constant_torsion` | `J` | Torsion constant | cm⁴ | — |
| `section.constant_warping` | `I_w` | Warping constant of an I-section | cm⁶ | — |

## Checks

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `missing-dimension` | Block | Conservative | A dimension the chosen shape needs was not given. |
| `geometry` | Block | Conservative | A thickness exceeds the size it sits in, or a loop encloses no area. |
| `thin-walled-torsion` | Warning | Conservative | The torsion constant is a thin-walled approximation. |
| `no-torsion` | Warning | Conservative | No torsion constant is computed for an arbitrary polygon or a composite. |
| `thick-wall` | Warning | Conservative | A wall thicker than a fifth of the section size: the thin-walled torsion estimate is rough. |

## Limitations

The torsion constant of a channel, angle or tee is the open thin-walled sum Σ b t³ / 3, which ignores the root fillet. Against a finite-element value it is about 4.3 % low for a 200 × 75 channel with t 6 mm, t_f 11 mm and r 12 mm, and about 8.9 % low for a 100 × 75 angle with t 10 mm and r 12 mm (both conservative). A sharp 150 × 150 tee is about 2.8 % high (unconservative). Take `J` from the section tables where it matters.

A polygon whose loop crosses itself, whose loops cross one another, or whose hole lies outside the outline or inside another hole is blocked. A hollow part of a composite keeps its bore when it is added. A subtracted part turns every solid loop into a hole and every bore of the part back into solid.

The caps are 2,000 vertex rows, 100 parts, 200 loops and 20,000 vertices per loop.

### Changes by version

**1.2.1.** Declares the change of meaning of 1.2.0 (ADR-0177 review): the ports `W_el_y` and `W_pl_y` meant the moduli about the vertical axis of the drawing (the minor axis of an upright I-section) before 1.2.0 and mean the EN 1993 y-y (major-axis) moduli since, so a link, pack binding or import that takes `W_el_y` or `W_pl_y` from a calculation made on 1.1.1 or earlier is refused with a sentence that says so, instead of binding the major-axis value under the old name. A link under the old symbols `W_el_x` and `W_pl_x` keeps its meaning. The composite column `t` is an alias of `t_w`. No calculated value changes.

**1.2.0 (C-2 consistency).** The properties are named in the EN 1993 axes: `I_xx` is now `I_y`, `I_yy` is `I_z`, `I_xy` is `I_yz`, `W_el_x` is `W_el_y`, `W_el_y` is `W_el_z`, `W_pl_x` is `W_pl_y`, `W_pl_y` is `W_pl_z`, `i_x` is `r_g_y`, `i_y` is `r_g_z` and `alpha_pl_x` is `alpha_pl_y`. The old symbols of `I_xx`, `I_yy`, `I_xy`, `W_el_x`, `W_pl_x`, `i_x`, `i_y` and `alpha_pl_x` stay as aliases of the ports, so a saved link, pack binding or example that names them still resolves. A link saved under the old `W_el_y` or `W_pl_y` (the minor-axis moduli) now meets the major-axis modulus of the same name; relink it to `W_el_z` or `W_pl_z`. The input `t` is now `t_w` (old symbol kept as an alias, and a composite table that still has a `t` column is read). The radius of gyration is shown in mm, and the warping constant in cm⁶ (1 dm⁶ is 1,000,000 cm⁶). The sheet declares its ports.

**1.1.1.** A polygon whose loop crosses itself, whose loops cross one another, or whose hole lies outside the outline or inside another hole is blocked (rule `geometry`) with a message that names the loop and the problem. A hole outside the outline used to run and subtract its area from the section (a 100 mm square with a 10 × 10 mm "hole" at x 200 mm gave 99 cm²). The torsion-constant warning for the channel, angle and tee states how far the sum is from a finite-element value.

**1.1.0.** A hollow part (tube, hollow rectangle, or a polygon with a bore) keeps its bore when it is added, so a tube is a ring. Before 1.1.0 the bore of an added hollow part was turned into solid material (a tube d 200, t 10 gave 568.6 cm² against 59.69 cm², with no warning) and the bore of a subtracted one stayed a hole.

## References

- Young and Budynas, Roark's Formulas for Stress and Strain, Table A.1 and Table 10.1, 8th ed.: Properties of sections; torsion of solid and thin-walled sections.
- Seaburg and Carter, Torsional Analysis of Structural Steel Members (AISC Design Guide 9), App., 1st ed.: Torsional properties of rolled I-shapes.
- BS EN 10365:2017: Hot rolled steel channels, I and H sections: dimensions and masses.
- BS EN 10210-2:2019: Hot finished structural hollow sections: tolerances, dimensions and sectional properties.

## Test cases

UB 457×191×98 and hot-finished SHS 100×100×5 against public Blue Book values (Confirmed, within 0.5 %; `J` within 1 %); rectangle and tube analytic; unequal angle as a polygon against the parallel-axis theorem; composite tube d 200 t 10 added (A 59.69 cm², I_y 2 701 cm⁴), SHS 100×100×5 added (18.73 cm²) and a 300 square minus the tube (840.31 cm², 64 799 cm⁴), each from closed forms (python3). The Blue Book calls the axes y-y and z-z, as this sheet does.

- `ka-ub-457x191x98`: UB 457×191×98 (SCI Blue Book). Source: SCI / Steel for Life Blue Book (P363 data), EC3 UK NA.
- `ka-shs-100x100x5`: Hot-finished SHS 100×100×5 (Blue Book). Source: SCI / Steel for Life Blue Book (P363 data), EC3 UK NA.
- `ka-rectangle`: Solid rectangle 100 × 200 (analytic). Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-tube`: Tube 200 × 10 (analytic). Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-angle-polygon`: Unequal angle 150 × 90 × 12, sharp corners, as parametric shape and as a polygon. Source: Analytic (two rectangles 12 × 150 and 78 × 12, parallel-axis theorem).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** UB 457×191×98 section properties (Blue Book) ([sheet](../../worked-examples/structural/struct.section-properties/ub-457x191x98/sheet.txt)).

```text
A = Green's theorem over 108 vertices in 1 loop(s) = 125.27 cm²
x_c = Green's theorem over 108 vertices in 1 loop(s) = 0 mm
y_c = Green's theorem over 108 vertices in 1 loop(s) = 0 mm
I_y = Green's theorem over 108 vertices in 1 loop(s) = 45728 cm⁴
I_z = Green's theorem over 108 vertices in 1 loop(s) = 2347 cm⁴
I_yz = Green's theorem over 108 vertices in 1 loop(s) = 0 cm⁴
I_1 = (45728 cm⁴ + 2347 cm⁴) / 2 + sqrt(((45728 cm⁴ − 2347 cm⁴) / 2)^2 + (0 cm⁴)^2) = 45728 cm⁴
I_2 = (45728 cm⁴ + 2347 cm⁴) / 2 − sqrt(((45728 cm⁴ − 2347 cm⁴) / 2)^2 + (0 cm⁴)^2) = 2347 cm⁴
theta = ½ atan2(0, 43381) = 0 deg
y_top = Green's theorem over 108 vertices in 1 loop(s) = 233.6 mm
y_bot = Green's theorem over 108 vertices in 1 loop(s) = 233.6 mm
x_ext = Green's theorem over 108 vertices in 1 loop(s) = 96.4 mm
W_el_y = 45728 cm⁴ / max(233.6 mm, 233.6 mm) = 1957.5 cm³
W_el_z = 2347 cm⁴ / 96.4 mm = 243.47 cm³
… 7 more step(s) on the sheet
```
<!-- hand-arithmetic:end -->
