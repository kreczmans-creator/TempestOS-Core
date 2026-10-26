# Motor start voltage dip and PF correction (`elec.motor-start@1.3.0`)

**In plain terms.** When a large motor starts it draws several times its running current, and the supply voltage dips for a moment, which can upset other equipment. The worksheet takes the supply impedance and the motor's starting current and power factor, and works out the voltage dip at the motor and at the point where it shares the supply with other loads, the effect of a soft starter or drive, and the capacitor needed to correct power factor without resonating with the supply.

## Scope

Calculates the voltage dip when a three-phase induction motor starts, from the source, cable and locked-rotor impedances. It also gives the capacitor kvar for a target power factor and the parallel-resonance order.

## Method

- Rated current: I_n = P/(√3·U·cos φ·η), or entered
- Starting current at rated voltage: DOL I_LR·I_n; star-delta I_LR·I_n/3; soft starter min(k_soft·I_n, I_LR·I_n); drive k_vfd·I_n
- Motor starting impedance (DOL, star-delta, and a soft starter that does not reach its limit): |Z_m| = (U/√3)/I_start, for a soft starter (U/√3)/(I_LR·I_n), at the starting power factor, Z_m = |Z_m|·(cos φ_st + j sin φ_st)
- Regulated current (a drive; a soft starter when its fully conducting current E/|Z_s + Z_c + Z_LR| exceeds k_soft·I_n): |I| = I_start held, lagging V_m by φ; with R + jX = Z_s + Z_c, a = |I|(R cos φ + X sin φ), b = |I|(X cos φ − R sin φ): |V_m| = √(E² − b²) − a, V_pcc = V_m + I·Z_c, and Z_m = V_m/I is the equivalent impedance
- Source and cable impedance: transformer |Z_T| = u_k·U²/S_n, plus an optional upstream network |Z_Q| = U²/S_kQ in series; fault level |Z_s| = c·U²/S_k or c·U/(√3·I_k); each element Z = |Z|·(1 + j·X/R)/√(1 + (X/R)²); cable R = ρ_T·L/(A·n) with ρ_T = ρ_20·[1 + α(T_c − 20)], or R′·L/n; X = X′·L/n
- Phasor divider and dip, for a passive motor (phase voltage E = U/√3 at 0°): I = E/(Z_s + Z_c + Z_m); V_pcc = E·(Z_c + Z_m)/(Z_s + Z_c + Z_m); V_m = E·Z_m/(Z_s + Z_c + Z_m); dip = 100·(1 − |V|/E) at the motor terminals and at the point of common coupling, checked against the limits; cable drop dV_cab = √3·I·(R cos φ_st + X sin φ_st)
- Power-factor correction: P_in = P/η (or √3·U·I_n·cos φ when I_n is entered), Q_c = P_in·(tan φ₁ − tan φ₂) with φ₁ the motor's (a drive's displacement factor with a drive), I_c = Q_c/(√3·U), I after = P_in/(√3·U·cos φ₂)
- Parallel resonance: S_sc = U²/|Z_s|, parallel resonance order h ≈ √(S_sc/Q_c) (|Z_s| used for X_s, so h reads 1 to 1.4 % low)

### Notes on the method

