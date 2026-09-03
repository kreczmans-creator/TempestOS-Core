using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Tempest.Core.Diagnostics;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Output panel's module/hosted-service rows (`WP-Z4` Productisation
/// Phase 1, P1) — each row must actually carry its severity's colour, not
/// merely a monochrome glyph indistinguishable from every other state at a
/// glance.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class OutputPanelViewTests
{
    [AvaloniaFact]
    public async Task Refresh_BuildsColouredRows_NotPlainStrings()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var diagnostics = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
            var modules = diagnostics.Modules.ToList();

            var panel = new OutputPanelView();
            panel.Refresh(diagnostics);

            var modulesField = typeof(OutputPanelView).GetField("_modules", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var modulesControl = (ItemsControl)modulesField.GetValue(panel)!;
            var rows = modulesControl.ItemsSource!.Cast<Control>().ToList();

            Assert.NotEmpty(rows);
            Assert.Equal(modules.Count, rows.Count);

            // A Running module's row must actually carry the Success
            // colour somewhere in its visual tree — not the same
            // undyed foreground every other state would also render with.
            var index = modules.FindIndex(m => m.State == Tempest.Core.Modules.ModuleState.Running);
            if (index >= 0)
            {
                var textBlocks = rows[index].GetVisualDescendants().OfType<TextBlock>().ToList();
                Assert.Contains(textBlocks, t => Equals(t.Foreground, SeverityColors.Resolve(FeedbackSeverity.Success)));
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
