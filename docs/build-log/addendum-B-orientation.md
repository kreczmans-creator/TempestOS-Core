# Addendum B — orientation (2026-10-24)

**Addendum A state.** All 13 items built, none dropped (STATUS: READY FOR OWNER TESTING). L1 linking as built: an input or a numeric table cell links to a declared output port of another calculation in the same project, pinned to its revision (`CalcProvenance.Link`, `CalcInstanceService.BindAsync` / `BindCellAsync`, ADR-0168 with amendments 1–3). Staleness passes through links, record revisions and template changes at every depth; a stale or draft upstream blocks Check and Issue (except members of one pack). Pack templates (`CalcPackTemplate`, `CalcPackBinding` with optional `Row`/`Column`, example inputs per step) instantiate a linked pack after an in-memory dry run. Reference records (`CalcDataRecord`, kinds per template, `LicensedSource` lock: rows withheld on issued sheets, exports refused, re-import blocked).

**Calculators Addendum B reuses (ID → class, key ports).**

| Ref | ID | Class (src/Tempest.Calc/…) | Ports used by chains |
|---|---|---|---|
| M1 | `mech.tolerance-stack` | Mechanical/ToleranceStackCalculator | (none declared; stack logic inside `Run`) |
| M2 | `mech.iso286-fit` | Mechanical/Iso286FitCalculator (+ `Iso286`) | fit limits |
| M3 | `mech.bolt-group` / `mech.bolted-joint` | BoltGroupCalculator / BoltedJointCalculator | `v_max`, `t_max` |
| M4 | `mech.shaft` | ShaftCalculator | `R_A`, `d_A` |
| M5 | `mech.bearing-life` | BearingLifeCalculator | `L_10h` |
| M6 | `mech.weld-group` | WeldGroupCalculator (+ `GroupGeometry`) | stresses |
| E1 | `elec.motor-sizing` | Electrical/MotorSizingCalculator | `T_rms`, `T_pk`, `n_pk`, `P_rms`, `P_cont` |
| E2 | `elec.enclosure-heat` | EnclosureHeatCalculator | `P_cool`, `T_max` |
| E3 | `elec.voltage-drop` | VoltageDropCalculator | — |
| E4 | `elec.conductor-sizing` | ConductorSizingCalculator | `I_b`, `I_n`, `CSA`, `I_z`, `L`, `device_code` |
| E5 | `elec.fault-protection` | FaultProtectionCalculator | `Z_s`, `I_f` |
| E6 | `elec.load-budget` | LoadBudgetCalculator | `I_3ph`, `Phi` |
| S1 | `struct.section-properties` | Structural/SectionPropertiesCalculator | section props |
| S2 | `struct.frame-2d` | FrameCalculator (`Tempest.Calc.Structural.Frame` solver) | `N_e1`, `M_abs_e1`, `V_e1`, `R1_*` |
| S3 | `struct.en1993-member` | MemberChecksCalculator | utilisations |
| S4 | `struct.connection-ec3` | ConnectionCalculator | `F_t_Rd`, `A_s`, `F_b_Rd`, `f_ub`, `f_yb` |
| S5 | `struct.load-combinations` | LoadCombinationsCalculator | `E_d_min`, combinations |
| S6 | `struct.base-plate` | BasePlateCalculator | `F_t_Ed`, `N_j_Rd`, `u_max` |
| B1 | `bs.pipe-pump` | BuildingServices/PipeFlowCalculator (+ `Fluids/Water`) | `Q_design`, `T_shaft`, `n_shaft`, `V_pipe` |
| B2 | `bs.psychrometrics` | PsychrometricsCalculator (+ `Fluids/MoistAir`, Magnus/Hyland–Wexler) | states |
| B4 | `bs.room-heat-loss` | RoomHeatLossCalculator | `Phi_HL`, `HLC` |
| B6 | `bs.sealed-system` | SealedSystemCalculator | `Phi_tot`, `dT_fr`, `t_m` |

**B6 gas-law kernel.** None. The vessel relation `V_e = e·V_s/[1 − (p_i + 1)/(p_f + 1)]` (isothermal Boyle on the gas side, absolute pressures) is an inline engine expression; water density (Kell 1975) is private to the calculator. B-0 extracts a shared `GasLaw` kernel (isothermal/adiabatic/polytropic p·Vⁿ, absolute-pressure handling) for B6 and F2 without changing B6's numbers.

**M1 stack engine.** Not callable as an API: worst-case/RSS/Monte Carlo logic lives in `ToleranceStackCalculator.Run`; only a table helper is public. B-0 extracts a pure `ToleranceStack` engine (inputs: contributors with nominal/limits/direction/distribution; outputs: worst case, RSS, MC statistics) used by M1 and by W2/scan-to-seal, with M1's tests unchanged.

**Disciplines.** Two registries exist. (1) Core (ADR-0163): engineering objects with typed attributes (`IDisciplineObject`, `src/Tempest.Core/EngineeringDomain/Contracts/DisciplineObjects.cs`) for Structural, Electrical, Building Services/HVAC beside Mechanical. (2) Calc: `CalcDiscipline` enum (`src/Tempest.Calc/Tempest.Calc.Sheets/Template.cs`: Mechanical, Structural, Electrical, BuildingServices) with Desktop labels in `CalcSheetView.cs` (picker, `DisciplineLabel`) and `MainWindowComposer.Layout.cs`, composition in `CalcCalculatorComposition`. Adding Fluid Power = enum member + labels + new `Tempest.Calc.FluidPower` assembly and `Tempest.Calc.Tests.FluidPower` project + composition + Core discipline kind following ADR-0163 + catalogue/search/Ctrl+K entries. ADR number: **ADR-0170** (next free; DB-01).

**Sequencing.** Addendum A §A9 met at 0772fcf8; Addendum B starts now, phases B-0 → B-5 per §B2, each on its own `v0.26.0/addB-*` branch, merged `--no-ff` when green; Opus review per phase (DA-16).
