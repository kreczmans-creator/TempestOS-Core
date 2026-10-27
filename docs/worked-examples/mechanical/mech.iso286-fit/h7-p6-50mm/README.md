# 50 mm H7/p6 interference (press) fit (`mech.iso286-fit`)

**Source.** ISO 286-2:2010 (public excerpt), 50 mm H7: +25/0 µm; p6: +42/+26 µm — Unconfirmed.

An interference fit: the formula value p = IT7 = 25.0 µm sits 1 µm under the tabulated +26 µm, so the minimum interference reads 0 rather than 1 µm — the documented rounding difference; enter overrides from a released reference record where the table governs.

| Input | Value | Source |
|---|---|---|
| `D` | 50 mm | drawing: DRG-HUB-04 rev C |
| `hole_grade` | 7 | spec: DRG-HUB-04: H7/p6 |
| `hole_letter` | H | spec: DRG-HUB-04: H7/p6 |
| `shaft_grade` | 6 | spec: DRG-HUB-04: H7/p6 |
| `shaft_letter` | p | spec: DRG-HUB-04: H7/p6 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `IT_hole` | 25 µm | 24.9799 µm | PASS |
| `i_max` | 42 µm | 40.5923 µm | PASS |

Governing check: Fit classes against the fit kind — PASS. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
