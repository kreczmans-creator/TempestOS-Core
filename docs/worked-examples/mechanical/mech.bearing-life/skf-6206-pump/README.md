# Pump shaft bearing, SKF 6206 at 1500 rpm, 95 % reliability (`mech.bearing-life`)

**Source.** SKF Rolling bearings catalogue (PUB BU/P1 17000), 2018, 6206 ratings C = 20.3 kN, C_0 = 11.2 kN (distributor listing; check against SKF's own product page) — Unconfirmed.

A 6206 deep groove ball bearing carries 2.5 kN radial at 1500 rpm. The basic life is 5949 h; at 95 % reliability with a_ISO = 2 the modified life is 7590 h, above the 5000 h required (utilisation 0.659). The ratings and factors would normally come from reference records; here they cite the catalogue directly.

| Input | Value | Source |
|---|---|---|
| `C` | 20.3 kN | spec: SKF catalogue, 6206 |
| `C_0` | 11.2 kN | spec: SKF catalogue, 6206 |
| `F_a` | 0 kN | assumption: Locating bearing elsewhere; no axial load here. |
| `F_r` | 2.5 kN | assumption: Radial reaction from the shaft calculation. |
| `L_req` | 5000 h | spec: Pump specification, minimum bearing life |
| `R` | 95 % | assumption: Client reliability target. |
| `X` | 0.56 | spec: Manufacturer factor for single-row deep groove bearings |
| `X_0` | 0.6 | spec: Manufacturer static factor |
| `Y` | 2 | spec: Manufacturer factor for single-row deep groove bearings |
| `Y_0` | 0.5 | spec: Manufacturer static factor |
| `a_iso` | 2 | assumption: Read from the manufacturer's life tool for clean ISO VG 68 oil. |
| `e_lim` | 0.22 | spec: Manufacturer factor for single-row deep groove bearings |
| `n` | 1500 rpm | spec: Pump datasheet, rated speed |
| `s_0_req` | 1 | assumption: Normal running, smooth loads. |
| `type` | ball (p = 3) | assumption: Deep groove ball bearing. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `L_10` | 535.387  | 535.387  | PASS |
| `L_10h` | 5948.75 h | 5948.75 h | PASS |
| `a_1` | 0.637912  | 0.637912  | PASS |
| `L_nmh` | 7589.6 h | 7589.55 h | PASS |
| `s_0` | 4.48  | 4.48  | PASS |

Governing check: Modified rating life against required life — PASS (utilisation 0.659). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
