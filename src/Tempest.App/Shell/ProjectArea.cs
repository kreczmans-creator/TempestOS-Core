namespace Tempest.App.Shell;

/// <summary>
/// The second level of the navigation model — which area of an open
/// project the user is looking at.
/// </summary>
/// <remarks>
/// Mirrors the mock-ups' own project tab strip. Every area the product
/// designs is declared here; which of them are backed by a real capability
/// today is declared once, in <see cref="ProjectAreas"/>, and an area that
/// is not says so on its own surface. New members are appended, never
/// inserted: <see cref="ShellLocation"/> is persisted by ordinal.
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

    /// <summary>The project's own tasks and actions (`TD-81`).</summary>
    Tasks,

    /// <summary>The project's own risks, issues and decisions (`FCR-0056`).</summary>
    Risks,

    /// <summary>The project's own schedule, milestones and deliverables (`TD-81`).</summary>
    Timeline,

    /// <summary>Reports generated from this project's own engineering evidence (`TD-81`).</summary>
    Reports,

    /// <summary>This project's own settings — identity, lifecycle and configuration (`TD-76`).</summary>
    Settings,
}
