# PCB trace width (IPC-2221) (`elec.pcb-trace@1.3.0`)

**In plain terms.** A copper track on a circuit board heats up when it carries current, and the thinner it is, the hotter it gets. The worksheet uses the old IPC-2221 curve to find the narrowest trace that carries a current without exceeding a chosen temperature rise, and then the resistance, voltage drop and heat of the trace as drawn.

## Scope

Calculates the minimum width of a printed-circuit trace for a current and an allowed temperature rise by the legacy IPC-2221 curve-fit, conservative for multilayer boards. It also gives the resistance, voltage drop and I²R loss of the trace as drawn.

**Reference diagram.** The worksheet draws the trace in plan with its width, length and current, and the board in section with the copper on the layer the choice selects. Bound inputs: w_act, L, I, Cu_oz, t_oz, T_amb, dT, k_ext, k_int, n_dT and n_A. Not to scale; inputs only.

## Method

- Trace cross-section: I = k·ΔT^0.44·A^0.725, so A = [I/(k·ΔT^0.44)]^(1/0.725) with A in mil², k = 0.048 external, 0.024 internal
- Copper thickness: t = w_Cu·t_oz, t_oz = 1.378 mil per oz/ft² (35 µm, derived)
- Minimum width: w_min = A/t; utilisation u_w = w_min/w_act
- Resistivity at the operating temperature: ρ_T = ρ_20·[1 + α·(T_amb + ΔT − 20)]
- Trace resistance: R = ρ_T·L/(w_act·t); voltage drop ΔV = I·R; loss P = I²·R

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `layer` | Layer |  | External layer | External layer, Internal layer | Setup |
| `w_act` | Trace width as drawn | mm | 2 | 0.05 to 10 | Geometry |
| `L` | Trace length | mm | 50 | 1 to 1000 | Geometry |
| `Cu_oz` | Finished copper weight in oz/ft² (0.5 to 6), not a thickness in µm: 17.5 µm is ½ oz, 35 µm is 1 oz, 70 µm is 2 oz, 105 µm is 3 oz |  | 1 | 0.5 to 6 | Material |
| `rho_20` | Copper resistivity at 20 °C (annealed copper, 1.724e-8 Ω·m) | Ω·mm²/m | 0.01724 | 0.0165 to 0.0185 | Material |
| `alpha` | Temperature coefficient of resistance of copper | 1/K | 0.00393 | 0.0035 to 0.0042 | Material |
| `I` | Trace current (the curve-fit was drawn for up to 35 A: above it the sheet is refused) | A | 3 | 0.01 to 35 | Loads |
| `T_amb` | Ambient temperature at the board | °C | 25 | -40 to 125 | Conditions |
| `dT` | Allowed temperature rise above ambient | K | 10 | 1 to 100 | Requirements |
| `k_ext` | Curve-fit constant k, external layer |  | 0.048 | 0.01 to 0.1 | Method |
| `k_int` | Curve-fit constant k, internal layer |  | 0.024 | 0.005 to 0.05 | Method |
| `n_dT` | Exponent of the temperature rise |  | 0.44 | 0.3 to 0.6 | Method |
| `n_A` | Exponent of the cross-section |  | 0.725 | 0.5 to 1 | Method |
| `t_oz` | Copper thickness per oz/ft² (35 µm = 1.378 mil, derived) | mil | 1.378 | 1.2 to 1.5 | Method |

## Assumptions

