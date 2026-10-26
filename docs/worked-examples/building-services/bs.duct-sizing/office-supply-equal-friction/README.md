# Office supply ductwork by equal friction at 1 Pa/m (`bs.duct-sizing`)

**Source.** ASHRAE Handbook — Fundamentals, 2017, Ch. 21 equal-friction method; equivalent diameters by the Ch. 21 relations — Confirmed.

Five sections sized at 1 Pa/m with the AHU main and floor main rectangular on fixed depths. The index run (four sections) adds friction, fittings (ΣC from the manufacturer's data) and the 60 Pa VAV terminal; the total is checked against the fan's external static allowance and every section against the 8 m/s limit.

| Input | Value | Source |
|---|---|---|
| `T` | 20 degC | assumption: Supply air 20 °C. |
| `dp_limit` | 250 Pa | spec: Fan external static allowance 250 Pa |
| `dp_target` | 1 Pa/m | spec: SPEC-M-07 §5: 1 Pa/m design friction rate |
| `eps` | 0.09 mm | spec: Galvanised sheet, medium roughness (ASHRAE Ch. 21 category) |
| `method` | equal friction | assumption: Equal friction for a low-pressure system. |
| `p_atm` | 101.325 kPa | assumption: Sea level. |
| `round_to` | 50 mm | spec: BS EN 1505 / 1506 preferred sizes, 50 mm steps |
| `sections` | 5 row(s) | drawing: DRG-M-205 rev B |
| `v_limit` | 8 m/s | spec: SPEC-M-07 §5: 8 m/s in risers |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `dp_fit_s4` | 0 Pa | 9.36574 Pa | PASS |

Governing check: Velocity ≤ limit — PASS (utilisation 0.88). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
