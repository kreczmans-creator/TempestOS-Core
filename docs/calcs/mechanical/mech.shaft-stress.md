# M9 — Shaft under combined bending and torsion (`mech.shaft-stress@1.0.0`)

**In plain terms.** A rotating shaft is bent by the loads it carries and twisted by the torque it transmits. The bending stress and the twisting shear stress peak together at the surface, and the two failure theories in common use combine them differently: the maximum-shear-stress theory is the more conservative, the von Mises theory the closer to test. The factor of safety is the yield strength over the combined stress, raised at a shoulder or keyway by the stress-concentration factors.

**Scope.** Solid round shaft, static loading. Ported from the legacy `calc.shaft-combined-stress` (ADR-0167). Clause numbers only (Shigley §5-4, §5-5, §7-4). `S_y` fills from a released material record.

**Method.** `σ = K_t·32M/(πd³)`; `τ = K_ts·16T/(πd³)`; `τ_max = √((σ/2)² + τ²)`; `σ' = √(σ² + 3τ²)`; `n_T = (S_y/2)/τ_max`; `n_VM = S_y/σ'`.

**Inputs.** `d`, `M`, `T`, `S_y`, `K_t`, `K_ts`, `n_req`. **Outputs.** `sigma`, `tau`, `tau_max`, `sigma_vm`, `n_T`, `n_VM`, `n_min`. **Checks.** governing factor against required.

**Limitations.** Static strength only (fatigue is `mech.fatigue-miner`); no hollow shafts; no axial load.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-1`, `ka-legacy-2`, `ka-legacy-3`).
