# Snap-fits (`mech.snap-fit@1.5.0`)

**In plain terms.** A snap fit is a plastic hook that bends out of the way as a part is pushed on and springs back into place. The risk is straining the plastic past what it can take, once or repeatedly. The worksheet takes the hook's shape, the material and the deflection needed to assemble it, and works out the strain at its root and the force to push it on and to take it off, for cantilever, tapered, ring and torsion forms.

## Scope

Calculates the strain, deflection force and joining forces of a plastic snap-fit: a cantilever lug, an annular ring or a torsion bar. It checks the strain against the permissible strain and whether the joint is self-locking.

The sheet has five forms, chosen by `mode`: a cantilever lug of constant section, of tapered thickness or of tapered width, an annular (closed ring) snap, and a torsion bar with a lever. For each it gives the strain at the critical section against the permissible strain of the plastic (one-time or repeated joining; FAIL above 1), the deflection force from beam theory with the secant modulus, the assembly force over the entry face, the separation force over the return face with Coulomb friction, the self-locking flag, the permissible deflection, and an always-shown root-fillet advisory.

No plastics data are shipped (Addendum B §B5): `Es`, the permissible strains, `ν`, `μ`, `K_ann` and `K_F` have no default. **Attribution:** the sheet cites the design guide (Covestro, *Snap-Fit Joints for Plastics: A Design Guide*, Ch. B) by section and title only, and reproduces none of its text, charts or tables. It cites the guide only for the relations the spec quotes (the constant-section strain and force, `γ_perm ≈ ε_perm·(1 + ν)`, `γ = φ·r/l`, the joining forces and the self-locking condition). The tapered-beam factors, the annular force and the torsion torque and force are derived here and cited as derived.

## Method

