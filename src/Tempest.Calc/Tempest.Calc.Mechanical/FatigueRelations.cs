namespace Tempest.Calc.Mechanical;

/// <summary>
/// The Goodman relation the fatigue checks of the mechanical calculators share: <c>1/n_f = a/S_e + m/S_u</c> for an
/// alternating stress <c>a</c> and a mean stress <c>m</c> of one kind (normal or shear), the endurance strength <c>S_e</c>
/// and the ultimate strength <c>S_u</c> of the same kind. M4 (<c>mech.shaft</c>) writes the same relation inline, with the
/// von Mises shear term <c>√3·τ_m</c> as its mean stress; this function is its general form and M4 is not changed by it.
/// </summary>
public static class FatigueRelations
{
    /// <summary>The Goodman utilisation <c>1/n_f = a/S_e + m/S_u</c>. Non-positive strengths give <see cref="double.PositiveInfinity"/>.</summary>
    public static double GoodmanUtilisation(double amplitude, double mean, double endurance, double ultimate) =>
        endurance > 0 && ultimate > 0 ? amplitude / endurance + mean / ultimate : double.PositiveInfinity;

    /// <summary>The Goodman safety factor <c>n_f</c>; <see cref="double.PositiveInfinity"/> when nothing is stressed.</summary>
    public static double GoodmanFactor(double amplitude, double mean, double endurance, double ultimate)
    {
        var u = GoodmanUtilisation(amplitude, mean, endurance, ultimate);
        return u > 0 ? 1 / u : double.PositiveInfinity;
    }
}