- Rated current `I_rated = P/(√3·U·cos φ·η)` or the entered `I_n`. Starting current at rated voltage: DOL `I_LR·I_rated`; star-delta `I_LR·I_rated/3`; soft starter `min(k_soft·I_rated, I_LR·I_rated)`; drive `k_vfd·I_rated` at the drive's displacement power factor.
- Source: transformer `|Z_T| = u_k·U²/S_n`, plus an optional upstream network `|Z_Q| = U²/S_kQ` (X/R `XR_Q`) added as phasors; fault level `c·U²/S_k` or `c·U/(√3·I_k)` (c blank defaults to 1.0 for S_k and 1.1 for I_k, the factor fault-protection computes `I_k3` with); each element `Z = |Z|(1 + j·X/R)/√(1 + (X/R)²)`; `Z_src = |Z_s|`.
- Cable: `R = ρ_T·L/(A·n)`, `ρ_T = ρ_20[1 + α(T_c − 20)]` (or `R′·L/n`), `X = X′·L/n`.
- **Passive start** (DOL, star-delta, and a soft starter that does not reach its limit): the motor is a winding of constant impedance, `|Z_m| = (U/√3)/I_start` (for a soft starter `(U/√3)/(I_LR·I_n)`), `Z_m = |Z_m|(cos φ_st + j sin φ_st)`; the current falls with the dipped voltage. Phasors (E = U/√3 at 0°, `System.Numerics.Complex`, every phasor printed on the sheet with magnitude and angle): `Z_t = Z_s + Z_c + Z_m`, `I = E/Z_t`, `V_m = E·Z_m/Z_t`, `V_pcc = E·(Z_c + Z_m)/Z_t`.
- **Regulated start** (a drive; a soft starter only when it reaches its limit): a soft starter cannot pass more than the locked-rotor current at the dipped voltage, so the passive locked-rotor divider is solved first, `|I_pas| = E/|Z_s + Z_c + Z_LR|`; if `|I_pas| ≤ k_soft·I_n` the sheet reports "Full conduction, limit not reached" (`start_mode`) and the passive result stands, otherwise the current is held, `|I| = I_start`, lagging `V_m` by φ (the starting power factor, or the drive's displacement factor). With `R + jX = Z_s + Z_c`, `a = |I|(R cos φ + X sin φ)`, `b = |I|(X cos φ − R sin φ)`: `|V_m| = √(E² − b²) − a`, `V_pcc = V_m + I·Z_c`, and `Z_m = V_m/I` is the equivalent impedance. A drive whose current the supply cannot hold blocks the sheet (`current-limit`, naming `k_vfd` to lower); a soft starter never asks for more than the passive current, so it does not block. A passive impedance would let the current fall and understate this dip (about 9 % of the dip on the soft-start case, which can turn a fail into a pass near the limit).
- Dip `= 100·(1 − |V|/E)`. `dV_cab = √3·Re[I·Z_c·V̄_m/|V_m|]` is the cable drop along the starting voltage; it equals voltage-drop's `√3·I·(R cos φ + X sin φ)` at the dipped current `I_dip` and the starting power factor (proved in a test by running voltage-drop). `k_T = (|V_m|/E)²` is the torque factor of a passive start (not given for a regulated start, where torque follows current).
- PF correction: `P_in = P/η` (or `√3·U·I_n·cos φ` when `I_n` is entered, with a warning if the two differ by more than 5 %), `Q_c = P_in(tan φ₁ − tan φ₂)` where φ₁ is the motor's (a drive's displacement factor with a drive; zero, with a warning, when the target is not above it), `I_c = Q_c/(√3·U)`, running current before and after. Resonance: `S_sc = U²/|Z_s|`, `h ≈ √(S_sc/Q_c)`; `|Z_s|` stands in for `X_s`, so h reads 1 to 1.4 % low.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `source` | Source impedance from |  | Transformer | Fault level at the supply, Transformer | Setup |
| `fault_basis` | Fault level given as |  | Fault level S_k (MVA) | Fault level S_k (MVA), Fault current I_k (kA) | Setup |
| `starter` | Starter type |  | Direct on line (DOL) | Direct on line (DOL), Star-delta, Soft starter (current limit), Variable-frequency drive (current limit) | Setup |
| `cable_data` | Cable resistance from |  | resistivity and area | resistivity and area, R and X per km | Setup |
| `harmonics` | Harmonic loads present on the supply |  | 0 |  | Setup |
| `L_cab` | Cable length, source to motor (links conductor-sizing L) | m | 25 | 0 to 1000 | Geometry |
| `n_par` | Parallel conductors per phase |  | 1 | 1 to 6 | Geometry |
| `A_c` | Conductor cross-section (links conductor-sizing CSA) | mm² | 35 | 1 to 1000 | Geometry |
| `rho_20` | Resistivity at 20 °C (copper 0.01724, aluminium 0.0282 Ω·mm²/m) | Ω·mm²/m | 0.01724 | 0.01 to 0.05 | Material |
| `alpha` | Temperature coefficient of resistance | 1/K | 0.00393 | 0.003 to 0.0045 | Material |
| `X_km` | Reactance per km per conductor | Ω/km | 0.08 | 0 to 1 | Material |
| `R_km` | Resistance per km per conductor (cable datasheet or your own table) (optional) | Ω/km |  | 0.01 to 50 | Material |
| `U` | Supply and motor rated voltage (line-to-line) | V | 400 | 50 to 690 | Conditions |
| `T_c` | Conductor temperature for the resistance | °C | 70 | 20 to 90 | Conditions |
| `S_k` | Three-phase fault level at the point of supply (use the minimum fault level, which gives the deepest dip) | MVA | 10 | 0.5 to 500 | Conditions |
| `I_k` | Three-phase fault current at the point of supply (links fault-protection I_k3) | kA | 15 | 1 to 150 | Conditions |
| `S_n` | Transformer rating | kVA | 500 | 25 to 5000 | Conditions |
| `u_k` | Transformer impedance voltage | % | 4 | 1 to 12 | Conditions |
| `XR` | Source X/R ratio (of the transformer, or of the fault level) |  | 4.2 | 0.5 to 40 | Conditions |
| `XR_Q` | Upstream network X/R ratio |  | 10 | 1 to 50 | Conditions |
| `c_src` | Voltage factor c for the fault-level route, linked from the fault-protection c_k3 (optional; blank = 1.1 for the I_k basis, as fault-protection I_k3 is computed with, and 1.0 for a declared S_k) (optional) |  |  | 1 to 1.1 | Conditions |
| `S_kQ` | Upstream network fault level at the transformer primary, in series with the transformer, as the minimum fault level (optional; blank = infinite bus) (optional) | MVA |  | 1 to 5000 | Conditions |
| `k_soft` | Soft-starter current limit as a multiple of the rated current |  | 3.5 | 1 to 6 | Factors |
| `k_vfd` | Drive line-current limit as a multiple of the rated current |  | 1.5 | 0.5 to 2.5 | Factors |
| `cos_phi_vfd` | Drive line displacement power factor while starting |  | 0.95 | 0.5 to 1 | Factors |
| `dip_limit` | Dip limit at the motor terminals | % | 10 | 1 to 30 | Requirements |
| `cos_phi_target` | Target power factor after correction |  | 0.95 | 0.8 to 1 | Requirements |
| `dip_limit_pcc` | Dip limit at the point of common coupling (when the network operator sets one) (optional) | % |  | 0.5 to 20 | Requirements |
| `P` | Motor rated output (shaft) power, from the nameplate (motor-sizing P_cont is only a lower bound) | kW | 55 | 0.1 to 2000 | Motor |
| `eta` | Motor efficiency at rated load |  | 0.93 | 0.5 to 0.99 | Motor |
| `cos_phi` | Motor power factor at rated load |  | 0.88 | 0.5 to 1 | Motor |
| `I_LR_r` | Locked-rotor current ratio I_LR/I_n |  | 7 | 3 to 10 | Motor |
| `cos_phi_st` | Power factor at locked rotor (starting) |  | 0.3 | 0.1 to 0.8 | Motor |
| `I_n` | Motor rated current (when given it replaces the value from P, η and cos φ, for the starting current and the correction power) (optional) | A |  | 0.1 to 5000 | Motor |

