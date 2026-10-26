using System.Globalization;
using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum A, §A7: one issued calc pack per built-in chain (drive train, motor circuit, wet heating, cooling, steel support, hydraulic power unit, mechanism review, actuated linkage, envelope moisture, cold pipe, pipework supports, electronics enclosure), written
/// under docs/worked-examples/packs/&lt;chain&gt; when TEMPEST_WRITE_WORKED_EXAMPLES=1: the pack's issue sheet, each step's issued sheet,
/// every step's inputs and a README naming the chain, every link and the issue reference. The PDFs are made later.
/// </summary>
public sealed class ChainPackWorkedExampleTests
{
    public static TheoryData<string> Chains => ["drive-train", "motor-circuit", "wet-heating", "cooling", "steel-support", "hydraulic-power-unit", "mechanism-review", "actuated-linkage", "envelope-moisture", "cold-pipe", "pipework-supports", "electronics-enclosure"];

    private sealed record Example(CalcPackTemplate Template, string Slug, string Title, string Reference, string Client, string Project, string Note, Func<LibraryTestHost, Task<IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>>>> Inputs, string Remarks);

    private static IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>> Plain(Dictionary<string, IReadOnlyList<CalcInputValue>> inputs) => inputs;

    private static readonly Example[] Examples =
    [
        new(CalcPackTemplates.DriveTrain, "drive-train", "Conveyor head drive calc pack", "CNV-DRV-001", "Client Co", "Conveyor 7 head drive", "Checked against SPEC-CNV-07 and DRG-CNV-410 to 412 rev B.",
            host => Task.FromResult(Plain(ChainInputs.DriveTrain(host))),
            "A 4-pole motor (about 23 kW at 1475 rpm) turns a conveyor drum at 118 rpm through a 12.5:1 gearbox against a steady 1.8 kN·m. The shaft is the motor-side shaft, the gearbox input shaft: its steady torque is the motor's load torque at the motor shaft (151.6 N·m, no ratio or efficiency applied; the output-side torques are separate ports of the motor step for a shaft after the gearbox), its alternating torque is the motor step's torque swing (0 in continuous duty, as here; (T_pk − T_dec)/2 on a point-to-point cycle) and its speed is the motor's; the bearing's axial load is the motor step's peak ball-screw thrust (0 for this rotary drive); the shaft's reaction and speed give bearing A its load and speed; the shaft's seat diameter is the nominal size of the bearing-bore fit (the bore has its own tolerance, entered from the bearing record, against a k6 seat); and the peak torque the load demands at the motor shaft (T_pk, run-up included) is the torque the four-bolt coupling flange carries and the torque the shaft's yield check sees. The gear mesh forces on the shaft are entered for the motor's torque (a pinion of 60 mm pitch radius), not linked."),
        new(CalcPackTemplates.MotorCircuit, "motor-circuit", "Servo axis motor circuit calc pack", "LINE-ELEC-001", "Client Co", "Line 12 axis 3", "Checked against SPEC-LINE-12 and DRG-LINE-120 rev B.",
            async host => Plain(await MotorCircuitInputsAsync(host)),
            "A ball-screw servo axis (50 kg, 0.5 m in 0.5 s) is sized, its panel demand and heat found, its cable and device chosen, and the voltage drop, earth-fault disconnection and enclosure temperature checked. **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**"),
        new(CalcPackTemplates.WetHeating, "wet-heating", "Plant room heating calc pack", "HTG-001", "Client Co", "Plant room heating", "Checked against SPEC-HTG-01 §3.",
            host => Task.FromResult(Plain(ChainInputs.WetHeating(host))),
            "Two rooms' design heat losses are the two rows of the sealed system's room table (table-cell links); the sealed system's total heat, flow-to-return difference and mean water temperature give the primary pipework its flow and fluid; the pump's shaft torque and speed are the circulator motor's load. The loop is closed, so there is no static lift, and the 40 m of 10 mm microbore carries about 57 L/h for a 0.64 kW load: the pump duty is a deliberately tiny illustration. The sealed system's pipework water content (3.14 L) is entered, equal to the pipework's own V_pipe, because a link back from the pipework would be a cycle."),
        new(CalcPackTemplates.Cooling, "cooling", "Drive panel cooling calc pack", "MCC-COOL-001", "Client Co", "Motor control centre 3", "Checked against SPEC-MCC-03 and DRG-MCC-210/215 rev B.",
            host => Task.FromResult(Plain(ChainInputs.Cooling(host))),
            "The heat a drive panel dissipates is a linked load of its enclosure; the heat the enclosure cannot lose naturally is the required duty of a water-cooled air coil. The coil's hot side is the enclosure air, entering at the enclosure's temperature limit (E2 `T_max`, linked) with c_p 1006 J/(kg·K), the conventional dry-air value E2 itself uses; the air flow through the coil (0.4 kg/s) and U = 60 W/(m²·K) are the coil's own figures, and no intermediate water loop inside the enclosure is modelled. The coil's duty, cold-side temperature rise, mean cold-side temperature and cold-side specific heat (linked, so a glycol mixture changes the flow) give the cooling-water pipework and pump their flow and fluid. The enclosure step's own check, natural dissipation holding the limit, FAILS by design: that is the reason the coil is needed, and the duty it hands over is exactly the shortfall."),
        new(CalcPackTemplates.SteelSupport, "steel-support", "Equipment support steelwork calc pack", "STR-SUP-001", "Client Co", "Equipment support ES-4", "Checked against SPEC-STR-09 and DRG-STR-310 to 315 rev B.",
            host => Task.FromResult(Plain(ChainInputs.SteelSupport(host))),
            "The equipment's dead (12 kN) and maintenance (8 kN) loads, both downward, are combined (EN 1990 6.10: 28.2 kN); that ULS effect is a cell of the frame's load table; the frame's column forces check the column; the arm's end shear and moment load a six-bolt group whose governing-bolt shear and tension are the arm connection's design forces (S4); the frame's base reactions are the base plate's actions; and a second S4 run for the 25 mm base plate gives S6 its holding-down bolt data. The 5 kN horizontal action at the column top is an ULS design value (combined at factor 1). **The links depend on the frame's numbering: nodes 1 (column base, fixed), 2 (column top) and 3 (arm tip); member 1 the column (1 → 2) and member 2 the arm (2 → 3); the first load row the vertical load at node 3 (filled from the load combinations) and the second the horizontal action at node 2.** The bolt group's bolt resistances (94.0 kN shear, 141.0 kN tension) are typed values equal to what the arm connection step computes for M20 8.8; a test guards the match. The holding-down bolts' own demand fields are zero: S6 checks the anchor bolts against the base reactions. **The anchorage (concrete and anchor design) is done in Hilti PROFIS: the file attached to the base-plate step is a PLACEHOLDER, not a PROFIS report, to be replaced by the engineer's own report before the pack is used.**"),
        new(CalcPackTemplates.HydraulicPowerUnit, "hydraulic-power-unit", "Press hydraulic power unit calc pack", "HPU-001", "Client Co", "Press 5 hydraulic power unit", "Checked against SPEC-HPU-05 and DRG-HPU-510 rev B.",
            async host => Plain(await HydraulicPowerUnitInputsAsync(host)),
            "A 100/70 × 500 mm cylinder at 160 bar and 36 L/min, fed by a 28 cm³/rev pump at 1450 rpm (38.57 L/min delivered), presses with 80 kN and pulls with 40 kN. Its swept volume (3.927 L, the extend stroke) and its supply pressure (160 bar, a gauge pressure, so the accumulator's pressures stay on the Gauge basis; Absolute would read the linked value one atmosphere low and the sheet would warn) size an emergency-stroke accumulator (30.1 L of gas, held at 160 bar after a slow charge and discharged at n = 1.4, 10 to 50 °C, working range 120 to 160 bar; 120 bar still finishes the 80 kN push, which needs 107 bar).\n\nThe pressure-line step judges the line the cylinder step judges, the line to the cylinder cap port: its flow is the cylinder step's `Q_prs`, the larger of the two strokes, the 75.6 L/min retract discharge (4.54 m³/h; the 36 L/min circuit flow only passes the cap port on the extend stroke), and the oil density (870 kg/m³) and the 20 mm bore (the first row of its segment table) are linked from the cylinder step's `rho` and `d_prs` too, so revising the cylinder's line bore makes the pressure line stale (a test proves it). B1 judges the cap-port line at the retract discharge, where its pressure drop (about 2.5 bar over the 78.5 cm² cap area) is the back pressure the retract stroke pushes against; neither that drop nor the supply-side drop is fed back into F1, whose back pressure and supply pressure are entered.\n\nThe 12 m line runs at 4.01 m/s, inside the 5 m/s limit (a 16 mm line would read 6.3 m/s and fail the cylinder step).\n\n**Transitional flow:** at 75.6 L/min in 20 mm with the 30 mPa·s oil the Reynolds number is 2327, just above the 2300 where the pressure-line sheet switches from f = 64/Re to Colebrook–White; the sheet warns that the friction factor is uncertain and uses the Colebrook value (f = 0.0489, against 0.0275 for 64/Re), which is the larger and so the conservative one, so the line costs 247.5 kPa (2.5 bar, 1.5 % of the 160) where the laminar relation would give 157.6 kPa; the step is not flagged laminar, and the oil's viscosity is taken at the 50 °C working temperature only (colder oil is more viscous and would drop more pressure).\n\nThe 6 velocity heads of fittings are a turbulent-flow coefficient; the real K rises at low Reynolds number, so at Re 2327 the fittings loss (42.0 kPa of the 247.5) is understated, and the sheet says so (`fittings` warning).\n\nThe pump's shaft torque (76.97 N·m) and speed (1450 rpm) are the continuous load of its drive motor (77.75 N·m through a 0.99 coupling, 11.81 kW); the candidate motor is rated 120 N·m at a rated speed of 1460 rpm (an INVENTED nameplate figure for this example, to be replaced from the real motor's nameplate; the pump runs at 1450 rpm), which is 18.35 kW, and **that rated power, not the load, sizes the supply cable** (400 V, η 0.92, cos φ 0.86: 33.5 A, a 40 A type C device, 10 mm²), so the cable is good for the motor that will be fitted up to its rating, not only for this pump.\n\nThe cable's design current, length and conductor area give the **voltage drop** (3.16 V, 0.79 % of 400 V) and, with its device rating and type code, the **earth-fault check** (loop impedance 0.174 Ω against a 0.546 Ω limit for the 40 A type C device, 400 A to trip; the 10 mm² protective conductor is the cable's size by hand; the 0.05 Ω external loop is the INVENTED figure of a close-coupled 400 kVA transformer).\n\nThe supply transformer (400 kVA, 4 %: INVENTED) is entered once, on the fault-protection step, and its three-phase fault current (15.94 kA, `I_k3`, computed with c = 1.1, which also reaches the start step as `c_k3`) is the source of the **motor start**: the start step takes that fault current, the cable's length and conductor area, and the motor's rated power (18.35 kW) by link, and judges a star-delta start (7 × I_rated / 3 = 78.1 A, the 7 × locked-rotor ratio and 0.3 starting power factor INVENTED) as a 1.25 % dip at the motor terminals (0.53 % at the point of common coupling; **this is the fault level at the transformer terminals, a lower bound on the dip**: the supply cabling and any upstream network are not in it, and the start step's `S_kQ` is where an upstream network is added), with 5.28 kvar to correct the running power factor from 0.86 to 0.95.\n\nThe start step comes last because it takes from both the cable and the fault protection; its rated current is **not** fed back to the cable step (cable, fault protection and start would then form a cycle), so the cable is still sized on the motor's rated power, and the two steps' copies of the motor's efficiency and power factor are checked equal by a test (and their rated current and design current agree at 33.47 A).\n\nThe pump is started **unloaded**, through a vent or unloading valve to tank, not against its relief valve (a pump held at relief pressure is at full load, and in star the motor gives about a third of its direct-on-line torque: the drive step's run-up check, made at the direct-on-line peak torque, would not cover a loaded star start; a test checks that a third of the 275 N·m peak covers the safety factor times the run-up and an invented 20 N·m unloaded torque).\n\nThe start current is for coordination only: the star-delta 78 A stays below the 200 A at which a 40 A type C device's instantaneous band begins, where a direct-on-line start of the same motor (234 A) would not; the open-transition changeover from star to delta is not modelled and can briefly approach the direct-on-line current; the fault current was computed at √3 × 230 V = 398.4 V and the start at the motor's 400 V, 0.4 % apart (conservative).\n\n**The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**"),
        new(CalcPackTemplates.MechanismReview, "mechanism-review", "Carton tamper cam calc pack", "TMP-001", "Client Co", "Carton tamper station 4", "Checked against SPEC-TMP-04 and DRG-TMP-410 to 420 rev B.",
            async host => Plain(await MechanismReviewInputsAsync(host)),
            "A carton tamper: a 4 kg follower on a Ø30 roller (60 mm base circle, in line) rises 40 mm in 120° (cycloidal), dwells 60°, returns in 120° (modified trapezoid) and dwells 60°, at 150 cam rpm against a return spring of 9.874 N/mm and 50 N preload (all INVENTED). The cam step finds a peak follower speed of 0.6 m/s and acceleration of 14.1 m/s², a peak pressure angle of 22.3° against the 30° limit, no undercut (smallest radius of curvature 73.3 mm against the 15 mm roller), a follower that never leaves the cam (smallest force 50 N), a spring force at full lift of 444.96 N (which is also the peak roller contact force, because the pressure angle is zero at the top), a peak input torque of 10.11 N·m and an RMS input torque of 4.816 N·m. The spring force at full lift is the return spring's second working load (a 6 mm wire on a 50 mm coil, 10.5 active coils, 145 mm free: rate 9.874 N/mm, working travel 40.0 mm, shear stress 305.5 MPa at full lift, surge frequency 82 Hz, 33 times the 2.5 Hz cam frequency); the contact force (N into kN) is a cell of the cam shaft's load row (Ø20/25/20, bearings 250 mm apart, 222.5 N on each, M_max 27.8 N·m), the peak input torque is the shaft's alternating torque amplitude and its peak torque, and the speed is the shaft's speed; the shaft's bearing reaction and the cam speed give bearing A its load and speed (L₁₀ 4.33·10⁵ h on INVENTED ratings, C 3.5 kN and C₀ 1.8 kN); and the RMS torque and speed are the load of a 9.6:1 geared motor (0.557 N·m at 1440 rpm, 84.1 W, against an INVENTED nameplate of 1.0 N·m at that 1440 rpm = 150.8 W), whose rated power sizes the supply cable (0.34 A, a 6 A device, 1.5 mm²). **Not linked, deliberately.** The spring step cannot give its rate and preload back to the cam (a cycle), so the cam carries a typed 9.874 N/mm and 50 N, and a test checks that they are the spring step's computed rate (to 0.01 %) and preload; the cam's lift is the spring's expected working travel (`s_work`: the spring step warns `travel-mismatch` above 1 %, which is what a revised cam rate that the spring no longer matches shows at run time) and the cam frequency in Hz (`f_in`; a speed in rpm is an angular velocity and cannot be linked to a frequency) the spring's operating frequency; the cam's peak torque is checked against the motor's 3 N·m peak by a test, since the motor step is judged on the RMS torque. **The cam's torque enters the shaft as an alternating torque, with a steady torque of 0:** the cam's torque is fully alternating (mean zero, −9.61 to +10.11 N·m), so its peak magnitude is the shaft's alternating amplitude (a conservative bound on the swing); a test recomputes the shaft's fatigue factor independently (9.0, 6 times the 1.5 target) and checks that the cam's mean torque is a small fraction of the amplitude. Bending dominates here, so the earlier steady-RMS stand-in (8.97) read nearly the same; it would understate the stress on a torque-dominated or keyed section. **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**"),
        new(CalcPackTemplates.ActuatedLinkage, "actuated-linkage", "Ejector cylinder calc pack", "EJT-001", "Client Co", "Ejector station 2", "Checked against SPEC-EJT-02 and DRG-EJT-210 rev B.",
            host => Task.FromResult(Plain(ChainInputs.ActuatedLinkage(host))),
            "A slider-crank ejector (a 40 kg slide on a 100 mm crank and 300 mm rod, in line, at 30 rpm against a 3 kN load that always opposes the motion; invented) is to be replaced by a hydraulic cylinder. The linkage step finds a 200 mm stroke, a peak slide speed of 0.3313 m/s and a peak slide-direction force of 3052.6 N (m a + F_L with a = r ω² (1 + r/l) = 1.316 m/s²). **The link into the cylinder is a kinematic proxy.** A cylinder-actuated linkage runs at about constant cylinder speed, not at constant crank speed, so the crank's peak speed and peak force are what the cylinder must reach and carry, not what it would reproduce through the stroke. The peak speed (m/s into mm/s) is the cylinder step's required extend speed, which gives its flow (the flow field is blank): 25.49 L/min on a 40 mm bore at 0.98 volumetric efficiency; the peak force (N into kN) is the load to push and, conservatively for both strokes, to pull. The 40/22 cylinder at 100 bar (extend 11.94 kN, retract 8.33 kN, 3.05 kN needed) passes all six checks, with the cap-port line at 36.5 L/min and 3.03 m/s in a 16 mm line and the rod at 0.28 of its screening limit. The cylinder's stroke is typed (200 mm, the slide's stroke; the linkage has no stroke port) and a test checks it. Its swept volume (0.2513 L) and supply pressure (100 bar, gauge, so the accumulator's basis stays Gauge) size an emergency-stroke accumulator (1.310 L of gas, held at 100 bar after a slow charge and discharged at n = 1.4, 10 to 50 °C, working range 60 to 100 bar; 60 bar still finishes the stroke under the peak force, which needs 25.6 bar). A faster crank (45 rpm) raises the flow to 38.2 L/min and the line velocity to 4.54 m/s, still inside the 5 m/s limit, and leaves the accumulator's sizing unchanged (a test proves it)."),
        new(CalcPackTemplates.EnvelopeMoisture, "envelope-moisture", "Internally insulated wall moisture calc pack", "ENV-MOI-001", "Client Co", "Studio 3 external wall", "Checked against SPEC-ENV-02 and DRG-ENV-210 rev B.",
            host => Task.FromResult(Plain(ChainInputs.EnvelopeMoisture(host))),
            "A solid brick wall (215 mm) lined inside with 80 mm of mineral wool and 12.5 mm plasterboard, rendered outside, in a room held at 21 °C and 50 % relative humidity. The heat-loss step finds the wall's U-value (0.3991 W/m²K, 89 % of its 0.45 target); the psychrometrics step gives the room air state, and its dry-bulb (21 °C) and relative humidity (50 %, as a percentage quantity) are the Glaser step's internal conditions. The Glaser step checks the same wall, with a 0.2 mm vapour control layer behind the plasterboard (μ 20000, an invented figure for a taped but imperfect membrane), over twelve months of an INVENTED maritime climate: the vapour pressure touches saturation only at the interface between the mineral wool and the brick, condensate collects from December to February (peak 9.8 g/m² in January, against an invented 500 g/m² limit), and the wall is dry again in March; the surface temperature factor (0.905) is far above the 0.609 the room needs at 80 % critical humidity. **The wall's layers are typed in both the heat-loss and the Glaser steps** (the heat-loss step offers no per-layer output to link); the one value they share, the wall's U-value, is linked from the heat-loss step to the Glaser step, which compares it with 1/R_T of its own layers and warns if they differ by more than 2 % (here 0.02 %: the vapour control layer, 0.2 mm, is the only difference, and it is thermally negligible). **That comparison is of the thermal resistance only: it cannot see a swapped layer order or a wrong vapour resistance factor in the twice-typed layers, either of which changes the Glaser result, so the layers must be checked against each other by eye.** **The monthly climate, the vapour resistance factors, the surface resistances, the critical humidity and the limit are the owner's records; the example's are invented and are not data from ISO 13788 or any other standard.** A timber or breathable build-up is outside the Glaser method (EN 15026 applies)."),
        new(CalcPackTemplates.ColdPipe, "cold-pipe", "Chilled water pipe insulation calc pack", "CHW-INS-001", "Client Co", "Plant room 2 chilled water", "Checked against SPEC-CHW-04 and DRG-CHW-410 rev B.",
            host => Task.FromResult(Plain(ChainInputs.ColdPipe(host))),
            "A chilled-water flow-and-return line, 6 °C flow and 12 °C return (60 m of 54 × 1.2 mm copper tube, inner diameter 51.6 mm, 12 m³/h at 1.6 m/s), runs through a plant room held at 28 °C and 60 % relative humidity. The pipe step takes its fluid properties at the 9 °C MEAN temperature and finds the flow velocity and pressure drop in the 51.6 mm bore; it offers the design temperature of the line, `T_line`, the 6 °C flow (the coldest in the line, which is what the anti-condensation sizing needs: linking the mean would understate the thickness by 19 %), and the pipe's **outside** diameter (54 mm: the optional OD column of its segments table, added to B1 in 1.4.0) as ports; the psychrometrics step gives the room air's dry-bulb (28 °C), dew point (19.51 °C) and humidity (60 %, which the insulation step prints but does not use because the dew point is entered). The insulation step takes all five by link and finds the thickness at which the outer surface of a 0.035 W/(m·K) cover stays 1 K above the dew point (7.52 mm), then the heat gain (6.54 W/m) and surface temperature (25.50 °C) at the chosen 25 mm. The heat-gain limit is not a criterion here, so the dew-point margin is the only one. **The conductivity, surface coefficient, margin and chosen thickness are the owner's; the example's are invented and are not maker's or standard's values, and no thickness table is used.** The tube roughness and fittings K are invented illustrations."),
        new(CalcPackTemplates.PipeworkSupports, "pipework-supports", "Pipe anchor post calc pack", "PIP-SUP-001", "Client Co", "Pipe rack 4 anchor A2", "Checked against SPEC-PIP-11 and DRG-PIP-610/611 rev B.",
            host => Task.FromResult(Plain(ChainInputs.PipeworkSupports(host))),
            "A 219.1 × 6.3 mm steel line anchored 60 m apart, 10 to 150 °C, absorbed by a 9.5 m offset leg: the line grows 100.8 mm, the leg needs 9.33 m at an INVENTED allowable range of 160 MPa (u 0.98) and puts 7.07 kN on its anchor (the 4.4 N along the leg from the run is negligible); the screening ratio, 292 mm²/m², is inside an INVENTED limit of 400. That anchor force is the first load row (a table cell) of a 2.5 m cantilever post (a 203 × 203 UC); with the 6 kN weight, the ULS combination takes 1.5 × 7.07 = 10.6 kN along the pipe at the top and 8.1 kN down, which is 26.5 kN·m, 8.1 kN and 10.6 kN at the base: the base plate's actions (the pipe step's moment is NOT carried to the post: it is the guided-cantilever root moment, not an applied one). The 350 × 300 × 25 base plate on C25/30 passes with 56 kN in each bolt. **The anchorage (concrete and anchor design) is done in Hilti PROFIS: the file attached to the base-plate step is a PLACEHOLDER, not a PROFIS report, to be replaced by the engineer's own report before the pack is used.** **The links depend on the frame's numbering: node 1 the column base (fixed), node 2 the anchor; member 1 the post; the first load row the thermal force.** The holding-down bolt data (M20 8.8 in a 25 mm plate) are typed because this chain has no bolt-check step; a test guards them against the connection step. The pipe step is a screening, not a stress analysis: its allowable range and screening limit are the owner's records and the example's are invented, not code values."),
        new(CalcPackTemplates.ElectronicsEnclosure, "electronics-enclosure", "Motor controller enclosure calc pack", "ELX-ENC-001", "Client Co", "Controller box 7", "Checked against SPEC-ELX-02 and DRG-ELX-210 rev B.",
            host => Task.FromResult(Plain(ChainInputs.ElectronicsEnclosure(host))),
            "An 8 A supply trace (2 oz external copper, 2 mm wide, 150 mm long, 30 K allowed rise) loses 1.507 W (23.55 mΩ at 90 °C). That loss is the first row of the board's load table (a table cell, W into kW), all released as heat; with the drivers, the power stage's loss, the display and the 24 V supply's own loss (26.5 W: the supply sits inside the enclosure) the board dissipates 73.0 W, and with 4 W of terminals the 500 × 600 × 200 mm wall-mounted enclosure carries 77.0 W: 21.9 K above the 35 °C room, 56.9 °C inside, below its 60 °C limit. **The supply's loss row is TYPED, not computed (E6 counts a supply's loss only if it is a row); a test guards it against the loads it serves, but it does not follow them automatically.** The internal temperature is the cover latches' temperature of use. The enclosure is **sealed, closed at 25 °C, with a pressure-compensation breather that cracks at 1 kPa (an INVENTED rating, not a maker's data)**: the air warming to 56.9 °C at constant volume would put 10.83 kPa across its walls (an upper bound), and the breather limits it to 1 kPa. That 1 kPa is the uniform pressure on the 600 × 500 × 4 mm simply supported aluminium front cover (the enclosure's largest face, so the governing panel; 3 mm would deflect 2.03 mm against the 2 mm limit), which deflects 0.855 mm against its span/250 limit of 2 mm (utilisation 0.43; stress 5.9 MPa against an indicative 120 MPa); on the 0.3 m² cover it is a force of 300 N, which six cantilever snap hooks (a typed count, an invented illustration; each strained 1.67 % against 3 %) hold with 431.6 N (utilisation 0.695). With the breather governing, revising the trace leaves the pressure and the force unchanged, so the cover and the latch load stay current; revising the breather rating (1.5 kPa puts 450 N on the latches and fails them) makes both stale. **The trace's board ambient (60 °C), the latches' modulus and permissible strain and the cover's modulus, density, Poisson's ratio and limit stress are TYPED for 60 °C, the enclosure's limit, not linked:** the trace's real ambient is the enclosure's own temperature, downstream of it (a link back would be a cycle), and the latch and plate steps take their material values as typed numbers for a temperature; the enclosure step's own check (natural dissipation holds T_max) guards them at run time, and a test shows it failing when the trace current takes the enclosure past 60 °C. The plastic's and the alloy's values are INVENTED or indicative illustrations, not a maker's data. **Idealisation:** the cover plate is simply supported all round, whereas the real cover is held at six latches and a gasket; its edge reactions peak at mid-edge, so the latches nearest mid-edge carry more than F_req/n. Equal sharing is an idealisation: place the latches at the mid-edges or apply a load-share factor to F_req."),
    ];

