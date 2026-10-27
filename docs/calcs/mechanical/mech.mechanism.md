# Mechanism kinematics (`mech.mechanism@1.3.0`)

**In plain terms.** A mechanism is a set of rigid links that turns one motion into another, like a crank driving a rocker or a slider. The worksheet takes the link lengths (or the cam profile) and works out, for each crank angle, where every link is, its speed and acceleration, how well it transmits force, and the forces in the pins and guides. It also tells you what kind of linkage you have, and where it locks.

## Scope

Calculates the position, velocity, acceleration, input torque and forces of a one-degree-of-freedom planar four-bar, slider-crank or cam mechanism. The input turns at constant speed, and the links are rigid, with no clearance and no friction.

## Method

- Four-bar position: a·e^{jθ₂} + b·e^{jθ₃} = d + c·e^{jθ₄}, with ℓ = |A − O₄|, cos γ = (c² + ℓ² − b²)/(2cℓ) and θ₄ = φ ∓ γ (φ the direction O₄→A; open: −, crossed: +); θ₃ from B − A
- Four-bar velocity and acceleration: differentiated loop, 2×2 system with det = b·c·sin(θ₃ − θ₄); ω₃ = ω₂·a·sin(θ₄ − θ₂)/(b·sin(θ₃ − θ₄)), ω₄ = ω₂·a·sin(θ₃ − θ₂)/(c·sin(θ₃ − θ₄)); α from the second derivative at constant ω₂
- Grashof class and transmission angle: s + l < p + q (crank-rocker, double-crank or double-rocker by the shortest link), = at the change point, > non-Grashof; transmission angle cos μ = (b² + c² − ℓ²)/(2bc) folded to 0 … 90°; toggle where ℓ = b + c or |b − c|
- Slider-crank position: x = r·cosθ + σ·√(l² − (r·sinθ − e)²) (σ = +1 open, −1 crossed); dx/dθ = −r·sinθ − r·cosθ·(r·sinθ − e)/(x − r·cosθ); dead centres where crank and rod are collinear; μ = 90° − |rod angle to the slide line|
- Input torque and forces: T_in·ω_in = Σ F·v by virtual work; four-bar T_in = (T_L + J·α₄)·ω₄/ω₂, tangential pin force P = (T_L + J·α₄)/c and coupler force P/sin μ; slider T_in = (m·a + F_L)·dx/dθ, slide-direction force F_x = m·a + F_L, rod force F_x/cos θ₃ and guide reaction F_x·tan θ₃; RMS torque and mean power over the sweep by Simpson's rule
- Assembly limits and loop closure: four-bar toggles at cos θ₂ = (d² + a² − ℓ²)/(2ad), ℓ = b + c or |b − c|; slider sin θ = (e ± l)/r; the sweep is cut at them and the unbounded peaks are not reported; closure |a·e^{jθ₂} + b·e^{jθ₃} − c·e^{jθ₄} − d| (slider: |r·e^{jθ} + l·e^{jθ₃} − (x + j·e)|) below 1e-9 mm at every step
- Cam motion: per segment with x = (θ − θ₀)/β, rise s = s₀ + h·f(x), return s = s₀ − h·f(x); cycloidal f = x − sin(2πx)/(2π); 3-4-5 f = 10x³ − 15x⁴ + 6x⁵; 4-5-6-7 f = 35x⁴ − 84x⁵ + 70x⁶ − 20x⁷; modified trapezoid from its acceleration pieces (C_v = 2, C_a = 8π/(π + 2), C_j = 32π²/(π + 2)); SVAJ at constant ω: v = ω·s′, a = ω²·s″, j = ω³·s‴ (′ = d/dθ), so v_pk = C_v·h·ω/β
- Cam contact: roller pressure angle φ = arctan[(s′ − e)/(√(R_p² − e²) + s)] with R_p = R_b + R_r, base circle R_p,req = √(s₀,req² + e²) with s₀,req = max(|s′ − e|/tan φ_max − s); pitch-curve radius ρ = |P′|³/(P′ × P″), undercut where ρ_min ≤ R_r; flat-faced profile radius ρ = R_b + s + s″ above zero; follower force F = m·a + k·s + F_pre + F_ext (the follower leaves the cam where F < 0), input torque T = F·s′, roller normal force F/cos φ

Notes on the method, from the first spec:

