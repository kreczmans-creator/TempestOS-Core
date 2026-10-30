using System.Globalization;

namespace Tempest.Calc.Engine.Physics;

/// <summary>
/// The gas-law kernel shared by the sealed-system expansion vessel (B6,
/// <c>bs.sealed-system</c>) and the hydraulic accumulator (F2): the
/// polytropic relation <c>p₁·V₁ⁿ = p₂·V₂ⁿ</c> on a fixed mass of gas, with
/// absolute-pressure helpers. Pure SI doubles (Pa, m³ — or any pair of
/// consistent pressure and volume units, since only ratios enter); nothing
/// here knows about sheets, templates or units, so both discipline
/// assemblies reach it through the engine they already reference.
/// </summary>
/// <remarks>
/// Every pressure passed in is ABSOLUTE. A gauge reading is converted
/// first with <see cref="ToAbsolute"/>. The exponent <c>n</c> is 1 for an
/// isothermal change (slow, with heat exchange: a sealed-system vessel), κ
/// for an adiabatic one (fast, no heat exchange: a shock or pulsation
/// event, <see cref="Adiabatic"/>) and anything between for a polytropic
/// one. Real-gas behaviour is not modelled; a caller that needs it applies
/// its own factor (F2 states that as a user record).
/// </remarks>
public static class GasLaw
{
    /// <summary>Standard atmosphere, 101 325 Pa — the default gauge-to-absolute offset.</summary>
    public const double StandardAtmosphere = 101_325.0;

    /// <summary>One bar, 100 000 Pa — the "+ 1 bar" offset heating-system practice (CIBSE Guide B1) adds to a gauge pressure.</summary>
    public const double OneBar = 100_000.0;

    /// <summary>The exponent of an isothermal change, <c>n = 1</c> (Boyle's law).</summary>
    public const double Isothermal = 1.0;

    /// <summary>The largest exponent accepted: above any real polytropic or adiabatic gas exponent (κ ≤ 5/3 for a monatomic gas).</summary>
    public const double MaxExponent = 2.0;

    /// <summary>The adiabatic exponent κ of an ideal gas, validated (1 ≤ κ ≤ <see cref="MaxExponent"/>). Air and nitrogen: 1.4.</summary>
    public static double Adiabatic(double kappa)
    {
        if (!double.IsFinite(kappa) || kappa < 1.0 || kappa > MaxExponent)
            throw new CalcEvaluationException($"The adiabatic exponent must be between 1 and {Show(MaxExponent)}, not {Show(kappa)}.");

        return kappa;
    }

    /// <summary>A polytropic exponent, validated (0 &lt; n ≤ <see cref="MaxExponent"/>); 1 is isothermal, κ adiabatic.</summary>
    public static double Polytropic(double n)
    {
        if (!double.IsFinite(n) || n <= 0.0 || n > MaxExponent)
            throw new CalcEvaluationException($"The polytropic exponent must be above 0 and at most {Show(MaxExponent)}, not {Show(n)}.");

        return n;
    }

    /// <summary>The absolute pressure of a gauge reading: <c>gauge + atmosphere</c>. The result must be positive (a gauge vacuum cannot go below absolute zero).</summary>
    /// <param name="gauge">The gauge pressure.</param>
    /// <param name="atmosphere">The atmospheric offset in the same unit; <see cref="StandardAtmosphere"/> (Pa) by default, <see cref="OneBar"/> for the "+ 1 bar" convention.</param>
    public static double ToAbsolute(double gauge, double atmosphere = StandardAtmosphere)
    {
        if (!double.IsFinite(gauge) || !double.IsFinite(atmosphere) || atmosphere < 0.0)
            throw new CalcEvaluationException("A gauge pressure and its atmospheric offset must be finite, the offset not negative.");

        var absolute = gauge + atmosphere;

        if (!(absolute > 0.0))
            throw new CalcEvaluationException($"A gauge pressure of {Show(gauge)} with an atmosphere of {Show(atmosphere)} is at or below absolute zero.");

        return absolute;
    }

    /// <summary>The gauge reading of an absolute pressure: <c>absolute − atmosphere</c>.</summary>
    public static double ToGauge(double absolute, double atmosphere = StandardAtmosphere)
    {
        RequirePressure(absolute, "absolute pressure");

        if (!double.IsFinite(atmosphere) || atmosphere < 0.0)
            throw new CalcEvaluationException("The atmospheric offset must be finite and not negative.");

        return absolute - atmosphere;
    }

