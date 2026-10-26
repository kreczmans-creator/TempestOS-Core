# M2a — ISO 286 limits and fits (`mech.iso286-fit@1.4.1`)

**In plain terms.** A hole and a shaft each get a tolerance band from the ISO system: the letter fixes how far the band sits from the nominal size and the number fixes how wide it is. Pairing the largest hole with the smallest shaft gives the loosest fit, and the smallest hole with the largest shaft the tightest. If the hole is always larger the fit has clearance, if it is always smaller it has interference, and if the bands overlap it is a transition fit that can go either way.

**Scope.** Hole and shaft limits for a nominal size and tolerance classes; fit kind and extreme clearance/interference. Formulae only (ISO 286-1 §4–§5); no tables are embedded.

**Method.** Size step (lower, upper] → geometric mean D. `i = 0.45 D^(1/3) + 0.001 D` µm (D ≤ 500), `I = 0.004 D + 2.1` above. `ITn = m_n · i` (IT5…IT18 multipliers 7, 10, 16, 25, 40, 64, 100, 160, 250, 400, 640, 1000, 1600, 2500; IT01–IT1 linear in D; IT2–IT4 geometric). Shaft fundamental deviations d…zc from their published formulae (`g = −2.5 D^0.34`, `p = IT7` (above 500 mm `0.072 D + 37.8`), `s = IT8 + 1` or `IT7 + 0.4 D`, …); hole deviations by the general rule (`EI = −es` for A–H; `ES = −ei + Δ`, Δ = ITn − IT(n−1), for K–N ≤ IT8 and P–ZC ≤ IT7; N above IT8 has `ES = 0` for sizes over 3 mm, as in the standard). A size exactly on a step limit (18 mm) belongs to the step it closes, however the unit conversion rounded it (1.3.0; 18 mm read IT7 20.918 µm, the 25 mm value, instead of 17.32 µm). Overrides from a reference record replace any deviation. **A rolling-bearing bore is not an ISO 286 hole:** it has its own tolerance (ISO 492) lying below nominal, so `hole_kind` offers "rolling-bearing bore", whose deviations must come from the bearing record (`ES_override`, `EI_override`); without them the sheet blocks (rule `bearing-bore`) instead of treating the bore as H7. H7/k6 on a 35 mm seat is a transition fit; a bore of 0/−9 µm (illustrative) on k6 is an interference fit at every size combination.

| Input | Notes |
|---|---|
| `D` | nominal size, 0 < D ≤ 3150 mm |
| `hole_kind` | ISO 286 hole class (default) or rolling-bearing bore |
| `hole_letter`, `hole_grade`, `shaft_letter`, `shaft_grade` | tolerance classes (the hole pair is not used for a bearing bore) |
| `ES/EI/es/ei_override` | optional, µm, both of a pair together |

**Outputs.** `IT_hole`, `IT_shaft`, `ES`, `EI`, `es`, `ei`, limits, `fit` (clearance / transition / interference), `c_max`, `c_min`, `i_max`, `i_min`, `c_mean`.

**Limitations.** Unrounded formula values; for the clearance and transition letters (a to n) the tabulated values differ by the standard's rounding (about 1 µm). For the interference letters p to zc the tables also carry the standard's own increments and sub-steps (p6 at 450 mm: 62.2 µm from the formula against 68 µm tabulated; up to about 9 µm in the 100 to 565 mm checks), so the formula value can be several µm below the tabulated one and the maximum interference is understated: since 1.3.0 the sheet warns (`interference-letter`) for any p to zc letter taken from the formula, and the overrides carry the tabulated value. Letters a, b, c, j need overrides.

**Changes in 1.5.0 (mech.iso286-fit-03, from the C-1 verification).** The K row's `ES = −ei(k) + Δ` form applies to every grade up to IT8, and the k deviation in it is `0.6·D^(1/3)` (the shaft k of IT4 to IT7), but only IT8 had been given it: a K3 hole still used the shaft formula's 0 for IT3, so its `ES` was `0.6·D^(1/3)` µm too high (25 mm K3: ES +1.328 µm before, −0.384 µm now; K5, K6, K7 are unchanged at +1.860, +2.210, +6.132 µm). Δ is applied from IT3 to IT8 only, as the standard gives it; below IT3 it is 0 (K1, K2 and the M, N and P to ZC holes of IT1 and IT2 no longer carry `IT(n) − IT(n−1)`). The owner is asked to confirm the K rows below IT4 against the licensed copy (DECISIONS text in the C-1 report).

**Changes in 1.4.0 (mech.iso286-fit-03, -04).** -03: a hole K at IT8 keeps the k deviation: `ES = −ei(k) + Δ` takes the shaft k of the grades the standard lists it for (IT4 to IT7), because the shaft formula returns 0 at IT8 and the K8 hole lost it (25 mm K8: ES 11.766 µm before, 10.054 µm now, the table value for 18 to 30 mm being +10 µm; K7, K6, M8 and K above IT8 are unchanged). -04: the t shaft (and T hole) exists above 24 mm only; at 24 mm and below the sheet now refuses it with a message naming the 24 mm limit instead of returning the s value (10 mm H7/t6 gave ei 23.453 µm, the s value), and from 24 to 30 mm it is evaluated at the sub-step mean `√(24·30)` (28 mm: ei 33.684 µm before, 37.823 µm now, in the family of the formula-versus-table notice for the letters p to zc).

**Test cases.** `ka-h7g6-25` (25 mm H7/g6: 21/13 µm, g −7 µm, clearance 7…41 µm, within 3–6 %); `ka-h7p6-50` (50 mm H7/p6, interference up to 42 µm); `ka-bearing-bore-k6` (35 mm bore 0/−9 µm on k6: c_max −2.030 µm, c_min −26.642 µm — formula values recomputed in python3, Confirmed; the bore deviations are invented for the test).
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
Governing: PASS  Fit classified: H7/g6 vs clearance
```
<!-- hand-arithmetic:end -->
