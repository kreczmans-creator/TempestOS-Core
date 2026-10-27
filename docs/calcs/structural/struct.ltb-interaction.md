# Lateral-torsional buckling and N+M interaction (EN 1993-1-1) (`struct.ltb-interaction@1.3.1`)

**In plain terms.** A long slender I-beam loaded in bending can twist and buckle sideways well before the steel yields, and a column can bow before it crushes; with both loads together the two effects add. The worksheet finds the moment at which the member would buckle elastically, reduces the plain strength by how close the member is to that moment, and checks the axial force and the bending moments together against the Eurocode 3 interaction rules.

## Scope

Calculates the lateral-torsional buckling, flexural buckling and bending-with-compression interaction of a uniform doubly-symmetric I-section member on fork supports, to EN 1993-1-1. Class 4 sections are outside this sheet.

## Method

- Critical moment (NCCI SN003 form): M_cr = C₁·π²E·I_z/(kL)²·[√((k/k_w)²·I_w/I_z + (kL)²·G·I_t/(π²E·I_z) + (C₂z_g)²) − C₂z_g]; C₁ = 1.88 − 1.40ψ + 0.52ψ² ≤ 2.70 (end moments, k = 1) unless entered
- LTB slenderness: λ̄_LT = √(W_y·f_y/M_cr), W_y = W_pl,y (class 1, 2) or W_el,y (class 3)
- Lateral-torsional buckling, general case (6.3.2.2): Φ_LT = 0.5[1 + α_LT(λ̄_LT − 0.2) + λ̄_LT²]; χ_LT = 1/(Φ_LT + √(Φ_LT² − λ̄_LT²)) ≤ 1
- Lateral-torsional buckling, rolled sections (6.3.2.3): Φ_LT = 0.5[1 + α_LT(λ̄_LT − λ̄_LT,0) + β·λ̄_LT²]; χ_LT = 1/(Φ_LT + √(Φ_LT² − β·λ̄_LT²)) ≤ 1, ≤ 1/λ̄_LT²; optional χ_LT,mod = χ_LT/f ≤ 1, ≤ 1/λ̄_LT² (eq. 6.58), f = 1 − 0.5(1 − k_c)[1 − 2(λ̄_LT − 0.8)²] ≤ 1
- Buckling resistance in bending: M_b,Rd = χ_LT·W_y·f_y/γ_M1
- Flexural buckling: N_cr = π²EI/L_cr², λ̄ = √(A·f_y/N_cr), Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²], χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1, N_b,Rd = χ·A·f_y/γ_M1; the curve of each axis from Table 6.2 (section type, h/b, t_f; f_y ≤ 420 MPa) or chosen
- Interaction (6.3.3(4)): N_Ed/(χ_y·N_Rk/γ_M1) + k_yy·M_y,Ed/(χ_LT·M_y,Rk/γ_M1) + k_yz·M_z,Ed/(M_z,Rk/γ_M1) ≤ 1; N_Ed/(χ_z·N_Rk/γ_M1) + k_zy·M_y,Ed/(χ_LT·M_y,Rk/γ_M1) + k_zz·M_z,Ed/(M_z,Rk/γ_M1) ≤ 1
- Factors k_ij: Annex B from the slenderness, N_Ed/(χ·N_Rk/γ_M1) and C_m (C_m = 0.6 + 0.4ψ ≥ 0.4 for a linear diagram, or entered); Annex A from the μ, w, n_pl, a_LT and C_ij expressions (class 1, 2: plastic column of Table A.1; class 3: elastic column, without the 0.6√(w_i/w_j) factors of k_yz, k_zy)

