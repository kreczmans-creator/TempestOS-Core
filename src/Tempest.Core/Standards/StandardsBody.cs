namespace Tempest.Core.Standards;

/// <summary>The organisation that publishes a standard.</summary>
/// <remarks>
/// <para>
/// <see cref="Code"/> is free text, not an enum. The set of standards
/// bodies in the world is open, changes without notice, and includes every
/// company that writes an internal standard; a closed list would be
/// obsolete on the day it was written and would force real publishers into
/// an "Other" bucket. <see cref="Kind"/> carries the part that
/// <em>is</em> classifiable.
/// </para>
/// <para>
/// <b>Naming a body is not endorsing it, and not asserting accreditation.</b>
/// This record says only which organisation a source attributed the
/// standard to.
/// </para>
/// </remarks>
/// <param name="Code">The body's own short designation as it appears in a standard number's prefix. Required.</param>
/// <param name="Name">The body's full name. <see langword="null"/> if the source gave none.</param>
/// <param name="Kind">What kind of organisation the body is.</param>
public sealed record StandardsBody(
    string Code,
    string? Name = null,
    StandardsBodyKind Kind = StandardsBodyKind.Unspecified)
{
    /// <summary>The body's own short designation.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A standards body must carry a code.", nameof(Code))
        : Code.Trim();

    /// <summary>The key body identity is matched and indexed on — case- and whitespace-insensitive, so one body is one body however a source capitalised it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string CodeKey => Code.ToUpperInvariant();
}
