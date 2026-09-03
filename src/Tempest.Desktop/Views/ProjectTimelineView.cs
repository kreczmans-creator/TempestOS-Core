using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Projects;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Project Workspace's Timeline area — the project's own milestones, the
/// deliverables due against them, and the work contributing to each.
/// </summary>
/// <remarks>
/// <para>
/// <b>A dated list, not a Gantt chart.</b> Milestones are shown in date
/// order with what is attached to each. There is no time axis, no bar, no
/// dependency line and no critical path, because none of those exist in the
/// domain — drawing them would mean inventing a schedule the platform does
/// not hold. This is the honest surface for the model as it stands.
/// </para>
/// <para>
/// <b>A view, not a decision-maker.</b> Everything shown comes from
/// <see cref="IProjectMilestoneRegister"/>, and every action the user takes
/// is raised as an event for the shell to perform through
/// <see cref="IProjectMilestoneService"/> — the same discipline
/// <see cref="ProjectTasksView"/> and <see cref="ProjectRisksView"/> follow.
/// </para>
/// <para>
/// <b>An ordinary surface (`TD-72`).</b> A plain <see cref="UserControl"/>
/// rendered into the project workspace's own tab host, exactly like
/// Documents, Requirements, Tasks and Risks.
/// </para>
/// </remarks>
public sealed class ProjectTimelineView : UserControl
{
    /// <summary>The heading shown above the timeline.</summary>
    public const string Heading = "Timeline";

    /// <summary>What the surface says when the project has no milestones at all.</summary>
    public const string EmptyHeadline = "No milestones in this project";

    /// <summary>The note shown against a milestone whose date has passed with work still open.</summary>
    public const string PastDueNote = "Past target date, work still open";

    /// <summary>The note shown against a milestone whose date has passed with nothing attached.</summary>
    public const string NothingLinkedNote = "Past target date, nothing linked to it";

    /// <summary>What a milestone with no work or deliverables reads as.</summary>
    public const string NoLinkedWorkLabel = "Nothing linked yet";

    private readonly StackPanel _list = new() { Spacing = DesignTokens.SpaceSm };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly Button _newMilestone = new() { Content = "Set Milestone", MinHeight = DesignTokens.MinControlSize };

    private string? _projectLabel;

    /// <summary>Raised when the user asks to set a milestone.</summary>
    public event Action? CreateMilestoneRequested;

    /// <summary>Raised when the user asks to add a deliverable against a milestone.</summary>
    public event Action<Guid>? AddDeliverableRequested;

    /// <summary>Raised when the user asks to edit a milestone.</summary>
    public event Action<Guid>? EditMilestoneRequested;

    /// <summary>Initialises a new instance of the <see cref="ProjectTimelineView"/> class.</summary>
    public ProjectTimelineView()
    {
        var heading = new TextBlock
        {
            Text = Heading,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        // `WP-Z4` Productisation Phase 1 (P1) — the one primary
        // call-to-action this area offers.
        _newMilestone.Classes.Add(ChromeStyles.Primary);

        AutomationProperties.SetName(_newMilestone, "Set a milestone in this project");
        _newMilestone.Click += (_, _) => CreateMilestoneRequested?.Invoke();

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(_newMilestone);

        var root = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelPadding };
        root.Children.Add(heading);
        root.Children.Add(_summary);
        root.Children.Add(actions);
        root.Children.Add(_list);

        AutomationProperties.SetName(this, Heading);
        Content = new ScrollViewer { Content = root };

        Render();
    }

    /// <summary>The milestones currently on screen, in the order the register returned them.</summary>
    public IReadOnlyList<ProjectMilestoneEntry> Milestones { get; private set; } = [];

    /// <summary>Whether the surface is showing its empty state.</summary>
    public bool IsShowingEmptyState { get; private set; } = true;

    /// <summary>The summary line, exactly as a user reads it.</summary>
    public string SummaryText => _summary.Text ?? string.Empty;

    /// <summary>Renders <paramref name="milestones"/> for the project named <paramref name="projectLabel"/>.</summary>
    public void Show(IReadOnlyList<ProjectMilestoneEntry> milestones, string? projectLabel)
    {
        ArgumentNullException.ThrowIfNull(milestones);

        Milestones = milestones;
        _projectLabel = projectLabel;

        Render();
    }

    private void Render()
    {
        _list.Children.Clear();

        var project = string.IsNullOrWhiteSpace(_projectLabel) ? "this project" : _projectLabel;

        IsShowingEmptyState = Milestones.Count == 0;
        if (IsShowingEmptyState)
        {
            _summary.Text = $"No milestones in {project} yet.";
            _list.Children.Add(EmptyState(
                EmptyHeadline,
                "A milestone is a date this project is working to. Set one here, add the deliverables due against it, and link tasks to it from the Tasks area — this timeline then shows what each date is actually carrying."));
            return;
        }

        var pastDue = Milestones.Count(m => m.IsPastWithOutstandingWork);
        var unlinked = Milestones.Count(m => !m.HasLinkedWork);

        _summary.Text =
            $"{Milestones.Count} milestone(s) in {project} — {pastDue} past their date with work still open, {unlinked} with nothing linked.";

        foreach (var entry in Milestones)
            _list.Children.Add(BuildMilestone(entry));
    }

