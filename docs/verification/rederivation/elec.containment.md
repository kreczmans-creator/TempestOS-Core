# elec.containment — independent re-derivation (Track P, P1)
Calculator: Containment fill, bend radius and cable pulling, version 1.1.0. Source file: src/Tempest.Calc/Tempest.Calc.Electrical/ContainmentCalculator.cs. Spec: docs/calcs/electrical/elec.containment.md.
Script: tools/verification/elec.containment.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

The product matches the independent script on all 44 KAT values and on 10 extra probes (144 values, below 1e-7).
The brief asked about the IET On-Site Guide cable-factor method. The product does not offer it, by design (spec scope). Finding 03 records this.
The one material finding is an unconservative bend model for heavy cables on large radii.

## Method re-derived
- Fill: A_cab = Σ n·π·d²/4. A_int = π·D²/4 for a conduit, W·H for trunking or tray. fill = A_cab/A_int; u_fill = fill/f_max. Geometry only.
- Bend radius: R_min = n_bend·d_max. Each bend u_b = R_min/R. BS 7671, reg. 522.8, cited for context only.
- Bundle weight w = Σ n·m (kg/m). Weight per metre w·g·w_c, g = 9.80665 m/s².
- Straight, horizontal: T₂ = T₁ + w·g·w_c·μ·L. Coulomb friction on the containment floor.
- Bend: T₂ = T₁·e^(w_c·μ·θ), θ in radians. Capstan equation, Beer and Johnston, Vector Mechanics for Engineers: Statics, Ch. 8.
- Sidewall pressure SWP = T₂/R, with R in metres (N/m).
- The route is run as listed and reversed. The tension never falls along a pull, so the maximum is the end tension.
- Recommended direction: the only one that passes every limit; else the lower maximum tension; then the lower SWP.
- Cable limit T_cab = k_n·N·min(T_max). k_n applies only above 3 cables. u_T = T/T_cab. u_grip = T/T_grip. u_P = P_max/P_lim.
- Jam ratio 1.05·D/d for three equal cables in a conduit; warning for 2.8 to 3.2. Cable-pulling guides.
- Not in the product, and not in the script: the IET On-Site Guide cable-factor method, with its cable and conduit factor tables and its run-length and bend rules.

## Known-answer cases
The script prints every row. Each case below lists its key values.

| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-fill-3x10-in-25 (15 values) | fill / u_fill | 48 % / 0.96 | same | 0 | 1e-9 | PASS |
| ka-fill-3x10-in-25 | T_max_a / T_max_b | 154.9195 N / 138.4210 N | same | <2e-10 | 1e-9 | PASS |
| ka-fill-3x10-in-25 | P_max_a / P_max / T_cab | 461.4812 / 284.2706 N/m / 1200 N | same | <2e-10 | 1e-9 | PASS |
| ka-pull-tray-two-bends (11 values) | T_max_a / T_max_b | 1074.2353 N / 941.1261 N | same | <2e-10 | 1e-9 | PASS |
| ka-pull-tray-two-bends | T_cab / u_T / fill | 450 N / 2.09139 / 5.5292 % | same | <2e-10 | 1e-9 | PASS |
| ka-direction-passes-every-check (8 values) | T_max_a / T_max_b | 1166.9421 N / 1281.1418 N | same | <2e-10 | 1e-9 | PASS |
| ka-direction-passes-every-check | max_tension / P_max | 1281.1418 N / 1010.4127 N/m | same | <2e-10 | 1e-9 | PASS |
| ka-capstan-180 (4 values) | T_max_a / P_max | 600.5674 N / 1501.4184 N/m | same | <2e-10 | 1e-9 | PASS |
| ka-trunking-fill (4 values) | fill / u_fill | 12.0637 % / 0.287231 | same | <2e-10 | 1e-9 | PASS |
| ka-bend-too-tight (2 values) | R_min / u_bend | 120 mm / 1.2 | same | 0 | 1e-9 | PASS |

Worst relative difference over 44 values: 2.2e-10, from the 10-digit rounding of the KAT figures. All KATs agree with the relations as I know them. No KAT is Secondary or Unconfirmed.

## Extra probes (product vs independent)
All values agree to better than 1e-7. Probe Z14 to Z16 compare the product with a bend model that has a weight term. They are informational in the script.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| Z1 | fill only: 4 × 6 mm + 2 × 9 mm in 32 mm conduit | fill / u_fill | 29.883 % / 0.74707 | same | <1e-10 | PASS |
| Z2 | 5 cables, route starts with a bend, w_c 1.5, k_n 0.8, grip 2500 N | T_cab / direction | 2400 N / as listed | same | <1e-9 | PASS |
| Z3 | trunking, one cable without T_max, T₁ 150 N | T_max_b / P_max | 602.18 N / 513.69 N/m | same | <1e-10 | PASS |
| Z4 | μ = 0, T₁ 75 N, two bends | T_max_a / direction | 75 N / either | same | 0 | PASS |
| Z6a | three 10 mm cables in 30 mm conduit | jam warning (J 3.15) | raised | expected | – | PASS |
| Z8 | symmetric route, T₁ 0 | direction | either | either | 0 | PASS |
| Z10 | neither direction passes (T_cab 200 N, P_lim 300 N/m) | max_tension / direction | 411.31 N / reverse | same | <1e-10 | PASS |
| Z11 | imperial: 0.5 in, 1.5 in, lb/ft, 100 ft, lbf, 12 in | T_max_b / T_cab | 178.017 N / 1779.29 N | same | <2e-8 | PASS |
| Z12 | very large: 500 m + 720° bend + 500 m, μ 0.5 | T_max_a | 711 197 N | same | <1e-10 | PASS |
| Z13 | 40 cables in 300 × 100 trunking, k_n 0.7 | T_cab / u_grip | 8400 N / 0.75774 | same | <1e-10 | PASS |
| Z14 | one 10 kg/m cable, 90° bend R 3 m, T₁ 100 N, μ 0.3 | T_max_a | 160.198 N | capstan 160.198; with weight term 263.22 N | +64 % | INFO |
| Z15 | as Z14, T₁ 0 N | T_max_a | 0 N | capstan 0; with weight term 143.83 N | – | INFO |
| Z16 | 1.35 kg/m bundle, 90° bend R 0.5 m, T₁ 100 N, μ 0.4 | T_max_a | 187.446 N | with weight term 187.592 N | +0.1 % | INFO |

