# elec.fault-protection — independent re-derivation (Track P, P1)
Calculator: Fault protection: Zs, disconnection, adiabatic, fault level, version 1.1.0 (1.0.0 at first review). Source file: src/Tempest.Calc/Tempest.Calc.Electrical/FaultProtectionCalculator.cs. Spec: docs/calcs/electrical/elec.fault-protection.md.
Script: tools/verification/elec.fault-protection.py (exit 0 = all KATs match; 67 checks, 15 KAT rows and 52 extra rows).
Verdict: MATCH WITH FINDINGS (reviewed: five High)

The formulas reproduce all five KATs. The High findings concern what the model leaves out, not the formulas. They are the conductor resistance (01), the missing maximum-fault energy check for both conductors (02, 03) and a curve that does not bracket I_f (09, R1).

## Method re-derived
- Loop impedance: Z_s = Z_e + ρ₂₀·L·(1/S_line + 1/S_cpc)·[1 + α(θ − 20)]. Units: ρ in Ω·mm, L in m, S in mm². Source: BS 7671:2018+A2:2022 reg. 411.4.4 and the usual hot-conductor multiplier.
- Fault current: I_f = C_min·U₀/Z_s. Z_s,max = C_min·U₀/I_a, with C_min = 0.95 (BS 7671 reg. 411.4.4).
- MCB trip current I_a = 5, 10, 20 × I_n for types B, C, D. These are the upper limits of the instantaneous band in BS EN 60898-1.
- Fuse or MCCB: I_a at t_req and t_d at I_f by log-log interpolation of user points, without extrapolation.
- Adiabatic: S_min = √(I²t)/k (BS 7671 reg. 543.1.3 and 434.5.2). I²t = I_f²·t_d from the curve, or I_f²·0.1 s for an MCB.
- k = √[Q_c(β + 20)/ρ₂₀ · ln(1 + (θ_f − θ_i)/(β + θ_i))] (IEC 60364-5-54 Annex A). My values: 114.84 (PVC, 70 to 160 °C), 142.87 (XLPE, 90 to 250 °C), 76.09 (aluminium).
- Fault level: Z_T = u_k·U_n²/S_r with U_n = √3·U₀. I_k3 = c·U_n/(√3·Z_T) (IEC 60909-0:2016 cl. 6.3.1 for Z_T; cl. 5.3.1 and Table 1 for c, owner to confirm).

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-mcb-b32 | Z_s | 0.7900823 | 0.7900823 | 3.5e-08 | 1e-06 | PASS |
| ka-mcb-b32 | I_f | 276.5535 | 276.5535 | 8.1e-08 | 1e-06 | PASS |
| ka-mcb-b32 | Z_s_max | 1.365625 | 1.365625 | 0 | 1e-09 | PASS |
| ka-mcb-b32 | u_Zs | 0.5785499 | 0.5785499 | 8.2e-08 | 1e-06 | PASS |
| ka-mcb-b32 | k | 114.8361 | 114.8361 | 2.4e-07 | 1e-06 | PASS |
| ka-mcb-b32 | S_min | 0.7615538 | 0.7615538 | 1.2e-08 | 1e-06 | PASS |
| ka-fuse-curve | I_a | 220 | 220 | 0 | 1e-09 | PASS |
| ka-fuse-curve | Z_s_max | 0.9931818 | 0.9931818 | 1.8e-08 | 1e-06 | PASS |
| ka-fuse-curve | t_d | 0.1715774 | 0.1715774 | 4.4e-09 | 1e-06 | PASS |
| ka-fuse-curve | S_min | 0.9975407 | 0.9975407 | 5.1e-09 | 1e-06 | PASS |
| ka-fault-level | Z_T | 0.01587 | 0.01587 | 0 | 1e-06 | PASS |
| ka-fault-level | I_k3 (kA) | 15.94203 | 15.94203 | 6.4e-08 | 1e-06 | PASS |
| ka-fault-level | u_Icn | 0.6376812 | 0.6376812 | 6.4e-08 | 1e-06 | PASS |
| ka-wiring-matters-b32 | Z_s_max | 1.37 | 1.365625 | 3.2e-03 | 5e-03 | PASS |
| ka-schneider-k | k | 143 | 142.874 | 8.8e-04 | 5e-03 | PASS |

