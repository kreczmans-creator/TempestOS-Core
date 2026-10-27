# Load combinations (EN 1990) (`struct.load-combinations@1.3.0`)

**In plain terms.** Real structures carry a permanent load (their own weight) and several variable loads (people, wind, snow), and the code asks for them to be combined with safety factors in several ways, each variable load taking its turn as the leading one. The worksheet takes each load's characteristic value and category, builds every combination Eurocode 0 asks for (strength, serviceability and accidental), and reports the largest and smallest design value and the combination that governs.

## Scope

Calculates the design values of one action effect under every EN 1990 Annex A1 combination for buildings, from persistent to accidental. Each variable action leads in turn; the envelopes and governing combinations are reported.

## Method

- ULS 6.10: Σ γ_G·G + γ_Q·K_FI·Q_1 + Σ γ_Q·K_FI·ψ_0·Q_i (K_FI also on γ_G,sup)
- ULS 6.10a: Σ γ_G·G + Σ γ_Q·K_FI·ψ_0·Q_i; 6.10b: Σ ξ·γ_G·G + γ_Q·K_FI·Q_1 + Σ γ_Q·K_FI·ψ_0·Q_i
- Permanent factor: γ_G,sup when the action adds to the effect sought, γ_G,inf when it opposes it
- Variable actions: included only when they add to the effect sought (otherwise zero)
- SLS characteristic 6.14b: Σ G + Q_1 + Σ ψ_0·Q_i; frequent 6.15b: Σ G + ψ_1·Q_1 + Σ ψ_2·Q_i; quasi-permanent 6.16b: Σ G + Σ ψ_2·Q_i
- Accidental 6.11b: Σ G + A_d + ψ_1·Q_1 + Σ ψ_2·Q_i

Each combination is shown as formula → substitution → result (`C1…` for the maximum, `D1…` for the minimum). A permanent action takes γ_G,sup (×ξ in 6.10b) when it adds to the effect sought and γ_G,inf when it opposes it; a variable action enters only when it adds (EN 1990 cl. 6.4.3.2, 6.4.3.3, 6.5.3; Annex A1 Table A1.2(B)). K_FI multiplies the partial factors of unfavourable actions (γ_G,sup, ξ·γ_G,sup and γ_Q; EN 1990 Annex B, B3.3), never γ_G,inf.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `actions` | Actions (one effect, signed) | — | — | table | Setup | — |
| `unit` | Unit of the effect | — | kN | kN, kN/m, kN/m^2, kN*m, kN*m/m | Setup | — |
| `method` | ULS expression | — | 6.10 | 6.10, 6.10a and 6.10b | Setup | — |
| `factor_set` | Factor set: the EN 1990 recommended values as entered, or a National Annex record applied to the partial factors and the ψ table | — | EN recommended | EN recommended, National Annex record | Setup | — |
| `gamma_G_sup` | γ_G,sup (unfavourable permanent) | — | 1.35 | 1 to 2 | Factors | — |
| `gamma_G_inf` | γ_G,inf (favourable permanent) | — | 1 | 0.8 to 1.2 | Factors | — |
| `gamma_Q` | γ_Q (variable) | — | 1.5 | 1 to 2 | Factors | — |
| `xi` | ξ reduction for 6.10b | — | 0.85 | 0.8 to 1 | Factors | — |
| `K_FI` | Consequence-class factor | — | 1 | 0.9 to 1.1 | Factors | — |
| `psi` | ψ factors by category (replaces the recommended set) | — | blank | table | Factors | — |

Columns of `actions`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `name` | Action | — | yes |
| `category` | Category (G permanent, A domestic, B office, C congregation, D shopping, E storage, F traffic ≤ 30 kN, G traffic 30–160 kN, H roofs, snow ≤ 1000 m, snow > 1000 m, wind, thermal, accidental) | — | yes |
| `F_k` | Characteristic value (signed, in the effect unit) | — | yes |

