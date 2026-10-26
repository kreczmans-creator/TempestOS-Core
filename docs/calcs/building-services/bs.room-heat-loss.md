# B4 — U-values and room heat loss (`bs.room-heat-loss@1.4.0`)

**In plain terms.** A room loses heat through its walls, floor, roof and windows and by air leaking or being ventilated out. The worksheet works out the U-value of each construction from its layers (how easily heat flows through it), the heat lost through each element and by ventilation at the design temperatures, and the total heating the room needs, with an allowance for thermal bridges. It also checks each U-value against its target.

**Scope.** One room per sheet: the U-value of each built-up element from its layers and surface resistances, and the steady-state design heat loss of the room (fabric through each element to its adjacent temperature, a thermal-bridge allowance, ventilation through the outside air or an MVHR supply), the heat loss coefficient, and an optional check of each element against a target U.

**Method.** `U = 1/(R_si + Σ d/λ + R_g + R_se)` for homogeneous layers in series (BS EN ISO 6946), or the user's U override for ground floors and glazing. `Φ_i = A·U·f·(θ_int − θ_adj)`, with `θ_adj = θ_e` where the element faces outside. `Φ_tb = y_tb·Σ(A·f)_ext·(θ_int − θ_e)` on the elements facing outside, with the temperature factor applied as it is to U (the allowance is added to U, then multiplied by f). An element faces outside when its adjacent temperature is blank or at or below `θ_e` (so typing `θ_e` for an external wall changes nothing). `Φ_T = ΣΦ_i + Φ_tb`. `q_v = n·V`; `Φ_V = 0.34·q_v·(θ_int − θ_e)`, or `(θ_int − θ_supply)` when an MVHR supply temperature is entered (BS EN 12831-1 cl. 6.3.3); with MVHR an optional infiltration rate `n_inf` is added at the outside temperature, `Φ_V = 0.34·[n·V·(θ_int − θ_supply) + n_inf·V·(θ_int − θ_e)]`, and `n` is then the MVHR rate (`n_inf` is ignored with a warning without MVHR). `Φ_HL = Φ_T + Φ_V`; `HLC = Φ_HL/(θ_int − θ_e)`; `Φ_A = Φ_HL/A_floor`. The 0.34 Wh/(m³·K) is the conventional rounded ρ·c_p of air (derived: about 1.2 kg/m³ × 1005 J/(kg·K) ÷ 3600). The method selector changes the citations; the CIBSE DHDG option also applies the user's additional allowance, `Φ_HL = (Φ_T + Φ_V)(1 + f_add)` (ignored, with a warning, for BS EN 12831-1). The U arithmetic is the series-resistance sum of the legacy module `calc.plane-wall-heat-transfer` (films 1/R_si and 1/R_se); an agreement test in the Library test project runs both on the same walls to 1e-9.

**Inputs.**
- `method` ("BS EN 12831-1 elemental" or "CIBSE DHDG reduced method"); `theta_int` 16–24 °C; `theta_e` −15 to +5 °C (record `climate.design-temperature`).
- `layers` table (≤ 200 rows): element, layer name, `d` 1–500 mm, `λ` 0.01–5 W/(m·K) (record `material.conductivity`). Several elements share the table, keyed by the element name, or by the `construction` cell of the elements table where two elements share one build-up.
- `elements` table (≤ 60 rows, unique names): area 0.1–500 m²; optional adjacent temperature (blank = outside); optional `f` (blank = 1); optional `flow` (heat-flow direction through the element: horizontal, upward for a roof or ceiling, downward for a floor); optional `Rsi`, `Rse`, `Rg`; optional U override (ground floor, glazing); optional per-element target U; optional `construction`.
- `y_tb` W/(m²·K), default 0 (record `thermal.bridging`); optional `U_target` for built-up elements **facing outside** that have no target of their own (an internal partition or floor is checked only against a target on its own row).
- `n` 0.1–3 h⁻¹; `V` m³; optional `theta_supply` (MVHR) and `n_inf` 0–3 h⁻¹ (with an MVHR supply temperature and `n_inf` blank the room has no infiltration loss: since 1.2.2 the sheet warns, `mvhr-no-infiltration`; an explicit 0 means airtight; bs.room-heat-loss-R1: V 50 m³, n 0.5, supply 16 °C, 21/−3 °C gives Φ_V 42.5 W blank and 124.1 W with `n_inf` 0.2); optional `A_floor`; `f_add` 0–50 % (DHDG only).
- Blank `Rsi` is the BS EN ISO 6946 conventional value for the element's heat-flow direction (0.13 m²K/W horizontal, 0.10 upward, 0.17 downward); blank `Rse` is 0.04 outside or the same as `Rsi` for an element facing a heated space. These three values are the only surface-resistance values in the product and are listed on the sheet's assumptions. The elements table is geometry, not a licensed record, so a new sheet keeps it. No conductivity, design temperature or CIBSE/MCS table ships.

