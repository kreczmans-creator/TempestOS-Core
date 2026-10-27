# Solid brick wall with internal mineral wool and no vapour control layer: interstitial condensation over a year (`bs.glaser`)

**Source.** Analytic (Glaser method: series resistance, Magnus saturation pressure, tangent construction, g = δ0·Δp/Δs_d; recomputed in python3 with an independent obstacle-problem solve), Plasterboard 12.5 mm (λ 0.25, μ 8), mineral wool 50 mm (λ 0.035, μ 1) and existing solid brick 215 mm (λ 0.8, μ 10) at 20 °C / 50 % inside; the monthly climate is invented (a maritime year, not ISO 13788 data) and every material value is an invented illustrative input, not design data — Confirmed.

Condensation starts at the insulation–brick interface in November, builds to 3023 g/m² in March (month lengths are the calendar days) and has dried out by June, so the annual balance passes, but the peak is six times the 500 g/m² project limit and the sheet fails. The surface check passes (f_Rsi 0.877 against f_Rsi,min 0.590, governed by January). The cure is the vapour control layer of the next example.

| Input | Value | Source |
|---|---|---|
| `Mc_limit` | 500 g/m^2 | spec: SPEC-A-02 §4.4: limit on the condensate per interface |
| `RH_crit` | 80 % | spec: SPEC-A-02 §4.4: critical surface humidity |
| `RH_int` | 50 % | spec: SPEC-A-02 §3.1: internal design humidity |
| `Rse` | 0.04 m^2*K/W | spec: Project surface-resistance record (external), assumed |
| `Rsi` | 0.13 m^2*K/W | spec: Project surface-resistance record (internal, horizontal flow), assumed |
| `Rsi_f` | 0.25 m^2*K/W | spec: Conventional R_si,f = 0.25 m²K/W for the surface-condensation check of an opaque element (a record, to be checked against the owner's copy of ISO 13788), assumed |
| `T_int` | 20 degC | spec: SPEC-A-02 §3.1: living-room design temperature |
| `breathable` | no | assumption: Mineral wool and brick: no timber or hygroscopic layers. |
| `climate` | 12 row(s) | assumption: Invented illustrative maritime year; replace with the project's own monthly climate record (take it from your licensed source). |
| `humidity_mode` | constant internal relative humidity | assumption: Constant internal humidity; no humidity-class record is held for this project. |
| `layers` | 3 row(s) | drawing: A-310 rev B |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `R_T` | 1.91732 m^2*K/W | 1.91732 m^2*K/W | PASS |
| `S_d` | 2.3 m | 2.3 m | PASS |
| `n_start` | 11  | 11  | PASS |
| `Mc_3` | 3022.62 g/m^2 | 3022.62 g/m^2 | PASS |
| `Mc_6` | 0 g/m^2 | 0 g/m^2 | PASS |
| `Mc_if_2` | 3022.62 g/m^2 | 3022.62 g/m^2 | PASS |
| `Mc_max` | 3022.62 g/m^2 | 3022.62 g/m^2 | PASS |
| `Mc_end` | 0 g/m^2 | 0 g/m^2 | PASS |
| `u_Mc` | 6.04525  | 6.04525  | PASS |
| `fRsi` | 0.87729  | 0.87729  | PASS |
| `fRsi_min` | 0.590256  | 0.590256  | PASS |
| `pass` | 0  | 0  | PASS |

Governing check: Peak condensate on an interface against its limit — FAIL (utilisation 6.05). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
