# Lube-oil transfer line, laminar flow (`bs.pipe-pump`)

**Source.** Çengel & Cimbala, Fluid Mechanics, 4th ed., Ch. 8 laminar pipe flow: Δp = 32 μ L v / D² — Confirmed.

Viscous oil at Re ≈ 8: the sheet flags laminar flow, uses f = 64/Re and reproduces Hagen–Poiseuille (Δp = 32 μ L v / D² = 57.9 kPa) exactly. Roughness plays no part in laminar flow.

| Input | Value | Source |
|---|---|---|
| `Q` | 1 m^3/h | spec: Transfer pump duty |
| `T` | 20 degC | assumption: Ambient 20 °C. |
| `fluid` | custom (ρ and μ given) | spec: Oil datasheet ISO VG 680 at 20 °C |
| `h_out` | 0 m | assumption: Open discharge. |
| `mu_custom` | 800 mPa*s | spec: Oil datasheet ISO VG 680 at 20 °C |
| `rho_custom` | 888 kg/m^3 | spec: Oil datasheet ISO VG 680 |
| `segments` | 1 row(s) | drawing: DRG-L-020 rev A |
| `v_max` | 3 m/s | assumption: Practice limit. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `Re_s1` | 7.852  | 7.85164  | PASS |
| `dp_total` | 57.946 kPa | 57.9465 kPa | PASS |

Governing check: Velocity ≤ limit — PASS (utilisation 0.05). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
