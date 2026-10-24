using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Calc.Library;
using Tempest.Core.Evidence;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum A (A8-UI): the worksheet form's saved-calculations list (reopen a saved calculation, with its stale mark) and its
/// packs panel (create a pack from a template, list its members, check and issue it). The view raises events and renders what
/// it is given; <see cref="CalcSheetCoordinator"/> talks to <see cref="CalcInstanceService"/>.
/// </summary>
public sealed partial class CalcSheetView
{
    public const string SavedPanelName = "Saved calculations";
    public const string OpenSavedCaption = "Open";
    public const string PacksPanelName = "Calculation packs";
    public const string CreatePackCaption = "Create pack from template";
    public const string OpenPackMemberCaption = "Open pack member";
    public const string CheckPackCaption = "Check pack";
    public const string IssuePackCaption = "Issue pack";
    public const string StaleListMark = "STALE";

    /// <summary>One saved calculation as listed: its name, template title, revision, status and whether it is stale.</summary>
    public sealed record SavedCalculation(Guid Id, string Name, string TemplateTitle, string Revision, string Status, bool IsStale)
    {
        public string Label => $"{Name} · {TemplateTitle} · rev {Revision} · {Status}{(IsStale ? " · " + StaleListMark : string.Empty)}";
    }

    /// <summary>One pack as listed.</summary>
    public sealed record PackChoice(Guid Id, string Title, string Revision, CalcInstanceStatus Status, int Members)
    {
        public string Label => $"{Title} · rev {Revision} · {Status} · {Members} calculation(s)";
    }

    /// <summary>A pack template the project can start a pack from.</summary>
    public sealed record PackTemplateChoice(string Id, string Title, string Description);

    /// <summary>What the user typed to check a pack.</summary>
    public sealed record PackCheckDraft(string CheckerName, string Organisation, string Statement, CheckOutcome Outcome);

    /// <summary>What the user typed to issue a pack.</summary>
    public sealed record PackIssueDraft(string IssueReference, string Client);