    /// <summary>The polytropic constant <c>p·Vⁿ</c> of a gas state (absolute pressure, volume).</summary>
    public static double Constant(double pressure, double volume, double n)
    {
        RequirePressure(pressure, "pressure");
        RequireVolume(volume, "volume");
        return pressure * Math.Pow(volume, Polytropic(n));
    }

    /// <summary>
    /// Solves <c>p₁·V₁ⁿ = p₂·V₂ⁿ</c> for the volume at pressure <paramref name="targetPressure"/>, given the state (<paramref name="pressure"/>,
    /// <paramref name="volume"/>): <c>V₂ = V₁·(p₁/p₂)^(1/n)</c>. All pressures are absolute. By symmetry it also gives the earlier volume
    /// from the later state.
    /// </summary>
    public static double VolumeAt(double pressure, double volume, double targetPressure, double n)
    {
        RequirePressure(pressure, "pressure");
        RequireVolume(volume, "volume");
        RequirePressure(targetPressure, "target pressure");
        return volume * Ratio(pressure / targetPressure, Polytropic(n));
    }

    /// <summary>
    /// Solves <c>p₁·V₁ⁿ = p₂·V₂ⁿ</c> for the absolute pressure at volume <paramref name="targetVolume"/>, given the state (<paramref name="pressure"/>,
    /// <paramref name="volume"/>): <c>p₂ = p₁·(V₁/V₂)ⁿ</c>.
    /// </summary>
    public static double PressureAt(double pressure, double volume, double targetVolume, double n)
    {
        RequirePressure(pressure, "pressure");
        RequireVolume(volume, "volume");
        RequireVolume(targetVolume, "target volume");
        var exponent = Polytropic(n);
        var ratio = volume / targetVolume;
        return pressure * (exponent == Isothermal ? ratio : Math.Pow(ratio, exponent));
    }

    /// <summary>The exponent <c>n = ln(p₂/p₁) / ln(V₁/V₂)</c> that joins two measured states of the same gas; <see langword="null"/> when the volumes are equal.</summary>
    public static double? ExponentBetween(double pressure1, double volume1, double pressure2, double volume2)
    {
        RequirePressure(pressure1, "pressure");
        RequirePressure(pressure2, "pressure");
        RequireVolume(volume1, "volume");
        RequireVolume(volume2, "volume");
        var denominator = Math.Log(volume1 / volume2);
        return denominator == 0.0 ? null : Math.Log(pressure2 / pressure1) / denominator;
    }

    /// <summary>
    /// The gas volume V₀ that, charged at <paramref name="chargePressure"/> and compressed from <paramref name="lowPressure"/> to
    /// <paramref name="highPressure"/>, displaces <paramref name="displaced"/> of liquid:
    /// <c>V₀ = ΔV / [(p₀/p₁)^(1/n) − (p₀/p₂)^(1/n)]</c>. The accumulator form (F2) with a pre-charge at or below the low working pressure (above it is refused); for
    /// <paramref name="chargePressure"/> = <paramref name="lowPressure"/> it is the sealed-system vessel form
    /// (<see cref="GasVolumeForDisplacement"/>). All pressures are absolute; <paramref name="lowPressure"/> &lt; <paramref name="highPressure"/>.
    /// <paramref name="displaced"/> may be zero or negative (a contraction); the result then follows its sign.
    /// </summary>
    public static double ChargeVolume(double displaced, double chargePressure, double lowPressure, double highPressure, double n)
    {
        RequireFinite(displaced, "displaced volume");
        RequirePressure(chargePressure, "pre-charge pressure");
        RequirePressure(lowPressure, "low pressure");
        RequirePressure(highPressure, "high pressure");

        if (!(lowPressure < highPressure))
            throw new CalcEvaluationException("The low pressure must be below the high pressure.");

        // Above the low working pressure the gas is already compressed when the liquid starts to enter: (p₀/p₁)^(1/n) exceeds 1 and the
        // formula would give a volume that is too small (an undersized accumulator), so it is not answered.
        if (chargePressure > lowPressure)
            throw new CalcEvaluationException($"The pre-charge pressure ({Show(chargePressure)}) must not be above the low working pressure ({Show(lowPressure)}).");

        var exponent = Polytropic(n);
        var denominator = Ratio(chargePressure / lowPressure, exponent) - Ratio(chargePressure / highPressure, exponent);

        if (!(denominator > 0.0))
            throw new CalcEvaluationException("The pressures leave no usable gas volume between them.");

        return displaced / denominator;
    }

