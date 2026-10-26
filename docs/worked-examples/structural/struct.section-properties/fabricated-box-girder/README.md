# Fabricated box girder with a hole, as a polygon (`struct.section-properties`)

**Source.** Analytic (outer 400 × 600 rectangle minus 360 × 560 opening), A = 384 cm², I_y = (400·600³ − 360·560³)/12 = 193,152 cm⁴, I_z = 102,272 cm⁴, Wel,y = 6,438.4 cm³, Wpl,y = (400·600² − 360·560²)/4 = 7,776 cm³ — Confirmed.

A welded box drawn as an outer loop and a hole loop. The polygon route gives the same answers as the closed form for a rectangle minus a rectangle; no torsion constant is reported for an arbitrary polygon (warning), so Bredt's formula on the hollow-rectangle shape is the route for J.

| Input | Value | Source |
|---|---|---|
| `shape` | polygon | drawing: DRG-BG-100 rev C |
| `vertices` | 8 row(s) | drawing: DRG-BG-100 rev C |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `A` | 384 cm^2 | 384 cm^2 | PASS |
| `I_y` | 193152 cm^4 | 193152 cm^4 | PASS |
| `I_z` | 102272 cm^4 | 102272 cm^4 | PASS |
| `W_pl_y` | 7776 cm^3 | 7776 cm^3 | PASS |
| `W_el_y` | 6438.4 cm^3 | 6438.4 cm^3 | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
