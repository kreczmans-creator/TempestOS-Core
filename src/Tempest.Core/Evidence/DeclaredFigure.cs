namespace Tempest.Core.Evidence;

/// <summary>Whether a <see cref="DeclaredFigure"/> was fed into the work, or came out of it.</summary>
public enum DeclaredFigureRole
{
    /// <summary>A value the evidence's own work took as given.</summary>
    Input,

    /// <summary>A value the evidence's own work produced.</summary>
    Result,
}

/// <summary>
/// One named, typed quantity declared against a piece of evidence, so
/// figures are searchable and comparable across evidence (`ADR-0148`).
/// </summary>
/// <param name="Name">What the figure is, in the engineer's own words (e.g. "Utilisation", "Max stress").</param>
/// <param name="Role">Whether this figure was fed into the work or came out of it.</param>
/// <param name="Quantity">
/// The value, as <c>"&lt;value&gt; &lt;unit symbol&gt;"</c> text — the
/// runtime-dimension <see cref="Tempest.Core.UnitsAndQuantities.Quantity"/>'s
/// own <c>ToString</c>/<c>Parse</c> form (`ADR-0147`), checked against
/// <see cref="EvidenceUnitCatalog.KnownUnits"/> at declaration time; an
/// unrecognised unit is refused.
/// </param>
public sealed record DeclaredFigure(string Name, DeclaredFigureRole Role, string Quantity);
