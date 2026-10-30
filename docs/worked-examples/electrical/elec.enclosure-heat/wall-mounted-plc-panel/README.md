# Wall-mounted PLC panel, natural dissipation and fan (`elec.enclosure-heat`)

**Source.** Effective-surface method (IEC TR 60890 approach), A_eff from face exposure; ΔT = P/(h_conv·A_eff); V = P/(ρ c_p ΔT) — Confirmed.

300 W in a wall-mounted 600 × 800 × 300 enclosure gives a 48 K natural rise against a 15 K allowance, so natural dissipation fails; a filter fan delivering 114 m³/h at its duty point covers the 62 m³/h needed with margin.

| Input | Value | Source |
|---|---|---|
| `A_sun` | 0 m^2 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `D` | 0.3 m | spec: Enclosure datasheet AE-1380: 600 × 800 × 300 |
| `H` | 0.8 m | spec: Enclosure datasheet AE-1380: 600 × 800 × 300 |
| `I_sun` | 0 W/m^2 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `P_add` | 0 kW | assumption: No further heat load linked into this panel. |
| `T_amb` | 35 degC | spec: Site conditions SC-2: 35 °C plant room; 50 °C inside limit for the drives |
| `T_max` | 50 degC | spec: Site conditions SC-2: 35 °C plant room; 50 °C inside limit for the drives |
| `V_pts` | [0, 100, 200] m^3/h | spec: Fan datasheet FF-120: curve points |
| `V_ref` | 100 m^3/h | spec: Filter datasheet: 20 Pa at 100 m³/h |
| `W` | 0.6 m | spec: Enclosure datasheet AE-1380: 600 × 800 × 300 |
| `alpha_sun` | 0.5 | assumption: Not used indoors (no sun); template default kept. |
| `altitude` | 0 m | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `f_bottom` | 0 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `f_front` | 1 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `f_left` | 1 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `f_rear` | 0 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `f_right` | 1 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `f_top` | 1 | assumption: Wall mounted on a floor plinth: rear and bottom covered; indoors. |
| `h_conv` | 5.5 W/(m^2*K) | reference: Reference record: painted steel enclosure, still air, 5.5 W/m²K (manufacturer guide) |
| `loads` | 2 row(s) | spec: Panel schedule PNL-04 |
| `p_pts` | [60, 30, 0] Pa | spec: Fan datasheet FF-120: curve points |
| `p_sys` | 20 Pa | spec: Filter datasheet: 20 Pa at 100 m³/h |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `dT_nat` | 47.847 K | 47.8469 K | yes |
| `V_duty` | 113.75 m^3/h | 113.746 m^3/h | yes |

Governing check: Natural temperature rise against the allowed rise — FAIL (utilisation 3.19). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
