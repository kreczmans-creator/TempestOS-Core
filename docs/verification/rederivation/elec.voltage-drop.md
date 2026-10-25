# elec.voltage-drop — independent re-derivation (Track P, P1)
Calculator: Voltage drop and I²R loss, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Electrical/VoltageDropCalculator.cs. Spec: docs/calcs/electrical/elec.voltage-drop.md.
Script: tools/verification/elec.voltage-drop.py (exit 0 = all KATs match; 61 rows: 11 KAT rows and 50 extra rows).
Verdict: MATCH WITH FINDINGS (reviewed: one High, two Medium)

All 11 KAT values match. All 10 extra probes match to better than 1e-9. The formulas are right. The default copper resistivity gives resistances 5 to 11 % below the BS EN 60228 class 2 maximum values that the BS 7671 tables use. Two silent-default and guard gaps remain.

## Method re-derived
- Resistivity at conductor temperature: ρ_T = ρ_20 [1 + α (T_c − 20)]. With ρ_20 = 0.01724 Ω·mm²/m and α = 0.00393 /K. Source: Hughes, Electrical and Electronic Technology, resistance and temperature coefficient. Unit Ω·mm²/m.
- Conductor resistance R = ρ_T L/(A n). Per-kilometre route: R = R′ L/(1000 n), X = X′ L/(1000 n). Units Ω, with L in m.
- DC: ΔV = 2 I R. Single-phase: ΔV = 2 I (R cos φ + X sin φ). Three-phase: ΔV = √3 I (R cos φ + X sin φ).
- mV/A/m route: ΔV = (mV/A/m) × I × L/1000, divided by n for n parallel conductors. Source: BS 7671 (18th ed.) Appendix 4 section 6. The three-phase figure already carries the √3/2 factor.
- Loss: 2 I² R (DC and single-phase), 3 I² R (three-phase). Per cent drop is ΔV/V_n, with V_n line-to-line for three-phase.
- Conductor temperature: T_c,est = T_amb + (T_rated − T_amb)(I/I_z)². I checked it against the BS 7671 Appendix 4 temperature-correction form t = t_p − (C_a² C_g² − I_b²/I_t²)(t_p − 30) from memory. With I_z including the ambient factor the two forms are the same.
- BS 7671 uses (230 + t)/(230 + 20)-type factors. They give 1.20 at 70 °C. The product gives 1.1965. The difference is 0.3 %.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-dc | R (Ω) | 0.10314 | 0.103138 | 1.7e-5 | 1e-4 | PASS |
| ka-dc | dV (V) | 4.1256 | 4.12553 | 1.7e-5 | 1e-4 | PASS |
| ka-dc | dV_pct | 8.595 | 8.59486 | 1.7e-5 | 1e-3 | PASS |
| ka-dc | P_loss (W) | 82.51 | 82.5106 | 7.8e-6 | 1e-3 | PASS |
| ka-three-phase | R (Ω) | 0.047150 | 0.0471489 | 2.3e-5 | 1e-4 | PASS |
| ka-three-phase | dV (V) | 7.526 | 7.52542 | 7.8e-5 | 1e-3 | PASS |
| ka-three-phase | dV_pct | 1.8815 | 1.88135 | 7.8e-5 | 1e-3 | PASS |
| ka-three-phase | P_loss (W) | 1414.5 | 1414.47 | 2.3e-5 | 1e-3 | PASS |
| ka-three-phase | T_c_est (°C) | 58.25 | 58.2466 | 5.9e-5 | 1e-3 | PASS |
| ka-mvam | dV (V) | 7.56 | 7.56 | 1.2e-16 | 1e-9 | PASS |
| ka-mvam | dV_pct | 1.89 | 1.89 | 1.2e-16 | 1e-9 | PASS |

No KAT value disagrees with physics. The KAT sources are labelled "Analytic". None uses a published BS 7671 table figure. See -01.