- The curve-fit is the legacy IPC-2221 relation for a trace with no plane or convective help. Basis: IPC-2221 curve-fit (legacy); IPC-2152 is not implemented.
- The curve-fit was drawn for 1 to 3 oz copper and currents up to 35 A. Basis: IPC-2221 curve-fit (legacy).
- The curve-fit was drawn for rises of 10 to 100 K and widths up to 10 mm. Basis: IPC-2221 curve-fit (legacy).
- The result screens the trace width and is not a thermal analysis of the board. Basis: Scope of the curve-fit.
- The curve-fit is conservative for multilayer boards. Basis: IPC-2221 curve-fit (legacy).
- Copper thickness is the nominal 1.378 mil (35 µm) per oz/ft² of finished copper. Basis: Derived: 35 µm / 25.4 µm per mil.
- Plating build-up on outer layers and etch-back are not modelled. Basis: Scope of the sheet.
- The resistance uses the trace as drawn at the temperature ambient plus rise. Basis: Derived constants for annealed copper.
- The resistivity at 20 °C is corrected with a linear temperature coefficient. Basis: Derived constants for annealed copper.
- Vias, pads, bends and skin effect are ignored. Basis: Scope of the sheet.
- The current is steady direct current, or RMS current at low frequency. Basis: Scope of the curve-fit.
- The rise is the allowed rise in the worst case. Basis: Scope of the curve-fit.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `A_req` | Minimum cross-section | mil² |
| `w_min` | Minimum trace width | mm |
| `u_w` | Width utilisation |  |
| `trace_R` | Trace resistance | mΩ |
| `vdrop` | Voltage drop | mV |
| `trace_Ploss` | Power loss | mW |
| `dT_act` | Temperature rise of the trace as drawn | K |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `trace.resistance` | `trace_R` | ohm | Resistance of the trace as drawn at its operating temperature |
| `trace.power_loss` | `trace_Ploss` | W | I²R power loss in the trace |
| `trace.voltage_drop` | `vdrop` | V | Voltage drop along the trace |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Minimum trace width against the width as drawn (IPC-2221 cl. 6 (legacy))
- Copper weight against the range of the curve-fit (IPC-2221 cl. 6 (legacy))

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `narrow` | Warning | Conservative | The trace as drawn is narrower than the minimum for the allowed rise (u_w above 1): it runs hotter, and the resistance, drop and loss are taken at the rise the curve-fit gives for the drawn width. |
| `width` | Block | Unconservative | The minimum width exceeds 10 mm, beyond the range of the source curve-fit, so the sheet is refused. |
| `I` | Block | Unconservative | The current exceeds 35 A, beyond the range of the source curve-fit, so the sheet is refused. |
| `copper` | Warning | Unconservative | The copper weight is outside 1 to 3 oz/ft², the range the source curve-fit was drawn for: the copper-range check fails (a block would refuse the half-ounce known-answer case). |
| `rise` | Block | Unconservative | The allowed temperature rise is below 10 K, the smallest rise the source curve-fit was drawn for, so the sheet is refused. |

## Limitations

A curve-fit for steady current, drawn for 1 to 3 oz copper, up to 35 A, 10 to 100 K rise and 10 mm width; conservative for multilayer boards, not a thermal analysis of the board (no planes, vias, neighbouring heat, convection or altitude). Plating build-up and etch-back are not modelled; resistance ignores vias, pads, bends and skin effect. The resistance uses the trace as drawn at the design rise, which is conservative when the drawn trace is wider than the minimum.

## References

- IPC-2221B:2012, cl. 6: Electrical properties: current-carrying capacity of printed-board conductors (legacy curve-fit; edition to be checked)
- Hughes, Electrical and Electronic Technology, Ch. on DC circuits, 12th ed. (edition to be checked): Resistivity and temperature coefficient of resistance

## Test cases

All analytic and recomputed in python3 from the formula (Confirmed): `ka-3a-10k-1oz-external` (A 74.1616 mil², w 1.366985 mm), `ka-3a-10k-1oz-internal` (A 192.9270 mil², w 3.556130 mm), `ka-5a-20k-2oz-resistance` (w 0.907900 mm, R 10.81896 mΩ, ΔV 54.0948 mV, P 270.474 mW), `ka-half-oz-internal-narrow` (u_w 1.6046, ΔT_act 65.392 K, R 526.28 mΩ). **Unconfirmed + Secondary:** `ka-published-3a-10k-1oz` (about 1.37 mm external) and `ka-published-3a-10k-1oz-internal` (about 3.56 mm internal), the widths as the addendum quotes them from trace-width calculators, to be checked against the owner's copy of IPC-2221 and a calculator; they agree with the formula to 0.3 %.

