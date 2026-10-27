# O-ring and seal gland (`fp.oring-gland@1.5.0`)

**In plain terms.** An O-ring seals by being squeezed in its groove, and how it fits changes with stretch, temperature, swelling in the fluid and the tolerances of the parts. The worksheet works out the squeeze, the stretch, how full the groove is and the gap the ring could be pushed into, at the coldest and hottest ends of the range, for a piston, rod or face seal, and says whether each stays within the band for static or dynamic duty.

## Scope

Calculates squeeze, stretch, gland fill and extrusion gap of a piston, rod or face O-ring seal at the cold and hot ends of its temperature and swell range. You supply the ring sizes, bands and extrusion limits.

## Method

- Ring stretch: X = (g·D_ref)/(f·d) − 1, D_ref the diameter the ring ID sits on (groove root for a piston or face seal, rod for a rod seal); f = (1 + α(T − 20 °C))(1 + swell)^(1/3) the ring and g = 1 + α_m(T − 20 °C) the groove metal
- Ring cross-section: W_eff = f·W·(1 − r(X))/√(1 − c), r(X) the chosen relation (none; 1 − 1/√(1+X); Y = 0.56 + 0.59X − 0.0046X² per cent as cited, used for about 3 to 25 % stretch only; ν·X) and c the compression of the ring outside diameter where it is held (rod seal in the housing groove; internal-pressure face seal on the groove outer wall), c = 1 − g·D_g/(f·(d + 2W))
- Depth and gap: piston h = (D_s − D_g)/2, gap = D_s − D_c; rod h = (D_g − D_s)/2, gap = D_c − D_s; face h and gap entered directly (all times g)
- Ring squeeze: q = 1 − g·h/W_eff, minimum at the cold end from the largest depth over the smallest W_eff, maximum at the hot end
- Gland fill: ring area over the groove area g·b·g·h − 2(1 − π/4)(g·r_g)²; maximum at the hot end on a volume basis, (π/4)·(f·W)²·(f/g) with f³ = (1 + αΔT)³(1 + s) (an outside diameter held in the groove: (π/4)·(f·W)²/(1 − c)), minimum at the cold end (π/4)·W_eff²
- Worst case: each range from the engine's worst-case stack of the dimension (Σ|coefficient|·half-band), combined at the corner that is worst for the quantity
- Monte Carlo: the engine's seeded Monte Carlo on the exact criterion, each dimension from its own distribution; the fraction below the lower and above the upper limit. The capability index C_pk uses the criterion linearised about the mean dimensions (secant slope across each band)
- Extrusion gap: the largest gap over the installed, cold and hot temperatures (the sealing-surface part and the groove metal each with its own expansion) plus the bore dilation under pressure, against the permitted gap from your record (interpolated in hardness and pressure) or the value entered; with neither the check fails (not verified)

### Notes on the method

