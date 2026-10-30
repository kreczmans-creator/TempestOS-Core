# struct.base-plate — independent re-derivation (Track P, P1)
Calculator: Column base plate (EN 1993-1-8), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Structural/BasePlateCalculator.cs. Spec: docs/calcs/structural/struct.base-plate.md.
Script: tools/verification/struct.base-plate.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (after review: one High, three Medium)

All 65 KAT values match. The script also reproduces 351 extra probe values from the C# product to 1e-6. One defect is real: the required thickness of a CHS column base is wrong (P1-struct.base-plate-01). The review raised -03 to Medium and added R1 (f_y does not follow the plate thickness).

## Method re-derived
- Bearing strength: f_cd = α_cc f_ck/γ_c (EN 1992-1-1 cl. 3.1.6); f_jd = (2/3) α f_cd with α ≤ 3 (EN 1992-1-1 cl. 6.7; EN 1993-1-8 cl. 6.2.5). Units MPa.
- Outstand c = t_p √(f_y/(3 f_jd γ_M0)) (EN 1993-1-8 cl. 6.2.5). Units mm.
- A_eff: I/H, RHS and CHS footprints grown by c. Overlap gives the full rectangle. Outer outstands are cut at the plate edge. Outstands that point into the column keep the full c. The area is capped at B·L.
- N_j,Rd = f_jd A_eff (cl. 6.2.8.2). Compression T-stub C_Rd = f_jd A_flange, capped at A_eff/2 (cl. 6.2.6.9, 6.2.6.10).
- Table 6.7 by equilibrium about the tension row, with M in kN·mm. C = (M + N x_b)/z and T = C − N, with z = z_C + x_b. This is regime 1.
- Regime 2 (N > 0, T < 0): lever arm 2 z_C, C = N/2 ± M/(2 z_C). Regime 3 (N < 0, C < 0): lever arm 2 x_b, T = −N/2 ± M/(2 x_b).
- Regime limits in eccentricity form: regime 1 starts at M = N z_C for N > 0 and at M = |N| x_b for N < 0. The forces are continuous across both limits.
- z_C: I/H (h − t_f)/2, RHS (h − t)/2, CHS centroid of the half annulus 4(R³ − r³)/(3π(R² − r²)).
- Tension T-stub (Table 6.2, cl. 6.2.4, 6.2.6.11): M_pl,Rd = 0.25 l_eff t_p² f_y/γ_M0. Without prying min(2 M_pl/m, n_t F_t,Rd). With prying modes 4 M_pl/m, (2 M_pl + n Σ F_t,Rd)/(m + n) and Σ F_t,Rd, with n = min(e_min, 1.25 m).
- Shear (cl. 6.2.2): α_bc = 0.44 − 0.0003 f_yb; F_2,vb,Rd = α_bc f_ub A_s/γ_M2; V_Rd = C_f,d max(N,0) + n_b min(F_1, F_2).
- Required thickness: smallest t_p in 10–100 mm with u_N, u_C, u_T ≤ 1 (Brent root of max utilisation − 1).

## Known-answer cases
The script prints every value (one row each). The table gives the worst value of each case. All KAT tolerances are 1e-6 or 1e-9. The worst relative difference is 4.4e-10.

| Case | Symbol (worst) | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-ih-moment-bolts (21 values) | F_t_Ed | 110.9075044 | 110.9075044 | 3.3e-10 | 1e-6 | PASS 21/21 |
| ka-ih-moment-prying (6) | T_Rd_2 | 331.2698413 | 331.2698413 | 9.1e-11 | 1e-6 | PASS 6/6 |
| ka-ih-both-compression (5) | u_C | 0.7719863767 | 0.7719863767 | 5.8e-11 | 1e-6 | PASS 5/5 |
| ka-ih-both-tension (5) | T_Ed_2 | 89.39393939 | 89.39393939 | 4.4e-11 | 1e-6 | PASS 5/5 |
| ka-ih-axial (9) | f_jd | 14.16666667 | 14.16666667 | 2.4e-10 | 1e-6 | PASS 9/9 |
| ka-rhs-axial (4) | t_req | 14.4652113 | 14.4652113 | 2.9e-10 | 1e-6 | PASS 4/4 |
| ka-chs-axial (4) | N_j_Rd | 1044.436541 | 1044.436541 | 4.4e-10 | 1e-6 | PASS 4/4 |
| ka-chs-moment (8) | F_t_Ed | 106.4228956 | 106.4228956 | 3.6e-10 | 1e-6 | PASS 8/8 (see note) |
| ka-ih-overlap-clip (3) | N_j_Rd | 1502.369932 | 1502.369932 | 2.8e-10 | 1e-6 | PASS 3/3 |

