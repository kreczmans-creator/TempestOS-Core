using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Projects;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Which of the three governance registers the Risks area is showing.</summary>
public enum GovernanceRegisterTab
{
    /// <summary>Risks and hazards.</summary>
    Risks,

    /// <summary>Issues.</summary>
    Issues,

    /// <summary>Decisions.</summary>
    Decisions,
}

/// <summary>
/// The Project Workspace's Risks area — the project's own risks, issues and
/// decisions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three registers, one area, because that is what the area declares.</b>
/// <c>ProjectAreas</c> has always described this tab as "Risks, issues and
/// decisions for this project". Splitting them into three sibling tabs would
/// contradict the declaration and push two of the three off the tab strip;
/// they are shown as three switchable registers inside the one area instead.
/// </para>
/// <para>
/// <b>A view, not a decision-maker.</b> Everything shown comes from
/// <see cref="IProjectGovernanceRegister"/>, and every action the user takes
/// is raised as an event for the shell to perform through
/// <see cref="IProjectGovernanceService"/>. This class does not touch the
/// domain, does not decide what a permitted status change is, and holds no
/// governance state of its own — the same discipline
/// <see cref="ProjectTasksView"/> follows, for the same reason: a surface
/// that decides things is a surface that can disagree with the domain.
/// </para>
/// <para>
/// <b>An ordinary surface (`TD-72`).</b> A plain <see cref="UserControl"/>
/// rendered into the project workspace's own tab host, exactly like
/// Documents, Requirements and Tasks. No reserved region, no window of its
/// own.
/// </para>
/// </remarks>
public sealed class ProjectRisksView : UserControl
{
    /// <summary>The heading shown above the registers.</summary>
    public const string Heading = "Risks, Issues & Decisions";

    /// <summary>What the surface says when the project has no risks at all.</summary>
    public const string EmptyRisksHeadline = "No risks in this project";

    /// <summary>What the surface says when the project has no issues at all.</summary>
    public const string EmptyIssuesHeadline = "No issues in this project";

    /// <summary>What the surface says when the project has no decisions at all.</summary>
    public const string EmptyDecisionsHeadline = "No decisions in this project";

    /// <summary>What a risk with no owner reads as.</summary>
    public const string UnownedLabel = "Unowned";

    /// <summary>What an issue with no assignee reads as.</summary>
    public const string UnassignedLabel = "Unassigned";

    /// <summary>What an unscored risk reads as.</summary>
    public const string UnscoredLabel = "Unscored";

    private readonly StackPanel _list = new() { Spacing = DesignTokens.SpaceSm };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly Button _create = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

    private string? _projectLabel;

    /// <summary>Raised when the user asks to raise a risk.</summary>
    public event Action? CreateRiskRequested;

    /// <summary>Raised when the user asks to raise an issue.</summary>
    public event Action? CreateIssueRequested;

    /// <summary>Raised when the user asks to propose a decision.</summary>
    public event Action? CreateDecisionRequested;

    /// <summary>Raised when the user asks to move a risk to a status.</summary>
    public event Action<Guid, RiskStatus>? RiskStatusChangeRequested;

    /// <summary>Raised when the user asks to move an issue to a status.</summary>
    public event Action<Guid, IssueStatus>? IssueStatusChangeRequested;

    /// <summary>Raised when the user asks to move a decision to a status.</summary>
    public event Action<Guid, DecisionStatus>? DecisionStatusChangeRequested;

    /// <summary>Raised when the user asks to own a risk themselves.</summary>
    public event Action<Guid>? OwnRiskRequested;

    /// <summary>Raised when the user asks to assign an issue to themselves.</summary>
    public event Action<Guid>? AssignIssueToMeRequested;

    /// <summary>Raised when the user asks to score a risk.</summary>
    public event Action<Guid>? ScoreRiskRequested;

    /// <summary>Raised when the user asks to edit a risk.</summary>
    public event Action<Guid>? EditRiskRequested;

    /// <summary>Raised when the user asks to edit an issue.</summary>
    public event Action<Guid>? EditIssueRequested;

    /// <summary>Raised when the user asks to edit a decision.</summary>
    public event Action<Guid>? EditDecisionRequested;

