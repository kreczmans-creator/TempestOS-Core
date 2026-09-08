namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// The named <see cref="Dimension"/> vector for every dimension this
/// framework holds — the runtime counterpart to each compile-time marker
/// type's own <c>static Dimension Vector</c> (<c>IDimension.Vector</c>).
/// </summary>
/// <remarks>
/// <para>
/// `ADR-0147`. A separate type from <see cref="Dimension"/> itself — see
/// <see cref="Dimension"/>'s own remarks for why: three of these names
/// (<see cref="Length"/>, <see cref="Mass"/>, <see cref="Temperature"/>)
/// would collide with an identically-named instance exponent property if
/// they lived on <see cref="Dimension"/> directly.
/// </para>
/// <para>
/// Two names can share the exact same vector on purpose.
/// <see cref="Stress"/> is a pure alias of <see cref="Pressure"/>;
/// <see cref="Torque"/> shares <see cref="Energy"/>'s own vector; and
/// <see cref="TorsionalStiffness"/> and <see cref="RotationalSpeed"/>
/// coincide with other vectors the same way, exactly as the generic
/// facade's own existing marker types already do (`ADR-0054`) — the
/// vector alone cannot, and is not meant to, separate a torque from an
/// energy or a stiffness from a torsional one. That separation is what
/// the compile-time <c>Quantity&lt;TDimension&gt;</c> facade exists for;
/// the runtime <see cref="Dimension"/> vector answers only "what does this
/// reduce to dimensionally," which for those pairs is genuinely the same
/// answer.
/// </para>
/// <para>
/// Declaration order matters: a derived vector below (e.g.
/// <see cref="Area"/>) is computed from an earlier one
/// (<see cref="Length"/>) via <c>*</c>/<c>/</c>/<see cref="Dimension.Pow"/>,
/// and C# initialises static fields in textual order — so every
/// dimension is declared after every other dimension it is built from.
/// </para>
/// </remarks>
public static class Dimensions
{
    /// <summary>The unit one — every exponent zero.</summary>
    public static readonly Dimension Dimensionless = default;

    /// <summary>L.</summary>
    public static readonly Dimension Length = new(length: 1);

    /// <summary>M.</summary>
    public static readonly Dimension Mass = new(mass: 1);

    /// <summary>T. Named <see cref="Duration"/>, matching <see cref="UnitsAndQuantities.Duration"/>'s own marker type.</summary>
    public static readonly Dimension Duration = new(time: 1);

    /// <summary>Θ.</summary>
    public static readonly Dimension Temperature = new(temperature: 1);

    /// <summary>I.</summary>
    public static readonly Dimension ElectricCurrent = new(electricCurrent: 1);

    /// <summary>N.</summary>
    public static readonly Dimension AmountOfSubstance = new(amountOfSubstance: 1);

    /// <summary>J.</summary>
    public static readonly Dimension LuminousIntensity = new(luminousIntensity: 1);

    /// <summary>L².</summary>
    public static readonly Dimension Area = Length.Pow(2);

    /// <summary>L³.</summary>
    public static readonly Dimension Volume = Length.Pow(3);

    /// <summary>L T⁻¹.</summary>
    public static readonly Dimension Velocity = Length / Duration;

    /// <summary>L T⁻².</summary>
    public static readonly Dimension Acceleration = Velocity / Duration;

    /// <summary>M L T⁻².</summary>
    public static readonly Dimension Force = Mass * Acceleration;

    /// <summary>M L⁻¹ T⁻².</summary>
    public static readonly Dimension Pressure = Force / Area;

    /// <summary>M L⁻¹ T⁻² — a pure alias of <see cref="Pressure"/>'s own vector.</summary>
    public static readonly Dimension Stress = Pressure;

    /// <summary>M L² T⁻².</summary>
    public static readonly Dimension Energy = Force * Length;

    /// <summary>M L² T⁻³.</summary>
    public static readonly Dimension Power = Energy / Duration;

    /// <summary>M L² T⁻² — the same vector as <see cref="Energy"/>, under its own name (`ADR-0054`).</summary>
    public static readonly Dimension Torque = Energy;

    /// <summary>M L⁻³.</summary>
    public static readonly Dimension MassDensity = Mass / Volume;

    /// <summary>M T⁻².</summary>
    public static readonly Dimension Stiffness = Force / Length;

    /// <summary>M L² T⁻² — the same vector as <see cref="Torque"/>; the radian a torsional rate is "per" is dimensionless in SI.</summary>
    public static readonly Dimension TorsionalStiffness = Torque;

    /// <summary>T⁻¹ — a revolution is a dimensionless count.</summary>
    public static readonly Dimension RotationalSpeed = Duration.Pow(-1);

    /// <summary>Dimensionless, under its own name.</summary>
    public static readonly Dimension PlaneAngle = Dimensionless;

    /// <summary>L² T⁻² Θ⁻¹.</summary>
    public static readonly Dimension SpecificHeatCapacity = Energy / (Mass * Temperature);

    /// <summary>M L T⁻³ Θ⁻¹.</summary>
    public static readonly Dimension ThermalConductivity = Power / (Length * Temperature);

    /// <summary>Θ⁻¹.</summary>
    public static readonly Dimension ThermalExpansion = Temperature.Pow(-1);

    /// <summary>L⁴.</summary>
    public static readonly Dimension SecondMomentOfArea = Length.Pow(4);

    /// <summary>L³.</summary>
    public static readonly Dimension SectionModulus = Length.Pow(3);

    /// <summary>T⁻¹.</summary>
    public static readonly Dimension Frequency = Duration.Pow(-1);

    /// <summary>M L² T⁻³ I⁻¹ — volts, watts per ampere.</summary>
    public static readonly Dimension Voltage = Power / ElectricCurrent;

    /// <summary>M L² T⁻³ I⁻² — ohms, volts per ampere.</summary>
    public static readonly Dimension Resistance = Voltage / ElectricCurrent;

    /// <summary>I T — coulombs, amperes times seconds.</summary>
    public static readonly Dimension ElectricCharge = ElectricCurrent * Duration;

    /// <summary>
    /// Θ — the same vector as <see cref="Temperature"/>. A temperature
    /// <em>interval</em> (5 kelvin of change) is dimensionally identical to
    /// a temperature <em>position</em> (5 kelvin above absolute zero); what
    /// distinguishes them is not the vector but which unit family
    /// (<see cref="TemperatureUnits"/> vs. <see cref="TemperatureDeltaUnits"/>)
    /// a given value was expressed in — see <c>Quantity</c>'s own remarks
    /// on affine arithmetic (`ADR-0147`, extending `ADR-0125`).
    /// </summary>
    public static readonly Dimension TemperatureDelta = Temperature;
}
