# Plate bending (`struct.plate-bending@1.4.0`)

**In plain terms.** A thin flat plate bends like a slab when a pressure or load pushes on its face, and the thinner it is, the faster the bending stress and the sag grow. The worksheet takes the plate's size, thickness, material, edge support and load, works out how far the middle sags and how hard the most stressed point is working, and compares that stress with the steel's yield and the sag with a limit you choose. It is a screening tool for lids, covers and panels, not a finite-element analysis.

## Scope

Calculates the deflection, von Mises stress and fundamental frequency of a thin, flat, isotropic plate under a lateral load, by Kirchhoff small-deflection theory. It covers circular and rectangular plates, simply supported or clamped, under uniform, patch or ring loads.

## Method

- Flexural rigidity: D = D_flex = E·t_p³/(12(1 − ν²)); surface stress σ = 6M/t_p²; von Mises σ_vm = √(σ_x² − σ_x·σ_y + σ_y² + 3τ_xy²)
- Circular, uniform q, clamped: w = q·a⁴/(64D); σ_edge = 3q·a²/(4t_p²); simply supported: w = (5 + ν)·q·a⁴/(64(1 + ν)D); σ_centre = 3(3 + ν)·q·a²/(8t_p²)
- Circular, central patch (radius c, pressure q) or ring (radius b, force F): w = A + B·r² (+ q·r⁴/64D) inside, A′ + B′·r² + C′·ln(r/a) + k·r²·ln(r/a) outside, k = q·c²/8D or F/8πD; constants from continuity of w, w′, M_r at the load radius and the edge conditions; M_r = −D(w″ + ν·w′/r), M_θ = −D(w′/r + ν·w″)
- Rectangular, simply supported: w = Σ W_mn·sin(mπx/a)·sin(nπy/b) over odd m, n; W_mn = q_mn/(π⁴D(m²/a² + n²/b²)²); q_mn = 16q/(π²mn) (uniform) or 16q/(π²mn)·sin(mπ/2)·sin(nπ/2)·sin(mπu/2a)·sin(nπv/2b) (patch u × v); M_x = Dπ²ΣW_mn(m²/a² + ν·n²/b²)·sin·sin, M_y likewise, M_xy = −D(1 − ν)π²ΣW_mn(mn/ab)·cos·cos; terms added until w, the centre moments and the corner M_xy each change by < 10⁻⁶ over a doubling (cap 1024 × 1024; the corner sum of a uniform load, which converges like 1/N², is Richardson-extrapolated before it is compared)
- Rectangular, clamped: Ritz, w = Σ c_ij·X_i(x/a)·Y_j(y/b), X_k(ξ) = ξ²(1 − ξ)²·P₂ₖ(2ξ − 1); c from the minimum of the total potential energy; order K taken as 8, 12, 16, 20, 25, 30 until w and the stress change by < 10⁻⁵ (or the change stops falling)
- Fundamental frequency: simply supported rectangle ω₁₁ = π²(1/a² + 1/b²)·√(D/(ρ·t_p)), f = ω/2π; circular ω = (λ²/a²)·√(D/(ρ·t_p)), λ² from the Bessel characteristic equation; clamped rectangle by Rayleigh–Ritz with the same basis
- Plate checks: σ_vm ≤ f_y/γ_M0; w_max ≤ span/ratio (circular span = diameter, rectangle span = shorter side); WARN w/t_p > 0.5

Formulas in own notation (see the template method text). `D_flex = E t_p³/(12(1 − ν²))` (written `D` below); surface stress `σ = 6M/t_p²` from the bending and twisting moments per unit width; von Mises `√(σx² − σxσy + σy² + 3τxy²)`.

