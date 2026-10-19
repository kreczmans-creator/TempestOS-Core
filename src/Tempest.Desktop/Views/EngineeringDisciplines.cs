namespace Tempest.Desktop.Views;

/// <summary>
/// The command categories the engineering surface serves, split the way
/// the Product Owner set out (`ADR-0163`): <see cref="Disciplines"/> are
/// engineering domains; <see cref="Modules"/> are shared tools any
/// discipline or project uses. Both navigators — the Engineering tree and
/// a project's Structure tab — list them in this order.
/// </summary>
/// <remarks>
/// Each entry is a <see cref="Tempest.Core.Commands.CommandDescriptor.Category"/>
/// exactly as its registration declares it — an exact list, so a future
/// business-scoped category stays out of the engineering surface with no
/// second edit (`WP 19.4A`).
/// </remarks>
public static class EngineeringDisciplines
{
    /// <summary>The engineering disciplines, in the order the navigators list them.</summary>
    public static IReadOnlyList<string> Disciplines { get; } =
    [
        "Mechanical", "Structural", "Electrical", "Building Services", "Manufacturing",
    ];

    /// <summary>The shared modules shown on the engineering surface, in the order the navigators list them.</summary>
    public static IReadOnlyList<string> Modules { get; } =
    [
        "Calculations", "Requirements", "Verification", "Documents", "Evidence",
    ];

    /// <summary>Every category the engineering surface shows — disciplines, then modules.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Disciplines, .. Modules];

    /// <summary>The same list as a set, for <see cref="RibbonView.SetCategoryFilter"/>.</summary>
    public static IReadOnlySet<string> Categories { get; } = new HashSet<string>(All, StringComparer.Ordinal);

    /// <summary>Whether <paramref name="category"/> is a discipline rather than a module.</summary>
    public static bool IsDiscipline(string category) => Disciplines.Contains(category, StringComparer.Ordinal);

    /// <summary>The label a navigator shows for <paramref name="category"/> — "Building Services / HVAC" for Building Services, the category itself otherwise.</summary>
    public static string Label(string category) => category == "Building Services" ? "Building Services / HVAC" : category;

    /// <summary>The automation name a navigator gives <paramref name="category"/>'s entry — "… discipline" or "… module", distinct from the project tab and editor names that share the bare word.</summary>
    public static string NavigatorName(string category) => IsDiscipline(category) ? $"{category} discipline" : $"{category} module";
}
