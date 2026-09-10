using Tempest.Workspace.Calculations;
using Tempest.Workspace.Deliverables;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Timesheets;
using Tempest.Workspace.Verification;
using Tempest.Core.Requirements;

namespace Tempest.Workspace;

/// <summary>
/// Which Project Explorer area shows an object of a given Kind
/// (`WP 17.9.4`). The shell needs this the moment something is created:
/// the new object must be shown where it lives, whichever discipline tab
/// the user happened to be on.
/// </summary>
/// <remarks>
/// Built from each discipline's own declared Kinds, so a Kind added to a
/// registry is mapped here by construction. A Kind no discipline declares
/// maps to <see langword="null"/>: the shell then leaves the area alone and
/// still opens the object.
/// </remarks>
public static class DisciplineAreas
{
    private static readonly IReadOnlyDictionary<string, string> AreaByKind = Build();

    /// <summary>The explorer area that lists <paramref name="kind"/>, or <see langword="null"/> when no discipline declares it.</summary>
    public static string? AreaFor(string? kind) =>
        kind is not null && AreaByKind.TryGetValue(kind, out var area) ? area : null;

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kind in MechanicalObjectFactoryRegistry.SupportedKinds)
            map[kind] = MechanicalWorkspaceExplorerModule.NavigationItemId;

        map[RequirementsService.RequirementDocumentKind] = RequirementsWorkspaceExplorerModule.NavigationItemId;
        map[RequirementsService.RequirementGroupDocumentKind] = RequirementsWorkspaceExplorerModule.NavigationItemId;
        map[RequirementsService.RequirementCollectionDocumentKind] = RequirementsWorkspaceExplorerModule.NavigationItemId;

        foreach (var kind in CalculationObjectFactoryRegistry.SupportedKinds)
            map[kind] = CalculationsWorkspaceExplorerModule.NavigationItemId;

        foreach (var kind in DocumentObjectFactoryRegistry.SupportedKinds)
            map[kind] = DocumentsWorkspaceExplorerModule.NavigationItemId;

        map["VerificationActivity"] = VerificationWorkspaceExplorerModule.NavigationItemId;

        foreach (var kind in ManufacturingObjectFactoryRegistry.SupportedKinds)
            map[kind] = ManufacturingWorkspaceExplorerModule.NavigationItemId;

        map[Tempest.Core.Evidence.Evidence.CanonicalKind] = EvidenceWorkspaceRegistration.ExplorerAreaId;

        // `ADR-0150` (`WP 19.0A`).
        map[Tempest.Core.Timesheets.TimesheetEntry.CanonicalKind] = TimesheetsWorkspaceRegistration.ExplorerAreaId;
        map[Tempest.Core.Deliverables.DeliverableCompletion.CanonicalKind] = DeliverableCompletionWorkspaceRegistration.ExplorerAreaId;

        return map;
    }
}
