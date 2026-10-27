using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Calc.Library;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// DA-12: the preparer's own check, on the worksheet form. A template that demands an independent check (E4, E5, S6) normally needs a different
/// person signed in to press Check; where there is no second person, the preparer ticks "Self-check (no second person available)" and writes why
/// they stand by the calculation. The statement is kept on the calculation, printed on the issued sheet and written to the audit trail. It is offered
/// for no other template, and never for one with an attestation or a second check (S8), whose rule is unchanged.
/// </summary>
public sealed partial class CalcSheetView
{
    public const string SelfCheckPanelName = "Self-check";

    private readonly StackPanel _selfCheckGroup = new() { Spacing = DesignTokens.SpaceXs, IsVisible = false };
    private readonly CheckBox _selfCheckBox = new() { Content = CalcInstanceService.SelfCheckCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _selfCheckStatement = new()
    {
        Watermark = "Why you stand by this calculation (at least 20 characters)",
        MaxLength = CalcInstanceService.MaxSelfCheckStatementLength,
        AcceptsReturn = false,
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        IsEnabled = false,
    };

    /// <summary>Whether the self-check box is offered: a draft whose template demands an independent check and takes a self-check.</summary>
    public bool IsSelfCheckOffered => _selfCheckGroup.IsVisible;

    /// <summary>
    /// The self-check statement to give <c>CheckAsync</c>: the trimmed text when the box is ticked and offered, otherwise <see langword="null"/>
    /// (an unticked box gives no statement, so the service asks for a different person).
    /// </summary>
    public string? SelfCheckStatement => _selfCheckGroup.IsVisible && _selfCheckBox.IsChecked == true ? (_selfCheckStatement.Text ?? string.Empty).Trim() : null;

    /// <summary>Ticks or clears the box and types the statement.</summary>
    public void FillSelfCheck(bool ticked, string statement)
    {
        _selfCheckBox.IsChecked = ticked;
        _selfCheckStatement.Text = statement;
    }

    /// <summary>Clears the box and the statement (after a check, or when another calculation is shown).</summary>
    public void ClearSelfCheck()
    {
        _selfCheckBox.IsChecked = false;
        _selfCheckStatement.Text = string.Empty;
    }

    private void WireSelfCheck()
    {
        AutomationProperties.SetName(_selfCheckGroup, SelfCheckPanelName);
        AutomationProperties.SetName(_selfCheckBox, CalcInstanceService.SelfCheckCaption);
        AutomationProperties.SetName(_selfCheckStatement, "Self-check statement");
        _selfCheckBox.IsCheckedChanged += (_, _) => _selfCheckStatement.IsEnabled = _selfCheckBox.IsChecked == true;
        _selfCheckGroup.Children.Add(Caption("This calculation needs an independent check. Where no second person is available you may check your own work: tick the box and say why you stand by it. The statement is printed on the issued sheet and recorded in the audit trail."));
        _selfCheckGroup.Children.Add(_selfCheckBox);
        _selfCheckGroup.Children.Add(_selfCheckStatement);
    }

    /// <summary>Shows the self-check box only for a draft whose template demands an independent check and takes a self-check.</summary>
    private void UpdateSelfCheck()
    {
        var template = SelectedTemplate?.Template;
        var offered = template is { IndependentCheck: true, HasExtraGovernance: false, FeatureFlag: null or "" } && CurrentInstance is { Status: CalcInstanceStatus.Draft, IsLegacyArchive: false };

        if (_selfCheckGroup.IsVisible && !offered)
            ClearSelfCheck();

        _selfCheckGroup.IsVisible = offered;
    }
}
