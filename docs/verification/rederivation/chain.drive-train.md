# chain.drive-train — independent review of the linked chain (Track P, P1)
Pack: `pack.drive-train`, "Drive train". Source file: src/Tempest.Calc/Tempest.Calc.Library/CalcPackTemplates.cs. Chain tests: tests/Tempest.Calc.Tests.Library/Chains/DriveTrainChainTests.cs.
Steps: E1 elec.motor-sizing → M4 mech.shaft → M5 mech.bearing-life → M2 mech.iso286-fit.
Script: none. I fed each step by hand with the probe. The inputs are in /tmp/claude-0/review-inputs/dt_*.json.
Verdict: VALUES TRANSFER CORRECTLY; THE TORQUE LINKS ARE WRONG IN MEANING (one Critical, three High, four Medium; reviewed at 298ade36)

Each link copies its value exactly, with the unit converted through SI. The problem is what the values mean. E1 gives RMS torque at the motor shaft. M4 reads it as the steady torque in the shaft it checks. That is unconservative in two ways. The axial thrust of a screw drive never reaches the bearing.

## Method re-derived
- Bindings: E1 T_rms → M4 T; E1 n_pk → M4 n; M4 R_A → M5 F_r; E1 n_pk → M5 n; M4 d_A → M2 D.
- E1 torques are at the motor shaft. T_pk = J_total·α + T_load. T_dec = −J_total·α + T_load. T_rms is the thermal RMS over move plus dwell.
- A shaft after a ratio i carries the output torque. At the output, T_out = J_L·α_m/i + T_L during acceleration. On the motor side, the motor torque times i·η gives about the same, less the rotor's own share.
- M4 treats T as steady (its assumption 1). Fatigue is DE-Goodman with σ_a from bending and τ_m from T. Yield uses √(σ_a² + 3τ_m²).
- A reversing servo cycle runs from +T_pk to T_dec. So T_a = (T_pk − T_dec)/2 and T_m = (T_pk + T_dec)/2. DE-Goodman then needs σ′_a = √(σ_a² + 3τ_a²) over S_e, plus σ′_m over S_ut (Shigley 11th ed., §7-4). Yield and key checks need the peak torque.
- M5 life in hours is L_10h = L_10·10⁶/(60 n). The right n is the mean speed of the bearing over the cycle. Higher n gives fewer hours, so peak speed is conservative.
- M4 gives R_A = √(R_A,y² + R_A,z²). It is a magnitude and cannot be negative.

## Known-answer cases
The chain has no known-answer case. The two chain tests check transfer only.

| Test | What it asserts | What it does not assert |
|---|---|---|
| Motor_Torque_And_Speed_Reach_The_Shaft_Bearing_And_Seat | Each bound input equals its source output in SI to 9 places. No step blocks. | Whether T_rms is the right torque for M4. Which shaft M4 is. Whether n_pk is the right speed for M5. |
| Changing_The_Motor_Makes_Every_Step_Stale_And_Updating_Carries_It_Through | Doubling m makes M4, M5 and M2 stale. Updating in order clears it. | That M4's radial loads should change with the torque (see -06). |

Both tests pass by design. Neither would catch findings 01, 02 or 05.

