# mech.iso286-fit — independent re-derivation (Track P, P1)
Calculator: ISO 286 limits and fits, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/Iso286FitCalculator.cs (helper: Iso286.cs). Spec: docs/calcs/mechanical/mech.iso286-fit.md.
Script: tools/verification/mech.iso286-fit.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

Both KATs match within their tolerances of 3 to 6 percent. Those tolerances are too loose to expose four real defects. Two of them (18 mm, N9) give errors above 3 µm in plausible use.

## Method re-derived
Source: ISO 286-1:2010, the tolerance-unit and fundamental-deviation formulae. I wrote them from memory of the standard. Units: D in mm, results in µm.
- Size steps (lower, upper]: 3, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500, then 630 to 3150. D is the geometric mean of the step limits. The first step uses 1 and 3.
- Tolerance unit: i = 0.45·D^(1/3) + 0.001·D for D up to 500 mm. I = 0.004·D + 2.1 above.
- IT5 to IT18 are 7, 10, 16, 25, 40, 64, 100, 160, 250, 400, 640, 1000, 1600, 2500 times i.
- IT01, IT0, IT1 are linear in D. IT2 to IT4 are geometric between IT1 and IT5. Above 500 mm, IT1 to IT4 are 2, 2.7, 3.7, 5 times I.
- Shaft: d −16D^0.44; e −11D^0.41; f −5.5D^0.41; g −2.5D^0.34; js ±IT/2; k +0.6D^(1/3) for IT4 to IT7 only.
- Shaft: m = IT7 − IT6; n = 5D^0.34; p = IT7 plus 0 to 5 in the tables; s = IT8 + 1 to 4 (D ≤ 50) or IT7 + 0.4D; t = IT7 + 0.63D; u = IT7 + D; r = √(p·s).
- Hole: A to H have EI = −es. K to N have ES = −ei + Δ up to IT8. P to U have ES = −ei + Δ up to IT7. Δ = ITn − IT(n−1).
- Above IT8, hole N has ES = 0 (tabulated). Above IT7, P to U have ES = −ei.
- Fit: c_max = ES − ei; c_min = EI − es; i_max = es − EI; i_min = ei − ES. Clearance if c_min ≥ 0; interference if c_max ≤ 0; else transition.
- The standard tabulates r to zc at finer sub-steps above 50 mm, and p above 500 mm by 0.072D + 37.8. The product uses neither.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-h7g6-25 | IT_hole (µm) | 21 | 20.918 | 3.9e-03 | 0.03 | PASS |
| ka-h7g6-25 | IT_shaft (µm) | 13 | 13.074 | 5.7e-03 | 0.03 | PASS |
| ka-h7g6-25 | es (µm) | -7 | -7.2853 | 4.1e-02 | 0.06 | PASS |
| ka-h7g6-25 | c_min (µm) | 7 | 7.2853 | 4.1e-02 | 0.06 | PASS |
| ka-h7g6-25 | c_max (µm) | 41 | 41.277 | 6.8e-03 | 0.03 | PASS |
| ka-h7p6-50 | IT_hole (µm) | 25 | 24.98 | 8.0e-04 | 0.03 | PASS |
| ka-h7p6-50 | ei (µm) | 26 | 24.98 | 3.9e-02 | 0.06 | PASS |
| ka-h7p6-50 | i_max (µm) | 42 | 40.592 | 3.4e-02 | 0.05 | PASS |
| ka-h7p6-50 | i_min (µm) | 0 | 0 | 0.0e+00 (abs) | 1.5 | PASS |

Product KAT results (probe `kat`): 2 of 2 cases pass. The KAT values agree with the tables as I know them. For example, g at 25 mm is −7 µm and p6 at 50 mm is +26 µm.

