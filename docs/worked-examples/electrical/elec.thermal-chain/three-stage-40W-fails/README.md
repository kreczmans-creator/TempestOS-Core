# The same chain at 40 W — over the limit (`elec.thermal-chain`)

**Source.** TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation, docs/engineering/calculations/calc.thermal-resistance-chain.md, Example 3 — Confirmed.

At 40 W the junction reaches 140 °C, 15 K over. The budget is 2.125 K/W; with 0.7 K/W already in the device and pad, the sink must be 1.4 K/W or better.

| Input | Value | Source |
|---|---|---|
| `P` | 40 W | spec: DS-PWR-902 device and heat sink datasheets rev A |
| `T_a` | 40 degC | spec: SPEC-EL-05 thermal limits |
| `T_max` | 125 degC | spec: SPEC-EL-05 thermal limits |
| `stages` | 3 row(s) | spec: DS-PWR-902 device and heat sink datasheets rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `T_1` | 140 degC | 140 degC | PASS |
| `dT_m` | -15 K | -15 K | PASS |
| `u` | 1.1765  | 1.17647  | PASS |
| `R_max` | 2.125 K/W | 2.125 K/W | PASS |

Governing check: Source temperature T_1 ≤ T_max — FAIL (utilisation 1.176). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
