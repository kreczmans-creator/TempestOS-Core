# Containment fill, bend radius and cable pulling (`elec.containment@1.3.0`)

**In plain terms.** Cables laid in conduit, trunking or tray must not fill it too far, must not be bent tighter than their diameter allows, and must not be pulled so hard that they are damaged. The worksheet works out how full the containment is against the space factor you set, whether the bends are wide enough, and the tension and sidewall pressure on the cables as they are pulled along the route, in both directions.

## Scope

Calculates the fill of a conduit, trunking or tray, the bend radius and the pull of the cables through a route. The space factor, bend multiple, friction coefficient and pulling limits are your own records.

**Reference diagram.** The worksheet draws the containment in section, round or rectangular as the choice selects, with three cables for the cables as listed, and the pull route in plan beside it with the start tension. Bound inputs: cables, D_int, W_int, H_int, route, T_1, mu, w_c, k_n, f_max, n_bend, T_grip and P_lim. Not to scale; inputs only.

## Method

- Cable area: A_cab = Σ n_i·π·d_i²/4 (n_i cables of overall diameter d_i)
- Containment fill: A_int = π·D²/4 (conduit, internal diameter D) or W·H (trunking or tray, internal width × depth); fill = A_cab/A_int; u_fill = fill/f_max, f_max the space factor from your record
- Bend radius: R_min = n_bend·d_max, d_max the largest cable diameter, n_bend from your record; each bend u_b = R_min/R
- Cable weight: w = Σ n_i·m_i (kg/m); force per metre w·g·w_c, g = 9.80665 m/s², w_c your weight-correction factor (1 = full weight)
- Straight length: T₂ = T₁ + w·g·w_c·μ·L
- Tension at a bend: T₂ = T₁·cosh(μ_e·θ) + √(T₁² + (w·g·w_c·R)²)·sinh(μ_e·θ), μ_e = w_c·μ, θ in radians (horizontal bend, cable on the containment floor, the default); capstan option, no weight term: T₂ = T₁·e^(μ_e·θ)
- Sidewall pressure and direction: SWP = T₂/R; the route is run in the listed order and in reverse, and the direction that passes every tension and sidewall pressure check is recommended (else the one of lower maximum tension)
- Pulling limits: T_cab = k_n·N·min(T_max,i) (load shared equally), u_T = T/T_cab; u_grip = T/T_grip, T_grip your grip or pulling-eye limit; u_P = SWP_max/P_lim

### Notes on the method

