# elec.conductor-sizing — independent re-derivation (Track P, P1)
Calculator: Conductor sizing and overload coordination (E4), version 1.1.0 (1.0.0 at first review). Source file: src/Tempest.Calc/Tempest.Calc.Electrical/ConductorSizingCalculator.cs. Spec: docs/calcs/electrical/elec.conductor-sizing.md.
Script: tools/verification/elec.conductor-sizing.py (exit 0 = all KATs match; 35 KAT rows and 70 extra rows).
Verdict: MATCH WITH FINDINGS (after review: two High, three Medium on the calculator; the arithmetic and the selection logic are right)

The arithmetic matches all six KATs and 17 extra probes. The selection rounds in the safe direction. The main gaps are a missing guard and a missing factor. An ambient temperature that is typed in never reaches the sizing unless C_a is also typed in. A buried cable gets no C_c factor (review finding R1).

## Method re-derived
- I_b = P/(√3·U·cosφ·η), P/(U·cosφ·η), P/U. P is shaft power, so η converts to input. Units: kW, V, A.
- I_n is the smallest rating in the user's list with I_n ≥ I_b. BS 7671 reg. 433.1.1 (i) and (ii).
- C_c = 1.45/2 = 0.725 for a semi-enclosed fuse. I_t,req = I_n/(C_a·C_g·C_i·C_c). BS 7671 Appendix 4 method.
- CSA is the smallest tabulated conductor with I_t ≥ I_t,req. I_z = I_t·C_a·C_g·C_i, without C_c.
- Checks: I_b ≤ I_n, I_n ≤ I_z, I₂ ≤ 1.45·I_z (reg. 433.1.1 (iii)). MCB to BS EN 60898: I₂ = 1.45·I_n. Semi-enclosed fuse: I₂ = 2·I_n. With C_c, 2·I_n ≤ 1.45·I_z is the same as I_n ≤ 0.725·I_z.
- Derived C_a = √[(θ_p − θ_a)/(θ_p − θ_ref)]. This is the BS 7671 Appendix 4 approximation.
- AWG: d = 0.127·92^((36 − n)/39) mm, A = πd²/4. Checked against common nominal areas (2 % band): 14 → 2.081, 10 → 5.261, 4/0 → 107.2 mm².

## Known-answer cases
All 34 expected values match. The largest relative difference is 3e-10 against a tolerance of 1e-6 or tighter.

| Case | Symbols checked | KAT expected | Independent | Max rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-three-phase-mcb-c | I_b, I_n, I_t_req, CSA, I_z, u_Ib, u_In | 14.1507 A, 16 A, 22.8343 A, 4 mm², 16.8869 A, 0.88442, 0.94748 | same | 1.3e-10 | 1e-9 to 1e-6 | PASS |
| ka-single-phase | I_b, I_n, I_t_req, CSA, I_z, u_In | 15.2555 A, 16 A, 16 A, 2.5 mm², 17.7 A, 0.90395 | same | 4.4e-11 | 1e-9 to 1e-6 | PASS |
| ka-dc | I_b, I_n, I_t_req, CSA, I_z, u_In | 25 A, 25 A, 27.4725 A, 6 mm², 29.029 A, 0.86121 | same | 9.2e-11 | 1e-9 to 1e-6 | PASS |
| ka-semi-enclosed | I_b, I_n, C_c, I_t_req, CSA, I_z, I2, u_I2 | 18 A, 20 A, 0.725, 27.5862 A, 6 mm², 31.9 A, 40 A, 0.86477 | same | 1.3e-10 | 1e-9 to 1e-6 | PASS |
| ka-derived-ca | C_a, I_t_req, CSA, I_z | 0.866025, 23.9938 A, 4 mm², 16.0708 A | same | 1.8e-11 | 1e-9 to 1e-6 | PASS |
| ka-awg-10 | CSA, I_n, I_z | 5.26115 mm², 25 A, 26.3 A | same | 9.3e-11 | 1e-9 | PASS |

