using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Manufacturing;

/// <summary>How well a source said a process and a material go together.</summary>
public enum ProcessMaterialSuitability
{
    /// <summary>The source recorded a relationship but did not qualify it. Never read as suitable.</summary>
    Unspecified,

    /// <summary>The source states the material is processed this way.</summary>
    Suitable,

    /// <summary>The source states the material is processed this way subject to stated conditions.</summary>
    ConditionallySuitable,

    /// <summary>The source states the material is not processed this way. Recorded, because knowing a combination does not work is as useful as knowing one does.</summary>
    NotSuitable
}

/// <summary>
/// A material a source associated with a process, and what it said about
/// the pairing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recorded, never inferred.</b> <see cref="Origin"/> says who made the
/// claim. TempestOS never concludes that a material can be processed a
/// given way from the properties of either: that is a manufacturing
/// judgement resting on equipment, tooling, geometry and experience the
/// library does not hold.
/// </para>
/// <para>
/// <see cref="MaterialFamily"/> comes from A1's own taxonomy rather than a
/// second, parallel list of materials in A7 — one concept, one owner. A
/// source that named a specific grade rather than a family records it in
/// <see cref="MaterialId"/> where the grade is registered, and in
/// <see cref="MaterialDesignation"/> verbatim where it is not.
/// </para>
/// </remarks>
/// <param name="Family">The material family the claim is about. <see cref="Materials.MaterialFamily.Unspecified"/> where the source named only a specific grade.</param>
/// <param name="Suitability">What the source said about the pairing.</param>
/// <param name="MaterialId">The registered A1 material the claim is about, where the source named a specific grade and that grade is registered. <see langword="null"/> otherwise.</param>
/// <param name="MaterialDesignation">The grade as the source designates it, verbatim. <see langword="null"/> where the source named only a family.</param>
/// <param name="Origin">Who made the claim.</param>
/// <param name="Conditions">The conditions a conditional suitability holds under, as the source states them. <see langword="null"/> if none were given.</param>
/// <param name="Notes">Anything else the source said about the pairing, verbatim. <see langword="null"/> if none.</param>
public sealed record ProcessMaterialCompatibility(
    MaterialFamily Family = MaterialFamily.Unspecified,
    ProcessMaterialSuitability Suitability = ProcessMaterialSuitability.Unspecified,
    string? MaterialId = null,
    string? MaterialDesignation = null,
    ReferenceValueOrigin Origin = ReferenceValueOrigin.Unknown,
    string? Conditions = null,
    string? Notes = null)
{
    /// <summary>Whether the entry names anything at all — a family, a registered material, or a designation.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NamesAMaterial =>
        Family != MaterialFamily.Unspecified
        || !string.IsNullOrWhiteSpace(MaterialId)
        || !string.IsNullOrWhiteSpace(MaterialDesignation);

    /// <summary>Whether TempestOS itself, rather than a source, made the claim.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDerived => Origin == ReferenceValueOrigin.DerivedByTempestOS;

    /// <summary>The key two entries are considered to be about the same material by.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SubjectKey =>
        MaterialId is { } id
            ? $"#{id}"
            : MaterialDesignation is { } designation
                ? $"@{designation.Trim().ToUpperInvariant()}"
                : $"~{Family}";
}
