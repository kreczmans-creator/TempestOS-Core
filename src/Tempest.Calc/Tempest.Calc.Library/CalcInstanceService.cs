using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Library;

/// <summary>What one run of an instance produced, with the sheet it renders to.</summary>
public sealed record CalcInstanceRun(CalcInstance Instance, CalcRunResult Result, CalcSheet Sheet, string SheetText);

/// <summary>The outcome of an action on an instance; a refusal carries one plain sentence.</summary>
public sealed record CalcActionResult(bool Succeeded, string? Reason, CalcInstance? Instance)
{
    public static CalcActionResult Refused(string reason, CalcInstance? instance = null) => new(false, reason, instance);

    public static CalcActionResult Ok(CalcInstance instance) => new(true, null, instance);
}

/// <summary>
/// The service every calculator surface talks to: create an instance,
/// update and run it, check it, issue it in one transaction with its
/// Evidence and its Calculation-register entry, revise it, and tell when
/// it has gone stale. Evidence governs everything (ADR-0165): an instance
/// moves only as its Evidence moves.
/// </summary>
public sealed class CalcInstanceService
{
    public const string RegisterKind = "Calculation";
    public const string DocumentedByRelationship = "documentedBy";
    public const string SheetFileExtension = ".pdf";

    private readonly CalcCatalogue _catalogue;
    private readonly CalcInstanceStore _store;
    private readonly IEvidenceService _evidence;
    private readonly EngineeringDomainContext _domain;
    private readonly ICalcSheetRenderer? _renderer;
    private readonly TimeProvider _time;
    private readonly ICalcEngine _engine;
    private readonly string? _applicationVersion;

    public CalcInstanceService(
        CalcCatalogue catalogue,
        CalcInstanceStore store,
        IEvidenceService evidence,
        EngineeringDomainContext domain,
        ICalcSheetRenderer? renderer = null,
        TimeProvider? time = null,
        ICalcEngine? engine = null,
        string? applicationVersion = null)
    {
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _renderer = renderer;
        _time = time ?? TimeProvider.System;
        _engine = engine ?? CalcEngine.Instance;
        _applicationVersion = applicationVersion;
    }

    public CalcCatalogue Catalogue => _catalogue;

    public CalcInstanceStore Store => _store;

    // ------------------------------------------------------------------
    // Create, update, run
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a draft instance of the current version of a template under
    /// a project, with its Evidence (classification Calculation) and its
    /// Calculation-register entry, so the register shows the calculation
    /// from its first day and the issue later only completes it.
    /// </summary>
    public async Task<CalcInstance> CreateAsync(string templateReference, Guid? projectId, string name, IEnumerable<CalcInputValue>? inputs = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var entry = _catalogue.Get(templateReference);
        var now = _time.GetUtcNow();
        var author = _domain.ResolveCurrentPrincipalId();
        var id = Guid.NewGuid();
        var cleanName = CalcSheetTextRenderer.Clean(name.Length > 200 ? name[..200] : name);

        var evidence = await _evidence.CreateAsync(projectId, EvidenceTitle(cleanName, entry.Template.Title, "A"), EvidenceClassification.Calculation, null, cancellationToken).ConfigureAwait(false);
        var register = await CreateRegisterEntryAsync(projectId, cleanName, evidence.Id, cancellationToken).ConfigureAwait(false);

        var instance = new CalcInstance(
            id, entry.Reference, entry.ContentHash, projectId, cleanName, "A",
            inputs?.ToList() ?? DefaultInputs(entry.Template), [], [], [], [],
            CalcInstanceStatus.Draft, evidence.Id, register?.Id, author, now, now);

        instance = await RunAndSnapshotAsync(entry, instance, cancellationToken).ConfigureAwait(false);
        await _store.SaveInstanceAsync(instance, cancellationToken).ConfigureAwait(false);
        return instance;
    }

    /// <summary>Replaces the inputs of a draft instance and re-runs it. Refused once the instance is checked or issued.</summary>
    public async Task<CalcActionResult> UpdateInputsAsync(Guid instanceId, IEnumerable<CalcInputValue> inputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft takes new inputs.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        var updated = instance with { Inputs = inputs.ToList(), UpdatedAt = _time.GetUtcNow() };
        updated = await RunAndSnapshotAsync(entry, updated, cancellationToken).ConfigureAwait(false);
        await _store.SaveInstanceAsync(updated, cancellationToken).ConfigureAwait(false);
        return CalcActionResult.Ok(updated);
    }

