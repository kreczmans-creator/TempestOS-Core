# Sealed heating system (`bs.sealed-system@1.5.1`)

**In plain terms.** In a closed heating circuit the water expands as it warms, and with nowhere to go it would overpressure the pipes; an expansion vessel with a cushion of gas takes the extra volume. The worksheet works out how much heat each radiator must give at your flow and return temperatures, how much the system water expands, and so the smallest vessel and its pre-charge pressure that will hold it.

## Scope

Calculates the catalogue output each room's emitter needs at the design flow, return and room temperatures, and checks it against the selected emitters. It also gives the minimum expansion vessel for the system volume, temperatures, static head and pressures.

## Method

- Emitter temperature difference: ΔT = (T_flow + T_return)/2 − T_room, or the log mean (T_flow − T_return)/ln((T_flow − T_room)/(T_return − T_room))
- Output at design: Φ = Φ50·(ΔT/50)^n; required catalogue output Φ50 = Φ_req/(ΔT/50)^n
- System volume: V_s = Σ V_em + V_pipe + V_plant
- Water expansion: e_x = ρ(T_fill)/ρ(T_max) − 1 (water density by Kell 1975), or a glycol record
- Vessel pre-charge: p_i = ρ·g·h_static + p_margin (at least the static head)
- Vessel size: V_e = e_x·V_s/[1 − (p_i + 1)/(p_f + 1)] (pressures in bar gauge, + 1 bar for absolute); the expansion volume only, sized for the higher of T_max and T_flow; a water reserve and a safety-valve allowance are not included

### Notes on the method

