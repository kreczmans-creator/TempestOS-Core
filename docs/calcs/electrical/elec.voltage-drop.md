# Voltage drop and I²R loss (`elec.voltage-drop@1.4.0`)

**In plain terms.** Current flowing through a cable's resistance and reactance loses some voltage before it reaches the load. How much depends on the current, the length, the conductor and, for alternating current, the power factor and whether the supply is single- or three-phase. The drop is compared with the allowed percentage of the supply voltage, and the same current gives the heat lost in the cable and an estimate of how warm the conductor runs.

## Scope

Calculates the voltage drop, the conductor power loss and the estimated conductor temperature of a DC, single-phase or three-phase circuit. The drop limit is a user input, because the limits of BS 7671 are not embedded.

## Method

- Conductor resistance at its operating temperature, for n conductors in parallel: R = ρ_T L/(A n), with ρ_T = ρ_20 [1 + α (T_c − 20)].
- Resistance and reactance from per-length figures instead: R = R′ L/n; X = X′ L/n.
- Voltage drop on a DC circuit: ΔV = 2 I R.
- Voltage drop on a single-phase AC circuit: ΔV = 2 I (R cos φ + X sin φ).
- Voltage drop on a three-phase AC circuit: ΔV = √3 I (R cos φ + X sin φ).
- Voltage drop from a tabulated mV/A/m figure, which already carries the 2 or √3: ΔV = (mV/A/m) I L / 1000.
- Power lost in the cable: P_loss = 2 I² R (DC and single-phase); 3 I² R (three-phase).
- Estimated conductor temperature at this current: T_c,est = T_amb + (T_rated − T_amb)(I/I_z)².

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `system` | System |  | AC three-phase | AC three-phase, AC single-phase, DC | Setup |
| `data` | Conductor data from |  | resistivity and area | resistivity and area, R and X per km, mV/A/m figure | Setup |
| `L` | Route length (one way) | m | 80 | 0.1 to 5000 | Geometry |
| `n_par` | Parallel conductors per phase |  | 1 | 1 to 6 | Geometry |
| `A_c` | Conductor cross-section (optional) | mm² | 35 | 1 to 1000 | Geometry |
| `alpha` | Temperature coefficient of resistance |  | 0.00393 | 0.003 to 0.0045 | Material |
| `X_km` | Reactance per km per conductor | Ω/km | 0.08 | 0 to 1 | Material |
| `T_rated` | Conductor rated temperature | °C | 70 | 60 to 90 | Material |
| `rho_20` | Resistivity at 20 °C, the effective value for the conductor resistance (default 0.0194 Ω·mm²/m: the largest class 2 maximum conductor resistance of the cable standard for 1.5 to 120 mm², so the drop is not below the cable tables; pure annealed copper is 0.01724, aluminium 0.0282 Ω·mm²/m) (optional) | Ω·mm²/m | 0.0194 | 0.01 to 0.05 | Material |
| `R_km` | Resistance per km per conductor (tabulated, at its stated temperature) (optional) | Ω/km |  | 0.01 to 50 | Material |
| `mVAm` | Voltage drop figure (mV/A/m, from the cable table) (optional) |  |  | 0.1 to 100 | Material |
| `V_n` | Nominal voltage (line-to-line for three-phase) | V | 400 | 12 to 1000 | Loads |
| `I` | Design current | A | 100 | 0.1 to 5000 | Loads |
| `cos_phi` | Power factor (AC) |  | 0.85 | 0.5 to 1 | Loads |
| `T_c` | Conductor operating temperature for the resistance | °C | 70 | 20 to 90 | Conditions |
| `T_amb` | Ambient temperature | °C | 30 | 0 to 60 | Conditions |
| `I_z` | Tabulated current-carrying capacity for the installed conditions (temperature estimate) (optional) | A |  | 1 to 5000 | Conditions |
| `dv_limit` | Voltage-drop limit |  | 5 | 1 to 10 | Requirements |

## Assumptions

- The load is balanced across the phases. Basis: Standard engineering approximation.
- The conductor reactance is the entered X′ per kilometre times the length. Basis: Standard engineering approximation.
- The voltage drop is the arithmetic sum R cos φ + X sin φ, not the vector difference. Basis: Standard engineering approximation.
- A mV/A/m figure is used as tabulated, with its own temperature and system convention. Basis: Cable table convention.
- The estimated conductor temperature scales the rated temperature rise with (I/I_z)². Basis: BS 7671:2018 Appendix 4 approach, formula only.
- I_z is the tabulated capacity for the installation method, with its correction factors applied. Basis: BS 7671:2018 Appendix 4 approach, formula only.
- When the estimate is above T_c, the resistance and the drop are computed at the estimate. Basis: Keeps the drop on the safe side.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `R` | Resistance | Ω |
| `X` | Reactance | Ω |
| `dV` | Voltage drop | V |
| `dV_pct` | Voltage drop (%) |  |
| `P_loss` | Power loss | kW |
| `T_c_est` | Estimated conductor temperature | °C |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `circuit.voltage_drop` | `dV` | V | Voltage drop along the circuit |
| `circuit.voltage_drop_percent` | `dV_pct` |  | Voltage drop relative to the nominal voltage, in % |
| `circuit.power_loss` | `P_loss` | kW | Power lost in the conductors |
| `circuit.temperature_conductor_estimated` | `T_c_est` | degC | Estimated conductor temperature at the design current |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Voltage drop against the limit (BS 7671:2018 reg. 525)
- Design current against the current-carrying capacity (BS 7671:2018 reg. 523)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `conductor-data` | Block | Conservative | The chosen conductor-data route needs values that were not given. |
| `default-conductor` | Warning | Conservative | The conductor cross-section or resistivity was left blank and the template default is used. |
| `overload` | Warning | Conservative | The design current exceeds the stated current-carrying capacity. |
| `temperature` | Warning | Conservative | The estimated conductor temperature is above the temperature entered for the resistance, so the resistance and the voltage drop are computed at the estimated one. |

