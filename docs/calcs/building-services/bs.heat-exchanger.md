# B5 — Heat exchanger, LMTD and ε-NTU (`bs.heat-exchanger@1.1.0`)

**Scope.** Single-phase exchanger with constant properties. Rating: duty and outlets for a given area. Sizing: area for a given duty. Counterflow, parallel flow, 1-shell-pass/2n-tube-pass, crossflow both unmixed (approximate, flagged), and one fluid at constant temperature (C_r = 0).

**Method.** `C = ṁ c_p`, `C_r = C_min/C_max`, `U_f = 1/(1/U + R_f)`, `NTU = U_f A/C_min`; ε(NTU, C_r) and its inverse per Incropera 7th ed. Tables 11.3–11.4 (crossflow inverse by bisection). `Q = ε C_min (T_h,in − T_c,in)`; outlets by energy balance. Cross-check `Q = U_f A F ΔT_lm`, F from R and P for one shell pass, back-calculated for crossflow. Pinched ends warn and skip the LMTD line.

**Inputs.** `arrangement`, `mode`; `m_h`, `m_c` 0.001–100 kg/s; `cp_h`, `cp_c` (record kind `fluid.properties`, or B2 for air); `T_h_in`, `T_c_in` −40 to 200 °C; `U` 10–10 000 W/m²K; `R_f`; `A` 0.01–1000 m² (rating); `Q_req` kW (sizing, optional in rating); `dT_min`.

**Outputs.** `C_r`, `NTU`, `eps`, `Q`, `T_h_out`, `T_c_out`, `dT_lm`, `F`, `dT_app`, `UA`, `A_req` (sizing), and from 1.1.0 `dT_c` (cold-side rise, K) and `T_c_m` (mean cold-side temperature, °C). **Ports.** `Q`, `T_h_out`, `T_c_out`, `A_req`, `dT_c`, `T_c_m`, `cp_c` → B1: `Q` is its `Phi_load`, `dT_c` its `dT_load`, `T_c_m` its fluid temperature `T` and `cp_c` its `cp_load` (the cooling chain, `pack.cooling`); `Q_req` takes E2's `P_cool` and `T_h_in` E2's `T_max` (the enclosure air enters the coil at its limit; hot-side c_p 1006 J/(kg·K) is the conventional dry-air value E2 uses). **Checks.** approach against the minimum; duty against required (rating).

**Limitations.** Single phase; constant properties; no pressure drop.

**Test cases.** `ka-counter-balanced` (ε 0.5, ΔT_lm 30 K), `ka-parallel-balanced` (ε 0.432332), `ka-shell-tube` (ε 0.539940, F 0.923456 — ε-NTU and LMTD agree), `ka-counter-sizing` (NTU 2, A 8.36 m²) — analytic, Confirmed; `ka-incropera-11-1` (q 8524 W, T_c,o 40.2 °C, ΔT_lm 43.2 °C, UA 197.3 W/K) — **Unconfirmed** until checked against Incropera 7th ed. Example 11.1.
