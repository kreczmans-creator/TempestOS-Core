# M2a — ISO 286 limits and fits (`mech.iso286-fit@1.0.0`)

**In plain terms.** A hole and a shaft each get a tolerance band from the ISO system: the letter fixes how far the band sits from the nominal size and the number fixes how wide it is. Pairing the largest hole with the smallest shaft gives the loosest fit, and the smallest hole with the largest shaft the tightest. If the hole is always larger the fit has clearance, if it is always smaller it has interference, and if the bands overlap it is a transition fit that can go either way.

**Scope.** Hole and shaft limits for a nominal size and tolerance classes; fit kind and extreme clearance/interference. Formulae only (ISO 286-1 §4–§5); no tables are embedded.

**Method.** Size step (lower, upper] → geometric mean D. `i = 0.45 D^(1/3) + 0.001 D` µm (D ≤ 500), `I = 0.004 D + 2.1` above. `ITn = m_n · i` (IT5…IT18 multipliers 7, 10, 16, 25, 40, 64, 100, 160, 250, 400, 640, 1000, 1600, 2500; IT01–IT1 linear in D; IT2–IT4 geometric). Shaft fundamental deviations d…zc from their published formulae (`g = −2.5 D^0.34`, `p = IT7`, `s = IT8 + 1` or `IT7 + 0.4 D`, …); hole deviations by the general rule (`EI = −es` for A–H; `ES = −ei + Δ`, Δ = ITn − IT(n−1), for K–N ≤ IT8 and P–ZC ≤ IT7). Overrides from a reference record replace any deviation.

| Input | Notes |
|---|---|
| `D` | nominal size, 0 < D ≤ 3150 mm |
| `hole_letter`, `hole_grade`, `shaft_letter`, `shaft_grade` | tolerance classes |
| `ES/EI/es/ei_override` | optional, µm, both of a pair together |

**Outputs.** `IT_hole`, `IT_shaft`, `ES`, `EI`, `es`, `ei`, limits, `fit` (clearance / transition / interference), `c_max`, `c_min`, `i_max`, `i_min`, `c_mean`.

**Limitations.** Unrounded formula values; tabulated values differ by the standard's rounding (≈ 1 µm, see the H7/p6 example). Letters a, b, c, j need overrides.

**Test cases.** `ka-h7g6-25` (25 mm H7/g6: 21/13 µm, g −7 µm, clearance 7…41 µm, within 3–6 %); `ka-h7p6-50` (50 mm H7/p6, interference up to 42 µm).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 25 mm H7/g6 sliding fit ([sheet](../../worked-examples/mechanical/mech.iso286-fit/h7-g6-25mm/sheet.txt)).

```text
D_step = (18, 30] mm = 23.238 mm
i = D = 23.238 mm = 1.3074 µm
IT_hole = 16 × i = 20.918 µm
IT_shaft = 10 × i = 13.074 µm
es = −2.5·D^0.34 = -7.2853 µm
ei = -7.2853 − 13.074 = -20.359 µm
ES = 0 + 20.918 = 20.918 µm
EI = −es of the same letter = 0 µm
D_hole_max = 25 mm + 20.918 µm = 25.021 mm
D_hole_min = 25 mm + 0 µm = 25 mm
d_shaft_max = 25 mm + -7.2853 µm = 24.993 mm
d_shaft_min = 25 mm + -20.359 µm = 24.98 mm
c_max = 20.918 µm − (-20.359 µm) = 41.277 µm
c_min = 0 µm − (-7.2853 µm) = 7.2853 µm
… 4 more step(s) on the sheet
Governing: PASS  Fit classified: H7/g6 vs clearance  utilisation 0
```
<!-- hand-arithmetic:end -->
