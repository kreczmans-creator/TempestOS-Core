using Avalonia.Controls;
using Avalonia.Input.Platform;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// `v0.26.0` (ADR-0165): the worksheet surface's own collaborator (the
/// same `ADR-0103` shape as <c>CalculationModulesCoordinator</c>). It
/// turns the view's events into calls on <see cref="CalcInstanceService"/>
/// and renders what comes back. Live recalculation is a
/// <see cref="CalcInstanceService.Preview"/> on every input change; a
/// save creates or updates the instance under the current project.
/// </summary>
public sealed class CalcSheetCoordinator : Tempest.Core.Events.IEventHandler<CalcStalenessChanged>, Tempest.Core.Events.IEventHandler<ProjectContextChangedEvent>
{
    private readonly CalcInstanceService _service;
    private readonly CalcSheetView _view;
    private readonly IProjectContext _projects;
    private readonly Func<string?> _principalId;
    private CalcInputSet? _latest;
    private bool _busy;
    private Guid? _pendingOpen;
    private bool _relistQueued;
    private Guid? _relistLinkState;
    private readonly CadImportViewModel _cad;
    private readonly ScanImportViewModel _scan;

    private readonly CalcReferenceFill? _references;
    private readonly Dictionary<string, IReadOnlyList<CalcReferenceOption>> _referenceOptions = new(StringComparer.Ordinal);

    private readonly CalcRegisterView? _register;

    public CalcSheetCoordinator(CalcInstanceService service, CalcSheetView view, IProjectContext projects, Func<string?> principalId, Tempest.Core.Events.IEventBus? events = null, Tempest.Core.Logging.ILogger? logger = null, CalcReferenceFill? references = null, CalcRegisterView? register = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(principalId);

        _service = service;
        _view = view;
        _projects = projects;
        _principalId = principalId;

        // Addendum B, W1: the CAD import panel lives under the records panel; its view model does the work.
        _cad = new CadImportViewModel(service, logger);
        var cadPanel = new CadImportPanel(_cad);
        _cad.Applied += instance =>
        {
            _view.ShowInstance(instance);
            _ = GuardedAsync(async () =>
            {
                var message = _cad.Status;
                await OpenAsync(instance.Id).ConfigureAwait(true);
                _view.ShowStatus(message);
            }, "show the imported values");
        };
        _view.InstallCadImportPanel(cadPanel);
        _view.ImportCadRequested += () => _ = GuardedAsync(ImportCadAsync, "start the CAD import");

        // Addendum B, W2: the scan deviation import panel, installed the same way.
        _scan = new ScanImportViewModel(service, logger);
        var scanPanel = new ScanImportPanel(_scan);
        _scan.Applied += instance =>
        {
            _view.ShowInstance(instance);
            _ = GuardedAsync(async () =>
            {
                var message = _scan.Status;
                await OpenAsync(instance.Id).ConfigureAwait(true);
                _view.ShowStatus(message);
            }, "show the imported values");
        };
        _view.InstallScanImportPanel(scanPanel);
        _view.ImportScanRequested += () => _ = GuardedAsync(ImportScanAsync, "start the scan import");

        _view.InputsChanged += inputs =>
        {
            _pendingOpen = null;
            Guarded(() => Recalculate(inputs), "recalculate");
        };
        _view.SaveRequested += () => _ = GuardedAsync(SaveAsync, "save that calculation");
        _view.CheckRequested += () => _ = GuardedAsync(CheckAsync, "record the check");
        _view.IssueRequested += () => _ = GuardedAsync(IssueAsync, "issue that calculation");
        _view.AttestRequested += draft => _ = GuardedAsync(() => ExclusiveAsync(() => AttestAsync(draft)), "record the attestation");
        _view.CountersignRequested += draft => _ = GuardedAsync(() => ExclusiveAsync(() => CountersignAsync(draft)), "record the second check");
        _view.ReviseRequested += () => _ = GuardedAsync(ReviseAsync, "revise that calculation");
        _view.ExportRequested += () => _ = GuardedAsync(ExportAsync, "export the inputs");
        _view.ImportRequested += () => _ = GuardedAsync(ImportAsync, "import the inputs");
        _view.LinkRequested += (input, upstream, output) => _ = GuardedAsync(() => LinkAsync(input, upstream, output), "link that input");
        _view.UpdateLinksRequested += () => _ = GuardedAsync(UpdateLinksAsync, "update the linked inputs");
        _view.AttachEvidenceRequested += () => _ = GuardedAsync(PickAndAttachAsync, "attach that report");
        _view.ApplyRecordRequested += id => _ = GuardedAsync(() => ApplyRecordAsync(id), "apply that record");
        _view.SaveRecordRequested += draft => _ = GuardedAsync(() => SaveRecordAsync(draft), "save that record");

        // Addendum C, C-3: which records the shown calculator is missing, and the way to the records screen to fill them.
        _view.TemplateShown += entry => _ = GuardedAsync(() => RefreshRequirementsAsync(entry), "list the records this calculation is missing");
        _view.OpenRecordsRequested += kind => _ = GuardedAsync(() => OpenRecordsAsync(kind), "open the records screen");
        _view.OpenSavedRequested += id => _ = GuardedAsync(() => ExclusiveAsync(() => RequestOpenAsync(id)), "open that calculation");
        _view.CreatePackRequested += (templateId, title) => _ = GuardedAsync(() => ExclusiveAsync(() => CreatePackAsync(templateId, title)), "create that pack");
        _view.PackSelected += id => _ = GuardedAsync(() => SelectPackAsync(id), "list that pack");
        _view.CheckPackRequested += (id, draft) => _ = GuardedAsync(() => ExclusiveAsync(() => CheckPackAsync(id, draft)), "check that pack");
        _view.CountersignPackRequested += (id, draft) => _ = GuardedAsync(() => ExclusiveAsync(() => CountersignPackAsync(id, draft)), "countersign that pack");
        _view.ReopenPackRequested += id => _ = GuardedAsync(() => ExclusiveAsync(() => ReopenPackAsync(id)), "reopen that pack");
        _view.IssuePackRequested += (id, draft) => _ = GuardedAsync(() => ExclusiveAsync(() => IssuePackAsync(id, draft)), "issue that pack");
        _references = references;
        _view.TemplateSelected += entry => _ = GuardedAsync(() => LoadReferenceOptionsAsync(entry), "list the released reference records");
        _view.ReferencePicked += (library, index) => _ = GuardedAsync(() => PickReferenceAsync(library, index), "fill from the reference record");
        _view.CompareRequested += () => _ = GuardedAsync(CompareAsync, "compare with the previous revision");
        _view.ArchiveSelected += id => _ = GuardedAsync(() => OpenAsync(id), "open that archived calculation");
        _register = register;
        events?.Subscribe<CalcStalenessChanged>(this);
        events?.Subscribe<ProjectContextChangedEvent>(this);
    }

