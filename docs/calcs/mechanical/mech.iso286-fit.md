# Limits and fits (ISO 286) (`mech.iso286-fit@1.6.1`)

**In plain terms.** A hole and a shaft each get a tolerance band from the ISO system: the letter fixes how far the band sits from the nominal size and the number fixes how wide it is. Pairing the largest hole with the smallest shaft gives the loosest fit, and the smallest hole with the largest shaft the tightest. If the hole is always larger the fit has clearance, if it is always smaller it has interference, and if the bands overlap it is a transition fit that can go either way.

## Scope

Calculates the limits of size of a hole and a shaft for a nominal size and ISO 286 tolerance classes, and the resulting fit with its extreme clearances and interferences. It uses the ISO 286-1 formulae, not the tables.

## Method

- Standard tolerance factor at the geometric mean D of the size step: i = 0.45·D^(1/3) + 0.001·D (µm).
- Width of the tolerance band for the grade: ITn = multiplier × i.
- Fundamental deviations by letter from the published formulae, and hole deviations by the general rule: EI_hole = −es_shaft (A–H); ES_hole = −ei_shaft + Δ (K–ZC).
- Limits of size of the hole and the shaft: limit = nominal + deviation.
- Fit extremes, the largest and smallest clearance (negative is interference): c_max = ES_hole − ei_shaft; c_min = EI_hole − es_shaft.
- Rolling-bearing bore mode: a bearing bore is not an ISO 286 hole; it has its own tolerance (ISO 492) lying below nominal, so ES_hole and EI_hole are entered from the bearing record instead of being computed.

The size step (lower, upper] gives the geometric mean `D`. `i = 0.45 D^(1/3) + 0.001 D` µm for `D` up to 500 mm, and `I = 0.004 D + 2.1` above. `ITn = m_n · i`, with the IT5 to IT18 multipliers 7, 10, 16, 25, 40, 64, 100, 160, 250, 400, 640, 1000, 1600 and 2500; IT01 to IT1 are linear in `D` and IT2 to IT4 geometric. Shaft fundamental deviations d to zc come from their published formulae (`g = −2.5 D^0.34`, `p = IT7`, above 500 mm `0.072 D + 37.8`, `s = IT8 + 1` or `IT7 + 0.4 D`, and so on). Hole deviations follow the general rule: `EI_hole = −es_shaft` for A to H, and `ES_hole = −ei_shaft + Δ`, with `Δ = ITn − IT(n−1)`, for K to N up to IT8 and P to ZC up to IT7. N above IT8 has `ES_hole = 0` for sizes over 3 mm, as in the standard. A size exactly on a step limit (18 mm) belongs to the step it closes. An override from a reference record replaces any deviation. A rolling-bearing bore is not an ISO 286 hole: it has its own tolerance (ISO 492) lying below nominal. So `hole_kind` offers "rolling-bearing bore", whose deviations must come from the bearing record (`ES_hole_override`, `EI_hole_override`). Without them the sheet blocks (rule `bearing-bore`) instead of treating the bore as H7. H7/k6 on a 35 mm seat is a transition fit; a bore of 0/−9 µm (illustrative) on k6 is an interference fit at every size combination.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `hole_kind` | Hole: an ISO 286 class, or a rolling-bearing bore (ES_hole and EI_hole from the bearing record, ISO 492) |  | ISO 286 hole class |  | Setup |
| `hole_letter` | Hole fundamental deviation letter |  | H |  | Setup |
| `hole_grade` | Hole IT grade |  | 7 | 5 to 11 | Setup |
| `shaft_letter` | Shaft fundamental deviation letter |  | g |  | Setup |
| `shaft_grade` | Shaft IT grade |  | 6 | 4 to 11 | Setup |
| `D` | Nominal size | mm | 25 | 1 to 500 | Geometry |
| `ES_hole_override` (was `ES_override`) | Hole upper deviation override (optional; blank = the formula value, or a reference record) | µm | optional | -1000 to 1000 | Overrides |
| `EI_hole_override` (was `EI_override`) | Hole lower deviation override (optional; blank = the formula value) | µm | optional | -1000 to 1000 | Overrides |
| `es_shaft_override` (was `es_override`) | Shaft upper deviation override (optional; blank = the formula value) | µm | optional | -1000 to 1000 | Overrides |
| `ei_shaft_override` (was `ei_override`) | Shaft lower deviation override (optional; blank = the formula value) | µm | optional | -1000 to 1000 | Overrides |

## Assumptions

1. IT values and deviations are the unrounded results of the ISO 286-1 formulae at the size step's geometric mean. Basis: The tables apply the standard's own rounding, so tabulated values differ by about 1 µm for the letters a to n.
2. For the letters p to zc the formula value can lie several µm below the tabulated deviation. Basis: The tables add their own increments and sub-steps, so p6 at 450 mm is 62.2 µm from the formula against 68 µm tabulated.
3. Hole deviations follow the general rule from the shaft deviations, with Δ where the standard applies it. Basis: Δ is ITn − IT(n−1), and the standard's table-specific departures are not reproduced.

