# struct.section-properties — independent re-derivation (Track P, P1)
Calculator: Section properties, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Structural/SectionPropertiesCalculator.cs (helpers: Geometry/PolygonSection.cs, Geometry/Shapes.cs). Spec: docs/calcs/structural/struct.section-properties.md.
Script: tools/verification/struct.section-properties.py (exit 0 = all KATs match). Run time about 16 s.
Verdict: MATCH WITH FINDINGS

All 34 expected values of the 5 known-answer cases (KATs) match.
The parametric shapes and the polygon mode agree with the independent script.
One Critical defect sits in the composite mode: tube and hollow-rectangle parts give the wrong area and inertia.
The error is grossly unconservative and carries no warning.

## Method re-derived
- Area and first and second moments of each closed loop by the shoelace form of Green's theorem. Holes carry a negative sign. Units: mm, mm², mm³, mm⁴.
- Centroidal values by the parallel-axis theorem. Principal values from the Mohr circle. Angle from tan 2θ = −2 Ixy/(Ixx − Iyy).
- Elastic modulus: Ixx divided by the largest fibre distance on either side of the centroid.
- Plastic modulus: bisect the axis that halves the area, then add the absolute first moments of both halves. Checked against Roark's Formulas for Stress and Strain, 8th ed., Table A.1 (rectangle, hollow circle).
- Solid rectangle torsion: Saint-Venant series (Timoshenko and Goodier, Theory of Elasticity, 3rd ed., torsion of a rectangular bar). Roark 8th ed., Table 10.1 gives the same value to 0.1 %.
- Circle and tube: J = π d⁴/32 and J = π (D⁴ − d⁴)/32.
- Closed thin wall (hollow rectangle): Bredt, J = 4 Am² t/pm + pm t³/3, on the mid-line.
- Open thin wall: J = Σ b t³/3.
- Rolled I-section: no closed form is trusted. The script solves the Prandtl stress function with linear triangles. It uses two mesh sizes and extrapolates, because the error falls with the square of the mesh size. The UB 457×191×98 outline gives 121.27 cm⁴.
- Warping constant of an I-section: Iw = I_minor (h − t_f)²/4.
- Fillets and corner radii: arcs with 4000 chords per quarter circle.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-ub-457x191x98 | A | 125 | 125.263 | 2.10e-03 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | I_xx | 45730 | 45727 | 6.66e-05 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | I_yy | 2347 | 2347.02 | 7.40e-06 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | i_x | 19.1 | 19.1062 | 3.27e-04 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | i_y | 4.33 | 4.3286 | 3.24e-04 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | W_el_x | 1957 | 1957.49 | 2.50e-04 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | W_el_y | 243 | 243.467 | 1.92e-03 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | W_pl_x | 2232 | 2232.41 | 1.84e-04 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | W_pl_y | 379 | 378.902 | 2.58e-04 | 5.0e-03 | PASS |
| ka-ub-457x191x98 | J | 121 | 121.271 | 2.24e-03 | 1.0e-02 | PASS |
| ka-ub-457x191x98 | I_w | 1.18 | 1.17554 | 3.78e-03 | 1.0e-02 | PASS |
| ka-shs-100x100x5 | A | 18.7 | 18.7317 | 1.70e-03 | 5.0e-03 | PASS |
| ka-shs-100x100x5 | I_xx | 279 | 279.432 | 1.55e-03 | 5.0e-03 | PASS |
| ka-shs-100x100x5 | W_pl_x | 66.4 | 66.3584 | 6.27e-04 | 5.0e-03 | PASS |
| ka-shs-100x100x5 | J | 439 | 438.059 | 2.14e-03 | 1.0e-02 | PASS |
| ka-rectangle | A | 200 | 200 | 0.00e+00 | 1.0e-09 | PASS |
| ka-rectangle | I_xx | 6666.67 | 6666.67 | 5.00e-07 | 1.0e-05 | PASS |
| ka-rectangle | I_yy | 1666.67 | 1666.67 | 2.00e-06 | 1.0e-05 | PASS |
| ka-rectangle | W_el_x | 666.667 | 666.667 | 5.00e-07 | 1.0e-05 | PASS |
| ka-rectangle | W_pl_x | 1000 | 1000 | 0.00e+00 | 1.0e-06 | PASS |
| ka-rectangle | W_pl_y | 500 | 500 | 0.00e+00 | 1.0e-06 | PASS |
| ka-rectangle | i_x | 5.7735 | 5.7735 | 4.66e-07 | 1.0e-04 | PASS |
| ka-rectangle | J | 4580 | 4573.63 | 1.39e-03 | 2.0e-03 | PASS |
| ka-tube | A | 59.69 | 59.6903 | 4.36e-06 | 1.0e-04 | PASS |
| ka-tube | I_xx | 2701.1 | 2700.98 | 4.28e-05 | 1.0e-04 | PASS |
| ka-tube | W_el_x | 270.11 | 270.098 | 4.28e-05 | 1.0e-04 | PASS |
| ka-tube | W_pl_x | 361.33 | 361.333 | 9.23e-06 | 1.0e-04 | PASS |
| ka-tube | J | 5402.2 | 5401.97 | 4.28e-05 | 1.0e-04 | PASS |
| ka-angle-polygon | A | 27.36 | 27.36 | 0.00e+00 | 1.0e-06 | PASS |
| ka-angle-polygon | x_c | 21.3947 | 21.3947 | 1.48e-07 | 1.0e-05 | PASS |
| ka-angle-polygon | y_c | 51.3947 | 51.3947 | 6.14e-08 | 1.0e-05 | PASS |
| ka-angle-polygon | I_xx | 631.8 | 631.801 | 9.00e-07 | 2.0e-04 | PASS |
| ka-angle-polygon | I_yy | 174.31 | 174.313 | 1.47e-05 | 2.0e-04 | PASS |
| ka-angle-polygon | I_xy | -191.2 | -191.203 | 1.38e-05 | 2.0e-04 | PASS |

