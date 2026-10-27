# Shaft design (`mech.shaft@1.7.0`)

**In plain terms.** A shaft carries gears, pulleys and bearings, so it bends and twists, and every turn the bending stress reverses, which is why shafts fail by fatigue at shoulders and keyways. The worksheet takes the stepped shaft, its loads and torque, finds the bending moment, slope and deflection along it, the fatigue and yield safety factors at the sections you mark, the keys, and the speed at which the shaft would whirl.

## Scope

Calculates the bending moment, slope, deflection, fatigue and yield safety factors, critical speed and keys of a stepped shaft on two bearings. It takes radial loads in two planes and a steady torque with an optional alternating part.

The shaft may be hollow. It carries radial loads in two planes and a torque with a steady (mean) part and an optional alternating part. The sheet gives the bending moment, slope and deflection along the shaft, the fatigue and yield factors at the sections the user marks, the slopes at the bearings, the deflection at the loads, the first lateral critical speed and parallel keys.

## Method

The shaft is an S2 beam (`Tempest.Calc.Structural.Frame`, pin at A, roller at B) solved once per plane; M, slope and deflection combine as vectors. `K_f = 1 + q(K_t − 1)`; `σ_a = K_f 32 M d/(π(d⁴ − d_i⁴))`, `τ_m = K_fs 16 T d/(π(d⁴ − d_i⁴))` on the smaller side of a shoulder. `S_e = k_a k_b k_c k_d k_e k_f S_e1`, `S_e1` (the rotating-beam limit S_e′) `= 0.5 S_ut` (≤ 1400 MPa, else 700); `k_a = a S_ut^b` (ground, machined, hot-rolled, forged; 10th ed. Table 6-2 coefficients, overridable by a `shaft.marin` record — the 11th edition revised them), `k_b = 1.24 d^−0.107` (≤ 51 mm) or `1.51 d^−0.157` (to 254 mm, held above), `k_c = 1`, `k_e = 1 − 0.08 z_R` (normal quantile). DE-Goodman with the torque split into a steady part `T_m` (input `T`) and an alternating amplitude `T_a` (input `T_a`, 1.2.0; blank = 0), bending fully reversed: `1/n_f = √(σ_a² + 3τ_a²)/S_e + √3 τ_m/S_ut` with `τ_a = K_fs 16 T_a d/(π(d⁴ − d_i⁴))` (with `T_a` = 0 this is the earlier `σ_a/S_e + √3 τ_m/S_ut`; with `T_m` = 0 the torque is fully alternating); `n_y = S_y/√(σ_a² + 3τ_pk²)`, where `τ_pk` is built from the larger of `T_m + T_a` and the optional peak torque `T_pk` (blank: `T_m + T_a`), so the first-cycle yield check sees the peak torque (Shigley 10th ed. §6-9, §7-4). **The key is checked on the same peak torque** (1.2.0; it used `T` alone): `τ = 2T_k/(d w L)`, `σ = 4T_k/(d h L)`, `L_req` with `T_k` = the larger of `T_m + T_a` and `T_pk`. Rayleigh `ω = √(g Σm y/Σm y²)` with shaft self-mass and carried masses; Dunkerley `1/ω² = Σ m δ_ii` (§7-6); **the ratio `r_c` and the warning use the Dunkerley lower bound** (the Rayleigh value is an upper bound), warning when `n_c,D/n < 2`. When nothing deflects (ρ = 0 and every carried mass on a bearing, or no mass at all) no critical speed is found and a warning (`critical-speed-skipped`) says why. Keys `τ = 2T_k/(d w L)`, `σ = 4T_k/(d h L)`, `L_req = max(2T_k n_target/(d w 0.577 S_y), 4T_k n_target/(d h S_y))` at the target factor (§7-7). A key given only some of `x_key`, `w`, `h`, `L_key` warns (rule `key`) instead of dropping the check.

