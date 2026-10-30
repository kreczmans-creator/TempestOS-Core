# Helical springs (`mech.spring@1.6.0`)

**In plain terms.** A coil spring stores energy by twisting its wire, so its stiffness comes from the wire diameter, the coil diameter and the number of turns, and its stress is highest on the inside of the coil. The worksheet takes the spring geometry and working loads, finds the rate and the stress at each working point and at solid length, and checks them against the wire's allowable stress, with a surge check (the coil ringing at its own frequency) for compression, extension and torsion springs.

## Scope

Calculates the rate, working points, stresses, buckling, fatigue and surge of a cold-coiled round-wire helical spring: compression, extension or torsion. The characteristic is linear, with hook stresses for extension and legs for torsion springs.

The wire is up to 20 mm. Compression is the first mode: rate, working points (given as loads or as deflections), shear stress with a correction factor at the two working points and at solid length, solid length (EN 13906-1 form or Shigley form) with the minimum coil clearance at the working length, pitch by end type, buckling, Goodman fatigue and surge frequency. Extension adds the initial tension and the two hook stresses. Torsion gives the rate with the legs, the curvature-corrected bending stress, the wound diameter and the body length. EN 13906-1, -2 and -3 are cited by part and title only: their text, wire-strength tables and permissible-stress diagrams are not reproduced or shipped (Addendum B §B5).

## Method

