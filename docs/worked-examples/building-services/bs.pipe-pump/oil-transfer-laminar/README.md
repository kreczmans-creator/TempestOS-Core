# Lube-oil transfer line, laminar flow (`bs.pipe-pump`)

**Source.** Çengel & Cimbala, Fluid Mechanics, 4th ed., Ch. 8 laminar pipe flow: Δp = 32 μ L v / D² — Confirmed.

Viscous oil at Re ≈ 8: the sheet flags laminar flow, uses f = 64/Re and reproduces Hagen–Poiseuille (Δp = 32 μ L v / D² = 57.9 kPa) exactly. Roughness plays no part in laminar flow.

| Input | Value | Source |
|---|---|---|
| `D_law` | impeller trim (Q ∝ n·D, H ∝ n²·D²) | assumption: The impeller is unchanged (D_ratio = 1), so the law has no effect; template default kept. |
| `D_ratio` | 1 | assumption: Impeller unchanged. |
| `Q` | 1 m^3/h | spec: Transfer pump duty |
| `T_fluid` | 20 degC | assumption: Ambient 20 °C. |
| `circuit` | open (the pump lifts the water by the rises entered) | assumption: Open transfer line to an open discharge. |
| `eta_pump` | 0.7 | assumption: Illustrative pump efficiency for the shaft power; take it from the pump datasheet. |
| `fluid` | custom (ρ and μ given) | spec: Oil datasheet ISO VG 680 at 20 °C |
| `h_out` | 0 m | assumption: Open discharge. |
| `mu_custom` | 800 mPa*s | spec: Oil datasheet ISO VG 680 at 20 °C |
| `n_ratio` | 1 | assumption: No speed change. |
| `p_atm` | 101.325 kPa | assumption: Sea level. |
| `rho_custom` | 888 kg/m^3 | spec: Oil datasheet ISO VG 680 |
| `segments` | 1 row(s) | drawing: DRG-L-020 rev A |
| `v_limit` | 3 m/s | assumption: Practice limit. |
| `z_s` | 0 m | assumption: Pump suction level with the tank outlet. |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `Re_s1` | 7.852  | 7.85164  | yes |
| `dp_total` | 57.946 kPa | 57.9465 kPa | yes |

Governing check: Highest velocity against limit — PASS (utilisation 0.05). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