No KAT disagrees with the method. The KAT capacities and factors are synthetic. The KAT C_a of 0.91 at 35 °C is not a published value. The source text says so.

## Extra probes (product vs independent)
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| x1 | 7.5 kW, MCB C, all factors 1 | CSA / I_z / u_I2 | 2.5 mm² / 17.7 A / 0.90395 | same | <1e-10 | PASS |
| x2 | C_a = 16/17.7 (I_t,req equals I_t) | CSA / I_z | 2.5 mm² / 16 A, u_In = 1 | same | 0 | PASS |
| x3 | C_a lowered by 1e-8 | CSA / I_z | 4 mm² / 21.785 A | same | <2e-10 | PASS |
| x4 | semi-enclosed fuse, 7.5 kW | I_t_req / I2 / u_I2 | 22.069 A / 32 A / 0.91572 | same | <1e-10 | PASS |
| x5 | derived C_a, XLPE, 60 °C | C_a / CSA | 0.70711 / 4 mm² | same | <1e-10 | PASS |
| x6 | derived C_a, XLPE, 25 °C | C_a | 1.0408 | 1.0408 | 3e-10 | PASS |
| x8 | user θ_p 250, θ_ref 60, θ_a −10 | C_a | 1.16980 | 1.16980 | 2e-10 | PASS |
| x9 | AWG rows 4, 2, 0, 4/0; 63 A | CSA | 33.631 mm² (AWG 2) | 33.631 | 2e-11 | PASS |
| x10 | AWG 4/0 only qualifying | CSA | 107.22 mm² | 107.22 | 2e-10 | PASS |
| x11 | 500 kW, 1000 V, MCCB, I2 1000 A | I_b / I_n / u_I2 | 288.68 A / 630 A / 0.86207 | same | <2e-11 | PASS |
| x12 | MCCB, I2 = 1.6·I_n | u_I2 | 0.99747 | 0.99747 | 1e-11 | PASS |
| x15 | DC 10 W at 12 V | I_b / I_n / CSA | 0.8333 A / 6 A / 1.5 mm² | same | 4e-11 | PASS |
| x16 | rows entered out of order | CSA | 4 mm² | 4 mm² | 0 | PASS |
| m1–m5 | empty tables, I_b above top rating, I_t,req above top capacity | Block | ampacity / ratings / I_n / CSA blocks | same | n/a | PASS |

