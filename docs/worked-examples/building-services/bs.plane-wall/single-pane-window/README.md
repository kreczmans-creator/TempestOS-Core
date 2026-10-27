# Single-pane window, 20 °C inside, −10 °C outside (`bs.plane-wall`)

**Source.** TempestOS calc.plane-wall-heat-transfer (v0.21.0) hand calculation, after Çengel & Ghajar, docs/engineering/calculations/calc.plane-wall-heat-transfer.md, Example 1 — Confirmed.

An 8 mm pane, 0.8 × 1.5 m. The glass itself is under 8 % of the resistance; the two air films carry the rest. The inner surface sits at −2.2 °C, so the room side of the glass will frost.

| Input | Value | Source |
|---|---|---|
| `A` | 1.2 m^2 | drawing: DRG-GL-801 rev A |
| `T_1` | 20 degC | spec: SPEC-BS-04 design conditions |
| `T_2` | -10 degC | spec: SPEC-BS-04 design conditions |
| `h_1` | 10 W/(m^2*K) | spec: SPEC-BS-04 design conditions |
| `h_2` | 40 W/(m^2*K) | spec: SPEC-BS-04 design conditions |
| `layers` | 1 row(s) | drawing: DRG-GL-801 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `R_tot` | 0.11271 K/W | 0.112714 K/W | PASS |
| `Phi` | 0.26616 kW | 0.266161 kW | PASS |
| `U_wall` | 7.3934 W/(m^2*K) | 7.39336 W/(m^2*K) | PASS |
| `T_s0` | -2.1801 degC | -2.18009 degC | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
