# mech.weld-group — independent re-derivation (Track P, P1)
Calculator: Weld group (elastic line method), version 1.0.0 (re-checked at 1.1.0). Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/WeldGroupCalculator.cs (helper: GroupGeometry.cs). Spec: docs/calcs/mechanical/mech.weld-group.md.
Script: tools/verification/mech.weld-group.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

## Method re-derived
- Weld as a line of unit throat. L_w = Σ l; centroid from the weld midpoints. Source: Blodgett, Design of Welded Structures (1966), §7.4. Units mm.
- Line moments: I_ux = Σ[l (y_m − y_c)² + l Δy²/12], I_uy likewise, product I_uxy = Σ[l x′_m y′_m + l Δx Δy/12]. J_u = I_ux + I_uy. Units mm³. Source: Shigley 11th ed., Tables 9-1 and 9-2.
- Torsion: T = F_y (e_x − x_c) − F_x (e_y − y_c) + M_z. Torsion share (T/J_u) r, perpendicular to r.
- Out-of-plane: f_z = F_z/L_w + c₁ x′ + c₂ y′. The constants c₁, c₂ solve moment equilibrium with I_ux, I_uy and I_uxy.
- Resultant f_r is the 3D vector sum at each weld end. A line's maximum is at an end, because |f| is convex along it.
- Throat a = 0.707 s, τ = f_r,max/a, s_req = f_r,max/(0.707 f_w), u = τ/f_w. The test against f_w follows EN 1993-1-8:2005 cl. 4.5.3.3 (simplified method).

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-two-vertical | L_w | 200 | 200 | 0 | 1e-09 | PASS |
| ka-two-vertical | J_u | 291666.7 | 291666.7 | 1.1e-07 | 1e-06 | PASS |
| ka-two-vertical | T | −3.5 | −3.5 | 0 | 1e-09 | PASS |
| ka-two-vertical | f_r_max | 721.1103 | 721.1103 | 6.2e-08 | 1e-06 | PASS |
| ka-two-vertical | tau | 169.9930 | 169.9930 | 7.1e-08 | 1e-05 | PASS |
| ka-box-torsion | J_u | 1333333.3 | 1333333.3 | 2.5e-08 | 1e-06 | PASS |
| ka-box-torsion | f_r_max | 106.0660 | 106.0660 | 1.6e-07 | 1e-06 | PASS |
| ka-two-horizontal-bending | I_ux | 500000 | 500000 | 0 | 1e-09 | PASS |
| ka-two-horizontal-bending | f_r_max | 502.4938 | 502.4938 | 3.8e-08 | 1e-06 | PASS |
| ka-two-horizontal-bending | s_req | 3.230640 | 3.230640 | 7.1e-08 | 1e-05 | PASS |

All 10 KAT values agree with physics. The KAT text for case 1 lists the corner offset as (50, 25) mm. The true offset is (25, 50) mm. The magnitudes (600, 300) N/mm are correct. All three KAT layouts are symmetric, so they cannot expose finding -01.

## Extra probes (product vs independent)
The script holds 12 probes plus closed-form and guard checks. "Product" is the Probe value. Closed-form rows use Shigley Table 9-1 (C-shape, rectangle, ring).
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| L-shape-in-plane | L: 150 mm x + 100 mm y welds; F_x 5, F_y −30 at (200,−40), M_z 1 | f_r_max | 536.028 | 536.028 | 6.7e-10 | PASS |
| L-shape-Mx-only | L-shape, F_z 10 kN, M_x 5 kN·m | f_r_max | 1754.29 | 2140 | 2.2e-01 | FAIL |
| L-shape-Mx-only | L-shape, F_z 10 kN, M_x 5 kN·m | u | 1.87978 | 2.29309 | 2.2e-01 | FAIL |
| L-shape-Mx-My | L-shape, F_z 10, M_x 3, M_y 4 | f_r_max | 777.662 | 1620 | 1.1e+00 | FAIL |
| L-shape-Mx-My | L-shape, F_z 10, M_x 3, M_y 4 | u | 0.833293 | 1.73589 | 1.1e+00 | FAIL |
| C-section | C: 60 x 100 x 60, F_y −25 at (180,50), s 5, f_w 180 | f_r_max | 659.874 | 659.874 | 4.2e-10 | PASS |
| trapezoid-3D | 4-weld trapezoid, F_x −12, F_y −40, F_z 15, M_x 2, M_z −1 | f_r_max | 352.136 | 352.136 | 1.0e-09 | PASS |
| ring-48-torsion | 48-segment ring r = 100, M_z 10 | f_r_max | 159.724 | 159.724 | 2.7e-09 | PASS |
| rect-60x140-torsion | 60 x 140 box, M_z 4 | f_r_max | 228.473 | 228.473 | 7.7e-10 | PASS |
| zero-loads | two 100 mm welds, no load | f_r_max | 0 | 0 | 0.0e+00 | PASS |
| tiny-load | same, F_y −1e-6 kN | f_r_max | 1.4232e-05 | 1.4232e-05 | 2.3e-09 | PASS |
| large-4000mm-box | 4000 mm box, s 25, large loads | f_r_max | 9348.71 | 9348.71 | 8.4e-12 | PASS |
| zero-length-row | two welds plus a zero-length row | f_r_max | 206.155 | 206.155 | 1.4e-09 | PASS |
| leg-3mm | two welds, s = 3 mm (minimum) | f_r_max | 50 | 50 | 0.0e+00 | PASS |
| C-section | C: 60 x 100 x 60, F_y −25 at (180,50), s 5, f_w 180 | J_u | 468424 | 468424 | 9.1e-10 | PASS |
| rect-60x140-torsion | 60 x 140 box, M_z 4 | J_u | 1.33333e+06 | 1.33333e+06 | 2.5e-09 | PASS |
| ring-48-torsion | 48-segment ring r = 100, M_z 10 | J_u(2*pi*r^3) | 6.28319e+06 | 6.2608e+06 | 3.6e-03 | PASS |
| single-vertical | single vertical weld, F_y −10 | runs (1 = yes) | 0 (blocked) | 1 | n/a | FAIL |

