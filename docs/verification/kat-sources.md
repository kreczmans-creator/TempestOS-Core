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
| All | 28 | 5 | 2 | 35 |

Of the 28 confirmed, 3 rest on a manufacturer or IET page: B32 Z_s, ΔT 30 factor and SKF 6206.
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
| mech.bolted-joint | ka-shigley-stiffness | Secondary | k_f 3.816e6 N/mm; k_P 1.908e6 kN/m; Φ 0.0720 | Eq. 8-20 as shown in course notes (extract): https://mece307.cankaya.edu.tr/uploads/files/bolts2.pdf. Also Roymech: https://roymech.org/Useful_Tables/Screws/Joint_Stiffness.html | Contradicted | See Contradictions C2. The sheet puts hole diameter d_h in the log term. |
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
