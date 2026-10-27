# Fault protection (BS 7671) (`elec.fault-protection@1.6.0`)

**In plain terms.** If a live conductor touches an earthed part, the protective device must open the circuit fast enough that the fault is not dangerous. The worksheet works out the loop impedance and fault current of the circuit, whether the device opens in time, whether the protective and line conductors can carry the fault current without overheating, and whether the device can break the fault level. It demands an independent check, or the preparer's own check with a recorded statement where no second person is available.

## Scope

Calculates automatic disconnection of supply in a TN system for one circuit: loop impedance, disconnection time and the adiabatic conductor check. It also gives the prospective three-phase fault at the origin against the breaking capacity.

**Independent check demanded** (DA-12): the author cannot check it. Where no second person is available the preparer may check it alone: tick **Self-check (no second person available)** and write why you stand by it (20 to 500 characters). The statement is kept on the calculation, printed on the issued sheet as `Self-checked by the preparer: …` and written to the audit trail (DC-21).

## Method

- Loop impedance: Z_s = Z_e + (R_1 + R_2)·[1 + α(θ − 20)], R = k_R·ρ_20·L/S (k_R = 1.125 takes the pure-copper ρ_20 to the cable standard's maximum conductor resistance; k enters with the pure ρ_20)
- Fault current: I_f = C_min·U_0/Z_s, C_min = 0.95
- Maximum loop impedance: Z_s,max = C_min·U_0/I_a; I_a = 5, 10 or 20·I_n for MCB types B, C, D, or read from the time–current points at t_req (log-log)
- Adiabatic conductor check: S_min = √(I²t)/k, k = √[Q_c(β + 20)/ρ_20 · ln(1 + (θ_f − θ_i)/(β + θ_i))] (derived); I²t = I_f²·t_d from 0.1 s; below 0.1 s the device's let-through I²t at the maximum prospective fault bounds both conductors at the origin (reg. 434.5.2, 543.1.3), and without it the sheet fails 'Conductors against let-through at the origin'
- Protective device check: the device chosen here must be the one carried from the cable step (device_code 1 = MCB B, 2 = C, 3 = D, 4 = fuse or MCCB), else the sheet is blocked
- Fault level (simplified): Z_T = u_k·U_n²/S_r, I_k3 = c·U_n/(√3·Z_T), U_n = √3·U_0

### Notes on the method

`Z_s = Z_e + ρ₂₀ L (1/S_line + 1/S_cpc)[1 + α(θ − 20)]`; `I_f = C_min U₀/Z_s`, C_min = 0.95. MCB B/C/D: `I_a = 5/10/20 I_n`; fuse or MCCB: `I_a` at t_req by log-log interpolation of the user's time–current points (no extrapolation). `Z_s,max = C_min U₀/I_a` (BS 7671 reg. 411.4.4; times reg. 411.3.2.2/411.3.2.3; BS EN 60204-1 cl. 6.3.3). `S_min = √(I²t)/k` (reg. 543.1.3, 434.5.2), I²t from the curve time at 0.1 s and above; below 0.1 s the device's let-through at the **maximum prospective fault** (`I2t_let`). Without `I2t_let` the protective conductor check uses I_f²·0.1 s at the end-of-line fault only (labelled so, with a warning) and the **line conductor check (reg. 434.5.2) is not made**: the end-of-line earth fault is the minimum fault and does not bound the energy of the maximum one (Green G-B-03). `k = √[Q_c(β + 20)/ρ₂₀ · ln(1 + (θ_f − θ_i)/(β + θ_i))]`, labelled derived. `Z_T = u_k U_n²/S_r`, `I_k3 = c U_n/(√3 Z_T)` (IEC 60909-0 cl. 5.3). Optional 0.8 × Z_s,max field-test value, labelled rule of thumb. **Device agreement:** with the optional `device_code` (1 MCB type B, 2 type C, 3 type D, 4 fuse or MCCB, linked from conductor-sizing) the `device` chosen here must be the matching one, else the sheet is blocked (`device:`); in the motor-circuit pack the cable step's device therefore cannot silently differ from the one checked here (Green G-B-01, Black B-A-03).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `device` | Protective device |  | MCB type B (I_a = 5 I_n) | MCB type B (I_a = 5 I_n), MCB type C (I_a = 10 I_n), MCB type D (I_a = 20 I_n), fuse or MCCB (time–current points) | Setup |
| `I_n` | Rated current | A | 32 | 6 to 630 | Setup |
| `curve` | Time–current points (fuse or MCCB) (optional) |  | table | columns: t (s), I (A) | Setup |
| `device_code` | Device type code carried from the cable step (1 MCB type B, 2 type C, 3 type D, 4 fuse, MCCB or MPCB); the device chosen here must match it (optional) |  |  | 1 to 4 | Setup |
| `Z_s_max_table` | Maximum earth fault loop impedances from your licensed table, checked against Z_s at the device rating and the required disconnection time (optional; blank = no check against a table) |  | table | columns: device (text), t_disc (s), I_n (A), Z_s_max (Ω) | Setup |
| `L` | Circuit length | m | 20 | 1 to 500 | Geometry |
| `S_line` | Line conductor cross-section | mm² | 2.5 | 1 to 300 | Geometry |
| `S_cpc` | Protective conductor cross-section | mm² | 1.5 | 1 to 300 | Geometry |
| `rho_20` | Resistivity at 20 °C (used for k, and for R₁ + R₂ through k_R) | Ω·mm²/m | 0.017241 | 0.015 to 0.05 | Material |
| `k_R` | Conductor resistance factor on ρ₂₀ for R₁ + R₂ (1 = the resistivity itself; the default of 1.125 takes pure copper to the cable standard's maximum conductor resistances, so Z_s is not understated; k is not changed by it) |  | 1.125 | 1 to 1.25 | Material |
| `alpha` | Temperature coefficient of resistance | 1/K | 0.00393 | 0.0035 to 0.0045 | Material |
| `Q_c` | Volumetric heat capacity | J/(K·mm³) | 0.00345 | 0.002 to 0.005 | Material |
| `beta` | Reciprocal temperature coefficient β | K | 234.5 | 200 to 260 | Material |
| `theta_i` | Initial temperature for the adiabatic check | °C | 70 | 40 to 120 | Material |
| `theta_f` | Final temperature for the adiabatic check | °C | 160 | 140 to 300 | Material |
| `U_0` | Nominal voltage to earth | V | 230 | 100 to 600 | Conditions |
| `Z_e` | External earth fault loop impedance | Ω | 0.35 | 0.05 to 2 | Conditions |
| `T_op` | Conductor temperature for Z_s | °C | 70 | 30 to 90 | Conditions |
| `S_r` | Supply transformer rating (for the fault level) (optional) | kVA |  | 10 to 2500 | Conditions |
| `u_k` | Transformer impedance voltage (optional) | % |  | 2 to 10 | Conditions |
| `C_min` | Voltage factor for the minimum fault |  | 0.95 | 0.93 to 1 | Factors |
| `c_max` | Voltage factor c for the maximum fault |  | 1.1 | 1.05 to 1.1 | Factors |
| `t_req` | Required disconnection time | s | 0.4 | 0.2 to 5 | Requirements |
| `I2t_let` | Device let-through energy I²t at the maximum prospective fault (disconnection below 0.1 s) (optional) | A²·s |  | 100 to 50000000 | Device |
| `I_cn` | Rated breaking capacity (optional) | kA |  | 3 to 100 | Device |

## Assumptions

- The system is TN with a single earth fault of negligible impedance. Basis: Scope of the check.
- There is no motor contribution, no arc fault and no IT system. Basis: Scope of the check.
- The line and protective conductors are the same material at the same temperature. Basis: Conductor data from a material record.
- The resistance is ρ₂₀·L/S corrected by α. Basis: Conductor data from a material record.
- The factor k is derived from the material constants, not read from a licensed table. Basis: Derived constants, not read from a table.
- Fuse and MCCB characteristics are points the user reads off the manufacturer's curve. Basis: No time–current table ships.
- The points are interpolated log-log, with no extrapolation. Basis: No time–current table ships.
- The optional field-test value 0.8 × Z_s,max is a rule of thumb for measurements at ambient temperature. Basis: Labelled on the sheet.
- The line conductor check (BS 7671 clause 434.5.2) needs the device's let-through energy at the maximum prospective fault. Basis: Stated limitation: the end-of-line earth fault is the minimum fault, not the maximum.
- The end-of-line earth fault is the minimum fault and does not bound that energy. Basis: Stated limitation: the end-of-line earth fault is the minimum fault, not the maximum.
- The line conductor check is made only when I2t_let is entered or the disconnection time is 0.1 s or more. Basis: Stated limitation: the end-of-line earth fault is the minimum fault, not the maximum.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `R_12` | R₁ + R₂ | Ω |
| `Z_s` | Loop impedance | Ω |
| `I_f` | Earth fault current | A |
| `I_a` | Disconnection current | A |
| `t_d` | Disconnection time | s |
| `Z_s_max` | Maximum loop impedance | Ω |
| `u_Zs` | Loop impedance utilisation |  |
| `Z_test` | Field-test maximum (rule of thumb) | Ω |
| `Z_s_max_tab` | Tabulated maximum loop impedance (only with `Z_s_max_table`) | Ω |
| `u_Zs_tab` | Loop impedance utilisation of the tabulated maximum (only with `Z_s_max_table`) |  |
| `k` | Adiabatic factor (derived) |  |
| `S_min` | Minimum cross-section | mm² |
| `u_cpc` | Protective conductor utilisation |  |
| `u_line` | Line conductor utilisation |  |
| `Z_T` | Transformer impedance | Ω |
| `I_k3` | Prospective fault current | kA |
| `c_k3` | Voltage factor of I_k3 |  |
| `u_Icn` | Breaking capacity utilisation |  |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `fault.impedance_loop` | `Z_s` | ohm | Earth fault loop impedance |
| `fault.current_earth` | `I_f` | A | Earth fault current |
| `conductor.area_adiabatic_min` | `S_min` | mm^2 | Minimum protective conductor |
| `fault.current_three_phase` | `I_k3` | kA | Prospective three-phase fault |
| `fault.factor_voltage` | `c_k3` |  | Voltage factor c that I_k3 was computed with, c_max (→ motor-start c_src, so the motor start uses the same factor) |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Loop impedance against the maximum (BS 7671 reg. 411.4.4)
- Loop impedance against the tabulated maximum (BS 7671 reg. 411.4.4; only with `Z_s_max_table`)
- Disconnection time against the required time (BS 7671 reg. 411.3.2.2)
- Adiabatic minimum against the protective conductor (BS 7671 reg. 543.1.3)
- Conductors against let-through at the origin (BS 7671 reg. 434.5.2, 543.1.3)
- Adiabatic minimum against the line conductor (BS 7671 reg. 434.5.2)
- Prospective fault against the breaking capacity

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `curve` | Block | Conservative | The time–current points must bracket the required disconnection time; at least two points are needed. |
| `curve-range` | Warning | Unconservative | The fault current is outside the points entered: above the highest the fastest time entered bounds the disconnection time, below the lowest no time is read. The loop impedance verdict is still given. |
| `resistance` | Warning | Unconservative | R₁ + R₂ and Z_s may be understated: the conductor resistance factor k_R is below 1.05, or the protective conductor is entered equal to a line conductor above 16 mm². |
| `theta` | Warning | Unconservative | Z_s is taken at a conductor temperature below the adiabatic check's initial temperature theta_i: R₁ + R₂ and Z_s are understated. |
| `t_req` | Warning | Conservative | The required disconnection time is above 0.4 s at U_0 up to 230 V, or is the 230 V time at a higher voltage to earth: check it against the standard's table of times. |
| `let-through` | Warning | Unconservative | Disconnection below 0.1 s without a let-through I²t: the protective conductor check uses I_f²·0.1 s at the end-of-line fault only, the line conductor check is not made and neither conductor has been shown to withstand a fault near the origin, so the sheet fails 'Conductors against let-through at the origin' until I2t_let is entered. |
| `device` | Block | Conservative | The device chosen must be the one carried from the cable step (device_code). |
| `device_code` | Block | Conservative | The device type code carried from the cable step must be a whole number from 1 to 4. |
| `adiabatic` | Warning | Unconservative | No disconnection time: the adiabatic check is not made. |
| `temperatures` | Block | Conservative | The adiabatic final temperature must exceed the initial temperature. |
| `zs-table` | Block | Conservative | The table of maximum loop impedances holds no row, or more than one row, for the device rating and the required disconnection time. |

## Limitations

TN only; single earth fault; no motor contribution, arc fault or IT systems; no BS 7671 table embedded.

## References

- BS 7671:2018, reg. 411.3.2.2, 411.3.2.3, 411.4.4: Automatic disconnection in TN systems: times and the loop impedance condition
- BS 7671:2018, reg. 543.1.3, 434.5.2: Protective conductor and fault current adiabatic checks
- BS EN 60204-1:2018, cl. 6.3.3: Protection by automatic disconnection of supply
- IEC 60909-0:2016, cl. 5.3: Voltage factor c

## Test cases

### Notes on the cases

`ka-mcb-b32` (with `k_R` 1, the pure resistivity: Z_s 0.7901 Ω, Z_s,max 1.3656 Ω, S_min 0.7616 mm²), `ka-mcb-b32-maximum-resistance` (the default `k_R` 1.125: Z_s 0.8451 Ω, I_f 258.55 A, u_Zs 0.6188, S_min 0.7120 mm²), `ka-fuse-curve` (t_d 0.1716 s), `ka-fault-level` (I_k3 15.942 kA) — analytic, Confirmed; `ka-wiring-matters-b32` (1.37 Ω, IET Wiring Matters March 2023) and `ka-schneider-k` (k ≈ 143, Schneider guide) — **Unconfirmed** until checked against the articles (values as the addendum quotes them). Eaton Memshield 3 non-trip data (1.13 I_n) is not used by this calculation.

- `ka-mcb-b32`: 32 A type B MCB, 20 m of 2.5/1.5 mm² copper at 70 °C (Confirmed; source: Analytic (C_min relation and derived k; k_R 1, the pure resistivity))
- `ka-mcb-b32-maximum-resistance`: The same circuit at the cable standard's maximum conductor resistance (k_R 1.125, the default) (Confirmed; source: Analytic (C_min relation and derived k; k_R 1.125 takes the pure resistivity to the cable standard's maximum conductor resistance, recomputed in python3))
- `ka-fuse-curve`: Fuse from user-read curve points, 0.4 s (Confirmed; source: Analytic (log-log interpolation of illustrative points))
- `ka-fault-level`: Prospective fault from a 500 kVA, 5 % transformer (Confirmed; source: Analytic (simplified IEC 60909 form))
- `ka-wiring-matters-b32`: Maximum Z_s for a 32 A type B MCB (IET Wiring Matters) (Unconfirmed; source: IET Wiring Matters, "Determining the maximum earth fault loop impedance")
- `ka-schneider-k`: Derived k for copper, PVC 70 → 160 °C and XLPE 90 → 250 °C (Schneider guide) (Unconfirmed; source: Schneider Electric, Electrical Installation Guide, "Sizing of protective earthing conductor")