**Reference diagram.** The worksheet draws a stepped shaft on bearing A (pin) and bearing B (roller). It binds `segments`, `loads` and `sections` to representative shapes and `x_A`, `x_B` and `x_T1` to positions. `T` and `n` are arcs. `S_ut`, `S_y`, `E`, `finish` and `n_target` are labels. The drawing is not to scale (ADR-0158, DI-08).

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `segments` | Shaft segments (left to right) | table | Setup |
| `loads` | Radial loads and masses (a carried mass adds no weight unless w_masses is on: enter its weight as a force in F_y) | table | Setup |
| `sections` | Sections to check | table | Setup |
| `x_A` | Bearing A position | mm | Geometry |
| `x_B` | Bearing B position | mm | Geometry |
| `S_ut` | Ultimate tensile strength | MPa | Material |
| `S_y` | Yield strength | MPa | Material |
| `E` | Young's modulus | GPa | Material |
| `rho` | Density (shaft self-mass in the critical speed) | kg/m³ | Material |
| `T` | Torque transmitted, steady or mean part T_m | kN·m | Loads |
| `x_T1` | Torque enters at | mm | Loads |
| `T_a` | Alternating torque amplitude about the mean (optional; blank = 0, a steady torque; a reversing or cyclic drive needs it) | kN·m | Loads |
| `T_pk` | Peak torque for the yield check and the key (optional; blank = T + T_a) | kN·m | Loads |
| `x_T2` | Torque leaves at (optional; blank = the shaft length, so every section beyond x_T1 carries the torque) | mm | Loads |
| `w_masses` | Add the weight of the carried masses and of the shaft itself as downward vertical loads (optional; blank = refused when a mass is carried; off: the loads table is the whole load, the weights are entered as forces and the masses enter the critical speed only) | yes or no | Loads |
| `n` | Operating speed | rpm | Conditions |
| `finish` | Surface finish | choice | Factors |
| `R_rel` | Reliability | % | Factors |
| `k_d` | Temperature factor | none | Factors |
| `k_f` | Miscellaneous factor | none | Factors |
| `k_a` | Surface factor override (optional; blank = a·S_ut^b, Shigley 10th ed. Table 6-2) | none | Factors |
| `n_target` | Target safety factor | none | Requirements |
| `r_c_min` | Smallest ratio of the first critical speed to the operating speed | none | Requirements |
| `theta_max` | Slope limit at the bearings | rad | Requirements |
| `y_max` | Deflection limit at the loads | mm | Requirements |
| `x_key` | Key position | mm | Key |
| `w` | Key width | mm | Key |
| `h` | Key height | mm | Key |
| `L_key` | Key length | mm | Key |
| `S_y_key` | Key yield strength | MPa | Key |
| `S_y_hub` | Hub yield strength (optional; blank = the hub is not checked; the key bearing is checked against the weakest of key, shaft and hub) | MPa | Key |

The three tables have these columns. `segments`: `x_end` (1 to 3000 mm), `d` (5 to 300 mm) and the optional bore `d_i`. `loads`: `x`, `F_y` and `F_z` (kN) and the carried mass `m` (kg). `sections`: `x`, `K_t`, `K_ts`, `q` and `q_s`. The record kinds are `shaft.material` (`S_ut`, `S_y`, `E`), `shaft.marin` (`k_a`) and `shaft.limits` (`theta_max`, `y_max`).

`R_rel` was called `R` before 1.7.0. The old symbol still loads, so a saved calculation, an inputs file, a CSV file and a pack binding written with it give the same values.

Links in: the drive-train and mechanism-review packs link the torque and speed of `elec.motor-sizing` to this sheet: the load torque `T_load` as `T`, the torque swing `T_alt` as `T_a`, and the peak `T_pk` as `T_pk`. They do not link the RMS torque `T_rms`, which is a thermal figure. Before 1.2.0 the packs bound it to `T` as a steady torque, and the shaft saw no alternating torque.

## Assumptions

- The shaft is linear elastic on two simple bearing supports, a pin at A and a roller at B.
- Bending is fully reversed by rotation.
- The torque is a steady part `T_m` plus an alternating part `T_a`, acting between the torque-in and torque-out positions.
- The critical speed is the first lateral mode, by Rayleigh with Dunkerley as a lower bound.
- `K_t` and `K_ts` come from user records or published fit equations.
- At a shoulder both sides are checked, each on its own diameter and bore, and the lower factor governs.
- The load factor `k_c` is 1, because the stresses are combined by von Mises.
- The loads table is the whole load unless `w_masses` is on, which adds the weights.
- Loads, sections and steps closer together than 1/200 of the shaft length share one node.

## Outputs

`R_A`, `R_B`, `d_A`, `d_B`, `M_max`, `theta_A`, `theta_B`, `y_load`, `S_e`, `n_f_min`, `n_y_min`, the per-section steps `X1` and so on, `n_c`, `n_c_D`, `r_c`, and for a key `tau_key`, `sigma_key`, `L_key_req` and `u_key`.

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `shaft.reaction_a` | `R_A` | kN | Radial reaction at bearing A (to `mech.bearing-life` `F_r`) |
| `shaft.reaction_b` | `R_B` | kN | Radial reaction at bearing B (to `mech.bearing-life` `F_r`) |
| `shaft.seat_diameter_a` | `d_A` | mm | Seat diameter at A (to `mech.iso286-fit` `D`) |
| `shaft.seat_diameter_b` | `d_B` | mm | Seat diameter at B (to `mech.iso286-fit` `D`) |
| `shaft.safety_fatigue_min` | `n_f_min` | none | Lowest fatigue safety factor |
| `shaft.speed_critical` | `n_c` | rpm | First critical speed |