- *Circular, uniform.* Clamped `w = q a⁴/(64D)`, `σ_edge = 3q a²/(4t_p²)`; simply supported `w = (5 + ν) q a⁴/(64(1 + ν)D)`, `σ_centre = 3(3 + ν) q a²/(8t_p²)`. The von Mises stress is found from the general solution (below): at the clamped edge `σ_r√(1 − ν + ν²)` (since `σ_θ = νσ_r`), at the simply supported centre `σ_r = σ_θ`.
- *Circular, patch (radius c, pressure q) and ring (radius b, total force F).* Derived here from `D∇⁴w = q`: `w = A + B r² (+ q r⁴/64D)` inside the loaded radius and `A′ + B′ r² + C′ ln(r/a) + k r² ln(r/a)` outside, `k = q c²/8D` (patch) or `F/8πD` (ring); constants from continuity of `w`, `w′`, `M_r` at the load radius (`C′ = k c²/2` for the patch, `k b²` for the ring) and the edge conditions (`w = 0` with `w′ = 0` clamped or `M_r = 0` simply supported). Centre deflections, with `β = c/a` or `b/a`: patch clamped `q a⁴/(64D)·(4β² − 3β⁴ + 4β⁴ ln β)`; patch simply supported `q a⁴/(8D)·[(3+ν)/(1+ν)·(β²/2 − β⁴/4) + β⁴/2 ln β − β⁴/8]`; ring clamped `F a²/(16πD)·(1 − β² + 2β² ln β)`; ring simply supported `F a²/(16πD)·[(3+ν)/(1+ν)(1 − β²) + 2β² ln β]`. `M_r = −D(w″ + νw′/r)`, `M_θ = −D(w′/r + νw″)`; the greatest von Mises and principal moments are searched over `0 ≤ r ≤ a` (2000-point grid, the load radius, then golden-section refinement).
- *Rectangular, simply supported.* `w = Σ W_mn sin(mπx/a) sin(nπy/b)` over **odd** `m, n`, `W_mn = q_mn/(π⁴D(m²/a² + n²/b²)²)`; `q_mn = 16q/(π² m n)` (uniform) or `16q/(π² m n)·sin(mπ/2) sin(nπ/2) sin(mπu/2a) sin(nπv/2b)` (patch `u × v`). `M_x`, `M_y`, `M_xy` by differentiating the series. Terms are added in shells while the number of odd terms per direction `N` doubles from 8 up to the cap **1024** (1 048 576 terms), stopping when `w`, the centre von Mises moment and the corner twisting moment each change by less than **10⁻⁶** over a doubling (the corner twisting moment of a uniform load converges like 1/N², so its sum is Richardson-extrapolated from the last two doublings, `s_N + (s_N − s_N/2)/3`, before it is compared and returned; a patch that covers the plate is the uniform load and takes the same path; for a smaller patch the extrapolated corner value is used only on stages where it changes less than the raw sum (the corner sum of a patch converges faster than 1/N² once N is well above plate/patch, so extrapolating an early stage can add error rather than remove it). The `M_xy_k` formula text and the `navier-converge` message say the corner moment is extrapolated. Without this a plate above about 2.2:1 would hit the 1024 cap with a corner change of 10⁻⁶ to 10⁻⁵ and warn although the governing centre value had converged). The sheet reports `N`, the terms and the last change; the sheet is refused (`navier-converge`, 1.3.0) if the cap is reached first. The stress candidates are the **centre** (`M_x`, `M_y`) and a **corner** (`M_xy`, pure twist: `√3|M_xy|`); the behaviour tests scan the whole field on a quarter-plate grid for uniform loads (three plates) and for four central-patch cases and find no larger von Mises moment (a coarse check, 2 × 10⁻³; a python scan on a finer grid over more aspect ratios and ν agreed). Which of the two governs flips with ν and the aspect ratio, so both are evaluated. The corner reaction `2 M_xy` is reported (corners held down).
- *Rectangular, clamped (approximate).* Ritz: `w = Σ c_ij X_i(x/a) Y_j(y/b)`, `X_k(ξ) = ξ²(1 − ξ)² P₂ₖ(2ξ − 1)` (Legendre polynomials times the clamped bubble; `w = w′ = 0` on all edges, symmetric about the centre; 64-point Gauss–Legendre quadrature, exact for every order up to K = 32). `c` minimises the total potential energy (Gauss–Legendre quadrature, Cholesky solve with diagonal scaling). The order `K` follows the sparse schedule **8, 12, 16, 20, 25, 30** until `w` and the stress change by less than **10⁻⁵** between consecutive orders, or until the change stops falling (a change above 0.7 times the previous one counts as stalled and ends the iteration with `ritz-converge`); the run checks its cancellation token between orders. The Cholesky factor uses SIMD dot products and a transposed copy for the back substitution. Measured on the build machine: 5000 × 500 clamped 74 ms (K = 16, stalled), 10000 × 1000 55 ms, 1000 × 1000 with a 250 × 250 patch 144 ms (K = 25), a 500 × 500 square 13 ms (K = 16); the worst case, a plate that runs to K = 30 without stalling, about 0.5 s. Compared with K = 30, the stalled 5000 × 500 stress (K = 16) is within 3 × 10⁻⁵, and the 25 % patch within about 10⁻³. Stress is the greatest von Mises value on a 21 × 21 grid over a quarter of the plate (edges and centre included), with its position. Deflection converges to about 10⁻⁹. The stress converges slowest for long plates and small patches, and the last change understates its error (a 1000 mm square with a 100 × 100 mm patch was still about 1 % high at K = 20, which is why the patch minimum is 25 %). The sheet warns `ritz-approx` always, and warns `ritz-converge` and fails the check "Ritz change against the tolerance" (1.3.0) if the tolerance was not met, quoting the last change and about three times it as an estimate, not a bound. Not for patches below 25 % of a side.
- *Frequency (output Hz).* Simply supported rectangle `ω₁₁ = π²(1/a² + 1/b²)√(D/ρt_p)` (the §B6 formula is the angular frequency in rad/s; the sheet prints both and outputs `f = ω/2π` in Hz). Circular `ω = (λ²/a²)√(D/ρt_p)` with `λ²` from the characteristic equation solved at run time: clamped `J₀(λ)I₁(λ) + I₀(λ)J₁(λ) = 0` (`λ² = 10.2158`), simply supported `J₁/J₀ + I₁/I₀ = 2λ/(1 − ν)` (`λ² = 4.935` at ν = 0.3). Clamped rectangle: Rayleigh–Ritz lowest eigenvalue of `K c = ω² M c` with the same basis (square: `λ² = 35.985`).
- *Checks.* `u_stress = σ_vm/(f_y/γ_M0)` (BS EN 1993-1-1 6.2.1(5), the yield criterion; γ_M0 from 6.1(1)); `u_defl = w_max/(span/ratio)` with span the diameter (circular) or the shorter side. **WARN** `w/t_p > 0.5` (small-deflection theory invalid) and span/t_p < 10 (thick plate).

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram: not to scale, inputs only, no result (ADR-0158, DI-08). It draws the plate in plan with the loaded area, and in section on its edge supports under the load. One variant per shape, edge support, load type and load basis. It binds `shape`, `support`, `a`, `b`, `t_p`, `q`, `F`, `r0`, `u`, `v`, `r_ring`, `E`, `nu`, `rho` and `f_y`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `shape` | Plate shape | — | rectangular | circular, rectangular | Setup | — |
| `support` | Edge support, all round | — | simply supported | simply supported, clamped | Setup | — |
| `load_type` | Load type | — | uniform | uniform, central patch, ring (circular only) | Setup | — |
| `load_basis` | Uniform and patch loads given as | — | pressure q | pressure q, total force P | Setup | — |
| `a` | Plate radius (circular) or length in the x direction (rectangular) | mm | 600 | 10 to 10000 | Geometry | — |
| `b` | Length in the y direction (rectangular plate only, not used for a circular plate) | mm | 400 | 10 to 10000 | Geometry | — |
| `t_p` | Plate thickness | mm | 6 | 0.5 to 100 | Geometry | `t` |
| `E` | Young's modulus | GPa | 210 | 0.1 to 500 | Material | — |
| `nu` | Poisson's ratio | — | 0.3 | 0 to 0.45 | Material | — |
| `rho` | Density (for the frequency) | kg/m³ | 7850 | 100 to 20000 | Material | — |
| `f_y` | Yield strength | MPa | 275 | 5 to 2000 | Material | — |
| `q` | Pressure on the loaded area (uniform and patch loads, pressure basis) | kPa | 10 | 0.001 to 5000 | Loads | — |
| `F` | Total force (force basis: uniform, patch and ring loads; a ring load always uses the force) | kN | 5 | 0.001 to 10000 | Loads | `P` |
| `r0` | Patch radius (circular plate, central patch) | mm | 50 | 1 to 5000 | Loads | — |
| `u` | Patch size in x (rectangular plate, central patch) | mm | 100 | 1 to 10000 | Loads | — |
| `v` | Patch size in y (rectangular plate, central patch) | mm | 100 | 1 to 10000 | Loads | — |
| `r_ring` | Ring radius (circular plate, ring load) | mm | 150 | 1 to 5000 | Loads | — |
| `gamma_M0` | Partial factor on the resistance of the cross-section | — | 1 | 1 to 1.5 | Factors | — |
| `defl_ratio` | Deflection limit as span/ratio (span = diameter of a circular plate, shorter side of a rectangle) | — | 250 | 50 to 2000 | Requirements | — |

