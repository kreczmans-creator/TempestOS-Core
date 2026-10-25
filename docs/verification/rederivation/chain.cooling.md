# chain.cooling — independent review of the linked chain (Track P, P1)
Pack: `pack.cooling`, "Enclosure cooling". Source file: src/Tempest.Calc/Tempest.Calc.Library/CalcPackTemplates.cs. Example inputs: CalcPackExamples.cs (CoolE6, CoolE2, CoolB5, CoolB1). Chain tests: tests/Tempest.Calc.Tests.Library/Chains/CoolingChainTests.cs.
Steps: E6 elec.load-budget → E2 elec.enclosure-heat → B5 bs.heat-exchanger (sizing mode) → B1 bs.pipe-pump (heat-load form).
Script: none. I fed each step by hand with the probe. The inputs are in /tmp/claude-0/recheck3/ (drv.py, cool.py, cool_*.json). Release 298ade36 (A7b/A8).
Verdict: VALUES TRANSFER CORRECTLY; FOUR MEDIUM (-01 TO -04), SIX LOW, THREE INFO (reviewed 2026-10-24; no Critical or High)

Every binding moves the right quantity in the right unit and sign. The heat balance closes. The water mass flow from B1 equals the 0.25 kg/s typed in B5 to six digits.
The problems are what the chain leaves out. The coil is sensible-only. The pump head has no coil or plant loss. Glycol reaches B1 as c_p only. A hot ambient is refused.

## Method re-derived
- E6 gives Φ = Σ (1 − η) P d k in W. This is the heat the panel's drives and supplies dissipate. Source: see docs/verification/rederivation/elec.load-budget.md.
- Link: E6 Φ fills E2 `P_add`, a heat load added to the table loads. P_total = P_loads + P_add + P_solar in W.
- E2 gives the net duty P_cool = P_total − h A_eff (T_max − T_amb) in W. This is the heat natural dissipation cannot lose at the limit.
- Link: E2 P_cool fills B5 `Q_req` (W to kW). E2 T_max fills B5 `T_h_in` (K to °C). The air enters the coil at the enclosure limit.
- B5 sizing counterflow: ε = Q/(C_min ΔT_max), NTU from ε and C_r, A_req = NTU C_min/U_f. Source: Incropera, 7th ed., Table 11.4.
- B5 cold side: dT_c = Q/C_c, T_c_m = (T_c,in + T_c,out)/2, c_p,c as entered. These are the new 1.1.0 ports.
- Link: B5 Q, dT_c, T_c_m and cp_c fill B1 `Phi_load`, `dT_load`, `T` and `cp_load`. B1 then gives Q = Φ/(ρ c_p ΔT) with ρ at T.
- B1 head and power: H_sys = Σ dz + h_out + Σ h_f. P_hyd = ρ g Q H_sys. P_shaft = P_hyd/η_pump. T_shaft = P_shaft/ω.
- A cooling-water loop is closed. It has no net static lift. The pump covers friction, coil, valve and plant loss.
- A water-cooled air coil condenses water when its surface is below the air dew point. B5 and E2 carry no humidity.
- A cell or input link takes the upstream output in the input's default unit and pins the revision. It is stale when the revision moves, the output vanishes or the value differs by more than 1e-9 relative.

## Known-answer cases
The chain has no known-answer case of its own. CoolingChainTests holds five tests. I ran the Chains test classes: 38 pass in all, including these five.

| Test | What it asserts | What it does not assert |
|---|---|---|
| Panel_Heat_Reaches_The_Exchanger_Duty_And_The_Cooling_Water_Flow | Each bound value equals its source in SI. E2 P_cool equals B5 Q_req. B5 and B1 checks pass. | Whether the duty is physically right (-01, -02). Whether the E2 step passes (it fails by design, -08). |
| A_Panel_That_Cools_Itself_Leaves_The_Exchanger_Nothing_To_Size | A 20 W panel gives P_cool 0. The pack is refused at step b5 and the message names Q_req. | Whether that refusal is a good result for a panel that needs no coil (-06). |
| A_Bigger_Panel_Load_Makes_The_Chain_Stale_Blocks_The_Pack_And_Updating_Carries_The_Duty_Through | A bigger drive makes E2, B5 and B1 stale. The pack cannot be checked. Updating in order clears it. | An update out of order. |
| The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet | The pack checks and issues. The sheet names all seven links. | The engineering meaning of any value. |
| A_Glycol_Cold_Side_Changes_The_Pump_Flow_Through_The_Specific_Heat_Link | cp_load equals 3700. dT_c = Q/(0.25 × 3700). Q_design = 0.25/ρ_water(T_c_m). | That ρ and μ of the glycol reach B1 (-03). The test itself pins the water density. |