Note on ka-chs-moment, t_req = 15.39956 mm. It matches only when z_C is held at its value for the entered t_p = 20 mm. With z_C re-evaluated at each t, the value is 15.5873 mm. At 15.3996 mm the compression utilisation is 1.011. The KAT pins the defect in P1-struct.base-plate-01. The other 64 values do not depend on this.

## Extra probes (product vs independent)
All product values come from the C# probe. Full list: `EXTRAS` in the script (27 probes, 351 values, all PASS).

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| UC254, C30, α 1.8 | 450×450×30, N 800, M 150 | u_C / t_req | 0.86016 / 26.436 | 0.86016 / 26.436 | <1e-9 | PASS |
| Regime 1/2 limit | N 600, M 72.9 | C_Ed, T_Ed | 600.0, 0 | 600.0, 0 | 0 | PASS |
| Regime 1/3 limit | N −300, M 49.5 | T_Ed, F_t_Ed | 300.0, 150.0 | 300.0, 150.0 | 0 | PASS |
| Net tension, 3 bolts a row | N −1000, M 0 | F_t_Ed, t_req | 166.667, 22.563 | 166.667, 22.563 | <1e-9 | PASS |
| N = 0 | M 100 | T_Ed, t_req | 349.040, "bolts govern" | 349.040, bolts govern | <1e-9 | PASS |
| Near zero moment | N 300, M 0.001 | C_Ed (regime 2) | 150.00412 | 150.00412 | <1e-9 | PASS |
| RHS 250×150×8 with moment | N 250, M 80 | u_C | 0.78229 | 0.78229 | <1e-9 | PASS |
| RHS 200×200×10, plate 230×300 | A_eff cut to B·L | A_eff | 69000 | 69000 | 0 | PASS |
| CHS 300×10, plate 340×500 | outstand cut | A_eff, z_C | 86195.54, 75.132 | 86195.54, 75.132 | <1e-9 | PASS |
| I/H 300, plate 340×480 | cut, no overlap, M 150 | A_eff, u_C | 118572.84, 1.03716 | 118572.84, 1.03716 | <1e-9 | PASS |
| I/H 150, plate 500×500 | overlap, no cut | A_eff, u_C | 124961.41, 1.28382 | 124961.41, 1.28382 | <1e-9 | PASS |
| Prying, e_min 80 | mode 2 governs | u_T, T_mode | 0.69645, mode 2 | 0.69645, mode 2 | <1e-9 | PASS |
| Prying, thin plate | mode 1 governs | u_T | 2.19804 | 2.19804 | <1e-9 | PASS |
| f_yb 235, C_f,d 0.3, F_1 20 kN | F_1 governs shear | u_V | 0.88235 | 0.88235 | <1e-9 | PASS |
| α 3, α_cc 0.8, γ_c 1.2, γ_M0 1.1 | N 1500 | t_req | 11.7778 | 11.7778 | <1e-9 | PASS |
| t_p 100, plate 1500×1500 | N 3000, M 400 | t_req | 50.0259 | 50.0259 | <1e-9 | PASS |
| N 10000 | M 0 | t_req | "> 100 mm" | > 100 mm | n/a | PASS |
| CHS 219.1×8, t_p 60, M 150 | thicker plate than needed | t_req | 21.666 | 33.649 | 0.36 | DIFFERS (finding 01) |
| Same CHS, t_p 25 | thinner plate | t_req | "bolts govern" | 33.649 | n/a | DIFFERS (finding 01) |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| f_cd, f_jd, c | cl. 6.2.5, EN 1992-1-1 | Same; β_j = 2/3 fixed; grout checked only when entered | Yes |
| Units | MPa, mm, kN, kN·m | M × 1e3 to kN·mm; l_eff, m in mm; T-stub in N·mm then /1e3 | Yes |
| A_eff, overlap, edge cut | Spec: outer c cut, interior keeps c | Same; overlap tested with the uncut c | Yes |
| A_flange when cut | Spec: (b + 2c)(t_f + 2c) | (b + 2c_x)(t_f + c_y + c), more exact | Yes (spec wording is loose) |
| Table 6.7 regimes, lever arms | Equilibrium, z = z_C + x_b, 2 z_C, 2 x_b | Same; limits N z_C and \|N\| x_b are continuous | Yes |
| Signs | N compression positive, M ≥ 0 | Same; M < 0 is blocked (symmetric plate) | Yes |
| z_C for CHS | Half-annulus centroid | Same, with outer cut and inner full c | Yes |
| Compression T-stub check | Only M > 0 and C > 0 | Same | Yes |
| Tension T-stub | Table 6.2, prying off by default | Same, including n = min(e_min, 1.25 m) | Yes |
| Shear | cl. 6.2.2, α_bc, friction on compression only | Same; friction uses N_Ed, not C_Ed | Yes (conservative) |
| t_req search | u_N, u_C, u_T ≤ 1 at each t | z_C and forces fixed at entered t_p | NO for CHS (finding 01) |
| Guards: plate, section, bolts, x_b, m, e_min, grout | Spec | All fire at the stated thresholds (equal values block) | Yes |
| Guard: z_C_in | Spec lists none | None above the plate half-length | Gap (finding 02) |
| f_y with plate thickness | EN 10025-2 bands (16, 40, 63 mm) | One f_y for every t, also in the t_req search | No, see R1 |
| Hard limits | Equal to stated ranges | α 3.5, f_y 500, M < 0, t_p 9, N 10001 all blocked | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-struct.base-plate-01 | High | High (confirmed). Reproduced: t_p 60 gives t_req 21.666 mm. A re-run at t_p 21.666 gives u_C 1.116, u_T 1.040 and "bolts govern". t_req is unconservative guidance for a CHS with moment. The checks at the entered t_p are right. | For a CHS column, t_req and the "bolts govern" text depend on the entered t_p. z_C is the half-annulus centroid, which depends on c and so on t_p. The search holds z_C, C_Ed and T_Ed at the entered t_p. The reported t_req can fail its own checks. | CHS 219.1×8, plate 500×500, N 100, M 150, x_b 200, m 25, l_eff 400, F_t,Rd 250. At t_p 60 the product gives t_req 21.67 mm. At t_p 21.67 u_C is 1.116 and u_T is 1.040. At t_p 25 it says "bolts govern". At t_p 33.7 it gives 23.5. The true minimum is 33.65 mm. KAT ka-chs-moment gives 15.3996; the consistent value is 15.5873. | Recompute the centroid and the forces inside `RequiredThicknessAll` at every trial t. Test "bolts govern" at t = 100 mm. Change the KAT t_req to 15.58735 and the spec value 15.39956. I/H, RHS and entered z_C are not affected. | Open. Probe unchanged: t_p 60 gives t_req 21.666 mm. At t_p 21.667 u_C is 1.116 and u_T 1.040. t_p 25 gives 'bolts govern'. t_p 33.65 gives 23.53. |
| P1-struct.base-plate-02 | Medium | Medium (confirmed). Reproduced: z_C_in 1000 gives T_Ed 128.76 kN, u_T 0.457; blank gives 370.24 kN, u_T 1.313. | `z_C_in` has no upper guard. A value beyond the plate half-length puts the compression resultant outside the plate. A smaller T_Ed results, which is unconservative. | Default I/H, N −50, M 100. With z_C blank, T_Ed is 370.2 kN and u_T is 1.31 (FAIL). With z_C_in 1000, T_Ed is 128.8 kN and u_T is 0.46 (pass). A user who enters z (286.5 mm) instead of z_C (121.5 mm) triggers the same effect. | Block `z_C_in` ≥ L/2. Better, warn when it differs from the section value by more than c. State that z_C is not z. | Open. z_C_in 1000 still gives T_Ed 128.76 kN, u_T 0.457 (blank: 370.24 kN, 1.313). No guard added. |
| P1-struct.base-plate-03 | Low | Medium (upgraded). The effect is unconservative, not cosmetic. Probe: f_ub 1000, f_yb 640, V_Ed 250 kN: α_bc 0.248, F_2,vb,Rd 48.57 kN, u_V 0.983 pass. With α_bc 0.17 (f_yb 900): F_2,vb,Rd 33.29 kN, V_Rd 193.2 kN, u_V 1.29 FAIL. Block f_ub above 800 MPa, or warn that cl. 6.2.2 does not cover it. | The anchor shear factor α_bc is valid for f_yb of 235–640 MPa, so f_yb is capped at 640. The f_ub range reaches 1000 MPa. A class 10.9 bolt (f_yb about 900 MPa) cannot be entered truthfully. Entering 640 gives α_bc 0.248, where the formula extrapolates to 0.17. | f_ub 1000, f_yb 640 returns F_2,vb,Rd = 0.248·1000·A_s/γ_M2. | Warn when f_ub > 800 MPa that α_bc is not defined above f_yb 640 MPa. | Open. f_ub 1000, f_yb 640, V_Ed 250 still gives alpha_bc 0.248, u_V 0.983 (pass). No warning added. |
| P1-struct.base-plate-04 | Low | Low (confirmed). | The plate block message names `h` even when `b > B` fails. | B 250, h 260, b 256 gives "h: the column (260 × 256 mm) must stand inside the plate (L = 400, B = 250 mm)". | Name the failing symbol (b or h). | Open. B 250, h 260, b 256 still blocks with the message that names h. |
| P1-struct.base-plate-05 | Info | Info (confirmed). | Friction uses C_f,d·N_Ed. The contact force under a moment is C_Ed, which is larger. This is conservative and follows the wording of cl. 6.2.2. | Default KAT: friction 60 kN. C_f,d·C_Ed would be 104 kN. | None. Consider noting it on the sheet. | Open (no change). Friction still uses C_f,d·N_Ed. |
| P1-struct.base-plate-06 | Info | Info (confirmed). | A missing required input takes the template default with a Warning only. Omitting N_Ed silently uses 300 kN. | Probe with only the column inputs runs with N 300, M 100. | Platform behaviour. Consider a block for load inputs. | Partly fixed (3c637a3d): m and l_eff have no default and block when missing. alpha now defaults to 1.0, with a warning. N_Ed, M_Ed and V_Ed still take 300, 100 and 80 with a Warning only. |
| P1-struct.base-plate-07 | Info | Info (confirmed). | The spec gives A_flange as (b + 2c)(t_f + 2c) for all cases. The code uses (b + 2c_x)(t_f + c_y + c) when the outer outstand is cut. The code is the more exact one. | I/H 300, plate 340×480: A_flange agrees with the script. | Update the spec wording. | Open (no change). The code still uses (b + 2c_x)(t_f + c_y + c); the spec wording is as before. |
| P1-struct.base-plate-R1 | Medium | Medium (new) | One f_y is used at every plate thickness. EN 10025-2 lowers f_y of S275 to 265 MPa above 16 mm and 255 MPa above 40 mm. The record kind steel.strengths points to EN 10025-2, yet the default is 275 MPa at t_p 20 mm. The t_req search over 10–100 mm also keeps the entered f_y. | Probe: t_p 50 mm, l_eff 40 mm: M_pl,Rd 6.875 kN·m at 275 MPa, 6.375 kN·m at 255 MPa (7.8 % high). At 20 mm the default is 3.8 % high. EN 1993-1-1 Table 3.1 allows 275 MPa up to 40 mm, so only the UK NA route errs there. | Warn when t_p or t_req passes a thickness band of the entered f_y. Or take f_y(t) from the steel.strengths record inside the search. | Open. t_p 50, l_eff 40 still gives M_pl,Rd 6.875 kN·m at 275 MPa. No band warning. |
| P1-struct.base-plate-E1 | Medium | Medium (confirmed). Reproduced: t_p 20 gives t_req 22.84 mm, and t_p 22.84 fails at u_C 1.013. The checks at the entered t_p are right. From a thick plate t_req errs safe: t_p 40 gives 27.64, which passes at 0.943. | The Blue-round fix of the lever arm (G-A-10, 3c637a3d) is right. It makes z_C depend on c for I/H and RHS columns. This happens whenever the outstand is cut at the plate edge. The required-thickness search still holds z_C at the entered t_p. So t_req depends on the entered t_p. It can fail its own check. This is the CHS defect (-01) now reaching I/H and RHS. Where the outstand is cut, u_C can also rise with t_p. The spec rule 'each falls as t_p rises' then fails. | I/H 260×256×17×10 on a 360×360 plate: N 500, M 100, x_b 165, m 28, l_eff 200, alpha 1.5. At t_p 20 the product gives t_req 22.84 mm. At t_p 22.84 the product gives u_C 1.013 (FAIL). t_p 40 gives t_req 27.64. RHS 200×200×10, plate 230×300, N 300, M 60: t_p 20 gives t_req 29.60 and u_C 1.220. At t_p 29.60 u_C is 1.058. At t_p 40, 50 and 60 u_C is 1.017, 1.087 and 1.166 and t_req is '> 100 mm'. Script rows RHS-clip-*, IH-cutL-*. | Recompute z_C, C_Ed and T_Ed inside the t_req search at each trial t (as for -01). Warn when the outstand is cut and u_C rises with t_p. Keep the check at the entered t_p as it is. | New (open) Reviewer check (2026-10-25): reproduced, open. |

