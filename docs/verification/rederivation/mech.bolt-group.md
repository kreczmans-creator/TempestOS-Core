# mech.bolt-group — independent re-derivation (Track P, P1)
Calculator: Bolt group (elastic method), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/BoltGroupCalculator.cs. Spec: docs/calcs/mechanical/mech.bolt-group.md.
Script: tools/verification/mech.bolt-group.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

## Method re-derived
- Centroid: x_c = ΣA x/ΣA, y_c = ΣA y/ΣA. Source: AISC Steel Construction Manual, Part 7 (elastic method). Units mm.
- Torsion: T = F_y (e_x − x_c) − F_x (e_y − y_c) + T_z. Units kN·mm, shown in kN·m.
- Polar term: J = ΣA r² about the centroid, in mm⁴.
- Shear on bolt i: direct share A_i F/ΣA plus torsion share T A_i r_i/J, perpendicular to the radius. The two add as vectors.
- Tension on bolt i: plane-section law t_i = A_i (N/ΣA + c₁ x′_i + c₂ y′_i). The constants satisfy moment equilibrium about both centroidal axes, including ΣA x′y′.
- Bolts in net compression carry no tension. The parts bear on each other there.
- Prying: t_p = t (1 + b/a), the simple lever upper bound.
- Interaction: circular √((v/V_R)² + (t_p/T_R)²), and the linear form v/V_R + t_p/(1.4 T_R). Source: EN 1993-1-8:2005 Table 3.4.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-four-bolt-eccentric | T | −7.5 | −7.5 | 0 | 1e-06 | PASS |
| ka-four-bolt-eccentric | J | 1.686e6 | 1.686e6 | 0 | 1e-06 | PASS |
| ka-four-bolt-eccentric | v_max | 36.443 | 36.4434 | 1.2e-05 | 1e-03 | PASS |
| ka-four-bolt-eccentric | u_max | 0.91109 | 0.911086 | 4.1e-06 | 1e-03 | PASS |
| ka-moment-tension | t_max | 50 | 50 | 1.4e-16 | 1e-06 | PASS |
| ka-moment-tension | u_max | 0.83333 | 0.833333 | 4.0e-06 | 1e-04 | PASS |

All 6 KAT values agree with physics. Both KATs use symmetric patterns, so they cannot expose findings -02 and -03. The bolt area in the KATs is 84.3 mm² (set in the test helper).

