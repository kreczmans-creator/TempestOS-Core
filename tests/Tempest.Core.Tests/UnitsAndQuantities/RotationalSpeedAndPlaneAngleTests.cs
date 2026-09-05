using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

// The two dimensions WP A4 added to this framework. Purely additive, and
// held to exactly the same rules as the seven that preceded them.
public class RotationalSpeedAndPlaneAngleTests
{
    [Fact]
    public void RotationalSpeed_BaseUnit_IsTheRevolutionPerSecond()
    {
        Assert.Equal(1.0, RotationalSpeedUnits.RevolutionPerSecond.ToBaseUnitFactor);
        Assert.Equal("r/s", RotationalSpeedUnits.RevolutionPerSecond.Symbol);
    }

    [Fact]
    public void RotationalSpeed_RevolutionsPerMinute_ConvertToRevolutionsPerSecond()
    {
        var speed = new Quantity<RotationalSpeed>(3000, RotationalSpeedUnits.RevolutionPerMinute);

        Assert.Equal(50.0, speed.ConvertTo(RotationalSpeedUnits.RevolutionPerSecond).Value, 12);
    }

    [Fact]
    public void RotationalSpeed_RadiansPerSecond_ConvertToRevolutionsPerMinute()
    {
        var speed = new Quantity<RotationalSpeed>(2.0 * Math.PI, RotationalSpeedUnits.RadianPerSecond);

        Assert.Equal(60.0, speed.ConvertTo(RotationalSpeedUnits.RevolutionPerMinute).Value, 9);
    }

    [Fact]
    public void RotationalSpeed_RoundTripsThroughItsOwnCatalogueParser()
    {
        Assert.True(Quantity<RotationalSpeed>.TryParse("20000 r/min", RotationalSpeedUnits.All, out var parsed));
        Assert.Equal(20000, parsed.Value);
        Assert.Equal("20000 r/min", parsed.ToString());
    }

    [Fact]
    public void PlaneAngle_BaseUnit_IsTheRadian()
    {
        Assert.Equal(1.0, PlaneAngleUnits.Radian.ToBaseUnitFactor);
        Assert.Equal("rad", PlaneAngleUnits.Radian.Symbol);
    }

    [Fact]
    public void PlaneAngle_DegreesConvertToRadians()
    {
        var angle = new Quantity<PlaneAngle>(180, PlaneAngleUnits.Degree);

        Assert.Equal(Math.PI, angle.ConvertTo(PlaneAngleUnits.Radian).Value, 12);
    }

    [Fact]
    public void PlaneAngle_ArcMinutesConvertToDegrees()
    {
        var angle = new Quantity<PlaneAngle>(90, PlaneAngleUnits.ArcMinute);

        Assert.Equal(1.5, angle.ConvertTo(PlaneAngleUnits.Degree).Value, 12);
    }

    [Fact]
    public void BothNewDimensions_RefuseANonFiniteValueLikeEveryOther()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity<RotationalSpeed>(double.NaN, RotationalSpeedUnits.RevolutionPerMinute));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity<PlaneAngle>(double.PositiveInfinity, PlaneAngleUnits.Degree));
    }

    [Fact]
    public void BothNewDimensions_RefuseImplicitConversionInArithmeticLikeEveryOther()
    {
        var rpm = new Quantity<RotationalSpeed>(60, RotationalSpeedUnits.RevolutionPerMinute);
        var rps = new Quantity<RotationalSpeed>(1, RotationalSpeedUnits.RevolutionPerSecond);

        Assert.Throws<IncompatibleUnitsException>(() => rpm + rps);
    }

    [Fact]
    public void NewUnitCatalogues_ListEveryUnitTheyDefine()
    {
        Assert.Equal(3, RotationalSpeedUnits.All.Count);
        Assert.Equal(3, PlaneAngleUnits.All.Count);
    }
}