All 15 rows match, and the product KAT run also passes 5/5 cases. Two cases are Unconfirmed. I could not check the IET article or the Schneider guide. The two values agree with my own physics. The 1.37 Ω figure is the rounded form of 1.3656 Ω, and the XLPE k is 143 in round numbers.

## Extra probes (product vs independent)
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| x1 | C16, L 50 m, 1.5/1.0 mm² | Z_s / u_Zs | 2.06907 Ω / 1.51511 | 2.06907 Ω / 1.51511 | <1e-9 | PASS |
| x2 | D32 default circuit | Z_s_max / u_Zs | 0.341406 Ω / 2.3142 | 0.341406 Ω / 2.3142 | <1e-9 | PASS |
| x3 | XLPE 90→250 °C, θ 90 °C | Z_s / k / S_min | 0.81899 Ω / 142.874 / 0.59050 mm² | same | <1e-9 | PASS |
| x4 | I2t_let 20 kA²s | S_min | 1.23151 mm² | 1.23151 mm² | 2e-10 | PASS |
| x5 | L 0.1 m, Z_e 0.01 Ω | I_f / S_min | 17909.2 A / 49.317 mm² | same | <1e-9 | PASS |
| x6 | Fuse curve, t_req 5 s, L 30 m | I_a / t_d | 120 A / 0.42921 s | same | <1e-9 | PASS |
| x7 | Two-point curve | I_a / t_d | 226.048 A / 0.178959 s | same | <1e-9 | PASS |
| x8 | U₀ 1000 V, 5000 kVA, 1 % | Z_T / I_k3 | 0.006 Ω / 183.333 kA | same | <1e-9 | PASS |
| x9 | U₀ 120 V, 10 kVA, 4 %, c 1.0 | I_k3 | 0.694444 kA | 0.694444 kA | 6e-11 | PASS |
| x10 | Aluminium 16/16 mm² | k / Z_s | 76.0867 / 0.434898 Ω | same | <1e-9 | PASS |
| x11 | C_min 0.9, θ 20 °C | Z_s_max | 1.29375 Ω | 1.29375 Ω | 0 | PASS |
| x12 | C_min 1.0 | Z_s_max | 1.4375 Ω | 1.4375 Ω | 0 | PASS |
| x13 | U₀ 400 V, five-point curve | t_d | 0.030161 s | 0.030161 s | 7.6e-07 | PASS |
| x14 | Z_s set equal to Z_s,max | u_Zs | 1.0, check passes | 1.0 | 0 | PASS |