Formulas in own notation (see the template method text). `M_cr` by the general (NCCI SN003) expression with `C_1`, `C_2`, `k`, `k_w`, `z_g` (for a doubly-symmetric section; `z_g` positive for a load on the compression-flange side of the shear centre, which destabilises; `z_g` negative raises `M_cr`). `C_1 = 1.88 − 1.40ψ + 0.52ψ² ≤ 2.70` for end moments (derived curve fit, labelled) unless `C_1` is entered; for ψ < 0.25 the fit can exceed the exact elastic C₁ by up to about 10 % (long members, double curvature) and the sheet warns (`c1-psi`: check with an elastic buckling program and enter C₁); no table of `C_1` or `C_2` is shipped. `λ̄_LT = √(W_y f_y/M_cr)`, `W_y = W_pl,y` (class 1–2) or `W_el,y` (class 3). `χ_LT` by 6.3.2.2 (`Φ_LT = 0.5[1 + α_LT(λ̄_LT − 0.2) + λ̄_LT²]`, `χ_LT = 1/(Φ_LT + √(Φ_LT² − λ̄_LT²)) ≤ 1`) or 6.3.2.3 (`λ̄_LT,0`, `β`, `χ_LT ≤ 1/λ̄_LT²`); curve `auto` selects from the section type and h/b (Table 6.4 for 6.3.2.2, Table 6.5 for 6.3.2.3) or the curve is chosen. Optional `f` modification `χ_LT,mod = χ_LT/f`, not above 1 and not above `1/λ̄_LT²` (eq. 6.58), `f = 1 − 0.5(1 − k_c)[1 − 2(λ̄_LT − 0.8)²] ≤ 1` (6.3.2.3 only), `k_c` entered or `1/(1.33 − 0.33ψ)` for a linear diagram (**with `C_1` entered the sheet blocks until `k_c` is entered**, unless the diagram is given as end moments). `M_b,Rd = χ_LT W_y f_y/γ_M1`. Flexural buckling: `N_cr = π²EI/L_cr²`, `λ̄`, `χ` by the curve of each axis (`auto`, the default: Table 6.2 for S 235 to S 420 from the section type, `h/b` and the flange thickness `t_f` — rolled I, `h/b` > 1.2: a, b up to 40 mm and b, c up to 100 mm; `h/b` ≤ 1.2: b, c up to 100 mm and d, d beyond; welded I: b, c up to 40 mm and c, d above — or chosen by hand; for `f_y` above 420 MPa and the one combination without a row the curve must be chosen), `N_b,Rd = χ A f_y/γ_M1`. Interaction 6.61 and 6.62 with `k_yy, k_yz, k_zy, k_zz` from **Annex B** (Table B.1, or Table B.2 when the member is susceptible to torsional deformation; a Table B.1 choice for a member that buckles laterally (χ_LT < 1) with no recorded basis is checked on Table B.2, 1.2.0; plastic for class 1–2, elastic for class 3; `C_m = 0.6 + 0.4ψ ≥ 0.4` for a linear diagram or entered — with `C_1` entered `C_my` must be entered, unless the diagram is given as end moments) or from **Annex A** (Table A.1 plastic column for class 1–2, elastic column for class 3 — without the 0.6√(w_i/w_j) factors of `k_yz` and `k_zy`; μ, w, n_pl, a_LT, `C_ij`, `C_my`, `C_mz`, `C_mLT`; `N_cr,T` with `l_T = k_w L`; N must stay below the elastic buckling forces; the LTB terms `b_LT … e_LT` are zero when `λ̄_0 ≤ 0.2√C_1⁴√((1 − N/N_cr,z)(1 − N/N_cr,T))`). The Annex A and Annex B expressions are implemented from the published formulae; **their coefficients are to be checked against the owner's copy of the standard** (the Annex A sheet carries a warning to that effect).

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram (ADR-0158, DI-08). It is not to scale and shows inputs only, never a result. It draws the member on fork supports under the axial force and its end moments. Below it are the span and both buckling lengths; beside it is the I-section. One variant per `moment_mode`. It binds `L`, `N_Ed`, `M_y_A`, `M_y_B`, `z_g`, `M_z_Ed`, `L_cr_y`, `L_cr_z`, `t_f`, `section_class`, `b`, `h`, `f_y`, `E`, `G`, `gamma_M1`, `moment_mode`, `M_y_Ed` and `psi_y`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `edition` | EN 1993-1-1 edition (the 2022 choice changes the printed label only) | — | 2005+A1:2014 | 2005+A1:2014, 2022 | Setup | — |
| `ltb_method` | Method for the lateral-torsional buckling reduction factor | — | 6.3.2.3 (rolled and welded sections) | 6.3.2.3 (rolled and welded sections), 6.3.2.2 (general case) | Setup | — |
| `annex` | Annex for the interaction factors | — | Annex B (method 2) | Annex B (method 2), Annex A (method 1) | Setup | — |
| `torsion` | Member behaviour for Annex B | — | susceptible to torsional deformation (Table B.2) | susceptible to torsional deformation (Table B.2), not susceptible (Table B.1) | Setup | — |
| `section_class` | Cross-section class (class 4 is outside this sheet) | — | 1 | 1, 2, 3, 4 | Setup | — |
| `section_type` | Section type, which selects the buckling curve of Table 6.4 or Table 6.5 | — | rolled I | rolled I, welded I | Setup | — |
| `moment_mode` | Moment diagram given as | — | ψ and M_y,Ed | ψ and M_y,Ed, end moments | Setup | — |
| `ltb_curve` | Buckling curve for lateral-torsional buckling | — | auto (Table 6.4 / 6.5) | auto (Table 6.4 / 6.5), a, b, c, d | Setup | — |
| `curve_y` | Buckling curve about y-y (auto = Table 6.2 for f_y up to 420 MPa) | — | auto (Table 6.2) | auto (Table 6.2), a0, a, b, c, d | Setup | — |
| `curve_z` | Buckling curve about z-z (auto = Table 6.2 for f_y up to 420 MPa) | — | auto (Table 6.2) | auto (Table 6.2), a0, a, b, c, d | Setup | — |
| `use_f` | Apply the modification factor f of cl. 6.3.2.3(2) to the reduction factor | — | 0 | — | Setup | — |
| `h` | Overall section depth | mm | 467.2 | 100 to 1500 | Geometry | — |
| `b` | Flange width | mm | 192.8 | 50 to 600 | Geometry | — |
| `t_f` | Flange thickness, which selects the flexural buckling curves of Table 6.2 | mm | 19.6 | 5 to 60 | Geometry | — |
| `A` | Cross-section area | cm² | 125.27 | 10 to 1000 | Geometry | — |
| `I_y` | Second moment of area about y-y (major axis) | cm⁴ | 45728 | 500 to 1000000 | Geometry | — |
| `I_z` | Second moment of area about z-z (minor axis) | cm⁴ | 2347 | 50 to 100000 | Geometry | — |
| `I_t` | Torsion constant | cm⁴ | 121.29 | 1 to 10000 | Geometry | — |
| `I_w` | Warping constant | cm⁶ | 1175500 | 1000 to 1000000000 | Geometry | — |
| `W_pl_y` | Plastic section modulus about y-y | cm³ | 2232.5 | 10 to 20000 | Geometry | — |
| `W_el_y` | Elastic section modulus about y-y | cm³ | 1957.5 | 10 to 20000 | Geometry | — |
| `W_pl_z` | Plastic section modulus about z-z | cm³ | 378.9 | 5 to 10000 | Geometry | — |
| `W_el_z` | Elastic section modulus about z-z | cm³ | 243.47 | 5 to 10000 | Geometry | — |
| `L` | Length between lateral restraints (fork supports) | m | 6 | 0.5 to 50 | Geometry | — |
| `f_y` | Yield strength for the thickness | MPa | 355 | 235 to 420 | Material | — |
| `E` | Young's modulus | GPa | 210 | 200 to 215 | Material | — |
| `G` | Shear modulus | GPa | 81 | 75 to 85 | Material | — |
| `N_Ed` | Design axial force, tension positive and compression negative (a positive value is outside the range) | kN | -500 | -20000 to 0 | Loads | — |
| `M_z_Ed` | Greatest design moment about z-z | kN·m | 0 | 0 to 10000 | Loads | — |
| `psi_z` | Ratio of the end moments about z-z | — | 1 | -0.9 to 1 | Loads | — |
| `M_y_Ed` | Greatest design moment about y-y, used in the ψ mode (optional; blank = the sheet is blocked in the ψ mode) | kN·m | 300 | 0 to 10000 | Loads | — |
| `psi_y` | Ratio of the end moments about y-y, +1 for a uniform moment (optional; blank = 1, the conservative diagram) | — | 1 | -0.9 to 1 | Loads | — |
| `M_y_A` | Moment at end A about y-y as an ordinate of the bending-moment diagram, with the same sign at both ends for single curvature, unlike the end actions of a stiffness analysis (optional; blank = the sheet is blocked in the end-moment mode) | kN·m | blank | -10000 to 10000 | Loads | — |
| `M_y_B` | Moment at end B about y-y, with the same convention as end A: an ordinate of the bending-moment diagram, the same sign as A for single curvature (optional; blank = the sheet is blocked in the end-moment mode) | kN·m | blank | -10000 to 10000 | Loads | — |
| `k` | Effective length factor for end rotation about the minor axis | — | 1 | 0.7 to 1 | Conditions | — |
| `k_w` | Effective length factor for warping | — | 1 | 0.7 to 1 | Conditions | — |
| `z_g` | Load height above the shear centre, positive towards the compression flange (destabilising) | mm | 0 | -500 to 500 | Conditions | — |
| `L_cr_y` | Buckling length about y-y | m | 6 | 0.5 to 50 | Conditions | — |
| `L_cr_z` | Buckling length about z-z | m | 6 | 0.5 to 50 | Conditions | — |
| `gamma_M1` | Partial factor for member buckling | — | 1 | 1 to 1.1 | Factors | — |
| `alpha_a0` | Imperfection factor for curve a0 (recommended 0.13) | — | 0.13 | 0.1 to 0.8 | Factors | — |
| `alpha_a` | Imperfection factor for curve a (recommended 0.21) | — | 0.21 | 0.1 to 0.8 | Factors | — |
| `alpha_b` | Imperfection factor for curve b (recommended 0.34) | — | 0.34 | 0.1 to 0.8 | Factors | — |
| `alpha_c` | Imperfection factor for curve c (recommended 0.49) | — | 0.49 | 0.1 to 0.8 | Factors | — |
| `alpha_d` | Imperfection factor for curve d (recommended 0.76) | — | 0.76 | 0.1 to 0.8 | Factors | — |
| `lambda_LT0` | Plateau slenderness for cl. 6.3.2.3 (recommended 0.4) | — | 0.4 | 0.2 to 0.4 | Factors | — |
| `beta_LT` | Slenderness correction factor for cl. 6.3.2.3 (recommended 0.75) | — | 0.75 | 0.6 to 1 | Factors | — |
| `C_2` | Moment diagram factor C₂ (0 for end moments; enter it for a transverse load) | — | 0 | 0 to 1 | Factors | — |
| `C_1` | Moment diagram factor C₁ (optional; blank = derived from ψ, valid for end moments with k = 1) | — | blank | 1 to 3 | Factors | — |
| `k_c` | Correction factor for the f modification (optional; blank = 1/(1.33 − 0.33ψ) for a linear diagram) | — | blank | 0.6 to 1 | Factors | — |
| `C_m_y` | Equivalent uniform moment factor about y-y in Annex B (optional; blank = 0.6 + 0.4ψ_y, at least 0.4) | — | blank | 0.5 to 1 | Factors | — |
| `C_m_z` | Equivalent uniform moment factor about z-z in Annex B (optional; blank = 0.6 + 0.4ψ_z, at least 0.4) | — | blank | 0.5 to 1 | Factors | — |
| `C_m_LT` | Equivalent uniform moment factor for lateral-torsional buckling in Annex B (optional; blank = the value of C_my) | — | blank | 0.5 to 1 | Factors | — |

