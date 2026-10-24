# S3 — EN 1993-1-1 member checks, basic (`struct.en1993-member@1.0.1`)

**Scope.** Class 1–3 members: tension (6.2.3), compression (6.2.4), bending y/z (6.2.5), shear with the high-shear reduction (6.2.6, 6.2.8), flexural buckling about y and z (6.3.1.1, 6.3.1.2), linear cross-section interaction (6.2.1(7)) and a simplified member interaction (6.3.3 with user `k_yy`, `k_zz`, default 1.0). Not covered: lateral-torsional buckling (warned), class 4, torsion, web buckling.

**Method.** Formulas in own notation (see the template method text). Imperfection factors by curve (a0 … d) are textbook defaults (Gardner & Nethercot §6.3.1) overridable by `alpha_y` / `alpha_z`; partial factors `gamma_M0`, `gamma_M1` default 1.0 (UK NA) and are inputs. The rolled-I shear-area helper computes `A_v = max(A − 2bt_f + (t_w + 2r)t_f, η h_w t_w)` when `A_v` is blank.

**Inputs.** actions `N_Ed` (tension +; **opposite to S6's compression-positive `N_Ed`**: from S2 take the member force `N_e1`), `M_y_Ed`, `M_z_Ed`, `V_Ed`; material `f_y`, `E`, `gamma_M0`, `gamma_M1`; section `section_class`, `A`, `W_pl_y`, `W_el_y`, `W_pl_z`, `W_el_z`, `I_y`, `I_z`, `A_v` or `h`, `b`, `t_w`, `t_f`, `r`, `eta`; buckling `L_cr_y`, `L_cr_z`, `curve_y`, `curve_z`, `alpha_y`, `alpha_z`, `k_yy`, `k_zz`. **Outputs.** resistances `N_pl_Rd`, `V_pl_Rd`, `M_y_c_Rd`, `M_z_c_Rd`, `N_cr_*`, `lambda_*`, `chi_*`, `N_b_*_Rd`, `N_b_Rd`; utilisations `u_N`, `u_M_y`, `u_M_z`, `u_V`, `u_section`, `u_buckling`, `u_member`. **Checks.** seven (five in tension), governing first.

**Test cases.** UC 203×203×46 S355 4 m compression (analytic by the 6.3.1 formulae, Unconfirmed against a published example); UB 305×165×40 S275 bending + shear (analytic); class 3 tie with minor-axis moment (analytic, fails at 1.03).
