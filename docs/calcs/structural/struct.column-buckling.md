# S4 — Column buckling, Perry–Robertson (`struct.column-buckling@1.0.1`)

**In plain terms.** A slender strut fails not by crushing but by bowing sideways at Euler's critical load, which falls with the square of its slenderness (length over radius of gyration). A real strut is never perfectly straight, so it bows a little from the start and reaches yield on its concave face before the Euler load. Perry's formula puts that initial bow into the yield condition and Robertson sized the bow to fit tests, giving the strut curves: a stocky strut reaches its design strength, a slender one tends to Euler's value, and the curve bridges the two. The strut curve, chosen by section shape, sets the Robertson constant.

**Scope.** Pin-ended prismatic strut under axial load, about the axis of the given second moment; compressive strength, compression resistance and utilisation. Ported from the legacy `calc.column-buckling` (ADR-0167). Clause numbers only (BS 5950-1:2000 Annex C, 4.7.3.2, Table 23).

**Method.** `r = √(I/A)`, `λ = L_E/r`; `p_E = π²E/λ²`, `P_E = p_E·A`; `λ_0 = 0.2√(π²E/p_y)`; `η = max(0.001·a·(λ − λ_0), 0)`; `φ = (p_y + (η + 1)p_E)/2`; `p_c = p_E·p_y/(φ + √(φ² − p_E·p_y))`; `P_c = p_c·A`; `u = P/P_c`. Robertson constant `a` = 2.0, 3.5, 5.5, 8.0 for curves a–d.

**Inputs.** `L_E`, `A`, `I`, `E` and `p_y` (fillable from a released material record), `curve`, `P`. **Outputs.** `r`, `lambda`, `p_E`, `P_E`, `lambda_0`, `eta`, `p_c`, `P_c`, `u`. **Checks.** axial load against compression resistance.

**Limitations.** Flexural buckling only (no torsional or flexural–torsional modes); no local buckling (class 4) reduction; slenderness above 180 blocks (4.7.3.2). For EN 1993-1-1 design use `struct.en1993-member`.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-curve-c`, `ka-legacy-slender-b`, `ka-legacy-stocky-c`), every intermediate to five significant figures.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Pin-ended column, curve c, intermediate slenderness ([sheet](../../worked-examples/structural/struct.column-buckling/rhs-column-curve-c/sheet.txt)).

```text
a_R = 5.5
r = sqrt(200 cm⁴ / 2000 mm²) = 31.623 mm
lambda = 3 m / 31.623 mm = 94.868
p_E = π^2 × 205 GPa / 94.868^2 = 224.81 MPa
P_E = 224.81 MPa × 2000 mm² = 449.62 kN
lambda_0 = 0.2 × sqrt(π^2 × 205 GPa / 275 MPa) = 17.155
eta = max(0.001 × 5.5 × (94.868 − 17.155), 0) = 0.42742
phi = (275 MPa + (0.42742 + 1) × 224.81 MPa) / 2 = 297.95 MPa
p_c = 224.81 MPa × 275 MPa / (297.95 MPa + sqrt((297.95 MPa)^2 − 224.81 MPa × 275 MPa)) = 133.78 MPa
P_c = 133.78 MPa × 2000 mm² = 267.56 kN
u = 150 kN / 267.56 kN = 0.56062
Governing: PASS  Axial load against compression resistance: u = 0.56062 vs u ≤ 1  utilisation 0.561  « BS 5950-1 Annex C
```
<!-- hand-arithmetic:end -->
