# Press ram: 125/90 cylinder at 180 bar on a 45 cm³/rev pump, lines checked, rod screened for buckling (`fp.cylinder`)

**Source.** Analytic (closed-form areas, force, speed, pump flow and power, line continuity and the Johnson parabola; all numbers are an invented design example), A_b = π·125²/4 = 122.718 cm²; A_a = 122.718 − 63.617 = 59.101 cm²; F_ext = 180e5·122.718e-4·0.95 = 209.849 kN; Q_p = 45 cm³/rev·1450 rpm·0.95 = 61.9875 L/min; P = 180e5·61.9875e-3/60/0.88 = 21.132 kW; T = 139.170 N·m; return flow 61.9875·122.718/59.101 = 128.712 L/min, 2.667 m/s in 32 mm, and the same flow in the 25 mm cap-port line on the retract stroke, 4.370 m/s; suction at the geometric delivery 45 cm³/rev·1450 rpm = 65.25 L/min, 0.554 m/s in 50 mm; λ = 950/22.5 = 42.22 < 91.05: Johnson F_cr = 2838.87 kN, judged at the larger of the 150 kN push load and the 209.85 kN thrust: u_buck = 209.85·3.5/2838.87 = 0.2587 — Confirmed.

The ram makes 209.8 kN on the press stroke and 101.1 kN on the return, against loads of 150 and 60 kN, and needs a swept volume of 9.82 L per stroke. The pump delivers 61.99 L/min, 3 % above the 60 L/min the ram takes, for 21.1 kW of shaft power and 139 N·m at 1450 rpm. The return line carries the cap-side discharge, 128.7 L/min on the retract stroke, at 2.67 m/s in the 32 mm line: close to its 3 m/s limit (utilisation 0.89), and it governs the line checks. The 25 mm line to the cap port carries the same 128.7 L/min on the retract stroke, 4.37 m/s against its 5 m/s limit (utilisation 0.87). The suction line carries the pump's geometric delivery, 65.25 L/min; the smallest suction bore the 1.2 m/s limit needs is 34.0 mm, so the next listed size is 38 mm; the 50 mm line drawn is larger than that. The 90 mm rod over 950 mm (clevis at both ends, K = 1) has a slenderness of 42, below the 91 transition, so the Johnson parabola gives a critical load of 2839 kN; the rod is judged at the larger of the 150 kN push load and the ram's own 209.8 kN thrust, which gives a buckling utilisation of 0.26 at a safety factor of 3.5. This is a screening only; the ISO/TS 13725 method is not implemented. The velocity limits and the hose list are placeholders for your own records, and the figures are an invented design example.

| Input | Value | Source |
|---|---|---|
| `D` | 125 mm | drawing: DWG-4310 rev B |
| `E` | 210 GPa | reference: record: rod material (chromed carbon steel), to be taken from the material library |
| `F_pull` | 60 kN | spec: Hydraulic specification HS-31: return stroke load 60 kN |
| `F_push` | 150 kN | spec: Hydraulic specification HS-31: pressing force 150 kN |
| `K_m` | 1 | reference: record: cylinder mounting (clevis at both ends, pinned), to be taken from the cylinder maker's mounting guidance |
| `L_add` | 150 mm | drawing: DWG-4310 rev B |
| `Q` | 60 L/min | spec: Hydraulic specification HS-31: ram flow 60 L/min |
| `S_f` | 3.5 | assumption: Safety factor chosen for this example; the design reference sets the real one. |
| `V_g` | 45 cm^3/rev | spec: Hydraulic specification HS-31: fixed-displacement pump 45 cm³/rev |
| `d_prs` | 25 mm | drawing: DWG-4311 rev A |
| `d_r` | 90 mm | drawing: DWG-4310 rev B |
| `d_ret` | 32 mm | drawing: DWG-4311 rev A |
| `d_suc` | 50 mm | drawing: DWG-4311 rev A |
| `eta_m` | 0.95 | assumption: Typical seal and guide friction for a new cylinder; to be confirmed against the cylinder maker's figure. |
| `eta_tp` | 0.88 | assumption: Pump total efficiency at 180 bar; to be taken from the pump data sheet. |
| `eta_vc` | 0.98 | assumption: Allowance for leakage past the piston; to be confirmed against the cylinder maker's figure. |
| `eta_vp` | 0.95 | assumption: Pump volumetric efficiency at 180 bar; to be taken from the pump data sheet. |
| `f_y` | 500 MPa | reference: record: rod material (chromed carbon steel), to be taken from the rod maker's certificate |
| `hoses` | 7 row(s) | reference: record: illustrative hose size list invented for this example, to be replaced by the supplier's range |
| `mode` | Standard | assumption: Standard connection: the ram needs its full bore force for the press stroke. |
| `n_p` | 1450 rpm | spec: Hydraulic specification HS-31: 4-pole motor, 1450 rpm |
| `p` | 180 bar | spec: Hydraulic specification HS-31: relief valve set 180 bar at the ram |
| `p_back` | 0 bar | assumption: Return line direct to tank; the return filter drop is neglected for this example. |
| `rho` | 870 kg/m^3 | reference: fluid data sheet: mineral oil, density at 40 °C, to be taken from the oil supplier's data |
| `stroke` | 800 mm | drawing: DWG-4310 rev B |
| `t_dwell` | 0 s | assumption: Cycle time of the strokes only; dwell added by the machine controller. |
| `v_prs_max` | 5 m/s | reference: record: line velocity limits, a placeholder to be replaced from the design reference |
| `v_ret_max` | 3 m/s | reference: record: line velocity limits, a placeholder to be replaced from the design reference |
| `v_suc_max` | 1.2 m/s | reference: record: line velocity limits, a placeholder to be replaced from the design reference |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F_ext` | 209.849 kN | 209.849 kN | PASS |
| `F_ret` | 101.063 kN | 101.063 kN | PASS |
| `v_ext` | 79.8576 mm/s | 79.8576 mm/s | PASS |
| `t_cycle` | 14.8424 s | 14.8424 s | PASS |
| `dV` | 9.81748 L | 9.81748 L | PASS |
| `Q_p` | 61.9875 L/min | 61.9875 L/min | PASS |
| `u_pump` | 0.967937  | 0.967937  | PASS |
| `P_pump` | 21.1321 kW | 21.1321 kW | PASS |
| `T_shaft` | 139.17 N*m | 139.17 N*m | PASS |
| `v_rtn` | 2.66733 m/s | 2.66733 m/s | PASS |
| `u_rtn` | 0.88911  | 0.88911  | PASS |
| `d_rec_suc` | 38 mm | 38 mm | PASS |
| `v_prs` | 4.37015 m/s | 4.37015 m/s | PASS |
| `u_prs` | 0.87403  | 0.87403  | PASS |
| `F_cr` | 2838.87 kN | 2838.87 kN | PASS |
| `F_buck` | 209.849 kN | 209.849 kN | PASS |
| `u_buck` | 0.258719  | 0.258719  | PASS |

Governing check: Pump delivery covers the circuit flow — PASS (utilisation 0.968). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
