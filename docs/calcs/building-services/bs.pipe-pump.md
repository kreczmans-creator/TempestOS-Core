# B1 — Pipe pressure drop and pump duty (`bs.pipe-pump@1.6.0`)

**In plain terms.** Water loses pressure as it flows, by friction along the pipe and at every bend, valve and fitting. The friction factor depends on how rough the pipe is and how turbulent the flow is, which the Colebrook equation captures. Plotting the pressure needed against flow gives the system curve; where it crosses the pump's curve is the flow the pump will actually deliver. The suction side is checked so the pressure never falls far enough for the water to boil and cavitate.

**Scope.** Series pipe segments at a design flow: velocity, Re, Darcy friction factor, friction + fitting (K and equivalent length) losses, elevation, static head, system curve; pump curve as user points, duty point, affinity shift (speed, impeller), shaft power; NPSH available with an optional NPSH-required check; velocity limit check.

**Method.** Darcy–Weisbach; laminar `f = 64/Re` (Re < 2300, warned); Colebrook–White by fixed-point on 1/√f from the Swamee–Jain start (≤ 100 iterations, Swamee–Jain reported as a cross-check); transition 2300–4000 warned. System curve `H_static + k Q²` with k from the design flow. `H_static = Σdz + h_out` for an **open** circuit; a **closed** flow-and-return circuit (`circuit` = closed) has no static lift, the rise in the flow being recovered in the return, so `H_static = h_out` and the rises entered are ignored (an assumption records that). `V_pipe = Σ π/4·D²·L` is the water held in the segments. Duty by bisection on `H_pump(Q) − H_sys(Q)` over the curve's flow range; no crossing ⇒ warning and failed check. Affinity `Q ∝ n D³`, `H ∝ n² D²`. Water properties by published correlations (Kell 1975 density, Vogel-fit viscosity, Hyland–Wexler vapour pressure), 0–100 °C; other fluids (glycol mixtures) are entered with ρ and μ and their provenance (D-17).

**Inputs.** `fluid`, `T`, `rho_custom`, `mu_custom`, `Q` (optional: blank when a heat load gives the flow), `Phi_load`, `dT_load`, `cp_load` (a heat load, its flow-to-return difference and the fluid's specific heat; together they give the design flow `Q_design = Φ/(ρ c_p ΔT)` and replace `Q`; `Phi_load`, `dT_load` and `T` can be linked to B6's `Phi_tot`, `dT_fr` and `t_m`). `cp_load` has no default: for water a blank `cp_load` records the step "c_p of water 4186 J/(kg·K) (assumed)" and an assumption; a custom fluid must give it (block). `circuit` (open or closed), `n_pump` (optional: the speed of the pump curve as entered; gives the shaft torque and speed), `segments` (name, L, D, **OD** (optional outside diameter, added in 1.4.0: it must exceed D; the first segment's is the port `OD_1`), and the input `T_line` (1.5.0, see the outputs), ε, ΣK, L_eq, dz, side), `h_out`, `v_max`, `Q_pump`, `H_pump`, `n_ratio`, `D_ratio`, `eta_pump`, `p_atm`, `z_s`, `NPSH_r`. **Outputs.** per segment `v_s<i>`, `Re_s<i>`, `f_s<i>`, `f_sj_s<i>`, `h_f_s<i>` (≤ 50 segments); `Q_design`, `h_f_total`, `H_static`, `V_pipe` (litres; a port for B6 where the chain order allows: in the wet-heating chain B6 feeds B1's flow, so a link back would be a cycle and B6's `V_pipe` is entered equal to it), `H_sys`, `dp_total`, `dp_sys`, `P_hyd`, `P_shaft_design = P_hyd/η` and `T_shaft = P/ω` (with `n_pump`; P is the larger of `P_shaft_design` and, when a pump curve gives a duty point, `P_shaft` as entered — output `P_shaft_sel`, stated on the sheet; `T_shaft` and `n_shaft` are the ports E1 takes as its load torque and speed), `Q_duty`, `H_duty`, `P_shaft` (+ `…2` after affinity), `NPSH_a`, `v_peak`; **added in 1.4.0 and 1.5.0:** `OD_1` (the first segment's outside diameter, mm, only when its `OD` is entered; a port) and `T_line` (a port, degC): the design temperature of the line for the insulation step, being the optional input `T_line` (the coldest or hottest fluid temperature in the line, for example the 6 °C flow of a 6/12 °C chilled line whose mean `T` is 9 °C; not used for the fluid properties) or else the entered `T`; when neither is entered nothing is offered, so the 20 °C default never travels down a link. Both exist for the cold-pipe chain: B9 takes `T_line` as the process temperature `theta_i` and `OD_1` as the inside diameter of the insulation `D_i` (the bore B1 judges the flow in is not the diameter the insulation covers). Series: system curve, pump curve(s). **Checks.** duty point exists, velocity ≤ limit, NPSH_r ≤ NPSH_a.

**Test cases.** Heat-load flow `Q = Φ/(ρ c_p ΔT)` (analytic, 12 kW at 20 K, water at 70 °C); laminar oil (Hagen–Poiseuille, exact); turbulent water Re = 1e5, ε/D = 0.001 against an independently solved Colebrook value (Moody point f = 0.02237); duty point against a parabolic pump curve (analytic); `ka-closed-circuit` (the same loop closed: H_static 0, V_pipe 301.593 L). Crane TP-410 cited for the K/L_eq practice only.

**Notice rules added in 1.1.0.** `heat-flow` (block): a heat load needs its temperature difference and the reverse; with neither a heat load nor a flow there is nothing to size. `heat-flow-q` (warning): a flow typed beside a heat load is ignored. `heat-t` (warning): a heat load is given without the fluid temperature `T`, so the template's 20 °C is used. `cp_load` (block): a custom fluid on the heat-load path needs its specific heat. `eta_pump` is entered once whether it serves the pump curve, the shaft power or both.

**Fittings at low Reynolds number (1.3.0).** Fitting loss coefficients K are turbulent-flow values and rise at low Re; for a segment below Re 4000 with K entered the sheet warns (`fittings`) that the fittings term is understated. The term is used as entered.
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
Governing: FAIL  Velocity ≤ limit: u_v = 1.0045 vs u_v ≤ 1  utilisation 1
```
<!-- hand-arithmetic:end -->
