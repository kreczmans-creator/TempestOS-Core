using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Companion.Contracts;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// One project's own concise operational summary — identity, lifecycle
/// status, revision, creation, and Digital Thread link count, from the
/// already-fetched <see cref="ProjectSummaryDto"/>. Record values are
/// machine data: Space Mono, UTC stamps with a trailing <c>Z</c>
/// (`WP 14.1A`).
/// </summary>
public sealed class ProjectDetailPage : CompanionPage
{
    private readonly ProjectSummaryDto _project;
    private readonly Action _onBack;

    /// <summary>Initialises a new instance of the <see cref="ProjectDetailPage"/> class.</summary>
    /// <param name="project">The project to render.</param>
    /// <param name="onBack">Invoked when the back affordance is tapped.</param>
    public ProjectDetailPage(ProjectSummaryDto project, Action onBack)
        : base(project.DisplayName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(onBack);

        _project = project;
        _onBack = onBack;
        Render();
    }

    /// <inheritdoc />
    public override Task RefreshAsync()
    {
        // Renders the summary it was opened with - project-scoped live
        // reads beyond the list DTO are future Companion capability, not
        // silently faked here.
        Render();
        return Task.CompletedTask;
    }

    private void Render()
    {
        var column = new StackPanel { Spacing = CompanionTokens.CardSpacing };

        var back = BrandButtons.Quiet("Back to projects");
        back.HorizontalAlignment = HorizontalAlignment.Left;
        Avalonia.Automation.AutomationProperties.SetName(back, "Back to Projects");
        back.Click += (_, _) => _onBack();
        column.Children.Add(back);

        var identity = new CompanionCard(_project.DisplayName, ProjectsPage.LifecycleColour(_project.Status));
        identity.AddMonoLine($"identifier · {_project.Identifier?.ToLowerInvariant() ?? "—"}");
        identity.AddMonoLine($"project-id · {_project.Id:D}");
        identity.AddContent(StatusChip(_project.Status, ProjectsPage.LifecycleColour(_project.Status)));
        column.Children.Add(identity);

        var record = new CompanionCard("Record");
        record.AddMonoLine($"revision  · {_project.CurrentRevisionNumber}");
        record.AddMonoLine($"created   · {FormatMoment(_project.CreatedAtUtc)}");
        record.AddMonoLine($"links-out · {_project.OutgoingLinkCount}");
        column.Children.Add(record);

        var boundary = new CompanionCard("Work With This Project");
        boundary.AddLine("Structure, requirements, calculations and documentation for this project are authored in the Tempest OS desktop Workspace.", secondary: true);
        column.Children.Add(boundary);

        ShowContent(column);
    }
}
