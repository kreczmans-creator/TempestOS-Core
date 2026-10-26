# M9 — Shaft under combined bending and torsion (`mech.shaft-stress@1.0.1`)

**In plain terms.** A rotating shaft is bent by the loads it carries and twisted by the torque it transmits. The bending stress and the twisting shear stress peak together at the surface, and the two failure theories in common use combine them differently: the maximum-shear-stress theory is the more conservative, the von Mises theory the closer to test. The factor of safety is the yield strength over the combined stress, raised at a shoulder or keyway by the stress-concentration factors.

**Scope.** Solid round shaft, static loading. Ported from the legacy `calc.shaft-combined-stress` (ADR-0167). Clause numbers only (Shigley §5-4, §5-5, §7-4). `S_y` fills from a released material record.

**Method.** `σ = K_t·32M/(πd³)`; `τ = K_ts·16T/(πd³)`; `τ_max = √((σ/2)² + τ²)`; `σ' = √(σ² + 3τ²)`; `n_T = (S_y/2)/τ_max`; `n_VM = S_y/σ'`.

**Inputs.** `d`, `M`, `T`, `S_y`, `K_t`, `K_ts`, `n_req`. **Outputs.** `sigma`, `tau`, `tau_max`, `sigma_vm`, `n_T`, `n_VM`, `n_min`. **Checks.** governing factor against required.

**Limitations.** Static strength only (fatigue is `mech.fatigue-miner`); no hollow shafts; no axial load.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-1`, `ka-legacy-2`, `ka-legacy-3`).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 40 mm shaft in bending and torsion ([sheet](../../worked-examples/mechanical/mech.shaft-stress/shaft-40mm/sheet.txt)).

```text
sigma = 1 × 32 × 500 N·m / (π × (40 mm)^3) = 79.577 MPa
tau = 1 × 16 × 800 N·m / (π × (40 mm)^3) = 63.662 MPa
tau_max = sqrt((79.577 MPa / 2)^2 + (63.662 MPa)^2) = 75.073 MPa
sigma_vm = sqrt((79.577 MPa)^2 + 3 × (63.662 MPa)^2) = 135.98 MPa
n_T = 350 MPa / 2 / 75.073 MPa = 2.3311
n_VM = 350 MPa / 135.98 MPa = 2.5739
n_min = min(2.3311, 2.5739) = 2.3311
u = 2 / 2.3311 = 0.85798
Governing: PASS  Factor of safety against the required factor: u = 0.86 vs u ≤ 1  utilisation 0.86  « Shigley §7-4
```
<!-- hand-arithmetic:end -->
