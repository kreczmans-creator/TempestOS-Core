# U-values and room heat loss (`bs.room-heat-loss@1.6.0`)

**In plain terms.** A room loses heat through its walls, floor, roof and windows and by air leaking or being ventilated out. The worksheet works out the U-value of each construction from its layers (how easily heat flows through it), the heat lost through each element and by ventilation at the design temperatures, and the total heating the room needs, with an allowance for thermal bridges. It also checks each U-value against its target.

## Scope

Calculates the U-values of built-up elements and the steady-state design heat loss of one room. It gives the fabric and ventilation loss, the room load for sizing emitters, the heat loss coefficient and an optional check against target U-values.

## Method

- Element U-value: U = 1/(R_si + Σ d/λ + R_g + R_se), or the user's U override for ground floors and glazing, or the ground floor record U_g for a floor without layers or an override
- Element fabric loss: Φ_i = A·U·f·(T_int − θ_adj), with θ_adj = θ_e where the element faces outside
- Thermal bridges: Φ_tb = y_tb·Σ(A·f)_ext·(T_int − θ_e) on the elements facing outside
- Fabric total: Φ_T = ΣΦ_i + Φ_tb
- Ventilation loss: q_v = n·V; Φ_V = 0.34·q_v·(T_int − θ_e), or (T_int − T_supply) through an MVHR supply, plus 0.34·n_inf·V·(T_int − θ_e) infiltration with MVHR
- Room load: Φ_HL = Φ_T + Φ_V (CIBSE DHDG method: × (1 + additional allowance))
- Heat loss coefficient: HLC = Φ_HL/(T_int − θ_e); loss per floor area Φ_HL/A_floor
- U-value check: U against the target U of each element (the global target applies to built-up elements facing outside only; an element's own target applies wherever it faces)

### Notes on the method

`U = 1/(R_si + Σ d/λ + R_g + R_se)` for homogeneous layers in series (BS EN ISO 6946), or the user's U override for ground floors and glazing. `Φ_i = A·U·f·(θ_int − θ_adj)`, with `θ_adj = θ_e` where the element faces outside. `Φ_tb = y_tb·Σ(A·f)_ext·(θ_int − θ_e)` on the elements facing outside, with the temperature factor applied as it is to U (the allowance is added to U, then multiplied by f). An element faces outside when its adjacent temperature is blank or at or below `θ_e` (so typing `θ_e` for an external wall changes nothing). `Φ_T = ΣΦ_i + Φ_tb`. `q_v = n·V`; `Φ_V = 0.34·q_v·(θ_int − θ_e)`, or `(θ_int − θ_supply)` when an MVHR supply temperature is entered (BS EN 12831-1 cl. 6.3.3); with MVHR an optional infiltration rate `n_inf` is added at the outside temperature, `Φ_V = 0.34·[n·V·(θ_int − θ_supply) + n_inf·V·(θ_int − θ_e)]`, and `n` is then the MVHR rate (`n_inf` is ignored with a warning without MVHR). `Φ_HL = Φ_T + Φ_V`; `HLC = Φ_HL/(θ_int − θ_e)`; `Φ_A = Φ_HL/A_floor`. The 0.34 Wh/(m³·K) is the conventional rounded ρ·c_p of air (derived: about 1.2 kg/m³ × 1005 J/(kg·K) ÷ 3600). The method selector changes the citations; the CIBSE DHDG option also applies the user's additional allowance, `Φ_HL = (Φ_T + Φ_V)(1 + f_add)` (ignored, with a warning, for BS EN 12831-1). The U arithmetic is the series-resistance sum of the legacy module `calc.plane-wall-heat-transfer` (films 1/R_si and 1/R_se); an agreement test in the Library test project runs both on the same walls to 1e-9.

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram (ADR-0158, DI-08). It is not to scale and shows inputs only, never a result. It draws the room in section with the outside air beyond and the ventilation air entering. A layered wall, a roof, a floor and a window stand for the elements and layers as listed. It binds `layers`, `elements`, `U_target`, `f_add`, `V`, `T_int`, `n`, `theta_e`, `y_tb`, `n_inf`, `A_floor` and `T_supply`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `method` | Calculation method (changes the citations; the DHDG option adds the allowance below) |  | BS EN 12831-1 elemental | BS EN 12831-1 elemental, CIBSE DHDG reduced method |  | Setup |
| `elements` | Building elements bounding the room |  | table |  |  | Setup |
| `elements.name` | Element (column) |  |  | | | Setup |
| `elements.construction` | Layers of (blank = this element's name) (column) |  | optional | | | Setup |
| `elements.flow` | Heat flow direction through the element (blank = horizontal; upward for a roof or ceiling, downward for a floor) (column) |  | optional | | | Setup |
| `elements.area` | Area (column) | m² |  | | | Setup |
| `elements.theta_adj` | Adjacent temperature (blank or θ_e = outside) (column) | °C | optional | | | Setup |
| `elements.f` | Temperature factor f (blank = 1) (column) |  | optional | | | Setup |
| `elements.Rsi` | Rsi (blank = 0.13 horizontal, 0.10 upward, 0.17 downward) (column) | m²·K/W | optional | | | Setup |
| `elements.Rse` | Rse (blank = 0.04 outside, the same as Rsi where it faces another space) (column) | m²·K/W | optional | | | Setup |
| `elements.Rg` | Air-gap resistance Rg (column) | m²·K/W | optional | | | Setup |
| `elements.U_user` | U override (ground floor, glazing) (column) | W/(m²·K) | optional | | | Setup |
| `elements.U_max` | Target U for this element (column) | W/(m²·K) | optional | | | Setup |
| `layers` | Construction layers (several elements share the table, keyed by element name) |  | table |  |  | Setup |
| `layers.element` | Element (column) |  |  | | | Setup |
| `layers.name` | Layer (column) |  | optional | | | Setup |
| `layers.d` | Thickness (column) | mm |  | | | Setup |
| `layers.lambda` | Thermal conductivity λ (column) | W/(m·K) |  | | | Setup |
| `V` | Room volume | m³ | 40 | 5 to 1000 | 1 to 5000 | Geometry |
| `A_floor` | Floor area, for the loss per floor area (optional; blank = no loss per floor area) | m² | optional | 1 to 200 | 0.1 to 2000 | Geometry |
| `U_g` | Ground floor U-value from a record, applied to each element with downward heat flow that has no layers and no U override (optional; blank = none) | W/(m²·K) | optional | 0.05 to 2 | 0.01 to 10 | Material |
| `T_int` (was `theta_int`) | Internal design temperature | °C | 21 | 16 to 23 | 16 to 24 | Conditions |
| `theta_e` | External design temperature (location record) | °C | -3 | -15 to 3 | -15 to 5 | Conditions |
| `T_supply` (was `theta_supply`) | MVHR supply air temperature after heat recovery (optional; blank = natural ventilation, outside air) | °C | optional | -15 to 24 | -20 to 40 | Conditions |
| `y_tb` | Thermal-bridge allowance on the area facing outside | W/(m²·K) | 0 | 0 to 0.3 | 0 to 1 | Factors |
| `f_add` | Additional allowance on the room load (CIBSE DHDG method only) | % | 0 | 0 to 20 | 0 to 50 | Factors |
| `U_target` | Target U for built-up elements without their own target (optional; blank = no global target) | W/(m²·K) | optional | 0.05 to 8 | 0.01 to 10 | Requirements |
| `n` | Air change rate | 1/h | 0.5 | 0.1 to 2.5 | 0.1 to 3 | Ventilation |
| `n_inf` | Infiltration air change rate at outside temperature, with MVHR only (optional; blank = no infiltration loss, with a warning; 0 = airtight; n is then the MVHR rate) | 1/h | optional | 0 to 1 | 0 to 3 | Ventilation |

## Assumptions

1. Steady state: every temperature is constant, with no solar or internal gains, no thermal mass and no dynamic simulation. Basis: Design heat loss, not an annual energy estimate.
2. Layers are homogeneous and in series, with one-dimensional heat flow. Basis: BS EN ISO 6946:2017 homogeneous-layer calculation.
3. Bridged layers such as timber studs, mortar joints and fixings are not corrected for. Basis: BS EN ISO 6946:2017 homogeneous-layer calculation.
4. A thermal-bridge allowance y_tb is added instead of correcting bridged layers. Basis: Stated.
5. A blank Rsi is the conventional BS EN ISO 6946 value for the element's heat-flow direction. Basis: Small conventional BS EN ISO 6946 set (three values), not a table of design data.
6. The conventional Rsi is 0.13 m²K/W horizontal, 0.10 upward and 0.17 downward. Basis: Small conventional BS EN ISO 6946 set (three values), not a table of design data.
7. A blank Rse is 0.04 m²K/W for an element facing outside. Basis: Small conventional BS EN ISO 6946 set, not a table of design data.
8. Where an element faces another space, a blank Rse is the same as Rsi. Basis: Small conventional BS EN ISO 6946 set, not a table of design data.
9. The global target U applies to built-up elements facing outside only. Basis: Scope of the target check: an internal partition or floor is checked only against a target entered on its own row.
10. Ground-floor and glazing U-values are entered by the user as an override. Basis: Stated limit.
11. A ground floor U-value record fills U_g for a floor element without layers or an override. Basis: Stated limit: the record's value is the user's, from its table.
12. The calculator does not model ground heat transfer. Basis: Stated limit.
13. The air constant 0.34 Wh/(m³·K) is the conventional rounded ρ·c_p of air. Basis: Derived: about 1.2 kg/m³ × 1005 J/(kg·K) ÷ 3600, not read from a table.
14. Design temperatures, conductivities, surface resistances and the thermal-bridge allowance are the user's own records. Basis: No table from a standard or a manufacturer is shipped.

## Outputs

| Symbol | Description |
|---|---|
| `R_1` | Cavity wall: thermal resistance |
| `U_1` | Cavity wall: U-value |
| `Phi_1` | Cavity wall: fabric loss |
| `U_2` | Window: U-value |
| `Phi_2` | Window: fabric loss |
| `T_int` | Internal design temperature |
| `A_fab` | Total fabric area |
| `A_ext` | Fabric area facing outside |
| `A_tb` | Fabric area facing outside weighted by f |
| `U_m` | Mean fabric U-value (area-weighted) |
| `U_eff` | Heat-loss weighted fabric U-value (with f) |
| `dT` | Design temperature difference |
| `Phi_el` | Element fabric loss |
| `Phi_tb` | Thermal-bridge allowance |
| `Phi_T` | Fabric heat loss |
| `q_v` | Ventilation air flow |
| `Phi_V` | Ventilation heat loss |
| `Phi_HL` | Room design heat loss |
| `HLC` | Heat loss coefficient |
| `u_target_1` | Cavity wall: U against target |
| `R_2` | Partition: thermal resistance |
| `U_3` | Ground floor: U-value |
| `Phi_3` | Ground floor: fabric loss |

Per element `R_i`, `U_i`, `Phi_i` (and `u_i` against a target); `A_fab`, `A_ext`, `A_tb`, `U_m`, `U_eff`, `dT`, `Phi_el`, `Phi_tb`, `Phi_T`, `q_v`, `Phi_V` (with `q_inf` where `n_inf` is used), `Phi_HL`, `HLC`, `Phi_A`. **Ports.** `Phi_HL` (→ B6 room heat required), `HLC`, `Phi_T`, `Phi_V`, `U_1` (U-value of the first element of the elements table, added in 1.2.0 for the envelope-moisture chain: it is the B7 `U_ref` cross-check; it offers no per-layer output, so a wall's layers are typed in B7 as well). **Checks.** One utilisation check per element with a target (U/U_target ≤ 1).

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `room.heat_loss_design` | `Phi_HL` | kW | Room design heat loss (→ sealed-system rooms table, column Phi) |
| `room.temperature_internal` | `T_int` (was `theta_int`) | °C | Internal design temperature of the room (→ sealed-system rooms table, column t_i, the room temperature of that room's emitter) |
| `room.coefficient_heat_loss` | `HLC` | W/K | Heat loss coefficient |
| `room.heat_loss_fabric` | `Phi_T` | kW | Fabric heat loss including the thermal-bridge allowance |
| `room.heat_loss_ventilation` | `Phi_V` | kW | Ventilation heat loss |
| `room.u_value_first` | `U_1` | W/(m²·K) | U-value of the first element in the elements table (→ glaser U_ref, the wall U-value cross-check) |

## Checks

Checks the sheet makes:

- U-value of Cavity wall against its target
- U-value of Slab against its target
- U-value of Roof against its target

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `temperatures` | Block | Conservative | The internal design temperature must be above the external design temperature. |
| `elements` | Block | Conservative | Enter at least one element; names must be unique; each needs layers or a U override. |
| `layers` | Block | Conservative | A built-up element needs at least one layer; layers are matched to elements by name. |
| `supply` | Block | Conservative | The MVHR supply temperature must not exceed the internal design temperature. |
| `u-override-layers` | Warning | Conservative | Layers of an element with a U override are ignored. |
| `orphan-layers` | Warning | Conservative | Layers naming an element that is not in the elements table are ignored. |
| `f-and-adjacent` | Warning | Unconservative | An element with both a temperature factor f below 1 and an adjacent temperature above θ_e takes both reductions; f is for elements facing the outside only. |
| `f-add-ignored` | Warning | Conservative | The additional allowance applies to the DHDG method only. |
| `mvhr-no-infiltration` | Warning | Unconservative | An MVHR supply temperature is entered and the infiltration rate n_inf is blank: the room has no infiltration loss at the outside temperature. |
| `n-inf-ignored` | Warning | Conservative | The infiltration rate applies with an MVHR supply temperature only. |
| `gain` | Warning | Conservative | The room gains heat from warmer neighbours (Φ_HL ≤ 0). |
| `supply-below-external` | Warning | Conservative | The MVHR supply is colder than the outside design temperature. |
| `u-g-unused` | Warning | Conservative | U_g is given but no element with downward heat flow lacks layers and a U override, so it is not used. |

## Limitations

Steady state, no gains, no thermal mass or dynamic simulation. Homogeneous layers only: no bridged-layer (combined) method, no corrections for fixings, ventilated cavities or inverted roofs. Ground-floor U is user-entered; there is no ground model. The thermal-bridge allowance is a single W/(m²·K) on the area facing outside (a deliberate reading of "total fabric area"): elements to heated or unheated spaces entered with an adjacent temperature above `θ_e` get no bridge allowance. A warning is raised if Φ_HL ≤ 0 (the room gains heat from warmer neighbours). No intermittency or reheat allowance except the DHDG user allowance.

## References

- BS EN ISO 6946:2017: Thermal resistance and transmittance of building components (clause numbers to be checked).
- BS EN 12831-1:2017, cl. 6.3.2 (clause number to be checked against the owner's copy): Heating systems in buildings — design heat load: transmission heat loss.
- BS EN 12831-1:2017, cl. 6.3.3 (clause number to be checked against the owner's copy): Heating systems in buildings — design heat load: ventilation heat loss.
- CIBSE, Domestic Heating Design Guide: Room heat loss and the additional allowance, reduced method (name and edition unverified).

## Test cases

### Notes on the cases

`ka-cavity-wall` (R 4.157857 m²K/W, U 0.240509, Φ_T 216.244 W, Φ_V 163.2 W, Φ_HL 379.444 W, HLC 15.8102 W/K); `ka-mvhr-partition` (partition U 1.806240, bridge allowance 25.2 W with f applied, Φ_T 212.808 W, MVHR Φ_V 51 W, Φ_HL 263.808 W, HLC 10.9920 W/K); `ka-mvhr-infiltration` (Φ_V 91.8 W, Φ_HL 304.608 W); `ka-dhdg-allowance` (Φ_HL × 1.10 = 417.389 W); `ka-single-layer-target` (invented λ 1.4: U 4.142012, utilisation 0.9204 against a target of 4.5); `ka-roof-upward` (R_si 0.10 for upward flow: R 2.14 m²K/W, U 0.467290, utilisation 0.9346; the 18 °C partition U 2.173913 is not held to the outside target) — analytic, Confirmed, recomputed independently in python3. The conductivities in the cases are invented illustrative inputs, not design data (they are deliberately not the familiar textbook values). The CIBSE *Domestic Heating Design Guide* (2026 ed.) worked examples and the CIBSE Guide A (8th ed.) Ch. 3 U-value examples are **not pinned**: they need the owner's copies, and no example is entered from memory. Worked example: `semi-detached-living-room` (Φ_HL 638.11 W, 39.4 W/m²).

- `ka-cavity-wall`: Insulated cavity wall, a window and natural ventilation (Confirmed; source: Analytic (series resistance; Φ = A·U·Δθ + 0.34·n·V·Δθ))
- `ka-mvhr-partition`: MVHR supply, an 18 °C partition, a floor with a user U and a thermal-bridge allowance (Confirmed; source: Analytic (series resistance; MVHR Φ_V = 0.34·q_v·(θ_int − θ_supply)))
- `ka-dhdg-allowance`: CIBSE DHDG reduced method with a 10 % additional allowance (Confirmed; source: Analytic (the first case with Φ_HL × 1.10))
- `ka-single-layer-target`: One 100 mm layer against a target U (Confirmed; source: Analytic (series resistance))
- `ka-mvhr-infiltration`: MVHR supply with a separate infiltration rate (Confirmed; source: Analytic (Φ_V = 0.34·[n·V·(θ_int − θ_supply) + n_inf·V·(θ_int − θ_e)]))
- `ka-roof-upward`: A roof: upward heat flow takes R_si = 0.10, and a heated neighbour's partition is not held to the outside target (Confirmed; source: Analytic (series resistance; BS EN ISO 6946 conventional surface resistances by flow direction))

## Changes

**1.6.0 (DC-10, DC-21: Track P templates 42, 43 and 44).** Three record kinds are declared: `room.internal-temperatures` on `T_int` (template 42, one record per room type; its column `theta_i` fills `T_int`), `room.air-change-rates` on `n` (template 43, one record per room type; its column `n_ach` fills `n`) and `ground.u-values` on the new optional input `U_g` (template 44, one record per construction, soil conductivity, characteristic dimension and insulation; `lambda_g`, `B_prime` and `R_f` are stored and never applied). `U_g` (W/(m²·K), usual 0.05 to 2, hard 0.01 to 10, group Material) is the ground floor U-value from the record: each element with downward heat flow that has no layers and no U override takes it as its U, as a U override would (not built up: no R step, no global target), with the step `U_n = U_g (ground floor U-value record)`; such an element was blocked before. `U_g` given with no such element warns (rule `u-g-unused`). Template 48 (`surface.resistances`) stays proposed: the Rsi, Rse and Rg cells belong to the elements table, which is the room's geometry and not record data (Black B-A-10), and a record cannot fill three columns of a table without replacing it. No number of the known-answer cases, the worked examples or the packs moves; where `T_int` and `n` are typed the "records" warning now lists them. Minor bump.

**Changes in 1.5.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The internal design temperature `theta_int` is now `T_int` (alias kept; the port `room.temperature_internal` keeps `theta_int` as an alias) and the supply temperature `theta_supply` is now `T_supply`. The external design temperature `theta_e` and the adjacent-temperature column `theta_adj` keep their names, because the climate record and a saved element table use them. The heat losses `Phi_HL`, `Phi_T` and `Phi_V` read in kW. The element utilisations are now `u_target_N` with the `U_target_N` records, and the check compares physical values. The inputs sit in the groups Setup, Geometry, Conditions, Factors, Requirements and Ventilation; the 13 assumptions are one short sentence each, and the ports are named `room.*`.

**Changes in 1.3.0.** `U_m` is now the area-weighted mean U-value `Σ A·U / Σ A`, as its label says; the previous value, which included the temperature factor `f`, is the new output `U_eff = Σ A·U·f / Σ A` (probe: wall 18 m² U 0.1967, roof 20 m² U 0.1776, floor 20 m² U 0.22 with f 0.7, window 4 m² U 1.4: `U_m` 0.2544 → 0.2757, `U_eff` 0.2544). No check uses either. An element that has both `f` below 1 and an adjacent temperature above `θ_e` warns that the temperature reduction is counted twice (10 m², U 0.3, θ_adj 10 °C, f 0.5, θ_int 21 °C gives 16.5 W; either reduction alone gives 33 W or 36 W); `f` is for elements facing the outside only.

**Port added in 1.4.0.** `T_int` (the internal design temperature) is an output port, so the wet-heating pack links each room's design temperature to the room temperature of its emitter in the sealed-system step.

**Citations to check.** The BS EN 12831-1 clause numbers (cl. 6.3.2 transmission, cl. 6.3.3 ventilation) and the BS EN ISO 6946 clause numbers are not verified here and are to be checked against the owner's copies. "CIBSE DHDG reduced method" and its 2026 edition are the brief's names for the method and are unverified; the additional-allowance form is this product's own simplification, not a quotation of the guide.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Semi-detached living room: U-values and design heat loss for the emitter sizing ([sheet](../../worked-examples/building-services/bs.room-heat-loss/semi-detached-living-room/sheet.txt)).

```text
R_1 = Front wall: 0.13 + (0.12202 + 3.3333 + 0.5 + 0.0325) + 0 + 0.04 = 4.1579 m²·K/W
U_1 = 1 / 4.1579 = 0.24051 W/(m²·K)
Phi_1 = 8.7 × 0.24051 × 1 × (21 − -3) °C = 0.050218 kW
U_2 = Window: 1.4 W/m²K = 1.4 W/(m²·K)
Phi_2 = 2.1 × 1.4 × 1 × (21 − -3) °C = 0.07056 kW
R_3 = Gable wall: 0.13 + (0.12202 + 3.3333 + 0.5 + 0.0325) + 0 + 0.04 = 4.1579 m²·K/W
U_3 = 1 / 4.1579 = 0.24051 W/(m²·K)
Phi_3 = 8.64 × 0.24051 × 1 × (21 − -3) °C = 0.049872 kW
R_4 = Party wall: 0.13 + (0.0325 + 0.16667 + 0.16667 + 0.0325) + 0.18 + 0.13 = 0.83833 m²·K/W
U_4 = 1 / 0.83833 = 1.1928 W/(m²·K)
Phi_4 = 8.64 × 1.1928 × 1 × (21 − 18) °C = 0.030918 kW
U_5 = Ground floor: 0.25 W/m²K = 0.25 W/(m²·K)
Phi_5 = 16.2 × 0.25 × 1 × (21 − -3) °C = 0.0972 kW
A_fab = 44.28 m²
… 20 more step(s) on the sheet
Governing: PASS  U-value of Party wall against its target: 1.193 W/(m²·K) vs 1.200 W/(m²·K)  utilisation 0.99
```
<!-- hand-arithmetic:end -->