    /// <summary>
    /// Lists the open project's worksheet calculations in the register; with no project open, those saved without one. The store never lists
    /// every project's calculations at once (Red R-A-12), so the project is passed to it.
    /// </summary>
    public async Task RefreshRegisterAsync()
    {
        if (_register is null)
            return;

        var project = _projects.Current;
        var instances = await _service.Store.ListInstancesAsync(project?.Id).ConfigureAwait(true);
        _register.ShowEntries(instances, project?.DisplayName, reference => _service.Catalogue.Find(reference)?.Template.Title);
    }

    /// <summary>ADR-0167 §4: the current revision against the one it superseded.</summary>
    private async Task CompareAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var (comparison, reason) = await _service.CompareWithPredecessorAsync(instance.Id).ConfigureAwait(true);

        if (comparison is null)
        {
            _view.ShowStatus(reason ?? "Nothing to compare.");
            return;
        }

        _view.ShowComparison(comparison);
        _view.ShowStatus(comparison.HasChanges
            ? $"Compared rev {comparison.After.Revision} with rev {comparison.Before.Revision}: {comparison.Rows.Count + comparison.Omitted} value(s) differ."
            : $"Compared rev {comparison.After.Revision} with rev {comparison.Before.Revision}: nothing differs.");
    }

    /// <summary>The open project changed: relist its calculations and packs.</summary>
    public Task HandleAsync(ProjectContextChangedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        QueueRelist();
        return Task.CompletedTask;
    }

    /// <summary>Addendum A, L1: something upstream changed; refresh the stale marker if the shown calculation is affected.</summary>
    public Task HandleAsync(CalcStalenessChanged @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        // The lists carry a stale mark per row, so they refresh for any change; the open sheet's own banner only when it is affected.
        QueueRelist(_view.CurrentInstance is { } shown && @event.AffectedIds.Contains(shown.Id) ? shown.Id : null);

        return Task.CompletedTask;
    }

    /// <summary>How many times the lists have been re-read (a burst of staleness events costs one).</summary>
    public int RelistCount { get; private set; }

    /// <summary>
    /// Re-reads the lists once on the next dispatcher tick however many events asked for it; when the open sheet is affected the
    /// stale banner and link choices are re-read too.
    /// </summary>
    private void QueueRelist(Guid? linkStateFor = null)
    {
        if (linkStateFor is not null)
            _relistLinkState = linkStateFor;

        if (_relistQueued)
            return;

        _relistQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = GuardedAsync(async () =>
        {
            _relistQueued = false;
            var linkState = _relistLinkState;
            _relistLinkState = null;

            if (linkState is { } id)
                await RefreshLinkStateAsync(id).ConfigureAwait(true);
            else
                await RefreshListsAsync().ConfigureAwait(true);
        }, "refresh the stale marks"));
    }

    /// <summary>Runs one open or pack action at a time with the buttons off meanwhile, so a double click does nothing.</summary>
    private async Task ExclusiveAsync(Func<Task> action)
    {
        if (_busy)
            return;

        _busy = true;
        _view.SetBusy(true);

        try
        {
            await action().ConfigureAwait(true);
        }
        finally
        {
            _busy = false;
            _view.SetBusy(false);
        }
    }

    /// <summary>
    /// Open from a list: when the form holds edits that are not saved, the first press only says so; pressing Open for the same
    /// calculation again discards them. (Any edit in the meantime asks again.)
    /// </summary>
    private async Task RequestOpenAsync(Guid id)
    {
        if (_view.HasUnsavedEdits() && _pendingOpen != id)
        {
            _pendingOpen = id;
            var name = (await _service.Store.FindInstanceAsync(id).ConfigureAwait(true))?.Name ?? "that calculation";
            _view.ShowStatus($"The form has unsaved changes. Save them first, or press Open again to discard them and open '{name}'.");
            return;
        }

        _pendingOpen = null;
        await OpenAsync(id).ConfigureAwait(true);
    }

    /// <summary>Re-reads the shown calculation's staleness and the calculations its inputs can link to.</summary>
    public async Task RefreshLinkStateAsync(Guid? instanceId = null)
    {
        var id = instanceId ?? _view.CurrentInstance?.Id;
        await RefreshListsAsync().ConfigureAwait(true);

        if (id is null || await _service.Store.FindInstanceAsync(id.Value).ConfigureAwait(true) is not { } instance)
        {
            _view.SetLinkTargets([]);
            _view.SetRecords([]);
            return;
        }

        var staleness = await _service.StalenessAsync(instance).ConfigureAwait(true);
        string? caption = null;

        if (staleness.IsStale && instance.HasLinks)
            caption = instance.Status == CalcInstanceStatus.Draft ? "Update linked inputs" : $"Update to revision {CalcInstanceService.NextRevisionLabel(instance.Revision)}";

        _view.ShowStaleness(staleness, caption);

        var targets = new List<CalcSheetView.LinkTarget>();

        foreach (var candidate in await _service.Store.ListInstancesAsync(instance.ProjectId).ConfigureAwait(true))
        {
            if (candidate.Id == instance.Id || candidate.Status == CalcInstanceStatus.Superseded || candidate.Outputs.Count == 0 || targets.Count >= 200)
                continue;

            // A template that declares output ports offers only those for linking; the others (a factor it echoes from a record) are not (Red R-A-09).
            // C-2: a port is listed under its public name (<area>.<quantity>) with its output symbol beside it.
            var declared = _service.Catalogue.Find(candidate.TemplateReference)?.Template.OutputPorts is { Count: > 0 } offered ? offered : null;
            var ports = candidate.Outputs.Where(o => o.Si is not null && (declared is null || declared.Any(p => p.Matches(o.Symbol)))).Select(o => new CalcOutputPort(o.Symbol, o.Unit ?? string.Empty, o.Description, declared?.FirstOrDefault(p => p.Matches(o.Symbol))?.Name)).ToList();
            targets.Add(new CalcSheetView.LinkTarget(candidate.Id, $"{candidate.Name} · rev {candidate.Revision} · {candidate.Status}", ports));
        }

        _view.SetLinkTargets(targets);

        var records = _service.Catalogue.Find(instance.TemplateReference) is null ? [] : await _service.RecordsForAsync(instance.TemplateReference).ConfigureAwait(true);
        _view.SetRecords([.. records.Take(200).Select(r => new CalcSheetView.RecordChoice(r.Id, $"{r.Citation} · rev {r.Revision}"))]);
    }

    /// <summary>Opens a file picker for the external report (PDF) and files it on the calculation's Evidence.</summary>
    private async Task PickAndAttachAsync()
    {
        var storage = TopLevel.GetTopLevel(_view)?.StorageProvider;

        if (storage is null || !storage.CanOpen)
        {
            _view.ShowStatus("No file picker is available here.");
            return;
        }

        var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Attach external report",
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("PDF report") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }],
        }).ConfigureAwait(true);

        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync().ConfigureAwait(true);

        if (stream.CanSeek && stream.Length > CalcInstanceService.MaxExternalEvidenceBytes)
        {
            _view.ShowStatus($"That file is larger than {CalcInstanceService.MaxExternalEvidenceBytes / (1024 * 1024)} MB.");
            return;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(true);
        await AttachExternalAsync(files[0].Name, buffer.ToArray()).ConfigureAwait(true);
    }

    /// <summary>Files <paramref name="content"/> as an external report on the shown draft and cites it from its "_ref" input.</summary>
    public async Task AttachExternalAsync(string fileName, byte[] content)
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.AttachExternalEvidenceAsync(instance.Id, fileName, "application/pdf", content, _view.ExternalReportInput).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } updated && _service.Catalogue.Find(updated.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, updated);

        await ReportAsync(result, $"'{fileName}' is filed on the calculation's Evidence and cited by its input.").ConfigureAwait(true);
    }

    /// <summary>
    /// Opens the records screen on a record kind. The composition root sets it to the Engineering area's own navigation; with none set (a worksheet
    /// shown on its own) the form says so.
    /// </summary>
    public Func<string?, Task>? OpenRecordsScreenAsync { get; set; }

    private async Task OpenRecordsAsync(string? kind)
    {
        if (OpenRecordsScreenAsync is null)
        {
            _view.ShowStatus("The records screen is not available here.");
            return;
        }

        await OpenRecordsScreenAsync(kind ?? _view.SuggestedRecordKind()).ConfigureAwait(true);
    }

    /// <summary>Reads again which of the shown calculator's records are missing (after the records screen saved some).</summary>
    public Task RefreshRequirementsAsync() => _view.SelectedTemplate is { } entry ? RefreshRequirementsAsync(entry) : Task.CompletedTask;

    private async Task RefreshRequirementsAsync(CalcCatalogueEntry entry)
    {
        var requirements = await _service.RecordImport.RequirementsAsync(entry.Reference).ConfigureAwait(true);
        _view.SetRecordRequirements(entry.Reference, requirements);
    }

    private async Task ApplyRecordAsync(Guid recordId)
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.ApplyRecordAsync(instance.Id, recordId).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } applied && _service.Catalogue.Find(applied.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, applied);

        await ReportAsync(result, "Record applied: those inputs now cite it, its edition and its revision.").ConfigureAwait(true);
    }

    /// <summary>Saves the form, stores its values of one record kind as a new reference record, then applies it so the inputs cite it.</summary>
    private async Task SaveRecordAsync(CalcSheetView.RecordDraft draft)
    {
        if (_view.CurrentInstance is not { } instance || _view.SelectedTemplate?.Template.RecordKinds?.FirstOrDefault(k => k.Kind == draft.Kind) is not { } kind)
            return;

        var form = _view.ReadForm();
        var saved = await _service.UpdateInputsAsync(instance.Id, form.Values).ConfigureAwait(true);

        if (!saved.Succeeded)
        {
            await ReportAsync(saved, string.Empty).ConfigureAwait(true);
            return;
        }

        var values = kind.Symbols.Select(form.Find).OfType<CalcInputValue>().Where(v => v.Number is not null || v.Text is not null || v.Rows is not null)
            .Select(v => new CalcDataValue(v.Symbol, v.Number, v.Unit, v.Text, v.Rows)).ToList();
        var (record, refusal) = await _service.SaveRecordFromInputsAsync(new CalcDataRecord(Guid.Empty, draft.Kind, draft.Title, 0, draft.SourceTitle, draft.Edition, draft.TableOrClause, draft.LicenceHolder, string.Empty, draft.LicensedSource, CalcDataOrigin.UserEntered, values, default), kind.Symbols.Select(form.Find).OfType<CalcInputValue>()).ConfigureAwait(true);

        if (record is null)
        {
            _view.ShowStatus("Record not saved: " + refusal);
            return;
        }

        var result = await _service.ApplyRecordAsync(instance.Id, record.Id).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } applied && _service.Catalogue.Find(applied.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, applied);

        await ReportAsync(result, $"Saved '{record.Title}' as a reference record (rev {record.Revision}{(record.LicensedSource ? ", licensed: its values never leave in an export" : string.Empty)}) and applied it.").ConfigureAwait(true);
    }

    private async Task LinkAsync(string input, Guid upstreamId, string output)
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.BindAsync(instance.Id, input, upstreamId, output).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } bound && _service.Catalogue.Find(bound.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, bound);

        await ReportAsync(result, $"Linked {input} to {output}; it is pinned to that calculation's current revision.").ConfigureAwait(true);
    }

    private async Task UpdateLinksAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.UpdateLinksAsync(instance.Id).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } updated && _service.Catalogue.Find(updated.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, updated);

        await ReportAsync(result, result.Instance is { } i && i.Id != instance.Id
            ? $"Linked inputs updated as revision {i.Revision}; revision {instance.Revision} is superseded. Check and issue it again."
            : "Linked inputs updated to the current revisions and recalculated.").ConfigureAwait(true);
    }

    private async Task ReportAsync(CalcActionResult result, string success)
    {
        Report(result, success);
        await RefreshLinkStateAsync(result.Instance?.Id).ConfigureAwait(true);
    }

    public async Task RefreshAsync()
    {
        _view.ShowCatalogue(_service.Catalogue.Current());
        var archives = await _service.Store.ListInstancesAsync().ConfigureAwait(true);
        _view.ShowArchives([.. archives.Where(i => i.IsLegacyArchive)]);
        _view.SetPackTemplates([.. (await _service.ListPackTemplatesAsync().ConfigureAwait(true)).Select(t => new CalcSheetView.PackTemplateChoice(t.Id, t.Title, t.Description))]);
        await RefreshListsAsync().ConfigureAwait(true);
    }

    private const int MaxListed = 200;

    /// <summary>
    /// Re-reads the open project's saved calculations (superseded revisions left out, each with its stale mark), its packs,
    /// and the shown pack's calculations. Nothing is listed when no project is open.
    /// </summary>
    public async Task RefreshListsAsync()
    {
        var project = _projects.Current?.Id;
        _view.SetProjectOpen(project is not null);
        _view.SetPackChecker(CheckerName());
        _view.SuggestPackCheck("Tempest Engineering", "Checked in the worksheet form.");
        RelistCount++;

        if (project is not { } projectId)
        {
            _view.SetSavedCalculations([]);
            _view.SetPacks([]);
            return;
        }

        var saved = new List<CalcSheetView.SavedCalculation>();
        var current = (await _service.Store.ListInstancesAsync(projectId).ConfigureAwait(true)).Where(i => i.Status != CalcInstanceStatus.Superseded).ToList();

        // One read of the project answers the staleness of every row (the list used to ask once per row, each reading every calculation).
        var staleness = await _service.StalenessForProjectAsync(projectId).ConfigureAwait(true);

        foreach (var instance in current.Take(MaxListed))
            saved.Add(await DescribeAsync(instance, staleness.GetValueOrDefault(instance.Id)).ConfigureAwait(true));

        _view.SetSavedCalculations(saved, current.Count > MaxListed);

        var packs = await _service.Store.ListPacksAsync(projectId).ConfigureAwait(true);
        var templates = (await _service.Store.ListInstancesAsync(projectId).ConfigureAwait(true)).ToDictionary(i => i.Id, i => i.TemplateReference);
        bool NeedsSecondCheck(CalcPack p) => p.InstanceIds.Any(id => templates.TryGetValue(id, out var reference) && _service.Catalogue.Find(reference)?.Template.RequiresSecondCheck == true);
        _view.SetPacks([.. packs.Take(MaxListed).Select(p => new CalcSheetView.PackChoice(p.Id, p.Title, p.Revision, p.Status, p.InstanceIds.Count, NeedsSecondCheck(p), p.SecondCheck is not null))], packs.Count > MaxListed);
        await ShowPackMembersAsync(_view.SelectedPackId, staleness).ConfigureAwait(true);
    }

    private async Task<CalcSheetView.SavedCalculation> DescribeAsync(CalcInstance instance, CalcStaleness? known = null)
    {
        var stale = known ?? await _service.StalenessAsync(instance).ConfigureAwait(true);
        var title = _service.Catalogue.Find(instance.TemplateReference)?.Template.Title ?? instance.TemplateReference;
        return new CalcSheetView.SavedCalculation(instance.Id, instance.Name, title, instance.Revision, instance.Status.ToString(), stale.IsStale && instance.Status != CalcInstanceStatus.Superseded);
    }

    private async Task ShowPackMembersAsync(Guid? packId, IReadOnlyDictionary<Guid, CalcStaleness>? staleness = null)
    {
        var members = new List<CalcSheetView.SavedCalculation>();

        if (packId is { } id && await _service.Store.FindPackAsync(id).ConfigureAwait(true) is { } pack)
        {
            foreach (var memberId in pack.InstanceIds)
            {
                if (await _service.Store.FindInstanceAsync(memberId).ConfigureAwait(true) is { } member)
                    members.Add(await DescribeAsync(member, staleness?.GetValueOrDefault(member.Id)).ConfigureAwait(true));
            }
        }

        _view.SetPackMembers(members);
    }

    /// <summary>A pack was chosen: list its calculations and suggest its issue reference and client.</summary>
    private async Task SelectPackAsync(Guid packId)
    {
        // Issue is off until the reads below are done and the reference and client are refilled for this pack.
        var refilled = false;

        try
        {
            _view.SetPackChecker(CheckerName());
            await ShowPackMembersAsync(packId).ConfigureAwait(true);

            if (await _service.Store.FindPackAsync(packId).ConfigureAwait(true) is { } pack && _view.SelectedPackId == packId)
            {
                _view.SuggestPackIssue($"{CalcInstanceService.Safe(pack.Title)}-{pack.Revision}", _projects.Current?.DisplayName ?? "—");
                refilled = true;
            }
        }
        finally
        {
            if (!refilled)
                _view.CancelPackIssueRefill();
        }
    }

    /// <summary>Who is signed in now, as the single Check records them.</summary>
    private string CheckerName() => _principalId() ?? "engineer";

    private async Task CreatePackAsync(string templateId, string title)
    {
        if (_projects.Current?.Id is not { } projectId)
        {
            _view.ShowStatus("Open a project first: a pack belongs to a project.");
            return;
        }

        var template = (await _service.ListPackTemplatesAsync().ConfigureAwait(true)).FirstOrDefault(t => t.Id == templateId);

        if (template is null)
        {
            _view.ShowStatus("That pack template is no longer available.");
            return;
        }

        var (made, refusal) = await _service.InstantiatePackTemplateAsync(template, projectId, title).ConfigureAwait(true);

        // Whatever was made before a refusal is already saved; the lists show it.
        await RefreshListsAsync().ConfigureAwait(true);

        if (made is null)
        {
            _view.ShowStatus(refusal ?? "Refused.");
            return;
        }

        _view.ChoosePack(made.Pack.Id);
        _view.ShowStatus($"Pack '{made.Pack.Title}' created from '{template.Title}' with {made.Pack.InstanceIds.Count} linked calculation(s). Open any of them from the list, then check and issue the pack.");
    }

    private async Task CheckPackAsync(Guid packId, CalcSheetView.PackCheckDraft draft)
    {
        // The checker is the person signed in now (a "Switch person" since the panel was last drawn counts), never typed text.
        var who = CheckerName();
        _view.SetPackChecker(who);
        var result = await _service.CheckPackAsync(packId, who, draft.Organisation, draft.Statement, draft.Outcome).ConfigureAwait(true);
        await RefreshListsAsync().ConfigureAwait(true);
        _view.ShowStatus(result.Succeeded ? $"Pack checked by {who}. Issue it when ready." : result.Reason ?? "Refused.");
    }

    /// <summary>`ADR-0173`: records the pack's second, independent check under the signed-in principal, so a pack holding a calculation that needs one can be issued.</summary>
    private async Task CountersignPackAsync(Guid packId, CalcSheetView.PackCheckDraft draft)
    {
        var who = CheckerName();
        _view.SetPackChecker(who);
        var result = await _service.CountersignPackAsync(packId, who, draft.Organisation, draft.Statement, draft.Outcome).ConfigureAwait(true);
        await RefreshListsAsync().ConfigureAwait(true);
        _view.ShowStatus(result.Succeeded
            ? draft.Outcome == CheckOutcome.Rejected ? $"Second check by {who} recorded as rejected: the pack cannot be issued; reopen it." : $"Pack countersigned by {who}. Issue it when ready."
            : result.Reason ?? "Refused.");
    }

    private async Task ReopenPackAsync(Guid packId)
    {
        var result = await _service.ReopenPackAsync(packId).ConfigureAwait(true);
        await RefreshListsAsync().ConfigureAwait(true);
        _view.ShowStatus(result.Succeeded ? "Pack reopened as a new revision. Correct what went stale, then check it again." : result.Reason ?? "Refused.");
    }

    private async Task IssuePackAsync(Guid packId, CalcSheetView.PackIssueDraft draft)
    {
        var result = await _service.IssuePackAsync(packId, draft.IssueReference, draft.Client).ConfigureAwait(true);
        await RefreshListsAsync().ConfigureAwait(true);

        // A member open in the form may have been issued with the pack: show it as it now is.
        if (result.Succeeded && _view.CurrentInstance is { } shown && await _service.Store.FindInstanceAsync(shown.Id).ConfigureAwait(true) is { } now && now.Status != shown.Status)
            await OpenAsync(now.Id).ConfigureAwait(true);

        _view.ShowStatus(result.Succeeded ? $"Pack issued as '{draft.IssueReference}'. Its sheet and every calculation are attached to the pack's Evidence." : result.Reason ?? "Refused.");
    }

    /// <summary>
    /// `ADR-0173`: opens the worksheet that manages a piece of Evidence (the Evidence editor's "Open worksheet"). Evidence a calculation owns is
    /// checked, issued and revised only there. <paramref name="showSurface"/> brings the Worksheets surface forward for the owning template first.
    /// For a pack's Evidence the pack is chosen and its first calculation opened. Returns whether a worksheet was found.
    /// </summary>
    public async Task<bool> OpenForEvidenceAsync(Guid evidenceId, Func<string, Task>? showSurface = null)
    {
        var (instance, pack) = await _service.Store.FindByEvidenceAsync(evidenceId).ConfigureAwait(true);

        if (pack is not null)
            instance = pack.InstanceIds.Count > 0 ? await _service.Store.FindInstanceAsync(pack.InstanceIds[0]).ConfigureAwait(true) : null;

        if (instance is null)
        {
            _view.ShowStatus("No worksheet owns that Evidence.");
            return false;
        }

        if (showSurface is not null)
            await showSurface(instance.TemplateReference).ConfigureAwait(true);

        await OpenAsync(instance.Id).ConfigureAwait(true);

        if (pack is not null)
            _view.ChoosePack(pack.Id);

        return true;
    }

    /// <summary>Opens an existing instance in the form (read-only when issued).</summary>
    public async Task OpenAsync(Guid instanceId)
    {
        var instance = await _service.Store.FindInstanceAsync(instanceId).ConfigureAwait(true);

        if (instance is null)
        {
            _view.ShowStatus("That calculation no longer exists.");
            return;
        }

        if (instance.IsLegacyArchive)
        {
            var archive = await _service.RunAsync(instance).ConfigureAwait(true);
            _view.ShowArchive(instance, archive.SheetText);
            return;
        }

        var entry = _service.Catalogue.Find(instance.TemplateReference);

        if (entry is null)
        {
            // A calculator the installation has switched off is withheld, not missing: say which feature and where it is enabled (`ADR-0173`).
            if (_service.Catalogue.FeatureRefusal(instance.TemplateReference) is { } refusal)
            {
                _view.ShowStatus(refusal);
                return;
            }

            // The version it was made on is not installed (ADR-0177, DC-14). A draft made on an older version of a template that has since
            // been revised (Grey GR-B-01) cannot run on the current formulas, so its inputs open as a new, unsaved sheet on the current
            // version and the draft stays as it was. A checked, issued or superseded calculation is shown as it was stored, read-only,
            // with one sentence saying what became of its version and whether a re-run on the installed version reproduces it.
            if (_service.OpenOlderVersion(instance) is { } opening)
            {
                if (opening is { Kind: CalcOlderVersionKind.DraftUpgrade, Upgrade: { } upgrade })
                {
                    _view.SelectTemplate(upgrade.Current.Reference);
                    _view.ShowTemplate(upgrade.Current, null, upgrade.Inputs);
                    _view.SuggestName($"{instance.Name} ({upgrade.Current.Template.Version})");
                    _latest = upgrade.Inputs;
                    _view.ShowStatus(upgrade.Message);
                    return;
                }

                _view.ShowStoredRecord(instance, opening.RecordText);
                _view.ShowStatus(opening.Message);
                return;
            }

            _view.ShowStatus($"Template '{instance.TemplateReference}' is not registered in this build.");
            return;
        }

        _view.SelectTemplate(entry.Reference);
        _view.ShowTemplate(entry, instance);
        _view.ShowStatus($"Opened '{instance.Name}' · rev {instance.Revision} · {instance.Status}.{(instance.IsImmutable ? " It is read-only; revise it to start a new revision." : string.Empty)}");
        await RefreshLinkStateAsync(instance.Id).ConfigureAwait(true);
    }

    private void Recalculate(CalcInputSet inputs)
    {
        if (_view.SelectedTemplate is not { } entry)
            return;

        _latest = inputs;
        _view.ShowRun(_service.Preview(entry.Reference, inputs));
    }

    private async Task SaveAsync()
    {
        if (_view.SelectedTemplate is not { } entry || _latest is null)
            return;

        if (_view.CurrentInstance is { } existing)
        {
            var updated = await _service.UpdateInputsAsync(existing.Id, _latest.Values).ConfigureAwait(true);
            await ReportAsync(updated, "Saved.").ConfigureAwait(true);
            return;
        }

        var name = _view.CalculationName ?? $"{entry.Template.Title} {DateTime.UtcNow:yyyyMMdd-HHmm}";
        var result = await _service.TryCreateAsync(entry.Reference, _projects.Current?.Id, name, _latest.Values).ConfigureAwait(true);

        // A calculation the store would refuse for size (past 4 MB), or one whose links are not allowed, is said in one sentence naming it; nothing was made.
        if (result.Instance is not { } created)
        {
            _view.ShowStatus(result.Reason ?? "That calculation was not saved.");
            return;
        }

        _view.ShowInstance(created);
        _view.ShowStatus(_projects.HasProject
            ? $"Saved as '{created.Name}' in {_projects.Current!.DisplayName}; it is in the Calculation register and has its own Evidence."
            : $"Saved as '{created.Name}' with its own Evidence (open a project first to list it in a register).");
        await RefreshLinkStateAsync(created.Id).ConfigureAwait(true);
    }

    private async Task CheckAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var who = _principalId() ?? "engineer";
        var result = await _service.CheckAsync(instance.Id, who, "Tempest Engineering", "Checked in the worksheet form.", CheckOutcome.Accepted).ConfigureAwait(true);
        await ReportAsync(result, "Checked. Issue when ready.").ConfigureAwait(true);
    }

    /// <summary>
    /// `ADR-0173`: records the competent person's attestation under the signed-in principal. The attestation is bound to the saved inputs, so unsaved
    /// edits are refused here rather than attested.
    /// </summary>
    private async Task AttestAsync(CalcSheetView.AttestDraft draft)
    {
        if (_view.CurrentInstance is not { } instance)
        {
            _view.ShowStatus("Save the calculation first, then attest it.");
            return;
        }

        if (_view.HasUnsavedEdits())
        {
            _view.ShowStatus("The form has unsaved changes. Save them first: an attestation covers the saved inputs.");
            return;
        }

        var result = await _service.AttestAsync(instance.Id, draft.Name, draft.Qualification, draft.Statement).ConfigureAwait(true);

        // The attestation is written into the sheet's inputs: show them as they now are.
        if (result.Succeeded && result.Instance is { } attested && _service.Catalogue.Find(attested.TemplateReference) is { } entry)
        {
            _view.ShowTemplate(entry, attested);
            _view.ShowStatus("Attested. The attestation is bound to these inputs; the first check can be made now.");
            await RefreshLinkStateAsync(attested.Id).ConfigureAwait(true);
            return;
        }

        Report(result, "Attested.");
    }

    /// <summary>`ADR-0173`: records the second, independent check of a checked calculation under the signed-in principal.</summary>
    private async Task CountersignAsync(CalcSheetView.CountersignDraft draft)
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.CountersignAsync(instance.Id, CheckerName(), draft.Organisation, draft.Statement, draft.Outcome).ConfigureAwait(true);
        await ReportAsync(result, draft.Outcome == CheckOutcome.Rejected ? "Second check recorded as rejected: it cannot be issued; revise it." : "Second check recorded. Issue when ready.").ConfigureAwait(true);
    }

    private async Task IssueAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var reference = $"{CalcInstanceService.Safe(instance.Name)}-{instance.Revision}";
        var client = _projects.Current?.DisplayName ?? "—";
        var result = await _service.IssueAsync(instance.Id, reference, client).ConfigureAwait(true);
        await ReportAsync(result, $"Issued as '{reference}'. The sheet is attached to its Evidence and listed under Reports.").ConfigureAwait(true);
    }

    private async Task ReviseAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.ReviseAsync(instance.Id).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } next && _service.Catalogue.Find(next.TemplateReference) is { } entry)
        {
            _view.ShowTemplate(entry, next);
            _view.ShowStatus($"Revision {next.Revision} started as a draft; revision {instance.Revision} is superseded.");
            await RefreshLinkStateAsync(next.Id).ConfigureAwait(true);
            return;
        }

        Report(result, "Revised.");
    }

    private async Task ExportAsync()
    {
        if (_view.SelectedTemplate is not { } entry || _latest is null)
            return;

        // §A5: values taken from a licensed reference record are withheld from every export.
        var csv = CalcCsv.ExportInputs(entry.Template, await _service.WithholdLicensedAsync(_latest).ConfigureAwait(true));
        var clipboard = TopLevel.GetTopLevel(_view)?.Clipboard;

        if (clipboard is null)
        {
            _view.ShowStatus("No clipboard is available here.");
            return;
        }

        await clipboard.SetTextAsync(csv).ConfigureAwait(true);
        _view.ShowStatus($"Copied {entry.Template.Inputs.Count} input row(s) as CSV to the clipboard.");
    }

    /// <summary>Lists the released records of every library the template draws on (empty lists when no catalogue is wired).</summary>
    private async Task LoadReferenceOptionsAsync(CalcCatalogueEntry entry)
    {
        _referenceOptions.Clear();

        foreach (var library in CalcReferenceFill.LibrariesOf(entry.Template))
        {
            var options = _references is null ? [] : await _references.ListReleasedAsync(library).ConfigureAwait(true);
            _referenceOptions[library] = options;
            _view.SetReferenceOptions(library, options.Select(o => o.Label).ToList());
        }
    }

    private async Task PickReferenceAsync(string library, int index)
    {
        if (_references is null || _view.SelectedTemplate is not { } entry || !_referenceOptions.TryGetValue(library, out var options) || index < 0 || index >= options.Count)
            return;

        var option = options[index];
        var fill = await _references.FillAsync(entry.Template, library, option.RecordId).ConfigureAwait(true);

        if (fill.Values.Count > 0)
            _view.ApplyReferenceFill(fill.Values);

        var filled = string.Join(", ", fill.Values.Select(v => v.Symbol));
        _view.ShowStatus(fill.Problems.Count == 0
            ? $"Filled {filled} from {option.Label} ({option.Pin})."
            : $"{(fill.Values.Count > 0 ? $"Filled {filled} from {option.Label}; " : string.Empty)}{string.Join(" ", fill.Problems.Select(p => p.Message))}");
    }

    /// <summary>Inputs come in from the clipboard as the CSV the form exports or as a worked example's <c>inputs.json</c>; the file formats' own caps and notices apply (§3.8).</summary>
    private async Task ImportAsync()
    {
        if (_view.SelectedTemplate is null)
            return;

        var clipboard = TopLevel.GetTopLevel(_view)?.Clipboard;
        var text = clipboard is null ? null : await clipboard.TryGetTextAsync().ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(text))
        {
            _view.ShowStatus("Copy the inputs (CSV or JSON) to the clipboard first.");
            return;
        }

        ImportInputsText(text);
    }

    /// <summary>The CAD import view model, for the panel and the tests.</summary>
    public CadImportViewModel CadImport => _cad;

    /// <summary>
    /// "Import CAD parameters…": opens the preview panel on the shown draft. The import applies to the saved draft (it keeps the
    /// import record that a re-import is compared with), so an unsaved form, or one holding unsaved edits, is asked to be saved first.
    /// </summary>
    public async Task ImportCadAsync()
    {
        if (_view.CurrentInstance is not { Status: CalcInstanceStatus.Draft } draft)
        {
            _view.ShowStatus("Save the calculation as a draft first; CAD parameters are imported into a saved draft.");
            return;
        }

        if (_view.HasUnsavedEdits())
        {
            _view.ShowStatus("Save or discard your edits first; the import starts from the saved draft.");
            return;
        }

        await _cad.BeginAsync(draft).ConfigureAwait(true);
        _view.ShowStatus("Choose a CAD file, then a mapping profile; the preview shows what will change before anything is applied.");
    }

    /// <summary>The scan import view model, for the panel and the tests.</summary>
    public ScanImportViewModel ScanImport => _scan;

    /// <summary>
    /// "Import scan deviations…": opens the preview panel on the shown draft. As with the CAD import, the import applies to the saved draft (it keeps
    /// the import record a re-import is compared with), so an unsaved form, or one holding unsaved edits, is asked to be saved first.
    /// </summary>
    public async Task ImportScanAsync()
    {
        if (_view.CurrentInstance is not { Status: CalcInstanceStatus.Draft } draft)
        {
            _view.ShowStatus("Save the calculation as a draft first; scan deviations are imported into a saved draft.");
            return;
        }

        if (_view.HasUnsavedEdits())
        {
            _view.ShowStatus("Save or discard your edits first; the import starts from the saved draft.");
            return;
        }

        await _scan.BeginAsync(draft).ConfigureAwait(true);
        _view.ShowStatus("Choose one or more scan tables, then a scan profile; the preview shows the statistics and what will change before anything is applied.");
    }

    /// <summary>Parses <paramref name="text"/> as JSON (a list) or CSV, reports every notice, and re-seeds the form when nothing blocks.</summary>
    public bool ImportInputsText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (_view.SelectedTemplate is not { } entry)
            return false;

        var (inputs, notices) = ParseInputs(entry.Template, text);
        var blocks = notices.Where(n => n.Severity == CalcNoticeSeverity.Block).ToList();

        if (blocks.Count > 0)
        {
            _view.ShowStatus("Nothing imported: " + string.Join(" ", blocks.Select(b => b.Message)));
            return false;
        }

        var known = inputs.Values.Count(v => entry.Template.FindInput(v.Symbol) is not null);
        _view.ApplyImportedInputs(inputs);
        _view.ShowStatus($"Imported {known} input(s){(notices.Count > 0 ? "; " + string.Join(" ", notices.Select(n => n.Message)) : ".")}");
        return true;
    }

    /// <summary>JSON when the text starts a list or an object, else CSV. Shared with the tests.</summary>
    public static (CalcInputSet Inputs, IReadOnlyList<CalcNotice> Notices) ParseInputs(CalcTemplate template, string text)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith('[') || trimmed.StartsWith('{') ? CalcInputJson.Read(trimmed, template) : CalcCsv.ImportInputs(template, text);
    }

    private void Report(CalcActionResult result, string success)
    {
        if (result.Instance is { } instance)
            _view.ShowInstance(instance);

        _view.ShowStatus(result.Succeeded ? (string.IsNullOrWhiteSpace(result.Reason) ? success : $"{success} {result.Reason}".Trim()) : result.Reason ?? "Refused.");
    }

    private void Guarded(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _view.ShowStatus($"Could not {what}: {failure.Message}");
        }
    }

    private async Task GuardedAsync(Func<Task> action, string what)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _view.ShowStatus($"Could not {what}: {failure.Message}");
        }
    }
}
