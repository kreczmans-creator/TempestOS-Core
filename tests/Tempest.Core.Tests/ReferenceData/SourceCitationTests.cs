using Tempest.Core.Configuration;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Runtime;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// <see cref="SourceCitation"/> itself, and its round trip through a real
/// library over a real SQLite persistence root, including across a host
/// restart (`ADR-0149`).
/// </summary>
public class SourceCitationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var runTask = host.RunAsync();
        await RunningHostFixture.WaitUntilRunningAsync(host);
        await body(host);
        await host.StopAsync();
        await runTask;
    }

    private static IMaterialCatalog Materials(ITempestHost host) =>
        (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

    private static MaterialDefinition Widget(string name) => new() { Name = name, Family = MaterialFamily.Steel };

    // ------------------------------------------------------------
    // SourceCitation itself
    // ------------------------------------------------------------

    [Fact]
    public void ToString_EveryPartPresent_FormatsAsAnIssueSheetLine()
    {
        var citation = new SourceCitation("Aalco Metals Limited", "Stainless Steel Datasheet", "3rd", "12", "Table 4", "Row 7");

        Assert.Equal("Aalco Metals Limited, Stainless Steel Datasheet, ed. 3rd, p. 12, Table 4, row Row 7", citation.ToString());
    }

    [Fact]
    public void ToString_OnlyPublisherAndWork_OmitsEveryAbsentPart()
    {
        var citation = new SourceCitation("NIST", "CODATA Listing");

        Assert.Equal("NIST, CODATA Listing", citation.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankPublisherOrWork_Throws(string blank)
    {
        Assert.Throws<ArgumentException>(() => new SourceCitation(blank, "Work"));
        Assert.Throws<ArgumentException>(() => new SourceCitation("Publisher", blank));
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new SourceCitation("Aalco Metals Limited", "Datasheet", TableOrFigure: "Table 1");
        var b = new SourceCitation("Aalco Metals Limited", "Datasheet", TableOrFigure: "Table 1");

        Assert.Equal(a, b);
    }

    // ------------------------------------------------------------
    // Round trip over a real SQLite root, across a host restart
    // ------------------------------------------------------------

    [Fact]
    public async Task RegisterWithCitation_ReviseThroughTheCitationUnawareOverload_CarriesTheCitationForward()
    {
        // Acceptance test 1 (`WP 18.0B` brief): register with a citation,
        // revise through the pre-existing overload that says nothing about
        // one, and read both revisions back — over a real SQLite root and
        // after a host restart, not an in-memory fixture.
        using var temp = new TempDirectory();
        const string recordId = "src-cit-roundtrip";
        var citation = new SourceCitation("Aalco Metals Limited", "Stainless Steel Datasheet", TableOrFigure: "Table 1");

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = Materials(host);

            await materials.RegisterAsync(recordId, Widget("W-1"), ReferenceDataTestProvenance.Sourced(), citation);

            // The pre-existing four-argument overload — the one every
            // caller written before this Work Package still uses,
            // `ReferenceReviewService` included. It says nothing about a
            // citation, and per this Work Package's own design that means
            // "leave the record's own citation alone", not "wipe it" —
            // ReferenceReviewService revises a record's provenance on every
            // Verify/Release, and a citation set at registration must still
            // be there once the record it names is Released and cited from
            // Evidence.
            await materials.ReviseAsync(recordId, Widget("W-1 renamed"), ReferenceDataTestProvenance.Sourced(), "Renamed, citation untouched.");
        });

        // A second, independent host over the same root: the citation must
        // still be exactly what it was, on both revisions, after a restart.
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = Materials(host);

            var revision1 = await materials.GetRevisionAsync(recordId, 1);
            var revision2 = await materials.GetRevisionAsync(recordId, 2);
            var current = await materials.FindAsync(recordId);

            Assert.Equal(citation, revision1.Source);
            Assert.Equal(citation, revision2.Source);
            Assert.Equal(citation, current!.Source);
            Assert.Equal("W-1 renamed", current.Definition.Name);
        });
    }

    [Fact]
    public async Task ReviseThroughTheCitationAwareOverload_ReplacesTheCitation_IncludingWithNull()
    {
        using var temp = new TempDirectory();
        const string recordId = "src-cit-replace";
        var first = new SourceCitation("Aalco Metals Limited", "Datasheet One");
        var second = new SourceCitation("Siderticino SA", "Datasheet Two", "2019");

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = Materials(host);

            await materials.RegisterAsync(recordId, Widget("W-1"), ReferenceDataTestProvenance.Sourced(), first);
            var revised = await materials.ReviseAsync(
                recordId, Widget("W-1"), ReferenceDataTestProvenance.Sourced(), "Citation corrected.", second);
            Assert.Equal(second, revised.Source);

            // Explicit null through the citation-aware overload withdraws
            // the citation — a different act from the overload above that
            // does not mention one at all.
            var withdrawn = await materials.ReviseAsync(
                recordId, Widget("W-1"), ReferenceDataTestProvenance.Sourced(), "Citation withdrawn.", source: null);
            Assert.Null(withdrawn.Source);
        });
    }

    [Fact]
    public async Task RegisterWithNoCitation_LeavesSourceNull()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = Materials(host);

            var record = await materials.RegisterAsync("src-cit-none", Widget("W-1"), ReferenceDataTestProvenance.Sourced());

            Assert.Null(record.Source);
        });
    }

    [Fact]
    public async Task SetValidationStateAsync_AndSupersedeAsync_CarryTheCitationForward()
    {
        using var temp = new TempDirectory();
        var citation = new SourceCitation("Aalco Metals Limited", "Datasheet");

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = Materials(host);

            await materials.RegisterAsync("src-cit-primary", Widget("W-1"), ReferenceDataTestProvenance.Verified(), citation);
            await materials.RegisterAsync("src-cit-replacement", Widget("W-2"), ReferenceDataTestProvenance.Verified(), citation);

            var checkedRecord = await materials.SetValidationStateAsync("src-cit-primary", ReferenceValidationState.Checked, null);
            Assert.Equal(citation, checkedRecord.Source);

            var validated = await materials.SetValidationStateAsync("src-cit-primary", ReferenceValidationState.Validated, null);
            Assert.Equal(citation, validated.Source);

            var released = await materials.SetValidationStateAsync("src-cit-primary", ReferenceValidationState.Released, null);
            Assert.Equal(citation, released.Source);

            var superseded = await materials.SupersedeAsync("src-cit-primary", "src-cit-replacement", "Superseded.");
            Assert.Equal(citation, superseded.Source);
            Assert.Equal(ReferenceValidationState.Superseded, superseded.ValidationState);
        });
    }
}

/// <summary>Provenance builders shared by the citation and supersession tests, mirroring the fixture pattern <see cref="ReferenceDataFixtures"/> already uses.</summary>
internal static class ReferenceDataTestProvenance
{
    public static ReferenceProvenance Sourced() => new(
        SourceOrganisation: "TestFixture Publications",
        SourceDocument: "Fixture handbook (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 1",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    public static ReferenceProvenance Verified() => Sourced() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };
}
