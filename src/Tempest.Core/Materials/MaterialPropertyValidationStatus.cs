namespace Tempest.Core.Materials;

/// <summary>Whether a <see cref="MaterialProperty"/>'s own value has been independently checked.</summary>
public enum MaterialPropertyValidationStatus
{
    /// <summary>No validation has been recorded. The honest default — not a claim the value is wrong, only that nothing has confirmed it.</summary>
    Unvalidated,

    /// <summary>The value has been checked against its own <see cref="MaterialPropertyProvenance.SourceReference"/>.</summary>
    Validated,

    /// <summary>The value was once validated but a newer source or revision has since superseded it.</summary>
    Superseded
}
