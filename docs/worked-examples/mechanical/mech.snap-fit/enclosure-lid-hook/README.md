# Enclosure lid hook: cantilever snap-fit, thickness tapered to half (`mech.snap-fit`)

**Source.** Analytic (Euler–Bernoulli integration; recomputed in python3), l 18 mm, h 2.2 mm, b 7 mm, tip thickness h/2, y 1.2 mm, Es 2000 MPa (example), ε_perm 0.04 (example), μ 0.35 (example), α 30°, α′ 65°: J = 0.545177, K_y = 2J = 1.0903549, strain largest at the root; ε = y·h/(K_y·l²) = 0.747293 %; P = b·h²·Es·ε/(6·l) = 4.688573 N; u = 0.186823; y_perm = 6.423182 mm; W = P(μ + tan α)/(1 − μ·tan α) = 5.449054 N; arctan(1/μ) = 70.709954°; W′ = 46.891015 N — Confirmed.

The 2.2 mm thick lug is 18 mm long (l/h = 8.2) and tapers to half its thickness at the tip, which puts the largest strain at the root and gives the deflection factor K_y = 1.090. Bent 1.2 mm over the undercut it strains 0.75 % against the 4 % example limit (utilisation 0.19); it could be bent 6.4 mm. The tip force is 4.7 N. With a friction coefficient of 0.35 the 30° entry face needs 5.4 N to push the lid on, and the 65° return face needs 46.9 N to pull it off. The return face would lock for good at 70.7° and above (arctan(1/μ)); the sheet reports that as self-locking, not as a number. Es, the permissible strain and μ are example values marked as assumptions: replace them from the plastic's data and your own tests. The root of the lug needs a generous fillet; the strain above is nominal.

| Input | Value | Source |
|---|---|---|
| `Es` | 2000 MPa | assumption: Example secant modulus at the design strain and temperature. Enter Es for your plastic from its stress–strain data or a W3 material; no plastics data are shipped. |
| `Es_source` | Typed secant modulus | assumption: Secant modulus typed for this example; a W3 material with its Es(ε) points can be applied instead. |
| `T_use` | 23 degC | spec: Enclosure ENC-310 §6: room temperature |
| `alpha` | 30 deg | drawing: ENC-310-07 rev C |
| `alpha_r` | 65 deg | drawing: ENC-310-07 rev C |
| `b` | 7 mm | drawing: ENC-310-07 rev C |
| `eps_perm_once` | 0.04 | assumption: Example permissible strain for one-time joining. Enter it for your plastic at the temperature of use from its data or a W3 material; no plastics data are shipped. |
| `h` | 2.2 mm | drawing: ENC-310-07 rev C |
| `h_ratio` | 0.5 | drawing: ENC-310-07 rev C |
| `l` | 18 mm | drawing: ENC-310-07 rev C |
| `loading` | One-time (assembled once or rarely) | spec: Enclosure ENC-310 §6: the lid is opened for service only |
| `mode` | Cantilever, tapered thickness | drawing: ENC-310-07 rev C |
| `mu` | 0.35 | assumption: Example friction coefficient for a plastic-on-plastic pair. Enter μ for your pair of materials and surfaces from your reference or a test (record snapfit.friction). |
| `y` | 1.2 mm | drawing: ENC-310-07 rev C |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `K_y` | 1.09035  | 1.09035  | PASS |
| `eps` | 0.747293 % | 0.747293 % | PASS |
| `P` | 4.68857 N | 4.68857 N | PASS |
| `strain_util` | 0.186823  | 0.186823  | PASS |
| `y_perm` | 6.42318 mm | 6.42318 mm | PASS |
| `assembly_force` | 5.44905 N | 5.44905 N | PASS |
| `alpha_lock` | 70.71 deg | 70.71 deg | PASS |
| `retention_force` | 46.891 N | 46.891 N | PASS |

Governing check: Strain at the critical section against the permissible strain — PASS (utilisation 0.187). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
