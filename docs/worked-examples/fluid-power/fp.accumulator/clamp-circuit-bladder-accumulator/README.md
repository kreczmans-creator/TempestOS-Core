# Bladder accumulator for a clamp circuit: 14 L delivered between 100 and 210 bar (`fp.accumulator`)

**Source.** Analytic (isothermal gas law p·V = const, V0 = ΔV/[(p0/p1) − (p0/p2)]; pressures after the Parker HY10-4004/UK numbers quoted second-hand, read as absolute bar; size list invented for this example), V0_ideal = 14/(0.9 − 0.428571) = 29.69697 L; oil range 30 to 50 °C: f_T = 323.15/303.15 = 1.065974, V0 = 31.65620 L; next listed size 32 L; p0 at 20 °C from 50 °C = 90·293.15/323.15 = 81.64475 bar abs; p0 at 30 °C = 90·303.15/323.15 = 84.42983 bar abs, p2/p0 = 2.48727; PS 230 bar gauge on the 32 L: bar·L = 230·32 = 7360 — Confirmed.

Delivering 14 L between 100 and 210 bar from a gas pre-charge of 90 bar (all absolute, isothermal) needs 29.70 L of gas at the 50 °C maximum; because the oil can fall to 30 °C the gas delivers 6.6 % less when cold, so V0 is scaled by 323.15/303.15 to 31.66 L and the next listed size is 32 L (99 % utilised). The pre-charge sits exactly on the 0.9·p1 limit and the pressure ratio at the cold pre-charge of 84.4 bar (210/84.4 = 2.49) is well inside 4. Charged at 20 °C for a 50 °C maximum the pre-charge is 81.6 bar absolute. With the vessel's relief setting PS = 230 bar gauge the 32 L is 7360 bar·L, far above the 250 bar·L indication, and the sheet warns that 210 bar is above the 200 bar where real-gas behaviour should be considered. The numbers are those quoted for the Parker HY10-4004/UK example (not confirmed against the catalogue); the oil temperature range, the size list and the relief setting are illustrative.

| Input | Value | Source |
|---|---|---|
| `PS` | 230 bar | spec: Hydraulic specification HS-12: accumulator relief setting 230 bar gauge (vessel PS; illustrative, to be replaced by the nameplate) |
| `T_c` | 20 degC | assumption: Charged in the workshop at 20 °C. |
| `T_max` | 50 degC | spec: Hydraulic specification HS-12: oil temperature limit 50 °C |
| `T_min` | 30 degC | assumption: Oil temperature range assumed 30 to 50 °C in service (invented for this example); the gas is coldest at 30 °C. |
| `dV` | 14 L | spec: Hydraulic specification HS-12: clamp stroke plus leakage make-up, 14 L per cycle |
| `f_rg` | 1 | assumption: Ideal gas; the highest pressure is just above 200 bar, so the sheet warns. |
| `mode` | Energy storage | assumption: Slow clamp-and-hold cycle: stored-energy duty. |
| `n_gas` | 1 | assumption: Isothermal: the clamp holds for minutes between cycles. |
| `p_0` | 90 bar | spec: Hydraulic specification HS-12: pre-charge 90 bar at the maximum oil temperature |
| `p_1` | 100 bar | spec: Hydraulic specification HS-12: lowest clamp pressure 100 bar |
| `p_2` | 210 bar | spec: Hydraulic specification HS-12: pump cut-out 210 bar |
| `p_atm` | 1.01325 bar | assumption: Standard atmosphere, derived constant. |
| `p_basis` | Absolute | assumption: Pressures entered as absolute bar, as the source numbers are quoted. |
| `r_0` | 0.9 | reference: record: accumulator pre-charge rules (bladder type), to be taken from the manufacturer's guide |
| `r_p` | 4 | reference: record: accumulator pre-charge rules (bladder type), to be taken from the manufacturer's guide |
| `sizes` | 4 row(s) | reference: record: illustrative size list invented for this example, to be replaced by the manufacturer's range |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `V0` | 31.6562 L | 31.6562 L | PASS |
| `V_sel` | 32 L | 32 L | PASS |
| `u_V` | 0.989256  | 0.989256  | PASS |
| `p0` | 81.6447 bar | 81.6447 bar | PASS |
| `u_r0` | 1  | 1  | PASS |
| `u_rp` | 0.621818  | 0.621818  | PASS |
| `bar_litres` | 7360 bar*L | 7360 bar*L | PASS |

Governing check: Pre-charge against the p0/p1 rule — PASS (utilisation 1.00). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
