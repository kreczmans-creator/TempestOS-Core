namespace Tempest.Core.ReferenceData;

/// <summary>
/// A citation of an engineering standard from a reference-data record.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recording that a source cites a standard is not certifying
/// conformity.</b> This record says only what the source itself said:
/// nothing here, and nothing in Group A, asserts that an item complies
/// with a standard on TempestOS's own authority.
/// </para>
/// <para>
/// <see cref="StandardId"/> is the typed link into the Standards Library
/// (A2): where the cited standard is itself a registered record, the
/// citation points at it rather than duplicating its title, edition and
/// status into every citing library. Where it is not — a standard nobody
/// has registered yet — <see cref="Designation"/> alone still records what
/// the source said, so a citation is never lost for want of a registered
/// counterpart.
/// </para>
/// </remarks>
/// <param name="Designation">The standard's own designation as the source writes it (e.g. an ISO or national standard number). Required.</param>
/// <param name="StandardId">The <c>standardId</c> of the registered Standards Library record this citation resolves to. <see langword="null"/> if the standard is not registered.</param>
/// <param name="Body">The organisation that publishes the standard. <see langword="null"/> if the source did not name one.</param>
/// <param name="Edition">The edition or year of the standard the source cites. <see langword="null"/> if none was given.</param>
/// <param name="Applies">What the citation covers (e.g. boundary dimensions, tolerances, mechanical properties, designation system). Free text. <see langword="null"/> if the source did not say.</param>
public sealed record StandardReference(
    string Designation,
    string? StandardId = null,
    string? Body = null,
    string? Edition = null,
    string? Applies = null)
{
    /// <summary>The standard's own designation as the source writes it.</summary>
    public string Designation { get; } = string.IsNullOrWhiteSpace(Designation)
        ? throw new ArgumentException("A standard reference must carry a designation.", nameof(Designation))
        : Designation;

    /// <summary>Whether this citation resolves to a registered Standards Library record.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsResolved => !string.IsNullOrWhiteSpace(StandardId);
}
