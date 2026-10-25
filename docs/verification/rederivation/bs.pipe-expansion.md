# bs.pipe-expansion — independent re-derivation (Track P, P1)
Calculator: Pipe expansion, loops and anchor forces (screening), version 1.2.0. Source file: src/Tempest.Calc/Tempest.Calc.BuildingServices/PipeExpansionCalculator.cs. Spec: docs/calcs/building-services/bs.pipe-expansion.md.
Script: tools/verification/bs.pipe-expansion.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (reviewed: one Medium, R1; the rest Low or Info)

The product matches the independent script on all 75 KAT values and on 12 extra probes (200 values, relative difference below 1e-9).
It also behaves as specified on 14 guard probes. The findings are one silent-input inconsistency and one documented modelling limit.

## Method re-derived
- Section: d_i = OD − 2t; A_m = π/4 (OD² − d_i²); I = π/64 (OD⁴ − d_i⁴). Units mm, then SI.
- Growth: ΔL = α·L·|ΔT|. Linear thermal expansion, mean α between install and operating temperature.
- Fully restrained: σ_r = E·α·|ΔT|; F_r = E·α·|ΔT|·A_m. Fixed-fixed bar.
- Guided cantilever, end deflection Δ: F = 12 E I Δ/H³; M = F·H/2; σ = M/Z = 3 E OD Δ/H². Roark, Formulas for Stress and Strain, beam with one end guided.
- Required leg: L_req = √(3 E OD Δ/Sa). The same relation solved for σ = Sa. Same form as the manufacturer and ASHRAE guided-cantilever leg formula.
- Offset leg: Δ = ΔL. The run bends for the leg growth α|ΔT|H. Two runs act in series under one force F_run = 12 E I Δ_leg/(L³ + L2³).
- U-loop: two equal legs in series, Δ = ΔL/2. The cross leg is a rigid guide.
- Anchor force of an offset leg: √(F_leg² + F_run²). The two components are perpendicular.
- Screening: OD·Y/(L_dev − U)² against K1, with Y = α|ΔT|·U. ASME B31.3, para. 319.4.1, form only.
- Bellows: anchor force = P·A_eff + k·ΔL. Pressure thrust plus spring force.
- Sa and K1 are inputs. The ASME B31.3 allowable S_A = f(1.25 S_c + 0.25 S_h) is not computed by the product or the script.

## Known-answer cases
The script prints every row. The table gives each case with its worst relative difference and key values.

| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-offset-leg-steel-50m (24 values) | L_req / u_leg | 4.850045 m / 0.808341 | 4.850045 / 0.808341 | 0 | 1e-9 | PASS |
| ka-offset-leg-steel-50m | F_leg / M_leg | 388.1088 N / 1164.326 N·m | same | 0 | 1e-9 | PASS |
| ka-offset-leg-steel-50m | anchor_force / scr | 388.10882 N / 118.1678 | same | 0 | 1e-9 | PASS |
| ka-offset-leg-steel-50m | worst of 24 | | | 2.2e-16 | 1e-9 | PASS |
| ka-u-loop-too-short (13 values) | L_req / u_leg | 3.429500 m / 1.143167 | same | 0 | 1e-9 | PASS |
| ka-u-loop-too-short | F / M / scr | 1552.435 N / 2328.653 N·m / 103.7167 | same | 0 | 1e-9 | PASS |
| ka-cold-copper-loop (13 values) | dL_signed / L_req | −19.92 mm / 1.293553 m | same | 0 | 1e-9 | PASS |
| ka-cold-copper-loop | F_res / F / M | 8.05036 kN / 38.6245 N / 28.9684 N·m | same | 0 | 1e-9 | PASS |
| ka-z-bend-steel (18 values) | F_run / anchor_force | 0.287422 N / 388.108915 N | same | 0 | 1e-9 | PASS |
| ka-z-bend-steel | u_run / u_run2 | 0.0491884 / 0.0401622 | same | 0 | 1e-9 | PASS |
| ka-axial-bellows (7 values) | thrust / spring / anchors | 4.8 kN / 5040 N / 9840 N | same | 0 | 1e-9 | PASS |
| ka-axial-bellows | u_bellows / pass | 0.84 / 1 | same | 0 | 1e-9 | PASS |

All five KATs agree with physics and with the cited relations as I know them. No KAT is Secondary or Unconfirmed.
In ka-z-bend-steel the KAT stores sigma_run in MPa (0.24195). The spec text quotes the same value as 241.95 kPa. Both are the same number.