## Extra probes (product vs independent)
The script holds 14 probes. In the table, "Product" is the Probe value, and "(group max)" rows compare it with the true maximum over all bolts. The product reports values for its governing bolt only.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| grid6-all-loads | 3x2 grid, all loads, no prying | u_max | 0.94488 | 0.94488 | 4.9e-11 | PASS |
| grid6-all-loads | 3x2 grid, all loads, no prying | v_max(all) (group max) | 19.2257 | 39.3273 | 1.0e+00 | FAIL |
| grid6-prying | same, a = 30, b = 60 mm | u_max | 2.68585 | 2.68585 | 1.2e-09 | PASS |
| grid6-prying | same, a = 30, b = 60 mm | v_max(all) (group max) | 19.2257 | 39.3273 | 1.0e+00 | FAIL |
| two-bolts | two bolts 150 mm apart, F_y −30 at 300 mm, M_y 5 | u_max | 1.64148 | 1.64148 | 1.8e-10 | PASS |
| grid6-compression | 3x2 grid, N −100 kN, M_x 5 | u_max | 0.103771 | 0.103771 | 3.6e-09 | PASS |
| offset-5x5 | 5x5 grid at x, y = 50 m, large offsets | u_max | 0.299518 | 0.299518 | 1.6e-09 | PASS |
| ring-100 | 100 bolts on 1 m radius, N −300 | u_max | 0.489972 | 0.489972 | 6.5e-10 | PASS |
| tiny-spread | two bolts 1e-4 mm apart | u_max | 250000 | 250000 | 2.3e-16 | PASS |
| zero-loads | 3x2 grid, no loads | u_max | 0 | 0 | 0.0e+00 | PASS |
| prying-a-only | a = 30, b missing, N 60 | u_max | 0.217209 | 0.217209 | 1.2e-09 | PASS |
| linear-mismatch | 3x2 grid, F 25/−75 at (300,100), N 55, M_x −5 | u_max | 0.902355 | 0.902355 | 4.3e-10 | PASS |
| linear-mismatch | 3x2 grid, F 25/−75 at (300,100), N 55, M_x −5 | t_max(all) (group max) | 0 | 25.8333 | 2.6e+01 | FAIL |
| linear-mismatch | 3x2 grid, F 25/−75 at (300,100), N 55, M_x −5 | u_lin(all) (group max) | 0.902355 | 1.04944 | 1.6e-01 | FAIL |
| unequal-areas | 4 bolts, areas 50 to 300 mm² | t_max(gov) | 34.029 | 33.286 | 2.2e-02 | FAIL |
| unequal-areas | 4 bolts, areas 50 to 300 mm² | u_max | 1.16625 | 1.15764 | 7.4e-03 | FAIL |
| unequal-areas | 4 bolts, areas 50 to 300 mm² | u_lin(gov) | 1.4332 | 1.42259 | 7.4e-03 | FAIL |
| unequal-areas | 4 bolts, areas 50 to 300 mm² | t_max(all) (group max) | 34.029 | 33.286 | 2.2e-02 | FAIL |
| unequal-areas | 4 bolts, areas 50 to 300 mm² | u_lin(all) (group max) | 1.4332 | 1.42259 | 7.4e-03 | FAIL |
| triangle-Mx | 3 bolts, M_x 10 | u_max | 2 | 2 | 1.1e-16 | PASS |
| triangle-Mx-My | 3 bolts, M_x 10 and M_y 10 | t_max(gov) | 50 | 100 | 1.0e+00 | FAIL |
| triangle-Mx-My | 3 bolts, M_x 10 and M_y 10 | u_max | 1 | 2 | 1.0e+00 | FAIL |
| triangle-Mx-My | 3 bolts, M_x 10 and M_y 10 | u_lin(gov) | 0.714286 | 1.42857 | 1.0e+00 | FAIL |
| triangle-Mx-My | 3 bolts, M_x 10 and M_y 10 | t_max(all) (group max) | 50 | 100 | 1.0e+00 | FAIL |
| triangle-Mx-My | 3 bolts, M_x 10 and M_y 10 | u_lin(all) (group max) | 0.714286 | 1.42857 | 1.0e+00 | FAIL |
| triangle-Mx-My | 3 bolts, M_x 10 and M_y 10 | Mx_resisted | 1 | 0.5 | 5.0e-01 | FAIL |
| triangle-Mx-My | 3 bolts, M_x 10 and M_y 10 | My_resisted | 1 | 0.5 | 5.0e-01 | FAIL |
| collinear-Mx | 3 bolts in a row, M_x 20 | u_max | 0 | 0 | 0.0e+00 | PASS |
| collinear-Mx | 3 bolts in a row, M_x 20 | Mx_resisted | 1 | 0 | 1.0e+00 | FAIL |

