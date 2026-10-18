namespace Tempest.Desktop.Views;

/// <summary>
/// The engineering disciplines the command bar serves, in the order every
/// discipline navigator lists them (`v0.24.0` Product Owner review: the
/// Engineering tree and the project's Structure tab pick a discipline;
/// <see cref="RibbonView"/> no longer shows a tab strip of its own).
/// </summary>
/// <remarks>
/// Each entry is a <see cref="Tempest.Core.Commands.CommandDescriptor.Category"/>
/// exactly as its registration declares it (<c>CalculationsWorkspaceRegistration</c>,
/// <c>DocumentsWorkspaceRegistration</c>, <c>EvidenceWorkspaceRegistration</c>,
/// <c>ManufacturingWorkspaceRegistration</c>, <c>MechanicalWorkspaceRegistration</c>,
/// <c>RequirementsWorkspaceRegistration</c>, <c>VerificationWorkspaceRegistration</c>)
/// — an exact list, so a future business-scoped category stays out of the
/// engineering surface with no second edit (`WP 19.4A`).
/// </remarks>
public static class EngineeringDisciplines
{
    /// <summary>Every engineering discipline category, alphabetical — the order the command bar has always sorted them in.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "Calculations", "Documents", "Evidence", "Manufacturing", "Mechanical", "Requirements", "Verification",
    ];

    /// <summary>The same list as a set, for <see cref="RibbonView.SetCategoryFilter"/>.</summary>
    public static IReadOnlySet<string> Categories { get; } = new HashSet<string>(All, StringComparer.Ordinal);

    /// <summary>The automation name a discipline navigator gives <paramref name="category"/>'s entry — distinct from the project tab and editor names that share the bare word.</summary>
    public static string NavigatorName(string category) => $"{category} discipline";
}