**Outputs.** Per element `R_i`, `U_i`, `Phi_i` (and `u_i` against a target); `A_fab`, `A_ext`, `A_tb`, `U_m`, `U_eff`, `dT`, `Phi_el`, `Phi_tb`, `Phi_T`, `q_v`, `Phi_V` (with `q_inf` where `n_inf` is used), `Phi_HL`, `HLC`, `Phi_A`. **Ports.** `Phi_HL` (→ B6 room heat required), `HLC`, `Phi_T`, `Phi_V`, `U_1` (U-value of the first element of the elements table, added in 1.2.0 for the envelope-moisture chain: it is the B7 `U_ref` cross-check; it offers no per-layer output, so a wall's layers are typed in B7 as well). **Checks.** One utilisation check per element with a target (U/U_target ≤ 1).

**Changes in 1.3.0.** `U_m` is now the area-weighted mean U-value `Σ A·U / Σ A`, as its label says; the previous value, which included the temperature factor `f`, is the new output `U_eff = Σ A·U·f / Σ A` (probe: wall 18 m² U 0.1967, roof 20 m² U 0.1776, floor 20 m² U 0.22 with f 0.7, window 4 m² U 1.4: `U_m` 0.2544 → 0.2757, `U_eff` 0.2544). No check uses either. An element that has both `f` below 1 and an adjacent temperature above `θ_e` warns that the temperature reduction is counted twice (10 m², U 0.3, θ_adj 10 °C, f 0.5, θ_int 21 °C gives 16.5 W; either reduction alone gives 33 W or 36 W); `f` is for elements facing the outside only.

**Port added in 1.4.0.** `theta_int` (the internal design temperature) is an output port, so the wet-heating pack links each room's design temperature to the room temperature of its emitter in the sealed-system step.

**Limitations.** Steady state, no gains, no thermal mass or dynamic simulation. Homogeneous layers only: no bridged-layer (combined) method, no corrections for fixings, ventilated cavities or inverted roofs. Ground-floor U is user-entered; there is no ground model. The thermal-bridge allowance is a single W/(m²·K) on the area facing outside (a deliberate reading of "total fabric area"): elements to heated or unheated spaces entered with an adjacent temperature above `θ_e` get no bridge allowance. A warning is raised if Φ_HL ≤ 0 (the room gains heat from warmer neighbours). No intermittency or reheat allowance except the DHDG user allowance.

**Test cases.** `ka-cavity-wall` (R 4.157857 m²K/W, U 0.240509, Φ_T 216.244 W, Φ_V 163.2 W, Φ_HL 379.444 W, HLC 15.8102 W/K); `ka-mvhr-partition` (partition U 1.806240, bridge allowance 25.2 W with f applied, Φ_T 212.808 W, MVHR Φ_V 51 W, Φ_HL 263.808 W, HLC 10.9920 W/K); `ka-mvhr-infiltration` (Φ_V 91.8 W, Φ_HL 304.608 W); `ka-dhdg-allowance` (Φ_HL × 1.10 = 417.389 W); `ka-single-layer-target` (invented λ 1.4: U 4.142012, utilisation 0.9204 against a target of 4.5); `ka-roof-upward` (R_si 0.10 for upward flow: R 2.14 m²K/W, U 0.467290, utilisation 0.9346; the 18 °C partition U 2.173913 is not held to the outside target) — analytic, Confirmed, recomputed independently in python3. The conductivities in the cases are invented illustrative inputs, not design data (they are deliberately not the familiar textbook values). The CIBSE *Domestic Heating Design Guide* (2026 ed.) worked examples and the CIBSE Guide A (8th ed.) Ch. 3 U-value examples are **not pinned**: they need the owner's copies, and no example is entered from memory. Worked example: `semi-detached-living-room` (Φ_HL 638.11 W, 39.4 W/m²).

**Citations to check.** The BS EN 12831-1 clause numbers (cl. 6.3.2 transmission, cl. 6.3.3 ventilation) and the BS EN ISO 6946 clause numbers are not verified here and are to be checked against the owner's copies. "CIBSE DHDG reduced method" and its 2026 edition are the brief's names for the method and are unverified; the additional-allowance form is this product's own simplification, not a quotation of the guide.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Semi-detached living room: U-values and design heat loss for the emitter sizing ([sheet](../../worked-examples/building-services/bs.room-heat-loss/semi-detached-living-room/sheet.txt)).

```text
R_1 = Front wall: 0.13 + (0.12202 + 3.3333 + 0.5 + 0.0325) + 0 + 0.04 = 4.1579 m²·K/W
U_1 = 1 / 4.1579 = 0.24051 W/(m²·K)
Phi_1 = 8.7 × 0.24051 × 1 × (21 − -3) °C = 50.218 W
U_2 = Window: 1.4 W/m²K = 1.4 W/(m²·K)
Phi_2 = 2.1 × 1.4 × 1 × (21 − -3) °C = 70.56 W
R_3 = Gable wall: 0.13 + (0.12202 + 3.3333 + 0.5 + 0.0325) + 0 + 0.04 = 4.1579 m²·K/W
U_3 = 1 / 4.1579 = 0.24051 W/(m²·K)
Phi_3 = 8.64 × 0.24051 × 1 × (21 − -3) °C = 49.872 W
R_4 = Party wall: 0.13 + (0.0325 + 0.16667 + 0.16667 + 0.0325) + 0.18 + 0.13 = 0.83833 m²·K/W
U_4 = 1 / 0.83833 = 1.1928 W/(m²·K)
Phi_4 = 8.64 × 1.1928 × 1 × (21 − 18) °C = 30.918 W
U_5 = Ground floor: 0.25 W/m²K = 0.25 W/(m²·K)
Phi_5 = 16.2 × 0.25 × 1 × (21 − -3) °C = 97.2 W
A_fab = 44.28 m²
… 17 more step(s) on the sheet
Governing: PASS  U-value of Party wall against its target (1.2 W/m²K): u_4 = 0.99404 vs u_4 ≤ 1  utilisation 0.994
```
<!-- hand-arithmetic:end -->
