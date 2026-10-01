using Tempest.Core.Projects;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// <see cref="ProjectNumbering"/> — the one definition of project-centric
/// numbering (Product Owner decision 2026-10-01 §3, `ADR-0156`): the
/// five-letter code shape, the suggestion and uniqueness rules, the
/// <c>CUSTOMER-PROJECTREF</c> identifier and the per-project, per-type
/// <c>CUSTOMER-PROJECTREF-DOCTYPE-NNN</c> sequence.
/// </summary>
public sealed class ProjectNumberingTests
{
    [Theory]
    [InlineData("ACMEE", true)]
    [InlineData("acmee", false)]
    [InlineData("ACME", false)]
    [InlineData("ACMEEE", false)]
    [InlineData("ACM3E", false)]
    [InlineData("AC-EE", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCode_AcceptsExactlyFiveUpperCaseLetters(string? code, bool expected) =>
        Assert.Equal(expected, ProjectNumbering.IsValidCode(code));

    [Theory]
    [InlineData("Acme Engineering Ltd", "ACMEE")]
    [InlineData("Bridge", "BRIDG")]
    [InlineData("Ox", "OXXXX")]
    [InlineData("Ünïcode Wörks", "UNICO")]
    [InlineData("123 & Co", "COXXX")]
    [InlineData("", "XXXXX")]
    [InlineData(null, "XXXXX")]
    public void SuggestCode_IsTheFirstFiveLettersOfTheName_PaddedWithX(string? name, string expected) =>
        Assert.Equal(expected, ProjectNumbering.SuggestCode(name, []));

    [Fact]
    public void SuggestCode_StepsTheLastLetter_WhenTheStemIsTaken_CaseInsensitively()
    {
        Assert.Equal("ACMEA", ProjectNumbering.SuggestCode("Acme Engineering", ["acmee"]));
        Assert.Equal("ACMEC", ProjectNumbering.SuggestCode("Acme Engineering", ["ACMEE", "ACMEA", "ACMEB"]));
    }

    [Fact]
    public void SuggestCode_StepsTheLastTwoLetters_WhenEveryLastLetterIsTaken()
    {
        var taken = Enumerable.Range(0, 26).Select(i => "ACME" + (char)('A' + i)).ToList();

        Assert.Equal("ACMAA", ProjectNumbering.SuggestCode("Acme Engineering", taken));
    }

    [Fact]
    public void SuggestCode_IsDeterministic()
    {
        string[] taken = ["BRIDG", "BRIDA"];

        Assert.Equal(ProjectNumbering.SuggestCode("Bridge", taken), ProjectNumbering.SuggestCode("Bridge", taken));
    }

    [Fact]
    public void Validate_RefusesTheWrongShape_AndACodeAlreadyInUse_AcceptsAFreeOne()
    {
        Assert.Contains("exactly 5 letters", ProjectNumbering.Validate("ACME", [], "Customer code"), StringComparison.Ordinal);
        Assert.Contains("already in use", ProjectNumbering.Validate("acmee", ["ACMEE"], "Customer code"), StringComparison.Ordinal);
        Assert.Null(ProjectNumbering.Validate(" acmee ", ["BRIDG", null], "Customer code"));
    }

    [Fact]
    public void ComposeProjectIdentifier_JoinsTheTwoCodes_UpperCased()
    {
        Assert.Equal("ACMEE-BRIDG", ProjectNumbering.ComposeProjectIdentifier("acmee", "Bridg"));
        Assert.Throws<ArgumentException>(() => ProjectNumbering.ComposeProjectIdentifier("ACME", "BRIDG"));
        Assert.Throws<ArgumentException>(() => ProjectNumbering.ComposeProjectIdentifier("ACMEE", "BR1DG"));
    }

    [Theory]
    [InlineData("ACMEE-BRIDG", true, "ACMEE", "BRIDG")]
    [InlineData(" ACMEE-BRIDG ", true, "ACMEE", "BRIDG")]
    [InlineData("P-0001", false, "", "")]
    [InlineData("QUO-PRJ-REF", false, "", "")]
    [InlineData("acmee-bridg", false, "", "")]
    [InlineData(null, false, "", "")]
    public void TryParseProjectIdentifier_RecognisesOnlyTheProjectCentricShape(string? identifier, bool expected, string customer, string project)
    {
        Assert.Equal(expected, ProjectNumbering.TryParseProjectIdentifier(identifier, out var parsedCustomer, out var parsedProject));
        Assert.Equal(customer, parsedCustomer);
        Assert.Equal(project, parsedProject);
    }

    [Fact]
    public void ProjectReferencesIn_ReadsOnlyProjectCentricIdentifiers()
    {
        var references = ProjectNumbering.ProjectReferencesIn(["ACMEE-BRIDG", "P-0001", null, "OTHER-TOWER"]);

        Assert.Equal(["BRIDG", "TOWER"], references);
    }

    [Fact]
    public void TryGetDocumentPrefix_ForAProjectCentricProject_FallsBackForAnyOther()
    {
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