## Extra probes (chain fed by hand)
| Probe | Step and inputs (short) | Symbol | Chain value | Correct value | Effect |
|---|---|---|---|---|---|
| C1 | E1 direct rotary, J_L 2 kg·m², 0.25 rev in 0.3 s, dwell 0.7 s | T_pk / T_dec / T_rms | 174.93 / −174.93 / 78.23 N·m | same | E1 is right |
| C1 | M4 d 28 mm, 1 kN at mid-span (M 100 N·m), K_t 1.7, K_ts 1.5, q 0.85, q_s 0.9, S_ut 600, S_y 450 MPa | n_f | 2.009 (T = T_rms as steady) | 1.393 (T_a = 174.93 N·m) | Pass at target 1.5 becomes fail |
| C1 | same | n_y | 5.18 | 3.57 (T = T_pk) | Overstated by 45 % |
| C2 | E1 gearbox rotary, i 10, η 0.85 (ka-geared-index-table inputs) | T_pk / T_rms (motor) | 5.013 / 2.242 N·m | output peak 0.5 × 785.4/10 = 39.27 N·m | 17.5 times the bound value |
| C2 | M4 d 20 mm, 0.55 kN at mid-span (M 55 N·m), same section and material | n_f | 1.613 (T = 2.24 N·m) | 1.42 (T_a = 39.27 N·m); 1.39 even as steady 39.27 N·m | Pass becomes fail |
| C3 | M5 F_r = R_A 0.275 kN, C 20 kN, ball | L_10h | 8.55e6 h at n_pk 750 rpm | 4.27e7 h at the mean motor speed 150 rpm; 4.27e8 h at the mean output speed 15 rpm | Conservative, 5 to 50 times |
| C4 | M2 D = d_A 20 mm, pack defaults | fit | H7/g6, clearance 7.3 to 41.3 µm, check "pass" | Seat fit not assessed | See -07 |
| C5 | M4 to M5 sign | R_A | 0.275 kN, always ≥ 0 | same | No defect |

