# 48 V DC battery feed, 20 A over 30 m (`elec.voltage-drop`)

**Source.** Analytic (2 I R with temperature-corrected resistivity), 6 mm² copper at 70 °C — Confirmed.

A 48 V DC feed on 6 mm² copper drops 4.1 V (8.6 %), failing the 5 % limit: the next size up (10 mm²) or a shorter route is needed. The I²R loss of 83 W is reported for the battery budget.

| Input | Value | Source |
|---|---|---|
| `A_c` | 6 mm^2 | spec: Cable schedule CS-02: 6 mm² Cu |
| `I` | 20 A | spec: Load schedule DC-02: 20 A |
| `L` | 30 m | drawing: DRG-E-301 rev A |
| `T_c` | 70 degC | assumption: Design convention. |
| `V_n` | 48 V | assumption: Design convention. |
| `X_km` | 0 ohm/km | assumption: Design convention. |
| `alpha` | 0.00393 | reference: Reference record: annealed copper (Hughes) |
| `data` | resistivity and area | assumption: Design convention. |
| `dv_limit` | 5 | spec: Battery system spec BS-1: 5 % at the load |
| `n_par` | 1 | assumption: Design convention. |
| `rho_20` | 0.01724 ohm*mm^2/m | reference: Reference record: annealed copper (Hughes) |
| `system` | DC | assumption: Design convention. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `dV` | 4.1256 V | 4.12553 V | PASS |
| `dV_pct` | 8.595  | 8.59486  | PASS |

Governing check: Voltage drop ≤ limit — FAIL (utilisation 1.719). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