## Limitations

The load is balanced. The drop limits and the cable tables of BS 7671 are not embedded: the user enters the limit and any tabulated figure. The temperature estimate scales the rated rise with (I/I_z)² and is not a thermal model.

## References

- BS 7671:2018, reg. 525: Voltage drop in consumers' installations
- BS 7671:2018, App. 4 §6: Voltage drop (mV/A/m tables and the temperature-correction method)
- Hughes, Electrical and Electronic Technology, Ch. on DC and AC circuits, 12th ed. (edition to be checked): Resistance, resistivity and temperature coefficient

## Test cases

- `ka-dc`: DC 48 V, 20 A, 30 m, 6 mm² copper at 70 °C (analytic) (Confirmed; source: Analytic (Hughes, resistivity with temperature coefficient))
- `ka-three-phase`: Three-phase 400 V, 100 A, 80 m, 35 mm² copper, X′ = 0.08 Ω/km, cos φ = 0.85 (analytic) (Confirmed; source: Analytic (√3 I (R cos φ + X sin φ)))
- `ka-mvam`: Three-phase from a 2.4 mV/A/m table figure, 63 A, 50 m (analytic) (Confirmed; source: Analytic (BS 7671 Appendix 4 method, formula only))

## Changes

**Changes in 1.4.0 (C-2 consistency, phase C).** Layout and wording only; no result changes. The drop and the design current are checked as physical values: "Voltage drop against the limit" compares `dV` with the new step `dV_max`, the drop the limit allows in V, and "Design current against the current-carrying capacity" compares `I` with `I_z`. `P_loss` is shown in kW. The inputs are grouped Setup, Geometry, Material, Loads, Conditions and Requirements, in that order. The template declares the ports `circuit.*`. The scope, the assumptions and the messages follow the sheet standard.

**Changes in 1.1.0 (elec.voltage-drop-01).** The default `rho_20` is 0.0194 Ω·mm²/m instead of 0.01724: the largest class 2 maximum conductor resistance of the cable standard for 1.5 to 120 mm² (0.387 Ω/km at 50 mm² is 0.01935), so the drop is not below the cable tables the regulations' mV/A/m figures are built on. Enter 0.01724 for pure annealed copper or 0.0282 for aluminium; a stated value is used as entered.

**Changes in 1.2.0 (elec.voltage-drop-02, -03).** On the resistivity route a blank `A_c` or `rho_20` takes the template default (35 mm², 0.0194 Ω·mm²/m) and the sheet now says so (warning `default-conductor`, one per blank input; the KAT answer 0.04715 Ω came with no notice). The overload check `I ≤ I_z` and the conductor temperature estimate now run on every route when `I_z` is entered, the mV/A/m route included (60 A on a 50 A cable gave only the drop check; now `u_Iz` 1.2 fails, the warning `overload` is raised and `T_c_est` is 87.6 °C); the `temperature` warning stays with the resistivity route, the one that takes a conductor temperature.

**Changes in 1.3.0 (C-2 fix, M1).** `temperature`: when `I_z` is given and the estimate `T_c,est = T_amb + (T_rated − T_amb)(I/I_z)²` is above the `T_c` entered, the resistivity and the voltage drop are computed at the estimate (the output `T_c_used`) and the warning says so, so the drop is never below what the cable will show; a `T_c` at or above the estimate is used as entered, and a run without `I_z` is unchanged. The `ρ_T` step of the hand arithmetic below follows.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Three-phase motor feeder, 100 A over 80 m ([sheet](../../worked-examples/electrical/elec.voltage-drop/three-phase-motor-feeder/sheet.txt)).

```text
sin_phi = sqrt(1 − 0.85^2) = 0.52678
u_Iz = 100 A / 119 A = 0.84034
T_c_est = 30 degC + (70 degC − 30 degC) × 0.84034^2 = 58.247 degC
rho_T = 0.01724 ohm·mm²/m × (1 + 0.00393 × (70 degC − 20 degC) / 1 K) = 0.020628 ohm·mm²/m
R = 0.020628 ohm·mm²/m × 80 m / (35 mm² × 1) = 0.047149 ohm
X = 0.08 ohm/km × 80 m / 1 = 0.0064 ohm
dV = sqrt(3) × 100 A × (0.047149 ohm × 0.85 + 0.0064 ohm × 0.52678) = 7.5254 V
dV_pct = 100 × 7.5254 V / 400 V = 1.8814
P_loss = 3 × (100 A)^2 × 0.047149 ohm = 1.4145 kW
u_dv = 1.8814 / 5 = 0.37627
dV_max = 5 / 100 × 400 V = 20 V
Governing: PASS  Design current against the current-carrying capacity: 100 A vs 119 A  utilisation 0.84  « BS 7671:2018 reg. 523
```
<!-- hand-arithmetic:end -->