## Extra probes (product vs independent)
The product equals my formula implementation to 1e-3 µm in 20 probes. The table lists the main groups and every departure.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| p1 typical | 100 mm H8/f7 | ES / es | 54.313 / −36.035 | same | 0 | PASS |
| p2 transition | 10 mm H7/k6; 25 mm K7, N7, JS7/js6 | ES | 14.370; 6.132; −6.726; 10.459 | same | 0 | PASS |
| p3 interference | 25 mm P7, R7, S7, U7; 200 mm H7/s6 | ES / ei | as listed in script | same | 0 | PASS |
| p4 large | 1000 mm H7/s6; 3150 mm H7/g6 | ES / es | 90.843 / 505.39; 213.20 / −37.18 | same | 0 | PASS |
| p5 fine grade | 25 mm H3/h3; 600 mm H1/g2 | IT | 3.402; 8.690 | same | 0 | PASS |
| p6 boundary | 18 mm H7/h6 | IT_hole | 20.918 | 17.323 | 0.21 | FINDING 01 |
| p7 hole K | 25 mm K8/h7 | ES | 11.766 | 10.054 | 0.17 | FINDING 03 |
| p8 hole N | 25 mm N9/h9 | ES | −14.571 | 0 | 14.6 µm | FINDING 02 |
| p9 letter t | 28 mm H7/t6; 10 mm H7/t6 | ei | 33.684; 23.453 (s value) | 35.558; 19.250 | 0.05 | FINDING 04 |
| p10 overrides | ES 10, EI 30 µm | c_max, c_min | 30.4, 37.3 | not valid | — | FINDING 07 |
| p11 non-integer grade | hole_grade 6.5 | IT_hole | IT6 | — | — | FINDING 08 |