## Outputs

| Symbol | Description |
|---|---|
| `IT_hole` | Hole tolerance |
| `IT_shaft` | Shaft tolerance |
| `ES_hole` | Hole upper deviation |
| `EI_hole` | Hole lower deviation |
| `es_shaft` | Shaft upper deviation |
| `ei_shaft` | Shaft lower deviation |
| `D_hole_max` | Hole maximum |
| `D_hole_min` | Hole minimum |
| `d_shaft_max` | Shaft maximum |
| `d_shaft_min` | Shaft minimum |
| `fit` | Fit kind |
| `c_max` | Maximum clearance |
| `c_min` | Minimum clearance |
| `i_max` | Maximum interference |
| `i_min` | Minimum interference |
| `c_mean` | Mean clearance |

Since 1.6.0 the deviations are `ES_hole`, `EI_hole`, `es_shaft` and `ei_shaft`; before that they were `ES`, `EI`, `es` and `ei`, which differ only by letter case. The old symbols stay as aliases of the ports `fit.deviation_hole_upper`, `fit.deviation_hole_lower`, `fit.deviation_shaft_upper` and `fit.deviation_shaft_lower`.

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `fit.clearance_max` | `c_max` | µm | Largest clearance (negative: smallest interference) |
| `fit.clearance_min` | `c_min` | µm | Smallest clearance (negative: largest interference) |
| `fit.interference_max` | `i_max` | µm | Largest interference |
| `fit.interference_min` | `i_min` | µm | Smallest interference (negative: largest clearance) |
| `fit.deviation_hole_upper` | `ES_hole` (also `ES`) | µm | Hole upper deviation |
| `fit.deviation_hole_lower` | `EI_hole` (also `EI`) | µm | Hole lower deviation |
| `fit.deviation_shaft_upper` | `es_shaft` (also `es`) | µm | Shaft upper deviation |
| `fit.deviation_shaft_lower` | `ei_shaft` (also `ei`) | µm | Shaft lower deviation |
| `fit.tolerance_hole` | `IT_hole` | µm | Hole tolerance |
| `fit.tolerance_shaft` | `IT_shaft` | µm | Shaft tolerance |
| `fit.size_hole_max` | `D_hole_max` | mm | Hole maximum limit of size |
| `fit.size_hole_min` | `D_hole_min` | mm | Hole minimum limit of size |
| `fit.size_shaft_max` | `d_shaft_max` | mm | Shaft maximum limit of size |
| `fit.size_shaft_min` | `d_shaft_min` | mm | Shaft minimum limit of size |

## Checks

- Fit classes against the fit kind: the hole and shaft classes (for example `H7/g6`) against the kind of fit the extremes give (clearance, transition or interference). It is a yes/no check and always passes when the sheet gives a result.

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `bearing-bore` | Block | Conservative | A rolling-bearing bore needs ES_hole_override and EI_hole_override from the bearing record; it is not an H7 hole. |
| `shaft-override` | Block | Conservative | Only one of the two shaft deviation overrides is entered; both are needed. |
| `hole-override` | Block | Conservative | Only one of the two hole deviation overrides is entered; both are needed. |
| `shaft-letter` | Block | Conservative | The shaft letter has no formula at this size (the t shaft starts above 24 mm); choose another letter or enter the overrides. |
| `hole-letter` | Block | Conservative | The hole letter has no formula at this size (the T hole starts above 24 mm); choose another letter or enter the overrides. |
| `large-size` | Warning | Conservative | Nominal size above 500 mm uses the I = 0.004 D + 2.1 tolerance unit. |
| `interference-letter` | Warning | Unconservative | A fundamental deviation from p to zc is evaluated from the formula: the tabulated value can be several µm larger, so the maximum interference is understated; use the overrides where the table governs. |
| `fine-grade` | Warning | Conservative | IT grades below IT5 are evaluated by geometric interpolation; check against the standard. |

## Limitations

The sheet gives unrounded formula values. For the clearance and transition letters (a to n) the tabulated values differ by the standard's rounding, about 1 µm. For the interference letters p to zc the tables also carry the standard's own increments and sub-steps (p6 at 450 mm: 62.2 µm from the formula against 68 µm tabulated; up to about 9 µm in the 100 to 565 mm checks). The formula value can therefore lie several µm below the tabulated one, and the maximum interference is understated. Since 1.3.0 the sheet warns (`interference-letter`) for any p to zc letter taken from the formula, and the overrides carry the tabulated value. Letters a, b, c and j need overrides.

## Changes

**1.6.1.** Declares the four limits of size as ports (`fit.size_hole_max`, `fit.size_hole_min`, `fit.size_shaft_max`, `fit.size_shaft_min`): a downstream sheet that takes a bore or a seat size (a press fit, a bearing seat, the next step of a chain) links to them, as it could before 1.6.0 declared ports and offered only the deviations and tolerances. No calculated value changes.

