# The same wall with a polyethylene vapour control layer behind the plasterboard: no condensation (`bs.glaser`)

**Source.** Analytic (Glaser method; recomputed in python3), As the previous example with a 0.3 mm layer (λ 0.2, μ 20000: s_d 6 m) between the plasterboard and the mineral wool; all material values and the climate are invented illustrative inputs, not design data — Confirmed.

The 6 m diffusion-equivalent layer holds the vapour pressure at the cold interfaces below saturation in every month, so no interface condenses and the sheet passes all three checks. The vapour control layer must be continuous and sealed at laps and penetrations: this calculation does not model air leakage or holes in it.

| Input | Value | Source |
|---|---|---|
| `Mc_limit` | 500 g/m^2 | spec: SPEC-A-02 §4.4: limit on the condensate per interface |
| `RH_crit` | 80 % | spec: SPEC-A-02 §4.4: critical surface humidity |
| `RH_int` | 50 % | spec: SPEC-A-02 §3.1: internal design humidity |
| `Rse` | 0.04 m^2*K/W | spec: Project surface-resistance record (external), assumed |
| `Rsi` | 0.13 m^2*K/W | spec: Project surface-resistance record (internal, horizontal flow), assumed |
| `Rsi_f` | 0.25 m^2*K/W | spec: Project practice: larger R_si for the surface-condensation check, assumed |
| `breathable` | no | assumption: Mineral wool and brick: no timber or hygroscopic layers. |
| `climate` | 12 row(s) | assumption: Invented illustrative maritime year; replace with the project's own monthly climate record (take it from your licensed source). |
| `humidity_mode` | constant internal relative humidity | assumption: Constant internal humidity; no humidity-class record is held for this project. |
| `layers` | 4 row(s) | drawing: A-310 rev C |
| `theta_int` | 20 degC | spec: SPEC-A-02 §3.1: living-room design temperature |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `R_T` | 1.91882 m^2*K/W | 1.91882 m^2*K/W | PASS |
| `S_d` | 8.3 m | 8.3 m | PASS |
| `n_start` | 0  | 0  | PASS |
| `Mc_max` | 0 g/m^2 | 0 g/m^2 | PASS |
| `Mc_end` | 0 g/m^2 | 0 g/m^2 | PASS |
| `u_Mc` | 0  | 0  | PASS |
| `R_Tf` | 2.03882 m^2*K/W | 2.03882 m^2*K/W | PASS |
| `fRsi` | 0.87738  | 0.87738  | PASS |
| `fRsi_min` | 0.590256  | 0.590256  | PASS |
| `pass` | 1  | 1  | PASS |

Governing check: Surface condensation: f_Rsi against f_Rsi,min at 80 % critical humidity — PASS (utilisation 0.673). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
