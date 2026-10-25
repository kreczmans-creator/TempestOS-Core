# E6 — Load budget, PSU, transformer and battery (`elec.load-budget@1.2.1`)

**In plain terms.** A supply must be sized for what is connected, not just the sum of the nameplates, because loads are not all on at once. The worksheet adds each load with its duty and diversity, gives the current on each supply, sizes the DC power supply, the transformer (including the demand when motors start) and the battery for the backup time you need, and reports the heat released inside the enclosure.

**Scope.** Demand of a set of loads per supply (AC three-phase, AC single-phase, DC); minimum DC PSU, transformer (with a starting check) and battery; the heat released inside the enclosure for E2.

**Method.** Per load `P·d·k` and `P·d·k/cosφ` (arithmetic sum, conservative). `I_3φ = S_3φ/(√3 U)`, `I_1φ = S_1φ/U`, `I_DC = P_DC/U_DC`; with `psu_bus` set, the DC power supply's input `P_DC/η_PSU` (power factor 1) is added to that AC bus and the DC loads count at `P/η_PSU` in `S` (default: not counted, with a warning when DC loads exist: G-B-12). `Φ = Σ h P d k` with `h` the load's `heat` share (blank: the loss `1 − η` of a drive or supply; 1 for electronics inside the panel; 0 for a machine outside it), so a linked motor adds no panel heat (`heat_add` = 0) (G-B-04). `P_PSU ≥ margin·P_DC/f_derate`. `S_T_req = margin·S` for the running load; `S_peak = S + max(m P/cosφ − S_i) ≤ k_T·S_T` (the starting load undiversified; loads with no demand do not start), so `S_T_req_start = S_peak/k_T`; `S_T_min` is the larger of the two and is the rating that satisfies both of the sheet's transformer checks (B-A-09). Battery `C = I_b t/(DoD η_b k_temp k_age)`, `I_b = Σ backed P d k/(U_b η_conv)`; optional Peukert `I_b H (t/H)^(1/k_p)` (Linden 5th ed.).

**Inputs.** `P_add` (kW, a linked three-phase machine such as E1's RMS power `P_rms`, drawing P_add/η_add) with `cosphi_add`, `eta_add`, `inrush_add`, `heat_add` (0–1, default 0); `loads` table (name, supply, P 0–200 kW, cosφ 0.5–1, η 0.5–1, optional `heat` 0–1, d, k 0–1, inrush 1–20, backed; ≤ 100); `U_3ph`, `U_1ph`, `U_DC`; `psu_bus` and `eta_psu`; `margin` 1.0–1.5; `theta` −20 to 70 °C; `f_derate` (record `psu.derating`); optional `P_PSU`, `S_T`; `k_T`; `t_auto` 0–72 h; `U_b`, `eta_conv`; `DoD`, `eta_b`, `k_temp`, `k_age`, optional `k_p`, `H_rate` (record `battery.factors`); optional `C_batt`.

**Outputs.** `P_conn`, `P_dem`, `S_dem`, `I_3ph`, `I_1ph`, `I_DC`, `Phi`, `P_PSU_req`, `S_T_req`, `S_peak`, `S_T_req_start`, `S_T_min`, `I_b`, `Ah`, `C_req`. **Ports.** `P_dem`, `S_dem`, `I_3ph` (→ E4 I_b), `I_1ph`, `I_DC`, `Phi` (heat released inside the enclosure, → E2), `C_req`. **Checks.** selected PSU, transformer, starting peak, battery.

**Limitations.** No harmonic or unbalance assessment; inrush only for the single largest start.

**Test cases.** `ka-mixed-buses`, `ka-battery` — analytic, Confirmed; `ka-peukert` (Linden relation, numbers analytic) and `ka-psu-derating` (Phoenix Contact QUINT4 curve read as f = 0.75 at 70 °C) — **Unconfirmed** until checked against the book and the datasheet.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Pump skid control panel: supply, PSU, transformer and UPS battery ([sheet](../../worked-examples/electrical/elec.load-budget/pump-skid-panel/sheet.txt)).

```text
L1 = 11 × 1 × 1 = 11 kW; 12.791 kVA = 11 kW
L2 = 11 × 1 × 0 = 0 kW; 0 kVA = 0 kW
L3 = 0.06 × 1 × 1 = 0.06 kW; 0.066667 kVA = 0.06 kW
L4 = 0.02 × 0.1 × 1 = 0.002 kW; 0.0021053 kVA = 0.002 kW
L5 = 0.096 × 1 × 1 = 0.096 kW; 0.10667 kVA = 0.096 kW
L6 = 0.024 × 1 × 1 = 0.024 kW; 0.026667 kVA = 0.024 kW
P_conn = 11 + 11 + 0.06 + 0.02 + 0.096 + 0.024 = 22.2 kW
P_dem = 11 + 0 + 0.06 + 0.002 + 0.096 + 0.024 = 11.182 kW
S_dem = 12.791 + 0 + 0.066667 + 0.0021053 + 0.10667 + 0.026667 = 12.993 kVA
I_3ph = 12791 / (√3 × 400) = 18.462 A
I_1ph = 202.11 / 230 = 0.87872 A
I_DC = 120 / 24 = 5 A
Phi = 0.09×11 + 0.09×0 + 0.3×0.06 + 0×0.002 + 0.1×0.096 + 0.1×0.024 = 1.02 kW
P_DC = 120 W
… 12 more step(s) on the sheet
Governing: FAIL  Starting peak against transformer overload: u_inrush = 1.1112 vs u_inrush ≤ 1  utilisation 1.11
```
<!-- hand-arithmetic:end -->
