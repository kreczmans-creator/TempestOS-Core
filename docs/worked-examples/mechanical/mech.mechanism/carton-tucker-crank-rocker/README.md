# Carton flap tucker: crank-rocker 150/45/140/100 mm at 60 cycles/min (`mech.mechanism`)

**Source.** Independent python3 recomputation (Freudenstein quadratic, real 2×2 solves, 20001-point scan polished by golden section), Frame 150 mm, crank 45 mm, coupler 140 mm, rocker 100 mm, crank 60 rpm from 0°, open circuit, T_L = 4 N·m opposing the rocker, J = 0.012 kg·m²: s + l = 195 mm < p + q = 240 mm (Grashof crank-rocker); at θ₂ = 0° θ₄ = 93.8909°, μ = 48.4409° (also the smallest over the cycle); rocker swing 54.7864°; peak |ω₄| 3.543461 rad/s (pin 0.354346 m/s), peak |α₄| 31.5994 rad/s²; peak input torque by virtual work 2.265439 N·m — Confirmed.

A crank-rocker drives a carton flap tucker at 60 cycles a minute. The linkage is Grashof (s + l = 195 mm against p + q = 240 mm), so the 45 mm crank turns fully and the 100 mm rocker swings 54.8°. The transmission angle never falls below 48.4° (the 40° requirement passes at 0.83). With the example flap torque of 4 N·m opposing the rocker and 0.012 kg·m² of arm and blade, the motor must deliver at most 2.27 N·m at the crank, and the blade pin peaks at 0.354 m/s and 3.16 m/s². The torque, mass and the 40° rule are example values marked as assumptions: replace them from the station measurement and your design rules.

| Input | Value | Source |
|---|---|---|
| `J_out` | 0.012 kg*m^2 | assumption: Example: rocker arm and blade about the rocker pivot; take it from the CAD mass properties. |
| `L1` | 150 mm | drawing: PKG-TUCK-02 rev B |
| `L2` | 45 mm | drawing: PKG-TUCK-02 rev B |
| `L3` | 140 mm | drawing: PKG-TUCK-02 rev B |
| `L4` | 100 mm | drawing: PKG-TUCK-02 rev B |
| `T_load` | 4 N*m | assumption: Example flap-folding torque at the rocker shaft; measure it on the station. |
| `circuit` | Open | drawing: PKG-TUCK-02 rev B |
| `follower` | Roller | assumption: Cam mode only; this four-bar linkage does not use it, so the template default is kept. |
| `load_dir` | Opposes motion | assumption: The flap resists the blade in whichever direction it moves. |
| `mode` | Four-bar | spec: Carton line PKG-LINE-7 §3.2 tucker station rev C |
| `mu_req` | 40 deg | assumption: Default minimum transmission angle; set it from your design rule. |
| `n_in` | 60 rpm | spec: Carton line PKG-LINE-7 §3.2: 60 cartons/min rev C |
| `steps` | 72 | assumption: 5° table step. |
| `theta_range` | 360 deg | spec: Carton line PKG-LINE-7 §3.2: one cycle rev C |
| `theta_start` | 0 deg | spec: Carton line PKG-LINE-7 §3.2: crank zero at the home position rev C |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `S_plus_L` | 195 mm | 195 mm | yes |
| `P_plus_Q` | 240 mm | 240 mm | yes |
| `theta4_0` | 93.8909 deg | 93.8909 deg | yes |
| `mu_min` | 48.4409 deg | 48.4409 deg | yes |
| `swing` | 54.7864 deg | 54.7864 deg | yes |
| `omega_out_pk` | 3.54346 rad/s | 3.54346 rad/s | yes |
| `output_motion` | 0.354346 m/s | 0.354346 m/s | yes |
| `peak_accel` | 3.16246 m/s^2 | 3.16246 m/s^2 | yes |
| `input_torque` | 2.26544 N*m | 2.26544 N*m | yes |

Governing check: Required transmission angle against the smallest transmission angle — PASS (utilisation 0.83). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
