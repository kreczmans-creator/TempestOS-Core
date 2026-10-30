# fp.oring-gland — independent re-derivation (Track P, P1)
Calculator: O-ring and seal gland, version 1.2.0. Source file: src/Tempest.Calc/Tempest.Calc.FluidPower/ORingGlandCalculator.cs (helper: ORingGlandModel.cs). Spec: docs/calcs/fluid-power/fp.oring-gland.md.
Script: tools/verification/fp.oring-gland.py (exit 0 = all KATs match; it also re-runs 30 extra probe cases, about 780 values).
Verdict: MATCH WITH FINDINGS (reviewed: one High, -01; three Medium including R1)

The arithmetic matches the spec and the KATs to 1e-9. The findings concern what the model leaves out, not what it computes.

## Method re-derived
- Ring factor f = (1 + α(T − 20 °C))·(1 + s)^(1/3). Groove metal g = 1 + α_m(T − 20 °C). The bore or rod part uses its own α_s. Spec; thermal expansion times cube root of volume swell.
- Stretch X = g·D_ref/(f·d) − 1, with D_ref the groove root (piston, face) or the rod (rod seal). Parker ORD 5700 stretch definition. An internal-pressure face seal uses the ring OD.
- Constant-volume section change r = 1 − 1/√(1 + X). Derived: the circumference grows by (1 + X), so the section area falls by 1/(1 + X).
- OD compression c = 1 − g·D_g/(f·(d + 2W)), not below zero, only where the OD is held. The section thickens by 1/√(1 − c).
- Squeeze q = 1 − h/W_eff. Parker ORD 5700 squeeze definition with the stretch-reduced section. Cold end gives the minimum, hot end the maximum.
- Groove area A = (g·b)(h) − 2(1 − π/4)(g·r_g)². Fill is ring area over groove area, in per cent.
- Hot fill uses the unstretched section and a volume basis: (π/4)(f·W)²·max(f/g_ref, 1/(1 − c))/A_min. Cold fill uses (π/4)·W_eff²/A_max.
- Gap is the diametral clearance of the sealing part and the mating part, each with its own expansion. The largest over installed, cold and hot, plus the bore growth dD_p.
- Extrusion limit: linear in pressure within a curve, linear in hardness between curves, from the user record.
- Monte Carlo uses the engine's xoshiro256** sampler. I ported it from the engine source, so the seeded counts match exactly. C_pk uses secant slopes and a root-sum-square spread.
- Units: lengths in mm, ratios as fractions, temperatures in °C, pressure in bar.

## Known-answer cases
The script prints every value (89 rows). This table gives one row per case with the worst relative difference.

| Case | Symbols | KAT expected | Independent | Worst rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-thermal-swell | 9 | see script | recomputed | 1.8e-11 | 1e-9 | PASS |
| ka-worst-case-hand | 9 | see script | recomputed | 1.6e-11 | 1e-9 | PASS |
| ka-rod-seal | 11 | see script | recomputed | 8.6e-10 | 1e-9 | PASS |
| ka-face-seal | 6 | see script | recomputed | 1.1e-11 | 1e-9 | PASS |
| ka-face-seal-internal | 12 | see script | recomputed | 1.1e-9 (c_h, KAT tol 1e-8) | 1e-9 | PASS |
| ka-gap-over-temperature | 7 | see script | recomputed | 1.0e-12 | 1e-9 | PASS |
| ka-incompressible-reduction | 3 | see script | recomputed | 4.1e-12 | 1e-9 | PASS |
| ka-linear-reduction | 1 | 1.96 % | 1.96 % | 9.1e-16 | 1e-9 | PASS |
| ka-monte-carlo-seeded | 12 (6 counts, 6 C_pk) | see script | counts exact, C_pk 1.4e-9 | 1.4e-9 | 1e-9 / 1e-7 | PASS |
| ka-monte-carlo-measured | 13 | see script | counts exact, C_pk 9.5e-10 | 9.5e-10 | 1e-9 / 1e-7 | PASS |
| ka-extrusion-record | 2 | 0.1575 mm | 0.1575 mm | 3.1e-15 | 1e-9 | PASS |
| ka-parker-quadratic (Secondary) | 2 | 2.8464 % | 2.8464 % | 1.0e-12 | 1e-9 | PASS |
| ka-stretch-rule (Secondary) | 2 | 5.8 % | 5.8 % | 3.4e-11 | 1e-9 | PASS |