Guard probes (product only): a cable count of 2.5 blocks. A cable larger than the conduit fails the fit check. A route without μ blocks. μ without a route warns `route-limits`. Bends without `n_bend` warn. `k_n` with three cables warns and is not applied. The cable equal to the trunking depth fits; a cable equal to the conduit diameter does not.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Fill and areas | geometric | Σ n·π·d²/4; π·D²/4 or W·H | Yes |
| Space factor | user record, no default | `f_max` required, no default, 0.01 to 1 | Yes |
| IET factor method, 45 % / 40 % | not offered | not offered | By design (03) |
| Bend radius | n_bend × d_max | same; tightest bend governs | Yes |
| Straight tension | T₁ + w·g·w_c·μ·L | same | Yes |
| Bend tension | capstan, w_c·μ·θ | `tension *= exp(w_c·μ·θ)` | Yes (see 01) |
| Sidewall pressure | T₂/R | tension after the bend over R in m | Yes |
| Reverse direction | reversed order, same T₁ | same | Yes |
| Direction rule | only passer, else lower T, then SWP | `Passes` with 1e-9 tolerance | Yes |
| Cable limit | k_n·N·min(T_max) | needs T_max on every cable | Partly (02) |
| k_n | above 3 cables only | applied only when N > 3, else warns | Yes |
| Jam ratio | 1.05·D/d, 2.8 to 3.2, three equal cables | same | Yes |
| Units | SI; in, ft, lb/ft, lbf verified | engine conversion | Yes |
| Tension ceiling | refused above 1e12 N | block `route` | Yes |
| Cable weight on tray | w per metre | `w` in kg/m; `wg` in N/m only as a working step with a route | Yes (04) |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-elec.containment-01 | Medium | Bends use the capstan equation with no cable-weight term. For a heavy cable on a large radius, in a horizontal-plane bend, the cable also rubs on the duct floor. The tension after the bend is then too low, and a first bend at T₁ = 0 adds nothing. The limitation text names the omission. The error is unconservative. | Z14: 10 kg/m, 90° bend, R 3 m, T₁ 100 N, μ 0.3. Product 160.2 N. T_out = T₁cosh(μθ) + √(T₁² + (w·g·R)²)·sinh(μθ) gives 263.2 N (+64 %). SWP T/R rises from 53.4 to 87.7 N/m. Z15: T₁ 0 N gives 0 N; the formula gives 143.8 N. A light bundle on a small radius differs by 0.1 % (Z16). | Add the weight term to the bend (Rifenburg form) or warn when w·g·R is not small against T₁. Keep capstan as the option for vertical bends. At minimum, warn when a bend follows a start tension below w·g·R. |
| P1-elec.containment-02 | Low | One cable without `T_max` removes the whole cable limit. The sheet warns, but `T_cab`, `u_T` and the check are dropped even when other cables have limits. | Z3: power cables 1000 N each and one control cable without limit. No `T_cab`, no check; warning `T_cab` only. | Keep the warning. Optionally show the limit from the cables that have one, labelled "partial". |
| P1-elec.containment-03 | Info | The IET On-Site Guide factor method is not implemented: no cable factor sum against a conduit or trunking factor, no run-length and bend limits, no 45 % trunking or 40 % conduit rule. The user enters `f_max` from their own copy. Fill is the true area ratio. The two methods give different answers. | Spec scope: "the factor method is not offered". `f_max` has no default. | None needed. Make the sheet say so, so a user does not read the geometric fill as a factor-method pass. |
| P1-elec.containment-04 | Info | The weight per metre in N/m (`wg`) is a working step shown only when a route is entered. `w` in kg/m is always an output. There is no tray-support load check. | Probe Z1 (no route): `w` = 0.44 kg/m, no `wg`. | None, or add `wg` as an output if tray loading is wanted. |

## Notes for the owner's manual test
- Three 10 mm cables in a 25 mm conduit: expect 48.000 % fill, A_cab 235.62 mm², A_int 490.87 mm². With f_max 0.5, u_fill = 0.96.
- Single cable, 180° bend, R 400 mm, T₁ 200 N, μ 0.35: expect 600.567 N and 1501.42 N/m. Check 200·e^(0.35·π) by hand.
- Same route listed both ways (ka-direction-passes-every-check): expect 1166.94 N as listed, 1281.14 N reversed, and the reverse recommended when P_lim is 1500 N/m.
- Try a heavy cable (10 kg/m) on a 3 m radius with T₁ 100 N. The sheet shows 160.2 N. Compare with the weight-term value of 263.2 N (finding 01).
- Enter three equal 10 mm cables in a 30 mm conduit. Expect the jam warning with 1.05·D/d = 3.15.
