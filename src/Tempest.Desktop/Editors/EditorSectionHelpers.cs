using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Editors;

/// <summary>
/// The small handful of building blocks two or more <see cref="IEditorSection"/>
/// implementations share (`WP 21.1B`, brief scope item 2: "where two
/// sections share a helper, it moves to <c>EditorSectionHelpers.cs</c>") —
/// moved verbatim from <see cref="ObjectEditorView"/>'s own former private
/// static methods, with the same behaviour, the same automation names.
/// </summary>
/// <remarks>
/// <see cref="ObjectEditorView.BuildSeverityRow"/> and
/// <see cref="ObjectEditorView.BomKinds"/> stay on the shell, not here —
/// both are read from outside <c>Editors/</c> today (<c>PropertyInspectorView</c>,
/// <c>EngineeringCalculationView</c>, <c>InputDialog</c>, <c>NewProjectPrompt</c>,
/// <c>OutputPanelView</c>, and this Work Package's own test suite via
/// <c>ObjectEditorView.BomKinds</c>/<c>ObjectEditorView.CalculationPointerGuidance</c>),
/// so moving them would be a public-surface change this brief's "no
/// behaviour change of any kind" forbids — they are used by
/// <c>Sections/BillOfMaterialsSection.cs</c> and
/// <c>Sections/CalculationExecuteSection.cs</c> by their existing,
/// unchanged, fully-qualified name instead.
/// </remarks>
internal static class EditorSectionHelpers
{
    /// <summary>Wraps <paramref name="content"/> in one collapsible section — every section's own outermost <see cref="Expander"/>.</summary>
    public static Expander BuildSection(string title, Control content) => new()
    {
        Header = title,
        IsExpanded = true,
        Margin = DesignTokens.SectionMargin,
        Content = content,
    };

    /// <summary>One label-and-value row — every section's own field layout.</summary>
    public static Control LabeledRow(string label, Control valueControl)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*") };
        var text = new TextBlock { Text = label, Opacity = 0.8, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(valueControl, 1);
        row.Children.Add(text);
        row.Children.Add(valueControl);
        return row;
    }

    /// <summary>Builds one relationship row — "Navigation between related objects" (`WP 10.3A`), shared by the Relationships section's own real-object and Requirement-flow reads.</summary>
    public static async Task<Control> BuildRelationshipRowAsync(EditorSectionContext ctx, Guid otherId, string relationshipKind, string direction)
    {
        var other = await ctx.DomainContext.Repository.FindAsync(otherId).ConfigureAwait(true);
        var displayName = (other as IHasBusinessIdentifier)?.DisplayName ?? otherId.ToString();
        var otherKind = other?.Kind ?? ctx.ObjectKind;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Avalonia.Thickness(0, DesignTokens.SpaceXs) };

        var icon = new TextBlock { Text = IconRegistry.Resolve(otherKind), Margin = new Avalonia.Thickness(0, 0, DesignTokens.SpaceSm, 0) };
        var text = new TextBlock { Text = $"{direction} {relationshipKind} — {displayName}", TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(icon);
        row.Children.Add(text);

        if (other is not null)
        {
            var openContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
            openContent.Children.Add(new TextBlock { Text = "Open", FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center });
            openContent.Children.Add(IconGeometry.Build(IconGeometry.ChevronRight, 11));
            var openButton = new Button { Content = openContent, Padding = new Avalonia.Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceXs) };
            openButton.Classes.Add(ChromeStyles.Flat);

            // `WP 19.2B` (`TD-132`) — see this row's own pre-split remarks:
            // direction and relationship kind are included, not just the
            // related object's name, since one object can legitimately
            // appear in more than one relationship to the same object.
            Avalonia.Automation.AutomationProperties.SetName(openButton, $"Open {direction} {relationshipKind} — {displayName}");
            openButton.Click += (_, _) => ctx.NavigateToObject(otherId, otherKind);
            Grid.SetColumn(openButton, 2);
            row.Children.Add(openButton);
        }

        return row;
    }

    /// <summary>Builds one read-only, named, linked row for an object referenced by id — shared by <em>Where used</em> and Evidence's own <em>Subject</em>.</summary>
    public static async Task<Control> BuildObjectReferenceRowAsync(EditorSectionContext ctx, Guid referencedId)
    {
        var referenced = await ctx.DomainContext.Repository.FindAsync(referencedId).ConfigureAwait(true);
        var name = (referenced as IHasBusinessIdentifier)?.DisplayName ?? referencedId.ToString();
        var kind = referenced?.Kind ?? ctx.ObjectKind;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Avalonia.Thickness(0, DesignTokens.SpaceXs) };
        var icon = new TextBlock { Text = IconRegistry.Resolve(kind), Margin = new Avalonia.Thickness(0, 0, DesignTokens.SpaceSm, 0) };
        var text = new TextBlock { Text = $"{name} ({kind})", TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(icon);
        row.Children.Add(text);

        if (referenced is not null)
        {
            var openButton = new Button { Content = "Open", Padding = new Avalonia.Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
            openButton.Classes.Add(ChromeStyles.Flat);
            Avalonia.Automation.AutomationProperties.SetName(openButton, $"Open {name}");
            openButton.Click += (_, _) => ctx.NavigateToObject(referencedId, kind);
            Grid.SetColumn(openButton, 2);
            row.Children.Add(openButton);
        }

        return row;
    }

    /// <summary>Blank-to-<see langword="null"/> — every section's own "store nothing rather than an empty string" convention.</summary>
    public static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Parses the Commercial section's own <c>"&lt;amount&gt; &lt;currency&gt;"</c> budget text.</summary>
    public static bool TryParseMoney(string value, out Money money)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            try
            {
                money = new Money(amount, new CurrencyCode(parts[1]));
                return true;
            }
            catch (ArgumentException)
            {
                // Falls through to the failure return below.
            }
        }

        money = default;
        return false;
    }
}
