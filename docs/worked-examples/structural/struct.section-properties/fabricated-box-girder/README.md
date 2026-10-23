# Fabricated box girder with a hole, as a polygon (`struct.section-properties`)

**Source.** Analytic (outer 400 × 600 rectangle minus 360 × 560 opening), A = 384 cm², Ixx = (400·600³ − 360·560³)/12 = 193,152 cm⁴, Iyy = 102,272 cm⁴, Wel,x = 6,438.4 cm³, Wpl,x = (400·600² − 360·560²)/4 = 7,776 cm³ — Confirmed.

A welded box drawn as an outer loop and a hole loop. The polygon route gives the same answers as the closed form for a rectangle minus a rectangle; no torsion constant is reported for an arbitrary polygon (warning), so Bredt's formula on the hollow-rectangle shape is the route for J.

| Input | Value | Source |
|---|---|---|
| `shape` | polygon | drawing: DRG-BG-100 rev C |
| `vertices` | 8 row(s) | drawing: DRG-BG-100 rev C |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `A` | 384 cm^2 | 384 cm^2 | PASS |
| `I_xx` | 193152 cm^4 | 193152 cm^4 | PASS |
| `I_yy` | 102272 cm^4 | 102272 cm^4 | PASS |
| `W_pl_x` | 7776 cm^3 | 7776 cm^3 | PASS |
| `W_el_x` | 6438.4 cm^3 | 6438.4 cm^3 | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