## Extra probes (product vs independent)
Product values are hard-coded in the script, from the probe tool. All 12 agree to better than 1e-9 on every output.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| X1 | L-bend 80 m, 114.3×4, E 200, α 17, 20→150 °C, H 10 m, Sa 150 | L_req / F_leg | 8.99072 m / 895.593 N | same | <1e-10 | PASS |
| X2 | X1 with E_hot 190 GPa, K1 200 | F_leg / u_scr | 850.813 N / 1.15797 | same | <1e-10 | PASS |
| X3 | U-loop 40 m plastic-like (E 3, α 150), 20→−30 °C, H 1.8 m | u_leg / dL_signed | 2.39357 / −300 mm | same | <2e-10 | PASS |
| X4 | Z-bend 12 m + 40 m, leg 8 m, 5→95 °C | u_run / u_run2 / anchor | 0.027133 / 0.049538 / 218.935 N | same | <2e-10 | PASS |
| X5 | near zero: ΔT 0.01 K, U-loop H 1 m | dL / F_leg | 0.006 mm / 5.98796 N | same | <1e-10 | PASS |
| X6 | very large: 5000 m, 400 K, H 40 m | dL / L_req | 24 000 mm / 47.3316 m | same | <5e-11 | PASS |
| X7 | boundary: H = L_req, U-loop | u_leg / pass | 1 / 1 | 1 / 1 | 0 | PASS |
| X8 | bellows 16 bar, 150 cm², no spring, no rating | anchor_force / pass | 24 000 N / 0 | same | 0 | PASS |
| X9 | bellows, 42 mm against 40 mm rating | u_bellows / pass | 1.05 / 0 | same | 0 | PASS |
| X10 | 0.6 m legs, 114.3 mm pipe (under ten diameters) | F_leg | 177 295 N | same | <2e-10 | PASS |
| X11 | imperial: 160 ft, 6.625 in × 0.280 in, 68→350 °F, 20 ft, 20 ksi | L_req / anchor_force | 8.39560 m / 11 946.785 N | same | <5e-10 | PASS |
| X12 | L-bend with the run as the weak member (4 m run, 15 m leg) | u_run / anchor_force | 0.70997 / 449.103 N | same | <1e-10 | PASS |