## Assumptions

1. The member is uniform, doubly symmetric and on fork supports, free to warp and to rotate about the minor axis. Basis: Mono-symmetric, tapered and cantilever members and continuous restraint are not modelled.
2. A k or k_w below 1 models partial restraint of the end rotation or of the warping. Basis: The factors k and k_w are entered, not derived.
3. The cross-section class is the class the user states, 1, 2 or 3. Basis: Effective properties of class 4 sections are not derived.
4. The factor C₁ from the ψ formula applies to end-moment diagrams with k = 1. Basis: For other diagrams the user enters C₁, C₂, k_c and C_my from the source in use.
5. No table of C₁ or C₂ values is shipped, so the user takes them from the source in use. Basis: The factors are stored as reference records.
6. The imperfection factors, β and λ̄_LT,0 default to the recommended values, which the National Annex may change. Basis: They are a factor set the user can verify.
7. The 2022 edition choice changes the printed label only, and the arithmetic stays that of the 2005+A1:2014 edition. Basis: The differences between the editions cannot be stated with certainty, and the sheet warns.
8. The flexural buckling curves chosen automatically are those of Table 6.2 for S 235 to S 420. Basis: For a higher f_y or a combination without a row the user chooses the curve.
9. The Annex A and Annex B expressions are implemented as published. Basis: The user checks their coefficients against the owner's copy of the standard.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `edition_used` | EN 1993-1-1 edition | — |
| `psi_y` | ψ of the M_y diagram | — |
| `M_y_Ed` | Greatest design moment about y-y | kN·m |
| `C_1` | C₁ | — |
| `Mcr` | M_cr | kN·m |
| `lambda_LT` | λ̄_LT | — |
| `chi_LT` | χ_LT | — |
| `Mb_Rd` | M_b,Rd | kN·m |
| `N_Rk` | Characteristic axial resistance | kN |
| `N_cr_y` | N_cr,y | kN |
| `N_cr_z` | N_cr,z | kN |
| `lambda_y` | λ̄_y | — |
| `lambda_z` | λ̄_z | — |
| `chi_y` | χ_y | — |
| `chi_z` | χ_z | — |
| `N_b_y_Rd` | N_b,y,Rd | kN |
| `N_b_z_Rd` | N_b,z,Rd | kN |
| `N_b_Rd` | N_b,Rd | kN |
| `k_yy` | Interaction factor for major-axis bending in equation 6.61 | — |
| `k_yz` | Interaction factor for minor-axis bending in equation 6.61 | — |
| `k_zy` | Interaction factor for major-axis bending in equation 6.62 | — |
| `k_zz` | Interaction factor for minor-axis bending in equation 6.62 | — |
| `u_LTB` | LTB utilisation | — |
| `u_buckling` | Buckling utilisation | — |
| `u_61` | Equation 6.61 | — |
| `u_62` | Equation 6.62 | — |
| `u_interaction` | Interaction utilisation | — |
| `u_member` | Member utilisation | — |
| `alpha_LT` | α_LT | — |
| `alpha_y` | α_y | — |
| `alpha_z` | α_z | — |
| `Phi_LT` | Φ_LT | — |
| `chi_LT_0` | χ_LT before the f modification | — |
| `W_y` | Section modulus for bending about y-y | cm³ |
| `M_y_Rk` | M_y,Rk | kN·m |
| `M_z_Rk` | M_z,Rk | kN·m |
| `C_my` | Equivalent uniform moment factor about y-y | — |
| `C_mz` | Equivalent uniform moment factor about z-z | — |
| `C_mLT` | Equivalent uniform moment factor for lateral-torsional buckling | — |
| `u_N_y` | Axial term, y | — |
| `u_N_z` | Axial term, z | — |
| `u_M_y` | Bending term, y | — |
| `u_M_z` | Bending term, z | — |
| `governing` | Governing check | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `ltb.resistance_moment` | `Mb_Rd` | Design buckling resistance moment | kN·m | — |
| `ltb.moment_critical` | `Mcr` | Elastic critical moment for lateral-torsional buckling | kN·m | — |
| `ltb.reduction_ltb` | `chi_LT` | Lateral-torsional reduction factor used | — | — |
| `ltb.slenderness_ltb` | `lambda_LT` | Lateral-torsional non-dimensional slenderness | — | — |
| `ltb.resistance_buckling` | `N_b_Rd` | Flexural buckling resistance, the smaller of the two axes | kN | — |
| `ltb.utilisation_ltb` | `u_LTB` | Lateral-torsional buckling utilisation | — | — |
| `ltb.utilisation_interaction` | `u_interaction` | Greatest utilisation of the two interaction equations 6.61 and 6.62 | — | — |
| `ltb.utilisation_member` | `u_member` | Governing utilisation of the member: the greatest of buckling, LTB and the interaction ratios | — | — |

