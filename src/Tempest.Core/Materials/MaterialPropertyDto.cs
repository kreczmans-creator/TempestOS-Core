namespace Tempest.Core.Materials;

/// <summary>The plain, JSON-serializable shape a <see cref="MaterialProperty"/> is stored as.</summary>
internal sealed record MaterialPropertyDto(
    string DimensionKind,
    double Value,
    string UnitSymbol,
    double UnitToBaseFactor,
    MaterialPropertyProvenance Provenance);
