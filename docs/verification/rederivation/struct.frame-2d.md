# struct.frame-2d — independent re-derivation (Track P, P1)
Calculator: Beam and 2D frame analysis, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Structural/FrameCalculator.cs (helpers: Frame/FrameModel.cs, Frame/FrameSolver.cs, Frame/MemberLoads.cs). Spec: docs/calcs/structural/struct.frame-2d.md.
Script: tools/verification/struct.frame-2d.py (exit 0 = all KATs match). Run time under 1 s.
Verdict: MATCH WITH FINDINGS

All 26 expected values of the 5 known-answer cases (KATs) match.
Eleven extra models agree with my own solver in reactions, N, V, M and deflection.
Two High defects remain after independent review.
A concentrated couple on a member hides one side of the moment jump from M_max and M_min.
A point or couple load with a blank position silently moves to end i.

## Method re-derived
- Own dense direct-stiffness solver in numpy. Three freedoms per node. Euler–Bernoulli member, 6×6 stiffness with EA/L and EI/L³ terms. Hibbeler, Structural Analysis, Ch. 16 (stiffness method for plane frames), gives the matrix.
- Local to global transform with the rotation block [c s; −s c]. Local y is 90° anticlockwise from the member axis.
- Moment releases by static condensation. The released rotation is recovered from the zero end moment.
- Equivalent nodal loads by integrating each load against the cubic Hermite shape functions (Gauss, 10 points). This gives the exact fixed-end forces. No Macaulay tables are used.
- Member diagrams by direct integration from the end state. V = F_yi + loads, and M = −M_i + F_yi x + loads (sagging +).
- Axial force N = −F_xi − loads (tension +). Then EI y'' = M is integrated twice, with every load position as a station.
- Check inside the script: the integrated deflection reaches the solved far-end deflection to 3e-17 m.
- Supports: pin, roller, fixed by removing freedoms. Spring by adding stiffness; the reaction is −k u.
- Units: kN, m, kN·m, E in GPa, A in cm², I in cm⁴. Deflection in mm.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-ss-udl | R1_y | 30 | 30 | 2.37e-16 | 1.0e-09 | PASS |
| ka-ss-udl | R2_y | 30 | 30 | 2.37e-16 | 1.0e-09 | PASS |
| ka-ss-udl | M_max | 45 | 45 | 3.16e-16 | 1.0e-09 | PASS |
| ka-ss-udl | x_Mmax | 3 | 3 | 0.00e+00 | 1.0e-06 | PASS |
| ka-ss-udl | V_max | 30 | 30 | 2.37e-16 | 1.0e-09 | PASS |
| ka-ss-udl | d_max | 10.0446 | 10.0446 | 4.27e-06 | 1.0e-04 | PASS |
| ka-cantilever-point | R1_y | 20 | 20 | 1.78e-16 | 1.0e-09 | PASS |
| ka-cantilever-point | R1_m | 60 | 60 | 4.74e-16 | 1.0e-09 | PASS |
| ka-cantilever-point | M_min | -60 | -60 | 4.74e-16 | 1.0e-09 | PASS |
| ka-cantilever-point | d_max | 45 | 45 | 1.56e-08 | 1.0e-09 | PASS |
| ka-propped-cantilever | R2_y | 36 | 36 | 0.00e+00 | 1.0e-09 | PASS |
| ka-propped-cantilever | R1_y | 60 | 60 | 2.37e-16 | 1.0e-09 | PASS |
| ka-propped-cantilever | M_min | -96 | -96 | 1.48e-16 | 1.0e-09 | PASS |
| ka-propped-cantilever | M_max | 54 | 54 | 5.26e-16 | 1.0e-09 | PASS |
| ka-propped-cantilever | x_Mmax | 5 | 5 | 0.00e+00 | 1.0e-06 | PASS |
| ka-portal-sway | R1_x | -15 | -15 | 4.34e-08 | 1.0e-05 | PASS |
| ka-portal-sway | R4_x | -15 | -15 | 4.31e-08 | 1.0e-05 | PASS |
| ka-portal-sway | R1_y | -20 | -20 | 1.24e-10 | 1.0e-09 | PASS |
| ka-portal-sway | R4_y | 20 | 20 | 1.24e-10 | 1.0e-09 | PASS |
| ka-portal-sway | M_max | 60 | 60 | 4.34e-08 | 1.0e-05 | PASS |
| ka-portal-sway | N_e1 | 20 | 20 | 1.24e-10 | 1.0e-09 | PASS |
| ka-two-span-release | R3_y | 20 | 20 | 0.00e+00 | 1.0e-09 | PASS |
| ka-two-span-release | R2_y | 20 | 20 | 0.00e+00 | 1.0e-09 | PASS |
| ka-two-span-release | R1_y | 0 | 0 | 0.00e+00 | 1.0e-09 | PASS |
| ka-two-span-release | R1_m | 0 | 0 | 0.00e+00 | 1.0e-09 | PASS |
| ka-two-span-release | M_max | 40 | 40 | 0.00e+00 | 1.0e-09 | PASS |

