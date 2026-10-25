using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Calc.Library;
using Tempest.Core.Evidence;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// `ADR-0173` (review M9): the sign-off a template with extra governance demands, on the worksheet form. A template that requires a competent
/// person's attestation offers Attest on a draft; one that requires a second independent check offers Countersign on a checked calculation.
/// Neither is offered for any other template, so the form of every existing calculator is unchanged. The people are never typed: the
/// coordinator records both under the signed-in principal, so the issued sheet can only name who really signed.
/// </summary>
public sealed partial class CalcSheetView
{
    public const string GovernancePanelName = "Sign-off";
    public const string AttestCaption = "Attest";
    public const string CountersignCaption = "Countersign";

    /// <summary>What the signed-in competent person typed to attest a draft: the display name, qualification and statement the sheet prints.</summary>
    public sealed record AttestDraft(string Name, string Qualification, string Statement);

    /// <summary>What the signed-in second checker typed to countersign: their organisation, statement and outcome.</summary>
    public sealed record CountersignDraft(string Organisation, string Statement, CheckOutcome Outcome);

    private readonly StackPanel _governancePanel = new() { Spacing = DesignTokens.SpaceSm, IsVisible = false };
    private readonly TextBlock _governanceNote = Caption("Signatures are not authenticated by TempestOS: the names and statements below are recorded as typed, beside the person signed in. Whether they are who they say is the company's to establish.");
    private readonly StackPanel _attestGroup = new() { Spacing = DesignTokens.SpaceXs, IsVisible = false };
    private readonly TextBox _attestName = Input(string.Empty, "Competent person's name, as printed");
    private readonly TextBox _attestQualification = Input(string.Empty, "Qualification");
    private readonly TextBox _attestStatement = Input(string.Empty, "Attestation statement");
    private readonly Button _attestButton = new() { Content = AttestCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _attestState = Caption(string.Empty);
    private readonly StackPanel _countersignGroup = new() { Spacing = DesignTokens.SpaceXs, IsVisible = false };
    private readonly TextBox _countersignOrganisation = Input(string.Empty, "Second checker's organisation");
    private readonly TextBox _countersignStatement = Input(string.Empty, "Second check statement");
    private readonly ComboBox _countersignOutcome = new() { HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = Outcomes.Select(o => o.Text).ToList(), SelectedIndex = 0 };
    private readonly Button _countersignButton = new() { Content = CountersignCaption, MinHeight = DesignTokens.MinControlSize };

    /// <summary>The competent person attests the shown draft (the coordinator records it under the signed-in principal).</summary>
    public event Action<AttestDraft>? AttestRequested;

    /// <summary>The second checker countersigns the shown checked calculation.</summary>
    public event Action<CountersignDraft>? CountersignRequested;

    /// <summary>Whether the sign-off panel is shown (the template requires an attestation or a second check).</summary>
    public bool IsGovernancePanelVisible => _governancePanel.IsVisible;

    /// <summary>Whether Attest is offered (a draft whose template requires an attestation).</summary>
    public bool CanAttest => _attestGroup.IsVisible && _attestButton.IsEnabled;

    /// <summary>Whether Countersign is offered (a checked calculation whose template requires a second check that has not been made).</summary>
    public bool CanCountersign => _countersignGroup.IsVisible && _countersignButton.IsEnabled;

    /// <summary>The attestation state line as shown.</summary>
    public string AttestStateText => _attestState.Text ?? string.Empty;

    /// <summary>The not-authenticated notice as shown with the sign-off fields.</summary>
    public string GovernanceNoticeText => _governanceNote.Text ?? string.Empty;

    /// <summary>Types the attestation fields.</summary>
    public void FillAttest(string name, string qualification, string statement)
    {
        _attestName.Text = name;
        _attestQualification.Text = qualification;
        _attestStatement.Text = statement;
        UpdateGovernance();
    }

    /// <summary>Types the countersignature fields.</summary>
    public void FillCountersign(string organisation, string statement, CheckOutcome outcome = CheckOutcome.Accepted)
    {
        _countersignOrganisation.Text = organisation;
        _countersignStatement.Text = statement;
        _countersignOutcome.SelectedIndex = Array.FindIndex(Outcomes, o => o.Outcome == outcome);
        UpdateGovernance();
    }

    private void WireGovernance()
    {
        AutomationProperties.SetName(_governancePanel, GovernancePanelName);
        AutomationProperties.SetName(_attestName, "Attesting person's name");
        AutomationProperties.SetName(_attestQualification, "Attesting person's qualification");
        AutomationProperties.SetName(_attestStatement, "Attestation statement");
        AutomationProperties.SetName(_attestButton, AttestCaption);
        AutomationProperties.SetName(_attestState, "Attestation state");
        AutomationProperties.SetLiveSetting(_attestState, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_countersignOrganisation, "Second checker organisation");
        AutomationProperties.SetName(_countersignStatement, "Second check statement");
        AutomationProperties.SetName(_countersignOutcome, "Second check outcome");
        AutomationProperties.SetName(_countersignButton, CountersignCaption);

        foreach (var box in new[] { _attestName, _attestQualification, _attestStatement, _countersignOrganisation, _countersignStatement })
            box.TextChanged += (_, _) => UpdateGovernance();

        _attestButton.Click += (_, _) => AttestRequested?.Invoke(new AttestDraft(_attestName.Text?.Trim() ?? string.Empty, _attestQualification.Text?.Trim() ?? string.Empty, _attestStatement.Text?.Trim() ?? string.Empty));
        _countersignButton.Click += (_, _) =>
        {
            if (_countersignOutcome.SelectedIndex is >= 0 and var o && o < Outcomes.Length)
                CountersignRequested?.Invoke(new CountersignDraft(_countersignOrganisation.Text?.Trim() ?? string.Empty, _countersignStatement.Text?.Trim() ?? string.Empty, Outcomes[o].Outcome));
        };
    }

    private Control BuildGovernancePanel()
    {
        _attestGroup.Children.Add(Caption("A competent person attests this lifting calculation before it can be checked. Save the inputs first: a later change to any input voids the attestation."));
        _attestGroup.Children.Add(LabelledRow("Name", _attestName));
        _attestGroup.Children.Add(LabelledRow("Qualification", _attestQualification));
        _attestGroup.Children.Add(LabelledRow("Statement", _attestStatement));
        _attestGroup.Children.Add(_attestButton);
        _countersignGroup.Children.Add(Caption("A second, independent checker countersigns after the first check. They must not be an author, the attester or the first checker."));
        _countersignGroup.Children.Add(LabelledRow("Organisation", _countersignOrganisation));
        _countersignGroup.Children.Add(LabelledRow("Statement", _countersignStatement));
        _countersignGroup.Children.Add(LabelledRow("Outcome", _countersignOutcome));
        _countersignGroup.Children.Add(_countersignButton);
        _governancePanel.Children.Add(_governanceNote);
        _governancePanel.Children.Add(_attestState);
        _governancePanel.Children.Add(_attestGroup);
        _governancePanel.Children.Add(_countersignGroup);
        return Section(GovernancePanelName, _governancePanel);
    }

    /// <summary>Shows only the sign-off steps the shown template and the shown calculation's status call for.</summary>
    private void UpdateGovernance()
    {
        var template = SelectedTemplate?.Template;
        var instance = CurrentInstance;
        var needsAttest = template is { RequiresAttestation: true };
        var needsSecond = template is { RequiresSecondCheck: true };
        _governancePanel.IsVisible = needsAttest || needsSecond;

        var canAttest = needsAttest && instance is { Status: CalcInstanceStatus.Draft };
        _attestGroup.IsVisible = canAttest;
        _attestButton.IsEnabled = canAttest && !_busy
            && !string.IsNullOrWhiteSpace(_attestName.Text) && !string.IsNullOrWhiteSpace(_attestQualification.Text) && !string.IsNullOrWhiteSpace(_attestStatement.Text);

        _attestState.Text = needsAttest
            ? instance?.Attestation is { } attestation
                ? $"Attested by {attestation.Name} ({attestation.Qualification}), signed in as {attestation.Principal}, on {attestation.At:u}."
                : instance is null ? "Save the calculation, then attest it." : "Not attested: the first check is refused until a competent person attests."
            : string.Empty;

        var canCountersign = needsSecond && instance is { Status: CalcInstanceStatus.Checked, SecondCheck: null };
        _countersignGroup.IsVisible = canCountersign;
        _countersignButton.IsEnabled = canCountersign && !_busy
            && !string.IsNullOrWhiteSpace(_countersignOrganisation.Text) && !string.IsNullOrWhiteSpace(_countersignStatement.Text);
    }
}