## Assumptions

1. The plate is thin, flat, isotropic, linear elastic and of constant thickness, so Kirchhoff small-deflection theory applies. Basis: The middle surface is unstrained, and shear deformation is neglected, so the sheet is blocked below span/t of 10.
2. The deflection stays small against the thickness, with w/t at most 0.5. Basis: Above 0.5, membrane action stiffens the plate, and the small-deflection result overstates the deflection and the bending stress. The sheet warns.
3. The edge support is the same all round and ideal. Basis: A simply supported edge is held at zero deflection and is free to rotate, and a clamped edge allows no deflection or rotation. There are no elastic edges.
4. A simply supported rectangle has its corners held down. Basis: The sheet reports the corner reaction 2·M_xy. If the corners can lift, the deflection and the central moments are larger than computed.
5. The von Mises stress is that of the plane-stress field at the plate surface, from the bending and twisting moments. Basis: The stress is 6M/t². The through-thickness shear and the local stress under a concentrated load are not included.
6. The stress of a clamped rectangle is scanned on a 21 × 21 grid over a quarter of the plate. Basis: A simply supported rectangle is governed at the centre or at a corner, where the twisting moment acts.
7. The patches are centred and a ring load is concentric. Basis: A load smaller than the thickness is a concentrated load, and the sheet warns (rule `patch-thin`). Patches below 1 % (circular), 2 % (simply supported rectangle) or 25 % (clamped rectangle) of the plate size are refused.
8. The clamped rectangle is a Ritz approximation, so its moments and stress converge more slowly than its deflection. Basis: The sheet reports the order used and the last change, which understates the error for long plates and small patches. Compare a critical result with finite elements.
9. The material properties E, ν, ρ and f_y are inputs, each citing its source when taken from the material library. Basis: The generic textbook seeds in the library are indicative.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `w_max` | Maximum deflection | mm |
| `sigma_max` | Greatest von Mises stress | MPa |
| `sigma_principal` | Greatest principal stress | MPa |
| `f1` | Fundamental frequency | Hz |
| `f_yd` | Design yield strength | MPa |
| `u_stress` | Stress utilisation | — |
| `u_defl` | Deflection utilisation | — |
| `w_over_t` | Deflection over thickness | — |
| `D_flex` | Flexural rigidity | kN·m |
| `M_edge` | Edge moment | kN·m/m |
| `M_c` | Moment at the centre | kN·m/m |
| `lambda2` | Frequency parameter λ² | — |
| `location` | Governing stress point | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `plate.deflection_max` | `w_max` | Maximum plate deflection | mm | — |
| `plate.stress_max` | `sigma_max` | Greatest von Mises surface stress | MPa | — |
| `plate.frequency_fundamental` | `f1` | Fundamental natural frequency | Hz | — |
| `plate.rigidity_flexural` | `D_flex` | Flexural rigidity of the plate | kN·m | `D` |

