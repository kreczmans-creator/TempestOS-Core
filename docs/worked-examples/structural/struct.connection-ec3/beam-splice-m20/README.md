# Beam web splice: M20 8.8 in double shear through 10 mm S275 plates (`struct.connection-ec3`)

**Source.** Analytic (EN 1993-1-8 Table 3.4), EN 1993-1-8:2005 with UK NA, Two cover plates, two shear planes through the threads, 55 kN per bolt from the bolt group (M3) — Confirmed.

Each M20 8.8 bolt carries 55 kN in double shear (188 kN resistance), so bearing on the 10 mm plate governs: k₁ stays at its 2.5 cap with the 35 mm edge distance, and the 40 mm end distance gives α_b = 0.606, for 99.4 kN per bolt (utilisation 0.55).

| Input | Value | Source |
|---|---|---|
| `F_t_Ed` | 0 kN | assumption: No tension. |
| `F_v_Ed` | 55 kN | assumption: Governing bolt from the bolt-group calculation (M3). |
| `e_1` | 40 mm | drawing: SPL-001 rev B |
| `e_2` | 35 mm | drawing: SPL-001 rev B |
| `f_u` | 410 MPa | spec: EN 10025-2 S275, 3 ≤ t ≤ 100 mm |
| `f_y` | 275 MPa | spec: EN 10025-2 S275, t ≤ 16 mm |
| `gamma_M0` | 1 | spec: UK NA to BS EN 1993-1-1, NA.2.15 |
| `gamma_M2` | 1.25 | spec: UK NA to BS EN 1993-1-8, NA.2.3 |
| `grade` | 8.8 | drawing: SPL-001 rev B |
| `k_e1` | 1.2 | spec: EN 1993-1-8 Table 3.3 |
| `k_e2` | 1.2 | spec: EN 1993-1-8 Table 3.3 |
| `k_p1` | 2.2 | spec: EN 1993-1-8 Table 3.3 |
| `k_p2` | 2.4 | spec: EN 1993-1-8 Table 3.3 |
| `n_s` | 2 | drawing: SPL-001 rev B |
| `p_1` | 70 mm | drawing: SPL-001 rev B |
| `size` | M20 | drawing: SPL-001 rev B |
| `steel` | S275 | drawing: SPL-001 rev B |
| `t` | 10 mm | drawing: SPL-001 rev B |
| `threads` | yes | assumption: Threads may lie in the shear planes (short bolts). |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F_v_Rd` | 188.002 kN | 188.002 kN | PASS |
| `F_b_Rd` | 99.3939 kN | 99.3939 kN | PASS |
| `u_b` | 0.553354  | 0.553354  | PASS |

Governing check: Bolt bearing — PASS (utilisation 0.55). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
