# Three-stage chain, 25 W, 40 °C ambient (`elec.thermal-chain`)

**Source.** TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation, docs/engineering/calculations/calc.thermal-resistance-chain.md, Example 1 — Confirmed.

Junction to case, interface pad and heat sink in series, 2.5 K/W in all. The junction runs at 102.5 °C against 125 °C; the sink-to-ambient stage takes 45 of the 62.5 K rise, so a better sink is where any further margin would come from.

| Input | Value | Source |
|---|---|---|
| `P` | 25 W | spec: DS-PWR-901 device and heat sink datasheets rev A |
| `T_a` | 40 degC | spec: SPEC-EL-05 thermal limits |
| `T_max` | 125 degC | spec: SPEC-EL-05 thermal limits |
| `stages` | 3 row(s) | spec: DS-PWR-901 device and heat sink datasheets rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `T_1` | 102.5 degC | 102.5 degC | PASS |
| `dT_m` | 22.5 K | 22.5 K | PASS |
| `u` | 0.73529  | 0.735294  | PASS |
| `R_max` | 3.4 K/W | 3.4 K/W | PASS |

Governing check: Source temperature T_1 ≤ T_max — PASS (utilisation 0.74). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