Columns of `psi`:

| Column | Description | Unit | Required |
|---|---|---|---|
| `category` | Category (A domestic, B office, C congregation, D shopping, E storage, F traffic ≤ 30 kN, G traffic 30–160 kN, H roofs, snow ≤ 1000 m, snow > 1000 m, wind, thermal) | — | yes |
| `psi_0` | ψ₀ | — | yes |
| `psi_1` | ψ₁ | — | yes |
| `psi_2` | ψ₂ | — | yes |

## Assumptions

1. Each action's characteristic value is its signed contribution to one scalar effect. Basis: Linear superposition (EN 1990 cl. 6.3.1 representative values).
2. The combinations are those of EN 1990 Annex A1 for buildings. Basis: EN 1990 Annex A1 scope.
3. Bridge, fatigue and seismic combinations are not included. Basis: EN 1990 Annex A1 scope.
4. Without a ψ record the recommended values of EN 1990 Annex A1 are used and labelled on the sheet. Basis: No National Annex table is shipped.
5. UK National Annex values of ψ and γ come from a user record. Basis: No National Annex table is shipped.
6. The factor set says whether the factors are the EN recommended values or come from a National Annex record. Basis: No National Annex value is shipped; a record carries its source, edition and table or clause, and the sheet prints them.
6. Each permanent action takes γ_G,sup when it adds to the effect sought and γ_G,inf when it opposes it. Basis: EN 1990 Table A1.2(B) note on favourable and unfavourable permanent actions.
7. For an equilibrium check with one source, the source's permanent actions are entered as one row. Basis: EN 1990 Table A1.2(B) note on favourable and unfavourable permanent actions.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `E_d_max` | ULS design effect, maximum | kN |
| `E_d_min` | ULS design effect, minimum | kN |
| `E_d_gov` | ULS design effect, governing (max) | — |
| `E_k_max` | SLS characteristic effect, maximum | kN |
| `E_k_min` | SLS characteristic effect, minimum | kN |
| `E_k_gov` | SLS characteristic effect, governing (max) | — |
| `E_fr_max` | SLS frequent effect, maximum | kN |
| `E_fr_min` | SLS frequent effect, minimum | kN |
| `E_fr_gov` | SLS frequent effect, governing (max) | — |
| `E_qp_max` | SLS quasi-permanent effect, maximum | kN |
| `E_qp_min` | SLS quasi-permanent effect, minimum | kN |
| `E_qp_gov` | SLS quasi-permanent effect, governing (max) | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `combination.effect_uls_max` | `E_d_max` | ULS design effect, maximum (unit of the actions) | — | — |
| `combination.effect_uls_min` | `E_d_min` | ULS design effect, minimum | — | — |
| `combination.effect_sls_characteristic_max` | `E_k_max` | SLS characteristic effect, maximum | — | — |
| `combination.effect_sls_quasi_permanent_max` | `E_qp_max` | SLS quasi-permanent effect, maximum | — | — |

## Checks

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `no-actions` | Block | Conservative | No actions: nothing to combine. |
| `psi-missing` | Block | Conservative | A variable action's category has no ψ factors in the ψ record. |
| `no-permanent` | Warning | Conservative | No permanent action: check the combinations are what you intend. |
| `factor-set` | Block | Conservative | With the National Annex record set chosen, every partial factor and the ψ table must carry the pin of an applied record. |

## Limitations

One scalar effect by superposition; buildings only; no EQU set B/C split, bridges, fatigue or seismic.

