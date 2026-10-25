# chain.motor-circuit — independent review of the linked chain (Track P, P1)
Pack: `pack.motor-circuit`, "Motor circuit". Source file: src/Tempest.Calc/Tempest.Calc.Library/CalcPackTemplates.cs. Chain tests: tests/Tempest.Calc.Tests.Library/Chains/MotorCircuitChainTests.cs.
Steps: E1 elec.motor-sizing → E6 elec.load-budget → E4 elec.conductor-sizing → E3 elec.voltage-drop, E5 elec.fault-protection, E2 elec.enclosure-heat.
Script: none. I fed each step by hand with the probe. The inputs are in /tmp/claude-0/probe-inputs/ (chain.py, e1.json to e6r1.json).
Verdict: VALUES TRANSFER CORRECTLY; after review THREE HIGH FINDINGS IN THE LINKS (an unlinked device type, duty-weighted current from E6 R1, an unlinked cpc size), FIVE MEDIUM

Every binding copies its value with the unit converted through SI. The shaft-versus-input power step is right. The problems are what is not linked, and what the linked values mean.

## Method re-derived
- E1 P_pk = T_pk·ω_pk is peak mechanical power at the motor shaft. It is the end-of-acceleration point and an upper bound for an S-curve.
- E6 turns the linked P_add into electrical input: P = P_add/η_add, S = P/cosφ_add, I_3ph = S/(√3·U_3ph). The load has d = 1 and k = 1.
- E4 takes I_3ph as I_b_in, then I_n, CSA, I_z (see docs/verification/rederivation/elec.conductor-sizing.md).
- E3 takes I_b, L and CSA. E5 takes CSA, L and I_n. E2 takes E6's Phi as an added heat load.
- Phi = (1 − η)·P·d·k, with P the electrical input. For the linked load this is the motor and drive loss, at the peak power.
- A supply cable is sized for the thermal (RMS-like) current. A peak current held for a fraction of the cycle is conservative. Line-side current follows power, not torque.
- A motor circuit is normally sized from the motor or drive rated current (nameplate), not from the application duty.

## Known-answer cases
The chain has no known-answer case. The three tests that matter check transfer only.

| Test | What it asserts | What it does not assert |
|---|---|---|
| Motor_Demand_Reaches_The_Cable_Voltage_Drop_Fault_Check_And_Enclosure | Each bound input equals its source output in SI to 9 places. No step blocks. | Whether P_pk, I_3ph or Phi are the right quantities. That E5's device matches E4's. |
| A_Bigger_Motor_Makes_The_Chain_Stale_And_Updating_Carries_The_Current_Through | Raising m by 1.5 makes E6 to E2 stale. Updating clears it. | That values downstream are sensible. |
| Without_The_Owners_Tables_The_Chain_Is_Refused_At_The_Cable_Step | E4 blocks with "ampacity:" when no table is given. | None. This one is good. |

## Extra probes (chain fed by hand)
Base case: E1 ball-screw KAT inputs (50 kg, 10 mm lead, 0.5 m in 0.5 s, dwell 0.5 s). E6 and E5 at defaults. E4 with the KAT tables.

