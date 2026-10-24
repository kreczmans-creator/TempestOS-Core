# chain.wet-heating — independent review of the linked chain (Track P, P1)
Pack: `pack.wet-heating`, "Wet heating". Source file: src/Tempest.Calc/Tempest.Calc.Library/CalcPackTemplates.cs. Chain tests: tests/Tempest.Calc.Tests.Library/Chains/WetHeatingChainTests.cs. Cell-link tests: tests/Tempest.Calc.Tests.Library/CellLinkTests.cs.
Steps: B4 bs.room-heat-loss (room A) and B4 (room B) → B6 bs.sealed-system → B1 bs.pipe-pump → E1 elec.motor-sizing.
Script: none. I fed each step by hand with the probe. The inputs are in /tmp/claude-0/recheck2/ (wh.py, ref.py, wh2.py, wh3.py, gen_pp.py). Release e283c07e (A7a).
Verdict: VALUES TRANSFER CORRECTLY; TWO HIGH (-01 INHERITED FROM B4, R1 AFFINITY SPEED NOT PASSED TO E1), FOUR MEDIUM (-03 to -06), NINE LOW OR INFO (after independent review)

Every binding moves the right physical quantity in the right unit. The flow from the heat load matches the hand value to 6e-11. The problems are four. The test's reference loop carries a net lift that a closed loop cannot have. Some values stay typed beside linked ones. A B4 defect runs through the whole chain. A pump speed change never reaches E1 (R1).

## Method re-derived
- B4 gives Φ_HL in W: the steady design heat loss of one room at its θ_int and θ_e. Source: see docs/verification/rederivation/bs.room-heat-loss.md.
- Table-cell link: B4 Φ_HL of room A fills B6 `rooms` row 1, column `Phi` ("Heat required"). Room B fills row 2. The pack counts rows from 0, the stale messages from 1.
- B6: Φ_tot = Σ Φ_i over the rows, in W. Flow-to-return difference dT_fr = t_f − t_r in K. Mean water temperature t_m = (t_f + t_r)/2 in °C.
- B1: Q = Φ/(ρ c_p ΔT). ρ is Kell (1975) at t_m. c_p is 4186 J/(kg·K), recorded as assumed. Source: first law for a water circuit.
- B1 head and power: H_sys = Σ dz + h_out + Σ h_f. P_hyd = ρ g Q H_sys. P_shaft = P_hyd/η_pump. T_shaft = P_shaft/ω with ω = 2π n_pump/60.
- E1 continuous duty: T_load = T_L/(i η). T_rms = T_load. T_pk = J_total ω/t_start + T_load. P_cont = T_load ω. See docs/verification/rederivation/elec.motor-sizing.md.
- A cell link takes the upstream output in the column's default unit and pins the revision. It is stale when the revision moved or the output is gone. It is also stale when the value differs by more than 1e-9 relative, or the upstream is stale.
- A closed circuit has no net static head: the flow rises and the return falls. The circulator lifts nothing and covers friction, plant and emitter losses.

## Known-answer cases
The chain has no known-answer case of its own. WetHeatingChainTests holds five tests, and CellLinkTests holds nineteen. I ran both classes: 24 pass.

| Test | What it asserts | What it does not assert |
|---|---|---|
| Room_Heat_Reaches_The_Sealed_System_The_Pipework_Flow_And_The_Motor_Load | Each bound value equals its source in SI. Φ_tot is the sum. Q_design equals Φ/(ρ·4186·10). T_shaft equals P_hyd/0.7/ω. E1 passes its three checks. No step blocks. | Whether the head is right for a closed circuit. Whether room B is complete (-01). Whether B6 checks anything (-07). |
| A_Changed_Room_Makes_B6_B1_And_E1_Stale_And_Blocks_The_Pack_Until_Each_Is_Updated | A bigger room A makes B6, B1 and E1 stale. The pack cannot be checked or issued. Updating in order b6, b1, e1 clears it. | An update out of order (-11). |
| The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet | The pack checks and issues. The issued sheet names each cell link by row. An issued step is frozen. | The engineering meaning of any value. |
| B6_Is_Refused_Rows_It_Was_Not_Given | Instantiating without B6 rows is refused. No row is ever created. | More than two rooms (-08). |
| The_Heat_Load_Flow_Replaces_A_Design_Flow_With_A_Notice_And_Needs_Its_Temperature_Difference | Φ alone blocks. Φ with Q warns. A missing T warns. | The torque chain beyond B1. |