KATs matched: 13 of 13 (89 of 89 values). The product probe also reports 13 of 13.
No KAT value disagrees with physics. The two Secondary cases are arithmetic only; the 0.56 % intercept of the Parker quadratic and the 6 % stretch rule need the owner's handbook.

## Extra probes (product vs independent)
All 30 probe cases agree to 5e-10 or better in every output. A selection follows. Product values come from the probe tool.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| p01 | piston, dynamic, −30 to 100 °C, swell 10 % | fill_max | 86.176173 % | 86.176173 % | 6e-11 | PASS |
| p02 | rod, Parker, 90 Shore A, 400 bar, ext record | gap_max | 0.30018 mm | 0.30018 mm | 9e-16 | PASS |
| p03 | face external, dynamic, 120 °C | squeeze_min | 15.357963 % | 15.357963 % | 2e-10 | PASS |
| p04 | face internal, swell 25 %, 150 °C | fill_max | 101.21 % | 101.21 % | 5e-10 | PASS |
| p05 | cold −50 °C, shrink −5 %, linear ν 0.5 | X_c | 6.652308 % | 6.652308 % | 5e-11 | PASS |
| p06 | piston, Parker, 12 % stretch | red_nom | 6.9536085 % | 6.9536085 % | 4e-16 | PASS |
| p07 | piston, Parker, 28 % stretch (held at 25 % value) | red_nom | 12.435 % | 12.435 % | 0 | PASS |
| p08 | piston, linear ν 0.5, 20 % stretch | fill_max | 91.424054 % | 91.424054 % | 3e-11 | PASS |
| p09 | all tolerances zero | squeeze_max | 24.533918 % | 24.533918 % | 1e-10 | PASS |
| p10 | large ring d 970, W 8 | squeeze_min | 13.34131 % | 13.34131 % | <1e-9 | PASS |
| p11 | rod, α_s 2.3e-5 against α_m 1.2e-5, 120 °C | gap_min | −0.0189425 mm | −0.0189425 mm | 7e-14 | PASS |
| p12 | ring α 8e-6 below metal α 2.3e-5 | squeeze_max | 25.83282 % | 25.83282 % | 1e-10 | PASS |
| p14 | stretch 0 %, Parker blend | X_c | 0.35341 % | 0.35341 % | 5e-11 | PASS |
| p15 | hardness 60 below softest record curve | gap_allow check | FAIL + warning | FAIL + warning | n/a | PASS |
| p17 | rod, dynamic, OD limit 4 % | c_h | 4.79205 % | 4.79205 % | <1e-9 | PASS |
| fuzz | 150 random inputs, all three seal types, all four relations | all outputs | n/a | n/a | worst 4.9e-10 | PASS |
| fuzz | 24 random Monte Carlo inputs, 3 distributions, measured rows | counts, C_pk | n/a | n/a | counts equal | PASS |
| fuzz | 25 random extrusion records | gap_allow, range flag | n/a | n/a | exact | PASS |

