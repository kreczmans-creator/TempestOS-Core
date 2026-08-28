using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Contracts;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// Mobile project awareness — every live Project, most recent first,
/// each row opening the project's own operational summary. Row metadata
/// is machine data (mono, `·`-separated, per `WP 14.1A`); lifecycle is a
/// status dot plus its own text. A drill-down list, deliberately not the
/// desktop Project Explorer tree.
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

    /// <summary>Maps a <c>LifecycleState</c> name to a machine-state colour — released/approved run, in-review holds, cancelled-class states are dead.</summary>
    internal static IBrush LifecycleColour(string status) => status switch
    {
        "Released" or "Approved" => new SolidColorBrush(BrandPalette.Green500),
        "InReview" => new SolidColorBrush(BrandPalette.Amber500),
        "Cancelled" or "Obsolete" => new SolidColorBrush(BrandPalette.Red500),
        _ => new SolidColorBrush(BrandPalette.Slate500),
    };

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
            yield return new EmptyStateView("No Projects exist yet. Create one from the Tempest OS desktop Workspace.") { MinHeight = 320 };
            yield break;
        }

        var card = new CompanionCard($"Projects · {list.Projects.Count}");

        foreach (var project in list.Projects)
            card.AddContent(ProjectRow(project));

        yield return card;
    }

    private Control ProjectRow(ProjectSummaryDto project)
    {
        var app = Avalonia.Application.Current!;

        var body = new StackPanel { Spacing = CompanionTokens.SpaceXs };
        body.Children.Add(new TextBlock
        {
            Text = project.DisplayName,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = CompanionTokens.FontSizeBody,
            FontWeight = CompanionTokens.WeightHeading,
            Foreground = BrandPalette.Brush(app, BrandPalette.BodyTextBrushKey),
        });
        body.Children.Add(new TextBlock
        {
            Text = $"{project.Identifier?.ToLowerInvariant() ?? "—"} · rev {project.CurrentRevisionNumber} · {project.OutgoingLinkCount} links",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
            Foreground = BrandPalette.Brush(app, BrandPalette.SecondaryTextBrushKey),
        });
        body.Children.Add(StatusChip(project.Status, LifecycleColour(project.Status)));

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(body);
        var arrow = new TextBlock
        {
            Text = "→",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 16,
            Foreground = BrandPalette.Brush(app, BrandPalette.AccentBrushKey),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(arrow, 1);
        row.Children.Add(arrow);

        var button = new Button
        {
            MinHeight = CompanionTokens.MinTouchTarget,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(CompanionTokens.ControlCornerRadius),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Avalonia.Thickness(0, CompanionTokens.SpaceSm),
            Content = row,
        };
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(BrandPalette.Paper050, 0.05);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(BrandPalette.Paper050, 0.09);
        Avalonia.Automation.AutomationProperties.SetName(button, $"Open project {project.DisplayName}");
        button.Click += (_, _) => _onOpenProject(project);

        return button;
    }
}