Euler–Bernoulli beam theory. Position `t = x/l` from the root; tip load `F_defl`; moment `M = F_defl·l·(1 − t)`; surface strain `ε(t) = M·(h/2)/(E·I)`; tip deflection `y = ∫ M·m/(E·I) dx` with the unit-load moment `m = l·(1 − t)`.
- *Constant section.* `ε = 6·F_defl·l/(E·b·h²)` at the root, so `F_defl = (b·h²/6)·(Es·ε/l)` (the spec form) and `y = 4·F_defl·l³/(E·b·h³) = (2/3)·ε·l²/h`: the geometry factor is **`K_y = 2/3`**; the design guides print the rounded 0.67 (0.5 % higher). The sheet uses the derived 2/3.
- *Tapered thickness* `h(t) = h·(1 − a·t)`, `a = 1 − r_h`, `r_h = h_tip/h` (default 0.5, "h tip = h/2"), width constant. Strain shape `φ(t) = (1 − t)/(1 − a·t)²`, `ε_max = (6·F_defl·l/(E·b·h²))·φ_max`, so `F_defl = b·h²·Es·ε/(6·l·φ_max)`. Deflection `y = 12·F_defl·l³·J/(E·b·h³)` with `J = ∫₀¹ (1 − t)²/(1 − a·t)³ dt = [2r − r²/2 − ln r − 3/2]/a³` (`r = r_h`). Eliminating `F_defl`: **`K_y = 2·J/φ_max`**, `y = K_y·ε_max·l²/h`. The strain is largest at the root when `a ≤ 1/2` (`r_h ≥ 1/2`, `φ_max = 1`); for a steeper taper `φ'(0) > 0` and the peak is inside the beam at `t* = (2a − 1)/a`, `φ_max = (1 − t*)/(1 − a·t*)²` (sheet outputs `t_crit`, `phi_max`). `r_h = 0.5`: `J = 0.545177`, **`K_y = 1.09035`**.
- *Tapered width* `b(t) = b·(1 − a·t)`, `a = 1 − r_b` (default 0.5), thickness constant. `φ(t) = (1 − t)/(1 − a·t)`, largest at the root for every `a`, so `F_defl = b·h²·Es·ε/(6·l)` (as the constant section); `J = ∫₀¹ (1 − t)²/(1 − a·t) dt = [1/2 − 2r + 3r²/2 − r²·ln r]/a³`, **`K_y = 2·J`**. `r_b = 0.5`: `K_y = 0.77259`; `r_b = 0.25`: `K_y = 0.85520`.
- Both tapers join the constant section at a ratio of 1 (`K_y = 2/3`, tested). Below a taper of 0.02 the closed forms lose digits to cancellation, so `J` is integrated numerically there (Simpson, 2000 intervals; the switch is seamless, tested).
- *Strain from the deflection.* `ε = y·h/(K_y·l²)`; permissible deflection `y_perm = K_y·ε_perm·l²/h`.
- *Annular (derived, a free thin closed ring; not a relation of the design guide).* The **nominal** hoop strain of the mean diameter is `ε_nom = ΔD/D` (`ΔD` = interference on the diameter, `D` = mean diameter). The strain checked is the **larger of** `K_ann·ε_nom` and the inner-surface strain `ΔD/(D − h)`, with `K_ann` the user's strain factor (record `snapfit.annular-factor`, **no default**, required in this mode only; 1 for a plain free ring). The force comes from the nominal strain only: hoop force `Es·ε_nom·h·b` (`h` wall, `b` the **effective axial length of a free ring**, not the lip of a longer tube), radial contact force summed round the ring `F_defl = K_F·2π·Es·ε_nom·h·b`, with `K_F` the user's optional force factor (record `snapfit.annular-force-factor`, **no default**, blank is 1). A tube behind the ring stiffens the joint and raises the force; only `K_F` can represent that, and `K_ann` does not touch the force. The strain on the inner (contact) surface is `ΔD/(D − h)`, slightly above `ΔD/D` (output `eps_inner`); with `K_ann` below `D/(D − h)` (for example 1) it is the one checked. A wall above a tenth of the mean diameter (`h/D > 0.1`) is refused (`thick-ring`, 1.4.0): the thin-ring model has run out. `Es(ε)` (typed or from the W3 points) is the secant modulus **at the nominal hoop strain `ε_nom = ΔD/D`**, the strain that drives the force. `ΔD_perm = ε_perm·min(D/K_ann, D − h)`. Cross-check: the work of the radial force over the radial growth `ΔD/2` equals the hoop strain energy `½·Es·ε_nom²·π·D·h·b` (tested).
- *Torsion bar* (solid round, radius `r`; lever `a`; lever-end deflection `y`; `bar_support`): `φ = y/a` (small angle; WARN above 20°). **Held at one end** (default; `l` = the free length): `l_eff = l`. **Held at both ends with the lever in the middle** (`l` = the whole length between the supports): two halves twist in parallel, `l_eff = l/2`, `n = 2`. Then `γ = φ·r/l_eff` (both ends: `2φr/l`), equivalent tensile strain `ε = γ/(1 + ν)`, `γ_perm = ε_perm·(1 + ν)`, utilisation `γ/γ_perm = ε/ε_perm`; `G = Es/(2(1 + ν))`, `T = n·G·(π r⁴/2)·φ/l_eff`, `F_defl = T/a` (both ends: `F_defl = 4·G·J·φ/(l·a)`); `y_perm = γ_perm·l_eff·a/r`. For the same total length the bar held at both ends has twice the strain and four times the force of one held at one end. Cross-check: the strain energy `G·γ²·π·r²·l/4` equals `F_defl·y/2` for either support (tested). The torque, force and two-ended case are derived; `γ = φ·r/l` and `γ_perm` are the brief's design-guide relations, to be checked.
- *Assembly and separation.* `W = F_defl·(μ + tan α)/(1 − μ·tan α)` over the entry face (angle `α` to the joining direction), `W′ = F_defl·(μ + tan α′)/(1 − μ·tan α′)` over the return face (`α′`). **Self-locking** when `α′ ≥ arctan(1/μ)` (`π/2` when `μ = 0`): the separation force is infinite, the sheet prints `self-locking (infinite)` instead of a number and warns (rule `self-locking`; right for a permanent joint, wrong for a serviceable one); `alpha_lock = arctan(1/μ)` is always reported. The retention port has no number in that case, so a link to `retention_force` is refused by the linking service ("has no numeric output") and a pinned link goes stale ("no longer gives"); tested in the Library suite. Assembly **jams** when `μ·tan α ≥ 1`: no number, WARN `assembly-jam` and a failed check.
- *Secant modulus.* Typed (`Es`, no default), or interpolated linearly from the **W3 secant-modulus points `Es(ε)`** (`Es_pts`, `MaterialProperty` `Es_curve`, ≤ 50 rows, in any order, a point at zero strain allowed) at the design strain; outside the points the end value is held and the sheet warns (`es-range`), with no extrapolation. A usable row has a strain of zero or more and a positive modulus (`es-curve` blocks otherwise).
- *Temperature.* `T_use` is recorded on the sheet with the note that `Es` and the permissible strains must be the values at that temperature. The W3 points and strain limits are taken as entered; the sheet cannot check their temperature.
- *Root fillet.* An always-present advisory (`fillet_advisory`): the strain is nominal; a sharp corner at the root concentrates it; fillet generously and apply your own concentration factor if the radius is small. When a steep thickness taper puts the strain peak inside the beam, the advisory names where (`t_crit·l` from the root). No concentration chart is shipped.
- *Friction.* `μ` is a typed input (record `snapfit.friction`). A W3 material can hold friction coefficients by pair (`mu_pairs`), but the sheet does not read them: a change to a W3 friction pair does **not** make the sheet stale. Read `μ` from the pair and type it, with its source.

