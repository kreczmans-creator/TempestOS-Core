# B5 — Heat exchanger, LMTD and ε-NTU (`bs.heat-exchanger@1.2.1`)

**In plain terms.** A heat exchanger passes heat from a hot stream to a cold one through a wall, and how much it passes depends on the area, how well the wall conducts, and the flow arrangement. The worksheet gives either the duty and outlet temperatures of a given exchanger, or the area needed for a given duty, by the effectiveness-NTU method, and cross-checks the answer with the log-mean temperature difference.

**Scope.** Single-phase exchanger with constant properties. Rating: duty and outlets for a given area. Sizing: area for a given duty. Counterflow, parallel flow, 1-shell-pass/2n-tube-pass, crossflow both unmixed (approximate, flagged), and one fluid at constant temperature (C_r = 0).

**Method.** `C = ṁ c_p`, `C_r = C_min/C_max`, `U_f = 1/(1/U + R_f)`, `NTU = U_f A/C_min`; ε(NTU, C_r) and its inverse per Incropera 7th ed. Tables 11.3–11.4 (crossflow inverse by bisection). `Q = ε C_min (T_h,in − T_c,in)`; outlets by energy balance. Cross-check `Q = U_f A F ΔT_lm`, F from R and P for one shell pass, back-calculated for crossflow. Pinched ends warn and skip the LMTD line. **One fluid at constant temperature (C_r = 0)** takes `const_side` (the hot side, e.g. condensing vapour, or the cold side, e.g. boiling liquid): that fluid's capacity rate is infinite, `C_min` is the *other* stream's, `ε = 1 − e^−NTU`, and the constant stream leaves at its inlet temperature (its flow and specific heat are not used). The ε-NTU and LMTD duties describe one exchanger, so a gap of more than 2 % between them warns (`lmtd-mismatch`).

**Inputs.** `arrangement`, `const_side` (C_r = 0 only), `mode`; `m_h`, `m_c` 0.001–100 kg/s; `cp_h`, `cp_c` (record kind `fluid.properties`, or B2 for air); `T_h_in`, `T_c_in` −40 to 200 °C; `U` 10–10 000 W/m²K; `R_f`; `A` 0.01–1000 m² (rating); `Q_req` kW (sizing, optional in rating); `dT_min`.

**Outputs.** `C_r`, `NTU`, `eps`, `Q`, `T_h_out`, `T_c_out`, `dT_lm`, `F`, `Q_lmtd`, `dT_app`, `UA`, `A_req` (sizing), and from 1.1.0 `dT_c` (cold-side rise, K) and `T_c_m` (mean cold-side temperature, °C). **Ports.** `Q`, `T_h_out`, `T_c_out`, `A_req`, `dT_c`, `T_c_m`, `cp_c` → B1: `Q` is its `Phi_load`, `dT_c` its `dT_load`, `T_c_m` its fluid temperature `T` and `cp_c` its `cp_load` (the cooling chain, `pack.cooling`); `Q_req` takes E2's `P_cool` and `T_h_in` E2's `T_max` (the enclosure air enters the coil at its limit; hot-side c_p 1006 J/(kg·K) is the conventional dry-air value E2 uses). **Checks.** approach against the minimum; duty against required (rating).

**Limitations.** Single phase; constant properties; no pressure drop.

**Test cases.** `ka-counter-balanced` (ε 0.5, ΔT_lm 30 K), `ka-parallel-balanced` (ε 0.432332, Q 108.42896 kW — tightened to 1e-6), `ka-isothermal-hot` (hot side constant, cold 2 kg/s: NTU 0.5, Q 197.364 kW, cold side to 43.608 °C), `ka-isothermal-cold` (cold side constant: Q 158.536 kW, hot side to 42.073 °C), `ka-shell-tube` (ε 0.539940, F 0.923456 — ε-NTU and LMTD agree), `ka-counter-sizing` (NTU 2, A 8.36 m²) — analytic, Confirmed; `ka-incropera-11-1` (q 8524 W, T_c,o 40.2 °C, ΔT_lm 43.2 °C, UA 197.3 W/K) — **Unconfirmed** until checked against Incropera 7th ed. Example 11.1.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** District heating plate exchanger sizing, counterflow ([sheet](../../worked-examples/building-services/bs.heat-exchanger/lthw-plate-hx-sizing/sheet.txt)).

```text
C_h = 1.2 kg/s × 4190 J/(kg·K) = 5028 W/K
C_c = 1.5 kg/s × 4180 J/(kg·K) = 6270 W/K
C_min = min(5028 W/K, 6270 W/K) = 5028 W/K
C_r = min(5028 W/K, 6270 W/K) / max(5028 W/K, 6270 W/K) = 0.80191
U_f = 1 / (1 / 3000 W/(m²·K) + 0.0001 m²·K/W) = 2307.7 W/(m²·K)
eps = 120000 / (5028 × 40) = 0.59666
NTU = ε = 0.59666, C_r = 0.80191 = 1.2973
A_req = 1.2973 × 5028 / 2307.7 = 2.8267 m²
UA = 1.2973 × 5028 = 6523 W/K
Q = 0.59666 × 5028 × 40 = 120 kW
T_h_out = 80 °C − 120000 / 5028 = 56.134 degC
T_c_out = 40 °C + 120000 / 6270 = 59.139 degC
dT_c = 59.139 °C − 40 °C = 19.139 K
T_c_m = (40 °C + 59.139 °C)/2 = 49.569 degC
… 5 more step(s) on the sheet
Governing: PASS  Approach against minimum: u_app = 0.18595 vs u_app ≤ 1  utilisation 0.186
```
<!-- hand-arithmetic:end -->