## Extra probes (chain fed by hand)
Base case: the two B4 worked cases (cavity wall room A, MVHR partition room B) and B6 at 75/65/20 °C. B1 has DN28 × 40 m, ΣK 12, dz 3 m and 2800 rpm. E1 is direct rotary in continuous duty with J_L 0.002 kg·m² and a 5 s run-up. I entered room U-values as overrides.

| Probe | Step and inputs (short) | Symbol | Chain value | Correct or comparison value | Effect |
|---|---|---|---|---|---|
| W0 | B4 room A / room B | Φ_HL | 350.673 W / 243.872 W | same | B4 right |
| W0 | B6 rows 1 and 2 | Φ_tot / dT_fr / t_m | 594.545 W / 10 K / 70 °C | 350.673 + 243.872 = 594.545 | Cell links and sum right |
| W0 | B1 | Q_design | 1.452609e-5 m³/s (0.05229 m³/h) | 594.545/(977.7696·4186·10) = 1.452609e-5 | Flow right to 6e-11 |
| W0 | B1 | H_static / h_f / P_hyd / P_shaft / T_shaft | 3 m / 0.00196 m / 0.4181 W / 0.5973 W / 2.0372e-3 N·m | 0.5973/293.2153 = 2.0372e-3 | Torque right for the head given; see -02 |
| W0 | E1 continuous | T_load / T_pk / P_cont / P_pk | 2.2635e-3 N·m / 0.1443 N·m / 0.6637 W / 42.31 W | T_load = 2.0372e-3/0.9 | Right; see -09 |
| W1 | B1 with dz 0 (closed circuit) | T_shaft | 1.331e-6 N·m | 2.037e-3 N·m with dz 3 m | 1530 times lower, see -02 |
| W2 | 24 kW, 10 K, DN32 × 60 m, ΣK 15; dz 0, 3, 6 m | T_shaft | 0.0456, 0.1278, 0.2099 N·m | dz 0 is the closed circuit | 2.8 and 4.6 times too high, see -02 |
| W3 | Room B with n_inf 0.1 h⁻¹ (MVHR infiltration) | Φ_HL(B) / Φ_tot / T_shaft | 284.67 W / 635.34 W / 2.177e-3 N·m | 243.87 W / 594.54 W / 2.037e-3 N·m | Pack is 6.9 % low, see -01 |
| W4 | 350.4 W room, Φ50 400 W; t_i 20 then 21 °C | ΔT / u_emit | 50 K / 0.876, then 49 K / 0.899 | Room is designed at 21 °C | 2.7 % low per kelvin, see -05 |
| W5 | B6 V_pipe 20 L, then 24.63 L (B1 DN28 × 40 m) | V_e | 2.496 L, then 2.800 L | 24.63 L is the B1 pipe volume | 12.2 % low, see -04 |
| W6 | B1 η_pump 0.7, then 0.2 | T_shaft | 2.037e-3, then 7.13e-3 N·m | Small circulators run at low η | 3.5 times, see P1-bs.pipe-pump-C2 |
| W7 | B1 with an illustrative glycol (ρ 1030, μ 2 mPa·s, c_p 3800) | Q_design / h_f | +4.6 % / ×4.25 | Water | Fluid not carried, see -06 |
| W8 | B6 with the pack's rows (Φ50 0, no V_vessel) | checks | none listed | Emitter and vessel checks absent | See -07 |
| W9 | B1 with a pump curve (0,6) (1,5) (2,3.5) (3,1) m³/h, 24 kW, dz 0, 45 °C, 1450 rpm, η 0.55 | P_shaft_sel / T_shaft | 28.32 W / 0.1865 N·m | Design flow gives 17.26 W / 0.1137 N·m | Torque covers the duty point; right |
| W10 | B6 rooms row with Φ −779.9 W | block | "rooms[1].Phi must be at least 0 W" | Clear block | See -13 |
| W11 | B6 60/60/20 °C log mean, then B1 | dT_fr / block | 0 K / "dT_load must be at least 0.01 K" | Clear block | See -13 |