**Reference diagram.** The worksheet draws the form that `mode` selects. A cantilever of constant section, tapered thickness or tapered width binds `l`, `h`, `b`, `y`, `alpha`, `alpha_r`, `h_ratio` and `b_ratio`. An annular ring binds `D_ring`, `h`, `dD`, `b` and `K_ann`. A torsion bar binds `r_bar`, `a_arm`, `l`, `y`, `bar_support` and `nu`. `Es`, `eps_perm_once`, `mu`, `T_use` and `n_latch` are labels. The drawing is not to scale (ADR-0158, DI-08).

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `mode` | Form of snap-fit | choice | Setup |
| `bar_support` | How the torsion bar is held: at one end, or at both ends with the lever in the middle (twice the strain and four times the stiffness of one end for the same total length) | choice | Setup |
| `l` | Length of the beam (cantilever); of the torsion bar: its free length when held at one end, or its whole length between the two supports when held at both ends with the lever in the middle | mm | Geometry |
| `h` | Thickness at the root (cantilever); wall thickness of the ring (annular) | mm | Geometry |
| `b` | Width at the root (cantilever); effective axial length of the ring (annular: that of a free ring, not the lip of a longer tube) | mm | Geometry |
| `y` | Deflection at the tip (cantilever) or at the end of the lever (torsion): the undercut to be cleared | mm | Geometry |
| `h_ratio` | Tip thickness / root thickness (tapered thickness) | none | Geometry |
| `b_ratio` | Tip width / root width (tapered width) | none | Geometry |
| `D_ring` | Mean diameter of the ring (annular) | mm | Geometry |
| `dD` | Interference on the diameter: how much the ring is widened to pass the undercut (annular) | mm | Geometry |
| `K_ann` | Strain factor of the annular snap on the nominal hoop strain ΔD/D (record snapfit.annular-factor; 1 for a plain free ring; no default: from your design reference) | none | Geometry |
| `K_F` | Force factor of the annular snap on the hoop force (optional; blank = 1, the free ring; record snapfit.annular-force-factor, for example the stiffening of a tube behind the ring) | none | Geometry |
| `r_bar` | Radius of the torsion bar | mm | Geometry |
| `a_arm` | Length of the lever on the torsion bar | mm | Geometry |
| `Es_source` | Where the secant modulus comes from: typed at the design strain, or interpolated from the Es(ε) points of a material record | choice | Material |
| `loading` | How often the joint is made: selects the one-time or the repeated permissible strain | choice | Material |
| `T_use` | Temperature of use. Recorded: Es and the permissible strain must be the values at this temperature | °C | Material |
| `Es` | Secant modulus at the design strain and temperature (annular: at the nominal hoop strain ΔD/D) (typed; no default: the plastic's stress–strain data) | MPa | Material |
| `Es_pts` | Secant modulus against strain, Es(ε), of the plastic at the temperature of use (from the material record) | table | Material |
| `eps_perm_once` | Permissible strain for one-time joining, as a fraction, for example 0.04, at the temperature of use (no default: from the plastic's data) | none | Material |
| `eps_perm_repeated` | Permissible strain for repeated joining, as a fraction (no default) | none | Material |
| `nu` | Poisson's ratio ν (torsion bar) | none | Material |
| `alpha` | Entry (assembly) angle α of the inclined face to the joining direction | ° | Joining |
| `alpha_r` | Return (separation) angle α′ of the retaining face (90° is a face square to the joining direction) | ° | Joining |
| `mu` | Coefficient of friction between the two parts (record snapfit.friction; no default: the pair of materials, from your reference or a W3 friction pair) | none | Joining |
| `n_latch` | Number of identical latches that share a holding load (a whole number) (optional; blank = the holding force is not worked out) | none | Holding |
| `F_req` | Force the latches must hold against, for example the pressure on a cover (optional; blank = no check; n_latch must be entered too) | N | Holding |

The material record kind `material.properties` lists `Es_pts`, `eps_perm_once`, `eps_perm_repeated` and `nu`: applying a plastic from the library fills them, each citing the material, its revision and the page of the value, and the sheet goes stale when a later revision is saved. A blank input that a form needs blocks the sheet with its name (rule `missing`). Hard limits: the lengths and sections are positive (`y` and the diameter interference `dD` may be zero), `alpha` 0 to 89°, `alpha_r` 0 to 90°, `mu` 0 to 2, and the permissible strains 10⁻⁶ to 1.

## Assumptions

- The cantilever follows Euler–Bernoulli beam theory, with a rigid root, a linear taper and small deflection.
- The secant modulus is constant over the whole beam, at the design strain.
- The strain is the nominal surface strain at the critical section, with no local concentration from the root fillet.
- `Es` and the permissible strain are the plastic's values for the temperature, loading and moisture of use.
- The annular snap is a free, thin, closed ring of effective axial length `b`.
- The nominal hoop strain is the diametral interference over the mean diameter, ΔD/D.
- The strain checked is the larger of `K_ann` times the nominal strain and the inner-surface strain ΔD/(D − h).
- The contact force is the hoop force `Es·ε_nom·h·b` taken round the ring, times the optional force factor `K_F`.
- The torsion bar is solid and round, loaded at a lever end, with a small angle φ = y/a.
- The bar is held at one end, or at both ends with the lever in the middle.
- The permissible shear strain is `ε_perm·(1 + ν)`, and the equivalent tensile strain is `γ/(1 + ν)`.
- The mating part is rigid, with Coulomb friction `μ` on the inclined face (`α` for assembly, `α′` for separation).
- The deflection force is taken at the design deflection, with no creep, stress relaxation or impact effect in service.

## Outputs

`eps_max` (%, the maximum strain), `eps_perm` (%), `strain_util` (FAIL above 1), `Es`, `F_defl` (the deflection force), `y_perm`; cantilever `l_h` (refused below 5, rule `slender`), `K_y`, `phi_max` (and `t_crit` when the peak is inside); annular `eps_nom`, `eps_inner`, `K_F`; torsion `l_eff`, `phi`, `gamma`, `gamma_perm`, `G_sec`, `T_bar`; `assembly_force`, `alpha_lock`, `retention_force` (or the self-locking text), `self_locking`, `fillet_advisory`; and, with `n_latch`, `F_hold` and `u_hold`.

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `snapfit.force_assembly` | `assembly_force` | N | Force to push the parts together over the entry face |
| `snapfit.force_retention` | `retention_force` | N | Force to separate the parts over the return face; a text with no number when the return angle locks, so a link to it is refused then |
| `snapfit.utilisation_strain` | `strain_util` | none | Strain at the critical section over the permissible strain |
| `snapfit.force_hold` | `F_hold` | N | Force the latches hold, `n_latch` times the separation force (only when `n_latch` is entered and the joint is not self-locking) |

A port keeps its output symbol as an alias, so a link, a pack binding or an inputs file written with the symbol still resolves. The retention port has no number when the joint is self-locking, so the linking service refuses a link to it ("has no numeric output"), and a pinned link goes stale ("no longer gives"); the Library suite tests this.

**Links (manual for now).** No port of `mech.tolerance-stack` is wired to this sheet: the user types the stack result into `y` (or `dD`, `l`, `h`) by hand. The undercut `y` (and the interference ΔD, the lug length and thickness) is the natural result of the tolerance stack of the mating parts. The worst-case or Monte Carlo extreme of the undercut is the `y` of this sheet: the maximum interference gives the worst strain and assembly force, and the minimum gives the worst retention. This sheet is deterministic. A Monte Carlo run of the stack engine (distributions on `y`, `l`, `h`, `mu`) runs on its own and its extremes are typed in. There is no automatic hook yet.

## Checks

- Strain at the critical section against the permissible strain: `eps_max` against `eps_perm`.
- Entry-face friction μ·tan α against the jamming limit: a yes or no check that fails when `μ·tan α` is 1 or more.
- Required holding force against the force the latches hold: `F_req` against `F_hold`, made when `n_latch` and `F_req` are entered.
- Blocks: `missing`, `geometry`, `es-curve`, `slender`, `n-latch`, `thick-ring`, `small-angle`. Warnings: `large-deflection`, `es-range`, `assembly-jam`, `hold-unused`, `self-locking`.

## Limitations

Linear beam theory with a constant secant modulus over the beam at the design strain (the strain varies along a tapered beam; `Es` is read at the peak); nominal strain without the root-fillet concentration; no creep, stress relaxation, moisture or impact-speed effect; rigid mating part; rectangular cantilever (no trapezoidal or U-shaped section, no hollow); free thin closed annular ring (the effective length `b`, `K_ann` and `K_F` stand for anything else); round solid torsion bar, held at one end or at both ends with a central lever; small twist; Coulomb friction with one coefficient for both faces. Beam theory under-predicts stiffness when `l/h < 5` (warned).

## References

- Covestro, *Snap-Fit Joints for Plastics: A Design Guide*, Ch. B: cantilever, annular and torsion snap-fit relations. The edition is not recorded, so the owner supplies it.

## Test cases

All analytic and **Confirmed**, recomputed in python3 (`m8_geom.py`, `m8_kat.py`; the geometry factors also by numerical double integration of the curvature, agreeing to 10⁻⁹): `ka-constant` (K_y = 2/3, ε = 1.33333 %, F_defl = 8.53333 N, W = 9.055114 N, arctan(1/μ) = 73.3008°, W′ = 36.096414 N), `ka-taper-thickness` (K_y = 1.0903549, F_defl = 5.217465 N), `ka-taper-thickness-deep` (r_h = 0.3: t* = 0.571429, φ_max = 1.190476, K_y = 1.2684382), `ka-taper-width` (r_b = 0.25: K_y = 0.8551983), `ka-annular` (ε_nom = 1.33333 %, ε = 1.6 %, inner surface 1.403509 %, F_defl = 1507.964 N from the nominal strain, W = 1600.171 N), `ka-annular-inner-surface` (K_ann = 1, h = 3 mm: the inner surface 1.481481 % governs, u = 0.37037, ΔD_perm = 1.08 mm, F_defl = 3015.929 N), `ka-annular-force-factor` (K_F = 1.25: F_defl = 1884.956 N, strain unchanged), `ka-torsion` (one end: γ = 0.015, γ_perm = 0.0414, T = 0.136591 N·m, F_defl = 13.659098 N), `ka-torsion-both-ends` (γ = 0.03, T = 0.546364 N·m, F_defl = 54.636394 N, y_perm = 2.07 mm; also by strain energy), `ka-curve` (Es(2 %) = 2100 MPa from three points). The worked example `enclosure-lid-hook` (tapered thickness) is in `docs/worked-examples/mechanical/mech.snap-fit/`. **Not pinned — owner check:** (1) the Covestro *Snap-Fit Joints for Plastics: A Design Guide* Section B worked example (inputs and answer not available to the build); (2) the Covestro joining-techniques guide Fig. 25 relations (the figure's exact factors are not stated here). The derived factors `K_y = 1.09` (tip thickness h/2) and `0.86` (tip width b/4) agree with the values the build remembers the design guides printing for tapers, but that is memory, not a pinned source: check both, and the guide's printed 0.67 against the derived 2/3, against the owner's copy. The annular and torsion relations are written from first principles and from the brief's `γ_perm ≈ ε_perm·(1 + ν)`; compare them with the guide's before relying on them. **For the owner:** the sheet uses the derived `K_y = 2/3` for the constant section, 0.5 % below the 0.67 the guides print; say if you want the rounded value instead.

## Changes

**Changes in 1.5.0 (C-2).** The output `P` is renamed `F_defl` and the output `eps` is renamed `eps_max` (neither is a port, so no saved link names them). The inputs are in the groups Setup, Geometry, Material, Joining and Holding, with the optional inputs last in each. The three checks show physical values and the checks are named against their limits. The four ports have public names `snapfit.*`. The scope, the assumptions and every message are rewritten in short sentences. No result changes.

**Changes in 1.3.1 (mech.snap-fit-01).** A cantilever whose deflection is more than 0.3 of its length (`y/l > 0.3`) raises the warning `large-deflection`: linear beam theory under-predicts the strain and the force at that size. A large-deflection (elastica, fixed-direction tip load) solution compared at the same deflection gives strain +1.6 % and force +4.0 % at y/l 0.2, +4.2 % and +10 % at 0.3, +8.5 % and +22 % at 0.41 (l 20, h 1.2, b 6, y 8.2 mm, E_s 1800 MPa, ε_perm 4 %: beam theory ε 3.69 %, u 0.92, P 4.78 N; the exact solution about ε 4.00 %, u 1.0, P 5.8 N). The numbers of the sheet are unchanged.

**Changes in 1.4.0 (C-2 fix, F2).** Three notices that said the model no longer holds, and let the sheet pass, are now blocks: `slender` (a beam under five thicknesses, l/h < 5: beam theory under-states the stiffness and the strain), `thick-ring` (a ring wall above a tenth of the mean diameter) and `small-angle` (a torsion-bar twist y/a above 20°: the small-angle relation φ = y/a is not good). Each is now declared unconservative and is a block, with the message naming the input (`l`, `h`, `y`). Nothing else changes: a sheet inside those limits gives the same numbers as 1.3.2, and no known-answer case or worked example is outside them.

**Changes in 1.2.0:** the annular check takes the larger of `K_ann·ε_nom` and the inner-surface strain, `ΔD_perm` follows, `thick-ring` refuses `h/D > 0.1`; `Es` is documented as read at `ε_nom` for the annular snap; the Es interpolation is cited as "W3 material points (linear interpolation)" and `φ = y/a` as derived. `ka-annular` and the other existing values are unchanged (there `K_ann·ε_nom` governs).

**Changes in 1.1.0** (Opus review): the torsion bar can be held at both ends; the annular force no longer scales with `K_ann` (new optional `K_F`; `K_ann` has no default and is required in the annular mode only; inner-surface strain shown); derived relations are no longer cited to the guide; a W3 point at zero strain is accepted; the fillet advisory names an inside strain peak; `self_locking` reads `No` / `Yes: …`. `ka-annular` moved (P, W, W′: the force now uses the nominal strain); every other existing value is unchanged.

**Holding a load with several latches (1.3.0).** Optional `n_latch` (a whole number of identical latches sharing the load equally) gives `F_hold = n_latch·W′` (port, N; not offered for a self-locking joint, whose holding force is infinite); optional `F_req` (N; may be linked from E2's `F_seal`, the force of a sealed enclosure's pressure on its cover) adds the check "Latches hold the load: F_req ≤ n·W′" (`u_hold = F_req/F_hold`). A load without a count warns (`hold-unused`); a fractional count blocks (`n_latch:`). Test case `ka-holding` (four latches of `ka-constant`, 100 N: F_hold 144.38565 N, u 0.69259).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Enclosure lid hook: cantilever snap-fit, thickness tapered to half ([sheet](../../worked-examples/mechanical/mech.snap-fit/enclosure-lid-hook/sheet.txt)).

```text
l_h = 18 mm / 2.2 mm = 8.1818
J = r = 0.5 = 0.54518
phi_max = a = 0.5 = 1
K_y = 2 × 0.54518 / 1 = 1.0904
eps_max = 1.2 mm × 2.2 mm / (1.0904 × (18 mm)^2) = 0.74729 %
eps_perm = 0.04 = 4 %
F_defl = 7 mm × (2.2 mm)^2 × 2000 MPa × 0.74729 % / (6 × 18 mm × 1) = 4.6886 N
y_perm = 1.0904 × 4 % × (18 mm)^2 / 2.2 mm = 6.4232 mm
strain_util = 0.74729 % / 4 % = 0.18682
tan_a = tan(30 deg) = 0.57735
mu_tan_a = 0.35 × 0.57735 = 0.20207
assembly_force = 4.6886 N × (0.35 + 0.57735) / (1 − 0.20207) = 5.4491 N
alpha_lock = arctan(1/0.35) = 70.71 deg
tan_ar = tan(65 deg) = 2.1445
… 1 more step(s) on the sheet
Governing: PASS  Strain at the critical section against the permissible strain: 0.7473 % vs 4 %  utilisation 0.19  « Covestro, Snap-Fit Joints for Plastics: A Design Guide
```
<!-- hand-arithmetic:end -->
