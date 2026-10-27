# Pipe expansion, loops and anchor forces (screening) (`bs.pipe-expansion@1.5.1`)

**In plain terms.** A hot pipe grows longer, and if its ends are held it pushes on its anchors or buckles; a cold pipe shrinks and pulls. The worksheet works out the growth between anchors from the temperature change, the force and stress if the pipe is fully restrained, and the length of an offset or loop that absorbs the growth within a stress range you allow. It is a quick screening, not a piping stress analysis.

## Scope

Calculates the thermal growth of a pipe run between anchors, the leg or loop that absorbs it, and the force and moment on the anchors. It is a screening calculation, not a piping stress analysis.

**Reference diagram.** The worksheet draws the layout the choice selects: an offset leg as an L-bend (L2 blank) or a Z-bend, a U-loop, or an axial bellows between two anchors. The pipe section is drawn beside it. Bound inputs: OD, t_wall, L, H, L2, alpha, mu, k_b, E, Sa, T_install, T_operate, dT_in, and for the bellows P_b, A_eff and dL_rated. Not to scale; inputs only.

## Method

- Pipe section: d_i = OD − 2·t; A_m = π/4·(OD² − d_i²); I = π/64·(OD⁴ − d_i⁴); Z = I/(OD/2)
- Temperature change and growth: ΔT = T_operate − T_install, or as entered; L_tot = L, or L + L2 for a Z-bend; ΔL = α·L_tot·|ΔT|, its sign giving the direction
- Fully restrained pipe: σ_r = E·α·|ΔT| (E at the installation temperature); F_r = E_r·α·|ΔT|·A_m (E_r = E_hot, else E)
- Guided cantilever, required leg: Δ = ΔL for an offset leg or Z-bend and ΔL/2 for a U-loop; L_req = √(3·E·OD·Δ/Sa); PASS when L_req ≤ the available leg H
- Force and moment of the available leg: F_leg = 12·E_r·I·Δ/H³ (along the run); M_leg = 6·E_r·I·Δ/H² (= F_leg·H/2); σ_leg = 3·E·OD·Δ/H² = Sa·(L_req/H)²
- Runs of an offset leg or Z-bend: the runs bend for the leg's growth Δ_leg = α·|ΔT|·H under one force F_run = 12·E_r·I·Δ_leg/(L³ + L2³) along the leg (L2 = 0 for an L-bend); run i takes L_i³/(L³ + L2³) of Δ_leg, with σ_i = 3·E·OD·Δ_i/L_i² and M_i = F_run·L_i/2, and passes when √(σ_i/Sa) ≤ 1
- Force on the anchors: √(F_leg² + F_run²) from the two perpendicular components; the moment reported is the largest root moment of the legs and runs
- Screening and bellows: OD·Y/(L_dev − U)² ≤ K1 with Y = α·|ΔT|·U (offset leg or Z-bend L_dev = L_tot + H, U = √(L_tot² + H²); U-loop L_dev = L + 2·H, U = L); axial bellows anchor force = P·A_eff + k·ΔL, with no leg check and no screening

### Notes on the method

