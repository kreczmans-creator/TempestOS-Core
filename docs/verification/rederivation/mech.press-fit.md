# mech.press-fit — independent re-derivation (Track P, P1)
Calculator: Press and shrink fit, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/PressFitCalculator.cs. Spec: docs/calcs/mechanical/mech.press-fit.md.
Script: tools/verification/mech.press-fit.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (reviewed: one High, one Medium R1)

Every number matches my independent values to better than 1e-5 in 10 probe cases. One governing check is missing for hollow shafts. That omission is unconservative.

## Method re-derived
Sources: Shigley's Mechanical Engineering Design, 9th ed., section 3-16 and eqs. 3-56 to 3-58; Roark's Formulas for Stress and Strain, thick-walled cylinder cases. Units: mm, N, MPa, K.
- Radial interference δ_r = δ/2.
- Hub compliance C_h = (R/E_h)·[(r_o² + R²)/(r_o² − R²) + ν_h]. Shaft compliance C_s = (R/E_s)·[(R² + r_i²)/(R² − r_i²) − ν_s].
- Interface pressure p = δ_r/(C_h + C_s). For equal materials and a solid shaft, p = E·δ_r·(r_o² − R²)/(2R·r_o²).
- Hub bore: σ_θ = p·(r_o² + R²)/(r_o² − R²); σ_r = −p. This is the point of maximum stress in the hub.
- Shaft surface: σ_θ = −p·(R² + r_i²)/(R² − r_i²); σ_r = −p.
- Hollow shaft bore: σ_θ = −2pR²/(R² − r_i²); σ_r = 0. Its magnitude exceeds the surface hoop stress. This is the point of maximum stress in a hollow shaft.
- Von Mises with σ_z = 0: σ′ = √(σ_θ² − σ_θσ_r + σ_r²).
- Axial force F = μ·p·2πR·L. Torque T = F·R. Shrink temperature rise ΔT = (δ + c_a)/(α·d).

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-steel-steel-solid | p | 62.1 | 62.1 | 0.0e+00 | 1e-04 | PASS |
| ka-steel-steel-solid | sigma_t_hub | 103.5 | 103.5 | 0.0e+00 | 1e-04 | PASS |
| ka-steel-steel-solid | F_a | 87.8 | 87.791807 | 9.3e-05 | 1e-03 | PASS |
| ka-steel-steel-solid | T | 2195 | 2194.7952 | 9.3e-05 | 1e-03 | PASS |
| ka-steel-steel-solid | dT | 100 | 100 | 1.4e-16 | 1e-06 | PASS |
| ka-hollow-shaft | p | 49.63 | 49.68 | 1.0e-03 | 2e-03 | PASS |
| ka-hollow-shaft | sigma_t_shaft | -82.7 | -82.8 | 1.2e-03 | 2e-03 | PASS |

Product KAT results (probe `kat`): 2 of 2 cases pass. In ka-hollow-shaft the expected 49.63 MPa uses rounded intermediates. The exact value is 49.68 MPa (see finding 03).

## Extra probes (product vs independent)
All probes use the default inputs (50 mm shaft in a 100 mm hub, 40 µm, steel) except the listed changes. Relative differences are below 5e-6, which is the rounding of the printed product values.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| q1 dissimilar | Al hub E 70 GPa, ν 0.33, d_o 90, δ 60 µm | p / σ_vm,hub / ΔT | 34.115 / 86.814 / 69.565 | same | < 2e-6 | PASS |
| q2 hollow, large δ | bore 25 mm, δ 120 µm | p / σ_vm,shaft | 149.04 / 216.55 | same | < 1e-6 | PASS |
| q3 thin tube | bore 45 mm | σ_t,shaft / σ_vm,shaft | −140.94 / 134.16 | same | < 4e-6 | PASS |
| q4 thin hub | d_o 52 mm, δ 20 µm | p / σ_t,hub | 3.123 / 79.68 | same | < 1e-6 | PASS (warns) |
| q5 zero interference | δ 0 | p / F / ΔT | 0 / 0 / 33.33 | same | 0 | PASS |
| q6 large | 1000 mm, bore 300, d_o 1500, δ 800 µm | p / F / T | 43.60 / 10274 kN / 5.137e6 N·m | same | < 1e-6 | PASS |
| q8 ν limits | ν_h 0.5, ν_s 0 | p | 52.295 | same | < 1e-6 | PASS |
| q9 Cu hub | E 117 GPa, ν 0.34, μ 0.2, d 30, L 40 | p / F / T | 81.02 / 61.09 / 916.3 | same | < 3e-6 | PASS |
| q10 tiny | d 2 mm, δ 2 µm | p / F | 92 / 0.2601 | same | < 1e-6 | PASS |
| q11 near-solid tube | bore 49.9 mm | σ_t,shaft | −41.262 | same | < 1e-6 | PASS |

Hollow-shaft governing stress (finding 01):
| Probe | Product shaft σ_vm (surface) | Bore σ_vm | Bore utilisation | Product utilisation |
|---|---|---|---|---|
| bore 25 mm, δ 40 µm | 72.2 MPa | 132.5 MPa | 0.379 | 0.206 |
| bore 25 mm, δ 120 µm | 216.6 MPa | 397.4 MPa | 1.136 (yields) | 0.619 (pass) |
| bore 45 mm, δ 40 µm | 134.2 MPa | 155.7 MPa | 0.445 | 0.383 |