## Checks

Checks the sheet makes:

- Bending moment against lateral-torsional buckling resistance
- Axial force against flexural buckling resistance
- Interaction equation 6.61 against its limit
- Interaction equation 6.62 against its limit
- Bending interaction without compression, equation 6.61, against its limit
- Bending interaction without compression, equation 6.62, against its limit

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `class-4` | Block | Conservative | Class 4 cross-sections need effective properties: outside this sheet. |
| `section` | Block | Conservative | The plastic moduli cannot be below the elastic moduli. |
| `end-moments` | Block | Conservative | End-moment mode needs both end moments. |
| `n-cr` | Block | Conservative | The design compression must stay below the elastic buckling forces (Annex A). |
| `edition` | Warning | Conservative | The 2022 edition choice changes the label only. |
| `moment` | Block | Unconservative | The ψ mode needs the greatest moment M_y,Ed: a blank one is refused, since the template default of 300 kN·m is not the member's moment (give the diagram as end moments instead). |
| `k_c` | Block | Conservative | With C₁ entered the f modification needs k_c entered (unless the diagram is given as end moments). |
| `C_m_y` | Block | Conservative | With C₁ entered Annex B needs C_my entered (unless the diagram is given as end moments). |
| `C_m_LT` | Block | Conservative | Table B.2 divides by C_mLT − 0.25: C_mLT must exceed 0.25. |
| `curve` | Block | Conservative | The automatic flexural buckling curve is not defined for this section (f_y above 420 MPa, or a Table 6.2 combination without a row): choose the curve. |
| `engine` | Block | Conservative | The elastic critical moment could not be evaluated for these inputs. |
| `c1-psi` | Warning | Unconservative | C₁ from the ψ formula applies to end moments with k = 1 and can exceed the exact elastic value by up to about 10 % when ψ < 0.25; Annex A takes C_my,0 from ψ. |
| `table-b1` | Warning | Unconservative | Table B.1 is chosen for an open I-section whose χ_LT is below 1, which is susceptible to torsional deformation unless it is restrained against twist: the check uses Table B.2 unless a basis for B.1 is recorded. |
| `zg-c2` | Warning | Conservative | z_g has no effect while C₂ = 0. |
| `double-curvature` | Warning | Conservative | End moments of opposite sign are taken as double curvature (ψ < 0, C₁ up to its cap): bending-moment-diagram ordinates, not stiffness-analysis end actions. |
| `use-f` | Warning | Conservative | The f modification belongs to 6.3.2.3 and is not applied with 6.3.2.2. |
| `annex-a` | Warning | Conservative | Annex A coefficients are to be checked against the standard. |
| `slender` | Warning | Conservative | Non-dimensional slenderness above 2.0. |

