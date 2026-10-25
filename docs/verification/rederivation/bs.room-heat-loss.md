# bs.room-heat-loss — independent re-derivation (Track P, P1)
Calculator: U-values and room heat loss, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.BuildingServices/RoomHeatLossCalculator.cs. Spec: docs/calcs/building-services/bs.room-heat-loss.md.
Script: tools/verification/bs.room-heat-loss.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (after review: one High, two Medium)

All 5 known-answer cases (32 expected values) and 38 extra probe values agree with the independent script. The arithmetic is correct. The first-pass findings concern one misleading output, one spec mismatch and two documented limits. The review adds a High: MVHR with a blank infiltration rate drops infiltration (R1).

## Method re-derived
- Total resistance R = R_si + sum(d/lambda) + R_g + R_se, in m2K/W, d in m. Source: BS EN ISO 6946, homogeneous layers in series. U = 1/R in W/(m2K).
- Surface resistance defaults 0.13 (inside, horizontal flow), 0.04 (outside), 0.13 (second face of an internal partition). Source: BS EN ISO 6946, conventional values for walls.
- Element loss Phi_i = A·U·f·(theta_int − theta_adj), in W. Source: BS EN 12831-1, transmission loss with a temperature factor.
- Thermal-bridge allowance Phi_tb = y_tb · sum(A·f on outside-facing elements) · (theta_int − theta_e), in W. Product-specific simplification of the BS EN 12831-1 bridge term.
- Ventilation Phi_V = 0.34 · n · V · dT, with n·V in m3/h and result in W. With MVHR, dT is theta_int − theta_supply, plus 0.34 · n_inf · V · (theta_int − theta_e).
- Air constant check: 1.2 kg/m3 × 1005 J/(kg K) / 3600 = 0.335 Wh/(m3K). The product's 0.34 is 1.5 % higher, which is conservative.
- Room load Phi_HL = Phi_T + Phi_V; the DHDG option multiplies by (1 + f_add). HLC = Phi_HL/dT; Phi_A = Phi_HL/A_floor.
- Utilisation u = U/U_target for built-up elements, and for any element with its own target.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-cavity-wall | R_1 | 5.538045 | 5.538045 | 2.0e-08 | 1e-06 | PASS |
| ka-cavity-wall | U_1 | 0.1805691 | 0.1805691 | 1.8e-07 | 1e-06 | PASS |
| ka-cavity-wall | u_1 | 0.6018971 | 0.6018971 | 1.2e-08 | 1e-06 | PASS |
| ka-cavity-wall | Phi_1 | 86.67318 | 86.67318 | 4.0e-08 | 1e-06 | PASS |
| ka-cavity-wall | Phi_2 | 100.8 | 100.8 | 1.4e-16 | 1e-09 | PASS |
| ka-cavity-wall | Phi_T | 187.4732 | 187.4732 | 8.8e-08 | 1e-06 | PASS |
| ka-cavity-wall | q_v | 20 | 20 | 0.0e+00 | 1e-09 | PASS |
| ka-cavity-wall | Phi_V | 163.2 | 163.2 | 1.7e-16 | 1e-09 | PASS |
| ka-cavity-wall | Phi_HL | 350.6732 | 350.6732 | 4.7e-08 | 1e-06 | PASS |
| ka-cavity-wall | HLC | 14.61138 | 14.61138 | 1.8e-07 | 1e-06 | PASS |
| ka-mvhr-partition | U_2 | 1.851852 | 1.851852 | 8.0e-08 | 1e-06 | PASS |
| ka-mvhr-partition | Phi_1 | 65.00489 | 65.00489 | 3.7e-08 | 1e-06 | PASS |
| ka-mvhr-partition | Phi_2 | 66.66667 | 66.66667 | 5.0e-08 | 1e-06 | PASS |
| ka-mvhr-partition | Phi_3 | 36 | 36 | 0.0e+00 | 1e-09 | PASS |
| ka-mvhr-partition | A_tb | 21 | 21 | 0.0e+00 | 1e-09 | PASS |
| ka-mvhr-partition | Phi_tb | 25.2 | 25.2 | 1.4e-16 | 1e-09 | PASS |
| ka-mvhr-partition | Phi_T | 192.8716 | 192.8716 | 2.4e-07 | 1e-06 | PASS |
| ka-mvhr-partition | Phi_V | 51 | 51 | 1.4e-16 | 1e-09 | PASS |
| ka-mvhr-partition | Phi_HL | 243.8716 | 243.8716 | 1.9e-07 | 1e-06 | PASS |
| ka-mvhr-partition | HLC | 10.16131 | 10.16131 | 4.7e-07 | 1e-06 | PASS |
| ka-dhdg-allowance | Phi_T | 187.4732 | 187.4732 | 8.8e-08 | 1e-06 | PASS |
| ka-dhdg-allowance | Phi_V | 163.2 | 163.2 | 1.7e-16 | 1e-09 | PASS |
| ka-dhdg-allowance | Phi_HL | 385.7405 | 385.7405 | 4.6e-09 | 1e-06 | PASS |
| ka-dhdg-allowance | HLC | 16.07253 | 16.07252 | 5.7e-07 | 1e-06 | PASS |
| ka-single-layer-target | U_1 | 3.868538 | 3.868538 | 4.4e-08 | 1e-06 | PASS |
| ka-single-layer-target | u_1 | 0.9671345 | 0.9671345 | 4.4e-08 | 1e-06 | PASS |
| ka-single-layer-target | Phi_1 | 928.4492 | 928.4492 | 4.2e-08 | 1e-06 | PASS |
| ka-single-layer-target | Phi_V | 4.08 | 4.08 | 0.0e+00 | 1e-09 | PASS |
| ka-mvhr-infiltration | Phi_V | 91.8 | 91.8 | 1.5e-16 | 1e-09 | PASS |
| ka-mvhr-infiltration | Phi_T | 192.8716 | 192.8716 | 2.4e-07 | 1e-06 | PASS |
| ka-mvhr-infiltration | Phi_HL | 284.6716 | 284.6716 | 1.6e-07 | 1e-06 | PASS |
| ka-mvhr-infiltration | HLC | 11.86131 | 11.86131 | 4.0e-07 | 1e-06 | PASS |