## Implementation comparison
| Aspect | Engineering need | Pack | Agrees? |
|---|---|---|---|
| B4 Φ_HL → B6 `rooms` Φ | Room design heat loss as the emitter load, in W | Cell link to row 1 and row 2, column `Phi`, W to W | Yes; B4 defects pass through, see -01 |
| Σ rooms → system load | Sum of room loads | B6 Φ_tot adds every row | Yes for these rooms; no pipe loss, no reheat, see -08 |
| Room temperature | θ_int per room in B4 and B6 | Not linked; B6 has one t_i | No, see -05 |
| B6 Φ_tot, dT_fr, t_m → B1 | Q = Φ/(ρ c_p ΔT) at the loop temperature | Three links; units K and °C converted | Yes (-14) |
| Pipe volume B1 → B6 | One water volume for the vessel | Not linked | No, see -04 |
| Fluid B6 → B1 | Same fluid in both | Not linked | No, see -06 |
| Pump head | Friction plus plant and emitter loss; no static lift | Signed Σdz, which is 0 for a loop entered as flow and return; the test fixture enters 3 m; no plant loss asked for | Partly, see -02 and -03 |
| B1 T_shaft, n_shaft → E1 T_L, n_L | Torque and speed at the pump shaft | Linked, N·m and rpm, always at the rated speed | Yes at n_ratio 1; not after an affinity shift (R1); η, inertia and run-up differ, see -09 |
| Electrical input | Motor efficiency to the supply | Not in the chain | No, see -10 |
| Stale handling | Judged like input links | Same rules; transitive; pack blocked while stale | Yes; out-of-order update, see -11 |
| Cell provenance | Part of the record | Outside hashes and CSV | Partly, see -12 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-chain.wet-heating-01 | High | High (confirmed, as a propagation). Reproduced W3: Φ_HL(B) 284.67 W, Σ 635.34 W, T_shaft 2.177e-3 N·m. It is the same defect as bs.room-heat-loss-R1. Fix it once in B4, then add n_inf to the chain fixture. | The pack's own reference room B is an MVHR room with a blank infiltration rate. It carries bs.room-heat-loss-R1: the infiltration loss is dropped without a notice. The error runs unchanged through B6, B1 and E1. This is a propagation, not a new defect. | W3: room B (ka-mvhr-partition) Φ_HL 243.87 W. With n_inf 0.1 h⁻¹ it is 284.67 W (+16.7 %). Σ is 594.54 W against 635.34 W (+6.9 %). Q_design 1.4526e-5 against 1.5523e-5 m³/s. T_shaft 2.037e-3 against 2.177e-3 N·m. | Close bs.room-heat-loss-R1 in B4. Then re-pin the reference room B in WetHeatingChainTests and in the manual test. |
| P1-chain.wet-heating-02 | Medium | Low (downgraded). B1 sums signed dz, so it already handles a closed loop. Flow +3 m and return −3 m give H_static 0 (probe). The 3 m sits on one segment named "primary flow and return" in the chain test. That is a fixture error, and it errs on the safe side. The 1530× ratio only reflects a 2 mm friction head. Fix: set the fixture dz to 0 and re-pin. Label dz as the net rise, zero round a closed loop. Optionally warn when a heat load is given and Σdz ≠ 0. Do not add a switch that zeroes H_static. | B1 counts every dz as pumped static lift. A closed heating circuit has none: the flow rises and the return falls. The pack's reference case sets dz 3 m, so the torque is almost all static lift. The error is conservative, but it makes the reference values unrepresentative. | W0 and W1: H_static 3 m, h_f 0.00196 m. T_shaft 2.037e-3 N·m. With dz 0 it is 1.331e-6 N·m (1530 times less). W2: 24 kW loop gives 0.0456, 0.1278 and 0.2099 N·m for dz 0, 3 and 6 m. | Add a closed-circuit choice to B1 that sets H_static to zero. Or sum flow and return dz. State it in the pack text. Change the reference case. |
| P1-chain.wet-heating-03 | Medium | Medium (confirmed). Unconservative in normal use: h_out defaults to 0 and the pack names no plant, valve or emitter loss. Typical loss values: owner to confirm. | The pump head of a heating circuit also covers the boiler or heat exchanger, valves and emitters. B1 has pipe segments, K values and h_out (default 0). The pack asks for none of these losses, so the head can be pipe friction alone. The torque is then low. Owner to confirm typical values. | Reference case: H_sys 3.002 m, of which 0.002 m is friction. The 24 kW loop (W2) has h_f 1.664 m from pipe and fittings alone. Nothing in the pack names plant or emitter loss. | List plant, valve and emitter losses in the pack text. Warn in B1 when h_out is 0 on a heat-load sheet. Offer a plant-loss input. |
| P1-chain.wet-heating-04 | Medium | Medium (confirmed). Reproduced V_e 2.496 L at the 20 L default. DN28 × 40 m holds 24.63 L (π D² L/4). Unconservative whenever the typed volume is low. | B6's pipe water volume V_pipe is typed. It is not linked to the B1 pipe segments. The expansion vessel is sized on a different volume from the pipework the flow runs through. The error is unconservative when the typed volume is smaller. | W5: DN28 × 40 m holds 24.63 L. The B6 default is 20 L. V_e is 2.496 L against 2.800 L with 24.63 L (+12.2 %). | Give B1 a port for the water volume (π D² L/4 per segment). Bind it to B6 V_pipe. Or warn on the pack page that the two must match. |
| P1-chain.wet-heating-05 | Medium | Medium (confirmed). Reproduced W4. The defaults clash (B4 21 °C, B6 20 °C), so normal use overstates emitter output by about 2.7 % per kelvin. Unconservative but small. | B4 takes a design temperature per room (θ_int, default 21 °C). B6 takes one room temperature t_i (default 20 °C) for every emitter. Neither is linked. A room warmer than t_i gets its emitter output overstated. | W4: 350.4 W room, Φ50 400 W, 75/65 °C. t_i 20 °C gives ΔT 50 K and u_emit 0.876. t_i 21 °C gives ΔT 49 K and u_emit 0.899 (+2.7 %). | Add a room temperature column to the B6 rooms table. Link it to B4 θ_int. Or warn when t_i differs from a linked room's θ_int. |
| P1-chain.wet-heating-06 | Low | Medium (upgraded). Water in B1 understates flow and friction for a glycol loop, which is unconservative. B1 stays on water silently. Glycol is common on heat-pump circuits. The ×4.25 figure is laminar (Re 356) and overstates a turbulent loop. Glycol properties: owner to confirm. | The fluid is not carried from B6 to B1. B6 can take a glycol expansion record (e_fluid). B1 stays on water unless the user types a custom fluid with density, viscosity and c_p. Flow and friction then differ. | W7 (illustrative fluid, values assumed): Q_design +4.6 %. h_f ×4.25 on the reference case (Re 356, laminar). | Say in the pack text that B1's fluid must match B6's. Or add a fluid choice that B1 reads. |
| P1-chain.wet-heating-07 | Low | Low (confirmed). The 8 L of placeholder water errs on the safe side for the vessel. | The pack's reference B6 rows are placeholders. They carry Φ 1 W (the link replaces it), no selected emitter (Φ50 0) and 4 L of water. No emitter is chosen and no vessel is entered, so B6 shows no check. The 8 L of placeholder water also stays in V_s. | W8: B6 with Φ50 0 and no V_vessel lists no check. V_s is 38 L, of which 8 L is the two placeholder rows. | State in the pack text that the user types each room's emitter output and water content, and the vessel. Warn on the pack when no emitter is chosen. |
| P1-chain.wet-heating-08 | Low | Low (confirmed). Links live in the cell provenance, so reordering rows keeps each link on its room (code reading). | The pack holds exactly two rooms, bound by row position. Extra rooms are typed into B6 by hand. They have no B4 sheet, no link and no stale check. B6 row names are not linked to the B4 sheets. | WetHeatingChainTests gives B6 two rows. The bindings are row 0 and row 1. A typed third row adds to Φ_tot with no notice. | Offer packs for three and four rooms. Or let a pack add one B4 step per B6 row. Link the room names. |
| P1-chain.wet-heating-09 | Low | Low (confirmed). Reproduced T_pk 0.1443 N·m and J_ratio 11.1. | E1 receives only torque and speed. The inertia and the run-up time are typed. They set the peak torque, the inertia-ratio warning and u_peak. The candidate motor defaults are servo values, so the checks say little. | Reference E1: J_L 0.002 kg·m², t_start 5 s. T_pk 0.1443 N·m is 64 times T_load 0.002264 N·m. J_ratio 11.1 exceeds 10 and warns. u_rms 0.0014 against T_rated 2 N·m. | Offer pump inertia (impeller and water) as a B1 output or a pack prompt. State that E1 checks a typed candidate motor. |
| P1-chain.wet-heating-10 | Low | Low (confirmed). | The pack ends at shaft power. The circulator's electrical input needs a motor efficiency, and nothing binds it to E6. The supply cable and protection of the circulator are outside the chain. | E1 P_cont 0.6637 W is mechanical power at the motor shaft, after the transmission loss. E1 has no electrical-power output. | Add a motor efficiency input and an electrical-power port to E1. Bind that to E6 P_add. See P1-chain.motor-circuit-C1. |
| P1-chain.wet-heating-11 | Low | Low (confirmed by code reading). UpdateLinksAsync checks cycles and depth only. E1 then shows stale again, so the pack stays blocked. That is safe. | An update out of chain order is not refused. Updating E1 before B1 re-pins E1 to B1's old values, and E1 is stale again. For a checked or issued step each update makes a new revision. | Code reading, not run: UpdateLinksAsync reads the upstream's current outputs and never tests whether the upstream is stale. The chain test updates in order b6, b1, e1. | Warn or refuse when a step is updated while its upstream is stale. Offer one command that updates a pack in chain order. |
| P1-chain.wet-heating-12 | Low | Low (confirmed from DA-21; not reproduced). | Cell provenance is outside every hash, and the CSV export drops it (DA-21). A cell link does not change the content hash. A CSV round trip turns a linked cell into a typed number with no stale check. | Taken from DA-21 and the CHANGELOG. Not reproduced: the probe has no export. | Include cell links in the revision hash. Or warn on export that links are dropped. |
| P1-chain.wet-heating-13 | Info | Info (confirmed; not re-run). | Edge states stop the chain with a clear block. A net heat-gain room (B4 Φ_HL below 0) makes B6 refuse the cell. B6 accepts t_f = t_r with the log mean. Then dT_fr is 0 K and B1 blocks. | W10: Φ −779.9 W gives "rooms[1].Phi must be at least 0 W". W11: 60/60/20 °C gives dT_fr 0 and "dT_load must be at least 0.01 K". | None needed. Optionally let a heat-gain room enter as 0 W with a notice. |
| P1-chain.wet-heating-14 | Info | Info (confirmed). Reproduced W0: Q_design 1.452609e-5 m³/s, T_shaft 2.037e-3 N·m. | The bindings are right in kind and in units. Φ_HL sums to Φ_tot in W. dT_fr and t_m convert from K and °C. Q uses ρ at the mean temperature. T_shaft and n_shaft keep N·m and rpm. | W0: Q_design 1.452609e-5 m³/s against the hand value 1.452609e-5. T_shaft 2.037166e-3 N·m = 0.597328 W / 293.2153 rad/s. E1 T_load = T_shaft/0.9. | None. Note c_p is fixed at 4186 J/(kg·K) and ρ is taken at t_m. Both are within about 0.5 % for 40 to 90 °C (recalled). |
| P1-chain.wet-heating-R1 | High | High (new in review) | B1 hands E1 the torque and speed at the rated speed n_pump. This holds even when n_ratio or D_ratio moves the pump to a new duty. n_shaft is n_pump, not n_pump·n_ratio. The shifted duty power P_shaft2 never enters T_shaft. For n_ratio or D_ratio above 1 the motor load is too low, with no notice. | 24 kW, 10 K, 70 °C, DN32 × 60 m, ΣK 15, dz 0, n_pump 2800 rpm, curve (0,6) (1,5) (2,3.5) (3,1) m³/h. n_ratio 1: T_shaft 0.0740 N·m. n_ratio 1.2: P_shaft2 37.48 W at 3360 rpm, so 0.1065 N·m. The sheet still gives T_shaft 0.0740 N·m and n_shaft 2800 rpm. Shaft power to E1 is 42 % low. n_ratio 0.8 errs on the safe side. | When the affinity shift is used, give T_shaft from P_shaft2 at n_pump·n_ratio and set n_shaft to that speed. Or take the larger torque and warn. Record the same defect in bs.pipe-pump. |