- Index and rate: `C = D/d`; `k = G d⁴/(8 D³ n_active)` (compression, extension); torsion `k_φ = E d⁴/(64 D n_a)`, `n_a = n_active + (l_leg_1 + l_leg_2)/(3π D)`.
- Shear stress: `τ = k_c·8 F D/(π d³)`, `k_c` by choice: Bergsträsser `(C + 0.5)/(C − 0.75)` (the default; this is the form the spec brief calls the Wahl option, and the one printed as K_B in Shigley), Wahl `(4C − 1)/(4C − 4) + 0.615/C`, or none (uncorrected). The same factor is applied to the stress at solid, which is conservative for the static check. Since 1.3.0 "none" applies to the static stress only: the Goodman fatigue stresses always carry the curvature factor (Bergsträsser when "none" is chosen; `k_c_fat` on the sheet), because the peak stress on the inside of the coil is what a fatigue crack grows from (d 4, D 16, F 100 to 400 N, R_m 1700, S_e 300 MPa: `u_f` 0.634 with either choice; "none" read 0.458 before).
- Solid length by end type, two selectable forms (`solid_method`). **EN 13906-1 (the default):** `n_t = n_active + e_n`, `L_s = d_max (n_t + e_s)` with the maximum wire diameter `d_max` (input `d_max`, blank = the nominal `d`, no tolerance added) and `(e_n, e_s)` = (0, 1) plain, (1, 0) plain and ground, (2, 1.5) squared (closed, unground), (2, 0) squared and ground; plain ends have no row in the standard and keep the geometric form. The minimum sum of the coil clearances `S_a = (0.0015 D²/d + 0.1 d) n` gives the minimum length `L_n = L_s + S_a`; the sheet checks `L₂ ≥ L_n` (utilisation `s₂/(L₀ − L_n)`, check "Minimum coil clearance at L₂") and refuses a free length not above `L_n` (rule `solid`). **Shigley Table 10-1:** `L_s = d (n_t + e_s)` with nominal `d` and `e_s` = 1 for squared, no clearance. The two forms differ for squared unground ends (0.5 d) and by the wire tolerance. Pitch `p_coil = (L₀ − a d)/(n_active + b)` with `(a, b)` = (1, 0), (0, 1), (3, 0), (2, 0). Travel to solid `s_c = L₀ − L_s`, `F_c = k s_c`; the stress at solid and the working deflection against the travel (`u_s = s₂/s_c`) are checked in both forms. The EN relations are written in own notation from the published expressions and are **to be checked against the owner's copy** of EN 13906-1.
- Extension: `F = F₀ + k s`; body length `d (n_active + 1)`; hook bending `σ_A = F [K_A·16 D/(π d³) + 4/(π d²)]`, `K_A = (4C₁² − C₁ − 1)/(4 C₁ (C₁ − 1))`, `C₁ = 2 r₁/d`; side-bend torsion `τ_B = K_B·8 F D/(π d³)`, `K_B = (4C₂ − 1)/(4C₂ − 4)`, `C₂ = 2 r₂/d`.
- Torsion: curvature factor by choice (`tors_factor`): **EN 13906-3 `q = (C + 0.07)/(C − 0.75)` (the default)** or Shigley's `K_i = (4C² − C − 1)/(4C (C − 1))` (about 1 % lower at C = 8); `σ = q·32 M/(π d³)` (or `K_i`); wound to angle φ the wire length is constant, so `D_w = D n_active/(n_active + φ/2π)` and the closed body is `d (n_active + 1 + φ/2π)` long.
- Buckling (compression): `λ_eff = ν L₀/D`; `y_cr = L₀ C₁′ [1 − √(1 − C₂′/λ_eff²)]`, `C₁′ = E/(2(E − G))`, `C₂′ = 2π²(E − G)/(2G + E)`; when `λ_eff² < C₂′` the spring is stable at any deflection. PASS when `s₂ < y_cr`, otherwise WARN (rule `buckling`).
- Fatigue (Goodman, the same relation as `mech.shaft` `mech.shaft`: `1/n_f = a/S_e + m/S_u`; shared function `FatigueRelations.GoodmanUtilisation`, `mech.shaft` itself unchanged): `a`, `m` from the stresses at working points 1 and 2; `S_u = f_su·R_m` in shear (compression, extension body), `R_m` in bending (torsion). Checked as `n_f,req·(1/n_f) ≤ 1`. `S_e` has no default: a blank one blocks the sheet ("S_e: enter …"), so the fatigue check is always made; warning `fatigue-cycles` when `N_c < 10⁴`.
- Wire strength `R_m(d)` (`rm_source`): typed (`Rm`, **no default**; blank blocks with what to enter, rule `wire-strength`) or taken from the curve of a material record, `R_m = A/d^m` with `A` = `Rm_A` (MPa, for `d` in mm) and `m` = `Rm_m` (both `MaterialProperty`, in the record kind `material.properties`, pinned by revision: a new revision makes the sheet stale). The curve value must lie in 50 … 5000 MPa. The diameter range over which the fit holds is the material record's to state and the user's to respect; the sheet does not know it.
- Surge: `f_n = (d/(2π n_active D²))√(G/(2ρ))`, both ends fixed (the same as `½√(k/m)` with the wire mass). With an operating frequency `f_op`, `r_f = f_n/f_op`, WARN (rule `surge`) when `r_f < 13`.

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram (ADR-0158, DI-08). It is not to scale and shows inputs only, never a result. It draws the spring that `mode` selects, with its loads at the two working points. A compression spring sits between its seats; an extension spring has its hooks; a torsion spring has its legs. One variant per `mode`. It binds `n_active`, `d`, `D`, `L0`, `F_1`, `F_2`, `ends`, `corr`, `G`, `Rm`, `S_e`, `n_f_req`, `f_op`, `work`, `mode`, `r_h1`, `r_h2`, `F_0`, `l_leg_1`, `l_leg_2`, `M_2`, `M_1` and `E`.

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `mode` | Kind of spring | choice | Setup |
| `corr` | Stress correction factor k_c for the shear stress | choice | Setup |
| `ends` | End type (compression) | choice | Setup |
| `work` | Working points given as | choice | Setup |
| `rm_source` | Where R_m(d) comes from: typed (no default: the wire certificate or your wire standard) or the curve R_m = A/d^m of a material record | choice | Setup |
| `solid_method` | Solid length and clearance (compression): EN 13906-1 uses the maximum wire diameter, 1.5 d more for squared unground ends, and checks the minimum coil clearance at L₂; Shigley uses the nominal d | choice | Setup |
| `tors_factor` | Curvature factor of the bending stress (torsion): EN 13906-3 q or Shigley K_i | choice | Setup |
| `d` | Wire diameter | mm | Geometry |
| `D` | Mean coil diameter | mm | Geometry |
| `n_active` | Active coils (torsion: coils of the body) | none | Geometry |
| `d_max` | Maximum wire diameter, for the EN 13906-1 solid length (optional; blank = the nominal d, no tolerance added) | mm | Geometry |
| `L0` | Free length (compression) | mm | Geometry |
| `r_h1` | Hook mean radius, bending (extension) | mm | Geometry |
| `r_h2` | Hook side-bend mean radius, torsion (extension) | mm | Geometry |
| `l_leg_1` | Leg length 1 (torsion) | mm | Geometry |
| `l_leg_2` | Leg length 2 (torsion) | mm | Geometry |
| `G` | Shear modulus | GPa | Material |
| `E` | Young's modulus | GPa | Material |
| `rho` | Density | kg/m³ | Material |
| `S_e` | Endurance strength for the stress kind (shear; bending in torsion) at N_c cycles, alternating (no default: from the wire maker or your fatigue reference) | MPa | Material |
| `Rm` | Tensile strength of the wire at this diameter, typed (no default) | MPa | Material |
| `Rm_A` | Curve coefficient A of the material record (R_m = A/d^m, A in MPa for d in mm) | MPa | Material |
| `Rm_m` | Curve exponent m of the material record | none | Material |
| `S_e_b` | Bending endurance strength of the hook (extension) at N_c cycles, alternating (optional; blank = hook bending fatigue is not assessed) | MPa | Material |
| `F_1` | Force at working point 1 (loads) | N | Loads |
| `F_2` | Force at working point 2 (loads) | N | Loads |
| `s_1` | Deflection at working point 1 (deflections) | mm | Loads |
| `s_2` | Deflection at working point 2 (deflections) | mm | Loads |
| `F_0` | Initial tension (extension) | N | Loads |
| `M_1` | Torque at working point 1 (torsion, loads) | N·m | Loads |
| `M_2` | Torque at working point 2 (torsion, loads) | N·m | Loads |
| `phi_1` | Angle at working point 1 (torsion, deflections) | ° | Loads |
| `phi_2` | Angle at working point 2 (torsion, deflections) | ° | Loads |
| `s_work` | Expected working travel s₂ − s₁ (optional; blank = no check). The sheet warns when the spring's own travel differs by more than 1 %. Torsion mode does not use it, and the sheet says so | mm | Loads |
| `guided` | Spring guided by a mandrel or in a bore (compression): the guide restrains it against buckling, so the free-buckling check is not made | yes or no | Conditions |
| `nu_seat` | End-support coefficient ν (0.5 both ends fixed, 0.7 one fixed one pinned, 1 both pinned, 2 one free) | none | Conditions |
| `f_op` | Operating (excitation) frequency | Hz | Conditions |
| `f_tau` | Permissible shear stress as a fraction of R_m (generic textbook default; replace with the licensed EN 13906 value) | none | Factors |
| `f_su` | Ultimate shear strength as a fraction of R_m (Goodman, shear; generic textbook default, replace with the licensed EN 13906 value) | none | Factors |
| `f_sigma` | Permissible bending stress as a fraction of R_m (torsion, hooks; generic textbook default, replace with the licensed EN 13906 value) | none | Factors |
| `n_f_req` | Required Goodman safety factor | none | Requirements |
| `N_c` | Number of cycles the endurance strength belongs to | none | Requirements |

