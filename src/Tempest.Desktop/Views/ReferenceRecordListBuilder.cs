using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Bearings;
using Tempest.Core.Components;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Standards;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The one compact list every reference-record surface shares
/// (Engineering → Reference data's <see cref="LibrariesView"/> and
/// Business → Staff's <see cref="StaffView"/> and Business → Rate cards'
/// <see cref="RateCardsView"/>) — Product Owner runbook F1,
/// 2026-10-01: "they should just show the title … a title and release
/// status. Opening them gives the detail. Also make the titles/families
/// collapsible."
/// </summary>
/// <remarks>
/// <para>
/// A row is the record's own title plus a release-status badge, with a
/// Release action only while the record is not yet released, and Open.
/// Everything else a record holds — revision, source citation, its full
/// definition — lives in the record view Open shows
/// (<see cref="ReferenceRecordView"/>), which also carries Verify and
/// Revise. The automation names (<c>Release {id}</c>, <c>Open {id}</c>)
/// are unchanged from the earlier row layout.
/// </para>
/// <para>
/// Library headings and the family groups under them collapse. What a
/// person collapsed or expanded is remembered for the session — across
/// refreshes and across both list instances — in
/// <see cref="SessionExpandedState"/>. Library headings start expanded;
/// family groups start collapsed, so a library opens as its own short
/// list of families with counts.
/// </para>
/// </remarks>
internal static class ReferenceRecordListBuilder
{
    /// <summary>The session's remembered expanded/collapsed state, keyed by group key. Process-wide on purpose: it is the session's memory, not a view's.</summary>
    private static readonly Dictionary<string, bool> SessionExpandedState = new(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="key"/> is expanded — the remembered state, else <paramref name="expandedByDefault"/>.</summary>
    public static bool IsExpanded(string key, bool expandedByDefault) =>
        SessionExpandedState.TryGetValue(key, out var expanded) ? expanded : expandedByDefault;

    /// <summary>Records <paramref name="key"/>'s expanded state for the rest of the session.</summary>
    public static void SetExpanded(string key, bool expanded) => SessionExpandedState[key] = expanded;

    /// <summary>The group key a library heading is remembered under.</summary>
    public static string LibraryKey(string library) => library;

    /// <summary>The group key a family group within a library is remembered under.</summary>
    public static string FamilyKey(string library, string family) => $"{library}/{family}";

    /// <summary>
    /// A collapsible section: a heading button reading
    /// "<paramref name="title"/> (<paramref name="count"/>)" over
    /// <paramref name="body"/>, shown or hidden as the heading toggles.
    /// </summary>
    /// <param name="key">The session key the expanded state is remembered under.</param>
    /// <param name="title">The heading's own text, before the count.</param>
    /// <param name="count">The number of records the section holds.</param>
    /// <param name="body">The section's own content.</param>
    /// <param name="expandedByDefault">Whether the section starts expanded the first time it is shown this session.</param>
    /// <param name="isLibrary">A library heading (larger) rather than a family group heading.</param>
    public static CollapsibleSection BuildSection(string key, string title, int count, Control body, bool expandedByDefault, bool isLibrary)
    {
        var section = new CollapsibleSection(key, title, count, body, IsExpanded(key, expandedByDefault), isLibrary);
        return section;
    }

    /// <summary>The AutomationId of a record row's own Open button — unique per record, where the announced name (the title) need not be.</summary>
    public static string OpenAutomationId(string recordId) => $"Open {recordId}";

    /// <summary>The AutomationId of a record row's own Release button.</summary>
    public static string ReleaseAutomationId(string recordId) => $"Release {recordId}";

    /// <summary>One compact record row: title, release-status badge, Release (while unreleased) and Open.</summary>
    /// <param name="recordId">The record's own Id — carried by the buttons' AutomationIds and the title's tooltip; the announced names use <paramref name="title"/>.</param>
    /// <param name="title">The record's own title or designation.</param>
    /// <param name="state">The record's validation state.</param>
    /// <param name="open">Opens the record.</param>
    /// <param name="release">Releases the record; <see langword="null"/> offers no Release action.</param>
    public static Grid BuildRow(string recordId, string title, ReferenceValidationState state, Func<Task> open, Func<Task>? release)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), Margin = new Thickness(DesignTokens.SpaceMd, 1, 0, 1) };

        var text = new TextBlock
        {
            Text = title,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(text, $"{title} ({recordId})");
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var badge = BuildStatusBadge(state);
        badge.Margin = new Thickness(DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm, 0);
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        // A double-tap on the row opens it too; the title stays a plain
        // label, so the automation walk sees one named control per action.
        grid.DoubleTapped += async (_, _) => await open().ConfigureAwait(true);

        if (release is not null)
        {
            var releaseButton = new Button
            {
                Content = "Release",
                Padding = new Thickness(10, 2),
                IsVisible = state is ReferenceValidationState.Draft or ReferenceValidationState.Checked or ReferenceValidationState.Validated,
            };
            releaseButton.Classes.Add(ChromeStyles.Subtle);
            releaseButton.Click += async (_, _) => await release().ConfigureAwait(true);
            AutomationProperties.SetName(releaseButton, $"Release {title}");
            AutomationProperties.SetAutomationId(releaseButton, ReleaseAutomationId(recordId));
            Grid.SetColumn(releaseButton, 2);
            grid.Children.Add(releaseButton);
        }

        var openButton = new Button { Content = "Open", Padding = new Thickness(10, 2) };
        openButton.Classes.Add(ChromeStyles.Flat);
        openButton.Click += async (_, _) => await open().ConfigureAwait(true);
        // v0.23.0 board N12: a screen reader announces the visible title,
        // not the record id; the id stays on the AutomationId, which is
        // unique where titles need not be.
        AutomationProperties.SetName(openButton, $"Open {title}");
        AutomationProperties.SetAutomationId(openButton, OpenAutomationId(recordId));
        Grid.SetColumn(openButton, 3);
        grid.Children.Add(openButton);

        return grid;
    }

    /// <summary>The release-status badge — the validation state's own name, coloured with the platform's existing health text colours (Released healthy, in review attention, Draft/Superseded neutral).</summary>
    public static Border BuildStatusBadge(ReferenceValidationState state)
    {
        var brushKey = state switch
        {
            ReferenceValidationState.Released => ApplicationPalette.HealthTextHealthyBrushKey,
            ReferenceValidationState.Checked or ReferenceValidationState.Validated => ApplicationPalette.HealthTextAttentionBrushKey,
            _ => ApplicationPalette.HealthTextUnknownBrushKey,
        };

        var label = new TextBlock { Text = state.ToString(), FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(label, TextBlock.ForegroundProperty, brushKey);

        var badge = new Border
        {
            Child = label,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
            Padding = new Thickness(6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeReactiveBrush.Bind(badge, Border.BorderBrushProperty, brushKey);
        return badge;
    }

    /// <summary>The family group a material lists under.</summary>
    public static string GroupFor(MaterialDefinition d) => d.Family switch
    {
        MaterialFamily.Steel => "Steels",
        MaterialFamily.StainlessSteel => "Stainless steels",
        MaterialFamily.CastIron => "Cast irons",
        MaterialFamily.Aluminium => "Aluminium alloys",
        MaterialFamily.CopperAlloy => "Copper alloys",
        MaterialFamily.Titanium => "Titanium alloys",
        MaterialFamily.NickelAlloy => "Nickel alloys",
        MaterialFamily.OtherMetal => "Other metals",
        MaterialFamily.Thermoplastic => "Thermoplastics",
        MaterialFamily.Thermoset => "Thermosets",
        MaterialFamily.Elastomer => "Elastomers",
        MaterialFamily.Ceramic => "Ceramics",
        MaterialFamily.Composite => "Composites",
        MaterialFamily.Glass => "Glasses",
        MaterialFamily.Other => "Other materials",
        _ => "Unclassified",
    };

    /// <summary>The group a fastener lists under — its property class where one is recorded, else its family.</summary>
    public static string GroupFor(FastenerDefinition d) =>
        d.Mechanical.PropertyClass is { Length: > 0 } propertyClass ? $"Property class {propertyClass}" : Humanise(d.Family.ToString());

    /// <summary>The group a bearing lists under — its series where one is recorded, else its family.</summary>
    public static string GroupFor(BearingDefinition d) =>
        d.Identity.Series is { Length: > 0 } series ? $"Series {series}" : Humanise(d.Family.ToString());

    /// <summary>The group a standard lists under — its publishing body.</summary>
    public static string GroupFor(StandardDefinition d) => d.Body.Code;

    /// <summary>The group a constant lists under — its category.</summary>
    public static string GroupFor(ConstantDefinition d) => Humanise(d.Category.ToString());

    /// <summary>The group a manufacturing process lists under — its family.</summary>
    public static string GroupFor(ProcessDefinition d) => Humanise(d.Family.ToString());

    /// <summary>The group a component lists under — its family.</summary>
    public static string GroupFor(ComponentDefinition d) => Humanise(d.Family.ToString());

    /// <summary>"AtomicAndNuclear" → "Atomic and nuclear"; "Unspecified" → "Unclassified".</summary>
    internal static string Humanise(string pascal)
    {
        if (string.Equals(pascal, "Unspecified", StringComparison.Ordinal))
            return "Unclassified";

        var builder = new StringBuilder(pascal.Length + 8);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(pascal[i - 1]))
            {
                builder.Append(' ');
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

/// <summary>A heading button over a body that shows or hides as the heading toggles, remembering the choice for the session (<see cref="ReferenceRecordListBuilder"/>).</summary>
internal sealed class CollapsibleSection : StackPanel
{
    private readonly string _key;
    private readonly Control _body;
    private readonly TextBlock _chevron = new() { Width = 14, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Initialises a new instance of the <see cref="CollapsibleSection"/> class.</summary>
    public CollapsibleSection(string key, string title, int count, Control body, bool expanded, bool isLibrary)
    {
        _key = key;
        _body = body;
        Title = title;

        var heading = new TextBlock
        {
            Text = $"{title} ({count})",
            FontWeight = isLibrary ? DesignTokens.WeightHeading : DesignTokens.WeightBody,
            FontSize = isLibrary ? DesignTokens.FontSizeHeading : DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
        content.Children.Add(_chevron);
        content.Children.Add(heading);

        Header = new Button
        {
            Content = content,
            Padding = new Thickness(2, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = isLibrary ? new Thickness(0, DesignTokens.SpaceMd, 0, DesignTokens.SpaceXs) : new Thickness(DesignTokens.SpaceSm, 0, 0, 0),
        };
        Header.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(Header, isLibrary ? $"{title} library" : $"{title} group");
        Header.Click += (_, _) => SetExpanded(!IsExpanded);

        Children.Add(Header);
        Children.Add(body);
        Apply(expanded);
    }

    /// <summary>The section's own title, before the count.</summary>
    public string Title { get; }

    /// <summary>The heading button that toggles the section.</summary>
    public Button Header { get; }

    /// <summary>Whether the body is currently shown.</summary>
    public bool IsExpanded { get; private set; }

    /// <summary>Shows or hides the body, remembering the choice for the session.</summary>
    public void SetExpanded(bool expanded)
    {
        ReferenceRecordListBuilder.SetExpanded(_key, expanded);
        Apply(expanded);
    }

    private void Apply(bool expanded)
    {
        IsExpanded = expanded;
        _body.IsVisible = expanded;
        _chevron.Text = expanded ? "▾" : "▸";
        AutomationProperties.SetHelpText(Header, expanded ? "Expanded — select to collapse" : "Collapsed — select to expand");
    }
}