## Extra probes (chain fed by hand)
Base case: the pack's example inputs from CalcPackExamples.cs (inputs.json of the worked example gives the same values). E6 has four drive rows. E2 is a 1200 × 2000 × 600 mm floor-standing panel with three exposed faces. Ambient is 35 °C and the limit is 45 °C. B5 is a counterflow air coil, air 0.4 kg/s, water 0.25 kg/s at 22 °C, U 60 W/(m²·K). B1 has 30 m of DN25, ΣK 12 and dz 3 m.

| Probe | Step and inputs (short) | Symbol | Chain value | Correct or comparison value | Effect |
|---|---|---|---|---|---|
| K0 | E6 four drive rows | Φ | 3510 W | 55 × 0.03 + 30 × 0.025 + 30 × 0.04 × 0.8 + 1.5 × 0.1 = 3.51 kW | E6 right |
| K0 | E2 with P_add 3510 W | P_total / A_eff / P_nat_max / P_cool | 3660 W / 4.32 m² / 237.6 W / 3422.4 W | 3660 − 5.5 × 4.32 × 10 = 3422.4 | Transfer and net duty right |
| K0 | E2 | T_max / u_nat / T_int | 45 °C / 15.40 FAIL / 189.04 °C | Fails by design; see -08 | Check cannot pass |
| K0 | B5 sizing, Q_req 3.4224 kW, T_h_in 45 °C | T_h_out / T_c_out / dT_c / T_c_m | 36.495 / 25.275 °C / 3.27502 K / 23.6375 °C | 45 − 3422.4/402.4 = 36.495; 3422.4/1045 = 3.27502 | Cold-side ports right |
| K0 | B5 | ε / NTU / A_req / dT_app | 0.3698 / 0.5010 / 3.3802 m² / 14.495 K | Counterflow, C_r 0.3851 | Sizing right |
| K0 | B1 heat-load form | Q_design / mass flow | 2.506554e-4 m³/s / 0.2500 kg/s | ρ(23.64 °C) = 997.4 kg/m³; 0.25 kg/s typed in B5 | Flow right |
| K0 | B1 | H_static / h_f / H_sys / P_hyd / T_shaft | 3 m / 0.6606 m / 3.6606 m / 8.975 W / 0.043725 N·m | 12.82 W/293.215 rad/s = 0.043725 | Torque right for the head given; see -02, -05 |
| K1 | E6 loads ÷ 1000 (panel needs no coil) | P_cool / B5 | 0 W / Block "Q_req must be at least 0.000001 kW" | Pack refused (test) | See -06 |
| K2 | B5 cp_c 3600 (30 % glycol, assumed) | dT_c / B1 Q_design | 3.8027 K / 2.50672e-4 m³/s | Same mass flow, ρ_water used | Flow volume only right if ρ is water; see -03 |
| K3 | B1 custom fluid ρ 1030, μ 3.5 mPa·s (assumed) | Q_design / Re / h_f | 2.4272e-4 m³/s / 3638 / 0.7894 m | Water: 2.5066e-4 / 13842 / 0.6606 m | Flow 3.2 % lower, friction +19.5 %; see -03 |
| K4 | B1 dz 0 (closed loop) | H_sys / T_shaft | 0.6606 m / 0.007891 N·m | 0.043725 N·m with dz 3 m | 5.5 times lower; see -05 |
| K5 | B5 air 0.4, 0.2, 0.1 kg/s | A_req / T_h_out | 3.380 m² / 36.5 °C; 4.979 m² / 28.0 °C; Block (ε 1.479) | U fixed at 60 | Area moves 47 % with a typed air flow; see -07 |
| K6 | E2 T_amb 46 °C against T_max 45 °C | block | "T_max: the maximum internal temperature must exceed the ambient" | A hot plant room is the coil's use case | See -04 |
| K7 | bs.psychrometrics 35 °C, 60 % RH | t_dp | 26.07 °C | Coil water inlet 22 °C | Coil surface below dew point; see -01 |

