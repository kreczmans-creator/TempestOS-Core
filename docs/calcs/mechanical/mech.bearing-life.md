# Rolling bearing life (ISO 281) (`mech.bearing-life@1.3.0`)

**In plain terms.** A rolling bearing wears out by fatigue of its rolling surfaces, and the load sets how many revolutions that takes: halving the load multiplies the life of a ball bearing by eight. The worksheet follows ISO 281 by combining radial and axial load, or a whole duty cycle, into one equivalent load, giving the life in hours at the running speed, adjusting it for the reliability and lubrication you need, and checking the static load safety factor.

## Scope

Calculates the basic and modified rating life of a rolling bearing under a steady load or a duty cycle, with the static safety factor, against required values. It takes ratings, load factors and a_ISO from reference records.

## Method

- Equivalent load: F_dyn = X_1·F_r + Y_1·F_a when F_a/F_r ≤ e_lim (X_1 = 1, Y_1 = 0 for a single-row radial ball bearing), else F_dyn = X·F_r + Y·F_a (Y = 0 is refused when an axial load acts above e_lim)
- Duty cycle: F_dyn = (Σ F_dyn,i^p·U_i / Σ U_i)^(1/p)
- Basic rating life: L_10 = (C/F_dyn)^p million revolutions, p = 3 ball, 10/3 roller
- Life in hours: L_10h = 10⁶·L_10 / (60·n)
- Reliability factor (ISO 281:2007, derived): a_1 = 0.95·[ln(100/R_rel)/ln(100/90)]^(2/3) + 0.05
- Modified life: L_nm = a_1·a_ISO·L_10
- Static safety: P_0 = max(X_0·F_r + Y_0·F_a, F_r), s_0 = C_0/P_0

The equivalent load is `F_dyn = X_1·F_r + Y_1·F_a` when `F_a/F_r ≤ e_lim` (`X_1 = 1`, `Y_1 = 0` for a single-row radial ball bearing, the defaults; `Y_1 > 0` for double-row angular-contact and paired bearings), else `X·F_r + Y·F_a` (ISO 281:2007 cl. 6–7). `Y = 0` with an axial load above `e_lim` is refused (rule `axial-factor`): it would make the axial load count for nothing in `F_dyn`, so the sheet asks for the bearing's X and Y instead of passing on `F_dyn = F_r`. Pure thrust (`F_r = 0`) is refused the same way. `L_10h = 10⁶ L_10/(60 n)`. The reliability factor is derived (ISO 281:2007); ISO 281:1990 gave 0.62 at 95 % and 0.21 at 99 %. `P_0 = max(X_0 F_r + Y_0 F_a, F_r)` and `s_0 = C_0/P_0` follow ISO 76:2006. The factor `e` is entered as `e_lim` because `e` is the engine's constant. `kappa` has the range 0.1 to 100: below 0.1 is refused by the range, and above 4 it is treated as 4 with a warning.