The three fuzz rows ran outside the script, in /tmp/claude-0/probe-inputs. The script embeds 12 of the fuzz cases.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Ring and metal factors | f with cube root of swell; g from α_m | Same; Cbrt(1 + s); T converted from K | Yes |
| Stretch and reduction | X on D_ref; r per chosen relation | Same; internal face seal clamps X at zero | Yes |
| Squeeze | 1 − h/W_eff, ends independent | Same | Yes |
| Hot fill held circumference | f/g, g of the groove metal | f/g_ref; g_ref is the rod factor for a rod seal | Yes (spec wording differs, finding 12) |
| OD compression clamp | not stated | c clamped at 0.9 without a notice | Yes (edge only) |
| Parker relation | quadratic, hold at 25 %, blend below 3 % | Same; zero for X ≤ 0 | Yes; see finding 09 |
| Gap | diametral, own expansion, + dD_p | Same; face seal ignores dD_p and α_s silently | Yes; see finding 06 |
| Extrusion lookup | linear, out of range fails | Same; hardness above the hardest curve uses it | Yes |
| Monte Carlo order | W, d, D_s, D_g, D_c, h_f, b, e_f | Same, by enum order | Yes |
| MC criteria | not listed in full | Stretch and OD use installed state only | See finding 04 |
| Guards | blocks for geometry, order, gap | Present; e_f of zero blocks | See finding 07 |
| Limits and defaults | x_hi 6 %, bands 15–30 / 10–25 % | Same | See finding 08 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
| --- | --- | --- | --- | --- | --- |
| P1-fp.oring-gland-01 | Medium | High (upgraded). Reproduced: e_f 0.5 +0.05 mm gives squeeze_min 16.023 % and a pass, the same as e_f 0.05. The ring is then not squeezed at all (h up to 3.35 mm against W_eff 3.334 mm). The KAT's own gap of 0.05 to 0.10 mm moves the minimum from 16.0 % (pass) to 13.0 % (fail against 15 %). Normal face-seal use. | Face seal squeeze and fill ignore the flange gap e_f. If the flanges are apart, the ring is compressed over h_f + e_f. The result is unconservative. | ka-face-seal inputs: squeeze_min is 16.023 % for any e_f. Adding e_f,max = 0.10 mm to the height gives 13.023 % (script note N1). | Add e_f to the compression height, or state that h_f is the total gland height and e_f is a clearance only. |
| P1-fp.oring-gland-02 | Medium | Medium (confirmed). The spec lists the limit, but no input can carry it. | No input for gland eccentricity or run-out. Minimum squeeze and maximum squeeze are both unconservative. This limit is listed in the spec but has no workaround in the sheet. | An offset of 0.05 mm changes the one-sided depth by 0.05 mm. On W = 3.5 mm that is about 1.5 points of squeeze. | Add an optional eccentricity input. Subtract it from the minimum depth and add it to the maximum depth. |
| P1-fp.oring-gland-03 | Medium | Medium (confirmed, label defect). The code compares a diametral clearance. To my knowledge the common chart reads diametral clearance; owner to confirm against licensed copy. | The permitted gap (`gap_allow`, `ext` column `gap`) does not say diametral or radial. The model compares it with the diametral clearance D_s − D_c. A radial chart value is then twice too lenient. | Probe p16: gap_allow 0.05 mm against gap_max 0.10608 mm (diametral, 56.946 − 56.84). To my knowledge the Parker extrusion chart reads diametral clearance; please confirm in your copy. | Say "diametral" in both labels and in the gap outputs. Optionally add a radial/diametral choice. |
| P1-fp.oring-gland-04 | Low | Low (confirmed). | Monte Carlo `p_stretch` and `p_od` use the installed state only. The deterministic checks also test cold-end stretch X_c and hot-end OD compression c_h. The statistics then read zero while a check fails. | p05: X_c 6.652 % FAIL against 6 %, but p_stretch 0 % and cpk 3.575. p04: c_h 10.149 % FAIL against 6 %, but p_od 0 %, cpk_od 6.98. | Add cold-stretch and hot-OD criteria to the Monte Carlo, or label the two as installed-only. |
| P1-fp.oring-gland-05 | Low | Low (confirmed). | When the ring expands less than the groove metal, the two ends are not the bounds. The sheet only warns. Maxima are understated by up to 0.7 points. | p12 (α 8e-6, α_m 2.3e-5, −40 to 150 °C): scan squeeze 17.44 to 26.11 %, fill 64.71 to 82.80 %. Product: 17.75 to 25.83 %, 65.20 to 82.10 %. | When α < α_m, evaluate all four temperature and swell corners and take the extremes. |
| P1-fp.oring-gland-06 | Low | Low (confirmed). Fix it together with -01. | For a face seal, `dD_p` and `alpha_s` are ignored with no notice. Flange separation under pressure is the usual extrusion driver of a face seal. | Probe f1: dD_p 0.05 mm entered, gap_max stays 0.1 mm. | Warn when these are entered for a face seal. Or use dD_p as flange separation added to e_f. |
| P1-fp.oring-gland-07 | Low | Low (confirmed). With -01 fixed, a zero gap is the common case and must be allowed. | The face gap e_f cannot be zero. Metal-to-metal flanges, the common case, are blocked as "zero or negative". Also u_gap reads 0 when gap_allow is 0 while the check fails. | Probe f4: e_f 0 with deviations 0 blocks. Probe f2: gap_allow 0 gives u_gap 0 and FAIL. | Allow a zero lower gap for face seals. Show u_gap as infinite when the limit is zero. |
| P1-fp.oring-gland-08 | Low | Low (confirmed). The 5 % figure is from memory; owner to confirm against licensed copy. | The default stretch limit x_hi is 6 %. The brief and common handbook guidance give about 5 %. The product itself marks the rule unconfirmed. | Probe p13: 5.85 % stretch passes. ka-stretch-rule reads 5.8 % as 0.967 of the limit. | Default to 5 %, or show the handbook edition beside the 6 % default. |
| P1-fp.oring-gland-09 | Low | Low (confirmed). | The Parker relation gives no section thickening for a compressed ring (X < 0). The other relations thicken. The Parker choice then gives the lower squeeze_max. | Hot ring 15 % swell, α 3e-4, 150 °C: squeeze_max 30.99 % (Parker) against 33.12 % (constant volume). Script note N3. | Apply the constant-volume thickening for X < 0 under the Parker option too. |
| P1-fp.oring-gland-10 | Low | Low (confirmed). | gap_min can go negative (interference of rod and housing bore at the hot end). No notice is given. | Probe p11: gap_min −0.0189 mm at 120 °C. | Warn when gap_min is below zero. |
| P1-fp.oring-gland-11 | Info | Info (confirmed). | No compression-set or stress-relaxation allowance. No back-up ring rule or hint when the extrusion check fails. Both are in the spec limitations. | Spec Limitations; Failing "Extrusion gap" check carries no advice. | Add a note on a failing extrusion check: consider back-up rings. Show the minimum squeeze as "as installed". |
| P1-fp.oring-gland-12 | Info | Info (confirmed). | The spec says g in f/g is the groove metal. The code uses the factor of the part the ring ID sits on (rod for a rod seal). The code is the better choice. | Fuzz, rod with α_s ≠ α_m: fill_max 80.75 % (code) against 80.62 % (spec reading). KATs are unaffected. | Change the spec wording to "the reference part". |
| P1-fp.oring-gland-R1 | Medium | Medium (new) | The squeeze and fill extremes use the cold and hot ends only. The installed state (20 °C, no swell) is not among them. When the range lies wholly above 20 °C, the minimum squeeze at installation and pressure test is lower than reported. | Defaults with T_min 60, T_max 100 °C and no swell: squeeze_min 18.320 %. The same with T_min 20 °C: 17.643 %. The gap already uses installed, cold and hot. | Add the installed state to the squeeze and fill extremes, as for the gap. Or warn when 20 °C lies outside T_min to T_max. |

