# chain.hydraulic-power-unit — independent review of the linked chain (Track P, P1)
Pack: `pack.hydraulic-power-unit`, "Hydraulic power unit". Source files: src/Tempest.Calc/Tempest.Calc.Library/CalcPackTemplates.cs (HydraulicPowerUnit) and CalcPackExamples.cs (HpuF1, HpuF2, HpuB1, HpuE1, HpuE4). Chain tests: tests/Tempest.Calc.Tests.Library/Chains/HydraulicPowerUnitChainTests.cs. Decision: DB-03 in docs/decisions/v0.26.0-DECISIONS.md.
Steps: F1 fp.cylinder → F2 fp.accumulator → B1 bs.pipe-pump → E1 elec.motor-sizing → E4 elec.conductor-sizing.
Script: none. I fed each step by hand with the probe, using the pack's own inputs (docs/worked-examples/packs/hydraulic-power-unit/inputs.json). Harness and inputs: /tmp/claude-0/probe-inputs/hpu_*.json. Release 2f53791c. I did not re-run the .NET chain tests.
Verdict: VALUES TRANSFER CORRECTLY; ONE HIGH (-01, INHERITED FROM F2), TWO MEDIUM (-02, -03), SIX LOW OR INFO

Every binding moves the right quantity in the right unit. I reproduced the pack README to its printed digits. The links are sound. The weak points are three. The accumulator inherits two open defects of F2. Nothing ties the accumulator's end pressure to the push load. No step carries line, valve or relief-margin pressure back to the pump.

## Method re-derived
- F1 pump: Q_p = V_g n η_vp = 38.57 L/min. P = p Q_p/η_tp = 11.688 kW. T = P/ω = 76.973 N·m at 1450 rpm. The torque equals p V_g/(2π η_hm) with η_hm = η_tp/η_vp. See fp.cylinder.md.
- F1 cap-port line: Q_prs = Q_p·A_b/A_a = 75.627 L/min. This is the retract discharge from the cap side. It becomes 4.5376 m³/h in B1.
- F1 → F2: dV = A_b·stroke = 3.927 L. F1 p (160 bar gauge) fills the highest working pressure p_2. F2 stays on the Gauge basis.
- F2: V0 = ΔV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)] on absolute pressures, times T_max/T_min. See fp.accumulator.md.
- B1: v = Q/A, Re = ρ v D/μ. Re 2327 is above 2300, so Colebrook–White applies (f = 0.04891). h_f = f L/D v²/2g + K v²/2g. Δp = ρ g h_f = 247.5 kPa. Source: Darcy–Weisbach, as in bs.pipe-pump.md.
- E1 continuous duty: T_load = T_L/(i η) = 77.751 N·m. T_pk = J_total ω/t_start + T_load = 85.725 N·m. P_rated = T_rated·ω_rated = 120 N·m × 1460 rpm = 18.347 kW.
- E4: I_b = P/(√3 U cos φ η) = 18347/(√3 × 400 × 0.86 × 0.92) = 33.470 A. The 40 A type C device and a 10 mm² cable follow from the user's tables.
- A pack link takes the upstream value in the input's unit and pins the revision. A table-cell link does the same for one cell.

## Known-answer cases
The chain has no known-answer case of its own. HydraulicPowerUnitChainTests holds nine tests. The first one asserts the whole value path, as below. I did not run them.

| Test | What it asserts | What it does not assert |
|---|---|---|
| Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units | Every bound value equals its source in SI. dV, p, Q_prs, ρ, d_prs, T_shaft, n_shaft and P_rated reach their targets. F1 passes 7 of 7. F2 and E1 and E4 pass their checks. B1 warns `transition` and `fittings`. | Whether V0 is large enough (-01). Whether the push load is still met at p_1 for another load (-02). Pressure margins (-03). |
| Revising_The_Cylinder_Line_Bore_Makes_The_Pressure_Line_Stale… | A new d_prs makes B1 stale. The update carries the bore into the segment. | Other upstream changes that matter physically, such as F_push. |
| A_Bigger_Cylinder_And_Pump_Make_Every_Downstream_Step_Stale… | A change in F1 makes F2, B1, E1 and E4 stale. The pack is refused until each is updated in order. | An update out of order. |
| Without_The_Motors_Rated_Speed_The_Pack_Is_Refused… | No n_rated gives no P_rated, and the pack is refused with the name. | |

