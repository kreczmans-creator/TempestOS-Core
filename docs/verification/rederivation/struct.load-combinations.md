# struct.load-combinations — independent re-derivation (Track P, P1)
Calculator: Load combinations (EN 1990), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Structural/LoadCombinationsCalculator.cs. Spec: docs/calcs/structural/struct.load-combinations.md.
Script: tools/verification/struct.load-combinations.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

All 11 expected values of the 5 KATs match. All 11 extra probes (78 values) also match, and 3 guard probes behave as intended. The combination logic is correct. The findings concern defaults and labels only.
The repository was on branch claude/tempestos-v0260-test-readiness-s4jwh1 (VERSION 0.26.0), not release/v0.26.0. The code under test is the same version.

## Method re-derived
- ULS 6.10: Σ γ_G G + γ_Q K_FI Q_1 + Σ γ_Q K_FI ψ_0 Q_i (BS EN 1990:2002+A1:2005 cl. 6.4.3.2, Eq. 6.10).
- ULS 6.10a: all variable actions with ψ_0. ULS 6.10b: ξ γ_G,sup on unfavourable permanent actions, one leading variable (Eq. 6.10a, 6.10b). The less favourable governs.
- SLS characteristic Σ G + Q_1 + Σ ψ_0 Q_i (Eq. 6.14b); frequent Σ G + ψ_1 Q_1 + Σ ψ_2 Q_i (Eq. 6.15b); quasi-permanent Σ G + Σ ψ_2 Q_i (Eq. 6.16b).
- Accidental Σ G + A_d + ψ_1 Q_1 + Σ ψ_2 Q_i (Eq. 6.11b, cl. 6.4.3.3).
- A permanent action takes γ_G,sup if it adds to the effect sought, else γ_G,inf. A variable action enters only if it adds. Each variable is tried as leading (Annex A1 Table A1.2(B)).
- ψ values: Annex A1 Table A1.1 recommended set. I checked all 12 categories against my own knowledge of the table.
- Maximum and minimum are separate searches. The minimum flips the sign of all actions, so favourable and unfavourable swap.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-610-single | E_d_max | 21 | 21 | 0 | 1e-9 | PASS |
| ka-610-single | E_d_min | 10 | 10 | 0 | 1e-9 | PASS |
| ka-610-single | E_k_max | 15 | 15 | 0 | 1e-9 | PASS |
| ka-610-single | E_fr_max | 12.5 | 12.5 | 0 | 1e-9 | PASS |
| ka-610-single | E_qp_max | 11.5 | 11.5 | 0 | 1e-9 | PASS |
| ka-610ab-single | E_d_max | 18.975 | 18.975 | 0 | 1e-9 | PASS |
| ka-610-two-variables | E_d_max | 24.75 | 24.75 | 0 | 1e-9 | PASS |
| ka-610-two-variables | E_k_max | 17.5 | 17.5 | 0 | 1e-9 | PASS |
| ka-uplift | E_d_min | -20 | -20 | 0 | 1e-9 | PASS |
| ka-uplift | E_d_max | 13.5 | 13.5 | 0 | 1e-9 | PASS |
| ka-designers-guide | E_d_max | 37.65 | 37.65 | 0 | 1e-9 | PASS |

ka-designers-guide stays Unconfirmed against the book. The arithmetic is right: 6.10a gives 37.65 and 6.10b gives 37.2 kN/m. I did not check the book.

