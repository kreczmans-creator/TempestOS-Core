# Office floor beam line load, UK National Annex 6.10a/6.10b (`struct.load-combinations`)

**Source.** UK NA to BS EN 1990:2002+A1:2005, incorporating Amendment No. 1, NA.A1.2(B): γ_G,sup 1.35, γ_G,inf 1.0, γ_Q 1.5, ξ 0.925; ψ for category B from NA.A1.1 — Unconfirmed.

Permanent 18 kN/m and office imposed 10 kN/m. 6.10a gives 1.35×18 + 1.5×0.7×10 = 34.8 kN/m; 6.10b gives 0.925×1.35×18 + 1.5×10 = 37.48 kN/m, which governs. The ψ factors are the recommended set (the same as the UK NA for category B). SLS characteristic 28 kN/m, quasi-permanent 21 kN/m.

| Input | Value | Source |
|---|---|---|
| `K_FI` | 1 | assumption: Consequence class CC2. |
| `actions` | 2 row(s) | drawing: GA-L2-001 rev C |
| `gamma_G_inf` | 1 | spec: UK NA Table NA.A1.2(B) |
| `gamma_G_sup` | 1.35 | spec: UK NA Table NA.A1.2(B) |
| `gamma_Q` | 1.5 | spec: UK NA Table NA.A1.2(B) |
| `method` | 6.10a and 6.10b | spec: UK NA Table NA.A1.2(B): the less favourable of 6.10a and 6.10b |
| `unit` | kN/m | assumption: Line load on the beam. |
| `xi` | 0.925 | spec: UK NA Table NA.A1.2(B) |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `E_d_max` | 37.4775 kN/m | 37.4775 kN/m | PASS |
| `E_k_max` | 28 kN/m | 28 kN/m | PASS |
| `E_qp_max` | 21 kN/m | 21 kN/m | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
