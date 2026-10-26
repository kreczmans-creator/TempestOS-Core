# E3 — Voltage drop and I²R loss (`elec.voltage-drop@1.3.0`)

**In plain terms.** Current flowing through a cable's resistance and reactance loses some voltage before it reaches the load. How much depends on the current, the length, the conductor and, for alternating current, the power factor and whether the supply is single- or three-phase. The drop is compared with the allowed percentage of the supply voltage, and the same current gives the heat lost in the cable and an estimate of how warm the conductor runs.

**Scope.** DC, single-phase and three-phase circuits; conductor data from resistivity + cross-section with temperature correction, from R′/X′ per km, or from a mV/A/m figure; parallel conductors; power factor; drop in V and %; conductor power loss; conductor temperature estimate from I/I_z; user limits (BS 7671 525 / Appendix 4 cited, not embedded — D-21).

**Method.** ρ_T = ρ_20 [1 + α (T_c − 20)]; R = ρ_T L/(A n); X = X′ L/n; ΔV = 2IR (DC), 2I(R cos φ + X sin φ) (1φ), √3 I(R cos φ + X sin φ) (3φ), or (mV/A/m) I L/1000; P_loss = 2I²R or 3I²R; T_c,est = T_amb + (T_rated − T_amb)(I/I_z)².

**Inputs.** `system`, `V_n`, `I`, `L`, `cos_phi`, `n_par`, `data`, `rho_20`, `alpha`, `A_c`, `T_c`, `R_km`, `X_km`, `mVAm`, `dv_limit`, `I_z`, `T_rated`, `T_amb`. **Outputs.** `R`, `X`, `dV`, `dV_pct`, `P_loss`, `T_c_est`. **Checks.** drop ≤ limit; I ≤ I_z (when given); warnings for overload and for an estimated temperature above T_c.

**Changes in 1.1.0 (elec.voltage-drop-01).** The default `rho_20` is 0.0194 Ω·mm²/m instead of 0.01724: the largest class 2 maximum conductor resistance of the cable standard for 1.5 to 120 mm² (0.387 Ω/km at 50 mm² is 0.01935), so the drop is not below the cable tables the regulations' mV/A/m figures are built on. Enter 0.01724 for pure annealed copper or 0.0282 for aluminium; a stated value is used as entered.

**Changes in 1.2.0 (elec.voltage-drop-02, -03).** On the resistivity route a blank `A_c` or `rho_20` takes the template default (35 mm², 0.0194 Ω·mm²/m) and the sheet now says so (warning `default-conductor`, one per blank input; the KAT answer 0.04715 Ω came with no notice). The overload check `I ≤ I_z` and the conductor temperature estimate now run on every route when `I_z` is entered, the mV/A/m route included (60 A on a 50 A cable gave only the drop check; now `u_Iz` 1.2 fails, the warning `overload` is raised and `T_c_est` is 87.6 °C); the `temperature` warning stays with the resistivity route, the one that takes a conductor temperature.

**Changes in 1.3.0 (C-2 fix, M1).** `temperature`: when `I_z` is given and the estimate `T_c,est = T_amb + (T_rated − T_amb)(I/I_z)²` is above the `T_c` entered, the resistivity and the voltage drop are computed at the estimate (the output `T_c_used`) and the warning says so, so the drop is never below what the cable will show; a `T_c` at or above the estimate is used as entered, and a run without `I_z` is unchanged. The `ρ_T` step of the hand arithmetic below follows.

**Test cases.** DC 48 V / 6 mm², three-phase 400 V / 35 mm² with reactance, mV/A/m route — all analytic (Confirmed).

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
P_loss = 3 × (100 A)^2 × 0.047149 ohm = 1414.5 W
u_dv = 1.8814 / 5 = 0.37627
Governing: PASS  I ≤ I_z: u_Iz = 0.84 vs u_Iz ≤ 1  utilisation 0.84  « BS 7671 523
```
<!-- hand-arithmetic:end -->
