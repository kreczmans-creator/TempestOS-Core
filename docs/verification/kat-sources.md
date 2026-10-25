# KAT source verification

Date: 2026-10-24. Branch: release/v0.26.0. Scope: the 14 Unconfirmed KATs and 21 Secondary KATs.
I checked each KAT against open sources only: manufacturer pages, publisher or course notes, and open standards excerpts.
I recorded values, locations and URLs only. I copied no book text. Some sites blocked direct fetches, as noted per row.

Outcome rules. **Confirmed**: an open source, or an independent recomputation from an openly published relation, agrees with every checked value.
**Still unconfirmed**: some value or example in the cited source could not be read openly. **Contradicted**: an open source prints a different value.
Both contradictions below are provisional. They rest on open secondary sources, and the owner should settle them with the book.

## Summary

| Group | Confirmed | Still unconfirmed | Contradicted | Total |
|---|---|---|---|---|
| Unconfirmed KATs | 8 | 5 | 1 | 14 |
| Secondary KATs | 20 | 0 | 1 | 21 |
| Addendum B-0/B-1 cases (90d15f11) | 7 | 2 | 1 | 10 |
| Addendum B-2/B-3 cases (2f53791c) | 3 | 2 | 1 | 6 |
| All | 38 | 9 | 4 | 51 |

The first three rows cover the earlier 35 cases, checked on 2026-10-24. The two addendum rows are detailed in the last two sections.
The B-2/B-3 section also holds a relation-level check of the Analytic cases in seven calculators. Those checks are not in the totals.

Of the earlier 28 confirmed, 3 rest on a manufacturer or IET page: B32 Z_s, ΔT 30 factor and SKF 6206.
The other 25 use open course notes, open secondary pages or recomputation. Each row says which.
Not covered: `ka-rectangle`, `ka-tube` and `ka-two-span-release`. They are analytic or statics-only and cite no book example.

## Main table

Source column abbreviations: "extract" means a search-tool extract of the page, because direct fetch failed or the page is script-rendered.

