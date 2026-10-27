# Conductor sizing and overload coordination (`elec.conductor-sizing@1.4.0`)

**In plain terms.** A cable must be big enough to carry the circuit's current without overheating, and the device protecting it must trip before the cable is damaged. The worksheet takes the load current, picks the smallest standard device that carries it, finds the smallest cable whose rated capacity, once reduced for temperature and grouping, carries the device rating, and checks that the device really protects the cable.

## Scope

Calculates the design current, the smallest preferred device rating and the smallest conductor whose tabulated capacity carries that rating. It checks I_b ≤ I_n ≤ I_z and I₂ ≤ 1.45 I_z, using your own capacity and rating records.

## Method

- Design current: I_b = P/(√3·U·cosφ·η) three-phase, P/(U·cosφ·η) single-phase, P/U DC; or the linked I_b
- Protective device rating: I_n = smallest preferred rating in the ratings record with I_n ≥ I_b
- Fuse factor: C_c = 0.725 for a semi-enclosed fuse (1.45/2, derived), else 1
- Required tabulated capacity: I_t,req = I_n/(C_a·C_g·C_i·C_c); optional derived C_a = √[(θ_p − θ_a)/(θ_p − θ_ref)] (derived approximation — compare with the licensed table)
- Conductor cross-section: CSA = smallest cross-section in the ampacity record with I_t ≥ I_t,req; AWG gauge n: d = 0.127·92^((36 − n)/39) mm, area = π d²/4 (derived)
- Carrying capacity: I_z = I_t·C_a·C_g·C_i
- Device and conductor coordination: I_b ≤ I_n ≤ I_z and I₂ ≤ 1.45·I_z, I₂ = 1.45·I_n inherent for MCB types B, C, D; I₂ = 2·I_n by default for a semi-enclosed fuse; an entered I₂ (datasheet) is always checked; in the BS 7671 installation context a gG fuse to BS 88 (and an MCCB or MPCB entered without I₂, taken as I₂ ≤ 1.45·I_n) has the I₂ condition deemed satisfied when I_n ≤ I_z; in the machine-wiring context (IEC 60269-1 gG fuses have I₂ = 1.6·I_n, not 1.45·I_n) I₂ must be entered
- Device code: 1 = MCB type B, 2 = type C, 3 = type D, 4 = fuse, MCCB or MPCB (carried to fault-protection so the fault check uses the device chosen here)

### Notes on the method

