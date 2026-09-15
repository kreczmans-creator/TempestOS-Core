using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.People;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.ReferenceData;

namespace Tempest.Core.Tests.People;

/// <summary>
/// `WP 20.10F` (Product Owner finding D8): the People library's own
/// lifecycle, uniqueness and rehydration — the shared Group A machinery
/// itself is already proven generically by <c>ReferenceDataCatalogTests</c>;
/// what is genuinely this library's own is a duplicate display name being
/// refused (`Person` has no other outward identity to key on) and that a
/// registered person reads back correctly after the catalogue instance
/// itself is thrown away and rebuilt over the same backing stores — the
/// same "survives a restart" property every other durable record needs.
/// </summary>
public sealed class PersonCatalogTests
{
    private static PersonCatalog BuildCatalog() => BuildCatalog(out _, out _);

    private static PersonCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new PersonCatalog(documentStore, persistenceStore);
    }

    private static Person Person(string displayName = "A. Fictional Engineer", string? role = "Structural Engineer", bool isActive = true) => new()
    {
        DisplayName = displayName,
        Role = role,
        Email = "a.fictional@example.invalid",
        IdentityId = "identity-1",
        IsActive = isActive,
    };

    // ----------------------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ReturnsARecordCarryingTheDefinitionAndDraftState()
    {
        var catalog = BuildCatalog();

        var record = await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);

        Assert.Equal("person-1", record.Id);
        Assert.Equal("A. Fictional Engineer", record.Definition.DisplayName);
        Assert.Equal("Structural Engineer", record.Definition.Role);
        Assert.Equal(ReferenceValidationState.Draft, record.ValidationState);
    }

    [Fact]
    public async Task ALifecycleWalk_FromDraftToReleased_Succeeds()
    {
        var catalog = BuildCatalog();
        await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);

        // `PersonProvenance.Default` names a source but is not yet
        // verified — Released needs a reviewer and a verification date,
        // exactly as every other Group A library's own provenance rule
        // requires (`ReferenceValidationStates.DescribeProvenanceShortfall`).
        var verified = PersonProvenance.Default with
        {
            VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
            ReviewerPrincipalId = "reviewer-1",
            VerificationDate = new DateOnly(2026, 9, 15),
        };
        await catalog.ReviseAsync("person-1", Person(), verified, "Verified for the test.");

        await catalog.SetValidationStateAsync("person-1", ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync("person-1", ReferenceValidationState.Validated, "Rules pass.");
        var released = await catalog.SetValidationStateAsync("person-1", ReferenceValidationState.Released, "Released.");

        Assert.Equal(ReferenceValidationState.Released, released.ValidationState);
    }

    [Fact]
    public async Task FindSelectableAsync_OffersOnlyReleasedAndActivePeople_OrderedByDisplayName()
    {
        var catalog = BuildCatalog();

        // Draft — never offered.
        await catalog.RegisterAsync("person-draft", Person("Zed Draft"), PersonProvenance.Default);

        // Released but inactive (left the consultancy) — never offered.
        await RegisterReleasedAsync(catalog, "person-inactive", Person("Amy Inactive", isActive: false));

        // Released and active — offered, twice, to prove the ordering.
        await RegisterReleasedAsync(catalog, "person-b", Person("Bob Released"));
        await RegisterReleasedAsync(catalog, "person-a", Person("Amy Released"));

        var selectable = await catalog.FindSelectableAsync();

        Assert.Equal(["Amy Released", "Bob Released"], selectable.Select(r => r.Definition.DisplayName).ToArray());
    }

    // ----------------------------------------------------------------
    // Uniqueness — a duplicate display name is refused
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ADuplicateDisplayName_Throws()
    {
        var catalog = BuildCatalog();
        await catalog.RegisterAsync("person-1", Person("Jordan Casey"), PersonProvenance.Default);

        var exception = await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("person-2", Person("Jordan Casey"), PersonProvenance.Default));

        Assert.Equal("person-1", exception.ExistingRecordId);
    }

    [Fact]
    public async Task RegisterAsync_ADuplicateDisplayName_CaseAndWhitespaceInsensitive_Throws()
    {
        var catalog = BuildCatalog();
        await catalog.RegisterAsync("person-1", Person("Jordan Casey"), PersonProvenance.Default);

        await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("person-2", Person("  JORDAN CASEY  "), PersonProvenance.Default));
    }

    [Fact]
    public async Task FindByDisplayNameAsync_ResolvesTheRecord()
    {
        var catalog = BuildCatalog();
        await catalog.RegisterAsync("person-1", Person("Jordan Casey"), PersonProvenance.Default);

        var found = await catalog.FindByDisplayNameAsync("jordan casey");

        Assert.NotNull(found);
        Assert.Equal("person-1", found!.Id);
    }

    // ----------------------------------------------------------------
    // Rehydration round-trip — a fresh catalogue instance over the same
    // backing stores, the Core-level equivalent of "close, relaunch".
    // ----------------------------------------------------------------

    [Fact]
    public async Task ARegisteredPerson_ReadsBackIdentically_FromAFreshCatalogueOverTheSameStores()
    {
        var original = BuildCatalog(out var documentStore, out var persistenceStore);
        var person = Person("Rehydrated Person", role: "Mechanical Engineer");
        await original.RegisterAsync("person-rehydrate", person, PersonProvenance.Default);

        // A brand-new PersonCatalog instance — nothing in memory is shared
        // with `original` beyond the backing document/persistence stores
        // themselves, exactly as a fresh process would see after a restart.
        var rehydrated = new PersonCatalog(documentStore, persistenceStore);
        var record = await rehydrated.FindAsync("person-rehydrate");

        Assert.NotNull(record);
        Assert.Equal(person.DisplayName, record!.Definition.DisplayName);
        Assert.Equal(person.Role, record.Definition.Role);
        Assert.Equal(person.Email, record.Definition.Email);
        Assert.Equal(person.IdentityId, record.Definition.IdentityId);
        Assert.Equal(person.IsActive, record.Definition.IsActive);
        Assert.Equal(ReferenceValidationState.Draft, record.ValidationState);
        Assert.Equal(1, record.RevisionNumber);

        // The secondary (display-name) index rehydrates too — a fresh
        // instance can still resolve by the name, not only by id.
        var byName = await rehydrated.FindByDisplayNameAsync("Rehydrated Person");
        Assert.Equal("person-rehydrate", byName?.Id);
    }

    private static async Task RegisterReleasedAsync(PersonCatalog catalog, string recordId, Person person)
    {
        var verified = PersonProvenance.Default with
        {
            VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
            ReviewerPrincipalId = "reviewer-1",
            VerificationDate = new DateOnly(2026, 9, 15),
        };

        await catalog.RegisterAsync(recordId, person, verified);
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Validated, "Rules pass.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Released, "Released.");
    }
}