Fill is geometric: `A_cab = Σ nᵢ·π·dᵢ²/4`, `A_int = π·D²/4` (conduit) or `W·H` (trunking or tray, internal width × depth), `fill = A_cab/A_int`, utilisation `u_fill = fill/f_max`. Bend radius: `R_min = n_bend·d_max` with `d_max` the largest cable diameter; each bend `u_b = R_min/R`, the check is the tightest bend. Weight `w = Σ nᵢ·mᵢ` (kg/m), force per metre `w·g·w_c` (`w_c` your weight-correction factor, default 1), `g = 9.80665 m/s²`. Pulling, in pull order from a start tension `T₁`: straight length `T₂ = T₁ + w·g·w_c·μ·L` (w in kg/m times g gives N/m); bend `T₂ = T₁·e^(w_c·μ·θ)` (capstan equation, friction scaled by `w_c`, θ in radians); sidewall pressure at a bend `SWP = T₂/R` (N/m). The route is run as listed (direction a) and reversed (direction b), with the same start tension; tension never falls along a pull, so the maximum is the end tension. The recommended direction is the only one that passes every tension and SWP check, if exactly one does; otherwise the one with the lower maximum tension (equal tension: the lower SWP; equal both: either). Cable limit, load shared equally so each cable carries `T/N` and the weakest governs: `T_cab = k_n·N·min(T_max,ᵢ)` (`k_n` your derating for more than 3 cables, 1 if none entered), `u_T = max_i (T/N)/(k_n·T_max,ᵢ) = max_tension/T_cab`, with one working line per cable. Grip or pulling-eye limit `T_grip` (separate, optional): `u_grip = max_tension/T_grip`. `u_P = P_max/P_lim`. The largest cable must also fit: diameter below the conduit ID, or not above the smaller side of the trunking or tray.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `cables` | Cables in the containment and pulled together |  | table | columns: name, od (mm), count, mass (kg/m), T_max (N) | Setup |
| `cont_type` | Containment |  | Conduit (round) | Conduit (round), Trunking or tray (rectangular) | Setup |
| `bend_model` | Tension round a bend: horizontal bend with the cable's weight on the containment floor (default), or the capstan equation without the weight term |  | Horizontal bend, cable on the containment floor (weight term) | Horizontal bend, cable on the containment floor (weight term), Capstan (no weight term) | Setup |
| `route` | Pull route in pull order (straight lengths and bends) (optional) |  | table | columns: name, kind, L (m), theta (°), R (mm) | Setup |
| `D_int` | Conduit internal diameter (conduit) (optional) | mm |  | 10 to 300 | Geometry |
| `W_int` | Trunking or tray internal width (optional) | mm |  | 20 to 1000 | Geometry |
| `H_int` | Trunking or tray internal depth (optional) | mm |  | 20 to 300 | Geometry |
| `T_1` | Tension at the start of the pull (pulling rope, drum or head) | N | 0 | 0 to 10000 | Loads |
| `mu` | Coefficient of friction, cable sheath on containment (from your record) (optional) |  |  | 0.05 to 1 | Conditions |
| `w_c` | Weight-correction factor on the cable weight and, by the same factor, the bend friction (optional; blank = 1, full weight on the containment; above 1 for cradled or triangular arrangements, from your record) (optional) |  |  | 1 to 3 | Factors |
| `k_n` | Derating of the cable limits for more than 3 cables on one grip (from your record) (optional) |  |  | at least 0.3 | Factors |
| `f_max` | Space factor limit (fill limit, from your record) |  |  | 0.1 to 0.6 | Requirements |
| `n_bend` | Minimum bend radius as a multiple of the largest cable diameter (from your record) (optional) |  |  | 3 to 30 | Requirements |
| `T_grip` | Grip or pulling-eye limit of the whole bundle (from the pulling equipment or your record) (optional) | N |  | 100 to 1000000 | Requirements |
| `P_lim` | Sidewall pressure limit, force per metre of bend (from the cable maker) (optional) | N/m |  | 100 to 100000 | Requirements |

## Assumptions

