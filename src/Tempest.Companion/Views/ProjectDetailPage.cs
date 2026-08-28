using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Companion.Contracts;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// One project's own concise operational summary — identity, lifecycle
/// status, revision, creation, and Digital Thread link count, from the
/// already-fetched <see cref="ProjectSummaryDto"/>. A drill-down the user
/// returns naturally from (the shell's own back affordance); deeper
/// engineering interaction stays on the desktop by design.
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
        // reads beyond the list DTO are future Companion capability
        // (FCR: richer per-project drill-down), not silently faked here.
        Render();
        return Task.CompletedTask;
    }

    private void Render()
    {
        var column = new StackPanel { Spacing = CompanionTokens.CardSpacing };

        var back = new Button
        {
            Content = "‹  Projects",
            MinHeight = CompanionTokens.MinTouchTarget,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Avalonia.Automation.AutomationProperties.SetName(back, "Back to Projects");
        back.Click += (_, _) => _onBack();
        column.Children.Add(back);

        var identity = new CompanionCard("⬡", _project.DisplayName);
        identity.AddMonoLine($"Identifier  {_project.Identifier ?? "—"}");
        identity.AddMonoLine($"Project Id  {_project.Id:D}");
        identity.AddContent(StatusChip(_project.Status, CompanionStatusColors.ForHealth(_project.Status == "Released" ? "Healthy" : "Unknown")));
        column.Children.Add(identity);

        var record = new CompanionCard("≡", "Record");
        record.AddMonoLine($"Revision    {_project.CurrentRevisionNumber}");
        record.AddMonoLine($"Created     {FormatMoment(_project.CreatedAtUtc)}");
        record.AddMonoLine($"Links out   {_project.OutgoingLinkCount}");
        column.Children.Add(record);

        var boundary = new CompanionCard("☰", "Work With This Project");
        boundary.AddLine("Structure, requirements, calculations and documentation for this project are authored in the TempestOS desktop Workspace.", secondary: true);
        column.Children.Add(boundary);

        ShowContent(column);
    }
}