- Four-bar: ground `d` from O₂ (origin) to O₄ = (d, 0), crank `a`, coupler `b`, rocker `c` (inputs `L1 … L4`). Loop: `a·e^{jθ₂} + b·e^{jθ₃} = d + c·e^{jθ₄}`. With `ℓ = |A − O₄|` and `cos γ = (c² + ℓ² − b²)/(2cℓ)` (law of cosines, the Freudenstein result), `θ₄ = φ − γ` (**open**) or `φ + γ` (**crossed**, the mirror image about the line O₄A), `φ` the direction O₄→A; `θ₃` from `B − A`. The sweep stays on the chosen branch.
- Velocity and acceleration (constant ω₂): the differentiated loop is a 2×2 system with determinant `b·c·sin(θ₃ − θ₄)`; `ω₃ = ω₂·a·sin(θ₄ − θ₂)/(b·sin(θ₃ − θ₄))`, `ω₄ = ω₂·a·sin(θ₃ − θ₂)/(c·sin(θ₃ − θ₄))`, and α from the second derivative. Where `|sin(θ₃ − θ₄)| < 10⁻⁹` (a toggle) the system is singular and the row is left out of the table and counted (warning `reach`).
- Grashof: `s + l < p + q` Grashof (crank-rocker if the input link is the shortest, double-crank if the ground link is, double-rocker if the coupler is, "rocker-crank" if the output link is; in the last two the input link cannot turn fully), `=` the change point (parallelogram, deltoid: a collinear singular position every cycle), `>` non-Grashof (triple rocker). The class text is output `grashof`; `S_plus_L`, `P_plus_Q` the two sums.
- Transmission angle `cos μ = (b² + c² − ℓ²)/(2bc)`, folded into 0 … 90°; the check is `μ_req/μ_min ≤ 1` (default `μ_req` 40°, editable). When the sweep reaches a toggle (`μ_min` = 0) and `μ_req` is above zero there is no finite ratio: `u_mu` is the text "— (μ_min = 0° …)", the check fails as a condition and its utilisation prints "—" (no sentinel number); a zero requirement is met at any angle. Toggle crank angles (coupler and rocker collinear) are where `ℓ = b + c` or `|b − c|`: `cos θ₂ = (d² + a² − ℓ²)/(2ad)`, output `toggle_angles`.
- Slider-crank: crank `r` (`L2`), rod `l` (`L3`), slide line at height `e` (`e_off`): `x = r·cosθ + σ·√(l² − (r·sinθ − e)²)`, `σ = +1` open (slider on the +x side), `−1` crossed. `dx/dθ = −r·sinθ − r·cosθ·(r·sinθ − e)/(x − r·cosθ)`, rod angle `θ₃ = atan2(e − r·sinθ, x − r·cosθ)`, `dθ₃/dθ = −r·cosθ/(x − r·cosθ)`, second derivatives analytically; `v = ω·dx/dθ`, `a = ω²·d²x/dθ²`. Transmission angle `μ = 90° − |rod angle to the slide line| = arccos(|r·sinθ − e|/l)`. Dead centres (crank and rod collinear, v = 0): outer `θ = asin(e/(l + r))`, inner `θ = π + asin(e/(l − r))` (open; `π − …` and `−…` on the crossed circuit); stroke between them `√((l + r)² − e²) − √((l − r)² − e²)` (output `stroke_closed`; `stroke` is the swept extent). The crank turns fully only if `l ≥ r + |e|` (output `slider_class`).
- Input torque by virtual work, `T_in·ω_in = Σ F·v`: four-bar `T_in = (T_L + J·α₄)·ω₄/ω₂` with `T_L` the external torque on the output link (constant in direction, or `sign(ω₄)·T_L` when the load opposes the motion) and `J` its moment of inertia about the pivot; slider `T_in = (m·a + F_L)·dx/dθ` with the slider mass `m` and external force `F_L`. The crank and the coupler or rod are massless (add their mass to the output element for a first allowance).
- **Cam.** The cam turns at constant speed `ω` (`n_in`); the segment table (`segs`, at most 24 rows) lists rise, dwell and return with a motion law, the angle β and the lift h, and must add to 360° and close (rises equal returns, never below the base circle). In a segment with `x = (θ − θ₀)/β`: rise `s = s₀ + h·f(x)`, return `s = s₀ − h·f(x)`. Laws: cycloidal `f = x − sin(2πx)/(2π)`; 3-4-5 `10x³ − 15x⁴ + 6x⁵`; 4-5-6-7 `35x⁴ − 84x⁵ + 70x⁶ − 20x⁷`; modified trapezoid from the five acceleration pieces `A·sin 4πx`, `A`, `A·cos 4π(x − 3/8)`, `−A`, `−A·cos 4π(x − 7/8)` with `A = 8π/(π + 2)`. Peak coefficients `C_v = max f′`, `C_a = max |f″|`, `C_j = max |f‴|`: cycloidal 2, 2π, 4π²; modified trapezoid 2, 8π/(π + 2) = 4.8881, 32π²/(π + 2) = 61.43; 3-4-5 15/8, 10/√3 = 5.7735, 60; 4-5-6-7 35/16, 7.5132, 52.5; so `v_pk = C_v·h·ω/β`, `a_pk = C_a·h·ω²/β²`, `j_pk = C_j·h·ω³/β³`. SVAJ: `v = ω·s′`, `a = ω²·s″`, `j = ω³·s‴` (′ = d/dθ).
- Roller follower (offset `e_off`, prime radius `R_p = R_b + R_r`): pressure angle `φ = arctan[(s′ − e)/(√(R_p² − e²) + s)]` (for `e = 0` the `arctan[s′/(R_p + s)]` of the in-line follower), checked against `phi_max` (default 30°, editable; check `φ_max/φ_allowed ≤ 1`); `Rb_min_phi` is the base circle that holds φ at its limit (`R_p,req = √(s₀,req² + e²)`, `s₀,req = max(|s′ − e|/tan φ_max − s)`). The pitch curve is `P(θ) = Rot(−θ)·(e, √(R_p² − e²) + s)`; its radius of curvature `ρ = |P′|³/(P′ × P″)` where convex (for `e = 0` Norton's `[(R_p + s)² + s′²]^{3/2}/[(R_p + s)² + 2s′² − (R_p + s)s″]`); the profile is **undercut** where `ρ_min ≤ R_r` (the check "Smallest radius of curvature against the roller radius" FAILs; `rho_min` reads "none" when the pitch curve is nowhere convex). Flat-faced in-line follower: no pressure angle, the profile radius `R_b + s + s″` must stay positive (cusp check) and the face must reach `w_face = max |s′|` either side of the axis.
- Follower force `F = m·a + k·s + F_pre + F_ext` (mass `m_f`, spring rate `k_sp` kN/m and preload `F_pre` N at the base circle, constant external load `F_ext`; `k_sp` and `F_pre` take the dimensions of the rate and force ports of the spring sheet so they can be linked); the follower leaves the cam where `F < 0` (check "Smallest follower force against zero", warning `contact`). Input torque by virtual work `T = F·s′` (from `T·ω = F·v`).
- **Assembly limits and unbounded peaks (1.1.0).** A four-bar cannot be assembled beyond its toggles (`ℓ = b + c` or `|b − c|`, the angles of output `toggle_angles`); a slider-crank with `l < r + |e|` not beyond `sin θ = (e ± l)/r` (output `limit_angles`). Driven at constant speed into one of these the speed, acceleration, torque and force are unbounded, so the sheet **cuts the sweep** to the first assemblable stretch (the closed-form limit angles bound it, and each is marked when it lies in the requested sweep), warns (rule `limit`, naming the angles and how to set `theta_start` and `theta_range` inside the stretch), reports `mu_min = 0`, and **withholds every peak, RMS and mean quantity and all ports** instead of printing a discretisation artefact. A sweep that stays inside the stretch reports them normally. A change-point linkage (parallelogram, deltoid) is continued through its singular position **on the same branch by continuity** (γ changes sign there), the table shows the continued motion, and the peaks are withheld with a warning. Singular rows at an exact limit are left out of the table.
- Forces and drive outputs (1.1.0): the four-bar's tangential pin force `P = (T_L + J·α₄)/c` is `follower_force`; the coupler (a two-force member) carries `P/sin μ`, which is also the crank-pin force: `F_pin_pk`. Slider: the slide-direction force `F_x = m·a + F_L` is `follower_force`, the rod carries `F_x/cos θ₃` (`F_pin_pk`) and the guide reacts with `F_x·tan θ₃` (`F_guide_pk`). Cam: `follower_force` is the axial force, `F_pin_pk` the roller contact normal force `F/cos φ`. `T_rms_in` is the RMS input torque, `P_mean = ω·mean(T_in)` the mean input power (Simpson's rule; zero for a conservative load over a cycle), `n_in` the input speed, for thermal sizing. For a spring sheet: `lift` and `F_spring_max = k·lift + F_pre` (the spring force at full lift, without inertia).
- **Self-consistency.** The loop residual `|a·e^{jθ₂} + b·e^{jθ₃} − c·e^{jθ₄} − d|` (slider: `|r·e^{jθ} + l·e^{jθ₃} − (x + j·e)|`) is computed at every table row and every scan angle, reported as `res_max` (mm), and asserted below `10⁻⁹` mm by a check ("Loop-closure residual against its limit") and a warning if it fails.
- **Peaks** are those of the continuous motion over the swept range: a dense scan (about 4 points per degree of sweep, at least 720) refined by golden section between the neighbours of the best sample, so they do not depend on the table's step count `N` (`steps`, 2 … 3600, N + 1 rows). A sweep that passes a toggle or leaves the assembly range uses only the rows that exist.

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram (ADR-0158, DI-08). It is not to scale and shows inputs only, never a result. It draws the mechanism that `mode` selects: a four-bar linkage, a slider-crank, or a cam with its follower. One variant per `mode`, and one per `follower` for a cam. It binds `L1`, `L2`, `L3`, `L4`, `n_in`, `T_load`, `J_out`, `theta_start`, `theta_range`, `circuit`, `steps`, `mu_req`, `load_dir`, `mode`, `m_slider`, `e_off`, `F_load`, `Rb`, `follower`, `m_f`, `k_sp`, `F_pre`, `F_ext`, `segs`, `R_r` and `phi_max`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `mode` | Kind of mechanism |  | Four-bar |  | Setup |
| `circuit` | Assembly circuit (four-bar, slider-crank): open is θ₄ = φ − γ (slider on the +x side of the crank pin), crossed the mirror branch |  | Open |  | Setup |
| `L1` | Ground link d, O₂ to O₄ (four-bar) | mm | 100 | 1 to 10000 | Geometry |
| `L2` | Crank length a (four-bar) or r (slider-crank) | mm | 30 | 1 to 10000 | Geometry |
| `L3` | Coupler length b (four-bar) or connecting-rod length l (slider-crank) | mm | 90 | 1 to 10000 | Geometry |
| `L4` | Rocker length c (four-bar) | mm | 70 | 1 to 10000 | Geometry |
| `e_off` | Offset e of the slide line above the crank centre, or of the cam follower axis at +x: positive e reduces the pressure angle where ds/dθ exceeds e (optional; blank = 0) | mm | 0 | -1000 to 1000 | Geometry |
| `steps` | Intervals N of the table (N + 1 rows, a whole number, at most 3600; the peaks do not depend on it) |  | 72 | 4 to 720 | Sweep |
| `n_in` | Input speed (constant) | rpm | 120 | 0.1 to 30000 | Sweep |
| `theta_start` | Input angle at the first row of the table | ° | 0 | -720 to 720 | Sweep |
| `theta_range` | Swept range of the input angle (360° for a full turn) | ° | 360 | 1 to 720 | Sweep |
| `load_dir` | External load: constant direction, or always opposing the output motion |  | Opposes motion |  | Loads |
| `T_load` | External load torque on the output link (four-bar). Positive acts in the −θ₄ direction, resisting positive ω₄; with 'Opposes motion' its sign is applied against the actual direction of ω₄ (a negative value then assists) | N·m | 1.5 | -10000 to 10000 | Loads |
| `J_out` | Moment of inertia of the output link about its pivot (four-bar) | kg·m² | 0.002 | 0 to 1000 | Loads |
| `m_slider` | Slider mass (slider-crank) | kg | 0.5 | 0 to 10000 | Loads |
| `F_load` | External load on the slider (slider-crank). Positive acts in the −x direction, resisting +x motion; with 'Opposes motion' it is applied against the actual direction of motion | N | 200 | -1000000 to 1000000 | Loads |
| `mu_req` | Minimum transmission angle required (linkages) | ° | 40 | 10 to 80 | Requirements |
| `follower` | Follower type: roller (translating, pressure angle and undercut checked) or flat-faced (in-line) |  | Roller |  | Cam |
| `Rb` | Base-circle radius of the cam profile | mm | 40 | 1 to 1000 | Cam |
| `R_r` | Roller radius (roller follower) | mm | 8 | 1 to 200 | Cam |
| `phi_max` | Maximum pressure angle allowed (roller follower) | ° | 30 | 5 to 45 | Cam |
| `segs` | Cam segments: rise, dwell or return, with motion law, angle and lift; the angles add to 360° and the rises equal the returns |  | optional |  | Cam |
| `segs.kind` | Segment (column) |  |  | | Cam |
| `segs.law` | Motion law (rise, return) (column) |  | optional | | Cam |
| `segs.beta` | Cam angle β (column) | ° |  | | Cam |
| `segs.h` | Lift h (rise, return; ignored for a dwell) (column) | mm | optional | | Cam |
| `m_f` | Follower mass (all moving parts that follow the cam) | kg | 0.2 | 0 to 1000 | Follower loads |
| `k_sp` | Return spring rate | kN/m | 5 | 0 to 1000 | Follower loads |
| `F_pre` | Spring preload at the base circle | N | 20 | 0 to 100000 | Follower loads |
| `F_ext` | External load on the follower, positive against the rise | N | 0 | -100000 to 100000 | Follower loads |

