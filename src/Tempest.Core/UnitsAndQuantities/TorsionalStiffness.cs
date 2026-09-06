namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The torsional-stiffness (torque per unit angular deflection) dimension. Base unit: newton metre per radian.</summary>
/// <remarks>
/// <para>
/// Added by `Group A` (P01 Engineering Reference Data) — purely additive.
/// Needed by <c>Tempest.Core.Components</c> for a torsion spring's own
/// rate and a coupling's torsional stiffness.
/// </para>
/// <para>
/// <b>Distinct from <see cref="Torque"/> on purpose.</b> The radian is
/// dimensionless in SI, so torque per radian and torque share the same
/// base dimensions and a purely dimensional model could not tell them
/// apart. They are nonetheless entirely different engineering quantities,
/// and adding a torque to a torsional rate is a modelling error rather
/// than an arithmetic one. Separating them here is exactly what this
/// framework's phantom-typed dimensions exist to make possible
/// (`ADR-0054`): the compiler refuses the mistake that the units alone
/// could not catch.
/// </para>
/// </remarks>
public sealed class TorsionalStiffness : IDimension
{
    private TorsionalStiffness()
    {
    }
}