No KAT expected value disagrees with physics or with the method as I know it. The KAT conductivities are illustrative inputs, as the spec states.

## Extra probes (product vs independent)
Product values come from the probe tool. The script hard-codes them and re-checks them.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| p1-typical | wall 18 m2, roof 20 m2 Rsi 0.10, floor U 0.22 f 0.7, window 4 m2; ti 21, te -4, y 0.08, n 0.7, V 55 | U_1 | 0.1967149 | 0.1967149 | 1.7e-10 | PASS |
| p1-typical | same | U_2 | 0.1776221 | 0.1776221 | 2.4e-10 | PASS |
| p1-typical | same | Phi_tb | 112 | 112 | 1.3e-16 | PASS |
| p1-typical | same | Phi_T | 506.3328 | 506.3328 | 2.8e-11 | PASS |
| p1-typical | same | Phi_V | 327.25 | 327.25 | 1.7e-16 | PASS |
| p1-typical | same | Phi_HL | 833.5828 | 833.5828 | 1.7e-11 | PASS |
| p1-typical | same | HLC | 33.34331 | 33.34331 | 1.0e-10 | PASS |
| p1-typical | same | Phi_A | 41.67914 | 41.67914 | 1.0e-10 | PASS |
| p1-typical | same | U_m | 0.2544082 | 0.2544082 | 8.7e-11 | PASS |
| p2-max-boundary | 500 m2, d 500 mm, lambda 5; ti 16, te -15, y 0.3, n 3, V 1000 | Phi_T | 62057.41 | 62057.41 | 4.2e-11 | PASS |
| p2-max-boundary | same | Phi_V | 31620 | 31620 | 1.2e-16 | PASS |
| p2-max-boundary | same | Phi_HL | 93677.41 | 93677.41 | 2.8e-11 | PASS |
| p2-max-boundary | same | HLC | 3021.852 | 3021.852 | 4.9e-11 | PASS |
| p3-near-zero | 0.1 m2, lambda 0.01; ti 16, te 5, supply 16, n 0.1, V 5 | Phi_T | 0.02192545 | 0.02192545 | 8.0e-11 | PASS |
| p3-near-zero | same | Phi_V | 0 | 0 | 0.0e+00 | PASS |
| p3-near-zero | same | HLC | 0.001993223 | 0.001993223 | 1.7e-10 | PASS |
| p4-gain-adj33 | partition 30 m2 at 33 degC; te 5 | Phi_T | -782.6087 | -782.6087 | 6.1e-11 | PASS |
| p4-gain-adj33 | same | Phi_HL | -779.8887 | -779.8887 | 6.1e-11 | PASS |
| p4-gain-adj33 | same | HLC | -48.74304 | -48.74304 | 3.6e-11 | PASS |
| p5-adj-below-te | wall 10 m2, adjacent -8 degC, te -3, y 0.1 | Phi_T | 807.7838 | 807.7838 | 2.0e-11 | PASS |
| p5-adj-below-te | same | Phi_tb | 24 | 24 | 0.0e+00 | PASS |
| p5b-adj-equals-te | wall 10 m2, adjacent -3 degC = te, y 0.1 | Phi_T | 672.6486 | 672.6486 | 7.2e-11 | PASS |
| p6-mvhr-cold-supply | supply -8 degC, n_inf 0.2 | Phi_V | 262.48 | 262.48 | 0.0e+00 | PASS |
| p6-mvhr-cold-supply | same | Phi_HL | 911.1286 | 911.1286 | 5.3e-11 | PASS |
| p7-shared-build-up | two walls share one build-up (construction key) | Phi_T | 1167.568 | 1167.568 | 3.7e-10 | PASS |
| p7-shared-build-up | same | Phi_HL | 1330.768 | 1330.768 | 3.2e-10 | PASS |
| p9-dhdg-20pct | DHDG, f_add 20 % | Phi_HL | 974.2184 | 974.2184 | 2.2e-11 | PASS |
| p9b-en-ignores-f_add | EN method, f_add 10 % (ignored, warning) | Phi_HL | 811.8486 | 811.8486 | 6.0e-11 | PASS |
| p10-ninf-natural-ignored | natural ventilation, n_inf 0.3 (ignored, warning) | Phi_HL | 811.8486 | 811.8486 | 6.0e-11 | PASS |
| p11-rsi-0.17 | Rsi 0.17 entered | U_1 | 2.439024 | 2.439024 | 1.0e-10 | PASS |
| p11-rsi-0.17 | same | Phi_HL | 748.5659 | 748.5659 | 5.5e-11 | PASS |
| p12-partition-18C | adjacent 18 degC, Rse blank (0.13) | U_1 | 2.173913 | 2.173913 | 2.2e-10 | PASS |
| p12-partition-18C | same | Phi_HL | 228.4174 | 228.4174 | 1.9e-11 | PASS |
| p13-rg-0.18 | Rg 0.18, Rsi 0, Rse 0 | U_1 | 2.631579 | 2.631579 | 1.4e-10 | PASS |
| u1-unit-conversion | 69.8 degF, 270.15 K, 0.1 m, 40000 L | Phi_HL | 811.8486 | 811.8486 | 6.0e-11 | PASS |
| u3-targets | element targets 2.0 / 0.3 and global 0.35; override element | u_2 | 1.248439 | 1.248439 | 2.5e-10 | PASS |
| u3-targets | same | u_1 | 1.351351 | 1.351351 | 2.6e-10 | PASS |
| u3-targets | same | U_m | 1.270894 | 1.270894 | 1.3e-10 | PASS |