| Probe | Step and inputs (short) | Symbol | Chain value | Correct or comparison value | Effect |
|---|---|---|---|---|---|
| C0 | E1 | T_pk / n_pk / P_pk | 2.579 N·m / 9000 rpm / 2.4306 kW | same | E1 is right |
| C0 | E6 P_add 2.4306 kW, cosφ 0.85, η 0.9, empty table | S_dem / I_3ph / Phi | 3.177 kVA / 4.586 A / 270.1 W | same | Transfer and shaft-to-input step right |
| C0 | E4 I_b_in 4.586 A, defaults (MCB C) | I_n / CSA / I_z | 6 A / 1.5 mm² / 12.3 A | same | E4 right |
| C0 | E3 I 4.586 A, L 20 m, A_c 1.5 mm² | dV_pct | 0.466 % | same | Transfer right |
| C1 | E5 default device, S_line 1.5, L 150 m, I_n 6 A | Z_s_max / u_Zs | 7.283 Ω / 0.615 pass (type B, 5·I_n) | 3.642 Ω / 1.229 FAIL (type C, 10·I_n) | Pass hides a fail, see -01 |
| C2 | E6 plus a 7.5 kW fan row, cosφ 0.85, d 0.5 | I_3ph | 10.95 A | 17.32 A at full duty | 37 % low, see -02 |
| C2 | E4 on those currents | I_n / CSA | 16 A / 2.5 mm² | 20 A / 4 mm² | One size too small |
| C3 | Mean motoring power of the C0 cycle (hand estimate) | P_el mean / I | about 240 W / 0.41 A | 2701 W / 4.586 A peak | Peak is 11 times the mean, see -03 |
| C4 | E1 with T_rated 20 N·m instead of 2 | P_pk | 2.4306 kW | same value | Motor rating does not reach the chain, see -04 |
| C5 | E2 P_add 270 W, default 0.6×0.8×0.3 m enclosure | T_int / u_nat | 78.1 °C / 2.87 FAIL, 176 W cooling | Mean motor loss about 24 W: passes (natural 94 W) | Spurious cooling unit, see -05 |
| C6 | E5 S_line 6 mm², L 100 m, type C, S_cpc 1.5 (default) or 6 | Z_s | 2.069 Ω or 1.038 Ω | Z_s 1.038 Ω with a 6 mm² cpc | Default doubles Z_s, see -06 |
| C7 | E3 I 4.586 A, A_c 1.5 mm², V_n 400 or 230 V | dV_pct | 0.466 % or 0.810 % | 0.810 % at 230 V | V_n is not linked, see -07 |
| C8 | E1 vertical axis, 2000 kg, θ −90° | P_pk | −1.09 kW | E6 P_add needs ≥ 0 | E6 blocks, see -09 |

