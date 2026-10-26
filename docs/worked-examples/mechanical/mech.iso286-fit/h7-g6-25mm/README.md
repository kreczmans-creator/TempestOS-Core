# 25 mm H7/g6 sliding fit (`mech.iso286-fit`)

**Source.** Machinery's Handbook, 30th ed., Preferred metric fits, 25 mm H7/g6: hole +0.021/0, shaft −0.007/−0.020, clearance 0.007…0.041 mm — Unconfirmed.

A close-running fit from the formulae: IT7 = 20.9 µm, IT6 = 13.1 µm, g = −7.3 µm; clearance 7.3–41.3 µm against the tabulated 7–41 µm.

| Input | Value | Source |
|---|---|---|
| `D` | 25 mm | drawing: DRG-SHAFT-12 rev A |
| `hole_grade` | 7 | spec: DRG-SHAFT-12 note 3 |
| `hole_letter` | H | spec: DRG-SHAFT-12 note 3: H7/g6 |
| `shaft_grade` | 6 | spec: DRG-SHAFT-12 note 3 |
| `shaft_letter` | g | spec: DRG-SHAFT-12 note 3 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `IT_hole` | 21 µm | 20.918 µm | PASS |
| `es` | -7 µm | -7.28529 µm | PASS |
| `c_min` | 7 µm | 7.28529 µm | PASS |
| `c_max` | 41 µm | 41.277 µm | PASS |

Governing check: Fit classified — PASS. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