Probes that blocked or warned as designed: theta_supply above theta_int (block), lambda 0.005 (block), n 3.5 (block), theta_e 21 (block), duplicate names (block), no layers and no U (block), a layer row without lambda (block), an orphan layer (warning), a U override with layers (warning), supply below theta_e (warning), Phi_HL at or below 0 (warning), f_add under EN (warning), n_inf without MVHR (warning).

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| U from layers | R = R_si + sum d/lambda + R_g + R_se | Same; d converted from mm to m by the unit layer | Yes |
| Units | d in mm, lambda in W/(mK), theta in degC, K or degF, V in m3 or L | Converted to SI; probe u1 (degF, K, m, L) gives the same result | Yes |
| Ventilation constant | 0.34 Wh/(m3K), flow in m3/h | 0.34 × q_v (m3/h) × dT, result in W | Yes (1.5 % above 0.335) |
| MVHR | Supply temperature replaces theta_e; infiltration at theta_e | As specified; no efficiency input; blank n_inf silently means no infiltration | No, see R1 and -04 |
| Adjacent temperature at or below theta_e | Spec: theta_adj = theta_e | Code uses the entered (colder) temperature; bridge term still uses theta_e | No, see -02 |
| Thermal bridges | On outside-facing area times f | A_tb = sum(A·f) outside only; adjacent above theta_e gets none | Yes (documented) |
| Default surface resistances | 0.13 / 0.04 / 0.13 | Same for every element; no flow-direction choice | Yes (documented), see -03 |
| Mean U (U_m) | Output label "Mean fabric U-value" | Computes sum(A·U·f)/sum(A), so f lowers the mean | No, see -01 |
| Targets | One check per element with a target | Built-up elements take the global target; overrides need their own | Yes |
| Guards | theta_int above theta_e; supply not above theta_int; unique names; layers present | All present and tested | Yes |
| Hard limits | Template limits | Enforced as blocks; soft limits give warnings (Rsi 0 warns) | Yes |
| Division by zero | Area at least 0.1 m2, dT above 0 | A_fab, dT and A_floor are guarded by limits | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-bs.room-heat-loss-01 | Medium | Medium (confirmed). Misleading label; no check uses U_m. | Output U_m is labelled "Mean fabric U-value" and "area-weighted", but it includes the temperature factor f. It understates the true mean U. | Probe p1: wall 18 m2 U 0.1967, roof 20 m2 U 0.1776, floor 20 m2 U 0.22 f 0.7, window 4 m2 U 1.4. Product U_m = 0.2544. Area-weighted U without f = 0.2757 (8 % higher). | Divide by sum(A) using sum(A·U) only. Or relabel the output "Σ A·U·f / Σ A (heat-loss weighted)" and add a true mean U. | Open. Probe: the typical room still gives U_m 0.2544 (with f), labelled 'Mean fabric U-value'; the true area-weighted U is 0.2757. |
| P1-bs.room-heat-loss-02 | Low | Low (confirmed). Conservative; the spec text and the code disagree. | Spec says an element at or below theta_e uses theta_e as its adjacent temperature. The code uses the entered colder temperature for the element loss. The bridge term still uses theta_e. | Probe p5: wall 10 m2, U 2.7027, adjacent −8 degC, theta_e −3, y 0.1. Phi_1 = 783.78 W, not 648.65 W from the spec. | Update the spec to say the entered colder temperature is used (conservative). Or clamp to theta_e. Decide and align text and code. | Open. Probe: wall with adjacent −8 °C and θ_e −3 °C still gives the loss at −8 °C (Phi_T 807.78 W). |
| P1-bs.room-heat-loss-03 | Low | Low (confirmed). Corrected the evidence: the sheet records an assumption naming the defaulted elements, but no warning. | A blank Rsi gives 0.13 for roofs, ceilings and floors. Upward flow uses a smaller value in BS EN ISO 6946, so a blank roof U is slightly low. The sheet records an assumption naming the defaulted elements, but raises no warning. | Probe l5: an element named "Roof" with blank Rsi gets R = 0.37, same as a wall. With the upward-flow R_si of 0.10, R falls from 0.37 to 0.34 and U rises 8 %. For a well-insulated roof the error is under 1 %. | Add a per-element flow direction (up, horizontal, down) that sets R_si and R_se. Or show the defaulted-resistance notice as a warning for each defaulted element. | Fixed (3c637a3d). New 'flow' column: upward gives R_si 0.10, downward 0.17. Roof of 25/200/12.5 mm: R 5.630 m²K/W with 'upward', 5.660 with blank (horizontal). A blank flow still means horizontal. |
| P1-bs.room-heat-loss-04 | Info | Info (confirmed). The missing infiltration with MVHR is a separate, larger defect: see R1. | The product takes an MVHR supply temperature, not an efficiency, and gives no reheat or intermittent-heating allowance under the BS EN 12831-1 option. The DHDG option has one user percentage. | Template inputs: theta_supply, f_add (DHDG only). | Optional: add an efficiency input with theta_supply = theta_e + eta·(theta_extract − theta_e). State the missing reheat allowance on the sheet. | Open (Info). Unchanged. |
| P1-bs.room-heat-loss-05 | Info | Info (confirmed). | The 0.34 constant is 1.5 % above the derived 0.335. This is conservative. Check the BS EN 12831-1 value against the owner's copy. | Script line "Air constant". | None, unless the owner's copy gives a different value. | Open (Info). Unchanged. |
| P1-bs.room-heat-loss-R1 | High | High (new) | With an MVHR supply temperature and n_inf blank, the room has no infiltration loss and no notice. n is then the MVHR rate only. A room with MVHR still leaks air at θ_e. The spec has the same gap (n_inf optional). | Probe: wall 20 m² U 0.18, window 3 m² U 1.4, V 50 m³, n 0.5, supply 16 °C. Blank n_inf: Phi_V 42.5 W, Phi_HL 229.7 W. With n_inf 0.2: Phi_V 124.1 W, Phi_HL 311.3 W. The blank case is 26 % low. | Warn when theta_supply is entered and n_inf is blank. Or make n_inf required with MVHR. Update the spec. | Open. Probe: MVHR supply 16 °C, n 0.5, V 50, n_inf blank still gives Phi_V 42.5 W and no notice. With n_inf 0.2 it gives 124.1 W. |
| P1-bs.room-heat-loss-R2 | Medium | Medium (new) | An element can take both a temperature factor f below 1 and an adjacent temperature. Both reduce the loss, so the reduction counts twice, with no notice. | Probe: 10 m², U 0.3, θ_adj 10 °C, f 0.5, θ_int 21 °C: Phi_1 16.5 W. Either reduction alone gives 33 W. | Warn when f differs from 1 and θ_adj is above θ_e. State in the column help that f is for elements to θ_e only. | Open. Probe: 10 m², U 0.3, θ_adj 10 °C, f 0.5 still gives Phi_1 16.5 W with no notice. Either reduction alone gives 33 W or 36 W. |