## Extra probes (chain fed by hand)
Base case: the pack's five input sets. Links came from the upstream probe result, in the target unit.

| Probe | Step and inputs (short) | Symbol | Chain value | Hand or comparison value | Effect |
|---|---|---|---|---|---|
| H0 | F1 | Q_p / P_pump / T_shaft / Q_prs | 38.57 L/min / 11.688 kW / 76.973 N·m / 75.627 L/min | same by hand | Right |
| H0 | F2 (dV 3.92699 L, p_2 160 bar g) | V0 / u_r0 / u_rp | 26.695 L / 0.97339 / 0.43334 | 26.695 L (product form) | Right; see -01 |
| H0 | B1 (4.53765 m³/h, ρ 870, D 20 mm, μ 30 mPa·s, 12 m, K 6) | v / Re / f / Δp | 4.0122 m/s / 2327.1 / 0.048912 / 247.51 kPa | Colebrook by brentq: 0.048912; 247.513 kPa | Right to 6 digits |
| H0 | B1 warnings | transition, fittings | both present | Re 2327 is in 2300–4000, K 6 | Right |
| H0 | E1 (T_L 76.973, n_L 1450) | T_load / T_pk / P_cont / u_rms | 77.751 N·m / 85.725 N·m / 11.806 kW / 0.8099 | 76.973/0.99; 0.10504 × 151.84/2 + T_load | Right |
| H0 | E1 | P_rated | 18.347 kW | 120 × 2π × 1460/60 = 18346.90 W | Right |
| H0 | E4 (P 18.3469 kW) | I_b / I_n / CSA / I_z | 33.470 A / 40 A / 10 mm² / 44.6 A | I_b by hand 33.470 A | Right; tables synthetic |
| H1 | F_push 100 kN (needs 134.0 bar at the cylinder) | all checks | F1 u_push 0.838, F2 V0 26.695 L, every check passes | p_1 stays 120 bar | Pack does not notice; see -02 |
| H2 | F1 p 176 bar (relief 10 % above 160 bar) | T_shaft / E1 u_rms | 84.67 N·m / 0.891 | 76.97 N·m / 0.810 | +10 % torque; see -03 |
| H3 | Supply flow in the B1 line at 36 and 38.57 L/min | Re / Δp | 1108 and 1187 / 64.5 and 69.9 kPa | laminar, f = 64/Re | The 247.5 kPa is the retract discharge only; see -05 |
| H4 | F1 with the cap drop 2.475 bar as p_back | F_ret | 59.04 kN | 60.88 kN without | −3.0 %; pull load 40 kN still met; see -03 |
| H5 | F1 n_p 1480 rpm (4-pole motor at 65 % load) | Q_p / Q_prs / P_cont / B1 Δp | 39.37 L/min / 77.19 L/min / 12.05 kW / 256.6 kPa | 38.57 / 75.63 / 11.81 / 247.5 | +2 %; see -04 |
| H6 | E1 T_rated 60 N·m, T_peak 140 N·m | E1 u_rms / E4 I_b, CSA | 1.620 FAIL / 16.735 A, 4 mm², all pass | load needs 21.5 A | E4 follows the nameplate; see -06 |
| H7 | B1 T 20 °C, then 80 °C | Δp | 247.51 kPa both | custom fluid | T has no effect; see -07 |
| H8 | F2 with p_basis Absolute and a linked p_2 | warning | `link-basis`: "read 1.0132 bar low" | V0 26.543 L | New warning works |
| H9 | E1 without n_rated | P_rated | absent, `rated-speed` warning | | Correct; the pack refuses |
| H10 | F2 hold-then-stroke at 10 °C, ideal gas, n 1.4 | V0 needed | 30.08 L | product 26.695 L (11 % short) | See -01 |
| H10 | same, real nitrogen (CoolProp 8.0.0, Span et al. 2000) | V0 needed | 39.43 L | product 26.695 L (32 % short) | See -01 |
| H10 | 26.695 L vessel, 10 °C, end pressure after the 3.927 L stroke | p_end | 116.1 bar g ideal, 106.6 bar g real | 107.2 bar g for 80 kN | See -01 |