- `ka-3a-10k-1oz-external`: 3 A, 10 K rise, 1 oz copper, external layer (analytic) (Confirmed; source: Analytic (IPC-2221 legacy curve-fit, recomputed))
- `ka-3a-10k-1oz-internal`: 3 A, 10 K rise, 1 oz copper, internal layer (analytic) (Confirmed; source: Analytic (IPC-2221 legacy curve-fit, recomputed))
- `ka-5a-20k-2oz-resistance`: 5 A, 20 K rise, 2 oz copper, external, 100 mm long at 2.5 mm; resistance, drop and loss (analytic) (Confirmed; source: Analytic (IPC-2221 legacy curve-fit, copper resistivity))
- `ka-half-oz-internal-narrow`: 1 A, 30 K rise, 0.5 oz copper, internal, 200 mm long at 0.5 mm: too narrow (analytic) (Confirmed; source: Analytic (IPC-2221 legacy curve-fit, copper resistivity))
- `ka-published-3a-10k-1oz`: 3 A, 10 K, 1 oz: widely quoted widths of about 1.37 mm external and 3.56 mm internal (Unconfirmed; source: IPC-2221 trace-width calculators and PCB design guides as quoted second-hand)
- `ka-published-3a-10k-1oz-internal`: 3 A, 10 K, 1 oz, internal layer: about 3.56 mm (Unconfirmed; source: IPC-2221 trace-width calculators and PCB design guides as quoted second-hand)

## Changes

**Changes in 1.3.0 (C-2 consistency, phase C).** Layout and wording only; no result changes. The check compares `w_min` with `w_act` in mm. The hard-limit rule of `I` is declared as `I`, because the engine adds the `input.` prefix. The inputs are grouped Setup, Geometry, Material, Loads, Conditions, Requirements and Method. The ports are named `trace.*`, and the titles of the two record kinds repeat the noun of their id. The scope, the assumptions and the references follow the sheet standard.

**Changes in 1.1.0 (elec.pcb-trace-02).** When the trace as drawn is narrower than the minimum (`u_w` above 1) it runs hotter than the allowed rise, so its resistance, drop and loss are no longer taken at ambient plus the allowed rise. The curve-fit is solved for the rise at the drawn cross-section, `ΔT_act = [I/(k·A_act^n_A)]^(1/n_dT)` with `A_act` in mil², and `R`, `ΔV`, `P` use `T_amb + ΔT_act`; the output `dT_act` and the warning `narrow` say so. ka-half-oz-internal-narrow (1 A, 0.5 oz, internal, 200 mm at 0.5 mm, 40 °C ambient): the drawn trace runs at 65.392 K, `R` 526.28 mΩ (471.47 mΩ at the allowed 30 K, 10.4 % low). A trace that is wide enough keeps the allowed rise, the higher resistance and so the conservative one.

**Changes in 1.2.0 (C-2 fix, F2).** Four notices that said the curve-fit was extrapolated, and let the sheet pass, now stop it: `input.I` (the hard limit of `I` is 35 A), `rise` (dT under 10 K) and `width` (w_min above 10 mm) are blocks; `copper` (Cu_oz outside 1 to 3 oz/ft²) keeps its warning and fails the check "Copper weight against the range of the curve-fit", a failing check and not a block because the known-answer case `ka-half-oz-internal-narrow` (0.5 oz) is a shipped case. python3: 15 A at 10 K on 1 oz external copper needs w_min = 12.585 mm, which the sheet used to pass at 13 mm; it now refuses. The other known-answer cases and the worked example are inside the range and are unchanged.

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
Governing: PASS  Minimum trace width against the width as drawn: 0.9079 mm vs 2.500 mm  utilisation 0.36  « IPC-2221 cl. 6 (legacy)
```
<!-- hand-arithmetic:end -->
