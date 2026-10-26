# Pipe pressure drop and pump duty (`bs.pipe-pump@1.10.0`)

**In plain terms.** Water loses pressure as it flows, by friction along the pipe and at every bend, valve and fitting. The friction factor depends on how rough the pipe is and how turbulent the flow is, which the Colebrook equation captures. Plotting the pressure needed against flow gives the system curve; where it crosses the pump's curve is the flow the pump will actually deliver. The suction side is checked so the pressure never falls far enough for the water to boil and cavitate.

## Scope

Calculates the friction and fitting losses of series pipe segments at a given flow, the system curve and the duty point against a pump curve. It also gives the affinity-law shift for a speed or impeller change, and the NPSH.

## Method

- Design flow and velocity: Q = Φ/(ρ c_p ΔT) when a heat load is given in place of a flow; v = Q/(πD²/4)
- Reynolds number and friction factor: Re = ρvD/μ; f = 64/Re for laminar flow (Re < 2300); Colebrook–White 1/√f = −2 log10(ε/3.7D + 2.51/(Re√f)) for turbulent flow, by fixed-point iteration (at most 100 steps) from the Swamee–Jain value, which also cross-checks it
- Head lost in a segment: h_f = f (L + L_eq)/D · v²/2g + K v²/2g
- System curve: H_sys(Q) = Σdz + h_out + Σh_f,lam·(Q/Q₀) + Σh_f,other·(Q/Q₀)², with a laminar segment's friction scaled as Q and every other friction and fitting loss as Q² (a closed circuit has Σdz = 0)
- Water held in the pipes: V_pipe = Σ π/4·D²·L
- Duty point: H_pump(Q) = H_sys(Q), every crossing found by a scan and refined by bisection, the stable one nearest the design flow reported; pump affinity laws Q ∝ n·D^a, H ∝ n²·D², with a = 1 for a trimmed impeller (the default) and a = 3 for a geometrically similar pump
- Net positive suction head available: NPSH_a = p_atm/ρg + z_s − h_f,suction − p_v/ρg, with h_f,suction that of the 'suction' segments at the larger of the design flow and the duty flow
- Shaft torque for the motor: T = P/ω, with P the larger of the design-flow and duty-point shaft power at the speed n_pump, or the shifted duty power at the shifted speed n_pump·n₂/n₁

### Notes on the method