KAT values agree with statics and with the cited tables. One source note is wrong; see finding -05.
Rows with a KAT tolerance below 1e-6 use 1e-6 as the pass limit.

## Extra probes (product vs independent)
Units: kN, m, kN·m, mm. Every row passes unless it says DIFF.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| F1 | 3-span beam 5+7+6 m, partial UDL, point load | R2_y | 97.875022 | 97.875022 | 3e-11 | PASS |
| F1 | same | M_max, M_min | 61.2255; −95.0419 | same | 4e-11 | PASS |
| F2 | rafter (6, 3) m, gravity UDL 12 kN/m | R2_y, M_max | 40.2492; 60.3738 | same | 4e-11 | PASS |
| F2 | same | N_max, N_min | 18; −18 | same | 2e-16 | PASS |
| F3 | fixed-base portal, beam UDL, sway load, column UDL | R1_m, R4_m | −9.9823; 47.8608 | same | 5e-11 | PASS |
| F3 | same | M_max, d_max | 73.2297; 6.5508 | same | 7e-08 | PASS |
| F4 | trapezoid load, couple, spring support, nodal load | R2_y (spring) | 80.9404 | 80.9404 | 4e-11 | PASS |
| F4 | same | M_max, d_max | 31.1963; 6.8241 | same | 9e-08 | PASS |
| F6 | portal with two moment releases | M_max, M_min | 55; −40 | same | 4e-16 | PASS |
| F7 | fixed column, lateral UDL and axial load | R1_x, M_max | 40; 100 | same | 3e-16 | PASS |
| F8 | inclined members, E override, global point load, couple | M_max, d_max | 82.4444; 175.776 | same | 5e-11 | PASS |
| X1 | A 1e6 cm², I 1 cm⁴ (extreme stiffness ratio) | M_max, d_max | 45; 80357.1 | same | 4e-11 | PASS |
| X7 | fixed–fixed bar with an inclined point force | R1_x, N_min | −66.667; −33.333 | same | 1e-10 | PASS |
| B120 | 120 members of 0.5 m, summary-only output | M_max, M_min | 137.401; −153.706 | same | 2e-10 | PASS |
| COMB | 3 combinations, report ULS2 | M_max, d_max | 43.9563; 2.80533 | same | 3e-11 | PASS |
| COMB | envelope over the 3 combinations | M_max, M_min, V_max | 98.104; −131.822; 113.518 | same | 3e-11 | PASS |
| C1 | SS beam L 6, 30 kN·m couple at 3 m | M_max | 13.5 | 15.0 | 1.0e-01 | DIFF, see -01 |
| C2 | same, couple −30 kN·m | M_min | −13.5 | −15.0 | 1.0e-01 | DIFF, see -01 |
| C3 | same, couple at 3.3 m | M_max | 15.0 | 16.5 | 9.1e-02 | DIFF, see -01 |
| C4 | same, couple at 6.0 m (member end) | M_max | 28.5 | 30.0 | 5.0e-02 | DIFF, see -01 |
| C5 | same as C1 with 4 stations | M_max | 7.5 | 15.0 | 5.0e-01 | DIFF, see -01 |