Units follow the KAT: cm², cm⁴, cm³, cm, dm⁶ and mm. Rows with a KAT tolerance below 1e-6 use 1e-6 as the pass limit.
Observations on the KAT values themselves:
- The rectangle J expected value (4580 cm⁴) uses the rounded coefficient 0.229. The exact series gives 4573.6 cm⁴. The difference is 0.14 %.
- The tube I_xx expected value (2701.1 cm⁴) is 0.004 % above the exact 2700.98 cm⁴.
- The UB area (125 cm²) is the Blue Book value. The idealised outline gives 125.26 cm². The difference is 0.2 %.
- The angle KAT title says "as parametric shape and as a polygon". Only the polygon is tested.

## Extra probes (product vs independent)
Values are in mm, mm², mm³, mm⁴ and mm⁶. "J vs FE" means the finite-element torsion constant.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| E1 | I 300×150, t_w 7.1, t_f 10.7, r 15 | A | 5381.71 | 5381.2 | 9.4e-05 | PASS |
| E1 | same | I_xx | 8.35701e+07 | 8.35611e+07 | 1.1e-04 | PASS |
| E1 | same | W_pl_x | 628423 | 628356 | 1.1e-04 | PASS |
| E1 | same | I_w | 1.26333e+11 | 1.26332e+11 | 8.6e-06 | PASS |
| E1 | same | J vs FE | 199187 | 197789 | 7.1e-03 | PASS (tol 1.5 %) |
| E2 | channel 200×75, t 6, t_f 11, r 12 | I_xx | 1.80334e+07 | 1.80323e+07 | 6.4e-05 | PASS |
| E2 | same | W_pl_y | 55079 | 55078.4 | 1.1e-05 | PASS |
| E2 | same | J (sum b t³/3) | 79366 | 79366 | 0 | PASS |
| E2 | same | J vs FE | 79366 | 82922 | −4.3e-02 | DIFF, see -03 |
| E3 | tee 150×150, t 10, t_f 15, r 0 | I_xx | 6.83859e+06 | 6.83859e+06 | 0 | PASS |
| E3 | same | W_pl_x | 106650 | 106650 | 0 | PASS |
| E3 | same | J vs FE | 213750 | 207917 | +2.8e-02 | DIFF, see -03 |
| E4 | angle 100×75, t 10, r 12 | I_xy | −660643 | −660650 | 9.8e-06 | PASS |
| E4 | same | W_pl_x | 43821.5 | 43821.2 | 6.8e-06 | PASS |
| E4 | same | J vs FE | 55000 | 60358 | −8.9e-02 | DIFF, see -03 |
| E5 | hollow 200×100, t 8, r_o 12, r_i 4 | I_xx | 2.20001e+07 | 2.20028e+07 | 1.2e-04 | PASS |
| E5 | same | J (Bredt) | 1.79967e+07 | 1.79967e+07 | 3.5e-10 | PASS |
| E6 | circle d 100 | J | 9.81748e+06 | 9.81748e+06 | 2.5e-10 | PASS |
| E7a | composite T, two rectangles | I_xx | 2.42e+07 | 2.42e+07 | 0 | PASS |
| E7b | composite, 100×20 rotated 30° | I_xy | 692820 | 692820 | 4.0e-11 | PASS |
| E7b | same | θ (deg) | −60 | −60 | 1.2e-16 | PASS |
| E7c | composite, rectangle minus rectangle | I_xx | 1.61778e+07 | 1.61778e+07 | 1.4e-09 | PASS |
| E7d | composite, one tube d 200, t 10, add | A | 56859.9 | 5969.03 | +8.5 | DIFF, see -01 |
| E7d | same | I_xx | 1.30057e+08 | 2.70098e+07 | +3.8 | DIFF, see -01 |
| E7e | composite, one SHS 100×100×5, add | A | 18030.1 | 1873.17 | +8.6 | DIFF, see -01 |
| E7f | composite, 300 square minus tube | A | 33140.1 | 84031 | −0.61 | DIFF, see -01 |
| E8 | polygon with a square hole | I_xy | −222527 | −222527 | 2.1e-09 | PASS |
| E9 | rectangle 0.001×0.002 and 1e5×1e5 | I_xx | exact | exact | <1e-09 | PASS |
| E10 | rectangle 10×1000 and 100×100 | J | 331233; 1.40577e+07 | same | <3e-08 | PASS |
| E11 | thin I 1000×300×0.5, r 0 | J | 66.625 | 66.625 | 0 | PASS |

