# Static piston O-ring seal of a 63 mm locking cylinder: squeeze, fill, stretch and extrusion over tolerance, temperature and swell (`fp.oring-gland`)

**Source.** Analytic (stacks of the toleranced dimensions through the tolerance-stack engine; constant-volume reduction 1 − 1/√(1 + X); dimensions invented for this example, not taken from a size table), depth h = (D_s − D_g)/2 = 2.780 mm ± 0.030 with W 3.5 ± 0.1 mm; cold end (−25 °C) squeeze_min = 1 − h_max/W_eff,min = 15.125 %; hot end (70 °C, 4 % swell) squeeze_max = 25.225 %; fill 63.953 % to 83.783 % (hot end on the volume basis, ring area (π/4)·(f·W)²·(f/g)); largest stretch 63.0 … 57.5/55.55 − 1 = 3.5104 %; largest gap 63.046 − 62.94 = 0.106 mm as dimensioned, 0.10606 mm at 70 °C — Confirmed.

The groove leaves a gland depth of 2.780 mm ± 0.030 for a 3.5 ± 0.1 mm ring. Over the −25 to +70 °C range with up to 4 % swell the squeeze runs from 15.1 % (cold, the ring shrunk and stretched most, so its section is 2.0 % thinner) to 25.2 % (hot and swollen, the ring almost unstretched), against a 15 to 30 % band: the minimum has 0.1 points to spare. The gland fill is 64.0 to 83.8 % inside 60 to 85 % (the hot figure is on a volume basis: the held ring takes the whole thermal and swell volume change in its section). The ring is stretched at most 3.51 % when installed and 4.16 % at the cold end, inside 6 %, and the largest extrusion gap over the temperature range is 0.1061 mm against an illustrative 0.20 mm. The Monte Carlo finds no sample outside any limit in 20 000 (seed 42); the capability index of the minimum squeeze is 1.56 and of the maximum fill 1.65, which shows the thin margin on the squeeze minimum even though the worst case passes. The Monte Carlo runs the exact non-linear model. The ring size, the bands and the extrusion limit are illustrative records, not a size table or a handbook chart.

| Input | Value | Source |
|---|---|---|
| `D_c` | 63 mm | drawing: LC-63-002 rev B |
| `D_c_l` | -0.06 mm | drawing: LC-63-002 rev B |
| `D_c_u` | -0.03 mm | drawing: LC-63-002 rev B |
| `D_g` | 57.5 mm | drawing: LC-63-002 rev B |
| `D_g_l` | -0.074 mm | drawing: LC-63-002 rev B |
| `D_g_u` | 0 mm | drawing: LC-63-002 rev B |
| `D_s` | 63 mm | drawing: LC-63-001 rev B |
| `D_s_l` | 0 mm | drawing: LC-63-001 rev B |
| `D_s_u` | 0.046 mm | drawing: LC-63-001 rev B |
| `H_A` | 70 | spec: Seal specification: compound 70 Shore A |
| `T_max` | 70 degC | spec: Hydraulic specification HS-63: oil temperature range −25 to +70 °C |
| `T_min` | -25 degC | spec: Hydraulic specification HS-63: oil temperature range −25 to +70 °C |
| `W` | 3.5 mm | reference: record: oring.ring, illustrative cross-section invented for this example |
| `W_tol` | 0.1 mm | reference: record: oring.ring, illustrative tolerance invented for this example |
| `alpha` | 0.00015 1/K | assumption: Indicative elastomer figure; replace by the compound's value or a material from the library. |
| `alpha_m` | 0.000012 1/K | assumption: Steel groove metal, indicative. |
| `b` | 4.7 mm | drawing: LC-63-002 rev B |
| `b_l` | 0 mm | drawing: LC-63-002 rev B |
| `b_u` | 0.1 mm | drawing: LC-63-002 rev B |
| `d_r` | 56 mm | reference: record: oring.ring, illustrative ring size invented for this example, to be replaced by the supplier's catalogue size |
| `d_r_tol` | 0.45 mm | reference: record: oring.ring, illustrative tolerance invented for this example |
| `dist` | Normal | assumption: Normal distribution of the machined dimensions. |
| `duty` | Static | spec: Cylinder datasheet LC-63: locking duty, no stroke under pressure |
| `fill_hi` | 85 % | reference: record: oring.criteria, gland fill band (default 60 to 85 %) |
| `fill_lo` | 60 % | reference: record: oring.criteria, gland fill band (default 60 to 85 %) |
| `gap_allow` | 0.2 mm | reference: record: oring.extrusion, illustrative permitted gap invented for this example, to be replaced by the seal maker's value for 70 Shore A at 160 bar |
| `k_sigma` | 3 | assumption: Normal tolerances taken as ±3σ. |
| `n_mc` | 20000 | assumption: 20 000 samples. |
| `p_op` | 160 bar | spec: Hydraulic specification HS-63: working pressure 160 bar |
| `q_hi_s` | 30 % | reference: record: oring.criteria, static squeeze band, to be taken from the owner's handbook or the seal maker |
| `q_lo_s` | 15 % | reference: record: oring.criteria, static squeeze band, to be taken from the owner's handbook or the seal maker |
| `r_g` | 0.25 mm | drawing: LC-63-002 rev B |
| `red` | Incompressible rubber (derived) | assumption: Constant-volume rubber relation, derived. |
| `s_max` | 4 % | assumption: Volume swell of the compound in the oil, assumed 4 %; replace by the compound's immersion data. |
| `s_min` | 0 % | assumption: No shrinkage assumed in the mineral oil. |
| `seed` | 42 | assumption: Fixed seed for a repeatable sheet. |
| `type` | Piston (radial) | spec: Cylinder datasheet LC-63: ring on the piston, sealing on the bore |
| `x_hi` | 6 % | reference: record: oring.criteria, installed stretch limit (up to 6 % on a piston groove, as quoted second-hand) |
| `x_lo` | 0 % | reference: record: oring.criteria, installed stretch lower limit |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `squeeze_min` | 15.125 % | 15.125 % | PASS |
| `squeeze_max` | 25.2246 % | 25.2246 % | PASS |
| `squeeze_nom` | 19.5406 % | 19.5406 % | PASS |
| `fill_max` | 83.7826 % | 83.7826 % | PASS |
| `fill_min` | 63.9529 % | 63.9529 % | PASS |
| `stretch` | 3.51035 % | 3.51035 % | PASS |
| `gap_max` | 0.106064 mm | 0.106064 mm | PASS |
| `p_sq_min` | 0 % | 0 % | PASS |
| `cpk_sq_min` | 1.56122  | 1.56122  | PASS |
| `cpk_fill_max` | 1.648  | 1.648  | PASS |

Governing check: Squeeze minimum within the band — PASS (utilisation 0.000). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
