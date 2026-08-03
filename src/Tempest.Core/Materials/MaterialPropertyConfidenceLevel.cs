namespace Tempest.Core.Materials;

/// <summary>How confidently a <see cref="MaterialProperty"/>'s own value is believed to be accurate.</summary>
public enum MaterialPropertyConfidenceLevel
{
    /// <summary>No confidence assessment has been recorded. The honest default — recorded, not guessed at, per this project's own Unknown-over-invented discipline.</summary>
    Unknown,

    /// <summary>Low confidence — e.g. a typical/nominal value from a general reference, not a certified test result.</summary>
    Low,

    /// <summary>Medium confidence — e.g. a manufacturer datasheet value.</summary>
    Medium,

    /// <summary>High confidence — e.g. a certified test result traceable to a specific specimen or batch.</summary>
    High
}