Guard probes that behaved correctly: thickness at or above half the size (tube, hollow rectangle, I, angle) blocks. A missing dimension blocks. A hollow wall above one fifth of the size warns. The 2001-row polygon blocks.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Green's theorem integrals, parallel axis | Standard | PolygonSection.cs, same sums | Yes |
| Principal values and angle | tan 2θ = −2Ixy/(Ixx − Iyy) | atan2 form, angle to the axis of largest I | Yes |
| W_el | I over the largest fibre distance | I_xx / max(y_top, y_bot) | Yes |
| W_pl | Equal-area axis, |Q| sum | Bisection, 80 steps, half-plane clipping | Yes |
| Rectangle J | Series | 25 odd terms | Yes |
| Rolled I J | Open sum plus fillet term | El Darwish–Johnston regression for α, plus −0.42 t_f⁴ | Yes, 0.01 % from FE for the UB |
| Hollow rectangle J | Bredt | Mid-line radius (r_o + r_i)/2 | Yes, 0.1 % from the Blue Book |
| Channel, tee, angle J | Open sum | Sum b t³/3, fillets ignored, warned | Yes, but see -03 |
| Circle and tube | Closed form | Closed form (outline polygon has 360 chords) | Yes |
| Composite sign logic | Add keeps holes, subtract inverts all | Hole loops of a part flip the wrong way | No, see -01 |
| Guards | Thickness and fit checks | Present for most shapes; channel fillet fit missing | Mostly, see -02 |
| Polygon validity | Simple outline, holes inside | Not checked; only a cryptic block appears | Partly, see -04 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-struct.section-properties-01 | High | Critical (upgraded). Every hollow part in a composite is wrong. A is about 9 times too large for common walls. An SHS with a cover plate is a routine built-up section. No warning appears. | A tube or hollow-rectangle part in a composite gives a wrong section with no warning. Area is about 9.5 times too large for "add". | Composite, one tube d 200, t 10, add: A = 568.6 cm² (correct 59.69), I_xx = 13006 cm⁴ (correct 2701). SHS 100×100×5 as part: 180.3 cm² (correct 18.73). Rectangle 300 minus that tube: 331.4 cm² (correct 840.3). | SectionPropertiesCalculator.cs line 479: `subtract == l.IsHole ? ... : ...` flips hole loops of an added part. Use `new SectionLoop(moved.Vertices, subtract ? !l.IsHole : l.IsHole)`. Add a composite-tube KAT. | Open (no change). Probe: composite tube d 200, t 10 (add) still gives A 568.6 cm² and I_xx 13 006 cm⁴. SHS 100×100×5 gives 180.3 cm². Rectangle 300 minus the tube gives 331.4 cm². |
| P1-struct.section-properties-R1 | Low | Low (new) | Overlapping "add" parts are counted twice with no check or warning. The assumptions state non-overlap only for polygon holes. | CompositeLoops sums every loop. A plate placed over a flange adds the shared area twice. | Add an assumption that added parts must not overlap. Warn when part bounding boxes overlap. | Open (no change). |
| P1-struct.section-properties-02 | Low | Low (confirmed). Only for a root radius larger than the flange outstand, which no rolled channel has. | A channel whose root radius does not fit the flange gives a self-intersecting outline with no block. | Channel h 100, b 20, t 10, t_f 5, r 15. Here tw + r = 25 mm exceeds b = 20 mm. The sheet returns A = 11.97 cm². | Line 337: add `d.T + r > d.B` for the channel, as for the I-section and tee. | Open (no change). |
| P1-struct.section-properties-03 | Medium | Medium (confirmed). Channel and angle errors are conservative and over 5 %. The tee error is 2.8 % unconservative. | J of channel, angle and tee ignores the root fillet. Channel is 4.3 % low and angle 8.9 % low against a finite-element value. The error is conservative and a warning appears. The sharp tee is 2.8 % high. | E2, E3, E4 above. FE reference uses the same outline as the sheet. | Add a fillet term for channel, tee and angle, or state the error size in the warning text. | Open (no change). |
| P1-struct.section-properties-04 | Low | Medium (upgraded). A hole outside the outline can also give a silent wrong result, not only a block. | A self-crossing polygon, or a hole outside the outline, ends in a cryptic engine block. The text is "sqrt of a negative number is undefined". | Vertices (0,0), (100,100), (100,0), (0,60) as one loop. Square outline 100 with a second loop at x 200–300. | Test the loops for crossing and containment. Report a geometry block with a clear message. Reviewer probe: square 100 outline with a 10 × 10 "hole" at x 200–210 runs with no notice and gives A = 99 cm². | Open (no change). |
| P1-struct.section-properties-05 | Info | Info (confirmed) | Circle and tube parts in a composite use a 360-chord polygon. Area is 5e-05 low and I is 1e-04 low. | E7f area differs by 5e-06 of the total. | No action needed. | Open (no change). |