## Assumptions

- The supply is a stiff source behind the stated impedance, with voltage E = U/√3 before the start. Basis: Stated limitation.
- A direct-on-line or star-delta start draws current through a passive winding. Basis: Stated limitation.
- A soft starter that does not reach its current limit at the dipped voltage (full conduction) does the same. Basis: Stated limitation.
- The passive winding is a constant impedance equal to the locked-rotor impedance at rated voltage. Basis: Stated limitation.
- The impedance is taken at the starting power factor, so the current falls with the dipped voltage. Basis: Stated limitation.
- A soft starter that reaches its limit and a drive regulate the current. Basis: Regulated-current starters.
- They hold |I| = I_start whatever the voltage, so the dip is deeper than the passive model gives. Basis: Regulated-current starters.
- The current phasor lags the voltage at the starter or drive input by the stated power factor. Basis: Regulated-current starters.
- The dip is the steady-state phasor value at the instant of locked rotor. Basis: Stated limitation.
- The current falls as the motor accelerates, so the dip is the worst case. Basis: Stated limitation.
- No other load is running on the supply. Basis: Stated limitation.
- No run-up dynamics, flicker (Pst) or tap-changer action is assessed. Basis: Stated limitation.
- A star-delta start draws one third of the direct-on-line current at rated voltage. Basis: Star-delta connection ratio.
- A star-delta start also gives one third of the starting torque, which is not checked here. Basis: Star-delta connection ratio.
- The current spike at the star-to-delta changeover is not modelled. Basis: Star-delta connection ratio.
- The cable resistance follows the voltage-drop method: from the resistivity, area and temperature, or from R′ per kilometre. Basis: User or record values.
- X′ per kilometre is a user or record value. Basis: User or record values.
- No cable table is embedded. Basis: User or record values.
- The capacitor kvar corrects the running power factor of the motor input power P/η at rated load. Basis: Standard power-factor-correction relation.
- The capacitor sits at the point of common coupling. Basis: Standard power-factor-correction relation.
- The resonance order uses the short-circuit apparent power U²/|Z_s| at that point. Basis: Standard power-factor-correction relation.
- Using |Z_s| for X_s makes h read 1 to 1.4 % low. Basis: Standard power-factor-correction relation.
- Capacitor switching, detuning and self-excitation on disconnection are not assessed. Basis: Standard power-factor-correction relation.
- The fault-protection loop quantities Z_s and I_f are not the positive-sequence source impedance, so they are not linked. Basis: Linking decision.
- The fault level links the fault-protection I_k3. Basis: Linking decision.
- Fault-protection computes I_k3 with c = 1.1 and U_n = √3·U_0 (398.4 V at 230 V). Basis: Linking decision.
- So the I_k basis defaults c to 1.1, and U should be √3·U_0 for an exact match. Basis: Linking decision.
- Load-budget declares no impedance port, so the transformer rating and impedance are entered. Basis: Linking decision.
- The source impedance should be taken at the minimum fault level, which gives the deepest dip. Basis: Stated limitation.
- A transformer alone sits on an infinite bus, so it understates the dip and overstates the resonance order. Basis: Stated limitation.
- Entering the upstream network fault level S_kQ removes this understatement. Basis: Stated limitation.
- With a drive, the power-factor correction starts from the drive's displacement factor, not the motor's. Basis: Regulated-current starters.
- The drive is itself a harmonic load. Basis: Regulated-current starters.
- With a regulated current, the voltage V_m is at the starter or drive input, not at the motor terminals. Basis: Regulated-current starters.
- The torque factor k_T is reported for that voltage only. Basis: Regulated-current starters.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `Z_src` | Source impedance (magnitude) | Ω |
| `Z_m` | Motor starting impedance (magnitude) | Ω |
| `I_rated` | Motor rated current | A |
| `I_start` | Starting current at rated voltage | A |
| `S_start` | Starting apparent power | kVA |
| `I_dip` | Starting current at the dipped voltage | A |
| `V_m` | Voltage at the motor terminals while starting | V |
| `V_pcc` | Point of common coupling voltage while starting | V |
| `start_dip` | Voltage dip at the motor terminals | % |
| `dip_pcc` | Voltage dip at the point of common coupling | % |
| `dV_cab` | Cable drop along the starting voltage | V |
| `k_T` | Torque factor at the dipped voltage |  |
| `u_dip` | Dip utilisation at the motor terminals |  |
| `P_in` | Motor input power at rated load | kW |
| `kvar` | Capacitor kvar to the target power factor | kvar |
| `I_cap` | Capacitor current | A |
| `I_run1` | Running current before correction | A |
| `I_run2` | Running current after correction | A |
| `S_sc` | Short-circuit apparent power at the bank | MVA |
| `h_res` | Parallel resonance order |  |
| `start_mode` | Starter state while starting |  |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `start.voltage_dip` | `start_dip` | % | Voltage dip at the motor terminals while starting, or at the starter or drive input for a soft starter in current limit or a drive |
| `start.capacitor_reactive_power` | `kvar` | kvar | Capacitor kvar to reach the target power factor |
| `start.current_start` | `I_start` | A | Starting current at rated voltage for the starter chosen, for the starting-current coordination check (not a design current) |
| `start.current_rated` | `I_rated` | A | Motor rated current, the running design current (→ conductor-sizing I_b_in) |