## Extra probes (product vs independent)
The script compares all eight envelope values (E_d, E_k, E_fr, E_qp, max and min) per probe. Units: kN-type.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| L1 UK office | G 18, B 10 kN/m, 6.10a/b, ξ 0.925 | E_d_max | 37.4775 | 37.4775 | 2e-16 | PASS |
| L2 mixed | G 12, G -3, B 6, wind -8, snow 4 | E_d_max / E_d_min | 25.5 / -4.05 | same | <1e-15 | PASS |
| L3 accidental | G 10, B 5, wind 4, impact 30 | E_acc_max / min | 42.5 / 10 | same | 0 | PASS |
| L4 EQU-like | γ_G 1.1/0.9, K_FI 1.1, uplift -20, roof Q 3 | E_d_max / min | 15.95 / -24 | same | 0 | PASS |
| L5 no permanent | wind 5, thermal -2 kN·m | E_d_max / min | 7.5 / -3 | same | 0 | PASS |
| L6 storage | G 25, E 40, snow>1000 m 6, wind 9, 6.10a/b | E_d_max | 108.4875 | 108.4875 | 0 | PASS |
| L7 user ψ | wind 0.5/0.2/0, B office | E_d_max | 31.8 | 31.8 | 0 | PASS |
| L9 very large | values to 4e6 | E_d_max | 8.9475e6 | 8.9475e6 | 0 | PASS |
| L10 negative G | G -10, B +5 | E_d_max / min | -2.5 / -13.5 | same | 0 | PASS |
| L11 accidental only | A +20, G 10 | E_acc_max | 30 | 30 | 0 | PASS |
| L12 two accidentals | A -20, A2 +15, G 10, B 5 | E_acc_max / min | 27.5 / -10 | same | 0 | PASS |
| Guards | 0 rows; 31 rows; blank ψ cell | notice | Block; Block; Block | n/a | n/a | PASS |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| 6.10, 6.10a/b, SLS, accidental | EN 1990 Eq. 6.10-6.16b | Same terms and factors | Yes |
| Favourable and unfavourable permanent | γ_G,sup if it adds, γ_G,inf if it opposes | Chosen per action by sign against the direction sought | Yes |
| ξ | Applies to unfavourable permanent actions only | ξ times γ_G,sup only | Yes |
| Leading variable | Each tried; worst governs | Each tried; MaxBy or MinBy per family | Yes |
| Variable that opposes the effect | Not included | Excluded by sign test | Yes |
| K_FI | Multiplies the unfavourable actions (EN 1990 Annex B) | Multiplies γ_Q and, since DA-17, γ_G,sup in ULS | Yes (-03 fixed) |
| Accidental | One accidental action at a time | One per combination; ψ_1 on the leader | Yes |
| ψ table | Table A1.1 recommended | All 12 rows match | Yes |
| User ψ record | Replaces the set | Replaces; a missing category blocks; a blank cell blocks | Yes |
| Row limit, empty table | 30 rows, at least 1 | Both blocked | Yes |
| Default ξ | Recommended 0.85; UK NA 0.925 | 0.85, warned as template default | See -01 |
| Default ψ | Recommended; UK NA differs | Recommended, stated in assumption A7 | See -02 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 614ddf89 | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|---|
| P1-struct.load-combinations-01 | Low | Medium (upgraded: correct for the declared EN set, but up to 5.6 % unconservative for a UK user; the member sheet defaults to UK NA, this one does not) | The default ξ is 0.85 (EN recommended). The UK NA value is 0.925. A UK user who leaves ξ at the default gets a lower 6.10b result. The warning says "template default"; it does not say "EN recommended, not UK NA". | G 18, B office 10 kN/m, 6.10a/b: default gives 35.655 kN/m. The UK NA ξ gives 37.478 kN/m. The default is 4.9 % lower. | Add a factor-set choice (UK NA or EN recommended). It should fill γ_G, γ_Q, ξ and ψ, and print the set name on the sheet. | Open. Probe: G 18, office 10, 6.10a/b with default ξ 0.85 still gives 35.655 kN/m. | Open. Probe: G 18, office 10, 6.10a/b with default ξ 0.85 still gives 35.655 kN/m. |
| P1-struct.load-combinations-02 | Low | Medium (upgraded: 7 % unconservative in the example; the sheet does label the set as recommended; UK NA values from memory, owner to confirm against licensed copy) | The recommended ψ_0 values differ from the UK NA for roofs (category H) and wind. As I recall the NA, roofs are 0.7 and wind is 0.5. Please check Table NA.A1.1. A UK user who relies on the default set gets different results. | G 10, wind 6, roof imposed 4 kN, 6.10: recommended set gives 24.9 kN. The recalled UK values give 26.7 kN. | Label the default ψ set on the sheet as "EN 1990 recommended, not UK NA". Ship the UK set as a selectable record. | Open. Probe: recommended ψ set still gives 24.9 kN for G 10, wind 6, roof 4. | Open. Probe: the recommended ψ set still gives 24.9 kN for G 10, wind 6, roof 4. |
| P1-struct.load-combinations-03 | Info | Medium (upgraded: my reading of Annex B is that K_FI multiplies γ_F of all unfavourable actions, permanent included; then ULS is about 6 % low at K_FI 1.1; owner to confirm against licensed copy) | K_FI multiplies γ_Q only, as the spec says. I could not confirm from memory whether EN 1990 Annex B Table B3 or the UK NA also applies K_FI to unfavourable permanent actions. | Source line: gQ * kfi in all ULS terms; permanent terms use γ_G only. | Owner to confirm against Annex B and the NA. Change the spec text if K_FI also applies to γ_G. | Fixed (499bfb01, DA-17). Probe: K_FI 1.1, G 20, Q 9, 6.10a/b now gives 40.095 kN (K_FI on γ_G,sup, ξ·γ_G,sup·K_FI in 6.10b). Method text updated. | Fixed (499bfb01, DA-17). Probe: K_FI 1.1, G 20, Q 9, 6.10a/b still gives 40.095 kN. |
| P1-struct.load-combinations-04 | Info | Info (confirmed) | The worked example office-floor-beam-uk-na says the ψ set equals the UK NA for category B. That is true. It is not true for categories H and wind (see -02). | Example text in StructuralWorkedExamples.cs. | Narrow the wording to "category B". | Open (Info). Worked-example wording is unchanged. | Open (Info). Worked-example wording is unchanged. |

