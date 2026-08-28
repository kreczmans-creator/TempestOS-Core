using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Contracts;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// Everything requiring the user's attention — the Companion's triage
/// surface, and the home of its one real quick action: approving or
/// returning a Document-family object awaiting review, dispatched
/// server-side through the existing <c>SetDocumentStatusCommand</c>
/// (`WP 14.0A`'s observe → understand → decide → act loop). Every act is
/// confirmed before it is sent (destructive-action protection), and the
/// outcome — success or the command's own failure message — is shown
/// inline, never swallowed.
/// </summary>
public sealed class AttentionPage : CompanionPage
{
    private readonly CompanionDataService _data;

    /// <summary>Initialises a new instance of the <see cref="AttentionPage"/> class.</summary>
    /// <param name="data">The Companion data service.</param>
    public AttentionPage(CompanionDataService data)
        : base("Attention")
    {
        ArgumentNullException.ThrowIfNull(data);

        _data = data;
        ShowLoading();
    }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        ShowLoading();
        var result = await _data.GetAttentionAsync();
        ShowResult(result, Render);
    }

    private IEnumerable<Control> Render(AttentionDto attention)
    {
        var anything = attention.AttentionItems.Count > 0 || attention.BlockedItems.Count > 0
            || attention.OpenDecisions.Count > 0 || attention.PendingReviews.Count > 0;

        if (!anything)
        {
            yield return new EmptyStateView("✓", "Nothing needs your attention right now.") { MinHeight = 320 };
            yield break;
        }

        if (attention.PendingReviews.Count > 0)
            yield return PendingReviewsCard(attention.PendingReviews);

        if (attention.AttentionItems.Count > 0)
        {
            var card = new CompanionCard("⚠", $"What Needs Attention ({attention.AttentionItems.Count})", Brushes.DarkOrange);
            foreach (var item in attention.AttentionItems)
            {
                card.AddLine(item.Title);
                card.AddLine(item.Detail, secondary: true);
            }
            yield return card;
        }

        if (attention.BlockedItems.Count > 0)
        {
            var card = new CompanionCard("⊘", $"Blocked Items ({attention.BlockedItems.Count})", Brushes.Crimson);
            foreach (var item in attention.BlockedItems)
                card.AddLine(item);
            yield return card;
        }

        if (attention.OpenDecisions.Count > 0)
        {
            var card = new CompanionCard("◇", $"Open Decisions ({attention.OpenDecisions.Count})", Brushes.MediumPurple);
            foreach (var item in attention.OpenDecisions)
                card.AddLine(item);
            yield return card;
        }

        var tasks = new CompanionCard("☑", "Open Tasks & Actions");
        tasks.AddMonoLine($"{attention.OpenTaskCount} open");
        yield return tasks;

        if (attention.UpcomingMilestones.Count > 0)
        {
            var milestones = new CompanionCard("⚑", "Upcoming Milestones");
            foreach (var item in attention.UpcomingMilestones)
                milestones.AddMonoLine(item);
            yield return milestones;
        }
    }

    private CompanionCard PendingReviewsCard(IReadOnlyList<PendingReviewDto> reviews)
    {
        var card = new CompanionCard("✓", $"Reviews Awaiting Decision ({reviews.Count})", BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.AccentBrushKey));

        foreach (var review in reviews)
            card.AddContent(new PendingReviewRow(review, _data, () => _ = RefreshAsync()));

        return card;
    }

    /// <summary>
    /// One actionable pending review: identity, then Approve/Return
    /// actions, each gated by an inline confirmation step before the
    /// command is dispatched, with the outcome rendered in place.
    /// </summary>
    private sealed class PendingReviewRow : StackPanel
    {
        private readonly PendingReviewDto _review;
        private readonly CompanionDataService _data;
        private readonly Action _onCompleted;
        private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, Spacing = CompanionTokens.SpaceMd };

        public PendingReviewRow(PendingReviewDto review, CompanionDataService data, Action onCompleted)
        {
            _review = review;
            _data = data;
            _onCompleted = onCompleted;

            Spacing = CompanionTokens.SpaceSm;
            Margin = new Avalonia.Thickness(0, CompanionTokens.SpaceSm);

            Children.Add(new TextBlock
            {
                Text = review.DisplayName,
                FontFamily = CompanionTokens.BodyFont,
                FontSize = CompanionTokens.FontSizeBody,
                FontWeight = CompanionTokens.WeightHeading,
                TextWrapping = TextWrapping.Wrap,
            });
            Children.Add(new TextBlock
            {
                Text = $"{review.Kind} · {review.Status}",
                FontFamily = CompanionTokens.MonoFont,
                FontSize = CompanionTokens.FontSizeCaption,
            });

            Children.Add(_actions);
            ShowChoices();
        }

        private void ShowChoices()
        {
            _actions.Children.Clear();
            _actions.Children.Add(ActionButton("Approve", () => ShowConfirm("Approved")));
            _actions.Children.Add(ActionButton("Return to Draft", () => ShowConfirm("Draft")));
        }

        private void ShowConfirm(string targetStatus)
        {
            _actions.Children.Clear();
            _actions.Children.Add(new TextBlock
            {
                Text = $"Set '{_review.DisplayName}' to {targetStatus}?",
                FontFamily = CompanionTokens.BodyFont,
                FontSize = CompanionTokens.FontSizeCaption,
                VerticalAlignment = VerticalAlignment.Center,
            });
            _actions.Children.Add(ActionButton("Confirm", () => _ = ExecuteAsync(targetStatus)));
            _actions.Children.Add(ActionButton("Cancel", ShowChoices));
        }

        private async Task ExecuteAsync(string targetStatus)
        {
            _actions.Children.Clear();
            _actions.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 96, Height = 4, VerticalAlignment = VerticalAlignment.Center });

            string message;
            bool succeeded;

            try
            {
                var outcome = await _data.SetDocumentStatusAsync(new SetObjectStatusRequest(_review.Id, _review.Kind, targetStatus));
                (succeeded, message) = (outcome.Succeeded, outcome.Message ?? (outcome.Succeeded ? "Done." : "The command failed."));
            }
            catch (Client.CompanionApiException ex)
            {
                (succeeded, message) = (false, ex.Message);
            }

            _actions.Children.Clear();
            _actions.Children.Add(new TextBlock
            {
                Text = succeeded ? $"✓ {message}" : $"⊗ {message}",
                Foreground = succeeded ? Brushes.SeaGreen : Brushes.Crimson,
                FontFamily = CompanionTokens.BodyFont,
                FontSize = CompanionTokens.FontSizeCaption,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            });

            if (succeeded)
                _onCompleted();
        }

        private static Button ActionButton(string label, Action onClick)
        {
            var button = new Button
            {
                Content = label,
                MinHeight = CompanionTokens.MinTouchTarget,
            };
            button.Click += (_, _) => onClick();
            return button;
        }
    }
}
