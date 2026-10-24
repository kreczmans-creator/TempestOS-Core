# Addendum A — orientation (2026-10-24)

**Where v0.26.0 stands.** Phase 7/7 done; every §10 gate passed at `6afead3` (release head `7c43ef0`). No item in progress. `v0.26.0-rc1` exists as a *local* tag only (the push was refused, HTTP 403); left as is (DA-01). Per A0.3 the addendum is built now and Security plus every colour team re-run on the delta.

**Calculators present (all pass DoD, none dropped).** Under `src/Tempest.Calc/Tempest.Calc.<Discipline>/`:

| Item | ID | File |
|---|---|---|
| M1 | `mech.tolerance-stack` | `Mechanical/ToleranceStackCalculator.cs` |
| M2 | `mech.iso286-fit`, `mech.press-fit`, `mech.thick-cylinder` | `Mechanical/Iso286FitCalculator.cs`, `PressFitCalculator.cs`, `ThickCylinderCalculator.cs` |
| M3 | `mech.bolted-joint`, `mech.bolt-group` | `Mechanical/BoltedJointCalculator.cs`, `BoltGroupCalculator.cs` |
| S1 | `struct.section-properties` | `Structural/SectionPropertiesCalculator.cs` |
| S2 | `struct.frame-2d`, `struct.beam-formulas` | `Structural/FrameCalculator.cs`, `BeamFormulasCalculator.cs` |
| S3 | `struct.en1993-member` | `Structural/MemberChecksCalculator.cs` |
| B1 | `bs.pipe-pump` | `BuildingServices/PipeFlowCalculator.cs` |
| B2 | `bs.psychrometrics` | `BuildingServices/PsychrometricsCalculator.cs` |
| B3 | `bs.duct-sizing` | `BuildingServices/DuctSizingCalculator.cs` |
| E1 | `elec.motor-sizing` | `Electrical/MotorSizingCalculator.cs` |
| E2 | `elec.enclosure-heat` | `Electrical/EnclosureHeatCalculator.cs` |
| E3 | `elec.voltage-drop` | `Electrical/VoltageDropCalculator.cs` |

**Engine.** Own implementation (D-01), not CalcpadCE and not UnitsNet: `Tempest.Calc.Engine`, a hand-written parser and evaluator over a runtime SI dimension vector with a step log. `ICalcEngine` = `EngineVersion`, `CreateSession(CalcLimits?, significantFigures)` → `ICalcSession` (define, evaluate, check, notice), `Validate(expression)`. Calculators implement `ICalcCalculator` (`Template` + `Calculate(ICalcSession, CalcInputSet)`), run by `CalcRunner`.

**How outputs are exposed today.** `CalcRunResult.Outputs` (`CalcOutput`: symbol, SI value, unit, text, description); an instance stores `CalcOutputSnapshot`s and an `OutputsHash`. A first form of linking exists: an input whose provenance is `reference: calc:<instanceId>` names another instance, `CalcInstance.UpstreamIds` lists them, and `StalenessAsync` flags a downstream instance when the upstream `OutputsHash` changed; `IssueAsync` refuses a stale instance. Missing for L1: a `link` provenance kind bound to an upstream *output symbol and revision*, declared output ports with dimensions, dimension check, cycle and depth checks, propagation on revise/supersede, a re-pin action, and Check refusing a stale instance. Packs exist (`CreatePackAsync`, `CheckPackAsync`, `IssuePackAsync`: one Evidence bundle, index, revision table).

**v0.25.2 bolt shear.** `calc.bolt-shear-capacity` (`src/Tempest.Core/Calculations/EngineeringCalculationDefinitions.cs`) takes the shear strength from `StandardParts.BoltShearStrengths` (`src/Tempest.Core/Calculations/Modules/CalculationModuleDescriptors.cs`): **0.6 × fub for every class**, including 10.9 (EN 1993-1-8 Table 3.4: αv = 0.5 for 4.8, 5.8, 6.8 and 10.9 with threads in the shear plane). Reconciled in S4 (A-3).

**Branching.** Addendum work branches from and merges `--no-ff` into `release/v0.26.0`; `release/v0.27.0` (built on v0.26.0) takes it by a forward merge after the addendum (DA-02).