    private readonly ListBox _savedList = new() { MinHeight = 60, MaxHeight = 240 };
    private readonly Button _openSaved = new() { Content = OpenSavedCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly TextBlock _savedEmpty = Caption("Open a project to list its saved calculations.");
    private IReadOnlyList<SavedCalculation> _saved = [];

    private readonly ComboBox _packTemplatePick = new() { HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Pack template" };
    private readonly TextBlock _packTemplateNote = Caption(string.Empty);
    private readonly TextBox _packTitle = new() { Watermark = "Pack title", MaxLength = 200, FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _createPack = new() { Content = CreatePackCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly ListBox _packList = new() { MinHeight = 60, MaxHeight = 200 };
    private readonly ListBox _packMemberList = new() { MinHeight = 60, MaxHeight = 200 };
    private readonly Button _openPackMember = new() { Content = OpenPackMemberCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly TextBox _packChecker = PackInput("Checker name");
    private readonly TextBox _packOrganisation = PackInput("Checker's organisation");
    private readonly TextBox _packStatement = PackInput("Check statement");
    private readonly ComboBox _packOutcome = new() { HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = Enum.GetValues<CheckOutcome>(), SelectedIndex = 0 };
    private readonly Button _checkPack = new() { Content = CheckPackCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly TextBox _packReference = PackInput("Issue reference");
    private readonly TextBox _packClient = PackInput("Client");
    private readonly Button _issuePack = new() { Content = IssuePackCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private IReadOnlyList<PackTemplateChoice> _packTemplates = [];
    private IReadOnlyList<PackChoice> _packs = [];
    private IReadOnlyList<SavedCalculation> _packMembers = [];
    private bool _projectOpen;
    private bool _loadingPacks;

    /// <summary>Open the chosen saved calculation or pack member in the form.</summary>
    public event Action<Guid>? OpenSavedRequested;

    /// <summary>Create a pack (template id, title) from a pack template under the open project.</summary>
    public event Action<string, string>? CreatePackRequested;

    /// <summary>The user chose a pack in the list: the coordinator lists its calculations and suggests the issue reference.</summary>
    public event Action<Guid>? PackSelected;

    /// <summary>Check the pack (id) with what was typed.</summary>
    public event Action<Guid, PackCheckDraft>? CheckPackRequested;

    /// <summary>Issue the pack (id) with what was typed.</summary>
    public event Action<Guid, PackIssueDraft>? IssuePackRequested;

    /// <summary>The saved calculations listed, as shown.</summary>
    public IReadOnlyList<SavedCalculation> SavedCalculations => _saved;

    /// <summary>The packs listed, as shown.</summary>
    public IReadOnlyList<PackChoice> Packs => _packs;

    /// <summary>The selected pack's calculations, as shown.</summary>
    public IReadOnlyList<SavedCalculation> PackMembers => _packMembers;

    /// <summary>The pack templates offered, as shown.</summary>
    public IReadOnlyList<PackTemplateChoice> PackTemplates => _packTemplates;

    /// <summary>The saved calculation selected in the list, or <see langword="null"/>.</summary>
    public Guid? SelectedSavedId => SelectedOf(_savedList, _saved)?.Id;

    /// <summary>The pack selected in the list, or <see langword="null"/>.</summary>
    public Guid? SelectedPackId => _packList.SelectedIndex >= 0 && _packList.SelectedIndex < _packs.Count ? _packs[_packList.SelectedIndex].Id : null;

    /// <summary>The title the pack would be created with.</summary>
    public string PackTitle => _packTitle.Text ?? string.Empty;

    /// <summary>Whether Open is available for the selected saved calculation.</summary>
    public bool CanOpenSaved => _openSaved.IsEnabled;

    /// <summary>Whether Create pack is available.</summary>
    public bool CanCreatePack => _createPack.IsEnabled;

    /// <summary>Whether Check pack is available.</summary>
    public bool CanCheckPack => _checkPack.IsEnabled;

    /// <summary>Whether Issue pack is available.</summary>
    public bool CanIssuePack => _issuePack.IsEnabled;

    /// <summary>Whether a project is open (Create pack needs one; the lists show the open project's calculations and packs).</summary>
    public void SetProjectOpen(bool open)
    {
        _projectOpen = open;
        _savedEmpty.IsVisible = !open || _saved.Count == 0;
        _savedEmpty.Text = open ? "This project has no saved calculations yet." : "Open a project to list its saved calculations.";
        UpdateSavedAndPackButtons();
    }

    /// <summary>Lists the project's saved calculations (superseded revisions are left out by the coordinator); the selection is kept by id.</summary>
    public void SetSavedCalculations(IReadOnlyList<SavedCalculation> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var keep = SelectedSavedId;
        _saved = saved;
        _savedList.ItemsSource = ListItems(saved);
        _savedList.SelectedIndex = keep is { } id ? saved.ToList().FindIndex(x => x.Id == id) : -1;
        _savedEmpty.IsVisible = !_projectOpen || saved.Count == 0;
        UpdateSavedAndPackButtons();
    }

    /// <summary>Offers the pack templates; the selection is kept by id.</summary>
    public void SetPackTemplates(IReadOnlyList<PackTemplateChoice> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var keep = (_packTemplatePick.SelectedIndex >= 0 && _packTemplatePick.SelectedIndex < _packTemplates.Count) ? _packTemplates[_packTemplatePick.SelectedIndex].Id : null;
        _packTemplates = templates;
        _loadingPacks = true;
        _packTemplatePick.ItemsSource = templates.Select(t => t.Title).ToList();
        _packTemplatePick.SelectedIndex = keep is null ? -1 : templates.ToList().FindIndex(t => t.Id == keep);
        _loadingPacks = false;
        UpdateSavedAndPackButtons();
    }

    /// <summary>Lists the project's packs; the selection is kept by id and raises no <see cref="PackSelected"/>.</summary>
    public void SetPacks(IReadOnlyList<PackChoice> packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        var keep = SelectedPackId;
        _packs = packs;
        _loadingPacks = true;
        _packList.ItemsSource = ListItems(packs.Select(p => (p.Label, false)));
        _packList.SelectedIndex = keep is { } id ? packs.ToList().FindIndex(p => p.Id == id) : -1;
        _loadingPacks = false;

        if (_packList.SelectedIndex < 0)
            SetPackMembers([]);

        UpdateSavedAndPackButtons();
    }

    /// <summary>Lists the selected pack's calculations.</summary>
    public void SetPackMembers(IReadOnlyList<SavedCalculation> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var keep = SelectedOf(_packMemberList, _packMembers)?.Id;
        _packMembers = members;
        _packMemberList.ItemsSource = ListItems(members);
        _packMemberList.SelectedIndex = keep is { } id ? members.ToList().FindIndex(x => x.Id == id) : -1;
        UpdateSavedAndPackButtons();
    }

    /// <summary>Fills the check fields that are still empty (the checker, the organisation and a statement), as the single Check does.</summary>
    public void SuggestPackCheck(string checkerName, string organisation, string statement)
    {
        FillIfBlank(_packChecker, checkerName);
        FillIfBlank(_packOrganisation, organisation);
        FillIfBlank(_packStatement, statement);
        UpdateSavedAndPackButtons();
    }

    /// <summary>Sets the issue reference and client suggested for the pack just chosen.</summary>
    public void SuggestPackIssue(string issueReference, string client)
    {
        _packReference.Text = issueReference;
        _packClient.Text = client;
        UpdateSavedAndPackButtons();
    }

    /// <summary>Selects a saved calculation as a click on its row would.</summary>
    public void ChooseSaved(Guid id) => _savedList.SelectedIndex = _saved.ToList().FindIndex(x => x.Id == id);

    /// <summary>Selects a pack template (by its id) as the picker would.</summary>
    public void ChoosePackTemplate(string id) => _packTemplatePick.SelectedIndex = _packTemplates.ToList().FindIndex(t => t.Id == id);

    /// <summary>Types the pack title.</summary>
    public void SetPackTitle(string title) => _packTitle.Text = title;

    /// <summary>Selects a pack as a click on its row would (this raises <see cref="PackSelected"/>).</summary>
    public void ChoosePack(Guid id) => _packList.SelectedIndex = _packs.ToList().FindIndex(p => p.Id == id);

    /// <summary>Selects one of the shown pack's calculations.</summary>
    public void ChoosePackMember(Guid id) => _packMemberList.SelectedIndex = _packMembers.ToList().FindIndex(x => x.Id == id);

    /// <summary>Types the check fields.</summary>
    public void FillPackCheck(string checkerName, string organisation, string statement, CheckOutcome outcome = CheckOutcome.Accepted)
    {
        _packChecker.Text = checkerName;
        _packOrganisation.Text = organisation;
        _packStatement.Text = statement;
        _packOutcome.SelectedItem = outcome;
        UpdateSavedAndPackButtons();
    }

    /// <summary>Types the issue fields.</summary>
    public void FillPackIssue(string issueReference, string client)
    {
        _packReference.Text = issueReference;
        _packClient.Text = client;
        UpdateSavedAndPackButtons();
    }

    private void WireSavedAndPacks()
    {
        AutomationProperties.SetName(_savedList, SavedPanelName);
        AutomationProperties.SetName(_openSaved, OpenSavedCaption);
        AutomationProperties.SetName(_packTemplatePick, "Pack template");
        AutomationProperties.SetName(_packTitle, "Pack title");
        AutomationProperties.SetName(_createPack, CreatePackCaption);
        AutomationProperties.SetName(_packList, "Packs in this project");
        AutomationProperties.SetName(_packMemberList, "Calculations in the selected pack");
        AutomationProperties.SetName(_openPackMember, OpenPackMemberCaption);
        AutomationProperties.SetName(_packChecker, "Pack checker name");
        AutomationProperties.SetName(_packOrganisation, "Pack checker organisation");
        AutomationProperties.SetName(_packStatement, "Pack check statement");
        AutomationProperties.SetName(_packOutcome, "Pack check outcome");
        AutomationProperties.SetName(_checkPack, CheckPackCaption);
        AutomationProperties.SetName(_packReference, "Pack issue reference");
        AutomationProperties.SetName(_packClient, "Pack client");
        AutomationProperties.SetName(_issuePack, IssuePackCaption);

        _savedList.SelectionChanged += (_, _) => UpdateSavedAndPackButtons();
        _packMemberList.SelectionChanged += (_, _) => UpdateSavedAndPackButtons();
        _packTitle.TextChanged += (_, _) => UpdateSavedAndPackButtons();

        foreach (var box in new[] { _packChecker, _packOrganisation, _packStatement, _packReference, _packClient })
            box.TextChanged += (_, _) => UpdateSavedAndPackButtons();

        _packTemplatePick.SelectionChanged += (_, _) =>
        {
            if (_loadingPacks)
                return;

            if (_packTemplatePick.SelectedIndex is >= 0 and var i && i < _packTemplates.Count)
            {
                _packTitle.Text = _packTemplates[i].Title;
                _packTemplateNote.Text = _packTemplates[i].Description;
            }
            else
            {
                _packTemplateNote.Text = string.Empty;
            }

            UpdateSavedAndPackButtons();
        };

        _packList.SelectionChanged += (_, _) =>
        {
            UpdateSavedAndPackButtons();

            if (!_loadingPacks && SelectedPackId is { } id)
                PackSelected?.Invoke(id);
        };

        _openSaved.Click += (_, _) =>
        {
            if (SelectedSavedId is { } id)
                OpenSavedRequested?.Invoke(id);
        };
        _openPackMember.Click += (_, _) =>
        {
            if (SelectedOf(_packMemberList, _packMembers) is { } member)
                OpenSavedRequested?.Invoke(member.Id);
        };
        _createPack.Click += (_, _) =>
        {
            if (_packTemplatePick.SelectedIndex is >= 0 and var i && i < _packTemplates.Count && !string.IsNullOrWhiteSpace(_packTitle.Text))
                CreatePackRequested?.Invoke(_packTemplates[i].Id, _packTitle.Text.Trim());
        };
        _checkPack.Click += (_, _) =>
        {
            if (SelectedPackId is { } id && _packOutcome.SelectedItem is CheckOutcome outcome)
                CheckPackRequested?.Invoke(id, new PackCheckDraft(_packChecker.Text?.Trim() ?? string.Empty, _packOrganisation.Text?.Trim() ?? string.Empty, _packStatement.Text?.Trim() ?? string.Empty, outcome));
        };
        _issuePack.Click += (_, _) =>
        {
            if (SelectedPackId is { } id)
                IssuePackRequested?.Invoke(id, new PackIssueDraft(_packReference.Text?.Trim() ?? string.Empty, _packClient.Text?.Trim() ?? string.Empty));
        };

        UpdateSavedAndPackButtons();
    }

    private void UpdateSavedAndPackButtons()
    {
        var pack = SelectedPackId is { } id ? _packs.FirstOrDefault(p => p.Id == id) : null;
        _openSaved.IsEnabled = _projectOpen && SelectedSavedId is not null;
        _openPackMember.IsEnabled = _projectOpen && SelectedOf(_packMemberList, _packMembers) is not null;
        _createPack.IsEnabled = _projectOpen && _packTemplatePick.SelectedIndex >= 0 && !string.IsNullOrWhiteSpace(_packTitle.Text);
        _checkPack.IsEnabled = _projectOpen && pack is { Status: CalcInstanceStatus.Draft }
            && !string.IsNullOrWhiteSpace(_packChecker.Text) && !string.IsNullOrWhiteSpace(_packOrganisation.Text);
        _issuePack.IsEnabled = _projectOpen && pack is { Status: CalcInstanceStatus.Checked }
            && !string.IsNullOrWhiteSpace(_packReference.Text) && !string.IsNullOrWhiteSpace(_packClient.Text);
    }

    private Control BuildSavedPanel()
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm };
        panel.Children.Add(_savedEmpty);
        panel.Children.Add(_savedList);
        panel.Children.Add(_openSaved);
        AutomationProperties.SetName(panel, SavedPanelName);
        return Section(SavedPanelName, panel);
    }

    private Control BuildPacksPanel()
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm };
        panel.Children.Add(Caption("Start a pack from a saved chain, or pick one of the project's packs."));
        panel.Children.Add(_packTemplatePick);
        panel.Children.Add(_packTemplateNote);
        panel.Children.Add(_packTitle);
        panel.Children.Add(_createPack);
        panel.Children.Add(new TextBlock { Text = "Packs in this project", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody, Margin = DesignTokens.SectionMargin });
        panel.Children.Add(_packList);
        panel.Children.Add(Caption("Calculations in the selected pack"));
        panel.Children.Add(_packMemberList);
        panel.Children.Add(_openPackMember);
        panel.Children.Add(new TextBlock { Text = "Check the pack", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody, Margin = DesignTokens.SectionMargin });
        panel.Children.Add(_packChecker);
        panel.Children.Add(_packOrganisation);
        panel.Children.Add(_packStatement);
        panel.Children.Add(_packOutcome);
        panel.Children.Add(_checkPack);
        panel.Children.Add(new TextBlock { Text = "Issue the pack", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody, Margin = DesignTokens.SectionMargin });
        panel.Children.Add(_packReference);
        panel.Children.Add(_packClient);
        panel.Children.Add(_issuePack);
        AutomationProperties.SetName(panel, PacksPanelName);
        return Section(PacksPanelName, panel);
    }

    private static TextBox PackInput(string watermark) => Input(string.Empty, watermark);

    private static void FillIfBlank(TextBox box, string value)
    {
        if (string.IsNullOrWhiteSpace(box.Text))
            box.Text = value;
    }

    private static SavedCalculation? SelectedOf(ListBox list, IReadOnlyList<SavedCalculation> items) =>
        list.SelectedIndex >= 0 && list.SelectedIndex < items.Count ? items[list.SelectedIndex] : null;

    private static List<Control> ListItems(IReadOnlyList<SavedCalculation> items) => ListItems(items.Select(x => (x.Label, x.IsStale)));

    private static List<Control> ListItems(IEnumerable<(string Label, bool Stale)> rows)
    {
        var items = new List<Control>();

        foreach (var (label, stale) in rows)
        {
            var text = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody };

            if (stale)
                text.Foreground = Brushes.DarkOrange;

            AutomationProperties.SetName(text, label);
            items.Add(text);
        }

        return items;
    }
}