    /// <summary>The motor circuit's inputs with the synthetic E4 tables saved as (unlicensed, synthetic) records and pinned.</summary>
    internal static async Task<Dictionary<string, IReadOnlyList<CalcInputValue>>> MotorCircuitInputsAsync(LibraryTestHost host)
    {
        var (ampacity, ratings) = await SyntheticCableRecordsAsync(host);
        return ChainInputs.MotorCircuit(host, ampacity, ratings);
    }

    /// <summary>The hydraulic power unit's inputs with the synthetic E4 tables saved as (unlicensed, synthetic) records and pinned.</summary>
    internal static async Task<Dictionary<string, IReadOnlyList<CalcInputValue>>> HydraulicPowerUnitInputsAsync(LibraryTestHost host)
    {
        var (ampacity, ratings) = await SyntheticCableRecordsAsync(host);
        return ChainInputs.HydraulicPowerUnit(host, ampacity, ratings);
    }

    /// <summary>The mechanism review's inputs with the synthetic E4 tables saved as (unlicensed, synthetic) records and pinned.</summary>
    internal static async Task<Dictionary<string, IReadOnlyList<CalcInputValue>>> MechanismReviewInputsAsync(LibraryTestHost host)
    {
        var (ampacity, ratings) = await SyntheticCableRecordsAsync(host);
        return ChainInputs.MechanismReview(host, ampacity, ratings);
    }

