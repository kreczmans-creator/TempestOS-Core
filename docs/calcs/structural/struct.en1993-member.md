# S3 — EN 1993-1-1 member checks, basic (`struct.en1993-member@1.5.0`)

**In plain terms.** EN 1993-1-1 checks a steel member in two stages. First the cross-section: can it carry the tension, compression, bending and shear without yielding. Then the member: a strut buckles sideways before it crushes, so its compression resistance is reduced by a factor that depends on how slender it is and on the buckling curve for its shape. Combined compression and bending use a simplified interaction, and the governing ratio is the highest of all the checks.

**Scope.** Class 1–3 members: tension (6.2.3), compression (6.2.4), bending y/z (6.2.5), shear with the high-shear reduction (6.2.6, 6.2.8), flexural buckling about y and z (6.3.1.1, 6.3.1.2), linear cross-section interaction (6.2.1(7)) and a simplified member interaction (6.3.3 with user `k_yy`, `k_zz`, default 1.0). Not covered: lateral-torsional buckling (warned), class 4, torsion, web buckling.

**Method.** Formulas in own notation (see the template method text). Imperfection factors by curve (a0 … d) are textbook defaults (Gardner & Nethercot §6.3.1) overridable by `alpha_y` / `alpha_z`; partial factors `gamma_M0`, `gamma_M1` default 1.0 (UK NA) and are inputs. The rolled-I shear-area helper computes `A_v = max(A − 2bt_f + (t_w + 2r)t_f, η h_w t_w)` when `A_v` is blank. The helper dimensions `h`, `b`, `t_w`, `t_f`, `r` carry **no defaults** (a new sheet opens without them, so the shear check blocks until `A_v` or the section's own dimensions are entered); when the helper is used and `2 b t_f + (h − 2 t_f) t_w` differs from `A` by more than 10 % the sheet warns (`helper-area`). When `N_Ed` is compressive, `N_Ed/N_b,Rd` exceeds 0.3, a moment acts about the axis and `k_yy` or `k_zz` is left at 1.0, the sheet warns (`k-default`) and shows the Annex A/B upper bound `C_m(1 + 0.8 n)` with `C_m = 1` and the member interaction it would give; no Annex table is shipped, so `k` is still entered by the user.

**Inputs.** actions `N_Ed` (tension +; **opposite to S6's compression-positive `N_Ed`**: from S2 take the member force `N_e1`), `M_y_Ed`, `M_z_Ed`, `V_Ed`; material `f_y`, `E`, `gamma_M0`, `gamma_M1`; section `section_class`, `A`, `W_pl_y`, `W_el_y`, `W_pl_z`, `W_el_z`, `I_y`, `I_z`, `A_v` or `h`, `b`, `t_w`, `t_f`, `r`, `eta`; buckling `L_cr_y`, `L_cr_z`, `curve_y`, `curve_z`, `alpha_y`, `alpha_z`, `k_yy`, `k_zz`. **Outputs.** resistances `N_pl_Rd`, `V_pl_Rd`, `M_y_c_Rd`, `M_z_c_Rd`, `N_cr_*`, `lambda_*`, `chi_*`, `N_b_*_Rd`, `N_b_Rd`; utilisations `u_N`, `u_M_y`, `u_M_z`, `u_V`, `u_section`, `u_buckling`, `u_member`. **Checks.** seven (five in tension), governing first.

**Changes in 1.3.0.** High shear (`V_Ed > 0.5 V_pl,Rd`, cl. 6.2.8): for a class 1–2 I section whose `h`, `t_w` and `t_f` are entered, only the web part of the plastic modulus is reduced, `M_y,V,Rd = (W_pl,y − ρ A_w²/(4 t_w)) f_y/γ_M0` with `A_w = (h − 2 t_f) t_w` (Eq. 6.30), and `M_z,c,Rd` is not reduced (the flanges carry the minor-axis moment); without those dimensions, or for class 3, the whole resistance keeps the conservative `(1 − ρ)` and the warning says so. The member interaction (6.3.3, Eq. 6.61 and 6.62) divides the moments by the characteristic resistance over `γ_M1`, `M_y,b,Rd = W_y f_y/γ_M1` and `M_z,b,Rd = W_z f_y/γ_M1`, without the shear reduction (before, by `M_c,Rd` over `γ_M0`, which is unconservative when `γ_M1 > γ_M0`). A tension case warns that only gross-section yielding is checked: the net-section fracture resistance `N_u,Rd` (6.2.3(2)(b)) is not computed and governs a tie with bolt holes.

**Changes in 1.4.0 (C-2 fix, M1).** Four checks that passed while the sheet said the result was unsafe now stop or fail. `net-section`: a tension member (`N_Ed` > 0) needs the net area `A_net` (enter `A` where there are no holes, never above `A`) and `f_u` (new inputs, with `γ_M2`, default 1.1); without them the sheet is blocked, and with them it shows `N_u,Rd = 0.9 A_net f_u/γ_M2` (6.2.3(2)(b)) and `u_net = N_Ed/N_u,Rd` with the check "Tension against the net-section fracture resistance" (the pinned example in the worked values gives `N_u,Rd` 801.8 kN, `u_net` 0.4989). `no-ltb`: the new choice `ltb` (Restrained, the default, or Not restrained) says what the sheet assumes; a member bending about y with the flange not restrained is blocked and sent to the LTB interaction sheet instead of passing with a warning. `k-default`: a `k_yy` or `k_zz` left at 1.0 under compression with a moment (no threshold since 1.5.0; it was `N_Ed/N_b,Rd` above 0.3) is replaced in the interaction by the Annex A/B upper bound `C_m(1 + 0.8n)` (`C_m` = 1), shown as the outputs `k_yy_eff` / `k_zz_eff`; one entered with a recorded basis (an assumption, a specification, a drawing) is used as entered, and one entered as 1.0 with no basis keeps its warning with the interaction the bound would give. Utilisation, ports and checks are otherwise unchanged; the value of every other run is unchanged.

**Changes in 1.5.0 (C-2 fix, F1 and F7).** (F1) The `k-default` threshold is gone: a `k_yy` or `k_zz` left at 1.0 (not entered, or entered as 1.0 with no recorded basis) under compression with a moment is replaced in the check by the upper bound C_m(1 + 0.8 n) whatever the compression, because the bound is above 1 for any n. python3 on the SHS member of the verification case: N_Ed/N_b,Rd = 0.2973593 gave u_member 0.992195 with k at 1.0 (a pass) and gives 1.1574877 with the bound 1.2378875 (a fail); a second case, u_member 0.9626522 before and 1.1390474 now. A k recorded with a basis is used as entered, as before. (F7) `ltb` has **no default**: it is optional, and with `M_y_Ed` above 0 and `ltb` blank the sheet blocks (rule `ltb`) and asks whether the compression flange is restrained, where the default Restrained had been assumed and passed an unrestrained beam. Restrained and Not restrained read as before (Not restrained still blocks with `no-ltb`); a saved instance with the choice made is unchanged, one that never made it blocks until it does. The worked examples, the known-answer case `ka-ub-bending-shear` and the pack's column record Restrained; the pack's column is a cantilever with a free top, which nothing restrains, so that entry is an assumption of the example and is stated as one (the member sheet cannot check a cantilever's lateral-torsional buckling).

**Test cases.** UC 203×203×46 S355 4 m compression (analytic by the 6.3.1 formulae, Unconfirmed against a published example); UB 305×165×40 S275 bending + shear (analytic); class 3 tie with minor-axis moment (analytic, fails at 1.03).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** UC 203×203×46 S355 column, compression with major-axis moment ([sheet](../../worked-examples/structural/struct.en1993-member/uc-column-axial-moment/sheet.txt)).

```text
r_h = 10.2 mm
A_v_rolled = 58.7 cm² − 2 × 203.6 mm × 11 mm + (7.2 mm + 2 × 10.2 mm) × 11 mm = 16.944 cm²
A_v_min = 1 × (203.2 mm − 2 × 11 mm) × 7.2 mm = 13.046 cm²
A_v = max(16.944 cm², 13.046 cm²) = 16.944 cm²
N_pl_Rd = 58.7 cm² × 355 MPa / 1 = 2083.8 kN
W_y = 497 cm³
W_z = 231 cm³
V_pl_Rd = 16.944 cm² × 355 MPa / (sqrt(3) × 1) = 347.28 kN
u_V = 20 kN / 347.28 kN = 0.05759
rho = 0
M_y_c_Rd = (1 − 0) × 497 cm³ × 355 MPa / 1 = 176.44 kN·m
M_z_c_Rd = (1 − 0) × 231 cm³ × 355 MPa / 1 = 82.005 kN·m
u_M_y = 40 kN·m / 176.44 kN·m = 0.22671
u_M_z = 0 kN·m / 82.005 kN·m = 0
… 19 more step(s) on the sheet
Governing: FAIL  Member interaction (simplified): u_member = 1.05 vs u_member ≤ 1  utilisation 1.05  « 6.3.3
```
<!-- hand-arithmetic:end -->
