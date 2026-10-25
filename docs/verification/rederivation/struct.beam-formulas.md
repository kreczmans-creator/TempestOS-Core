# struct.beam-formulas — independent re-derivation (Track P, P1)
Calculator: Beam formulas (single span), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Structural/BeamFormulasCalculator.cs. Spec: docs/calcs/structural/struct.beam-formulas.md.
Script: tools/verification/struct.beam-formulas.py (exit 0 = all KATs match). Run time under 1 s.
Verdict: MATCH WITH FINDINGS

All 15 expected values of the 4 known-answer cases (KATs) match.
Seven of the eight cases match my closed forms and my finite-element beam in every numeric output.
The propped-cantilever case reports the wrong position of the maximum deflection.

## Method re-derived
- Euler–Bernoulli beam, EI y'' = M. Left end at x = 0, sagging moment positive, downward load and deflection positive.
- Own closed forms for all eight cases, from statics and double integration. Roark's Formulas for Stress and Strain, 8th ed., Table 8.1 agrees where I could check it.
- Simply supported, point load: R_A = P b/L and M_max = P a b/L. Deflection is P c (L² − c²)^1.5/(9√3 L EI), where c is the shorter segment.
- That deflection occurs at √((L² − c²)/3) from the support next to the longer segment.
- Simply supported, couple at the left end: δ = M0 L²/(9√3 EI) at 0.4226 L from the loaded end.
- Cantilever: δ_tip = w L⁴/(8 EI) and P a² (3L − a)/(6 EI). Fixed-ended: M_end = −w L²/12, −P L/8. δ_mid = w L⁴/(384 EI), P L³/(192 EI).
- Propped cantilever, fixed at x = 0: R_A = 5wL/8, R_B = 3wL/8 and M_A = −wL²/8.
- Its sagging maximum is 9wL²/128 at 5L/8. Maximum deflection is w L⁴/(184.634 EI) at 0.5785 L from the fixed end.
- Second route: a Hermite finite-element beam with 600 elements. It uses no closed form. The two routes agree within 2e-6 in all eight cases.
- Units: E in GPa and I in cm⁴ give EI in kN·m². Deflection in mm.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-ss-udl | R_A | 30 | 30 | 0.00e+00 | 1.0e-09 | PASS |
| ka-ss-udl | M_max | 45 | 45 | 0.00e+00 | 1.0e-09 | PASS |
| ka-ss-udl | x_M | 3 | 3 | 0.00e+00 | 1.0e-09 | PASS |
| ka-ss-udl | delta_max | 10.0446 | 10.0446 | 4.27e-06 | 1.0e-04 | PASS |
| ka-ss-point | R_A | 16.6667 | 16.6667 | 2.00e-06 | 1.0e-04 | PASS |
| ka-ss-point | R_B | 33.3333 | 33.3333 | 1.00e-06 | 1.0e-04 | PASS |
| ka-ss-point | M_max | 66.6667 | 66.6667 | 5.00e-07 | 1.0e-04 | PASS |
| ka-ss-point | x_delta | 3.266 | 3.26599 | 4.19e-06 | 1.0e-04 | PASS |
| ka-ss-point | delta_max | 11.5202 | 11.5202 | 2.95e-06 | 1.0e-04 | PASS |
| ka-cantilever-point | R_A | 20 | 20 | 0.00e+00 | 1.0e-09 | PASS |
| ka-cantilever-point | M_min | -60 | -60 | 0.00e+00 | 1.0e-09 | PASS |
| ka-cantilever-point | delta_max | 45 | 45 | 0.00e+00 | 1.0e-09 | PASS |
| ka-fixed-udl | M_min | -30 | -30 | 0.00e+00 | 1.0e-09 | PASS |
| ka-fixed-udl | M_max | 15 | 15 | 0.00e+00 | 1.0e-09 | PASS |
| ka-fixed-udl | delta_max | 2.00893 | 2.00893 | 7.11e-07 | 1.0e-04 | PASS |

I found no KAT expected value that disagrees with physics.

