# mech.thick-cylinder — independent re-derivation (Track P, P1)
Calculator: Thick-walled cylinder stresses (Lamé), version 2.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/ThickCylinderCalculator.cs. Spec: docs/calcs/mechanical/mech.thick-cylinder.md.
Script: tools/verification/mech.thick-cylinder.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (reviewed: one High, -01)

All 17 KAT values and all 12 extra probes match my independent Lamé solution. The findings concern guards and display, not the stress formulas.

## Method re-derived
Sources: Shigley's Mechanical Engineering Design, 9th ed., section 3-14; Roark's Formulas for Stress and Strain, thick-walled cylinder cases; Timoshenko, Theory of Elasticity (Lamé problem). Units: mm, MPa.
- Constants: A = (a²p_i − b²p_o)/(b² − a²); B = a²b²(p_i − p_o)/(b² − a²).
- Stresses at radius r: σ_r = A − B/r²; σ_θ = A + B/r². I checked the boundary conditions: σ_r(a) = −p_i and σ_r(b) = −p_o.
- Axial stress: σ_z = A for closed ends (end load over the wall area); σ_z = 0 for open ends.
- Von Mises from the three principal stresses. Tresca = largest minus smallest principal stress.
- Both equivalent stresses are monotone in r, so the maximum is at the bore. For closed ends σ_vm = √3·|B|/r². For open ends σ_vm² = A² + 3B²/r⁴.
- Thin-wall estimate: (p_i − p_o)(a + b)/(2(b − a)). Utilisation u = max von Mises / allowable. Wall ratio k_wall = b/a.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-internal-closed | sigma_t_a | 83.333 | 83.333333 | 4.0e-06 | 1e-04 | PASS |
| ka-internal-closed | sigma_r_a | -50 | -50 | 2.8e-16 | 1e-06 | PASS |
| ka-internal-closed | sigma_t_b | 33.333 | 33.333333 | 1.0e-05 | 1e-04 | PASS |
| ka-internal-closed | sigma_z | 16.667 | 16.666667 | 2.0e-05 | 1e-04 | PASS |
| ka-internal-closed | sigma_vm_a | 115.47 | 115.47005 | 4.7e-07 | 1e-04 | PASS |
| ka-internal-closed | sigma_tr_a | 133.33 | 133.33333 | 2.5e-05 | 1e-04 | PASS |
| ka-internal-closed | u | 0.57735 | 0.57735027 | 4.7e-07 | 1e-04 | PASS |
| ka-internal-closed | sigma_thin | 75 | 75 | 0.0e+00 | 1e-06 | PASS |
| ka-external-closed | sigma_t_a | -72 | -72 | 0.0e+00 | 1e-06 | PASS |
| ka-external-closed | sigma_t_b | -52 | -52 | 0.0e+00 | 1e-06 | PASS |
| ka-external-closed | sigma_z | -36 | -36 | 0.0e+00 | 1e-06 | PASS |
| ka-external-closed | sigma_vm_a | 62.354 | 62.353829 | 2.7e-06 | 1e-04 | PASS |
| ka-external-closed | sigma_vm_b | 27.713 | 27.712813 | 6.8e-06 | 1e-04 | PASS |
| ka-external-closed | u | 0.62354 | 0.62353829 | 2.7e-06 | 1e-04 | PASS |
| ka-internal-open | sigma_vm_a | 116.67 | 116.66667 | 2.9e-05 | 1e-04 | PASS |
| ka-internal-open | sigma_vm_b | 33.333 | 33.333333 | 1.0e-05 | 1e-04 | PASS |
| ka-internal-open | u | 0.58333 | 0.58333333 | 5.7e-06 | 1e-04 | PASS |

Product KAT results (probe `kat`): 3 of 3 cases pass. The KAT expected values are rounded to 5 significant figures, so my differences reach 3e-5. That is rounding of the KAT value, not a product error.

## Extra probes (product vs independent)
Each probe compares 11 outputs (σ_r and σ_θ at both radii, σ_z, von Mises and Tresca at both radii, u and the thin-wall estimate). Values near zero are compared with a floor of 1e-9 of the stress scale.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| r2 both pressures | a 60, b 90, p_i 80, p_o 30, closed | u / σ_vm,a | 0.62354 / 155.88 | same | 1.2e-10 | PASS |
| r2o same, open ends | as r2, open | u / σ_vm,a | 0.62482 / 156.20 | same | 1.2e-10 | PASS |
| r3 hydrostatic | p_i = p_o = 40 | u / σ_vm | 0 / 0 | same | 0 | PASS |
| r4 thin wall | a 100, b 103, p_i 10 | σ_θ,a / u | 338.41 / 1.5086 | same | 2.5e-10 | PASS (warns) |
| r5 tiny | a 0.01, b 0.02 mm, p_i 1 | σ_θ,a / u | 1.6667 / 0.011547 | same | 3.3e-10 | PASS |
| r6 large | a 5000, b 6000, p_i 1000 | σ_θ,a / u | 5545.5 / 1.8895 | same | 1.2e-10 | PASS |
| r7 b ≫ a | a 1, b 1000, p_i 100 | σ_θ,a / σ_vm,a | 100.0002 / 173.205 | same | 2.2e-10 | PASS |
| r7o b ≫ a, open | as r7, open | σ_vm,b | 0.0002 | same | 2.2e-10 | PASS |
| r8 external, thin | a 95, b 100, p_o 6 | σ_θ,a / u | −123.08 / 0.5329 | same | 2.1e-10 | PASS |
| r10 overstress | a 50, b 100, p_i 150 | u | 1.7321 (FAIL) | same | 2.5e-10 | PASS |
| r11 p_o above p_i | a 30, b 45, p_i 10, p_o 60 | σ_θ,a / σ_z | −190 / −100 | same | 1.2e-10 | PASS |
| r12 very thin | a 100, b 100.01 | σ_θ,a / u | 10000.5 / 43.31 | same | 5.0e-10 | PASS |

