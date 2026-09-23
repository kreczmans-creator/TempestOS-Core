using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Quotations;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Editors;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The quotation's own Lines section (`WP 19.5B`, `ADR-0152`) — mirrors
/// <see cref="InvoiceLinesSection"/> exactly, read-only: editing a
/// quotation's own lines is <c>ProjectQuoteView</c>'s job, reached through
/// the project's own Quote tab; this is what a quotation opened from the
/// Explorer or the Command Palette shows instead. Moved verbatim from
/// <see cref="ObjectEditorView"/>'s own former <c>PopulateQuotation</c>
/// (`WP 21.1B`).
/// </summary>
internal sealed class QuotationLinesSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Lines";

    public bool AppliesTo(IEngineeringObject? subject) =>
        subject is Quotation && _ctx?.Declarations?.For(_ctx.ObjectKind) is { } declaration && declaration.HasSection(EditorSectionKeys.QuotationLines);

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        _expander = EditorSectionHelpers.BuildSection(Title, _panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        if (_ctx.Declarations?.For(_ctx.ObjectKind) is null || subject is not Quotation quotation)
        {
            _expander.IsVisible = false;
            return Task.CompletedTask;
        }

        _expander.IsVisible = AppliesTo(subject);
        _panel.Children.Clear();

        _panel.Children.Add(new TextBlock
        {
            Text = $"{quotation.Reference}  •  {quotation.Status}  •  {quotation.QuoteDate:yyyy-MM-dd}  •  {quotation.Currency}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = TextWrapping.Wrap,
        });

        if (quotation.Lines.Count == 0)
            _panel.Children.Add(new TextBlock { Text = "(no lines)", Opacity = 0.5, FontSize = DesignTokens.FontSizeBody });

        foreach (var line in quotation.Lines)
        {
            _panel.Children.Add(new TextBlock
            {
                Text = line.Basis == QuotationLineBasis.Hourly
                    ? $"{line.Description}  •  {line.Hours:0.##} × {line.Rate}  =  {line.Amount}"
                    : $"{line.Description}  •  {line.Amount} (fixed)",
                TextWrapping = TextWrapping.Wrap,
                FontSize = DesignTokens.FontSizeBody,
            });
        }

        _panel.Children.Add(new TextBlock
        {
            Text = $"Total {quotation.Total}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0),
        });

        return Task.CompletedTask;
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