## Limitations

The sheet covers uniform, doubly symmetric members on fork supports. It does not cover mono-symmetric or tapered members, cantilevers, intermediate restraint or class 4 effective properties.

The sheet makes no shear check and no cross-section check (cl. 6.2.9). The member check sheet makes those checks.

The sheet takes one moment diagram about y-y. The diagram is linear, or you describe it with `C_1`, `k_c` and `C_m`.

A `k` or `k_w` below 1 models partial end restraint. The ψ formula for `C_1` applies to end moments with `k` = 1.

The `edition` input offers 2005+A1:2014 and 2022. The 2022 choice changes the printed label only, and the sheet warns about it. The arithmetic stays that of 2005+A1:2014, because the differences between the editions cannot be stated with certainty.

Table B.1 applies to a member that is not susceptible to torsional deformation. An open I-section with `chi_LT` below 1 is susceptible unless it is restrained against twist. For such a member without a recorded basis, the sheet uses Table B.2 and warns (rule `table-b1`).

Four SCI P364 worked examples (3, 4, 11 and 12, UK NA) and one STAAD.Pro verification (UKB 356×171×51 S275, 5.7 m, M_b,Rd about 103.5 kN·m) are not pinned. Their inputs and answers cannot be stated from memory, so the owner enters them and compares the results with their own copies. LTBeam or the SCI/BCSA M_cr tool is the external oracle for M_cr.

