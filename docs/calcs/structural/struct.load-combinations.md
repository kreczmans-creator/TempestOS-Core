# S5 — Load combinations, EN 1990 Annex A1 (`struct.load-combinations@1.0.0`)

**In plain terms.** Real structures carry a permanent load (their own weight) and several variable loads (people, wind, snow), and the code asks for them to be combined with safety factors in several ways, each variable load taking its turn as the leading one. The worksheet takes each load's characteristic value and category, builds every combination Eurocode 0 asks for (strength, serviceability and accidental), and reports the largest and smallest design value and the combination that governs.

**Scope.** Design values of one action effect for buildings under every EN 1990 combination: ULS 6.10 or 6.10a/6.10b, SLS 6.14b/6.15b/6.16b, accidental 6.11b. Each variable action is tried as leading; maximum and minimum envelopes with the governing combination are output.

**Method.** Each combination is shown as formula → substitution → result (`C1…` for the maximum, `D1…` for the minimum). A permanent action takes γ_G,sup (×ξ in 6.10b) when it adds to the effect sought and γ_G,inf when it opposes it; a variable action enters only when it adds (EN 1990 cl. 6.4.3.2, 6.4.3.3, 6.5.3; Annex A1 Table A1.2(B)). K_FI multiplies the partial factors of unfavourable actions (γ_G,sup, ξ·γ_G,sup and γ_Q; EN 1990 Annex B, B3.3), never γ_G,inf.

**Inputs.** `actions` table (name, category, signed F_k; ≤ 30 rows); `unit` of the effect (kN, kN/m, kN/m², kN·m, kN·m/m); `method`; `gamma_G_sup`, `gamma_G_inf`, `gamma_Q`, `xi`, `K_FI` 0.9–1.1; optional `psi` table (category, ψ₀, ψ₁, ψ₂). Factors and ψ come from an `en1990.factors` record (UK NA NA.A1.2(A)–(C), or the Annex A1 recommended set, labelled). Without a ψ table the recommended Table A1.1 set is used and stated on the sheet.

**Outputs.** `E_d_max/min/gov`, `E_k_*`, `E_fr_*`, `E_qp_*`, and `E_acc_*` when an accidental action is entered. **Ports.** `E_d_max`, `E_d_min`, `E_k_max`, `E_qp_max` (unit of the actions) → S3, S4, S6. **To S2.** `LoadCombinationsCalculator.FrameCombinations(inputs)` gives the ULS and SLS-characteristic combinations as S2 `combinations` rows (action row n = S2 load case n, names C1…/D1… as on this sheet, at most 50); S2 then envelopes them.

**Limitations.** One scalar effect by superposition; buildings only; no EQU set B/C split, bridges, fatigue or seismic.

**Test cases.** `ka-610-single` (21 kN), `ka-610ab-single` (18.975 kN), `ka-610-two-variables` (24.75 kN, wind leading), `ka-uplift` (−20 kN) — analytic, Confirmed; `ka-designers-guide` (37.65 kN/m) — pattern after Gulvanessian, Calgaro & Holický (2002), **Unconfirmed** until checked against the book. Worked example `office-floor-beam-uk-na` (37.48 kN/m) — UK NA factors, **Unconfirmed** until checked against the NA.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Office floor beam line load, UK National Annex 6.10a/6.10b ([sheet](../../worked-examples/structural/struct.load-combinations/office-floor-beam-uk-na/sheet.txt)).

```text
C1 = 1.35×18 + 1.05×10 = 34.8 kN/m
C2 = 1.2488×18 + 1.5×10 = 37.478 kN/m
C3 = 1×18 + 1×10 = 28 kN/m
C4 = 1×18 + 0.5×10 = 23 kN/m
C5 = 1×18 + 0.3×10 = 21 kN/m
D1 = 1×18 = 18 kN/m
D2 = 1×18 = 18 kN/m
D3 = 1×18 = 18 kN/m
D4 = 1×18 = 18 kN/m
D5 = 1×18 = 18 kN/m
E_d_max = ULS 6.10b, Office imposed leading = 37.478 kN/m
E_d_min = ULS 6.10a = 18 kN/m
E_k_max = SLS characteristic 6.14b, Office imposed leading = 28 kN/m
E_k_min = SLS characteristic 6.14b, no variable action = 18 kN/m
… 4 more step(s) on the sheet
```
<!-- hand-arithmetic:end -->