## Implementation comparison
| Aspect | Engineering need | Pack | Agrees? |
|---|---|---|---|
| E6 Φ → E2 P_add: which value | Mean heat, since the panel warms slowly | Φ is duty-weighted, in W | Yes; see -09 for what Φ counts |
| E6 Φ → E2 P_add: unit and sign | Heat added, in W, not negative | W to W; E6 refuses a negative load | Yes |
| E2 P_cool → B5 Q_req: which value | Net duty at the limit, not the gross load | Net duty, W to kW | Yes |
| E2 T_max → B5 T_h_in | Air enters the coil no hotter than the limit | Limit, K to °C | Yes |
| Air-side humidity | Dew point against the coil surface | Not in either step | No, see -01 |
| B5 Q, dT_c, T_c_m → B1 | Same heat, rise and temperature on the water side | Four links; K and °C converted | Yes (-11) |
| B5 fluid → B1 fluid | One fluid in coil and pipework | Only c_p travels | Partly, see -03 |
| Pump head | Friction plus coil, valve and plant loss; no lift | Pipe friction plus a typed 3 m lift; h_out 0 | No, see -02 and -05 |
| Peak or continuous | Continuous heat; the pump at the design flow | Both continuous | Yes |
| Per-unit basis | Whole panel, one coil | Whole panel, one coil | Yes |
| Stale handling | Judged like input links | Same rules; transitive; pack blocked while stale | Yes (-12) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-chain.cooling-01 | Medium | Medium (confirmed). No number is wrong, but the chain gives no condensation warning for a panel with live parts. The dew point depends on site humidity: owner to state it. | The coil is sensible-only. The example water enters at 22 °C, below the dew point of ambient air. The coil surface would condense water inside the panel. Neither E2 nor B5 carries humidity, so the latent load and the drain are missing. | K7: 35 °C and 60 % RH give a dew point of 26.07 °C. The coil surface sits between the water (22 to 25 °C) and the air (36 to 45 °C). The duty 3422.4 W is sensible only. The chain gives no notice. | Add a warning when T_c,in is below an entered dew point. Offer a dew-point input on E2 or B5. State in the pack text that the duty is sensible only. | Open. E2 and B5 have no humidity or dew-point input. The duty 3422.4 W is still sensible only. The chain gives no condensation notice. |
| P1-chain.cooling-02 | Medium | Medium (confirmed). Unconservative in normal use: h_out defaults to 0 and the pack names no coil loss. Same rating as chain.wet-heating-03. Coil Δp values: owner to confirm. | The pump head has no coil water-side loss, valve loss or plant loss. B1's h_out defaults to 0 and the pack never asks for it. The coil's own water pressure drop is usually larger than the pipe friction. The torque is low. Same cause as P1-chain.wet-heating-03. | K0: H_sys 3.6606 m, of which friction is 0.6606 m. The coil Δp is not in the chain. Typical coil values: owner to confirm. | Ask for the coil water-side loss in the pack text. Warn in B1 when h_out is 0 on a heat-load sheet. Offer a coil-loss output on B5. | Open. The pack's B1 still has h_out 0 and no coil loss. Chain probe: H_sys 0.6606 m, all friction. |
| P1-chain.cooling-03 | Medium | Medium (confirmed). Reproduced K3 with B1 "custom (ρ and μ given)". On a closed loop (dz 0) T_shaft is 0.009429 N·m, against 0.007885 on water (16 % low). Same rating as chain.wet-heating-06. Glycol data: owner to confirm. | A glycol cold side reaches B1 as c_p only. B1 stays on water for density and viscosity. The volume flow is then 3.3 % high. Friction is understated by about 20 %. The pack text says the mixture "reaches the pump flow". That holds for c_p and mass flow only. | K2: cp_c 3600 gives dT_c 3.8027 K and the same Q_design 2.5067e-4 m³/s. K3: ρ 1030 and μ 3.5 mPa·s (assumed) give Q 2.4272e-4 and h_f 0.7894 m against 0.6606 m. The glycol test pins ρ_water. | Add a fluid choice that B1 reads from B5. Or carry ρ and μ ports. Say in the pack text that B1's fluid must match B5's. | Open. Chain probe, cp_c 3600 and custom ρ 1030, μ 3.5 mPa·s: h_f 0.7894 m and T_shaft 0.009429 N·m, as before. B1 does not read B5's fluid. |
| P1-chain.cooling-04 | Medium | Medium (confirmed). Reproduced K6. The block is safe, since nothing issues. It removes the coil's main use, so it rates above R2 (Low) in E2 alone. | A hot ambient stops the chain at E2. When T_amb is at or above T_max, E2 blocks. A water coil is most useful in that case. The pack then gives no duty and no coil. This is P1-elec.enclosure-heat-R2 reaching the chain. | K6: T_amb 46 °C and T_max 45 °C give Block "limit". E2 offers no P_cool. B5 and B1 take nothing from a blocked step. | Fix R2 in E2: compute P_cool = P + h A_eff (T_amb − T_max) in that case. Then re-test the chain with a hot ambient. | Open. Chain probe: T_amb 46 °C with T_max 45 °C still blocks E2 ('T_max must exceed the ambient'). B5 and B1 get nothing. |
| P1-chain.cooling-05 | Low | Low (confirmed). B1 sums signed dz, so a loop entered as flow and return gives H_static 0 (see chain.wet-heating-02). The 3 m is a fixture error on the safe side. | B1 counts the 3 m of dz as pumped static lift. A closed cooling loop has none. The torque is conservative and the reference values are unrepresentative. Same fixture issue as P1-chain.wet-heating-02. | K4: dz 0 gives H_sys 0.6606 m and T_shaft 0.007891 N·m. With dz 3 m it is 0.043725 N·m (5.5 times more). | Set the fixture dz to 0 and re-pin. Label dz as the net rise. | Fixed (3c637a3d, a23e6e18). The pack's B1 is a closed loop (dz 0, circuit = closed). Chain probe: H_static 0, H_sys 0.6606 m, T_shaft 0.007891 N·m. |
| P1-chain.cooling-06 | Low | Low (confirmed). | A panel that needs no coil cannot be recorded. P_cool is 0, B5 refuses Q_req 0, and the pack is refused. The message names Q_req, which is correct but gives no reason in plain terms. | K1: E6 loads ÷ 1000 give P_cool 0 W and "Q_req (Required duty) must be at least 0.000001 kW". The test pins the refusal. | Say in the refusal that natural dissipation holds the limit and no coil is needed. | Open. Chain probe: loads ÷ 1000 give P_cool 0 W and B5 still refuses Q_req 0 with the same message. |
| P1-chain.cooling-07 | Low | Low (confirmed). | The coil's air flow (0.4 kg/s) and U (60 W/(m²·K)) are typed. Nothing ties them to each other or to E2. A lower air flow raises A_req sharply, while U stays fixed. Below about 0.15 kg/s the duty is unattainable (ε above 1). | K5: A_req 3.380 m² at 0.4 kg/s, 4.979 m² at 0.2 kg/s (+47 %). At 0.1 kg/s B5 blocks (ε 1.479). | State in the pack text that U and the air flow go together. Warn when T_h_out falls below T_c,in + dT_min. | Open. Chain probe: A_req 3.380 m² at 0.4 kg/s, 4.979 m² at 0.2 kg/s; B5 blocks at 0.1 kg/s. |
| P1-chain.cooling-08 | Low | Low (confirmed). | The E2 step fails its own check by design. The issued pack shows "0 of 1 PASS" for E2. A reader or a gate that wants every check to pass will reject the sheet. The README explains it. | K0: u_nat 15.40, FAIL. T_int prints 189.04 °C, a mean rise from a constant h with no validity warning (P1-elec.enclosure-heat-03). | Mark the E2 check as informational in this pack. Or reword it as "coil required". | Open. The README still shows 'Enclosure heat: 0 of 1 PASS' for E2. |
| P1-chain.cooling-09 | Low | Low (confirmed). | E6 Φ counts the heat of every row and omits the supply's own loss. A motor row in the field would inflate the panel heat. The example rows are drives and supplies, so it is right here. This is P1-elec.load-budget-05. | Φ 3510 W equals Σ (1 − η) P d k for the four rows. The "24 V supplies" row carries the supply loss by hand. | Add an "inside the enclosure" flag per load. See load-budget-05. | Fixed (874bc235). E6 has a per-row `heat` share (blank: 1 − η; a machine outside the enclosure: 0). It also has `heat_add` (default 0 for the linked machine). Chain probe: a 22 kW field motor with heat 0 leaves Φ at 3510 W. With heat blank it adds 2200 W. |
| P1-chain.cooling-10 | Info | Info (confirmed). | The chain ends at the pump shaft. It has no motor step, so the η_pump default 0.7 sets the final torque. A small pump has a lower efficiency. This is P1-bs.pipe-pump-C2. | K0: P_shaft_design 12.82 W at η 0.7. | None for the chain. Optionally add an E1 step as in the wet-heating pack. | Open (Info). The chain still ends at the pump shaft with η_pump 0.7. |
| P1-chain.cooling-11 | Info | Info (confirmed). Reproduced K0: Q_design 2.506554e-4 m³/s and T_shaft 0.043725 N·m. | The bindings are right in kind, unit and sign. Φ, P_cool and Q are heat in W. T_max and T_c_m are absolute temperatures in °C. dT_c is a difference in K. B1's mass flow equals B5's typed 0.25 kg/s. | K0: B1 Q_design 2.506554e-4 m³/s × 997.4 kg/m³ = 0.2500 kg/s. B5 dT_c 3.27502 K = 3422.4/(0.25 × 4180). | None. | Open (Info, no defect). Chain probe: Φ 3510 W, P_cool 3422.4 W, A_req 3.3802 m², dT_c 3.27502 K, Q_design 2.5066e-4 m³/s. T_shaft is now 0.007891 N·m because the loop is closed. |
| P1-chain.cooling-12 | Info | Info (confirmed). | Stale handling works. A bigger drive makes E2, B5 and B1 stale and blocks the pack until each is updated in order. A step that blocks gives its downstream steps no outputs, and the dry run says so. | CoolingChainTests (3rd test) passes. K1 and K6 show the block path. Out-of-order update is not tested (see P1-chain.wet-heating-11). | None. | Open (Info). Stale handling unchanged in kind; the library staleness code was refactored (Red round). |
| P1-chain.cooling-R1 | Low | Low (new in review) | The coil's own fan is not counted. An air-to-water coil moves its air with a fan inside the enclosure. The fan's power is panel heat. The example E2 load table has no fan row, and B5 adds none. The duty is then a little low. | The example moves 0.4 kg/s of air, about 1290 m³/h. A coil fan of 100 to 150 W (owner to confirm) adds 3 to 4 % to P_cool 3422.4 W. | Add a coil fan row to the example E2 loads. Say in the pack text that the coil fan is a panel load. | Open. The pack's E2 loads still have no coil-fan row. |