## Notes for the owner's manual test
- Defaults with gap_allow = 0.15 mm: squeeze 16.956 to 26.957 %, fill 63.951 to 84.643 %, X_c 4.4072 %, gap 0.10608 mm.
- Hand check without thermal effects: set T_min = T_max = 20 °C, swell 0, reduction None. Expect squeeze 19.176 to 25.000 %, fill 67.513 to 78.644 %, stretch 2.083 to 3.831 %.
- Face seal (ka-face-seal inputs): change e_f from 0.05 to 0.5 mm. Squeeze does not move (finding 01). Expect 16.023 % minimum.
- Extrusion: enter H = 80, p = 150 bar with the ka-extrusion-record table. Expect 0.1575 mm. Raise p above the last point. Expect a failing check and an `extrusion-range` warning.
- Rod seal (ka-rod-seal inputs): expect OD compression 2.714 % installed and 5.069 % at the hot end; squeeze maximum 26.820 %.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24, release 2f53791c.
- Script re-run: exit 0. It is an independent model; the sampler port is needed to match seeded counts.
- Reproduced -01 with the probe (e_f 0.5 mm). Raised it to High: a face seal can pass with no squeeze.
- Reproduced -02 and -03 from the template and p16. Both stay Medium.
- New -R1 (Medium): the installed state is missing from the squeeze and fill extremes.
- Verdict changed: one High finding.
- Residual doubt: the extrusion chart basis and the 5 % stretch rule need the owner's handbook.
