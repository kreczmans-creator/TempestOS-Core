using Avalonia.Controls;
using Tempest.Companion.Contracts;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// Recent meaningful TempestOS activity — the Workspace's own recent
/// navigation trail, most recent first: what was worked on, of what Kind,
/// and when. The mobile counterpart of the desktop Cockpit's Recent
/// Engineering Activity region, as its own full page.
/// </summary>
public sealed class ActivityPage : CompanionPage
{
    private readonly CompanionDataService _data;

    /// <summary>Initialises a new instance of the <see cref="ActivityPage"/> class.</summary>
    /// <param name="data">The Companion data service.</param>
    public ActivityPage(CompanionDataService data)
        : base("Activity")
    {
        ArgumentNullException.ThrowIfNull(data);

        _data = data;
        ShowLoading();
    }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        ShowLoading();
        var result = await _data.GetActivityAsync();
        ShowResult(result, Render);
    }

    private static IEnumerable<Control> Render(ActivityDto activity)
    {
        if (activity.RecentActivity.Count == 0)
        {
            yield return new EmptyStateView("↻", "No Workspace activity recorded since TempestOS started.") { MinHeight = 320 };
            yield break;
        }

        var card = new CompanionCard("↻", $"Recent Activity ({activity.RecentActivity.Count})");

        foreach (var item in activity.RecentActivity)
        {
            card.AddLine(item.Title);
            card.AddMonoLine($"{item.Kind} · {FormatMoment(item.OpenedAtUtc)}");
        }

        yield return card;
    }
}