Three inputs were renamed in 1.6.0 and the old symbols still load, so a saved calculation, an inputs file, a CSV file and a pack binding written with them give the same values: `n` is now `n_active`, `l_1` is now `l_leg_1` and `l_2` is now `l_leg_2`. The output `p` is now `p_coil`; no link can name it, because it is not a port.

User inputs with records: `rm_source` and `Rm` = R_m(d) (`spring.wire-strength`, no default), `f_tau`, `f_sigma`, `f_su` (`spring.permissible`; they default to the generic textbook fractions 0.45, 0.75 and 0.67, which the user replaces with the licensed EN 13906 value), `nu_seat` = ν, the end-support coefficient (`spring.seating`; 0.5 both ends fixed and parallel, 0.7 one fixed one pinned, 1 both pinned, 2 one end free), `S_e` (`spring.endurance`, no default), optional `S_e_b` (extension hook bending endurance, no default), `N_c`, `n_f_req`, `f_op`. Inputs a mode does not use are optional; one the chosen mode needs and that is blank blocks with a message naming it (`missing`). **No strength is invented:** `Rm` and `S_e` have no default (a new sheet opens blocked and says what to enter). The remaining defaults on a new sheet are example values; none is a published figure.

**Expected working travel (1.2.0).** Optional input `s_work` (mm, 0.1–1000): the working travel the mechanism expects of the spring (the `lift` of `mech.mechanism` links here). In compression and extension the sheet compares it with its own `s_trav = s₂ − s₁` (an output) and warns `travel-mismatch` when they differ by more than 1 %: the rate the mechanism assumed is then not this spring's. In torsion mode the travel check does not apply (a torsion spring works through an angle): a typed or linked `s_work` is not used and the sheet warns `s-work-unused`. Blank, nothing changes.

## Assumptions

