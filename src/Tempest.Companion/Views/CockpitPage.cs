using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Contracts;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// The Companion's default landing screen — the mobile expression of the
/// Engineering Cockpit (`ADR-0069`, `WP 14.0A`): the identical named
/// regions the desktop Cockpit renders (What Needs Attention, Continue
/// Where I Left Off, Project Health, Open Decisions, Blocked Items,
/// Upcoming Milestones, Risk Summary, Recent Activity), prioritised for a
/// single phone-width column — attention first, then continuity, then
/// health, then context. Cockpit-first, not menu-first: opening the app
/// answers "what is happening in TempestOS right now."
/// </summary>
public sealed class CockpitPage : CompanionPage
{
    private readonly CompanionDataService _data;
    private readonly Action<Guid, string, string>? _onOpenRecent;

    /// <summary>Initialises a new instance of the <see cref="CockpitPage"/> class.</summary>
    /// <param name="data">The Companion data service.</param>
    /// <param name="onOpenRecent">Invoked when a recent-activity entry is tapped — (objectId, kind, title).</param>
    public CockpitPage(CompanionDataService data, Action<Guid, string, string>? onOpenRecent = null)
        : base("Cockpit")
    {
        ArgumentNullException.ThrowIfNull(data);

        _data = data;
        _onOpenRecent = onOpenRecent;
        ShowLoading();
    }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        ShowLoading();
        var result = await _data.GetCockpitAsync();
        ShowResult(result, Render);
    }

    private IEnumerable<Control> Render(CockpitSummaryDto cockpit)
    {
        yield return HealthCard(cockpit);
        yield return AttentionCard(cockpit);
        yield return ContinueCard(cockpit);

        if (cockpit.OpenDecisions.Count > 0)
            yield return ListCard("◇", "Open Decisions", cockpit.OpenDecisions, Brushes.MediumPurple);

        if (cockpit.BlockedItems.Count > 0)
            yield return ListCard("⊘", "Blocked Items", cockpit.BlockedItems, Brushes.Crimson);

        yield return TasksCard(cockpit);

        if (cockpit.UpcomingMilestones.Count > 0)
            yield return MonoListCard("⚑", "Upcoming Milestones", cockpit.UpcomingMilestones);

        yield return new CompanionCard("△", "Risk Summary").AddMonoLine(cockpit.RiskSummary);
        yield return RecentProjectsCard(cockpit);
        yield return ActivityCard(cockpit);
        yield return SystemFooter(cockpit);
    }

    private static CompanionCard HealthCard(CockpitSummaryDto cockpit)
    {
        var healthColour = CompanionStatusColors.ForHealth(cockpit.Health);
        var card = new CompanionCard("▣", "Project Health", healthColour);

        var hero = new StackPanel { Orientation = Orientation.Horizontal, Spacing = CompanionTokens.SpaceLg };
        hero.Children.Add(new TextBlock { Text = "●", FontSize = 22, Foreground = healthColour, VerticalAlignment = VerticalAlignment.Center });
        hero.Children.Add(new TextBlock
        {
            Text = cockpit.Health.ToUpperInvariant(),
            FontFamily = CompanionTokens.TitleFont,
            FontSize = CompanionTokens.FontSizeHero,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        card.AddContent(hero);
        card.AddMonoLine(cockpit.HealthScoreDisplay);
        card.AddLine(cockpit.ProjectName, secondary: true);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Avalonia.Thickness(0, CompanionTokens.SpaceSm, 0, 0),
        };
        var row = 0;
        for (var i = 0; i < cockpit.DisciplineStatuses.Count; i++)
        {
            if (i % 2 == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                row = grid.RowDefinitions.Count - 1;
            }

            var status = cockpit.DisciplineStatuses[i];
            var cell = new StackPanel { Spacing = CompanionTokens.SpaceXs, Margin = new Avalonia.Thickness(0, CompanionTokens.SpaceSm) };
            cell.Children.Add(new TextBlock
            {
                Text = status.Discipline,
                FontFamily = CompanionTokens.BodyFont,
                FontSize = CompanionTokens.FontSizeCaption,
            });
            cell.Children.Add(StatusChip(status.Status, CompanionStatusColors.ForHealth(status.Status)));

            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, i % 2);
            grid.Children.Add(cell);
        }
        card.AddContent(grid);

        return card;
    }

    private static CompanionCard AttentionCard(CockpitSummaryDto cockpit)
    {
        var card = new CompanionCard("⚠", $"What Needs Attention ({cockpit.AttentionItems.Count})", Brushes.DarkOrange);

        foreach (var item in cockpit.AttentionItems.Take(6))
        {
            card.AddLine(item.Title);
            card.AddLine(item.Detail, secondary: true);
        }

        if (cockpit.AttentionItems.Count > 6)
            card.AddLine($"+ {cockpit.AttentionItems.Count - 6} more — see Attention", secondary: true);

        return card;
    }

    private CompanionCard ContinueCard(CockpitSummaryDto cockpit)
    {
        var card = new CompanionCard("↩", "Continue Where I Left Off");

        if (cockpit.ContinueWhereILeftOff is not { } item)
            return card.AddLine("Nothing opened yet this session.", secondary: true);

        if (_onOpenRecent is null)
            return card.AddLine(item.Title).AddMonoLine($"{item.Kind} · {FormatMoment(item.OpenedAtUtc)}");

        var button = new Button
        {
            MinHeight = CompanionTokens.MinTouchTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = new StackPanel
            {
                Spacing = CompanionTokens.SpaceXs,
                Children =
                {
                    new TextBlock { Text = item.Title, FontFamily = CompanionTokens.BodyFont, FontSize = CompanionTokens.FontSizeBody },
                    new TextBlock { Text = $"{item.Kind} · {FormatMoment(item.OpenedAtUtc)}", FontFamily = CompanionTokens.MonoFont, FontSize = CompanionTokens.FontSizeCaption },
                },
            },
        };
        button.Click += (_, _) => _onOpenRecent(item.ObjectId, item.Kind, item.Title);
        return card.AddContent(button);
    }

    private static CompanionCard TasksCard(CockpitSummaryDto cockpit)
    {
        var card = new CompanionCard("☑", "Open Tasks & Actions");
        card.AddMonoLine($"{cockpit.OpenTaskCount} open");
        // The desktop Cockpit's own honest disclosure, repeated verbatim
        // in spirit: no due-date field exists in the Domain, so "overdue"
        // cannot be computed honestly - open count is the real substitute.
        card.AddLine("Overdue tracking needs a due-date field the Engineering Domain does not carry yet.", secondary: true);
        return card;
    }

    private static CompanionCard ListCard(string glyph, string title, IReadOnlyList<string> items, IBrush? accent = null)
    {
        var card = new CompanionCard(glyph, $"{title} ({items.Count})", accent);
        foreach (var item in items.Take(8))
            card.AddLine(item);
        if (items.Count > 8)
            card.AddLine($"+ {items.Count - 8} more", secondary: true);
        return card;
    }

    private static CompanionCard MonoListCard(string glyph, string title, IReadOnlyList<string> items)
    {
        var card = new CompanionCard(glyph, title);
        foreach (var item in items.Take(8))
            card.AddMonoLine(item);
        return card;
    }

    private static CompanionCard RecentProjectsCard(CockpitSummaryDto cockpit)
    {
        var card = new CompanionCard("⬡", "Recent Projects");

        if (cockpit.RecentProjects.Count == 0)
            return card.AddLine("No Projects exist yet.", secondary: true);

        foreach (var name in cockpit.RecentProjects.Take(5))
            card.AddLine(name);

        return card;
    }

    private CompanionCard ActivityCard(CockpitSummaryDto cockpit)
    {
        var card = new CompanionCard("↻", "Recent Activity");

        if (cockpit.RecentActivity.Count == 0)
            return card.AddLine("No activity recorded this session.", secondary: true);

        foreach (var item in cockpit.RecentActivity.Take(5))
        {
            card.AddLine(item.Title);
            card.AddMonoLine($"{item.Kind} · {FormatMoment(item.OpenedAtUtc)}");
        }

        return card;
    }

    private static Control SystemFooter(CockpitSummaryDto cockpit) =>
        new TextBlock
        {
            Text = $"TempestOS {cockpit.PlatformVersion} · generated {FormatMoment(cockpit.GeneratedAtUtc)}",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 10,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, CompanionTokens.SpaceMd, 0, CompanionTokens.SpaceXl),
        };
}