    /// <summary>Runs an instance's calculator on its stored inputs and builds its sheet. Pure apart from reading the instance.</summary>
    public async Task<CalcInstanceRun?> RunAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return instance is null ? null : await RunAsync(instance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CalcInstanceRun> RunAsync(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var entry = _catalogue.Get(instance.TemplateReference);
        var inputs = new CalcInputSet(instance.Inputs);
        var result = CalcRunner.Run(entry.Calculator, inputs, _engine, cancellationToken: cancellationToken);
        var sheet = await BuildSheetAsync(entry, instance, inputs, result, cancellationToken).ConfigureAwait(false);
        return new CalcInstanceRun(instance, result, sheet, CalcSheetTextRenderer.Render(sheet));
    }

    /// <summary>Runs the calculator for a form that has not been saved yet (live recalculation, §3.8a).</summary>
    public CalcRunResult Preview(string templateReference, CalcInputSet inputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var entry = _catalogue.Get(templateReference);
        return CalcRunner.Run(entry.Calculator, inputs, _engine, cancellationToken: cancellationToken);
    }

    // ------------------------------------------------------------------
    // Check and issue
    // ------------------------------------------------------------------

    /// <summary>Records the check on the governing Evidence and moves the instance to Checked. Blocks stop a check.</summary>
    public async Task<CalcActionResult> CheckAsync(Guid instanceId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome, CancellationToken cancellationToken = default)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft can be checked.", instance);

        if (instance.Blocks.Count > 0)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is blocked: {instance.Blocks[0].Message}", instance);

        if (instance.EvidenceId is not { } evidenceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no Evidence to check.", instance);

        var result = await _evidence.RecordCheckAsync(evidenceId, checkerName, checkerOrganisation, statement, outcome, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            return CalcActionResult.Refused(result.Reason ?? "The check was refused.", instance);

        if (outcome == CheckOutcome.Rejected)
            return CalcActionResult.Refused("The checker rejected the calculation; it stays a draft.", instance);

        var checker = result.Evidence?.Check?.CheckerIdentityId ?? checkerName;
        var checkedAt = _time.GetUtcNow();
        var updated = instance with { Status = CalcInstanceStatus.Checked, CheckedBy = checker, CheckedAt = checkedAt, UpdatedAt = checkedAt };
        await _store.SaveInstanceAsync(updated, cancellationToken).ConfigureAwait(false);
        return CalcActionResult.Ok(updated);
    }

    /// <summary>
    /// Issues a checked instance: in <b>one</b> transaction the Evidence
    /// records its issue and attaches the rendered sheet, and the instance
    /// snapshot (inputs, outputs, sheet text, template text, engine
    /// version, test results, status Issued) is written. Provenance is
    /// mandatory; a block refuses. The register entry is completed after
    /// the commit (idempotent on retry).
    /// </summary>
    public async Task<CalcActionResult> IssueAsync(Guid instanceId, string issueReference, string client, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);

        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Checked)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; check it before issuing.", instance);

        if (instance.EvidenceId is not { } evidenceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no Evidence to issue.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        var inputs = new CalcInputSet(instance.Inputs);
        var provenance = CalcInputValidator.ValidateProvenanceForIssue(entry.Template, inputs);

        if (provenance.Count > 0)
            return CalcActionResult.Refused(provenance[0].Message, instance);

        var result = CalcRunner.Run(entry.Calculator, inputs, _engine, cancellationToken: cancellationToken);

        if (result.IsBlocked)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is blocked: {result.Blocks[0].Message}", instance);

        var staleness = await StalenessAsync(instance, cancellationToken).ConfigureAwait(false);

        if (staleness.IsStale)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is stale: {staleness.Reasons[0]} Re-run it as a new revision first.", instance);

        var report = KnownAnswerRunner.Run(entry.Calculator, _time);
        CalcInstance? issued = null;

        var outcome = await _evidence.IssueInOneTransactionAsync(
            evidenceId, issueReference, instance.Revision, client,
            renderSheetAsync: async (evidence, issuedAtUtc, token) =>
            {
                var header = await HeaderAsync(entry, instance, evidence, CalcSheetStatus.Issued, issuedAtUtc, issueReference, client, token).ConfigureAwait(false);
                var footer = Footer(entry, report);
                var sheet = CalcSheetBuilder.Build(entry.Template, inputs, result, header, footer);
                var text = CalcSheetTextRenderer.Render(sheet);
                var bytes = _renderer?.Render(sheet) ?? Encoding.UTF8.GetBytes(text);
                var fileName = SheetFileName(instance, issueReference);
                var attachmentId = Guid.NewGuid();

                issued = Snapshot(instance, result) with
                {
                    Status = CalcInstanceStatus.Issued,
                    IssuedAt = issuedAtUtc,
                    UpdatedAt = issuedAtUtc,
                    IssueReference = issueReference,
                    Issue = new CalcIssueSnapshot(text, CalcTemplateHasher.CanonicalText(entry.Template, CalcTemplateHasher.Formulas(entry.Calculator)), _engine.EngineVersion, report.Passed, report.Total, report.RunAt, attachmentId.ToString("N"), _applicationVersion),
                };

                return new IssueSheetContent(fileName, _renderer is null ? "text/plain" : "application/pdf", bytes, attachmentId);
            },
            alsoWrite: (transaction, token) => CalcInstanceStore.WriteInstanceAsync(transaction, issued!, token),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!outcome.Succeeded || issued is not { } committed)
            return CalcActionResult.Refused(outcome.Reason ?? "The issue was refused.", instance);

        await CompleteRegisterEntryAsync(committed, cancellationToken).ConfigureAwait(false);
        return CalcActionResult.Ok(committed);
    }

    /// <summary>Starts a new revision of an issued instance: the old one becomes Superseded, the new one a draft with the same inputs, under fresh Evidence.</summary>
    public async Task<CalcActionResult> ReviseAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Issued)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only an issued calculation can be revised.", instance);