- `I_b = P/(√3·U·cosφ·η)` three-phase, `P/(U·cosφ·η)` single-phase, with `P` the **rated output (shaft) power** and η converting it to input; `P/U` DC, where `P` is the electrical power drawn (power factor and efficiency not applied); or the linked `I_b_in`, which replaces the computed value.
- `I_n` = smallest rating in the `ratings` record with `I_n ≥ I_b`.
- `C_c = 0.725` for a semi-enclosed fuse only (1.45 divided by an assumed fusing factor of 2, derived), else 1.
- `I_t,req = I_n/(C_a·C_g·C_i·C_c)`. `C_a` is entered, or with `derive_Ca` derived as `√[(T_user − T_amb)/(T_user − T_ref)]`, labelled "derived approximation — compare with the licensed table" (T_user 70 °C for PVC, 90 °C for XLPE, the entered `T_user` for "user"; T_ref default 30 °C).
- `CSA` = smallest cross-section in the `ampacity` record with `I_t ≥ I_t,req` (row order does not matter). With the AWG system the row carries the gauge n and the area is the derived geometry `d = 0.127·92^((36 − n)/39) mm`, `A = π d²/4`; no AWG capacity is supplied.
- `I_z = I_t·C_a·C_g·C_i` (the factors actually applied; C_c is not part of it).
- Checks (BS 7671 reg. 433.1.1; BS EN 60204-1 cl. 12.4 and 7.2 in the machine context): `I_b ≤ I_n`, `I_n ≤ I_z`, `I₂ ≤ 1.45·I_z`. For MCB types B, C, D `I₂ = 1.45·I_n` is inherent. An entered `I2` (datasheet) is always checked. For a semi-enclosed fuse `I₂ = 2·I_n` by default (reg. 433.1.202). In the **BS 7671 installation context** a gG fuse to BS 88 without `I2` shows no `I₂`: the condition is deemed satisfied when `I_n ≤ I_z` (reg. 433.1.201, cited by number; shown as a check with its real ratio `I_n/I_z`), and an MCCB or MPCB without `I2` is treated the same with a warning that this presumes `I₂` not above 1.45·`I_n`. In the **machine-wiring context** a gG fuse, MCCB or MPCB without `I2` is **blocked** (`I2:`): an IEC 60269-1 gG fuse has `I₂ = 1.6·I_n`, not 1.45·`I_n`, so the BS 88 deeming rule is not carried over (Green G-B-02, G-B-13). The two regulation numbers 433.1.201 and 433.1.202 are cited as given to me and are to be checked against the owner's copy.

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram (ADR-0158, DI-08). It is not to scale and shows inputs only, never a result. It draws the circuit: the supply, the protective device, the conductors of the circuit length, and the load. One variant per `supply`: three line conductors, or line and neutral, or two DC conductors. It binds `U`, `device`, `P`, `L`, `system`, `cos_phi`, `eta`, `T_amb`, `N_g`, `C_a`, `C_g`, `C_i`, `ratings`, `insulation` and `supply`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `context` | Design context (changes clause citations and the scope note only) |  | BS EN 60204-1 machine wiring | BS EN 60204-1 machine wiring, BS 7671 installation | Setup |
| `supply` | Supply |  | three-phase | three-phase, single-phase, DC | Setup |
| `device` | Protective device |  | MCB type C | MCB type B, MCB type C, MCB type D, gG fuse, semi-enclosed fuse, MCCB, MPCB | Setup |
| `insulation` | Insulation |  | PVC 70 °C | PVC 70 °C, XLPE 90 °C, user | Setup |
| `system` | Conductor system (AWG: geometry only, no AWG capacities are supplied) |  | metric | metric, AWG | Setup |
| `method` | Installation method key (descriptive only, e.g. B2, C, E) (optional) |  |  |  | Setup |
| `ratings` | Preferred device ratings (optional) |  | table | columns: I_n (A) | Setup |
| `ampacity` | Current-carrying capacities for the installation method (optional) |  | table | columns: CSA (mm²), AWG, I_t (A) | Setup |
| `L` | Circuit length (carried to the voltage-drop calculation) | m | 20 | 1 to 300 | Geometry |
| `T_user` | Conductor operating temperature for 'user' insulation (optional) | °C |  | 60 to 180 | Material |
| `P` | Rated output (shaft) power; η converts it to input (DC: electrical power drawn, η not applied) | kW | 7.5 | 0.1 to 200 | Loads |
| `U` | Supply voltage (line voltage for three-phase) | V | 400 | 12 to 690 | Loads |
| `cos_phi` | Power factor (not used for DC) |  | 0.85 | 0.6 to 1 | Loads |
| `eta` | Efficiency (not used for DC) |  | 0.9 | 0.6 to 1 | Loads |
| `I_b_in` | Design current from another calculation (replaces the computed I_b when given) (optional) | A |  | 0.1 to 10000 | Loads |
| `T_amb` | Ambient temperature | °C | 30 | 0 to 60 | Conditions |
| `T_ref` | Reference ambient temperature for the derived C_a | °C | 30 | 20 to 40 | Conditions |
| `N_g` | Grouped circuits |  | 1 | 1 to 10 | Factors |
| `C_a` | Ambient correction factor (1 does not apply the ambient: enter the factor for θ_a from your licensed table) |  | 1 | 0.5 to 1.3 | Factors |
| `C_g` | Grouping correction factor (at most 1) |  | 1 | 0.3 to 1 | Factors |
| `C_i` | Thermal insulation correction factor (at most 1) |  | 1 | 0.3 to 1 | Factors |
| `derive_Ca` | Derive C_a from the temperatures instead of the entered factor |  | 0 |  | Factors |
| `I2` | Conventional operating current I₂ (gG fuse, MCCB, MPCB: required in the machine-wiring context; a semi-enclosed fuse defaults to 2 I_n) (optional) | A |  | 1 to 10000 | Device |

## Assumptions