Each port keeps its output symbol as an alias, so a link, a pack binding or an inputs file written before the port names still resolves.

## Checks

- Fatigue safety factor against target (DE-Goodman): `n_target` against `n_f_min`.
- Yield safety factor against target: `n_target` against `n_y_min`.
- Slope at the bearings against limit: the larger bearing slope `theta_bearing` against `theta_max`.
- Deflection at the loads against limit: `y_load` against `y_max`.
- Critical speed against the operating speed: `n_c_req` (`r_c_min` times the operating speed) against `n_c_D`. For a running shaft with no critical speed found, this check fails.
- Key length against required: `L_key_req` against `L_key`, made when the key and its yield strength are entered.
- Blocks: `geometry`, `strength`, `mass-weight`. Warnings: `critical-speed`, `critical-speed-skipped`, `torque-span`, `key-hub`, `key`, `size-factor`.

## Limitations

Linear elastic; two simple supports (the bearings must be at least 1/200 of the shaft length apart, else the sheet blocks in its own words); no torsional vibration; first lateral mode only; K_t from records or fit equations, never a digitised chart.

## References

- Budynas, *Shigley's Mechanical Engineering Design*, Ch. 6 (§6-9, Table 6-2 Marin surface factors) and Ch. 7 (§7-4 to §7-7), 10th ed.: fatigue, shaft stresses, deflection, critical speeds, keys.
- Mott, *Machine Elements in Mechanical Design*, Ch. 12, 6th ed.: shaft design.

## Test cases

`ka-simply-supported` (slope 2.430 mrad, deflection 0.3240 mm, n_c 5305.2 rpm — analytic, Confirmed), `ka-key` (τ 36.99, σ 84.56 MPa — analytic, Confirmed), `ka-shigley-7-1` (S_e 201.50 MPa, n_f 1.621, n_y 4.489 with the example's data converted to SI exactly — **Unconfirmed** until compared with the book's printed answers). Mott 6th ed. shaft examples are not pinned (owner copy needed). 1.2.0: alternating torque T_m 50 + T_a 150 N·m on the Example 7-1 shaft with a 28.48 N·m bending moment (n_f 2.1325 against 6.9987 for the 50 N·m taken alone, n_y 4.9555), fully alternating 124.28 N·m (n_f 1.5999 against 1.6210 as a steady torque), and the key on T_pk, all from the formulas above in python3.

## Changes

**Changes in 1.7.0 (C-2).** `R` is renamed `R_rel` (the old symbol is an alias). The three tables are in the `Setup` group, and the other inputs are in the groups Geometry, Material, Loads, Conditions, Factors, Requirements and Key, with the optional inputs last in each. Every number has a usual range inside its hard range (soft limits). The six checks of strength, slope, deflection and key show physical values (the new step `theta_bearing` is the larger bearing slope). Every description and message is rewritten in short sentences, and the references are cited in the standard form. No result changes.

**Changes in 1.3.1 (mech.shaft-02, from the C-1 verification).** Each section line prints its yield safety factor again, `n_y = …`, as the governing side's value (the side with the lower fatigue factor), and names the other side's value when that is lower and sets `n_y_min`; 1.3.0 had dropped it when the shoulder was checked on both sides. Output text only; no number changes. The test of -01 now pins the sheet's own Rayleigh value of 7246.6 rpm at 0.1 % (the finite-element value 7189.7 rpm is 0.8 % below it, as an upper bound should be; a continuous-beam quadrature of the same signed quotient gives 7263.8 rpm, python3).

**Changes in 1.3.0 (mech.shaft-01 to -05).** -01: the Rayleigh quotient uses the signed static deflection, so an overhung mass that lifts the span no longer inflates the numerator (bearings at 100 and 500 mm, 40 mm shaft of 700 mm, 5 kg at the tip: `n_c` 7975.0 rpm before, 7246.6 rpm now against the consistent-mass eigenvalue 7189.7 rpm; Dunkerley 7006.7 rpm stays below). -02: a shoulder is checked on both sides, each on its own diameter and bore, and the lower fatigue safety factor governs (Ø50 with a Ø46 bore beside Ø40 solid, 2 kN at the shoulder: `n_f` 5.3065 before, 2.8700 now). -03: `x_T2` has no 400 mm default; blank is the shaft length (L 800 mm, T 1 kN·m, section at 600 mm: `n_f` 5.3065 with no torque there, 2.3914 now), and a torque with a section or key outside `x_T1 … x_T2` warns (`torque-span`). -04: the carried-mass column and the shaft's own weight add no weight unless the new switch `w_masses` is on, which loads the vertical plane with m·g at the nodes (20 kg at mid-span with 2 kN: `M_max` 200 N·m, 219.61 N·m with the switch); a carried mass with the switch off warns (`mass-weight`) and the label and assumption say so. -05: the key bearing uses the lowest yield strength of the key, the shaft and the new optional `S_y_hub` (shaft 250 MPa, key 400 MPa, T 1.2 kN·m, Ø50, 14 × 9 key, n_target 2: `L_key_req` 53.33 mm before, 85.33 mm now; a 200 MPa hub gives 106.67 mm); without a hub strength the key check warns (`key-hub`).