## Extra probes (product vs independent)
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| V1 single-phase | 230 V, 32 A, 25 m, 4 mm², cos φ 0.9 | dV | 7.481752 V | 7.481752 | <1e-9 | PASS |
| V2 per-km, 2 parallel | R′ 0.63, X′ 0.095 Ω/km, 63 A, 50 m, cos φ 0.8 | dV | 1.530397 V | 1.530397 | <1e-9 | PASS |
| V3 mV/A/m, 2 parallel | 1.5 mV/A/m, 60 A, 40 m, single-phase | dV | 1.8 V | 1.8 | 0 | PASS |
| V4 near zero | 1 µA, 1 mm, 1e5 mm² | dV | 3.5728e-16 V | 3.5728e-16 | <1e-9 | PASS |
| V5 very large | 1e6 A, 1e6 m, 20 parallel, cos φ 0.5 | dV | 8.393204e6 V | 8.393204e6 | <1e-9 | PASS |
| V6 DC cold | 24 V, 2.5 mm², T_c −40 °C | R | 0.0632391 Ω | 0.0632391 | <1e-9 | PASS |
| V7 power factor 0.01 | KAT three-phase inputs | dV | 1.190121 V | 1.190121 | <1e-9 | PASS |
| V8 overload | 150 A on I_z 100 A, T_amb 40 °C | T_c_est | 107.5 °C | 107.5 | <1e-9 | PASS |
| V9 aluminium | ρ 0.0282, α 0.00403, 95 mm², 90 °C | dV | 15.68799 V | 15.68799 | <1e-9 | PASS |
| V12 per-km with I_z | 4.61 Ω/km, 20 A, I_z 32 A | T_c_est | 45.625 °C | 45.625 | <1e-9 | PASS |

The overload warning and the temperature warning both fired in V8. A missing R′ gave a clear Block.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| ρ_T, R, X, ΔV, loss | As above | Same formulas | Yes |
| Parallel conductors | R/n, X/n; mV/A/m drop divided by n | Same | Yes |
| Percent drop | ΔV/V_n | Same | Yes |
| Conductor temperature | t_p − (…)(t_p − 30) form | Same when I_z includes ambient correction | Yes |
| Default ρ_20 for copper | BS EN 60228 class 2 maximum R (about 0.0184 to 0.0194 Ω·mm²/m for 35 to 50 mm²) | 0.01724 (annealed copper) | No, see -01 |
| Missing A_c or ρ_20 | Block rule "conductor-data" | Silent defaults 35 mm² and copper | No, see -02 |
| mV/A/m route | I ≤ I_z check, temperature estimate | Early return; I_z ignored | No, see -03 |
| Per-km route | R at the operating temperature | No correction; the user enters the hot value | See -04 |
| Temperature correction of mV/A/m | BS 7671 Appendix 4 correction factor | Not offered | See -04 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-elec.voltage-drop-01 | Medium | High (upgraded). Unconservative by 5 to 11 % in every default run, against the basis of the BS 7671 tables. Same root and fix as elec.fault-protection-01. Table values: owner to confirm against licensed copy. | The default ρ_20 = 0.01724 gives conductor resistances 5 to 11 % below the BS EN 60228 class 2 maximum values. BS 7671 mV/A/m tables are built on those values. The drop is unconservative against the tables. | 35 mm² at 70 °C, three-phase, cos φ 1, X 0: product 1.0208 mV/A/m. The table has about 1.1 (7 % higher). Differences at 20 °C: 1.5 mm² −5.0 %, 6 mm² −6.7 %, 35 mm² −6.0 %, 50 mm² −10.9 %. Table values are from memory; check them against your copy. | Add a size-based resistance table from BS EN 60228 as an option, or state the gap beside the ρ_20 input. | Open. Probe: 35 mm², 80 m, 70 °C, three-phase gives R 0.04715 Ω, which is 1.0208 mV/A/m. The resistivity default is unchanged. |
| P1-elec.voltage-drop-02 | Medium | Medium (confirmed). Probe: blank A_c and ρ_20 give R 0.04715 Ω with no notice. Only X_km gets the generic default notice. | A blank A_c or ρ_20 is replaced by 35 mm² and copper with no notice. The "conductor-data" block rule cannot fire on this route. | Resistivity route with only system, V_n, I, L and cos φ: R = 0.04715 Ω, the KAT answer, and no notice for A_c or ρ_20. | Remove the defaults for A_c, or add a notice when an optional default is used. | Open. Probe: blank A_c and ρ_20 still give R 0.04715 Ω with no notice for either. |
| P1-elec.voltage-drop-03 | Low | Medium (upgraded). Probe: I 60 A against I_z 50 A on this route gives a pass and no notice. The user entered I_z and expects the check. | The mV/A/m route ignores I_z. There is no overload check and no T_c_est, even when I_z is entered. | V3 inputs with I = 60 A, I_z = 50 A: only the drop check is shown. | Run the I ≤ I_z check on all routes. Optionally apply the Appendix 4 temperature correction. | Open. Probe: mV/A/m route, I 60 A, I_z 50 A still gives only the drop check. There is no overload check and no T_c_est. |
| P1-elec.voltage-drop-04 | Low | Low (confirmed). BS 7671 mV/A/m tables are already at the maximum operating temperature, so that route is normally conservative. | The per-km route and the mV/A/m route have no temperature correction. A 20 °C resistance entered there gives an unconservative drop. The label says "at its stated temperature" but gives no way to correct. | R_km 3.08 Ω/km (6 mm², 20 °C) is used as entered. At 70 °C it would be 3.69 Ω/km. | Add a "tabulated at" temperature input and correct to T_c. | Open. Probe: R_km 3.08 Ω/km, T_c 70 °C is used as entered (R 0.154 Ω for 50 m). No tabulated-at input. |
| P1-elec.voltage-drop-05 | Info | Info (confirmed). | The aluminium resistivity needs the user to change α as well. The label gives ρ only. | α_Al is about 0.0040 against the default 0.00393; the effect is below 0.5 % at 70 °C. | Mention α beside the ρ_20 hint. | Open (Info). The ρ_20 label is unchanged and gives no α hint. |
| P1-elec.voltage-drop-R1 | Low | Low (new) | The assumption says X is zero on the resistivity route unless X_km is given. The code applies the default X_km 0.08 Ω/km. The error is conservative. | Probe vd1: blank X_km gives the notice that 0.08 Ω/km is used. | Align the assumption text with the code. | Open. Probe: blank X_km still gives the notice that 0.08 Ω/km is used. The assumption text is unchanged. |
| P1-elec.voltage-drop-R2 | Low | Low (new) | V_n defaults to 400 V for every system. A single-phase or DC circuit left at the default shows the per cent drop against 400 V. Only the generic default notice appears. | Single-phase 230 V circuit with V_n blank: dV_pct is 230/400 of the true value, 43 % low. | Make V_n required for single-phase and DC, or warn when V_n is 400 V on those systems. | Open. Probe: single-phase, V_n blank gives dV_pct 0.4073 against the 400 V default. Only the generic default notice shows. |

