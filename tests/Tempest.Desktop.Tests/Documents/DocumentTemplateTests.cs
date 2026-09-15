using SkiaSharp;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>
/// The shared two-phase layout template itself (`WP 20.10G`, PO finding
/// D4, closing `TD-182`) — exercised directly, independent of either
/// renderer, so a regression in the template's own pagination or footer
/// numbering fails here rather than only showing up as a mystifying
/// symptom in a renderer's own test.
/// </summary>
public sealed class DocumentTemplateTests
{
    [Fact]
    public void AddTable_SixtyRows_Paginates_AndEveryFootersCarriesTheRightPageNOfM()
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddHeaderBand(state, measure, "TEST DOCUMENT", "REF-0001  ·  2026-01-01  ·  Draft");
        DocumentTemplate.AddTable(
            state, measure,
            headers: ["Description", "Amount"],
            widths: [DocumentTemplate.ContentWidth * 0.7f, DocumentTemplate.ContentWidth * 0.3f],
            rows: [.. Enumerable.Range(1, 60).Select(i => new[]
            {
                $"Line {i} — a reasonably long description of the work covered by this particular line item, so it wraps across more than one row of the table.",
                $"£{i}.00",
            })],
            columnAligns: [SKTextAlign.Left, SKTextAlign.Right]);

        Assert.True(state.Pages.Count > 1, $"60 rows must overflow a single A4 page; got {state.Pages.Count} page(s).");

        DocumentTemplate.AppendFooters(state.Pages, OrganisationIdentity.TempestDefaults, "Test detail line");

        var total = state.Pages.Count;
        for (var i = 0; i < total; i++)
        {
            var expected = $"Page {i + 1} of {total}";
            Assert.Contains(state.Pages[i].Texts, t => t.Text == expected && t.Align == SKTextAlign.Right);
        }
    }

    [Fact]
    public void AppendFooters_OnePage_ReadsTheRightPage1Of1()
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };
        DocumentTemplate.AddWrappedLine(state, measure, "A short document.", DocumentTemplate.BodySize, bold: false);

        var identity = new OrganisationIdentity("Acme Ltd", "00000001", "www.acme.example", null, null, null, null);
        DocumentTemplate.AppendFooters(state.Pages, identity, "detail");

        Assert.Single(state.Pages);
        var page = state.Pages[0];
        Assert.Contains(page.Texts, t => t.Text == "Acme Ltd · Company No. 00000001" && t.Align == SKTextAlign.Left);
        Assert.Contains(page.Texts, t => t.Text == "www.acme.example" && t.Align == SKTextAlign.Right);
        Assert.Contains(page.Texts, t => t.Text == "detail" && t.Align == SKTextAlign.Left);
        Assert.Contains(page.Texts, t => t.Text == "Page 1 of 1" && t.Align == SKTextAlign.Right);
    }

    [Fact]
    public void AppendFooters_NoWebsiteRecorded_DrawsOnlyThePageNumberOnTheRight()
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };
        DocumentTemplate.AddWrappedLine(state, measure, "Body.", DocumentTemplate.BodySize, bold: false);

        var identity = new OrganisationIdentity("Acme Ltd", null, null, null, null, null, null);
        DocumentTemplate.AppendFooters(state.Pages, identity, "detail");

        var rightAligned = state.Pages[0].Texts.Where(t => t.Align == SKTextAlign.Right).ToList();
        var only = Assert.Single(rightAligned);
        Assert.Equal("Page 1 of 1", only.Text);
    }

    [Fact]
    public void EnsureSpace_ContentThatFitsWithinOnePage_NeverStartsASecondPage()
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddWrappedLine(state, measure, "One short line.", DocumentTemplate.BodySize, bold: false);

        Assert.Single(state.Pages);
        Assert.Same(state.Pages[0], state.Page);
    }

    [Fact]
    public void ReferenceColourTokens_CarryEveryTokenTheDesignSystemReferenceNamesVerbatim()
    {
        var byName = DocumentTemplate.ReferenceColourTokens.ToDictionary(t => t.Name, t => t.Hex);

        Assert.Equal("#070915", byName["navy-900"]);
        Assert.Equal("#f5f6fa", byName["paper-050"]);
        Assert.Equal("#16181d", byName["ink-900"]);
        Assert.Equal("#1c2d97", byName["indigo-600"]);
        Assert.Equal("#40a2ce", byName["cyan-500"]);
    }

    [Fact]
    public void ReferenceTypeNames_CarriesAllThreeFamilies() =>
        Assert.Equal(["Chakra Petch", "Inter", "Space Mono"], DocumentTemplate.ReferenceTypeNames);
}
