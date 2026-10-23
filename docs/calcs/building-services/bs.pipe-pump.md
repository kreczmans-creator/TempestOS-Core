# B1 — Pipe pressure drop and pump duty (`bs.pipe-pump@1.0.0`)

**In plain terms.** Water loses pressure as it flows, by friction along the pipe and at every bend, valve and fitting. The friction factor depends on how rough the pipe is and how turbulent the flow is, which the Colebrook equation captures. Plotting the pressure needed against flow gives the system curve; where it crosses the pump's curve is the flow the pump will actually deliver. The suction side is checked so the pressure never falls far enough for the water to boil and cavitate.

**Scope.** Series pipe segments at a design flow: velocity, Re, Darcy friction factor, friction + fitting (K and equivalent length) losses, elevation, static head, system curve; pump curve as user points, duty point, affinity shift (speed, impeller), shaft power; NPSH available with an optional NPSH-required check; velocity limit check.

**Method.** Darcy–Weisbach; laminar `f = 64/Re` (Re < 2300, warned); Colebrook–White by fixed-point on 1/√f from the Swamee–Jain start (≤ 100 iterations, Swamee–Jain reported as a cross-check); transition 2300–4000 warned. System curve `H_static + k Q²` with k from the design flow. Duty by bisection on `H_pump(Q) − H_sys(Q)` over the curve's flow range; no crossing ⇒ warning and failed check. Affinity `Q ∝ n D³`, `H ∝ n² D²`. Water properties by published correlations (Kell 1975 density, Vogel-fit viscosity, Hyland–Wexler vapour pressure), 0–100 °C; other fluids (glycol mixtures) are entered with ρ and μ and their provenance (D-17).

**Inputs.** `fluid`, `T`, `rho_custom`, `mu_custom`, `Q`, `segments` (name, L, D, ε, ΣK, L_eq, dz, side), `h_out`, `v_max`, `Q_pump`, `H_pump`, `n_ratio`, `D_ratio`, `eta_pump`, `p_atm`, `z_s`, `NPSH_r`. **Outputs.** per segment `v_s<i>`, `Re_s<i>`, `f_s<i>`, `f_sj_s<i>`, `h_f_s<i>` (≤ 50 segments); `h_f_total`, `H_static`, `H_sys`, `dp_total`, `dp_sys`, `P_hyd`, `Q_duty`, `H_duty`, `P_shaft` (+ `…2` after affinity), `NPSH_a`, `v_peak`. Series: system curve, pump curve(s). **Checks.** duty point exists, velocity ≤ limit, NPSH_r ≤ NPSH_a.

**Test cases.** Laminar oil (Hagen–Poiseuille, exact); turbulent water Re = 1e5, ε/D = 0.001 against an independently solved Colebrook value (Moody point f = 0.02237); duty point against a parabolic pump curve (analytic). Crane TP-410 cited for the K/L_eq practice only.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Chilled-water riser: pressure drop, duty point, speed turn-down ([sheet](../../worked-examples/building-services/bs.pipe-pump/chilled-water-riser-duty/sheet.txt)).

```text
rho = T = 6 °C = 999.94 kg/m³
mu = T_K = 279.15 K = 1.4684 mPa·s
p_v = T = 6 °C = 0.93525 kPa
v_s1 = 30 m³/h / (π × 100² mm² / 4) = 1.061 m/s
Re_s1 = 999.94 × 1.061 × 0.1 / 0.0014684 = 72253
f_s1 = Colebrook–White, 10 iterations (Swamee–Jain start 0.021152) = 0.021093
f_sj_s1 = 0.25 / log10(ε/3.7D + 5.74/Re^0.9)² = 0.021152
h_f_s1 = 0.021093 × (8 + 0) / 0.1 × 0.057399 + 3 × 0.057399 = 0.26906 m
v_s2 = 30 m³/h / (π × 80² mm² / 4) = 1.6579 m/s
Re_s2 = 999.94 × 1.6579 × 0.08 / 0.0014684 = 90316
f_s2 = Colebrook–White, 10 iterations (Swamee–Jain start 0.020946) = 0.020849
f_sj_s2 = 0.25 / log10(ε/3.7D + 5.74/Re^0.9)² = 0.020946
h_f_s2 = 0.020849 × (60 + 0) / 0.08 × 0.14014 + 8 × 0.14014 = 3.3123 m
v_s3 = 30 m³/h / (π × 65² mm² / 4) = 2.5113 m/s
… 22 more step(s) on the sheet
Governing: FAIL  Velocity ≤ limit: u_v = 1.0045 vs u_v ≤ 1  utilisation 1
```
<!-- hand-arithmetic:end -->
