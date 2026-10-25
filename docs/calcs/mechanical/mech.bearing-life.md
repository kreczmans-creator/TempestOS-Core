# M5 — Rolling bearing life, ISO 281 (`mech.bearing-life@1.1.0`)

**In plain terms.** A rolling bearing wears out by fatigue of its rolling surfaces, and the load sets how many revolutions that takes: halving the load multiplies the life of a ball bearing by eight. The worksheet follows ISO 281 by combining radial and axial load, or a whole duty cycle, into one equivalent load, giving the life in hours at the running speed, adjusting it for the reliability and lubrication you need, and checking the static load safety factor.

**Scope.** Basic and modified rating life of a rolling bearing under a steady load or a duty cycle, at a chosen reliability, against a required life; static safety factor s₀ against a required value. Ratings, X/Y/e and a_ISO come from reference records (`bearing.ratings`, `bearing.factors`, `bearing.aiso`); no catalogue table ships.

**Method.** `P = X_1·F_r + Y_1·F_a` when `F_a/F_r ≤ e_lim` (`X_1 = 1`, `Y_1 = 0` for a single-row radial ball bearing, the defaults; `Y_1 > 0` for double-row angular-contact and paired bearings), else `X·F_r + Y·F_a` (ISO 281:2007 cl. 6–7). **`Y = 0` with an axial load above `e_lim` is refused** (rule `axial-factor`): it would make the axial load count for nothing in `P`, so the sheet asks for the bearing's X and Y instead of passing on `P = F_r`; pure thrust (`F_r = 0`) is refused the same way. Duty: `P_eq = (Σ P_i^p U_i / Σ U_i)^(1/p)`. `L_10 = (C/P)^p`, p = 3 ball, 10/3 roller; `L_10h = 10⁶ L_10/(60 n)`. `a_1 = 0.95 [ln(100/R)/ln(100/90)]^(2/3) + 0.05` (ISO 281:2007, derived; ISO 281:1990 gave 0.62 at 95 % and 0.21 at 99 %). `L_nm = a_1 a_ISO L_10` (cl. 9). `P_0 = max(X_0 F_r + Y_0 F_a, F_r)`, `s_0 = C_0/P_0` (ISO 76:2006).

**Inputs.** `type`; `F_r`, `F_a` 0–2000 kN; optional `duty` table (F_r, F_a, U; ≤ 50 rows); `n` 1–50 000 rpm; `C`, `C_0` kN; `e_lim`, `X_1`, `Y_1`, `X`, `Y`, `X_0`, `Y_0`; `R` 90–99.95 %; `a_iso` 0.1–50; optional `kappa` (range 0.1–100, so below 0.1 is refused by the range; above 4 it is treated as 4 with a warning); `L_req` h; `s_0_req` 0.5–4. The factor `e` is entered as `e_lim` because `e` is the engine's constant.

**Outputs.** `P`, `L_10`, `L_10h`, `a_1`, `L_nm`, `L_nmh`, `P_0`, `s_0`, `u_life`, `u_s0`. **Ports.** `L_nmh`, `L_10h` (h), `P`, `P_0` (kN), `s_0`. **Checks.** modified life against required; s₀ against required.

**Limitations.** Constant speed per step; a_ISO is the manufacturer's value, not computed; P > 0.5 C warns. A load so light that `L_10` would exceed 10¹² million revolutions is no longer blocked: the life is held at 10¹² Mrev with a warning (`light-load`).

**Test cases.** `ka-ball-95` (L_10h 16 666.7 h, a_1 0.6379 — analytic, Confirmed); `ka-roller-99` (L_1m 1070.03 Mrev — analytic, Confirmed); `ka-combined` (P 4.68 kN, s_0 5.333 — analytic, Confirmed); `ka-duty-cycle` (P_eq 3.3019 kN — analytic, Confirmed); `ka-skf-6206` (C 20.3 kN, C_0 11.2 kN from a distributor listing of the SKF catalogue — **Unconfirmed** until checked against SKF PUB BU/P1 17000).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Pump shaft bearing, SKF 6206 at 1500 rpm, 95 % reliability ([sheet](../../worked-examples/mechanical/mech.bearing-life/skf-6206-pump/sheet.txt)).

```text
p = 3
P = 1 × 2.5 kN + 0 × 0 kN = 2.5 kN
P_0 = max(0.6 × 2.5 kN + 0.5 × 0 kN, 2.5 kN) = 2.5 kN
L_10 = (20.3 kN / 2.5 kN)^3 = 535.39
L_10h = 535.39 × 1000000 × 2 × π / 1500 rpm = 5948.7 h
a_1 = 0.95 × (ln(1 / 95 %) / ln(1 / 0.9))^(2 / 3) + 0.05 = 0.63791
L_nm = 0.63791 × 2 × 535.39 = 683.06
L_nmh = 0.63791 × 2 × 5948.7 h = 7589.6 h
s_0 = 11.2 kN / 2.5 kN = 4.48
u_life = 5000 h / 7589.6 h = 0.6588
u_s0 = 1 / 4.48 = 0.22321
Governing: PASS  Modified rating life against required life: u_life = 0.6588 vs u_life ≤ 1  utilisation 0.659  « ISO 281:2007 cl. 9
```
<!-- hand-arithmetic:end -->
