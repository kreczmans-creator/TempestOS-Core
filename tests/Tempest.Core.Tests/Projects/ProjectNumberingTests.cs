using Tempest.Core.Projects;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// <see cref="ProjectNumbering"/> — the one definition of project-centric
/// numbering (Product Owner decision 2026-10-01 §3, `ADR-0156`): the
/// five-character customer code and six-character project reference shapes, the suggestion and uniqueness rules, the
/// <c>CUSTOMER-PROJECTREF</c> identifier and the per-project, per-type
/// <c>CUSTOMER-PROJECTREF-DOCTYPE-NNN</c> sequence.
/// </summary>
public sealed class ProjectNumberingTests
{
    [Theory]
    [InlineData("ACMEE", true)]
    [InlineData("ACME1", true)]
    [InlineData("12345", true)]
    [InlineData("acmee", false)]
    [InlineData("ACME", false)]
    [InlineData("ACMEEE", false)]
    [InlineData("AC-EE", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCustomerCode_AcceptsExactlyFiveUpperCaseLettersOrDigits(string? code, bool expected) =>
        Assert.Equal(expected, ProjectNumbering.IsValidCustomerCode(code));

    [Theory]
    [InlineData("BRIDG1", true)]
    [InlineData("BRIDGE", true)]
    [InlineData("000042", true)]
    [InlineData("BRIDG", false)]
    [InlineData("BRIDGES", false)]
    [InlineData("bridg1", false)]
    [InlineData("BR DG1", false)]
    [InlineData(null, false)]
    public void IsValidProjectReference_AcceptsExactlySixUpperCaseLettersOrDigits(string? code, bool expected) =>
        Assert.Equal(expected, ProjectNumbering.IsValidProjectReference(code));

    [Theory]
    [InlineData("Acme Engineering Ltd", "ACMEE")]
    [InlineData("Acme 1 Engineering", "ACME1")]
    [InlineData("Bridge", "BRIDG")]
    [InlineData("Ox", "OXXXX")]
    [InlineData("Ünïcode Wörks", "UNICO")]
    [InlineData("123 & Co", "123CO")]
    [InlineData("", "XXXXX")]
    [InlineData(null, "XXXXX")]
    public void SuggestCustomerCode_IsTheFirstFiveLettersAndDigitsOfTheName_PaddedWithX(string? name, string expected) =>
        Assert.Equal(expected, ProjectNumbering.SuggestCustomerCode(name, []));

    [Theory]
    [InlineData("Bridge", "BRIDGE")]
    [InlineData("Bridge 1 refurbishment", "BRIDGE")]
    [InlineData("A1 Bridge", "A1BRID")]
    [InlineData("Ox", "OXXXXX")]
    [InlineData(null, "XXXXXX")]
    public void SuggestProjectReference_IsTheFirstSixLettersAndDigitsOfTheName_PaddedWithX(string? name, string expected) =>
        Assert.Equal(expected, ProjectNumbering.SuggestProjectReference(name, []));

    [Fact]
    public void SuggestCode_StepsTheLastCharacter_WhenTheStemIsTaken_CaseInsensitively()
    {
        Assert.Equal("ACMEA", ProjectNumbering.SuggestCustomerCode("Acme Engineering", ["acmee"]));
        Assert.Equal("ACMEC", ProjectNumbering.SuggestCustomerCode("Acme Engineering", ["ACMEE", "ACMEA", "ACMEB"]));
        Assert.Equal("BRIDGA", ProjectNumbering.SuggestProjectReference("Bridge", ["BRIDGE"]));
    }

    [Fact]
    public void SuggestCode_StepsThroughDigits_ThenTheLastTwoCharacters_WhenEveryLastCharacterIsTaken()
    {
        var letters = Enumerable.Range(0, 26).Select(i => "ACME" + (char)('A' + i)).ToList();
        Assert.Equal("ACME0", ProjectNumbering.SuggestCustomerCode("Acme Engineering", letters));

        var all = letters.Concat(Enumerable.Range(0, 10).Select(i => "ACME" + i)).ToList();
        Assert.Equal("ACMAA", ProjectNumbering.SuggestCustomerCode("Acme Engineering", all));
    }

    [Fact]
    public void SuggestCode_IsDeterministic()
    {
        string[] taken = ["BRIDGE", "BRIDGA"];

        Assert.Equal(ProjectNumbering.SuggestProjectReference("Bridge", taken), ProjectNumbering.SuggestProjectReference("Bridge", taken));
    }

    [Fact]
    public void Validate_RefusesTheWrongShape_AndACodeAlreadyInUse_AcceptsAFreeOne()
    {
        Assert.Contains("exactly 5 characters", ProjectNumbering.ValidateCustomerCode("ACME", []), StringComparison.Ordinal);
        Assert.Contains("already in use", ProjectNumbering.ValidateCustomerCode("acme1", ["ACME1"]), StringComparison.Ordinal);
        Assert.Null(ProjectNumbering.ValidateCustomerCode(" acme1 ", ["BRIDG", null]));

        Assert.Contains("exactly 6 characters", ProjectNumbering.ValidateProjectReference("BRIDG", []), StringComparison.Ordinal);
        Assert.Contains("Project reference 'BRIDG1' is already in use", ProjectNumbering.ValidateProjectReference("bridg1", ["BRIDG1"]), StringComparison.Ordinal);
        Assert.Null(ProjectNumbering.ValidateProjectReference("bridg1", ["BRIDG", "TOWER1"]));
    }

    [Fact]
    public void ComposeProjectIdentifier_JoinsTheTwoCodes_UpperCased()
    {
        Assert.Equal("ACME1-BRIDG1", ProjectNumbering.ComposeProjectIdentifier("acme1", "Bridg1"));
        Assert.Throws<ArgumentException>(() => ProjectNumbering.ComposeProjectIdentifier("ACME", "BRIDG1"));
        Assert.Throws<ArgumentException>(() => ProjectNumbering.ComposeProjectIdentifier("ACMEE", "BRIDG"));
        Assert.Throws<ArgumentException>(() => ProjectNumbering.ComposeProjectIdentifier("ACMEE", "BR-DG1"));
    }

    [Theory]
    [InlineData("ACME1-BRIDG1", true, "ACME1", "BRIDG1")]
    [InlineData(" ACMEE-BRIDGE ", true, "ACMEE", "BRIDGE")]
    [InlineData("ACMEE-BRIDG", true, "ACMEE", "BRIDG")]
    [InlineData("ACME1-BRIDG", false, "", "")]
    [InlineData("ACMEE-BRID1", false, "", "")]
    [InlineData("ACMEE-BRIDGES", false, "", "")]
    [InlineData("P-0001", false, "", "")]
    [InlineData("QUO-PRJ-REF", false, "", "")]
    [InlineData("acme1-bridg1", false, "", "")]
    [InlineData(null, false, "", "")]
    public void TryParseProjectIdentifier_RecognisesTheProjectCentricShape_AndTheEarlierAllLetterOne(string? identifier, bool expected, string customer, string project)
    {
        Assert.Equal(expected, ProjectNumbering.TryParseProjectIdentifier(identifier, out var parsedCustomer, out var parsedProject));
        Assert.Equal(customer, parsedCustomer);
        Assert.Equal(project, parsedProject);
    }

    [Fact]
    public void ProjectReferencesIn_ReadsOnlyProjectCentricIdentifiers()
    {
        var references = ProjectNumbering.ProjectReferencesIn(["ACME1-BRIDG1", "P-0001", null, "OTHER-TOWER", "OTHR2-TOWER2"]);

        Assert.Equal(["BRIDG1", "TOWER", "TOWER2"], references);
    }

    [Fact]
    public void TryGetDocumentPrefix_ForAProjectCentricProject_FallsBackForAnyOther()
    {
        Assert.True(ProjectNumbering.TryGetDocumentPrefix("ACME1-BRIDG1", ProjectNumbering.Quotation, out var current));
        Assert.Equal("ACME1-BRIDG1-Q-", current);

        Assert.True(ProjectNumbering.TryGetDocumentPrefix("ACMEE-BRIDG", ProjectNumbering.Quotation, out var prefix));
        Assert.Equal("ACMEE-BRIDG-Q-", prefix);

        Assert.False(ProjectNumbering.TryGetDocumentPrefix("P-0001", ProjectNumbering.Quotation, out _));
        Assert.False(ProjectNumbering.TryGetDocumentPrefix(null, ProjectNumbering.PurchaseOrder, out _));
    }

    [Fact]
    public void NextNumber_StartsAt001_AndContinuesPastTheHighestUnderThatPrefixOnly()
    {
        Assert.Equal("ACMEE-BRIDG-Q-001", ProjectNumbering.NextNumber("ACMEE-BRIDG-Q-", []));

        string?[] existing =
        [
            "ACMEE-BRIDG-Q-001", "ACMEE-BRIDG-Q-004", "ACMEE-BRIDG-PO-009", "ACMEE-TOWER-Q-007", "Q-2026-012", null, "ACMEE-BRIDG-Q-CUSTOM",
        ];

        Assert.Equal("ACMEE-BRIDG-Q-005", ProjectNumbering.NextNumber("ACMEE-BRIDG-Q-", existing));
        Assert.Equal("ACMEE-BRIDG-PO-010", ProjectNumbering.NextNumber("ACMEE-BRIDG-PO-", existing));
        Assert.Equal("ACMEE-TOWER-Q-008", ProjectNumbering.NextNumber("ACMEE-TOWER-Q-", existing));
        Assert.Equal("OTHER-BRIDG-Q-001", ProjectNumbering.NextNumber("OTHER-BRIDG-Q-", existing));
    }

    [Fact]
    public void DocumentTypes_IsOneTable_WithUniqueUpperCaseCodes()
    {
        var codes = ProjectNumbering.DocumentTypes.Select(t => t.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, c => Assert.Matches("^[A-Z]+$", c));
        Assert.Contains(ProjectNumbering.Quotation, codes);
        Assert.Contains(ProjectNumbering.ChangeOrder, codes);
        Assert.Contains(ProjectNumbering.PurchaseOrder, codes);
        Assert.Contains(ProjectNumbering.InvoiceRequest, codes);
        Assert.Contains(ProjectNumbering.Document, codes);
        Assert.Contains(ProjectNumbering.Drawing, codes);
        Assert.Contains(ProjectNumbering.CadModel, codes);
        Assert.Contains(ProjectNumbering.Calculation, codes);
    }
}