- The rate is constant between the working points, with no progressive or conical spring and no variable pitch.
- The active coils are as entered, and end coils count only in the solid length and the pitch.
- Hook flexibility is not in the rate, and leg flexibility is in the rate only for torsion springs.
- The EN 13906-1 solid length uses the maximum wire diameter `d_max` and adds 1.5 d for an unground squared end.
- The EN 13906-1 form checks the minimum coil clearance `S_a` at the working length `L₂`.
- Plain ends have no EN row and keep the geometric form, and the Shigley form (nominal d) is selectable.
- The default bending stress factor of a torsion spring is the EN 13906-3 `q = (C + 0.07)/(C − 0.75)`.
- Shigley's `K_i = (4C² − C − 1)/(4C(C − 1))` is selectable and 1 % lower at C = 8.
- R_m(d) and the endurance strength are user inputs from the wire standard or certificate, with no default.
- The permissible stress fractions default to generic textbook fractions, which the user replaces with the licensed EN 13906 value. The defaults are 0.45 for `f_tau`, 0.75 for `f_sigma` and 0.67 for `f_su`.
- R_m(d) can come from the curve R_m = A/d^m of a material record, A in MPa and d in mm.
- The stress correction factor `k_c` is applied to the working and the solid stress alike.
- The hook shape beyond its two radii is not assessed in an extension spring.
- Hook fatigue is Goodman, on the bending stress and on the side-bend torsion between the two working points.
- The initial tension of an extension spring is a force the user enters.
- Surge is the first natural frequency of a spring with both ends fixed.
- A spring with one end free has half the surge frequency.

## Outputs

Compression: `C`, `k`, `k_c`, `n_t`, `L_s`, `p_coil`, `F_1`, `F_2`, `s_1`, `s_2`, `L_1`, `L_2`, `s_c`, `F_c`, `tau_1`, `tau_2`, `tau_c`, `u_2`, `u_c`, `u_s`, with the EN form `d_max`, `S_a`, `L_n`, `s_n`, `u_n`; `y_cr`, `u_b`, `buckling` (PASS/FAIL, or "not checked (guided)"), `f_n`, `r_f`, `u_f`, `n_f`; `Rm` in every mode. Extension: the rate and working points, `tau_0`, `tau_1`, `tau_2`, `u_2`, `K_A`, `K_B`, `sigma_A`, `tau_B`, `u_A`, `u_B`, `f_n`, `r_f`, `u_f`, `n_f`. Torsion: `n_a`, `k_phi`, `q` (EN) or `K_i` (Shigley), `phi_1`, `phi_2`, `M_1`, `M_2`, `sigma_1`, `sigma_2`, `u_2`, `D_w`, `L_w`, `u_f`, `n_f`.

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `spring.rate` | `k` | N/mm | Spring rate, compression and extension |
| `spring.force_working_1` | `F_1` | N | Force at working point 1, at the deflection `s₁` of the sheet |
| `spring.force_working_2` | `F_2` | N | Force at working point 2, at the deflection `s₂` of the sheet |
| `spring.length_solid` | `L_s` | mm | Solid length in compression, closed body length in extension and torsion |
| `spring.frequency_surge` | `f_n` | Hz | Surge (natural) frequency |

A port keeps its output symbol as an alias. The two force ports are the forces at the two working points of the sheet, that is at the entered or derived deflections. They are not a force at an arbitrary deflection, which `mech.mechanism` gets from the rate `k`. The torsion rate is `k_phi` (N·m/rad, a different dimension) and is not a port. Links onward go to `mech.mechanism` (follower force and rate), `mech.shaft` and `mech.tolerance-stack`.

## Checks

- Compression: shear stress at solid length against the permissible stress; working deflection against the travel to solid; working deflection against the travel to the minimum length, keeping the coil clearance at `L₂` (EN form); working deflection against the buckling deflection; Goodman fatigue against the required safety factor.
- Extension: shear stress at working point 2, hook bending stress and hook side-bend torsion stress, each against the permissible stress; Goodman fatigue of the body, of the hook side bend and (with `S_e_b`) of the hook bending, each against the required safety factor.
- Torsion: bending stress at working point 2 against the permissible stress; Goodman fatigue against the required safety factor.
- Blocks: `geometry`, `solid`, `missing`, `wire-strength`, `d-max`, `sequence`, `hook`. Warnings: `index`, `buckling`, `surge`, `travel-mismatch`, `s-work-unused`, `hook-fatigue`, `fatigue-cycles`.

## Limitations