## Changes

**1.6.0 (DC-10, DC-21: Track P template 03).** The record kind `bs7671.max-zs` ("Maximum earth fault loop impedance") is declared on the new optional table input `Z_s_max_table` (columns `device` text, `t_disc` s, `I_n` A, `Z_s_max` Ω, at most 200 rows): the user's own licensed table of maximum Z_s by device family, disconnection time and rating, imported from template 03 or typed on the records screen. When the table is given the sheet reads the row whose `I_n` and `t_disc` equal the device rating and `t_req`, records it as `Z_s_max_tab` with the utilisation `u_Zs_tab`, and adds the check "Loop impedance against the tabulated maximum"; the derived `Z_s,max = C_min·U_0/I_a` and its check are unchanged. A table with no such row, or with more than one (two device families in one table), is refused (rule `zs-table`, a block: `Z_s_max_table: no row has I_n = 32 A and t_disc = 0.4 s. Add the row, or clear the table.`). Without the table nothing changes: no number of the known-answer cases, the worked example or the packs moves. Minor bump.

**1.5.1.** Declares the unit change of 1.5.0 (ADR-0177 review): `rho_20` was in Ω·mm before 1.5.0, so a bare stored number from an earlier version is read in Ω·mm, never Ω·mm²/m. No calculated value changes.