## Notes for the owner's manual test
- UB 457×191×98 with h 467.2, b 192.8, t 11.4, t_f 19.6 and r 10.2 mm: expect A 125.3 cm² and I_xx 45,728 cm⁴.
- The same UB gives W_pl_x 2,232 cm³, J 121.3 cm⁴ and I_w 1.176 dm⁶.
- Composite with one tube (d 200, t 10): the sheet should show A 59.69 cm². The current build shows 568.6 cm². Use this to confirm finding -01.
- Composite of SHS 150×150×8 (default corners) alone: expect A 44.75 cm². The current build shows 402.8 cm².
- Rectangle 100×200: J should be 4,573.6 cm⁴. Roark's table coefficient 0.229 gives 4,580 cm⁴.
- Angle 100×75×10, r 12: compare J with the Blue Book table value. Expect the sheet to be about 9 % below an exact torsion value.
- The template lists r = 10.2 mm as the default. If the form fills it in, clear r for a hollow rectangle. Otherwise the corner radius is not 1.5 t.

## Independent review
- Reviewer: senior verification reviewer (Claude), 2026-10-24.
- The script builds its own loops and integrals. It runs with exit 0 in about 16 s.
- The script's rolled-I torsion uses the same El Darwish–Johnston regression as the C#. Only its finite-element route is independent for that J.
- I reproduced -01 with the probe. One tube d 200, t 10 gives A 568.6 cm² and I_xx 13,006 cm⁴. A 300 square minus that tube gives 331.4 cm².
- Cause of -01: line 479 turns a hole into a solid for "add" and keeps it a hole for "subtract". Both are backwards.
- A realistic case also fails. SHS 150×150×8 plus a 200×10 plate gives A 422.8 cm² instead of 64.75 cm².
- I raised -01 to Critical because the error is large, unconservative and silent in normal composite use.
- I raised -04 to Medium after a hole outside the outline ran silently.
- New finding R1 (Low): overlapping added parts double count with no warning.
- I read Run, TryPrimitive, PrimitiveTorsion, CompositeLoops, Shapes and PolygonSection line by line. The principal angle sign and the plastic clipping are correct.
- Residual doubt: the composite fix needs a KAT with a tube part and a subtracted tube. Track S should add both.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source change: none. SectionPropertiesCalculator.cs has no diff between 298ade36 and 0772fcf8.
- KATs: `Probe kat` passes all 6 cases and no expected value changed. The script exits 0 (4 extra checks show the known DIFF rows for finding -01).
- Spot-check of the Critical finding -01 with the live probe: composite tube gives A 568.6 cm² (correct 59.69), SHS 100×100×5 gives 180.3 cm² (correct 18.73), rectangle minus tube gives 331.4 cm² (correct 840.3). Identical to the review.
- Findings: 6 earlier rows, 0 fixed, 6 open. The Critical finding -01 is still open.
- New defects: none; the code did not change.
- Probe inputs: /tmp/claude-0/recheck4/struct-bs/sp1.py.
