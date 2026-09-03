using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Projects;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Project Workspace's own Requirements area — this project's
/// requirements, and what verification actually says about them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two statuses, deliberately.</b> Each row shows the requirement's own
/// declared lifecycle status <em>and</em> what its verification history
/// records, because they are different claims and they can disagree. A
/// requirement marked Verified with no verification record behind it is
/// exactly the thing a reviewer needs to see, and a surface that showed
/// one field would hide it. Where they do disagree the row says so
/// outright rather than quietly preferring either one.
/// </para>
/// <para>
/// The register reads the existing Requirements and Verification domain —
/// no new requirements model, no second status. This view renders that
/// answer and raises intent; it decides nothing.
/// </para>
/// </remarks>
public sealed class ProjectRequirementsView : UserControl
{
    /// <summary>The heading shown above the register.</summary>
    public const string Heading = "Requirements";

    /// <summary>What the surface says when no requirement is linked into the project.</summary>
    public const string EmptyHeadline = "No requirements in this project";

    /// <summary>The note shown against a requirement whose declared status claims verification nothing recorded.</summary>
    public const string UnrecordedVerificationNote = "Status claims verification, but nothing is recorded";

    private readonly StackPanel _list = new() { Spacing = DesignTokens.SpaceSm };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    /// <summary>Raised when the user asks to work on requirements in the Engineering Workspace.</summary>
    public event Action? EngineeringRequested;

    /// <summary>Initialises a new instance of the <see cref="ProjectRequirementsView"/> class.</summary>
    public ProjectRequirementsView()
    {
        var heading = new TextBlock
        {
            Text = Heading,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        var openWorkspace = new Button
        {
            Content = "Open in Engineering →",
            MinHeight = DesignTokens.MinControlSize,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        // `WP-Z4` Productisation Phase 1 (P1) — this area has no create
        // action of its own (requirements are authored in Engineering),
        // so "Open in Engineering" is its one real call-to-action.
        openWorkspace.Classes.Add(ChromeStyles.Primary);

        AutomationProperties.SetName(openWorkspace, "Open requirements in the Engineering Workspace");
        openWorkspace.Click += (_, _) => EngineeringRequested?.Invoke();

        var root = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelPadding };
        root.Children.Add(heading);
        root.Children.Add(_summary);
        root.Children.Add(openWorkspace);
        root.Children.Add(_list);

        AutomationProperties.SetName(this, Heading);
        Content = new ScrollViewer { Content = root };
    }

    /// <summary>The entries currently on screen, in the order they are shown.</summary>
    public IReadOnlyList<ProjectRequirementEntry> Entries { get; private set; } = [];

    /// <summary>Whether the surface is showing its empty state.</summary>
    public bool IsShowingEmptyState { get; private set; } = true;

    /// <summary>The summary line, exactly as a user reads it.</summary>
    public string SummaryText => _summary.Text ?? string.Empty;

    /// <summary>Renders <paramref name="entries"/> for the project named <paramref name="projectLabel"/>.</summary>
    public void Show(IReadOnlyList<ProjectRequirementEntry> entries, string? projectLabel)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = entries;
        _list.Children.Clear();

        var project = string.IsNullOrWhiteSpace(projectLabel) ? "this project" : projectLabel;

        IsShowingEmptyState = entries.Count == 0;
        if (IsShowingEmptyState)
        {
            _summary.Text = $"No requirement is allocated to anything in {project}.";
            _list.Children.Add(EmptyState(
                EmptyHeadline,
                "A requirement belongs to a project once it is linked to something the project owns. Allocate a requirement to an engineering object in the Engineering Workspace and it will appear here."));
            return;
        }

        var verified = entries.Count(e => e.Verification == RequirementVerificationState.Passed);
        var failed = entries.Count(e => e.Verification == RequirementVerificationState.Failed);
        var unverified = entries.Count(e => e.Verification == RequirementVerificationState.NotVerified);

        _summary.Text =
            $"{entries.Count} requirement(s) in {project} — {verified} passed, {failed} failed, {unverified} with no verification recorded.";

        foreach (var entry in entries)
            _list.Children.Add(BuildEntry(entry));
    }

    /// <summary>How a verification state reads to a user.</summary>
    public static string Describe(RequirementVerificationState state) => state switch
    {
        RequirementVerificationState.Passed => "Verified — passed",
        RequirementVerificationState.Failed => "Verified — failed",
        RequirementVerificationState.Conditional => "Verified — conditional",
        RequirementVerificationState.Unknown => "Verification not visible to you",
        _ => "Not verified",
    };

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
            FontFamily = DesignTokens.TitleFont,
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

    private static Control BuildEntry(ProjectRequirementEntry entry)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        header.Children.Add(new TextBlock
        {
            Text = entry.Identifier,
            FontWeight = DesignTokens.WeightHeading,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Status: {entry.Status}",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock
        {
            Text = Describe(entry.Verification),
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center,
        });
        rows.Children.Add(header);

        rows.Children.Add(new TextBlock
        {
            Text = entry.Statement,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });

        var linked = entry.LinkedObjectIds.Count == 1
            ? "Allocated to 1 object in this project"
            : $"Allocated to {entry.LinkedObjectIds.Count} objects in this project";

        var records = entry.VerificationCount == 1
            ? "1 verification record"
            : $"{entry.VerificationCount} verification records";

        rows.Children.Add(new TextBlock
        {
            Text = $"{linked} · {records}",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.7,
        });

        if (entry.ClaimsUnrecordedVerification)
        {
            rows.Children.Add(new TextBlock
            {
                Text = UnrecordedVerificationNote,
                FontSize = DesignTokens.FontSizeCaption,
                FontWeight = DesignTokens.WeightHeading,
                Opacity = 0.9,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = rows,
        };

        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }
}
