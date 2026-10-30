namespace Tempest.Workspace.Disciplines;

/// <summary>
/// The engineering disciplines built on the shared
/// <see cref="Tempest.Core.EngineeringDomain.DisciplineObject"/> shape
/// (`ADR-0163`) — the three `Capability Categories.md` named and nothing
/// had delivered: Structural, Electrical and Building Services / HVAC —
/// and, from `ADR-0170`, Fluid Power.
/// </summary>
/// <remarks>
/// Attributes are recorded as the engineer types them. No design-code
/// logic, safety factor or rating check is applied to any of them
/// (`Engineering Principles.md`, "What This Document Does Not Cover").
/// </remarks>
public static class DisciplineCatalogue
{
    /// <summary>Structural engineering: members, connections, foundations and load cases.</summary>
    public static DisciplineDefinition Structural { get; } = new(
        "Structural", "structural", "tempest.structural.management",
        [
            new("StructuralMember", "Member", "Members", new("00000000-0000-4010-8000-000000000001"),
                ["Member type", "Section", "Material", "Grade", "Length (mm)"]),
            new("StructuralConnection", "Connection", "Connections", new("00000000-0000-4010-8000-000000000002"),
                ["Connection type", "Bolts", "Weld", "Design force (kN)"]),
            new("StructuralFoundation", "Foundation", "Foundations", new("00000000-0000-4010-8000-000000000003"),
                ["Foundation type", "Dimensions", "Bearing capacity (kPa)"]),
            new("StructuralLoadCase", "Load case", "Load cases", new("00000000-0000-4010-8000-000000000004"),
                ["Load category", "Magnitude", "Partial factor"]),
        ]);

    /// <summary>Electrical engineering: distribution boards, circuits, loads and cables.</summary>
    public static DisciplineDefinition Electrical { get; } = new(
        "Electrical", "electrical", "tempest.electrical.management",
        [
            new("ElectricalDistributionBoard", "Distribution board", "Distribution boards", new("00000000-0000-4011-8000-000000000001"),
                ["Voltage (V)", "Phases", "Rated current (A)", "Fault level (kA)"]),
            new("ElectricalCircuit", "Circuit", "Circuits", new("00000000-0000-4011-8000-000000000002"),
                ["Protective device", "Rating (A)", "Cable", "Voltage drop (%)"]),
            new("ElectricalLoad", "Load", "Loads", new("00000000-0000-4011-8000-000000000003"),
                ["Power (kW)", "Power factor", "Phases", "Duty"]),
            new("ElectricalCable", "Cable", "Cables", new("00000000-0000-4011-8000-000000000004"),
                ["Cable type", "Size (mm²)", "Cores", "Length (m)", "Installation method"]),
        ]);

    /// <summary>Building services and HVAC: systems, zones, equipment, ductwork and pipework.</summary>
    public static DisciplineDefinition BuildingServices { get; } = new(
        "Building Services", "building-services", "tempest.building-services.management",
        [
            new("BuildingServicesSystem", "System", "Systems", new("00000000-0000-4012-8000-000000000001"),
                ["System type", "Medium", "Design capacity (kW)"]),
            new("BuildingServicesZone", "Zone", "Zones", new("00000000-0000-4012-8000-000000000002"),
                ["Floor area (m²)", "Design temperature (°C)", "Occupancy", "Air changes (/h)"]),
            new("BuildingServicesEquipment", "Equipment", "Equipment", new("00000000-0000-4012-8000-000000000003"),
                ["Equipment type", "Manufacturer", "Model", "Duty"]),
            new("BuildingServicesDistribution", "Ductwork / pipework run", "Ductwork and pipework", new("00000000-0000-4012-8000-000000000004"),
                ["Distribution type", "Size", "Material", "Flow rate"]),
        ]);

    /// <summary>Fluid power (`ADR-0170`): hydraulic circuits, actuators and power units. Pneumatics follows later as further Kinds.</summary>
    public static DisciplineDefinition FluidPower { get; } = new(
        "Fluid Power", "fluid-power", "tempest.fluid-power.management",
        [
            new("FluidPowerCircuit", "Circuit", "Circuits", new("00000000-0000-4013-8000-000000000001"),
                ["Circuit type", "Working pressure (bar)", "Fluid", "Flow rate (L/min)"]),
            new("FluidPowerActuator", "Actuator", "Actuators", new("00000000-0000-4013-8000-000000000002"),
                ["Actuator type", "Bore (mm)", "Rod diameter (mm)", "Stroke (mm)", "Working pressure (bar)"]),
            new("FluidPowerPowerUnit", "Power unit", "Power units", new("00000000-0000-4013-8000-000000000003"),
                ["Pump type", "Displacement (cm³/rev)", "Motor power (kW)", "Reservoir volume (L)", "Relief setting (bar)"]),
        ]);

    /// <summary>Every discipline in this catalogue, in the order the Engineering tree lists them.</summary>
    public static IReadOnlyList<DisciplineDefinition> All { get; } = [Structural, Electrical, BuildingServices, FluidPower];

    /// <summary>Every Kind any discipline in this catalogue offers.</summary>
    public static IReadOnlyList<string> AllKinds { get; } = [.. All.SelectMany(d => d.KindNames)];

    /// <summary>The discipline that offers <paramref name="kind"/>, or <see langword="null"/>.</summary>
    public static DisciplineDefinition? ForKind(string kind) => All.FirstOrDefault(d => d.Find(kind) is not null);
}