All other symbols (L_w, x_c, y_c, I_ux, I_uy, J_u, T, tau, s_req, u) agree to 1e-8 in every symmetric probe. The L-shape rows follow the decoupled law exactly (DECOUP rows PASS).

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| L_w, centroid, I_ux, I_uy, J_u | Shigley Tables 9-1, 9-2 | Same; own-axis term l Δ²/12 | Yes |
| Units | mm, kN, kN·m, N/mm | Kernel in mm; outputs match | Yes |
| T and sign convention | Spec formula | Same; KAT sign matches | Yes |
| In-plane shear share | Direct plus torsion, vector | Same | Yes |
| Out-of-plane law | Plane section with I_uxy | M_x/I_ux and M_y/I_uy only | No (-01) |
| Critical point | Weld ends | Every distinct end | Yes |
| Throat and resistance | 0.707 s; f_r/a against f_w | Same | Yes |
| Collinear guard | Block when a needed stiffness is zero | Blocks any horizontal or vertical line | No (-02) |
| Diagonal collinear group | Block when moment is about the line | Not blocked | No (-03) |
| Zero-length rows; duplicate welds | Not stated | Zero rows dropped; duplicates counted twice | Info (-04) |
| Missing table cell | Block | Block ("needs a value") | Yes |
| Input limits | s 3–25 mm, ±2000 mm, 50 rows | Enforced; 4000 mm box runs | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 614ddf89 | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|---|
| P1-mech.weld-group-01 | High | High (confirmed: reproduced 777.7 vs 1620 N/mm by hand; L-welds under bending are plausible use) | The out-of-plane law ignores the product I_uxy. For layouts without symmetry the force field breaks moment equilibrium. The result is unconservative. | L-shape (0,0)–(150,0) and (0,0)–(0,100), s 6, f_w 220, F_z 10, M_x 3, M_y 4: product f_r 777.7 N/mm, τ 183.3 MPa, u 0.833 pass. Equilibrium gives 1620 N/mm, u 1.74. F_z 10 with M_x 5: u 1.88 vs 2.29 (+22 %). M_x 5 alone: f_r 1714 vs 2100 N/mm, u 1.84 vs 2.25 (+22 %). | Add I_uxy to the kernel. Solve the 2x2 system for c₁ and c₂. Keep the current law only when I_uxy is near zero. | Open (numbers changed). I_uxy is still ignored. With F_z's eccentricity now added, L-shape F_z 10, M_x 5: product u 1.84, equilibrium 2.16 (+18 %). With M_x 3, M_y 4: 0.79 against 1.61. | Fixed (3c637a3d). L-shape with F_z 10 and M_x 5 now gives 2020 N/mm, u 2.16, which equals my equilibrium solve. With M_x 3 and M_y 4 it gives 1500 N/mm, u 1.61 (was 741.8, u 0.79). Product KAT ka-l-group-bending (1500 N/mm) matches. |
| P1-mech.weld-group-02 | Medium | Medium (confirmed: refusal, not a wrong number; it blocks Shigley Table 9-1 case 1 and the message is wrong) | The "collinear" block fires for any horizontal or vertical line, even with no bending. It refuses valid cases. The message is wrong for torsion about the centroid. | One vertical weld (0,0)–(0,100), F_y −10 at (100,50): blocked. Table 9-1 case 1 gives J_u = d³/12 = 83 333 mm³ and f_r 608 N/mm. | Block per axis, only when a load needs the missing stiffness (M_x with I_ux = 0, M_y with I_uy = 0, T with J_u = 0). | Open. Probe: one vertical weld still Blocks 'collinear'. | Fixed (3c637a3d). One vertical weld (0,0)-(0,100) with F_y −10 now runs. J_u is 83 333 mm³ and f_r 608.28 N/mm, as in Table 9-1 case 1. A moment about the line still blocks, which is correct. |
| P1-mech.weld-group-03 | Medium | Medium (confirmed: reproduced f_r 2121 N/mm, no notice) | A diagonal collinear group is not blocked. The moment about the line itself cannot be resisted, yet the product returns numbers. | One weld (0,0)–(100,100), M_x 5 kN·m: I_ux = I_uy = I_uxy = 117 851 mm³ (singular). Product shows f_r 2121 N/mm, u 2.27, no notice. | Test the determinant I_ux I_uy − I_uxy². Block or warn when it is near zero and a moment is applied. | Open. Probe: one diagonal weld with M_x 5 kN·m still gives f_r 2121 N/mm, u 2.27, no notice. | Fixed (3c637a3d). One diagonal weld with M_x 5 kN·m now gives a Block notice 'collinear'. With M_x = M_y = 5 (along the line) it runs: f_r 2121.3 N/mm, equal to my solve. |
| P1-mech.weld-group-04 | Low | Low (confirmed) | Duplicate weld rows count twice without notice. Zero-length rows drop silently. | Two 100 mm welds plus a repeat of the first: L_w 300 mm, J_u 416 667 mm³, no notice. Bolt group blocks coincident bolts. | Warn on identical or overlapping rows and on dropped zero-length rows. | Open. Probe: a repeated 100 mm weld still gives L_w 300 mm with no notice. | Open. Probe dup: a repeated 100 mm weld still gives L_w 300 mm with no notice. The zero-length row still drops silently. |
| P1-mech.weld-group-05 | Info | Info (confirmed) | The sheet does not check weld length limits, end returns or the directional method of EN 1993-1-8. The design strength f_w is a user input. | Source and spec text. | State this in the spec limitations. | Open (Info). Unchanged. | Open (Info). Unchanged. |

