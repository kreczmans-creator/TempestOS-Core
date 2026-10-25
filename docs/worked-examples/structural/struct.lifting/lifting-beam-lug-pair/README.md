# Lifting beam, two lugs at 30° and 2° out of plane, 150 kN rated load, BTH-1 and EN 13155 modes with the 6° tilt (feature flag on in the test harness only) (`struct.lifting`)

**Source.** Analytic (the spec equations of struct.lifting, evaluated independently in python); the design factors are illustrative user-record values, not values from any standard, struct.lifting spec, θ_d = 36°, β_d = 8°, F_s = 75/cos 36° = 92.705 kN, F_op = 12.902 kN, N_beam = 54.491 kN, M_op = 2.3224 kN·m; C_r = 0.885436, φ = 50°; A_t = 2000, A_net = 2375, A_v = 2866.17, A_b = 1250 mm²; BTH-1 u = 0.4370, 0.3714, 0.4677, 0.5159 (bearing), EN 13155 u = 0.2959, 0.2492, 0.3576, 0.4734 (bearing, 3× route); capacity 290.74 kN; beam axial u = 0.0569; proof load 225 kN — Confirmed.

Two lugs on a horizontal lifting beam carry 150 kN at a sling angle of 30° from the vertical and 2° out of plane; the beam adds the automatic 6° tilt, so the sheet works at 36° and 8°. Bearing on the pin governs both the BTH-1 mode (utilisation 0.516, capacity 290.7 kN) and the EN 13155 mode (0.473, 316.9 kN, the 3×WLL no-release route). The design factors are illustrative user-record values: this example shows the arithmetic and the layout of the sheet and is not an approval of any lug. The calculator is behind the lifting feature flag (off by default); this example runs it directly, as the test harness does. To issue a sheet like this the competent-person attestation (name, qualification, statement) must be filled in by someone other than the author, and two different independent checkers must sign; the attestation inputs are left blank here.

| Input | Value | Source |
|---|---|---|
| `A_beam` | 80 cm^2 | drawing: LB-150-03 rev A |
| `Dh` | 55 mm | drawing: LB-150-02 rev A |
| `Dp` | 50 mm | drawing: LB-150-02 rev A |
| `Nd` | 1.8 | assumption: Illustrative user-record value for this worked example, not a value from any standard: replace with the design factor of your Design Category from your own copy of the code. |
| `R` | 75 mm | drawing: LB-150-02 rev A |
| `WLL` | 150 kN | spec: Lifting beam specification LB-150, rated load 150 kN |
| `be` | 40 mm | drawing: LB-150-02 rev A |
| `beam` | yes | drawing: LB-150-01 rev A |
| `beta` | 2 deg | assumption: Allowance for the sling not hanging exactly in the plane of the lug. |
| `dish_max` | 40 | assumption: Illustrative user-record value, not from a standard: replace with your own record. |
| `f_u` | 470 MPa | spec: S355 plate 25 mm, mill certificate MC-2291 (lower bound of the ultimate range) |
| `f_y` | 345 MPa | spec: S355 plate 25 mm, mill certificate MC-2291 (16 mm < t ≤ 40 mm) |
| `h` | 180 mm | drawing: LB-150-02 rev A |
| `k_b` | 1.2 | assumption: Illustrative user-record value, not from a standard: replace with your own record. |
| `k_proof` | 1.5 | assumption: Illustrative user-record value, not from a standard: replace with the proof-load factor of your procedure. |
| `mode` | both | assumption: Both modes are run so the sheet shows which governs; the owner's code route decides which counts. |
| `n_cycles` | 1000 | spec: Lifting beam specification LB-150: not more than 1000 lifts in its life |
| `n_lugs` | 2 | drawing: LB-150-01 rev A |
| `n_u` | 2.2 | assumption: Illustrative user-record value, not from a standard: replace with your own record. |
| `n_y` | 1.6 | assumption: Illustrative user-record value, not from a standard: replace with your own record. |
| `t` | 25 mm | drawing: LB-150-02 rev A |
| `theta` | 30 deg | drawing: LB-150-01 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F_s` | 92.7051 kN | 92.7051 kN | PASS |
| `F_op` | 12.9021 kN | 12.9021 kN | PASS |
| `N_beam` | 54.4907 kN | 54.4907 kN | PASS |
| `M_op` | 2.32237 kN*m | 2.32237 kN*m | PASS |
| `Cr` | 0.885436  | 0.885436  | PASS |
| `phi` | 50 deg | 50 deg | PASS |
| `A_v` | 2866.17 mm^2 | 2866.17 mm^2 | PASS |
| `u_t` | 0.437009  | 0.437009  | PASS |
| `u_sp` | 0.371433  | 0.371433  | PASS |
| `u_v` | 0.467666  | 0.467666  | PASS |
| `u_b` | 0.515924  | 0.515924  | PASS |
| `u_bth` | 0.515924  | 0.515924  | PASS |
| `cap_bth` | 290.74 kN | 290.74 kN | PASS |
| `u_en` | 0.473388  | 0.473388  | PASS |
| `cap_en` | 316.865 kN | 316.865 kN | PASS |
| `u_beam` | 0.0568599  | 0.0568599  | PASS |
| `capacity` | 290.74 kN | 290.74 kN | PASS |
| `util` | 0.515924  | 0.515924  | PASS |
| `proof_load` | 225 kN | 225 kN | PASS |

Governing check: BTH-1: bearing — PASS (utilisation 0.516). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
