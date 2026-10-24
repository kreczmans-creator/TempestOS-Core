# UB 457×191×98 section properties (Blue Book) (`struct.section-properties`)

**Source.** SCI / Steel for Life Blue Book (P363 data), EC3 UK NA, UB 457×191×98 dimensions and properties — Confirmed.

A rolled universal beam with its root fillets. Every tabulated property is reproduced within 0.5 % (torsion and warping within 1 %) from the dimensions alone, so the sheet can stand in for the table when a section is not listed.

| Input | Value | Source |
|---|---|---|
| `b` | 192.8 mm | spec: BS EN 10365 UB 457×191×98 |
| `h` | 467.2 mm | spec: BS EN 10365 UB 457×191×98 |
| `r` | 10.2 mm | spec: BS EN 10365 UB 457×191×98 |
| `shape` | I-section | spec: BS EN 10365 UB 457×191×98 |
| `t` | 11.4 mm | spec: BS EN 10365 UB 457×191×98 |
| `t_f` | 19.6 mm | spec: BS EN 10365 UB 457×191×98 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `A` | 125 cm^2 | 125.265 cm^2 | PASS |
| `I_xx` | 45730 cm^4 | 45728 cm^4 | PASS |
| `W_pl_x` | 2232 cm^3 | 2232.46 cm^3 | PASS |
| `J` | 121 cm^4 | 121.287 cm^4 | PASS |
| `I_w` | 1.18 dm^6 | 1.17554 dm^6 | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
