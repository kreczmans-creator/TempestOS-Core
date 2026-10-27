# Member checks (EN 1993-1-1) (`struct.en1993-member@1.6.0`)

**In plain terms.** EN 1993-1-1 checks a steel member in two stages. First the cross-section: can it carry the tension, compression, bending and shear without yielding. Then the member: a strut buckles sideways before it crushes, so its compression resistance is reduced by a factor that depends on how slender it is and on the buckling curve for its shape. Combined compression and bending use a simplified interaction, and the governing ratio is the highest of all the checks.

## Scope

Calculates the resistance of a class 1, 2 or 3 steel member to tension, compression, buckling, bending, shear and their interaction, to EN 1993-1-1. Tension is positive; class 4 sections and lateral-torsional buckling are outside this sheet.

## Method

- Axial plastic resistance: N_pl,Rd = A f_y/γ_M0.
- Bending resistance, plastic modulus for class 1–2 and elastic for class 3: M_c,Rd = W f_y/γ_M0.
- Shear resistance: V_pl,Rd = A_v f_y/(√3 γ_M0).
- Bending resistance reduced by high shear, when V_Ed > 0.5 V_pl,Rd: ρ = (2V_Ed/V_pl,Rd − 1)². Class 1–2 I-section with h, t_w, t_f entered: M_y,V,Rd = (W_pl,y − ρ A_w²/(4 t_w)) f_y/γ_M0, A_w = (h − 2t_f) t_w, and M_z,c,Rd is not reduced; otherwise the whole resistance × (1 − ρ).
- Elastic critical load and non-dimensional slenderness: N_cr = π²EI/L_cr²; λ̄ = √(A f_y/N_cr).
- Buckling reduction factor and buckling resistance: Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²]; χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1; N_b,Rd = χ A f_y/γ_M1.
- Cross-section check, linear interaction: N_Ed/N_Rd + M_y,Ed/M_y,Rd + M_z,Ed/M_z,Rd ≤ 1.
- Member check, axial force and bending with buckling (6.61, 6.62): N_Ed/N_b,Rd,min + k_yy M_y,Ed/(W_y f_y/γ_M1) + k_zz M_z,Ed/(W_z f_y/γ_M1) ≤ 1, the characteristic bending resistance M_Rk over γ_M1 without the shear reduction.