## Implementation comparison
| Aspect | Engineering need | Pack | Agrees? |
|---|---|---|---|
| F1 dV → F2 dV | Liquid the accumulator gives for the stroke | Larger of the two strokes, L to L | Yes |
| F1 p → F2 p_2 | Highest working pressure, gauge | Link, bar to bar; Gauge basis kept; warns if Absolute | Yes (H8) |
| F2 sizing process | Gas held at p_2, then a fast discharge | Adiabatic from the pre-charge for every mode | No, see -01 |
| F2 p_1 against the load | End pressure still gives the push force | p_1 typed; one test asserts it for the example | No, see -02 |
| F1 Q_prs, ρ, d_prs → B1 | Judge the same line at the same flow | Three links; flow L/min to m³/h; bore by cell link | Yes; label and regime, see -05 |
| Pump pressure | Cylinder pressure + line, valve and filter drops, below the relief setting | p everywhere; B1 drop not fed back | No, see -03 |
| F1 T_shaft, n_shaft → E1 | Pump torque at the worst continuous duty | Torque at p, full delivery over relief; N·m and rpm | Yes at p; see -03 |
| Pump speed against motor speed | Same shaft, same speed | n_p 1450 typed in F1, n_rated 1460 typed in E1 | Partly, see -04 |
| E1 P_rated → E4 P | Cable for the motor that will be fitted | Nameplate rated power, W to kW | Yes; but see -06 |
| Fluid data | One fluid, one temperature | ρ linked; μ typed in B1; T unused | Partly, see -07 |
| Placeholders | Real mounting, safety factor, limits | F1 uses defaults K_m 1, S_f 3.5, L_add 100, E, f_y; E4 tables synthetic | Disclosed, see -08 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-chain.hydraulic-power-unit-01 | High | — | The accumulator in the pack inherits fp.accumulator-R1 (Critical) and -R2 (High). F2 uses the adiabatic-from-pre-charge form. The pack's use is a long hold at 160 bar, then a stroke. The 26.695 L is short, and the 120 bar claim for the end of the stroke fails when cold. The README says 120 bar still finishes the 80 kN push. | H10: ambient 10 °C, pre-charge 105 bar g at 50 °C, p_2 160, p_1 120, n 1.4, dV 3.927 L. Ideal gas needs 30.08 L (product 0.887 of it). Real nitrogen needs 39.43 L (0.677). In the 26.695 L vessel the stroke ends at 116.1 bar g (ideal) or 106.6 bar g (real). The push needs 107.2 bar g. At 20 °C real: 108.8 bar g. The product's V0 already includes the cold-gas factor 1.1413, which is 4 % above the exact 1.0975. | Close fp.accumulator-R1 and -R2. Then re-pin V0 in HydraulicPowerUnitChainTests and the README. Change the README claim until then. |
| P1-chain.hydraulic-power-unit-02 | Medium | — | Nothing links the accumulator's lowest pressure p_1 to the push load. F1 judges the push at p = 160 bar. F2 sizes between p_1 and p_2. A larger F_push leaves every check green while the accumulator cannot finish the stroke. The test asserts p_1 > 107.2 bar for the example only. | H1: F_push 100 kN needs 134.0 bar at the cylinder (100 kN/(78.54 cm² × 0.95)). p_1 stays 120 bar. F1 u_push 0.838. F2 V0 26.695 L. All 16 checks pass. | Add an F2 check or a link: p_1 ≥ F_push/(A_b η_m) + p_back + line drop. Take A_b and η_m from F1 by link. |
| P1-chain.hydraulic-power-unit-03 | Medium | — | No pressure allowance reaches the pump. The pump pressure equals the cylinder pressure, and p is also the stall pressure. A real unit has valve and filter drops, and a relief valve above the working pressure. Pump torque, E1 and the rated power are then low. The pack README states that B1's drop is not fed back. | H2: a relief setting of 176 bar (10 % up, recalled practice; owner to confirm) gives T_shaft 84.67 N·m against 76.97 and E1 u_rms 0.891 against 0.810. H3: the line alone gives 0.65 to 0.70 bar on the supply side. H4: the cap drop 2.475 bar as back pressure lowers F_ret from 60.88 to 59.04 kN (−3.0 %). The example motor has 55 % margin, so it stays green. | Add a supply-side drop and a relief margin to F1 as inputs. Take the B1 drop by link into F1 `p_back`. State which pressure the accumulator and the stall load use. See fp.cylinder-02 and -06. |
| P1-chain.hydraulic-power-unit-04 | Low | — | The pump speed and the motor speed are two typed numbers. F1 n_p is 1450 rpm. E1 n_rated is 1460 rpm (invented). A 4-pole motor at 65 % load turns near 1480 rpm. Pump flow, line velocity, B1 drop and shaft power then rise about 2 %. No notice says so. | H5: n_p 1480 gives Q_p 39.37 L/min, Q_prs 77.19 L/min, Δp 256.6 kPa, P_cont 12.05 kW. T_shaft stays 76.97 N·m. | Warn when n_p differs from the E1 running speed. Or take n_p from the motor's loaded speed. |
| P1-chain.hydraulic-power-unit-05 | Low | — | B1's "pressure line" result is the retract discharge from the cap port, not the supply line. The supply line carries 36 to 38.6 L/min and is laminar (Re 1100 to 1190). B1's 247.5 kPa, at Re 2327, is above the 2300 switch where f jumps from 0.0275 to 0.0489. The README says this. The B1 step title and result name can still be read as the pump supply drop. | H3: supply flow Δp 64.5 and 69.9 kPa. H0: retract discharge 247.5 kPa (157.6 kPa if laminar). Cause: fp.cylinder-04 and bs.pipe-pump-06. | Title the B1 step "Cap-port line at the retract discharge". Offer a second B1 step at the supply flow, or a flow-direction input. |
| P1-chain.hydraulic-power-unit-06 | Low | — | E4 is sized on the nameplate power only. If the motor is smaller than the pump load, E1 fails but E4 passes on the smaller nameplate. The cable then fits the motor and not the load. The E1 FAIL is the only flag. | H6: T_rated 60 N·m. E1 u_rms 1.620 FAIL. P_rated 9.17 kW. E4 I_b 16.7 A, 4 mm², all pass. The pump load P_cont is 11.81 kW, which needs 21.5 A. | Use P = max(P_rated, P_cont/η_mech) for E4. Or warn on E4 when a linked E1 check fails. |
| P1-chain.hydraulic-power-unit-07 | Low | — | The oil data are split. ρ is linked from F1. μ (30 mPa·s) is typed in B1. B1's temperature input (50 °C) has no effect for a custom fluid, and no sheet states the temperature of ρ or μ. Cold oil is more viscous and would drop more pressure. | H7: T 20 °C and 80 °C give the same Δp 247.51 kPa. README: viscosity taken at 50 °C only. | Label B1 `T` as unused for a custom fluid, or warn. State the reference temperature of ρ and μ on F1 and B1. |
| P1-chain.hydraulic-power-unit-08 | Low | — | The example leaves the rod mounting factor (K_m 1, pinned-pinned), the safety factor (3.5), the line limits, the buckling length add-on L_add (100 mm), E and f_y at sheet placeholders. F1 shows seven "not from a record" warnings. The pack still issues and shows rod buckling PASS at 0.246. A fixed-free mounting (K 2) gives 0.319. | H0 notices. Probe: K_m 2 gives u_buck 0.319, F_cr 1378.6 kN. See fp.cylinder-07. | State in the pack text that mounting and safety factor are the user's records. Consider a pack notice listing them. |
| P1-chain.hydraulic-power-unit-09 | Info | — | The bindings are right in kind and in units. dV, p, Q_prs, ρ, d_prs, T_shaft, n_shaft and P_rated reach their targets with the right unit conversion. The new link-basis warning on F2 fires for an Absolute basis. | H0 and H8: Q_prs 75.627 L/min = 4.53765 m³/h; d_prs 20 mm into segments[0].D; P_rated 18346.90 W = 18.3469 kW. | None. |