Linear characteristic; cold-coiled round wire up to 20 mm; no conical or variable-pitch springs, no rectangular wire; the wire-diameter tolerance enters only through the optional `d_max` of the EN solid-length form; the spring ranges are soft by design (index outside 4 … 20 and `n_active` below 2 only warn, `d` above 20 mm is a soft limit with a wider hard one), because Addendum B §B4 requires a hard FAIL outside the valid range for S7, S8, F1 and F2 only; the Goodman function is the shared `FatigueRelations.GoodmanUtilisation` (`mech.shaft` itself is unchanged), not a call into the `mech.shaft` sheet; the extension hook shape beyond the two radii is not assessed, and the hook bending fatigue needs the bending endurance `S_e_b` (blank: not assessed, with a warning); surge for both ends fixed only; R_m(d), permissible stress fractions and endurance strength are the user's (no EN 10270 table, no EN 13906 permissible-stress diagram); the fatigue check is a Goodman line on the user's endurance strength, not a diagram.

## References

- BS EN 13906-1:2013: calculation of compression springs, with the rate, stress correction, solid length, buckling, fatigue and surge.
- BS EN 13906-2:2013: calculation of extension springs, with the rate, initial tension and hook stress.
- BS EN 13906-3:2014: calculation of torsion springs, with the rate and the bending stress with curvature correction.
- Budynas, *Shigley's Mechanical Engineering Design*, Ch. 10, 10th ed.: helical springs, end geometry, buckling, critical frequency, extension and torsion springs.

## Test cases

Analytic and Confirmed, recomputed in python3: `ka-compression` (k = 4.8828125 N/mm exactly, L_s 20 mm, s₂ 8.192 mm, τ₂ 238.842 MPa, τ_c 728.888 MPa, f_n 350.845 Hz, 1/n_f 0.347693), `ka-wahl-slender` (Wahl k_c 1.144833, y_cr 15.7950 mm, s₂/y_cr 0.759734, f_n 179.633 Hz), `ka-extension` (k = 1.953125 N/mm exactly, σ_A 693.006 MPa, τ_B 381.972 MPa, f_n 140.338 Hz), `ka-torsion` (Shigley K_i chosen explicitly: k_φ 0.503096 N·m/rad, σ₂ 210.596 MPa, D_w 15.8745 mm), `ka-rm-curve` (R_m = 1800/2^0.1 = 1679.459 MPa from A and m; the stresses and utilisations follow). **Unconfirmed** (the EN 13906 relations are typed from the published expressions): `ka-torsion-en` (q = 1.113103, σ₂ 212.587 MPa), `ka-squared-en` (squared unground ends with d_max 2.04 mm: L_s 23.46 mm, S_a 3.136 mm, L_n 26.596 mm, u_n 0.445). **Unconfirmed + Secondary** (solutions manual): `ka-shigley-10-3`, Shigley 10th ed. problem 10-3, d 2.5 mm, D 28.5 mm, n 13, plain and ground ends, G 81 GPa, R_m 1935.9 MPa: the sheet's method reproduces k = 1.3142 N/mm, K_B = 1.1174, L_s = 35 mm, F_s = π d³·0.45 R_m/(8 K_B D) = 167.85 N (published 167.86) and L₀ = L_s + F_s/k = 162.72 mm; the published L₀ is entered, so F_c = 167.856 N and u_c = 1.0000 at solid length. Working points, E, ρ and S_e of that case are placeholders, not part of the problem. **Not pinned (owner check):** the Shigley solutions-manual values for problems 10-5 (k 28 lbf/in, τ_s 48.56 kpsi) and 10-6 (k 3.333 N/mm, τ_s 178.2 MPa): the problem statements are not available to the build, so their inputs cannot be stated; add them as Unconfirmed + Secondary cases once the owner has the problem text. The EN 13906 clause numbers are not quoted (only part and title) for the same reason.

## Changes

**Changes in 1.6.0 (C-2).** `n` is renamed `n_active`, `l_1` and `l_2` are renamed `l_leg_1` and `l_leg_2` (the old symbols are aliases), and the output `p` is renamed `p_coil`. The inputs are in the standard groups, with the optional inputs last in each. The twelve static and fatigue checks show physical values and are named against their limits. The four standard designations are written in full, `BS EN 13906-1:2013`, `BS EN 13906-2:2013` and `BS EN 13906-3:2014`. The five ports have public names `spring.*`. Every message and assumption is rewritten in short sentences. No result changes.