## Notes for the owner's manual test
- DC KAT by hand: ρ_70 = 0.01724 (1 + 0.00393 × 50) = 0.020628. R = 0.020628 × 30/6 = 0.10314 Ω. ΔV = 2 × 20 × 0.10314 = 4.1256 V (8.595 % of 48 V).
- Three-phase KAT: ΔV = √3 × 100 × (0.04715 × 0.85 + 0.0064 × 0.5268) = 7.526 V. T_c,est = 30 + 40 (100/119)² = 58.25 °C.
- Compare 35 mm² copper, 70 °C, cos φ 1 with your BS 7671 Appendix 4 table. Expect the product at 1.021 mV/A/m three-phase and 1.179 mV/A/m single-phase (-01).
- Use the mV/A/m route with 2.4 mV/A/m, 63 A, 50 m. Expect 7.56 V, 1.89 % of 400 V. Set n = 2 and expect 3.78 V.
- Leave A_c empty on the resistivity route. Expect the 35 mm² answer with no warning (-02).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script: all rows pass. It is an independent Python derivation.
- I reproduced 02 and 03 with the probe, and read Run line by line. The drop formulas, the √3 and 2 factors and the parallel division are right.
- Changes: 01 raised to High, to match the same defect in elec.fault-protection. 03 raised to Medium. R1 and R2 added, both Low.
- Residual doubt: the BS EN 60228 maxima in 01 are from memory. Owner to confirm against licensed copy.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source not changed since 298ade36 (version 1.0.0, no diff in VoltageDropCalculator.cs). The KATs are unchanged; the product kat run passes 3 of 3.
- Probe at 0772fcf8: all seven findings are still Open. Each was re-run with the original input.
- Pack use changed in 874bc235: the motor-circuit pack now runs E3 as three-phase (G-B-05). That is a template choice, not a change to this calculator. See chain.motor-circuit.
- Script unchanged; it exits 0.
- New defects: none found.