## Checks

Checks the sheet makes:

- Von Mises stress against the design yield strength
- Deflection against the deflection limit

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `ring-rect` | Block | Conservative | A ring load applies to a circular plate. |
| `aspect` | Block | Conservative | The long side of a rectangle can be at most 10 times the short side (beyond that the plate is a one-way strip). |
| `patch-size` | Block | Conservative | The patch cannot be larger than the plate. |
| `patch-min` | Block | Conservative | The patch must be at least 1 % (circular), 2 % (simply supported rectangle) or 25 % (clamped rectangle) of the plate size for the solution to resolve it. |
| `ring-radius` | Block | Conservative | The ring radius must lie between 1 % and 99 % of the plate radius. |
| `engine` | Block | Conservative | The solution could not be evaluated for these inputs. |
| `large-deflection` | Warning | Unconservative | w/t above 0.5: the small-deflection theory is not valid. |
| `thin-plate` | Block | Unconservative | span/t below 10: shear deformation matters and the thin-plate theory over-states the stiffness, so the sheet is refused. |
| `patch-thin` | Warning | Unconservative | A patch smaller than the thickness is a concentrated load: the local stress is not given by thin-plate theory. |
| `navier-converge` | Block | Unconservative | The Navier series reached its cap without changing by less than the tolerance, so the sheet is refused. |
| `ritz-converge` | Warning | Unconservative | The Ritz solution reached its highest order without changing by less than the tolerance: the convergence check fails (a block would refuse the clamped patch at its documented minimum and the long clamped plates the sheet runs). |
| `ritz-frequency` | Warning | Unconservative | The inverse iteration for the clamped rectangle's fundamental frequency did not settle. |
| `ritz-approx` | Warning | Conservative | The clamped rectangle is a Ritz approximation. |

