namespace Tempest.Core.Fasteners;

/// <summary>The head form of a fastener.</summary>
/// <remarks>
/// Orthogonal to <see cref="FastenerDriveType"/>: a countersunk head takes
/// a cross recess, a hexalobular socket or a slot, and conflating the two
/// would lose which is which.
/// </remarks>
public enum FastenerHeadType
{
    /// <summary>Not recorded. Never a claim the fastener is headless — use <see cref="None"/> for that.</summary>
    Unspecified,

    /// <summary>The family has no head, which for a stud or set screw is a fact rather than a gap.</summary>
    None,

    /// <summary>A plain hexagonal head.</summary>
    Hexagon,

    /// <summary>A hexagonal head with an integral bearing flange.</summary>
    HexagonFlange,

    /// <summary>A cylindrical head with an internal socket.</summary>
    SocketCap,

    /// <summary>A domed head with an internal socket.</summary>
    ButtonHead,

    /// <summary>A conical head that sits flush in a countersunk hole.</summary>
    Countersunk,

    /// <summary>A countersunk head with a raised dome above the cone.</summary>
    RaisedCountersunk,

    /// <summary>A cylindrical head with a flat top.</summary>
    Cheese,

    /// <summary>A shallow domed head with a flat bearing face.</summary>
    Pan,

    /// <summary>A hemispherical head.</summary>
    Round,

    /// <summary>A square head.</summary>
    Square,

    /// <summary>A twelve-point (bi-hexagonal) head.</summary>
    TwelvePoint,

    /// <summary>A head form this taxonomy does not classify. <see cref="FastenerDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}
