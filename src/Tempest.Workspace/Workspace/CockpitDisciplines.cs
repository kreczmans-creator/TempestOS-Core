namespace Tempest.Workspace;

/// <summary>
/// The closed, lower-case discipline vocabulary the Engineering Cockpit
/// uses to say which of its six discipline collaborators (`ADR-0103`) a
/// given entry came from — and <see cref="Cockpit"/> for the entries the
/// composition root itself adds. First used by
/// <see cref="EngineeringCockpit.AttentionItemsByDiscipline"/>, and reused
/// verbatim as the JSON keys of the Dashboard Export's own
/// <c>engineering-status.json</c> (schema v2), so the Pi dashboard and the
/// desktop Cockpit name every discipline the same way.
/// </summary>
public static class CockpitDisciplines
{
    /// <summary>The Mechanical Product Structure discipline (<c>MechanicalCockpitReadModel</c>).</summary>
    public const string Mechanical = "mechanical";

    /// <summary>The Requirements discipline (<c>RequirementsCockpitReadModel</c>).</summary>
    public const string Requirements = "requirements";

    /// <summary>The Calculations discipline (<c>CalculationsCockpitReadModel</c>).</summary>
    public const string Calculations = "calculations";

    /// <summary>The Documents discipline (<c>DocumentsCockpitReadModel</c>).</summary>
    public const string Documents = "documents";

    /// <summary>The Verification discipline (<c>VerificationCockpitReadModel</c>).</summary>
    public const string Verification = "verification";

    /// <summary>The Manufacturing discipline (<c>ManufacturingCockpitReadModel</c>).</summary>
    public const string Manufacturing = "manufacturing";

    /// <summary>An entry the Cockpit composition root itself adds, belonging to no single discipline — for example the fixed "Other disciplines still placeholder" attention item.</summary>
    public const string Cockpit = "cockpit";
}