## Limitations

The sheet covers thin plates with span/t of at least 10 and small deflections with w/t at most 0.5.

The plate is isotropic and of constant thickness, with one support type all round and centred loads only.

The sheet has no elastic edges, no local effects under concentrated loads, no temperature loads and no in-plane loads.

A simply supported rectangle has its corners held down.

Book values beyond the coefficients in the test cases are not pinned. They are the Timoshenko tables for other aspect ratios and patch loads, and any published clamped-plate frequency other than the square.

### Changes by version

**1.4.0 (C-2 consistency).** The plate thickness `t` is now `t_p` (old symbol kept as an alias, because a bare `t` means time), the same name as the base plate and connection sheets. The total force `P` is now `F`, and the flexural rigidity `D` is now `D_flex`, because a bare `P` means power and a bare `D` means a diameter. The old symbols stay as aliases: `P` on the input, and `D` through the new port `plate.rigidity_flexural`. The flexural rigidity is shown in kN·m and the bending moments per unit width in kN·m/m, the standard units of their quantity types, and the values do not change. Every check shows its demand and capacity with units: the von Mises stress against the design yield strength (new step `f_yd`) and the deflection against its limit. The inputs are grouped Setup, Geometry, Material, Loads, Factors and Requirements. The four ports have public names `plate.*`. The scope, messages, assumptions and references follow the sheet standard. No calculated value changes.

**Worked example.** `docs/worked-examples/structural/struct.plate-bending/enclosure-lid-aluminium/`: 300 × 200 × 4 mm aluminium lid under 20 kPa (chain 10, enclosure): `w_max` 0.59 mm, σ_vm 21.6 MPa at the centre, f₁ 353 Hz. E, ν, ρ are pinned to the W3 generic aluminium seed (indicative); the yield strength is assumed; γ = 1.1 is the EN 1999 value entered in the γ_M0 slot, to be checked.

**Revision 1.1.0 (review).** Corner `M_xy` extrapolated and the Navier convergence test made on the extrapolated value (long plates no longer warn); `RitzCap` 20 → 30, clamped-patch minimum 10 % → 25 %, `ritz-converge` and `ritz-approx` reworded (no accuracy claim); the `ring-basis` warning dropped (it fired on the default setting); the stress check cites 6.2.1(5); an exact 0 for the simply supported edge moment; worked example uses the W3 aluminium seed and γ = 1.1.

**Revision 1.2.0 (second review).** Sparse Ritz schedule with a stall rule and cancellation check, SIMD Cholesky, frequency iteration capped (150 iterations, 10⁻¹¹), `Solve` limited to K ≤ 30; the extrapolation rule for patch corner sums as above, and a patch covering the plate is the uniform path (the full-patch-equals-uniform test is now exact to 10⁻¹²).

**Revision 1.2.1.** The Rayleigh–Ritz frequency iteration reports whether it settled; if the 150-iteration cap is reached the sheet warns (`ritz-frequency`) with the last relative change. Timing tests scale by `TEMPEST_TEST_TIMEOUT_FACTOR`.

