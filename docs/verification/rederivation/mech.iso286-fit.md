# mech.iso286-fit — independent re-derivation (Track P, P1)
Calculator: ISO 286 limits and fits, version 1.0.0 (re-checked at 1.1.0). Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/Iso286FitCalculator.cs (helper: Iso286.cs). Spec: docs/calcs/mechanical/mech.iso286-fit.md.
Script: tools/verification/mech.iso286-fit.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (reviewed: three High — -01, -02, -05 — and two Medium)

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
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-mech.iso286-fit-01 | High | High: confirmed with the probe. 18 mm H7 gives IT 20.918 µm, the same as 30 mm. I checked all 21 limits: only 18 mm × 0.001 × 1000 rounds up. 18 mm is a common size | A nominal size of exactly 18 mm is placed in the 18 to 30 mm step instead of 10 to 18 mm. IT and all deviations are then wrong. IT7 reads 20.9 µm; the standard value is 18 µm. | D = 18 mm H7/h6 gives IT_hole 20.918. D = 18.001 gives the same. Cause: 18 mm × 0.001 × 1000 = 18.000000000000004. The other 20 step limits are correct. | In Iso286.Step, compare with a relative slack, for example `sizeMm <= upper * (1 + 1e-9)`. Add a KAT at 18 mm. | Open. Probe: 18 mm H7/h6 still gives IT_hole 20.918 µm (standard 18 µm). 18.001 mm gives the same. |
| P1-mech.iso286-fit-02 | High | High: confirmed. ISO 286-1 sets ES = 0 for N above IT8 (sizes over 3 mm). N9 keyway widths read 0/−IT9 in every keyway table. The product gives −14.6/−66.9 µm at 25 mm | Hole N above IT8 uses ES = −5D^0.34. The tabulated value is ES = 0. | 25 mm N9/h9: product ES −14.571, EI −66.866. Table: 0 and −52. Error 14.6 µm, 28 % of IT9. | Set ES = 0 for N when the grade is above 8. Add a KAT for N9. Check M above IT8 against the standard; I could not confirm it. | Open. Probe: 25 mm N9/h9 still gives ES −14.571, EI −66.866 µm (table 0 and −52). |
| P1-mech.iso286-fit-03 | Medium | Medium: confirmed. The hole rule should take ei from the shaft one grade finer (k7 for K8), as the Δ rule implies. K8 18–30 mm table value +10 µm: owner to confirm against licensed copy | Hole K at IT8 loses the k deviation, because the shaft k formula returns 0 for grade 8. Table K8 uses 0.6D^(1/3). The error grows to 4.8 µm at 500 mm. | 25 mm K8/h7: product ES 11.766. Independent 10.054. Table 18 to 30 mm: +10. | Use the grade-independent k deviation (0.6D^(1/3)) in the hole rule for K up to IT8. | Open. Probe: 25 mm K8/h7 still gives ES 11.766 µm (independent 10.054). |
| P1-mech.iso286-fit-04 | Medium | Medium: confirmed (10 mm H7/t6 returns the s value, ei 23.453). Iso286.cs has the same silent fallback for v and y, but those letters are not offered in the UI | Letter t silently returns the s deviation when the step mean is 24 mm or less. The standard has no t below 24 mm. For 24 to 30 mm it has t but the product still returns s. | 10 mm H7/t6: ei 23.453 equals IT8 + 1, the s value. 28 mm: ei 33.684 against t formula 35.558. No notice. | Block t below 24 mm. Evaluate t at the 24 to 30 mm sub-step. | Open. Probe: 10 mm H7/t6 still gives ei 23.453 µm (the s value) with no notice. 28 mm gives 33.684. |
| P1-mech.iso286-fit-05 | Medium | High: wrong in plausible use (H7/p6 at 100 mm, H7/s6 at 110 mm). The ISO 286-1 formula table itself has the p increment (0 to 5 µm). It also has the r–zc sub-steps above 50 mm. The spec's "about 1 µm" claim is wrong. Tabulated values: owner to confirm against licensed copy | The spec and code comment say the formula values differ from the tables by about 1 µm. For interference letters this is wrong. p uses IT7 and omits the 0 to 5 µm increment and the 0.072D + 37.8 rule above 500 mm. r to u ignore sub-steps. Maximum interference is understated by up to 8.5 µm (p6, 565 mm). | See the table above. p6 at 450 mm: 62.2 against 68. u6 at 28 mm: 44.2 against 48. s6 at 110 mm: 73.9 against 79. | Add a warning on any interference or transition fit that uses p to u: "tabulated values differ by up to X µm; use overrides". Or embed the sub-step rules. Correct the stated error in the spec. | Open. Probes: p6 at 450 mm ei 62.2 µm (table 68); s6 at 110 mm 73.95 (79); u6 at 28 mm 44.16 (48). |
| P1-mech.iso286-fit-06 | Low | Low: confirmed. The ISO 286-1 formula table gives k = 0 above 500 mm. Above 500 mm the m and n formulae (0.024D + 12.6, 0.04D + 21) match the code within 0.4 µm | Shaft k above 500 mm still uses 0.6D^(1/3). The standard formula covers up to 500 mm. I believe the tabulated k is 0 above 500 mm; I could not confirm this. | 565 mm H7/k6: ei 4.949 µm. | Confirm in ISO 286-1, then set k to 0 above 500 mm. | Open. Probe: 565 mm H7/k6 still gives ei 4.949 µm. |
| P1-mech.iso286-fit-07 | Low | Low: confirmed | Overrides are not checked. ES below EI gives a negative tolerance and a clearance result. IT_hole still shows the formula value. | ES_override 10, EI_override 30: c_max 30.4, c_min 37.3, fit "clearance". | Block when ES < EI or es < ei. Show IT from the override. | Open. Probe: ES_override 10, EI_override 30 still runs and reports clearance. The same holds in bearing-bore mode (ES −9, EI 0). |
| P1-mech.iso286-fit-08 | Low | Low: confirmed | A non-integer grade is rounded without notice. hole_grade 6.5 runs as IT6. | 25 mm H6.5/g6: IT_hole 13.074. | Require an integer, or warn on rounding. | Open. Probe: hole_grade 6.5 still runs as IT6 (IT_hole 13.074 µm) with no notice. |
| P1-mech.iso286-fit-09 | Info | Info: confirmed | The KAT tolerances (3 to 6 %) cannot catch findings 01 to 05. | KATs pass with findings 01 to 05 present. | Tighten the KATs and add cases at 18 mm, N9, K8, and p6 at 400 to 500 mm. | Changed: one new KAT (ka-bearing-bore-k6) has a tight tolerance (1e-5). The older two KATs still use 3 to 6 % and no case sits at 18 mm, N9, K8 or p6 above 100 mm. |
| P1-mech.iso286-fit-R1 | — | Low | Iso286.ShaftFundamentalDeviation("cd") returns 0, because c has no formula in the code. So cd acts as h and CD as H. ISO 286-1 gives c (−52D^0.2 up to 40 mm). The fit sheet does not offer cd or CD, so users cannot reach it today. | Code: the cd branch multiplies √0 by the d term. | Add the a, b and c formulae, or return null for cd. | Open (Low). Iso286.cs is unchanged; cd is still not offered on the sheet. |
| P1-mech.iso286-fit-E1 | Low | — | In bearing-bore mode the sheet still shows IT_hole from the unused hole class and grade. It reads 25.0 µm for a bore whose tolerance ES − EI is 9 µm. | Probe: 35 mm, bore ES 0, EI −9 µm, shaft k6: IT_hole 24.98 µm (the H7 value). The limits and the fit values are correct. | In bore mode, show IT_hole as ES − EI, or hide the hole class and grade inputs. | Open (new) |

