# B6 — Sealed system: emitters and expansion vessel (`bs.sealed-system@1.3.0`)

**In plain terms.** In a closed heating circuit the water expands as it warms, and with nowhere to go it would overpressure the pipes; an expansion vessel with a cushion of gas takes the extra volume. The worksheet works out how much heat each radiator must give at your flow and return temperatures, how much the system water expands, and so the smallest vessel and its pre-charge pressure that will hold it.

**Scope.** Closed LTHW system below 100 °C: the catalogue output (ΔT 50 K) each room's emitter needs at the design temperatures, checked against the selected emitters; the minimum expansion vessel.

**Method.** `ΔT = (t_f + t_r)/2 − t_i` or the log mean (BS EN 442-2 cl. 5). `Φ = Φ50 (ΔT/50)^n`; `Φ50,req = Φ_req/(ΔT/50)^n`. `V_s = ΣV_em + V_pipe + V_plant`. `e_x = ρ(t_fill)/ρ(t_max) − 1` (Kell 1975, within about 0.01 % of IAPWS-95 at 1 atm), or a glycol record. `p_i = ρ g h + margin`. `V_e = e_x V_s/[1 − (p_i + 1)/(p_f + 1)]` (bar g; + 1 bar as usually written). The vessel is sized for the higher of `t_max` and `t_f` (a warning when `t_max < t_f`), and `t_fill` must be below `t_max` (blocked otherwise, instead of a negative vessel). **`V_e` is the expansion volume only:** it contains no water reserve for system losses and no margin between the final pressure and the safety valve setting beyond `p_f < p_prv`; the sheet states this as an assumption and cites CIBSE Guide B1 only (it no longer cites BS EN 12828 for what it does not implement). A selected emitter output of 0 W is refused (leave `Phi50` empty for none).

**Inputs.** optional `rooms` table (name, Φ, selected Φ50, V_em; ≤ 200); `t_f`, `t_r` 30–90 °C; `t_i` 16–24 °C; `n` 1.1–1.5 (record `emitter.rating`); `dT_method`; `V_pipe`, `V_plant` (V_s must be 10–10 000 L); `t_fill`, `t_max`; optional `e_fluid` (record `fluid.expansion`); `h_static` 0–50 m; `p_margin`; `p_f`; `p_prv` 1.5–6 bar; optional `V_vessel`. The symbol `e` is the engine's constant, so the coefficient is `e_x`.

**Outputs.** `dT`, `dT_fr` (flow-to-return difference `t_f − t_r`, added in 1.1.0 because the emitter mean difference `dT` is not the temperature drop of the loop), `t_m` (mean water temperature `(t_f + t_r)/2`, °C), `f_dT`, `R1…` (Φ50 needed per room), `Phi_tot`, `V_em`, `V_s`, `e_x`, `p_i`, `V_e`, `u_emit`, `u_vessel`. **Ports.** `Phi_tot`, `dT_fr` and `t_m` → B1 (`Phi_load`, `dT_load`, `T`), `V_s`, `V_e`, `dT`. The `Phi` column of `rooms` takes a cell link from B4's `Phi_HL` (ADR-0168 amendment 2). **Checks.** governing room emitter; selected vessel.

**Changes in 1.3.0.** The rooms table has an optional column `t_i`, the room's own design temperature (blank: the sheet's `t_i`); a room with it set takes its emitter's temperature difference from it, `ΔT = (t_f + t_r)/2 − t_i` (or the log mean) and `f_dT = (ΔT/50)^n` for that room (a 350.4 W room on a 400 W emitter at 75/65 °C, n 1.3: `t_i` 20 °C gives ΔT 50 K and utilisation 0.876, 21 °C gives ΔT 49 K and 0.899, +2.7 %). A room temperature at or above the return blocks. In the wet-heating pack each room's `t_i` is linked from its heat-loss step's `theta_int`. A `V_pipe` left at the 20 L default warns (rule `v-pipe-default`): it is the pipework's water content, which B1 outputs as `V_pipe` but cannot feed back (a cycle in the pack), so a larger system would get an undersized vessel (DN28 × 40 m holds 24.63 L: `V_e` 2.496 L against 2.800 L).

**Limitations.** Closed LTHW below 100 °C; no pipe-loss or emitter-position corrections; Kell density at atmospheric pressure.

**Test cases.** `ka-dt50`, `ka-dt30`, `ka-lmtd`, `ka-vessel` (V_e 6.568 L) — analytic, Confirmed. The unattributed manufacturer factor 0.51 (`ka-manufacturer-dt30`) was dropped: the analytic cases cover the characteristic equation. CIBSE Guide B1 (2016) and vessel-manufacturer worked examples are not pinned: the owner's copies are needed.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Heat pump retrofit at 55/45 °C: existing radiators and expansion vessel ([sheet](../../worked-examples/building-services/bs.sealed-system/heat-pump-retrofit-55-45/sheet.txt)).

```text
dT = (55 degC + 45 degC) / 2 − 21 degC = 29 K
dT_fr = 55 degC − 45 degC = 10 K
t_m = (55 degC + 45 degC) / 2 = 50 degC
f_dT = (29 K / 50 K)^1.3 = 0.49256
R1 = 1800 / 0.49256 = 3654.4 W
R2 = 1100 / 0.49256 = 2233.2 W
R3 = 900 / 0.49256 = 1827.2 W
R4 = 700 / 0.49256 = 1421.2 W
R5 = 600 / 0.49256 = 1218.1 W
Phi_tot = 5100 W
V_em = 36 L
V_s = 36 L + 40 L + 15 L = 91 L
rho_fill = t = 10 °C = 999.7 kg/m³
rho_max = t = 60 °C = 983.2 kg/m³
… 6 more step(s) on the sheet
Governing: FAIL  Selected emitters against room heat (governing room): u_emit = 1.1074 vs u_emit ≤ 1  utilisation 1.11
```
<!-- hand-arithmetic:end -->
