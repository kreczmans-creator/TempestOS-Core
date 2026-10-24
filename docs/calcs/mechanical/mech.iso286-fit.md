# M2a — ISO 286 limits and fits (`mech.iso286-fit@1.0.0`)

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