    /// <summary>Initialises a new instance of the <see cref="ProjectRisksView"/> class.</summary>
    public ProjectRisksView()
    {
        var heading = new TextBlock
        {
            Text = Heading,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        // `WP-Z4` Productisation Phase 1 (P1) — the one primary
        // call-to-action this area offers (Raise Risk/Raise Issue/Propose
        // Decision, whichever register is selected) now carries the same
        // accent-filled treatment every other primary CTA in the shell
        // uses, instead of rendering identically to every secondary
        // action button on the page.
        _create.Classes.Add(ChromeStyles.Primary);

        _create.Click += (_, _) =>
        {
            switch (SelectedTab)
            {
                case GovernanceRegisterTab.Risks:
                    CreateRiskRequested?.Invoke();
                    break;
                case GovernanceRegisterTab.Issues:
                    CreateIssueRequested?.Invoke();
                    break;
                default:
                    CreateDecisionRequested?.Invoke();
                    break;
            }
        };

        foreach (var tab in Enum.GetValues<GovernanceRegisterTab>())
            _tabs.Children.Add(BuildTabButton(tab));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(_create);

        var root = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelPadding };
        root.Children.Add(heading);
        root.Children.Add(_tabs);
        root.Children.Add(_summary);
        root.Children.Add(actions);
        root.Children.Add(_list);

        AutomationProperties.SetName(this, Heading);
        Content = new ScrollViewer { Content = root };

        Render();
    }

    /// <summary>The risks currently loaded, in the order the register returned them.</summary>
    public IReadOnlyList<ProjectRiskEntry> Risks { get; private set; } = [];

    /// <summary>The issues currently loaded.</summary>
    public IReadOnlyList<ProjectIssueEntry> Issues { get; private set; } = [];

    /// <summary>The decisions currently loaded.</summary>
    public IReadOnlyList<ProjectDecisionEntry> Decisions { get; private set; } = [];

    /// <summary>Which register is on screen.</summary>
    public GovernanceRegisterTab SelectedTab { get; private set; } = GovernanceRegisterTab.Risks;

    /// <summary>Whether the surface is showing an empty state for the selected register.</summary>
    public bool IsShowingEmptyState { get; private set; } = true;

    /// <summary>The summary line, exactly as a user reads it.</summary>
    public string SummaryText => _summary.Text ?? string.Empty;

    /// <summary>Renders the three registers for the project named <paramref name="projectLabel"/>.</summary>
    public void Show(
        IReadOnlyList<ProjectRiskEntry> risks,
        IReadOnlyList<ProjectIssueEntry> issues,
        IReadOnlyList<ProjectDecisionEntry> decisions,
        string? projectLabel)
    {
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(decisions);

        Risks = risks;
        Issues = issues;
        Decisions = decisions;
        _projectLabel = projectLabel;

        Render();
    }

    /// <summary>Switches the surface to <paramref name="tab"/>.</summary>
    public void SelectTab(GovernanceRegisterTab tab)
    {
        SelectedTab = tab;
        Render();
    }

    /// <summary>How a risk status reads to a user.</summary>
    public static string Describe(RiskStatus status) => RiskStatuses.For(status).Name;

    /// <summary>How an issue status reads to a user.</summary>
    public static string Describe(IssueStatus status) => IssueStatuses.For(status).Name;

    /// <summary>How a decision status reads to a user.</summary>
    public static string Describe(DecisionStatus status) => DecisionStatuses.For(status).Name;

    private Button BuildTabButton(GovernanceRegisterTab tab)
    {
        var button = new Button { Content = tab.ToString(), MinHeight = DesignTokens.MinControlSize, Tag = tab };

        AutomationProperties.SetName(button, $"Show {tab}");
        button.Click += (_, _) => SelectTab(tab);
        return button;
    }

    private void Render()
    {
        _list.Children.Clear();

        var project = string.IsNullOrWhiteSpace(_projectLabel) ? "this project" : _projectLabel;

        // Every tab button says how many are behind it, so a user can see
        // there are issues without switching to find out.
        foreach (var child in _tabs.Children)
        {
            if (child is Button { Tag: GovernanceRegisterTab tab } button)
                button.Content = $"{tab} ({CountFor(tab)})";
        }

        _create.Content = SelectedTab switch
        {
            GovernanceRegisterTab.Risks => "Raise Risk",
            GovernanceRegisterTab.Issues => "Raise Issue",
            _ => "Propose Decision",
        };
        AutomationProperties.SetName(_create, $"{_create.Content} in this project");

        switch (SelectedTab)
        {
            case GovernanceRegisterTab.Risks:
                RenderRisks(project);
                break;
            case GovernanceRegisterTab.Issues:
                RenderIssues(project);
                break;
            default:
                RenderDecisions(project);
                break;
        }
    }

    private int CountFor(GovernanceRegisterTab tab) => tab switch
    {
        GovernanceRegisterTab.Risks => Risks.Count,
        GovernanceRegisterTab.Issues => Issues.Count,
        _ => Decisions.Count,
    };