## Earlier findings that propagate
I read the findings of the calculator reports. This table says which reach the chain. Rows here are not findings.

| Report and finding | Reaches the chain? | How |
|---|---|---|
| elec.load-budget-05 (Medium) | Yes | Φ counts all rows and omits the supply loss. See -09. |
| elec.load-budget-R1 (High) | No | It scales currents and the PSU, not Φ. Φ needs the mean duty. |
| elec.load-budget-03 (Medium) | Yes, if used | A blank η becomes 1, so Φ is 0 for that row. The example fills η. |
| elec.load-budget-01, -02, -04, -06 to -10, C1 | No | They concern starting peak, DC current, batteries and the PSU. |
| elec.enclosure-heat-R2 (Low) | Yes | A hot ambient blocks E2. See -04. |
| elec.enclosure-heat-01 (Medium) | Slightly | Plain areas move P_nat by about 7 %. P_nat is 237.6 W of 3660 W, so P_cool moves under 0.5 %. |
| elec.enclosure-heat-03 (Low) | Yes | T_int 189 °C prints with no validity note. See -08. |
| elec.enclosure-heat-02, -04, -05, R1 | No | They concern the zero-area block, fan curves, the pressure exponent and exhaust density. |
| bs.heat-exchanger-01 (Medium) | In kind | An isothermal choice gives a wrong dT_c. The pack uses counterflow. |
| bs.heat-exchanger-02, -03 (Low) | Slightly | Sizing blocks near ε 0.999. A_req outside 0.01 to 1000 m² gives no notice. The example is far from both. |
| bs.heat-exchanger-04 to -06 | No | They concern a KAT value, F at the limit and the crossflow fit. |
| bs.pipe-pump-C2 (Medium) | Yes | η_pump 0.7 sets T_shaft. See -10. |
| bs.pipe-pump-03 (Medium) | No here | The example is turbulent (Re 13842). Laminar water loops would inherit the Q² error. |
| bs.pipe-pump-R1, R2, -01, -04 | No | They concern NPSH and curve crossings. No curve or suction segment is given. |
| bs.pipe-pump-02, -05 to -09 | No | They concern the diameter law, spec text and display. |