**Changes in 1.5.1 (merge of the C-1 line's 1.3.1 and the C-2 line's 1.5.0).** The C-1 line had 1.3.1 (the section lines print `n_y` again) and the C-2 line 1.5.0 (the critical-speed check, the title and the public port names); the merged template is 1.5.1, above both. No number changes from the merge.

**Changes in 1.5.0 (C-2 fix, M1; shipped as part of 1.5.1).** `critical-speed`: the first critical speed (Dunkerley) must be at least twice the operating speed; below that the new check "Critical speed against the operating speed" fails (input `n_c_req = 2·n`, capacity `n_c_D`), where it only warned before. Every other value is unchanged.

**Changes in 1.6.0 (C-2 fix, F2 and F7).** A carried mass has a weight, and the sheet no longer leaves it out in silence: `w_masses` is optional with **no default**, and a mass carried with `w_masses` blank blocks (`mass-weight`; it warned and passed with the weight out of the reactions, the moment and the deflection). On adds the weights of the masses and of the shaft; off says the loads table is the whole load, the weights entered as forces, and the masses enter the critical speed only. A saved instance with the switch set reads as before; one that never set it and carries a mass blocks until it does. With no mass carried a blank switch is fine. The margin of the critical-speed check is an input, `r_c_min` (smallest ratio of the first critical speed to the operating speed, default 2, usual 1.5 to 4, hard 1 to 10): the check "Critical speed against the operating speed" compares `n_c,D` with `r_c_min` × the operating speed (default 2: results unchanged). When no critical speed is found (every mass on a bearing and ρ = 0; `critical-speed-skipped`, now declared unconservative) a **running** shaft (n above 0) fails that check, "no critical speed found", where the check was left out and the sheet passed; a shaft at rest is not failed on it. The known-answer case `ka-simply-supported`, the gearbox worked example and the drive chain's M4 step record `w_masses` off, as they were computed.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Gearbox input shaft: pinion between bearings, coupling overhung ([sheet](../../worked-examples/mechanical/mech.shaft/gearbox-input-shaft/sheet.txt)).

```text
R_A = √(1.1² + -3²) = 3.1953 kN
R_B = √(1.1² + -3²) = 3.1953 kN
d_A = x = 20 mm = 35 mm
d_B = x = 280 mm = 35 mm
M_max = 37 stations = 0.41539 kN·m
theta_A = x = 20 mm = 0.0008961 rad
theta_B = x = 280 mm = 0.0008961 rad
y_load = 0.071775 mm at 150 = 0.071775 mm
k_a = 4.51 × 700^-0.265 (machined or cold-drawn) = 0.79474
k_e = 1 − 0.08 × 2.3263 = 0.81389
S_e1 = 700 MPa = 350 MPa
X1 = x = 120 mm, d = 40 mm, k_b = 0.83561, S_e = 189.17 MPa, K_f = 1.56, K_fs = 1.45, M = 319.53 N·m, T = 0 N·m, σ_a = 79.334 MPa, τ_m = 0 MPa, n_y = 6.3025 (shoulder: the side with d = 40 mm governs, of 2) = 2.3845
X2 = x = 150 mm, d = 45 mm, k_b = 0.82514, S_e = 186.8 MPa, K_f = 1.912, K_fs = 2.8, M = 415.39 N·m, T = 300 N·m, σ_a = 88.778 MPa, τ_m = 46.947 MPa, n_y = 4.1532 = 1.6909
X3 = x = 180 mm, d = 40 mm, k_b = 0.83561, S_e = 189.17 MPa, K_f = 1.56, K_fs = 1.45, M = 319.53 N·m, T = 300 N·m, σ_a = 79.334 MPa, τ_m = 34.616 MPa, n_y = 5.0281 (shoulder: the side with d = 40 mm governs, of 2) = 1.9801
… 17 more step(s) on the sheet
Governing: PASS  Slope at the bearings against limit: 0.0008961 rad vs 0.001000 rad  utilisation 0.90
```
<!-- hand-arithmetic:end -->
