namespace Tempest.Core.People;

/// <summary>
/// One person in the consultancy's own reference library of people (Product
/// Owner finding D8): who a requirement's <c>Owner</c>, and any future
/// person picker this platform grows, is chosen from — never typed free
/// text again once a person exists here.
/// </summary>
/// <remarks>
/// Deliberately minimal, mirroring <see cref="BusinessOperations.Crm.Contact"/>'s
/// own "business contact details only" discipline: a name, a role, an
/// e-mail, and — since this is the consultancy's <em>own</em> people rather
/// than a client's — an optional link to the signed-in identity they use to
/// sign in, and whether they are still active. No employment, HR or payroll
/// data of any kind: not an ERP, not a PLM (this release's own product
/// guard) — this record earns its place because a requirement's owner
/// needs picking from a real list, nothing more.
/// </remarks>
public sealed record Person
{
    /// <summary>Their name, as it should read wherever a person is shown. Required.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Their role or title at the consultancy. <see langword="null"/> where unrecorded.</summary>
    public string? Role { get; init; }

    /// <summary>Their e-mail address. <see langword="null"/> where unrecorded.</summary>
    public string? Email { get; init; }

    /// <summary>
    /// The stable id of the signed-in identity this person corresponds to
    /// (<see cref="Identity.IIdentity.Id"/>) — the same id
    /// <see cref="Identity.IPrincipalDirectory.Describe"/> resolves a
    /// display name for. <see langword="null"/> where this person has never
    /// been linked to a signed-in identity.
    /// </summary>
    public string? IdentityId { get; init; }

    /// <summary>Whether this person is still with the consultancy. A person who has left is kept, never deleted — the same "retained, never erased" discipline every reference record follows — but stops being offered by a picker.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>The case-insensitive key <see cref="DisplayName"/> is indexed under — a duplicate display name is refused, exactly as a duplicate designation/reference/code is in every sibling library.</summary>
    public string DisplayNameKey => DisplayNameKeyFor(DisplayName);

    /// <summary>The case-insensitive key <paramref name="displayName"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is null, empty, or whitespace.</exception>
    public static string DisplayNameKeyFor(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return displayName.Trim().ToUpperInvariant();
    }
}