## Notes for the owner's manual test
- Enter the cavity-wall case: brick 102.5/0.77, PIR 100/0.022, block 100/0.15, plaster 13/0.57, wall 20 m2, window 3 m2 U 1.4, 21 and −3 degC, n 0.5, V 40. Expect U 0.18057, Phi_HL 350.67 W, HLC 14.611 W/K.
- Add a floor with U 0.22 and f 0.7 beside several walls. Compare "Mean fabric U-value" with your own area-weighted U. It will read low (finding 01).
- Type an adjacent temperature of −8 degC for a wall with theta_e −3 degC. The loss uses −8 degC (finding 02).
- Enter MVHR with supply 16 degC, n 0.6, V 50. Expect Phi_V = 0.34 × 30 × 5 = 51 W. Add n_inf 0.1 and expect 91.8 W.
- Switch to the DHDG method with f_add 10 %. Expect Phi_HL × 1.10. Under the BS EN method the same entry only warns.

## Independent review
- Reviewer: senior review (Claude), 2026-10-24.
- I ran the script (exit 0). It is written from the formulas, not translated from the C#.
- There was no Critical or High finding to reproduce. I re-ran the MVHR and temperature-factor cases with the probe. Inputs are in /tmp/claude-0/review-inputs/rhl/.
- I read Run line by line. U, the element losses, the bridge term and the ventilation terms are right.
- Changes: original severities stand. I corrected the -03 evidence: an assumption note does appear.
- New: R1 (High), MVHR without n_inf drops infiltration. R2 (Medium), f and θ_adj can both reduce one element.
- Residual doubt: the 0.34 constant and the BS EN 12831-1 infiltration term need the owner's licensed copy.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source change (3c637a3d, a23e6e18; version 1.1.0): a new `flow` column sets the default R_si by heat-flow direction (0.13, 0.10, 0.17). The global target U now applies to outside-facing built-up elements only (B-A-04). The known-answer layers use invented conductivities, so the KAT values changed (for example Phi_HL 350.67 to 379.44 W). A new KAT `ka-roof-upward` was added.
- KATs: `Probe kat` passes all 7 cases. The script carried the old KAT inputs and values. I moved it to the new ones and added the flow and target rules. It exits 0 (all rows pass).
- Script check: I ran every hard-coded extras case through the live probe. All match to 1e-6. The script has a new roof/partition KAT row.
- Findings: 7 earlier rows, 1 fixed (-03), 6 open. The High finding R1 (MVHR with blank n_inf) is still open.
- New defects: none found. The new target scope is disclosed in an assumption.
- Probe inputs: /tmp/claude-0/recheck4/struct-bs/rhlcmp.py.