Guards behave as specified. d_o ≤ d, d_i ≥ d and δ < 0 block. The thin-hub warning appears at (d_o − d)/2 < 0.1 d.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Radial interference | δ/2 | Same | Yes |
| Compliances and p | Lamé, Shigley 3-56 | Same, including the −ν_s term | Yes |
| Hub stresses | at the bore | Same | Yes |
| Shaft stresses | surface; bore for hollow | Surface only | No (finding 01) |
| Von Mises | σ_z = 0 | Same | Yes |
| Force and torque | μ p 2πR L; × R | Same | Yes |
| Shrink temperature | (δ + c_a)/(α d) | Same | Yes |
| Units | µm, mm, GPa, MPa | Converted correctly | Yes |
| Guards | geometry block; thin-hub warn | Same thresholds | Yes |
| Checks | hub yield, shaft yield | Hub correct; shaft check not governing for hollow shafts | Partly |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-mech.press-fit-01 | High | High: confirmed with the probe (216.55 MPa, u 0.619, pass; bore 397.4 MPa). The assumption list names the surface, but a hollow-shaft input is offered | The shaft yield check uses the von Mises stress at the shaft outer surface. For a hollow shaft the bore governs. The bore stress is 2pR²/(R² − r_i²) and can be 1.84 times the checked value. A shaft that yields at its bore can pass. | d 50, bore 25, hub 100 mm, δ 120 µm, S_y 350 MPa. Product: shaft σ_vm 216.6 MPa, utilisation 0.619, pass. Bore σ_vm 397.4 MPa, utilisation 1.136. | Compute σ_vm at the shaft bore and check the larger of bore and surface. Output both values. Rename "Shaft von Mises stress" to say where it applies. Add a hollow-shaft KAT. | Open. Probe q2 (d 50, bore 25, δ 120 µm): shaft von Mises still 216.55 MPa, u 0.619, pass. The bore value is 397.4 MPa (u 1.136). |
| P1-mech.press-fit-02 | Info | Info: confirmed | There is no upper limit or notice on the shrink temperature rise. A 233 K rise (q2) can exceed tempering or lubricant limits. | q2: ΔT 233.3 K, no notice. | Optional: warn above a stated temperature rise such as 200 K. | Open (Info). Probe q2 still gives ΔT 233.3 K with no notice. |
| P1-mech.press-fit-03 | Low | Low: confirmed | The ka-hollow-shaft expected values use rounded intermediate terms. Exact values are p = 49.68 MPa and σ_θ,shaft = −82.80 MPa, against 49.63 and −82.7. They pass on the 2e-3 tolerance. | Script: relative differences 1.0e-3 and 1.2e-3. | Update the expected values to 49.68 and −82.80. Tighten the tolerance to 1e-4. | Open. The KAT ka-hollow-shaft still expects 49.63 and −82.7 MPa. The product gives 49.68 and −82.80 (1.0e-3 and 1.2e-3 off, tolerance 2e-3). |
| P1-mech.press-fit-04 | Info | Info: confirmed | The same μ is used for assembly force and torque capacity, as stated in the assumptions. Assembly with sliding friction and retention with static friction usually differ. | Spec assumption 2. | None. Keep the note visible in the report. | Open (Info). Unchanged. |
| P1-mech.press-fit-R1 | — | Medium | Torque capacity and force use the nominal interference as entered. Surface smoothing on assembly lowers the effective interference (DIN 7190-1 takes about 0.8 of the summed Rz). The sheet neither applies this nor states it. Torque capacity therefore reads high for fine fits. The sheet also gives no reminder to run torque at the minimum interference of the fit. | Default case, δ 40 µm, Rz 4 µm on each part: effective δ ≈ 33.6 µm, so p and T fall by 16 %. Product T 2194.8 N·m; about 1844 N·m after smoothing. | State the assumption. Better, add an optional roughness input. Note that torque needs the minimum interference and stress the maximum. | Open. The default case still gives T 2194.8 N·m at the nominal δ. No roughness input exists and the sheet does not state the assumption. |

## Notes for the owner's manual test
- Enter the default case (50 mm, 100 mm hub, 40 µm, steel). Expect p 62.1 MPa, hub hoop 103.5 MPa, F 87.79 kN, T 2194.8 N·m, ΔT 100 K.
- Set the shaft bore to 25 mm. Expect p 49.68 MPa and shaft surface hoop −82.8 MPa.
- With bore 25 mm, raise δ to 120 µm. Expect hand bore stress 2pR²/(R² − r_i²) = 397 MPa, which exceeds 350 MPa. The product still passes the shaft check.
- Set d_o to 52 mm. Expect the thin-hub warning and hub hoop stress near 80 MPa at δ 20 µm.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I ran the script (exit 0). It uses its own Lamé relations and does not translate the C#.
- I reproduced -01 with the probe: shaft σ_vm 216.55 MPa passes at u 0.619. My bore value 2pR²/(R² − r_i²) is 397.4 MPa.
- I read the Run method line by line. Compliances, p, hub and surface stresses, force, torque and ΔT agree with Shigley §3-16.
- New: R1 (Medium). The torque capacity ignores surface smoothing and the min/max interference split, and does not say so.
- Residual doubt: the 0.8 Rz smoothing rule is from memory of DIN 7190-1. The owner should confirm it against a licensed copy.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed: none. PressFitCalculator.cs and its spec are untouched, and the version stays 1.0.0. Shared sheet code changed, so I re-ran the probes.
- Both KATs pass in the probe (2 of 2). The script is unchanged and exits 0.
- I re-ran all 10 extra probes from the script (q1 to q11, with the input defaults supplied). They agree with the hard-coded product values to 1e-5. Nothing shifted.
- All five findings are still open: -01 (High), -02 (Info), -03 (Low), -04 (Info) and R1 (Medium). The thin-hub warning and the geometry blocks still work.
- I found no new defect.
