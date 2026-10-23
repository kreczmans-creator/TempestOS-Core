using Tempest.Calc.Engine.Numerics;

namespace Tempest.Calc.BuildingServices.Fluids;

/// <summary>A moist-air state: dry-bulb t (°C), humidity ratio W (kg/kg dry air), pressure p (kPa) and the derived properties.</summary>
public sealed record MoistAirState(double T, double W, double P)
{
    public double Pws => MoistAir.SaturationPressure(T) / 1e3;

    public double Pw => MoistAir.VapourPressureFromW(W, P);

    public double RelativeHumidity => Pw / Pws;

    public double Enthalpy => MoistAir.Enthalpy(T, W);

    public double SpecificVolume => MoistAir.SpecificVolume(T, W, P);

    public double DewPoint => MoistAir.DewPoint(Pw * 1e3);

    public double WetBulb => MoistAir.WetBulb(T, W, P);

    public double Density => (1 + W) / SpecificVolume;
}

/// <summary>
/// Psychrometric relations in the form given in ASHRAE Fundamentals Ch. 1
/// (ideal-gas moist air; Hyland–Wexler saturation pressure; 0.621945
/// molecular-mass ratio). Enthalpy in kJ/kg dry air, pressures in kPa
/// unless stated, temperatures in °C.
/// </summary>
public static class MoistAir
{
    public const double MinT = -60;
    public const double MaxT = 100;
    private const double Ratio = 0.621945;

    public static double SaturationPressure(double celsius) => celsius >= 0 ? Water.VapourPressure(celsius) : Water.VapourPressureOverIce(celsius);

    public static double HumidityRatioFromPw(double pw, double p) => Ratio * pw / (p - pw);

    public static double VapourPressureFromW(double w, double p) => p * w / (Ratio + w);

    public static double SaturationHumidityRatio(double t, double p) => HumidityRatioFromPw(SaturationPressure(t) / 1e3, p);

    public static double Enthalpy(double t, double w) => 1.006 * t + w * (2501 + 1.86 * t);

    public static double HumidityRatioFromEnthalpy(double t, double h) => (h - 1.006 * t) / (2501 + 1.86 * t);

    public static double TemperatureFromEnthalpy(double h, double w) => (h - 2501 * w) / (1.006 + 1.86 * w);

    public static double SpecificVolume(double t, double w, double p) => 0.287042 * (t + 273.15) * (1 + 1.607858 * w) / p;

    /// <summary>Standard-atmosphere pressure in kPa at altitude z (m).</summary>
    public static double PressureAtAltitude(double z) => 101.325 * Math.Pow(1 - 2.25577e-5 * z, 5.2559);

    /// <summary>Dew point (°C) for a vapour pressure in Pa, by bisection on the saturation curve.</summary>
    public static double DewPoint(double pwPa)
    {
        if (pwPa <= 0)
            return MinT;

        return RootFinding.Bisect(t => SaturationPressure(t) - pwPa, MinT, MaxT, 1e-9, 200);
    }

    /// <summary>Thermodynamic wet-bulb temperature (°C) from the adiabatic-saturation relation, by bisection.</summary>
    public static double WetBulb(double t, double w, double p)
    {
        double Residual(double ts) => HumidityRatioFromWetBulb(t, ts, p) - w;
        var lo = MinT;
        var hi = t;

        if (Residual(hi) * Residual(lo) > 0)
            return t; // saturated or beyond: the wet bulb is the dry bulb.

        return RootFinding.Bisect(Residual, lo, hi, 1e-9, 200);
    }

    /// <summary>Humidity ratio for a dry-bulb t and wet-bulb t* (adiabatic saturation, water above 0 °C, ice below).</summary>
    public static double HumidityRatioFromWetBulb(double t, double ts, double p)
    {
        var wsStar = SaturationHumidityRatio(ts, p);
        return ts >= 0
            ? ((2501 - 2.326 * ts) * wsStar - 1.006 * (t - ts)) / (2501 + 1.86 * t - 4.186 * ts)
            : ((2830 - 0.24 * ts) * wsStar - 1.006 * (t - ts)) / (2830 + 1.86 * t - 2.1 * ts);
    }

    public static MoistAirState FromRelativeHumidity(double t, double rh, double p) => new(t, HumidityRatioFromPw(rh * SaturationPressure(t) / 1e3, p), p);

    public static MoistAirState FromWetBulb(double t, double twb, double p) => new(t, HumidityRatioFromWetBulb(t, twb, p), p);

    public static MoistAirState FromDewPoint(double t, double tdp, double p) => new(t, HumidityRatioFromPw(SaturationPressure(tdp) / 1e3, p), p);

    public static MoistAirState FromEnthalpy(double t, double h, double p) => new(t, HumidityRatioFromEnthalpy(t, h), p);
}
