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
public sealed partial class CalcInstanceService
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
    private readonly Tempest.Core.Events.IEventBus? _events;
    private readonly IReadOnlyList<CalcPackTemplate> _packTemplates;

    public CalcInstanceService(
        CalcCatalogue catalogue,
        CalcInstanceStore store,
        IEvidenceService evidence,
        EngineeringDomainContext domain,
        ICalcSheetRenderer? renderer = null,
        TimeProvider? time = null,
        ICalcEngine? engine = null,
        string? applicationVersion = null,
        Tempest.Core.Events.IEventBus? events = null,
        IReadOnlyList<CalcPackTemplate>? packTemplates = null)
    {
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _renderer = renderer;
        _time = time ?? TimeProvider.System;
        _engine = engine ?? CalcEngine.Instance;
        _applicationVersion = applicationVersion;
        _events = events;
        _packTemplates = packTemplates ?? [];
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

        // A new calculation's links (an import, a pack step's inputs) meet the same rules as any other (ADR-0168 amendment).
        if (inputs is not null)
        {
            var list = inputs.ToList();
            var probe = new CalcInstance(id, entry.Reference, entry.ContentHash, projectId, cleanName, "A", [], [], [], [], [], CalcInstanceStatus.Draft, null, null, author, now, now);

            if (await LinkProblemAsync(probe, entry.Template, list, cancellationToken).ConfigureAwait(false) is { } problem)
                throw new CalcLinkRefusedException(problem);

            inputs = list;
        }

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

        if (instance.PackId is { } packId && await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false) is { Status: not CalcInstanceStatus.Draft } pack)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' belongs to pack '{pack.Title}', which is {pack.Status}; its inputs are frozen (R-17).", instance);

        var newInputs = inputs.ToList();

        if (newInputs.Any(v => (v.Provenance is { } p && !p.IsWithinLimits) || (v.Rows?.Any(r => r?.Cells.Values.Any(c => c?.Provenance is { IsWithinLimits: false }) == true) ?? false)))
            return CalcActionResult.Refused($"A provenance reference is longer than {CalcProvenance.MaxReferenceLength} characters.", instance);

        // Every new or changed link meets the same rules as BindAsync: upstream exists, gives the output in the input's
        // dimension, closes no cycle (through any revision) and stays within the depth cap (Opus review, finding 4).
        if (await LinkProblemAsync(instance, entry.Template, newInputs, cancellationToken).ConfigureAwait(false) is { } linkProblem)
            return CalcActionResult.Refused(linkProblem, instance);

        var updated = instance with { Inputs = newInputs, UpdatedAt = _time.GetUtcNow() };
        updated = await RunAndSnapshotAsync(entry, updated, cancellationToken).ConfigureAwait(false);

        try
        {
            await _store.SaveInstanceAsync(updated, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException || ex.Message.Contains("larger than", StringComparison.Ordinal))
        {
            return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
        }

        if (!string.Equals(updated.OutputsHash, instance.OutputsHash, StringComparison.Ordinal))
            await PublishStalenessAsync(updated, $"'{updated.Name}' rev {updated.Revision} has new outputs.", cancellationToken).ConfigureAwait(false);

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
        if ((MissingText(checkerName, "A checker name") ?? MissingText(checkerOrganisation, "The checker's organisation")) is { } missing)
            return CalcActionResult.Refused(missing);

        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft can be checked.", instance);

        // Addendum A, L1: a stale calculation cannot be checked; it is updated to the current upstream revisions first.
        if (await StalenessAsync(instance, cancellationToken).ConfigureAwait(false) is { IsStale: true } stale)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is stale: {stale.Reasons[0]} Update it before checking.", instance);

        if (instance.Blocks.Count > 0)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is blocked: {instance.Blocks[0].Message}", instance);

        if (instance.EvidenceId is not { } evidenceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no Evidence to check.", instance);

        // Addendum A, §A3: E4, E5 and S6 demand an independent check whatever the Evidence setting — the signed-in checker
        // must not be the author (DA-12).
        if (_catalogue.Find(instance.TemplateReference)?.Template.IndependentCheck == true
            && _domain.ResolveCurrentPrincipalId() is var principal
            && (string.IsNullOrWhiteSpace(principal) || string.Equals(principal, instance.CreatedBy, StringComparison.Ordinal)))
            return CalcActionResult.Refused($"'{instance.Name}' needs an independent check: sign in as someone other than its author ({instance.CreatedBy}) to check it.", instance);

        // What Issue will demand is demanded here, so a checked calculation is never a dead end (Black B-13).
        var provenanceGate = ProvenanceGate(instance);

        if (provenanceGate is not null)
            return CalcActionResult.Refused(provenanceGate.Replace("before the sheet can be issued", "before it can be checked", StringComparison.Ordinal), instance);

        if (outcome == CheckOutcome.Rejected)
        {
            // A rejection is kept on the instance only: the Evidence stays Draft so the calculation can be corrected,
            // checked again and issued (Black B-12 — Core's check record always moves the Evidence to Checked).
            var when = _time.GetUtcNow();
            var note = CalcSheetTextRenderer.Clean($"Rejected by {checkerName} ({checkerOrganisation}) on {when:yyyy-MM-dd}: {statement}");
            var rejectedInstance = instance with { LastRejection = note.Length > 1_000 ? note[..1_000] : note, UpdatedAt = when };

            try
            {
                await _store.SaveInstanceAsync(rejectedInstance, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException)
            {
                return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
            }

            return CalcActionResult.Refused($"The checker rejected the calculation; it stays a draft. {statement}".TrimEnd(), rejectedInstance);
        }

        // The Evidence check record and the instance's Checked mark commit together, after a
        // transaction-time re-read of both (R-15: an Update racing this check is refused; R-19: a second check is refused).
        var checkedAt = _time.GetUtcNow();
        var updated = instance with { Status = CalcInstanceStatus.Checked, CheckedBy = checkerName, CheckedAt = checkedAt, UpdatedAt = checkedAt };
        EvidenceActionResult result;

        try
        {
            result = await _evidence.RecordCheckInOneTransactionAsync(evidenceId, checkerName, checkerOrganisation, statement, outcome,
                alsoWrite: (transaction, token) => CalcInstanceStore.WriteGuardedAsync(transaction, updated, instance.UpdatedAt, token),
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException)
        {
            return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
        }

        if (!result.Succeeded)
            return CalcActionResult.Refused(result.Reason ?? "The check was refused.", instance);

        var checker = result.Evidence?.Check?.CheckerIdentityId ?? checkerName;
        return CalcActionResult.Ok(updated with { CheckedBy = checker });
    }

    private static string? MissingText(string? value, string what) => string.IsNullOrWhiteSpace(value) ? $"{what} is needed." : null;

    /// <summary>The first provenance block Issue would raise for this instance's inputs, or <see langword="null"/>.</summary>
    private string? ProvenanceGate(CalcInstance instance)
    {
        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return null;

        return CalcInputValidator.ValidateProvenanceForIssue(entry.Template, new CalcInputSet(instance.Inputs)).FirstOrDefault()?.Message;
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
        if ((MissingText(issueReference, "An issue reference") ?? MissingText(client, "A client")) is { } missing)
            return CalcActionResult.Refused(missing);

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

        EvidenceActionResult outcome;

        try
        {
            outcome = await _evidence.IssueInOneTransactionAsync(
            evidenceId, issueReference, instance.Revision, client,
            renderSheetAsync: async (evidence, issuedAtUtc, token) =>
            {
                var header = await HeaderAsync(entry, instance, evidence, CalcSheetStatus.Issued, issuedAtUtc, issueReference, client, token).ConfigureAwait(false);
                var footer = Footer(entry, report);
                var sheet = await DescribeLinksAsync(CalcSheetBuilder.Build(entry.Template, inputs, result, header, footer), inputs, token).ConfigureAwait(false);
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
            alsoWrite: (transaction, token) => CalcInstanceStore.WriteIssuedInstanceAsync(transaction, issued!, token),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrentIssueException || ex.Message.Contains("larger than", StringComparison.Ordinal))
        {
            return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
        }

        if (!outcome.Succeeded || issued is not { } committed)
            return CalcActionResult.Refused(outcome.Reason ?? "The issue was refused.", instance);

        await CompleteRegisterEntryAsync(committed, cancellationToken).ConfigureAwait(false);
        return CalcActionResult.Ok(committed);
    }

    /// <summary>Starts a new revision of an issued instance: the old one becomes Superseded, the new one a draft with the same inputs, under fresh Evidence.</summary>
    public Task<CalcActionResult> ReviseAsync(Guid instanceId, CancellationToken cancellationToken = default) => ReviseCoreAsync(instanceId, null, cancellationToken);

    /// <summary>
    /// The revise path; with <paramref name="inputsOverride"/> (updating links, Addendum A L1) a checked calculation may
    /// be revised too, so a checked calculation whose upstream moved on is never a dead end (ADR-0166).
    /// </summary>
    private async Task<CalcActionResult> ReviseCoreAsync(Guid instanceId, IReadOnlyList<CalcInputValue>? inputsOverride, CancellationToken cancellationToken)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Issued && !(inputsOverride is not null && instance.Status == CalcInstanceStatus.Checked))
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only an issued calculation can be revised.", instance);

        var entry = _catalogue.Get(instance.TemplateId);
        var now = _time.GetUtcNow();
        var author = _domain.ResolveCurrentPrincipalId();
        var id = Guid.NewGuid();
        var nextRevision = NextRevision(instance.Revision);

        // A revision that already exists (an earlier attempt that crashed after its Evidence was created, or a concurrent Revise) is adopted, not duplicated (R-16).
        var existingSuccessor = (await _store.ListInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(i => i.PredecessorId == instance.Id && string.Equals(i.Revision, nextRevision, StringComparison.Ordinal));

        if (existingSuccessor is not null)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' already has revision {nextRevision} ({existingSuccessor.Status}).", existingSuccessor);

        // An earlier attempt that crashed after the register entry (and perhaps the Evidence record) was created but before the
        // draft was written left both behind; adopt them so the calculation can still be revised. A completed entry, or a record
        // past Draft, belongs to a live revision and refuses.
        var registerName = $"{instance.Name} (rev {nextRevision})";
        var (adoptedRegister, adoptedEvidence, adoptionRefusal) = await FindAbandonedRevisionAsync(instance.ProjectId, registerName, cancellationToken).ConfigureAwait(false);

        if (adoptionRefusal is not null)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' revision {nextRevision} {adoptionRefusal}", instance);

        var evidence = adoptedEvidence;

        if (evidence is null)
        {
            try
            {
                evidence = await _evidence.CreateAsync(instance.ProjectId, EvidenceTitle(instance.Name, entry.Template.Title, nextRevision), EvidenceClassification.Calculation, null, cancellationToken).ConfigureAwait(false);
            }
            catch (DuplicateBusinessIdentifierException ex)
            {
                return CalcActionResult.Refused($"Calculation '{instance.Name}' revision {nextRevision} is already being created: {ex.Message}", instance);
            }
        }

        IEngineeringObject? register;

        if (adoptedRegister is not null)
        {
            register = adoptedRegister;

            if (adoptedEvidence is null)
                await LinkRegisterEntryAsync(register, evidence.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            try
            {
                register = await CreateRegisterEntryAsync(instance.ProjectId, registerName, evidence.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (DuplicateBusinessIdentifierException ex)
            {
                // A concurrent Revise won the register entry between the lookup above and this write; it owns revision B.
                return CalcActionResult.Refused($"Calculation '{instance.Name}' revision {nextRevision} is already being created: {ex.Message}", instance);
            }
        }

        var next = new CalcInstance(
            id, entry.Reference, entry.ContentHash, instance.ProjectId, instance.Name, nextRevision,
            inputsOverride ?? instance.Inputs, [], [], [], [], CalcInstanceStatus.Draft, evidence.Id, register?.Id, author, now, now, PredecessorId: instance.Id);
        next = await RunAndSnapshotAsync(entry, next, cancellationToken).ConfigureAwait(false);

        // The new draft and the superseded mark on the old instance commit together; the mark changes only status and time (the store checks the rest).
        try
        {
            await _store.SaveInstancesAsync([(next, null), (instance with { Status = CalcInstanceStatus.Superseded, UpdatedAt = now }, instance.UpdatedAt)], cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException)
        {
            return CalcActionResult.Refused(ex.Message, await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) ?? instance);
        }

        await PublishStalenessAsync(instance, $"'{instance.Name}' rev {instance.Revision} is superseded by rev {next.Revision}.", cancellationToken).ConfigureAwait(false);
        return CalcActionResult.Ok(next);
    }

    // ------------------------------------------------------------------
    // Staleness
    // ------------------------------------------------------------------

    // StalenessAsync: CalcInstanceService.Linking.cs (Addendum A, L1).

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    public async Task<CalcPack> CreatePackAsync(Guid? projectId, string title, IReadOnlyList<Guid> instanceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(instanceIds);

        instanceIds = instanceIds.Distinct().ToList();

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
        if ((MissingText(checkerName, "A checker name") ?? MissingText(checkerOrganisation, "The checker's organisation")) is { } missing)
            return CalcActionResult.Refused(missing);

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

            if (ProvenanceGate(member) is { } gate)
                return CalcActionResult.Refused($"Pack member '{member.Name}': {gate.Replace("before the sheet can be issued", "before the pack can be checked", StringComparison.Ordinal)}");

            if (await StalenessAsync(member, cancellationToken).ConfigureAwait(false) is { IsStale: true } stale)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is stale: {stale.Reasons[0]} Update it before checking the pack.");

            if (_catalogue.Find(member.TemplateReference)?.Template.IndependentCheck == true
                && _domain.ResolveCurrentPrincipalId() is var principal
                && (string.IsNullOrWhiteSpace(principal) || string.Equals(principal, member.CreatedBy, StringComparison.Ordinal)))
                return CalcActionResult.Refused($"Pack member '{member.Name}' needs an independent check: sign in as someone other than its author ({member.CreatedBy}) to check the pack.");
        }

        // A rejection leaves the pack and its Evidence as drafts (Black B-12).
        if (outcome == CheckOutcome.Rejected)
            return CalcActionResult.Refused($"The checker rejected the pack; it stays a draft. {statement}".TrimEnd());

        var result = await _evidence.RecordCheckAsync(evidenceId, checkerName, checkerOrganisation, statement, outcome, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            return CalcActionResult.Refused(result.Reason ?? "The check was refused.");

        var now = _time.GetUtcNow();
        await _store.SavePackAsync(pack with { Status = CalcInstanceStatus.Checked, UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        return new CalcActionResult(true, null, null);
    }

    /// <summary>Issues a pack: one Evidence, one sheet (cover with index and revision table, then every member sheet), every member snapshot written Issued — all in one transaction.</summary>
    public async Task<CalcActionResult> IssuePackAsync(Guid packId, string issueReference, string client, CancellationToken cancellationToken = default)
    {
        if ((MissingText(issueReference, "An issue reference") ?? MissingText(client, "A client")) is { } missing)
            return CalcActionResult.Refused(missing);

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

            if (member.UpdatedAt > pack.UpdatedAt)
                return CalcActionResult.Refused($"Pack member '{member.Name}' changed after the pack was checked; check the pack again.");

            if (await StalenessAsync(member, cancellationToken).ConfigureAwait(false) is { IsStale: true } stale)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is stale: {stale.Reasons[0]}");

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

        EvidenceActionResult outcome;

        try
        {
            outcome = await _evidence.IssueInOneTransactionAsync(
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
                    var sheet = await DescribeLinksAsync(CalcSheetBuilder.Build(entry.Template, inputs, result, header, Footer(entry, report)), inputs, token).ConfigureAwait(false);
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
                    await CalcInstanceStore.WriteIssuedInstanceAsync(transaction, m, token).ConfigureAwait(false);

                await CalcInstanceStore.WritePackAsync(transaction, issuedPack!, token).ConfigureAwait(false);
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrentIssueException or CalcConcurrencyException or CalcInstanceImmutableException || ex.Message.Contains("larger than", StringComparison.Ordinal))
        {
            return CalcActionResult.Refused(ex.Message);
        }

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
        return await DescribeLinksAsync(CalcSheetBuilder.Build(entry.Template, inputs, result, header, footer), inputs, cancellationToken).ConfigureAwait(false);
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

        await LinkRegisterEntryAsync(created, evidenceId, cancellationToken).ConfigureAwait(false);
        return created;
    }

    private async Task LinkRegisterEntryAsync(IEngineeringObject register, Guid evidenceId, CancellationToken cancellationToken)
    {
        if (register is IHasRelationships relationships)
            await relationships.LinkAsync(evidenceId, DocumentedByRelationship, cancellationToken).ConfigureAwait(false);

        await _evidence.SetSubjectAsync(evidenceId, register.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the register entry an earlier Revise left behind when it crashed before its draft was written (R-16), with the
    /// Evidence record it had linked, if any. Returns a refusal reason when the entry belongs to a live revision instead.
    /// </summary>
    private async Task<(IEngineeringObject? Register, Evidence? Evidence, string? Refusal)> FindAbandonedRevisionAsync(Guid? projectId, string registerName, CancellationToken cancellationToken)
    {
        if (projectId is not { } scope || _domain.BusinessIdentifierIndex.FindConflict(RegisterKind, scope, registerName, Guid.Empty) is not { } existingId)
            return (null, null, null);

        if (await _domain.Repository.FindAsync(existingId, cancellationToken).ConfigureAwait(false) is not Calculation { Completed: false } register)
            return (null, null, "already exists in the register as an issued calculation.");

        var link = (await register.GetRelationshipsAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(r => r.SourceId == register.Id && string.Equals(r.RelationshipKind, DocumentedByRelationship, StringComparison.Ordinal));
        var evidence = link is null ? null : await FindEvidenceAsync(link.TargetId, cancellationToken).ConfigureAwait(false);

        if (evidence is not null && evidence.Status != EvidenceStatus.Draft)
            return (null, null, $"already exists with its evidence {evidence.Status}.");

        return (register, evidence, null);
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

    private static IReadOnlyList<CalcInputValue> DefaultInputs(CalcTemplate template) => CalcTemplateHasher.NewSheetInputs(template).Values.ToList();

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
    public static string Safe(string s)
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