Guard probes that behaved correctly: mechanism (single pin, vertical rollers only, all-released frame, zero spring) blocks. Zero length, a load off the member, a missing node and duplicate ids block. A missing combination case blocks. A nodal couple at a fully released node blocks. An unknown report name warns. A negative E blocks. A missing node coordinate blocks. n_div = 2 and 100 give exact M_max for a UDL.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Member stiffness and transform | Hibbeler Ch. 16 | LocalStiffness, ToGlobal | Yes |
| Releases | Static condensation | Condense and Recover | Yes |
| Fixed-end forces | Exact | Macaulay integrals, closed solve for the clamped member | Yes, to 1e-10 |
| Gravity row | w per metre of member, global −y | Local components −w c and −w s | Yes |
| Perpendicular load sign | + towards local y | Same | Yes |
| Couple sign | Anticlockwise + | Same | Yes |
| Spring reaction | Force on the structure | −k u | Yes |
| Extremes of M | Stations, zero shear, jump values | Jump values on one side only | No (see -01) |
| Extremes of V and N | Both sides of a jump | Sampled at ±1e-9 L | Yes |
| Deflection | Absolute local v | Includes end translation | Yes, label issue (see -04) |
| Mechanism test | Non-positive pivot | Pivot ≤ 1e-10 × largest diagonal | Yes in all probes |
| Blank point-load position | Not stated | Taken as 0 | Unsafe (see -02) |
| Large-deflection warning | Small-displacement check | L/50 of each element | Noisy (see -03) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 298ade36 |
|---|---|---|---|---|---|---|
| P1-struct.frame-2d-01 | High | High (confirmed). Reproduced C1 (13.5 vs 15.0) and C4 (28.5 vs 30.0). Unconservative; couples on members are plausible, not routine. | A concentrated couple on a member makes the moment diagram jump. M_max or M_min takes only the value after the jump. The other side is missed. The error is unconservative and grows with station spacing. | Simply supported, L 6, 30 kN·m couple at 3 m: sheet M_max = 13.5, correct 15.0. At 3.3 m: 15.0 against 16.5. With 4 stations: 7.5 against 15.0. A couple at the member end gives 28.5 against 30. | FrameSolver.cs MomentExtremes (line 70): add candidates at each couple position minus and plus 1e-9 L, as Summarise does for V and N. Add a couple KAT. Output both values in the M series. | Open. Probe: C1 still gives M_max 13.5 against 15.0. C4 gives 28.5 against 30.0. |
| P1-struct.frame-2d-02 | Medium | High (upgraded). Reproduced: M_max 0, R2_y 0. A missing guard lets an unconservative result through. | A point or couple load with a blank position "a" goes to the start of the member with no warning. The result looks plausible and is unconservative. | Simply supported 6 m, point load F_y −60, "a" blank. The sheet gives R1_y = 60 kN, R2_y = 0 and M_max = 0. | FrameCalculator.cs Si() returns 0 for a blank cell. Block a blank "a" for kinds "point" and "moment". | Open. Probe: a point load with 'a' blank still gives R1_y 60, R2_y 0 and M_max 0. |
| P1-struct.frame-2d-03 | Medium | Medium (confirmed). False warnings only; never unconservative. | The large-deflection warning compares each element deflection with its own length. A beam cut into short elements gets false warnings. | 10 m continuous beam in 120 elements of 0.5 m: largest deflection 27.2 mm (span/370). The sheet shows 30 warnings "deflects 10 … 27 mm, more than L/50". | FrameCalculator.cs line 237: compare the deflection relative to the chord with the length of the chain of collinear elements, or lower the notice to Info. | Open. Probe: a 10 m beam in 20 elements of 0.5 m still gives 16 false warnings. The 120-element beam gives 120. |
| P1-struct.frame-2d-04 | Low | Low (confirmed) | d_max and d_e<n> are the absolute transverse displacement in member axes. They are not relative to the chord between the displaced ends. The label does not say so. | Pinned portal KAT model: d_e1 = 26.7 mm (column sway). The deflection relative to the chord is 2.9 mm. | Rename the label to "transverse displacement" and add a chord-relative value for serviceability checks. | Open. The d_max label is unchanged ('Maximum transverse deflection'). |
| P1-struct.frame-2d-05 | Low | Low (confirmed). The roller at node 2 takes the 20 kN; span 1 is unloaded. | The source note of KAT ka-two-span-release contradicts its own expected values. | The note says span 1 carries 20 kN as a cantilever with M_fixed −100 kN·m. The roller at node 2 takes the 20 kN, so R1_y = 0 and R1_m = 0 as expected. | Correct the note in FrameCalculator.cs (the CalcReferenceSource text). | Open. The KAT note still says span 1 carries 20 kN as a cantilever. |
| P1-struct.frame-2d-R1 | Medium | Medium (new) | A blank load magnitude runs as zero with no warning. A trapezoid with w2 blank silently becomes a triangle with half the load. A udl with w1 blank carries nothing. | Simply supported 6 m, trapezoid a 0, b 6, w1 −10, w2 blank: ΣR_y = 30 kN, M_max = 23.09 kN·m. The uniform intent gives 60 kN and 45 kN·m. A udl row with only F_y filled gives ΣR_y = 0. | FrameCalculator.cs Build() and Si(): block a blank w1 (udl, trapezoid), w2 (trapezoid), F_x and F_y both blank (point) and M (moment). Share the fix with -02. | Open. Probe: trapezoid with w2 blank still gives ΣR_y 30 kN and M_max 23.09 kN·m. A udl with w1 blank still carries 0. |
| P1-struct.frame-2d-D1 | Medium | — | The new port M_abs_e<n> (1.1.0) is the larger magnitude of M_max and M_min of a member. It inherits -01. A couple on a member hides one side of the jump, so the port can be low. S3 and M3 take it in pack.steel-support. | Probe: simply supported 6 m beam, 30 kN·m couple at the member end. M_abs_e1 is 28.5 kN·m, the correct value is 30. Script row added. | Fix -01 in MomentExtremes. The port then follows. | Open (new) |

