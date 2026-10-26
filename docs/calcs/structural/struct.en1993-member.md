# S3 — EN 1993-1-1 member checks, basic (`struct.en1993-member@1.2.0`)

**In plain terms.** EN 1993-1-1 checks a steel member in two stages. First the cross-section: can it carry the tension, compression, bending and shear without yielding. Then the member: a strut buckles sideways before it crushes, so its compression resistance is reduced by a factor that depends on how slender it is and on the buckling curve for its shape. Combined compression and bending use a simplified interaction, and the governing ratio is the highest of all the checks.

**Scope.** Class 1–3 members: tension (6.2.3), compression (6.2.4), bending y/z (6.2.5), shear with the high-shear reduction (6.2.6, 6.2.8), flexural buckling about y and z (6.3.1.1, 6.3.1.2), linear cross-section interaction (6.2.1(7)) and a simplified member interaction (6.3.3 with user `k_yy`, `k_zz`, default 1.0). Not covered: lateral-torsional buckling (warned), class 4, torsion, web buckling.

**Method.** Formulas in own notation (see the template method text). Imperfection factors by curve (a0 … d) are textbook defaults (Gardner & Nethercot §6.3.1) overridable by `alpha_y` / `alpha_z`; partial factors `gamma_M0`, `gamma_M1` default 1.0 (UK NA) and are inputs. The rolled-I shear-area helper computes `A_v = max(A − 2bt_f + (t_w + 2r)t_f, η h_w t_w)` when `A_v` is blank. The helper dimensions `h`, `b`, `t_w`, `t_f`, `r` carry **no defaults** (a new sheet opens without them, so the shear check blocks until `A_v` or the section's own dimensions are entered); when the helper is used and `2 b t_f + (h − 2 t_f) t_w` differs from `A` by more than 10 % the sheet warns (`helper-area`). When `N_Ed` is compressive, `N_Ed/N_b,Rd` exceeds 0.3, a moment acts about the axis and `k_yy` or `k_zz` is left at 1.0, the sheet warns (`k-default`) and shows the Annex A/B upper bound `C_m(1 + 0.8 n)` with `C_m = 1` and the member interaction it would give; no Annex table is shipped, so `k` is still entered by the user.

**Inputs.** actions `N_Ed` (tension +; **opposite to S6's compression-positive `N_Ed`**: from S2 take the member force `N_e1`), `M_y_Ed`, `M_z_Ed`, `V_Ed`; material `f_y`, `E`, `gamma_M0`, `gamma_M1`; section `section_class`, `A`, `W_pl_y`, `W_el_y`, `W_pl_z`, `W_el_z`, `I_y`, `I_z`, `A_v` or `h`, `b`, `t_w`, `t_f`, `r`, `eta`; buckling `L_cr_y`, `L_cr_z`, `curve_y`, `curve_z`, `alpha_y`, `alpha_z`, `k_yy`, `k_zz`. **Outputs.** resistances `N_pl_Rd`, `V_pl_Rd`, `M_y_c_Rd`, `M_z_c_Rd`, `N_cr_*`, `lambda_*`, `chi_*`, `N_b_*_Rd`, `N_b_Rd`; utilisations `u_N`, `u_M_y`, `u_M_z`, `u_V`, `u_section`, `u_buckling`, `u_member`. **Checks.** seven (five in tension), governing first.

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
… 17 more step(s) on the sheet
Governing: FAIL  Member interaction (simplified): u_member = 1.0435 vs u_member ≤ 1  utilisation 1.04  « 6.3.3
```
<!-- hand-arithmetic:end -->
