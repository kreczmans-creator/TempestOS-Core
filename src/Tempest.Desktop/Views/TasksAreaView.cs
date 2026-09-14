using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Workspace.Tasks;
using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Tasks;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Tasks module (`WP 19.7A`, Product Owner IA sketches item 6 delta
/// (a)): every bucket <see cref="ITasksReadModel"/> reports — Overdue, Due
/// today, Due this week, Later, Reviews, Approvals, Finance — plus the next
/// upcoming milestones, each row opening right up, and a New task action.
/// </summary>
/// <remarks>
/// A pure view over the read model, exactly as <see cref="TimesheetWeekView"/>
/// is over <c>ITimesheetService</c>: no state of its own beyond the last
/// snapshot read, re-read on every entry and on every
/// <see cref="IWorkspaceChanges.Changed"/> (`WP 19.2B`'s "load when you
/// land here" discipline). Creating a task dispatches the already-wired
/// <see cref="CreateTaskCommand"/> (`WP 19.5C`) through
/// <see cref="ICommandDispatcher"/> — the identical shape
/// <see cref="TimesheetWeekView"/>'s own Record button already uses for a
/// create with no further prompting needed beyond the title collected
/// here.
/// </remarks>
public sealed class TasksAreaView : UserControl
{
    private readonly ITasksReadModel _readModel;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly Func<Task<string?>> _promptForTitle;
    private readonly Action<Guid, string> _openObjectRightUp;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly StackPanel _sections = new() { Spacing = DesignTokens.SpaceLg };
    private readonly Button _newTaskButton = new() { Content = "New task…", MinHeight = DesignTokens.ControlSizeMedium };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention.</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The change feed this view reloads every bucket from.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="TasksAreaView"/> class.</summary>
    /// <param name="readModel">Every bucket this view lists.</param>
    /// <param name="commandDispatcher">Dispatches <see cref="CreateTaskCommand"/> for the New task action.</param>
    /// <param name="promptForTitle">Collects a new task's own title, or <see langword="null"/> if the user cancelled.</param>
    /// <param name="openObjectRightUp">Opens a row's own source object — the same delegate every other rail area's "open right up" already uses.</param>
    public TasksAreaView(ITasksReadModel readModel, ICommandDispatcher commandDispatcher, Func<Task<string?>> promptForTitle, Action<Guid, string> openObjectRightUp)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(promptForTitle);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);

        _readModel = readModel;
        _commandDispatcher = commandDispatcher;
        _promptForTitle = promptForTitle;
        _openObjectRightUp = openObjectRightUp;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        _newTaskButton.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(_newTaskButton, "New task…");
        _newTaskButton.Click += async (_, _) => await CreateAsync().ConfigureAwait(true);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        actions.Children.Add(_newTaskButton);

        ThemeReactiveBrush.Bind(_status, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg, MaxWidth = 960, HorizontalAlignment = HorizontalAlignment.Left };
        body.Children.Add(PageHeading.Label("TASKS"));
        body.Children.Add(PageHeading.Title("Tasks"));
        body.Children.Add(PageHeading.Lead("Every open task, review, approval and finance chaser across the platform, due-dated where it has a date."));
        body.Children.Add(actions);
        body.Children.Add(_status);
        body.Children.Add(_sections);

        AutomationProperties.SetName(this, "Tasks");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Re-reads every bucket from the Tasks read model.</summary>
    public async Task RefreshAsync()
    {
        var snapshot = await _readModel.ReadAsync().ConfigureAwait(true);
        Render(snapshot);
    }

    private void Render(TasksSnapshot snapshot)
    {
        _sections.Children.Clear();

        var total = snapshot.Counts.Values.Sum();
        _status.Text = total == 0
            ? "Nothing outstanding right now."
            : $"{total} item(s) across every bucket.";

        AddBucketSection("Overdue", snapshot.OpenTasks.Where(t => t.Bucket == TaskBucket.Overdue).ToList());
        AddBucketSection("Due today", snapshot.OpenTasks.Where(t => t.Bucket == TaskBucket.DueToday).ToList());
        AddBucketSection("Due this week", snapshot.OpenTasks.Where(t => t.Bucket == TaskBucket.DueThisWeek).ToList());
        AddBucketSection("Later", snapshot.OpenTasks.Where(t => t.Bucket == TaskBucket.Later).ToList());
        AddBucketSection("Reviews", snapshot.Reviews);
        AddBucketSection("Approvals", snapshot.Approvals);
        AddBucketSection("Finance", snapshot.Finance);

        if (snapshot.UpcomingMilestones.Count > 0)
        {
            var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
            section.Children.Add(SectionHeading("Upcoming milestones"));
            foreach (var milestone in snapshot.UpcomingMilestones)
                section.Children.Add(new TextBlock { Text = $"{milestone.TargetDate:d}  —  {milestone.Title}", FontSize = DesignTokens.FontSizeBody });
            _sections.Children.Add(section);
        }
    }

    private void AddBucketSection(string title, IReadOnlyList<TaskItem> items)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
        section.Children.Add(SectionHeading($"{title} ({items.Count})"));

        if (items.Count == 0)
        {
            var empty = new TextBlock { Text = "Nothing here.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 };
            section.Children.Add(empty);
        }
        else
        {
            foreach (var item in items)
                section.Children.Add(BuildRow(item));
        }

        _sections.Children.Add(section);
    }

    private Control BuildRow(TaskItem item)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0) };

        var label = new TextBlock
        {
            Text = item.DueDate is { } due ? $"{item.Title}  ({due:d})" : item.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var kind = new TextBlock { Text = item.Kind, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(DesignTokens.SpaceMd, 0) };
        Grid.SetColumn(kind, 1);
        row.Children.Add(kind);

        var open = new Button { Content = "Open", MinHeight = DesignTokens.ControlSizeSmall };
        open.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(open, $"Open {item.Title}");
        open.Click += (_, _) => _openObjectRightUp(item.ObjectId, item.Kind);
        Grid.SetColumn(open, 2);
        row.Children.Add(open);

        return row;
    }

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text,
        FontFamily = DesignTokens.TitleFont,
        FontWeight = DesignTokens.WeightHeading,
        FontSize = DesignTokens.FontSizeBody + 2,
    };

    private async Task CreateAsync()
    {
        var title = await _promptForTitle().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(title))
            return;

        var result = await _commandDispatcher.DispatchAsync(new CreateTaskCommand(title, null, null), CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            ActionCompleted?.Invoke(result.Message ?? "The task was refused.", ActionOutcome.Failed);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        ActionCompleted?.Invoke(result.Message ?? "Task created.", ActionOutcome.From(true));

        // `WP 17.9.4`: what you make opens right up.
        if (result.SubjectId is { } createdId)
            _openObjectRightUp(createdId, ManualTask.CanonicalKind);
    }

    private void OnWorkspaceChanged(WorkspaceChange change) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Best-effort background refresh — a surface the user is
                // not currently looking at re-reads correctly the next
                // time it is entered regardless (`OnEnter` in the area
                // registry), mirroring every sibling rail view's own
                // identical "the next real entry is the backstop" shape
                // (`ReportsView.OnWorkspaceChanged`).
            }
        });
}