## Earlier findings that propagate
I read the findings of the four calculator reports. This table says which reach the chain. Rows here are not findings.

| Report and finding | Reaches the chain? | How |
|---|---|---|
| bs.room-heat-loss-R1 (High) | Yes | Reference room B drops infiltration. See -01. |
| bs.room-heat-loss-R2 (Medium) | Yes, if used | A double reduction in a room lowers Φ_HL. It passes to B6, B1 and E1 without a notice. |
| bs.room-heat-loss-04 (Info) | Yes | No reheat or intermittent allowance in Φ_HL. B6 sizes emitters on the steady loss. |
| bs.room-heat-loss-03, -05 | Slightly | A blank roof Rsi lowers Φ_T. The 0.34 constant adds 1.5 % to Φ_V. |
| bs.room-heat-loss-01, -02 | No | U_m is unused. The colder adjacent temperature only raises Φ. |
| bs.sealed-system-01 to -03, -05, R1 | No | They change the vessel only. The pack's reference B6 has no vessel selected (-07). |
| bs.sealed-system-04, -06, -07 | No | They change the emitter factor only. dT_fr and t_m do not depend on the ΔT method. |
| bs.sealed-system-C1 (Low) | Yes, as a hazard | Linking dT instead of dT_fr gives a flow 5 times too small. |
| bs.pipe-pump-03 (Medium) | Yes, with a pump curve | The Q² law is wrong for laminar flow. The reference case is laminar (Re 1600). The duty-point power and T_shaft inherit it. |
| bs.pipe-pump-04 (Medium) | Yes, with a pump curve | With two crossings there is no duty point. T_shaft silently falls back to the design-flow power. |
| bs.pipe-pump-C2 (Medium) | Yes | Default η_pump 0.7 sets the torque. See W6. |
| bs.pipe-pump-01, -02, -05 to -09, R1, R2 | No | They concern NPSH, the affinity law, spec text and display. The pack asks for none of them. |
| elec.motor-sizing-07 and -C4 | Yes | Unused inputs raise default notices on the E1 step. |
| elec.motor-sizing-08 (Info) | Yes | u_speed has no safety factor. |
| elec.motor-sizing-R2 | In kind | The peak check compares T_pk with the stall peak, not with the torque at speed. Small for a pump. |
| elec.motor-sizing-C3 (Info) | Yes | Direct coupling carries η 0.9, so T_load is 11 % above T_shaft. Conservative. |
| elec.motor-sizing-01 to -06, R1, C1, C2 | No | They concern the move cycle, a blank T_L and a negative T_L. The pack links T_L. |

