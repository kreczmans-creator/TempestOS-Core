# Bladder accumulator for a clamp circuit: 14 L delivered between 100 and 210 bar (`fp.accumulator`)

**Source.** Analytic (isothermal gas law p·V = const, V0 = ΔV/[(p0/p1) − (p0/p2)]; pressures after the Parker HY10-4004/UK numbers quoted in Addendum B, read as absolute bar; size list invented for this example), V0 = 14/(0.9 − 0.428571) = 29.69697 L; next listed size 32 L; p0 at 20 °C from 50 °C = 90·293.15/323.15 = 81.64475 bar abs; PS = 210 − 1.01325 = 208.98675 bar; bar·L = 208.98675·32 = 6687.576 — Confirmed.

Delivering 14 L between 100 and 210 bar from a gas pre-charge of 90 bar (all absolute, isothermal) needs 29.70 L of gas volume, so the next listed size is 32 L. The pre-charge sits exactly on the 0.9·p1 limit and the pressure ratio 210/90 is well inside 4. Charged at 20 °C for a 50 °C maximum the pre-charge is 81.6 bar absolute. The 32 L at 208.99 bar gauge is 6688 bar·L, far above the 250 bar·L indication, and the sheet warns that 210 bar is above the 200 bar where real-gas behaviour should be considered. The numbers are those quoted for the Parker HY10-4004/UK example (not confirmed against the catalogue); the size list is illustrative.

| Input | Value | Source |
|---|---|---|
| `T_c` | 20 degC | assumption: Charged in the workshop at 20 °C. |
| `T_max` | 50 degC | spec: Hydraulic specification HS-12: oil temperature limit 50 °C |
| `dV` | 14 L | spec: Hydraulic specification HS-12: clamp stroke plus leakage make-up, 14 L per cycle |
| `f_rg` | 1 | assumption: Ideal gas; the highest pressure is just above 200 bar, so the sheet warns. |
| `mode` | Energy storage | assumption: Slow clamp-and-hold cycle: stored-energy duty. |
| `n` | 1 | assumption: Isothermal: the clamp holds for minutes between cycles. |
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
| `V0` | 29.697 L | 29.697 L | PASS |
| `V_sel` | 32 L | 32 L | PASS |
| `u_V` | 0.92803  | 0.92803  | PASS |
| `p0` | 81.6447 bar | 81.6447 bar | PASS |
| `u_r0` | 1  | 1  | PASS |
| `u_rp` | 0.583333  | 0.583333  | PASS |
| `bar_litres` | 6687.58 bar*L | 6687.58 bar*L | PASS |

Governing check: Pre-charge within the p0/p1 rule — PASS (utilisation 1.000). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
