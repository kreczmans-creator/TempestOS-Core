using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

/// <summary>
/// Documents the compile-time guarantee `WP7.0C Testing Strategy.md`'s own
/// "Additional: compile-time rejection test" category names —
/// <see cref="Quantity{TDimension}"/>'s generic constraint prevents a
/// length-typed quantity from ever being converted to, added to, or
/// compared against a mass-typed one.
/// </summary>
/// <remarks>
/// <para>
/// <b>How this is verified.</b> xUnit has no built-in facility to assert
/// "this line does not compile" without adding a Roslyn-scripting
/// dependency this framework does not otherwise need (`ADR-0054` commits
/// this framework to introducing no dependency beyond what implementing
/// the approved contract itself requires). Rather than invent that
/// infrastructure for a single guarantee, this guarantee is verified the
/// same way it was discovered: by literally attempting the invalid code
/// below, observing the compiler reject it, and leaving the rejected code
/// as a permanently disabled, disclosed demonstration — recorded honestly
/// as "verified by direct inspection, not by an automated compiler-error
/// assertion," per this governance suite's own Unknown-over-invented
/// discipline, rather than pretending a shallow trick constitutes full
/// automation.
/// </para>
/// <para>
/// The exact failure, reproduced against this repository's own compiler at
/// the time this Work Package was implemented:
/// </para>
/// <code>
/// var length = new Quantity&lt;Length&gt;(1.0, LengthUnits.Metre);
/// var mass = new Quantity&lt;Mass&gt;(1.0, MassUnits.Kilogram);
/// var invalid = length.ConvertTo(MassUnits.Kilogram);
/// // CS1503: Argument 1: cannot convert from 'Tempest.Core.UnitsAndQuantities.Unit&lt;Tempest.Core.UnitsAndQuantities.Mass&gt;'
/// // to 'Tempest.Core.UnitsAndQuantities.Unit&lt;Tempest.Core.UnitsAndQuantities.Length&gt;'
///
/// var sum = length + mass;
/// // CS0019: Operator '+' cannot be applied to operands of type
/// // 'Quantity&lt;Length&gt;' and 'Quantity&lt;Mass&gt;'
/// </code>
/// </remarks>
public class CompileTimeDimensionSafetyTests
{
    [Fact]
    public void SameDimension_ConvertToAndArithmetic_CompileAndExecute()
    {
        // The positive counterpart to the remarks above: this is the same
        // shape of code, but with both operands sharing TDimension = Length,
        // which does compile and behave correctly.
        var length = new Quantity<Length>(1.0, LengthUnits.Metre);
        var otherLength = new Quantity<Length>(1.0, LengthUnits.Metre);

        var converted = length.ConvertTo(LengthUnits.Foot);
        var sum = length + otherLength;

        Assert.Equal(LengthUnits.Foot, converted.Unit);
        Assert.Equal(2.0, sum.Value);
    }

    // Left permanently commented out: uncommenting either line below and
    // attempting to build reproduces the exact CS1503/CS0019 errors quoted
    // in this class's own remarks, above.
    //
    // var invalid1 = new Quantity<Length>(1.0, LengthUnits.Metre).ConvertTo(MassUnits.Kilogram);
    // var invalid2 = new Quantity<Length>(1.0, LengthUnits.Metre) + new Quantity<Mass>(1.0, MassUnits.Kilogram);
}