Hard limits work at P, U and C_g. An out-of-range value is refused with a clear message.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| I_b for each supply | Three formulas | Same; linked I_b_in replaces them | Yes |
| Choice of I_n | Smallest rating ≥ I_b | Same; tolerance 1e-9 relative | Yes |
| Rounding direction | Next size up | Next rating and next conductor up; no rounding down | Yes |
| C_c and I_z | C_c in I_t,req only | Same | Yes |
| C_c for a buried cable | 0.9 (BS 7671 Appendix 4, owner to confirm) | Not offered | No, see R1 |
| Smallest conductor | Smallest with I_t ≥ I_t,req | Same; sorted, so row order is free | Yes |
| Derived C_a guards | θ_p above θ_a and θ_ref; 0.1 to 1.5 | Blocks both cases; blocks 7.7 and 0.07 | Yes |
| AWG | Whole gauge; −3 is 4/0 | Same; fractional gauge blocks | Yes |
| Missing records | Block | Empty ampacity or ratings blocks; none are shipped | Yes |
| I₂ for MCB, gG, semi-enclosed, MCCB | As spec | Same; MCCB without I2 warns and skips | Yes |
| Ambient input and C_a | Ambient should reach the factor | θ_a is carried to the sheet only | No, see -01 |
| C_g and C_i range | At most 1 | 0.1 to 1.5 accepted | No, see -02 |
| Reference ambient | Table dependent | θ_ref default 30 in both contexts | Partly, see -03 |
| Table consistency | Not stated | No check of duplicate or inverted rows | No, see -05 |
| I₂ in sizing | Not stated | Conductor chosen on I_n only | Partly, see -06 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-elec.conductor-sizing-01 | High | High (confirmed). Reproduced: θ_a 50 °C, C_a 1, 2.5 mm², I_z 17.7 A, all checks pass, no notice. The real I_z is about 12.5 A, below I_n 16 A. θ_a 70 °C on PVC also passes. | The ambient temperature θ_a never changes the result unless C_a is typed. C_a defaults to 1. No warning appears when θ_a is far from the reference. The conductor is then too small. | PVC, 7.5 kW, MCB C, θ_a 50 °C, C_a 1 entered: 2.5 mm², I_z 17.7 A, pass, no warning. With C_a 0.7071 the pick is 4 mm². θ_a 70 °C on PVC also passes. | Warn when θ_a differs from θ_ref, derive_Ca is off and C_a is 1. Or block when θ_a is at or above θ_p. | Open. Probe: PVC, θ_a 50 °C, C_a 1 still gives 2.5 mm², I_z 17.7 A, three passes and no warning. A 70 °C ambient passes too. |
| P1-elec.conductor-sizing-02 | Medium | Medium (confirmed). Reproduced: C_g = C_i = 1.5 gives 1.5 mm² and I_z 27.675 A. Unconservative, but it needs a typing slip. | C_g and C_i accept up to 1.5. Grouping and thermal-insulation factors are not above 1 in BS 7671. A typing slip makes the cable smaller with no notice. | C_g 1.5 and C_i 1.5, 16 A MCB: I_t,req 7.11 A, 1.5 mm², I_z 27.7 A, pass. | Set the hard maximum of C_g and C_i to 1. Keep 1.5 for C_a only. | Open. Probe: C_g = C_i = 1.5 still gives 1.5 mm², I_z 27.675 A and passes. The hard maximum is unchanged. |
| P1-elec.conductor-sizing-03 | Medium | Medium (confirmed). Reproduced C_a 0.8660 in the machine context. The 40 °C reference is right as far as I know; owner to confirm against licensed copy. The same default is unconservative for a 20 °C ground table: θ_a 25 °C gives 1.061 against 0.949 (12 % high). The warning must name θ_ref. | The derived C_a uses θ_ref 30 °C in the default machine context. BS EN 60204-1 conductor tables use a 40 °C reference as I know them. The context claims to change citations only. The error is conservative. | PVC, θ_a 40 °C, θ_ref 30: C_a 0.866. A 40 °C-based table needs 1.000. I_t,req is 15.5 % too high. | Default θ_ref to 40 in the machine context. Or warn that θ_ref must match the table's reference. Owner to confirm the 40 °C value. | Open. Probe: PVC, θ_a 40 °C, derive_Ca still gives C_a 0.8660 at the default θ_ref 30 °C. |
| P1-elec.conductor-sizing-04 | Low | Low (confirmed). The factor is labelled an approximation and warns. As I recall, the IEC 60364-5-52 ambient table matches the formula at 25 °C (1.06, 1.04). Owner to confirm both tables against licensed copy. | Below 30 °C the derived C_a is higher than the BS 7671 table value as I recall it. The product labels the factor as an approximation. | PVC at 25 °C: formula 1.061, table 1.03 (recalled). XLPE: 1.041 against 1.02. Up to 3 % unconservative. Owner to confirm against the licensed table. | Cap derived C_a at 1.0 when θ_a is below θ_ref, or state the 3 % in the warning. | Open. Probe: PVC, θ_a 25 °C still gives C_a 1.0607 with only the "derived approximation" warning. |
| P1-elec.conductor-sizing-05 | Medium | Medium (confirmed). Reproduced: duplicate 4 mm² rows, product uses 30 A and gives I_z 30 A. | The ampacity table is not checked for sense. A duplicate cross-section takes the higher capacity. A larger section with a lower capacity is ignored without a notice. A mixed-method table passes silently. | Rows 4 mm² at 24.1 A and 4 mm² at 30 A, 25 A needed: product picks 4 mm² at 30 A. Using 24.1 A, the answer is 6 mm². | Warn on duplicate sections. Use the lower capacity. Warn when capacity falls as section rises. | Open. Probe: duplicate 4 mm² rows (24.1 A, 30 A) still give 4 mm² with I_z 30 A and no notice. Re-run with I2 40 A, which the machine context now needs. |
| P1-elec.conductor-sizing-06 | Low | Low (confirmed). The sheet shows FAIL, so nothing unsafe gets through. | The conductor is chosen on I_n only. A device I₂ above 1.45·I_z fails the check, but the conductor is not sized up. The sheet shows FAIL, so the risk is low. | MCCB, I2 25.6 A, rows 2.5 mm² at 16.5 A: I_n 16 A, pick 2.5 mm², u_I2 1.070 FAIL. A passing design needs I_z of 17.66 A. | Use I_t,req = max(I_n, I₂/1.45)/(C_a·C_g·C_i·C_c). Or add a notice that the check failed for the chosen size. | Open. Probe: MCCB, I2 25.6 A, 2.5 mm² at 16.5 A still keeps 2.5 mm² and shows u_I2 1.070 FAIL. |
| P1-elec.conductor-sizing-07 | Low | Low (confirmed). Reproduced the block at exactly 1000 mm². | A cross-section of exactly 1000 mm² is refused as above the hard limit 1000 mm². The unit conversion adds float noise. Boundary values should pass. | One row of 1000 mm², 1000 A: "must be at most 1000 mm^2; 1000 mm^2 was given". 999.9999 mm² is accepted. | Compare hard limits with a relative tolerance of 1e-12 after conversion. Check the other area and length inputs. | Open. Probe: one row of 1000 mm² still blocks with "must be at most 1000 mm^2; 1000 mm^2 was given". |
| P1-elec.conductor-sizing-08 | Info | Info (confirmed). | A tolerance of 1e-9 (relative) is used when comparing I_b with I_n and I_t with I_t,req. The effect is below any displayed digit. | I_b_in 16.0000000001 A selects the 16 A rating. | None. | Open (Info). The 1e-9 tolerance is untouched. |
| P1-elec.conductor-sizing-09 | Info | Info (confirmed). The I₂ check still enforces 2·I_n ≤ 1.45·I_z. | For a semi-enclosed fuse an entered I2 below 2·I_n lowers the I₂ check. The conductor stays sized with C_c 0.725, so the result is still safe. The I_n ≤ I_z check shows a limit of 1, not 0.725. | Semi-enclosed fuse, I2 25.6 A: u_I2 0.733, pass; I_z 24.1 A against I_n 16 A (u_In 0.664). | Show the I_n ≤ 0.725·I_z limit for this device. | Open (Info). Probe: semi-enclosed fuse, I2 25.6 A still gives u_I2 0.733 pass and u_In 0.664. |
| P1-elec.conductor-sizing-10 | Info | Info (confirmed). | The deemed gG rule and the I₂ = 1.45·I_n rule cover only the product standards the regulations name. The product offers a generic "gG fuse" and "MCB". Reg. 433.1.201 and 433.1.202 stay unchecked, as the spec says. | MCB breakers to BS EN 60947-2 usually have I₂ 1.30·I_n; use the MCCB type with I2 for them. | Add a note under the device choice. Owner to check the regulation numbers in their copy. | Partly fixed (874bc235). The gG fuse and MCCB/MPCB without I2 are deemed only in the installation context. The machine context blocks until I2 is entered. The generic MCB and gG labels remain. |
| P1-elec.conductor-sizing-R1 | High | High (new) | The product has no rating factor for a cable in the ground. As I recall, BS 7671:2018 Appendix 4 applies C_c = 0.9 to a cable buried direct or in ducts. Owner to confirm the clause against licensed copy. The method key is descriptive only, and C_c is set only for a semi-enclosed fuse. A buried circuit gets I_t,req 11 % too low. | KAT tables, factors 1, 7.5 kW, MCB C: product picks 2.5 mm² (I_t 17.7 A). With C_c 0.9, I_t,req = 16/0.9 = 17.78 A, so 4 mm² is needed. | Add an "installed in the ground" switch that sets C_c 0.9. State how it combines with 0.725. Cite the clause on the sheet. | Open. Probe: the template still has no ground or duct input and no C_c other than the semi-enclosed 0.725. The buried pick stays 2.5 mm². |
| P1-elec.conductor-sizing-E1 | Low | — | The method text says an entered I₂ is always checked. For MCB types B, C and D the entered I₂ is ignored. The sheet uses 1.45·I_n with no notice. | Probe: MCB type B, I2 40 A entered, 16 A rating: I2 shows 23.2 A and u_I2 0.904. Nothing says the entry was dropped. | Warn when I2 is entered for an MCB. Or correct the method wording. | Open (new at 0772fcf8). |

