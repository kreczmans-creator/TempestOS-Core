# Double-pane window with a 10 mm air gap (`bs.plane-wall`)

**Source.** TempestOS calc.plane-wall-heat-transfer (v0.21.0) hand calculation, after Çengel & Ghajar, docs/engineering/calculations/calc.plane-wall-heat-transfer.md, Example 2 — Confirmed.

The same window with two 4 mm panes and still air between. The 10 mm gap is three-quarters of the wall's resistance; the heat loss falls to a quarter and the inner surface warms to 14.2 °C.

| Input | Value | Source |
|---|---|---|
| `A` | 1.2 m^2 | drawing: DRG-GL-802 rev A |
| `T_1` | 20 degC | spec: SPEC-BS-04 design conditions |
| `T_2` | -10 degC | spec: SPEC-BS-04 design conditions |
| `h_1` | 10 W/(m^2*K) | spec: SPEC-BS-04 design conditions |
| `h_2` | 40 W/(m^2*K) | spec: SPEC-BS-04 design conditions |
| `layers` | 3 row(s) | drawing: DRG-GL-802 rev A |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `R_2` | 0.32051 K/W | 0.320513 K/W | yes |
| `Phi` | 0.069248 kW | 0.0692478 kW | yes |
| `U_wall` | 1.9236 W/(m^2*K) | 1.92355 W/(m^2*K) | yes |
| `T_s0` | 14.229 degC | 14.2293 degC | yes |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
