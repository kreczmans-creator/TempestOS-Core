# mech.bearing-life — independent re-derivation (Track P, P1)
Calculator: Rolling bearing life (ISO 281), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/BearingLifeCalculator.cs. Spec: docs/calcs/mechanical/mech.bearing-life.md.
Script: tools/verification/mech.bearing-life.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (two findings upgraded to High in review)

## Method re-derived
- Step equivalent load: P_i = F_r when F_a/F_r ≤ e, else X F_r + Y F_a. A step with F_r = 0 uses the second form. Source: ISO 281:2007 cl. 6 (factor tables). Units kN.
- Duty cycle: P = (Σ P_i^p U_i / Σ U_i)^(1/p), with p = 3 for ball and 10/3 for roller bearings. U_i is the share of revolutions.
- Basic life: L_10 = (C/P)^p in million revolutions. L_10h = 10⁶ L_10/(60 n), n in rpm. Source: ISO 281:2007 cl. 7.
- Reliability factor: a_1 = 0.05 + 0.95 [ln(100/R)/ln(100/90)]^(2/3). It gives 0.6379 at 95 %, 0.2483 at 99 % and 0.077 at 99.95 %, which match the a_1 table of ISO 281:2007 to two digits.
- Modified life: L_nm = a_1 a_ISO L_10, then L_nmh = 10⁶ L_nm/(60 n). The a_ISO value comes from the user.
- Static: P_0 = max(X_0 F_r + Y_0 F_a, F_r) and s_0 = C_0/P_0. For a duty cycle the heaviest step governs. Source: ISO 76:2006 cl. 6–7.
- Utilisations: u_life = L_req/L_nmh and u_s0 = s_0,req/s_0.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-ball-95 | L_10 | 1000 | 1000 | 0 | 1e-09 | PASS |
| ka-ball-95 | L_10h | 16666.67 | 16666.67 | 2.0e-07 | 1e-06 | PASS |
| ka-ball-95 | a_1 | 0.637912 | 0.6379117 | 5.3e-07 | 1e-05 | PASS |
| ka-ball-95 | L_nmh | 10631.86 | 10631.86 | 9.9e-08 | 1e-05 | PASS |
| ka-ball-95 | u_life | 0.940569 | 0.9405691 | 1.1e-07 | 1e-05 | PASS |
| ka-roller-99 | L_10 | 2154.435 | 2154.435 | 1.4e-07 | 1e-06 | PASS |
| ka-roller-99 | L_10h | 71814.49 | 71814.49 | 4.6e-09 | 1e-06 | PASS |
| ka-roller-99 | a_1 | 0.248332 | 0.2483317 | 1.3e-06 | 1e-05 | PASS |
| ka-roller-99 | L_nm | 1070.029 | 1070.029 | 2.6e-07 | 1e-05 | PASS |
| ka-roller-99 | L_nmh | 35667.62 | 35667.62 | 1.1e-07 | 1e-05 | PASS |
| ka-combined | P | 4.68 | 4.68 | 0 | 1e-09 | PASS |
| ka-combined | L_10 | 263.4063 | 263.4063 | 1.1e-07 | 1e-06 | PASS |
| ka-combined | L_10h | 3658.421 | 3658.421 | 6.3e-08 | 1e-06 | PASS |
| ka-combined | P_0 | 3 | 3 | 0 | 1e-09 | PASS |
| ka-combined | s_0 | 5.333333 | 5.333333 | 6.3e-08 | 1e-06 | PASS |
| ka-duty-cycle | P | 3.301927 | 3.301927 | 7.5e-08 | 1e-06 | PASS |
| ka-duty-cycle | L_10h | 9042.245 | 9042.245 | 4.1e-08 | 1e-06 | PASS |
| ka-duty-cycle | P_0 | 4 | 4 | 0 | 1e-09 | PASS |
| ka-skf-6206 | L_10 | 535.3873 | 535.3873 | 5.2e-08 | 1e-06 | PASS |
| ka-skf-6206 | L_10h | 5948.748 | 5948.748 | 1.5e-08 | 1e-06 | PASS |
| ka-skf-6206 | s_0 | 4.48 | 4.48 | 2.0e-16 | 1e-06 | PASS |

All 21 KAT values agree. The ka-skf-6206 case is Unconfirmed. The ratings C 20.3 kN and C_0 11.2 kN agree with my recollection of the SKF listing. I could not check them against the catalogue.