## Assumptions

1. The mechanism is rigid and planar, has one degree of freedom, and has no joint clearance or friction. Basis: Scope limits of the model.
2. The input turns at a constant speed. Basis: Scope limits of the model.
3. The position is the closed-form solution of the vector loop, on the chosen circuit. Basis: Open and crossed are the two branches: θ₄ = φ − γ and φ + γ for the four-bar, σ = +1 and −1 for the slider.
4. The sweep stays on the chosen circuit and follows it by continuity through a change point. Basis: Only a change point, with all four links collinear (θ₂ = 0 or π), flips the branch.
5. A linkage with one change point per turn repeats every 720°, and a sweep of more than one turn warns. Basis: A kite, for example 40/40/100/100, is such a linkage.
6. The table leaves out angles where the linkage cannot be assembled or sits at a toggle. Basis: It counts them, and at a toggle the velocity system is singular.
7. The input torque comes from the output load and the inertia of the output element only. Basis: The crank and the coupler or connecting rod are massless: add their mass to the output element for a first allowance.
8. The crank, the coupler and the connecting rod are massless two-force members. Basis: Rigid body, frictionless.
9. The peaks are those of the continuous motion over the swept range, not of the printed table. Basis: A dense scan refined by golden section finds them, so they do not depend on the step count of the table (up to 3600).
10. At an assembly limit the sweep is cut, and the unbounded peaks, torque and forces are not reported. Basis: The limit angles are listed: ℓ = b + c or |b − c| (four-bar), |r·sinθ − e| = l (slider).
11. A change-point four-bar is continued through its singular position on the same branch. Basis: Its speeds and torques next to the singular position are not reported.
12. The cam drives a translating follower at constant speed. Basis: Norton, Design of Machinery, cam design: the four classical laws, pressure angle and radius of curvature.
13. The follower force is m·a + k·s + F_pre + F_ext, and contact is lost below zero. Basis: The spring rate and the preload are those at the base circle, and the segment table must add to 360° and close.
14. Pressure angle and undercut are checked for the roller follower, and the profile radius for the flat-faced one. Basis: The roller has the prime radius R_p = R_b + R_r.
15. The output speed is a kinematic proxy at constant input speed, not the speed of an actuated cylinder. Basis: For motor sizing, use the peak input_torque for the peak check and T_rms_in with n_in for the thermal sizing. The mean power is P_mean.