- Every tabulated value is entered from the user's own licensed copy or a manufacturer datasheet. Basis: No table from a standard or a manufacturer is shipped.
- No table from a standard or a manufacturer ships with the calculator. Basis: No table from a standard or a manufacturer is shipped.
- The capacities in the table are for the installation method stated. Basis: Records carry their source and edition.
- The method key is descriptive and is not interpreted. Basis: Records carry their source and edition.
- A gG fuse is deemed to meet I₂ ≤ 1.45·I_z when I_n ≤ I_z only under BS 7671. Basis: BS 7671 reg. 433.1.201; IEC 60269-1.
- An IEC 60269-1 gG fuse has I₂ = 1.6·I_n, so in the machine-wiring context I₂ is entered from the datasheet. Basis: BS 7671 reg. 433.1.201; IEC 60269-1.
- The ambient, grouping and insulation factors are applied as a product. Basis: The count is carried to the sheet and warns when C_g = 1 with more than one circuit.
- C_g is not looked up from the number of grouped circuits. Basis: The count is carried to the sheet and warns when C_g = 1 with more than one circuit.
- The optional derived C_a is an approximation for the temperature difference only. Basis: Derived, not tabulated.
- The derived C_a is labelled as an approximation, so compare it with the licensed table. Basis: Derived, not tabulated.
- C_c = 0.725 applies to a semi-enclosed fuse only: 1.45 divided by an assumed fusing factor of 2. Basis: Derived.
- AWG areas follow the geometry d = 0.127·92^((36 − n)/39) mm. Basis: Derived.
- No AWG capacity is supplied. Basis: Derived.
- DC circuits use I_b = P/U. Basis: Method.
- The power factor and the efficiency are not applied to a DC circuit. Basis: Method.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `I_b` | Design current | A |
| `I_n` | Rated current of the device | A |
| `C_c` | Semi-enclosed fuse factor |  |
| `C_a` | Ambient factor |  |
| `I_t_req` | Required tabulated capacity | A |
| `CSA` | Selected cross-section | mm² |
| `I_z` | Current-carrying capacity | A |
| `L` | Circuit length | m |
| `u_Ib` | I_b / I_n |  |
| `u_In` | I_n / I_z |  |
| `T_op` | Conductor operating temperature for the insulation | °C |
| `I2` | Conventional operating current | A |
| `u_I2` | I₂ / (1.45 I_z) |  |
| `device_code` | Protective device type code |  |
| `gauge` | Selected AWG gauge |  |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `conductor.current_design` | `I_b` | A | Design current |
| `conductor.rating_device` | `I_n` | A | Rated current of the protective device |
| `conductor.area_selected` | `CSA` | mm^2 | Selected conductor cross-section |
| `conductor.capacity_current` | `I_z` | A | Current-carrying capacity of the selected conductor |
| `conductor.length_circuit` | `L` | m | Circuit length |
| `conductor.temperature_operating` | `T_op` | degC | Conductor operating temperature for the insulation (70 PVC, 90 XLPE, the entered T_user for 'user'; → fault-protection T_op, voltage-drop T_c) |
| `conductor.code_device` | `device_code` |  | Protective device type code: 1 MCB B, 2 MCB C, 3 MCB D, 4 fuse, MCCB or MPCB (→ fault-protection device_code) |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Design current against the device rating (BS EN 60204-1 cl. 7.2; BS 7671 reg. 433.1.1)
- Device rating against the conductor capacity (BS EN 60204-1 cl. 7.2; BS 7671 reg. 433.1.1)
- Conventional operating current against 1.45 times the conductor capacity (BS EN 60204-1 cl. 7.2; BS 7671 reg. 433.1.1)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `ampacity` | Block | Conservative | The ampacity record must hold at least one usable row. |
| `ampacity-ignored` | Warning | Conservative | A row without the cross-section or gauge of the conductor system is ignored. |
| `ratings` | Block | Conservative | The ratings record must hold at least one rating. |
| `theta_p` | Block | Conservative | The conductor operating temperature must be entered for 'user' insulation to derive C_a. |
| `derive-Ca` | Block | Conservative | The ambient and the reference ambient must both be below the conductor temperature to derive C_a. |
| `C_a-range` | Block | Conservative | The derived C_a must lie between 0.1 and 1.5. |
| `C_a` | Warning | Conservative | The ambient factor is derived from the temperatures: an approximation to compare with the licensed table. |
| `I2-deemed` | Warning | Conservative | The conventional operating current of the device is not entered, and the condition is deemed satisfied when I_n ≤ I_z. |
| `I_n` | Block | Conservative | A preferred rating of at least I_b must be in the ratings record. |
| `CSA` | Block | Conservative | A conductor in the ampacity record must carry I_t,req. |
| `I2` | Block | Conservative | For a gG fuse, MCCB or MPCB in the machine-wiring context, I₂ must be entered (blocked without it); in the BS 7671 installation context a gG fuse to BS 88 is deemed to meet I₂ ≤ 1.45 I_z when I_n ≤ I_z, and an MCCB or MPCB without I₂ is treated the same with a warning. |
| `C_g` | Warning | Unconservative | More than one grouped circuit with C_g = 1 is probably a missing grouping factor. |
| `theta_a` | Warning | Unconservative | An ambient temperature above the reference with C_a left at 1: the ambient has not reached the sizing. |
| `ampacity-sense` | Warning | Conservative | A cross-section entered twice (the lower capacity is used) or a capacity that falls as the cross-section grows is probably a typing slip or a mix of installation methods. |
| `theta_a-limit` | Block | Conservative | An ambient temperature at or above the conductor operating temperature leaves no current-carrying capacity. |