    /// <summary>
    /// The gas volume V₀ of an accumulator whose gas is charged to <paramref name="highPressure"/> by one process and discharged to
    /// <paramref name="lowPressure"/> by another: charged from the pre-charge state at <paramref name="chargePressure"/> with exponent
    /// <paramref name="chargeExponent"/> (V₂ = V₀·(p₀/p₂)^(1/n_c)), then discharged with <paramref name="dischargeExponent"/>
    /// (V₁ = V₂·(p₂/p₁)^(1/n_d)), so that <c>ΔV = V₁ − V₂</c> and
    /// <c>V₀ = ΔV / [(p₀/p₂)^(1/n_c) · ((p₂/p₁)^(1/n_d) − 1)]</c>. A held accumulator is charged slowly (<c>n_c</c> = <see cref="Isothermal"/>,
    /// V₂ = V₀·p₀/p₂) and discharged fast (<c>n_d</c> = κ), which gives <c>V₀ = ΔV·(p₂/p₀) / [(p₂/p₁)^(1/κ) − 1]</c>. With
    /// <c>n_c</c> = <c>n_d</c> = n it is <see cref="ChargeVolume"/>. All pressures are absolute; the same rules as <see cref="ChargeVolume"/> apply
    /// (<paramref name="chargePressure"/> not above <paramref name="lowPressure"/>, <paramref name="lowPressure"/> &lt; <paramref name="highPressure"/>).
    /// </summary>
    public static double ChargeVolumeSplit(double displaced, double chargePressure, double lowPressure, double highPressure, double chargeExponent, double dischargeExponent)
    {
        RequireFinite(displaced, "displaced volume");
        RequirePressure(chargePressure, "pre-charge pressure");
        RequirePressure(lowPressure, "low pressure");
        RequirePressure(highPressure, "high pressure");

        if (!(lowPressure < highPressure))
            throw new CalcEvaluationException("The low pressure must be below the high pressure.");

        if (chargePressure > lowPressure)
            throw new CalcEvaluationException($"The pre-charge pressure ({Show(chargePressure)}) must not be above the low working pressure ({Show(lowPressure)}).");

        // V₂/V₀ from the charge process, V₁/V₂ − 1 from the discharge process.
        var heldFraction = Ratio(chargePressure / highPressure, Polytropic(chargeExponent));
        var denominator = heldFraction * (Ratio(highPressure / lowPressure, Polytropic(dischargeExponent)) - 1.0);

        if (!(denominator > 0.0))
            throw new CalcEvaluationException("The pressures leave no usable gas volume between them.");

        return displaced / denominator;
    }

    /// <summary>
    /// The gas volume at <paramref name="lowPressure"/> that displaces <paramref name="displaced"/> when compressed to
    /// <paramref name="highPressure"/>: <c>V = ΔV / [1 − (p₁/p₂)^(1/n)]</c>. With n = 1 and absolute pressures this is the expansion
    /// vessel of a sealed system: <c>V_e = ΔV / [1 − (p_i + 1)/(p_f + 1)]</c> (B6).
    /// </summary>
    public static double GasVolumeForDisplacement(double displaced, double lowPressure, double highPressure, double n)
    {
        RequireFinite(displaced, "displaced volume");
        RequirePressure(lowPressure, "low pressure");
        RequirePressure(highPressure, "high pressure");

        if (!(lowPressure < highPressure))
            throw new CalcEvaluationException("The low pressure must be below the high pressure.");

        var denominator = 1.0 - Ratio(lowPressure / highPressure, Polytropic(n));

        if (!(denominator > 0.0))
            throw new CalcEvaluationException("The pressures leave no usable gas volume between them.");

        return displaced / denominator;
    }

    /// <summary><c>ratio^(1/n)</c>; exact for the isothermal exponent (no <see cref="Math.Pow(double, double)"/> rounding).</summary>
    private static double Ratio(double ratio, double n) => n == Isothermal ? ratio : Math.Pow(ratio, 1.0 / n);

    private static void RequirePressure(double value, string what)
    {
        if (!double.IsFinite(value) || !(value > 0.0))
            throw new CalcEvaluationException($"The {what} must be a positive absolute pressure, not {Show(value)}.");
    }

    private static void RequireVolume(double value, string what)
    {
        if (!double.IsFinite(value) || !(value > 0.0))
            throw new CalcEvaluationException($"The {what} must be positive, not {Show(value)}.");
    }

    private static void RequireFinite(double value, string what)
    {
        if (!double.IsFinite(value))
            throw new CalcEvaluationException($"The {what} must be a finite number.");
    }

    private static string Show(double value) => double.IsFinite(value) ? value.ToString("G6", CultureInfo.InvariantCulture) : "not a number";
}
