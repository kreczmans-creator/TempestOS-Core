# E7 — PCB trace width, legacy IPC-2221 curve-fit (`elec.pcb-trace@1.1.0`)

**In plain terms.** A copper track on a circuit board heats up when it carries current, and the thinner it is, the hotter it gets. The worksheet uses the old IPC-2221 curve to find the narrowest trace that carries a current without exceeding a chosen temperature rise, and then the resistance, voltage drop and heat of the trace as drawn.

**Scope.** The minimum width of a printed-circuit trace for a current and an allowed temperature rise, then the resistance, voltage drop and I²R loss of the trace as drawn at its operating temperature (ambient plus rise). External or internal layer; copper weight in oz/ft². The sheet is labelled **"legacy IPC-2221 curve-fit, conservative for multilayer boards"**. The IPC-2152 method (charts, plane and board-thickness factors) is not implemented and none of its data ships (Addendum B §B5); IPC-2221 is cited by section number only.

**Method.** `I = k·ΔT^0.44·A^0.725` with A in mil², k = 0.048 (external) or 0.024 (internal), so `A = [I/(k·ΔT^0.44)]^(1/0.725)`. Thickness `t = Cu_oz · 1.378 mil` (35 µm per oz/ft², derived). `w_min = A/t`; utilisation `u_w = w_min/w_act`. `T = T_amb + ΔT`; `ρ_T = ρ_20 [1 + α (T − 20)]` (annealed copper, 0.01724 Ω·mm²/m and 0.00393 per K, derived physical constants); `R = ρ_T L/(w_act · t)`; `ΔV = I R`; `P = I² R`.

**Inputs.** `I` 0.01–35 A (usual range; hard limits 1 mA–1 kA), `dT` 1–100 K, `T_amb`, `layer` (External layer / Internal layer), `Cu_oz` 0.5–6 oz/ft² (hard limits 0.1–6: a copper thickness typed in µm, such as 18, 35 or 70, is refused with the reminder that 35 µm is 1 oz), `w_act` trace width as drawn, `L` trace length. Method record `pcb.method` (`k_ext`, `k_int`, `n_dT`, `n_A`, `t_oz`) and material record `conductor.material` (`rho_20`, `alpha`): ordinary editable inputs that a reference record fills, each carrying its source and edition. Copper resistivity is not a W3 material property (the W3 catalogue has no electrical resistivity), so no `MaterialProperty` binding applies.

**Outputs.** `A_req` (mil²), `w_min`, `u_w`, `trace_R`, `vdrop`, `trace_Ploss`. **Ports.** `trace_R`, `trace_Ploss`, `vdrop` (`pcb.trace_R`, `pcb.trace_Ploss`, `pcb.vdrop`; links E6, E2, E3). **Checks.** width as drawn against the minimum (PASS/FAIL on `u_w ≤ 1`). **Warnings.** current above 35 A (the usual-range notice on `I`); minimum width above 10 mm (`width`); copper weight outside 1 to 3 oz/ft² (`copper`) and a rise below 10 K (`rise`), outside the range the curve-fit was drawn for; constants not taken from a record.

**Limitations.** A curve-fit for steady current, drawn for 1 to 3 oz copper, up to 35 A, 10 to 100 K rise and 10 mm width; conservative for multilayer boards, not a thermal analysis of the board (no planes, vias, neighbouring heat, convection or altitude). Plating build-up and etch-back are not modelled; resistance ignores vias, pads, bends and skin effect. The resistance uses the trace as drawn at the design rise, which is conservative when the drawn trace is wider than the minimum.

**Changes in 1.1.0 (elec.pcb-trace-02).** When the trace as drawn is narrower than the minimum (`u_w` above 1) it runs hotter than the allowed rise, so its resistance, drop and loss are no longer taken at ambient plus the allowed rise. The curve-fit is solved for the rise at the drawn cross-section, `ΔT_act = [I/(k·A_act^n_A)]^(1/n_dT)` with `A_act` in mil², and `R`, `ΔV`, `P` use `T_amb + ΔT_act`; the output `dT_act` and the warning `narrow` say so. ka-half-oz-internal-narrow (1 A, 0.5 oz, internal, 200 mm at 0.5 mm, 40 °C ambient): the drawn trace runs at 65.392 K, `R` 526.28 mΩ (471.47 mΩ at the allowed 30 K, 10.4 % low). A trace that is wide enough keeps the allowed rise, the higher resistance and so the conservative one.

**Test cases.** All analytic and recomputed in python3 from the formula (Confirmed): `ka-3a-10k-1oz-external` (A 74.1616 mil², w 1.366985 mm), `ka-3a-10k-1oz-internal` (A 192.9270 mil², w 3.556130 mm), `ka-5a-20k-2oz-resistance` (w 0.907900 mm, R 10.81896 mΩ, ΔV 54.0948 mV, P 270.474 mW), `ka-half-oz-internal-narrow` (u_w 1.6046, ΔT_act 65.392 K, R 526.28 mΩ). **Unconfirmed + Secondary:** `ka-published-3a-10k-1oz` (about 1.37 mm external) and `ka-published-3a-10k-1oz-internal` (about 3.56 mm internal), the widths as the addendum quotes them from trace-width calculators, to be checked against the owner's copy of IPC-2221 and a calculator; they agree with the formula to 0.3 %.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Motor-driver supply rail on a 2 oz outer layer, 5 A at 20 K rise ([sheet](../../worked-examples/electrical/elec.pcb-trace/motor-driver-supply-rail/sheet.txt)).

```text
k = 0.048
I_n = 5 A / 1 A = 5
dT_n = 20 K / 1 K = 20
A_req = (5 / (0.048 × 20^0.44))^(1 / 0.725) × 1 mil^2 = 98.511 mil²
t_Cu = 2 × 1.378 mil = 70.002 um
w_min = 98.511 mil² / 70.002 um = 0.9079 mm
u_w = 0.9079 mm / 2.5 mm = 0.36316
A_act = 2.5 mm × 70.002 um = 0.17501 mm²
T_trace = 25 degC + 20 K = 45 degC
rho_T = 0.01724 ohm·mm²/m × (1 + 0.00393 1/K × (45 degC − 20 degC)) = 0.018934 ohm·mm²/m
trace_R = 0.018934 ohm·mm²/m × 100 mm / 0.17501 mm² = 10.819 mohm
vdrop = 5 A × 10.819 mohm = 54.095 mV
trace_Ploss = (5 A)^2 × 10.819 mohm = 270.47 mW
Governing: PASS  Trace width as drawn against the minimum for the rise: u_w = 0.36316 vs u_w ≤ 1  utilisation 0.363  « IPC-2221 §6 (legacy)
```
<!-- hand-arithmetic:end -->