Guards behave as specified. b ≤ a blocks. Both pressures zero block. A negative pressure blocks at the input limit. The thin-wall warning appears when (b − a) < 0.05 a.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| A and B | Lamé constants | Same expressions | Yes |
| σ_r, σ_θ at a and b | A ∓ B/r² | Same | Yes |
| σ_z | A closed, 0 open | Same | Yes |
| Von Mises | three principals | Same expression | Yes |
| Tresca | max − min of three | Same | Yes |
| Governing point | bore (monotone in r) | Maximum of bore and outer value | Yes |
| Thin-wall estimate | mean-radius hoop stress | Same | Yes |
| Units | mm, MPa | Converted correctly | Yes |
| Signs | tension positive | Compression negative | Yes |
| Check | von Mises against allowable | u = σ_vm,max/σ_allow; pass if u ≤ 1 | Yes |
| External pressure | stress only | No stability warning | Partly (finding 01) |
| Boundary stress values | exactly −p_i and −p_o | Round-off noise at times | Partly (finding 02) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-mech.thick-cylinder-01 | Medium | High: a missing guard lets an unsafe pass through in plausible use. With the default 200 MPa allowable, elastic collapse governs whenever t/r_m is below about 0.06. Real tubes collapse well below the elastic value because of out-of-roundness | Under external pressure on a moderately thin wall, the check passes although elastic collapse governs. The calculator checks stress only and gives no stability notice. | a 95 mm, b 100 mm, p_o 7.5 MPa, closed, allowable 200 MPa. Product: u = 0.666, pass. Long-tube collapse pressure E/(4(1 − ν²))·(t/r_m)³ with E 207 GPa and ν 0.3 is 7.67 MPa. The load is 98 % of it. | Add a warning when p_o > p_i and (b − a)/a is below about 0.1. State that stability is not checked. Optionally add the collapse pressure as a comparison output. | Open. Probe: a 95, b 100, p_o 7.5 MPa, closed still gives u 0.666, pass, with no stability notice. Long-tube collapse is 7.67 MPa. |
| P1-mech.thick-cylinder-02 | Low | Low: confirmed (σ_r,a −1.4901e-14 MPa in my probe) | A boundary stress that should be exactly zero shows round-off noise such as −1.49e-14 MPa. Such values appear in the sheet and outputs. | a 95, b 100, p_i 0, p_o 7.5: σ_r,a = −1.4901e-14 MPa. a 5000, b 6000: σ_r,b = −4.77e-13 MPa. a 50, b 100, p_i 50: σ_r,b = 1.86e-15 MPa. | Set σ_r(a) = −p_i and σ_r(b) = −p_o directly. The Lamé form then only supplies the interior values. | Open. Probe: σ_r,a is still −1.4901e-14 MPa (a 95, b 100, p_o 7.5) and σ_r,b is 1.86e-15 MPa (a 50, b 100, p_i 50). |
| P1-mech.thick-cylinder-03 | Info | Info: confirmed | The check uses von Mises only. Tresca is reported but not checked. Tresca is 15 % higher in the first KAT (133.3 against 115.5 MPa). | KAT ka-internal-closed. | Say in the check text which criterion the allowable stress assumes. | Open (Info). Unchanged. The check still uses von Mises only. |
| P1-mech.thick-cylinder-04 | Info | Info: confirmed | At exactly 5 % wall, the thin-wall warning depends on floating-point rounding. | a 50 mm, b 52.5 mm warns; the spec threshold is "thinner than 5 %". | None needed. Optionally add a small tolerance. | Open (Info). Probe: a 50, b 52.5 still warns 'thin-wall' at exactly 5 %; a 50, b 52.4 warns too. |

## Notes for the owner's manual test
- Enter a 50, b 100, p_i 50 MPa, closed. Expect σ_θ,bore 83.33, σ_r,bore −50, σ_z 16.67, σ_vm 115.47, Tresca 133.33, thin-wall 75 MPa.
- Switch to open ends. Expect σ_z 0 and σ_vm,bore 116.67 MPa.
- Enter a 40, b 60, p_o 20 MPa. Expect σ_θ,bore −72, σ_θ,outer −52, σ_z −36, σ_vm,bore 62.35 MPa.
- Set p_i = p_o. Expect all three principal stresses equal to −p and u = 0.
- Try a 95, b 100, p_o 7.5 MPa. The sheet passes. Check collapse by hand with a stability formula (finding 01).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I ran the script (exit 0). It solves the Lamé problem on its own and does not translate the C#.
- I reproduced -01 with the probe: a 95, b 100, p_o 7.5 MPa passes at u 0.666 with no notice.
- I checked the long-tube collapse value by hand: 7.67 MPa, so the load is 98 % of it.
- I read Run line by line. A, B, σ_z for closed ends, von Mises, Tresca and the thin-wall estimate are all correct.
- Change: -01 up to High. Its trigger should cover p_o > p_i with b/a up to about 1.1, not only the 5 % thin-wall case.
- No new findings. Residual doubt: none on the stress formulae.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed: none. ThickCylinderCalculator.cs and its spec are untouched, and the version stays 2.0.0. Shared sheet code changed, so I re-ran the probes.
- All three KATs pass in the probe. The script is unchanged and exits 0 (12 cases agree to 5e-10).
- I re-ran the findings and the reference cases (internal closed, open ends, external pressure, p_i = p_o, very thin). Every value is as before.
- All four findings are still open: -01 (Medium, reviewed High), -02 (Low), -03 (Info) and -04 (Info).
- I found no new defect.
