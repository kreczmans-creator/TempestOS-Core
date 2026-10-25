# E4 — Heat sink thermal resistance chain (`elec.thermal-chain@1.0.1`)

**In plain terms.** The heat a component makes has to flow out to the air through a chain of resistances: from the chip to its case, from the case through the interface pad to the heat sink, and from the sink to the air. Each resistance in kelvin per watt times the power gives that stage's temperature rise. Adding the rises to the ambient gives the junction temperature. Turned round, the permitted rise divided by the power gives the largest total resistance the chain may have: the budget a heat sink is chosen against.

**Scope.** Steady-state source (junction) temperature from any number of thermal resistances in series to ambient. Ported from the legacy `calc.thermal-resistance-chain` (ADR-0167); filed under Electrical as electronics cooling (D-40). Method after Çengel & Ghajar §15-2; resistances as JEDEC JESD51-1 defines them; clause numbers only.

**Method.** `ΔT_i = P·R_i`; `R_tot = ΣR_i`; `ΔT_tot = P·R_tot`; `T_1 = T_a + ΔT_tot`, each node below it the one above less that stage's rise; margin `T_max − T_1`; budget `R_max = (T_max − T_a)/P`; `u = ΔT_tot/(T_max − T_a)`.

**Inputs.** `P`, `T_a`, table `stages` (name, resistance), `T_max`. **Outputs.** `T_1`, `dT_m`, `u`, `R_max` (unbounded at zero power), `R_tot`, `dT_tot`, `dT_i` and node temperatures `T_i`. **Checks.** u ≤ 1 (source temperature at or below its maximum).

**Limitations.** Steady state only (no thermal capacitance or transient overload); one series path (no parallel path through the board); constant resistances. Rises are temperature differences in kelvin; only node temperatures carry Celsius's offset. A chain with no stages, a zero total resistance, or a maximum at or below ambient blocks.

**Test cases.** The legacy module's four hand calculations (`ka-legacy-three-stage`, `ka-legacy-sink-selection`, `ka-legacy-over-limit`, `ka-legacy-on-limit`).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Three-stage chain, 25 W, 40 °C ambient ([sheet](../../worked-examples/electrical/elec.thermal-chain/three-stage-25W/sheet.txt)).

```text
R_tot = 0.5 + 0.2 + 1.8 K/W = 2.5 K/W
dT_tot = 25 W × 2.5 K/W = 62.5 K
T_1 = 40 degC + 62.5 K = 102.5 degC
dT_1 = 25 W × 0.5 K/W = 12.5 K
dT_2 = 25 W × 0.2 K/W = 5 K
T_2 = 102.5 °C − 12.5 K = 90 degC
dT_3 = 25 W × 1.8 K/W = 45 K
T_3 = 90 °C − 5 K = 85 degC
dT_m = 125 degC − 102.5 degC = 22.5 K
u = 62.5 K / (125 degC − 40 degC) = 0.73529
R_max = (125 degC − 40 degC) / 25 W = 3.4 K/W
Governing: PASS  Source temperature T_1 ≤ T_max: u = 0.73529 vs u ≤ 1  utilisation 0.735  « Çengel §15-2
```
<!-- hand-arithmetic:end -->