- Cables are circular and counted by overall diameter. Basis: Fill by geometry; the factor is a user record.
- There is no packing or cable and conduit factor method. Basis: Fill by geometry; the factor is a user record.
- The jam-ratio warning is the only jamming check. Basis: Fill by geometry; the factor is a user record.
- The space factor is yours, from your own record. Basis: Fill by geometry; the factor is a user record.
- Every cable is pulled together as one bundle on one pulling head with the load shared equally. Basis: Equal sharing; unequal sharing is not modelled.
- Each cable carries T/N, so the weakest cable governs. Basis: Equal sharing; unequal sharing is not modelled.
- The cable limit is N times the lowest maximum pulling tension, derated by your factor above 3 cables. Basis: Equal sharing; unequal sharing is not modelled.
- The grip or pulling-eye limit is a separate limit you enter. Basis: Equal sharing; unequal sharing is not modelled.
- Straight lengths are horizontal and the cable weight bears on the containment over the full length. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- Bends are taken in the plane of the route. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- Inclines, vertical runs and pulley or roller reductions are not modelled, so fold them into μ. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- A cradled or triangular weight correction is entered as w_c. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- The factor w_c scales the weight on straight lengths and the friction in bends. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- The contact force of a cradled or triangular arrangement is w_c times that of a flat one. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- On a straight length T₂ = T₁ + w·g·w_c·μ·L. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- At a bend μ_e = w_c·μ in the cosh/sinh or capstan form. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- A bend in a vertical plane has the weight along the cable rather than on the floor. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- For that bend, take the capstan option and fold the rise into the route. Basis: Equivalent friction w_c·μ at the bends; blank w_c means 1.
- The coefficient of friction is one constant for the whole route. Basis: A user record with its source.
- Static and running friction are not distinguished. Basis: A user record with its source.
- Sidewall pressure is the tension leaving the bend over the bend radius, for the bundle as a whole. Basis: SWP = T₂/R.
- Enter the cable maker's per-cable limit unchanged. Basis: SWP = T₂/R.
- The bundle T/R against it is conservative for a triangular arrangement and roughly equal for a cradled one. Basis: SWP = T₂/R.
- Do not multiply the limit by the number of cables. Basis: SWP = T₂/R.
- The bend radius uses the individual cable diameter. Basis: Cable data sheet.
- For a triplexed or bundled assembly, enter the assembly diameter from the cable maker as the overall diameter. Basis: Cable data sheet.
- The tension at the start of the pull is the same in both directions. Basis: Weight term for a horizontal bend; capstan equation as the option.
- With the horizontal-bend (weight) model a heavy cable gains tension at a bend even from T₁ = 0. Basis: Weight term for a horizontal bend; capstan equation as the option.
- With the capstan option a bend at the very start of a pull with no start tension adds nothing. Basis: Weight term for a horizontal bend; capstan equation as the option.
- Enter the pulling rope or drum tension when it matters. Basis: Weight term for a horizontal bend; capstan equation as the option.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `A_cab` | Total cable cross-section | mm² |
| `A_int` | Internal cross-section | mm² |
| `fill` | Fill | % |
| `u_fill` | Fill utilisation |  |
| `OD_max` | Largest cable diameter | mm |
| `w` | Mass per metre of the bundle | kg/m |
| `R_min` | Minimum bend radius | mm |
| `u_bend` | Bend radius utilisation |  |
| `T_max_a` | Maximum tension, as listed | N |
| `T_max_b` | Maximum tension, reversed | N |
| `P_max_a` | Maximum sidewall pressure, as listed | N/m |
| `P_max_b` | Maximum sidewall pressure, reversed | N/m |
| `max_tension` | Maximum tension, recommended direction | N |
| `P_max` | Maximum sidewall pressure, recommended direction | N/m |
| `T_cab` | Pulling tension limit from the cables | N |
| `u_T` | Cable tension utilisation |  |
| `u_P` | Sidewall pressure utilisation |  |
| `direction` | Recommended pull direction |  |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `containment.fill` | `fill` | % | Cable fill of the containment |
| `containment.tension_max` | `max_tension` | N | Maximum pulling tension in the recommended direction |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Fill against the space factor limit
- Largest cable against the smaller internal side
- Smallest bend radius needed against the tightest bend (cable maker's limit; cf. BS 7671 reg. 522.8)
- Pulling tension against the cable limit
- Sidewall pressure against the limit

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `cables` | Block | Conservative | Each cable row needs a whole number of cables of at least one. |
| `containment` | Block | Conservative | The conduit diameter, or the trunking or tray width and depth, must be entered for the containment chosen. |
| `route` | Block | Conservative | A straight row needs a length, a bend row an angle and a radius; the tension must stay below 1e12 N. |
| `mu` | Block | Conservative | A route needs the coefficient of friction. |
| `n_bend` | Warning | Unconservative | No bend-radius multiple entered: the bends are not checked against the cable. |
| `T_cab` | Warning | Unconservative | A cable has no maximum pulling tension: the cable limit is not checked. |
| `jam` | Warning | Unconservative | Three equal cables in a conduit with 1.05·D/d between 2.8 and 3.2 may jam (geometry, 5 % allowance for ovality). |
| `k_n` | Warning | Conservative | A derating is entered but the bundle has 3 cables or fewer: it is not applied. |
| `route-limits` | Warning | Conservative | A route-only value (friction, grip limit, sidewall limit, derating) is entered but there is no route: it is not used. |
| `P_lim` | Warning | Unconservative | No sidewall pressure limit entered: the pressure is not checked. |

## Limitations

Circular cables counted by overall diameter, no packing rules, no cable or conduit factors (the jam-ratio warning is the only jamming check). Straight lengths are horizontal and the cable weight bears on the containment over the full length (enter a cradled or triangular correction as `w_c`, which scales the weight on straights and the friction in bends, so a bend is `T₁·e^(w_c·μ·θ)`); inclines, vertical runs, rollers and pulleys are not modelled (fold them into μ). The bend radius uses the individual cable diameter; for a triplexed or bundled assembly enter the assembly diameter from the cable maker. One μ for the whole route; static and running friction not distinguished. Bends use the capstan equation only (no weight term). SWP is the whole bundle's tension over the bend radius; enter the cable maker's per-cable limit unchanged (conservative for a triangular arrangement, roughly equal for a cradled one; never multiply it by the cable count). Load is shared equally between cables on one pulling head; unequal sharing is not modelled.

## References

- BS 7671:2018, reg. 522.8: Other mechanical stresses on wiring systems (damage during installation, bending radius)
- BS EN 61386-1:2008: Conduit systems for cable management: general requirements (edition to be checked)
- Beer and Johnston, Vector Mechanics for Engineers: Statics, Ch. 8, 11th ed. (edition to be checked): Friction: belt friction (the capstan equation)

## Test cases

### Notes on the cases

All Confirmed and analytic, recomputed in python3: `ka-fill-3x10-in-25` (3 × 10 mm in a 25 mm conduit: 48 % exactly; with a five-segment route at μ 0.35: 154.919528 N as listed, 138.420988 N reversed, SWP 461.4812 / 284.2706 N/m, T_cab 1200 N), `ka-pull-tray-two-bends` (mixed bundle in 100 × 50 trunking from 100 N: 1074.235304 N as listed, 941.126090 N reversed; T_cab 3 × 150 = 450 N, u_T 2.0914, FAIL because the 150 N control cable carries a third of the pull), `ka-direction-passes-every-check` (the lower-tension direction, 1166.942 N, presses 1716 N/m against a 1500 N/m limit, so the reverse, 1281.142 N and 1010.413 N/m, is recommended), `ka-capstan-180` (T₂ = 200·e^(0.35π) = 600.567352 N, SWP 1501.41838 N/m), `ka-trunking-fill` (12 × 8 mm in 100 × 50: 12.0637 %, u_fill 0.2872 at an illustrative factor of 0.42), `ka-bend-too-tight` (R_min 120 mm against a 100 mm bend: u_b = 1.2). **Not pinned — owner check:** a cable maker's published pulling-guide example (straight plus bend tension and sidewall pressure); none is stated here because its inputs and answer cannot be quoted with confidence, so no Unconfirmed or Secondary case is declared. Every record value in the known-answer cases (space factor, bend multiple, friction, limits) is illustrative, not from any standard. The clause citations are from memory and should be checked against the owner's copies: BS 7671 reg. 522.8 (and its bend-radius sub-regulation), BS EN 61386-1 (cited as general), and Beer and Johnston Statics Ch. 8 (belt friction).

- `ka-fill-3x10-in-25`: Three 10 mm cables in a 25 mm conduit: 48 % fill, and a pull through two bends in both directions (analytic) (Confirmed; source: Analytic (geometry, capstan equation as the bend_model option; recomputed in python3))
- `ka-pull-tray-two-bends`: Mixed bundle pulled through two bends from a 100 N start tension, in a 100 × 50 mm tray (analytic) (Confirmed; source: Analytic (capstan equation, the bend_model option; recomputed in python3))
- `ka-direction-passes-every-check`: A route whose lower-tension direction breaks the sidewall limit: the other direction is recommended (analytic) (Confirmed; source: Analytic (capstan equation, the bend_model option; recomputed in python3))
- `ka-capstan-180`: A single 180° bend with a 200 N start tension: the capstan equation (analytic) (Confirmed; source: Analytic (T₂ = T₁·e^(μθ), the bend_model capstan option; recomputed in python3))
- `ka-heavy-cable-horizontal-bend`: A heavy cable round a large horizontal bend: the weight term adds 64 % to the capstan tension (analytic) (Confirmed; source: Analytic (horizontal bend with the cable's weight on the containment floor, recomputed in python3))
- `ka-heavy-cable-bend-from-rest`: The same bend with no start tension: the weight term still pulls tension in (analytic) (Confirmed; source: Analytic (horizontal bend with the cable's weight on the containment floor, recomputed in python3))
- `ka-trunking-fill`: Twelve 8 mm cables in 100 × 50 mm trunking (analytic) (Confirmed; source: Analytic (geometry, recomputed in python3))
- `ka-bend-too-tight`: A 100 mm bend for 10 mm cables with a multiple of 12: too tight (analytic) (Confirmed; source: Analytic (R_min = n·d_max))

## Changes

**Changes in 1.3.0 (C-2 consistency, phase C).** Layout and wording; no result changes. `D_c`, `W_c` and `H_c` are `D_int`, `W_int` and `H_int`; the old symbols stay as aliases, so a saved calculation and an inputs file still load (`W_c` and `w_c` differed only by letter case). The checks compare physical values: `fill` with the new step `f_lim` (the space factor in %), `R_min` with the new step `R_bend` (the tightest bend), `max_tension` with `T_cab` and with `T_grip`, and `P_max` with `P_lim`. The method is eight lines, and the details moved to the notes below. The inputs are grouped Setup, Geometry, Loads, Conditions, Factors and Requirements, and every input with no usual range now has one (`f_max`, `n_bend`, `mu`, `T_grip`, `k_n`, `P_lim`). The route column `R` keeps its key, because a table column has no alias mechanism. The ports are named `containment.*`. The assumptions are single sentences, and the references name the edition.

**Changes in 1.2.0 (elec.containment-01).** Tension round a bend in the horizontal plane now includes the cable's weight on the containment floor: `T₂ = T₁·cosh(μ_e·θ) + √(T₁² + (w·g·w_c·R)²)·sinh(μ_e·θ)`, `μ_e = w_c·μ`. A heavy cable (10 kg/m) on a 3 m radius, 90°, `μ` 0.3, `T₁` 100 N gives 263.2 N where the capstan equation gave 160.2 N, and a pull that starts at a bend from zero tension no longer reads zero. The input `bend_model` keeps the capstan equation `T₂ = T₁·e^(μ_e·θ)` as the option for a weightless pull (and the known-answer cases that were derived from it); the two agree to about 0.1 % for a light bundle on a small radius.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Servo power and encoder cables pulled through 40 mm conduit with three bends ([sheet](../../worked-examples/electrical/elec.containment/servo-cable-pull-three-bends/sheet.txt)).

```text
A_c1 = 2 × π × (14 mm)² / 4 = 307.88 mm²
A_c2 = 4 × π × (9 mm)² / 4 = 254.47 mm²
A_cab = A_c1 + A_c2 = 562.35 mm²
OD_max = 14 mm
w = 2 × 0.35 kg/m + 4 × 0.12 kg/m = 1.18 kg/m
A_int = π × (40 mm)^2 / 4 = 1256.6 mm²
fill = 562.35 mm² / 1256.6 mm² = 44.75 %
u_fill = 562.35 mm² / (1256.6 mm² × 0.5) = 0.895
R_min = 10 × 14 mm = 140 mm
u_b2 = 140 mm / 500 mm = 0.28
u_b4 = 140 mm / 400 mm = 0.35
u_b6 = 140 mm / 600 mm = 0.23333
u_bend = 3 bend(s) = 0.35
R_bend = 3 bend(s) = 400 mm
… 31 more step(s) on the sheet
Governing: PASS  Fill against the space factor limit: 44.75 % vs 50 %  utilisation 0.90
```
<!-- hand-arithmetic:end -->