## Extra probes (product vs independent)
The script holds 14 probes, 140 rows. All rows agree to 1e-6. The table shows P, L_nmh and, where useful, P_0. Product hours are converted from seconds.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| ball-R97-aiso1.5 | ball, 2 kN, R 97 %, a_ISO 1.5 | P | 2 | 2 | 0.0e+00 | PASS |
| ball-R97-aiso1.5 | ball, 2 kN, R 97 %, a_ISO 1.5 | L_nmh | 11633.8 | 11633.8 | 2.5e-09 | PASS |
| roller-axial-above-e | roller, F_r 10, F_a 6, e 0.4, X 0.4, Y 1.5, R 95 % | P | 13 | 13 | 0.0e+00 | PASS |
| roller-axial-above-e | roller, F_r 10, F_a 6, e 0.4, X 0.4, Y 1.5, R 95 % | L_nmh | 49306.9 | 49306.9 | 6.8e-10 | PASS |
| ratio-equals-e | F_a/F_r = e = 0.3 | P | 10 | 10 | 1.8e-16 | PASS |
| ratio-equals-e | F_a/F_r = e = 0.3 | L_nmh | 1066.67 | 1066.67 | 3.1e-09 | PASS |
| ratio-just-above-e | F_a/F_r = 0.30001 | P | 10.1001 | 10.1001 | 0.0e+00 | PASS |
| ratio-just-above-e | F_a/F_r = 0.30001 | L_nmh | 1035.25 | 1035.25 | 2.0e-09 | PASS |
| duty-mixed-branches | 3-step duty, steps on both sides of e, R 96 % | P | 4.21913 | 4.21913 | 2.0e-10 | PASS |
| duty-mixed-branches | 3-step duty, steps on both sides of e, R 96 % | L_nmh | 12609.3 | 12609.3 | 3.5e-10 | PASS |
| duty-mixed-branches | 3-step duty, steps on both sides of e, R 96 % | P_0 | 4 | 4 | 0.0e+00 | PASS |
| P-equals-0.5C | P = 0.5 C (no warning) | P | 10 | 10 | 1.8e-16 | PASS |
| P-equals-0.5C | P = 0.5 C (no warning) | L_nmh | 133.333 | 133.333 | 2.5e-09 | PASS |
| P-just-above-0.5C | P = 0.50005 C (warning) | P | 10.001 | 10.001 | 1.8e-16 | PASS |
| P-just-above-0.5C | P = 0.50005 C (warning) | L_nmh | 133.293 | 133.293 | 2.5e-09 | PASS |
| light-load-limit | C/P = 10 000, L_10 = 1e12 (runs) | P | 0.002 | 0.002 | 4.3e-16 | PASS |
| light-load-limit | C/P = 10 000, L_10 = 1e12 (runs) | L_nmh | 1.66667e+13 | 1.66667e+13 | 2.0e-09 | PASS |
| R-99.95-aiso-50 | R 99.95 %, a_ISO 50 | P | 2 | 2 | 0.0e+00 | PASS |
| R-99.95-aiso-50 | R 99.95 %, a_ISO 50 | L_nmh | 64026.9 | 64026.9 | 5.3e-11 | PASS |
| n-1-rpm | n = 1 rpm | P | 1 | 1 | 0.0e+00 | PASS |
| n-1-rpm | n = 1 rpm | L_nmh | 1.33333e+08 | 1.33333e+08 | 2.5e-09 | PASS |
| n-50000-rpm | n = 50 000 rpm | P | 5 | 5 | 1.8e-16 | PASS |
| n-50000-rpm | n = 50 000 rpm | L_nmh | 21.3333 | 21.3333 | 1.6e-09 | PASS |
| duty-U-not-normalised | duty shares sum to 0.4 | P | 3.30193 | 3.30193 | 3.3e-10 | PASS |
| duty-U-not-normalised | duty shares sum to 0.4 | L_nmh | 7233.8 | 7233.8 | 5.1e-10 | PASS |
| thrust-Fr-zero | F_r 0, F_a 5, Y 1.2 | P | 6 | 6 | 1.5e-16 | PASS |
| thrust-Fr-zero | F_r 0, F_a 5, Y 1.2 | L_nmh | 2083.33 | 2083.33 | 1.6e-09 | PASS |
| thrust-Fr-zero | F_r 0, F_a 5, Y 1.2 | P_0 | 2.5 | 2.5 | 0.0e+00 | PASS |
| default-factors-Fa-4 | F_r 2, F_a 4, default X = 1, Y = 0 | P | 2 | 2 | 0.0e+00 | PASS |
| default-factors-Fa-4 | F_r 2, F_a 4, default X = 1, Y = 0 | L_nmh | 16666.7 | 16666.7 | 2.0e-09 | PASS |