Rows for T, J, v_max, t_max and u_linear also agree in every symmetric probe when read at the governing bolt. The script prints all 169 rows. The unequal-areas and triangle rows follow the decoupled formula exactly (DECOUP rows PASS), which proves the cause.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Centroid, J, T and sign | Spec method; AISC Part 7 | Same; KAT sign matches | Yes |
| Units | mm, kN, kN·m | Engine converts; outputs match | Yes |
| Shear (direct plus torsion) | Vector sum | Same | Yes |
| Tension from M_x and M_y | Plane section with ΣA x′y′ | Two separate terms, no product term | No (-02) |
| Collinear bolts with moment | Moment cannot be resisted | Term set to 0 silently | No (-03) |
| Compression bolts | No tension | t clamped at 0 | Yes |
| Prying | t(1 + b/a) | Applied only when both a and b are given | Partly (-05) |
| Governing bolt | Highest utilisation | Highest circular utilisation only | Partly (-01) |
| Linear EN form | Per bolt, take the maximum | Evaluated at the circular-governing bolt | No (-01) |
| v_max, t_max, shear and tension alone | Group maxima | Governing-bolt values | No (-04) |
| Guards | Fewer than 2 bolts; coincident bolts | Both block correctly | Yes |
| Input limits | Spec | Up to 200 rows; coordinates ±1e5 mm | Yes |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-mech.bolt-group-01 | High | The EN 1993-1-8 linear check is evaluated only at the bolt with the highest circular utilisation. Another bolt can fail the linear form. | 3x2 grid (x 0, 80, 160; y 0, 100; A 84.3), F_x 25, F_y −75 at (300, 100), N 55, M_x −5, V_R 60, T_R 90. Bolt 6 governs: u_max 0.902, u_lin 0.902 pass. Bolt 5 has v 50.7, t 25.8 kN: u_lin 1.049. | Compute u_lin for every bolt and report the maximum. Name the governing bolt for each check. |
| P1-mech.bolt-group-02 | High | Tension from M_x and M_y ignores ΣA x′y′. Moment equilibrium fails for patterns without symmetry. The result is unconservative. | Bolts (0,0), (100,0), (0,100), M_x = M_y = 10 kN·m, T_R 50: product t_max 50, u_max 1.0. Equilibrium gives 100 kN, u 2.0. The product resists only half of each moment. Unequal areas: t_max 34.03 vs 33.29 kN. | Solve the 2x2 plane-section system with ΣA x′², ΣA y′², ΣA x′y′. Or warn when \|ΣA x′y′\| is not near zero. |
| P1-mech.bolt-group-03 | High | When ΣA y′² is zero, the M_x term is dropped silently. The same happens for M_y with ΣA x′². All checks pass with u = 0. | Bolts (0,0), (100,0), (200,0), M_x = 20 kN·m, V_R 40, T_R 50: t_max 0, u_max 0, no notice. | Block, or warn, when a moment is applied about an axis the pattern cannot resist (collinear bolts). |
| P1-mech.bolt-group-04 | Medium | v_max and t_max are values at the governing bolt. The labels say "Maximum bolt shear" and "Maximum bolt tension". "Shear alone" and "Tension alone" use the same values. | Grid probe with all loads: v_max 19.23 kN, but another bolt carries 39.33 kN. Shear alone u 0.32 vs 0.66. Linear-mismatch probe: t_max 0 while bolts carry 25.8 kN. | Report group maxima, and name the bolt for each. Or relabel as "governing bolt". |
| P1-mech.bolt-group-05 | Low | Prying is ignored without notice when only a or only b is entered. | a = 30 mm, b empty, N 60 kN: t_max 10.0 kN, the no-prying value. | Warn when exactly one of a and b is given. |
| P1-mech.bolt-group-06 | Info | The spec lists only the circular check, but the product also reports the linear check. The tension law puts the neutral axis at the bolt centroid. Large eccentricities with plate contact can give higher bolt tension (AISC Part 7, second case). | Source and template text. | Add the linear form and the centroid assumption to the spec limitations. |

## Notes for the owner's manual test
- Square pattern, 100 mm, A 84.3 mm², F_y −50 kN at e_x 200, e_y 50: expect T −7.5 kN·m, J 1.686e6 mm⁴, v_max 36.44 kN.
- Triangle (0,0), (100,0), (0,100), M_x = M_y = 10 kN·m: by hand t_max is 100 kN. The product shows 50 kN.
- Put three bolts in a row and apply M_x. Expect a warning. The product shows no tension.
- Use the 3x2 grid in -01. Check bolt 5 by hand: v 50.7 kN and t 25.8 kN give a linear utilisation of 1.05.
- Compare "Shear alone" with the largest shear you find by hand over all bolts.