## Notes for the owner's manual test
- Enter G 10 kN and B office 5 kN, 6.10: expect E_d_max 21, E_k 15, E_fr 12.5, E_qp 11.5 kN.
- Enter the same with 6.10a/b and ξ left at default: 6.10b gives 18.975 kN. Set ξ = 0.925: expect 19.9875 kN.
- Enter G 10 kN down and wind -20 kN: expect E_d_min -20 kN and E_d_max 13.5 kN.
- Add a second permanent row of opposite sign (G -3 kN) and check that it takes γ_G,inf in the maximum and γ_G,sup in the minimum.
- Add an accidental row (+30 kN) and check E_acc_max uses ψ_1 for the leading variable and ψ_2 for the others.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- The script is an independent Python enumeration of the combinations. It is not a port. I re-ran it: TOTAL FAILS 0, exit 0.
- I read Run line by line. The combination logic, ξ placement, sign tests and accidental loop are correct.
- I checked the recommended ψ table row by row. It matches Table A1.1.
- A blank category cell is blocked by the validator, so the `?? Permanent` fallback cannot be reached from the sheet.
- No finding is Critical or High. Three move from Low or Info to Medium.
- -01 and -02: the defaults are right for the EN set but unconservative for UK users. A named factor-set choice fixes both.
- -03: with K_FI 1.1, G 20 and Q 9 (6.10a/b), the sheet gives 37.8. K_FI on γ_G too gives 40.1.
- Residual doubt: UK NA ψ values and the K_FI scope come from memory. The owner should confirm them.

## Re-check after DA-17/DA-18 and A-4/A-5 (2026-10-24)
- Source changed (Opus review, finding 8): K_FI now also multiplies γ_G,sup in ULS 6.10, 6.10a and 6.10b. Favourable γ_G,inf, SLS and accidental terms stay unscaled.
- The combination code moved into a new `Generate` helper. A new public `FrameCombinations` feeds the S5 combinations into the S2 frame (S5 to S2 link).
- Finding 03 is Fixed. The probe with K_FI 1.1 gives 40.095 kN, the value my review predicted (40.1). Findings 01, 02 and 04 are still Open.
- Script updated: K_FI multiplies γ_G,sup in the ULS model, and probe L4 now expects 17.05 (was 15.95). It exits 0 with all other probes unchanged.
- I read `FrameCombinations` briefly and ran no probe on it. The probe tool cannot call it, so it is untested here.
- New defects: none found.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source change (a23e6e18): an assumption label was reworded, and the empty-table block now uses the standard "enter at least one row of …" text. No maths changed.
- KATs: `Probe kat` passes all 6 cases and no expected value changed. The script exits 0 with 0 fails.
- Probes re-run: default ξ (35.655 kN/m), the recommended ψ set (24.9 kN), K_FI 1.1 (40.095 kN) and the empty table (block, with the new wording). All match the earlier results.
- Findings: 4 earlier rows, 1 fixed (-03, earlier), 3 open (-01, -02 Low; -04 Info). There was no Critical or High finding.
- New defects: none found.
- Probe inputs: /tmp/claude-0/probe-inputs/struct-lc-*.json and /tmp/claude-0/recheck4/struct-bs/.