Lengths in millimetres inside the model, ratios as fractions. Case factors: ring `f = (1 + α(T − 20 °C))·(1 + s)^(1/3)` (linear thermal expansion times the cube root of the volume swell), groove metal `g = 1 + α_m(T − 20 °C)`; cold end `(T_min, s_min)`, hot end `(T_max, s_max)`, installed `f = g = 1`. Stretch `X = g·D_ref/(f·d) − 1` where `D_ref` is the diameter the ring ID sits on: the groove root `D_g` (piston, face) or the rod `D_s` (rod seal). Effective section `W_eff = f·W·(1 − r(X))/√(1 − c)`, where `c = 1 − g·D_g/(f·(d + 2W))` (not below zero) is the compression of the ring outside diameter where that is held in the groove — a rod seal in its housing groove, an internal-pressure face seal on the groove outer wall — and zero for every other seal (a constant-volume thickening, M4). Depth `h` is `(g_s·D_s − g_m·D_g)/2` (piston), `(g_m·D_g − g_s·D_s)/2` (rod) or `g_m·h_f` (face), each part at its own temperature (`g_s` the sealing-surface part's factor from `alpha_s`, `g_m` the groove metal's; one coefficient gives the former `g·(D_s − D_g)/2`), and the rod seal's stretch reference `D_s` is scaled by `g_s` (L-f); gap is `D_s − D_c`, `D_c − D_s` or the entered `e_f`. Squeeze `q = 1 − g·h/W_eff`, where `h` is the gland depth: half the diametral difference for a radial seal and, since 1.3.0, `h = h_f + e_f` for a face seal (the ring is compressed between the groove bottom and the other flange, which stands the gap `e_f` off the groove face; before 1.3.0 `e_f` was only an extrusion gap, so a 0.5 mm flange gap gave the same squeeze as a 0.05 mm one and squeezed a ring that was not). Groove area `A_g = (g·b)(g·h) − 2(1 − π/4)(g·r_g)²`. Fill is a volume ratio: the maximum (hot end) is `(π/4)·(f·W_max)²·(f/g)/A_g,min`, i.e. `(π/4)·W²·(1 + αΔT)³(1 + s)/(A_g,nom·g³)` — a ring held circumferentially by the groove takes the whole thermal and swell volume change in its section (M2; the isotropic `f²` it replaces read 2 to 5 points low, on the unsafe side); where the outside diameter is held the same volume change is the compression, `(π/4)·(f·W)²/(1 − c)` — a ring that is barely compressed still takes the `f/g` of its held circumference, so the larger of `f/g` and `1/(1 − c)` is used; the minimum (cold end) is `(π/4)·W_eff²/A_g,max`. A rod seal and an internal-pressure face seal also report the outside-diameter compression `(d + 2W − D_g)/(d + 2W)`, checked against its own limit `od_hi`.
The stretch is also reported and checked at the cold end (`X_c`, against `x_hi`), where the ring is shortest against the groove.
The cross-section reduction `r(X)` is a user-selectable relation: **None**; **Incompressible rubber (derived)**, `r = 1 − 1/√(1 + X)` (constant volume, default); **Parker ORD 5700 section 3.5 quadratic (as cited)**, `Y = 0.56 + 0.59·X − 0.0046·X²` with X and Y in per cent, applied for X above zero and used for about **3 to 25 % stretch only** (M3): above 25 % the 25 % value is held, below 3 % the reduction is blended linearly to zero at no stretch so that it does not jump from nothing to 0.56 % at the first trace of stretch, and the sheet warns (`stretch`) when a stretch lies outside the range — implemented as Addendum B quotes it, with the section reference only, and **not confirmed** against the handbook (what Y measures, and the exact range it covers, are an owner check; the sub-3 % blend is this sheet's, not the handbook's); **Linear in stretch**, `r = ν·X` for the Poisson ratio ν (a W3 material property).
**Worst case** (engine): each range is the worst-case band of a linear stack (depth, ring section scaled by `f`, groove width, the two ring and seat diameters, the gap); the stretch ends are `D_ref,max/d_min − 1` and `D_ref,min/d_max − 1`. Minimum squeeze `= 1 − h_max,cold/W_eff,min` with `W_eff,min = f_c·W_min·(1 − r(X_c,max))`; maximum squeeze `= 1 − h_min,hot/W_eff,max`. Maximum fill uses the hot end on the unstretched section `f_h·W_max` over the smallest area; minimum fill the cold end on `W_eff,min` over the largest area. The ends of one dimension are taken independently, so where the groove root diameter sets both the depth and the stretch of a piston seal the worst case is slightly conservative; the sheet prints the exact non-linear corner as a check and warns (`worst-case`) if it is more than 0.2 points below the stack value.
**Monte Carlo** (engine): the engine's seeded `MonteCarlo.Run` evaluates the exact criterion (`ORingGlandModel.Evaluate`) on every sample, each dimension that varies drawn in dimension order (W, d, D_s, D_g, D_c, h_f, b, e_f) from its own distribution; the fraction below the lower and above the upper limit is reported (L1; the linearised model under-predicted the minimum-squeeze tail by about 9 % relative). The capability index C_pk stays on the criterion linearised about the mean dimensions with a secant slope across each band, `a_i = (F(mid_i + half_i) − F(mid_i − half_i))/(2·half_i)`, through `ToleranceStackEngine.Analyse`. Dimensions are normal, uniform or triangular about their mid-limit (`σ = half/k`, `half/√3`, `half/√6`); a measured row sets the mean, σ and distribution, with half-band `k·σ`, `σ√3` or `σ√6`; a measured row whose `dim` is not a gland dimension blocks (L6).
**Extrusion gap.** The largest gap of a radial seal is taken over the installed, cold and hot temperatures with each part expanding by its own metal — the sealing-surface part (bore of a piston seal, rod of a rod seal) by `alpha_s` (blank: the groove metal's `alpha_m`), the groove-metal part by `alpha_m` — plus the diametral growth of the bore under pressure `dD_p` that the user enters (0 if not known; L3). A face seal's axial gap `e_f` is as dimensioned; since 1.3.0 it is also part of the compression height (below).
**Extrusion limit.** The permitted gap is the smaller of the entered `gap_allow` and the record `ext` read at the ring hardness `H_A` and the pressure `p_op`: points `(H, p, gap)` are grouped by hardness into curves of gap against pressure; linear in pressure within a curve and in hardness between curves. Pressure above a curve's last point, or hardness below the softest listed curve, is outside the record: the nearest listed value is shown, the check **fails** and the sheet warns (`extrusion-range`). Hardness above the hardest curve uses that curve (conservative). With neither a record nor a value the extrusion is not verified: the sheet warns (`extrusion`) and the check "Extrusion verified against a permitted gap" **fails** while the operating pressure is above zero (L2).

**Reference diagram.** The worksheet draws the gland that `type` selects in section: a piston seal, a rod seal or a face seal. The ring is bound to `W`, the groove to `b`, and the diameters to `D_s`, `D_c` and `D_g` (`h_f` and `e_f` for a face seal), with `p_op` on one side. `d_r`, `duty`, `T_min`, `T_max`, `q_lo_s`, `q_hi_s` and `gap_allow` are labels. The drawing is not to scale (ADR-0158, DI-08).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `type` | Gland arrangement |  | Piston (radial) | Piston (radial), Rod (radial), Face (axial) |  | Setup |
| `duty` | Static or dynamic duty (selects the squeeze band) |  | Static | Static, Dynamic |  | Setup |
| `face_p` | Face seal only: which side the pressure acts on (external: the ring ID seats on the groove inner wall and is stretched; internal: the ring OD seats on the groove outer wall and is compressed) |  | External (ring ID seated on the groove) | External (ring ID seated on the groove), Internal (ring OD seated on the groove OD) |  | Setup |
| `d_r` | Ring inside diameter d (mean of the supplier's tolerance) | mm | 50 | 3 to 500 | 1 to 1000 | Ring |
| `d_r_tol` | Ring inside diameter tolerance, plus or minus | mm | 0.4 | 0 to 2 | 0 to 5 | Ring |
| `W` | Ring cross-section diameter | mm | 3.5 | 1 to 10 | 0.5 to 20 | Ring |
| `W_tol` | Ring cross-section tolerance, plus or minus | mm | 0.1 | 0 to 0.4 | 0 to 2 | Ring |
| `D_s` | Sealing-surface diameter: the bore of a piston seal, the rod of a rod seal (not used for a face seal) | mm | 56.9 | 5 to 1000 | 1 to 2000 | Gland |
| `D_s_u` | Sealing-surface diameter, upper deviation | mm | 0.046 | -0.5 to 0.5 | -5 to 5 | Gland |
| `D_s_l` | Sealing-surface diameter, lower deviation | mm | 0 | -0.5 to 0.5 | -5 to 5 | Gland |
| `D_g` | Groove root diameter: on the piston, in the housing of a rod seal, or for a face seal the diameter the ring seats on (the groove inner wall for external pressure, the groove outer wall for internal pressure) | mm | 51.5 | 5 to 1000 | 1 to 2000 | Gland |
| `D_g_u` | Groove root diameter, upper deviation | mm | 0 | -0.5 to 0.5 | -5 to 5 | Gland |
| `D_g_l` | Groove root diameter, lower deviation | mm | -0.05 | -0.5 to 0.5 | -5 to 5 | Gland |
| `D_c` | Mating part that sets the extrusion gap: the piston outside diameter, or the housing bore round the rod (not used for a face seal) | mm | 56.9 | 5 to 1000 | 1 to 2000 | Gland |
| `D_c_u` | Mating part diameter, upper deviation | mm | -0.03 | -0.5 to 0.5 | -5 to 5 | Gland |
| `D_c_l` | Mating part diameter, lower deviation | mm | -0.06 | -0.5 to 0.5 | -5 to 5 | Gland |
| `h_f` | Face-seal groove depth (face seal only), where the ring is compressed over the depth plus the gap | mm | 2.7 | 0.3 to 20 | 0.1 to 50 | Gland |
| `h_f_u` | Face-seal groove depth, upper deviation | mm | 0.05 | -0.5 to 0.5 | -5 to 5 | Gland |
| `h_f_l` | Face-seal groove depth, lower deviation | mm | 0 | -0.5 to 0.5 | -5 to 5 | Gland |
| `e_f` | Face-seal gap between the flanges (face seal only), which is the extrusion gap and adds to the compression height | mm | 0.05 | 0 to 2 | 0 to 10 | Gland |
| `e_f_u` | Face-seal gap, upper deviation | mm | 0.05 | -0.5 to 0.5 | -5 to 5 | Gland |
| `e_f_l` | Face-seal gap, lower deviation | mm | 0 | -0.5 to 0.5 | -5 to 5 | Gland |
| `b` | Groove width | mm | 4.8 | 1 to 20 | 0.3 to 50 | Gland |
| `b_u` | Groove width, upper deviation | mm | 0.1 | -0.5 to 0.5 | -5 to 5 | Gland |
| `b_l` | Groove width, lower deviation | mm | 0 | -0.5 to 0.5 | -5 to 5 | Gland |
| `ecc` | Gland eccentricity or run-out (one-sided offset; it deepens the gland by this much where the squeeze is least and shallows it where the squeeze is most; 0 if none) | mm | 0 | 0 to 0.5 | 0 to 5 | Gland |
| `r_g` | Groove bottom corner radius | mm | 0.2 | 0 to 1 | 0 to 5 | Gland |
| `T_min` | Lowest operating temperature (the ring sits at 20 °C when installed) | °C | -20 | -60 to 100 | -150 to 400 | Conditions |
| `T_max` | Highest operating temperature | °C | 80 | -40 to 200 | -150 to 400 | Conditions |
| `alpha` | Ring linear thermal expansion coefficient α (an indicative elastomer figure; take yours from the compound or the material library) | 1/K | 0.00015 | 5e-05 to 0.0003 | 0 to 0.001 | Conditions |
| `alpha_m` | Groove metal linear thermal expansion coefficient α_m (the part that carries the groove; one material record per sheet, so this one is entered from the metal's data sheet) | 1/K | 1.2e-05 | 5e-06 to 3e-05 | 0 to 0.0001 | Conditions |
| `s_min` | Smallest volume change of the ring in the fluid (negative for shrinkage; 0 if none) | % | 0 | -10 to 0 | -20 to 100 | Conditions |
| `s_max` | Largest volume swell of the ring in the fluid | % | 5 | 0 to 25 | -20 to 100 | Conditions |
| `alpha_s` | Linear thermal expansion coefficient of the part that carries the sealing surface, the bore of a piston seal or the rod of a rod seal, which sets the extrusion gap over temperature (optional; blank = the groove metal's α_m) | 1/K | optional | 5e-06 to 3e-05 | 0 to 0.0001 | Conditions |
| `red` | Cross-section reduction with stretch |  | Incompressible rubber (derived) | None, Incompressible rubber (derived), Parker ORD 5700 section 3.5 quadratic (as cited), Linear in stretch, nu times X (derived) |  | Stretch |
| `nu` | Poisson ratio of the ring, for the linear relation |  | 0.49 | 0.3 to 0.5 | 0 to 0.5 | Stretch |
| `q_lo_s` | Static duty: lowest acceptable squeeze (from your handbook) | % | 15 | 5 to 30 | 0 to 60 | Requirements |
| `q_hi_s` | Static duty: highest acceptable squeeze | % | 30 | 10 to 45 | 0 to 80 | Requirements |
| `q_lo_d` | Dynamic duty: lowest acceptable squeeze | % | 10 | 5 to 25 | 0 to 60 | Requirements |
| `q_hi_d` | Dynamic duty: highest acceptable squeeze | % | 25 | 10 to 35 | 0 to 80 | Requirements |
| `fill_lo` | Gland fill: lowest acceptable | % | 60 | 40 to 80 | 0 to 100 | Requirements |
| `fill_hi` | Gland fill: highest acceptable | % | 85 | 60 to 95 | 0 to 100 | Requirements |
| `x_lo` | Installed stretch: lowest acceptable (negative allows a ring that is compressed on its seat) | % | 0 | -5 to 5 | -50 to 50 | Requirements |
| `x_hi` | Installed stretch: highest acceptable (the installation rule is up to 6 % on a piston groove); also applied at the cold end | % | 6 | 1 to 10 | 0.1 to 100 | Requirements |
| `od_hi` | Ring outside-diameter compression: highest acceptable, in a rod-seal groove or against the groove outer wall of an internal-pressure face seal (your record; the default is a placeholder equal to the stretch limit, not a handbook value) | % | 6 | 1 to 10 | 0.1 to 100 | Requirements |
| `H_A` | Ring hardness, Shore A (for the extrusion record) |  | 70 | 40 to 95 | 20 to 100 | Extrusion |
| `p_op` | Operating pressure (for the extrusion record, gauge) | bar | 160 | 0 to 700 | 0 to 2000 | Extrusion |
| `dD_p` | Diametral growth under the operating pressure of the bore (piston seal) or of the housing bore round the rod (rod seal), from your cylinder or housing calculation; added to the extrusion gap (0 if not known) | mm | 0 | 0 to 0.5 | 0 to 5 | Extrusion |
| `gap_allow` | Largest permitted extrusion gap at this pressure and hardness, if you have it as one value: diametral (the clearance D_s − D_c) for a radial seal, axial for a face seal; a radial chart value is half of it | mm | optional | 0.01 to 1 | 0 to 5 | Extrusion |
| `ext` | Permitted extrusion gap against hardness and pressure (your own record) |  | table |  |  | Extrusion |
| `ext.H` | Hardness, Shore A (column) |  |  | | | Extrusion |
| `ext.p` | Pressure (column) | bar |  | | | Extrusion |
| `ext.gap` | Largest permitted gap (diametral for a radial seal, axial for a face seal) (column) | mm |  | | | Extrusion |
| `n_mc` | Monte Carlo samples |  | 10000 | 1000 to 100000 | 1 to 100000 | Statistics |
| `seed` | Monte Carlo seed |  | 1 | 0 to 1000000 | 0 to 4294967295 | Statistics |
| `k_sigma` | Standard deviations per half-band (normal tolerances and measured normals) |  | 3 | 1 to 6 | 0.1 to 100 | Statistics |
| `dist` | Distribution of the toleranced dimensions |  | Normal | Normal, Uniform, Triangular |  | Statistics |
| `measured` | Measured groove or ring dimensions (mean, standard deviation, distribution), replacing the toleranced ones |  | table |  |  | Statistics |
| `measured.dim` | Dimension (column) |  |  | | | Statistics |
| `measured.mean` | Measured mean (column) | mm |  | | | Statistics |
| `measured.sigma` | Measured standard deviation (column) | mm |  | | | Statistics |
| `measured.dist` | Distribution (column) |  | optional | | | Statistics |

## Assumptions

1. The gland is a plain rectangular groove with a bottom corner radius. Basis: Closed-form gland geometry; no gland table.
2. The ring is a circle of the stated cross-section. Basis: Closed-form gland geometry; no gland table.
3. Edge breaks, groove wall angles and surface finish are not modelled. Basis: Closed-form gland geometry; no gland table.
4. Squeeze is the compressed fraction of the effective cross-section W_eff. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
5. W_eff is the nominal section after thermal expansion, swell and the cross-section reduction of the stretch. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
6. The reduction relation is your choice, and the default is the constant-volume rubber relation. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
7. The quadratic from the Parker handbook is implemented as quoted second-hand and is not confirmed against the handbook. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
8. The quadratic is used for about 3 to 25 % stretch only. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
9. Below 3 % the reduction is blended linearly to zero. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
10. Above 25 % the reduction is held at its 25 % value. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
11. Where the ring outside diameter is held, the compression c thickens the section to W/√(1 − c). Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
12. A rod seal in its housing groove and an internal-pressure face seal hold the ring outside diameter. Basis: The relation is an input choice, and the default is the constant-volume rubber relation, derived.
13. The cold end is the lowest temperature with the least swell, and it sets the minimum squeeze and fill. Basis: Bounding cases of a monotonic model.
14. The hot end is the highest temperature with the most swell, and it sets the maximum squeeze and fill. Basis: Bounding cases of a monotonic model.
15. This assumes the ring expands more than the groove metal (alpha above alpha_m). Basis: Bounding cases of a monotonic model.
16. The worst case combines each dimension at its extreme independently. Basis: Independent ends of linear stacks.
17. Where one dimension appears twice, the worst case is slightly conservative. Basis: Independent ends of linear stacks.
18. The groove root diameter sets both the depth and the stretch of a piston seal. Basis: Independent ends of linear stacks.
19. The Monte Carlo samples each dimension from its own distribution about its mean. Basis: Exact model on the engine's seeded sampler.
20. It evaluates each criterion exactly, with the non-linear model, on the seeded sampler of the engine. Basis: Exact model on the engine's seeded sampler.
21. It counts the fraction outside each limit at the cold or hot end. Basis: Exact model on the engine's seeded sampler.
22. It does not model pressure, friction or compression set. Basis: Exact model on the engine's seeded sampler.
23. The capability index C_pk uses the first-order (secant) linearisation. Basis: Exact model on the engine's seeded sampler.
24. The gland fill at the hot end is on a volume basis. Basis: Volume conservation of the held ring.
25. A ring held circumferentially by the groove takes the whole volume change of thermal expansion and swell in its section. Basis: Volume conservation of the held ring.
26. That change is (1 + αΔT)³(1 + s), while the groove circumference grows with the groove metal. Basis: Volume conservation of the held ring.
27. This is higher than scaling the section area isotropically. Basis: Volume conservation of the held ring.
28. The extrusion gap is the largest diametral clearance of a radial seal over the installed, cold and hot temperatures. Basis: Extrusion limits are licensed data and are not shipped.
29. The extrusion gap of a face seal is its axial gap. Basis: Extrusion limits are licensed data and are not shipped.
30. The sealing-surface part and the groove metal each have their own expansion coefficient. Basis: Extrusion limits are licensed data and are not shipped.
31. The gap adds the diametral growth of the bore under pressure that you enter, or 0 if none. Basis: Extrusion limits are licensed data and are not shipped.
32. The permitted gap is your record, and the sheet never states a handbook value. Basis: Extrusion limits are licensed data and are not shipped.
33. With no permitted gap entered, the extrusion check fails (not verified) while the operating pressure is above zero. Basis: Extrusion limits are licensed data and are not shipped.

## Outputs

| Symbol | Description |
|---|---|
| `squeeze_nom` | Squeeze at the mean dimensions |
| `squeeze_min` | Smallest squeeze |
| `squeeze_max` | Largest squeeze |
| `fill_min` | Smallest gland fill |
| `fill_max` | Largest gland fill |
| `stretch_nom` | Installed stretch, mean dimensions |
| `stretch` | Largest installed stretch |
| `stretch_min` | Smallest installed stretch |
| `X_c` | Largest stretch at the cold end |
| `u_stretch` | Largest stretch against its limit |
| `red_nom` | Cross-section reduction at the mean stretch |
| `gap_min` | Smallest extrusion gap |
| `gap_max` | Largest extrusion gap, diametral for a radial seal (over the temperature range, with the bore dilation) |
| `gap_allow` | Permitted extrusion gap, diametral for a radial seal |
| `u_gap` | Largest gap against the permitted gap |
| `p_sq_min` | Fraction outside the limit: squeeze below the lower limit (cold end) |
| `cpk_sq_min` | C_pk: squeeze below the lower limit (cold end) |
| `p_sq_max` | Fraction outside the limit: squeeze above the upper limit (hot end) |
| `cpk_sq_max` | C_pk: squeeze above the upper limit (hot end) |
| `p_fill_max` | Fraction outside the limit: gland fill above the upper limit (hot end) |
| `cpk_fill_max` | C_pk: gland fill above the upper limit (hot end) |
| `p_fill_min` | Fraction outside the limit: gland fill below the lower limit (cold end) |
| `cpk_fill_min` | C_pk: gland fill below the lower limit (cold end) |
| `p_stretch` | Fraction outside the limit: installed stretch outside its limits |
| `cpk_stretch` | C_pk: installed stretch outside its limits |
| `p_gap` | Fraction outside the limit: extrusion gap above the permitted gap |
| `cpk_gap` | C_pk: extrusion gap above the permitted gap |
| `od_comp_nom` | Ring OD compression against the groove, mean dimensions |
| `od_comp` | Largest ring OD compression against the groove |
| `c_c` | Smallest OD compression at the cold end (thickens the section) |
| `c_h` | Largest OD compression at the hot end (thickens the section) |
| `p_od` | Fraction outside the limit: ring OD compression above the limit |
| `cpk_od` | C_pk: ring OD compression above the limit |

Squeeze `squeeze_nom` (mean dimensions, installed), `squeeze_min`, `squeeze_max`; fill `fill_min`, `fill_max`; stretch `stretch_nom`, `stretch_min`, `stretch`, `X_c` (cold end), `u_stretch` (against `x_hi`), `red_nom`; rod seals and internal-pressure face seals `od_comp_nom`, `od_comp`, `c_c`, `c_h`; gap `gap_min`, `gap_max`, `gap_allow`, `u_gap`; Monte Carlo `p_sq_min`, `p_sq_max`, `p_fill_max`, `p_fill_min`, `p_stretch`, `p_od` (rod, internal face), `p_gap` and `cpk_*` for each. A contribution Pareto of the minimum-squeeze spread. **Ports.** `squeeze_min`, `squeeze_max`, `fill_max`, `stretch` (`seal.squeeze_min`, `seal.squeeze_max`, `seal.fill_max`, `seal.stretch`), all in %. **Checks (PASS/FAIL).** Squeeze minimum and maximum within the band; fill maximum and minimum within the band; installed stretch not above its limit, also at the cold end, and not below its lower limit (not for an internal-pressure face seal); ring OD compression not above `od_hi` (rod, internal face), installed and at the hot end, where the ring has grown most against the groove (`c_h`, L-d); extrusion gap within the permitted gap, or the failing "Extrusion verified" check when no limit is entered. **Warnings.** `measured`, `extrusion`, `extrusion-range`, `expansion` (ring expands less than the groove metal: the cold and hot ends are not the bounds), `stretch`, `contact` (minimum squeeze not above zero), `face` (a dynamic face seal), `worst-case`, tabulated values not from a record.

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `gland.squeeze_min` | `squeeze_min` | % | Smallest squeeze over tolerance, temperature and swell (cold end) |
| `gland.squeeze_max` | `squeeze_max` | % | Largest squeeze over tolerance, temperature and swell (hot end) |
| `gland.fill_max` | `fill_max` | % | Largest gland fill (hot end, unstretched section) |
| `gland.stretch_max` | `stretch` | % | Largest installed stretch of the ring inside diameter |

## Checks

Checks the sheet makes:

- Squeeze minimum against the lower band limit (oring.criteria record)
- Squeeze maximum against the upper band limit (oring.criteria record)
- Gland fill maximum against the upper band limit (oring.criteria record)
- Gland fill minimum against the lower band limit (oring.criteria record)
- Installed stretch against the upper limit (oring.criteria record; PTD 5705 installation rule)
- Installed stretch at the cold end against the upper limit (oring.criteria record)
- Installed stretch against the lower limit (oring.criteria record)
- Extrusion gap against the permitted gap (oring.extrusion record)
- Ring OD compression against the limit (oring.criteria record)
- Ring OD compression at the hot end against the limit (oring.criteria record)
- Extrusion verified against a permitted gap (oring.extrusion record)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `geometry` | Block | Conservative | Every dimension stays positive across its band, the deviations are in order, the gland has a positive depth and area and the ring section does not vanish. |
| `limits` | Block | Conservative | Each lower limit is below its upper limit and the temperature and swell ranges are in order. |
| `measured` | Warning | Conservative | A measured row names a dimension the arrangement does not use (a name that is not a dimension at all blocks). |
| `extrusion` | Warning | Conservative | No permitted extrusion gap is entered (no record rows and no value), so the extrusion is not verified and the check fails while the operating pressure is above zero. |
| `extrusion-range` | Warning | Conservative | The pressure or hardness lies outside the extrusion record; the check fails because the record cannot confirm it. |
| `expansion` | Warning | Unconservative | The ring expands less than the groove metal: the cold and hot ends are not the bounding cases. |
| `stretch` | Warning | Unconservative | The installed stretch is far beyond installation practice, or the Parker quadratic is used outside about 3 to 25 % stretch. |
| `installed-state` | Warning | Unconservative | The operating temperature range does not include the installation temperature 20 °C: the installed state can have a smaller squeeze than the cold end. |
| `contact` | Warning | Conservative | The minimum squeeze is zero or negative: the ring can lose contact. |
| `face` | Warning | Conservative | A face seal is normally a static seal. |
| `worst-case` | Warning | Unconservative | The exact corner is outside the stack worst case by more than 0.2 points, so the stack is not conservative for this geometry. |

## Limitations

A plain rectangular groove with a bottom corner radius and a circular ring: edge breaks, groove wall angles, surface finish, groove eccentricity, pressure, friction, compression set, stress relaxation and gland fill under swell beyond the volume swell entered are not modelled. Squeeze and fill are bounded at the cold and hot ends only (the model is monotonic in temperature and swell when the ring expands more than the groove metal). The worst case is a stack of independent ends and the capability index is first-order in the tolerances; the Monte Carlo is exact in the model. The reduction relation applies to a ring stretched uniformly; the quadratic of Parker ORD 5700 section 3.5 is as quoted in Addendum B and used for about 3 to 25 % only. The gap does not model the bore's dilation by itself (it is entered) or a groove-side metal different from the mating part. The extrusion limit is the permitted gap from the user's record: no handbook chart is shipped. A face seal is entered with its depth and gap directly and has no coupling between the seat diameter and the depth; the internal-pressure face seal takes its ring OD seat from `D_g`. Not a seal-maker's design review: the compound, the surface finish, the lead-in chamfer and the dynamic duty (friction, wear, spiral failure) are for the seal maker.

## References

- Parker O-Ring Handbook ORD 5700, §3.5: Cross-section reduction with stretch (relation quoted second-hand; not confirmed).
- Parker PTD 5705: O-ring installation guidance (stretch rule quoted second-hand; not confirmed).
- ISO 3601-1:2012: O-rings: inside diameters, cross-sections, tolerances and size codes.
- ISO 3601-2:2016: O-rings: housing dimensions for general applications.

## Test cases

### Notes on the cases

Analytic, recomputed in python3 (Confirmed): `ka-thermal-swell` (the defaults: squeeze 16.956 % to 26.957 %, fill 63.951 % to 84.643 % on the volume basis, cold-end stretch 4.4072 %, gap 0.106076 mm at 80 °C), `ka-worst-case-hand` (no reduction, no thermal change: squeeze 19.17647 % to 25 %, fill 67.5132 % to 78.6440 %, stretch 2.0833 % to 3.8306 %, gap 0.106 mm, all by hand arithmetic), `ka-rod-seal` (OD thickening: squeeze_max 26.820 %, fill_max 84.566 %), `ka-face-seal` (compression height `h_f + e_f` = 2.80 to 2.90 mm: squeeze 13.0242 % to 24.8434 %, fill 60.2886 % to 79.9484 %, against 16.0233 % to 26.1855 % and 62.4444 % to 81.4038 % with the depth alone), `ka-face-seal-internal` (pressure inside: OD compression 1.477 % at most; squeeze 13.7030 % to 25.5848 %, fill 61.2408 % to 81.1470 %), `ka-gap-over-temperature` (aluminium bore round a steel piston, 0.02 mm dilation: gap 0.163661 mm), `ka-incompressible-reduction` (r = 1 − 1/√1.04 = 1.94193 % at 4 % stretch), `ka-linear-reduction` (ν·X = 1.96 %), `ka-extrusion-record` (interpolation in an illustrative record invented for the test: 0.1575 mm at 80 Shore A and 150 bar), and two seeded Monte Carlo cases, `ka-monte-carlo-seeded` (20 000 samples, seed 7, normal tolerances, Parker quadratic) and `ka-monte-carlo-measured` (10 000 samples, seed 11, triangular tolerances with a measured groove and ring section), whose fractions are checked against a python3 re-implementation of the engine's xoshiro256** sampler on the exact non-linear criteria, and whose capability indices against the linearised stacks (the counts agree exactly; the nearest sample to any limit is more than 3e-7 away, far above rounding). **Unconfirmed + Secondary:** see *Definition of done* below.

- `ka-thermal-swell`: Piston seal with the constant-volume reduction, thermal expansion and swell: the defaults (analytic) (Confirmed; source: Analytic worst case, recomputed in python3)
- `ka-worst-case-hand`: Piston seal, hand-calculated worst case: no reduction, no thermal change (analytic) (Confirmed; source: Analytic worst case by hand, recomputed in python3)
- `ka-rod-seal`: Rod seal: ring stretched on the rod, ring OD compressed in the housing groove (analytic) (Confirmed; source: Analytic worst case, recomputed in python3)
- `ka-face-seal`: Face seal: ring stretched onto the groove seat, depth and gap entered directly (analytic) (Confirmed; source: Analytic worst case, recomputed in python3)
- `ka-face-seal-internal`: Face seal with the pressure inside: the ring OD seats on the groove outer wall and is compressed, not stretched (analytic) (Confirmed; source: Analytic worst case, recomputed in python3)
- `ka-gap-over-temperature`: Extrusion gap over temperature with a bore of another metal and the bore dilation under pressure (analytic) (Confirmed; source: Analytic worst case, recomputed in python3)
- `ka-incompressible-reduction`: Cross-section reduction of constant-volume rubber at 4 % stretch (analytic) (Confirmed; source: Analytic: r = 1 − 1/√(1 + X), derived from constant volume)
- `ka-linear-reduction`: Cross-section reduction linear in stretch at 4 % stretch (analytic) (Confirmed; source: Analytic: r = ν·X, small-strain Poisson contraction)
- `ka-monte-carlo-seeded`: Seeded Monte Carlo, normal tolerances, Parker quadratic: the fractions outside each limit (analytic) (Confirmed; source: Python3 re-implementation of the engine's xoshiro256** sampler on the exact non-linear criteria, and of the linearised stacks for C_pk)
- `ka-monte-carlo-measured`: Seeded Monte Carlo, triangular tolerances with a measured groove and ring section (analytic) (Confirmed; source: Python3 re-implementation of the engine's sampler on the exact non-linear criteria, and of the linearised stacks for C_pk)
- `ka-extrusion-record`: Permitted extrusion gap interpolated in hardness and pressure from a user record (analytic) (Confirmed; source: Analytic interpolation of an illustrative record invented for this test (not a handbook chart))
- `ka-parker-quadratic`: Parker ORD 5700 section 3.5 quadratic at 4 % stretch, as quoted second-hand (Unconfirmed; source: Parker Hannifin, O-Ring Handbook ORD 5700, section 3.5, relation as quoted second-hand)
- `ka-stretch-rule`: Installation rule: up to 6 % stretch on a piston groove (5.8 % reads 0.967 of the limit) (Unconfirmed; source: Parker Hannifin, PTD 5705, installation guidance, rule as quoted second-hand)

## Changes

**Changes in 1.5.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The inputs sit in the groups Setup, Ring, Gland, Conditions, Stretch, Requirements, Extrusion and Statistics; the optional `alpha_s` is last in Conditions and `seed` has a usual range. The ten checks read "Squeeze minimum against the lower band limit" and the like, and the ports are `gland.squeeze_min`, `gland.squeeze_max`, `gland.fill_max` and `gland.stretch_max`. The scope is two sentences, the 33 assumptions are one short sentence each, the messages are one or two short sentences, and the record kind of the bands is titled "Squeeze, fill and stretch criteria". ISO 3601-1 and ISO 3601-2 carry their year. The pressure column `p` of the extrusion table keeps its key, because a saved table and its CSV use it. The two manufacturer references stay as they were: the pattern for book citations needs an edition ordinal that the handbook does not give, and the brand rule does not fit a citation whose author is the manufacturer.

**Conservatism (L-e).** Where the sheet makes a simplification it takes the side of the larger fill and squeeze maxima and the smaller minima: the hot-end fill uses the unstretched section (no reduction by stretch), the larger of the held-circumference and compression factors, and the section thickened by the largest compression; the cold-end minima use the smallest compression (none if the ring OD is clear of the groove) and the largest stretch; the worst case combines every dimension at its end independently, so a dimension that appears twice (the groove root) is conservative, and it is never less conservative than the exact corner (the `worst-case` warning checks the squeeze). The Monte Carlo and C_pk do not carry these margins: they are the probability of the model as it is, so a worst case that passes can still show a small tail.

**Changes in 1.4.0 (fp.oring-gland-02, -03, -R1).** -02: the new input `ecc` (mm, default 0) is the one-sided eccentricity or run-out of the gland. It deepens the gland by `ecc` where the squeeze is least (the cold end, so the minimum squeeze and the minimum fill fall) and shallows it by `ecc` where the squeeze is most (the hot end, so the maximum squeeze and fill rise), in the sheet, the exact-corner check, the linearisation and the Monte Carlo. With every tolerance zero, 3.5 mm ring, 56.9 mm bore, 51.5 mm groove root, −20 to 80 °C and 5 % swell: squeeze 21.055 % to 24.534 % with no offset, 19.593 % to 25.930 % with `ecc` 0.05 mm (fill 70.98 % to 80.01 % against 69.69 % to 81.52 %). -03: the permitted gap (`gap_allow`, the `gap` column of the extrusion record) and the gap outputs say "diametral for a radial seal, axial for a face seal": the sheet compares it with the diametral clearance `D_s − D_c`, so a radial chart value is entered doubled. -R1: when the operating range does not include the 20 °C of installation the sheet warns (`installed-state`): the installed state (no swell, 20 °C) can have a smaller squeeze than the cold end (T_min 60 °C, T_max 100 °C, no swell: 18.32 % at the cold end against 17.64 % installed).

**Definition of done (Addendum B §A3, §B4).** Every case above that is analytic is Confirmed; no published known-answer case is shipped, and none is invented. **Unconfirmed / Secondary items to list in the owner test pack:** `ka-parker-quadratic` (Unconfirmed, Secondary: Y at 4 % stretch is 2.8464 %, the arithmetic of the relation as Addendum B quotes it from Parker ORD 5700 section 3.5; the 3 to 25 % range, the sub-3 % blend and what Y measures are to be checked against the owner's copy of the handbook) and `ka-stretch-rule` (Unconfirmed, Secondary: the installation rule of up to 6 % piston-groove stretch, quoted from Parker PTD 5705; 5.8 % reads 0.967 of the limit; to be checked against the owner's copy, including the edition). **Not pinned (owner check):** the Parker ORD 5700 design examples of sections 3.5 to 3.7 — their inputs and answers are not available, so no published example is pinned.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Static piston O-ring seal of a 63 mm locking cylinder: squeeze, fill, stretch and extrusion over tolerance, temperature and swell ([sheet](../../worked-examples/fluid-power/fp.oring-gland/locking-cylinder-piston-seal/sheet.txt)).

```text
v_W = 3.5 mm nominal → mid 3.5 ± 0.1 mm, σ 0.033333 mm = 3.5 mm
v_d = 56 mm nominal → mid 56 ± 0.45 mm, σ 0.15 mm = 56 mm
v_Ds = 63 mm nominal → mid 63.023 ± 0.023 mm, σ 0.0076667 mm = 63.023 mm
v_Dg = 57.5 mm nominal → mid 57.463 ± 0.037 mm, σ 0.012333 mm = 57.463 mm
v_Dc = 63 mm nominal → mid 62.955 ± 0.015 mm, σ 0.005 mm = 62.955 mm
v_b = 4.7 mm nominal → mid 4.75 ± 0.05 mm, σ 0.016667 mm = 4.75 mm
f_c = (1 + 0.00015·(-25 − 20))·(1 + 0)^(1/3) = 0.99325
g_c = 1 + 0.000012·(-25 − 20) = 0.99946
f_h = (1 + 0.00015·(70 − 20))·(1 + 0.04)^(1/3) = 1.0208
g_h = 1 + 0.000012·(70 − 20) = 1.0006
stretch_nom = 57.463/56 − 1 = 2.6125 %
stretch = 57.5/55.55 − 1 = 3.5104 %
stretch_min = 57.426/56.45 − 1 = 1.729 %
red_nom = r(2.6125 %) = 1.2812 %
… 37 more step(s) on the sheet
Governing: PASS  Squeeze minimum against the lower band limit: squeeze_min 15.125 % vs ≥ 15 %  « oring.criteria record
```
<!-- hand-arithmetic:end -->