**Reference diagram.** The worksheet draws the bearing on its shaft in section, a ball or a roller bearing by `type`. It binds `F_r`, `F_a` and `n` to the loads and the speed and `C` to the rings. `C_0`, `e_lim`, `X`, `Y`, `R_rel`, `a_iso`, `L_req`, `s_0_req` and `duty` are labels. The drawing is not to scale (ADR-0158, DI-08).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `type` | Bearing type |  | ball (p = 3) |  | Setup |
| `F_r` | Radial load | kN | 2 | 0 to 1000 | Loads |
| `F_a` | Axial load | kN | 0 | 0 to 1000 | Loads |
| `n` | Speed | rpm | 1000 | 1 to 30000 | Loads |
| `duty` | Duty cycle (optional; blank = the steady load F_r, F_a) |  | optional |  | Loads |
| `duty.F_r` | Radial load (column) | kN |  | | Loads |
| `duty.F_a` | Axial load (column) | kN |  | | Loads |
| `duty.U` | Share of revolutions (column) |  |  | | Loads |
| `C` | Basic dynamic load rating | kN | 20 | 0.1 to 10000 | Bearing |
| `C_0` | Basic static load rating | kN | 11 | 0.1 to 10000 | Bearing |
| `e_lim` | Load ratio limit e |  | 0.3 | 0 to 5 | Factors |
| `X_1` | Radial factor X₁ for F_a/F_r ≤ e (1 for a single-row radial ball bearing) |  | 1 | 0 to 5 | Factors |
| `Y_1` | Axial factor Y₁ for F_a/F_r ≤ e (0 for a single-row radial ball bearing; above 0 for double-row and paired bearings) |  | 0 | 0 to 10 | Factors |
| `X` | Radial factor for F_a/F_r above e |  | 1 | 0 to 5 | Factors |
| `Y` | Axial factor for F_a/F_r above e (from the manufacturer; zero makes the axial load count for nothing and is refused when one acts) |  | 0 | 0 to 10 | Factors |
| `X_0` | Static radial factor X₀ |  | 0.6 | 0 to 5 | Factors |
| `Y_0` | Static axial factor Y₀ |  | 0.5 | 0 to 10 | Factors |
| `R_rel` (was `R`) | Reliability | % | 90 | 90 to 99.9 | Life |
| `a_iso` | Life modification factor a_ISO (from the manufacturer's tool) |  | 1 | 0.1 to 20 | Life |
| `kappa` | Viscosity ratio κ, 0.1 to 100, used for a_ISO (optional; blank = κ is not checked; above 4 it is read at 4) |  | optional | 0.1 to 4 | Life |
| `L_req` | Required life | h | 20000 | 1 to 1000000 | Requirements |
| `s_0_req` | Required static safety factor |  | 1 | 0.5 to 3 | Requirements |

## Assumptions

1. The ratings, load factors and a_ISO are the manufacturer's values, entered as reference records. Basis: No catalogue table ships with TempestOS.
2. The reliability factor a₁ uses the ISO 281:2007 Weibull form, 0.64 at 95 % and 0.25 at 99 %. Basis: ISO 281:1990 gave 0.62 and 0.21, and the sheet states the edition.
3. Loads are steady within each duty step, and the speed is constant. Basis: Duty steps are weighted by their share of revolutions.

## Outputs

| Symbol | Description |
|---|---|
| `F_dyn` | Equivalent dynamic load |
| `L_10` | Basic rating life in millions of revolutions |
| `L_10h` | Basic rating life |
| `a_1` | Reliability factor |
| `L_nm` | Modified rating life in millions of revolutions |
| `L_nmh` | Modified rating life |
| `P_0` | Equivalent static load |
| `s_0` | Static safety factor |
| `u_life` | Life utilisation |
| `u_s0` | Static utilisation |

`F_dyn` is the equivalent dynamic load; it was `P` until 1.3.0, and `P` is an alias of the port `bearing.load_dynamic`. The reliability input is `R_rel` (it was `R`; the old symbol is an alias, so an inputs file, a CSV, a saved draft and a pack example that use `R` still load). The utilisations are `u_life` and `u_s0`.

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `bearing.life_modified` | `L_nmh` | h | Modified rating life in hours |
| `bearing.life_basic` | `L_10h` | h | Basic rating life in hours |
| `bearing.load_dynamic` | `F_dyn` (also `P`) | kN | Equivalent dynamic load |
| `bearing.load_static` | `P_0` | kN | Equivalent static load |
| `bearing.safety_static` | `s_0` |  | Static safety factor |

## Checks

- Required life against modified life
- Required static safety factor against the static safety factor

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `no-load` | Block | Conservative | No load: nothing to compute. |
| `kappa-low` | Block | Conservative | κ below 0.1 is outside the range a_ISO is defined for. |
| `kappa-high` | Warning | Conservative | κ above 4 is treated as 4 when a_ISO is read. |
| `light-load` | Warning | Conservative | Load so small against C that L_10 exceeds 10¹² million revolutions: the life is capped at that value. |
| `axial-factor` | Block | Conservative | An axial load acts above e_lim but Y is 0, so it would count for nothing: enter the bearing's Y. |
| `heavy-load` | Warning | Unconservative | P above 0.5·C: the rating-life equations lose accuracy. |
| `duty` | Block | Conservative | A duty cycle needs a positive total share of revolutions. |

## Limitations

The speed is constant within each duty step. a_ISO is the manufacturer's value and is not computed. A load above 0.5·C warns. A load so light that `L_10` would exceed 10¹² million revolutions no longer blocks: the life is held at 10¹² million revolutions with a warning (`light-load`). The share-of-revolutions column of the duty table is named `U`; a saved table keeps that key, so it is not renamed.

## Changes

1.3.0 (C-2): the equivalent dynamic load `P` is now `F_dyn` and the reliability `R` is now `R_rel` (the old symbols stay as aliases). The inputs are grouped (`Setup`, `Loads`, `Bearing`, `Factors`, `Life`, `Requirements`; the optional duty table is last in `Loads`) and the numeric inputs have usual ranges inside their hard ranges. The two checks show the required value against the achieved value ("Required life against modified life" and "Required static safety factor against the static safety factor"). The block and warning messages are at most two sentences of 20 words or fewer and name one input. The record kind `bearing.aiso` is titled "Bearing aISO life modification factor". The scope, the assumptions and the Shigley reference follow the standard. No calculated value changes. The port names of 1.2.0 (`bearing.life_modified`, `bearing.life_basic`, `bearing.load_dynamic`, `bearing.load_static`, `bearing.safety_static`) are unchanged.

## References

- ISO 281:2007, cl. 6–7: Dynamic equivalent load and basic rating life.
- ISO 281:2007, cl. 9: Modified rating life.
- ISO 76:2006, cl. 6–7: Static equivalent load and static safety factor.
- Budynas and Nisbett, Shigley's Mechanical Engineering Design, Ch. 11, 11th ed.: Rolling-contact bearings.

## Test cases

`ka-ball-95` (L_10h 16 666.7 h, a_1 0.6379, analytic, Confirmed); `ka-roller-99` (L_1m 1070.03 million revolutions, analytic, Confirmed); `ka-combined` (`F_dyn` 4.68 kN, s_0 5.333, analytic, Confirmed); `ka-duty-cycle` (equivalent load 3.3019 kN, analytic, Confirmed); `ka-skf-6206` (C 20.3 kN and C_0 11.2 kN from a distributor listing of the SKF catalogue, Unconfirmed until checked against SKF PUB BU/P1 17000).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Pump shaft bearing, SKF 6206 at 1500 rpm, 95 % reliability ([sheet](../../worked-examples/mechanical/mech.bearing-life/skf-6206-pump/sheet.txt)).

```text
p = 3
F_dyn = 1 × 2.5 kN + 0 × 0 kN = 2.5 kN
P_0 = max(0.6 × 2.5 kN + 0.5 × 0 kN, 2.5 kN) = 2.5 kN
L_10 = (20.3 kN / 2.5 kN)^3 = 535.39
L_10h = 535.39 × 1000000 × 2 × π / 1500 rpm = 5948.7 h
a_1 = 0.95 × (ln(1 / 95 %) / ln(1 / 0.9))^(2 / 3) + 0.05 = 0.63791
L_nm = 0.63791 × 2 × 535.39 = 683.06
L_nmh = 0.63791 × 2 × 5948.7 h = 7589.6 h
s_0 = 11.2 kN / 2.5 kN = 4.48
u_life = 5000 h / 7589.6 h = 0.6588
u_s0 = 1 / 4.48 = 0.22321
Governing: PASS  Required life against modified life: 5000 h vs 7590 h  utilisation 0.66  « ISO 281:2007 cl. 9
```
<!-- hand-arithmetic:end -->
