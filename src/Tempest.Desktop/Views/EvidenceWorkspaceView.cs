using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Evidence;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Files;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Evidence workspace (`WP 18.2A`, `ADR-0148`, `D-028`): two tabs,
/// <b>Evidence</b> — the open project's own evidence, created by picking
/// files or dropping them in, and <b>Libraries</b> — the five governed
/// reference libraries evidence cites (<see cref="LibrariesView"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> The
/// Evidence list is read fresh — one coherent
/// <see cref="EngineeringDomainContext.Repository"/> read per refresh,
/// never composed from several independent reads that could straddle a
/// commit — every time <see cref="IWorkspaceChanges.Changed"/> touches an
/// Evidence object, exactly the discipline
/// <see cref="Tempest.Desktop.Editors.ObjectEditorView"/> and
/// <see cref="Tempest.Desktop.Views.PropertyInspectorView"/> already
/// established for `WP 18.1A`. There is no manual refresh call site
/// anywhere else in this class.
/// </para>
/// <para>
/// <b>No blocking calls.</b> Every read and write here is
/// <see langword="await"/>ed; nothing in this file calls
/// <c>GetAwaiter().GetResult()</c>, <c>.Result</c> or <c>.Wait()</c> — the
/// `WP 18.1A` structural guard (<c>NoBlockingPersistenceCallsTests</c>)
/// applies to this file exactly as it does to every other one under
/// <c>src/Tempest.Desktop</c>.
/// </para>
/// </remarks>
public sealed class EvidenceWorkspaceView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IFilePicker _filePicker;
    private readonly Func<Guid?> _currentProjectId;
    private readonly Action<Guid, string> _openObject;

    private readonly ListBox _list = new() { MinHeight = 200 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly Button _createButton = new() { Content = "Create", MinHeight = DesignTokens.MinControlSize };

    private IWorkspaceChanges? _workspaceChanges;
    private readonly Control _libraries;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Collects Create's own classification value — reuses
    /// <see cref="Views.RibbonView.ParameterPrompt"/>'s own pattern
    /// (`WP 18.2A`, §2) so a test can stub it exactly as
    /// <c>CreatedObjectOpensRightUpTests</c> already stubs the Ribbon's.
    /// Left unwired (any test constructing this view directly), Create
    /// reports that honestly through <see cref="ActionCompleted"/> rather
    /// than running without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>
    /// Collects Create's own optional subject, via <see cref="SubjectPicker"/>
    /// (`WP 18.2A`, §4) — <see langword="null"/> (the default, and every
    /// test that does not need a subject) means no subject is offered.
    /// </summary>
    public Func<CancellationToken, Task<Guid?>>? SubjectPrompt { get; set; }

    /// <summary>The change feed this view reloads its own Evidence list from (`WP 18.1A`).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges;
        set
        {
            if (ReferenceEquals(_workspaceChanges, value))
                return;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed -= OnWorkspaceChanged;

            _workspaceChanges = value;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed += OnWorkspaceChanged;
        }
    }

    /// <summary>Initialises a new instance of the <see cref="EvidenceWorkspaceView"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own real object graph.</param>
    /// <param name="commandDispatcher">Dispatches <see cref="CreateEvidenceFromFilesCommand"/>.</param>
    /// <param name="filePicker">Picks the files a new piece of evidence attaches.</param>
    /// <param name="currentProjectId">The open project's own id, read fresh on every use — <see langword="null"/> when no project is open.</param>
    /// <param name="openObject">Opens an object's own editor — the same delegate the Object Editor's own relationship links and the Cockpit's Favourites card already use.</param>
    /// <param name="libraries">The Libraries tab's own content.</param>
    public EvidenceWorkspaceView(
        EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, IFilePicker filePicker,
        Func<Guid?> currentProjectId, Action<Guid, string> openObject, Control libraries)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(currentProjectId);
        ArgumentNullException.ThrowIfNull(openObject);
        ArgumentNullException.ThrowIfNull(libraries);
        _libraries = libraries;

        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _filePicker = filePicker;
        _currentProjectId = currentProjectId;
        _openObject = openObject;

        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

        _createButton.Classes.Add(ChromeStyles.Primary);
        _createButton.Click += async (_, _) => await OnCreateAsync().ConfigureAwait(true);

        _list.DoubleTapped += (_, _) =>
        {
            if (_list.SelectedItem is ListBoxItem { Tag: EvidenceRow row })
                _openObject(row.Id, Evidence.CanonicalKind);
        };

        // Dropping files onto the list does the same as Create (`WP 18.2A`,
        // §2) — the identical obsolete-but-functional IDataObject API
        // `ProjectExplorerView`'s own drag/drop already uses, narrowly
        // suppressed rather than migrated (see that class's own remarks:
        // Avalonia only warns, this platform's TreatWarningsAsErrors gate
        // turns the warning into a build break, and the typed
        // DataTransfer/DataFormat<T> replacement has no built-in file
        // format).
        DragDrop.SetAllowDrop(_list, true);
        _list.AddHandler(DragDrop.DragOverEvent, OnListDragOver);
        _list.AddHandler(DragDrop.DropEvent, OnListDrop);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = new TextBlock { Text = "Evidence", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading };
        Grid.SetColumn(title, 0);
        Grid.SetColumn(_createButton, 1);
        header.Children.Add(title);
        header.Children.Add(_createButton);

        var evidenceBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        evidenceBody.Children.Add(header);
        evidenceBody.Children.Add(_status);
        evidenceBody.Children.Add(new TextBlock { Text = "Drop files here, or Create, to record a new piece of evidence.", Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption });
        evidenceBody.Children.Add(_list);

        var evidenceTab = new TabItem { Header = "Evidence", Content = new ScrollViewer { Content = evidenceBody } };
        var librariesTab = new TabItem { Header = "Libraries", Content = libraries };

        var tabs = new TabControl();
        tabs.Items.Add(evidenceTab);
        tabs.Items.Add(librariesTab);

        Content = tabs;
    }

    /// <summary>Reloads the Evidence list for the currently open project — empty, honestly, when no project is open or the project has no evidence yet.</summary>
    public async Task RefreshAsync()
    {
        // The Libraries tab loads with the area, not on its own: nothing
        // else ever asks it to, and the first Windows run of v0.18.0 found
        // it empty for exactly that reason (the tests had refreshed it by
        // hand). Libraries do not depend on a project being open.
        if (_libraries is LibrariesView libraries)
            await libraries.RefreshAsync().ConfigureAwait(true);

        var projectId = _currentProjectId();

        if (projectId is not { } id)
        {
            _list.ItemsSource = null;
            _status.Text = "Open a project to see, and record, its evidence.";
            return;
        }

        // One coherent read of the Engineering Domain's own repository —
        // every piece of evidence under this project, at whatever state
        // the in-memory object graph holds right now (the same read
        // discipline `EvidenceNodeProvider` already established for the
        // Explorer's own Evidence area).
        var everyEvidence = await _domainContext.Repository.ListByKindAsync(Evidence.CanonicalKind).ConfigureAwait(true);
        var mine = everyEvidence
            .OfType<Evidence>()
            .Where(e => e is not IDeletable { IsDeleted: true } && e.ParentId == id)
            .OrderBy(e => e.DisplayName, StringComparer.Ordinal)
            .ToList();

        var rows = new List<EvidenceRow>(mine.Count);
        foreach (var evidence in mine)
            rows.Add(await ToRowAsync(evidence).ConfigureAwait(true));

        _list.ItemsSource = rows.Select(r => new ListBoxItem { Content = Describe(r), Tag = r }).ToList();
        _status.Text = rows.Count == 0 ? "No evidence recorded yet." : $"{rows.Count} record(s).";
    }

    private async Task<EvidenceRow> ToRowAsync(Evidence evidence)
    {
        string? subjectName = null;
        if (evidence.SubjectId is { } subjectId
            && await _domainContext.Repository.FindAsync(subjectId).ConfigureAwait(true) is { } subject)
        {
            subjectName = (subject as IHasBusinessIdentifier)?.DisplayName ?? subjectId.ToString();
        }

        return new EvidenceRow(
            evidence.Id, evidence.Classification, evidence.DisplayName, subjectName, evidence.Status,
            evidence.Check?.CheckerName, evidence.Issue?.IssueReference);
    }

    private static string Describe(EvidenceRow row) =>
        $"{row.Classification}  —  {row.Title}"
        + (row.SubjectName is null ? string.Empty : $"  •  subject: {row.SubjectName}")
        + $"  •  {row.Status}"
        + (row.Checker is null ? string.Empty : $"  •  checked by {row.Checker}")
        + (row.IssueReference is null ? string.Empty : $"  •  issued as {row.IssueReference}");

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e => e.Kind == Evidence.CanonicalKind))
            return;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ActionCompleted?.Invoke($"Refresh failed: {ex.Message}", ActionOutcome.Failed);
            }
        });
    }

    private async Task OnCreateAsync()
    {
        var projectId = _currentProjectId();
        if (projectId is null)
        {
            Report("Open a project before recording evidence.", succeeded: false);
            return;
        }

        var picked = await _filePicker.PickFilesAsync(new FilePickerRequest(
            "Pick the files this evidence records",
            AllowMultiple: true,
            FileTypeDescription: "Evidence files",
            Extensions: ["xlsx", "pdf", "docx", "dwg", "png", "jpg", "txt", "csv"])).ConfigureAwait(true);

        if (picked.Count == 0)
        {
            Report("No files were picked.", succeeded: false);
            return;
        }

        await CreateFromFilesAsync(projectId.Value, picked).ConfigureAwait(true);
    }

    /// <summary>Runs Create's own classification/subject prompt, then dispatches, for a given already-picked file set — shared by the Create button and the drop target.</summary>
    private async Task CreateFromFilesAsync(Guid projectId, IReadOnlyList<PickedFile> files)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can collect the classification here — Create is unavailable.", succeeded: false);
            return;
        }

        var descriptor = new CommandDescriptor("evidence.create-from-files", "Create Evidence", category: "Evidence");
        var parameters = new[]
        {
            new CommandParameter(
                "classification", "Classification",
                DefaultValue: nameof(EvidenceClassification.Calculation),
                AllowedValues: Enum.GetNames<EvidenceClassification>()),
        };

        var values = await ParameterPrompt(descriptor, parameters, confirmationMessage: null, CancellationToken.None).ConfigureAwait(true);
        if (values is null)
        {
            Report("Create was cancelled.", succeeded: false);
            return;
        }

        var classification = Enum.Parse<EvidenceClassification>(values["classification"], ignoreCase: true);

        Guid? subjectId = null;
        if (SubjectPrompt is not null)
            subjectId = await SubjectPrompt(CancellationToken.None).ConfigureAwait(true);

        var title = Path.GetFileNameWithoutExtension(files[0].Name);
        if (string.IsNullOrWhiteSpace(title))
            title = files[0].Name;

        var command = new CreateEvidenceFromFilesCommand(title, classification, files, projectId, subjectId);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Create failed.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Created.", succeeded: true);

        // `WP 17.9.4`'s own rule, honoured here directly: what you make
        // opens right up.
        if (result.SubjectId is { } createdId)
            _openObject(createdId, Evidence.CanonicalKind);
    }

    private void OnListDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618 // 'DragEventArgs.Data' is obsolete — see this class's own remarks on OnListDrop.
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
#pragma warning restore CS0618
    }

    // See ProjectExplorerView.OnTreeDrop's own identical remark: the
    // IDataObject/GetFiles API is obsolete in favour of a new typed
    // DataTransfer/DataFormat<T> API; the old one is still present and
    // fully functional (Avalonia only warns), and migrating is a real
    // rewrite out of this Work Package's own scope. Suppressed narrowly,
    // with no behaviour change.
#pragma warning disable CS0618 // 'DragEventArgs.Data'/'IDataObject.GetFiles' are obsolete
    private async void OnListDrop(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;

        var projectId = _currentProjectId();
        if (projectId is null)
        {
            Report("Open a project before recording evidence.", succeeded: false);
            return;
        }

        var dropped = e.Data.GetFiles()?.OfType<Avalonia.Platform.Storage.IStorageFile>().ToList();
        if (dropped is not { Count: > 0 })
            return;

        IReadOnlyList<PickedFile> files = [.. dropped.Select(ToPickedFile)];
        await CreateFromFilesAsync(projectId.Value, files).ConfigureAwait(true);
    }
#pragma warning restore CS0618

    private static PickedFile ToPickedFile(Avalonia.Platform.Storage.IStorageFile file) =>
        new(file.Name, FileContentTypes.ForFileName(file.Name), () => ReadAllBytesAsync(file));

    private static async Task<ReadOnlyMemory<byte>> ReadAllBytesAsync(Avalonia.Platform.Storage.IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync().ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }

    private sealed record EvidenceRow(
        Guid Id, EvidenceClassification Classification, string Title, string? SubjectName,
        EvidenceStatus Status, string? Checker, string? IssueReference);
}
