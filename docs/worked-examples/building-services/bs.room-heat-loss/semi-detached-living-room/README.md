# Semi-detached living room: U-values and design heat loss for the emitter sizing (`bs.room-heat-loss`)

**Source.** Analytic (series resistance; steady-state Φ = ΣA·U·f·Δθ + y_tb·Σ(A·f)_ext·Δθ + 0.34·n·V·Δθ), Semi-detached living room 4.5 × 3.6 × 2.4 m: front wall with a 2.1 m² window, gable wall, party wall to a neighbour at 18 °C, ground floor with a user-entered U; all material data are invented illustrative inputs, not design data — Confirmed.

The cavity wall has U = 0.241 W/m²K (R = 4.158 m²K/W) and passes the 0.30 target with utilisation 0.80; the party wall (U = 1.193) loses only to the 18 °C neighbour through a 3 K difference and is held to its own 1.2 target (utilisation 0.99). Fabric loss 384 W includes 85.5 W of thermal-bridge allowance on the 35.64 m² facing outside; ventilation at 0.8 h⁻¹ adds 253.8 W. The room load Φ_HL = 638.1 W (HLC 26.59 W/K, 39.4 W/m²) is the figure to carry to the emitter selection in the sealed-system calculation.

| Input | Value | Source |
|---|---|---|
| `A_floor` | 16.2 m^2 | drawing: A-101 rev C |
| `T_int` | 21 degC | spec: SPEC-A-02 §3.1: living-room design temperature |
| `U_target` | 0.3 W/(m^2*K) | spec: SPEC-A-02 §4.2: external wall U-value target |
| `V` | 38.88 m^3 | drawing: A-101 rev C |
| `elements` | 5 row(s) | drawing: A-101 rev C |
| `layers` | 8 row(s) | drawing: A-210 rev C |
| `method` | BS EN 12831-1 elemental | assumption: Elemental method; the design-team default. |
| `n` | 0.8 1/h | assumption: Natural ventilation allowance for a living room. |
| `theta_e` | -3 degC | assumption: Illustrative location design temperature; take the project's own from its design-temperature record. |
| `y_tb` | 0.1 W/(m^2*K) | assumption: Illustrative thermal-bridge allowance; replace with the project's bridging record. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `U_1` | 0.240508 W/(m^2*K) | 0.240509 W/(m^2*K) | PASS |
| `Phi_1` | 0.0502182 kW | 0.0502182 kW | PASS |
| `Phi_2` | 0.07056 kW | 0.07056 kW | PASS |
| `Phi_3` | 0.0498718 kW | 0.0498718 kW | PASS |
| `U_4` | 1.19284 W/(m^2*K) | 1.19284 W/(m^2*K) | PASS |
| `Phi_4` | 0.0309185 kW | 0.0309185 kW | PASS |
| `Phi_5` | 0.0972 kW | 0.0972 kW | PASS |
| `Phi_tb` | 0.085536 kW | 0.085536 kW | PASS |
| `Phi_T` | 0.384304 kW | 0.384305 kW | PASS |
| `Phi_V` | 0.253809 kW | 0.253809 kW | PASS |
| `Phi_HL` | 0.638113 kW | 0.638113 kW | PASS |
| `HLC` | 26.588 W/K | 26.588 W/K | PASS |
| `Phi_A` | 39.3897 W/m^2 | 39.3897 W/m^2 | PASS |

Governing check: U-value of Party wall against its target — PASS (utilisation 0.99). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
