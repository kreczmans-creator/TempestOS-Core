using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates Command Palette Opening (`WP 10.0B`'s own "Demonstrate"
/// list) against a real <see cref="ICommandRegistry"/> resolved from a
/// running <see cref="WorkspaceHost"/> — the same registry every real
/// discipline's own commands (`mechanical.create`, `requirements.revise`,
/// etc.) are already registered against, unchanged (`ADR-0070`).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CommandPaletteOverlayTests
{
    [AvaloniaFact]
    public async Task Open_ShowsTheOverlay_PopulatedFromTheRealCommandRegistry()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            Assert.True(registry.Items.Count > 0, "Expected real commands already registered by the six Engineering Disciplines.");

            var palette = new CommandPaletteOverlay(registry);
            Assert.False(palette.IsOpen);

            palette.Open();
            Assert.True(palette.IsOpen);

            palette.Close();
            Assert.False(palette.IsOpen);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ConfirmingACommandWithNoCreateDefault_RaisesCommandUnavailable_NotASilentNoOp()
    {
        // WP 10.3B's own genuine, disclosed defect fix (class remarks):
        // every real discipline command has CreateDefault == null, so
        // pressing Enter on one previously closed the palette and did
        // nothing else, with zero feedback. Confirmed here directly.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            Assert.Contains(registry.Items, d => d.CreateDefault is null); // the real, confirmed precondition

            var palette = new CommandPaletteOverlay(registry);
            palette.Open();

            CommandDescriptor? unavailable = null;
            string? unavailableReason = null;
            CommandDescriptor? invoked = null;
            palette.CommandUnavailable += (d, r) => { unavailable = d; unavailableReason = r; };
            palette.CommandInvoked += (d, _) => invoked = d;

            // OnQueryKeyDown is wired to the palette's own inner query
            // TextBox (its own first child), not the palette Border
            // itself — found directly rather than exposing a test-only
            // accessor.
            var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.NotNull(unavailable);
            Assert.Null(invoked);
            Assert.False(palette.IsOpen); // still closes, exactly as a real invocation would
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 18.1B` §2: a non-empty query, with <see cref="CommandPaletteOverlay.ObjectSearchSource"/>
    /// wired, appends an Objects section once the background search
    /// returns; selecting that row raises <see cref="CommandPaletteOverlay.ObjectSelected"/>
    /// instead of trying to invoke a command.
    /// </summary>
    [AvaloniaFact]
    public async Task TypingAQuery_WithAnObjectSearchSourceWired_ShowsAnObjectsRow_AndSelectingItRaisesObjectSelected()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var palette = new CommandPaletteOverlay(registry);
            var hitId = Guid.NewGuid();
            var hit = new PaletteObjectHit(hitId, "Part", "Bracket Mounting Plate", "Demo Project");
            palette.ObjectSearchSource = (_, _) => Task.FromResult<IReadOnlyList<PaletteObjectHit>>([hit]);

            PaletteObjectHit? selected = null;
            palette.ObjectSelected += h => selected = h;

            palette.Open();

            var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
            var results = (ListBox)((StackPanel)palette.Child!).Children[1];

            queryBox.Text = "bra";

            var deadline = DesktopTestHelpers.Deadline(5);
            while (!HasObjectRow(results) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(HasObjectRow(results), "Expected the Objects section to appear once the background search completed.");

            results.SelectedIndex = ((IReadOnlyList<ListBoxItem>)results.ItemsSource!).Count - 1;
            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.NotNull(selected);
            Assert.Equal(hitId, selected!.ObjectId);
            Assert.False(palette.IsOpen);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `TD-177`: <see cref="CommandPaletteOverlay.Open(string?)"/> seeds the
    /// query box with the text passed in, caret at the end, and filters
    /// exactly as if that text had been typed by hand — the Objects
    /// section included, once the (stubbed) background search returns —
    /// so the header's own search box never makes anyone retype what they
    /// already typed once.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_WithAQuery_SeedsTheQueryTextWithCaretAtEnd_AndFiltersExactlyAsIfTyped()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var palette = new CommandPaletteOverlay(registry);
            var hitId = Guid.NewGuid();
            var hit = new PaletteObjectHit(hitId, "Part", "Bracket Mounting Plate", "Demo Project");
            palette.ObjectSearchSource = (q, _) =>
                Task.FromResult<IReadOnlyList<PaletteObjectHit>>(q == "bra" ? [hit] : []);

            palette.Open("bra");

            Assert.True(palette.IsOpen);
            var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
            var results = (ListBox)((StackPanel)palette.Child!).Children[1];
            Assert.Equal("bra", queryBox.Text);
            Assert.Equal("bra".Length, queryBox.CaretIndex);

            var deadline = DesktopTestHelpers.Deadline(5);
            while (!HasObjectRow(results) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(HasObjectRow(results), "Expected the seeded query to drive the same Objects search a typed query would.");

            // `Open(null)` behaves exactly like the historical parameterless
            // open: every existing call site (Ctrl+K, the palette command)
            // is unaffected by this overload.
            palette.Close();
            palette.Open(null);
            Assert.True(palette.IsOpen);
            Assert.Equal(string.Empty, queryBox.Text);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static bool HasObjectRow(ListBox results) =>
        results.ItemsSource is IReadOnlyList<ListBoxItem> items
            && items.Any(i => i.Content is string s && s.Contains("Bracket Mounting Plate", StringComparison.Ordinal));

    // ==================================================================
    // TD-77 (`WP 20.2A`): an empty query lists what applies, grouped
    // ==================================================================

    /// <summary>
    /// With a Requirement selected, many Requirements commands apply at
    /// once — a real case where the old "every registered command"
    /// listing buried them among a hundred-plus disabled rows. The empty
    /// query now lists exactly the set <see cref="ICommandRegistry.Evaluate"/>
    /// reports available, grouped by <see cref="CommandDescriptor.Category"/>,
    /// alphabetical within a group before anything has been invoked this
    /// session — reconstructed independently here from the real registry,
    /// rather than asserting a handful of rows in isolation.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_WithEmptyQuery_ListsOnlyAvailableCommands_GroupedByCategory()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var context = CommandContext.For(Guid.NewGuid(), "Requirement");

            var palette = new CommandPaletteOverlay(registry) { ContextSource = () => context };
            palette.Open();

            var results = (ListBox)((StackPanel)palette.Child!).Children[1];
            var rows = ((System.Collections.IEnumerable)results.ItemsSource!).Cast<ListBoxItem>()
                .Select(i => (string)i.Content!).ToList();

            Assert.Equal(ExpectedGroupedRows(registry, context), rows);

            // The specific case this Work Package closed: several
            // Requirements commands (Move among them, since `WP 20.2A`
            // gives it a real binding) are listed together, not scattered
            // among disabled rows.
            var headerIndex = rows.IndexOf("REQUIREMENTS");
            Assert.True(headerIndex >= 0);
            Assert.Contains("Move Requirement", rows.Skip(headerIndex + 1));
            Assert.Contains("Set Requirement Owner", rows.Skip(headerIndex + 1));

            // Nothing unavailable is listed at all.
            Assert.DoesNotContain(rows, row => row.Contains(" — ", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Invoking a command through the palette moves it to the front of its
    /// own category group the next time the palette opens with an empty
    /// query — a session-only ranking (this class's own remarks), so no
    /// second palette instance is expected to know about it.
    /// </summary>
    [AvaloniaFact]
    public async Task InvokingACommand_MovesItToTheFrontOfItsOwnGroup_OnTheNextEmptyQueryOpen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var context = CommandContext.For(Guid.NewGuid(), "Requirement");

            var palette = new CommandPaletteOverlay(registry)
            {
                ContextSource = () => context,
                // Answers every declared parameter generically — this test
                // cares about recency ordering, not what value was set.
                ParameterPrompt = (_, parameters, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    parameters.ToDictionary(p => p.Name, p => p.DefaultValue ?? "Recency Test", StringComparer.Ordinal)),
            };

            var results = (ListBox)((StackPanel)palette.Child!).Children[1];

            palette.Open();
            var before = ((System.Collections.IEnumerable)results.ItemsSource!).Cast<ListBoxItem>()
                .Select(i => (string)i.Content!).ToList();

            var headerIndex = before.IndexOf("REQUIREMENTS");
            Assert.NotEqual("Set Requirement Owner", before[headerIndex + 1]);

            CommandDescriptor? invoked = null;
            palette.CommandInvoked += (d, _) => invoked = d;

            results.SelectedIndex = before.IndexOf("Set Requirement Owner");
            var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.NotNull(invoked);
            Assert.Equal("requirements.set-owner", invoked!.Id);
            Assert.False(palette.IsOpen);

            palette.Open();
            var after = ((System.Collections.IEnumerable)results.ItemsSource!).Cast<ListBoxItem>()
                .Select(i => (string)i.Content!).ToList();

            // The one row that moved, and where it moved to: first in its
            // own group, everything else in the identical relative order.
            Assert.Equal("Set Requirement Owner", after[headerIndex + 1]);
            Assert.Equal(before.Where(r => r != "Set Requirement Owner"), after.Where(r => r != "Set Requirement Owner"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Reconstructs TD-77's own empty-query contract independently: available commands, grouped by Category, alphabetical within a group (no invocation has happened yet in these tests' own fresh palette).</summary>
    private static List<string> ExpectedGroupedRows(ICommandRegistry registry, CommandContext context)
    {
        var available = registry.Items
            .Where(d => registry.Evaluate(d.Id, context).IsAvailable)
            .OrderBy(d => d.Category ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.DisplayName, StringComparer.Ordinal)
            .ToList();

        var rows = new List<string>();
        string? currentCategory = null;
        var isFirst = true;

        foreach (var descriptor in available)
        {
            if (isFirst || descriptor.Category != currentCategory)
            {
                rows.Add((descriptor.Category ?? "General").ToUpperInvariant());
                currentCategory = descriptor.Category;
                isFirst = false;
            }

            rows.Add(descriptor.DisplayName);
        }

        return rows;
    }
}