Guard probes (product only, not in the script): wall = OD/2 blocks "wall"; wall 44.4 mm solves. Equal temperatures block. Missing Sa or H blocks after dT, ΔL and F_res are shown. Bellows without A_eff blocks. L2 with a U-loop gives `l2-unused`. K1 with a bellows gives `k1-unused`. A_eff with a leg layout gives `bellows-unused`. L = 0.05 m and H = 1500 m are refused by the hard limits. A 0.5 m L2 gives `short-leg`. Imperial units convert correctly.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| ΔL, σ_r, F_r | α·L·|ΔT|; E·α·|ΔT|(·A_m) | same; E for stress, E_hot (else E) for forces | Yes |
| Guided cantilever | 3 E OD Δ/Sa; 12 E I Δ/H³; F·H/2 | same | Yes |
| Offset leg and Z-bend | runs in series under one force; shares L_i³/ΣL³ | S_run = L³ + L2³; σ_i = 3 E OD Δ_leg L_i/S_run | Yes |
| U-loop | Δ = ΔL/2, moment at leg root | same | Yes |
| Anchor force | perpendicular components, resultant | √(F_leg² + F_run²); U-loop F_leg only | Yes |
| Anchor moment | largest root moment | max of M_leg, M_run, M_run2 | Yes |
| Screening | Y = α|ΔT|U; L_dev, U per layout | same; ratio in mm²/m² | Yes |
| Bellows | P·A_eff + k·ΔL | same; moment 0 | Yes |
| Units | mm, m, MPa, bar, cm², N/mm, K | engine converts; ft, in, °F, ksi verified | Yes |
| Wall guard | t < OD/2 | block at 2t ≥ OD | Yes |
| Short leg | under 10 OD warns | tests H, run L and L2; U-loop L not tested (correct) | Yes |
| Hard limits | spec ranges | enforced as blocks; soft ranges warn | Yes |
| Pass flag | 1 only with K1 and all checks met | `Checks.All(Passed)` and K1 given | Yes |
| Temperature input | mode chooses source | other mode's inputs ignored without notice | No (P1-bs.pipe-expansion-01) |
| Stress intensification | none, stated | none; caveat only in assumptions | Documented (P1-bs.pipe-expansion-02) |
| Allowable S_A | owner input | not computed | By design (P1-bs.pipe-expansion-03) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
| --- | --- | --- | --- | --- | --- |
| P1-bs.pipe-expansion-01 | Low | Low (confirmed). | The unused temperature inputs are ignored without a notice. With `temp_mode` = install and operating temperatures, an entered `dT_in` is dropped. With `temp_mode` = temperature change entered, `T_install` and `T_operate` are dropped. A user who types −40 K and forgets the mode gets a hot-line result. | Probe g10: T 10→80 °C and dT_in = −40 K. Product used dT = +70 K and dL = 42 mm. No warning. Other unused inputs (L2, bellows, K1) do warn. | Add a warning such as `dT-unused` when the input of the other mode is present. Reuse the wording of `l2-unused`. |
| P1-bs.pipe-expansion-02 | Low | Low (confirmed). Stated in the spec and an assumption. | No stress-intensification factor. A leg that passes on beam stress M/Z can exceed Sa at a real elbow. The `pass` output and the check label carry no caveat. The caveat sits only in the assumptions. | For 88.9 × 3.2 mm with a 1.5 D elbow, the B31.3 flexibility factor from the usual h = tR/r² relation gives i near 2. A leg at u_leg = 0.8 has beam stress 0.64 Sa and then reaches about 1.3 Sa at the elbow root. In ka-offset-leg-steel-50m the root stress is 65.3 MPa against Sa 100 MPa. The product states this limit in its spec and its third assumption. | Add the words "beam stress, no intensification" to the leg check label. Optionally add an input `i_SIF` (default 1) that multiplies σ_leg and σ_run. |
| P1-bs.pipe-expansion-03 | Info | Info (confirmed). | The sheet does not compute S_A = f(1.25 S_c + 0.25 S_h) or the range factor f. The owner enters Sa. This is the stated no-table rule (spec §B5). | Template: Sa is an optional record with no default. | None. Tell the owner to compute Sa by hand and enter it with its source. |
| P1-bs.pipe-expansion-04 | Info | Info (confirmed). | `pass` is 0 for ka-offset-leg-steel-50m although the leg (u 0.81) and run (u 0.03) both pass. The screening check fails and is part of `pass`. This follows the spec. | KAT `pass` = 0. Probe X1: no K1, so `pass` = 0. | None. A user may read `pass` = 0 as a leg failure. The `screen` text explains it. |
| P1-bs.pipe-expansion-R1 | Medium | Medium (new) | The output `anchor_force` holds no support friction. Every guide and slide between the anchor and the loop adds μ·w·L. On a long run this exceeds the leg force several times. The omission is in an assumption only, so the output label misleads. | 88.9 × 3.2 steel, water-filled, 12.1 kg/m: 50 m on slides at μ 0.3 gives about 1.78 kN. The KAT anchor force is 388 N. Hand calculation; μ 0.3 is a typical figure, owner to confirm. | Add an optional friction input (μ and weight per metre) to the anchor force. Or rename the output "anchor force from the leg only, no friction". |

## Notes for the owner's manual test
- Steel 88.9 × 3.2, E 210 GPa, α 12, 50 m, 10→80 °C, offset leg 6 m, Sa 100 MPa: expect ΔL = 42.000 mm, L_req = 4.850 m, F = 388.1 N, M = 1164.3 N·m. Check L_req = √(3·210e9·0.0889·0.042/100e6) by hand.
- Change to a U-loop with H = 3 m: expect Δ = 21 mm, L_req = 3.4295 m, u = 1.143 (FAIL). Force rises to 1552 N.
- Enter ΔT = −40 K for 30 m of copper (E 120, α 16.6): expect dL = 19.92 mm and dL_signed = −19.92 mm.
- Bellows at 6 bar on 80 cm² with 120 N/mm over 42 mm: expect 4.8 kN + 5040 N = 9840 N and no moment.
- Try wall = 44.45 mm on an 88.9 mm pipe: the sheet must block. Enter dT_in together with two temperatures and note that it is silently ignored (finding 01).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24, release 2f53791c.
- Script re-run: exit 0, 275 values. It is independent.
- No finding was Medium or higher. I skimmed all four and agree.
- New -R1 (Medium): the anchor force leaves out support friction.
- Verdict unchanged in kind; one Medium added.