## Earlier findings that propagate
I read the findings of the four calculator reports and fp.cylinder.md. This table says which reach the chain. Rows here are not findings.

| Report and finding | Reaches the chain? | How |
|---|---|---|
| fp.accumulator-R1 (Critical) | Yes | Emergency-stroke form is the wrong process path. See -01. |
| fp.accumulator-R2 (High) | Yes | n 1.4 is far below nitrogen's real exponent at 120 to 160 bar. No warning below 200 bar abs. See -01. |
| fp.accumulator-01, -02 (High, Medium) | Yes, in kind | The real-gas factor f_rg stays 1. p_2 is 161 bar abs, so no `realgas` warning appears. Real nitrogen needs 39.4 L (H10). |
| fp.accumulator-03 (Medium) | Yes | The T_max/T_min factor 1.1413 against the exact 1.0975 adds 4 % to V0. It is conservative. |
| fp.accumulator-04, -05, R3 | No | Pre-charge rules pass at 0.973 and 0.433. PS and vessel are not entered. |
| fp.cylinder-02 (Low) | Yes | No pressure drop allowance. See -03. |
| fp.cylinder-04 (Low) | Yes | Q_prs is the cap-side discharge. See -05. |
| fp.cylinder-06 (Info) | Yes | Stall pressure is the working pressure. See -03. |
| fp.cylinder-07 (Info) | Yes | L_add and K_m placeholders. See -08. |
| fp.cylinder-01, -03, -05, -08 | No or slightly | Regenerative mode, short pumps and piston speed are not used. |
| bs.pipe-pump-06 (Info) | Yes | The friction factor jumps at Re 2300. The pack sits at Re 2327. See -05. |
| bs.pipe-pump-03 (Medium) | No | The pack has no pump curve. The Q² law is not used. |
| bs.pipe-pump-01, -02, -04, -05, -07 to -09, R1, R2 | No | NPSH, affinity, duty point, spec text and display are not used. |
| bs.pipe-pump-C1, C2 | No | B1 is not given a pump. T_shaft comes from F1. |
| elec.motor-sizing-07 and -C4 (Low) | Yes | Continuous duty lists default notices for unused inputs. |
| elec.motor-sizing-08 (Info) | Yes | u_speed has no safety factor. |
| elec.motor-sizing-C1 (Medium) | In kind | A blank T_L passes. The pack links T_L; without a pump in F1 the link has nothing to bind. |
| elec.motor-sizing-01 to -06, R1, R2, C2, C3, E1 | No | They concern the move cycle and signs. The pack sets η 0.99 and links T_L. |
| elec.conductor-sizing-01 (High), -03 (Medium) | In kind | θ_a 30 °C and C_a 1 are defaults. A hot pump room needs C_a typed in. The machine context uses θ_ref 30 °C. |
| elec.conductor-sizing-05 (Medium) | In kind | The tables are user records and here synthetic. |
| elec.conductor-sizing-02, -04, -06 to -10, R1, E1 | No | Different inputs, or no buried cable. |

## Notes for the owner's manual test
- Build the five calculations. Type nothing for the linked values. Expect F1 Q_p 38.57 L/min, P_pump 11.688 kW, T_shaft 76.973 N·m and Q_prs 75.627 L/min.
- F2: expect V0 26.695 L. Then compute hold-then-stroke by hand at 10 °C: 30.08 L ideal. A nitrogen table gives about 39 L. Compare (-01).
- B1: expect Re 2327.1, f 0.048912, Δp 247.51 kPa and two warnings. Set Q to 2.314 m³/h and expect Re 1187 and Δp 69.9 kPa (-05).
- E1 and E4: expect T_load 77.751 N·m, P_rated 18.347 kW, I_b 33.470 A. Change T_rated to 60 N·m: E1 fails, E4 passes (-06).
- Raise F_push to 100 kN in F1. Expect every check to stay green while the push needs 134 bar and p_1 is 120 bar (-02).
- Set F1 p to 176 bar. Expect T_shaft 84.67 N·m and E1 u_rms 0.891 (-03).
