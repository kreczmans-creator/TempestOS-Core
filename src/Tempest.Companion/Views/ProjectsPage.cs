using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Companion.Contracts;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// Mobile project awareness — every live Project, most recent first, each
/// row opening the project's own operational summary
/// (<see cref="ProjectDetailPage"/>). A drill-down list, deliberately not
/// the desktop Project Explorer tree: a phone triages projects, it does
/// not author product structure (`WP 14.0A`'s own observe → understand →
/// decide → act scope).
/// </summary>
public sealed class ProjectsPage : CompanionPage
{
    private readonly CompanionDataService _data;
    private readonly Action<ProjectSummaryDto> _onOpenProject;

    /// <summary>Initialises a new instance of the <see cref="ProjectsPage"/> class.</summary>
    /// <param name="data">The Companion data service.</param>
    /// <param name="onOpenProject">Invoked when a project row is tapped.</param>
    public ProjectsPage(CompanionDataService data, Action<ProjectSummaryDto> onOpenProject)
        : base("Projects")
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(onOpenProject);

        _data = data;
        _onOpenProject = onOpenProject;
        ShowLoading();
    }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        ShowLoading();
        var result = await _data.GetProjectsAsync();
        ShowResult(result, Render);
    }

    private IEnumerable<Control> Render(ProjectListDto list)
    {
        if (list.Projects.Count == 0)
        {
            yield return new EmptyStateView("⬡", "No Projects exist yet. Create one from the TempestOS desktop Workspace.") { MinHeight = 320 };
            yield break;
        }

        var card = new CompanionCard("⬡", $"Projects ({list.Projects.Count})");

        foreach (var project in list.Projects)
            card.AddContent(ProjectRow(project));

        yield return card;
    }

    private Control ProjectRow(ProjectSummaryDto project)
    {
        var body = new StackPanel { Spacing = CompanionTokens.SpaceXs };
        body.Children.Add(new TextBlock
        {
            Text = project.DisplayName,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = CompanionTokens.FontSizeHeading,
            FontWeight = CompanionTokens.WeightHeading,
        });
        body.Children.Add(new TextBlock
        {
            Text = $"{project.Identifier ?? "—"} · rev {project.CurrentRevisionNumber} · {project.OutgoingLinkCount} link(s)",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
        });
        body.Children.Add(StatusChip(project.Status, CompanionStatusColors.ForHealth(project.Status == "Released" ? "Healthy" : "Unknown")));

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(body);
        var chevron = new TextBlock
        {
            Text = "›",
            FontSize = 22,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(chevron, 1);
        row.Children.Add(chevron);

        var button = new Button
        {
            MinHeight = CompanionTokens.MinTouchTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = row,
        };
        Avalonia.Automation.AutomationProperties.SetName(button, $"Open project {project.DisplayName}");
        button.Click += (_, _) => _onOpenProject(project);

        return button;
    }
}
