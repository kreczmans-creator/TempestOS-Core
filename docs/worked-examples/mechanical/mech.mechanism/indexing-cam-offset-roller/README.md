# Indexing-station cam: offset roller follower, cycloidal rise and 3-4-5 return, 120 rpm (`mech.mechanism`)

**Source.** Independent python3 recomputation (laws from their closed forms, pitch-curve curvature by a 4th-order finite-difference stencil, peaks by a scan of each segment polished by golden section), Rise 20 mm cycloidal over 120°, dwell 60°, return 20 mm 3-4-5 over 120°, dwell 60°, 120 rpm, R_b 40 mm, R_r 8 mm, offset +10 mm, follower 0.2 kg, spring 5 N/mm with 20 N preload: v_pk = 2hω/β = 0.24 m/s, a_pk = 4.5239 m/s² (cycloidal rise), j_pk = 259.2 m/s³ (3-4-5 return), F from 20 to 120 N, peak input torque 1.53449 N·m, φ_max 26.5958°, ρ_min 44.6811 mm, smallest base circle for φ ≤ 30° 32.9632 mm — Confirmed.

A 20 mm index stroke with a cycloidal rise over 120° and a 3-4-5 return over 120°, 60° of dwell at each end, 120 cycles a minute. The offset roller follower peaks at 0.24 m/s and 4.5 m/s²; the 3-4-5 return has the larger jerk (259 m/s³). The pressure angle peaks at 26.6° against the 30° allowed (utilisation 0.89; a 33.0 mm base circle would just reach 30°), the pitch curve's smallest radius of curvature is 44.7 mm against the 8 mm roller (no undercut), and the follower force stays between 20 and 120 N, so the spring keeps the follower on the cam. The follower mass, spring and preload are example values marked as assumptions: replace them from the CAD mass properties and the spring sheet.

| Input | Value | Source |
|---|---|---|
| `F_ext` | 0 N | assumption: No external load on the follower in this example. |
| `F_pre` | 20 N | assumption: Example preload at the base circle; link a force of the spring sheet. |
| `R_r` | 8 mm | drawing: IDX-12-CAM rev A |
| `Rb` | 40 mm | drawing: IDX-12-CAM rev A |
| `e_off` | 10 mm | drawing: IDX-12-CAM rev A |
| `follower` | Roller | drawing: IDX-12-CAM rev A |
| `k_sp` | 5 kN/m | assumption: Example return-spring rate; link the rate k of the spring sheet. |
| `m_f` | 0.2 kg | assumption: Example: follower arm, roller and tooling; take it from the CAD mass properties. |
| `mode` | Cam | spec: Indexing station IDX-12 §2 rev A |
| `n_in` | 120 rpm | spec: Indexing station IDX-12 §2: 120 index cycles/min rev A |
| `phi_max` | 30 deg | assumption: Usual limit for a translating roller follower; set it from your design rule. |
| `segs` | 4 row(s) | spec: Indexing station IDX-12 §2.3 motion table rev A |
| `steps` | 72 | assumption: 5° table step. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `lift` | 20 mm | 20 mm | PASS |
| `output_motion` | 0.24 m/s | 0.24 m/s | PASS |
| `peak_accel` | 4.52389 m/s^2 | 4.52389 m/s^2 | PASS |
| `j_pk` | 259.2 m/s^3 | 259.2 m/s^3 | PASS |
| `F_min` | 20 N | 20 N | PASS |
| `follower_force` | 120 N | 120 N | PASS |
| `input_torque` | 1.53449 N*m | 1.53449 N*m | PASS |
| `phi_max_pk` | 26.5958 deg | 26.5958 deg | PASS |
| `rho_min` | 44.6811 mm | 44.6811 mm | PASS |
| `Rb_min_phi` | 32.9632 mm | 32.9632 mm | PASS |

Governing check: Largest pressure angle against the allowed pressure angle — PASS (utilisation 0.89). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
