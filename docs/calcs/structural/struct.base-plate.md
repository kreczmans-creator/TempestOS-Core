# Column base plate (EN 1993-1-8) (`struct.base-plate@1.7.0`)

**In plain terms.** A column stands on a concrete foundation through a steel base plate, which spreads the column's load over the concrete and, when the column is bent, is pulled up on one side by holding-down bolts. The worksheet works out how much the grout and concrete can bear, how much tension the bolts take under axial load and moment, how thick the plate must be to carry that, and whether friction and the bolts resist the shear.

## Scope

Calculates the bearing, bolt tension, T-stub and shear utilisations of a base plate under an I/H, RHS or CHS column on grout, and the plate thickness that satisfies them. The anchorage is designed outside this sheet.

**Independent check demanded** (DA-12): the author cannot check it. Where no second person is available the preparer may check it alone: tick **Self-check (no second person available)** and write why you stand by it (20 to 500 characters). The statement is kept on the calculation, printed on the issued sheet as `Self-checked by the preparer: …` and written to the audit trail (DC-21).

## Method

- Bearing (cl. 6.2.5): f_cd = α_cc·f_ck/γ_c; f_jd = β_j·α·f_cd, β_j = 2/3; c = t_p·√[f_y/(3·f_jd·γ_M0)]
- Axial (cl. 6.2.8.2): N_j,Rd = f_jd·A_eff, A_eff the bearing strips of width c round the column footprint (full rectangle when they overlap); u_N = N_c,Ed/N_j,Rd
- Moment (cl. 6.2.8.3, Table 6.7): z_C from the section ((h − t_f)/2, (h − t)/2, half-annulus centroid) or entered, x_b the bolt row from the axis, z = z_C + x_b; C_Ed = (M_Ed + N_Ed·x_b)/z and T_Ed = C_Ed − N_Ed when both are ≥ 0; with N_Ed > 0 and T_Ed < 0 both sides are in compression, C = N_Ed/2 ± M_Ed/(2z_C); with N_Ed < 0 and C_Ed < 0 both rows are in tension, T = −N_Ed/2 ± M_Ed/(2x_b); F_t,Ed = T_Ed/n_t at the more loaded row
- Plate T-stub (cl. 6.2.6.11, 6.2.4, Table 6.2): M_pl,Rd = 0.25·l_eff·t_p²·f_y/γ_M0; without prying min(2·M_pl,Rd/m, n_t·F_t,Rd); with prying the modes 4·M_pl,Rd/m, (2·M_pl,Rd + n·ΣF_t,Rd)/(m + n) with n = min(e_min, 1.25m), and n_t·F_t,Rd; l_eff is the total for the n_t bolts
- Compression T-stub (cl. 6.2.6.9, 6.2.6.10): C_Rd = f_jd·A_flange, checked when M_Ed > 0 and the resultant is in compression
- Shear (cl. 6.2.2): F_2,vb,Rd = α_bc·f_ub·A_s/γ_M2, α_bc = 0.44 − 0.0003·f_yb; F_vb,Rd = min(F_1,vb,Rd, F_2,vb,Rd); V_Rd = C_f,d·N_c,Ed + n_b·F_vb,Rd; V_b,Ed = max(V_Ed − C_f,d·N_c,Ed, 0)/n_b
- Required thickness: the smallest t_p in 10–100 mm with u_N, u_C and u_T all ≤ 1, z_C, C_Ed and T_Ed re-evaluated at every trial t_p

