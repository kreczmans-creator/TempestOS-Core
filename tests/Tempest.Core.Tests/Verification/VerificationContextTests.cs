using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Verification;

public class VerificationContextTests
{
    [Fact]
    public void RecordCriterion_AddsToCriteria()
    {
        var context = new VerificationContext();

        context.RecordCriterion("must hold", true, "detail");

        var criterion = Assert.Single(context.Criteria);
        Assert.Equal("must hold", criterion.Description);
        Assert.True(criterion.IsSatisfied);
        Assert.Equal("detail", criterion.Detail);
    }

    [Fact]
    public void RecordCriterion_WhitespaceDescription_ThrowsArgumentException()
    {
        var context = new VerificationContext();

        Assert.Throws<ArgumentException>(() => context.RecordCriterion("   ", true));
    }

    [Fact]
    public void RecordEvidence_AddsToEvidence()
    {
        var context = new VerificationContext();

        context.RecordEvidence("inspection note", "ref-1");

        var evidence = Assert.Single(context.Evidence);
        Assert.Equal("inspection note", evidence.Description);
        Assert.Equal("ref-1", evidence.Reference);
    }

    [Fact]
    public void RecordEvidence_NullDescription_ThrowsArgumentNullException()
    {
        var context = new VerificationContext();

        Assert.Throws<ArgumentNullException>(() => context.RecordEvidence(null!));
    }

    [Fact]
    public void LinkDocument_AddsToLinkedDocumentIds()
    {
        var context = new VerificationContext();
        var id = Guid.NewGuid();

        context.LinkDocument(id);

        Assert.Equal([id], context.LinkedDocumentIds);
    }

    [Fact]
    public void LinkCalculationRecord_AddsToLinkedCalculationRecordIds()
    {
        var context = new VerificationContext();
        var id = Guid.NewGuid();

        context.LinkCalculationRecord(id);

        Assert.Equal([id], context.LinkedCalculationRecordIds);
    }

    [Fact]
    public void ReferenceMaterial_AddsToReferencedMaterialIds()
    {
        var context = new VerificationContext();

        context.ReferenceMaterial("material-1");

        Assert.Equal(["material-1"], context.ReferencedMaterialIds);
    }

    [Fact]
    public void ReferenceMaterial_NullMaterialId_ThrowsArgumentNullException()
    {
        var context = new VerificationContext();

        Assert.Throws<ArgumentNullException>(() => context.ReferenceMaterial(null!));
    }

    [Fact]
    public void NewContext_HasNoRecordedData()
    {
        var context = new VerificationContext();

        Assert.Empty(context.Criteria);
        Assert.Empty(context.Evidence);
        Assert.Empty(context.LinkedDocumentIds);
        Assert.Empty(context.LinkedCalculationRecordIds);
        Assert.Empty(context.ReferencedMaterialIds);
    }

    // ----------------------------------------------------------------
    // Equality / immutability of the small record types
    // ----------------------------------------------------------------

    [Fact]
    public void VerificationCriterion_SameValues_AreEqual()
    {
        var a = new VerificationCriterion("desc", true, "detail");
        var b = new VerificationCriterion("desc", true, "detail");

        Assert.Equal(a, b);
    }

    [Fact]
    public void VerificationCriterion_With_ProducesNewInstance_OriginalUnchanged()
    {
        var original = new VerificationCriterion("desc", true, null);

        var modified = original with { IsSatisfied = false };

        Assert.True(original.IsSatisfied);
        Assert.False(modified.IsSatisfied);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void VerificationEvidenceEntry_SameValues_AreEqual()
    {
        var a = new VerificationEvidenceEntry("desc", "ref");
        var b = new VerificationEvidenceEntry("desc", "ref");

        Assert.Equal(a, b);
    }
}