The product also blocks a curve that does not bracket t_req, and blocks θ_f not above θ_i. Both blocks behave as the spec says.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| R₁+R₂ and Z_s | ρ₂₀·L/S, multiplier 1 + α(θ − 20) | Same, with ρ₂₀ = 17.241e-6 Ω·mm | Formula yes; resistance value see P1-01 |
| Z_s,max | C_min·U₀/I_a | Same | Yes |
| MCB I_a | 5, 10, 20 × I_n | Same, no I_n limit | Yes; see P1-10 |
| Interpolation | Log-log, no extrapolation | Log-log, null outside points | Yes |
| t_d for MCB | At most 0.1 s when I_f ≥ I_a | Fixed 0.1 s when I_f ≥ I_a | Yes |
| k | Derived formula | Same, unit conversions correct | Yes |
| Adiabatic energy | I²t at the governing fault | I_f²·t from earth fault at the far end only | Partly; see P1-02, -03, -04 |
| Fault level | Z_T, I_k3 | Same, U_n = √3·U₀, no network, no K_T | Yes, within 2 % and conservative |
| Applicability rule "curve" | Block if I_f is outside the points | Warning only for I_f; Block for t_req | No; see P1-09 |
| Checks | Z_s, t_d, CPC, line, I_cn | Same five, I_cn only with S_r and u_k | Partly; see P1-08 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 614ddf89 | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|---|
| P1-elec.fault-protection-01 | High | High (confirmed). Probe gives R₁+R₂ 0.3678 Ω at 20 °C. IEC 60228 maxima are 5 to 7 % higher at every size, so Z_s is low in normal use. | Default ρ₂₀ is the pure-copper value. Real conductors have higher maximum resistance, so R₁+R₂ and Z_s come out low. This is unconservative. | 20 m of 2.5/1.5 mm²: product 0.3678 Ω at 20 °C. IEC 60228 class 1 maxima (7.41 and 12.1 Ω/km) give 0.3902 Ω, which is 6.1 % higher. Z_s at 70 °C is 0.7901 Ω against 0.8182 Ω, 3.6 % low. | Add a per-size resistance in mΩ/m (IEC 60228 maxima) for R₁+R₂, or an effective ρ near 18.5e-6 Ω·mm. Keep 17.241e-6 for k only. Add a warning on the pure-copper value. | Open. Probe unchanged: R₁+R₂ 0.3678 Ω at 20 °C (ρ₂₀ 17.241e-6). | Open. Probe: R₁+R₂ is still 0.3678 Ω at 20 °C (ρ₂₀ 17.241e-6). No warning. |
| P1-elec.fault-protection-02 | High | High (confirmed). The line check never sees the maximum fault. The warning wrongly calls the 0.1 s energy conservative. | The line-conductor check (u_line, labelled reg. 434.5.2) uses the earth-fault I_f from the far end. It ignores the short-circuit let-through. I_k3 is computed but never used. The warning calls I_f²·0.1 s "conservative", which is false for the line at the origin. | B32 MCB, 2.5 mm² line, 500 kVA 5 % supply: u_line = 0.30 with no I2t_let. With an assumed 90 kA²s let-through, u_line = 1.045 and the check fails (S_min 2.612 mm²). | For the line check, use I_k3 with the device let-through I²t. Require I2t_let for an MCB when S_r is given. Reword the warning to say it covers the far-end fault only. | Open. Probe with 90 kA²s, 500 kVA, 5 %: u_line still uses I_f; I_k3 15.94 kA unused. Warning text unchanged. | Fixed (874bc235). Probe: B32 with no I2t_let no longer shows u_line or the line check. The warning says the check is not made. With 90 kA²s, u_line is 1.045 FAIL. Residual: I_k3 is still unused. |
| P1-elec.fault-protection-03 | Medium | High (upgraded). Same gap as 02, and the CPC is the smaller conductor. A 1.5 mm² CPC holds only 29.8 kA²s with k 115. MCB let-through near the origin can exceed this. Fix together with 02. | The CPC check covers only the far-end minimum current. Faults near the origin carry far higher current and the MCB let-through, and are not checked. | Same 90 kA²s example gives u_cpc = 1.74 (S_min 2.612 mm² against 1.5 mm²). Without I2t_let the sheet shows 0.51. | State the limit on the sheet. Add an optional second check at the maximum earth-fault current with the let-through I²t. | Open. Same probe: u_cpc 1.74 only with a typed I2t_let; no second check at the origin. | Partly fixed (874bc235). The warning and method now say the cpc check uses I_f²·0.1 s at the end-of-line fault only. The base case still shows u_cpc 0.508 pass. There is no second check at the origin. |
| P1-elec.fault-protection-04 | Medium | Medium (confirmed). Probe: C16, 50 m gives u_cpc 0.29 pass with I_f 105.6 A < I_a 160 A. The warning also says the disconnection is below 0.1 s, which is false. Total verdict fails on u_Zs. | When I_f is below the tripping current, the adiabatic checks still use 0.1 s and report pass with the "conservative" warning. The device is then in its slow region. | C16 MCB, L 50 m, 1.5/1.0 mm²: I_f = 105.6 A < I_a = 160 A. S_min = 0.291 mm², u_cpc = 0.29 pass. A 5 s disconnection gives 2.06 mm². Z_s fails, so the total verdict fails. | When u_Zs > 1, mark the adiabatic rows "not evaluated". Do not show a pass with a 0.1 s assumption. | Fixed (499bfb01, DA-17). C16, 50 m now gives no S_min and no adiabatic pass; the sheet fails 'Disconnection achieved' and warns. | Fixed (499bfb01, DA-17), re-verified. Probe: C16, 50 m gives no S_min, no adiabatic pass, a failed 'Disconnection achieved' and a warning. |
| P1-elec.fault-protection-05 | Medium | Medium (confirmed). | t_req is a free input (0.1 to 5 s, default 0.4 s). It is not linked to U₀ or circuit type. No notice appears for 5 s on a final circuit, or for 0.4 s at U₀ = 400 V. | Fuse curve, t_req 5 s: I_a 120 A, Z_s,max 1.82 Ω, no notice. U₀ = 400 V keeps 0.4 s with no notice. | Warn when t_req exceeds 0.4 s and U₀ ≤ 230 V. Warn when U₀ > 230 V and t_req is above the Table 41.1 time. Check those times against the owner's copy. | Open. Probe: t_req 5 s and U₀ 400 V at 0.4 s still give no notice. | Open. Probe: t_req 5 s on a fuse curve, and U₀ 400 V at 0.4 s, still give no notice. |
| P1-elec.fault-protection-06 | Medium | Medium (confirmed). Unconservative by about 3.7 % with no notice. | θ for Z_s and θ_i for the adiabatic check are separate inputs. XLPE at 90 °C with the default θ of 70 °C gives Z_s about 4 % low, with no notice. | θ_i 90, θ_f 250, θ 70: Z_s = 0.7901 Ω. With θ 90 °C: 0.8190 Ω (3.7 % higher). | Warn when θ < θ_i, or default θ to θ_i. | Open. Probe: θ_i 90 with θ 70 gives Z_s 0.7901 Ω and no notice. | Open. Probe: θ_i 90 °C with θ 70 °C still gives Z_s 0.7901 Ω and no θ notice. |
| P1-elec.fault-protection-07 | Low | Low (confirmed). | C_min accepts 0.9 to 1.0 with no notice. BS 7671 fixes 0.95. A value of 1.0 raises Z_s,max by 5.3 %. | C_min 1.0: Z_s,max = 1.4375 Ω against 1.3656 Ω. | Warn when C_min is above 0.95. | Open. Probe: C_min 1.0 gives Z_s,max 1.4375 Ω with no notice. | Open. Probe: C_min 1.0 still gives Z_s,max 1.4375 Ω with no notice. |
| P1-elec.fault-protection-08 | Low | Low (confirmed). | I_cn without S_r and u_k gives no breaking-capacity check and no notice. | I_cn 6 kA alone: four checks listed, none for I_cn. | Add a warning that I_cn needs S_r and u_k. | Open. Probe: I_cn 6 kA alone still adds no check and no notice beyond the generic record notice. | Open. Probe: I_cn 6 kA alone still adds no check and no notice. |
| P1-elec.fault-protection-09 | Low | High (upgraded). Unconservative when the last point lies above 0.1 s. See the reviewer's evidence in R1. | The rule "curve must bracket the fault current" is declared Block. The code only warns. The t_d check is skipped when I_f is outside the points. | I_f = 17.9 kA above the last point (700 A): warning only, no t_d, I_f²·0.1 s used. | Align the code with the rule, or change the rule text to Warning. | Partly fixed: above the curve the code now uses the fastest time as a bound and warns. The rule text still says Block for I_f; code warns only. | Partly fixed (499bfb01), unchanged. Probe: above the curve the code warns and uses the fastest time as a bound. The rule text still says Block. |
| P1-elec.fault-protection-10 | Low | Low (confirmed). | MCB types B, C, D accept I_n up to 1600 A. BS EN 60898-1 MCBs stop at 125 A. Larger devices need the curve option. | B, I_n 1600 A gives I_a 8000 A with no notice. | Warn when an MCB type is chosen above 125 A. | Open. Probe: B, I_n 1600 A still gives I_a 8000 A with no MCB size notice. | Open. Probe: type B with I_n 1600 A still gives I_a 8000 A and no size notice. |
| P1-elec.fault-protection-11 | Low | Rejected. In IEC 60909-0:2016, cl. 5.3.1 with Table 1 holds the voltage factor c, and cl. 6.3 holds transformers. The product label cl. 5.3 is right. Owner to confirm against licensed copy. | The clause label for I_k3 is "IEC 60909-0:2016 cl. 5.3, Voltage factor c". As I know it, cl. 5.3 is the transformer impedance and the factor c is in cl. 4.3.1. | Template CalcClauseRef. Not checked against a copy. | Check the clause in the owner's copy and correct the label. | Rejected (no change). Label not touched by the diff. | Rejected (no change). The clause label is not touched. |
| P1-elec.fault-protection-12 | Info | Info (confirmed). | The fault level is conservative by about 2 %. U_n = √3·230 V = 398.4 V instead of 400 V gives +0.4 %. K_T and the network impedance are omitted. | 500 kVA, 5 %: product 15.942 kA. U_n = 400 V gives 15.877 kA. K_T near 1.015 would lower it by about 1.5 %. | None. Optionally document it. Single-phase earth faults on a Dyn transformer can exceed I_k3 and are not covered. | Open (Info). Fault level code moved into FaultLevel(); maths unchanged. | Open (Info). Fault level maths unchanged: I_k3 15.942 kA. |
| P1-elec.fault-protection-13 | Info | Info (confirmed). | Small constant differences. α = 0.00393 gives a multiplier of 1.1965 at 70 °C against the usual 1.20 (0.3 % low on R₁+R₂). Derived k is 0.1 % under the tabulated 115 and 143, which is conservative. | Z_s 0.7901 Ω with 1.1965. About 0.7914 Ω with 1.20. | None needed. For conductors above 300 mm² with PVC the user must set θ_f = 140 °C. | Open (Info). Constants unchanged. | Open (Info). Constants unchanged. |
| P1-elec.fault-protection-R1 | High | High (new) | When I_f lies above the highest curve current, the code uses I_f²·0.1 s. The real time can be as long as the last point's time. A curve that stops above 0.1 s then gives an unconservative adiabatic pass. The warning also wrongly says the disconnection is below 0.1 s. | Probe: points (5 s, 120 A) and (0.4 s, 220 A), defaults otherwise. I_f 276.55 A is above 220 A. Product S_min 0.7616 mm², u_cpc 0.508 pass. The fuller curve gives t_d 0.1716 s and S_min 0.998 mm². The bound t ≤ 0.4 s gives S_min 1.523 mm², u_cpc 1.015, fail. | Block when I_f is outside the points, as the rule already says. At the least, use the time of the last point as t in I²t. | Fixed (499bfb01, DA-17). Probe: S_min 1.5231 mm², u_cpc 1.0154 fail, t_d 0.4 s bound, warning shown. | Fixed (499bfb01, DA-17), re-verified. Probe: curve ending at 0.4 s gives S_min 1.5231 mm², u_cpc 1.0154 FAIL, t_d 0.4 s bound and a warning. |
| P1-elec.fault-protection-E1 | Info | — | The device_code check separates MCB types B, C, D from curve devices. Code 4 covers a gG fuse, a semi-enclosed fuse, an MCCB and an MPCB. Any curve then passes the check. | Probe: device_code 4 with a fuse curve runs. E4 gives code 4 for a semi-enclosed fuse (installation context, 16 A). E5 cannot tell which device the curve belongs to. | Carry a finer code, or state on the sheet that the curve must belong to the cable-step device. | — | Open (new at 0772fcf8). |
| P1-elec.fault-protection-E2 | Low | — | For an MCB the line check now needs a typed I2t_let. With S_r and u_k given the sheet knows I_k3. It still drops the line check and shows only a warning. A user who reads the passes alone sees three checks and no gap. | Probe: B32 with S_r 500 kVA, u_k 5 %, no I2t_let: u_Zs, t_d and u_cpc pass. The line check and u_line are absent. The let-through warning is the only sign. | With S_r and u_k given and I2t_let blank, add a failing 'line check not made' row. Or require I2t_let. | — | Open (new at 0772fcf8). |