**Factor set.** `factor_set` names where the partial and ψ factors come from. `EN recommended` (the default, and what a calculation saved before 1.3.0 reads) is the sheet as it was: the factors as entered, whose defaults are the EN 1990 Annex A1 recommended values. `National Annex record` takes `gamma_G_sup`, `gamma_G_inf`, `gamma_Q`, `xi` and the `psi` table from an `en1990.factors` record applied to the calculation (C-3 record template 15: the four partial factors on the first row, the ψ table in the category rows, with the National Annex, its edition and its tables as the record's source); its values are licensed and are never shipped or typed into the sheet. With that set chosen and no record applied to every one of the five inputs the sheet refuses in one sentence naming `factor_set` (rule `factor-set`). The sheet prints the set name as the text result `factor_set_used`; for a record it adds the record's table or clause and edition, lists each input's record citation in the Inputs section, and states the set under Assumptions.

### Changes by version

**1.3.0 (DC-07 S5, owner decision DC-21).** New choice `factor_set` (Setup; `EN recommended`, the default, or `National Annex record`). With the record set chosen the five factor inputs must carry the pin of an applied `en1990.factors` record, else the sheet refuses naming `factor_set`; the set name and the record's provenance are printed (`factor_set_used`, an assumption). The known-answer cases and the worked example stay on `EN recommended` and keep their values; a calculation saved without `factor_set` reads `EN recommended`. The record kind `en1990.factors`, its template 15 and its requirement row (C-3) already existed and carry the set; no second kind was added, because the importer keys a kind by the inputs the calculator declares and the National Annex record holds the same five. No calculated value changes.

**1.2.0 (C-2 consistency).** The inputs are grouped: the actions table, the unit and the ULS expression in Setup, the factors and the ψ table in Factors. The hard range of `K_FI` is widened to 0.5 to 2 so that its usual range of 0.9 to 1.1 sits inside it (a value outside 0.9 to 1.1 now warns instead of being refused). The standard is written in full (BS EN 1990:2002+A1:2005) and the UK National Annex is cited by its tables. The scope, assumptions and one message follow the sheet standard. No calculated value changes.

## References

- BS EN 1990:2002+A1:2005, cl. 6.4.3.2: Combinations for persistent or transient design situations (6.10, 6.10a, 6.10b).
- BS EN 1990:2002+A1:2005, cl. 6.4.3.3: Combinations for accidental design situations (6.11b).
- BS EN 1990:2002+A1:2005, cl. 6.5.3: Serviceability combinations (6.14b, 6.15b, 6.16b).
- BS EN 1990:2002+A1:2005, Annex A1, Tables A1.1, A1.2(B): Application for buildings: ψ and γ factors.
- BS EN 1990:2002+A1:2005, Table NA.A1.2(A) to (C): UK National Annex: UK values of partial factors.

## Test cases

`ka-610-single` (21 kN), `ka-610ab-single` (18.975 kN), `ka-610-two-variables` (24.75 kN, wind leading), `ka-uplift` (−20 kN) — analytic, Confirmed; `ka-designers-guide` (37.65 kN/m) — pattern after Gulvanessian, Calgaro & Holický (2002), **Unconfirmed** until checked against the book. Worked example `office-floor-beam-uk-na` (37.48 kN/m) — UK NA factors, **Unconfirmed** until checked against the NA.

- `ka-610-single`: Permanent plus office imposed, 6.10. Source: Analytic (EN 1990 6.10, recommended factors), EN 1990:2002+A1:2005.
- `ka-610ab-single`: Permanent plus office imposed, 6.10a and 6.10b. Source: Analytic (EN 1990 6.10a/6.10b, recommended factors), EN 1990:2002+A1:2005.
- `ka-610-two-variables`: Imposed and wind, each leading in turn. Source: Analytic (EN 1990 6.10, recommended factors), EN 1990:2002+A1:2005.
- `ka-uplift`: Wind uplift against favourable permanent load. Source: Analytic (EN 1990 6.10, recommended factors), EN 1990:2002+A1:2005.
- `ka-designers-guide`: Floor beam: permanent, imposed and snow (Designers' Guide pattern). Source: Gulvanessian, Calgaro & Holický, Designers' Guide to EN 1990, 2002.

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
