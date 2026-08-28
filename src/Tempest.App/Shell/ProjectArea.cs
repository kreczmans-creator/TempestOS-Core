namespace Tempest.App.Shell;

/// <summary>
/// The second level of the navigation model — which area of an open
/// project the user is looking at.
/// </summary>
/// <remarks>
/// Mirrors the mock-ups' own project tab strip. Only the areas the
/// platform can genuinely serve today are declared: an area is added here
/// when a real surface exists behind it, never in advance to make a tab
/// strip look complete. Tasks, Commercial, Risks and Timeline are
/// deliberately absent — see `TD-81`.
/// </remarks>
public enum ProjectArea
{
    /// <summary>The project's own summary — identity, lifecycle, contents and activity.</summary>
    Overview,

    /// <summary>The project's own engineering objects, opened in the Engineering Workspace.</summary>
    Engineering,

    /// <summary>The project's own documents and drawings.</summary>
    Documents,

    /// <summary>The project's own requirements and their verification.</summary>
    Requirements,
}