## Notes for the owner's manual test
- Enter 20 m, 2.5/1.5 mm², Z_e 0.35 Ω, B32. Expect Z_s 0.790 Ω, I_f 276.6 A, Z_s,max 1.3656 Ω, S_min 0.762 mm². Compare Z_s with your own table of R₁+R₂ values in mΩ/m. Expect about 3.6 % difference (finding 01).
- Select D32 on the same circuit. Expect Z_s,max 0.3414 Ω and the loop check to fail with u_Zs 2.31.
- Enter 90 000 A²s as I2t_let, and add S_r 500 kVA, u_k 5 %. Expect S_min 2.612 mm² and both conductor checks to fail (findings 02, 03).
- Use the fuse points (5 s, 120 A), (0.4 s, 220 A), (0.1 s, 320 A) and (0.01 s, 700 A). Expect I_a 220 A, t_d 0.1716 s and S_min 0.9975 mm².
- Set 500 kVA, 5 %, I_cn 25 kA. Expect Z_T 0.01587 Ω, I_k3 15.942 kA, u_Icn 0.638.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script: 56/56 pass. It is an independent derivation in Python, not a copy of the C#.
- I reproduced 04 with the probe: C16, 50 m, 1.5/1.0 mm² gives u_cpc 0.29 pass while I_f is below I_a.
- I reproduced R1 with the probe: a two-point curve ending at 0.4 s passes the CPC at 0.51. The true bound is 1.02.
- I read Run line by line. The k units, the log-log interpolation and Z_s,max agree with my own working.
- Changes: 03 and 09 raised to High. 11 rejected, since the product's clause label looks right. R1 added.
- I corrected the IEC 60909-0 clause for c in the method section and in the script docstring.
- Residual doubt: the IEC 60228 maxima and the clause numbers are from memory. Owner to confirm against licensed copy.
- Residual doubt: typical MCB let-through values quoted in 03 are indicative only. Use the maker's energy-class data.

