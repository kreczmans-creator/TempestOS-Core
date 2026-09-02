using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Projects;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Project Workspace's Tasks area — the project's own real tasks and
/// actions, as a list or a status board.
/// </summary>
/// <remarks>
/// <para>
/// <b>A view, not a decision-maker.</b> Everything shown comes from
/// <see cref="IProjectTaskRegister"/>, and every action the user takes is
/// raised as an event for the shell to perform through
/// <see cref="IProjectTaskService"/>. This class does not touch the domain,
/// does not decide what a permitted status change is, and holds no task
/// state of its own — the same discipline
/// <see cref="ProjectRequirementsView"/> follows, for the same reason: a
/// surface that decides things is a surface that can disagree with the
/// domain.
/// </para>
/// <para>
/// <b>An ordinary surface (`TD-72`).</b> It is a plain
/// <see cref="UserControl"/> rendered into the project workspace's own tab
/// host, exactly like Documents and Requirements. There is no task-specific
/// panel slot, no reserved region and no window of its own.
/// </para>
/// </remarks>
public sealed class ProjectTasksView : UserControl
{
    /// <summary>The heading shown above the register.</summary>
    public const string Heading = "Tasks";

    /// <summary>What the surface says when the project has no tasks at all.</summary>
    public const string EmptyHeadline = "No tasks in this project";

    /// <summary>The note shown against a task that is past its due date and still open.</summary>
    public const string OverdueNote = "Overdue";

    /// <summary>What a task with no assignee reads as.</summary>
    public const string UnassignedLabel = "Unassigned";

    private readonly StackPanel _list = new() { Spacing = DesignTokens.SpaceSm };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly Button _newTask = new() { Content = "New Task", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _toggleView = new() { MinHeight = DesignTokens.MinControlSize };

    /// <summary>Raised when the user asks to create a task.</summary>
    public event Action? CreateRequested;

    /// <summary>Raised when the user asks to assign a task to themselves.</summary>
    public event Action<Guid>? AssignToMeRequested;

    /// <summary>Raised when the user asks to move a task to a work state.</summary>
    public event Action<Guid, TaskWorkState>? WorkStateChangeRequested;

    /// <summary>Raised when the user asks to edit a task.</summary>
    public event Action<Guid>? EditRequested;

    /// <summary>Raised when the user asks to set or change a task's due date.</summary>
    public event Action<Guid>? DueDateChangeRequested;

    /// <summary>Initialises a new instance of the <see cref="ProjectTasksView"/> class.</summary>
    public ProjectTasksView()
    {
        var heading = new TextBlock
        {
            Text = Heading,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        AutomationProperties.SetName(_newTask, "Create a task in this project");
        _newTask.Click += (_, _) => CreateRequested?.Invoke();

        _toggleView.Click += (_, _) =>
        {
            IsShowingBoard = !IsShowingBoard;
            Render();
        };

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(_newTask);
        actions.Children.Add(_toggleView);

        var root = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelPadding };
        root.Children.Add(heading);
        root.Children.Add(_summary);
        root.Children.Add(actions);
        root.Children.Add(_list);

        AutomationProperties.SetName(this, Heading);
        Content = new ScrollViewer { Content = root };

        UpdateToggleCaption();
    }

    /// <summary>The tasks currently on screen, in the order the register returned them.</summary>
    public IReadOnlyList<ProjectTaskEntry> Entries { get; private set; } = [];

    /// <summary>The board columns currently on screen.</summary>
    public IReadOnlyList<ProjectTaskBoardColumn> Board { get; private set; } = [];

    /// <summary>Whether the board is showing rather than the list.</summary>
    public bool IsShowingBoard { get; private set; }

    /// <summary>Whether the surface is showing its empty state.</summary>
    public bool IsShowingEmptyState { get; private set; } = true;

    /// <summary>The summary line, exactly as a user reads it.</summary>
    public string SummaryText => _summary.Text ?? string.Empty;

    private string? _projectLabel;

    /// <summary>Renders <paramref name="entries"/> and <paramref name="board"/> for the project named <paramref name="projectLabel"/>.</summary>
    public void Show(
        IReadOnlyList<ProjectTaskEntry> entries,
        IReadOnlyList<ProjectTaskBoardColumn> board,
        string? projectLabel)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(board);

        Entries = entries;
        Board = board;
        _projectLabel = projectLabel;

        Render();
    }

    /// <summary>How a work state reads to a user.</summary>
    public static string Describe(TaskWorkState state) => TaskWorkStates.For(state).Name;

    private void Render()
    {
        _list.Children.Clear();
        UpdateToggleCaption();

        var project = string.IsNullOrWhiteSpace(_projectLabel) ? "this project" : _projectLabel;

        IsShowingEmptyState = Entries.Count == 0;
        if (IsShowingEmptyState)
        {
            _summary.Text = $"No tasks in {project} yet.";
            _list.Children.Add(EmptyState(
                EmptyHeadline,
                "A task belongs to a project once it sits under something the project owns. Create one here and it will appear in this register, on the board, and in the Engineering Workspace."));
            return;
        }

        var open = Entries.Count(e => e.IsOpen);
        var overdue = Entries.Count(e => e.IsOverdue);
        var unassigned = Entries.Count(e => e.IsUnassigned);

        _summary.Text =
            $"{Entries.Count} task(s) in {project} — {open} open, {overdue} overdue, {unassigned} unassigned.";

        if (IsShowingBoard)
        {
            _list.Children.Add(BuildBoard());
            return;
        }

        foreach (var entry in Entries)
            _list.Children.Add(BuildEntry(entry));
    }