## Notes for the owner's manual test
- The desktop has no pack screen and no cell-link screen. Build the five calculations by hand. Type room A's Φ_HL (350.673 W) and room B's (243.872 W) into B6 yourself. Link the other five values with the Link panel.
- Build the reference case. Expect Φ_tot 594.545 W, Q_design 0.052294 m³/h, T_shaft 2.0372e-3 N·m, E1 T_load 2.2635e-3 N·m and P_cont 0.66370 W.
- Set dz to 0 in B1. Expect T_shaft 1.331e-6 N·m (-02). Splitting the loop into flow (+3 m) and return (−3 m) gives the same.
- Add n_inf 0.1 to room B. Expect Φ_HL 284.67 W and T_shaft 2.177e-3 N·m (-01).
- Set B6 V_pipe to 24.63 L. Expect V_e 2.800 L against 2.496 L (-04).
- Compare B6 t_i 20 and 21 °C with a 400 W emitter (-05).

## First-pass notes
- Reviewer: P1 chain pass (Claude), 2026-10-24. This report is the first pass for the chain. The independent review below has since filled the Reviewed column.
- I read the pack, its validation and instantiation, and the cell-link code. I read E1's continuous branch, B1's heat-load branch and B6's new outputs. I ran WetHeatingChainTests and CellLinkTests (24 pass).
- I fed B4, B6, B1 and E1 by hand with the probe. I checked each step against a hand calculation.
- The B4 inputs use U-value overrides in place of the layer table. They reproduce Φ_HL 350.673 W and 243.872 W exactly.
- Residual doubt: -03 and -06 depend on typical plant losses and glycol properties. I did not take them from a catalogue. The owner should state them.
- Residual doubt: -11 is from code reading only. The probe cannot keep saved calculations.
- Residual doubt: -12 repeats the project's own decision record. I did not test an export.