## Extra probes (product vs independent)
Units: kN, m, kN·m, mm. "FE" is the finite-element beam; it is the referee for the propped case.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| B1 | propped UDL, L 8, EI 42,000, w 12 | R_A, R_B | 60; 36 | 60; 36 | 0 | PASS |
| B1 | same | M_max, x_M, M_min | 54; 5; −96 | 54; 5; −96 | 0 | PASS |
| B1 | same | δ_max | 6.32587 | 6.33841 (FE) | 1.98e-03 | DIFF, see -02 |
| B1 | same | x_δ | 3.37228 | 4.62772 (FE) | 2.71e-01 | DIFF, see -01 |
| B2 | SS point, L 6, P 50, a 1.5 (left of centre) | δ_max, x_δ | 9.35854; 2.64590 | 9.35854; 2.64590 | 4e-11 | PASS, warning shown |
| B2 | same | R_A, M_max | 37.5; 56.25 | 37.5; 56.25 | 0 | PASS |
| B3 | SS point, a = L/2 | δ_max, x_δ | 13.3929; 3 | 13.3929; 3 | 2e-10 | PASS |
| B4 | SS couple at left end, M0 20, L 6 | R_A, R_B | −3.3333; 3.3333 | same | 1e-10 | PASS |
| B4 | same | δ_max, x_δ | 2.74929; 2.53590 | same | 5e-11 | PASS |
| B5 | cantilever UDL, L 4, w 12 | M_min, δ_max | −96; 38.4 | −96; 38.4 | 0 | PASS |
| B5 | same | x_M | 0 | M_max = 0 sits at x = 4 | label only | see -03 |
| B6 | cantilever point, a 2.4 of L 6, P 30 | M_min, δ_max | −72; 44.928 | −72; 44.928 | 2e-16 | PASS |
| B7 | fixed, central point, L 6, P 40 | M_max, M_min, δ | 30; −30; 2.67857 | same | 2e-10 | PASS |
| B8 | fixed UDL, L 100, w 10,000 (very large) | M_min, δ_max | −8.3333e+06; 12400.8 | same | 4e-11 | PASS |
| B9 | SS point, L 9, a 6.3, P 70 | δ_max, x_δ | 405.963; 4.95681 | same | 1e-10 | PASS |
| B10 | SS UDL, L 0.1 m (very small) | δ_max | 3.1002e-04 | 3.1002e-04 | 1e-10 | PASS |
| B11 | SS point at the right support (a = L) | R_B, M_max, δ_max | 50; 0; 0 | 50; 0; 0 | 0 | PASS |
| B12 | cantilever point at the fixed end (a = 0) | M_min, δ_max | −0; 0 | 0; 0 | 0 | PASS |

Guard probes that behaved correctly: a > L blocks. A negative load blocks. L = 0 blocks. Out-of-range E, I or L only warn.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| SS UDL, point, couple | Closed forms above | Same formulas | Yes |
| SS point mirror rule | Use the shorter segment | c_far chosen from a versus L/2; position mirrored | Yes |
| Cantilever, fixed-ended | Closed forms above | Same formulas | Yes |
| Propped M, R | Closed forms above | Same | Yes |
| Propped δ_max | w L⁴/(184.634 EI) | w L⁴/(185 EI) | Rounded, 0.20 % low (see -02) |
| Propped x_δ | 0.5785 L from the fixed end | 0.4215 L from the left (fixed) end | No (see -01) |
| Couple sign | Not stated in the spec | Clockwise couple at the left end | Undocumented (see -05) |
| Units | E GPa, I cm⁴, δ mm | Engine converts; KATs pass | Yes |
| Position guard | 0 ≤ a ≤ L | Blocks outside | Yes |
| Negative loads | Not stated | HardMin 0, so uplift cannot be entered | Info (see -05) |
| Citation labels | One numbering of Roark Table 8.1 | Case numbers differ between the families | See -04 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-struct.beam-formulas-01 | Medium | Medium (confirmed). Reproduced 3.3723 m against 4.6277 m. Wrong in every propped run, but δ_max itself is right and nothing consumes x_δ. | The propped cantilever reports the position of maximum deflection from the wrong end. | L = 8 m: sheet gives x_δ = 3.372 m. Correct is 4.628 m from the fixed left end (3.372 m from the pinned end). My FE beam confirms it. | BeamFormulasCalculator.cs line 181: use `L * (15 - sqrt(33)) / 16`. Add a propped-cantilever KAT. | Open (no change). Probe: propped cantilever L 8, w 12, EI 42 000 still gives x_δ 3.3723 m (correct 4.6277 m from the fixed end). |
| P1-struct.beam-formulas-02 | Low | Low (confirmed). Exact coefficient 184.634; error 0.20 % unconservative. | Maximum deflection of the propped cantilever uses the rounded coefficient 1/185. The exact value is 1/184.634. The result is 0.20 % low (unconservative). | L 8, w 12, EI 42,000: 6.3259 mm against 6.3384 mm. | Line 182: use the coefficient 184.634. The exact form is x²(1 − x)(3 − 2x)/48 at x = (15 − √33)/16. | Open (no change). Probe: δ_max still 6.3259 mm (exact 6.3384 mm). |
| P1-struct.beam-formulas-03 | Low | Low (confirmed) | For both cantilever cases the output "x_M", labelled "Position of M_max", shows 0 m. The sagging maximum is 0 and lies at the free end. The 0 m is the position of the hogging extreme. | B5 and B6. Lines 126, 142 and 190. | Rename the label to "Position of the extreme moment", or output both positions. | Open (no change). Probe: cantilever UDL still shows x_M 0 m with M_max 0. |
| P1-struct.beam-formulas-04 | Low | Low (confirmed, sharpened; owner to confirm against licensed copy). | The Roark case numbers in the step labels do not follow one numbering. | Simply supported UDL is 2e. Fixed-ended UDL is 2d, but fixed central point is 1d. The end couple is 3a. Cantilever cases are 1a and 1e. | Check each label against the printed Table 8.1 of the cited edition. Reviewer reading of the 8th ed. scheme (load type 1–3, end condition a–f): SS point is 1e, SS end couple is 3e and cantilever UDL is 2a. The labels 2a (also in the ka-ss-point source), 3a and 1e look wrong. | Open (no change). |
| P1-struct.beam-formulas-05 | Info | Info (confirmed) | The couple direction is not stated. Uplift loads and anticlockwise couples cannot be entered. An omitted load input silently takes its template default. | Couple case gives R_A = −M0/L, so the couple is clockwise. w = −10 blocks. Omitting w runs with 10 kN/m. | State "clockwise" in the input label. | Open (no change). |