### Changes by version

**1.3.1.** Declares the unit change of 1.3.0 (ADR-0177 review): `I_w` was in dm⁶ before 1.3.0, so a bare stored number from an earlier version is read in dm⁶, never cm⁶. No calculated value changes.

**1.3.0 (C-2 consistency).** Every check shows its demand and capacity as values with units: the bending moment against its resistance, the compression force against its resistance, and each interaction sum against the limit 1 (new steps `N_abs` and `u_limit`). The inputs are grouped Setup, Geometry, Material, Loads, Conditions and Factors, and optional inputs come last in their group. The warping constant `I_w` is entered in cm⁶ instead of dm⁶, the same unit as the section properties sheet, and the value does not change. The eight ports have public names `ltb.*`. Every numeric input has a usual range inside its hard range, and no hard range changes. For the quantities defined on a closed interval (`k`, `k_w`, `psi_y`, `psi_z`, `k_c` and `C_m_*`) the usual range is narrower at one end. The messages, descriptions, assumptions and references follow the sheet standard, and the double-curvature warning now names `M_y_B`. The record kind titles and the reference to NCCI SN003 keep their form, because the first would need a data migration and the second has no edition number. No calculated value changes.

**Worked example.** `docs/worked-examples/structural/struct.ltb-interaction/ub-457x191x98-beam-column/`: UB 457×191×98 S355, 5 m between restraints, N = 400 kN, end moments 340 and 170 kN·m (Unconfirmed: Annex B coefficients), equation 6.62 governs at 0.93.