    private void UpdateToggleCaption()
    {
        _toggleView.Content = IsShowingBoard ? "View as list" : "View as board";
        AutomationProperties.SetName(_toggleView, IsShowingBoard ? "Show tasks as a list" : "Show tasks as a status board");
    }

    private Control BuildBoard()
    {
        var columns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };

        foreach (var column in Board)
        {
            var stack = new StackPanel { Spacing = DesignTokens.SpaceSm, Width = 260 };

            stack.Children.Add(new TextBlock
            {
                Text = $"{column.Title} ({column.Entries.Count})",
                FontWeight = DesignTokens.WeightHeading,
            });

            // An empty column still says so. A board that drops "Blocked"
            // when nothing is blocked reshapes itself under the user as
            // work moves, which makes it unreadable at a glance.
            if (column.Entries.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "Nothing here.",
                    FontSize = DesignTokens.FontSizeCaption,
                    Opacity = 0.7,
                });
            }

            foreach (var entry in column.Entries)
                stack.Children.Add(BuildEntry(entry, compact: true));

            var border = new Border
            {
                Padding = DesignTokens.PanelPadding,
                CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
                BorderThickness = new Thickness(1),
                Child = stack,
                Tag = column.State,
            };

            AutomationProperties.SetName(border, $"{column.Title} column");
            ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
            columns.Children.Add(border);
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = columns,
        };
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

    private Control BuildEntry(ProjectTaskEntry entry, bool compact = false)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        header.Children.Add(new TextBlock
        {
            Text = entry.Identifier ?? entry.Kind,
            FontWeight = DesignTokens.WeightHeading,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(Caption(Describe(entry.WorkState)));
        header.Children.Add(Caption($"Priority: {entry.Priority}"));
        rows.Children.Add(header);

        rows.Children.Add(new TextBlock
        {
            Text = entry.DisplayName,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });

        if (!compact && !string.IsNullOrWhiteSpace(entry.Description))
        {
            rows.Children.Add(new TextBlock
            {
                Text = entry.Description,
                TextWrapping = TextWrapping.Wrap,
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.8,
            });
        }

        var owner = entry.AssignedToPrincipalId ?? UnassignedLabel;
        var due = entry.DueDate is { } dueDate ? dueDate.ToString("yyyy-MM-dd") : "No due date";
        rows.Children.Add(Caption($"{owner} · {due}"));

        if (entry.ContributesTo is { } target)
            rows.Children.Add(Caption($"Contributes to {target.Kind} “{target.DisplayName}”"));

        if (entry.IsOverdue)
        {
            rows.Children.Add(new TextBlock
            {
                Text = OverdueNote,
                FontSize = DesignTokens.FontSizeCaption,
                FontWeight = DesignTokens.WeightHeading,
                Opacity = 0.9,
            });
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

        AutomationProperties.SetName(border, $"Task {entry.Identifier ?? entry.DisplayName}");
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private Control BuildActions(ProjectTaskEntry entry)
    {
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };

        var edit = new Button { Content = "Edit", MinHeight = DesignTokens.MinControlSize, Margin = ActionSpacing, Tag = entry.ObjectId };
        AutomationProperties.SetName(edit, $"Edit {entry.DisplayName}");
        edit.Click += (_, _) => EditRequested?.Invoke(entry.ObjectId);
        actions.Children.Add(edit);

        if (entry.IsUnassigned)
        {
            var assign = new Button { Content = "Assign to me", MinHeight = DesignTokens.MinControlSize, Margin = ActionSpacing, Tag = entry.ObjectId };
            AutomationProperties.SetName(assign, $"Assign {entry.DisplayName} to me");
            assign.Click += (_, _) => AssignToMeRequested?.Invoke(entry.ObjectId);
            actions.Children.Add(assign);
        }

        var due = new Button { Content = "Due date", MinHeight = DesignTokens.MinControlSize, Margin = ActionSpacing, Tag = entry.ObjectId };
        AutomationProperties.SetName(due, $"Set the due date for {entry.DisplayName}");
        due.Click += (_, _) => DueDateChangeRequested?.Invoke(entry.ObjectId);
        actions.Children.Add(due);

        // Only the moves the domain actually permits from here get a
        // button. Offering "Done" on a task that is already done, or a
        // move the transition table refuses, would put the user in front
        // of a control whose only outcome is an error.
        foreach (var target in TaskWorkStateTransitions.GetPermittedTargets(entry.WorkState))
        {
            var move = new Button
            {
                Content = Describe(target),
                MinHeight = DesignTokens.MinControlSize,
                Margin = ActionSpacing,
                Tag = target,
            };

            AutomationProperties.SetName(move, $"Move {entry.DisplayName} to {Describe(target)}");
            move.Click += (_, _) => WorkStateChangeRequested?.Invoke(entry.ObjectId, target);
            actions.Children.Add(move);
        }

        return actions;
    }

    private static Thickness ActionSpacing => new(0, DesignTokens.SpaceXs, DesignTokens.SpaceSm, 0);

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontSizeCaption,
        Opacity = 0.75,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