## Notes for the owner's manual test
- Two vertical welds, 50 mm apart, 100 mm long, F_y −20 kN at 175 mm from the centroid: expect J_u 291 667 mm³ and f_r 721.11 N/mm.
- Try the L-shape in -01 with M_x 3 and M_y 4 kN·m. Solve the 2x2 system by hand. Expect about 1620 N/mm.
- Enter one vertical weld. Expect J_u = d³/12. The product blocks it.
- Enter a C-shape 60 x 100 x 60 mm. Expect J_u = 468 424 mm³ from the Table 9-1 closed form.
- Enter a 48-segment ring of radius 100 mm with M_z 10 kN·m. Expect J_u close to 2π r³ = 6.28e6 mm³ (0.4 % lower).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- The script is an independent line-property derivation with I_uxy and a 2x2 solve. It is not a port of the C#.
- I re-ran the script: 10/10 KAT rows pass, exit 0.
- I reproduced -01, -02 and -03 with the probe. I checked -01 by hand: c₁ 17.16, c₂ 29.40 N/mm², f_z 1620 N/mm at (0, 100).
- -01 stays High. An L-weld under one moment is already 22 % unconservative.
- The -01 evidence row named "M_x 5 alone" but the script case also had F_z 10. I added the true M_x-only figures.
- -02 stays Medium. It refuses a valid textbook case but never prints a wrong number.
- I read Run line by line. Units, torsion sign, end-point maximum and the 0.707 s throat are correct. I found no new defect.
- Residual doubt: the throat check uses the full vector f_r against f_w. That is the EN simplified method only; the directional method is not offered (-05).

## Re-check after DA-17/DA-18 and A-4/A-5 (2026-10-24)
- Source changed (Opus review, finding 7). F_z acts at (e_x, e_y), so F_z(e_y − y_c) now adds to M_x.
- Likewise F_z(e_x − x_c) now adds to M_y. New steps M_x_tot and M_y_tot appear when F_z is not zero.
- The KATs have F_z = 0, so they are unchanged. Probe results with an F_z load changed: L-shape u 1.88 to 1.84 and 0.83 to 0.79; trapezoid 0.377 to 0.383.
- No finding in this report was about F_z eccentricity. All five findings are still Open. Finding 01 now carries new numbers, shown in the Status column.
- Script updated: the model adds the F_z eccentricity, and four probe rows carry the new product values. The decoupled-law rows pass again. It exits 0 with 10 of 10 KAT rows.
- The sign of the added moment follows the product's own M_x, M_y convention (first moment of f_z). It is self-consistent. The owner should confirm it against the spec.
- New defects: none found.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed (3c637a3d, a23e6e18): version 1.1.0. The kernel GroupGeometry carries the product moment, so f_z uses the unsymmetric-bending form. A new output I_uxy appears. A group on one line blocks only when a moment acts about that line.
- A new KAT, ka-l-group-bending, passes. The three older KATs are unchanged and pass.
- Fixed: -01 (High), -02 and -03 (both Medium). The L-shape rows now match my equilibrium solve to 6 digits. The one-line cases behave as I expect.
- Still open: -04 (Low) and -05 (Info). I found no new defect. The snap of a load placed on the centroid works: F_z at the middle of a vertical weld runs.
- Script updated: the old decoupled rows were removed. It has the new KAT, six new probe cases, new BLOCK rows and I_uxy. It exits 0, with 214 of 214 extra rows passing.