**Changes in 1.2.0 (C-2 fix, F2).** (1) `moment`: in the ψ mode a blank greatest moment `M_y_Ed` now **blocks** ("M_y_Ed: enter the greatest design moment about y-y, or give the moment diagram as end moments."); it ran on the template default of 300 kN·m, a moment nobody entered, and passed members whose real moment was higher. A blank `psi_y` is still the uniform moment, ψ = 1, the conservative diagram, and is listed as a default used. (2) `table-b1`: Table B.1 is for a member not susceptible to torsional deformation, and an open I-section that buckles laterally is susceptible unless it is restrained against twist; Table B.1 then gives k_zy = 0.6 k_yy, far below Table B.2's (u_62 1.008 against 1.248, 19 % unconservative). A B.1 choice with **no recorded basis** is therefore checked on Table B.2 (the warning says so; the result carries Table B.2); one for which the person recorded a basis (an assumption, a specification, a drawing) is theirs and is checked on Table B.1 with the warning kept. The known-answer case `ka-annex-b-class3-b1` records its basis (Table B.1 as the standard states it) and keeps its expected values. A C_mLT at or below 0.25 with a replaced B.1 blocks (Table B.2 divides by C_mLT − 0.25).

## References

- BS EN 1993-1-1:2005+A1:2014, cl. 6.3.1.2, 6.3.1.3: Buckling curves; slenderness for flexural buckling.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.3.2.2: Lateral-torsional buckling curves: general case.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.3.2.3: Lateral-torsional buckling curves for rolled sections and equivalent welded sections.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.3.3: Uniform members in bending and axial compression.
- BS EN 1993-1-1:2005+A1:2014, Annex A, Annex B: Interaction factors k_ij: methods 1 and 2.
- NCCI SN003, Mcr expression: Elastic critical moment of a doubly-symmetric member.
- BS EN 1993-1-1:2022, cl. 8.3: Stability of members (label only, see the edition note).

## Test cases

Analytic, **Confirmed**, recomputed in python (independent reimplementation): `ka-mcr-uniform-6322` (welded I 500×200×16/10 from plate properties — `I_w = I_z,flanges·h_s²/4 = 1249365.3 cm⁶ (1.2493653 dm⁶)`, exact for the plates, the web adding nothing to the warping constant (review M-7: it was 1.2516493, the total `I_z`) — 6 m, uniform moment: M_cr 398.103 kN·m, λ̄_LT 1.367254, χ_LT 0.315851 curve d, M_b,Rd 235.059 kN·m; the classical closed form is also asserted in the behaviour tests), `ka-6323-f-modified` (ψ = 0: C_1 1.88, M_cr 748.434, χ_LT 0.561531, f 0.885586, χ_LT,mod 0.634078, M_b,Rd 471.886), `ka-mcr-k-zg-c2` (k 0.8, C_1 1.35, C_2 0.45, z_g +250: M_cr 440.518; C_my entered), `ka-flexural-buckling` (curves from Table 6.2 for the welded section: χ_y 0.931891, χ_z 0.603545, N_b,Rd 2373.98 kN). **Unconfirmed** (analytic from the Annex B/A expressions as implemented; the coefficient tables are typed from the published expressions): `ka-annex-b-class2` (Table B.2, k_yy 0.848513, k_zz 1.600473, 6.62 = 1.24809), `ka-annex-b-class3-b1` (Table B.1 elastic), `ka-annex-a-class2`, `ka-annex-a-class3` (the elastic column of Table A.1: k_yz 1.395680, k_zy 1.181153; with the plastic 0.6 factors kept — the error of version 1.0.0 — they were 0.8374 and 0.7087). The behaviour tests cover the 1/λ̄_LT² cap on χ_LT,mod (eq. 6.58), the blocks with `C_1` entered, `zg-c2`, the Table 6.2 selection and the declared rule ids.