## Implementation comparison
| Aspect | Engineering need | Pack | Agrees? |
|---|---|---|---|
| P_pk → P_add: shaft or input power | Shaft power, then input = P/η | P_pk is shaft power. E6 divides by η_add. | Yes, see -11 |
| P_pk: peak or continuous | Continuous or thermal-equivalent for the cable | Peak held as a continuous load | Conservative, see -03 |
| P_pk: load or motor rating | Motor or drive rated input current | Load-driven only | No, see -04 |
| I_3ph → I_b_in: duty | Current while the load is on | I_3ph is duty-weighted for table rows (R1) | No, see -02 |
| I_3ph: one circuit or the whole bus | The motor branch | Sum of every three-phase load | Partly, see -08 |
| E4 I_b, L, CSA, I_n → E3, E5 | As bound | Values and units correct | Yes |
| E4 device → E5 device | Same device type | Not bound; defaults B and C differ | No, see -01 |
| E4 CSA → E5 S_cpc | cpc follows the line size | S_cpc stays at 1.5 mm² | No, see -06 |
| E4 insulation → E5 θ, E3 T_c | Conductor temperature follows the cable | Both stay at 70 °C | No, see R1 |
| Voltage and power factor in E3, E5 | Same as E6 and E4 | Not bound | No, see -07 |
| E6 Phi → E2 P_add | Heat inside the panel | Motor loss at peak, drive loss missing | No, see -05 |
| Ports | Declared on each step | E1, E3 and E2 declare none | Partly, see -10 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ e283c07e | Status @ 298ade36 | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|---|---|
| P1-chain.motor-circuit-01 | High | High (confirmed). Reproduced in E5: I_n 6 A, 1.5/1.5 mm², 150 m. Type B u_Zs 0.615 pass; type C 1.229 FAIL. Motors normally use type C or D, so the default B errs the unsafe way. | E5's device type is not linked to E4's. E4 defaults to MCB type C. E5 defaults to type B (5·I_n). Only I_n is bound. The loop impedance check passes a circuit that fails with the real device. | C1: I_n 6 A, 1.5 mm², L 150 m. Type B: Z_s 4.476 Ω against 7.283 Ω, pass. Type C: 3.642 Ω, FAIL (u 1.229). Type D: u 2.458. | Bind the E4 device to E5 with a mapping (MCB B, C, D). For gG, MCCB and semi-enclosed, require curve points in E5. Warn when E4 and E5 device types differ. | Open. Probe: E5 still defaults to type B. I_n 6 A, 1.5 mm², 150 m gives u_Zs 0.615 pass. Type C gives 1.229 fail. | Open. The example pairs E4 type C with E5 type B, the default. At 25 m u_Zs is 0.12. At 150 m type B gives 0.50, type C 1.002 (fail). | Fixed (874bc235). E4 now outputs device_code and E5 takes it. Probe: 150 m, type C gives u_Zs 1.229 FAIL. E5 left at type B is blocked with a clear message. Residual: code 4 covers four device kinds (see elec.fault-protection-E1). Reviewer check (2026-10-25): confirmed Fixed. The pack binds E4 device_code to E5. With code 2, type B and a blank device both block. Type C at 150 m gives u_Zs 1.229 FAIL. |
| P1-chain.motor-circuit-02 | High | High (confirmed). This is the chain effect of P1-elec.load-budget-R1, not a separate defect. Fix it in E6. The chain needs only a re-test, and it closes when R1 closes. | E6's I_3ph is weighted by duty d. It feeds E4 as the design current. A cable must carry the load while it is on. This is E6 finding R1, and it still holds at this commit. The linked load uses d = 1, so only table rows are hit. | C2: a 7.5 kW fan at d 0.5 gives I_3ph 10.95 A. The full-duty value is 17.32 A (37 % low). E4 then picks 16 A and 2.5 mm². It should pick 20 A and 4 mm². | Fix R1 in E6 (currents from P·k, not P·d·k). Until then, warn in the pack that table rows with d below 1 under-size the cable. | Open. Probe: a 7.5 kW fan at d 0.5 gives I_3ph 10.954 A, so E4 picks 16 A and 2.5 mm². At full duty E4 picks 20 A and 4 mm². | Open. Probe (example tables): a 7.5 kW fan at d 0.5 gives I_3ph 10.62 A. E4 picks 4 mm² and 16 A. At d 1 it picks 6 mm² and 20 A. | Open. Probe (example tables): a 7.5 kW fan at d 0.5 gives I_3ph 7.74 A and E4 picks 10 A. At d 1 it gives 14.11 A and 16 A. Closes when load-budget-R1 closes. |
| P1-chain.motor-circuit-03 | Medium | Medium (confirmed). Conservative; over 5 % by a wide margin. | E1's P_pk is the peak of one cycle. The pack uses it as a steady load. The current and heat are conservative by about 10 times for a move-and-dwell cycle. The label "peak" on the port is correct. The use is not. | C3: P_pk 2.43 kW against a mean motoring power of about 216 W (shaft). I_b 4.59 A against about 0.41 A. Phi 270 W against about 24 W. | Add P_mean (positive energy per cycle) to E1. Bind that to E6 for heat. Keep P_pk for the cable current and the device rating. | Open. Probe: E1 point-to-point P_pk is still 2.4306 kW and the pack still binds it as a steady load. | Open. Probe: E1 P_pk is still 2.4306 kW and the pack still binds it as a steady load. | Fixed (874bc235). The pack binds E1 P_rms. Probe: P_rms 784.1 W against P_pk 2430.6 W; E6 I_3ph 1.373 A, against 4.586 A for the peak. RMS is the right thermal basis. |
| P1-chain.motor-circuit-04 | Medium | Medium (confirmed). Not unsafe for the cable: the device still protects it (I_n ≤ I_z). The risks are nuisance trips and a device below the drive maker's required supply protection. | The circuit is sized from the load, not from the motor or drive. A larger motor or drive draws more current than the load needs. A cable and device sized on the duty can be below the drive's rated input current. | C4: T_rated 2 or 20 N·m gives the same P_pk 2.4306 kW. A 20 N·m motor at 3000 rpm is 6.3 kW rated. | Add a motor (or drive) rated input current to E6's linked load. Size on the larger of that and the duty current. State which basis the pack uses. | Open. Probe: T_rated 20 N·m still gives P_pk 2.4306 kW. | Open. Probe: T_rated 20 N·m still gives P_pk 2.4306 kW. | Open. Probe: T_rated 20 N·m still gives P_rms 784.1 W and I_3ph 1.373 A. |
| P1-chain.motor-circuit-05 | Medium | Medium (confirmed). Mostly conservative. The missing drive loss is the unconservative part. Same root as P1-elec.load-budget-05. | E6's Phi for the linked load is the loss of the motor and drive at the peak. The motor stands outside the panel. The drive and the PSU stand inside, and their loss is missing. E2 then reports a wrong verdict. Same family as E6 finding 05. | C5: Phi 270 W gives T_int 78.1 °C, FAIL, and a 176 W cooling unit. With about 24 W the 94 W natural dissipation passes. | Add an "inside the enclosure" flag per load and a drive-loss input. Bind a mean heat, not a peak. Reword the port label. | Open. Probe: E2 with 270 W still gives T_int 78.1 °C, a fail, and a 176 W cooling unit. | Changed. The example sets η_add 0.97 and a 1 × 1 × 0.3 m panel with T_max 55 °C. Φ is 105 W, so E2 passes (u_nat 0.886). The peak-heat binding and the missing drive loss are unchanged. | Partly fixed (874bc235). The linked motor now adds no panel heat (heat_add 0) and E6 heat shares are explicit. Probe: Φ 140 W, E2 passes (u_nat 0.90). Drive loss is a typed 60 W in E2; PSU loss is still missing. |
| P1-chain.motor-circuit-06 | Medium | High (upgraded). The first pass saw only the conservative side. When E4 picks a line below 1.5 mm², the default cpc understates Z_s. Probe: 1.0 mm² line, type C 6 A, 90 m: u_Zs 0.946 pass with cpc 1.5; 1.116 FAIL with cpc 1.0. | E5's S_cpc keeps its default 1.5 mm². It is not tied to E4's CSA. On larger cables the loop impedance is overstated and the checks fail wrongly. A user may then change both defaults and swap errors. | C6: S_line 6 mm², L 100 m, type C. Z_s 2.069 Ω with S_cpc 1.5; 1.038 Ω with 6. | Bind S_cpc to the CSA (equal up to 16 mm², half above, with the owner's table). Or warn when S_cpc is the default. | Open. Probe: 6 mm² line, 100 m, type C. Z_s is 2.069 Ω with the default cpc and 1.038 Ω with 6 mm². | Changed. The example types S_cpc 2.5 mm² against a 1.5 mm² line. Z_s is 0.900 Ω. With the cpc equal to the line it is 1.038 Ω (15 % higher). Still not bound. | Open. Probe: I_b_in 30 A gives a 10 mm² line and a 32 A device. S_cpc stays 1.5 mm²: u_Zs 1.092 FAIL. With S_cpc 10 mm² it is 0.664 pass. The example types 1.5 mm² for a 1.5 mm² line. |
| P1-chain.motor-circuit-07 | Medium | Medium (confirmed). Unconservative only when the supply is set below 400 V. | E3's V_n, cos φ and system, and E5's U_0, are not linked to E6's U_3ph and cosphi_add or to E4. A changed supply voltage leaves the voltage drop and the loop check on 400 V and 230 V. For a lower voltage the drop is understated. | C7: V_n 230 V gives dV 0.810 %. With E3 left at 400 V it shows 0.466 %, a factor of 1.74 low. | Bind U_3ph to V_n and U_3ph/√3 to U_0, cosphi_add to cos_phi. Or list the unlinked inputs on the pack page. | Open. Probe: E3 gives dV 0.810 % at V_n 230 V and 0.466 % at 400 V. V_n is still not linked. | Open. Probe: with U_3ph 230 V, E3 at its 400 V gives dV 0.763 %. With V_n 230 V it is 1.327 %. | Open. Probe: U_3ph 230 V in E6 gives I_3ph 2.387 A. E3 left at 400 V shows 0.303 %; with V_n 230 V it is 0.527 %. No binding for V_n, cos φ or U_0. |
| P1-chain.motor-circuit-08 | Low | Low (confirmed). | I_3ph is the sum of every three-phase load in E6. E3, E5 and E4 treat it as one circuit with one length. With extra loads the voltage drop and fault path describe a branch that does not exist. | C2: the fan row adds to the linked load in I_3ph 10.95 A. The cable length L is still the motor's. | Say so in the pack text. Or bind only the linked load's current when the pack is "motor circuit". | Open. Probe: with the fan row, I_3ph 10.954 A still stands for the motor's 20 m cable. | Open. Probe: the fan row still adds to I_3ph for the motor's 25 m cable. | Open. Probe: with the 7.5 kW fan row, I_3ph is 7.74 A, not 1.37 A. The cable is still the motor's 25 m. |
| P1-chain.motor-circuit-09 | Low | Low (confirmed). | An overhauling axis gives a negative P_pk. E6 refuses it with a range message. The user cannot build the chain for a lowering load, although the supply still feeds the drive. | C8: 2000 kg, θ −90°: P_pk −1.09 kW. E6: "P_add must be at least 0 kW". | Bind max(P_pk, 0), with a notice. Add the brake or regeneration case as a separate input. | Open. Probe: E6 still refuses P_add −1.09 kW. E1 with 2000 kg at −90° gives P_pk −1.090 kW. | Open. Probe: E1 with 2000 kg at −90° gives P_pk −1.090 kW. E6 refuses P_add below 0 kW. | Fixed (874bc235) for point-to-point. P_rms is never negative. Probe: 2000 kg at −90° gives P_pk −1.09 kW but P_rms 20.19 kW, and the chain builds (I_3ph 35.35 A). Regeneration counts as draw, which is conservative. A negative T_L in continuous duty still gives a negative P_rms (motor-sizing-E1). |
| P1-chain.motor-circuit-10 | Low | Low (confirmed). E1, E3 and E2 declare no OutputPorts in source. | The chain tests check transfer and staleness only. E1, E3 and E2 declare no output ports. The bindings pass because undeclared outputs are offered as ports (consistency audit item 44). | MotorCircuitChainTests asserts equality of bound values. E1, E3, E2 templates show no ports. | Declare the ports. Add a chain known-answer case with a hand-checked cable size and loop check. | Partly fixed (0fe142cf). E1 1.1.0 declares 16 output ports. E3 and E2 still declare none, and no chain known-answer case exists. | Partly fixed (0fe142cf, 64447456). E1 and E2 now declare ports. E3 still declares none. No chain known-answer case exists. | Partly fixed (0fe142cf, 64447456, 874bc235). E1 (17 ports, now with P_rms), E2 and E5 declare ports; E3 declares none. The chain test now pins P_rms 784.14 W and Φ 140 W. It hand-checks no cable size or loop check. |
| P1-chain.motor-circuit-11 | Info | Info (confirmed). | The power bindings are right in kind. P_pk is shaft power. E6 divides by η_add (default 0.9) and uses cosphi_add (default 0.85). A servo drive with a rectifier front end can have a different power factor. | C0: 2.4306 kW → 2.7007 kW input → 3.177 kVA → 4.586 A. | Note that the defaults are for an induction motor. Let the user enter the drive's values. | Open (Info). The defaults are unchanged. | Open (Info). The defaults are unchanged. The example sets η_add 0.97. | Open (Info). The defaults are unchanged. The example sets η_add 0.97 and cos φ_add 0.85. |
| P1-chain.motor-circuit-R1 | Medium | Medium (new) | E4's insulation choice does not reach E5 or E3. E5 computes Z_s at θ 70 °C and E3 its resistance at T_c 70 °C. An XLPE cable runs at 90 °C, so both are low. E5's adiabatic θ_i and θ_f also stay at the PVC values, which is conservative. | E5, 2.5/2.5 mm², 100 m, type C 16 A: Z_s 2.000 Ω at 70 °C against 2.109 Ω at 90 °C (5.4 % low). At 45 m, u_Zs 0.800 against 0.836. | Bind θ_p from E4's insulation to E5 θ and E3 T_c. Or warn on the pack page that they must match. | Open. Probe: 2.5/2.5 mm², 100 m, type C 16 A. Z_s is 2.000 Ω at 70 °C and 2.109 Ω at 90 °C. | Open. By code reading and bindings: E4 insulation still does not reach E5 θ or E3 T_c. | Open. Probe and bindings: the example cable is PVC 70 °C. E5 θ 70 °C and E3 T_c 70 °C are separate inputs. No binding carries E4's insulation. |
| P1-chain.motor-circuit-C1 | Medium | Medium (confirmed). Reproduced P_pk 295.7 W and P_cont 244.3 W for ka-continuous-pump. The error errs on the safe side by over 5 %. The 64× circulator ratio comes from typed servo-like inertia and run-up values. | E1 1.1.0 adds continuous duty. There P_pk is the end of the run-up with full load torque, and the steady power is the new P_cont. The pack still binds P_pk to E6 P_add whatever the duty. For a pump or fan this oversizes the supply. | ka-continuous-pump: P_pk 295.7 W, P_cont 244.3 W; E6 I_3ph 0.558 A against 0.461 A. Circulator of pack.wet-heating: P_pk 42.3 W, P_cont 0.664 W, 64 times. | Add a port P_run to E1 (P_pk for point-to-point, P_cont for continuous). Bind that to E6. Or warn when E1 is in continuous duty. | Open (new). | Open. Probe: a continuous E1 gives P_pk 846.9 W and P_cont 349.1 W. The pack still binds P_pk. | Fixed (874bc235). Probe: a continuous E1 (pump, 4:1) gives P_pk 287.1 W, P_cont and P_rms 244.3 W. The pack binds P_rms, so E6 I_3ph is 0.428 A. |
| P1-chain.motor-circuit-E1 | Low | — | E3 now checks the voltage drop at the RMS design current. A servo axis draws about three times that during acceleration. The peak-current drop is not shown. | Probe: example chain, E3 at I 1.373 A gives dV 0.174 %. At 4.255 A (the E1 peak power through E6) it gives 0.540 %. Both pass the 5 % limit here. | State on the pack page that E3 is a running-current check. Or add a second drop at the peak current. | — | — | Open (new at 0772fcf8). |

## Notes for the owner's manual test
- Build the pack with E1 as the ball-screw KAT (50 kg, 10 mm, 0.5 m in 0.5 s, dwell 0.5 s). Expect P_pk 2.431 kW, E6 I_3ph 4.586 A, E4 6 A and 1.5 mm² with the KAT tables.
- Leave E5 at its defaults and set the length to 150 m. Expect a pass. Change E5 to type C by hand. Expect u_Zs 1.23 and a fail (-01).
- Add a 7.5 kW row to E6, d 0.5, cosφ 0.85. Expect I_3ph 10.95 A. Your hand value at full duty is 17.32 A (-02).
- Compare E2's heat (270 W) with the motor's mean loss. Decide what should heat the panel (-05).
- Set E6 U_3ph to 230 V and look at E3. It still uses 400 V (-07).

## First-pass notes
- Reviewer: P1 chain pass (Claude), 2026-10-24. This report is the first pass for the chain.
- I read the pack, E6's linked-load code, E1's P_pk definition and the chain tests. I ran each step with the probe and fed the output into the next.
- The mean motoring power in C3 is my hand estimate from the trapezoid: 0.5·P_pk·t_acc plus T_load·ω·t_const, over a 1 s cycle, divided by η 0.9.
- Residual doubt: whether the pack means to size the supply from the load or from the motor. -04 depends on that choice.
- Residual doubt: whether other loads in E6 are meant to be on this cable. -08 depends on that choice.

## Independent review
- Reviewer: senior review (Claude), 2026-10-24. Report only; the pack has no script.
- I read the pack bindings, E6's linked-load code, and E5's and E3's inputs.
- I reproduced -01 and -06 with the probe on E5. Inputs are in /tmp/claude-0/review-inputs/mc/.
- -02 stays High as the propagation of P1-elec.load-budget-R1. Track S fixes it once, in E6.
- Changes: -06 raised to High. Its unconservative side shows when E4 picks a line below 1.5 mm².
- New: R1 (Medium). The cable temperature does not follow E4's insulation into E5 and E3.
- Residual doubt: -04 and -08 depend on whether the pack sizes from the load or the motor. The owner should state the basis.
- Residual doubt: I did not re-run E1 or E2. I relied on the first pass for C3 and C5.

## Re-check after A7a (2026-10-24)
- Source changed: CalcPackTemplates.cs gained pack.wet-heating. The pack.motor-circuit steps and bindings are unchanged. E1 is now version 1.1.0 (continuous duty, declared ports).
- I re-ran probes C0 to C8 and R1 with the stored inputs. Every value is identical to the first pass.
- Eleven findings are still Open. P1-chain.motor-circuit-10 is Partly fixed: E1 now declares its ports; E3 and E2 do not.
- New: C1 (Medium). A continuous-duty E1 would feed P_pk, not P_cont, to the load budget.
- I did not re-run MotorCircuitChainTests. The 24 wet-heating and cell-link tests pass (see chain.wet-heating).

## Re-check after A7b/A8 (2026-10-24)
- Source changed: the pack gained example inputs for every step except E4's tables. The bindings are unchanged. E2 1.2.0 declares output ports.
- I ran the example chain with the worked-example tables (/tmp/claude-0/recheck3/mc.py). The Chains tests pass (38).
- Open: -01 to -04, -07 to -09, -11, R1 and C1. Changed: -05 and -06. Partly fixed: -10.
- The example itself pairs E4 type C with E5 type B (-01). It passes at 25 m, but fails at 150 m with type C.
- New defects: none found.

## Re-check after the Addendum A Blue round (0772fcf8)
- Pack changed (874bc235, CalcPackTemplates.cs and CalcPackExamples.cs). E1 P_rms (not P_pk) now enters E6 P_add. E4 device_code now enters E5 device_code. E6 heat_add is 0 for the linked motor.
- The example changed. E3 runs three-phase. E5 uses a type C device, a 1.5 mm² cpc and I2t_let 15 kA²s.
- E6 rows carry heat shares and a single-phase PSU bus. The enclosure is 1.0 × 1.2 × 0.3 m.
- I rebuilt the chain by hand with the probe (/tmp/claude-0/recheck4/elec/mc4.py). The base case equals the issued sheet: P_rms 784.14 W, I_3ph 1.3727 A, 6 A, 1.5 mm², Φ 140 W.
- Fixed: 01, 03, 09 and C1. Partly fixed: 05 and 10. Open: 02, 04, 06, 07, 08, 11 and R1. Of the two High findings, 01 is fixed. 02 stays open through load-budget-R1.
- New defect: E1 (Low), the voltage drop is checked at the RMS current only. No new High or Medium defect found.