## Notes for the owner's manual test
- Build the four calculations. Link E6 Φ to E2 P_add. Expect Φ 3510 W, E2 P_cool 3422.4 W and a failed natural-dissipation check.
- Link P_cool and T_max to B5, and expect A_req 3.3802 m². Expect dT_c 3.27502 K and T_c_m 23.6375 °C.
- Link B5 to B1. Expect Q_design 0.90236 m³/h, H_sys 3.6606 m and T_shaft 0.043725 N·m.
- Set B1 dz to 0 and expect T_shaft 0.007891 N·m (-05).
- Set B5 cp_c to 3600 and B1 to a custom fluid with ρ 1030 and μ 3.5 mPa·s. Expect h_f 0.789 m (-03).
- Set E2 ambient to 46 °C and expect the block (-04).

## First-pass notes
- Reviewer: P1 chain pass (Claude), 2026-10-24. This is a first pass. The Reviewed column is "—" until the independent review.
- I read the pack, its examples, the instantiation and dry-run code, and the five chain tests. I ran the Chains test classes (38 pass).
- I fed E6, E2, B5 and B1 by hand with the probe. I checked each step against a hand calculation.
- The dew point comes from bs.psychrometrics at an assumed 60 % RH. The glycol properties are illustrative and assumed.
- Residual doubt: -01 depends on the site humidity. The owner should state it.
- Residual doubt: -02 and -03 depend on the coil pressure drop and the glycol grade. The owner should state both.

