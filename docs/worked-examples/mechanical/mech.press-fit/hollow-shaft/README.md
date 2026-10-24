# Hollow 50 mm shaft (25 mm bore) in a 100 mm hub (`mech.press-fit`)

**Source.** Analytic (Lamé), p = 49.6 MPa; shaft surface hoop −82.7 MPa — Confirmed.

The bore makes the shaft more compliant, so the same interference gives 50 MPa instead of 62 MPa and the shaft surface sees −83 MPa hoop compression.

| Input | Value | Source |
|---|---|---|
| `E_h` | 207 GPa | spec: steel |
| `E_s` | 207 GPa | spec: steel |
| `L` | 60 mm | drawing: DRG-HUB-04 rev C |
| `S_y_h` | 355 MPa | spec: EN 10025 S355 |
| `S_y_s` | 355 MPa | spec: EN 10025 S355 |
| `alpha_h` | 0.000012 1/K | spec: steel |
| `c_a` | 20 µm | assumption: Assembly clearance. |
| `d` | 50 mm | drawing: DRG-HUB-04 rev C |
| `d_i` | 25 mm | drawing: DRG-SHAFT-13 rev A |
| `d_o` | 100 mm | drawing: DRG-HUB-04 rev C |
| `delta` | 40 µm | drawing: DRG-HUB-04 rev C |
| `mu` | 0.15 | assumption: Dry steel on steel. |
| `nu_h` | 0.292 | spec: steel |
| `nu_s` | 0.292 | spec: steel |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `p` | 49.63 MPa | 49.68 MPa | PASS |
| `sigma_t_shaft` | -82.7 MPa | -82.8 MPa | PASS |

Governing check: Hub yield — PASS (utilisation 0.327). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