    private Control BuildMilestone(ProjectMilestoneEntry entry)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        header.Children.Add(new TextBlock
        {
            // The date leads, because this is a timeline and the date is
            // what a reader is scanning for.
            Text = entry.TargetDate.ToString("yyyy-MM-dd"),
            FontWeight = DesignTokens.WeightHeading,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(Caption(entry.Identifier ?? "Milestone"));
        header.Children.Add(Caption(entry.Status.ToString()));
        rows.Children.Add(header);

        rows.Children.Add(new TextBlock
        {
            Text = entry.DisplayName,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });

        if (!string.IsNullOrWhiteSpace(entry.Description))
            rows.Children.Add(Detail(entry.Description));

        // The two honest warnings, kept distinct: a date that has passed
        // with work outstanding is a different problem from a date nobody
        // ever attached anything to.
        if (entry.IsPastWithOutstandingWork)
            rows.Children.Add(Warning(PastDueNote));
        else if (entry.IsPastWithNothingLinked)
            rows.Children.Add(Warning(NothingLinkedNote));

        if (!entry.HasLinkedWork)
        {
            rows.Children.Add(Caption(NoLinkedWorkLabel));
        }
        else
        {
            rows.Children.Add(Caption(
                $"{entry.Deliverables.Count} deliverable(s) · {entry.Contributions.Count} contributing item(s), {entry.OpenContributionCount} still open"));

            foreach (var deliverable in entry.Deliverables)
                rows.Children.Add(BuildDeliverable(entry, deliverable));

            // Work that contributes to the milestone directly, rather than
            // through one of its deliverables.
            foreach (var contribution in entry.Contributions.Where(c => !c.IsIndirect))
                rows.Children.Add(BuildContribution(contribution, indent: 1));
        }

        rows.Children.Add(BuildActions(entry));

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = rows,
            Tag = entry.ObjectId,
        };

        AutomationProperties.SetName(border, $"Milestone {entry.Identifier ?? entry.DisplayName}");
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private Control BuildDeliverable(ProjectMilestoneEntry milestone, ProjectMilestoneDeliverable deliverable)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = Indent(1) };

        rows.Children.Add(new TextBlock
        {
            Text = $"{deliverable.Identifier ?? "Deliverable"} — {deliverable.DisplayName} ({deliverable.Status})",
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeCaption,
        });

        // The work carried by this specific deliverable, so a reader can see
        // what each one is actually waiting on.
        foreach (var contribution in milestone.Contributions.Where(c => c.ViaDeliverableId == deliverable.ObjectId))
            rows.Children.Add(BuildContribution(contribution, indent: 2));

        return rows;
    }

    private static Control BuildContribution(ProjectMilestoneContribution contribution, int indent) => new TextBlock
    {
        Text = $"{contribution.Identifier ?? contribution.Kind} — {contribution.DisplayName} ({ProjectTasksView.Describe(contribution.WorkState)})",
        TextWrapping = TextWrapping.Wrap,
        FontSize = DesignTokens.FontSizeCaption,
        Opacity = contribution.IsOpen ? 0.9 : 0.6,
        Margin = Indent(indent),
    };

    private Control BuildActions(ProjectMilestoneEntry entry)
    {
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };

        var edit = new Button { Content = "Edit", MinHeight = DesignTokens.MinControlSize, Margin = ActionSpacing, Tag = entry.ObjectId };
        AutomationProperties.SetName(edit, $"Edit {entry.DisplayName}");
        edit.Click += (_, _) => EditMilestoneRequested?.Invoke(entry.ObjectId);
        actions.Children.Add(edit);

        var deliverable = new Button { Content = "Add Deliverable", MinHeight = DesignTokens.MinControlSize, Margin = ActionSpacing, Tag = entry.ObjectId };
        AutomationProperties.SetName(deliverable, $"Add a deliverable against {entry.DisplayName}");
        deliverable.Click += (_, _) => AddDeliverableRequested?.Invoke(entry.ObjectId);
        actions.Children.Add(deliverable);

        return actions;
    }

    private static Control EmptyState(string headline, string detail)
    {
        var stack = new StackPanel
        {
            Spacing = DesignTokens.SpaceSm,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, DesignTokens.SpaceXxl, 0, 0),
            MaxWidth = 460,
        };

        stack.Children.Add(new TextBlock
        {
            Text = headline,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        stack.Children.Add(new TextBlock
        {
            Text = detail,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.8,
        });

        return stack;
    }

    private static Thickness Indent(int level) => new(level * DesignTokens.SpaceMd, 0, 0, 0);

    private static Thickness ActionSpacing => new(0, DesignTokens.SpaceXs, DesignTokens.SpaceSm, 0);

    private static TextBlock Warning(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontSizeCaption,
        FontWeight = DesignTokens.WeightHeading,
        Opacity = 0.9,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock Detail(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = DesignTokens.FontSizeCaption,
        Opacity = 0.8,
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontSizeCaption,
        Opacity = 0.75,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
