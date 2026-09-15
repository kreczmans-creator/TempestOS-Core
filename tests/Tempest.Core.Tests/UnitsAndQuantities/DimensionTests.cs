namespace Tempest.Core.Tests.UnitsAndQuantities;

using Tempest.Core.UnitsAndQuantities;

// Direct tests of the Dimension struct itself (WP 21.5D: no test file
// previously exercised it directly at all — every existing test reaches it
// only indirectly, through Quantity<TDimension>/Quantity arithmetic).
public class DimensionTests
{
    // ----------------------------------------------------------------
    // Construction / properties
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_Default_EveryExponentIsZero()
    {
        var dimension = new Dimension();

        Assert.Equal(0, dimension.Length);
        Assert.Equal(0, dimension.Mass);
        Assert.Equal(0, dimension.Time);
        Assert.Equal(0, dimension.Temperature);
        Assert.Equal(0, dimension.ElectricCurrent);
        Assert.Equal(0, dimension.AmountOfSubstance);
        Assert.Equal(0, dimension.LuminousIntensity);
    }

    [Fact]
    public void Constructor_NamedExponents_SetTheCorrespondingProperty()
    {
        var dimension = new Dimension(
            length: 1, mass: 2, time: 3, temperature: 4, electricCurrent: 5, amountOfSubstance: 6, luminousIntensity: 7);

        Assert.Equal(1, dimension.Length);
        Assert.Equal(2, dimension.Mass);
        Assert.Equal(3, dimension.Time);
        Assert.Equal(4, dimension.Temperature);
        Assert.Equal(5, dimension.ElectricCurrent);
        Assert.Equal(6, dimension.AmountOfSubstance);
        Assert.Equal(7, dimension.LuminousIntensity);
    }

    // ----------------------------------------------------------------
    // IsDimensionless — every one of the seven exponents matters
    // individually, not only as a group.
    // ----------------------------------------------------------------

    [Fact]
    public void IsDimensionless_EveryExponentZero_IsTrue() => Assert.True(new Dimension().IsDimensionless);

    [Theory]
    [InlineData(1, 0, 0, 0, 0, 0, 0)]
    [InlineData(0, 1, 0, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 1, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 1, 0, 0)]
    [InlineData(0, 0, 0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, 1)]
    public void IsDimensionless_ExactlyOneExponentNonZero_IsFalse(
        sbyte length, sbyte mass, sbyte time, sbyte temperature, sbyte electricCurrent, sbyte amountOfSubstance, sbyte luminousIntensity)
    {
        var dimension = new Dimension(length, mass, time, temperature, electricCurrent, amountOfSubstance, luminousIntensity);

        Assert.False(dimension.IsDimensionless);
    }

    // ----------------------------------------------------------------
    // Multiplication — exponents add
    // ----------------------------------------------------------------

    [Fact]
    public void Multiplication_AddsCorrespondingExponents()
    {
        var force = Dimensions.Mass * Dimensions.Acceleration; // M * (L T^-2)
        var length = Dimensions.Length;

        var energy = force * length; // M L^2 T^-2

        Assert.Equal(2, energy.Length);
        Assert.Equal(1, energy.Mass);
        Assert.Equal(-2, energy.Time);
        Assert.Equal(0, energy.Temperature);
    }

    [Fact]
    public void Multiplication_AddsEveryOneOfTheSevenExponentsIndependently()
    {
        // Every exponent non-zero and asymmetric between operands, so a
        // mutation swapping + for - on any single component (Length,
        // Time and LuminousIntensity aside — the other four are otherwise
        // always 0+0, where + and - coincide) is observable.
        var left = new Dimension(length: 1, mass: 2, time: 3, temperature: 4, electricCurrent: 5, amountOfSubstance: 6, luminousIntensity: 7);
        var right = new Dimension(length: 10, mass: 20, time: 30, temperature: 40, electricCurrent: 50, amountOfSubstance: 60, luminousIntensity: 70);

        var product = left * right;

        Assert.Equal(11, product.Length);
        Assert.Equal(22, product.Mass);
        Assert.Equal(33, product.Time);
        Assert.Equal(44, product.Temperature);
        Assert.Equal(55, product.ElectricCurrent);
        Assert.Equal(66, product.AmountOfSubstance);
        Assert.Equal(77, product.LuminousIntensity);
    }

    [Fact]
    public void Multiplication_ByDimensionless_IsUnchanged()
    {
        var length = Dimensions.Length;

        Assert.Equal(length, length * Dimensions.Dimensionless);
    }

    [Fact]
    public void Multiplication_OverflowingAnExponent_ThrowsOverflowException()
    {
        var nearMax = new Dimension(length: 100);

        Assert.Throws<OverflowException>(() => nearMax * nearMax);
    }

    // ----------------------------------------------------------------
    // Division — exponents subtract
    // ----------------------------------------------------------------

    [Fact]
    public void Division_SubtractsCorrespondingExponents()
    {
        var velocity = Dimensions.Length / Dimensions.Duration; // L T^-1

        Assert.Equal(1, velocity.Length);
        Assert.Equal(-1, velocity.Time);
        Assert.Equal(0, velocity.Mass);
    }