**Changes in 1.4.0 (mech.spring-R1, from the C-1 verification).** The hooks of an extension spring have their own Goodman rows, counted in the verdict: the hook bending stress `σ_A` and the side-bend torsion stress `τ_B` (both proportional to the force) swing between their values at the two working points, `a = (x₂ − x₁)/2`, `m = (x₂ + x₁)/2`. The side bend uses the shear endurance `S_e` and `S_su = f_su·R_m` as the body does; the bending uses the new optional input `S_e_b` (bending endurance at `N_c`, no default) and `S_u = R_m`. Blank `S_e_b` leaves the bending row out and the sheet warns (`hook-fatigue`) that it is not assessed. ka-extension (F₁ 30 N, F₂ 60 N, `R_m` 1800 MPa, `S_e` 400 MPa): `σ_A` 346.50 and 693.01 MPa, `τ_B` 190.99 and 381.97 MPa; side bend `1/n_f` 0.4763 (`n_f` 2.100, passes); hook bending with `S_e_b` 200 MPa `1/n_f` 1.1550 (`n_f` 0.866, FAILS) while the body (`1/n_f` 0.4467) passes. New outputs `u_f_A`, `n_f_A`, `u_f_B`, `n_f_B`. Compression and torsion are unchanged.

**Changes in 1.3.1 (mech.spring-R1).** In extension mode the Goodman check is the body's: the row is now "Goodman fatigue of the body only against the required safety factor (hook fatigue not assessed)" and the sheet warns (`hook-fatigue`) that hook bending and side-bend torsion are not assessed for fatigue and usually govern an extension spring (ka-extension: body shear 358.3 MPa, hook bending 693.0 MPa at the same force; only the static hook checks `u_A` and `u_B` exist). Compression and torsion keep their rows. No number changes; Goodman rows for the hooks are left to a later version.

**Changes in 1.4.1 (merge of the C-1 line's 1.4.0 and the C-2 line's 1.4.0).** Both lines had bumped the template to 1.4.0 with different content (the C-1 follow-up's hook fatigue rows; the C-2 phase A title, ports and `buckling` check), so neither id could stay; the merged template is 1.4.1 and the manifest keeps the C-1 line's 1.4.0 hash. Rules: `hook-fatigue` keeps the C-1 wording and is declared unconservative. No number changes from the merge.

**Changes in 1.4.0 (C-2 fix, M1; shipped as part of 1.4.1).** `buckling`: a compression spring whose working deflection `s₂` reaches the buckling deflection `y_cr` now fails the check "Working deflection against the buckling deflection" (a spring that is stable at any deflection passes it with no buckling deflection to reach); the `buckling` output reads FAIL where it read WARN, and the warning stays to say what to change. Every other value is unchanged.

**Changes in 1.5.0 (C-2 fix, F7).** New Boolean input `guided` (default off, group Buckling): a compression spring guided by a mandrel or in a bore is restrained against buckling, so the free-buckling check is not made. With it on, `y_cr` reads "— (guided: not checked)", `buckling` reads "not checked (guided)", `u_b` is 0, the check "Working deflection against the buckling deflection" is **not in the verdict** (the sheet claims no buckling pass), and the run states the assumption that the guide restrains the spring. Off, and a saved instance without the input, is the free spring of 1.4.1, unchanged.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Plunger return spring: squared and ground compression spring, 60 to 180 N ([sheet](../../worked-examples/mechanical/mech.spring/plunger-return-spring/sheet.txt)).

```text
C = 24 mm / 3 mm = 8
k_c = (8 + 0.5) / (8 − 0.75) = 1.1724
k_c_fat = 1.1724
tau_perm = 0.45 × 1650 MPa = 742.5 MPa
k = 79.3 GPa × (3 mm)^4 / (8 × (24 mm)^3 × 9) = 6.4535 N/mm
n_t = 9 + 2 = 11
d_max = 3 mm
L_s = 3 mm × (11 + 0.0) = 33 mm
S_a = (0.0015 × (24 mm)^2 / 3 mm + 0.1 × 3 mm) × 9 = 5.292 mm
L_n = 33 mm + 5.292 mm = 38.292 mm
p_coil = (70 mm − 2 × 3 mm) / (9 + 0) = 7.1111 mm
s_c = 70 mm − 33 mm = 37 mm
F_c = 6.4535 N/mm × 37 mm = 238.78 N
s_1 = 60 N / 6.4535 N/mm = 9.2974 mm
… 23 more step(s) on the sheet
Governing: PASS  Working deflection against the travel to the minimum length, keeping the coil clearance at L₂: 27.89 mm vs 31.71 mm  utilisation 0.88  « BS EN 13906-1:2013
```
<!-- hand-arithmetic:end -->
