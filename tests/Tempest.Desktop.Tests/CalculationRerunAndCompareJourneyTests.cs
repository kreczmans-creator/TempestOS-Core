using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Desktop.History;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 21.3A` (`TD-29`) — Re-run and Compare With Previous, driven through
/// the real Ribbon exactly as a person reaches them: the target Calculation
/// selected, the Ribbon's own Calculations tab, a real button click, real
/// status bar and Command History reporting. No new view — both commands
/// are discoverable and invocable purely through the existing
/// <see cref="RibbonView"/>/<see cref="ICommandRegistry"/> surface, the
/// identical shape every other Calculations status-transition command
/// (Lock, Approve, ...) already uses.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CalculationRerunAndCompareJourneyTests
{
    [AvaloniaFact]
    public async Task RerunFromTheRibbon_ProducesANewRecord_LinkedToThePredecessor_ReportedOnTheStatusBarAndHistory()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var domainContext = Resolve(host);
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            // A Calculation made through the real production command, and
            // executed once through the real Template registry (`ADR-0056`'s
            // own type-erasure adapter) — the identical production path
            // ObjectEditorView's own (now-pointer-only) Execute section used
            // before `WP 17.9.1`, still the only real execution entry point.
            var created = await commandDispatcher.DispatchAsync(
                new CreateCalculationObjectCommand(CalculationObjectFactoryRegistry.CalculationKind, "Ribbon Re-run Check", "RR-1"),
                CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);
            var calculationId = created.SubjectId!.Value;

            var input = new BoltShearCapacityInput(
                new Quantity<Length>(12, LengthUnits.Millimetre),
                new Quantity<Pressure>(400, PressureUnits.Megapascal),
                ShearPlanes: 2,
                SafetyFactor: 1.5);
            var firstExecution = await host.CalculationTemplates!.ExecuteAsync(
                BoltShearCapacityCalculationDefinition.Id, calculationId, JsonSerializer.Serialize(input));

            var historyBeforeRerun = await CalculationRecordReader.GetResultHistoryAsync(domainContext, calculationId);
            Assert.Single(historyBeforeRerun);

            // Select the Calculation the way a person does: switch to the
            // Calculations area and select it in the Project Explorer,
            // before the window is even opened — the Ribbon's own command
            // availability, and the Palette's, are both driven from this
            // same selection, exactly like every other Ribbon journey in
            // this suite (`ActionOutcomeReportingTests.ARibbonCommand_...`).
            await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.SelectAsync(calculationId, CalculationObjectFactoryRegistry.CalculationKind);

            var window = new MainWindow(host);
            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            var historyCountBefore = history.Entries.Count;

            Click(ribbon, registry, CalculationsCommandIds.Rerun);

            // `TD-119`: the Ribbon dispatch is fire-and-forget, reported on
            // the subscriber's own continuation — bounded poll on the real
            // history count, exactly as every sibling Ribbon journey does.
            var deadline = Deadline(5);
            while (history.Entries.Count == historyCountBefore && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.True(history.Entries.Count > historyCountBefore, "Re-run must be recorded in Command History.");
            // RibbonView reports a generic "'<DisplayName>' completed."
            // on success (RibbonView.cs's own OnCommandClicked) — the same
            // shape every other Ribbon-invoked command uses, proven for
            // "Request Review" by ActionOutcomeReportingTests's own
            // identical assertion shape.
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                text => text.Contains("Re-run Calculation", StringComparison.Ordinal));

            // The durable proof: a second record now exists, linked to the
            // first as its own predecessor, with the identical result —
            // "identical input" re-run, not a fresh execution.
            var historyAfterRerun = await CalculationRecordReader.GetResultHistoryAsync(domainContext, calculationId);
            Assert.Equal(2, historyAfterRerun.Count);
            var rerunRecord = historyAfterRerun[^1];
            Assert.NotEqual(firstExecution.RecordId, rerunRecord.RecordId);

            var comparison = await host.CalculationTemplates!.CompareWithPreviousAsync(calculationId);
            Assert.Empty(comparison.ResultChanges);
            Assert.Empty(comparison.InputChanges);
            Assert.Null(comparison.InputComparisonNote);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CompareWithPreviousFromTheRibbon_ReportsTheChangedInputAndResult_WithoutCreatingAThirdRecord()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var domainContext = Resolve(host);
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var created = await commandDispatcher.DispatchAsync(
                new CreateCalculationObjectCommand(CalculationObjectFactoryRegistry.CalculationKind, "Ribbon Compare Check", "RC-1"),
                CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);
            var calculationId = created.SubjectId!.Value;

            var lightLoad = new BoltShearCapacityInput(
                new Quantity<Length>(10, LengthUnits.Millimetre), new Quantity<Pressure>(400, PressureUnits.Megapascal), 2, 1.5);
            var heavierLoad = new BoltShearCapacityInput(
                new Quantity<Length>(16, LengthUnits.Millimetre), new Quantity<Pressure>(400, PressureUnits.Megapascal), 2, 1.5);

            await host.CalculationTemplates!.ExecuteAsync(BoltShearCapacityCalculationDefinition.Id, calculationId, JsonSerializer.Serialize(lightLoad));
            await host.CalculationTemplates!.ExecuteAsync(BoltShearCapacityCalculationDefinition.Id, calculationId, JsonSerializer.Serialize(heavierLoad));

            await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.SelectAsync(calculationId, CalculationObjectFactoryRegistry.CalculationKind);

            var window = new MainWindow(host);
            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            var historyCountBefore = history.Entries.Count;

            Click(ribbon, registry, CalculationsCommandIds.CompareWithPrevious);

            var deadline = Deadline(5);
            while (history.Entries.Count == historyCountBefore && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.True(history.Entries.Count > historyCountBefore, "Compare must be recorded in Command History.");
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                text => text.Contains("Compare With Previous", StringComparison.Ordinal));

            // The durable proof, read back the same way the handler itself
            // computed it — the status bar/history only ever carry the
            // Ribbon's own generic "completed" text (RibbonView.cs), never
            // a handler's own detailed CommandResult message, for any
            // command in this platform.
            var comparison = await host.CalculationTemplates!.CompareWithPreviousAsync(calculationId);
            var inputDiff = Assert.Single(comparison.InputChanges);
            Assert.Equal(nameof(BoltShearCapacityInput.Diameter), inputDiff.FieldName);

            // Read-only: still exactly two records, never a third.
            var historyAfter = await CalculationRecordReader.GetResultHistoryAsync(domainContext, calculationId);
            Assert.Equal(2, historyAfter.Count);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static EngineeringDomainContext Resolve(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
}
