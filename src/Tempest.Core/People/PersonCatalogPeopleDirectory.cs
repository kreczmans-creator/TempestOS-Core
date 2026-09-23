using Tempest.Core.ReferenceData;

namespace Tempest.Core.People;

/// <summary>
/// <see cref="IPeopleDirectory"/> over the People reference library
/// (`WP 20.10F`, <see cref="IPersonCatalog"/>): the people a session can be
/// switched to are the library's selectable (Released, active) records that
/// carry an identity id. `WP 21.3B` built its Switch person flow against
/// this seam with an in-memory stand-in because the library was not on its
/// base; the two met at merge and this adapter is what the host registers.
/// </summary>
public sealed class PersonCatalogPeopleDirectory : IPeopleDirectory
{
    private readonly IPersonCatalog _people;

    public PersonCatalogPeopleDirectory(IPersonCatalog people)
    {
        ArgumentNullException.ThrowIfNull(people);
        _people = people;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Person>> ListSwitchableAsync(CancellationToken cancellationToken = default)
    {
        var selectable = await _people.FindSelectableAsync(cancellationToken).ConfigureAwait(false);
        return selectable
            .Select(record => record.Definition)
            .Where(person => person.IsActive && !string.IsNullOrWhiteSpace(person.IdentityId))
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