**Changes in 1.3.0 (C-2 fix, F2).** `thin-plate` (span/t below 10: shear deformation is not in the theory and the plate comes out stiffer and less stressed than it is) and `navier-converge` (the Navier series stopped at its cap) are now blocks; both warned and passed. `ritz-converge` (the Ritz solution stopped without meeting the tolerance, its stress error several times the last change) now fails the check "Ritz change against the tolerance" and keeps its warning and its numbers: it is a failing check and not a block because the clamped patch at its documented minimum (a quarter of the plate) and the long clamped plates stop at the stall rule without converging, and the sheet reports them. The Navier series converged for every input the sheet accepts in the cases run; the block is the guard.

## References

- Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells, Ch. 3, 2nd ed.: Symmetrical bending of circular plates.
- Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells, Ch. 5, 2nd ed.: Simply supported rectangular plates (Navier solution).
- Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells, Ch. 6, 2nd ed.: Rectangular plates with various edge conditions.
- Leissa, Vibration of Plates (NASA SP-160), Ch. 2 and 4, 1st ed.: Natural frequencies of circular and rectangular plates.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.1(1): Partial factor γ_M0.
- BS EN 1993-1-1:2005+A1:2014, cl. 6.2.1(5): Yield criterion for the cross-section.

## Test cases

Analytic, **Confirmed** (recomputed in python): `ka-circular-clamped-uniform` and `ka-circular-simple-uniform` (the closed forms and the frequencies from the Bessel equations); `ka-circular-simple-patch` and `ka-circular-clamped-ring` (derived solutions, cross-checked in python by finite differences and by integrating the ring solution over the patch); `ka-rect-simple-square-series` (the Navier series summed in numpy at 1024 and 512 terms with the 1/N² tail removed: `w_max = 0.0040623527 q a⁴/D`, `M_x = 0.0478864 q a²`, `|M_xy| = 0.0324823 q a²`); `ka-rect-simple-natural`; `ka-rect-simple-patch`. **Unconfirmed**: `ka-rect-simple-square-book` (`0.00406`, Timoshenko & Woinowsky-Krieger 2nd ed. Ch. 5), `ka-rect-clamped-square` (the Ritz solution recomputed in an independent numpy implementation; approximate method), `ka-rect-clamped-square-book` (`0.00126`, Ch. 6), `ka-rect-clamped-natural` (`λ² = 35.99`, Leissa, to be checked against a copy). The behaviour tests cover the closed forms against the general solution, the ring-sum check, the centre/corner stress claim, the strip limit of a long clamped plate, convergence, and a CsCheck property (deflection ∝ 1/t³, stress ∝ 1/t²).