- Bearing (cl. 6.2.5): `f_cd = α_cc f_ck/γ_c`; `f_jd = β_j α f_cd` with β_j = 2/3 fixed and shown; `c = t_p √[f_y/(3 f_jd γ_M0)]`. `α` is the user's concentration factor √(A_c1/A_c0) (EN 1992-1-1 cl. 6.7, ≤ 3); a value above 1 warns until `alpha_basis` states the foundation geometry. β_j = 2/3 assumes the grout conditions of cl. 6.2.5 (strength at least 0.2·f_ck, thickness at most 0.2 of the smaller plate dimension); enter `f_ck_g` and `t_g` to have them checked (warning `grout`), otherwise the sheet records the assumption.
- Effective area `A_eff` round the column footprint. I/H: `2(b + 2c)(t_f + 2c) + (h − 2t_f − 2c)(t_w + 2c)`; when `h − 2t_f − 2c < 0` the strips overlap and the full rectangle `(b + 2c)(h + 2c)` is used, and the sheet says so. RHS: `(b + 2c)(h + 2c) − (b − 2t − 2c)(h − 2t − 2c)`, inner terms ≥ 0. CHS: `π[(D/2 + c)² − (D/2 − t − c)²]`, inner term ≥ 0. **Where c would pass the plate edge it is cut back to the edge** (outstand `min(c, (B − b)/2)` across the width, `min(c, (L − h)/2)` along the length, `min(c, (min(B, L) − D)/2)` for CHS), so no area outside the plate is counted; the whole is also capped at B·L. This is never larger than the uncut area clipped to B·L, and equal to it when nothing is cut.
- Axial (cl. 6.2.8.2): `N_j,Rd = f_jd A_eff`; `u_N = max(N_Ed, 0)/N_j,Rd`. `t_req_N` is the smallest t_p in 10–100 mm (bisection) carrying N_c,Ed in bearing alone; shown as 10 mm when carried at the minimum, "> 100 mm" when 100 mm is not enough, "— (no compression)" when N_Ed ≤ 0.
- Moment (cl. 6.2.8.3, Table 6.7, simplified). The compression resultant acts at `z_C` from the column axis (default from the section: I/H `(h − t_f)/2 − (c − c_cut)/2`, RHS `(h − t)/2 − (c − c_cut)/2` (the bearing strip runs from c inside the flange to the outstand outside it, so when that outstand is cut back to the plate edge the strip's centroid moves inward by `(c − c_cut)/2`; with nothing cut these are `(h − t_f)/2` and `(h − t)/2`), CHS the centroid of the half annulus `4(R³ − r³)/(3π(R² − r²))`, R = D/2 + c, r = D/2 − t − c; or `z_C_in` entered); the bolt row is at `x_b` from the axis (outside the column face, inside the plate edge); `z = z_C + x_b`. Three regimes:
  1. Tension at one bolt row, compression under the other flange (`C_Ed = (M_Ed + N_Ed x_b)/z ≥ 0` and `T_Ed = C_Ed − N_Ed ≥ 0`).
  2. Both sides in compression (`N_Ed > 0`, `T_Ed` would be negative): lever arm `2 z_C`, `C = N_Ed/2 ± M_Ed/(2 z_C)`, no bolt tension.
  3. Both bolt rows in tension (`N_Ed < 0`, `C_Ed` would be negative): lever arm `2 x_b`, `T = −N_Ed/2 ± M_Ed/(2 x_b)`, `n_t` bolts in each row.
  `F_t,Ed = T_Ed/n_t` at the more loaded row is the port to PROFIS (`T_Ed_2` and `C_Ed_2` are the other row and flange; the regime is stated on the sheet). With symmetric `z_C = x_b = z/2` regime 1 reduces to `M_Ed/z ∓ N_Ed/2`. The compression T-stub is checked (only when M_Ed > 0 and the resultant is compressive, cl. 6.2.6.9, 6.2.6.10): `C_Rd = f_jd A_flange`, A_flange = `(b + 2c)(t_f + 2c)` (I/H), `(b + 2c)(t + 2c)` (RHS), half of A_eff (CHS), capped at half of A_eff.
- Plate tension T-stub (cl. 6.2.6.11, 6.2.4, Table 6.2): `M_pl,Rd = 0.25 l_eff t_p² f_y/γ_M0` (l_eff is the total for the n_t bolts of the row). **Prying is off by default**: the resistance is `min(2 M_pl,Rd/m, n_t F_t,Rd)`, the conservative case and usual for cast-in bolts. With `prying` on: mode 1 `4 M_pl,Rd/m`, mode 2 `(2 M_pl,Rd + n n_t F_t,Rd)/(m + n)`, `n = min(e_min, 1.25 m)`, mode 3 `n_t F_t,Rd`; the **prying force Q is not included in F_t,Ed and PROFIS must add it** (warning `prying`, and since 1.6.0 a failing check unless `u_profis` is entered). `u_T = T_Ed/T_Rd`, `u_bt = F_t,Ed/F_t,Rd` (cl. 6.2.6.12).
- Shear (cl. 6.2.2): the anchor bolt shear is `F_2,vb,Rd = α_bc f_ub A_s/γ_M2` with `α_bc = 0.44 − 0.0003 f_yb` (f_yb in MPa, valid 235–640 MPa; 0.248 for class 8.8), not the Table 3.4 bolt shear; `F_vb,Rd = min(F_1,vb,Rd, F_2,vb,Rd)` with `F_1,vb,Rd` the bearing resistance from S4; `V_Rd = C_f,d max(N_Ed, 0) + n_b F_vb,Rd`; `u_V = V_Ed/V_Rd`; `V_b,Ed = max(V_Ed − C_f,d N_c,Ed, 0)/n_b` per anchor for PROFIS. A column in net tension has no friction.
- Required thickness `t_req`: the smallest t_p in 10–100 mm with `u_N`, `u_C` and `u_T` all ≤ 1, with the lever arm `z_C` (the strip or half-annulus centroid, which depends on `c` and so on t_p, unless `z_C_in` is entered) and the Table 6.7 forces `C_Ed`, `T_Ed` re-evaluated at every trial thickness, so the reported `t_req` passes its own checks when entered as t_p (scan in 0.05 mm steps, then bisection in the first passing step); "bolts govern" when `T_Ed > n_t F_t,Rd` at the 100 mm end of the range so no thickness helps; "> 100 mm" when 100 mm is not enough.
- `u_max` is the largest utilisation. The anchorage condition "Anchorage designed externally (PROFIS report attached as Evidence)" is made, and passes, **only when `profis_ref` carries Evidence provenance** (a report attached with "Attach external report"; the library refuses an evidence source that does not point at a file attached to the calculation, and the condition reads "external report attached: <file name>" so the issued sheet names the file); typed text, a manual or assumed reference, or nothing gives a warning (rule `anchorage`, "not attached") and no check, so it never counts as a PASS.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `column` | Column section type | — | I/H | I/H, RHS, CHS | Setup | — |
| `prying` | Prying forces develop (short bolts, L_b ≤ L_b*); off is conservative for the plate | — | 0 | — | Setup | — |
| `B` | Plate width | mm | 400 | 150 to 1200 | Geometry | — |
| `L` | Plate length | mm | 400 | 150 to 1200 | Geometry | — |
| `t_p` | Plate thickness | mm | 20 | 10 to 60 | Geometry | — |
| `n_t` | Holding-down bolts in each bolt row (one row each side) | — | 2 | 1 to 6 | Geometry | — |
| `n_b` | Holding-down bolts in total (both sides) | — | 4 | 2 to 12 | Geometry | — |
| `x_b` | Bolt row distance from the column axis (outside the column face, inside the plate edge) | mm | 165 | 80 to 600 | Geometry | — |
| `m` | Bolt to flange-weld toe distance, tension T-stub (required: it follows from x_b, the column and the weld) | mm | — | 10 to 100 | Geometry | — |
| `l_eff` | Effective length of the tension T-stub, total for the n_t bolts (required: from the cl. 6.2.6.5 patterns for the plate) | mm | — | 50 to 800 | Geometry | — |
| `h` | Column depth, along the plate length L (I/H and RHS) (optional) | mm | 260 | 100 to 1000 | Geometry | — |
| `b` | Column width, along the plate width B (I/H and RHS) (optional) | mm | 256 | 100 to 1000 | Geometry | — |
| `t_f` | Flange thickness (I/H) (optional) | mm | 17 | 5 to 50 | Geometry | — |
| `t_w` | Web thickness (I/H) (optional) | mm | 10 | 4 to 40 | Geometry | — |
| `t_wall` | Wall thickness (RHS and CHS) (optional) | mm | 10 | 3 to 40 | Geometry | `t` |
| `D` | Outside diameter (CHS) (optional) | mm | 220 | 60 to 1000 | Geometry | — |
| `z_C_in` | Compression resultant distance from the column axis (optional; blank = from the section) | mm | blank | 30 to 500 | Geometry | — |
| `e_min` | Edge distance of the bolt row, used only with prying (optional; blank = prying needs it) | mm | 35 | 10 to 200 | Geometry | — |
| `t_g` | Grout thickness (optional; blank = the β_j = 2/3 condition is not checked) | mm | blank | 5 to 100 | Geometry | — |
| `f_y` | Plate yield strength | MPa | 275 | 235 to 440 | Material | — |
| `f_ck` | Concrete characteristic cylinder strength | MPa | 25 | 20 to 45 | Material | — |
| `F_t_Rd` | Tension resistance per bolt (from the connection sheet) | kN | 141 | 10 to 1000 | Material | — |
| `f_ub` | Anchor bolt ultimate tensile strength | MPa | 800 | 400 to 900 | Material | — |
| `f_yb` | Anchor bolt yield strength (α_bc is valid for 235–640 MPa) | MPa | 640 | 240 to 640 | Material | — |
| `A_s` | Anchor bolt tensile stress area (from the connection sheet) | mm² | 244.79 | 80 to 1000 | Material | — |
| `F_1_vb_Rd` | Bolt bearing resistance on the plate (from the bearing resistance of the connection sheet) | kN | 198.8 | 10 to 1000 | Material | — |
| `f_ck_g` | Grout characteristic strength (optional; blank = the β_j = 2/3 condition is not checked) | MPa | blank | 20 to 100 | Material | — |
| `N_Ed` | Axial force, compression positive and opposite in sign to the member sheet's axial force (a frame reaction R1_y is compression positive) | kN | 300 | -2000 to 5000 | Loads | — |
| `M_Ed` | Major-axis moment (uniaxial) | kN·m | 100 | 0 to 1000 | Loads | — |
| `V_Ed` | Shear force | kN | 80 | 0 to 1000 | Loads | — |
| `alpha_cc` | Coefficient for long-term effects on the concrete strength | — | 0.85 | 0.8 to 0.9 | Factors | — |
| `gamma_c` | Partial factor for concrete | — | 1.5 | 1.2 to 1.6 | Factors | — |
| `gamma_M0` | Partial factor for the plate | — | 1 | 1 to 1.15 | Factors | — |
| `gamma_M2` | Partial factor for the anchor bolts | — | 1.25 | 1.1 to 1.4 | Factors | — |
| `C_fd` | Friction coefficient, plate to grout | — | 0.2 | 0 to 0.4 | Factors | — |
| `alpha` | Concentration factor √(A_c1/A_c0) of the foundation (1 when the foundation geometry is not known) | — | 1 | 1 to 2.5 | Factors | — |
| `alpha_basis` | Foundation geometry that the concentration factor rests on (state it when the factor is above 1) (optional) | — | blank | — | Factors | — |
| `u_profis` | Governing utilisation reported by the PROFIS anchorage design, from the attached report (optional; blank = the anchorage is not assessed here) | — | blank | 0 to 2 | Anchorage | — |
| `profis_ref` | Hilti PROFIS report reference (the report is filed as Evidence) (optional) | — | blank | — | Anchorage | — |

## Assumptions

1. The axial force is positive in compression, the opposite of the member check sheet. Basis: Sign convention.
2. A link from the frame sheet takes the reaction R1_y, never the member force N_e1. Basis: R1_y is up positive, so compression into the base.
3. Loading is static, with a major-axis moment only. Basis: Scope of this sheet.
4. The plate is unstiffened and rectangular, on grout adequate for β_j = 2/3. Basis: The grout is checked when its strength and thickness are entered.
5. The simplified T-stub model has one compression resultant and one tension resultant per bolt row. Basis: BS EN 1993-1-8:2005 cl. 6.2.8.3.
6. The compression resultant acts at the flange centre, or at the half-annulus centroid of a CHS. Basis: BS EN 1993-1-8:2005 cl. 6.2.8.3.
7. The force regime follows Table 6.7, and the web is not credited in the moment check. Basis: BS EN 1993-1-8:2005 cl. 6.2.8.3.
8. The effective length l_eff is the total for the n_t bolts of a row. Basis: Read from the cl. 6.2.6.5 patterns.
9. One l_eff serves modes 1 and 2, which is conservative for mode 2. Basis: l_eff,1 is not more than l_eff,2.
10. The concrete, pull-out, breakout, edge and interaction failures of the anchors are designed in Hilti PROFIS. Basis: From F_t,Ed and V_b,Ed per anchor; the report is filed as Evidence and referenced.
11. With prying switched on, the prying force Q is not in F_t,Ed and PROFIS must add it. Basis: External anchorage design.
12. The friction resistance counts only the compression, C_f,d·max(N_Ed, 0). Basis: BS EN 1993-1-8:2005 cl. 6.2.2.
13. A column in net tension has no friction resistance. Basis: BS EN 1993-1-8:2005 cl. 6.2.2.
14. Anchor bolts share the shear equally. Basis: BS EN 1993-1-8:2005 cl. 6.2.2.
15. Anchor bolt holes are no larger than the standard clearance. Basis: Oversized or slotted holes are outside this sheet.
16. The bearing outstand c is cut back where it would pass the plate edge. Basis: Conservative against an uncut c.
17. The web and the column interior keep the full outstand c. Basis: Conservative against an uncut c.
18. The CHS compression side is half the annular bearing area, with its centroid as lever arm. Basis: An approximation.
19. The compression T-stub is checked against the flange area alone when M_Ed is above 0. Basis: Conservative limitation.
20. The whole footprint resists at M_Ed = 0, so the compression check jumps just above 0, conservatively. Basis: Limitation.
21. Partial factors and the friction coefficient come from the records in use. Basis: γ_c and α_cc: EN 1992-1-1 NA; γ_M2: EN 1993-1-8 NA; γ_M0: EN 1993-1-1 NA; C_f,d: the grout used.
22. The defaults are the EN recommended and UK values. Basis: A small set of factors only; no table from a standard is shipped.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `f_cd` | Concrete design strength | MPa |
| `beta_j` | Joint coefficient β_j | — |
| `f_jd` | Design bearing strength of the joint | MPa |
| `c` | Bearing outstand round the column footprint | mm |
| `A_eff` | Effective bearing area | mm² |
| `N_j_Rd` | Axial bearing resistance | kN |
| `u_N` | Axial bearing utilisation | — |
| `t_req_N` | Plate thickness required for axial bearing alone | mm |
| `t_req` | Plate thickness required (bearing, compression and tension T-stubs) | mm |
| `z_C` | Distance from the column axis to the compression resultant | mm |
| `z` | Lever arm between the compression resultant and the bolt row | mm |
| `C_Ed` | Compression resultant | kN |
| `C_Ed_2` | Compression resultant, other side | kN |
| `T_Ed` | Tension resultant, more loaded row | kN |
| `T_Ed_2` | Tension resultant, other row | kN |
| `F_t_Ed` | Bolt tension per bolt (to PROFIS) | kN |
| `A_flange` | Compression-side bearing area | mm² |
| `C_Rd` | Compression-side resistance | kN |
| `u_C` | Compression-side utilisation | — |
| `M_pl_Rd` | Plate plastic moment | kN·m |
| `T_Rd_1` | T-stub mode 1-2, no prying | kN |
| `T_Rd_3` | T-stub mode 3 | kN |
| `T_Rd` | Tension T-stub resistance | kN |
| `u_T` | Tension T-stub utilisation | — |
| `u_bt` | Bolt tension utilisation | — |
| `alpha_bc` | Anchor shear factor α_bc | — |
| `F_2_vb_Rd` | Anchor shear resistance F_2,vb,Rd | kN |
| `F_vb_Rd` | Anchor shear resistance per bolt | kN |
| `F_f_Rd` | Friction resistance | kN |
| `V_Rd` | Shear resistance | kN |
| `u_V` | Shear utilisation | — |
| `V_b_Ed` | Shear per anchor bolt (to PROFIS) | kN |
| `u_max` | Governing utilisation | — |
| `regime` | Force regime (Table 6.7) | — |
| `T_mode` | Governing T-stub mode | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `baseplate.force_tension_bolt` | `F_t_Ed` | Holding-down bolt tension per bolt at the more loaded row (→ Hilti PROFIS) | kN | — |
| `baseplate.force_shear_anchor` | `V_b_Ed` | Shear per anchor bolt after friction (→ Hilti PROFIS) | kN | — |
| `baseplate.resistance_axial` | `N_j_Rd` | Axial bearing resistance of the joint | kN | — |
| `baseplate.utilisation_max` | `u_max` | Governing utilisation | — | — |

## Checks

Checks the sheet makes:

- Axial force against concrete bearing resistance
- Compression force against compression flange bearing resistance
- Tension at the bolt row against the plate T-stub resistance
- Bolt tension against the bolt tension resistance
- Shear force against friction plus anchor bolt shear resistance
- Prying force against the bolt tension
- Compression force against compression-side half annulus bearing resistance

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `plate` | Block | Conservative | The column must stand inside the plate. |
| `section` | Block | Conservative | The wall or flange thickness must be less than half the section, and the section's dimensions must be entered. |
| `bolts` | Block | Conservative | The bolt counts must be whole numbers with n_b at least 2·n_t. |
| `x_b` | Block | Conservative | The bolt row must lie outside the column face and inside the plate edge, with m and e_min fitting between. |
| `z_C` | Block | Conservative | An entered compression lever arm z_C must lie inside the plate (below L/2). |
| `z_C_entered` | Warning | Conservative | An entered compression lever arm that differs from the section's by more than the outstand c is probably the lever arm z, not z_C. |
| `alpha_bc` | Block | Conservative | α_bc is defined for f_yb of 235–640 MPa; an anchor bolt strength above 800 MPa has a higher yield strength than the formula covers, so the sheet blocks unless the anchor design's governing utilisation u_profis is entered. |
| `alpha_bc_profis` | Warning | Conservative | An anchor bolt strength above 800 MPa with u_profis entered: u_V is not reliable, the anchor design's utilisation counts. |
| `f_y_thickness` | Warning | Unconservative | One plate yield strength is used at every thickness; EN 10025-2 lowers it for thicker plate. |
| `outstand-cut` | Warning | Conservative | With the bearing outstand cut at the plate edge a thicker plate can raise the compression utilisation. |
| `alpha_basis` | Warning | Unconservative | A concentration factor above 1 needs its foundation geometry stated. |
| `grout` | Warning | Unconservative | β_j = 2/3 needs the grout at least 0.2·f_ck and no thicker than 0.2 of the smaller plate dimension. |
| `prying` | Warning | Unconservative | With prying switched on and no anchor design utilisation u_profis entered, the prying force Q is not in F_t,Ed, so the bolt tension is understated: the prying check fails. |
| `prying-anchor` | Warning | Conservative | Prying is switched on and u_profis is entered: the prying force Q is not in F_t,Ed, and the anchor design must add it to the bolt tension. |
| `anchorage` | Warning | Conservative | The PROFIS report is not attached as Evidence (a typed reference is not enough). |
| `anchorage-result` | Warning | Unconservative | The anchorage (concrete cone, pull-out, edge and shear failure of the anchors) is not assessed on this sheet and u_max excludes it, unless the report's governing utilisation u_profis is entered. |

## Limitations

Static loading; uniaxial (major-axis) moment; unstiffened plate; the simplified T-stub model (one compression resultant per flange and one tension resultant per bolt row; the web gets no credit in the moment check, so the compression check jumps at M_Ed = 0⁺ where the whole footprint resists); one `l_eff` for modes 1 and 2; prying is the user's `prying` switch, not a computed L_b ≤ L_b*, and Q is not included in F_t,Ed; anchor shear assumes standard clearance holes and equal sharing (oversized or slotted holes are outside this sheet); β_j = 2/3 depends on the grout conditions (checked only when entered); the CHS compression side is half the annulus (approximate); **anchorage, pull-out, breakout, edge distances and the tension–shear interaction of the anchors are designed externally in PROFIS**; no shear key, no weld design, no fatigue.

### Changes by version

**1.7.0 (C-2 consistency).** The wall thickness of an RHS or CHS column `t` is now `t_wall` (old symbol kept as an alias; a bare `t` means time). Every check shows demand and capacity with units: the axial force against the bearing resistance, the compression and tension resultants against their resistances, the bolt tension against the bolt resistance, the shear force against the shear resistance, and the PROFIS utilisation against the limit 1 (new step `u_limit`). The inputs are grouped Setup, Geometry, Material, Loads, Factors and Anchorage, and every numeric input has a usual range inside its hard range. The hard range of `f_y`, `f_ck`, `f_ub`, `alpha_cc`, `gamma_c`, `gamma_M0`, `gamma_M2` and `C_fd` is wider than before, so that the usual range (the old limits) sits inside it: a value outside the usual range now warns instead of being refused (the earlier statement that each hard limit equals its stated range no longer holds for these inputs). The four ports have public names `baseplate.*`. The scope, assumptions and messages follow the sheet standard. The record kind `ec3-1-1.national-annex` keeps its id (renaming it needs a migration of stored records).

**Changes in 1.5.0 (struct.base-plate-03, -R1, chain.steel-support-04, from the C-1 verification).** -03: an anchor bolt strength above 800 MPa was only a warning (`f_ub` 1000, `f_yb` 640, `V_Ed` 250 kN: α_bc 0.248, `u_V` 0.983, a pass, where the formula at the bolt's real yield of about 900 MPa gives α_bc 0.17 and `u_V` 1.29); it is now a block unless `u_profis` is entered. -R1: the `f_y_thickness` warning no longer says that `t_req` is above 16 mm when it is below (S6 with `t_p` 25 mm and `t_req` 15.423 mm). chain.steel-support-04: the anchorage check cites "PROFIS report (EN 1992-4)", not EN 1993-1-8 cl. 6.2.6.12 (anchor bolts in tension), which is the steel bolt check `u_bt`.

**Changes in 1.6.0 (C-2 fix, F2).** `prying`: with prying on and no anchor design utilisation `u_profis` entered, the prying force Q is not in F_t,Ed and the bolt tension is understated, and the sheet warned and passed. It now fails the check "Prying force against the bolt tension" (F_t,Ed without Q against F_t,Ed + Q, Q from the anchor design); the warning stays. With `u_profis` entered the anchor design's utilisation is counted and only the warning (`prying-anchor`) stays. It is a failing check and not a block because the known-answer case `ka-ih-moment-prying` (the modes 785.71 / 331.27 / 282 kN) is a shipped case with prying on; its expected values are unchanged.

## References

- BS EN 1993-1-8:2005, cl. 6.2.2: Design resistance of column bases in shear.
- BS EN 1993-1-8:2005, cl. 6.2.4, Table 6.2: Equivalent T-stub in tension.
- BS EN 1993-1-8:2005, cl. 6.2.5: Equivalent T-stub in compression.
- BS EN 1993-1-8:2005, cl. 6.2.6.9: Concrete in compression including grout.
- BS EN 1993-1-8:2005, cl. 6.2.6.10: Base plate in bending under compression.
- BS EN 1993-1-8:2005, cl. 6.2.6.11: Base plate in bending under tension.
- BS EN 1993-1-8:2005, cl. 6.2.6.12: Anchor bolts in tension.
- BS EN 1993-1-8:2005, cl. 6.2.8.2: Column bases subject to axial forces.
- BS EN 1993-1-8:2005, cl. 6.2.8.3, Table 6.7: Column bases subject to axial force and bending moment.
- BS EN 1992-1-1:2004, cl. 3.1.6, 6.7: Design strength of concrete; partial area loading.

## Test cases

All analytic, Confirmed, recomputed independently in python (`model2.py`, `kat2.py`, `kat3.py`). `ka-ih-moment-bolts` (I-section 260×256×17×10 on 400×400×20 S275, C25/30, N 300, M 100, V 80, x_b 165, m 28, no prying: z_C 121.5, z 286.5 mm; C_Ed 521.82, T_Ed 221.82 kN, F_t,Ed 110.91 kN per bolt; C_Rd 601.84 kN; mode 1-2 392.86, mode 3 282 → 282 kN; α_bc 0.248, F_2,vb,Rd 38.853, V_Rd 215.41 kN; V_b,Ed 5 kN; t_req 17.609 mm), `ka-ih-moment-prying` (modes 785.71 / 331.27 / 282 kN), `ka-ih-both-compression` (N 600, M 40: 464.61 and 135.39 kN), `ka-ih-both-tension` (N −300, M 20: rows 210.61 and 89.39 kN, 105.30 kN per bolt), `ka-ih-axial` (f_jd 14.167 MPa, c 50.875 mm, A_eff 98 849.99 mm², N_j,Rd 1400.37 kN, t_req 17.075 mm), `ka-rhs-axial` (A_eff 84 929.55 mm²), `ka-chs-axial` (A_eff 73 724.93 mm²), `ka-chs-moment` (z_C 73.155 mm from the half-annulus centroid; C_Ed 412.85, T_Ed 212.85 kN; t_req 15.587 mm with z_C re-evaluated at each trial thickness), `ka-ih-overlap-clip` (overlap and plate-edge cut-back: A_eff 106 049.64 mm²), `ka-ih-moment-clipped` (300×300 section on a 340 mm long plate, only 20 mm of plate outside the flange: z_C 126.063 mm, C_Ed 513.64 kN, T_Ed 213.64 kN, where the uncut centroid gave 487.77 kN). **Not pinned — owner check:** the SCI P358 column-base worked examples and Wald, Sokol, Steenhuis & Jaspart, "Component method for steel column bases", *Heron* 53(1/2), 2008, are not available to the author; no published inputs or answers are stated here. The α_bc range 235–640 MPa and the clause numbers (6.2.6.9, 6.2.6.10, 6.2.6.12, 6.2.8.2, 6.2.8.3, Table 6.7) are to be checked against the owner's copy of EN 1993-1-8. The worked example `portal-column-base-uc254` is **Unconfirmed** (UC 254×254×89 dimensions, grout datasheet placeholder and the `l_eff` estimate to be checked).

- `ka-ih-moment-bolts`: The 260×256 I-section with N = 300 kN, M = 100 kN·m, V = 80 kN; two bolts a row, bolt rows at 165 mm from the axis, no prying. Source: Analytic (simplified T-stub model, Table 6.7 regime 1; recomputed independently), BS EN 1993-1-8:2005.
- `ka-ih-moment-prying`: The same base with prying switched on and the edge distance e_min = 35 mm. Source: Analytic (EN 1993-1-8 Table 6.2 modes 1 to 3), BS EN 1993-1-8:2005.
- `ka-ih-both-compression`: N = 600 kN, M = 40 kN·m: eccentricity below z_C, both flanges in compression (Table 6.7). Source: Analytic (EN 1993-1-8 Table 6.7, both sides in compression, lever arm 2·z_C), BS EN 1993-1-8:2005.
- `ka-ih-both-tension`: N = −300 kN (tension), M = 20 kN·m: both bolt rows in tension (Table 6.7). Source: Analytic (EN 1993-1-8 Table 6.7, both sides in tension, lever arm 2·x_b), BS EN 1993-1-8:2005.
- `ka-ih-axial`: UC-like I-section 260×256, t_f 17, t_w 10, on a 400×400×20 S275 plate, C25/30, axial 1200 kN. Source: Analytic (EN 1993-1-8 cl. 6.2.5 relations, recomputed independently), BS EN 1993-1-8:2005.
- `ka-rhs-axial`: RHS 200×200×10 on a 350×350×20 S275 plate, C25/30, axial 900 kN. Source: Analytic (EN 1993-1-8 cl. 6.2.5 relations), BS EN 1993-1-8:2005.
- `ka-chs-axial`: CHS 220×10 on a 350×350×20 S275 plate, C25/30, axial 800 kN. Source: Analytic (EN 1993-1-8 cl. 6.2.5 relations), BS EN 1993-1-8:2005.
- `ka-chs-moment`: CHS 220×10 on a 350×350×20 plate with N = 200 kN, M = 60 kN·m, bolt rows at 140 mm: lever arm from the half-annulus centroid. Source: Analytic (EN 1993-1-8 Table 6.7 regime 1 with the half-annulus centroid as z_C), BS EN 1993-1-8:2005.
- `ka-ih-overlap-clip`: Small I-section 150×150 on a 300×400×40 plate: the strips overlap and the outstand across the width is cut back to the plate edge. Source: Analytic (EN 1993-1-8 cl. 6.2.5 relations, outstand cut at the plate edge), BS EN 1993-1-8:2005.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Portal-frame column base: UC 254×254×89 on a 450×400×25 S275 plate, four M24 8.8 bolts ([sheet](../../worked-examples/structural/struct.base-plate/portal-column-base-uc254/sheet.txt)).

```text
f_cd = 0.85 × 30 MPa / 1.5 = 17 MPa
beta_j = grout conditions of cl. 6.2.5 assumed met = 0.66667
f_jd = 0.66667 × 1.5 × 17 MPa = 17 MPa
c = 25 mm × sqrt(265 MPa / (3 × 17 MPa × 1)) = 56.987 mm
A_eff = 2 × 370.27 × 131.27 + 111.73 × 124.27 mm² = 111100 mm²
N_j_Rd = 17 MPa × 111100 mm² = 1888.7 kN
N_c_Ed = N_Ed = 210 kN = 210 kN
u_N = 210 kN / 1888.7 kN = 0.11119
t_req_N = N_c,Ed = 210 kN; carried at the 10 mm minimum = 10 mm
z_C = (260.3 − 17.3)/2 mm = 121.5 mm
z = 121.5 + 185 = 306.5 mm
C_Ed = (95 kN·m + 210 kN × 185 mm)/306.5 mm = 436.7 kN
T_Ed = 436.7 − 210 = 226.7 kN
C_Ed_2 = other side = 0 kN
… 20 more step(s) on the sheet
Governing: PASS  Tension at the bolt row against the plate T-stub resistance: 226.70 kN vs 406 kN  utilisation 0.56  « EN 1993-1-8 cl. 6.2.6.11
```
<!-- hand-arithmetic:end -->