**Linking.** `I_k` takes fault-protection `I_k3` (kA), and since 1.2.0 `c_src` takes fault-protection `c_k3` (the factor `c_max` that `I_k3` was computed with), so the source impedance stays fault-protection's own `Z_T` whatever `c_max` is; the `c_src` warning is raised only when `c_src` is typed, not when it is linked. The linked `I_k3` is the fault level at the transformer terminals: the dip is a lower bound unless a supply cable or upstream network (`S_kQ`) is added. fault-protection computes it with c = 1.1 and `U_n = √3·U_0` (398.4 V at 230 V): the blank `c_src` already defaults to 1.1 on the I_k basis, a typed `c_src` other than 1.1 with a linked `I_k` warns (the fault level's voltage factor must match `c_src`; linking `c_src` from fault-protection `c_k3` removes the mismatch), and setting `U` to `√3·U_0` makes the source impedance equal fault-protection's `Z_T` exactly (tested). fault-protection `Z_s` and `I_f` are earth-fault-loop quantities (line to earth through the protective conductor), not the positive-sequence source impedance a motor start sees, so they are not linked. load-budget declares no impedance port (`P_dem`, `S_dem`, `I_3ph` only), so `S_n` and `u_k` are entered. `P` is the motor nameplate rating, entered: E1 `P_cont` is a load requirement and only a lower bound, and `P_pk` would inflate `I_n` and the dip. `A_c` and `L_cab` take conductor-sizing `CSA` and `L`; `I_rated` feeds conductor-sizing `I_b`.

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Motor-terminal dip against the limit
- Starter or drive input dip against the limit

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `cable-data` | Block | Conservative | The chosen cable-data route needs values that were not given. |
| `pfc` | Warning | Conservative | The target power factor is not above the motor's own, so no capacitor is needed. |
| `pfc-starter` | Warning | Conservative | Power-factor capacitors must not be connected on the output of a drive, or across a soft starter during the start. |
| `harmonics` | Warning | Unconservative | Harmonic loads are present (or a drive is used): the capacitor bank forms a parallel resonance with the supply at the stated order. |
| `current-limit` | Block | Conservative | The supply cannot hold a drive's regulated starting current: the source and cable drop alone exceeds the supply voltage. |
| `rated` | Warning | Conservative | The entered rated current and P/η disagree by more than 5 % in input power. |
| `c_src` | Warning | Conservative | A linked fault current needs the voltage factor c_src to match the c its fault level was computed with: a typed c_src other than 1.1 is flagged; linking c_src from the fault-protection step's c_k3 removes the mismatch. |

