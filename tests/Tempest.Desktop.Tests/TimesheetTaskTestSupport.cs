using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Tempest.Core.Deliverables;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Runbook G1: the Record dialog's own task is a drop-down of the chosen
/// project's deliverables, not free text — every journey that records time
/// through the real dialog first gives the project a deliverable, then
/// picks it here.
/// </summary>
internal static class TimesheetTaskTestSupport
{
    /// <summary>Adds a deliverable titled <paramref name="title"/> to <paramref name="projectId"/> and returns its id and the task label an entry booked against it carries.</summary>
    public static async Task<(Guid Id, string Label)> AddDeliverableAsync(WorkspaceHost host, Guid projectId, string title)
    {
        var deliverables = (IDeliverableService)host.Services!.GetService(typeof(IDeliverableService));
        var deliverable = await deliverables.AddDeliverableAsync(projectId, title);
        return (deliverable.Id, TimesheetService.DeliverableTaskLabel(deliverable));
    }

    /// <summary>The Record dialog's own task drop-down.</summary>
    public static ComboBox TaskCombo(TimesheetEntryPrompt prompt) =>
        prompt.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Task");

    /// <summary>The deliverable ids the task drop-down currently lists.</summary>
    public static IReadOnlyList<Guid> ListedDeliverables(TimesheetEntryPrompt prompt) =>
        TaskCombo(prompt).ItemsSource is { } items ? [.. items.Cast<ComboBoxItem>().Select(i => (Guid)i.Tag!)] : [];

    /// <summary>Waits until the task drop-down lists <paramref name="deliverableId"/>, then selects it.</summary>
    public static async Task SelectTaskAsync(TimesheetEntryPrompt prompt, Guid deliverableId, Func<Func<bool>, Task> renderUntil)
    {
        await renderUntil(() => ListedDeliverables(prompt).Contains(deliverableId));
        var combo = TaskCombo(prompt);
        combo.SelectedItem = combo.ItemsSource!.Cast<ComboBoxItem>().First(i => (Guid)i.Tag! == deliverableId);
    }
}