## Implementation comparison
| Aspect | Engineering need | Pack | Agrees? |
|---|---|---|---|
| Torque for shaft yield and keys | Peak torque at the shaft | T_rms at the motor | No, see -01 and -02 |
| Torque for shaft fatigue | Alternating and mean parts of a reversing cycle | T_rms as a steady mean | No, see -01 |
| Ratio between motor and shaft | T·i·η, n/i | None | No, see -02 |
| Speed for critical speed (M4 n) | Highest shaft speed | n_pk at the motor | Conservative |
| Speed for bearing life (M5 n) | Mean bearing speed | n_pk at the motor | Conservative, see -03 |
| Bearing radial load | Reaction from loads that follow the torque | R_A from fixed user loads | Partly, see -06 |
| Bearing axial load | Screw or helical thrust | F_a left at 0 | No, see -05 |
| R_A sign | F_r ≥ 0 | Magnitude ≥ 0 | Yes, see -04 |
| Seat fit | Bearing bore tolerance and a maker's shaft class | ISO 286 H7/g6 defaults | No, see -07 |
| Bearings covered | A and B | A only | Partly, see -08 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 614ddf89 | Status @ e283c07e | Status @ 298ade36 |
|---|---|---|---|---|---|---|---|---|
| P1-chain.drive-train-01 | Critical | Critical (still). The binding is unchanged. E1's default duty is point-to-point, and C1 reproduces at 298ade36. The continuous example only hides the defect. | E1 T_rms is bound to M4 T, which M4 treats as steady torque. Yield and key stresses never see T_pk. DE-Goodman sees no alternating torque, although E1's own cycle reverses (T_dec < 0). | C1: product n_f 2.009 and n_y 5.18, both pass. With T_a = T_pk = 174.93 N·m, n_f is 1.393 (fails 1.5) and n_y is 3.57. Key stress also scales with T_rms, here 2.24 times low. | Give M4 an alternating torque input and a peak torque input. Bind T_a = (T_pk − T_dec)/2, T_m = (T_pk + T_dec)/2 and T_pk from E1. Until then, remove the T_rms → T binding or block the pack with a notice. | Open. Pack bindings unchanged. Probe: M4 with T_rms as steady torque still gives n_f 2.009 and n_y 5.18. | Open. Probe: M4 with T_rms 78.23 N·m as steady torque still gives n_f 2.009 and n_y 5.177. Bindings unchanged. | Open (still Critical). Binding unchanged. Probe: C1 inputs still give n_f 2.009 and n_y 5.18 on T_rms. The example with E1 set to point-to-point gives T_rms 108.25 against T_pk 177.90 N·m. M4 n_y is then 18.72 against 11.91 (57 % high). |
| P1-chain.drive-train-02 | Critical | High (downgraded). The example puts M4 on the motor side, where the unscaled torque is right. A shaft after the ratio still gets motor torque with no notice. That is a missing guard on an unconservative pass. | E1 torques are at the motor shaft. The binding applies no ratio or efficiency. Any shaft after a gearbox or belt carries about T·i·η. E1 offers gearbox, belt and rack ratios, and the pack does not say which shaft M4 is. | C2: i 10, η 0.85. The bound T is 2.24 N·m. The output shaft sees ±39.27 N·m peak. n_f falls from 1.613 (pass) to 1.42 (fail). | Add output-side ports to E1 (T_out,pk, T_out,a, T_out,m, n_out). Bind those. State in the pack text that M4 is the output shaft. | Open. Probe: geared case still binds T_rms 2.24 N·m; M4 gives n_f 1.613 against 1.39 at 39.27 N·m. | Open. Probe: the geared case still binds T_rms 2.24 N·m. M4 gives n_f 1.613 against 1.39 at the output peak. | Open (High). The binding applies no ratio. Probe: the example M4 at the output torque 1800 N·m and 120 rpm fails (n_f 1.47, n_y 1.21). The bound 151.58 N·m passes (n_f 6.99, n_y 13.83). The pack text still does not say which shaft M4 is. |
| P1-chain.drive-train-03 | Medium | Medium (first review; conservative). | E1 n_pk feeds M5 n. Bearing life in hours needs the mean speed over the cycle, and the output speed after a ratio. Peak speed is conservative, by a large factor for servo cycles with dwell. | C3: 750 rpm peak against 150 rpm mean (motor) and 15 rpm mean (output). L_10h is understated 5 to 50 times. | Add n_mean = θ_m/(2π·t_cycle) to E1, and its output-side value. Bind that to M5 n. Keep n_pk for the M4 critical speed. | Open. Probe: M5 still gets n_pk; L_10h 8.55e6 h at 750 rpm against 4.27e7 h at 150 rpm. | Open. Probe: M5 at n_pk 750 rpm gives L_10h 8.55e6 h. At 150 rpm it gives 4.27e7 h. | Changed. Continuous duty gives a constant n_pk of 1500 rpm, so peak equals mean. M5 life is 75545 h. A servo cycle is still conservative. |
| P1-chain.drive-train-04 | Info | Info (first review). | The suspected sign clash between M4 R_A and M5 F_r does not exist. R_A is the vector magnitude √(R_A,y² + R_A,z²). M5 F_r accepts 0 and up. | ShaftCalculator records R_A from both planes as a root sum of squares. C5: 0.275 kN. | None. | Open (Info, no defect). Probe: R_A 0.275 kN, never negative. | Open (Info, no defect). Probe: R_A is 0.275 kN, never negative. | Open (Info, no defect). Probe: R_A 1.346 kN, never negative. |
| P1-chain.drive-train-05 | High | High (first review). Still holds for a screw or rack E1. | A screw or rack drive puts axial thrust on the bearings. The pack leaves M5 F_a at its default 0. E1's default mechanism is the ball screw, so the default chain models a screw support bearing with no thrust. | ka-ball-screw: F_load 49.0 N plus m·a 450 N gives about 499 N peak thrust during acceleration. M5 sees F_a = 0. | Add an axial-force port to E1: peak force, and the revolution-weighted cube mean for ISO 281. Bind it to M5 F_a or its duty table. Warn when E1 is a linear mechanism and F_a is not bound. | Open. M5 F_a is still not bound. | Open. M5 F_a is still not bound. | Changed. The example mechanism is a gearbox rotary drive with no thrust. A screw or rack mechanism still leaves M5 F_a at 0. |
| P1-chain.drive-train-06 | Medium | Medium (first review). | M4's radial loads are user rows, not linked to the torque. Gear and belt forces scale with torque (F_t = 2T/D). When the motor case changes, T updates but R_A and the bending moment do not. The staleness test then reports the chain current. The static check s_0 also sees only the steady load, not the peak tooth or belt force at T_pk. | Chain test 2 doubles m. M4 T changes, but R_A and M stay the same. | Add a torque-derived load row to M4 (2T/D at a position and angle). Or warn on the pack that the loads must be re-entered when E1 changes. | Open. M4 loads are still user rows. | Open. The M4 loads are still user rows. | Open. The M4 loads are still typed rows. See D2 for the example's pinion loads. |
| P1-chain.drive-train-07 | Medium | Medium (first review). | The seat-fit step uses M2 defaults H7/g6, a clearance fit. A bearing bore follows the bearing tolerance standard (ISO 492, 0 to negative), not H7. A rotating inner ring under a rotating load normally needs a transition or interference shaft class from the maker. The step still shows "pass". | C4: D 20 mm gives clearance 7.3 to 41.3 µm, "Fit classified" pass. Owner to confirm the maker's recommended classes against licensed data. | Default the pack's M2 to the bearing bore deviations from a bearing record and a maker's shaft class. Or warn that the seat fit is not assessed. | Open. Probe: M2 D 20 mm still reports H7/g6 'Fit classified' pass. | Open. Probe: M2 at D 20 mm still reports H7/g6 "Fit classified" as a pass. | Partly fixed (6c46e861). The example M2 is H7/k6, a transition fit (i_max 17.6 µm). The hole is still H7, not a bearing bore tolerance. The template default is unchanged. |
| P1-chain.drive-train-08 | Low | Low (first review). | Only bearing A is checked. R_B and d_B are not used. With an overhung pulley, B carries the larger load. | M4 publishes R_B and d_B as ports, but the pack binds neither. | Add M5 and M2 steps for bearing B, or bind the larger reaction. | Open. Only bearing A is still bound. | Open. Only bearing A is still bound. | Open. Only bearing A is bound. |
| P1-chain.drive-train-09 | Low | Low (first review). Fixed. | E1 declares no output ports. The pack binds T_rms and n_pk, which only pass validation because undeclared outputs are offered as ports. This is consistency audit item 44. | ValidatePackTemplate checks OutputPorts; E1 has none declared. | Declare E1 ports, including the output-side values from -02, -03 and -05. | Open. Probe template: E1 still declares no ports. The new motor-circuit pack binds E1 P_pk the same way. | Fixed (0fe142cf). E1 1.1.0 declares 16 output ports, including T_rms and n_pk. Output-side, mean-speed and thrust ports stay under -02, -03 and -05. | Fixed (0fe142cf). Unchanged since the A7a re-check. |
| P1-chain.drive-train-10 | Low | Low (first review). | The chain tests check value transfer and staleness only. No test checks the engineering meaning of a link. | DriveTrainChainTests asserts equality of bound values. | Add a chain known-answer case with a geared, reversing cycle and a hand-checked shaft factor. | Open. DriveTrainChainTests is unchanged; no meaning test added. | Open. DriveTrainChainTests is unchanged. No test of meaning was added. | Partly fixed (64447456). The chain test now hand-checks T_load = 1800/(12.5 × 0.95) and v_max = T_pk/(4 × 0.040). It still checks no shaft factor. |
| P1-chain.drive-train-C1 | Medium | Not reviewed here. Rated with elec.load-budget-R1. | The new pack.motor-circuit in the same file binds the load budget I_3ph to the cable design current (e6 I_3ph → e4 I_b_in). I_3ph is built from P·d·k, so a duty below 1 on any table load lowers the cable current. This is load-budget finding R1 reaching a cable. The linked motor itself enters at d = k = 1 and is not affected. | Probe (elec.load-budget): 7.5 kW pump, cosφ 0.85, d 0.5 gives I_3ph 6.37 A against 12.74 A running. Binding source: CalcPackTemplates.MotorCircuit. | Fix load-budget R1, or bind a current without duty. Until then note on the pack that table loads need d = 1 for cable sizing. | Open (new) | Open. Probe: a 7.5 kW fan at d 0.5 gives I_3ph 10.954 A against 17.32 A at d 1. The pack is unchanged. | Open. Probe (motor-circuit example): a 7.5 kW fan at d 0.5 gives I_3ph 10.62 A, 4 mm² and 16 A. At d 1 it is 16.99 A, 6 mm² and 20 A. |
| P1-chain.drive-train-D1 | Medium | High (upgraded). The pack text says M3 carries "the motor's peak torque". T_pk is only the load's run-up torque. A jam or stall drives the coupling towards T_peak, 2.5 times more here. Unconservative in normal use. | The coupling bolts (M3) carry E1's T_pk, the torque of the load run-up. The drive can deliver more. The example E1 holds a typed T_peak of 400 N·m, and the chain never uses it. A coupling should be checked for the torque the drive can deliver. | Probe: M3 with T_pk 159.4 N·m gives v_max 0.996 kN, u 0.071. With the drive's 400 N·m it is 2.500 kN, u 0.179. Both pass. | Add a port for the drive's peak torque (or T_pk at T_peak) to E1. Bind that to M3 T_z. | — | — | Open (new) |
| P1-chain.drive-train-D2 | Low | Low (confirmed). Only a source comment names the 60 mm pinion. The shipped sheets state no pitch, and the loads fit a 120 mm pinion. -06 holds the real gap. | The example pinion loads (F_y −0.92, F_z 2.53 kN) match T_rms 151.58 N·m only for a 120 mm pitch diameter. The example text says a 60 mm pitch pinion. At 60 mm the loads double. The bearing life then fails. | Probe: loads × 2 give R_A 2.692 kN, L_10h 9443 h against 20000 h required, u_life 3.32. Loads as typed give u_life 0.415. | State the pitch diameter in the example and make the loads match. Or derive the pinion load from T (see -06). | — | — | Open (new) |
| P1-chain.drive-train-D3 | Low | Low (confirmed). V_R, T_R and A are typed in the same step as the bolts. No other step computes them, so nothing diverges silently. Contrast chain.steel-support-02. | The M3 step holds typed bolt data. The shear resistance V_R (14.0 kN), tension resistance T_R (21.0 kN) and area (36.6 mm²) link to no connection step. A change of bolt does not make M3 stale. | The example inputs type M8 8.8 values. The chain test checks no link for them. | Link the resistances from a bolt-resistance step placed before M3. Or warn on the pack text. | — | — | Open (new) |