`d_i = OD − 2t`, `A_m = π/4(OD² − d_i²)`, `I = π/64(OD⁴ − d_i⁴)`, `Z = I/(OD/2)`. `ΔT = T_operate − T_install` (or entered; negative for a cold line). `ΔL = α·L·|ΔT|` (magnitude; `dL_signed = α·L·ΔT` and the text `direction` give the sign: growth presses on the anchors, contraction pulls). Fully restrained: `σ_r = E·α·|ΔT|`, `F_r = E_r·α·|ΔT|·A_m`, `u_res = σ_r/Sa` (information: above 1 the movement has to be absorbed).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `layout` | What absorbs the movement |  | offset leg (L- or Z-bend: the leg takes the whole movement) | offset leg (L- or Z-bend: the leg takes the whole movement), U-loop (two legs, each takes half the movement), axial bellows (no leg or loop: the bellows absorbs the movement) |  | Setup |
| `temp_mode` | Temperature change from |  | install and operating temperatures | install and operating temperatures, temperature change entered |  | Setup |
| `OD` | Pipe outside diameter | mm | 88.9 | 10 to 1500 | 1 to 10000 | Geometry |
| `t_wall` | Pipe wall thickness | mm | 3.2 | 0.5 to 60 | 0.1 to 1000 | Geometry |
| `L` | Anchor spacing: the straight length that grows (an offset leg or Z-bend: the first run, which also bends for the leg's growth) | m | 50 | 1 to 500 | 0.1 to 10000 | Geometry |
| `H` | Available leg length (each leg of a U-loop; an offset leg: the leg off the run, which may be the longer of the two); optional for an axial bellows, which has no leg; no default | m | optional | 0.5 to 50 | 0.01 to 1000 | Geometry |
| `L2` | Second run of a Z-bend, on the other side of the leg (optional; blank = L-bend; offset-leg layout only) | m | optional | 1 to 500 | 0.1 to 10000 | Geometry |
| `E` | Young's modulus of the pipe material at the installation (reference) temperature, for the stress check, in GPa (steel 210, copper 120, PE and PP about 1; not MPa) | GPa | 210 | 0.5 to 500 | 0.1 to 1000 | Material |
| `alpha` | Mean coefficient of thermal expansion between the install and operating temperatures | um/(m·K) | 12 | 1 to 250 | 0.1 to 1000 | Material |
| `E_hot` | Young's modulus at the operating temperature, for the reactions on the anchors, in GPa (optional; blank = E); do not use it for the stress check | GPa | optional | 0.5 to 500 | 0.1 to 1000 | Material |
| `T_install` | Temperature at installation (when the run is anchored) | °C | 10 | -50 to 50 | -100 to 500 | Conditions |
| `T_operate` | Operating temperature (hot or cold) | °C | 80 | -50 to 500 | -200 to 1000 | Conditions |
| `dT_in` | Temperature change ΔT (negative for a cold line), when entered directly (optional; blank = from the two temperatures) | K | optional | -300 to 500 | -300 to 1000 | Conditions |
| `Sa` | Allowable displacement-stress range for the legs and runs (your piping code and its formula; optional for an axial bellows; no default) | MPa | optional | 5 to 500 | 0.1 to 5000 | Requirements |
| `K_scr` | Screening limit K1 for OD·Y/(L − U)², in mm²/m² (or in²/ft², …) from your piping code (optional; blank = screening not evaluated, so the sheet does not pass; no default) | mm²/m² | optional | 1 to 1000 | 1e-06 to 1000000000 | Requirements |
| `P_b` | Axial bellows governing pressure, gauge, at or above zero: the design or test pressure that governs the anchor sizing (a vacuum bellows is outside this sheet; compression and extension ratings can differ, enter the governing one) | kPa | optional | 10 to 4000 | 0 to 100000 | Bellows |
| `A_eff` | Axial bellows effective area (manufacturer's data) | cm² | optional | 1 to 5000 | 0.01 to 1000000 | Bellows |
| `k_b` | Axial bellows spring rate (optional; blank = no spring force; manufacturer's data) | kN/m | optional | 1 to 5000 | 0 to 1000000 | Bellows |
| `dL_rated` | Rated axial movement of the bellows (optional; blank = movement not checked; manufacturer's data) | mm | optional | 1 to 500 | 0.01 to 10000 | Bellows |
| `mu` | Friction coefficient μ of the slides and guides between the anchor and the loop (optional; blank = friction is not in the anchor force) |  | optional | 0.05 to 1.5 | 0 to 5 | Support friction |
| `w_pipe` | Weight of the pipe per metre with its contents, in kg/m (enter with μ) | kg/m | optional | 0.1 to 2000 | 0 to 100000 | Support friction |
| `L_slide` | Length of pipe on sliding supports between the anchor and the loop (optional; blank = the anchor spacing L) | m | optional | 1 to 500 | 0 to 10000 | Support friction |

## Assumptions

1. The result is a screen, not a piping stress analysis. Basis: Stated scope.
2. The result tells whether a simple leg or loop is plausible and what it puts on its anchors. Basis: Stated scope.
3. It does not replace a flexibility analysis of the real routing, including elbow flexibility, intensification, torsion, branches, supports and loads. Basis: Stated scope.
4. A guided cantilever is straight, uniform, fixed against rotation at both ends and loaded only by the movement it absorbs. Basis: Beam theory.
5. Its stiffness is 12·E·I/H³ and its end moment is F·H/2. Basis: Beam theory.
6. Elbows are taken as rigid and the leg as slender. Basis: Beam theory.
7. The beam model is not valid for legs shorter than about ten diameters. Basis: Beam theory.
8. No stress-intensification factor and no elbow flexibility are applied. Basis: Stated limit.
9. The stress at the elbow root of a real bend is higher than the beam stress M/Z. Basis: Stated limit.
10. A leg that passes here can still exceed Sa at the elbow, which is why the result is a screen. Basis: Stated limit.
11. A U-loop is two identical legs in series, each a guided cantilever taking half the growth. Basis: Loop model.
12. The cross leg of a U-loop is a rigid guide. Basis: Loop model.
13. The force along the line is the same in both legs of a U-loop. Basis: Loop model.
14. The moment M is the moment at the leg roots, the corners of the loop. Basis: Loop model.
15. It reaches an anchor only where the leg ends at the anchor, as in an offset leg. Basis: Loop model.
16. The growth of the loop's own legs, which the cross leg has to absorb, is not checked. Basis: Loop model.
17. The run of an offset leg bends for the leg's growth as a second guided cantilever, checked against Sa. Basis: Offset-leg and Z-bend model.
18. In a Z-bend the two runs are in series under one force. Basis: Offset-leg and Z-bend model.
19. Each run takes its share of the leg's growth in proportion to L_i³ and is checked as its own cantilever. Basis: Offset-leg and Z-bend model.
20. The leg of a Z-bend takes the growth of both runs. Basis: Offset-leg and Z-bend model.
21. The force on the anchors is the resultant of the two perpendicular forces along the run and along the leg. Basis: Offset-leg and Z-bend model.
22. anchor_moment is the largest root moment of the legs and runs, M = F·L/2 with each end fixed against rotation. Basis: Stated limit.
23. It is not the moment on a frame anchor, which depends on the whole frame. Basis: Stated limit.
24. A formal analysis is needed before an anchor is sized for moment. Basis: Stated limit.
25. α is the mean coefficient of expansion between the install and operating temperatures. Basis: Material library.
26. E is the modulus at the installation temperature, used for the stress check as the piping codes do. Basis: The ASME process piping code, cl. 319.3.2 and 319.5 (numbers to be checked against the owner's copy).
27. E_hot, when given, is used only for the reactions on the anchors. Basis: Code convention to be checked.
28. The material properties are taken from a library material applied at the right temperature. Basis: Material library.
29. Sa and K1 are the owner's records from the owner's own copy of the piping code or the design basis. Basis: No table from a standard is shipped.
30. No allowable stress, K1 or code constant ships. Basis: No table from a standard is shipped.
31. The sheet applies the relation to the numbers entered, whatever code or load case they come from. Basis: No table from a standard is shipped.
32. No weight, spring-hanger or cold-spring effect is included in the leg force. Basis: Stated limit.
33. The anchor force adds support friction μ·w·L only when μ and the weight per metre are entered. Basis: Stated limit; a warning says so when they are not.
34. The bellows anchor force is the pressure thrust P·A_eff plus the spring force k·ΔL at the governing pressure. Basis: Stated limit.
35. The governing pressure is the design or test pressure, whichever governs, because compression and extension ratings can differ. Basis: Stated limit.
36. Guide friction and any other bellows load come from the manufacturer's data. Basis: Stated limit.

## Outputs

| Symbol | Description |
|---|---|
| `D_i` | Inside diameter |
| `A_m` | Metal cross-section |
| `I_x` | Second moment of area |
| `Z_x` | Section modulus |
| `dT` | Temperature change |
| `L_tot` | Length that grows |
| `dL` | Thermal movement between the anchors (magnitude) |
| `dL_signed` | Thermal movement, signed |
| `sigma_res` | Fully restrained stress |
| `F_res` | Fully restrained force |
| `direction` | Direction of the movement |
| `F_friction` | Support friction on the run |
| `u_res` | Fully restrained stress over Sa |
| `Delta` | Movement absorbed by each leg |
| `L_req` | Required leg length |
| `u_leg` | Required leg over available leg |
| `F_leg` | Force of the leg |
| `M_leg` | Moment at the leg root |
| `sigma_leg` | Bending stress at the leg root |
| `u_sigma` | Bending stress over Sa |
| `Delta_run` | Leg growth absorbed by the run or runs |
| `S_run` | Flexibility of the runs |
| `F_run` | Force of the runs |
| `sigma_run` | Bending stress at the first run root |
| `L_req_run` | Required length of the first run |
| `u_run` | First run: required over available |
| `M_run` | Moment at the first run root |
| `F_along_run` | Anchor force component along the run |
| `F_along_leg` | Anchor force component along the leg |
| `anchor_force` | Force on the anchors (resultant) |
| `anchor_moment` | Largest root moment (guided-cantilever model) |
| `L_dev` | Developed length |
| `U_a` | Anchor distance |
| `Y_scr` | Resultant displacement Y |
| `scr` | Screening ratio OD·Y/(L − U)² |
| `u_scr` | Screening ratio over K1 |
| `screen` | Screening result |
| `pass` | All checks pass |
| `sigma_run2` | Bending stress at the second run root |
| `L_req_run2` | Required length of the second run |
| `u_run2` | Second run: required over available |
| `M_run2` | Moment at the second run root |
| `F_thrust` | Bellows pressure thrust |
| `F_spring` | Bellows spring force |
| `u_bellows` | Movement over the rated movement |

The section, `dT`, `dL`, `dL_signed`, `direction`, `sigma_res`, `F_res` are produced before `Sa` is asked for, so they are on the sheet even while it is blocked on `Sa` or `H`. Then `u_res`, `Delta`, `L_req`, `u_leg`, `F_leg`, `M_leg`, `sigma_leg`, `u_sigma`; offset leg only `L_tot`, `Delta_run`, `S_run`, `F_run`, `sigma_run`, `L_req_run`, `u_run`, `M_run` (and `sigma_run2`, `L_req_run2`, `u_run2`, `M_run2` for a Z-bend); `F_along_run`, `F_along_leg`, `anchor_force`, `anchor_moment`, `L_dev`, `U_a`, `Y_scr`, `scr`, `u_scr` (with `K1`), `screen` (text), `pass`; axial bellows `F_thrust`, `F_spring`, `u_bellows`. **Ports** `exp.anchor_force` → `anchor_force` (N; the resultant of the force along the run and the force along the leg, or thrust plus spring for a bellows), `exp.anchor_moment` → `anchor_moment` (N·m; the largest root moment of the guided-cantilever model, not a frame anchor moment; zero for a bellows), `exp.dL` → `dL` (mm, a magnitude; `dL_signed` carries the sign). **Checks.** Available leg against required leg; each run against its required length (offset leg: the run; Z-bend: the first and the second run); screening `u_scr ≤ 1` (named "Screening only … not met: the line needs a formal stress analysis") when `K1` is given; movement against rating (bellows). `pass` = 1 only when `K1` is given and every check passes (leg layouts). **Blocks (1.4.0).** `short-leg` (the available leg, or for an offset leg a run, under ten pipe diameters: the beam model does not hold; the message starts with the input concerned, `H`, `L` or `L2`), `modulus-low` (E or E_hot under 0.5 GPa). **Warnings.** `bellows-unused`, `screening-k1` (no `K1`).

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `piping.force_anchor` | `anchor_force` | kN | Force on the anchors: the resultant of the force along the run (the leg's) and along the leg (the runs') for an offset leg or Z-bend, the leg's for a U-loop, or P·A_eff plus the spring force for an axial bellows |
| `piping.moment_anchor` | `anchor_moment` | kN·m | Largest root moment of the legs and runs in the guided-cantilever model (not a frame anchor moment); zero for an axial bellows |
| `piping.growth_thermal` | `dL` | mm | Thermal growth (or contraction) of the run between anchors, as a magnitude |

## Checks

Checks the sheet makes:

- Available leg against the required leg
- Run against the length it needs to absorb the leg's growth
- Screening ratio against the limit K1 (not met: the line needs a formal stress analysis) (ASME B31.3-2022 cl. 319.4.1 (screening form; number to be checked against the owner's copy))
- First run against the length it needs to absorb the leg's growth
- Second run against the length it needs to absorb the leg's growth
- Movement against the rated axial movement of the bellows

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `wall` | Block | Conservative | The wall must be less than half the outside diameter. |
| `Sa` | Block | Conservative | Enter the allowable displacement-stress range Sa for a leg or a loop; it has no default. |
| `dT_in` | Block | Conservative | Enter the temperature change when it is the source of the movement; it must not be zero. |
| `dT-zero` | Block | Conservative | Install and operating temperatures are equal: there is no movement to absorb. |
| `H` | Block | Conservative | Enter the available leg H for a leg or a loop; it has no default. |
| `bellows` | Block | Conservative | An axial bellows needs both the pressure P_b and the effective area A_eff. |
| `modulus-low` | Block | Unconservative | E (or E_hot) is below 0.5 GPa, lower than any usual pipe material: it may have been typed in MPa, which makes a leg look 1000 times more flexible and passes a leg that fails, so the sheet is refused. |
| `short-leg` | Block | Unconservative | The available leg (or, for an offset leg or Z-bend, a run) is shorter than ten pipe diameters: the guided-cantilever beam model is not valid, so the sheet is refused. |
| `bellows-unused` | Warning | Conservative | Bellows inputs are given with a leg or loop layout and are not used; choose the axial bellows layout. |
| `k1-unused` | Warning | Conservative | K1 is given with the axial bellows layout, which has no screening; it is not used. |
| `l2-unused` | Warning | Conservative | L2 is given with a layout other than the offset leg; it is not used. |
| `friction` | Warning | Unconservative | The anchor force holds no support friction unless both μ and the weight per metre are entered. |
| `screening-k1` | Warning | Conservative | No K1 was entered: the screening ratio is shown and not judged, and the sheet does not pass. |

## Limitations

Screening only. Guided-cantilever beam model: straight uniform slender leg, rigid elbows. **No stress-intensification factor or elbow flexibility is applied, so the stress at the elbow root can be higher than the beam stress** (stated as its own sheet assumption). No pressure or ovalisation effects, torsion or out-of-plane legs, multi-plane routing, branches or tees, support friction, weight, spring hangers or cold spring, sustained or occasional loads, or fatigue cycle count beyond what `Sa` contains. A Z-bend has two parallel runs in one plane only; a three-dimensional or multi-leg route needs a formal analysis. `E` and `α` are single values. **No allowable stress, K1 or code constant is shipped** (§B5): `Sa` and `K1` are the owner's records from the owner's own copy of the code, with provenance; the invented `Sa` and `K1` in the test cases and worked examples are illustrative inputs, not code values.

## References

- ASME B31.3-2022, cl. 319.4.1 (screening relation; number to be checked against the owner's copy): Process piping — flexibility analysis, exemption from formal analysis.
- ASME B31.3-2022, cl. 319.3.2 and 319.5 (reference and hot moduli; numbers to be checked against the owner's copy): Process piping — displacement stress range and reactions.
- BS EN 13480-3:2017, cl. 12 (number to be checked against the owner's copy): Metallic industrial piping — flexibility analysis.

## Test cases

- `ka-offset-leg-steel-50m`: Steel pipe 50 m between anchors, 70 K: growth 42 mm, an offset leg of 6 m against 4.850 m required (Confirmed; source: Analytic (ΔL = α·L·ΔT; guided cantilever L = √(3·E·D·Δ/Sa), F = 12·E·I·Δ/L³, M = 6·E·I·Δ/L²; screening Y = α·ΔT·U; recomputed in python3))
- `ka-u-loop-too-short`: The same line with a U-loop of two 3 m legs: each takes 21 mm and needs 3.429 m, so the loop is too short (Confirmed; source: Analytic (U-loop as two guided cantilevers, Δ = ΔL/2; recomputed in python3))
- `ka-cold-copper-loop`: A cold copper line (ΔT −40 K) with a 1.5 m U-loop leg: contraction of 19.92 mm, both checks met (Confirmed; source: Analytic (ΔL = α·L·|ΔT|; U-loop Δ = ΔL/2; recomputed in python3))
- `ka-z-bend-steel`: A Z-bend of two runs (30 m and 20 m) and a 6 m leg: the leg takes the 42 mm of both runs, the runs share the leg's growth (Confirmed; source: Analytic (Z-bend: leg a guided cantilever for α·(L + L2)·ΔT; runs in series under F_run = 12·E·I·Δ_leg/(L³ + L2³), run i taking Δ_leg·L_i³/(L³ + L2³); resultant anchor force; recomputed in python3))
- `ka-axial-bellows`: The 50 m steel line on an axial bellows: 600 kPa on 80 cm² and 120 kN/m over 42 mm anchor at 9.84 kN (Confirmed; source: Analytic (ΔL = α·L·ΔT; anchor force = P·A_eff + k·ΔL; recomputed in python3))

## Changes

**1.5.1.** Declares the unit change of 1.5.0 (ADR-0177 review): `P_b` was in bar before 1.5.0, so a bare stored number from an earlier version is read in bar, never kPa. No calculated value changes.

**Changes in 1.5.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The inputs sit in the groups Setup, Geometry, Material, Conditions, Requirements, Bellows and Support friction. The bellows pressure `P_b` now reads in kPa and the bellows stiffness `k_b` in kN/m; the forces and moments in the ports `piping.force_anchor` and `piping.moment_anchor` read in kN and kN·m (the stored SI values are unchanged). The applicability rule `H` is declared: the available leg `H` must be entered for a leg or a loop. The checks read "Available leg against the required leg" and the like, the 36 assumptions are one short sentence each, and the scope and the method are rewritten. The reference to the process piping code keeps its short designation (ASME B31.3-2022), which the sheet standard's year pattern does not accept for an ASME standard.

**Screening only — not a piping stress analysis.** The sheet says so in its scope and in its first assumption.

**Test cases (analytic, Confirmed; recomputed in python3).** - `ka-offset-leg-steel-50m` — steel 88.9 × 3.2 mm, E 210 GPa, α 12 µm/(m·K), 10 → 80 °C, 50 m: **ΔL = 42 mm**, σ_r = 176.4 MPa, F_r = 151.98 kN, A_m = 861.55 mm²; invented Sa 100 MPa, 6 m offset leg: L_req = 4.85005 m (u 0.80834), F = 388.11 N, M = 1164.33 N·m, σ_leg 65.34 MPa; the run needs 1.68010 m (u 0.0336) for the leg's 5.04 mm and pushes 0.080 N along the leg, so the anchors take the resultant 388.10882 N; Y = 42.301 mm (U 50.3587 m, L_dev 56 m), ratio 118.168 against an invented K1 of 100 mm²/m², so "needs formal stress analysis".
- `ka-u-loop-too-short` — the same line with a U-loop of two 3 m legs: Δ = 21 mm, L_req = 3.42950 m (u 1.14317, FAIL), F = 1552.44 N, M = 2328.65 N·m, ratio 103.717 (Y = 42 mm), K1 120 met.
- `ka-cold-copper-loop` — 28 × 1.2 mm copper, E 120 GPa, α 16.6, 30 m, ΔT = −40 K entered: ΔL = 19.92 mm (`dL_signed` −19.92), σ_r = 79.68 MPa, F_r = 8.0504 kN; U-loop legs 1.5 m, invented Sa 60 MPa: L_req = 1.29355 m (u 0.86237), F = 38.62 N, M = 28.97 N·m, ratio 61.97 (K1 150 met), pass.
- `ka-axial-bellows` — the 50 m steel line on a bellows at 6 bar, A_eff 80 cm², 120 N/mm, rated 50 mm: thrust 4800 N, spring 5040 N, anchors 9840 N, u 0.84, pass.

**Not pinned — owner check.** (1) The ASHRAE *HVAC Systems and Equipment* handbook pipe-bend (expansion-loop) relation: its coefficients and a worked example could not be stated from memory with confidence, so no case is entered. (2) Chevron Phillips Performance Pipe technical note PP 814-TN "Thermal Effects" loop equations (a secondary, vendor source for polyethylene): inputs and answer not stated from memory, so no case is entered (it would be `Unconfirmed` and `Secondary` if added). No `Unconfirmed` case is in the template. Both need the owner's copy.

**Worked examples.** `lthw-main-expansion-loop` (U-loop of two 4 m legs on a 50 m steel LTHW main: 3.43 m required, passes, 655 N on the anchors, screen passes), `copper-offset-leg-too-short` (24 m copper riser, a 2.5 m offset leg against 2.82 m required fails both the leg check and the screen; the run is not the problem) and `copper-axial-bellows` (the same riser on a bellows: 2.4 kN thrust plus 1.96 kN spring, 4.36 kN on the anchors, within the rated movement). E and α are the W3 seed values (`Carbon steel (generic)`, `Copper (generic)`), labelled as assumed and indicative.

**Version 1.1.0 (review fixes).** Screening `Y` is the resultant `α·|ΔT|·U` (was `ΔL`: unconservative for an offset leg); the run of an offset leg is checked for the leg's growth; the axial bellows layout; `E` is the reference-temperature modulus with an optional `E_hot` for the reactions; `pass` is 0 without a `K1`; the section, `ΔL` and `F_r` no longer wait for `Sa`; `dL_signed` and `direction`; `dT_in` limit −300 K; the short-leg warning is about the available leg only; the elbow intensification factor is its own assumption.

**Version 1.2.0 (second review).** `anchor_force` is the resultant `√(F_leg² + F_run²)` of the perpendicular components (also reported separately); `anchor_moment` is stated as the guided-cantilever simplification and not a frame anchor moment; the optional second run `L2` makes the offset-leg layout a Z-bend, each run checked as its own cantilever (replacing the earlier advice to enter a combined spacing); the method text is reworded; `k1-unused` is its own warning and the warnings are prefixed with the input they are about; `P_b` is the governing pressure and the compression/extension rating note is added.

**Version 1.3.0 (support friction).** The anchor force holds the leg's (or runs', or bellows') force alone; the slides and guides between the anchor and the loop add `μ·w·L_slide` along the run (88.9 × 3.2 steel, water-filled 12.1 kg/m, 50 m on slides at μ 0.3: 1.78 kN against the leg's 388 N). New optional inputs `mu`, `w_pipe` (kg/m) and `L_slide` (blank: the anchor spacing `L`) give the output `F_friction` and fold it into `F_along_run` and `anchor_force` (the resultant for an offset leg or Z-bend, the sum for a U-loop or bellows); when `μ` and `w_pipe` are not both entered `F_friction` is 0 and the sheet warns (rule `friction`) that the anchor force holds no support friction. The pack `pack.pipework-supports` binds `anchor_force` to the post's `F_x`; its example enters no friction, so it carries the warning.

**Changes in 1.4.0 (C-2 fix, F2).** `modulus-low` and `short-leg` are blocks (both warned and passed): a modulus under 0.5 GPa (a metal's modulus typed in MPa lands at 0.07 to 0.21 GPa, which made a leg look 1000 times more flexible and the anchor force 1000 times low) and a leg or run under ten diameters (the guided-cantilever model is not valid there and its force and stress are not safe). A leg of exactly ten diameters runs. The worked examples and known-answer cases are inside both limits and are unchanged.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Low-temperature hot-water main, 50 m between anchors: a U-loop of two 4 m legs ([sheet](../../worked-examples/building-services/bs.pipe-expansion/lthw-main-expansion-loop/sheet.txt)).

```text
dT = 80 degC − 10 degC = 70 K
E_r = 210 GPa
D_i = 88.9 mm − 2 × 3.2 mm = 82.5 mm
A_m = π / 4 × ((88.9 mm)^2 − (82.5 mm)^2) = 861.55 mm²
I_x = π / 64 × ((88.9 mm)^4 − (82.5 mm)^4) = 79.206 cm⁴
Z_x = 79.206 cm⁴ / (88.9 mm / 2) = 17.819 cm³
L_tot = 50 m
dL = 12 um/(m·K) × 50 m × abs(70 K) = 42 mm
dL_signed = 12 um/(m·K) × 50 m × 70 K = 42 mm
sigma_res = 210 GPa × 12 um/(m·K) × abs(70 K) = 176.4 MPa
F_res = 210 GPa × 12 um/(m·K) × abs(70 K) × 861.55 mm² = 151.98 kN
F_friction = μ and w_pipe blank = 0 kN
u_res = 176.4 MPa / 100 MPa = 1.764
Delta = 42 mm / 2 = 21 mm
… 16 more step(s) on the sheet
Governing: PASS  Available leg against the required leg: 3.429 m vs 4 m  utilisation 0.86
```
<!-- hand-arithmetic:end -->