## Notes for the owner's manual test
- Enter the KAT tables (ratings 6 to 63 A; 1.5 to 10 mm² with 12.3, 17.7, 24.1, 31.9, 44.6 A). Run 7.5 kW, 400 V, 0.85, 0.9, MCB C, C_a 0.91, C_g 0.77. Expect I_b 14.151 A, 16 A, 4 mm², I_z 16.887 A.
- Set θ_a to 50 °C and C_a to 1. Expect 2.5 mm² with no warning (finding 01). Then set C_a to 0.71. Expect 4 mm².
- Choose the semi-enclosed fuse on the same load. Expect C_c 0.725, I_t,req 22.07 A, 4 mm², I₂ 32 A.
- Tick derive_Ca, PVC, θ_a 40 °C. Expect C_a 0.8660. Compare it with your licensed table.
- Enter AWG rows 14, 12, 10, 8 with a linked 25 A and a gG fuse. Expect AWG 10, 5.261 mm². Try a fractional gauge. Expect a block.

## Independent review
- Reviewer: senior review (Claude), 2026-10-24.
- I ran the script (exit 0). It is an independent derivation from the formulas, not a translation of the C#.
- I reproduced -01, -02, -03, -05 and -07 with the probe. Inputs are in /tmp/claude-0/review-inputs/cs/.
- I read Run line by line. The selection, C_c, I_z and the I₂ branches are right.
- Changes: all original severities stand. -03 also covers a 20 °C ground table, where the θ_ref default is unconservative.
- New: R1 (High). There is no C_c 0.9 for a buried cable.
- Residual doubt: R1 and the -03/-04 table values come from memory. The owner should confirm them against licensed copies.
- Residual doubt: whether C_c 0.9 and 0.725 combine. I did not assert it.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed (874bc235): version 1.1.0. New output port device_code (1 B, 2 C, 3 D, 4 fuse/MCCB/MPCB) for E5.
- A gG fuse, MCCB or MPCB is deemed to meet I₂ ≤ 1.45·I_z only in the BS 7671 context. The machine context blocks until I2 is entered.
- Two new messages cover AWG rows in a metric system and metric rows in an AWG system. They replace "table empty".
- Probe at 0772fcf8: findings 01 to 09 and R1 are Open. Finding 10 is partly fixed. No KAT value changed.
- KAT inputs changed. ka-dc now uses a gG fuse with I2 40 A (u_I2 0.95030). ka-awg-10 uses a type C MCB. The product matches both.
- Script updated: ka-dc and ka-awg-10 follow the new inputs; new case x18 (gG, I2 1.6·I_n). It runs 35 KAT rows and 70 extra rows and exits 0.
- New defect: E1 (Low). An I2 entered for an MCB is ignored although the method text says an entered I₂ is always checked.