**Changes in 1.5.0 (C-2 consistency, phase C).** Layout and wording; no result changes. The conductor temperature `theta` is now `T_op`; the old symbol stays as an alias, so a saved calculation, an inputs file and the pack binding from the cable step still load. `beta`, `theta_i` and `theta_f` keep their symbols, because the record kind `conductor.material` and the record import files name them. `rho_20` is entered in Ω·mm²/m (the value is the same in SI). The checks compare physical values: `Z_s` with `Z_s,max`, `t_d` with `t_req`, `S_min` with `S_cpc` and with `S_line`, and `I_k3` with `I_cn`. The warning about a protective conductor equal to the line conductor is raised under the rule `resistance`, which already covers an understated R₁ + R₂. The new rule `device_code` declares the block on the device type code; `device_code` now has the hard limits 0 to 9 and the sheet itself blocks any value but 1 to 4. The inputs are grouped Setup, Geometry, Material, Conditions, Factors, Requirements and Device, and every usual range is inside its hard limits.

**Changes in 1.3.0 (elec.fault-protection-01, -03, -09).** `R₁ + R₂ = k_R·ρ₂₀·L·(1/S_line + 1/S_cpc)·[1 + α(θ − 20)]`: the new input `k_R` (default 1.125, range 1 to 1.5) takes the pure-copper resistivity to the cable standard's maximum conductor resistance (5 to 12 % higher), so `Z_s` is no longer understated; `k` still uses the pure `ρ₂₀`. `k_R` below 1.05 warns (rule `resistance`). Below 0.1 s without a let-through `I²t` the protective conductor has not been shown to withstand a fault near the origin, where the earth-fault current is largest: the sheet now fails the check 'Conductors against let-through at the origin' (and says so in the `let-through` warning) until `I2t_let` is entered; with it, both conductors are checked against it. A fault current outside the entered curve points is a Warning under rule `curve-range` (above the highest point the fastest time entered is an upper bound); the Block rule `curve` is the curve that does not bracket `t_req`.