## Limitations

Single circuit, one installation method per run; the factors are applied as a product and are the user's; the derived `C_a` is a temperature-difference approximation only; the I²t/short-circuit and voltage-drop checks are fault-protection and voltage-drop; fault-protection accepts a line conductor of 0.5 mm² and above, so a selected CSA below 0.5 mm² (AWG 21 and finer) cannot be linked to fault-protection; no cable ampacity, rating factor or standard table is embedded (§A5).

## References

- BS 7671:2018, reg. 433.1.1: Co-ordination of conductors and overload protective devices
- BS 7671:2018, reg. 523.1: Current-carrying capacity of a cable
- BS EN 60204-1:2018, cl. 12.4: Current-carrying capacity of conductors and cables in normal service
- BS EN 60204-1:2018, cl. 7.2: Overcurrent protection
- BS EN 60898-1:2019, cl. (number to be checked): Circuit-breakers for overcurrent protection: conventional tripping current
- BS 7671:2018, reg. 433.1.201, 433.1.202: Overload protection by gG fuses and semi-enclosed fuses: deemed conditions

## Test cases

### Notes on the cases

All analytic and Confirmed, recomputed in Python. The capacities (1.5→12.3, 2.5→17.7, 4→24.1, 6→31.9, 10→44.6 A; AWG 14→11.2, 12→16.9, 10→26.3, 8→37.4 A; and, in the behaviour tests, 11.4/16.2/23.7/30.8/43.1 A, AWG 18.4 and 27.3 A and C_g 0.58, all invented) and the factors (C_a 0.91, C_g 0.77) are synthetic values invented for these tests, not taken from any table; the ratings 6, 10, 16, 20, 25, 32, 40, 50, 63 A are the typical preferred series. `ka-three-phase-mcb-c` (T_amb 35 °C, two circuits; I_b 14.1507 A, I_n 16 A, I_t,req 22.8343 A, 4 mm², I_z 16.8869 A), `ka-single-phase` (I_b 15.2555 A, 2.5 mm², I_z 17.7 A), `ka-dc` (I_b 25 A, I_t,req 27.4725 A, 6 mm², I_z 29.029 A; a gG fuse in the machine context with `I2` = 40 A entered, u_I2 = 0.95030), `ka-semi-enclosed` (C_c 0.725, I_t,req 27.5862 A, 6 mm², I₂ 40 A), `ka-derived-ca` (C_a = √(30/40) = 0.866025), `ka-awg-10` (AWG 10 = 5.2612 mm², on a type C MCB). Not pinned — owner check: the Schneider *Electrical Installation Guide* chapter G worked examples, the IET *Electrical Installation Design Guide* examples and the Kollmorgen current-carrying-capacity note; no value from them is in source, tests or docs. The worked example `conveyor-motor-circuit` (4 kW rated output, type C MCB, machine panel) uses synthetic values entered with record-style provenance.

- `ka-three-phase-mcb-c`: Three-phase 7.5 kW (output) load on a type C MCB, θ_a 35 °C with C_a 0.91, two circuits with C_g 0.77 (Confirmed; source: Analytic (synthetic test values invented for these tests, not taken from any table))
- `ka-single-phase`: Single-phase 3 kW (output) load on a type B MCB (Confirmed; source: Analytic (synthetic test values invented for these tests, not taken from any table))
- `ka-dc`: DC 600 W load at 24 V on a gG fuse, C_a 0.91 (Confirmed; source: Analytic (synthetic test values invented for these tests, not taken from any table))
- `ka-semi-enclosed`: Semi-enclosed fuse on a linked 18 A design current: C_c = 0.725 (Confirmed; source: Analytic (synthetic test values invented for these tests, not taken from any table))
- `ka-derived-ca`: Derived ambient factor: PVC 70 °C, θ_ref 30 °C, θ_a 40 °C (Confirmed; source: Analytic (the derived-approximation formula))
- `ka-awg-10`: AWG geometry: AWG 10 is 5.2612 mm² (Confirmed; source: Analytic (the AWG geometry formula))

## Changes