`ΔT = (T_flow + T_return)/2 − t_i` or the log mean (BS EN 442-2 cl. 5). `Φ = Φ50 (ΔT/50)^n`; `Φ50,req = Φ_req/(ΔT/50)^n`. `V_s = ΣV_em + V_pipe + V_plant`. `e_x = ρ(T_fill)/ρ(T_max) − 1` (Kell 1975, within about 0.01 % of IAPWS-95 at 1 atm), or a glycol record. `p_i = ρ g h + margin`. `V_e = e_x V_s/[1 − (p_i + 1)/(p_f + 1)]` (bar g; + 1 bar as usually written). The vessel is sized for the higher of `T_max` and `T_flow` (a warning when `T_max < T_flow`), and `T_fill` must be below `T_max` (blocked otherwise, instead of a negative vessel). **`V_e` is the expansion volume only:** it contains no water reserve for system losses and no margin between the final pressure and the safety valve setting beyond `p_f < p_prv`; the sheet states this as an assumption and cites CIBSE Guide B1 only (it no longer cites BS EN 12828 for what it does not implement). A selected emitter output of 0 W is refused (leave `Phi50` empty for none).

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram (ADR-0158, DI-08). It is not to scale and shows inputs only, never a result. It draws the sealed circuit: the boiler, the flow and return pipework, and the expansion vessel below the static height. Three emitters stand for the rooms as listed. It binds `V_plant`, `V_pipe`, `rooms`, `T_flow`, `T_return`, `T_room`, `V_vessel`, `h_static`, `T_fill`, `T_max`, `p_f`, `p_prv`, `p_margin` and `n`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `dT_method` | Mean temperature difference |  | arithmetic mean (catalogue rating) | arithmetic mean (catalogue rating), log mean |  | Setup |
| `rooms` | Rooms and emitters (optional; blank = no emitter check) |  | table |  |  | Setup |
| `rooms.name` | Room (column) |  |  | | | Setup |
| `rooms.Phi` | Heat required (column) | kW |  | | | Setup |
| `rooms.Phi50` | Selected emitter output at ΔT 50 K (column) | kW | optional | | | Setup |
| `rooms.V_em` | Emitter water content (column) | L | optional | | | Setup |
| `rooms.t_i` | Room design temperature (blank = the sheet's room temperature T_room) (column) | °C | optional | | | Setup |
| `h_static` | Static height above the vessel | m | 5 | 0 to 40 | 0 to 50 | Geometry |
| `V_pipe` | Pipework water content | L | 20 | 0 to 5000 | 0 to 10000 | Geometry |
| `V_plant` | Plant water content (boiler, cylinder coil, buffer) | L | 10 | 0 to 5000 | 0 to 10000 | Geometry |
| `V_vessel` | Selected vessel volume (optional; blank = no vessel check) | L | optional | 1 to 5000 | 0.01 to 100000 | Geometry |
| `e_fluid` | Expansion coefficient from a glycol record (optional; blank = water) |  | optional | 0.001 to 0.2 | 0 to 0.5 | Material |
| `T_flow` (was `t_f`) | Flow temperature | °C | 75 | 40 to 85 | 30 to 90 | Conditions |
| `T_return` (was `t_r`) | Return temperature | °C | 65 | 35 to 80 | 30 to 90 | Conditions |
| `T_room` (was `t_i`) | Room temperature | °C | 20 | 16 to 23 | 16 to 24 | Conditions |
| `T_fill` (was `t_fill`) | Fill temperature | °C | 10 | 4 to 30 | 1 to 40 | Conditions |
| `T_max` (was `t_max`) | Maximum system temperature | °C | 85 | 30 to 95 | 30 to 99 | Conditions |
| `p_f` | Final (maximum working) pressure, gauge | kPa | 250 | 50 to 600 | 10 to 600 | Conditions |
| `p_prv` | Safety valve setting, gauge | kPa | 300 | 200 to 550 | 150 to 600 | Conditions |
| `n` | Emitter exponent |  | 1.3 | 1.2 to 1.4 | 1.1 to 1.5 | Factors |
| `p_margin` | Pre-charge allowance over static head | kPa | 30 | 0 to 100 | 0 to 200 | Factors |

## Assumptions

1. The system is a closed LTHW system below 100 °C. Basis: Catalogue ratings at 75/65/20 °C.
2. Emitter output follows Φ50·(ΔT/50)^n with the manufacturer's n. Basis: Catalogue ratings at 75/65/20 °C.
3. Water density comes from the Kell (1975) correlation, within about 0.01 % of IAPWS-95 at atmospheric pressure. Basis: Formula, not a table.
4. Glycol mixtures take the expansion coefficient e from a record. Basis: Formula, not a table.
5. Gauge pressures are made absolute by adding 1 bar, as the vessel relation is usually written. Basis: Within 1.3 % of the standard atmosphere.
6. V_e is the expansion volume by the CIBSE heating guide's relation only. Basis: Stated omission.
7. V_e does not include a water reserve for system losses. Basis: Stated omission; add it when the vessel is selected, from the heating-system standard in use.
8. V_e does not include a margin between the final pressure and the safety valve setting beyond p_f below p_prv. Basis: Stated omission; add it when the vessel is selected, from the heating-system standard in use.

## Outputs

| Symbol | Description |
|---|---|
| `dT` | Mean temperature difference |
| `dT_fr` | Flow-to-return temperature difference |
| `T_m` | Mean water temperature |
| `f_dT` | Output correction factor |
| `R1` | Lounge: Φ50 needed |
| `Phi_tot` | Total heat required |
| `V_em` | Emitter water content |
| `V_s` | System water volume |
| `e_x` | Expansion coefficient |
| `p_i` | Pre-charge pressure |
| `V_e` | Minimum vessel volume |
| `u_emit` | Emitter utilisation |
| `u_vessel` | Vessel utilisation |

`dT`, `dT_fr` (flow-to-return difference `T_flow − T_return`, added in 1.1.0 because the emitter mean difference `dT` is not the temperature drop of the loop), `T_m` (mean water temperature `(T_flow + T_return)/2`, °C), `f_dT`, `R1…` (Φ50 needed per room), `Phi_tot`, `V_em`, `V_s`, `e_x`, `p_i`, `V_e`, `u_emit`, `u_vessel`. **Ports.** `Phi_tot`, `dT_fr` and `T_m` → B1 (`Phi_load`, `dT_load`, `T`), `V_s`, `V_e`, `dT`. The `Phi` column of `rooms` takes a cell link from B4's `Phi_HL` (ADR-0168 amendment 2). **Checks.** governing room emitter; selected vessel.

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `heating.heat_required` | `Phi_tot` | kW | Total heat required by the rooms (→ pipe-pump Phi_load) |
| `heating.volume_system` | `V_s` | L | System water volume |
| `heating.volume_expansion_min` | `V_e` | L | Minimum expansion vessel volume |
| `heating.temperature_difference_emitter` | `dT` | K | Emitter mean temperature difference |
| `heating.temperature_difference_loop` | `dT_fr` | K | Flow-to-return temperature difference of the loop (→ pipe-pump dT_load) |
| `heating.temperature_mean` | `T_m` (was `t_m`) | °C | Mean water temperature of the loop, (T_flow + T_return)/2 (→ pipe-pump T_fluid) |

## Checks

Checks the sheet makes:

- Selected emitters against room heat (governing room)
- Selected vessel against minimum volume (CIBSE Guide B1 §1.4)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `temperatures` | Block | Conservative | The flow temperature must be above the return temperature, and the return above the room temperature. |
| `pressures` | Block | Conservative | Pre-charge below final pressure below the safety valve setting. |
| `volume` | Block | Conservative | System volume between 10 and 10 000 L. |
| `v-pipe-default` | Warning | Unconservative | The pipework water content V_pipe is the template default of 20 L: it is not linked to the pipework, so a larger system gets an undersized expansion vessel. |
| `t-fill` | Block | Conservative | The fill temperature must be below the maximum system temperature. |
| `emitter` | Block | Conservative | A selected emitter output of 0 W is refused: leave the cell empty for none selected. |
| `t-max` | Warning | Conservative | The maximum temperature is below the flow temperature: the vessel is sized for the flow temperature. |

## Limitations

Closed LTHW below 100 °C; no pipe-loss or emitter-position corrections; Kell density at atmospheric pressure.

## References

- BS EN 442-2:2014, cl. 5: Radiators and convectors — test methods and rating (characteristic equation).
- CIBSE Guide B1, §1.4, 2016 ed.: Heating — sealed systems and expansion.

## Test cases

### Notes on the cases

`ka-dt50`, `ka-dt30`, `ka-lmtd`, `ka-vessel` (V_e 6.568 L) — analytic, Confirmed. The unattributed manufacturer factor 0.51 (`ka-manufacturer-dt30`) was dropped: the analytic cases cover the characteristic equation. CIBSE Guide B1 (2016) and vessel-manufacturer worked examples are not pinned: the owner's copies are needed.

- `ka-dt50`: Catalogue conditions 75/65/20: correction 1 (Confirmed; source: Analytic (characteristic equation))
- `ka-dt30`: Low-temperature 55/45/20, arithmetic mean (Confirmed; source: Analytic (characteristic equation))
- `ka-lmtd`: Low-temperature 55/45/20, log mean (Confirmed; source: Analytic (log-mean form))
- `ka-vessel`: Expansion vessel for 100 L, 10 → 85 °C, 5 m static (Confirmed; source: Analytic (Kell 1975 density; vessel relation))

## Changes

**1.5.1.** Declares the unit changes of 1.5.0 (ADR-0177 review): `p_f`, `p_prv` and `p_margin` were in bar and the room columns `Phi` and `Phi50` in W before 1.5.0, so a bare stored number from an earlier version is read in its own unit, never the new one. No calculated value changes.

**Changes in 1.5.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The temperatures are now `T_flow`, `T_return`, `T_room`, `T_fill` and `T_max` (were `t_f`, `t_r`, `t_i`, `t_fill` and `t_max`), and the port mean temperature is `T_m` (alias `t_m`); the old symbols stay as aliases. The pressures `p_f`, `p_prv` and `p_margin` now read in kPa (the defaults are 250, 300 and 30 kPa, the same pressures as before), and the heat columns and `Phi_tot` read in kW. The emitter exponent `n` and the room-temperature column `t_i` of the rooms table keep their names, because the emitter record and a saved table use them. The inputs sit in the groups Setup, Geometry, Material, Conditions and Factors. The governing room heat and the emitter output are records with a check that compares physical values, and the vessel check reads "Selected vessel against minimum volume". The record kind of the emitter exponent is titled "Emitter rating (exponent)".

**Port names (C-2).** Each port is listed under its public name, `<area>.<quantity>`; the output symbol stays as its symbol and as an alias, so a link, a pack binding or an inputs file written before the rename still resolves. `Phi_tot` = `heating.heat_required`; `V_s` = `heating.volume_system`; `V_e` = `heating.volume_expansion_min`; `dT` = `heating.temperature_difference_emitter`; `dT_fr` = `heating.temperature_difference_loop`; `T_m` = `heating.temperature_mean`.

**Changes in 1.3.0.** The rooms table has an optional column `t_i`, the room's own design temperature (blank: the sheet's `T_room`); a room with it set takes its emitter's temperature difference from it, `ΔT = (T_flow + T_return)/2 − t_i` (or the log mean) and `f_dT = (ΔT/50)^n` for that room (a 350.4 W room on a 400 W emitter at 75/65 °C, n 1.3: `t_i` 20 °C gives ΔT 50 K and utilisation 0.876, 21 °C gives ΔT 49 K and 0.899, +2.7 %). A room temperature at or above the return blocks. In the wet-heating pack each room's `t_i` is linked from its heat-loss step's `theta_int`. A `V_pipe` left at the 20 L default warns (rule `v-pipe-default`): it is the pipework's water content, which B1 outputs as `V_pipe` but cannot feed back (a cycle in the pack), so a larger system would get an undersized vessel (DN28 × 40 m holds 24.63 L: `V_e` 2.496 L against 2.800 L).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Heat pump retrofit at 55/45 °C: existing radiators and expansion vessel ([sheet](../../worked-examples/building-services/bs.sealed-system/heat-pump-retrofit-55-45/sheet.txt)).

```text
dT = (55 degC + 45 degC) / 2 − 21 degC = 29 K
dT_fr = 55 degC − 45 degC = 10 K
T_m = (55 degC + 45 degC) / 2 = 50 degC
f_dT = (29 K / 50 K)^1.3 = 0.49256
R1 = 1800 / 0.49256 = 3.6544 kW
R2 = 1100 / 0.49256 = 2.2332 kW
R3 = 900 / 0.49256 = 1.8272 kW
R4 = 700 / 0.49256 = 1.4212 kW
R5 = 600 / 0.49256 = 1.2181 kW
Phi_tot = 5100 W = 5.1 kW
V_em = 36 L
V_s = 36 L + 40 L + 15 L = 91 L
rho_fill = T = 10 °C = 999.7 kg/m³
rho_max = T = 60 °C = 983.2 kg/m³
… 8 more step(s) on the sheet
Governing: FAIL  Selected emitters against room heat (governing room): 0.6000 kW vs 0.5418 kW  utilisation 1.11
```
<!-- hand-arithmetic:end -->