Formulas in own notation (see the template method text). Imperfection factors by curve (a0 … d) are textbook defaults (Gardner & Nethercot §6.3.1) overridable by `alpha_y` / `alpha_z`; partial factors `gamma_M0`, `gamma_M1` default 1.0 (UK NA) and are inputs. The rolled-I shear-area helper computes `A_v = max(A − 2bt_f + (t_w + 2r)t_f, η h_w t_w)` when `A_v` is blank. The helper dimensions `h`, `b`, `t_w`, `t_f`, `r` carry **no defaults** (a new sheet opens without them, so the shear check blocks until `A_v` or the section's own dimensions are entered); when the helper is used and `2 b t_f + (h − 2 t_f) t_w` differs from `A` by more than 10 % the sheet warns (`helper-area`). When `N_Ed` is compressive, `N_Ed/N_b,Rd` exceeds 0.3, a moment acts about the axis and `k_yy` or `k_zz` is left at 1.0, the sheet warns (`k-default`) and shows the Annex A/B upper bound `C_m(1 + 0.8 n)` with `C_m = 1` and the member interaction it would give; no Annex table is shipped, so `k` is still entered by the user.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `section_class` | Cross-section class | — | 1 | 1, 2, 3, 4 | Setup | — |
| `curve_y` | Buckling curve about y-y | — | b | a0, a, b, c, d | Setup | — |
| `curve_z` | Buckling curve about z-z | — | c | a0, a, b, c, d | Setup | — |
| `ltb` | Compression flange against lateral-torsional buckling (optional; blank = blocks when M_y_Ed is above 0) | — |  | Restrained, Not restrained | Setup | — |
| `A` | Area | cm² | 58.7 | 1 to 100000 | Geometry | — |
| `W_pl_y` | Plastic section modulus about y-y | cm³ | 497 | 0.1 to 10000000 | Geometry | — |
| `W_el_y` | Elastic section modulus about y-y | cm³ | 450 | 0.1 to 10000000 | Geometry | — |
| `W_pl_z` | Plastic section modulus about z-z | cm³ | 231 | 0.1 to 10000000 | Geometry | — |
| `W_el_z` | Elastic section modulus about z-z | cm³ | 152 | 0.1 to 10000000 | Geometry | — |
| `I_y` | Second moment of area about y-y | cm⁴ | 4568 | 0.1 to 1000000000 | Geometry | — |
| `I_z` | Second moment of area about z-z | cm⁴ | 1548 | 0.1 to 1000000000 | Geometry | — |
| `A_v` | Shear area (optional; blank = rolled I-section from h, b, t_w, t_f and r) | cm² | blank | 0.1 to 100000 | Geometry | — |
| `h` | Depth for the shear area (optional; blank = use A_v) | mm | blank | 1 to 5000 | Geometry | — |
| `b` | Width for the shear area (optional; blank = use A_v) | mm | blank | 1 to 5000 | Geometry | — |
| `t_w` | Web thickness for the shear area (optional; blank = use A_v) | mm | blank | 0.1 to 500 | Geometry | — |
| `t_f` | Flange thickness for the shear area (optional; blank = use A_v) | mm | blank | 0.1 to 500 | Geometry | — |
| `r` | Root radius for the shear area (optional; blank = 0) | mm | blank | 0 to 500 | Geometry | — |
| `A_net` | Net area of a tension member (optional; blank = the sheet is blocked when N_Ed is tension) | cm² | blank | 0.1 to 100000 | Geometry | — |
| `f_y` | Yield strength for the thickness | MPa | 355 | 200 to 700 | Material | — |
| `E` | Young's modulus | GPa | 210 | 190 to 220 | Material | — |
| `f_u` | Ultimate tensile strength (optional; blank = the sheet is blocked when N_Ed is tension) | MPa | blank | 300 to 900 | Material | — |
| `N_Ed` | Design axial force, tension positive and compression negative, opposite in sign to the base plate sheet's axial force | kN | -800 | -100000 to 100000 | Loads | — |
| `M_y_Ed` | Design moment about y-y (major axis) | kN·m | 60 | 0 to 100000 | Loads | — |
| `M_z_Ed` | Design moment about z-z (minor axis) | kN·m | 0 | 0 to 100000 | Loads | — |
| `V_Ed` | Design shear force | kN | 40 | 0 to 100000 | Loads | — |
| `L_cr_y` | Buckling length about y-y | m | 4 | 0.1 to 100 | Conditions | — |
| `L_cr_z` | Buckling length about z-z | m | 4 | 0.1 to 100 | Conditions | — |
| `gamma_M0` | Partial factor for cross-section resistance | — | 1 | 1 to 1.25 | Factors | — |
| `gamma_M1` | Partial factor for member buckling | — | 1 | 1 to 1.25 | Factors | — |
| `gamma_M2` | Partial factor for net-section fracture | — | 1.1 | 1 to 1.35 | Factors | — |
| `eta` | Shear-area factor | — | 1 | 1 to 1.2 | Factors | — |
| `k_yy` | Interaction factor for bending about y-y in the member check | — | 1 | 0.5 to 2 | Factors | — |
| `k_zz` | Interaction factor for bending about z-z in the member check | — | 1 | 0.5 to 2 | Factors | — |
| `alpha_y` | Imperfection factor about y-y (optional; blank = from the buckling curve) | — | blank | 0.1 to 0.8 | Factors | — |
| `alpha_z` | Imperfection factor about z-z (optional; blank = from the buckling curve) | — | blank | 0.1 to 0.8 | Factors | — |

## Assumptions

1. The cross-section class is the class the user states, 1, 2 or 3. Basis: Class 4 effective properties are not derived here.
2. The compression flange is restrained against lateral-torsional buckling, as the input ltb says. Basis: Lateral-torsional buckling of a member that is not restrained is checked on the LTB interaction sheet.
3. A k left at 1.0 is replaced by the Annex A or B upper bound C_m(1 + 0.8n). Basis: Applies under compression with a moment; the smaller flexural-buckling reduction is used on the axial term and the factors are not derived.
4. Imperfection factors are the textbook values for the buckling curve unless the user overrides them. Basis: Gardner and Nethercot, Designers' Guide to EN 1993-1-1.
5. Partial factors are 1.0 as in the UK National Annex unless the user overrides them. Basis: UK National Annex to BS EN 1993-1-1.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `A_v` | Shear area | cm² |
| `N_pl_Rd` | Plastic axial resistance | kN |
| `V_pl_Rd` | Plastic shear resistance | kN |
| `rho_shear` | Shear reduction factor ρ | — |
| `M_y_c_Rd` | Bending resistance about y-y | kN·m |
| `M_z_c_Rd` | Bending resistance about z-z | kN·m |
| `M_y_b_Rd` | Characteristic bending resistance about y-y over the partial factor | kN·m |
| `M_z_b_Rd` | Characteristic bending resistance about z-z over the partial factor | kN·m |
| `N_cr_y` | Elastic critical force about y-y | kN |
| `N_cr_z` | Elastic critical force about z-z | kN |
| `lambda_y` | Non-dimensional slenderness about y-y | — |
| `lambda_z` | Non-dimensional slenderness about z-z | — |
| `chi_y` | Buckling reduction factor about y-y | — |
| `chi_z` | Buckling reduction factor about z-z | — |
| `N_b_y_Rd` | Buckling resistance about y-y | kN |
| `N_b_z_Rd` | Buckling resistance about z-z | kN |
| `N_b_Rd` | Governing buckling resistance | kN |
| `u_N` | Axial utilisation | — |
| `u_M_y` | Bending y-y utilisation | — |
| `u_M_z` | Bending z-z utilisation | — |
| `u_V` | Shear utilisation | — |
| `u_section` | Cross-section interaction | — |
| `u_buckling` | Buckling utilisation | — |
| `u_member` | Member interaction | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `member.resistance_axial_plastic` | `N_pl_Rd` | Plastic axial resistance | kN | — |
| `member.resistance_buckling` | `N_b_Rd` | Governing buckling resistance | kN | — |
| `member.resistance_bending_y` | `M_y_c_Rd` | Bending resistance about y-y | kN·m | — |
| `member.resistance_bending_z` | `M_z_c_Rd` | Bending resistance about z-z | kN·m | — |
| `member.resistance_shear` | `V_pl_Rd` | Plastic shear resistance | kN | — |
| `member.reduction_shear` | `rho_shear` | Shear reduction factor | — | `rho` |
| `member.utilisation_section` | `u_section` | Cross-section interaction utilisation | — | — |
| `member.utilisation_buckling` | `u_buckling` | Buckling utilisation | — | — |
| `member.utilisation_member` | `u_member` | Member interaction utilisation | — | — |

## Checks

Checks the sheet makes:

- Compression against plastic resistance
- Bending moment y-y against bending resistance
- Bending moment z-z against bending resistance
- Shear force against plastic shear resistance
- Cross-section interaction against its limit
- Axial force against buckling resistance
- Member interaction against its limit
- Tension against plastic resistance
- Tension against the net-section fracture resistance

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `class-4` | Block | Conservative | Class 4 cross-sections need effective properties: outside this sheet. |
| `shear-area` | Block | Conservative | No shear area and no rolled-I helper dimensions: the shear check cannot run. |
| `helper-area` | Warning | Conservative | The rolled-I helper dimensions give a web-and-flange area that differs from A by more than 10 %: the helper dimensions probably belong to another section. |
| `k-default` | Warning | Unconservative | k_yy or k_zz is left at 1.0 under compression with a moment: Annex A or B give k above 1, so the check uses the upper bound C_m(1 + 0.8n); a k entered as 1.0 with a recorded basis is used as entered. |
| `high-shear` | Warning | Conservative | V_Ed exceeds half the plastic shear resistance: the bending resistance is reduced (6.2.8). |
| `net-section` | Block | Unconservative | A member in tension needs its net area A_net (A where there are no holes) and f_u: the net-section fracture resistance N_u,Rd (6.2.3(2)(b)) is checked, and the sheet is blocked when they are blank. |
| `ltb` | Block | Unconservative | A member bending about y must say whether its compression flange is restrained against lateral-torsional buckling: the sheet has no default for it, and blocks until it is entered. |
| `no-ltb` | Block | Unconservative | A member bending about y whose compression flange is not restrained is outside this sheet: lateral-torsional buckling (6.3.2) is checked on the LTB interaction sheet. |
| `slender` | Warning | Conservative | Non-dimensional slenderness above 2.0: check the member is intended to be this slender. |

## Limitations

Lateral-torsional buckling is not checked here. A member bending about y-y must say whether its compression flange is restrained, and one that is not restrained is checked on the LTB interaction sheet. Class 4 sections, torsion and web buckling are outside this sheet.

`N_Ed` is positive in tension. The base plate sheet takes its `N_Ed` positive in compression, so from the frame sheet take the member force `N_e1` here and the reaction `R1_y` there.

### Changes by version

**1.6.0 (C-2 consistency).** Every check now shows its demand and capacity as values with units (the two interaction checks show their sum against the limit 1, the step `u_limit`). The checks are named "<quantity> against <limit>", and the new step `N_abs` is the magnitude of `N_Ed`. The output `rho` is now `rho_shear` (the old symbol stays as an alias of the port `member.reduction_shear`, because a bare `rho` means a density). The inputs are grouped Setup, Geometry, Material, Loads, Conditions and Factors, and optional inputs come last in their group. The sheet declares nine ports. The warning of the rolled-I helper names `h`, and the scope, assumptions, references and messages follow the sheet standard. No calculated value changes.

**Changes in 1.3.0.** High shear (`V_Ed > 0.5 V_pl,Rd`, cl. 6.2.8): for a class 1–2 I section whose `h`, `t_w` and `t_f` are entered, only the web part of the plastic modulus is reduced, `M_y,V,Rd = (W_pl,y − ρ A_w²/(4 t_w)) f_y/γ_M0` with `A_w = (h − 2 t_f) t_w` (Eq. 6.30), and `M_z,c,Rd` is not reduced (the flanges carry the minor-axis moment); without those dimensions, or for class 3, the whole resistance keeps the conservative `(1 − ρ)` and the warning says so. The member interaction (6.3.3, Eq. 6.61 and 6.62) divides the moments by the characteristic resistance over `γ_M1`, `M_y,b,Rd = W_y f_y/γ_M1` and `M_z,b,Rd = W_z f_y/γ_M1`, without the shear reduction (before, by `M_c,Rd` over `γ_M0`, which is unconservative when `γ_M1 > γ_M0`). A tension case warns that only gross-section yielding is checked: the net-section fracture resistance `N_u,Rd` (6.2.3(2)(b)) is not computed and governs a tie with bolt holes.

**Changes in 1.4.0 (C-2 fix, M1).** Four checks that passed while the sheet said the result was unsafe now stop or fail. `net-section`: a tension member (`N_Ed` > 0) needs the net area `A_net` (enter `A` where there are no holes, never above `A`) and `f_u` (new inputs, with `γ_M2`, default 1.1); without them the sheet is blocked, and with them it shows `N_u,Rd = 0.9 A_net f_u/γ_M2` (6.2.3(2)(b)) and `u_net = N_Ed/N_u,Rd` with the check "Tension against the net-section fracture resistance" (the pinned example in the worked values gives `N_u,Rd` 801.8 kN, `u_net` 0.4989). `no-ltb`: the new choice `ltb` (Restrained, the default, or Not restrained) says what the sheet assumes; a member bending about y with the flange not restrained is blocked and sent to the LTB interaction sheet instead of passing with a warning. `k-default`: a `k_yy` or `k_zz` left at 1.0 under compression with a moment (no threshold since 1.5.0; it was `N_Ed/N_b,Rd` above 0.3) is replaced in the interaction by the Annex A/B upper bound `C_m(1 + 0.8n)` (`C_m` = 1), shown as the outputs `k_yy_eff` / `k_zz_eff`; one entered with a recorded basis (an assumption, a specification, a drawing) is used as entered, and one entered as 1.0 with no basis keeps its warning with the interaction the bound would give. Utilisation, ports and checks are otherwise unchanged; the value of every other run is unchanged.

**Changes in 1.5.0 (C-2 fix, F1 and F7).** (F1) The `k-default` threshold is gone: a `k_yy` or `k_zz` left at 1.0 (not entered, or entered as 1.0 with no recorded basis) under compression with a moment is replaced in the check by the upper bound C_m(1 + 0.8 n) whatever the compression, because the bound is above 1 for any n. python3 on the SHS member of the verification case: N_Ed/N_b,Rd = 0.2973593 gave u_member 0.992195 with k at 1.0 (a pass) and gives 1.1574877 with the bound 1.2378875 (a fail); a second case, u_member 0.9626522 before and 1.1390474 now. A k recorded with a basis is used as entered, as before. (F7) `ltb` has **no default**: it is optional, and with `M_y_Ed` above 0 and `ltb` blank the sheet blocks (rule `ltb`) and asks whether the compression flange is restrained, where the default Restrained had been assumed and passed an unrestrained beam. Restrained and Not restrained read as before (Not restrained still blocks with `no-ltb`); a saved instance with the choice made is unchanged, one that never made it blocks until it does. The worked examples, the known-answer case `ka-ub-bending-shear` and the pack's column record Restrained; the pack's column is a cantilever with a free top, which nothing restrains, so that entry is an assumption of the example and is stated as one (the member sheet cannot check a cantilever's lateral-torsional buckling).

## References

- BS EN 1993-1-1:2005+A1:2014, cl. 6.2.3: Tension.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.2.4: Compression.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.2.5: Bending moment.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.2.6: Shear.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.2.8: Bending and shear.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.2.1(7): Linear summation of utilisations.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.3.1.1, 6.3.1.2: Uniform members in compression: buckling resistance, buckling curves.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.3.3: Uniform members in bending and axial compression.
- Gardner and Nethercot, Designers' Guide to EN 1993-1-1, §6.3.1, 1st ed.: Imperfection factors for the buckling curves.

## Test cases

UC 203×203×46 S355 4 m compression (analytic by the 6.3.1 formulae, Unconfirmed against a published example); UB 305×165×40 S275 bending + shear (analytic); class 3 tie with minor-axis moment (analytic, fails at 1.03).

- `ka-uc-compression`: UC 203×203×46 S355, L_cr = 4 m, axial compression (flexural buckling about z-z). Source: Analytic (EN 1993-1-1 6.3.1 formulae) in the manner of SCI P362 worked examples; properties from the Blue Book.
- `ka-ub-bending-shear`: UB 305×165×40 S275, bending and shear (rolled-I shear area helper). Source: Analytic (EN 1993-1-1 6.2.5, 6.2.6) with Blue Book properties.
- `ka-tension-class3`: Class 3 tie, tension with minor-axis moment (elastic modulus governs). Source: Analytic (6.2.3, 6.2.5 class 3, 6.2.1(7)).

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
rho_shear = 0
M_y_c_Rd = (1 − 0) × 497 cm³ × 355 MPa / 1 = 176.44 kN·m
M_z_c_Rd = (1 − 0) × 231 cm³ × 355 MPa / 1 = 82.005 kN·m
u_M_y = 40 kN·m / 176.44 kN·m = 0.22671
u_M_z = 0 kN·m / 82.005 kN·m = 0
… 21 more step(s) on the sheet
Governing: FAIL  Member interaction against its limit: 1.0436 vs 1  utilisation 1.05  « cl. 6.3.3
```
<!-- hand-arithmetic:end -->