    [Fact]
    public void Division_SubtractsEveryOneOfTheSevenExponentsIndependently()
    {
        // Same reasoning as the multiplication equivalent above: every
        // exponent non-zero and asymmetric, so a mutation swapping - for +
        // on any single component is observable.
        var left = new Dimension(length: 10, mass: 20, time: 30, temperature: 40, electricCurrent: 50, amountOfSubstance: 60, luminousIntensity: 70);
        var right = new Dimension(length: 1, mass: 2, time: 3, temperature: 4, electricCurrent: 5, amountOfSubstance: 6, luminousIntensity: 7);

        var quotient = left / right;

        Assert.Equal(9, quotient.Length);
        Assert.Equal(18, quotient.Mass);
        Assert.Equal(27, quotient.Time);
        Assert.Equal(36, quotient.Temperature);
        Assert.Equal(45, quotient.ElectricCurrent);
        Assert.Equal(54, quotient.AmountOfSubstance);
        Assert.Equal(63, quotient.LuminousIntensity);
    }

    [Fact]
    public void Division_ByItself_IsDimensionless()
    {
        var length = Dimensions.Length;

        Assert.True((length / length).IsDimensionless);
    }

    [Fact]
    public void Division_OverflowingAnExponent_ThrowsOverflowException()
    {
        var nearMin = new Dimension(length: -100);
        var positive = new Dimension(length: 100);

        Assert.Throws<OverflowException>(() => nearMin / positive);
    }

    // ----------------------------------------------------------------
    // Pow — every exponent scaled
    // ----------------------------------------------------------------

    [Fact]
    public void Pow_ScalesEveryExponent()
    {
        var area = Dimensions.Length.Pow(2);

        Assert.Equal(2, area.Length);
    }

    [Fact]
    public void Pow_Zero_IsDimensionless() => Assert.True(Dimensions.Length.Pow(0).IsDimensionless);

    [Fact]
    public void Pow_Negative_NegatesTheExponent()
    {
        var perLength = Dimensions.Length.Pow(-1);

        Assert.Equal(-1, perLength.Length);
    }

    [Fact]
    public void Pow_MultiExponentDimension_ScalesEveryComponent()
    {
        // Pressure is M L^-1 T^-2; squared it must double every exponent,
        // not only the first.
        var pressureSquared = Dimensions.Pressure.Pow(2);

        Assert.Equal(-2, pressureSquared.Length);
        Assert.Equal(2, pressureSquared.Mass);
        Assert.Equal(-4, pressureSquared.Time);
    }

    [Fact]
    public void Pow_OverflowingAnExponent_ThrowsOverflowException()
    {
        var dimension = new Dimension(length: 50);

        Assert.Throws<OverflowException>(() => dimension.Pow(10));
    }

    // ----------------------------------------------------------------
    // ToString — a stable, deterministic rendering in fixed L,M,T,Θ,I,N,J
    // order, every zero exponent omitted.
    // ----------------------------------------------------------------

    [Fact]
    public void ToString_Dimensionless_RendersAsOne() => Assert.Equal("1", new Dimension().ToString());

    [Fact]
    public void ToString_SingleExponent_RendersSymbolAndExponent() =>
        Assert.Equal("L^1", Dimensions.Length.ToString());

    [Fact]
    public void ToString_NegativeExponent_IncludesTheSign() =>
        Assert.Equal("T^-1", new Dimension(time: -1).ToString());

    [Fact]
    public void ToString_ForcesDimension_RendersInFixedOrder_OmittingZeroExponents()
    {
        // Force is M L T^-2: three non-zero exponents, and the rendering
        // must list them in L, M, T, ... order regardless of which
        // components happen to be non-zero, not the order they were set.
        var force = Dimensions.Force;

        Assert.Equal("L^1 M^1 T^-2", force.ToString());
    }

    [Fact]
    public void ToString_EveryComponentNonZero_RendersAllSevenInOrder()
    {
        var dimension = new Dimension(1, 2, 3, 4, 5, 6, 7);

        Assert.Equal("L^1 M^2 T^3 Θ^4 I^5 N^6 J^7", dimension.ToString());
    }

    [Fact]
    public void ToString_OnlyASingleMiddleComponent_HasNoLeadingOrTrailingSpace()
    {
        // Regression against the "append a separating space only once a
        // component has already been written" logic: with only
        // ElectricCurrent set, there must be no space at either end.
        var dimension = new Dimension(electricCurrent: 1);

        Assert.Equal("I^1", dimension.ToString());
    }

    // ----------------------------------------------------------------
    // Equality (record structure — regression against Dimension ever
    // acquiring hand-written equality that forgets a field)
    // ----------------------------------------------------------------

    [Fact]
    public void Equality_SameExponents_AreEqual()
    {
        var a = new Dimension(length: 1, mass: 2);
        var b = new Dimension(length: 1, mass: 2);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentExponents_AreNotEqual()
    {
        var a = new Dimension(length: 1);
        var b = new Dimension(length: 2);

        Assert.NotEqual(a, b);
    }

    // ----------------------------------------------------------------
    // Named catalogue sanity — a handful of composed vectors, confirming
    // Dimensions.cs's own declaration-order arithmetic actually produces
    // what its remarks claim.
    // ----------------------------------------------------------------

    [Fact]
    public void Stress_IsAPureAliasOfPressure() => Assert.Equal(Dimensions.Pressure, Dimensions.Stress);

    [Fact]
    public void Torque_SharesEnergysVector() => Assert.Equal(Dimensions.Energy, Dimensions.Torque);

    [Fact]
    public void Area_IsLengthSquared() => Assert.Equal(Dimensions.Length.Pow(2), Dimensions.Area);

    [Fact]
    public void Pressure_IsForceOverArea() => Assert.Equal(Dimensions.Force / Dimensions.Area, Dimensions.Pressure);
}