## Notes for the owner's manual test
- Build the pack with E1 as a direct rotary drive, J_L 2 kg·m², 0.25 rev in 0.3 s, dwell 0.7 s, J_motor 0.005 kg·m². Expect T_pk 174.9 N·m, T_dec −174.9 N·m, T_rms 78.2 N·m.
- In M4, set one 28 mm segment over 400 mm, 1 kN down at 200 mm, a section at 200 mm with K_t 1.7, K_ts 1.5, q 0.85, q_s 0.9. Expect n_f 2.01 from the chain. Your hand DE-Goodman with τ_a from 174.9 N·m gives about 1.39 (-01).
- Set E1 to the geared index table KAT (i 10). Expect T_rms 2.24 N·m in M4. The output shaft peak is 39.3 N·m by hand (-02).
- Keep E1 on the ball screw. Note that M5 F_a stays 0 while the screw thrust peaks near 0.5 kN (-05).
- Check the M2 step: it shows H7/g6 clearance. Compare with the bearing maker's seat recommendation (-07).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24. This report is the first pass for the chain, so there is no earlier severity to compare.
- I read the pack, the pack service's validation and instantiation, and both chain tests.
- I ran E1, M4, M5 and M2 with the probe and fed each output into the next step by hand. The fatigue values for a reversing torque are hand calculations on the probe's S_e, σ_a and τ values.
- Coordinator suspicion (a) is confirmed as Critical (-01). Suspicion (b) is confirmed as Critical (-02). Suspicion (c) is confirmed as conservative, rated Medium by the scale (-03). Suspicion (d) is rejected (-04).
- New items: screw thrust (-05, High), torque-linked loads (-06), the seat fit (-07), bearing B (-08), ports (-09) and tests (-10).
- Residual doubt: whether the pack means M4 to be the motor shaft or the output shaft. -02 holds for any shaft after a ratio. -01 holds for both.
- Residual doubt: the bearing seat practice in -07 is general knowledge, not checked against a maker's catalogue.

