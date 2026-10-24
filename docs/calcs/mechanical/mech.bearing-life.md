# M5 — Rolling bearing life, ISO 281 (`mech.bearing-life@1.1.0`)

**Scope.** Basic and modified rating life of a rolling bearing under a steady load or a duty cycle, at a chosen reliability, against a required life; static safety factor s₀ against a required value. Ratings, X/Y/e and a_ISO come from reference records (`bearing.ratings`, `bearing.factors`, `bearing.aiso`); no catalogue table ships.

**Method.** `P = X_1·F_r + Y_1·F_a` when `F_a/F_r ≤ e_lim` (`X_1 = 1`, `Y_1 = 0` for a single-row radial ball bearing, the defaults; `Y_1 > 0` for double-row angular-contact and paired bearings), else `X·F_r + Y·F_a` (ISO 281:2007 cl. 6–7). **`Y = 0` with an axial load above `e_lim` is refused** (rule `axial-factor`): it would make the axial load count for nothing in `P`, so the sheet asks for the bearing's X and Y instead of passing on `P = F_r`; pure thrust (`F_r = 0`) is refused the same way. Duty: `P_eq = (Σ P_i^p U_i / Σ U_i)^(1/p)`. `L_10 = (C/P)^p`, p = 3 ball, 10/3 roller; `L_10h = 10⁶ L_10/(60 n)`. `a_1 = 0.95 [ln(100/R)/ln(100/90)]^(2/3) + 0.05` (ISO 281:2007, derived; ISO 281:1990 gave 0.62 at 95 % and 0.21 at 99 %). `L_nm = a_1 a_ISO L_10` (cl. 9). `P_0 = max(X_0 F_r + Y_0 F_a, F_r)`, `s_0 = C_0/P_0` (ISO 76:2006).

**Inputs.** `type`; `F_r`, `F_a` 0–2000 kN; optional `duty` table (F_r, F_a, U; ≤ 50 rows); `n` 1–50 000 rpm; `C`, `C_0` kN; `e_lim`, `X_1`, `Y_1`, `X`, `Y`, `X_0`, `Y_0`; `R` 90–99.95 %; `a_iso` 0.1–50; optional `kappa` (range 0.1–100, so below 0.1 is refused by the range; above 4 it is treated as 4 with a warning); `L_req` h; `s_0_req` 0.5–4. The factor `e` is entered as `e_lim` because `e` is the engine's constant.

**Outputs.** `P`, `L_10`, `L_10h`, `a_1`, `L_nm`, `L_nmh`, `P_0`, `s_0`, `u_life`, `u_s0`. **Ports.** `L_nmh`, `L_10h` (h), `P`, `P_0` (kN), `s_0`. **Checks.** modified life against required; s₀ against required.

**Limitations.** Constant speed per step; a_ISO is the manufacturer's value, not computed; P > 0.5 C warns. A load so light that `L_10` would exceed 10¹² million revolutions is no longer blocked: the life is held at 10¹² Mrev with a warning (`light-load`).

**Test cases.** `ka-ball-95` (L_10h 16 666.7 h, a_1 0.6379 — analytic, Confirmed); `ka-roller-99` (L_1m 1070.03 Mrev — analytic, Confirmed); `ka-combined` (P 4.68 kN, s_0 5.333 — analytic, Confirmed); `ka-duty-cycle` (P_eq 3.3019 kN — analytic, Confirmed); `ka-skf-6206` (C 20.3 kN, C_0 11.2 kN from a distributor listing of the SKF catalogue — **Unconfirmed** until checked against SKF PUB BU/P1 17000).