## Notes for the owner's manual test
- Propped cantilever, L 8 m, w 12 kN/m, EI 42,000 kN·m²: expect R_A 60 kN, R_B 36 kN and M_A −96 kN·m.
- Expect sag 54 kN·m at 5 m, and δ_max 6.338 mm at 4.628 m from the fixed end.
- Simply supported beam, point load left of midspan (L 6, a 1.5, P 50): expect δ_max 9.359 mm at 2.646 m.
- A mirror warning is normal for that case.
- Couple at the left end (M0 20 kN·m, L 6): expect R_A −3.333 kN.
- Expect δ_max 2.749 mm at 2.536 m.
- Cantilever, load at a = 2.4 m on L = 6 m: the sheet reports the tip deflection.
- The tip value is P a²(3L − a)/(6EI). It is not the deflection under the load.

## Independent review
- Reviewer: senior verification reviewer (Claude), 2026-10-24.
- The script uses own closed forms and a 600-element Hermite beam. It is not a translation of the C#. It runs with exit 0.
- I reproduced -01 with the probe: L 8, w 12, EI 42,000 gives x_δ 3.3723 m and δ_max 6.3259 mm.
- From y ∝ ξ²(1 − ξ)(3 − 2ξ), dy/dξ = 0 gives ξ = (15 − √33)/16 = 0.5785 from the fixed end.
- Line 181 uses (1 + √33)/16 = 0.4215, which is the distance from the pinned end.
- I kept -01 at Medium. The deflection value is right; only its stated position is wrong.
- I fixed a wrong comment in the script. It said 0.4215 L from the pinned end next to the 0.5785 L formula.
- I read every case in Run line by line. Reactions, moments, shears and deflections are right in all eight cases.
- No new findings. The Roark case labels in -04 rest on my reading of the table layout; the owner should confirm.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source change: none. BeamFormulasCalculator.cs has no diff between 298ade36 and 0772fcf8.
- KATs: `Probe kat` passes all 5 cases and no expected value changed. The script exits 0.
- Spot-check with the live probe: propped cantilever still gives x_δ 3.3723 m and δ_max 6.3259 mm. Cantilever UDL still shows x_M 0 m.
- Findings: 5 earlier rows, 0 fixed, 5 open. There was no Critical or High finding.
- New defects: none; the code did not change.
- Probe inputs: two inline cases (propped cantilever L 8, w 12, EI 42 000; cantilever UDL L 6, w 10); not saved to a file.