**1.6.0 (C-2, phase B).** The four deviation overrides are now `ES_hole_override`, `EI_hole_override`, `es_shaft_override` and `ei_shaft_override` (the old `ES_override`, `EI_override`, `es_override` and `ei_override` are aliases: an inputs file, a CSV, a saved draft, a pack example and a scan-import profile that use them still load). The four deviation outputs are renamed as above. The inputs are grouped (`Setup`: the kind, letters and grades; `Geometry`: `D`; `Overrides`) and have usual ranges. The sheet declares ten ports (`fit.clearance_max`, `fit.clearance_min`, `fit.interference_max`, `fit.interference_min`, the four deviations and the two tolerances). The blocks for a missing override, a missing t or T letter and an unknown letter have the rule ids `shaft-override`, `hole-override`, `shaft-letter` and `hole-letter` (declared, without the `input.` prefix). The messages are sentences of at most 20 words. The check is named "Fit classes against the fit kind". No calculated value changes.

**1.5.1 (merge of the C-1 line's 1.5.0 and the C-2 line's 1.4.1).** The C-1 line had 1.5.0 (K3 and Δ below IT3) and the C-2 line 1.4.1 (the catalogue title `Limits and fits (ISO 286)`); the merged template is 1.5.1, above both. No number changes from the merge.

**1.5.0 (mech.iso286-fit-03, from the C-1 verification).** The K row's `ES = −ei(k) + Δ` form applies to every grade up to IT8, and the k deviation in it is `0.6·D^(1/3)` (the shaft k of IT4 to IT7), but only IT8 had been given it: a K3 hole still used the shaft formula's 0 for IT3, so its `ES` was `0.6·D^(1/3)` µm too high (25 mm K3: ES +1.328 µm before, −0.384 µm now; K5, K6, K7 are unchanged at +1.860, +2.210, +6.132 µm). Δ is applied from IT3 to IT8 only, as the standard gives it; below IT3 it is 0 (K1, K2 and the M, N and P to ZC holes of IT1 and IT2 no longer carry `IT(n) − IT(n−1)`). The owner is asked to confirm the K rows below IT4 against the licensed copy (DECISIONS text in the C-1 report).

**1.4.0 (mech.iso286-fit-03, -04).** -03: a hole K at IT8 keeps the k deviation: `ES = −ei(k) + Δ` takes the shaft k of the grades the standard lists it for (IT4 to IT7), because the shaft formula returns 0 at IT8 and the K8 hole lost it (25 mm K8: ES 11.766 µm before, 10.054 µm now, the table value for 18 to 30 mm being +10 µm; K7, K6, M8 and K above IT8 are unchanged). -04: the t shaft (and T hole) exists above 24 mm only; at 24 mm and below the sheet now refuses it with a message naming the 24 mm limit instead of returning the s value (10 mm H7/t6 gave ei 23.453 µm, the s value), and from 24 to 30 mm it is evaluated at the sub-step mean `√(24·30)` (28 mm: ei 33.684 µm before, 37.823 µm now, in the family of the formula-versus-table notice for the letters p to zc).

## References

- ISO 286-1:2010, §4, §5: Bases of tolerances, deviations and fits — tolerance grades and fundamental deviations.
- ISO 286-2:2010: Tables of standard tolerance classes and limit deviations.

## Test cases

`ka-h7g6-25` (25 mm H7/g6: 21/13 µm, g −7 µm, clearance 7 to 41 µm, within 3 to 6 %); `ka-h7p6-50` (50 mm H7/p6, interference up to 42 µm); `ka-bearing-bore-k6` (35 mm bore 0/−9 µm on k6: `c_max` −2.030 µm, `c_min` −26.642 µm, formula values recomputed in python3, Confirmed; the bore deviations are invented for the test).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 25 mm H7/g6 sliding fit ([sheet](../../worked-examples/mechanical/mech.iso286-fit/h7-g6-25mm/sheet.txt)).

```text
D_step = (18, 30] mm = 23.238 mm
i = D = 23.238 mm = 1.3074 µm
IT_hole = 16 × i = 20.918 µm
IT_shaft = 10 × i = 13.074 µm
es_shaft = −2.5·D^0.34 = -7.2853 µm
ei_shaft = -7.2853 − 13.074 = -20.359 µm
ES_hole = 0 + 20.918 = 20.918 µm
EI_hole = −es_shaft of the same letter = 0 µm
D_hole_max = 25 mm + 20.918 µm = 25.021 mm
D_hole_min = 25 mm + 0 µm = 25 mm
d_shaft_max = 25 mm + -7.2853 µm = 24.993 mm
d_shaft_min = 25 mm + -20.359 µm = 24.98 mm
c_max = 20.918 µm − (-20.359 µm) = 41.277 µm
c_min = 0 µm − (-7.2853 µm) = 7.2853 µm
… 4 more step(s) on the sheet
Governing: PASS  Fit classes against the fit kind: H7/g6 vs clearance
```
<!-- hand-arithmetic:end -->
