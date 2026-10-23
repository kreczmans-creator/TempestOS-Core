# S2b — Beam formulas, single span (`struct.beam-formulas@1.0.0`)

**In plain terms.** For a single span with standard supports and loads, the reactions, the largest bending moment and the deflection have closed-form answers worked out once in the textbooks. This worksheet applies those formulae directly, which makes it the quickest way to size or check a simple beam and a useful cross-check on a frame model.

**Scope.** Closed-form quick checks: reactions, maximum and minimum moment with position, maximum shear, maximum deflection with position for eight standard cases (simply supported UDL / point at `a` / end couple; cantilever UDL / point at `a`; fixed-ended UDL / central point; propped cantilever UDL).

**Method.** Roark Table 8.1 in own notation, every step an engine formula (formula → substitution → result). Simply supported point load deflection uses the shorter segment `c` with `δ = P c √((L² − c²)³) / (9√3 L EI)`; a load left of midspan mirrors the beam (warning).

**Inputs.** `case`, `L`, `E`, `I`, and `w` / `P` / `M_0` / `a` as the case needs; optional checks `y_c` with `sigma_allow` (bending stress M·c/I) and `delta_limit` (deflection), carried from the legacy `calc.beam-deflection` (ADR-0167). **Outputs.** `R_A`, `R_B`, `M_max`, `x_M`, `M_min`, `V_max`, `delta_max`, `x_delta`; with the stress inputs `M_abs`, `sigma_b`. **Checks.** bending stress, deflection (each when its inputs are given); a deep-beam warning when L/(2·y_c) < 10.

**Test cases.** SS UDL, SS off-centre point, cantilever tip, fixed UDL — analytic, Confirmed; the four legacy `calc.beam-deflection` hand calculations (`ka-legacy-*`, moment, stress and deflection to five significant figures); the propped-cantilever worked example cross-checks the frame solver.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Simply supported beam, off-centre point load ([sheet](../../worked-examples/structural/struct.beam-formulas/ss-point-offcentre/sheet.txt)).

```text
EI = 210 GPa × 8503 cm⁴ = 17856 kN·m²
b = 6 m − 4 m = 2 m
R_A = 50 kN × 2 m / 6 m = 16.667 kN
R_B = 50 kN × 4 m / 6 m = 33.333 kN
M_max = 50 kN × 4 m × 2 m / 6 m = 66.667 kN·m
x_M = 4 m
M_min = 0 kN*m = 0 kN·m
V_max = max(16.667 kN, 33.333 kN) = 33.333 kN
c_far = 2 m
x_delta = sqrt(((6 m)^2 − (2 m)^2) / 3) = 3.266 m
delta_max = 50 kN × 2 m × sqrt(((6 m)^2 − (2 m)^2)^3) / (9 × sqrt(3) × 6 m × 17856 kN·m²) = 10.839 mm
```
<!-- hand-arithmetic:end -->