    /// <summary>The cable step's two tables as SYNTHETIC records (test values, not from any regulation or manufacturer), and the provenance that pins each.</summary>
    private static async Task<(CalcProvenance Ampacity, CalcProvenance Ratings)> SyntheticCableRecordsAsync(LibraryTestHost host)
    {
        var (ampacity, r1) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "cable.ampacity", "SYNTHETIC cable capacities (test values, not a regulation table)", 0, "Synthetic test data, not from any standard", "n/a", "invented values for the worked example", "Tempest test data", "tempest.tests",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ampacity", Rows: ConductorSizingCalculator.SyntheticAmpacity().Rows)], DateTimeOffset.UnixEpoch));
        var (ratings, r2) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "device.ratings", "SYNTHETIC device ratings (test values, not a manufacturer table)", 0, "Synthetic test data, not from any catalogue", "n/a", "invented values for the worked example", "Tempest test data", "tempest.tests",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ratings", Rows: ConductorSizingCalculator.TypicalRatings().Rows)], DateTimeOffset.UnixEpoch));
        Assert.Null(r1);
        Assert.Null(r2);
        return (CalcInstanceService.RecordProvenance(ampacity!), CalcInstanceService.RecordProvenance(ratings!));
    }

    private static async Task<Guid?> ProjectAsync(LibraryTestHost host, string name)
    {
        var factory = new EngineeringObjectFactory<Project>(
            "Project", host.Context, (doc, rev) => new Project(doc, rev, host.Context, "EX-" + name.Split(' ')[0].ToUpperInvariant(), name, EngineeringObjectMetadata.Empty));
        return (await factory.CreateAsync("Worked example project.")).Id;
    }

    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All, .. FluidPowerCalculators.All],
        packTemplates: CalcPackTemplates.All);

    [Theory]
    [MemberData(nameof(Chains))]
    public async Task Each_Chain_Is_Issued_As_One_Pack_And_Written_As_A_Worked_Example(string slug)
    {
        var example = Examples.Single(e => e.Slug == slug);
        var host = Host();
        var inputs = await example.Inputs(host);
        var made = await ChainKit.InstantiateAsync(host, example.Template, example.Title, inputs, await ProjectAsync(host, example.Project));

        // The steel support's anchorage is designed elsewhere: file a clearly marked placeholder against the base plate.
        if (slug is "steel-support" or "pipework-supports")
        {
            var filed = await host.Service.AttachExternalEvidenceAsync(made.Instances["s6"].Id, "PROFIS-BP1-PLACEHOLDER.txt", "text/plain",
                Encoding.UTF8.GetBytes("PLACEHOLDER. The Hilti PROFIS anchorage report for base plate BP1 is not attached yet; the engineer replaces this file with the real report. This file is not a PROFIS report."), "profis_ref");
            Assert.True(filed.Succeeded, filed.Reason);
        }

        var sheet = await ChainKit.CheckAndIssueAsync(host, made, example.Template, example.Reference, example.Client, example.Note);

        if (Environment.GetEnvironmentVariable("TEMPEST_WRITE_WORKED_EXAMPLES") != "1")
            return;

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "VERSION"))) root = root.Parent;
        var folder = Path.Combine(root!.FullName, "docs", "worked-examples", "packs", slug);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "pack-issue-sheet.txt"), sheet);

        var issued = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

        foreach (var step in example.Template.Steps)
        {
            var member = (await host.Store.FindInstanceAsync(made.Instances[step.Key].Id))!;
            issued[step.Key] = member;
            File.WriteAllText(Path.Combine(folder, $"{step.Key}-{step.TemplateId.Replace('.', '-')}.sheet.txt"), CalcSheetTextRenderer.Render(await host.Service.SheetAsync(member)));
        }

        File.WriteAllText(Path.Combine(folder, "inputs.json"), InputsJson(example, issued));
        File.WriteAllText(Path.Combine(folder, "README.md"), Readme(example, issued));
    }

    [Fact]
    public void Yellow_Y_B_07_The_Hydraulic_Power_Unit_Narrative_Is_Short_Paragraphs_That_Keep_Every_Word()
    {
        // The README's narrative was one paragraph of 1074 words. It is split at sentence ends, one topic to a paragraph, and no word is added or lost.
        var remarks = Examples.Single(e => e.Slug == "hydraulic-power-unit").Remarks;
        var paragraphs = remarks.Split("\n\n");

        Assert.True(paragraphs.Length >= 12, $"{paragraphs.Length} paragraphs");
        Assert.All(paragraphs, p => Assert.InRange(Words(p), 1, 160));
        Assert.Equal(1074, Words(remarks));
        Assert.StartsWith("A 100/70 × 500 mm cylinder at 160 bar", paragraphs[0], StringComparison.Ordinal);
        Assert.StartsWith("**The cable-capacity and device-rating tables of the cable step are SYNTHETIC", paragraphs[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("  ", remarks, StringComparison.Ordinal);

        // Every paragraph starts a sentence and ends one.
        Assert.All(paragraphs, p => Assert.Matches(@"[.)*]$", p));
    }

    private static int Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string InputsJson(Example example, IReadOnlyDictionary<string, CalcInstance> issued)
    {
        var sb = new StringBuilder("{\n");
        var steps = example.Template.Steps;

        for (var i = 0; i < steps.Count; i++)
        {
            sb.Append("  \"").Append(steps[i].Key).Append("\": ");
            var json = CalcInputJson.Write(new CalcInputSet(issued[steps[i].Key].Inputs)).TrimEnd().Replace("\n", "\n  ", StringComparison.Ordinal);
            sb.Append(json).Append(i < steps.Count - 1 ? ",\n" : "\n");
        }

        return sb.Append("}\n").ToString();
    }

    /// <summary>The chain as the links draw it: each step that gives, with the steps it feeds, then any step that neither takes nor gives (an independent member), so a branched pack reads as branches rather than a line.</summary>
    internal static string ChainLine(CalcPackTemplate template)
    {
        var name = template.Steps.ToDictionary(s => s.Key, s => s.Name, StringComparer.Ordinal);
        var feeds = template.Steps
            .Where(s => template.Bindings.Any(b => b.FromStep == s.Key))
            .Select(s => $"{s.Name} (`{s.TemplateId}`) → {string.Join(", ", template.Bindings.Where(b => b.FromStep == s.Key).Select(b => name[b.ToStep]).Distinct())}")
            .ToList();
        var linked = template.Bindings.SelectMany(b => new[] { b.FromStep, b.ToStep }).ToHashSet(StringComparer.Ordinal);
        var alone = template.Steps.Where(s => !linked.Contains(s.Key)).Select(s => $"{s.Name} (`{s.TemplateId}`), linked to nothing").ToList();
        return string.Join("; ", [.. feeds, .. alone]) + ".";
    }

    /// <summary>Names any step no link reaches or leaves, which the staleness sentence must not claim to cover.</summary>
    internal static string IndependentNote(CalcPackTemplate template)
    {
        var linked = template.Bindings.SelectMany(b => new[] { b.FromStep, b.ToStep }).ToHashSet(StringComparer.Ordinal);
        var alone = template.Steps.Where(s => !linked.Contains(s.Key)).Select(s => s.Name).ToList();
        return alone.Count == 0 ? string.Empty : $" {string.Join(", ", alone)} {(alone.Count == 1 ? "is" : "are")} linked to nothing and so never goes stale with the others.";
    }

    private static string Number(double? n) => (n ?? 0).ToString("G6", CultureInfo.InvariantCulture);

    private static string Readme(Example example, IReadOnlyDictionary<string, CalcInstance> issued)
    {
        var template = example.Template;
        var name = template.Steps.ToDictionary(s => s.Key, s => s.Name, StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.Append("# Calc pack — ").Append(template.Title).Append(" (`").Append(template.Id).Append("`)\n\n");
        sb.Append("**Chain.** ").Append(ChainLine(template)).Append("\n\n");
        sb.Append("**Purpose.** ").Append(template.Description).Append("\n\n");
        sb.Append(example.Remarks).Append("\n\n");
        sb.Append("**Issue.** Project “").Append(example.Project).Append("”, pack “").Append(example.Title).Append("”, checked once as a bundle and issued under issue reference **`").Append(example.Reference).Append("`** to ").Append(example.Client)
            .Append(" in one transaction. After issue every step is read-only and carries that reference. ").Append(example.Note).Append("\n\n");

        sb.Append("## Steps\n\n| Step | Calculator | Revision | Checks |\n|---|---|---|---|\n");

        foreach (var step in template.Steps)
        {
            var i = issued[step.Key];
            var passed = i.Checks.Count(c => c.Passed);
            sb.Append("| ").Append(step.Name).Append(" | `").Append(step.TemplateId).Append("` | ").Append(i.Revision).Append(" | ")
                .Append(i.Checks.Count == 0 ? "none" : $"{passed} of {i.Checks.Count} PASS").Append(" |\n");
        }

        sb.Append("\n## Links\n\nEvery link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.").Append(IndependentNote(template)).Append("\n\n");
        sb.Append("| From | Output | To | Input | Value carried |\n|---|---|---|---|---|\n");

        foreach (var b in template.Bindings)
        {
            var target = issued[b.ToStep];
            string value;
            string input;

            if (b.Row is { } row && b.Column is { } column)
            {
                var cell = target.Inputs.Single(x => x.Symbol == b.Input).Rows![row].Get(column)!;
                value = $"{Number(cell.Number)} {cell.Unit}".Trim();
                input = $"`{b.Input}` row {row + 1} (`{column}`), a table-cell link";
            }
            else
            {
                var v = target.Inputs.Single(x => x.Symbol == b.Input);
                value = $"{Number(v.Number)} {v.Unit}".Trim();
                input = $"`{b.Input}`";
            }

            sb.Append("| ").Append(name[b.FromStep]).Append(" | `").Append(b.Output).Append("` | ").Append(name[b.ToStep]).Append(" | ").Append(input).Append(" | ").Append(value).Append(" |\n");
        }

        sb.Append("\n## Inputs and provenance\n\nEvery input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.\n");

        if (template.Id == CalcPackTemplates.MotorCircuit.Id || template.Id == CalcPackTemplates.HydraulicPowerUnit.Id || template.Id == CalcPackTemplates.MechanismReview.Id)
            sb.Append("\nThe cable-capacity and device-rating tables are the library's synthetic test tables (`ConductorSizingCalculator.SyntheticAmpacity()` and `TypicalRatings()`), saved as unlicensed records titled “SYNTHETIC … (test values, not a regulation table)” and pinned to the cable step. They are not published values.\n");

        if (template.Id == CalcPackTemplates.SteelSupport.Id || template.Id == CalcPackTemplates.PipeworkSupports.Id)
            sb.Append("\nThe base-plate step cites `PROFIS-BP1-PLACEHOLDER.txt`, filed against its Evidence; that file only says it is a placeholder, and the base plate's anchorage condition reads “external report attached: PROFIS-BP1-PLACEHOLDER.txt” so the issued sheet names what was attached. Its PASS in the base-plate step therefore records that a file is attached, not that the anchorage has been designed.\n");

        sb.Append("\n## Files\n\n- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.\n");

        foreach (var step in template.Steps)
            sb.Append("- `").Append(step.Key).Append('-').Append(step.TemplateId.Replace('.', '-')).Append(".sheet.txt` — ").Append(step.Name).Append(", the issued sheet.\n");

        sb.Append("- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.\n");
        sb.Append("\nGenerated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.\n");
        return sb.ToString();
    }
}
