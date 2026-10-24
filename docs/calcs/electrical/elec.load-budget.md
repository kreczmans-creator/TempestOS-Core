# E6 — Load budget, PSU, transformer and battery (`elec.load-budget@1.0.0`)

**Scope.** Demand of a set of loads per supply (AC three-phase, AC single-phase, DC); minimum DC PSU, transformer (with a starting check) and battery; heat dissipated for E2.

**Method.** Per load `P·d·k` and `P·d·k/cosφ` (arithmetic sum, conservative). `I_3φ = S_3φ/(√3 U)`, `I_1φ = S_1φ/U`, `I_DC = P_DC/U_DC`. `Φ = Σ(1 − η)P d k`. `P_PSU ≥ margin·P_DC/f_derate`. `S_T ≥ margin·S`; `S_peak = S + max((m − 1)S_i) ≤ k_T·S_T`. Battery `C = I_b t/(DoD η_b k_temp k_age)`, `I_b = Σ backed P d k/(U_b η_conv)`; optional Peukert `I_b H (t/H)^(1/k_p)` (Linden 5th ed.).

**Inputs.** `loads` table (name, supply, P 0–200 kW, cosφ 0.5–1, η 0.5–1, d, k 0–1, inrush 1–20, backed; ≤ 100); `U_3ph`, `U_1ph`, `U_DC`; `margin` 1.0–1.5; `theta` −20 to 70 °C; `f_derate` (record `psu.derating`); optional `P_PSU`, `S_T`; `k_T`; `t_auto` 0–72 h; `U_b`, `eta_conv`; `DoD`, `eta_b`, `k_temp`, `k_age`, optional `k_p`, `H_rate` (record `battery.factors`); optional `C_batt`.

**Outputs.** `P_conn`, `P_dem`, `S_dem`, `I_3ph`, `I_1ph`, `I_DC`, `Phi`, `P_PSU_req`, `S_T_req`, `S_peak`, `I_b`, `Ah`, `C_req`. **Ports.** `P_dem`, `S_dem`, `I_3ph` (→ E4 I_b), `I_1ph`, `I_DC`, `Phi` (→ E2), `C_req`. **Checks.** selected PSU, transformer, starting peak, battery.

**Limitations.** No harmonic or unbalance assessment; inrush only for the single largest start.

**Test cases.** `ka-mixed-buses`, `ka-battery` — analytic, Confirmed; `ka-peukert` (Linden relation, numbers analytic) and `ka-psu-derating` (Phoenix Contact QUINT4 curve read as f = 0.75 at 70 °C) — **Unconfirmed** until checked against the book and the datasheet.
