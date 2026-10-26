# E5 — Fault protection: Zs, disconnection, adiabatic, fault level (`elec.fault-protection@1.4.0`)

**In plain terms.** If a live conductor touches an earthed part, the protective device must open the circuit fast enough that the fault is not dangerous. The worksheet works out the loop impedance and fault current of the circuit, whether the device opens in time, whether the protective and line conductors can carry the fault current without overheating, and whether the device can break the fault level. It demands an independent check.

**Scope.** One circuit in a TN system: earth fault loop impedance against the device maximum, disconnection time, protective and line conductors against the adiabatic minimum, and a simplified prospective fault level against breaking capacity. **Independent check demanded** (the author cannot check it, DA-12); every input outside its range is a hard FAIL.

**Method.** `Z_s = Z_e + ρ₂₀ L (1/S_line + 1/S_cpc)[1 + α(θ − 20)]`; `I_f = C_min U₀/Z_s`, C_min = 0.95. MCB B/C/D: `I_a = 5/10/20 I_n`; fuse or MCCB: `I_a` at t_req by log-log interpolation of the user's time–current points (no extrapolation). `Z_s,max = C_min U₀/I_a` (BS 7671 reg. 411.4.4; times reg. 411.3.2.2/411.3.2.3; BS EN 60204-1 cl. 6.3.3). `S_min = √(I²t)/k` (reg. 543.1.3, 434.5.2), I²t from the curve time at 0.1 s and above; below 0.1 s the device's let-through at the **maximum prospective fault** (`I2t_let`). Without `I2t_let` the protective conductor check uses I_f²·0.1 s at the end-of-line fault only (labelled so, with a warning) and the **line conductor check (reg. 434.5.2) is not made**: the end-of-line earth fault is the minimum fault and does not bound the energy of the maximum one (Green G-B-03). `k = √[Q_c(β + 20)/ρ₂₀ · ln(1 + (θ_f − θ_i)/(β + θ_i))]`, labelled derived. `Z_T = u_k U_n²/S_r`, `I_k3 = c U_n/(√3 Z_T)` (IEC 60909-0 cl. 5.3). Optional 0.8 × Z_s,max field-test value, labelled rule of thumb. **Device agreement:** with the optional `device_code` (1 MCB type B, 2 type C, 3 type D, 4 fuse or MCCB, linked from E4) the `device` chosen here must be the matching one, else the sheet is blocked (`device:`); in the motor-circuit pack the cable step's device therefore cannot silently differ from the one checked here (Green G-B-01, Black B-A-03).

**Inputs.** `U_0`, `Z_e` 0.01–3 Ω, `C_min`, optional `S_r`, `u_k`, `c_max`; `L`, `S_line`, `S_cpc`, `theta` 20–90 °C; material record `conductor.material` (`rho_20`, `alpha`, `Q_c`, `beta`, `theta_i`, `theta_f`); `device`, `I_n`, optional `device_code` 1–4, optional `curve`, `t_req` 0.1–5 s, optional `I2t_let` (let-through I²t at the maximum prospective fault), `I_cn` (record `device.characteristic`).

**Outputs.** `R_12`, `Z_s`, `I_f`, `I_a`, `t_d`, `Z_s_max`, `u_Zs`, `Z_test`, `k`, `S_min`, `u_cpc`, `u_line` (only when the line check is made), `Z_T`, `I_k3`, `c_k3` (1.2.0: the voltage factor `c_max` that `I_k3` was computed with, when the transformer data are given), `u_Icn`. **Ports.** `Z_s`, `I_f`, `S_min`, `I_k3`, `c_k3` (1.2.0, → E9 `c_src`). **Checks.** loop impedance; disconnection time (shown as `t_d` against `t_req` with its real ratio); CPC adiabatic; line adiabatic (when made); breaking capacity.

**Port names (C-2).** Each port is listed under its public name, `<area>.<quantity>`; the output symbol stays as its symbol and as an alias, so a link, a pack binding or an inputs file written before the rename still resolves. `Z_s` = `fault.impedance_loop`; `I_f` = `fault.current_earth`; `S_min` = `conductor.area_adiabatic_min`; `I_k3` = `fault.current_three_phase`; `c_k3` = `fault.factor_voltage`.

**Limitations.** TN only; single earth fault; no motor contribution, arc fault or IT systems; no BS 7671 table embedded.