**Changes in 1.3.1 (chain.motor-circuit-06 remainder).** When `S_cpc` equals `S_line` and `S_line` is above 16 mm² the sheet warns (rule `cpc-full-size`): the packs link the cable step's CSA to both `S_line` and `S_cpc`, but above 16 mm² the protective conductor is normally smaller (BS 7671 Table 54.7), and the smaller conductor raises `R₁ + R₂`, `Z_s` and the adiabatic utilisation. The sheet cannot tell a link from a typed value, so the notice is raised on the equal values; enter the real `S_cpc`, or confirm the full-size conductor. No value changes.

**Changes in 1.3.2 (elec.fault-protection-05, -06).** Two warnings, no number changes. `theta` (rule `theta`): when the temperature at which Z_s is taken is below the adiabatic check's initial temperature `theta_i` the sheet warns that R₁ + R₂ and Z_s are understated (θ_i 90 °C with θ 70 °C gave Z_s 0.7901 Ω against 0.8190 Ω at 90 °C, 3.7 % low). `t_req` (rule `t_req`): the required disconnection time is a free input (0.1 to 5 s), so the sheet warns when it is above 0.4 s at U_0 up to 230 V (a longer time is for a distribution circuit only, BS 7671 reg. 411.3.2.3) and when U_0 is above 230 V and t_req is still the 230 V figure of 0.4 s or more; the times for other voltages are Table 41.1 of the owner's copy and are not built in.

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
Governing: FAIL  Fault current against the disconnection current: I_f below the instantaneous tripping current vs disconnection within t_req  « BS 7671 reg. 411.3.2.2
```
<!-- hand-arithmetic:end -->