## Notes for the owner's manual test
- Default I/H base (260×256×17×10, 400×400×20 S275, C25/30, α 1.5, N 300, M 100): expect z_C 121.5 mm, C_Ed 521.82 kN, T_Ed 221.82 kN, u_C 0.867, T_Rd 282 kN.
- Regime limits by hand: N 600 gives T_Ed = 0 at M = 72.9 kN·m. N −300 gives C_Ed = 0 at M = 49.5 kN·m. Both sides join smoothly.
- Net tension N −1000, M 0, three bolts a row: expect 500 kN a row and 166.67 kN a bolt.
- Test a CHS base with moment at two plate thicknesses. Compare t_req with a re-run at t_p = t_req. Expect u > 1 until finding 01 is fixed.
- Enter z_C_in 1000 with N −50, M 100 and see the guard gap (finding 02). Overlap check: 150×150×10×6 on 300×400×40 gives A_eff 106 049.64 mm².

## Independent review
- Reviewer: senior review (Claude), 2026-10-24.
- I ran the script (exit 0, 416/416). It derives the method from the clauses, not from the C#. It mirrors the product's edge-cut rule on purpose.
- I reproduced -01, -02 and -03 with the probe. Inputs are in /tmp/claude-0/review-inputs/bp/.
- I read Run, Effective, Statics, TensionTStub and RequiredThicknessAll line by line. The Table 6.7 regimes, units and T-stub modes are right.
- I checked the CHS half-annulus model. A smaller z_C with the full half area is conservative, so it is not a defect.
- Changes: -03 raised to Medium. New: R1 (Medium), f_y does not follow the plate thickness.
- Residual doubt: z_C at the flange mid-thickness ignores a cut outstand. That is the EN simplification, so I did not raise it.
- Residual doubt: the -01 fix also changes KAT ka-chs-moment t_req. Track S must update the KAT and the spec together.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source change (3c637a3d, a23e6e18): G-A-10 moves the I/H and RHS lever arm in by (c − c_cut)/2. This applies when the outstand is cut at the plate edge. `m` and `l_eff` lost their defaults. `alpha` defaults to 1.0. N_Ed is documented as compression positive. The PROFIS condition passes only when profis_ref carries Evidence provenance; typed text only warns.
- KATs: `Probe kat` passes all 10 cases and no expected value changed. The script still exits 0 (468/468 values match).
- Script change: the product is now right and the script tracked the old centroid, so `zc_default` now applies the cut. RHS-clip is re-observed, and four new rows cover the cut-outstand cases. A cross-check of all extras against the live probe shows no other difference.
- Findings: of the 8 earlier rows, 0 are fixed, 1 is partly fixed (-06) and 7 stay open. The High finding (-01, CHS t_req) is still open. The probe numbers are identical to those of the review.
- New: P1-struct.base-plate-E1 (Medium). The -01 defect now also affects I/H and RHS columns with a cut outstand, because z_C moves with c.
- Probe inputs: /tmp/claude-0/recheck4/struct-bs/ (bp1.py, bp2.py, bpcmp.py).