    private void RenderRisks(string project)
    {
        IsShowingEmptyState = Risks.Count == 0;
        if (IsShowingEmptyState)
        {
            _summary.Text = $"No risks in {project} yet.";
            _list.Children.Add(EmptyState(
                EmptyRisksHeadline,
                "A risk belongs to a project once it sits under something the project owns. Raise one here and it will appear in this register and in the Engineering Workspace."));
            return;
        }

        var live = Risks.Count(r => r.IsLive);
        var unowned = Risks.Count(r => r.IsUnowned);
        var unscored = Risks.Count(r => !r.IsScored);

        _summary.Text =
            $"{Risks.Count} risk(s) in {project} — {live} live, {unowned} unowned, {unscored} unscored.";

        foreach (var entry in Risks)
            _list.Children.Add(BuildRisk(entry));
    }

    private void RenderIssues(string project)
    {
        IsShowingEmptyState = Issues.Count == 0;
        if (IsShowingEmptyState)
        {
            _summary.Text = $"No issues in {project} yet.";
            _list.Children.Add(EmptyState(
                EmptyIssuesHeadline,
                "An issue belongs to a project once it sits under something the project owns. Raise one here, or record that a risk materialised, and it will appear in this register."));
            return;
        }

        var open = Issues.Count(i => i.IsOpen);
        var unassigned = Issues.Count(i => i.IsUnassigned);

        _summary.Text = $"{Issues.Count} issue(s) in {project} — {open} open, {unassigned} unassigned.";

        foreach (var entry in Issues)
            _list.Children.Add(BuildIssue(entry));
    }

    private void RenderDecisions(string project)
    {
        IsShowingEmptyState = Decisions.Count == 0;
        if (IsShowingEmptyState)
        {
            _summary.Text = $"No decisions in {project} yet.";
            _list.Children.Add(EmptyState(
                EmptyDecisionsHeadline,
                "A decision belongs to a project once it sits under something the project owns. Propose one here and the project keeps a record of what was decided, by whom, and why."));
            return;
        }

        var awaiting = Decisions.Count(d => d.IsAwaitingDecision);
        var inForce = Decisions.Count(d => d.IsInForce);

        _summary.Text =
            $"{Decisions.Count} decision(s) in {project} — {awaiting} awaiting a decision, {inForce} in force.";

        foreach (var entry in Decisions)
            _list.Children.Add(BuildDecision(entry));
    }

