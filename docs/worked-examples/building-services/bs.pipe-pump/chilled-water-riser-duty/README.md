# Chilled-water riser: pressure drop, duty point, speed turn-down (`bs.pipe-pump`)

**Source.** Analytic: parabolic pump curve H = 40 − 0.01 Q² against the system curve, Duty where H_pump = H_sys; affinity shift at n₂/n₁ = 0.9 — Confirmed.

Three series segments with K fittings, a 12 m rise and 3 m terminal head. The sheet reports each segment's velocity, Re, Colebrook f (with the Swamee–Jain cross-check) and loss, the system curve, the duty point on the datasheet curve, the duty after a 90 % speed turn-down, shaft power and NPSH available against the pump's requirement.

| Input | Value | Source |
|---|---|---|
| `D_ratio` | 1 | assumption: Impeller unchanged. |
| `H_pump` | [40, 36, 24, 4] m | spec: Pump datasheet P-01 rev 2, curve points |
| `NPSH_r` | 3 m | spec: Pump datasheet P-01 rev 2 |
| `Q` | 30 m^3/h | spec: Schedule CHW-01 design flow |
| `Q_pump` | [0, 20, 40, 60] m^3/h | spec: Pump datasheet P-01 rev 2, curve points |
| `T` | 6 degC | spec: SPEC-M-04 §3: 6 °C flow |
| `eta_pump` | 0.68 | spec: Pump datasheet P-01 rev 2 |
| `fluid` | water | spec: SPEC-M-04 §3: chilled water, no glycol |
| `h_out` | 3 m | spec: Terminal unit: 30 kPa ≈ 3 m |
| `n_ratio` | 0.9 | assumption: Inverter turn-down to 90 % speed. |
| `p_atm` | 101.325 kPa | assumption: Sea level. |
| `segments` | 3 row(s) | drawing: DRG-M-110 rev C |
| `v_max` | 2.5 m/s | spec: CIBSE Guide C practice limit, assumed |
| `z_s` | 2 m | drawing: DRG-M-110 rev C |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `H_static` | 15 m | 15 m | PASS |

Governing check: Velocity ≤ limit — FAIL (utilisation 1.005). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
