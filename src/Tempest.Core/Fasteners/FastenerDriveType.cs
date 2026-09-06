namespace Tempest.Core.Fasteners;

/// <summary>The feature an installer engages to drive a fastener.</summary>
public enum FastenerDriveType
{
    /// <summary>Not recorded. Never a claim the fastener has no drive — use <see cref="None"/> for that.</summary>
    Unspecified,

    /// <summary>The fastener has no driving feature of its own.</summary>
    None,

    /// <summary>Driven on external flats.</summary>
    ExternalHexagon,

    /// <summary>Driven in an internal hexagonal socket.</summary>
    InternalHexagon,

    /// <summary>Driven in an internal hexalobular socket.</summary>
    InternalHexalobular,

    /// <summary>Driven in a single slot.</summary>
    Slotted,

    /// <summary>Driven in a cross recess.</summary>
    CrossRecess,

    /// <summary>Driven in an internal or external square.</summary>
    Square,

    /// <summary>Driven in a twelve-point socket or on twelve-point flats.</summary>
    TwelvePoint,

    /// <summary>Turned by hand on a knurled, winged or slotted-thumb feature.</summary>
    HandDriven,

    /// <summary>A drive this taxonomy does not classify. <see cref="FastenerDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}
