# Load budget, PSU, transformer and battery (`elec.load-budget@1.6.1`)

**In plain terms.** A supply must be sized for what is connected, not just the sum of the nameplates, because loads are not all on at once. The worksheet adds each load with its duty and diversity, gives the current on each supply, sizes the DC power supply, the transformer (including the demand when motors start) and the battery for the backup time you need, and reports the heat released inside the enclosure.

## Scope

Calculates the demand of a set of loads per supply and the minimum DC power supply, transformer and battery for it. It also gives the heat the loads dissipate for the enclosure calculation.

**Reference diagram.** The worksheet draws the three buses with one load on each for the loads as listed, the additional load, the DC power supply fed from the bus the choice selects, the transformer and the battery. Bound inputs: loads, psu_bus, U_3ph, U_1ph, U_DC, P_add, eta_psu, k_T, U_b, t_auto, T_amb, SF, DoD and k_age. Not to scale; inputs only.

## Method

- Demand per load: P_i·d_i·k_i; apparent power P_i·d_i·k_i/cosφ_i (arithmetic sum, conservative); this duty-weighted demand sizes the transformer and the enclosure heat
- Running load of each load while it is on: P_i·k_i, apparent P_i·k_i/cosφ_i (the duty d is the share of time on, so it is left out: a cable, a bus and a power supply carry the load while it is on)
- Bus currents: I_3φ = S_3φ,run/(√3·U), I_1φ = S_1φ,run/U, I_DC = P_DC,run/U_DC, from the running loads; when a bus is chosen for the DC power supply its input P_DC,run/η_PSU (power factor 1) is added to that bus
- Heat dissipated inside the enclosure: Φ = Σ h_i·P_i·d_i·k_i + P_DC·(1/η_PSU − 1), h_i = the share of the input power released inside (blank: the loss 1 − η_i; electronics 1; a machine outside the enclosure 0); the second term is the power supply's own loss, present when an AC bus is chosen for it
- Power supply sizing: P_PSU ≥ SF·P_DC,run/f_derate (the running DC load, duty not applied; f_derate from the manufacturer's curve at T_amb and altitude)
- Transformer sizing: S_T ≥ SF·S for the running load; starting peak S_peak = S + max(m_i·P_i/cosφ_i − S_i) ≤ k_T·S_T (the starting load undiversified), so S_T ≥ S_peak/k_T for the start; the rating to buy is the larger of the two
- Battery sizing: C = I_b·t/(DoD·η_b·k_temp·k_age), I_b = Σ backed P_i·d_i·k_i/(U_b·η_conv); Peukert: I_b·H·(t/H)^(1/k_p) in place of I_b·t, never below I_b·t (the exponent is not valid beyond the rated hour rate H)

### Notes on the method

Per load `P·d·k` and `P·d·k/cosφ` (arithmetic sum, conservative) for the demand, the transformer, the heat and the battery energy; the bus currents and the PSU rating use the running load `P·k` (duty not applied): `I_3φ = S_3φ,run/(√3 U)`, `I_1φ = S_1φ,run/U`, `I_DC = P_DC,run/U_DC`, `P_PSU ≥ margin·P_DC,run/f_derate`; with `psu_bus` set, the DC power supply's input `P_DC/η_PSU` (power factor 1) is added to that AC bus, the DC loads count at `P/η_PSU` in `S` and the supply's loss `P_DC(1/η_PSU − 1)` is added to `Φ` (default: not counted, with a warning when DC loads exist: G-B-12). `Φ = Σ h P d k` with `h` the load's `heat` share (blank: the loss `1 − η` of a drive or supply; 1 for electronics inside the panel; 0 for a machine outside it), so a linked motor adds no panel heat (`heat_add` = 0) (G-B-04). `P_PSU ≥ margin·P_DC/f_derate`. `S_T_req = margin·S` for the running load; `S_peak = S + max(m P/cosφ − S_i) ≤ k_T·S_T` (the starting load undiversified; loads with no demand do not start), so `S_T_req_start = S_peak/k_T`; `S_T_min` is the larger of the two and is the rating that satisfies both of the sheet's transformer checks (B-A-09). Battery `C = I_b t/(DoD η_b k_temp k_age)`, `I_b = Σ backed P d k/(U_b η_conv)`; optional Peukert `I_b H (t/H)^(1/k_p)` (Linden 5th ed.), not below `I_b t` beyond the rated hour rate.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `loads` | Loads |  | table | columns: name, supply, P (kW), cosphi, eta, heat, d, k, inrush, backed | Setup |
| `psu_bus` | AC bus that feeds the DC power supply (its input P_DC/η_PSU is added to that bus) |  | not counted on an AC bus | not counted on an AC bus, AC single-phase, AC three-phase | Setup |
| `P_add` | Additional three-phase load (linked from the motor-sizing RMS power) | kW | 0 | 0 to 100 | Loads |
| `cosphi_add` | Power factor of the additional load |  | 0.85 | 0.6 to 1 | Loads |
| `eta_add` | Efficiency of the additional load |  | 0.9 | 0.6 to 1 | Loads |
| `inrush_add` | Starting multiple of the additional load |  | 1 | 1 to 10 | Loads |
| `heat_add` | Share of the additional load's input power released inside the enclosure (a linked machine is outside it: 0) |  | 0 | 0 to 0.5 | Loads |
| `P_rated_add` | Rated (nameplate) shaft power of the additional machine (linked from the motor-sizing P_rated); the supply carries the larger of this and the duty power P_add (optional; blank = the machine is sized on its duty only) (optional) | kW |  | 0 to 100 | Loads |
| `U_3ph` | Three-phase line voltage | V | 400 | 12 to 690 | Conditions |
| `U_1ph` | Single-phase voltage | V | 230 | 12 to 480 | Conditions |
| `U_DC` | DC bus voltage | V | 24 | 12 to 600 | Conditions |
| `T_amb` | Ambient temperature | °C | 40 | 0 to 60 | Conditions |
| `U_b` | Battery nominal voltage | V | 24 | 2 to 1000 | Conditions |
| `eta_psu` | DC power supply efficiency (power factor taken as 1) |  | 0.9 | 0.7 to 1 | Factors |
| `SF` | Sizing safety factor |  | 1.25 | 1 to 1.4 | Factors |
| `f_derate` | PSU derating factor at the ambient and altitude |  | 1 | 0.1 to 1 | Factors |
| `k_T` | Transformer short-time overload allowed for inrush |  | 3 | 1 to 20 | Factors |
| `eta_conv` | Conversion efficiency battery → loads |  | 1 | 0.7 to 1 | Factors |
| `DoD` | Allowed depth of discharge |  | 0.8 | 0.1 to 1 | Factors |
| `eta_b` | Battery discharge efficiency |  | 0.95 | 0.7 to 1 | Factors |
| `k_temp` | Temperature capacity factor |  | 1 | 0.3 to 1.2 | Factors |
| `k_age` | Ageing (end-of-life) factor |  | 0.8 | 0.5 to 1 | Factors |
| `k_p` | Peukert exponent (optional) |  |  | 1 to 1.5 | Factors |
| `H_rate` | Hour rate of the rated capacity (Peukert) (optional) | h |  | 1 to 100 | Factors |
| `t_auto` | Battery autonomy | h | 0 | 0 to 48 | Requirements |
| `P_PSU` | Selected PSU rating (optional) | kW |  | 0.001 to 100 | PSU |
| `S_T` | Selected transformer rating (optional) | kVA |  | 0.01 to 10000 | Transformer |
| `C_batt` | Selected battery capacity (optional) | A·h |  | 0.1 to 100000 | Battery |

## Assumptions

- Each load's P is its electrical input. Basis: Load budget convention.
- DC loads count at power factor 1 in the transformer demand. Basis: Load budget convention.
- With a PSU bus chosen, DC loads count at P/η_PSU, which already includes the supply's own loss. Basis: Load budget convention.
- The sheet adds that loss to Φ itself, as P_DC·(1/η_PSU − 1), whenever psu_bus and η_PSU are set. Basis: Load budget convention.
- Do not add the power supply as a load row, which would count its loss twice. Basis: Load budget convention.
- The heat released inside the enclosure is h·P·d·k per load. Basis: Stated convention for the heat a load releases inside the panel.
- The default h = 1 − η is the loss of a drive or supply. Basis: Stated convention for the heat a load releases inside the panel; change h for a load that differs.
- Electronics release all their input (h = 1) and a machine outside the panel releases none (h = 0). Basis: Stated convention for the heat a load releases inside the panel; change h for a load that differs.
- Apparent powers are added arithmetically, which is conservative when power factors differ. Basis: Upper bound on S.
- PSU derating, transformer overload and battery factors are the manufacturer's values, entered as records. Basis: Entered as reference records, which carry their source, edition and licence.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `P_conn` | Connected load | kW |
| `P_dem` | Demand active power | kW |
| `S_dem` | Demand apparent power | kVA |
| `I_3ph` | Three-phase bus current | A |
| `I_1ph` | Single-phase bus current | A |
| `I_DC` | DC bus current | A |
| `Phi` | Heat dissipated inside the enclosure | kW |
| `P_PSU_req` | Minimum PSU rating | kW |
| `S_T_req` | Minimum transformer rating, running load | kVA |
| `S_peak` | Peak apparent power at start | kVA |
| `S_T_req_start` | Minimum transformer rating, starting peak | kVA |
| `S_T_min` | Minimum transformer rating, running and starting | kVA |
| `I_b` | Battery discharge current | A |
| `Ah` | Discharge capacity | A·h |
| `C_req` | Minimum battery capacity | A·h |
| `U_L` | Three-phase line voltage | V |
| `pf_link` | Power factor of the linked machine |  |
| `u_batt` | Battery utilisation |  |
| `u_PSU` | PSU utilisation |  |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `load.power_demand` | `P_dem` | kW | Demand active power |
| `load.apparent_demand` | `S_dem` | kVA | Demand apparent power |
| `load.current_three_phase` | `I_3ph` | A | Three-phase bus current of the loads while they are on, duty not applied (→ conductor-sizing I_b_in) |
| `load.current_single_phase` | `I_1ph` | A | Single-phase bus current |
| `load.current_dc` | `I_DC` | A | DC bus current |
| `load.heat_dissipated` | `Phi` | kW | Heat dissipated inside the enclosure (→ enclosure-heat P_add) |
| `load.voltage_line` | `U_L` | V | Three-phase line voltage of the bus (→ voltage-drop V_n) |
| `load.power_factor_linked` | `pf_link` |  | Power factor of the linked machine (→ voltage-drop cos_phi) |
| `battery.capacity_required` | `C_req` | A*h | Minimum battery capacity |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Required battery capacity against the selected capacity
- Required PSU rating against the selected rating

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `no-loads` | Block | Conservative | No loads: nothing to budget. |
| `cosphi` | Block | Conservative | An AC load row without its power factor: the current, the apparent power and the transformer demand cannot be stated (a blank is not taken as 1). |
| `load-defaults` | Warning | Unconservative | A load row with a blank optional cell (η, d, k or inrush): the blank is taken as 1, which understates the heat (η) and the starting peak (inrush) and overstates the demand (d, k). |
| `psu_bus` | Warning | Unconservative | DC loads with no AC bus chosen for their power supply: the AC bus currents and the transformer demand omit its input. |
| `derate` | Warning | Unconservative | Ambient above 40 °C with no PSU derating entered. |
| `rated-add` | Warning | Unconservative | A linked machine with no rated power: the supply is sized on its duty power only, and the machine's rating can be higher. |
| `battery-charge` | Warning | Unconservative | A battery-backed load with an autonomy: the power supply rating omits the battery's charging power. |
| `peukert-range` | Warning | Conservative | An autonomy beyond the Peukert rated hour rate: the capacity is not taken below the plain product I_b·t. |
| `peukert` | Warning | Conservative | Peukert needs both the exponent and the hour rate. |

## Limitations

No harmonic or unbalance assessment; inrush only for the single largest start.

## References

- Reddy (ed.), Linden's Handbook of Batteries, Ch. 3–4, 5th ed.: Battery sizing and the Peukert relation
- BS EN 60204-1:2018, cl. 4.3: Electrical supply (supply voltage tolerance)

## Test cases

- `ka-mixed-buses`: Pump, fan, lighting and controls on three buses (Confirmed; source: Analytic (load budget arithmetic))
- `ka-battery`: 24 V DC battery for controls and comms, all derates (Confirmed; source: Analytic (capacity with derates))
- `ka-peukert`: Peukert-adjusted capacity (Linden's Handbook relation) (Unconfirmed; source: Linden's Handbook of Batteries)
- `ka-psu-derating`: 24 V PSU at 70 °C with the datasheet derating (manufacturer pattern) (Unconfirmed; source: Phoenix Contact QUINT4-PS/1AC/24DC/10 datasheet)

## Changes

**1.6.1.** Declares the unit change of 1.6.0 (ADR-0177 review): `P_PSU` was in W before 1.6.0, so a bare stored number from an earlier version is read in W, never kW. No calculated value changes.

**Changes in 1.6.0 (C-2 consistency, phase C).** Layout and wording; no result changes. `margin` is `SF` (the sizing safety factor) and `theta` is `T_amb`; the old symbols stay as aliases. `P_PSU` and `P_PSU_req` are in kW (the value is the same in SI). The checks compare physical values: the required PSU rating with `P_PSU`, the required transformer rating with `S_T`, the starting peak `S_peak` with the new step `S_T_surge` (`k_T` times `S_T`), and `C_req` with `C_batt`. The loads column `cosphi` keeps its key, because a table column has no alias mechanism. The inputs are grouped Setup, Loads, Conditions, Factors, Requirements, PSU, Transformer and Battery, and every usual range is inside its hard limits.

**Changes in 1.3.0 (elec.load-budget-03, -R1).** The bus currents `I_3ph`, `I_1ph`, `I_DC` and the PSU rating `P_PSU_req` are for the loads while they are on: `P·k` (and `P·k/cosφ`), not `P·d·k`, because the duty `d` is the share of time on and a cable, a bus or a supply carries the load while it is on. The demand `P_dem`, `S_dem`, the transformer rating, the starting peak, the enclosure heat `Phi` and the battery energy stay duty-weighted. A load row with blank optional cells (`eta` when `heat` is blank, `d`, `k`, `inrush` on an AC row) now warns by row and cell (rule `load-defaults`); an AC row with a blank `cosphi` is refused (rule `cosphi`, block) instead of being read as 1.

**Changes in 1.5.1 (merge of the C-1 line's 1.4.1 and the C-2 line's 1.5.0).** The C-1 line had 1.4.1 (the supply-loss assumption) and the C-2 line 1.5.0 (the public port names `load.*` and `battery.*` with their aliases); the merged template is 1.5.1, above both. No number changes from the merge.

**Changes in 1.4.1 (elec.load-budget-05, from the C-1 verification).** The load-convention assumption no longer says the power supply's own loss is "a load row if its heat is wanted": since 1.4.0 `Phi` adds `P_DC·(1/η_PSU − 1)` itself whenever `psu_bus` and `η_PSU` are set, so a row for the supply would count the loss twice. It now says the sheet adds the loss itself and the supply is not to be entered as a load row. Text only; no number changes.

**Changes in 1.4.0 (elec.load-budget-04, -05, -06).** `Phi` now includes the DC power supply's own loss `P_DC·(1/η_PSU − 1)` when an AC bus is chosen for it (`psu_bus`): 120 W of DC electronics at η 0.9 with the supply at η 0.9 releases 12 W + 13.33 W = 25.33 W inside the panel, not 12 W; with `psu_bus` left at 'not counted' the heat is as before. A battery-backed load with an autonomy raises the warning `battery-charge`: the PSU rating covers the DC loads only, not the battery's charging power (168 W backed load, 4 h, PSU 240 W gave u_PSU 0.875 and a pass with no charge in it). With Peukert, an autonomy beyond the rated hour rate `H_rate` raises `peukert-range` and the capacity is not taken below the plain product `I_b·t` (10 A, 72 h, H 20 h, k_p 1.3: 535.7 A·h before, 720 A·h now). Chain additions in the same version (chain.motor-circuit-04, -07): the optional input `P_rated_add`, the rated shaft power of the linked machine (E1's `P_rated`): the supply carries the larger of it and the duty power `P_add` (a 20 N·m motor on the duty of a 2 N·m one: I_3φ 1.3727 A before, 10.9994 A now; a rating below the duty changes nothing), and a linked machine without it raises the warning `rated-add`; and the ports `U_L` (the three-phase line voltage) and `pf_link` (the linked machine's power factor), which the motor circuit pack links to the voltage-drop step.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Pump skid control panel: supply, PSU, transformer and UPS battery ([sheet](../../worked-examples/electrical/elec.load-budget/pump-skid-panel/sheet.txt)).

```text
L1 = 11 × 1 × 1 = 11 kW; 12.791 kVA = 11 kW
L2 = 11 × 1 × 0 = 0 kW; 0 kVA = 0 kW
L3 = 0.06 × 1 × 1 = 0.06 kW; 0.066667 kVA = 0.06 kW
L4 = 0.02 × 0.1 × 1 = 0.002 kW; 0.0021053 kVA = 0.002 kW
L5 = 0.096 × 1 × 1 = 0.096 kW; 0.10667 kVA = 0.096 kW
L6 = 0.024 × 1 × 1 = 0.024 kW; 0.026667 kVA = 0.024 kW
P_conn = 11 + 11 + 0.06 + 0.02 + 0.096 + 0.024 = 22.2 kW
P_dem = 11 + 0 + 0.06 + 0.002 + 0.096 + 0.024 = 11.182 kW
S_dem = 12.791 + 0 + 0.066667 + 0.0021053 + 0.10667 + 0.026667 = 12.993 kVA
I_3ph = 12791 / (√3 × 400) = 18.462 A
I_1ph = 221.05 / 230 = 0.9611 A
I_DC = 120 / 24 = 5 A
Phi = 0.09×11 + 0.09×0 + 0.3×0.06 + 0×0.002 + 0.1×0.096 + 0.1×0.024 + PSU loss 0.013333 = 1.0333 kW
P_DC = 120 W
… 16 more step(s) on the sheet
Governing: FAIL  Starting peak against the transformer short-time rating: 83.35 kVA vs 75 kVA  utilisation 1.12
```
<!-- hand-arithmetic:end -->