- `ka-circular-clamped-uniform`: Circular plate, clamped, uniform pressure: w = q·a⁴/(64D), edge stress 3q·a²/(4t²). Source: Analytic closed forms (Kirchhoff theory), recomputed in python, Timoshenko & Woinowsky-Krieger, Ch. 3.
- `ka-circular-simple-uniform`: Circular plate, simply supported, uniform pressure: w = (5 + ν)q·a⁴/(64(1 + ν)D), centre stress 3(3 + ν)q·a²/(8t²). Source: Analytic closed forms (Kirchhoff theory), recomputed in python, Timoshenko & Woinowsky-Krieger, Ch. 3.
- `ka-circular-simple-patch`: Circular plate, simply supported, central patch of radius 0.4a: closed-form centre deflection and moment. Source: Analytic (patch solution derived from the Kirchhoff equations; cross-checked in python by integrating the ring solution over the patch and by finite differences), Timoshenko & Woinowsky-Krieger, Ch. 3.
- `ka-circular-clamped-ring`: Circular plate, clamped, ring load of 4 kN at radius 0.5a: closed-form centre deflection and the edge moment. Source: Analytic (ring solution derived from the Kirchhoff equations; cross-checked in python by finite differences), Timoshenko & Woinowsky-Krieger, Ch. 3.
- `ka-rect-simple-square-series`: Square plate, simply supported, uniform pressure (ν = 0.3): w_max = 0.0040624·q·a⁴/D from the Navier series. Source: Analytic: the Navier double series summed to the limit (python, numpy, 1024 odd terms per direction with the 1/N² tail removed), Timoshenko & Woinowsky-Krieger, Ch. 5.
- `ka-rect-simple-square-book`: Square plate, simply supported, uniform pressure: the classical coefficient w_max = 0.00406·q·a⁴/D. Source: Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells, 2nd ed..
- `ka-rect-simple-natural`: Rectangular plate 600 × 400 × 6 mm, simply supported: f₁₁ = (π/2)(1/a² + 1/b²)·√(D/(ρt)). Source: Analytic closed form for the (1,1) mode of a simply supported rectangle (ω = π²(1/a² + 1/b²)√(D/ρt), f = ω/2π), recomputed in python, Timoshenko & Woinowsky-Krieger, Ch. 5 / Leissa Ch. 4.
- `ka-rect-simple-patch`: Rectangular plate 600 × 400 × 6 mm, simply supported, central patch 150 × 100 mm of 50 kPa: Navier series with the patch coefficients. Source: Analytic: Navier double series with q_mn = 16q/(π²mn)·sin(mπ/2)sin(nπ/2)sin(mπu/2a)sin(nπv/2b), recomputed in python (numpy, 1024 odd terms per direction), Timoshenko & Woinowsky-Krieger, Ch. 5.
- `ka-rect-clamped-square`: Square plate, clamped, uniform pressure (ν = 0.3): Ritz solution, w_max about 0.00127·q·a⁴/D. Source: Ritz solution (the basis of this sheet) recomputed in an independent python implementation (numpy); approximate method, Timoshenko & Woinowsky-Krieger, Ch. 6.
- `ka-rect-clamped-square-book`: Square plate, clamped, uniform pressure: the classical coefficient w_max = 0.00126·q·a⁴/D. Source: Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells, 2nd ed..
- `ka-rect-clamped-natural`: Square plate, clamped: fundamental frequency ω = 35.99/a²·√(D/(ρt)). Source: Rayleigh–Ritz result of this sheet (python recomputation); published constant λ² = 35.99 (Leissa, Vibration of Plates, Ch. 4), to be checked against the owner's copy, NASA SP-160.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Aluminium enclosure lid 300 × 200 × 4 mm, simply supported, 20 kPa internal pressure rise ([sheet](../../worked-examples/structural/struct.plate-bending/enclosure-lid-aluminium/sheet.txt)).

```text
F_total = 20 kPa × 60000 mm² = 1.2 kN
D_flex = 70 GPa × (4 mm)^3 / (12 × (1 − 0.33^2)) = 0.41896 kN·m
terms_used = 128 odd terms per direction (16384 terms), last change 6.5537e-7 = 128
w_max = 128 odd terms per direction (16384 terms), last change 6.5537e-7 = 0.58996 mm
M_x_c = 128 odd terms per direction (16384 terms), last change 6.5537e-7 = 0.04162 kN·m/m
M_y_c = 128 odd terms per direction (16384 terms), last change 6.5537e-7 = 0.0656 kN·m/m
M_xy_k = 128 odd terms per direction (16384 terms), last change 6.5537e-7 = 0.032874 kN·m/m
R_corner = 2 × 0.032874 kN·m/m = 0.065747 kN
M_vm_c = M_x = 0.04162, M_y = 0.0656 kN·m/m = 0.057492 kN·m/m
M_vm_k = M_xy = 0.032874 kN·m/m = 0.056939 kN·m/m
omega_11 = a = 300 mm, b = 200 mm, D_flex = 0.41896 kN·m, ρt = 10.8 kg/m² = 2219.8 rad/s
M_vm = centre = 0.057492 kN·m/m
sigma_max = 6 × 0.057492 kN·m/m / (4 mm)² = 21.56 MPa
sigma_principal = 6 × 0.0656 kN·m/m / (4 mm)² = 24.6 MPa
… 6 more step(s) on the sheet
Governing: PASS  Deflection against the deflection limit: 0.5900 mm vs 0.8000 mm  utilisation 0.74  « Serviceability
```
<!-- hand-arithmetic:end -->