Darcy–Weisbach; laminar `f = 64/Re` (Re < 2300, warned); Colebrook–White by fixed-point on 1/√f from the Swamee–Jain start (≤ 100 iterations, Swamee–Jain reported as a cross-check); transition 2300–4000 warned. System curve `H_static + k Q²` with k from the design flow. `H_static = Σdz + h_out` for an **open** circuit; a **closed** flow-and-return circuit (`circuit` = closed) has no static lift, the rise in the flow being recovered in the return, so `H_static = h_out` and the rises entered are ignored (an assumption records that). `V_pipe = Σ π/4·D²·L` is the water held in the segments. Duty by bisection on `H_pump(Q) − H_sys(Q)` over the curve's flow range; no crossing ⇒ warning and failed check. Affinity for the speed `Q ∝ n`, `H ∝ n²`; for the diameter ratio `D_law` chooses a trimmed impeller of the same casing (`Q ∝ D`, `H ∝ D²`, the default since 1.7.0) or a geometrically similar pump (`Q ∝ D³`, `H ∝ D²`, the only law before 1.7.0). Water properties by published correlations (Kell 1975 density, Vogel-fit viscosity, Hyland–Wexler vapour pressure), 0–100 °C; other fluids (glycol mixtures) are entered with ρ and μ and their provenance (D-17).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `fluid` | Fluid |  | water | water, custom (ρ and μ given) |  | Setup |
| `circuit` | Circuit (a closed flow-and-return loop has no static lift: the rises entered are ignored) |  | open (the pump lifts the water by the rises entered) | open (the pump lifts the water by the rises entered), closed (flow and return: the rises cancel, no static lift) |  | Setup |
| `segments` | Pipe segments in series |  | table |  |  | Setup |
| `segments.name` | Segment (column) |  | optional | | | Setup |
| `segments.L` | Length (column) | m |  | | | Setup |
| `segments.D` | Inner diameter (column) | mm |  | | | Setup |
| `segments.OD` | Outside diameter of the pipe (optional; the first segment's is offered as OD_1, the inside diameter of its insulation) (column) | mm | optional | | | Setup |
| `segments.eps` | Roughness (column) | mm |  | | | Setup |
| `segments.K` | Σ K of fittings (column) |  | optional | | | Setup |
| `segments.L_eq` | Equivalent length of fittings (column) | m | optional | | | Setup |
| `segments.dz` | Rise in elevation (+ up) (column) | m | optional | | | Setup |
| `segments.side` | Pump side (column) |  | optional | | | Setup |
| `rho_custom` | Density (custom fluid) | kg/m³ | optional | 500 to 2000 | 1 to 100000 | Material |
| `mu_custom` | Dynamic viscosity (custom fluid) | mPa·s | optional | 0.1 to 1000 | 0.0001 to 1000000 | Material |
| `h_out` | Pressure head required at the outlet | m | 0 | 0 to 1000 | 0 to 100000 | Loads |
| `Q` | Design flow rate (optional; blank = the flow comes from the heat load) | m³/h | 20 | 0.01 to 100000 | 1e-09 to 10000000 | Loads |
| `Phi_load` | Heat carried by the water (gives the design flow with ΔT; replaces Q) | kW | optional | 0.01 to 100000 | 1e-06 to 10000000 | Loads |
| `dT_load` | Flow-to-return temperature difference across the load | K | optional | 1 to 100 | 0.01 to 500 | Loads |
| `cp_load` | Specific heat of the fluid in the heat-load flow (optional; blank = 4186 J/(kg·K) assumed for water, and required for a custom fluid) | J/(kg·K) | optional | 1000 to 6000 | 100 to 100000 | Loads |
| `T_fluid` (was `T`) | Fluid temperature (mean; link the mean water temperature of the sealed system for a heating loop) | °C | 20 | 0 to 100 | -50 to 300 | Conditions |
| `p_atm` | Atmospheric pressure (NPSH) | kPa | 101.325 | 50 to 110 | 1 to 1000 | Conditions |
| `z_s` | Static head at the pump suction (+ flooded, − lift) | m | 0 | -10 to 100 | -1000 to 10000 | Conditions |
| `T_line` | Design temperature of the line for the insulation step: the coldest (cold line) or hottest (hot line) fluid temperature in it, for example the flow temperature of a 6/12 °C chilled line whose mean is 9 °C (optional; blank = the fluid temperature; offered as a port only when it or the fluid temperature is entered; not used for the fluid properties) | °C | optional | -50 to 150 | -100 to 400 | Conditions |
| `v_limit` (was `v_max`) | Velocity limit (warning and check) | m/s | 3 | 0.5 to 10 | 0.01 to 100 | Requirements |
| `D_law` | Law for the diameter ratio: a trimmed impeller of the same casing (Q ∝ D, H ∝ D²) or a geometrically similar pump (Q ∝ D³, H ∝ D²) |  | impeller trim (Q ∝ n·D, H ∝ n²·D²) | impeller trim (Q ∝ n·D, H ∝ n²·D²), geometrically similar pump (Q ∝ n·D³, H ∝ n²·D²) |  | Pump |
| `n_ratio` | Speed ratio n₂/n₁ (affinity) |  | 1 | 0.2 to 2 | 0.01 to 10 | Pump |
| `D_ratio` | Impeller diameter ratio D₂/D₁ (affinity; the law is chosen above) |  | 1 | 0.7 to 1.1 | 0.1 to 5 | Pump |
| `eta_pump` | Pump efficiency at duty (for shaft power) |  | 0.7 | 0.2 to 0.95 | 0.01 to 1 | Pump |
| `Q_pump` | Pump curve: flow points (optional; blank = no duty point) | m³/h | optional |  |  | Pump |
| `H_pump` | Pump curve: head points (optional; blank = no duty point) | m | optional |  |  | Pump |
| `n_pump` | Pump rated speed, the speed of the pump curve as entered, which gives the shaft torque (optional; blank = no shaft torque) | rpm | optional | 100 to 6000 | 1 to 100000 | Pump |
| `p_v_custom` | Vapour pressure of the custom fluid at the fluid temperature, absolute (optional; blank = NPSH_a is not computed; needed for NPSH_a of a custom fluid) | kPa | optional | 0 to 500 | 0 to 10000 | NPSH |
| `NPSH_r` | NPSH required by the pump, at the flow NPSH_a is evaluated at: the larger of the design flow and the duty flow (optional) | m | optional | 0 to 50 | 0 to 1000 | NPSH |

## Assumptions

1. Flow is steady, incompressible and fully developed, and all segments carry the full design flow in series. Basis: Darcy–Weisbach.
2. Fitting losses scale with velocity head (K method) or add pipe length (equivalent-length method). Basis: Crane TP-410 practice.
3. Both fitting methods may be used on one segment. Basis: Crane TP-410 practice.
4. The system curve scales turbulent and transitional friction and all fitting losses with Q² from the design flow. Basis: H_sys = H_static + k₁ Q + k₂ Q².
5. The friction factor is held at its design-flow value on the system curve. Basis: H_sys = H_static + k₁ Q + k₂ Q².
6. The friction of a laminar segment scales with Q, and the static part is independent of flow. Basis: H_sys = H_static + k₁ Q + k₂ Q².
7. Water properties are from published correlations at atmospheric pressure, 0 to 100 °C. Basis: Kell 1975; Vogel fit; Hyland & Wexler 1983.
8. Any other fluid is entered with its own density and viscosity and their provenance. Basis: Stated.

## Outputs

| Symbol | Description |
|---|---|
| `T_line` | Line design temperature |
| `v_s1` | Velocity, oil line |
| `Re_s1` | Re, oil line |
| `f_s1` | f, oil line |
| `h_f_s1` | h_f, oil line |
| `h_f_total` | Σ h_f |
| `H_static` | Static head |
| `V_pipe` | Water held in the pipes |
| `H_sys` | System head at Q |
| `dp_total` | Friction Δp |
| `dp_sys` | System Δp |
| `Q_design` | Design flow |
| `P_hyd` | Hydraulic power |
| `v_peak` | Highest velocity |
| `f_sj_s1` | f (Swamee–Jain), main |
| `Q_duty` | Duty flow as entered |
| `H_duty` | Duty head as entered |
| `P_shaft` | Shaft power as entered |
| `Q_duty2` | Duty flow after the affinity shift |
| `H_duty2` | Duty head after the affinity shift |
| `P_shaft2` | Shaft power after the affinity shift |
| `NPSH_a` | NPSH available |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `pipe.flow_design` | `Q_design` | m³/h | Design flow (as entered, or from the heat load) |
| `pipe.head_static` | `H_static` | m | Static head |
| `pipe.volume_water` | `V_pipe` | L | Water held in the pipe segments (→ sealed-system V_pipe, where the chain order allows) |
| `pipe.head_system` | `H_sys` | m | System head at the design flow |
| `pipe.head_loss_total` | `h_f_total` | m | Total friction and fitting loss at the design flow |
| `pipe.pressure_drop_total` | `dp_total` | kPa | Friction pressure drop at the design flow |
| `pipe.pressure_system` | `dp_sys` | kPa | System pressure at the design flow |
| `pump.power_hydraulic` | `P_hyd` | kW | Hydraulic power at the design point |
| `pump.power_shaft_design` | `P_shaft_design` | kW | Pump shaft power at the design flow (needs the pump speed) |
| `pump.torque_shaft` | `T_shaft` | kN·m | Pump shaft torque, for the larger of the design-flow and duty-point shaft power (→ motor-sizing T_L) |
| `pump.speed_shaft` | `n_shaft` | rpm | Pump shaft speed (→ motor-sizing n_L) |
| `pipe.velocity_peak` | `v_peak` | m/s | Highest velocity |
| `pipe.temperature_line` | `T_line` | °C | Design temperature of the line to insulate: T_line as entered, else the fluid temperature when entered (not offered when neither is entered; → insulation T_proc) |
| `pipe.diameter_outside_first` | `OD_1` | mm | Outside diameter of the first pipe segment, where entered (→ insulation D_i) |
| `pump.npsh_available` | `NPSH_a` | m | NPSH available (water, or a custom fluid with its vapour pressure) |
| `pump.flow_duty` | `Q_duty` | m³/h | Duty flow against the pump curve |
| `pump.head_duty` | `H_duty` | m | Duty head against the pump curve |
| `pump.power_shaft_duty` | `P_shaft` | kW | Shaft power at the duty point |

## Checks

Checks the sheet makes:

- Highest velocity against limit
- Duty point against the system curve (as entered)
- Duty point against the system curve (after the affinity shift)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `heat-flow` | Block | Conservative | A heat load needs its temperature difference (and the reverse); without a heat load a design flow is required. |
| `heat-t` | Warning | Conservative | A heat load is given without the fluid temperature: 20 °C is taken. |
| `cp_load` | Block | Conservative | A custom fluid requires its specific heat for the heat-load flow. |
| `heat-flow-q` | Warning | Conservative | A design flow is entered as well as a heat load: the flow from the heat load is used and Q ignored. |
| `custom-fluid` | Block | Conservative | A custom fluid requires its density and viscosity. |
| `water-range` | Block | Conservative | Water correlations hold for 0–100 °C. |
| `segments` | Block | Conservative | At least one segment is required. |
| `laminar` | Warning | Conservative | Laminar flow in a segment (Re < 2300): f = 64/Re. |
| `transition` | Warning | Conservative | Transitional flow (2300 ≤ Re < 4000): the friction factor is uncertain; Colebrook is used. |
| `fittings` | Warning | Unconservative | Fitting loss coefficients K are entered for a segment below Re 4000: K is a turbulent-flow value and rises at low Re, so the fittings term K·v²/2g is understated. |
| `velocity` | Warning | Conservative | A segment exceeds the velocity limit. |
| `pump-curve` | Block | Conservative | The pump curve needs at least two points with matching flow and head counts, flows increasing. |
| `npsh-custom` | Block | Unconservative | NPSH_r is entered for a custom fluid with no vapour pressure p_v_custom: NPSH_a cannot be computed and no NPSH check could be made, so the sheet is refused. |
| `npsh-no-suction` | Warning | Unconservative | NPSH_r is entered but no segment is marked 'suction' (a blank side column means discharge): NPSH_a carries no suction friction and is optimistic unless the suction line has no friction. |
| `no-duty` | Warning | Conservative | The pump curve and the system curve do not cross within the curve's flow range. |
| `plant-loss` | Warning | Unconservative | A heat-load sheet has no outlet head h_out: the pump head then holds pipe friction only, without the water-side loss of the boiler, heat exchanger, coil, valves and emitters. |
| `fluid-cp` | Warning | Unconservative | The specific heat of the heat-load flow differs from water's by more than 5 %: the fluid is not water, but this sheet is on water unless a custom fluid is chosen, so the flow, friction and torque are those of water. |
| `multi-duty` | Warning | Conservative | The pump curve crosses the system curve more than once: all crossings are listed and the stable one nearest the design flow is reported. |
| `duty-unstable` | Warning | Unconservative | The only crossing of the pump curve and the system curve is on a rising part of the pump curve, where the operating point is unstable: the stable-duty check fails. |
| `eta-default` | Warning | Unconservative | The pump efficiency is the default 0.7 for a pump of under about 1 kW shaft power: small circulators have a far lower overall efficiency, so the shaft power and torque are understated. |
| `summary-only` | Warning | Conservative | More than 50 segments: per-segment lines are omitted. |

## Limitations



## References

- Crane Co., Flow of Fluids Through Valves, Fittings and Pipe (Technical Paper No. 410): Flow of fluids through valves, fittings and pipe.
- Çengel & Cimbala, Fluid Mechanics, Ch. 8, 4th ed.: Internal flow: friction factor, minor losses, pump duty.
- CIBSE Guide C, §4: Flow of fluids in pipes and ducts (principles).
- Kell, J. Chem. Eng. Data 20(1), 1975: Density of liquid water.

## Test cases

### Notes on the cases

Heat-load flow `Q = Φ/(ρ c_p ΔT)` (analytic, 12 kW at 20 K, water at 70 °C); laminar oil (Hagen–Poiseuille, exact); turbulent water Re = 1e5, ε/D = 0.001 against an independently solved Colebrook value (Moody point f = 0.02237); duty point against a parabolic pump curve (analytic); `ka-closed-circuit` (the same loop closed: H_static 0, V_pipe 301.593 L). Crane TP-410 cited for the K/L_eq practice only.

- `ka-laminar-oil`: Laminar oil flow (analytic Hagen–Poiseuille) (Confirmed; source: Çengel & Cimbala, Fluid Mechanics, 4th ed.)
- `ka-turbulent-water`: Turbulent water, Colebrook against an independent solution (Confirmed; source: Colebrook–White equation solved independently (Re = 1.0e5, ε/D = 0.001: f = 0.022175; Swamee–Jain 0.022342))
- `ka-duty-point`: Duty point against a parabolic pump curve, with K fittings and a rise (analytic) (Confirmed; source: Analytic: pump H = 40 − 0.01 Q² (Q in m³/h), system H = 10 + k Q² ⇒ duty where 30 = (k + 0.01) Q²)
- `ka-closed-circuit`: The duty-point loop as a closed flow-and-return circuit: no static lift, and the water it holds (Confirmed; source: Analytic (closed circuit: Σdz is not a lift; V_pipe = π/4·D²·L))
- `ka-heat-load-flow`: Design flow from a heat load: 12 kW at a 20 K difference, water at 70 °C (analytic) (Confirmed; source: Analytic: Q = Φ / (ρ c_p ΔT))

## Changes

**Changes in 1.10.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The fluid temperature `T` is now `T_fluid` and the velocity limit `v_max` is now `v_limit`; the old symbols stay as aliases. The heat load `Phi_load` now reads in kW, and the shaft torque `T_shaft` (output and port) in kN·m. The 18 ports are named `pipe.*` and `pump.*`. The inputs sit in the groups Setup, Material, Loads, Conditions, Requirements, Pump and NPSH. The duty-point check reads "Duty point against the system curve" with its basis in brackets, and the velocity and NPSH checks compare physical values. The messages are one or two short sentences.

**Notice rules added in 1.1.0.** `heat-flow` (block): a heat load needs its temperature difference and the reverse; with neither a heat load nor a flow there is nothing to size. `heat-flow-q` (warning): a flow typed beside a heat load is ignored. `heat-t` (warning): a heat load is given without the fluid temperature `T`, so the template's 20 °C is used. `cp_load` (block): a custom fluid on the heat-load path needs its specific heat. `eta_pump` is entered once whether it serves the pump curve, the shaft power or both.

**1.7.0 (C-1 findings).** `bs.pipe-pump-02`: the diameter ratio follows the law chosen in `D_law`; a trim (default) gives, for D₂/D₁ 0.8 on the verification probe, Q_duty2 32.999 m³/h and P_shaft2 1.866 kW where the similar-pump law gave 22.996 m³/h and 1.091 kW. `bs.pipe-pump-01`: a custom fluid with `NPSH_r` and the vapour pressure `p_v_custom` gets `NPSH_a` and the check; without `p_v_custom` the sheet blocks (`npsh-custom`, 1.9.0): NPSH is not computed. `bs.pipe-pump-R1`: with `NPSH_r` entered and no segment marked `suction` (a blank side means discharge) the warning `npsh-no-suction` says the suction friction is left out (water 60 °C, 30 m³/h, 60 m DN65, ΣK 6, z_s −3 m: NPSH_a 5.440 m blank, −2.151 m marked suction). `chain.wet-heating-R1`: when `n_ratio` or `D_ratio` shifts the duty, `T_shaft` is the larger of the design-flow and shifted duty shaft power divided by the shifted speed, and `n_shaft` is `n_pump·n_ratio` (24 kW, 10 K, 70 °C, DN32 × 60 m, ΣK 15, n_pump 2800 rpm, curve (0,6) (1,5) (2,3.5) (3,1) m³/h, n_ratio 1.2: P_shaft2 37.48 W at 3360 rpm, T_shaft 0.1065 N·m where 1.6.0 gave 0.0740 N·m at 2800 rpm). The torque for a diameter change alone is divided by `n_pump` (the speed does not change).

**Changes in 1.8.0.** (1) The system curve scales the friction of a laminar segment (Re < 2300) with `Q`, as `h_f ∝ Q` requires, and every other friction and fitting loss with `Q²`: probe W (oil ρ 900, μ 200 mPa·s, 100 m of DN50, rise 2 m, 5 m³/h) duty flow 5.467 → 5.878 m³/h, duty head 26.55 → 26.12 m, shaft power +5.9 %; turbulent results are unchanged. (2) The duty point is found by scanning the whole pump curve for every crossing with the system curve (each refined by bisection), not by the sign at the two curve ends: the stable crossing (the pump curve falling through the system curve) nearest the design flow is reported, and when there are several `multi-duty` lists them (probe R: crossings at 3.41 and 41.48 m³/h, previously "no crossing" and a failed duty check; now 41.48 m³/h); a pump curve that meets the system curve only where it rises warns `duty-unstable` and fails the check "Duty point against a stable crossing" (1.9.0). (3) With a pump curve, `NPSH_a` is evaluated at the larger of the design flow and the duty flow (the suction friction is higher at the duty flow): water 60 °C, design 30 m³/h, duty 38.54 m³/h, `NPSH_a` 3.21 → 1.76 m (a pass against 3 m becomes a fail); `NPSH_r` is to be read at that flow. (4) The default pump efficiency 0.7 used for a pump of under about 1 kW shaft power warns (`eta-default`): a small circulator's overall efficiency is far lower (at 0.2 the torque is 3.5 times higher).

**Notices in 1.8.1.** A heat-load sheet with `h_out` = 0 warns (`plant-loss`) that the pump head holds pipe friction only, without the water-side loss of the boiler or heat exchanger, coil, valves and emitters (chain probe: `h_out` 1 m raises `T_shaft` 3.7 times). A heat-load sheet on water whose `cp_load` differs from water's by more than 5 % warns (`fluid-cp`) that the fluid is not water: `cp_load` reaches B1 from B5 or B6 as a specific heat only, and the density and viscosity stay water's (a 3600 J/(kg·K) glycol mixture gave a flow 3.3 % high and friction 16 to 20 % low); choose the custom fluid and enter them.

**Fittings at low Reynolds number (1.3.0).** Fitting loss coefficients K are turbulent-flow values and rise at low Re; for a segment below Re 4000 with K entered the sheet warns (`fittings`) that the fittings term is understated. The term is used as entered.
**Changes in 1.9.0 (C-2 fix, F2).** `duty-unstable` (the only crossing of the pump curve and the system curve is on a rising part of the pump curve, where the pump does not hold the operating point) now fails the check "Duty point against a stable crossing"; it warned and the sheet passed (python3 scan: a DN80 100 m line, rise 10 m, pump (0, 8) (10, 14) (20, 18) m³/h and m: one crossing, at 3.41 m³/h, rising). `npsh-custom` (an NPSH_r entered for a custom fluid with no vapour pressure, so no NPSH_a and no NPSH check) is a block; it warned and passed.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Chilled-water riser: pressure drop, duty point, speed turn-down ([sheet](../../worked-examples/building-services/bs.pipe-pump/chilled-water-riser-duty/sheet.txt)).

```text
rho = T = 6 °C = 999.94 kg/m³
mu = T_K = 279.15 K = 1.4684 mPa·s
p_v = T = 6 °C = 0.93525 kPa
T_line = 6 °C = 6 degC
Q_design = 30 m³/h
v_s1 = 30 m³/h / (π × 100² mm² / 4) = 1.061 m/s
Re_s1 = 999.94 × 1.061 × 0.1 / 0.0014684 = 72253
f_s1 = Colebrook–White, 10 iterations (Swamee–Jain start 0.021152) = 0.021093
f_sj_s1 = 0.25 / log10(ε/3.7D + 5.74/Re^0.9)² = 0.021152
h_f_s1 = 0.021093 × (8 + 0) / 0.1 × 0.057399 + 3 × 0.057399 = 0.26906 m
v_s2 = 30 m³/h / (π × 80² mm² / 4) = 1.6579 m/s
Re_s2 = 999.94 × 1.6579 × 0.08 / 0.0014684 = 90316
f_s2 = Colebrook–White, 10 iterations (Swamee–Jain start 0.020946) = 0.020849
f_sj_s2 = 0.25 / log10(ε/3.7D + 5.74/Re^0.9)² = 0.020946
… 25 more step(s) on the sheet
Governing: FAIL  Highest velocity against limit: 2.512 m/s vs 2.500 m/s  utilisation 1.01
```
<!-- hand-arithmetic:end -->