## Re-check after DA-17/DA-18 and A-4/A-5 (2026-10-24)
- Source changed in 499bfb01 (DA-17). A fault current above the highest curve point now uses the fastest time as a bound.
- With no disconnection time the adiabatic check is now skipped.
- The code now has a `FaultLevel` helper and a new rule `adiabatic` (Warning). An early return fails "Disconnection achieved".
- Fixed: 04 and R1, re-run with the probe at 614ddf89. 09 is partly fixed. Ten rows are unchanged and Open. Row 11 stays Rejected.
- Script updated: it models both new behaviours and adds probe groups x15 and x16 and two asserts. It now runs 63 checks and exits 0.
- New defects: none found. The early return omits the S_min port, which is correct while the check is skipped.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed (874bc235): version 1.1.0. The line-conductor check (reg. 434.5.2) is made only with a typed let-through I²t, or a curve time of 0.1 s or more. The warning no longer calls the 0.1 s energy conservative.
- New input device_code (1 to 4). The sheet blocks when the chosen device differs from the code carried from E4. I checked match, mismatch, fraction, 0 and 5.
- The check "Disconnection time within required" now uses the ratio form (t_d against t_req). Results are the same.
- Probe at 0772fcf8: 02 is Fixed. 03 is partly fixed. 04 and R1 stay Fixed. Eight rows are Open, 09 is partly fixed and 11 is Rejected. No KAT value changed (5 of 5 pass).
- Script updated: new cases x17 and x17b cover u_cpc and u_line with a typed I²t. A note covers the missing line check.
- It runs 67 checks and exits 0.
- New findings: E1 (Info) and E2 (Low). See the table.