## Limitations

The source impedance should be the one at the minimum fault level (the deepest dip); a transformer alone is an infinite bus unless the upstream `S_kQ` is entered, which understates the dip by a few per cent and overstates the resonance order. A passive start is a constant impedance at the locked-rotor instant (the current falls as the motor accelerates, so the dip is the worst case); a regulated start holds the current. Low-voltage (≤ 1 kV) three-phase systems with a balanced source behind the stated impedance. The current spike at the star-to-delta changeover is not modelled. The capacitor sits at the PCC; switching transients, detuning, self-excitation and the capacitor voltage rating are not assessed; with a drive, the correction starts from the drive's displacement factor and the drive is itself a harmonic load. The dip limit and the network operator's PCC limit are yours to enter.

## References

- IEC 60909-0:2016, cl. 5.3: Voltage factor c and the equivalent voltage source
- IEC 61000-3-11:2017, cl. 1: Limitation of voltage changes and flicker in public low-voltage systems (limits are user inputs)
- Hughes, Electrical and Electronic Technology, Ch. on AC circuits, 12th ed. (edition to be checked): Complex impedance and the potential divider
- Hughes, Electrical and Electronic Technology, Ch. on power factor, 12th ed. (edition to be checked): Power-factor correction: Q = P (tan φ₁ − tan φ₂)

## Test cases

### Notes on the cases