**Changes in 1.3.0 (elec.fault-protection-01, -03, -09).** `R₁ + R₂ = k_R·ρ₂₀·L·(1/S_line + 1/S_cpc)·[1 + α(θ − 20)]`: the new input `k_R` (default 1.125, range 1 to 1.5) takes the pure-copper resistivity to the cable standard's maximum conductor resistance (5 to 12 % higher), so `Z_s` is no longer understated; `k` still uses the pure `ρ₂₀`. `k_R` below 1.05 warns (rule `resistance`). Below 0.1 s without a let-through `I²t` the protective conductor has not been shown to withstand a fault near the origin, where the earth-fault current is largest: the sheet now fails the check 'Conductors against let-through at the origin' (and says so in the `let-through` warning) until `I2t_let` is entered; with it, both conductors are checked against it. A fault current outside the entered curve points is a Warning under rule `curve-range` (above the highest point the fastest time entered is an upper bound); the Block rule `curve` is the curve that does not bracket `t_req`.

**Changes in 1.3.1 (chain.motor-circuit-06 remainder).** When `S_cpc` equals `S_line` and `S_line` is above 16 mm² the sheet warns (rule `cpc-full-size`): the packs link the cable step's CSA to both `S_line` and `S_cpc`, but above 16 mm² the protective conductor is normally smaller (BS 7671 Table 54.7), and the smaller conductor raises `R₁ + R₂`, `Z_s` and the adiabatic utilisation. The sheet cannot tell a link from a typed value, so the notice is raised on the equal values; enter the real `S_cpc`, or confirm the full-size conductor. No value changes.

**Changes in 1.3.2 (elec.fault-protection-05, -06).** Two warnings, no number changes. `theta` (rule `theta`): when the temperature at which Z_s is taken is below the adiabatic check's initial temperature `theta_i` the sheet warns that R₁ + R₂ and Z_s are understated (θ_i 90 °C with θ 70 °C gave Z_s 0.7901 Ω against 0.8190 Ω at 90 °C, 3.7 % low). `t_req` (rule `t_req`): the required disconnection time is a free input (0.1 to 5 s), so the sheet warns when it is above 0.4 s at U_0 up to 230 V (a longer time is for a distribution circuit only, BS 7671 reg. 411.3.2.3) and when U_0 is above 230 V and t_req is still the 230 V figure of 0.4 s or more; the times for other voltages are Table 41.1 of the owner's copy and are not built in.

**Test cases.** `ka-mcb-b32` (with `k_R` 1, the pure resistivity: Z_s 0.7901 Ω, Z_s,max 1.3656 Ω, S_min 0.7616 mm²), `ka-mcb-b32-maximum-resistance` (the default `k_R` 1.125: Z_s 0.8451 Ω, I_f 258.55 A, u_Zs 0.6188, S_min 0.7120 mm²), `ka-fuse-curve` (t_d 0.1716 s), `ka-fault-level` (I_k3 15.942 kA) — analytic, Confirmed; `ka-wiring-matters-b32` (1.37 Ω, IET Wiring Matters March 2023) and `ka-schneider-k` (k ≈ 143, Schneider guide) — **Unconfirmed** until checked against the articles (values as the addendum quotes them). Eaton Memshield 3 non-trip data (1.13 I_n) is not used by this calculation.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Workshop radial: 16 A type C MCB, 40 m of 4/2.5 mm² copper ([sheet](../../worked-examples/electrical/elec.fault-protection/workshop-radial-c16/sheet.txt)).

```text
R_12 = 0.000017241 ohm·mm × 1.125 × 40 m × (1 / 4 mm² + 1 / 2.5 mm²) × (1 + 0.00393 1/K × (70 degC − 20 degC)) = 0.60339 ohm
Z_s = 0.8 ohm + 0.60339 ohm = 1.4034 ohm
I_f = 0.95 × 230 V / 1.4034 ohm = 155.69 A
I_a = 10 × 16 A = 160 A
Z_s_max = 0.95 × 230 V / 160 A = 1.3656 ohm
u_Zs = 1.4034 ohm / 1.3656 ohm = 1.0277
Z_test = 0.8 × 1.3656 ohm = 1.0925 ohm
Governing: FAIL  Disconnection achieved: I_f below the instantaneous tripping current vs disconnection within t_req  « BS 7671 reg. 411.3.2.2
```
<!-- hand-arithmetic:end -->