## Independent review
- Reviewer: senior review (Claude), 2026-10-24, at 298ade36. Probe scripts are in /tmp/claude-0/review-inputs/s4/ (cool_rev.py, cool_gl.py).
- I read the cooling template, its example inputs and the bindings. All seven links match the report.
- I reproduced K0, K2, K3 and K6. Every value agrees with the report.
- I confirmed all four Medium findings. None is Critical or High.
- The pump head (-02) is low in normal use, but the missing loss is plant data the user types.
- -02 and -03 take the same ratings as chain.wet-heating-03 and -06, which share their cause.
- -05 is a fixture error. B1 already handles a closed loop through signed dz.
- New R1 (Low): the coil's own fan heat is not counted.
- Residual doubt: site humidity (-01), coil water-side loss (-02), glycol grade (-03) and coil fan power (R1). The owner should state each.

## Re-check after the Addendum A Blue round (0772fcf8)
- Chain changes: E6 gained a per-row heat share, and the example loads were reduced. E2 and the pack example (1.2 m wide enclosure) changed. B1 1.2.0 has a closed-circuit choice. The pack's B1 is closed with dz 0. B5 1.2.0 has `const_side`; the pack keeps counterflow and records the choice as an assumption.
- Method: I ran the pack's inputs.json through the probe step by step (E6, E2, B5, B1). Each link value came from the upstream result. Φ 3510 W, P_cool 3422.4 W, A_req 3.3802 m², dT_c 3.27502 K and the pump results match the pack README. The chain tests were not re-run.
- Findings: of 13 rows, 2 are fixed (-05 closed loop, -09 per-row heat share), 11 are open. There was no Critical or High finding. The four Medium findings -01 to -04 are all still open.
- New defects: none found.
- Probe inputs: /tmp/claude-0/recheck4/struct-bs/cool.py.