**Changes in 1.4.0 (C-2 consistency, phase C).** Layout and wording; no result changes. Three inputs take the standard symbols and keep the old ones as aliases: `theta_a` is `T_amb`, `theta_p` is `T_user` and `theta_ref` is `T_ref`. The port `theta_op` is `T_op` (public name `conductor.temperature_operating`, alias `theta_op`), and the pack bindings to voltage-drop and fault-protection use `T_op`. The checks compare physical values: `I_b` with `I_n`, `I_n` with `I_z`, and `I2` with the new step `I2_lim`, 1.45 `I_z`. The rules `ratings`, `theta_p`, `derive-Ca`, `C_a-range`, `C_a`, `ampacity-ignored` and `I2-deemed` are declared, so every emitted id is declared and its severity is the declared one; the rule ids `theta_a`, `theta_p` and `theta_a-limit` keep their names. The inputs are grouped Setup, Geometry, Material, Loads, Conditions, Factors and Device, and every usual range is inside its hard limits. The record kind `cable.ampacity` is titled "Cable ampacity (current-carrying capacities)".

**Changes in 1.2.0 (elec.conductor-sizing-01).** The ambient temperature `theta_a` reaches the sizing only through `C_a`. With `C_a` left at 1 and `theta_a` more than 0.5 K above `theta_ref` the sheet now warns (rule `theta_a`) that the ambient has not been applied; an ambient at or above the conductor operating temperature (70 °C PVC, 90 °C XLPE, the entered `theta_p` for 'user') is refused (rule `theta_a-limit`, block). A factor for a buried or ducted cable is not offered: that is an open owner decision (elec.conductor-sizing-R1).

**Changes in 1.3.0 (elec.conductor-sizing-02, -03, -05).** `C_g` and `C_i` are at most 1 (hard limit): neither the grouping nor the thermal-insulation factor of the cable standards exceeds 1, so a typing slip such as 1.5 used to shrink the conductor with no notice (C_g = C_i = 1.5, 16 A MCB gave 1.5 mm² with I_z 27.675 A); it is now refused. `C_a` keeps its 1.5 limit. The derived `C_a` warning (rule `C_a`) now names the reference ambient `θ_ref` used and says it must be the reference ambient of the table the capacities come from (the default 30 °C may differ from the table's; whether the machine-wiring tables use 40 °C is for the owner to confirm against the licensed copy). The ampacity record is checked for sense (rule `ampacity-sense`, a warning): a cross-section entered twice uses its lower capacity (rows 4 mm² 24.1 A and 4 mm² 30 A with 25 A required gave 4 mm² at 30 A; now 6 mm² at 31.9 A), and a larger cross-section with a lower capacity than a smaller one warns that the record probably mixes installation methods. A chain addition in the same version (chain.motor-circuit-R1): the new output port `theta_op`, the conductor operating temperature for the insulation (70 °C PVC, 90 °C XLPE, the entered `theta_p` for 'user'; none for 'user' without `theta_p`), which the motor circuit and hydraulic power unit packs link to the fault-loop temperature `θ` of E5 and the conductor temperature `T_c` of E3 (Z_s of a 2.5/2.5 mm², 100 m circuit: 2.000 Ω at 70 °C, 2.109 Ω at 90 °C).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 4 kW conveyor motor circuit in a machine panel, type C MCB ([sheet](../../worked-examples/electrical/elec.conductor-sizing/conveyor-motor-circuit/sheet.txt)).

```text
I_b = 4 kW / (sqrt(3) × 400 V × 0.82 × 0.85) = 8.2834 A
I_n = smallest rating ≥ 8.2834 A in the ratings record = 10 A
device_code = MCB type C = 2
C_c = MCB type C = 1
T_op = 70 degC
I_t_req = 10 A / (0.91 × 0.77 × 1 × 1) = 14.271 A
I_t = I_t,req = 14.271 A = 17.7 A
CSA = I_t,req = 14.271 A = 2.5 mm²
I_z = 17.7 A × 0.91 × 0.77 × 1 = 12.402 A
u_Ib = 8.2834 A / 10 A = 0.82834
u_In = 10 A / 12.402 A = 0.8063
I2 = 1.45 × 10 A = 14.5 A
I2_lim = 1.45 × 12.402 A = 17.983 A
u_I2 = 14.5 A / 17.983 A = 0.8063
Governing: PASS  Design current against the device rating: 8.283 A vs 10 A  utilisation 0.83  « BS EN 60204-1 cl. 7.2; BS 7671 reg. 433.1.1
```
<!-- hand-arithmetic:end -->
