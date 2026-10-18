using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;

namespace Tempest.Core.Tests.Invoicing.Xero.Contacts;

/// <summary>`v0.24.0` X2: how Xero contacts are ranked as candidates for a TempestOS organisation (design §5) — pure, no Xero.</summary>
public sealed class XeroContactMatcherTests
{
    private static Organisation Acme(string? vat = "GB123456789", string? code = "ACME1", OrganisationTradingType type = OrganisationTradingType.Customer)
    {
        var organisation = new Organisation { Reference = "ACME1", Name = "Acme Engineering Ltd", TaxRegistration = vat, CustomerCode = code };
        return organisation with { Roles = organisation.RolesFor(type) };
    }

    private static XeroWireContact Contact(
        string id, string name, string? tax = null, string? number = null, string status = "ACTIVE", bool customer = false, bool supplier = false) =>
        new(id, number, status, name, TaxNumber: tax, IsCustomer: customer, IsSupplier: supplier);

    [Fact]
    public void Rank_OrdersVatThenContactNumberThenExactThenSimilar_AndDropsTheRest()
    {
        var ranked = XeroContactMatcher.Rank(Acme(),
        [
            Contact("similar", "Acme Engineering Limited"),
            Contact("unrelated", "Borealis Fabrication"),
            Contact("exact", "ACME  engineering ltd"),
            Contact("number", "A.E. Holdings", number: "ACME1"),
            Contact("vat", "Totally Different Name", tax: "GB 123 4567 89"),
        ]);

        Assert.Equal(["vat", "number", "exact", "similar"], ranked.Select(c => c.ContactId));
        Assert.Equal(
            [XeroContactMatcher.MatchedOnVatNumber, XeroContactMatcher.MatchedOnContactNumber, XeroContactMatcher.MatchedOnExactName, XeroContactMatcher.MatchedOnSimilarName],
            ranked.Select(c => c.MatchedOn));
    }

    [Fact]
    public void Rank_OffersEachContactOnce_UnderItsStrongestReason()
    {
        var both = Contact("c-1", "Acme Engineering Ltd", tax: "GB123456789", number: "ACME1");

        var ranked = XeroContactMatcher.Rank(Acme(), [both, both with { }, Contact("C-1", "Acme Engineering Ltd")]);

        var only = Assert.Single(ranked);
        Assert.Equal(XeroContactMatcher.MatchedOnVatNumber, only.MatchedOn);
    }

    [Fact]
    public void Rank_NeverOffersAnArchivedContact()
    {
        var ranked = XeroContactMatcher.Rank(Acme(),
        [
            Contact("archived", "Acme Engineering Ltd", tax: "GB123456789", status: "ARCHIVED"),
            Contact("gdpr", "Acme Engineering Ltd", status: "GDPRREQUEST"),
        ]);

        Assert.Empty(ranked);
    }

    [Fact]
    public void Rank_WithinAReason_PrefersAContactXeroAlreadyMarksWithTheOrganisationsRole()
    {
        var contacts = new[]
        {
            Contact("a-customer", "Acme Engineering Ltd.", customer: true),
            Contact("a-supplier", "Acme Engineering, Ltd", supplier: true),
        };

        Assert.Equal("a-customer", XeroContactMatcher.Rank(Acme(type: OrganisationTradingType.Customer), contacts)[0].ContactId);
        Assert.Equal("a-supplier", XeroContactMatcher.Rank(Acme(type: OrganisationTradingType.Supplier), contacts)[0].ContactId);
    }

    [Fact]
    public void Candidate_CarriesXerosOwnFacts_Verbatim()
    {
        var candidate = XeroContactMatcher.Rank(Acme(),
            [new XeroWireContact("c-9", "ACME1", "active", "Acme Engineering Ltd", EmailAddress: " a@acme.example ", TaxNumber: "GB123456789", IsCustomer: true)])[0];

        Assert.Equal(new XeroContactCandidate("c-9", "Acme Engineering Ltd", "GB123456789", "ACME1", "a@acme.example", true, false, "ACTIVE", XeroContactMatcher.MatchedOnVatNumber), candidate);
    }

    [Theory]
    [InlineData("GB123456789", "GB 123 4567 89", true)]
    [InlineData("gb123456789", "123456789", true)]
    [InlineData("123-456-789", "GB123456789", true)]
    [InlineData("GB123456789", "GB123456788", false)]
    [InlineData("GB123456789", "IE123456789X", false)]
    [InlineData(null, "GB123456789", false)]
    [InlineData(" ", " ", false)]
    public void SameVatNumber_IgnoresFormattingAndAOneSidedCountryPrefix(string? left, string? right, bool same) =>
        Assert.Equal(same, XeroContactMatcher.SameVatNumber(left, right));

    [Theory]
    [InlineData("Acme Engineering Ltd", "Acme Engineering Limited", true)]
    [InlineData("Acme Engineering Ltd", "Acme Engineering (UK) Ltd", true)]
    [InlineData("Smith & Jones", "Smith and Jones Ltd", true)]
    [InlineData("Acme Engineering Ltd", "Acme", true)]
    [InlineData("Acme Engineering Ltd", "Borealis Engineering Ltd", false)]
    [InlineData("Acme Engineering Ltd", "Borealis Fabrication Ltd", false)]
    [InlineData("Acme Engineering Ltd", "Ltd", false)]
    public void SimilarName_ComparesCoreWords(string left, string right, bool similar) =>
        Assert.Equal(similar, XeroContactMatcher.SimilarName(left, right));

    [Theory]
    [InlineData("Acme Engineering Ltd.", "Acme Engineering")]
    [InlineData("Smith & Jones Limited", "Smith & Jones")]
    [InlineData("The Widget Company Ltd", "Widget")]
    [InlineData("Ltd", "Ltd")]
    [InlineData("  Borealis  ", "Borealis")]
    public void SearchTermFor_DropsLegalSuffixes(string name, string term) =>
        Assert.Equal(term, XeroContactMatcher.SearchTermFor(name));

    [Theory]
    [InlineData("Acme Engineering Ltd", "Engineering")]
    [InlineData("Borealis Ltd", null)]
    [InlineData("A B", null)]
    public void WiderSearchTermFor_IsTheLongestDistinctiveWord(string name, string? term) =>
        Assert.Equal(term, XeroContactMatcher.WiderSearchTermFor(name));

    [Fact]
    public void ContactNumberFor_IsTheCustomerCode_ElseTheReference_NeverLongerThanXeroAllows()
    {
        Assert.Equal("ACME1", XeroContactMatcher.ContactNumberFor(Acme()));
        Assert.Equal("ACME1", XeroContactMatcher.ContactNumberFor(Acme(code: null)));
        Assert.Equal("LEGACY-7", XeroContactMatcher.ContactNumberFor(new Organisation { Reference = " LEGACY-7 ", Name = "Old Ltd" }));
        Assert.Null(XeroContactMatcher.ContactNumberFor(new Organisation { Reference = new string('R', 51), Name = "Long Ltd" }));
    }
}