All analytic and recomputed in python3 with complex arithmetic (Confirmed): `ka-pump-dol-transformer` (75 kW DOL on 500 kVA, 4 %: dip 7.7757 % motor, 4.6746 % PCC, 18.892 kvar, h 25.72), `ka-hand-phasor` (all three impedances at 53.13°: dip exactly 20 % and 10 %, |I| 461.88 A, `dV_cab` 40 V, cross-checked against voltage-drop's 40 V in a test), `ka-star-delta-fault-current` (I_k route with c = 1.1), `ka-soft-start-full-conduction` (a 5.5 × I_n limit on a weak 2 MVA supply is not reached, so the passive DOL dip of 25.976 % stands), `ka-soft-start-current-limit` and `ka-vfd-current-limit` (current held; solved in python by fixed-point iteration of the phasors and checked against the closed form: 9.1223 % and 2.4783 % at the starter or drive input), `ka-transformer-upstream-network` (315 kVA behind 20 MVA: 9.5523 % and 7.4333 %), `ka-pfc-resonance`. **Not pinned — owner check:** the Schneider Electric *Electrical Installation Guide* power-factor-correction example (the kvar-per-kW factor table and its worked example); no inputs and answer are quoted here because they could not be stated with confidence. The relation it uses, `Q_c = P(tan φ₁ − tan φ₂)`, is covered by the analytic cases above.

- `ka-pump-dol-transformer`: 75 kW pump motor, direct on line, on a 500 kVA 4 % transformer, 40 m of 50 mm² copper (analytic) (Confirmed; source: Analytic (complex divider, recomputed with Python complex arithmetic))
- `ka-hand-phasor`: Hand phasor case: Z_s = Z_c = 0.03 + j0.04 Ω, Z_m = 0.24 + j0.32 Ω, all at 53.13° (analytic) (Confirmed; source: Analytic (complex divider; all three impedances share one angle))
- `ka-star-delta-fault-current`: 30 kW star-delta start from a 12 kA fault current (c = 1.1), 60 m of 25 mm² (analytic) (Confirmed; source: Analytic (complex divider, recomputed with Python complex arithmetic))
- `ka-soft-start-current-limit`: 132 kW soft start holding 3 × I_n on an 8 MVA supply, 80 m of 95 mm² (analytic, current source) (Confirmed; source: Analytic (regulated current: E = V_m + I·(Z_s + Z_c), solved by fixed-point iteration of the phasors in Python and checked against the closed form))
- `ka-soft-start-full-conduction`: 75 kW soft start with a 5.5 × I_n limit on a weak 2 MVA supply: the limit is not reached (analytic) (Confirmed; source: Analytic (passive locked-rotor divider; the held-current solution is used only when the fully conducting current exceeds the limit))
- `ka-vfd-current-limit`: 37 kW drive holding 1.5 × I_n at 0.92 displacement power factor on a 5 MVA supply, 30 m of 16 mm² (analytic, current source) (Confirmed; source: Analytic (regulated current: E = V_m + I·(Z_s + Z_c), solved by fixed-point iteration of the phasors in Python and checked against the closed form))
- `ka-transformer-upstream-network`: 55 kW pump direct on line on a 315 kVA 4 % transformer behind a 20 MVA upstream network, 30 m of 35 mm² (analytic) (Confirmed; source: Analytic (complex divider, recomputed with Python complex arithmetic))
- `ka-pfc-resonance`: 22 kW motor, 0.83 to 0.95, on a 6 MVA bus: capacitor kvar, current and resonance order (analytic) (Confirmed; source: Analytic (Q_c = P_in (tan φ₁ − tan φ₂)))

## Changes

**Changes in 1.3.0 (C-2 consistency, phase C).** Layout and wording; no result changes. `cos_phi2` is `cos_phi_target` (the old symbol stays as an alias). The dip checks compare physical values: `start_dip` with `dip_limit`, and `dip_pcc` with `dip_limit_pcc`. The method is eight lines. The inputs are grouped Setup, Geometry, Material, Conditions, Factors, Requirements and Motor. The ports are named `start.*`, and the record kind `cable.impedance` is titled "Cable impedance (resistance and reactance per kilometre)". The scope, the assumptions and the messages follow the sheet standard, and the references name the edition.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 75 kW pump motor started direct on line from a 500 kVA transformer ([sheet](../../worked-examples/electrical/elec.motor-start/pump-dol-500kva/sheet.txt)).

```text
I_rated = 75 kW / (sqrt(3) × 400 V × 0.87 × 0.945) = 131.67 A
I_start = 7 × 131.67 A = 921.7 A
S_start = sqrt(3) × 400 V × 921.7 A = 638.57 kVA
Z_T = 4 % × (400 V)^2 / 500 kVA = 0.0128 ohm
R_src = 0.0128 ohm / sqrt(1 + 4.2^2) = 0.0029647 ohm
X_src = 0.0029647 ohm × 4.2 = 0.012452 ohm
Z_src = hypot(0.0029647 ohm, 0.012452 ohm) = 0.0128 ohm
rho_T = 0.01724 ohm·mm²/m × (1 + 0.00393 1/K × (70 degC − 20 degC)) = 0.020628 ohm·mm²/m
R_cab = 0.020628 ohm·mm²/m × 40 m / (50 mm² × 1) = 0.016502 ohm
X_cab = 0.08 ohm/km × 40 m / 1 = 0.0032 ohm
Z_m = 400 V / (sqrt(3) × 921.7 A) = 0.25056 ohm
R_m = 0.25056 ohm × 0.3 = 0.075168 ohm
X_m = 0.25056 ohm × sqrt(1 − 0.3^2) = 0.23902 ohm
E_ph = 400 V / √3 = 230.94 V
… 25 more step(s) on the sheet
Governing: PASS  Point-of-common-coupling dip against the limit: 4.675 % vs 5 %  utilisation 0.93  « IEC 61000-3-11 cl. 1
```
<!-- hand-arithmetic:end -->