Product against tabulated ISO 286-2 values (values only, as I know them):
| Quantity | Table (µm) | Product (µm) | Difference |
|---|---|---|---|
| 25 mm g6 es; K7 ES; N7 ES; P7 ES | −7; +6; −7; −14 | −7.29; +6.13; −6.73; −13.07 | within 1 µm |
| p6 ei at 100, 200, 450, 565 mm | 37; 50; 68; 78 | 34.8; 46.3; 62.2; 69.5 | −2.2; −3.7; −5.8; −8.5 |
| s6 ei at 90 and 110 mm | 71; 79 | 73.95; 73.95 | +3.0; −5.0 |
| u6 ei at 20 and 28 mm | 41; 48 | 44.16; 44.16 | +3.2; −3.8 |
| k6 ei at 565 mm | 0 | 4.95 | +4.9 |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Tolerance unit, IT5 to IT18 | i formula and multipliers | Same | Yes |
| IT01 to IT4, and above 500 mm | linear / geometric; 2, 2.7, 3.7, 5 × I | Same | Yes |
| Size step | (lower, upper] at geometric mean | Same, but 18 mm falls in the next step through float error | No (finding 01) |
| Shaft d, e, f, g, h, js, m, n | formulae above | Same | Yes |
| Shaft p | IT7 + 0 to 5; 0.072D + 37.8 above 500 | IT7 at all sizes | No (finding 05) |
| Shaft r to u | sub-steps above 50 mm | Main-step mean | No (finding 05) |
| Shaft t | defined above 24 mm only | Falls back to s when the step mean is ≤ 24 mm | No (finding 04) |
| Shaft k | 0.6D^(1/3) up to 500 mm | Same, and also above 500 mm | Partly (finding 06) |
| Hole A to H, JS | EI = −es | Same | Yes |
| Hole K, M, N, P to U | ES = −ei + Δ | Same, except K8 loses k, and N above IT8 should be 0 | No (findings 02, 03) |
| Fit extremes | formulas above | Same; classification as specified | Yes |
| Units | D in mm, results in µm | SI converted correctly | Yes |
| Guards | large-size and fine-grade warnings | Present; overrides not checked | Partly (finding 07) |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-mech.iso286-fit-01 | High | A nominal size of exactly 18 mm is placed in the 18 to 30 mm step instead of 10 to 18 mm. IT and all deviations are then wrong. IT7 reads 20.9 µm; the standard value is 18 µm. | D = 18 mm H7/h6 gives IT_hole 20.918. D = 18.001 gives the same. Cause: 18 mm × 0.001 × 1000 = 18.000000000000004. The other 20 step limits are correct. | In Iso286.Step, compare with a relative slack, for example `sizeMm <= upper * (1 + 1e-9)`. Add a KAT at 18 mm. |
| P1-mech.iso286-fit-02 | High | Hole N above IT8 uses ES = −5D^0.34. The tabulated value is ES = 0. | 25 mm N9/h9: product ES −14.571, EI −66.866. Table: 0 and −52. Error 14.6 µm, 28 % of IT9. | Set ES = 0 for N when the grade is above 8. Add a KAT for N9. Check M above IT8 against the standard; I could not confirm it. |
| P1-mech.iso286-fit-03 | Medium | Hole K at IT8 loses the k deviation, because the shaft k formula returns 0 for grade 8. Table K8 uses 0.6D^(1/3). The error grows to 4.8 µm at 500 mm. | 25 mm K8/h7: product ES 11.766. Independent 10.054. Table 18 to 30 mm: +10. | Use the grade-independent k deviation (0.6D^(1/3)) in the hole rule for K up to IT8. |
| P1-mech.iso286-fit-04 | Medium | Letter t silently returns the s deviation when the step mean is 24 mm or less. The standard has no t below 24 mm. For 24 to 30 mm it has t but the product still returns s. | 10 mm H7/t6: ei 23.453 equals IT8 + 1, the s value. 28 mm: ei 33.684 against t formula 35.558. No notice. | Block t below 24 mm. Evaluate t at the 24 to 30 mm sub-step. |
| P1-mech.iso286-fit-05 | Medium | The spec and code comment say the formula values differ from the tables by about 1 µm. For interference letters this is wrong. p uses IT7 and omits the 0 to 5 µm increment and the 0.072D + 37.8 rule above 500 mm. r to u ignore sub-steps. Maximum interference is understated by up to 8.5 µm (p6, 565 mm). | See the table above. p6 at 450 mm: 62.2 against 68. u6 at 28 mm: 44.2 against 48. s6 at 110 mm: 73.9 against 79. | Add a warning on any interference or transition fit that uses p to u: "tabulated values differ by up to X µm; use overrides". Or embed the sub-step rules. Correct the stated error in the spec. |
| P1-mech.iso286-fit-06 | Low | Shaft k above 500 mm still uses 0.6D^(1/3). The standard formula covers up to 500 mm. I believe the tabulated k is 0 above 500 mm; I could not confirm this. | 565 mm H7/k6: ei 4.949 µm. | Confirm in ISO 286-1, then set k to 0 above 500 mm. |
| P1-mech.iso286-fit-07 | Low | Overrides are not checked. ES below EI gives a negative tolerance and a clearance result. IT_hole still shows the formula value. | ES_override 10, EI_override 30: c_max 30.4, c_min 37.3, fit "clearance". | Block when ES < EI or es < ei. Show IT from the override. |
| P1-mech.iso286-fit-08 | Low | A non-integer grade is rounded without notice. hole_grade 6.5 runs as IT6. | 25 mm H6.5/g6: IT_hole 13.074. | Require an integer, or warn on rounding. |
| P1-mech.iso286-fit-09 | Info | The KAT tolerances (3 to 6 %) cannot catch findings 01 to 05. | KATs pass with findings 01 to 05 present. | Tighten the KATs and add cases at 18 mm, N9, K8, and p6 at 400 to 500 mm. |

## Notes for the owner's manual test
- Enter 18 mm H7/h6. Expect IT7 = 18 µm (ES +18). The product shows 20.9 µm.
- Enter 25 mm N9/h9. Expect hole 0 to −52 µm. The product shows −14.6 to −66.9 µm.
- Enter 25 mm H7/g6. Expect ES +21, es −7, clearance 7 to 41 µm. Product: 20.9, −7.3, 7.3 to 41.3.
- Enter 450 mm H7/p6 and compare ei with the table (+68 µm). The product gives +62.2 µm.
- Enter H7/t6 at 10 mm. No t exists there. The product returns an s value without a notice.