Guard probes: P = 0.5 C gives no warning and P = 0.50005 C warns. C/P = 10 000 runs and C/P just above blocks (light-load). κ = 0.05 blocks, κ = 4.5 warns, κ = 1 is silent. Zero load, zero duty loads and zero duty shares all block.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Equivalent load, e test | ISO 281:2007 cl. 6, single row | F_r when F_a/F_r ≤ e, else X F_r + Y F_a | Yes, single row only (-02) |
| Duty cycle | Per-step P_i, then the p-mean | Same; shares normalised | Yes |
| Exponent p | 3 ball, 10/3 roller | Same | Yes |
| L_10h and units | 10⁶ L_10/(60 n) | Uses 2π/n in SI; matches to 1e-9 | Yes |
| a_1 | Weibull form, a_1 table | Same, with R as a fraction | Yes |
| L_nm, L_nmh | a_1 a_ISO L_10 | Same | Yes |
| Static P_0, s_0 | ISO 76:2006 | Same; peak step in a duty cycle | Yes |
| Factors X, Y defaults | Per bearing, from catalogue | X = 1, Y = 0 default | Silent F_a loss (-01) |
| Heavy-load warning | P above 0.5 C | Warns when P > 0.5 C | Yes |
| Light-load block | L_10 over 1e12 Mrev | Blocks | Yes |
| κ range | 0.1 to 4 | Blocks below 0.1; warns above 4; unused in maths | Yes (-04) |
| Duty plus F_r, F_a | Duty replaces loads | Replaces without notice | Info (-03) |
| Input limits | Spec ranges | Hard limits enforced as in the spec | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-mech.bearing-life-01 | Medium | High (upgraded: reproduced; plausible use, life 45 times too long, only provenance warnings; a missing guard) | The defaults X = 1, Y = 0 remove F_a from P without any notice. A user who enters F_a and keeps the defaults gets an unconservative life. | Ball bearing, F_r 2, F_a 4 kN, C 20 kN, defaults: P = 2 kN, L_nmh 16 667 h. With X 0.56, Y 1.5 the same load gives P = 7.12 kN and 45 times less life. Only "record" warnings appear. | Warn when F_a > 0 and the active branch has Y = 0. Or leave X and Y blank so the user must enter them. |
| P1-mech.bearing-life-02 | Medium | High (upgraded: reproduced; spherical roller and paired bearings are common and the sheet does not say single-row only) | The branch F_a/F_r ≤ e has no Y₁. ISO 281:2007 cl. 6 factor tables use P = F_r + Y₁ F_a for double-row bearings and bearing pairs. The sheet gives P = F_r. | F_r 10, F_a 1.9 kN, e 0.19, Y₁ 3.3: correct P = 16.27 kN, product 10 kN. Life is overstated 4.3 times for p = 3 and 5.1 times for p = 10/3. | Add X₁ and Y₁ inputs (defaults 1 and 0). Or state "single-row bearings only" in the spec and on the sheet. |
| P1-mech.bearing-life-03 | Low | Low (confirmed) | A duty table replaces F_r and F_a with no notice, even when both are entered. | F_r 9 kN with duty (2 kN, share 1): P = 2 kN, no notice. | Add an information notice when both are present. |
| P1-mech.bearing-life-04 | Info | Info (confirmed) | κ is only range-checked. No equation uses it. The warning "treated as 4" suggests a value is read. | κ = 4.5: same results as κ = 1, one warning. | Reword: "enter a_ISO for κ = 4". |
| P1-mech.bearing-life-05 | Info | Info (confirmed) | Every hand-entered rating or factor raises a "not taken from a reference record" warning. A plain run shows about eight such warnings. | Probe of the default case. | None. This is the intended provenance prompt. |

## Notes for the owner's manual test
- Ball, F_r 2 kN, C 20 kN, 1000 rpm, R 95 %: expect L_10 1000 Mrev, L_10h 16 667 h, a_1 0.6379, L_5h 10 632 h.
- Set R to 99 %: expect a_1 0.2483 (the ISO 281:2007 a_1 table gives 0.25). At 99.95 % expect 0.077.
- Enter F_r 3, F_a 1.5 kN with e 0.22, X 0.56, Y 2: expect P 4.68 kN. Then set F_a to 0.66: ratio equals e, so P = 3 kN.
- Keep the default factors and enter F_a 4 kN with F_r 2 kN. P stays 2 kN. That is finding -01.
- Duty cycle 2 kN and 4 kN at 50 % each: expect P = 3.302 kN and P_0 = 4 kN.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script: 21/21 KAT rows pass, exit 0.
- The script is an independent Python model. It adopts the sheet's single-row load law, so it cannot see -02 by itself.
- I reproduced -01 (P 2 kN with F_a 4 kN) and -02 (P 10 kN for a roller bearing with F_a/F_r = e) with the probe.
- -01 is upgraded to High. The defaults X 1, Y 0 look like real values, and they drop F_a without a notice.
- -02 is upgraded to High. Spherical roller and paired bearings use Y₁ below e, so the error hits common bearings.
- -02 evidence: the 4.3 times figure holds for ball bearings. For roller bearings the life is 5.1 times too long.
- I read Run line by line. The hours conversion, a_1, duty mean and static load are correct. I found no new defect.
- Residual doubt: X₀ 0.6 and Y₀ 0.5 suit deep groove ball bearings only. They are user inputs, so I left this as is.
