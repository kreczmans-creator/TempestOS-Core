using System.Text.Json;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.People;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.BusinessOperations;

/// <summary>
/// The organisation half of Product Owner decision 2026-10-01: a customer
/// code that is unique across the library and suggested from the name
/// (§3, `ADR-0156`), a Customer/Supplier/Both type read from — and
/// written to — the existing <see cref="Organisation.Roles"/> (§2), the
/// new company contact fields, and the backward compatibility of every
/// record stored before those fields existed (§1).
/// </summary>
public sealed class OrganisationCustomerCodeTests
{
    [Fact]
    public async Task FindByCustomerCodeAsync_FindsTheHolder_CaseInsensitively_AndNothingForAFreeCode()
    {
        var catalog = OperationsFixtures.BuildOrganisationCatalog();
        await OperationsFixtures.RegisterAsync(catalog, "org-1", OperationsFixtures.Organisation("ORG-1") with { CustomerCode = "ACMEE" });
        await OperationsFixtures.RegisterAsync(catalog, "org-2", OperationsFixtures.Organisation("ORG-2"));

        Assert.Equal("org-1", (await catalog.FindByCustomerCodeAsync("acmee"))!.Id);
        Assert.Null(await catalog.FindByCustomerCodeAsync("BRIDG"));
    }

    [Fact]
    public async Task SuggestCustomerCodeAsync_DerivesFromTheName_SkippingCodesAlreadyHeld_ExceptByTheRecordBeingEdited()
    {
        var catalog = OperationsFixtures.BuildOrganisationCatalog();

        Assert.Equal("ACMEE", await catalog.SuggestCustomerCodeAsync("Acme Engineering"));

        await OperationsFixtures.RegisterAsync(catalog, "org-1", OperationsFixtures.Organisation("ORG-1") with { CustomerCode = "ACMEE" });

        Assert.Equal("ACMEA", await catalog.SuggestCustomerCodeAsync("Acme Engineering"));
        Assert.Equal("ACMEE", await catalog.SuggestCustomerCodeAsync("Acme Engineering", excludingRecordId: "org-1"));
    }

    [Theory]
    [InlineData(new PartyKind[0], OrganisationTradingType.Customer)]
    [InlineData(new[] { PartyKind.Customer }, OrganisationTradingType.Customer)]
    [InlineData(new[] { PartyKind.Prospect }, OrganisationTradingType.Customer)]
    [InlineData(new[] { PartyKind.Supplier }, OrganisationTradingType.Supplier)]
    [InlineData(new[] { PartyKind.Customer, PartyKind.Supplier }, OrganisationTradingType.Both)]
    public void TradingType_IsReadFromRoles(PartyKind[] roles, OrganisationTradingType expected) =>
        Assert.Equal(expected, (OperationsFixtures.Organisation() with { Roles = roles }).TradingType);

    [Theory]
    [InlineData(OrganisationTradingType.Customer)]
    [InlineData(OrganisationTradingType.Supplier)]
    [InlineData(OrganisationTradingType.Both)]
    public void RolesFor_WritesTheType_KeepingEveryOtherRole(OrganisationTradingType type)
    {
        var organisation = OperationsFixtures.Organisation() with { Roles = [PartyKind.Supplier, PartyKind.Partner] };

        var roles = organisation.RolesFor(type);

        Assert.Equal(type, (organisation with { Roles = roles }).TradingType);
        Assert.Contains(PartyKind.Partner, roles);
    }

    [Fact]
    public async Task NewFields_RoundTripThroughTheCatalogue()
    {
        var catalog = OperationsFixtures.BuildOrganisationCatalog();
        var organisation = OperationsFixtures.Organisation("ORG-1") with
        {
            CustomerCode = "ACMEE",
            TelephoneNumber = "+44 1234 567890",
            EmailAddress = "accounts@acme.example",
        };
        await OperationsFixtures.RegisterAsync(catalog, "org-1", organisation);

        var read = (await catalog.FindAsync("org-1"))!.Definition;

        Assert.Equal("ACMEE", read.CustomerCode);
        Assert.Equal("+44 1234 567890", read.TelephoneNumber);
        Assert.Equal("accounts@acme.example", read.EmailAddress);
    }

    [Fact]
    public void AnOrganisationStoredBeforeTheNewFields_StillReads_WithNoCodeAndAsACustomer()
    {
        const string stored = """{"Reference":"OLD-1","Name":"Old Client Ltd"}""";

        var organisation = JsonSerializer.Deserialize<Organisation>(stored, ReferenceSerialisation.Options)!;

        Assert.Equal("Old Client Ltd", organisation.Name);
        Assert.Null(organisation.CustomerCode);
        Assert.Null(organisation.TelephoneNumber);
        Assert.Null(organisation.EmailAddress);
        Assert.Equal(OrganisationTradingType.Customer, organisation.TradingType);
    }

    [Fact]
    public void APersonStoredBeforeThePhoneField_StillReads_WithNoPhone()
    {
        const string stored = """{"DisplayName":"Jane Engineer","Role":"Senior","Email":"jane@example.com"}""";

        var person = JsonSerializer.Deserialize<Person>(stored, ReferenceSerialisation.Options)!;

        Assert.Equal("Jane Engineer", person.DisplayName);
        Assert.Null(person.Phone);
    }
}
