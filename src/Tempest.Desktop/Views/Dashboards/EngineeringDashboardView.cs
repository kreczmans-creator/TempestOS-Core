using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Tasks;
using Tempest.Core.Commands;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Views.Dashboards;

/// <summary>
/// The Engineering dashboard (`WP 19.7B`, Product Owner comment item 6,
/// sheet 4): Calculations, Open tasks and Engineering reviews, each row
/// opening right up — the tree's own "Dashboard + Reports" node in
/// <see cref="EngineeringAreaView"/>.
/// </summary>
/// <remarks>
/// One read, on entry and on <see cref="Tempest.Core.Events.IWorkspaceChanges"/>
/// (via the parent <see cref="EngineeringAreaView"/>'s own subscription):
/// <see cref="ITasksReadModel"/>. <see cref="TasksSnapshot.Calculations"/>
/// (`WP 20.1B`, `TD-181`) is its own section, each row also offering
/// Complete — <see cref="TasksSnapshot.OpenTasks"/> stays deliverables,
/// milestones and manual tasks only, exactly as it always has.
/// </remarks>
public sealed class EngineeringDashboardView : UserControl
{
    private readonly ITasksReadModel _readModel;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly Action<Guid, string> _openObjectRightUp;

    private readonly StackPanel _calculationsList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _openTasksList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _reviewsList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _approvalsList = new() { Spacing = DesignTokens.SpaceXs };

    /// <summary>Raised after Complete completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="EngineeringDashboardView"/> class.</summary>
    public EngineeringDashboardView(ITasksReadModel readModel, ICommandDispatcher commandDispatcher, Action<Guid, string> openObjectRightUp)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);

        _readModel = readModel;
        _commandDispatcher = commandDispatcher;
        _openObjectRightUp = openObjectRightUp;

        var page = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceXl, MaxWidth = 900 };
        page.Children.Add(PageHeading.Label("ENGINEERING"));
        page.Children.Add(PageHeading.Title("Dashboard"));
        page.Children.Add(Section("Calculations", _calculationsList));
        page.Children.Add(Section("Open tasks", _openTasksList));
        page.Children.Add(Section("Engineering reviews — awaiting check", _reviewsList));
        page.Children.Add(Section("Engineering reviews — awaiting issue", _approvalsList));

        AutomationProperties.SetName(this, "Engineering dashboard");
        Content = new ScrollViewer { Content = page };
    }

    /// <summary>Re-reads <see cref="ITasksReadModel"/> and rebuilds every region.</summary>
    public async Task RefreshAsync()
    {
        var snapshot = await _readModel.ReadAsync().ConfigureAwait(true);

        RenderCalculationsList(snapshot.Calculations);
        RenderList(_openTasksList, snapshot.OpenTasks, "No open tasks.");
        RenderList(_reviewsList, snapshot.Reviews, "Nothing awaiting check.");
        RenderList(_approvalsList, snapshot.Approvals, "Nothing awaiting issue.");
    }

    /// <summary>`WP 20.1B` (`TD-181`): each Calculation row opens right up and offers Complete.</summary>
    private void RenderCalculationsList(IReadOnlyList<TaskItem> items)
    {
        _calculationsList.Children.Clear();

        if (items.Count == 0)
        {
            _calculationsList.Children.Add(new TextBlock { Text = "No open calculations.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
            return;
        }

        foreach (var item in items)
            _calculationsList.Children.Add(CalculationRow(item));
    }

    private Control CalculationRow(TaskItem item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var label = new TextBlock
        {
            Text = item.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var open = new Button { Content = "Open", MinHeight = DesignTokens.ControlSizeSmall };
        open.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(open, $"Open {item.Title}");
        open.Click += (_, _) => _openObjectRightUp(item.ObjectId, item.Kind);
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);

        var complete = new Button { Content = "Complete", MinHeight = DesignTokens.ControlSizeSmall };
        complete.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(complete, $"Complete {item.Title}");
        complete.Click += async (_, _) => await OnCompleteAsync(item).ConfigureAwait(true);
        Grid.SetColumn(complete, 2);
        grid.Children.Add(complete);

        return grid;
    }

    private async Task OnCompleteAsync(TaskItem item)
    {
        var result = await _commandDispatcher
            .DispatchAsync(new CompleteCalculationCommand(item.ObjectId, item.Kind), CancellationToken.None)
            .ConfigureAwait(true);

        ActionCompleted?.Invoke(result.Message ?? "Complete failed.", ActionOutcome.From(result.Succeeded));

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
    }

    private void RenderList(StackPanel host, IReadOnlyList<TaskItem> items, string emptyText)
    {
        host.Children.Clear();

        if (items.Count == 0)
        {
            host.Children.Add(new TextBlock { Text = emptyText, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
            return;
        }

        foreach (var item in items)
            host.Children.Add(Row(item));
    }

    private Control Row(TaskItem item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var label = new TextBlock
        {
            Text = item.DueDate is { } due ? $"{item.Title}  ({due:d})" : item.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var kind = new TextBlock { Text = item.Kind, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(DesignTokens.SpaceMd, 0) };
        Grid.SetColumn(kind, 1);
        grid.Children.Add(kind);

        var open = new Button { Content = "Open", MinHeight = DesignTokens.ControlSizeSmall };
        open.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(open, $"Open {item.Title}");
        open.Click += (_, _) => _openObjectRightUp(item.ObjectId, item.Kind);
        Grid.SetColumn(open, 2);
        grid.Children.Add(open);

        return grid;
    }

    private static StackPanel Section(string title, Control content)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
        var heading = new TextBlock { Text = title, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2 };
        ThemeReactiveBrush.Bind(heading, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        section.Children.Add(heading);
        section.Children.Add(content);
        return section;
    }
}
