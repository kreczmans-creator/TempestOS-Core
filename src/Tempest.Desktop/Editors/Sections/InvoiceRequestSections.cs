using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Editors;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// An invoice request's own Lines — read-only, built by
/// <c>InvoicingService.RaiseFromCompletionAsync</c>, never edited here
/// (`WP 19.1A`, `ADR-0151`). Moved verbatim from <see cref="ObjectEditorView"/>'s
/// own former <c>PopulateInvoiceRequest</c> (`WP 21.1B`) — kept in the same
/// file as <see cref="InvoiceConnectorSection"/> since both read the
/// identical <see cref="InvoiceRequest"/>, as two independent
/// <see cref="IEditorSection"/> classes with no shared state (the pre-split
/// method combined them only for code proximity).
/// </summary>
internal sealed class InvoiceLinesSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Lines";

    public bool AppliesTo(IEngineeringObject? subject) =>
        subject is InvoiceRequest && _ctx?.Declarations?.For(_ctx.ObjectKind) is { } declaration && declaration.HasSection(EditorSectionKeys.InvoiceLines);

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        _expander = EditorSectionHelpers.BuildSection(Title, _panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        if (_ctx.Declarations?.For(_ctx.ObjectKind) is null || subject is not InvoiceRequest request)
        {
            _expander.IsVisible = false;
            return Task.CompletedTask;
        }

        _expander.IsVisible = AppliesTo(subject);
        _panel.Children.Clear();

        if (request.Lines.Count == 0)
            _panel.Children.Add(new TextBlock { Text = "(no lines)", Opacity = 0.5, FontSize = DesignTokens.FontSizeBody });

        foreach (var line in request.Lines)
        {
            _panel.Children.Add(new TextBlock
            {
                Text = $"{line.Description}  •  {line.Quantity:0.##} × {line.UnitRate}  =  {line.Amount}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = DesignTokens.FontSizeBody,
            });
        }

        _panel.Children.Add(new TextBlock
        {
            Text = $"Total {request.Total}",
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

/// <summary>
/// An invoice request's own connector and every external field it has
/// reported (`WP 19.1A`, `ADR-0151`) — see <see cref="InvoiceLinesSection"/>'s
/// own remarks for why the two stay in one file as independent classes.
/// </summary>
internal sealed class InvoiceConnectorSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Connector";

    public bool AppliesTo(IEngineeringObject? subject) =>
        subject is InvoiceRequest && _ctx?.Declarations?.For(_ctx.ObjectKind) is { } declaration && declaration.HasSection(EditorSectionKeys.InvoicingExternal);

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        _expander = EditorSectionHelpers.BuildSection(Title, _panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        if (_ctx.Declarations?.For(_ctx.ObjectKind) is null || subject is not InvoiceRequest request)
        {
            _expander.IsVisible = false;
            return Task.CompletedTask;
        }

        _expander.IsVisible = AppliesTo(subject);
        _panel.Children.Clear();

        foreach (var (label, value) in new[]
        {
            ("Connector", request.Connector),
            ("External Id", request.ExternalId),
            ("External Invoice Number", request.ExternalInvoiceNumber),
            ("External Status", request.ExternalStatus),
            ("Issued Date", request.IssuedDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            ("Paid Date", request.PaidDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            ("Last Error", request.LastError),
        })
        {
            _panel.Children.Add(new TextBlock
            {
                Text = $"{label}: {value ?? "—"}",
                Opacity = value is null ? 0.6 : 1.0,
                TextWrapping = TextWrapping.Wrap,
                FontSize = DesignTokens.FontSizeBody,
            });
        }

        return Task.CompletedTask;
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