## Outputs

| Symbol | Description |
|---|---|
| `n_in` | Input (crank) speed, passed through |
| `f_in` | Input (crank) frequency, for a spring surge check |
| `grashof` | Grashof classification |
| `S_plus_L` | Shortest plus longest link |
| `P_plus_Q` | Sum of the other two links |
| `theta2_sw` | Table: crank angle |
| `theta3_sw` | Table: coupler angle |
| `theta4_sw` | Table: rocker angle |
| `omega3_sw` | Table: coupler angular velocity |
| `omega4_sw` | Table: rocker angular velocity |
| `alpha3_sw` | Table: coupler angular acceleration |
| `alpha4_sw` | Table: rocker angular acceleration |
| `mu_sw` | Table: transmission angle |
| `T_in_sw` | Table: input torque by virtual work |
| `theta3_0` | Coupler angle at the first input angle |
| `theta4_0` | Rocker angle at the first input angle |
| `omega3_0` | Coupler angular velocity at the first input angle |
| `omega4_0` | Rocker angular velocity at the first input angle |
| `alpha3_0` | Coupler angular acceleration at the first input angle |
| `alpha4_0` | Rocker angular acceleration at the first input angle |
| `mu_0` | Transmission angle at the first input angle |
| `T_in_0` | Input torque at the first input angle |
| `theta4_alt` | Rocker angle on the other circuit at the first input angle |
| `theta4_min` | Smallest rocker angle |
| `theta4_max` | Largest rocker angle |
| `swing` | Rocker swing over the sweep |
| `mu_min` | Smallest transmission angle over the sweep |
| `res_max` | Largest loop-closure residual |
| `res_vel_max` | Largest velocity-loop residual, relative to the size of its terms |
| `toggle_angles` | Toggle crank angles (coupler and rocker collinear, μ = 0): the limits of the crank's assemblable range |
| `limit_positions` | Crank and coupler collinear (the rocker's dead-centre positions) |
| `omega3_pk` | Peak coupler angular velocity |
| `omega_out_pk` | Peak output (rocker) angular velocity |
| `alpha3_pk` | Peak coupler angular acceleration |
| `alpha_out_pk` | Peak output (rocker) angular acceleration |
| `input_torque` | Peak input (crank) torque by virtual work |
| `output_motion` | Peak speed of the output pin at the rocker length c (a kinematic proxy for a cylinder only if the cylinder acts perpendicular to the rocker at that radius; the angular velocity is omega_out_pk) |
| `peak_accel` | Peak acceleration of the output pin |
| `follower_force` | Peak force perpendicular to the rocker at the output pin, the drive force of the output |
| `F_pin_pk` | Peak force in the coupler, which is also the force at the crank pin and the pin loads on the bearings |
| `T_rms_in` | RMS input torque over the swept range |
| `P_mean` | Mean input power over the swept range (negative: the mechanism returns energy) |
| `u_mu` | Required against smallest transmission angle |

Tables of the sweep (vectors, also drawn as series): four-bar `theta2_sw`, `theta3_sw`, `theta4_sw`, `omega3_sw`, `omega4_sw`, `alpha3_sw`, `alpha4_sw`, `mu_sw`, `T_in_sw`; slider `theta_sw`, `x_sw`, `v_sw`, `a_sw`, `theta3_sw`, `omega3_sw`, `mu_sw`, `T_in_sw`. Values at the first input angle (`*_0`, and `theta4_alt` / `x_alt` on the other circuit). Peaks `omega_out_pk`, `alpha_out_pk`, `omega3_pk`, `alpha3_pk`, `swing`, `mu_min`, `x_max`, `x_min`, `stroke`, `res_max`. Cam: tables `theta_sw`, `s_sw`, `v_sw`, `a_sw`, `j_sw`, `phi_sw`, `kappa_sw` (roller: pitch-curve curvature, positive convex; flat-faced: `rho_sw`, the signed profile radius), `F_sw`, `T_in_sw`, the segment table (`seg_start`, `seg_beta`, `seg_s_end`, `seg_v_pk`, `seg_a_pk`, `seg_j_pk`, `seg_law`), peaks `lift`, `v_pk`, `a_pk`, `j_pk`, `F_max`, `F_min`, `phi_max_pk`, `Rb_min_phi`, `rho_min` (signed for the flat-faced follower; the cusp check FAILs at or below zero), `w_face`, `F_pin_pk`, `F_spring_max`, `T_rms_in`, `P_mean`. Linkage extras: `res_vel_max`, `toggle_angles` / `limit_angles`, `limit_positions` (crank and coupler collinear: the rocker's dead centres), `F_guide_pk`. `output_motion` is the **peak linear speed** of the output element (the slider, the cam follower, or for a four-bar the output pin at the rocker length `c`); the rocker's angular velocity is `omega_out_pk`. `output_motion`, `peak_accel` and `follower_force` are a kinematic proxy for a cylinder sheet: a cylinder-actuated linkage runs at about constant cylinder speed, not constant crank speed, and a four-bar pin speed is a cylinder speed only if the cylinder acts perpendicular to the rocker at that radius. Peak `input_torque` suits a motor's peak check; as a constant load it would make the RMS sizing very conservative. Linking `follower_force` to a spring sheet while `k_sp` comes from that sheet would be a cycle. Added in 1.1.0 beyond the four ports of the first spec, at the integrator's decision: `F_pin_pk`, `T_rms_in`, `n_in`, `f_in` (1.2.0: `n_in/60`; rpm is an angular velocity, so `n_in` cannot be linked to a frequency input), `P_mean`, `lift` and `F_spring_max`.

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `mechanism.input_torque` | `input_torque` | N·m | Peak input torque (crank or cam shaft) by virtual work. For thermal sizing use T_rms_in and n_in, not this peak as a constant load |
| `mechanism.output_motion` | `output_motion` | m/s | Peak output velocity: the slider or the cam follower, or for a four-bar the speed of the output pin at the rocker length c (omega_out_pk is the angular velocity). Into a cylinder sheet this is a kinematic proxy only: a cylinder-actuated linkage runs at about constant cylinder speed, not at constant crank speed |
| `mechanism.peak_accel` | `peak_accel` | m/s² | Peak output acceleration: the slider or the follower, or for a four-bar the magnitude of the acceleration of the output pin |
| `mechanism.follower_force` | `follower_force` | N | Peak drive force on the output element: the cam follower's axial force, the slider's force in the slide direction, or the tangential force at the rocker pin. Use it as a force estimate |
| `mechanism.pin_force_peak` | `F_pin_pk` | N | Peak force at the crank or cam-shaft pin: the force in the coupler (four-bar), in the connecting rod (slider), or the contact normal force F/cos φ (cam roller). Use it as a bearing load |
| `mechanism.torque_rms_input` | `T_rms_in` | N·m | RMS input torque over the swept range or the cam revolution. Use it for thermal sizing |
| `mechanism.speed_input` | `n_in` | rpm | Input speed (crank or cam shaft) passed through. Use it as the load speed |
| `mechanism.frequency_input` | `f_in` | Hz | Input frequency n_in/60 in Hz. Rpm is an angular velocity, so n_in cannot be linked to a frequency |
| `mechanism.power_mean` | `P_mean` | kW | Mean input power over the swept range or the cam revolution |
| `mechanism.lift` | `lift` | mm | Greatest follower displacement (cam). Use it as the spring travel |
| `mechanism.force_spring_max` | `F_spring_max` | N | Spring force at full lift, k·lift + F_pre (cam). The follower force includes inertia and is not the spring force |

## Checks

- Required transmission angle against the smallest transmission angle (linkages; `mu_req` against `mu_min`, utilisation `u_mu`; a requirement of 0 always passes).
- Loop-closure residual against its limit of 1e-9 mm, and velocity-loop residual against its limit of 1e-8 of the size of its terms (linkages).
- Largest pressure angle against the allowed pressure angle (roller cam; `phi_max_pk` against `phi_max`, utilisation `u_phi`).
- Smallest radius of curvature against the roller radius (roller cam; the undercut check) or against zero (flat-faced cam; the cusp check).
- Smallest follower force against zero (cam; warning `contact` when the follower leaves the cam).

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `missing` | Block | Conservative | An input the chosen mechanism needs is blank. |
| `geometry` | Block | Conservative | The mechanism cannot be assembled at any angle of the swept range, or the links are not a valid mechanism. |
| `reach` | Warning | Conservative | Some rows of the sweep cannot be assembled (the crank cannot turn fully) or sit at a toggle with no defined velocity: they are left out of the table. |
| `limit` | Warning | Conservative | The requested sweep reaches an assembly limit (a toggle, or the rod perpendicular to the slide line) or passes a change point: the sweep is cut where the mechanism cannot be assembled, and when the limit is inside it the speed, acceleration, torque and force peaks, which are unbounded at constant input speed, and the ports are not reported. |
| `closure` | Warning | Conservative | The loop-closure residual reached 1e-9 mm. |
| `grashof` | Warning | Conservative | The linkage is not a Grashof crank (the input link cannot turn through 360°) but the swept range is 360° or more. |
| `segments` | Block | Conservative | The cam segments do not form a cam: angles not adding to 360°, rises and returns not cancelling, a rise or return without a law or a lift, or a return below the base circle. |
| `cam-geometry` | Block | Conservative | The follower offset reaches the prime-circle radius. |
| `contact` | Warning | Unconservative | The follower force falls below zero, so the follower leaves the cam. A stronger spring or a smaller acceleration is needed. |

## Limitations

Rigid, planar, no clearance, no friction, single degree of freedom, constant input speed. The cam follower translates (no swing arm or oscillating follower), the flat-faced follower is in line, the spring is linear and its surge is not modelled (the spring sheet reports it). Link inertia and gravity are not modelled beyond the output element. The open and crossed labels name the two branches of the closed form, and the sweep stays on one of them. A change-point linkage (parallelogram, deltoid) is continued through its change points on the same branch, with the circuit named at a reference angle independent of the sweep (the middle of the first interval between change points in 0 … 2π, π/2 when there are none), so `theta_start` does not change which motion `Open` means and the label at a crank angle is the same on every turn. The parity at the start of the sweep is that of the start angle wrapped into 0 … 2π; only the true change points, θ₂ = 0 or π with all four links collinear, flip the branch, and the other toggles are assembly limits. A linkage with one change point per turn (a kite, for example 40/40/100/100) repeats every 720°, and a sweep of more than one turn warns. The peak of a quantity that jumps at a sign change of the velocity (an opposing load) is the limit value at the jump.

## Cam offset sign

The cam turns counter-clockwise (θ increasing) with the follower axis at +x offset `e` from the cam centre; positive `e` reduces the pressure angle where `ds/dθ > e` (the rise) and negative `e` raises it. `T_load` and `F_load` positive resist positive `ω₄` / `+x` motion; with "Opposes motion" the sign is applied against the actual direction of motion.

## Changes

**1.3.0 (C-2, phase B).** The inputs are grouped (`Setup`, `Geometry`, `Sweep`, `Loads`, `Requirements`, then the cam groups) and the sweep inputs that are required come before the optional ones. `mu_req` has a usual range of 10° to 80°. The return spring rate `k_sp` is entered in kN/m (the same value as N/mm) and the mean power `P_mean` is shown in kW (it was W). The eleven ports have public names `mechanism.<quantity>`, and their descriptions name the quantity, not a legacy module. The checks show a value against a value: the required transmission angle against the smallest one, and the largest pressure angle against the allowed one, both with their utilisation; the other conditions are named against their limit. The block and warning messages are at most two sentences of 20 words or fewer. The scope is two sentences, the method has eight lines, and the assumptions are single sentences. When `mu_req` or `phi_max` is blank, the default the sheet uses is a step of the working. Calculated values are unchanged.

**1.2.2 and earlier.** 1.2.0 added `f_in`, 1.1.0 the assembly limits, the unbounded-peak rule and the force and drive ports, and 1.0.0 the first sheet.

## References

- Norton, Design of Machinery, Ch. 4, 6, 7, 6th ed.: Position, velocity and acceleration analysis; transmission angle; Grashof condition.
- Uicker, Pennock and Shigley, Theory of Machines and Mechanisms, Ch. 4–6, 5th ed.: Loop-closure position analysis; velocity and acceleration of planar linkages.

## Test cases

Analytic and Confirmed, recomputed in python3 (Freudenstein quadratic as an independent route to the position; the differentiated loop solved as a real 2×2 system and cross-checked by central finite differences; peaks by a 20001-point scan polished by golden section): `ka-fourbar-crank-rocker-open` (120/40/110/90 mm at θ₂ = 90°: θ₃ 25.617841°, θ₄ 103.371151°, ω₄ 5.1533032 rad/s, α₄ 26.749883 rad/s², μ_min 45.815615°, peak T_in 1.5134299 N·m), `ka-fourbar-parallelogram` (closed form: θ₄ = θ₂, θ₃ = 0, ω₄ = ω₂, T_in = T_L, μ_min = 5° on a 5° … 175° sweep), `ka-fourbar-crank-rocker-crossed`, `ka-slider-inline` (closed form: x(90°) = √(l² − r²), v = −rω, a = ω²r²/√(l² − r²), stroke 2r, peak a = rω²(1 + r/l)), `ka-slider-offset` (stroke √((l + r)² − e²) − √((l − r)² − e²) = 100.335104 mm), `ka-slider-crossed`; cams: `ka-cam-cycloidal` (closed form: v_pk = 2hω/β = 1.6 m/s, a_pk = 2πhω²/β² = 201.06193 m/s², j_pk = 4π²hω³/β³ = 50532.375 m/s³), `ka-cam-modified-trapezoid` (C_a = 8π/(π + 2)), `ka-cam-345` (C_v = 15/8, C_a = 10/√3, C_j = 60), `ka-cam-4567` (C_v = 35/16, C_a = f″((5 − √5)/10) = 7.513188, C_j = 52.5), `ka-cam-dwell-only` (ρ_min = R_b + R_r = 48 mm exactly), `ka-cam-flat-faced` (w_face = 2h/β = 19.0986 mm), and the pressure angle, curvature radius and minimum base circle of the offset and in-line roller cams (`ka-cam-offset-roller`, `ka-cam-inline-roller`: pitch curve differentiated numerically by a 4th-order finite-difference stencil, independently of the closed form in the calculator). Worked examples: `carton-tucker-crank-rocker` (packaging crank-rocker) and `indexing-cam-offset-roller`. **Not pinned (owner check):** Norton, *Design of Machinery* 6th ed., Example 4-1, and the Uicker, Pennock and Shigley 5th ed. four-bar example: the book examples' link lengths and θ₃/θ₄ are not available to the build, so no published number is entered; add them as Unconfirmed cases from the owner's copy.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Carton flap tucker: crank-rocker 150/45/140/100 mm at 60 cycles/min ([sheet](../../worked-examples/mechanical/mech.mechanism/carton-tucker-crank-rocker/sheet.txt)).

```text
f_in = rpm to Hz = 1 Hz
S_plus_L = shortest + longest link = 195 mm
P_plus_Q = the other two links = 240 mm
theta2_sw = 73 rows, θ₂ from 0° to 360° = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, … (73 items)] deg
theta3_sw = 73 rows, θ₂ from 0° to 360° = [45.45, 43.305, 41.183, 39.119, 37.143, 35.277, 33.533, 31.922, 30.446, 29.103, 27.892, 26.806, … (73 items)] deg
theta4_sw = 73 rows, θ₂ from 0° to 360° = [93.891, 91.886, 90.182, 88.806, 87.774, 87.09, 86.749, 86.738, 87.038, 87.625, 88.475, 89.561, … (73 items)] deg
omega3_sw = 73 rows, θ₂ from 0° to 360° = [-2.6928, -2.6892, -2.6368, -2.5435, -2.4183, -2.2706, -2.1088, -1.9402, -1.7705, -1.6037, -1.4425, -1.2886, … (73 items)] rad/s
omega4_sw = 73 rows, θ₂ from 0° to 360° = [-2.6928, -2.3371, -1.9398, -1.5152, -1.0781, -0.64174, -0.21757, 0.18575, 0.56219, 0.90807, 1.2216, 1.5026, … (73 items)] rad/s
alpha3_sw = 73 rows, θ₂ from 0° to 360° = [-1.6439, 2.0963, 5.3499, 7.9793, 9.9336, 11.238, 11.97, 12.238, 12.157, 11.832, 11.354, 10.795, … (73 items)] rad/s²
alpha4_sw = 73 rows, θ₂ from 0° to 360° = [23.794, 27.273, 29.765, 31.194, 31.596, 31.098, 29.878, 28.129, 26.035, 23.751, 21.398, 19.064, … (73 items)] rad/s²
mu_sw = 73 rows, θ₂ from 0° to 360° = [48.441, 48.581, 48.999, 49.687, 50.631, 51.814, 53.216, 54.816, 56.592, 58.522, 60.583, 62.755, … (73 items)] deg
T_in_sw = 73 rows, θ₂ from 0° to 360° = [1.5919, 1.3661, 1.1246, 0.87436, 0.62126, 0.37043, 0.1261, 0.12823, 0.38586, 0.61929, 0.82764, 1.0113, … (73 items)] N·m
theta3_0 = at the first input angle = 45.45 deg
theta4_0 = at the first input angle = 93.891 deg
… 25 more step(s) on the sheet
Governing: PASS  Required transmission angle against the smallest transmission angle: 40° vs 48.44°  utilisation 0.83  « Transmission angle
```
<!-- hand-arithmetic:end -->