## Notes for the owner's manual test
- Enter 18 mm H7/h6. Expect IT7 = 18 µm (ES +18). The product shows 20.9 µm.
- Enter 25 mm N9/h9. Expect hole 0 to −52 µm. The product shows −14.6 to −66.9 µm.
- Enter 25 mm H7/g6. Expect ES +21, es −7, clearance 7 to 41 µm. Product: 20.9, −7.3, 7.3 to 41.3.
- Enter 450 mm H7/p6 and compare ei with the table (+68 µm). The product gives +62.2 µm.
- Enter H7/t6 at 10 mm. No t exists there. The product returns an s value without a notice.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I ran the script (exit 0). It evaluates the ISO 286-1 formulae on its own. A formula sheet leaves little room for a different method, so it is close to the code by nature.
- I reproduced -01, -02, -03, -04 and the 100 mm p6 case with the probe.
- I checked the code against the public ISO 286-1 formula table. That covers letters d to u and the Δ rule. The d to n, s, t and u formulae agree.
- Changes: -05 up to High. Its core is in the formula table (p increment, sub-steps), not only in table values.
- New: R1 (Low, cd returns 0; not reachable from the sheet).
- Residual doubt: table values in the comparison table and the K8 value come from the first reviewer's and my memory. The owner should confirm them against a licensed copy of ISO 286-2.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed (3c637a3d): version 1.1.0. A new choice hole_kind: an ISO 286 class (default) or a rolling-bearing bore. In bore mode the sheet blocks until ES_override and EI_override are given. The kernel Iso286.cs is unchanged.
- The two old KATs are unchanged and pass. One new KAT, ka-bearing-bore-k6, passes at 1e-5 against my own shaft formulae.
- None of the earlier findings is fixed. I re-ran every one with the probe: -01 to -08 and R1 behave as before. -09 is Changed because of the new tight KAT.
- Bearing-bore mode checks out: 35 mm bore (0 / −9 µm) on k6 and p6 seats and a 25 mm g6 seat match my solve. A missing override blocks. One new Low row, E1.
- Counts: 0 fixed, 1 changed (-09), 9 open from before (-01 to -08 and R1), plus the new E1.
- Script updated: the bore KAT and two bore probe cases. It exits 0.