## Notes for the owner's manual test
- Simply supported beam, L 6 m, 30 kN·m anticlockwise couple at 3 m: expect R1_y +5 kN and R2_y −5 kN.
- Expect M_max 15 kN·m and M_min −15 kN·m. The sheet currently shows 13.5 for M_max.
- Propped cantilever, L 8 m, w 12 kN/m (KAT): expect R1_y 60 kN and R2_y 36 kN.
- Expect M −96 kN·m at the fixed end and +54 kN·m at 5 m from it.
- Pinned portal, 30 kN sway load at node 2: expect R1_x = R4_x = −15 kN, R1_y = −20 kN and R4_y = +20 kN.
- Expect M 60 kN·m. The column d_e1 of 26.7 mm is sway, not bending.
- Release the left end of span 2 in a two-span beam and load span 2. Span 1 must then carry nothing.
- Leave "a" blank on a point load to see finding -02. Cut one beam into 0.5 m elements to see finding -03.

## Independent review
- Reviewer: senior verification reviewer (Claude), 2026-10-24.
- The script is an independent dense solver with Hermite load integration. It is not a translation of the C#. It runs with exit 0.
- I reproduced -01 with the probe: C1 gives M_max 13.5 at x 2.7 m, and C4 gives 28.5. The correct values are 15 and 30.
- Cause of -01: MemberLoads.Moment uses Step(x, a) with x ≥ a. A station at the couple therefore sees only the after-jump value.
- -01 only bites when the before-jump side is the extreme. A UDL plus a couple whose jump rises to the peak reported correctly (67.2 kN·m).
- I reproduced -02: a point load with "a" blank gives R1_y 60, R2_y 0 and M_max 0. I raised it to High under the "missing guard" rule.
- New finding R1: blank magnitudes run as zero. A trapezoid with w2 blank silently halves the load.
- I read Run, Summarise, Build, Apply, MemberLoads and the model checks line by line. I found no other defect in the stiffness, release or reaction code.
- Residual doubt: the mechanism test is relative (pivot ≤ 1e-10 × largest diagonal). A very soft spring next to very stiff members was not probed.

## Re-check after A7b/A8 (2026-10-24)
- Source changed: version 1.1.0 adds the ports R<n>_x_abs, R<n>_m_abs and M_abs_e<n>. It also snaps solver noise on unloaded reactions to zero. The stiffness maths is unchanged.
- Probe at 298ade36: -01 to -05 and R1 are all still Open. C1 and C4 still show the couple error.
- New ports checked on the steel-support frame (R1_y 28.2 kN, R1_m_abs 43.2 kN·m, M_abs_e1 43.2 kN·m). They agree with my solver to 1e-12.
- Script updated: A8 rows for the frame and one DIFF row for D1. It runs and exits 0.
- New: D1 (Medium). M_abs_e<n> inherits -01.
