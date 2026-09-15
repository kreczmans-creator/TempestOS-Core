# Fillet weld sizing, throat stress under combined load — `calc.fillet-weld-throat-stress`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.FilletWeldThroatStressCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/FilletWeldThroatStressVectors.cs`.

## Method

The simplified method of EN 1993-1-8 (Eurocode 3, design of joints),
clause 4.5.3.3: the resultant of all the forces a fillet weld transmits,
per unit length, must not exceed the weld's design resistance per unit
length, whatever the orientation of the forces to the weld axis. The
design shear strength of the weld is the ultimate strength of the weaker
part joined, divided by √3, by the correlation factor β_w of clause 4.5.3.2
(Table 4.1) and by the partial factor γ_M2. Divided through by the throat
thickness, the same check reads as a throat stress against a design shear
strength, which is how this module reports it. The clause is restated,
not reproduced.

## Inputs

| Input | Dimension (typical units) | Limits |
|---|---|---|
| `ParallelForce`, `TransverseForce`, `NormalForce` | Force | any sign; not all zero |
| `EffectiveLength` L | Length | ≥ max(30 mm, 6·a) — method limit |
| `ThroatThickness` a | Length | ≥ 3 mm — method limit |
| `UltimateStrength` f_u | Pressure | > 0; the weaker part joined, from its material record |
| `CorrelationFactor` β_w | — | > 0 (0.8 S235, 0.85 S275, 0.9 S355, 1.0 S420 and S460) |
| `PartialFactor` γ_M2 | — | ≥ 1 (1.25 recommended) |

## Formulae

1. Resultant force F = √(F_∥² + F_⊥² + F_n²).
2. Design shear strength f_vw,d = f_u / (√3 · β_w · γ_M2).
3. Throat stress τ_w = F / (a · L).
4. Weld design resistance F_w,Rd = f_vw,d · a · L (a Force).
5. Utilisation = τ_w / f_vw,d = F / F_w,Rd.
6. Required throat a_req = F / (f_vw,d · L); governing required throat =
   max(a_req, 3 mm); equal-leg size = √2 · throat.
7. Outcome meets criteria when utilisation ≤ 1 (framework slack).

## Outputs and recorded intermediates

Results: resultant force, design shear strength, throat stress, weld
design resistance, utilisation, computed and governing required throat,
required leg, outcome. Intermediates: f_vw,d, the resultant, the
utilisation, the 3 mm and length-minimum checks.

## Limits of applicability (refusals, not exceptions)

- **Throat below 3 mm** (clause 4.5.2(2)): refused.
- **Effective length below max(30 mm, 6·a)** (clause 4.5.1(2): such a weld
  should not be designed to carry load): refused.

## Worked examples (hand calculations)

**Example 1 — S355 lap weld in longitudinal shear.** f_u = 490 MPa,
β_w = 0.9, γ_M2 = 1.25, a = 5 mm, L = 200 mm, F_∥ = 150 kN.
f_vw,d = 490 / (1.732 05 × 0.9 × 1.25) = 490 / 1.948 56 = **251.47 MPa**.
τ_w = 150 000 / (5 × 200) = **150.00 MPa**. F_w,Rd = 251.47 × 1 000 =
**251 468 N**. Utilisation **0.59650**. a_req = 750 / 251.47 = **2.9825 mm**,
governing **3.0 mm**; leg for the 5 mm throat 7.0711 mm. Meets criteria.

**Example 2 — S235, combined parallel and transverse load.** f_u = 360 MPa,
β_w = 0.8, γ_M2 = 1.25, a = 4 mm, L = 120 mm, F_∥ = 40 kN, F_⊥ = 30 kN.
F = 50 kN. f_vw,d = 360 / (1.732 05 × 0.8 × 1.25) = 360 / 1.732 05 =
**207.85 MPa**. τ_w = 50 000 / 480 = **104.17 MPa**. Utilisation **0.50117**.
a_req = 416.67 / 207.85 = **2.0047 mm**, governing 3.0 mm. Meets criteria.

**Example 3 — overloaded normal weld.** S355 as Example 1, a = 4 mm,
L = 100 mm, F_n = 120 kN. τ_w = 120 000 / 400 = **300.00 MPa**; utilisation
300 / 251.47 = **1.1930**; a_req = 1 200 / 251.47 = **4.7719 mm**.
Does not meet criteria.

Precision: five significant figures; relative tolerance 1 × 10⁻⁵.

## Edge cases

- **Zero or negative:** all three forces zero, or a non-positive length,
  throat, f_u or β_w, or γ_M2 < 1 → invalid input. Negative force
  components are legitimate (direction) and are squared away.
- **Out of range:** a = 2.5 mm → refused; L = 20 mm → refused; a = 6 mm
  with L = 35 mm (< 6a = 36 mm) → refused.
- **Unit mismatch:** Example 1 in lbf, inches and psi gives the same
  base-unit results.