- `ka-annex-b-class2`: Annex B, Table B.2, class 2: N = 1200 kN with M_y = 200 kN·m (ψ = 0.5) and M_z = 15 kN·m. Source: Analytic from the Annex B expressions as implemented, recomputed in python; the coefficients of Tables B.1 and B.2 are typed from the published expressions and are to be checked against the owner's copy of the standard, EN 1993-1-1:2005+A1:2014.
- `ka-mcr-uniform-6322`: Welded I 500×200×16/10, S355, 6 m, uniform moment: M_cr, λ̄_LT, χ_LT (6.3.2.2, curve d), M_b,Rd. Source: Analytic: the classical closed form M_cr = (π/L)·√(E·I_z·G·I_t)·√(1 + π²E·I_w/(L²·G·I_t)) and the 6.3.2.2 formulae, recomputed in python, EN 1993-1-1:2005+A1:2014.
- `ka-6323-f-modified`: Same section, 6.3.2.3 with the f modification, triangular moment (ψ = 0). Source: Analytic (6.3.2.3 formulae with β = 0.75, λ̄_LT,0 = 0.4; k_c = 1/(1.33 − 0.33ψ)), recomputed in python, EN 1993-1-1:2005+A1:2014.
- `ka-mcr-k-zg-c2`: M_cr with k = 0.8, entered C₁ = 1.35, C₂ = 0.45 and a load 250 mm above the shear centre. Source: Analytic (the general M_cr expression written out term by term), recomputed in python, NCCI SN003 form.
- `ka-flexural-buckling`: Flexural buckling of the same section, curves selected from Table 6.2 (welded, t_f 16 mm: b about y-y, c about z-z): L_cr,y = 6 m, L_cr,z = 3 m, N = 1200 kN. Source: Analytic (6.3.1.2 formulae), recomputed in python, EN 1993-1-1:2005+A1:2014.
- `ka-annex-b-class3-b1`: Annex B, Table B.1, class 3 (elastic moduli): the same actions. Source: Analytic from the Annex B expressions as implemented, recomputed in python; coefficients to be checked against the owner's copy of the standard, EN 1993-1-1:2005+A1:2014.
- `ka-annex-a-class2`: Annex A, class 2: the actions of the Annex B case. Source: Analytic from the Annex A expressions as implemented (an independent python transcription of the same expressions); the coefficients are to be checked against the owner's copy of the standard, EN 1993-1-1:2005+A1:2014.
- `ka-annex-a-class3`: Annex A, class 3 (Table A.1, elastic column): the same actions. Source: Analytic from the Annex A expressions as implemented, the elastic column of Table A.1 (no 0.6√(w_i/w_j) factors in k_yz and k_zy; w = 1 so every C_ij = 1), recomputed in python; the coefficients are to be checked against the owner's copy of the standard, EN 1993-1-1:2005+A1:2014.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Beam-column UB 457×191×98 S355, 5 m between restraints: LTB, flexural buckling and N+M interaction ([sheet](../../worked-examples/structural/struct.ltb-interaction/ub-457x191x98-beam-column/sheet.txt)).

```text
M_y_Ed = max(|340|, |170|) kN·m = 340 kN·m
psi_y = 170 / 340 = 0.5
N_abs = the compression force, positive = 400 kN
C_1 = ψ = 0.5 = 1.31
Mcr = 1.31 × π² × 210000 × 23470000 / (5000)² × [√((1/1)² × 1175500000000/23470000 + (5000)² × 81000 × 1212900 / (π² × 210000 × 23470000) + (0 × 0)²) − 0] = 808.38 kN·m
W_y = plastic = 2232.5 cm³
lambda_LT = √(2232.5 cm³ × 345 MPa / 808.38 kN·m) = 0.9761
alpha_LT = Table 6.5, rolled, h/b = 2.4232 = 0.49
Phi_LT = 0.5 × [1 + 0.49 × (0.9761 − 0.4) + 0.75 × 0.9761²] = 0.99843
chi_LT_0 = 1/(0.99843 + √(0.99843² − 0.75 × 0.9761²)) = 0.6537
k_c = ψ = 0.5 = 0.85837
f = 1 − 0.5 × (1 − 0.85837) × [1 − 2 × (0.9761 − 0.8)²] = 0.93358
chi_LT_mod = 0.6537 / 0.93358, 1/λ̄_LT² = 1.0496 = 0.70022
chi_LT = as above = 0.70022
… 33 more step(s) on the sheet
Governing: PASS  Interaction equation 6.62 against its limit: 0.9305 vs 1  utilisation 0.93  « cl. 6.3.3(4)
```
<!-- hand-arithmetic:end -->