        var entry = _catalogue.Get(instance.TemplateId);
        var now = _time.GetUtcNow();
        var author = _domain.ResolveCurrentPrincipalId();
        var id = Guid.NewGuid();
        var nextRevision = NextRevision(instance.Revision);
        var evidence = await _evidence.CreateAsync(instance.ProjectId, EvidenceTitle(instance.Name, entry.Template.Title, nextRevision), EvidenceClassification.Calculation, null, cancellationToken).ConfigureAwait(false);
        var register = await CreateRegisterEntryAsync(instance.ProjectId, $"{instance.Name} (rev {nextRevision})", evidence.Id, cancellationToken).ConfigureAwait(false);

        var next = new CalcInstance(
            id, entry.Reference, entry.ContentHash, instance.ProjectId, instance.Name, nextRevision,
            instance.Inputs, [], [], [], [], CalcInstanceStatus.Draft, evidence.Id, register?.Id, author, now, now, PredecessorId: instance.Id);
        next = await RunAndSnapshotAsync(entry, next, cancellationToken).ConfigureAwait(false);

        // The superseded mark on the old instance changes only its status; its issued content is untouched (the store checks).
        await _store.SaveInstanceAsync(next, cancellationToken).ConfigureAwait(false);
        await _store.SaveInstanceAsync(instance with { Status = CalcInstanceStatus.Superseded, UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        return CalcActionResult.Ok(next);
    }

    // ------------------------------------------------------------------
    // Staleness
    // ------------------------------------------------------------------

    /// <summary>An instance is stale when its template's current content hash differs, or an upstream instance it takes a value from has new outputs.</summary>
    public async Task<CalcStaleness> StalenessAsync(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var reasons = new List<string>();
        var current = _catalogue.Find(instance.TemplateId);

        if (current is null)
            reasons.Add($"Template '{instance.TemplateReference}' is no longer registered.");
        else if (!string.Equals(current.Reference, instance.TemplateReference, StringComparison.Ordinal))
            reasons.Add($"Template {instance.TemplateReference} has a newer version {current.Reference}.");
        else if (!string.Equals(current.ContentHash, instance.TemplateHash, StringComparison.Ordinal))
            reasons.Add($"Template {instance.TemplateReference} content changed (hash {CalcTemplateHasher.Short(instance.TemplateHash)} → {current.ShortHash}).");

        foreach (var upstreamId in instance.UpstreamIds)
        {
            var upstream = await _store.FindInstanceAsync(upstreamId, cancellationToken).ConfigureAwait(false);

            if (upstream is null)
            {
                reasons.Add($"Upstream calculation {upstreamId} no longer exists.");
                continue;
            }

            var recorded = instance.UpstreamHashes is { } h && h.TryGetValue(upstreamId.ToString("N"), out var recordedHash) ? recordedHash : null;

            if (recorded is null || !string.Equals(recorded, upstream.OutputsHash, StringComparison.Ordinal))
                reasons.Add($"Upstream calculation '{upstream.Name}' has changed its outputs since this one took values from it.");
        }

        return reasons.Count == 0 ? CalcStaleness.Fresh : new CalcStaleness(true, reasons);
    }

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    public async Task<CalcPack> CreatePackAsync(Guid? projectId, string title, IReadOnlyList<Guid> instanceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(instanceIds);

        if (instanceIds.Count == 0 || instanceIds.Count > CalcPack.MaxInstances)
            throw new ArgumentException($"A pack holds between 1 and {CalcPack.MaxInstances} calculations.", nameof(instanceIds));

        var now = _time.GetUtcNow();
        var author = _domain.ResolveCurrentPrincipalId();
        var cleanTitle = CalcSheetTextRenderer.Clean(title.Length > 200 ? title[..200] : title);
        var evidence = await _evidence.CreateAsync(projectId, $"{cleanTitle} — calculation pack", EvidenceClassification.Calculation, null, cancellationToken).ConfigureAwait(false);
        var pack = new CalcPack(Guid.NewGuid(), projectId, cleanTitle, "A", instanceIds, [new CalcPackRevision("A", now, author, "First issue")], CalcInstanceStatus.Draft, evidence.Id, author, now, now);
        await _store.SavePackAsync(pack, cancellationToken).ConfigureAwait(false);

        foreach (var id in instanceIds)
        {
            var instance = await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false);

            if (instance is { Status: CalcInstanceStatus.Draft })
                await _store.SaveInstanceAsync(instance with { PackId = pack.Id, UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        }

        return pack;
    }

    /// <summary>Checks a pack's Evidence; every member must be a block-free draft or already checked.</summary>
    public async Task<CalcActionResult> CheckPackAsync(Guid packId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome, CancellationToken cancellationToken = default)
    {
        var pack = await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false);

        if (pack is null || pack.EvidenceId is not { } evidenceId)
            return CalcActionResult.Refused($"No pack '{packId}' exists.");

        if (pack.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Pack '{pack.Title}' is {pack.Status}.");

        foreach (var id in pack.InstanceIds)
        {
            var member = await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false);

            if (member is null)
                return CalcActionResult.Refused($"Pack member '{id}' no longer exists.");

            if (member.Blocks.Count > 0)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is blocked: {member.Blocks[0].Message}");
        }

        var result = await _evidence.RecordCheckAsync(evidenceId, checkerName, checkerOrganisation, statement, outcome, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            return CalcActionResult.Refused(result.Reason ?? "The check was refused.");

        if (outcome == CheckOutcome.Rejected)
            return CalcActionResult.Refused("The checker rejected the pack; it stays a draft.");

        var now = _time.GetUtcNow();
        await _store.SavePackAsync(pack with { Status = CalcInstanceStatus.Checked, UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        return new CalcActionResult(true, null, null);
    }

    /// <summary>Issues a pack: one Evidence, one sheet (cover with index and revision table, then every member sheet), every member snapshot written Issued — all in one transaction.</summary>
    public async Task<CalcActionResult> IssuePackAsync(Guid packId, string issueReference, string client, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);

        var pack = await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false);

        if (pack is null || pack.EvidenceId is not { } evidenceId)
            return CalcActionResult.Refused($"No pack '{packId}' exists.");

        if (pack.Status != CalcInstanceStatus.Checked)
            return CalcActionResult.Refused($"Pack '{pack.Title}' is {pack.Status}; check it before issuing.");

        var members = new List<(CalcInstance Instance, CalcCatalogueEntry Entry, CalcInputSet Inputs, CalcRunResult Result, KnownAnswerReport Report)>();

        foreach (var id in pack.InstanceIds)
        {
            var member = await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false);

            if (member is null)
                return CalcActionResult.Refused($"Pack member '{id}' no longer exists.");

            if (member.Status is CalcInstanceStatus.Superseded)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is superseded.");

            var entry = _catalogue.Find(member.TemplateReference);

            if (entry is null)
                return CalcActionResult.Refused($"Template '{member.TemplateReference}' is no longer registered.");

            var inputs = new CalcInputSet(member.Inputs);
            var provenance = CalcInputValidator.ValidateProvenanceForIssue(entry.Template, inputs);

            if (provenance.Count > 0)
                return CalcActionResult.Refused($"{member.Name}: {provenance[0].Message}");

            var result = CalcRunner.Run(entry.Calculator, inputs, _engine, cancellationToken: cancellationToken);

            if (result.IsBlocked)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is blocked: {result.Blocks[0].Message}");

            members.Add((member, entry, inputs, result, KnownAnswerRunner.Run(entry.Calculator, _time)));
        }

        var issuedMembers = new List<CalcInstance>();
        CalcPack? issuedPack = null;

        var outcome = await _evidence.IssueInOneTransactionAsync(
            evidenceId, issueReference, pack.Revision, client,
            renderSheetAsync: async (evidence, issuedAtUtc, token) =>
            {
                var sheets = new List<CalcSheet>();
                var index = new List<CalcPackIndexRow>();
                var n = 0;

                foreach (var (member, entry, inputs, result, report) in members)
                {
                    n++;
                    var header = await HeaderAsync(entry, member, evidence, CalcSheetStatus.Issued, issuedAtUtc, issueReference, client, token).ConfigureAwait(false);
                    var sheet = CalcSheetBuilder.Build(entry.Template, inputs, result, header, Footer(entry, report));
                    sheets.Add(sheet);
                    index.Add(new CalcPackIndexRow(n, member.Name, entry.Template.Title, member.Revision, "ISSUED", sheet.Governing is { } g ? $"{g.Name} {(g.Passed ? "PASS" : "FAIL")}" : null));

                    var text = CalcSheetTextRenderer.Render(sheet);
                    issuedMembers.Add(Snapshot(member, result) with
                    {
                        Status = CalcInstanceStatus.Issued,
                        IssuedAt = issuedAtUtc,
                        UpdatedAt = issuedAtUtc,
                        IssueReference = issueReference,
                        PackId = pack.Id,
                        Issue = new CalcIssueSnapshot(text, CalcTemplateHasher.CanonicalText(entry.Template, CalcTemplateHasher.Formulas(entry.Calculator)), _engine.EngineVersion, report.Passed, report.Total, report.RunAt, null, _applicationVersion),
                    });
                }

                var project = await ProjectLabelAsync(pack.ProjectId, token).ConfigureAwait(false);
                var cover = new CalcSheet(
                    new CalcSheetHeader(project, pack.Title, "Calculation pack", pack.Revision, CalcSheetStatus.Issued, evidence.AuthorIdentityId, evidence.Check?.CheckerName, evidence.CreatedAt, evidence.Check?.DateUtc, issuedAtUtc, client, issueReference),
                    "Calculation pack: the calculations listed in the index, issued together under one issue sheet.", string.Empty, [], [], [], [], [], [], [], [],
                    new CalcSheetFooter("pack", "—", _engine.EngineVersion, members.Sum(m => m.Report.Passed), members.Sum(m => m.Report.Total), issuedAtUtc, _applicationVersion),
                    pack.Revisions.Select(r => new CalcRevisionRow(r.Revision, r.Date, r.Author, r.Description)).ToList(), index);

                var bytes = _renderer?.RenderPack(cover, sheets) ?? Encoding.UTF8.GetBytes(CalcSheetTextRenderer.Render(cover) + "\n" + string.Join("\n\f\n", sheets.Select(CalcSheetTextRenderer.Render)));
                var attachmentId = Guid.NewGuid();
                issuedPack = pack with { Status = CalcInstanceStatus.Issued, IssuedAt = issuedAtUtc, UpdatedAt = issuedAtUtc, IssueReference = issueReference, SheetAttachmentId = attachmentId.ToString("N") };
                issuedMembers = issuedMembers.Select(m => m with { Issue = m.Issue! with { SheetAttachmentId = attachmentId.ToString("N") } }).ToList();
                return new IssueSheetContent($"{Safe(pack.Title)}-{Safe(issueReference)}-{Safe(pack.Revision)}{(_renderer is null ? ".txt" : SheetFileExtension)}", _renderer is null ? "text/plain" : "application/pdf", bytes, attachmentId);
            },
            alsoWrite: async (transaction, token) =>
            {
                foreach (var m in issuedMembers)
                    await CalcInstanceStore.WriteInstanceAsync(transaction, m, token).ConfigureAwait(false);

                await CalcInstanceStore.WritePackAsync(transaction, issuedPack!, token).ConfigureAwait(false);
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!outcome.Succeeded || issuedPack is null)
            return CalcActionResult.Refused(outcome.Reason ?? "The issue was refused.");

        foreach (var m in issuedMembers)
            await CompleteRegisterEntryAsync(m, cancellationToken).ConfigureAwait(false);

        return new CalcActionResult(true, null, null);
    }

    // ------------------------------------------------------------------
    // Sheets
    // ------------------------------------------------------------------

    /// <summary>The sheet an instance renders to now, with its current status watermark.</summary>
    public async Task<CalcSheet> SheetAsync(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var entry = _catalogue.Get(instance.TemplateReference);
        var inputs = new CalcInputSet(instance.Inputs);
        var result = CalcRunner.Run(entry.Calculator, inputs, _engine, cancellationToken: cancellationToken);
        return await BuildSheetAsync(entry, instance, inputs, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CalcSheet> BuildSheetAsync(CalcCatalogueEntry entry, CalcInstance instance, CalcInputSet inputs, CalcRunResult result, CancellationToken cancellationToken)
    {
        var evidence = instance.EvidenceId is { } id ? await FindEvidenceAsync(id, cancellationToken).ConfigureAwait(false) : null;
        var status = instance.Status switch
        {
            CalcInstanceStatus.Checked => CalcSheetStatus.Checked,
            CalcInstanceStatus.Issued => CalcSheetStatus.Issued,
            CalcInstanceStatus.Superseded => CalcSheetStatus.Superseded,
            _ => CalcSheetStatus.Draft,
        };
        var header = await HeaderAsync(entry, instance, evidence, status, instance.IssuedAt, instance.IssueReference, evidence?.Issue?.Client, cancellationToken).ConfigureAwait(false);
        var footer = instance.Issue is { } issue
            ? new CalcSheetFooter(entry.Reference, CalcTemplateHasher.Short(instance.TemplateHash), issue.EngineVersion, issue.KnownAnswerPassed, issue.KnownAnswerTotal, issue.TestRunAt, issue.ApplicationVersion)
            : Footer(entry, KnownAnswerRunner.Run(entry.Calculator, _time));
        return CalcSheetBuilder.Build(entry.Template, inputs, result, header, footer);
    }

    private async Task<CalcSheetHeader> HeaderAsync(CalcCatalogueEntry entry, CalcInstance instance, Evidence? evidence, CalcSheetStatus status, DateTimeOffset? issuedAt, string? issueReference, string? client, CancellationToken cancellationToken)
    {
        var project = await ProjectLabelAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        return new CalcSheetHeader(
            project, instance.Name, entry.Template.Title, instance.Revision, status,
            evidence?.AuthorIdentityId ?? instance.CreatedBy, evidence?.Check?.CheckerName ?? instance.CheckedBy,
            instance.CreatedAt, evidence?.Check?.DateUtc ?? instance.CheckedAt, issuedAt, client, issueReference);
    }

    private CalcSheetFooter Footer(CalcCatalogueEntry entry, KnownAnswerReport report) =>
        new(entry.Reference, entry.ShortHash, _engine.EngineVersion, report.Passed, report.Total, report.RunAt, _applicationVersion);

    private async Task<string> ProjectLabelAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is not { } id)
            return "—";

        var project = await _domain.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return id.ToString("N");

        if (project is IHasBusinessIdentifier named && !string.Equals(project.BusinessIdentifier, named.DisplayName, StringComparison.Ordinal))
            return $"{project.BusinessIdentifier} — {named.DisplayName}";

        return project.BusinessIdentifier;
    }

    private async Task<Evidence?> FindEvidenceAsync(Guid id, CancellationToken cancellationToken) =>
        await _domain.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false) as Evidence;

    // ------------------------------------------------------------------
    // Register
    // ------------------------------------------------------------------

    /// <summary>
    /// The Calculation-register entry (ADR-0163: Modules → Calculations →
    /// Calculation register): a <see cref="Calculation"/> object under the
    /// project, linked <c>documentedBy</c> to the governing Evidence, which
    /// in turn names it as its subject.
    /// </summary>
    private async Task<IEngineeringObject?> CreateRegisterEntryAsync(Guid? projectId, string name, Guid evidenceId, CancellationToken cancellationToken)
    {
        if (projectId is null)
            return null;

        var factory = new EngineeringObjectFactory<Calculation>(
            RegisterKind, _domain,
            (doc, rev) => new Calculation(doc, rev, _domain, identifier: null, name, EngineeringObjectMetadata.Empty));
        var created = await factory.CreateAsync($"Calculation sheet governed by evidence {evidenceId:N}.", projectId, cancellationToken).ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        if (created is IHasRelationships relationships)
            await relationships.LinkAsync(evidenceId, DocumentedByRelationship, cancellationToken).ConfigureAwait(false);

        await _evidence.SetSubjectAsync(evidenceId, created.Id, cancellationToken).ConfigureAwait(false);
        return created;
    }

    private async Task CompleteRegisterEntryAsync(CalcInstance issued, CancellationToken cancellationToken)
    {
        if (issued.RegisterObjectId is not { } id)
            return;

        if (await _domain.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false) is Calculation { Completed: false } calculation)
            await calculation.MarkCompletedAsync(DateOnly.FromDateTime((issued.IssuedAt ?? _time.GetUtcNow()).UtcDateTime), cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<CalcInstance> RunAndSnapshotAsync(CalcCatalogueEntry entry, CalcInstance instance, CancellationToken cancellationToken)
    {
        var result = CalcRunner.Run(entry.Calculator, new CalcInputSet(instance.Inputs), _engine, cancellationToken: cancellationToken);
        var snapshot = Snapshot(instance, result);
        var upstreamHashes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var upstreamId in instance.UpstreamIds)
        {
            var upstream = await _store.FindInstanceAsync(upstreamId, cancellationToken).ConfigureAwait(false);

            if (upstream?.OutputsHash is { } h)
                upstreamHashes[upstreamId.ToString("N")] = h;
        }

        return snapshot with { UpstreamHashes = upstreamHashes.Count == 0 ? null : upstreamHashes };
    }

    internal static CalcInstance Snapshot(CalcInstance instance, CalcRunResult result)
    {
        var outputs = result.Outputs.Select(o => new CalcOutputSnapshot(
            o.Symbol, o.Description, o.ValueText,
            o.Value is ScalarValue s ? s.Si : null,
            o.Value is ScalarValue s2 ? s2.DisplayUnit.Symbol : null)).ToList();

        return instance with
        {
            Outputs = outputs,
            Checks = result.Checks.Select(c => new CalcCheckSnapshot(c.Name, c.DemandText, c.CapacityText, double.IsFinite(c.Utilisation) ? c.Utilisation : 1e9, c.Passed)).ToList(),
            Warnings = result.Warnings.Select(n => new CalcNoticeSnapshot(n.RuleId, n.Severity.ToString(), n.Message)).ToList(),
            Blocks = result.Blocks.Select(n => new CalcNoticeSnapshot(n.RuleId, n.Severity.ToString(), n.Message)).ToList(),
            OutputsHash = HashOutputs(outputs),
        };
    }

    internal static string HashOutputs(IReadOnlyList<CalcOutputSnapshot> outputs)
    {
        var text = string.Join("\n", outputs.Select(o => $"{o.Symbol}={o.Si?.ToString("R", CultureInfo.InvariantCulture) ?? o.Text}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private static string EvidenceTitle(string name, string title, string revision) => $"{name} — {title} (rev {revision})";

    private static IReadOnlyList<CalcInputValue> DefaultInputs(CalcTemplate template) => CalcTemplateHasher.DefaultInputs(template).Values.ToList();

    internal static string NextRevision(string revision)
    {
        if (revision.Length == 1 && revision[0] is >= 'A' and < 'Z')
            return ((char)(revision[0] + 1)).ToString();

        if (int.TryParse(revision, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return (n + 1).ToString(CultureInfo.InvariantCulture);

        return revision + "+";
    }

    private string SheetFileName(CalcInstance instance, string issueReference) =>
        $"{Safe(instance.Name)}-{Safe(issueReference)}-{Safe(instance.Revision)}{(_renderer is null ? ".txt" : SheetFileExtension)}";

    /// <summary>A file name fragment: letters, digits, '-' and '_' only, at most 40 characters; no path separators can ever get in.</summary>
    internal static string Safe(string s)
    {
        var sb = new StringBuilder();

        foreach (var c in s)
        {
            if (char.IsAsciiLetterOrDigit(c))
                sb.Append(c);
            else if (c is ' ' or '-' or '_' or '.')
                sb.Append('-');

            if (sb.Length >= 40)
                break;
        }

        return sb.Length == 0 ? "calc" : sb.ToString();
    }
}