| Calculator | Case | Tag | Value(s) checked | Source checked (title, edition, URL, page/table) | Outcome | Notes |
|---|---|---|---|---|---|---|
| bs.heat-exchanger | ka-incropera-11-1 | Unconfirmed | q 8524 W; T_c,o 40.2 °C; ΔT_lm 43.2 K; UA 197.31 W/K; inputs 0.1 and 0.2 kg/s, 100 → 60 °C, 30 °C | Incropera et al., Fundamentals of Heat and Mass Transfer, Example 11.1, p. 680, via the FOSSEE Scilab/Python port: https://tbc-python.fossee.in/convert-notebook/Fundamentals_of_Heat_and_Mass_Transfer/Chapter_11.ipynb | Still unconfirmed | The port confirms the example, page and inputs. It prints only Re values and L = 65.71 m. The port's U is about 38.2, not 38.1. I recomputed q, T_c,o and ΔT_lm from the inputs: they match. The book's printed numbers and edition are unseen. |
| bs.psychrometrics | ka-ashrae-example-1 | Unconfirmed | W 6.5 g/kg; h 56.7 kJ/kg; v 0.896 m³/kg; t_d 7 °C; φ 14 % | HKU MEBS7012 "Exercises on Psychrometry (Solutions)", Exercise 1, which reproduces the ASHRAE Ch. 1 Example 1 text: http://ibse.hk/MEBS7012/Psych_exercise01_solutions.pdf | Confirmed | Printed values are chart readings. The KAT's W 6.40 is 1.5 % below 6.5 and inside its 2 % tolerance. My ASHRAE-equation recomputation gives W 6.40, h 56.73, v 0.8962, RH 13.98 %, t_d 7.43 °C. KAT t_d 7.4 versus printed 7 is chart rounding. The notes do not name the Handbook edition. |
| bs.sealed-system | ka-manufacturer-dt30 | Unconfirmed | f(ΔT 30 K) = 0.51 | Stelrad Radiator Book, Feb 2020, technical information page, operating factor table: https://www.stelrad.com/wp-content/uploads/2015/04/Pages-from-24321_The-Stelrad-Radiator-book_Feb-2020.pdf | Confirmed | Printed factor for ΔT 30 °C is 0.515. The KAT 0.51 sits inside its 0.01 tolerance. (30/50)^1.3 = 0.5148 reproduces the table. Consider storing 0.515. A web table (extract) lists 0.513. |
| elec.fault-protection | ka-wiring-matters-b32 | Unconfirmed | Z_s,max = 1.37 Ω for 32 A type B at 230 V | IET, "Reduction in maximum values of earth fault loop impedance in BS 7671" (Jan 2015), p. 2: https://electrical.theiet.org/media/1442/maximum-zs-values.pdf. Also Wiring Matters issue 100, May 2024: https://electrical.theiet.org/wiring-matters/years/2024/100-may-2024/why-are-the-values-of-maximum-earth-fault-loop-impedance-different/ | Confirmed | Both IET pages state 1.37 Ω (BS 7671 Table 41.3). The formula Z_s = U0·C_min/I_a with C_min 0.95 is stated. 0.95 × 230 / 160 = 1.366. I did not find the cited March 2023 article. Cite one of these two instead. |
| elec.fault-protection | ka-schneider-k | Unconfirmed | k = 143 for Cu XLPE 90 → 250 °C (and 115 for PVC 70 → 160 °C) | Schneider Electric, Electrical Installation Guide, "Sizing of protective earthing conductor", Fig. G60 (IEC 60364-5-54 Annex A): https://www.electrical-installation.org/enwiki/Sizing_of_protective_earthing_conductor | Confirmed | The site blocked direct fetch (403). The extract shows Cu multi-core cable k = 115 (PVC) and 143 (XLPE). I recomputed with Q_c 3.45e-3, β 234.5, ρ20 17.241e-6: k = 114.84 and 142.87. An open article (https://elek.com/articles/protective-earth-cable-sizing/) uses other constants and gets 139.4. That is a different method, not a contradiction. Re-open the Schneider page once. |
| elec.load-budget | ka-peukert | Unconfirmed | t = H (C/(I H))^k; 26.153 A·h and 43.015 A·h | Peukert's law, Wikipedia: https://en.wikipedia.org/wiki/Peukert%27s_law. Also Battle Born (extract): https://battlebornbatteries.com/peukert-effect | Confirmed | Same relation and symbols. Wikipedia does not cite Linden. A patent extract cites Linden p. 3-9 for the equation. I recomputed 5·20·0.2^(1/1.2) = 26.1532 and /0.608 = 43.0151. Linden 5th ed. page is unseen. Low risk. |
| elec.load-budget | ka-psu-derating | Unconfirmed | f = 0.75 at 70 °C for QUINT4-PS/1AC/24DC/10 | Phoenix Contact QUINT4 datasheets for 2909575 and 2904597 (1.3 A, hosted by RS): https://assets.rs-online.com/v1700070897/Datasheets/0c438cc470945c28e8d92807be04b5ff.pdf | Still unconfirmed | Phoenix Contact blocked its own pages (403). The 1.3 A datasheets say −25 … 70 °C with 2.5 %/K above 60 °C. That gives 0.75 at 70 °C. A distributor listing (extract) says the 10 A unit 2904601 has the same rule. The 10 A datasheet is unread. Phoenix lists 2904616 (/+) as successor with a −40 … 75 °C range. |
| mech.bearing-life | ka-skf-6206 | Unconfirmed | C 20.3 kN; C0 11.2 kN; L10 535.39 Mrev; L10h 5948.7 h; s0 4.48 | SKF product pages for 6206-RS1, 6206-2RS1, 6206 NR and 6206: https://www.skf.com/us/products/rolling-bearings/ball-bearings/deep-groove-ball-bearings/productid-6206-RS1 and .../productid-6206 | Confirmed | SKF pages list C 20.3 kN, C0 11.2 kN and Pu 0.475 kN. The open 6206 page lists 4 564 lbf and 2 518 lbf, which equal 20.30 and 11.20 kN. The pages are script-rendered, so I read them through search extracts. Variant 6206 ETN9 differs (23.4 and 12.9 kN). I recomputed L10 and s0: match. |
| mech.shaft | ka-shigley-7-1 | Unconfirmed | S_ut, S_y, q_s, S_e, n_y for the shoulder example | Shigley's MED 11th ed. (SI) Example 7-1, via lecture deck: https://www.slideshare.net/slideshow/w7shafts-and-shaft-componentspdf/265160335 | Contradicted | See Contradictions C1. The deck prints different strength and notch data than the KAT. |
| mech.tolerance-stack | ka-shigley-screw | Unconfirmed | gap 0.005 in; worst case ±0.010; RSS ±0.006 | Shigley's MED 9th ed., Ch. 1 tolerance example: no open reproduction found | Still unconfirmed | Arithmetic is right: 1.750 − 0.750 − 0.120 − 0.875 = 0.005; ±(0.003 + 0.001 + 0.005 + 0.001) = ±0.010; √(9 + 1 + 25 + 1)·1e-3 = 0.006. The book's example, dimensions and section number are unseen. |
| struct.connection-ec3 | ka-p363-m20-88 | Unconfirmed | F_v,Rd 94.1 kN; F_t,Rd 141 kN (M20 8.8) | Eurocode Applied, "Bolt design properties": https://eurocodeapplied.com/design/en1993/bolt-design-properties. Rules per EN 1993-1-8 Table 3.4. NSC AD 348: https://www.newsteelconstruction.com/wp/ad-348-bolt-resistance-tables-in-the-eurocode-blue-book/ | Confirmed | Open page lists A_s 245 mm², f_ub 800 MPa, 94.1 and 141.1 kN. I recomputed 0.6·800·245/1.25 = 94.08 and 0.9·800·245/1.25 = 141.12. NSC AD 348 describes the P363 table layout but gives no M20 value. The P363 table itself is unseen. |
| struct.connection-ec3 | ka-p358-fin-plate | Unconfirmed | α_b 0.606; k1 2.5; F_b,Rd 99.4 kN | SCI P358 (2014) fin plate example: no open reproduction found. Formula check against EN 1993-1-8 Table 3.4 | Still unconfirmed | I recomputed with d0 22, f_u 410, d 20, t 10: α_b = 40/66 = 0.606; F_b,Rd = 99.39 kN. No open page shows this P358 example. SteelConstruction.info (https://www.steelconstruction.info/topics/design/simple-connections) points to P358 but gives no values. |
| struct.load-combinations | ka-designers-guide | Unconfirmed | 6.10a 37.65 kN/m; 6.10b 37.2 kN/m | Gulvanessian, Calgaro, Holický, Designers' Guide to EN 1990 (2002), Ch. 6: no open text. ψ0 values: https://dnaop.com/html/60225_13.html | Still unconfirmed | ψ0 0.7 (domestic) and 0.5 (snow ≤ 1000 m) match a third-party copy of EN 1990 Table A1.1. I recomputed 37.65 and 37.2. I did not find ξ = 0.85 in an open source. The UK National Annex may use another ξ. The book example is unseen. |
| struct.en1993-member | ka-uc-compression | Unconfirmed | A 58.7 cm²; I_z 1548 cm⁴; N_cr,z 2005.3 kN; λ̄ 1.0194; χ 0.5288; N_b,z,Rd 1101.9 kN; N_pl,Rd 2083.85 kN | ArcelorMittal Orange Book, UC section properties: https://orangebook.arcelormittal.com/node/190. STAD UC table (extract): https://www.stad.fr/downloads/pdf/stad-technical-data-of-sections-uc.pdf | Confirmed | The source is analytic, not a book example. Properties agree: A 58.7; W_pl 497 and 231; I_y 4560 and I_z 1550 in the Orange Book (rounded); 4568 and 1548 in STAD. Orange Book r is 10.0; the KAT uses 10.2. The effect is nil here. I recomputed all outputs: match. |
| bs.duct-sizing | ka-round-given | Secondary | v 7.958 m/s; f 0.01714; Δp/L 1.633 Pa/m | Recomputed from Colebrook for 1 m³/s, 400 mm, ε 0.09 mm. ASHRAE Fundamentals 2017 Ch. 21 friction chart not opened | Confirmed | I get v 7.958, f 0.01715, Δp/L 1.63 Pa/m. The chart point "≈ 8 m/s, ≈ 1.6 Pa/m" is consistent. Air properties I used: ρ 1.204, μ 1.825e-5. |
| bs.duct-sizing | ka-rectangular-equivalent | Secondary | D_e 457.3 mm; Δp/L 0.556 Pa/m | Huebscher relation as published at Engineering Toolbox (extract): https://engineeringtoolbox.com/equivalent-diameter-d_443.html | Confirmed | The formula D_e = 1.30 (ab)^0.625/(a + b)^0.25 appears in open sources. I get D_e 457.0 mm and 0.559 Pa/m, inside the KAT tolerances. No open source ties the formula to ASHRAE by name. |
| bs.pipe-pump | ka-laminar-oil | Secondary | v 0.14147 m/s; Re 7.852; f 8.151; Δp 57.95 kPa; h_f 6.654 m | Hagen–Poiseuille relation, recomputed. Çengel & Cimbala 4th ed. Ch. 8 not opened | Confirmed | All values recompute. The KAT cites no example number, so there is nothing further to match. |
| bs.pipe-pump | ka-turbulent-water | Secondary | f 0.022175; h_f 1.1306 m | Colebrook–White, recomputed | Confirmed | I get f 0.022175 and h_f 1.1306 m. |
| bs.psychrometrics | ka-saturation-20 | Secondary | p_ws 2.3392 kPa; W_s 14.758 g/kg; h_s 57.555; v_s 0.8498 | Recomputed from Hyland–Wexler equations. ASHRAE 2017 Ch. 1 Table 3 not opened | Confirmed | I get p_ws 2.3388, W_s 14.762 with enhancement 1.0044, h_s 57.58, v_s 0.8502. All within 0.05 %. The table row itself is unseen. |
| mech.bolted-joint | ka-shigley-stiffness | Secondary | k_f 3.816e6 N/mm; k_P 1.908e6 kN/m; Φ 0.0720 | Eq. 8-20 as shown in course notes (extract): https://mece307.cankaya.edu.tr/uploads/files/bolts2.pdf. Also Roymech: https://roymech.org/Useful_Tables/Screws/Joint_Stiffness.html | Contradicted | See Contradictions C2. The sheet puts hole diameter d_h in the log term. Reviewer verdict (2026-10-24): contradiction upheld. Shigley's frustum relation (11th ed. Eq. 8-20) uses one diameter d in the prefactor and the log. Eq. 8-22 (two equal frusta, D = 1.5d) does the same. The derivation takes the frustum bore equal to d, and the book's examples insert the nominal bolt diameter. The sheet mixes d (prefactor) with d_h (log). That matches neither Shigley (k_P 2.213e6 kN/m) nor a consistent hole integration. The latter puts d_h in both places, as VDI 2230's cone does (2.067e6 kN/m). Relabel the KAT or align the formula; see P1-mech.bolted-joint-03. Equation numbers: owner to confirm against licensed copy. |
| mech.iso286-fit | ka-h7g6-25 | Secondary | H7 +21/0; g6 −7/−20 µm; clearance 7 … 41 µm | ISO 286 limits reference table, SimplyBearings: https://simplybearings.co.uk/shop/Info-Pages-ISO-Limits/c4746_4779/index.html | Confirmed | Column for 18–30 mm: g6 −7/−20; H7 +21/0. The page's size header is misaligned with its data. I mapped columns by their H7 values (12, 15, 18, 21, 25 …). ISO 286-1 formula check: g es ≈ −8.1, table −7; IT6 13 gives −20. ISO 286-2 itself is paywalled. |
| mech.iso286-fit | ka-h7p6-50 | Secondary | H7 +25/0; p6 +42/+26 µm; interference 1 … 42 µm | Same SimplyBearings table | Confirmed | Column for 30–50 mm: p6 +42/+26; H7 +25/0. The KAT note that the formula gives 25 µm, one below 26, is right: ei = IT7 + 1 in the table. |
| mech.weld-group | ka-two-vertical | Secondary | J_u 291 666.7 mm³; f_r 721.11 N/mm; τ 169.99 MPa | Recomputed from the KAT inputs. Shigley 11th ed. Table 9-1 not opened | Confirmed | T = 20 kN × 175 mm = 3.5 kN·m; 12 N/mm per mm; f_r = √(600² + 400²) = 721.11. Open lecture notes show similar formulas but garbled. Case number "2" is unverified. |
| mech.weld-group | ka-box-torsion | Secondary | J_u 1.3333e6 mm³; f 106.07 N/mm | Recomputed. Shigley Table 9-1 not opened | Confirmed | (b + d)³/6 = 200³/6; r = 70.71 mm gives 106.07. Case number "5" unverified. |
| mech.weld-group | ka-two-horizontal-bending | Secondary | I_u 5e5 mm³; f_r 502.49 N/mm; s_req 3.2306 mm | Recomputed. Shigley Table 9-2 not opened | Confirmed | Σ b·y² = 2 × 100 × 50² = 5e5; 5e6 × 50 / 5e5 = 500 N/mm; s_req = 502.49 / (0.707 × 220) = 3.2306. |
| struct.beam-formulas | ka-ss-udl | Secondary | R wL/2; M wL²/8; δ 5wL⁴/384EI | Wikipedia, Deflection (engineering): https://en.wikipedia.org/wiki/Deflection_(engineering) | Confirmed | Deflection formula matches. R and M are statics. Roark case "2e" unverified. |
| struct.beam-formulas | ka-ss-point | Secondary | R_A 16.667; R_B 33.333; M 66.667; δ 11.520 mm | Same Wikipedia page | Confirmed | Page gives δ = Fa(L² − a²)^1.5 / (9√3 L EI), with a the distance to the nearer support (2 m). I recomputed 11.520 mm at x = 3.266 m. |
| struct.beam-formulas | ka-cantilever-point | Secondary | M −PL; δ PL³/3EI | Same Wikipedia page | Confirmed | Formula matches. Roark case "1a" unverified. |
| struct.beam-formulas | ka-fixed-udl | Secondary | M_end −wL²/12; M_mid wL²/24; δ wL⁴/384EI | Hand derivation (no open table found) | Confirmed | Mid-span: wL²/8 − wL²/12 = wL²/24. Deflection: 5/384 − 4/384 = 1/384. Roark case "2d" unverified. |
| struct.frame-2d | ka-ss-udl | Secondary | as ka-ss-udl above | Same Wikipedia page | Confirmed | Same formulas as above. |
| struct.frame-2d | ka-cantilever-point | Secondary | as cantilever above | Same Wikipedia page | Confirmed | Same formulas as above. |
| struct.frame-2d | ka-propped-cantilever | Secondary | R 3wL/8; M_fixed −wL²/8; M_sag 9wL²/128 at 3L/8 | Hand derivation by superposition (no open table found) | Confirmed | Tip deflection wL⁴/8EI equals RL³/3EI, so R = 3wL/8. M_fixed = wL²/2 − 3wL²/8 = wL²/8. Max sag at x = R/w = 3L/8. Roark case "2c" unverified. |
| struct.frame-2d | ka-portal-sway | Secondary | column shear P/2; top moment Ph/2; reactions ±Ph/L | Statics. Hibbeler Structural Analysis 9th ed. not opened | Confirmed | Moment about a base gives V = Ph/L. Equal columns and a rigid beam split the shear evenly. Chapter and example number unverified. |
| struct.section-properties | ka-ub-457x191x98 | Secondary | A 125; I 45 730 and 2 347 cm⁴; W_el 1 957 and 243; W_pl 2 232 and 379; I_T 121; I_w 1.18 | BeamDimensions UB 457×191×98 (extract): https://beamdimensions.com/database/British/Steel/Universal_beams/457x191_UB_98/. Engineering Toolbox (extract): https://www.engineeringtoolbox.com/british-universal-steel-columns-beams-d_1316.html | Confirmed | Open sources list A 125.3, I 45 730 and 2 347, W_el 1 957 and 243.5, W_pl 2 232 and 379, J 121 cm⁴, warping 1.18 dm⁶. The KAT rounds A and W_el,z. Neither source is the Blue Book itself. Direct fetch was blocked (403). |
| struct.section-properties | ka-shs-100x100x5 | Secondary | A 18.7 cm²; I 279 cm⁴; W_pl 66.4; I_T 439 | BeamDimensions SHS 100×100×5, EN 10210-2 (extract): https://beamdimensions.com/database/European/Steel/EN_10210-2_SHS/SHS100x100x5 | Confirmed | All four values match. An Indian-standard page (IS 4923) gives other values; that is a different product standard. The Blue Book itself is unseen. |

## Contradictions

Both items are provisional. Each needs a check against the printed book.

### C1. mech.shaft / ka-shigley-7-1

- KAT claims "data as given there" for Shigley 11th ed., Example 7-1.
- Source: lecture deck of the 11th ed. SI text, Example 7-1: https://www.slideshare.net/slideshow/w7shafts-and-shaft-componentspdf/265160335.
- The deck prints d 27.94 mm, M 142.35 N·m, T 124.28 N·m. These match the KAT's loads after rounding.
- It prints S_ut 724 MPa. The KAT uses 735.
- It prints S_y 565 MPa. The KAT uses 574.
- It prints q_shear 0.88 and K_fs 1.37. The KAT uses q_s 0.92.
- It prints S_e 186.75 MPa and k_a 0.729. The KAT's computed S_e is 203.71 MPa, with k_a 0.7845.
- It prints σ'_a 104.91 MPa and n_y 4.50. The KAT's computed values are 104.27 MPa and 4.586.
- The deck does not print n_f. The KAT's n_f 1.650 is therefore unchecked.
- Caution: the deck is OCR-like and secondary. Its k_a 0.729 does not follow the Table 6-2 formula, which gives about 0.788 for 724 MPa. So the deck itself may be wrong.
- The KAT values 735 and 574 MPa may come from another edition or a metric rounding.
- Action: compare the printed example. Then fix the KAT inputs or its edition label.

### C2. mech.bolted-joint / ka-shigley-stiffness

- KAT claims Shigley 9th ed. Eq. 8-20 gives k_f = 3.816e6 N/mm and k_P = 1.908e6 kN/m.
- Open course notes (extract) give Eq. 8-20 with the bolt diameter d in both the prefactor and the logarithm: https://mece307.cankaya.edu.tr/uploads/files/bolts2.pdf.
- The sheet formula (`BoltedJointCalculator.cs`, row `k_f`) uses d = 12 mm in the prefactor.
- It uses d_h = 13 mm in the logarithm.
- With d = 12 mm in both places: the log argument is 2.740, not 3.220. Then k_f = 4.427e6 N/mm and k_P = 2.213e6 kN/m.
- That makes Φ = 0.0634, not 0.0720. The difference in k_P is 16 %. F_M (40.69 kN) does not change.
- The sheet's choice is close to VDI 2230 practice, which uses the hole diameter. The calculator header says "simplified VDI 2230".
- So the numbers are internally consistent. The attribution "Shigley Eq. 8-20" is the problem.
- Roymech lists the Wileman fit k = E d A exp(B d / l) with A 0.78715, B 0.62873. For this joint it gives about 2.34e6 kN/m.
- Action: relabel the KAT as "VDI-style, d_h in the log".
- Or add a Shigley-exact case with d = 12 mm.

## Other observations (not contradictions)

- ka-ashrae-example-1: the printed t_d is 7 °C. The KAT expects 7.4 with tolerance 0.05. The printed value is chart-rounded.
- ka-ashrae-example-1: print the expected W as 6.5 if the case should mirror the book.
- ka-wiring-matters-b32: the cited March 2023 article was not found. Two IET sources above carry the same value.
- ka-incropera-11-1: the KAT input U = 38.1 is not tested by any expected value. The port gives about 38.2.
- ka-uc-compression: Orange Book root radius 10.0 mm versus KAT 10.2 mm. Blue Book value unseen.
- ka-schneider-k: another open article derives 139.4 for XLPE 90 → 250 °C with other constants. The IEC value 143 stands.

## Owner action

Open the books below. Each item names what to read.

1. Shigley's Mechanical Engineering Design, 11th ed. (Budynas and Nisbett), Ch. 7, Example 7-1. Read S_ut, S_y, q, q_s, k_a, S_e, σ'_a, n_f, n_y. Resolves C1 (`ka-shigley-7-1`).
2. Shigley's Mechanical Engineering Design, 9th ed., Ch. 8, Eqs. 8-17, 8-20 and 8-21 (11th ed. equivalents if you hold that). Check which diameter sits in the log. Resolves C2.
3. Shigley's Mechanical Engineering Design, 9th ed., Ch. 1, the dimensions and tolerances section, shouldered-screw stack-up example. Read the four dimensions, ±tolerances, worst-case and RSS answers (`ka-shigley-screw`).
4. Incropera, DeWitt, Bergman, Lavine, Fundamentals of Heat and Mass Transfer, 7th ed., Ch. 11, Example 11.1 (p. 680 per the port). Read q, T_c,o, ΔT_lm, U and L (`ka-incropera-11-1`).
5. SCI P358, Joints in Steel Construction: Simple Joints to Eurocode 3 (2014), Section 5.6 fin plate worked example (about p. 137). Look for α_b 0.606, k1 2.5, F_b,Rd 99.4 kN for M20 8.8 (`ka-p358-fin-plate`). If it is absent, relabel the case as analytic.
6. Gulvanessian, Calgaro, Holický, Designers' Guide to EN 1990 (2002), Ch. 6 combination examples. Check whether any example uses G 20, Q_A 8, S 3 kN/m. If not, relabel the case as analytic. Also check ξ in your National Annex (`ka-designers-guide`).
7. Phoenix Contact QUINT4-PS/1AC/24DC/10 (2904601, or successor 2904616) datasheet, "Ambient temperature (operation)" and "Derating" lines. Free from phoenixcontact.com; it blocked my fetch (`ka-psu-derating`).
8. Linden's Handbook of Batteries, 5th ed., the Peukert equation page. Optional; the relation is confirmed openly (`ka-peukert`).
9. SCI P363 Blue Book (2013 or the online edition), non-preloaded 8.8 bolt table, M20 row (94.1 and 141 kN). Also the section tables for UB 457×191×98, SHS 100×100×5 (hot finished) and UC 203×203×46 (root radius). Optional; open secondary sources agree.
10. Roark's Formulas for Stress and Strain, 8th ed., Table 8.1: confirm case labels 1a, 2a, 2c, 2d, 2e. Hibbeler, Structural Analysis, 9th ed.: the pinned portal frame section. Çengel and Cimbala, 4th ed., Ch. 8: laminar pipe flow. Shigley 11th ed. Tables 9-1 and 9-2: confirm case numbers 2 and 5. All optional; the values recompute.
11. ASHRAE Handbook—Fundamentals 2017: Ch. 1 Example 1, Ch. 1 Table 3 (20 °C row), and Ch. 21 friction chart point. Optional; confirms the edition.
12. Schneider Electric, Electrical Installation Guide, "Sizing of protective earthing conductor", Fig. G60 in a normal browser. The site blocked my fetch (`ka-schneider-k`).

## Addendum B-0/B-1 cases (90d15f11)

Date: 2026-10-25. Release commit: 90d15f11. Rules and outcome words are the same as above.
I ran `python3 tools/verification/kat_inventory.py`. It lists 154 cases, 23 tagged Unconfirmed and 4 tagged Secondary.
Thirteen of the 23 Unconfirmed cases are the earlier ones in the main table. Ten cases are new and covered here.
The new cases are two PCB trace cases, one Parker accumulator case, three spring cases and four LTB cases.
I recorded values, locations and URLs only. I copied no source text.

### Summary of the new cases

| Outcome | Cases |
|---|---|
| Confirmed (7) | `ka-published-3a-10k-1oz`, `ka-published-3a-10k-1oz-internal`, `ka-parker-hy10-4004`, `ka-torsion-en`, `ka-squared-en`, `ka-annex-b-class2`, `ka-annex-b-class3-b1` |
| Still unconfirmed (2) | `ka-annex-a-class2`, `ka-annex-a-class3` |
| Contradicted (1) | `ka-shigley-10-3` |

Six of the seven confirmations rest on one or more open pages. `ka-parker-hy10-4004` needs a citation fix (see its row).
The Annex A cases stay open because I found no open page that prints the Annex A expressions.

### Changes to earlier cases

- The earlier `ka-manufacturer-dt30` no longer exists. The case is now `ka-dt30` in `SealedSystemCalculator.cs`, tagged Analytic and Confirmed.
- Its expected factor is 0.5147503, from (30/50)^1.3. This agrees with the 0.515 that the Stelrad table prints. The earlier row stands.
- None of the earlier 21 cases carries the Secondary tag now. I did not re-open their sources or values.
- The tag now reads Secondary only on `ka-published-3a-10k-1oz`, `ka-published-3a-10k-1oz-internal`, `ka-parker-hy10-4004` and `ka-shigley-10-3`.
- `ka-shigley-stiffness` (C2 above) is now described as "Shigley-style" and no longer tagged. I did not re-check its numbers.

### Table

| Calculator | Case | Tag | Value(s) checked | Source checked (title, edition, URL, page/table) | Outcome | Notes |
|---|---|---|---|---|---|---|
| elec.pcb-trace | ka-published-3a-10k-1oz | Unconfirmed/Secondary | w_min 1.37 mm external, 3 A, 10 K, 1 oz | NextPCB, "PCB trace width calculator, IPC-2221 guide", quick-reference table: https://www.nextpcb.com/blog/pcb-trace-width-calculator-ipc-2221-guide. Flux trace-width tool: https://unified.flux.ai/p/tools/trace-width-calculator | Confirmed | NextPCB prints 53.8 mil (1.37 mm) for 3 A external. Flux prints 1.37 mm (54 mil). I recomputed A = 74.162 mil², w = 1.36699 mm. KAT 1.37 ± 0.005 holds. Caveat in the paragraph below the table. |
| elec.pcb-trace | ka-published-3a-10k-1oz-internal | Unconfirmed/Secondary | w_min 3.56 mm internal, 3 A, 10 K, 1 oz | NextPCB page above, same table. Constants also on LCSC (https://www.lcsc.com/tools/conversion-calculator-pcb-trace-width) and Advanced PCB (https://advancedpcb.com/en-us/tools/trace-width-calculator) | Confirmed | NextPCB prints 140.0 mil (3.56 mm). Flux gives only "about 3.6 mm". I recomputed A = 192.927 mil², w = 3.55613 mm. KAT 3.56 ± 0.005 holds. |
| fp.accumulator | ka-parker-hy10-4004 | Unconfirmed/Secondary | P2 210, P1 100, P0 90 bar; ΔV 14 L; isothermal; V0 about 29.7 L; selection 32 L | Parker Olaer, Catalogue HY10-4001/UK, Bladder Accumulators (EHV), 06/2013, "How to size?" page, via distributor copy: https://cdn.kovaz.cz/data/user-content/katalogy/Akumulátory a chladiče/Vakové akumulátory EHV 250-690 bar/Vakové akumulátory EHV 250-690 bar.pdf | Confirmed | The example is in HY10-4001/UK, not HY10-4004/UK. Fix the citation. The page prints ratio 2.1 and a chart-read size of 32 L. It prints no V0 and no pressure basis. See the notes below. |
| mech.spring | ka-torsion-en | Unconfirmed | q = (C + 0.07)/(C − 0.75) = 1.1131034 at C = 8; σ₂ 212.587 MPa; u₂ 0.157472 | Gutekunst Federn, Federnshop blog, "Design of metal springs, part 2: calculation": https://blog.federnshop.com/en/design-of-metal-springs-part-2-calculation/ | Confirmed | The page prints q = (D/d + 0.07)/(D/d − 0.75) and cites EN 13906-3:2001. I recomputed q, σ₂ = q·32·M/(π d³) and u₂: match. The 2014 edition is unseen. |
| mech.spring | ka-squared-en | Unconfirmed | L_s = (n_t + 1.5)·d_max = 23.46 mm; S_a = 3.136 mm; L_n 26.596 mm; F_c 105.176 N | Same Gutekunst Federn page; it names DIN EN 13906-1 for its compression formulas | Confirmed | Printed: n_t = n + 2, L_c = (n_t + 1.5)·d_max for unground ends, S_a = (0.0015·D²/d + 0.1·d)·n. I recomputed all 12 expected values from these: match. |
| mech.spring | ka-shigley-10-3 | Unconfirmed/Secondary | k 1.3142; K_B 1.1174; L_s 35; F_c 167.86; L₀ 162.72 mm entered | Shigley's MED, 10th and 11th ed., problem 10-3 solutions (instructor copies, Oakland University): https://www.secs.oakland.edu/~latcha/me486/BN10/Chap10_10e.pdf and https://www.secs.oakland.edu/~latcha/ME4300/SM_PDF/CH10.pdf | Contradicted | See C3. Every value matches except L₀. Both editions print L₀ = 162.8 mm, not 162.72. |
| struct.ltb-interaction | ka-annex-b-class2 | Unconfirmed | M_cr 521.515 kN·m; χ_LT 0.457851; k_yy 0.848513; k_yz 0.960284; k_zy 0.917840; k_zz 1.600473; 6.61 0.94775; 6.62 1.24809 | SCI NCCI SN048b (steelconstruction.info): https://www.SteelConstruction.info/images/d/d8/SN048b.pdf. JRC Eurocodes workshop, Simões, Example 3: https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_Eurocodes_Steel_Workshop_SIMOES.pdf. SOFiSTiK DCE-EN29: https://docs.sofistik.com/2024/en/verification/_static/verification/pdf/dce-en29.pdf | Confirmed | SN048b prints the class 1 and 2 forms of k_yy, k_yz = 0.6 k_zz, k_zz, k_zy and C_m = 0.6 + 0.4ψ. The other two pages show k_yy and k_zy worked. I recomputed all nine values: match. A typo in the case text is noted below. |
| struct.ltb-interaction | ka-annex-b-class3-b1 | Unconfirmed | λ̄_LT 1.119598; χ_LT 0.494537; k_yy 0.860536; k_yz = k_zz 1.271128; k_zy 0.688429; 6.61 1.11105; 6.62 1.18268 | SN048b (above) for k_yy, k_zz and k_yz = k_zz, class 3. SCI Advisory Desk AD 552, New Steel Construction, Feb 2026, corrected P362 table: https://steelconstruction.info/images/d/d1/AD-552.pdf | Confirmed | AD 552 prints k_zy = 0.8 k_yy for class 3 members not susceptible to torsion. I recomputed all eight values with W_el: match. |
| struct.ltb-interaction | ka-annex-a-class2 | Unconfirmed | N_cr,T 2962.46 kN; a_LT 0.998475; C_my 0.948882; C_mz 1.058807; C_mLT 1.340335; k_yy 1.348996; k_yz 2.166104; k_zy 0.749357; k_zz 1.926807 | No open page found that prints the Annex A expressions. SN048b names Annex A but gives no formulas. SCIA Benchmark 21 (ECCS 119 Example 5) blocked my fetch (403) | Still unconfirmed | My own transcription of Annex A, written from memory of the standard, reproduces every value to six digits. That is not an open source, so I do not count it as confirmation. |
| struct.ltb-interaction | ka-annex-a-class3 | Unconfirmed | λ̄_0 1.281438; k_yy 1.327845; k_yz 1.395680; k_zy 1.181153; k_zz 1.241494; 6.61 1.42477; 6.62 1.48164 | Same as above | Still unconfirmed | The same transcription reproduces all values, including the elastic column without the 0.6√(w_i/w_j) factors. The open source for those factors is still missing. |

### Notes by case

**PCB trace cases.** All open calculators use the sheet's IPC-2221 curve-fit: k 0.048 and 0.024, exponents 0.44 and 0.725, 1.378 mil per oz.
So these checks confirm the arithmetic and the quoted widths. They do not test the curve-fit against the printed IPC-2221 chart. The Secondary tag stays correct.
The NextPCB worked example gives 39 mil for the external width, which conflicts with its own table. I ignored that line.

**ka-parker-hy10-4004.**
- The catalogue gives no V0 and does not say if its pressures are gauge or absolute. Its chart assumes P0 = 0.9·P1 at 20 °C.
- I recomputed V0 = 14/(90/100 − 90/210) = 29.697 L on absolute pressures. KAT 29.7 ± 0.005 holds.
- On gauge pressures plus 1.01325 bar, V0 would be 29.807 L, outside the KAT tolerance. The catalogue does not decide the basis.
- The 2016 price-book catalogue HY10-4004-PL/UK rev. 6 (Wilson Company copy) has a "How to size" page with no worked example.
- Change the case citation to HY10-4001/UK, 06/2013, and say that V0 is recomputed.

**ka-annex-b-class2 text error.** The case text says n_y = 0.1. The correct value is n_y = 1200/(0.931891 × 3933.4) = 0.32738. The expected k_yy uses 0.32738, so only the text is wrong.

**ka-annex-b-class3-b1 and the other table.** SN048b prints k_zy for class 3 members susceptible to torsion with 0.05 in place of 0.1. No case tests that entry.

**Annex A recomputation inputs.** I took N_cr,T with L_T = 6 m and i₀² = i_y² + i_z². That gives 2962.46 kN, as in the case. ε_y = 1.00283 and ψ_z = 1 for C_mz,0.

### C3. mech.spring / ka-shigley-10-3

- The KAT quotes the manual as k = 1.3142 N/mm, K_B = 1.1174, F_s = 167.86 N and L₀ = 162.72 mm.
- It enters L₀ = 162.72.
- Both solution copies print d 2.5 mm, OD 31 mm, N_t 14, N_a 13 and D 28.5 mm.
- They also print C 11.4 and G 81.0 GPa. All of these match the KAT inputs.
- They print S_ut = 1936 MPa (A 2211, m 0.145), S_sy 871.2 MPa, K_B 1.117 and F_s 167.9 N. The KAT's R_m 1935.9 and K_B 1.1174 agree after rounding.
- They print k = 1.314 N/mm and L_s = 35 mm. These agree.
- They print L₀ = 162.8 mm. The KAT's 162.72 mm is the sheet's own value, 35 + 167.8546/1.3142474 = 162.7191 mm.
- With the printed 162.8 mm the sheet gives F_c = 167.96 N and u_c = 1.0006. The KAT expects 167.86 N and 1.0000.
- The differences in k, K_B and F_s are print rounding. The L₀ difference is real, and the sheet value is mislabelled as published.
- Action: say that L₀ comes from the sheet. Or enter 162.8 and widen the F_c and u_c tolerances.
- Caution: these are instructor copies of a solutions manual. The book itself is unseen.

### Owner action for the addendum

1. Shigley's Mechanical Engineering Design, 10th ed., problem 10-3 and its solution. Read L₀ and k to confirm 162.8 mm and 1.314 N/mm (`ka-shigley-10-3`).
2. EN 1993-1-1:2005+A1:2014, Annex A, Tables A.1 and A.2. Check a_LT, C_my,0, C_my, C_mLT, C_ij, b_LT to e_LT and the class 3 column (`ka-annex-a-class2`, `ka-annex-a-class3`). Alternatively, open SCIA Benchmark 21 (ECCS 119 Example 5) in a normal browser.
3. EN 13906-3:2014, the stress correction factor q. Confirm the 2001 expression is unchanged (`ka-torsion-en`).
4. EN 13906-1:2013, the solid length of unground ends and the minimum coil clearance S_a. The Gutekunst page is a manufacturer source only (`ka-squared-en`).
5. IPC-2221 chart for 1 oz copper. Optional, since all calculators share the sheet's curve-fit (`ka-published-3a-10k-1oz` and `-internal`).
6. Parker catalogue HY10-4001/UK, the sizing page. Check whether its ratio is on absolute or gauge pressures, and update the citation (`ka-parker-hy10-4004`).

## Addendum B-2/B-3 cases (2f53791c)

Date: 2026-10-25. Release commit: 2f53791c. Rules and outcome words are the same as above.
I ran `python3 tools/verification/kat_inventory.py`. It lists 220 cases, 29 tagged Unconfirmed or Secondary.
Twenty-three of the 29 are in the tables above. Six are new and sit in `ORingGlandCalculator.cs` and `PlateBendingCalculator.cs`.
I recorded values, locations and URLs only. I copied no source text.

### Summary of the six new cases

| Outcome | Cases |
|---|---|
| Confirmed (3) | `ka-parker-quadratic`, `ka-rect-simple-square-book`, `ka-rect-clamped-square-book` |
| Still unconfirmed (2) | `ka-rect-clamped-square`, `ka-rect-clamped-natural` |
| Contradicted (1) | `ka-stretch-rule` |

The Parker checks use a free university copy of the 2001 edition (ORD 5700A/US). Current editions may differ.
The plate checks rest on search-tool extracts of vendor pages, because direct fetches returned no usable text.

### Table (tagged cases)

| Calculator | Case | Tag | Value(s) checked | Source checked (title, edition, URL, page/table) | Outcome | Notes |
|---|---|---|---|---|---|---|
| fp.oring-gland | ka-parker-quadratic | Unconfirmed/Secondary | Y = 0.56 + 0.59·X − 0.0046·X² for 3 to 25 % ID stretch; X = 4 gives Y 2.8464 % | Parker O-Ring Handbook ORD 5700A/US, 2001 ed., section 3.5 and Figure 3-3 (empirical formulas for the observed curve): https://wp.optics.arizona.edu/optomech/wp-content/uploads/sites/53/2016/10/Parker-O-ring-handbook.pdf | Confirmed | The handbook prints the same three coefficients. X is percent stretch on the inside diameter. Y is percent reduction in cross-section diameter. A separate quartic covers 0 to 3 %. The case uses 4 %, so the quadratic applies. I recomputed 0.56 + 2.36 − 0.0736 = 2.8464. I did not recheck squeeze_nom 26.14 %. |
| fp.oring-gland | ka-stretch-rule | Unconfirmed/Secondary | piston-groove stretch limit 6 % (5.8 % reads 0.9667 of the limit) | Same Parker handbook, section 3 design list item 1 and section 3.5; also section 5 text on stretch close to five percent | Contradicted | See C4. The 2001 edition recommends no more than 5 % and allows more only for a shorter life. PTD 5705 and the current edition are unseen. |
| struct.plate-bending | ka-rect-simple-square-book | Unconfirmed | α = 0.00406 in w_max = α·q·a⁴/D, simply supported square, uniform load; 0.25771 mm | SolidWorks Simulation help, "Simply Supported Rectangular Plate", citing Timoshenko and Woinowsky-Krieger, 2nd ed. (extract): https://help.solidworks.com/2025/English/simconntutorial/Simply_Supported_Rectangular_Plate_Static.htm | Confirmed | The page prints 0.00406. I recomputed D = 9846.15 N·m for E 210 GPa, ν 0.3, t 8 mm. Then w = 0.00406·10 kPa·0.5⁴/D = 0.25771 mm. The book itself is unseen. |
| struct.plate-bending | ka-rect-clamped-square-book | Unconfirmed | α = 0.00126, clamped square, uniform load; 0.07998 mm | SolidWorks Simulation help, "Clamped Square Plate Under Uniform Loading", citing the same book (extract): https://help.solidworks.com/2025/English/simconntutorial/Clamped_Square_Plate_Under_Uniform_Loading.htm | Confirmed | The page prints 0.00126. The expected 0.07998 mm recomputes from it. A CIMNE lab sheet (extract) uses 0.0012567 for the same case, which is 0.3 % below the book's rounding. See the note on ka-rect-clamped-square. |
| struct.plate-bending | ka-rect-clamped-square | Unconfirmed | w_max 0.001265319·q·a⁴/D; M_x centre 0.022905·q·a²; edge M −0.05133·q·a²; book 0.00126 and 0.0513 | Same SolidWorks page for the deflection. No open page found for the moment coefficients | Still unconfirmed | The sheet's converged 0.0012653 rounds to 0.00127, one unit above the book's 0.00126. The CIMNE value 0.0012567 sits below both. Only the book's table can settle the third digit. The moments 0.0231 and 0.0513 appear in no open page I found. |
| struct.plate-bending | ka-rect-clamped-natural | Unconfirmed | λ² = 35.99 (sheet 35.9852); f₁ = 286.85 Hz for 500 × 500 × 8 mm steel | Leissa, Vibration of Plates, NASA SP-160 (1969), Ch. 4: no open page found. An ANSYS verification page cites a different plate (extract) | Still unconfirmed | I recomputed f₁ = (35.9852/a²)·√(D/ρt)/2π = 286.85 Hz from the stated λ². The constant 35.99 is not read from any open source. |

### Relation-level checks of the Analytic cases

These cases carry the Analytic tag and cite no book. I checked whether the underlying relation appears in an open source.
I also recomputed a few expected values by hand. "Not checked" means I did not open any source for that case.

| Calculator | Cases | Relation or value checked | Source checked | Outcome | Notes |
|---|---|---|---|---|---|
| mech.snap-fit | ka-constant, ka-taper-thickness | y_perm = 0.67·ε·l²/h (the KAT uses 2/3); tapered to half thickness 1.09 (the KAT has K_y 1.0904); P = b·h²·Es·ε/(6·l) | Synectic, "How to Design a Snap Fit Joint": https://synectic.net/?p=42601. BASF snap-fit Part II (P = k·Y only): https://ulprospector.ul.com/1228/pe-snapfit-2/ | Confirmed | The Synectic page prints 0.67, 1.09 and the force relation. It cites no standard and swaps the meaning of H and B. The BASF manual formulas themselves are unseen. I could not find a Covestro guide. |
| mech.snap-fit | ka-taper-thickness-deep, ka-taper-width, ka-annular, ka-annular-inner-surface, ka-annular-force-factor, ka-torsion, ka-torsion-both-ends, ka-curve | Tapers beyond half thickness, width taper, thin ring, torsion bar and secant-modulus curve | None found | Still unconfirmed | No open source prints these forms. They rest on the author's python3 integration and strain-energy checks. |
| bs.pipe-expansion | ka-offset-leg-steel-50m, ka-u-loop-too-short, ka-cold-copper-loop | Δ = α·L·ΔT and guided-cantilever leg L = √(3·E·D·Δ/Sa) | EPCLAND, "Pipe expansion loops": https://epcland.com/pipe-expansion-loops/ | Confirmed | The page prints both relations. I recomputed L_req = 4.850 m for Δ 42 mm and 3.4295 m for Δ 21 mm with Sa 100 MPa. Sa is a record input. The B31.3 Sa equation is paid text. |
| bs.pipe-expansion | ka-axial-bellows | Anchor force 6 bar × 80 cm² + 120 N/mm × 42 mm | Recomputed | Confirmed | 4800 N + 5040 N = 9840 N. |
| bs.pipe-expansion | ka-z-bend-steel, and the F = 12·E·I·Δ/L³ and M = 6·E·I·Δ/L² forms | Guided-cantilever force and moment; Z-bend series sharing | None found | Still unconfirmed | EPCLAND prints no force or moment form. These are standard beam results but I opened no source. Manufacturer guides (for example the CDA copper handbook) gave no formula in the extract. |
| bs.insulation | ka-pipe-chosen, ka-pipe-heat-loss, ka-pipe-surface, ka-wall-and-film | Cylinder series resistance R = ln(D_e/D_i)/(2πλ) + 1/(h_se·π·D_e) | Recomputed | Confirmed | ka-pipe-chosen: 1.870085 + 0.198944 = 2.069029 K·m/W; q 28.999 W/m. Matches. |
| bs.insulation | ka-bare-complies-window | Critical diameter 2λ/h = 40 mm (critical radius λ/h) | Search extracts of several heat-transfer course pages. I opened none of them | Confirmed | r_cr = λ/h for a cylinder. The sphere form is 2λ/h, so do not mix them. |
| bs.insulation | ka-correlation-still, ka-correlation-wind, ka-flat-correlation, ka-flat-wind-turbulent, and the Magnus dew-point cases | Free-convection, Churchill–Bernstein, flat-plate and Magnus correlations | None opened | Still unconfirmed | Not checked here. The 25 W/m and 45 °C limits are record values. BS 5422 and ISO 12241 are paid. |
| elec.containment | ka-capstan-180, ka-fill-3x10-in-25, ka-pull-tray-two-bends, ka-direction-passes-every-check | T₂ = T₁·e^(μθ), θ in radians; fill = Σ(π·d²/4)/area | Wikipedia, Capstan equation: https://en.wikipedia.org/wiki/Capstan_equation | Confirmed | I recomputed 200·e^(0.35π) = 600.567 N and the 48 % fill. The bend loop of the two-bend cases is the same relation applied per bend. |
| elec.containment | ka-trunking-fill, ka-bend-too-tight | Space factor 0.42, bend multiple 12, friction and sidewall limits | None | Still unconfirmed | The KATs state these values are illustrative. BS 7671 Appendix 5 and IET Guidance Note 1 are paid. Nothing to match. |
| fp.cylinder | ka-orifice | Q = C_d·A·√(2Δp/ρ) | Wikipedia, Orifice plate: https://en.wikipedia.org/wiki/Orifice_plate | Confirmed | The page adds 1/√(1 − β⁴). The KAT omits it, so it holds only for β near 0. Check that the model says free discharge. |
| fp.cylinder | ka-euler-slender | F_cr = π²·E·I/(K·L)² | Wikipedia, Buckling: https://en.wikipedia.org/wiki/Buckling | Confirmed | I recomputed 59.06 kN. The page lists K = 2 for fixed-free. |
| fp.cylinder | ka-back-pressure-fixed-free, ka-circuit-standard, ka-regenerative, ka-lossless | Johnson parabola and λ_c = π√(2E/f_y); area and force arithmetic | None open for Johnson | Still unconfirmed | Wikipedia names Johnson's formula but prints neither it nor λ_c. The two curves meet at λ_c by algebra. The areas recompute (A_b 78.5398 cm²). |
| fp.oring-gland | ka-thermal-swell, ka-worst-case-hand, ka-rod-seal, ka-face-seal, ka-face-seal-internal, ka-gap-over-temperature, ka-incompressible-reduction, ka-linear-reduction, ka-extrusion-record | Worst-case stack-up, constant-volume and Poisson reductions | Not checked | Not checked | Analytic arithmetic. No source opened. |
| fp.oring-gland | ka-monte-carlo-seeded, ka-monte-carlo-measured | Seeded sampler results | Not applicable | Not checked | Both are tagged Published, but the source is a python re-implementation of the engine's own sampler. See the observations. |
| struct.plate-bending | ka-circular-clamped-uniform, ka-circular-simple-uniform, ka-circular-simple-patch, ka-circular-clamped-ring, ka-rect-simple-square-series, ka-rect-simple-natural, ka-rect-simple-patch | Kirchhoff closed forms and Navier series | Not checked | Not checked | The LIRA test 1.45 page (https://help.liraland.com/en-gb/section-1/test-1-45.html) prints a clamped circular plate with centre values from Timoshenko. I did not compare them with the case inputs. |

### Observations

- The two `ka-monte-carlo-*` cases carry the Published kind but check the engine against its own algorithm in python. Relabel them Analytic.
- `ka-rect-clamped-square` states the book values 0.00126 and 0.0513. Only the first is confirmed, and only to 3 s.f.
- `ka-rect-simple-square-book` and `ka-rect-clamped-square-book` carry tolerances of 1e-3 and 5e-3. Both are looser than the 3 s.f. print, which is acceptable.
- The remaining cases of the inventory are Analytic and Confirmed and outside this check, as in the main tables.

### C4. fp.oring-gland / ka-stretch-rule

- The KAT cites Parker PTD 5705 for a piston-groove stretch limit of 6 %. It reads 5.8 % as 0.9667 of that limit.
- The 2001 Parker O-Ring Handbook (ORD 5700A/US) says the installed ID stretch should not exceed 5 %. Section 3 list item 1 prints this.
- Section 3.5 repeats it. It allows more only when a shorter life is acceptable. The dynamic section again says stretch near five percent.
- I found no open page that prints 6 %, and I did not find PTD 5705.
- At a 5 % limit the case gives u_stretch 1.16 (FAIL). At 6 % it passes.
- Action: set the x_hi default to 5 and update the case. Or cite the exact edition that prints 6 %.

### Owner action for the addendum

1. Parker O-Ring Handbook, current edition: the section 3.5 stretch limit (`ka-stretch-rule`). Also check whether PTD 5705 prints 6 %. Free from parker.com.
2. Timoshenko and Woinowsky-Krieger, Theory of Plates and Shells, 2nd ed., Ch. 5 and 6 tables: α 0.00406 and 0.00126, clamped centre moment and edge moment coefficients (`ka-rect-clamped-square`).
3. Leissa, Vibration of Plates (NASA SP-160), Ch. 4 frequency table for the clamped square, λ² 35.99 (`ka-rect-clamped-natural`).
4. BASF Snap-Fit Design Manual, pages IV-4 and V-1 to V-4, for the cantilever formulas and the 1.09 taper factor. Optional.
5. ASME B31.3 expansion-loop guidance or a manufacturer guide that prints F = 12·E·I·Δ/L³ (`ka-z-bend-steel`). Optional.
6. BS 7671 Appendix 5 and IET Guidance Note 1 give cable space factors. BS 5422 and ISO 12241 give insulation limits. Both are paid; the KAT values are illustrative.