    private Control BuildRisk(ProjectRiskEntry entry)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        header.Children.Add(new TextBlock
        {
            Text = entry.Identifier ?? entry.Kind,
            FontWeight = DesignTokens.WeightHeading,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(Caption(Describe(entry.Status)));
        header.Children.Add(Caption(entry.Kind));
        rows.Children.Add(header);

        rows.Children.Add(Body(entry.DisplayName));

        if (!string.IsNullOrWhiteSpace(entry.Description))
            rows.Children.Add(Detail(entry.Description));

        // Both axes read together, and an unscored risk says so rather than
        // showing two blanks a reader has to interpret.
        var score = entry.IsScored
            ? $"Likelihood {entry.Likelihood} · Severity {entry.Severity}"
            : UnscoredLabel;
        rows.Children.Add(Caption($"{entry.OwnedByPrincipalId ?? UnownedLabel} · {score}"));

        if (entry.RealisedAsIssueId is not null)
            rows.Children.Add(Caption("Materialised — an issue was raised from this risk"));

        rows.Children.Add(BuildRiskActions(entry));

        return Card(rows, entry.ObjectId, $"Risk {entry.Identifier ?? entry.DisplayName}");
    }

    private Control BuildIssue(ProjectIssueEntry entry)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        header.Children.Add(new TextBlock
        {
            Text = entry.Identifier ?? "Issue",
            FontWeight = DesignTokens.WeightHeading,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(Caption(Describe(entry.Status)));
        header.Children.Add(Caption($"Priority: {entry.Priority}"));
        rows.Children.Add(header);

        rows.Children.Add(Body(entry.DisplayName));

        if (!string.IsNullOrWhiteSpace(entry.Description))
            rows.Children.Add(Detail(entry.Description));

        rows.Children.Add(Caption(entry.AssignedToPrincipalId ?? UnassignedLabel));

        if (entry.RaisedByRiskId is not null)
            rows.Children.Add(Caption("Raised from a risk that materialised"));

        rows.Children.Add(BuildIssueActions(entry));

        return Card(rows, entry.ObjectId, $"Issue {entry.Identifier ?? entry.DisplayName}");
    }

    private Control BuildDecision(ProjectDecisionEntry entry)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        header.Children.Add(new TextBlock
        {
            Text = entry.Identifier ?? "Decision",
            FontWeight = DesignTokens.WeightHeading,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(Caption(Describe(entry.Status)));
        rows.Children.Add(header);

        rows.Children.Add(Body(entry.DisplayName));

        // The rationale is the point of recording a decision, so it is
        // shown on the card rather than hidden behind an edit dialog.
        if (!string.IsNullOrWhiteSpace(entry.Rationale))
            rows.Children.Add(Detail($"Rationale: {entry.Rationale}"));

        var decided = entry.DecidedAt is { } at
            ? $"{entry.DecidedByPrincipalId ?? "Decided"} · {at:yyyy-MM-dd}"
            : "Not yet decided";
        rows.Children.Add(Caption(decided));

        if (entry.AddressesObjectIds.Count > 0)
            rows.Children.Add(Caption($"Addresses {entry.AddressesObjectIds.Count} item(s)"));

        rows.Children.Add(BuildDecisionActions(entry));

        return Card(rows, entry.ObjectId, $"Decision {entry.Identifier ?? entry.DisplayName}");
    }

    private Control BuildRiskActions(ProjectRiskEntry entry)
    {
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };

        actions.Children.Add(ActionButton("Edit", $"Edit {entry.DisplayName}", entry.ObjectId,
            () => EditRiskRequested?.Invoke(entry.ObjectId)));

        actions.Children.Add(ActionButton("Score", $"Score {entry.DisplayName}", entry.ObjectId,
            () => ScoreRiskRequested?.Invoke(entry.ObjectId)));

        if (entry.IsUnowned)
        {
            actions.Children.Add(ActionButton("Own this", $"Take ownership of {entry.DisplayName}", entry.ObjectId,
                () => OwnRiskRequested?.Invoke(entry.ObjectId)));
        }

        // Only the moves the domain actually permits from here get a
        // button, so the surface can never offer a transition the
        // transition table would refuse.
        foreach (var target in RiskStatusTransitions.GetPermittedTargets(entry.Status))
        {
            actions.Children.Add(ActionButton(Describe(target), $"Move {entry.DisplayName} to {Describe(target)}", entry.ObjectId,
                () => RiskStatusChangeRequested?.Invoke(entry.ObjectId, target)));
        }

        return actions;
    }

    private Control BuildIssueActions(ProjectIssueEntry entry)
    {
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };

        actions.Children.Add(ActionButton("Edit", $"Edit {entry.DisplayName}", entry.ObjectId,
            () => EditIssueRequested?.Invoke(entry.ObjectId)));

        if (entry.IsUnassigned)
        {
            actions.Children.Add(ActionButton("Assign to me", $"Assign {entry.DisplayName} to me", entry.ObjectId,
                () => AssignIssueToMeRequested?.Invoke(entry.ObjectId)));
        }

        foreach (var target in IssueStatusTransitions.GetPermittedTargets(entry.Status))
        {
            actions.Children.Add(ActionButton(Describe(target), $"Move {entry.DisplayName} to {Describe(target)}", entry.ObjectId,
                () => IssueStatusChangeRequested?.Invoke(entry.ObjectId, target)));
        }

        return actions;
    }

    private Control BuildDecisionActions(ProjectDecisionEntry entry)
    {
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };

        actions.Children.Add(ActionButton("Edit", $"Edit {entry.DisplayName}", entry.ObjectId,
            () => EditDecisionRequested?.Invoke(entry.ObjectId)));

        // A superseded decision has no permitted targets, so it correctly
        // offers nothing but Edit — the record stands.
        foreach (var target in DecisionStatusTransitions.GetPermittedTargets(entry.Status))
        {
            actions.Children.Add(ActionButton(Describe(target), $"Move {entry.DisplayName} to {Describe(target)}", entry.ObjectId,
                () => DecisionStatusChangeRequested?.Invoke(entry.ObjectId, target)));
        }

        return actions;
    }

    private static Button ActionButton(string caption, string automationName, Guid objectId, Action onClick)
    {
        var button = new Button
        {
            Content = caption,
            MinHeight = DesignTokens.MinControlSize,
            Margin = ActionSpacing,
            Tag = objectId,
        };

        AutomationProperties.SetName(button, automationName);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Control Card(Control content, Guid objectId, string automationName)
    {
        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = content,
            Tag = objectId,
        };

        AutomationProperties.SetName(border, automationName);
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
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

    private static Thickness ActionSpacing => new(0, DesignTokens.SpaceXs, DesignTokens.SpaceSm, 0);

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = DesignTokens.FontSizeBody,
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