## Independent review
- Reviewer: senior review (Claude), 2026-10-24, at release e283c07e. Probe inputs are in /tmp/claude-0/review-inputs/wh/.
- I re-ran W0, W1, W3, W4 and W5 with the probe. All reported values agree.
- I added a split loop (flow +3 m, return −3 m). It gives H_static 0, so B1 handles a closed loop when the loop is entered as one.
- -02 drops from Medium to Low. The 3 m lift is a test-fixture error, not a B1 defect, and it errs on the safe side.
- -06 rises from Low to Medium. Water in place of glycol understates flow and friction.
- New R1 (High): an affinity speed or diameter change never reaches T_shaft or n_shaft. At n_ratio 1.2 the shaft power to E1 is 42 % low.
- Bindings checked for direction of error: Φ_HL, Φ_tot, dT_fr and t_m transfer exactly. T_shaft and n_shaft are unconservative only through R1.
- I read the cell-link code. Links ride on the cell provenance, so they survive a row reorder.
- W9 used 45 °C, 1450 rpm and η 0.55. I added these to its row.
- Residual doubt: plant losses (-03), circulator efficiency (bs.pipe-pump-C2) and glycol properties (-06). The owner should state them.
- Residual doubt: R1 sits in B1 and should also be logged in the bs.pipe-pump report.
