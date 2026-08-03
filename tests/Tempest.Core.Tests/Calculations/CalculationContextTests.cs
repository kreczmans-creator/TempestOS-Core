using Tempest.Core.Calculations;

namespace Tempest.Core.Tests.Calculations;

public class CalculationContextTests
{
    [Fact]
    public void RecordIntermediate_AddsToIntermediateResults()
    {
        var context = new CalculationContext();

        context.RecordIntermediate("step-1", 42.0);

        var result = Assert.Single(context.IntermediateResults);
        Assert.Equal("step-1", result.Name);
        Assert.Equal(42.0, result.Value);
    }

    [Fact]
    public void RecordIntermediate_MultipleCalls_PreservesOrder()
    {
        var context = new CalculationContext();

        context.RecordIntermediate("first", 1.0);
        context.RecordIntermediate("second", 2.0);

        Assert.Equal(["first", "second"], context.IntermediateResults.Select(r => r.Name));
    }

    [Fact]
    public void RecordIntermediate_NullName_ThrowsArgumentNullException()
    {
        var context = new CalculationContext();

        Assert.Throws<ArgumentNullException>(() => context.RecordIntermediate(null!, 1.0));
    }

    [Fact]
    public void RecordIntermediate_NullValue_ThrowsArgumentNullException()
    {
        var context = new CalculationContext();

        Assert.Throws<ArgumentNullException>(() => context.RecordIntermediate("name", null!));
    }

    [Fact]
    public void RecordConstraintCheck_AddsToConstraintChecks()
    {
        var context = new CalculationContext();

        context.RecordConstraintCheck("must be positive", true, "value was 5");

        var check = Assert.Single(context.ConstraintChecks);
        Assert.Equal("must be positive", check.Description);
        Assert.True(check.IsSatisfied);
        Assert.Equal("value was 5", check.Detail);
    }

    [Fact]
    public void RecordConstraintCheck_WhitespaceDescription_ThrowsArgumentException()
    {
        var context = new CalculationContext();

        Assert.Throws<ArgumentException>(() => context.RecordConstraintCheck("   ", true));
    }

    [Fact]
    public void ReferenceMaterial_AddsToReferencedMaterialIds()
    {
        var context = new CalculationContext();

        context.ReferenceMaterial("material-1");
        context.ReferenceMaterial("material-2");

        Assert.Equal(["material-1", "material-2"], context.ReferencedMaterialIds);
    }

    [Fact]
    public void ReferenceMaterial_NullMaterialId_ThrowsArgumentNullException()
    {
        var context = new CalculationContext();

        Assert.Throws<ArgumentNullException>(() => context.ReferenceMaterial(null!));
    }

    [Fact]
    public void NewContext_HasNoRecordedData()
    {
        var context = new CalculationContext();

        Assert.Empty(context.IntermediateResults);
        Assert.Empty(context.ConstraintChecks);
        Assert.Empty(context.ReferencedMaterialIds);
    }

    // ----------------------------------------------------------------
    // Equality / immutability of the small record types
    // ----------------------------------------------------------------

    [Fact]
    public void CalculationAssumption_SameValues_AreEqual()
    {
        var a = new CalculationAssumption("desc", "just");
        var b = new CalculationAssumption("desc", "just");

        Assert.Equal(a, b);
    }

    [Fact]
    public void CalculationConstraintCheck_With_ProducesNewInstance_OriginalUnchanged()
    {
        var original = new CalculationConstraintCheck("desc", true, null);

        var modified = original with { IsSatisfied = false };

        Assert.True(original.IsSatisfied);
        Assert.False(modified.IsSatisfied);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void CalculationIntermediateResult_SameValues_AreEqual()
    {
        var a = new CalculationIntermediateResult("name", 5.0);
        var b = new CalculationIntermediateResult("name", 5.0);

        Assert.Equal(a, b);
    }
}
