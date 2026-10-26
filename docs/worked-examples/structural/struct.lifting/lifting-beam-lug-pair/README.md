# Lifting beam, two lugs at 30° and 2° out of plane, 150 kN rated load, BTH-1 and EN 13155 modes with the automatic 6° tilt cases (feature flag on in the test harness only) (`struct.lifting`)

**Source.** Analytic (the spec equations of struct.lifting 1.1.0, evaluated independently in python, s8_ref2); the design factors are illustrative user-record values, not values from any standard, struct.lifting spec 1.1.0, F_v = 75 kN; nominal F_s = 75/(cos 30° cos 2°) = 86.655 kN, roll (β + 6° = 8°) 87.454 kN, pitch (slings at 36° and 24°, 2·F_v·sin 36°/sin 60°/cos 2°) 101.869 kN governs; the force along the beam is 43.30 kN untilted (F_v·tan 30°) and F_v·sin 36°/cos 30° = 50.904 kN pitched, so the envelope N_beam is the pitched 50.904 kN; bearing governs both modes; capacities, utilisations and the beam axial utilisation as listed — Confirmed.

Two lugs on a horizontal lifting beam carry 150 kN at a sling angle of 30° from the vertical and 2° out of plane; the beam adds the automatic 6° tilt cases: the pitch case (slings at 36° and 24°, the load redistributed between the lugs) governs, with a sling force of 101.9 kN against 86.7 kN untilted. Bearing on the pin governs both the BTH-1 and the EN 13155 modes. The design factors are illustrative user-record values: this example shows the arithmetic and the layout of the sheet and is not an approval of any lug. The calculator is behind the lifting feature flag (off by default); this example runs it directly, as the test harness does. To issue a sheet like this the competent-person attestation (name, qualification, statement) must be filled in by someone other than the author, and two different independent checkers must sign; the attestation inputs are left blank here.

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
| `F_s` | 101.869 kN | 101.869 kN | PASS |
| `F_ip` | 101.807 kN | 101.807 kN | PASS |
| `F_op` | 12.1712 kN | 12.1712 kN | PASS |
| `N_beam` | 50.9037 kN | 50.9037 kN | PASS |
| `M_op` | 2.19081 kN*m | 2.19081 kN*m | PASS |
| `Cr` | 0.885436  | 0.885436  | PASS |
| `phi` | 50 deg | 50 deg | PASS |
| `be_eff` | 38.3391 mm | 38.3391 mm | PASS |
| `A_t` | 1916.95 mm^2 | 1916.95 mm^2 | PASS |
| `A_fr` | 1874.51 mm^2 | 1874.51 mm^2 | PASS |
| `A_v` | 2821.52 mm^2 | 2821.52 mm^2 | PASS |
| `A_b` | 1250 mm^2 | 1250 mm^2 | PASS |
| `Fa` | 213.636 MPa | 213.636 MPa | PASS |
| `D_bth` | 183.365 kN | 183.365 kN | PASS |
| `P_t` | 362.613 kN | 362.613 kN | PASS |
| `P_sp` | 354.584 kN | 354.584 kN | PASS |
| `P_v` | 348.014 kN | 348.014 kN | PASS |
| `P_b` | 320.455 kN | 320.455 kN | PASS |
| `u_t` | 0.505677  | 0.505677  | PASS |
| `u_sp` | 0.517127  | 0.517127  | PASS |
| `u_v` | 0.526889  | 0.526889  | PASS |
| `u_b` | 0.572203  | 0.572203  | PASS |
| `u_bth` | 0.572203  | 0.572203  | PASS |
| `cap_bth` | 262.145 kN | 262.145 kN | PASS |
| `P_en_t` | 300.323 kN | 300.323 kN | PASS |
| `u_en_t` | 0.3392  | 0.3392  | PASS |
| `P_en_sp` | 293.673 kN | 293.673 kN | PASS |
| `u_en_sp` | 0.346881  | 0.346881  | PASS |
| `P_en_v` | 255.21 kN | 255.21 kN | PASS |
| `u_en_v` | 0.399159  | 0.399159  | PASS |
| `P_en_b` | 195.833 kN | 195.833 kN | PASS |
| `u_en_b` | 0.520184  | 0.520184  | PASS |
| `u_en` | 0.520184  | 0.520184  | PASS |
| `cap_en` | 288.359 kN | 288.359 kN | PASS |
| `u_beam` | 0.0536113  | 0.0536113  | PASS |
| `lug_util` | 0.572203  | 0.572203  | PASS |
| `capacity` | 262.145 kN | 262.145 kN | PASS |
| `util` | 0.572203  | 0.572203  | PASS |
| `proof_load` | 225 kN | 225 kN | PASS |

Governing check: BTH-1: bearing — PASS (utilisation 0.57). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