## Re-check after DA-17/DA-18 and A-4/A-5 (2026-10-24)
- Source changed: CalcPackTemplates.cs gained a second pack, pack.motor-circuit (A-4). The pack.drive-train steps and bindings are unchanged.
- The steps M4, M5 and M2 changed only in citations (M4) or not at all. I re-ran the C1, C2, C3 and C4 probes with the stored inputs. The chain values are identical.
- All ten findings are still Open, including both Critical findings (01 and 02). There is no script for this chain.
- New: C1 (Medium). The motor-circuit pack feeds the duty-weighted I_3ph to the cable step. See load-budget R1.
- I did not review the motor-circuit pack beyond its bindings. Its own chain test (MotorCircuitChainTests) is new and was not run here.

## Re-check after A7a (2026-10-24)
- Source changed: E1 1.1.0 declares its output ports (0fe142cf) and adds continuous duty. The pack.drive-train steps and bindings are unchanged.
- I re-ran the C1 to C4 probes with the stored inputs. The chain values are identical.
- Fixed: -09. Open: -01 to -08, -10 and C1. Both Critical findings (-01, -02) are still Open.
- For a continuous-duty E1 (pump or fan) T_rms is the steady load torque, so -01 loses force there. -02 still holds.
- New defects: none found.

## Re-check after A7b/A8 (2026-10-24)
- Source changed: the pack gained example inputs, a fifth step M3 (coupling flange bolts) and the link E1 T_pk → M3 T_z. The example E1 is continuous duty through a 12.5:1 gearbox.
- I ran the example step by step with the probe (/tmp/claude-0/recheck3/dt.py). The Chains tests pass (38).
- Changed: -01, -02, -03 and -05. In the example M4 is the gearbox input shaft on a continuous drive, so they lose force there. The bindings are unchanged.
- Partly fixed: -07 and -10. Open: -04, -06, -08 and C1. Fixed earlier: -09.
- Both Critical findings stay Changed, not Fixed. A reversing cycle or an output-side shaft would still be wrong.
- New: D1 (Medium) coupling torque, D2 (Low) pinion loads, D3 (Low) typed M3 bolt data.

## Independent review of the A7b/A8 re-check (2026-10-24)
- Reviewer: senior review (Claude), 2026-10-24, at 298ade36. Probe scripts are in /tmp/claude-0/review-inputs/s4/ (dt_rev.py).
- I read the drive-train template and its example inputs. The bindings are the same five links plus E1 T_pk → M3 T_z.
- -01 stays Critical. "Changed" was wrong: the binding is unchanged. E1 defaults to point-to-point duty. C1 still gives n_f 2.009 and n_y 5.18.
- With the example E1 set to point-to-point, T_rms is 108.25 N·m and T_pk 177.90 N·m. M4 n_y is overstated by 57 %.
- -02 drops to High. The example names M4 as the gearbox input shaft, where motor torque is right. An output-side shaft is a plausible use, and nothing warns.
- At the output torque of 1800 N·m, the example shaft fails fatigue and yield. The bound value passes both.
- D1 rises to High. The coupling should be checked for the torque the drive can deliver, not the load's run-up torque.
- I reproduced D1 (v_max 0.996 against 2.500 kN) and the C1 values. D2 and D3 stay Low.
- Residual doubt: the design torque for a coupling (T_peak, a stall torque or a service factor) is the owner's choice. The owner should state it.
