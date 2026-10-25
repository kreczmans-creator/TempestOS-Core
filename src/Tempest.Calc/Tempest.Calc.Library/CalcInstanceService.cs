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
    private readonly Tempest.Core.Identity.IPrincipalDirectory? _principals;

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
        IReadOnlyList<CalcPackTemplate>? packTemplates = null,
        Tempest.Core.Identity.IPrincipalDirectory? principals = null)
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
        _principals = principals;
    }

    public CalcCatalogue Catalogue => _catalogue;

    /// <summary>The owner this service presents to <see cref="IEvidenceService"/>: the evidence of a calculation is checked, issued and revised here and nowhere else (`ADR-0173`).</summary>
    private static readonly EvidenceOwner CalcOwner = new(Evidence.CalcWorksheetOwner);

    /// <summary>
    /// Marks the Evidence of a calculation made before Evidence could be managed as managed by the worksheet, so the generic Evidence acts
    /// refuse it from now on. Evidence created now is managed from the start.
    /// </summary>
    private async Task AdoptEvidenceAsync(Guid? evidenceId, CancellationToken cancellationToken)
    {
        if (evidenceId is { } id)
            await _evidence.ManageAsync(id, CalcOwner, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The one-off startup sweep (review N4): marks the Evidence of every saved calculation and pack as managed by its worksheet, so Evidence made
    /// before the marker existed is closed to the generic Evidence acts at once, not at its next check or issue (which a switched-off or blocked
    /// calculation never reaches). Idempotent; Evidence already managed is left alone. Returns how many Evidence records it looked at.
    /// </summary>
    public async Task<int> AdoptAllEvidenceAsync(CancellationToken cancellationToken = default)
    {
        var ids = await _store.ListEvidenceIdsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var id in ids)
            await AdoptEvidenceAsync(id, cancellationToken).ConfigureAwait(false);

        return ids.Count;
    }

    /// <summary>
    /// Stands in for the signed-in principal when set. Internal, for the tests, which have no sign-in to switch between two people;
    /// the product always resolves the principal from the domain context.
    /// </summary>
    internal Func<string?>? PrincipalOverride { get; set; }

    private string CurrentPrincipal() => PrincipalOverride?.Invoke() ?? _domain.ResolveCurrentPrincipalId();

    private static bool IsKnownPerson(string? principal) => !string.IsNullOrWhiteSpace(principal) && !string.Equals(principal, Tempest.Core.EngineeringData.EngineeringDocumentStore.UnknownAuthorPrincipalId, StringComparison.Ordinal);

    /// <summary>The principals listed with <paramref name="more"/> added, without blanks, the "unknown" sentinel or repeats; <see langword="null"/> when none is left.</summary>
    private static IReadOnlyList<string>? WithAuthors(IReadOnlyList<string>? list, params string?[] more)
    {
        var all = (list ?? []).Concat(more).Where(IsKnownPerson).Select(a => a!).Distinct(StringComparer.Ordinal).ToList();
        return all.Count == 0 ? null : all;
    }

    public CalcInstanceStore Store => _store;

    private MaterialLibrary? _materials;

    /// <summary>The material property library (Addendum B, W3), built on the reference records of this service.</summary>
    public MaterialLibrary Materials => _materials ??= new MaterialLibrary(this);

    internal TimeProvider Time => _time;

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
        var author = CurrentPrincipal();
        var id = Guid.NewGuid();
        var cleanName = CalcSheetTextRenderer.Clean(name.Length > 200 ? name[..200] : name);

        // A new calculation's links (an import, a pack step's inputs) meet the same rules as any other (ADR-0168 amendment).
        if (inputs is not null)
        {
            var list = inputs.ToList();

            if (ProvenanceTooLong(list))
                throw new CalcLinkRefusedException($"A provenance reference is longer than {CalcProvenance.MaxReferenceLength} characters.");

            var probe = new CalcInstance(id, entry.Reference, entry.ContentHash, projectId, cleanName, "A", [], [], [], [], [], CalcInstanceStatus.Draft, null, null, author, now, now);

            if (await LinkProblemAsync(probe, entry.Template, list, cancellationToken).ConfigureAwait(false) is { } problem)
                throw new CalcLinkRefusedException(problem);

            // A new calculation has no Evidence yet, so nothing it cites can be an attachment of it.
            if (await UnresolvedEvidenceAsync(null, null, list, cancellationToken).ConfigureAwait(false) is { } unresolved)
                throw new CalcLinkRefusedException(unresolved);

            inputs = list;
        }

        var evidence = await _evidence.CreateManagedAsync(projectId, EvidenceTitle(cleanName, entry.Template.Title, "A"), EvidenceClassification.Calculation, Evidence.CalcWorksheetOwner, null, cancellationToken).ConfigureAwait(false);
        var register = await CreateRegisterEntryAsync(projectId, cleanName, evidence.Id, cancellationToken).ConfigureAwait(false);

        var instance = new CalcInstance(
            id, entry.Reference, entry.ContentHash, projectId, cleanName, "A",
            inputs?.ToList() ?? DefaultInputs(entry.Template), [], [], [], [],
            CalcInstanceStatus.Draft, evidence.Id, register?.Id, author, now, now, EditedBy: WithAuthors(null, author));

        instance = await RunAndSnapshotAsync(entry, instance, cancellationToken).ConfigureAwait(false);
        await _store.SaveInstanceAsync(instance, cancellationToken).ConfigureAwait(false);
        return instance;
    }

    /// <summary>Replaces the inputs of a draft instance and re-runs it. Refused once the instance is checked or issued.</summary>
    public Task<CalcActionResult> UpdateInputsAsync(Guid instanceId, IEnumerable<CalcInputValue> inputs, CancellationToken cancellationToken = default) =>
        UpdateInputsCoreAsync(instanceId, inputs, null, false, cancellationToken);

    /// <summary>
    /// As <see cref="UpdateInputsAsync(Guid, IEnumerable{CalcInputValue}, CancellationToken)"/>, for inputs built from a read made at
    /// <paramref name="expectedUpdatedAt"/>: refused, not overwritten, if the calculation was changed in between.
    /// </summary>
    public Task<CalcActionResult> UpdateInputsAsync(Guid instanceId, IEnumerable<CalcInputValue> inputs, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken = default) =>
        UpdateInputsCoreAsync(instanceId, inputs, expectedUpdatedAt, false, cancellationToken);

    /// <param name="repin">
    /// The change is only a link re-pinned to its upstream's current revision (Update to revision n): the person pressing it did not
    /// design anything, so they are recorded in <see cref="CalcInstance.RepinnedBy"/> and not among the authors (Purple P-A-03).
    /// </param>
    private async Task<CalcActionResult> UpdateInputsCoreAsync(Guid instanceId, IEnumerable<CalcInputValue> inputs, DateTimeOffset? expectedUpdatedAt, bool repin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        if (expectedUpdatedAt is { } expected && instance.UpdatedAt != expected)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' was changed after these inputs were read from it; reopen it and try again.", instance);

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft takes new inputs.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        if (instance.PackId is { } packId && await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false) is { Status: not CalcInstanceStatus.Draft } pack)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' belongs to pack '{pack.Title}', which is {pack.Status}; its inputs are frozen.", instance);

        var newInputs = inputs.ToList();

        if (ProvenanceTooLong(newInputs))
            return CalcActionResult.Refused($"A provenance reference is longer than {CalcProvenance.MaxReferenceLength} characters.", instance);

        // Every new or changed link meets the same rules as BindAsync: upstream exists, gives the output in the input's
        // dimension, closes no cycle (through any revision) and stays within the depth cap (Opus review, finding 4).
        if (await LinkProblemAsync(instance, entry.Template, newInputs, cancellationToken).ConfigureAwait(false) is { } linkProblem)
            return CalcActionResult.Refused(linkProblem, instance);

        if (await UnresolvedEvidenceAsync(instance.EvidenceId, instance.PredecessorId, newInputs, cancellationToken).ConfigureAwait(false) is { } unresolvedEvidence)
            return CalcActionResult.Refused(unresolvedEvidence, instance);

        var updated = instance with { Inputs = newInputs, UpdatedAt = _time.GetUtcNow(), EditedBy = repin ? instance.EditedBy : WithAuthors(instance.EditedBy, CurrentPrincipal()), RepinnedBy = repin ? WithAuthors(instance.RepinnedBy, CurrentPrincipal()) : instance.RepinnedBy };
        updated = await RunAndSnapshotAsync(entry, updated, cancellationToken).ConfigureAwait(false);

        // ADR-0173: an attestation is of the inputs as they were; any change to them voids it.
        if (updated.Attestation is { } attested && !string.Equals(attested.InputsHash, HashInputs(updated.Inputs), StringComparison.Ordinal))
            updated = updated with { Attestation = null };

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

    /// <summary>Whether any input, or any table cell, carries a provenance longer than the caps.</summary>
    private static bool ProvenanceTooLong(IEnumerable<CalcInputValue> inputs) =>
        inputs.Any(v => (v.Provenance is { } p && !p.IsWithinLimits)
            || (v.Rows?.Any(r => r?.Cells.Values.Any(c => c?.Provenance is { IsWithinLimits: false }) == true) ?? false));

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

        // ADR-0173: a calculator that is switched off cannot be checked, however the instance came to exist.
        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        // Addendum A, L1: a stale calculation cannot be checked; it is updated to the current upstream revisions first.
        if (await StalenessAsync(instance, cancellationToken).ConfigureAwait(false) is { IsStale: true } stale)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is stale: {stale.Reasons[0]} Update it before checking.", instance);

        // Red R-A-02: a draft upstream is edited in place under the same revision letter, so what is pinned to it can still change.
        if (await DraftUpstreamAsync(instance, null, cancellationToken).ConfigureAwait(false) is { } draftUpstream)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' {draftUpstream}", instance);

        if (instance.Blocks.Count > 0)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is blocked: {instance.Blocks[0].Message}", instance);

        if (instance.EvidenceId is not { } evidenceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no Evidence to check.", instance);

        await AdoptEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);

        // Addendum A, §A3: E4, E5 and S6 demand an independent check whatever the Evidence setting — the signed-in checker
        // must not be the author (DA-12).
        if (IndependenceRefusal(instance, $"'{instance.Name}'", "check it") is { } notIndependent)
            return CalcActionResult.Refused(notIndependent, instance);

        // What Issue will demand is demanded here, so a checked calculation is never a dead end (Black B-13).
        var provenanceGate = ProvenanceGate(instance);

        if (provenanceGate is not null)
            return CalcActionResult.Refused(provenanceGate.Replace("before the sheet can be issued", "before it can be checked", StringComparison.Ordinal), instance);

        // ADR-0173: a template that demands a competent-person attestation is not checked without one (Issue would refuse it).
        var checkedTemplate = _catalogue.Find(instance.TemplateReference)?.Template;

        if (outcome != CheckOutcome.Rejected && checkedTemplate is not null)
        {
            if (CheckGovernanceRefusal(instance, checkedTemplate) is { } attestationRefusal)
                return CalcActionResult.Refused(attestationRefusal, instance);

            if (AttesterIsSigningRefusal(instance, checkedTemplate, checkerName, $"Calculation '{instance.Name}'") is { } attesterChecks)
                return CalcActionResult.Refused(attesterChecks, instance);
        }

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

        // ADR-0173: the second check must be by a different person, so the first checker's sign-in is kept (only where a second check is asked for).
        if (checkedTemplate?.RequiresSecondCheck == true)
            updated = updated with { CheckerPrincipal = CurrentPrincipal() };

        EvidenceActionResult result;

        try
        {
            result = await _evidence.RecordCheckInOneTransactionAsync(evidenceId, checkerName, checkerOrganisation, statement, outcome,
                alsoWrite: (transaction, token) => CalcInstanceStore.WriteGuardedAsync(transaction, updated, instance.UpdatedAt, token),
                cancellationToken, owner: CalcOwner).ConfigureAwait(false);
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

    /// <summary>
    /// Why the signed-in person may not check a calculation that demands an independent check (E4, E5, S6): nobody is signed in (the
    /// "unknown" sentinel is not a person — Red R-A-11), or they created it or set or changed any of its inputs (Red R-A-05).
    /// </summary>
    private string? IndependenceRefusal(CalcInstance instance, string subject, string action)
    {
        if (_catalogue.Find(instance.TemplateReference)?.Template.IndependentCheck != true)
            return null;

        var principal = CurrentPrincipal();

        if (!IsKnownPerson(principal))
            return $"{subject} needs an independent check: nobody is signed in, so there is no one to {action}.";

        var authors = instance.Authors;

        if (!authors.Contains(principal, StringComparer.Ordinal))
            return null;

        var who = authors.Count == 1 ? $"its author ({authors[0]})" : $"anyone who set up or changed its inputs ({string.Join(", ", authors)})";
        return $"{subject} needs an independent check: sign in as someone other than {who} to {action}.";
    }

    /// <summary>
    /// What stops a calculation being checked or issued because it takes a value from a draft, or <see langword="null"/>. A draft is
    /// edited in place under its revision letter, so a link pinned to it can still change; a draft that is issued in the same pack
    /// (<paramref name="together"/>) is part of what is being issued and is allowed (Red R-A-02).
    /// </summary>
    private async Task<string?> DraftUpstreamAsync(CalcInstance instance, IReadOnlySet<Guid>? together, CancellationToken cancellationToken)
    {
        foreach (var id in instance.UpstreamIds)
        {
            if (together?.Contains(id) == true)
                continue;

            if (await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false) is { Status: CalcInstanceStatus.Draft } upstream)
                return $"takes a value from '{upstream.Name}' rev {upstream.Revision}, which is still a draft and can change; check and issue '{upstream.Name}' first, or issue both in one pack.";
        }

        return null;
    }

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

        // ADR-0173: first, whatever else is true of it, a calculation of a switched-off calculator is never issued.
        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        if (instance.Status != CalcInstanceStatus.Checked)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; check it before issuing.", instance);

        if (instance.EvidenceId is not { } evidenceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no Evidence to issue.", instance);

        await AdoptEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        // ADR-0173: the competent-person attestation and the second independent check a template can demand.
        if (IssueGovernanceRefusal(instance, entry.Template) is { } governance)
            return CalcActionResult.Refused(governance, instance);

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

        if (await DraftUpstreamAsync(instance, null, cancellationToken).ConfigureAwait(false) is { } draftUpstream)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' {draftUpstream}", instance);

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
                var sheet = await FinishSheetAsync(entry, inputs, result, header, footer, token).ConfigureAwait(false);
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
                    Issue = new CalcIssueSnapshot(text, CalcTemplateHasher.CanonicalText(entry.Template, CalcTemplateHasher.Formulas(entry.Calculator)), _engine.EngineVersion, report.Passed, report.Total, report.RunAt, attachmentId.ToString("N"), _applicationVersion, FeatureNoteFor(entry.Template)),
                };

                return new IssueSheetContent(fileName, _renderer is null ? "text/plain" : "application/pdf", bytes, attachmentId);
            },
            alsoWrite: (transaction, token) => CalcInstanceStore.WriteIssuedInstanceAsync(transaction, issued!, token),
            cancellationToken: cancellationToken,
            owner: CalcOwner).ConfigureAwait(false);
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

    /// <summary>The same, for <see cref="UpdateLinksAsync"/>: only links were re-pinned, so the person pressing it is not an author.</summary>
    private Task<CalcActionResult> ReviseRepinnedAsync(Guid instanceId, IReadOnlyList<CalcInputValue> inputs, CancellationToken cancellationToken) => ReviseCoreAsync(instanceId, inputs, cancellationToken, repin: true);

    /// <summary>
    /// The revise path; with <paramref name="inputsOverride"/> (updating links, Addendum A L1) a checked calculation may
    /// be revised too, so a checked calculation whose upstream moved on is never a dead end (ADR-0166).
    /// </summary>
    private async Task<CalcActionResult> ReviseCoreAsync(Guid instanceId, IReadOnlyList<CalcInputValue>? inputsOverride, CancellationToken cancellationToken, bool repin = false)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        // A checked calculation that has gone stale (a record was revised, a template changed, an upstream moved) is never a dead end:
        // it can be revised, which starts a draft to correct and check again (Red R-A-04, ADR-0166).
        if (instance.Status != CalcInstanceStatus.Issued
            && !(instance.Status == CalcInstanceStatus.Checked && (inputsOverride is not null || instance.SecondCheck is { Accepted: false } || (await StalenessAsync(instance, cancellationToken).ConfigureAwait(false)).IsStale)))
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only an issued calculation, or a checked one that has gone stale, can be revised.", instance);

        var entry = _catalogue.Get(instance.TemplateId);
        var now = _time.GetUtcNow();
        var author = CurrentPrincipal();
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
                evidence = await _evidence.CreateManagedAsync(instance.ProjectId, EvidenceTitle(instance.Name, entry.Template.Title, nextRevision), EvidenceClassification.Calculation, Evidence.CalcWorksheetOwner, null, cancellationToken).ConfigureAwait(false);
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

        // A revision starts unattested (review N6): the earlier attestation's inputs, with their "Attested by X on date" source, are not carried, so
        // the draft's sheet does not read as attested. A fresh AttestAsync writes them again.
        var carried = entry.Template.RequiresAttestation
            ? (inputsOverride ?? instance.Inputs).Where(i => !CalcAttestation.Symbols.Contains(i.Symbol, StringComparer.Ordinal)).ToList()
            : inputsOverride ?? instance.Inputs;

        var next = new CalcInstance(
            id, entry.Reference, entry.ContentHash, instance.ProjectId, instance.Name, nextRevision,
            carried, [], [], [], [], CalcInstanceStatus.Draft, evidence.Id, register?.Id, repin ? instance.CreatedBy : author, now, now, PredecessorId: instance.Id,
            EditedBy: repin ? WithAuthors(instance.EditedBy, instance.CreatedBy) : WithAuthors(instance.EditedBy, instance.CreatedBy, author),
            RepinnedBy: repin ? WithAuthors(instance.RepinnedBy, author) : instance.RepinnedBy);
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

        // A pack holds calculations of its own project: one from another project (or none) would be issued under this project's Evidence (Red R-A-12).
        var members = new List<CalcInstance>();

        foreach (var id in instanceIds)
        {
            if (await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false) is not { } found)
                continue;

            if (found.ProjectId != projectId)
                throw new ArgumentException($"Calculation '{found.Name}' belongs to another project; a pack holds the calculations of one project.", nameof(instanceIds));

            if (FeatureRefusal(found.TemplateReference) is { } switchedOff)
                throw new CalcFeatureDisabledException(switchedOff);

            members.Add(found);
        }

        var now = _time.GetUtcNow();
        var author = CurrentPrincipal();
        var cleanTitle = CalcSheetTextRenderer.Clean(title.Length > 200 ? title[..200] : title);
        var evidence = await _evidence.CreateManagedAsync(projectId, $"{cleanTitle} — calculation pack", EvidenceClassification.Calculation, Evidence.CalcWorksheetOwner, null, cancellationToken).ConfigureAwait(false);
        var pack = new CalcPack(Guid.NewGuid(), projectId, cleanTitle, "A", instanceIds, [new CalcPackRevision("A", now, author, "First issue")], CalcInstanceStatus.Draft, evidence.Id, author, now, now);
        await _store.SavePackAsync(pack, cancellationToken).ConfigureAwait(false);

        foreach (var member in members)
        {
            var instance = await _store.FindInstanceAsync(member.Id, cancellationToken).ConfigureAwait(false);

            if (instance is { Status: CalcInstanceStatus.Draft })
                await _store.SaveInstanceAsync(instance with { PackId = pack.Id, UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        }

        return pack;
    }

    /// <summary>Loads a pack's members, or the refusal for the first that no longer exists.</summary>
    private async Task<(List<CalcInstance> Members, string? Refusal)> PackMembersAsync(CalcPack pack, CancellationToken cancellationToken)
    {
        var members = new List<CalcInstance>();

        foreach (var id in pack.InstanceIds)
        {
            if (await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false) is not { } member)
                return (members, $"Pack member '{id}' no longer exists.");

            members.Add(member);
        }

        return (members, null);
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

        await AdoptEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);

        var (members, gone) = await PackMembersAsync(pack, cancellationToken).ConfigureAwait(false);

        if (gone is not null)
            return CalcActionResult.Refused(gone);

        // One read of the project answers the staleness of every member (Red R-A-07).
        var staleness = await StalenessForAsync(pack.ProjectId, members, cancellationToken).ConfigureAwait(false);
        var together = pack.InstanceIds.ToHashSet();

        foreach (var member in members)
        {
            // ADR-0173: a member of a switched-off calculator holds the whole pack back, whatever else is true of it.
            if (FeatureRefusal(member.TemplateReference) is { } switchedOff)
                return CalcActionResult.Refused($"Pack member '{member.Name}': {switchedOff}");

            if (member.Blocks.Count > 0)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is blocked: {member.Blocks[0].Message}");

            if (ProvenanceGate(member) is { } gate)
                return CalcActionResult.Refused($"Pack member '{member.Name}': {gate.Replace("before the sheet can be issued", "before the pack can be checked", StringComparison.Ordinal)}");

            if (staleness[member.Id] is { IsStale: true } stale)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is stale: {stale.Reasons[0]} Update it before checking the pack.");

            if (await DraftUpstreamAsync(member, together, cancellationToken).ConfigureAwait(false) is { } draftUpstream)
                return CalcActionResult.Refused($"Pack member '{member.Name}' {draftUpstream}");

            if (IndependenceRefusal(member, $"Pack member '{member.Name}'", "check the pack") is { } notIndependent)
                return CalcActionResult.Refused(notIndependent);

            // ADR-0173: a member that lacks the attestation its template demands holds an accepting check of the pack back (a rejection needs none), and
            // the attester of a member cannot be the one checking the pack.
            if (outcome != CheckOutcome.Rejected && _catalogue.Find(member.TemplateReference)?.Template is { } memberTemplate)
            {
                if (AttestationRefusal(member, memberTemplate, $"Pack member '{member.Name}'") is { } attestationRefusal)
                    return CalcActionResult.Refused(attestationRefusal);

                if (AttesterIsSigningRefusal(member, memberTemplate, checkerName, $"Pack member '{member.Name}'") is { } attesterChecks)
                    return CalcActionResult.Refused(attesterChecks);
            }
        }

        // A rejection leaves the pack and its Evidence as drafts (Black B-12).
        if (outcome == CheckOutcome.Rejected)
            return CalcActionResult.Refused($"The checker rejected the pack; it stays a draft. {statement}".TrimEnd());

        // The Evidence check and the pack's Checked mark commit together, after a re-read of the pack: a crash between them cannot
        // leave a pack that can never be checked (Red R-A-14).
        var now = _time.GetUtcNow();
        var checkedPack = pack with { Status = CalcInstanceStatus.Checked, UpdatedAt = now, CheckerPrincipal = CurrentPrincipal(), CheckerName = checkerName };
        EvidenceActionResult result;

        try
        {
            result = await _evidence.RecordCheckInOneTransactionAsync(evidenceId, checkerName, checkerOrganisation, statement, outcome,
                alsoWrite: (transaction, token) => CalcInstanceStore.WriteGuardedPackAsync(transaction, checkedPack, pack.UpdatedAt, token),
                cancellationToken, owner: CalcOwner).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException)
        {
            return CalcActionResult.Refused(ex.Message);
        }

        if (!result.Succeeded)
            return CalcActionResult.Refused(result.Reason ?? "The check was refused.");

        return new CalcActionResult(true, null, null);
    }

    /// <summary>
    /// Takes a checked pack back to a draft under a new pack revision and new Evidence, so a pack whose members went stale after the
    /// check (a record was revised, a template changed, an upstream moved) is never stranded: its members can be corrected, and the pack
    /// is checked again (Red R-A-04). The earlier check stays on the earlier Evidence.
    /// </summary>
    public async Task<CalcActionResult> ReopenPackAsync(Guid packId, CancellationToken cancellationToken = default)
    {
        var pack = await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false);

        if (pack is null || pack.EvidenceId is null)
            return CalcActionResult.Refused($"No pack '{packId}' exists.");

        if (pack.Status != CalcInstanceStatus.Checked)
            return CalcActionResult.Refused($"Pack '{pack.Title}' is {pack.Status}; only a checked pack is reopened (a draft is already open, an issued pack is final).");

        var next = NextRevision(pack.Revision);
        var now = _time.GetUtcNow();
        Evidence evidence;

        try
        {
            evidence = await _evidence.CreateManagedAsync(pack.ProjectId, $"{pack.Title} — calculation pack (rev {next})", EvidenceClassification.Calculation, Evidence.CalcWorksheetOwner, null, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateBusinessIdentifierException ex)
        {
            return CalcActionResult.Refused($"Pack '{pack.Title}' revision {next} already exists: {ex.Message}");
        }

        var reopened = pack with
        {
            Revision = next,
            Status = CalcInstanceStatus.Draft,
            EvidenceId = evidence.Id,
            UpdatedAt = now,
            Revisions = [.. pack.Revisions, new CalcPackRevision(next, now, CurrentPrincipal(), "Reopened after its check")],
            CheckerPrincipal = null,
            CheckerName = null,
            SecondCheck = null,
        };

        try
        {
            await _store.SavePackAsync(reopened, pack.UpdatedAt, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is CalcConcurrencyException or CalcInstanceImmutableException)
        {
            return CalcActionResult.Refused(ex.Message);
        }

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

        await AdoptEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);

        var (stored, gone) = await PackMembersAsync(pack, cancellationToken).ConfigureAwait(false);

        if (gone is not null)
            return CalcActionResult.Refused(gone);

        var staleness = await StalenessForAsync(pack.ProjectId, stored, cancellationToken).ConfigureAwait(false);
        var together = pack.InstanceIds.ToHashSet();
        var members = new List<(CalcInstance Instance, CalcCatalogueEntry Entry, CalcInputSet Inputs, CalcRunResult Result, KnownAnswerReport Report)>();

        // The authors of every member, not only those looked at so far (the second check is held to all of them).
        var allAuthors = stored.SelectMany(m => m.Authors).Distinct(StringComparer.Ordinal).ToList();

        // What the checks below were made against: the issue is refused if any of it changes before it commits (Red R-A-14).
        var seen = new Dictionary<Guid, (Guid? ProjectId, string Name, DateTimeOffset UpdatedAt)>();

        foreach (var member in stored)
        {
            if (FeatureRefusal(member.TemplateReference) is { } switchedOff)
                return CalcActionResult.Refused($"Pack member '{member.Name}': {switchedOff}");

            if (member.Status is CalcInstanceStatus.Superseded)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is superseded.");

            if (member.UpdatedAt > pack.UpdatedAt)
                return CalcActionResult.Refused($"Pack member '{member.Name}' changed after the pack was checked; check the pack again.");

            if (staleness[member.Id] is { IsStale: true } stale)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is stale: {stale.Reasons[0]}");

            if (await DraftUpstreamAsync(member, together, cancellationToken).ConfigureAwait(false) is { } draftUpstream)
                return CalcActionResult.Refused($"Pack member '{member.Name}' {draftUpstream}");

            var entry = _catalogue.Find(member.TemplateReference);

            if (entry is null)
                return CalcActionResult.Refused($"Template '{member.TemplateReference}' is no longer registered.");

            // ADR-0173: the attestation per member; the second check once for the pack, below.
            if (AttestationRefusal(member, entry.Template, $"Pack member '{member.Name}'", pack) is { } attestationRefusal)
                return CalcActionResult.Refused(attestationRefusal);

            if (entry.Template.RequiresSecondCheck)
            {
                var packRefusal = CountersignatureRefusal(pack.SecondCheck, pack.CheckerName, pack.CheckerPrincipal, allAuthors, $"Pack '{pack.Title}'");

                if (packRefusal is not null)
                    return CalcActionResult.Refused(packRefusal);
            }

            var inputs = new CalcInputSet(member.Inputs);
            var provenance = CalcInputValidator.ValidateProvenanceForIssue(entry.Template, inputs);

            if (provenance.Count > 0)
                return CalcActionResult.Refused($"{member.Name}: {provenance[0].Message}");

            var result = CalcRunner.Run(entry.Calculator, inputs, _engine, cancellationToken: cancellationToken);

            if (result.IsBlocked)
                return CalcActionResult.Refused($"Pack member '{member.Name}' is blocked: {result.Blocks[0].Message}");

            members.Add((member, entry, inputs, result, KnownAnswerRunner.Run(entry.Calculator, _time)));
            seen[member.Id] = (member.ProjectId, member.Name, member.UpdatedAt);

            foreach (var upstreamId in member.UpstreamIds)
            {
                if (!seen.ContainsKey(upstreamId) && await _store.FindInstanceAsync(upstreamId, cancellationToken).ConfigureAwait(false) is { } upstream)
                    seen[upstreamId] = (upstream.ProjectId, upstream.Name, upstream.UpdatedAt);
            }
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
                    var header = await HeaderAsync(entry, member, evidence, CalcSheetStatus.Issued, issuedAtUtc, issueReference, client, token, second: pack.SecondCheck).ConfigureAwait(false);
                    var sheet = await FinishSheetAsync(entry, inputs, result, header, Footer(entry, report), token).ConfigureAwait(false);
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
                        Issue = new CalcIssueSnapshot(text, CalcTemplateHasher.CanonicalText(entry.Template, CalcTemplateHasher.Formulas(entry.Calculator)), _engine.EngineVersion, report.Passed, report.Total, report.RunAt, null, _applicationVersion, FeatureNoteFor(entry.Template)),
                    });
                }

                var project = await ProjectLabelAsync(pack.ProjectId, token).ConfigureAwait(false);
                var cover = new CalcSheet(
                    new CalcSheetHeader(project, pack.Title, "Calculation pack", pack.Revision, CalcSheetStatus.Issued, evidence.AuthorIdentityId, CheckerLabel(evidence.Check?.CheckerName, pack.SecondCheck), evidence.CreatedAt, evidence.Check?.DateUtc, issuedAtUtc, client, issueReference),
                    "Calculation pack: the calculations listed in the index, issued together under one issue sheet.", string.Empty, [], [], [], [], [], [], [], [],
                    new CalcSheetFooter("pack", "—", _engine.EngineVersion, members.Sum(m => m.Report.Passed), members.Sum(m => m.Report.Total), issuedAtUtc, _applicationVersion,
                        string.Join("; ", members.Select(m => FeatureNoteFor(m.Entry.Template)).Where(n => n is not null).Distinct(StringComparer.Ordinal))),
                    pack.Revisions.Select(r => new CalcRevisionRow(r.Revision, r.Date, r.Author, r.Description)).ToList(), index);

                var bytes = _renderer?.RenderPack(cover, sheets) ?? Encoding.UTF8.GetBytes(CalcSheetTextRenderer.Render(cover) + "\n" + string.Join("\n\f\n", sheets.Select(CalcSheetTextRenderer.Render)));
                var attachmentId = Guid.NewGuid();
                issuedPack = pack with { Status = CalcInstanceStatus.Issued, IssuedAt = issuedAtUtc, UpdatedAt = issuedAtUtc, IssueReference = issueReference, SheetAttachmentId = attachmentId.ToString("N") };
                issuedMembers = issuedMembers.Select(m => m with { Issue = m.Issue! with { SheetAttachmentId = attachmentId.ToString("N") } }).ToList();
                return new IssueSheetContent($"{Safe(pack.Title)}-{Safe(issueReference)}-{Safe(pack.Revision)}{(_renderer is null ? ".txt" : SheetFileExtension)}", _renderer is null ? "text/plain" : "application/pdf", bytes, attachmentId);
            },
            alsoWrite: async (transaction, token) =>
            {
                // Under the transaction: nothing the checks relied on (a member, or a calculation a member takes a value from) has been changed since.
                foreach (var (id, (projectId, name, updatedAt)) in seen)
                    await CalcInstanceStore.AssertUnchangedAsync(transaction, projectId, id, name, updatedAt, token).ConfigureAwait(false);

                foreach (var m in issuedMembers)
                    await CalcInstanceStore.WriteIssuedInstanceAsync(transaction, m, token).ConfigureAwait(false);

                await CalcInstanceStore.WritePackAsync(transaction, issuedPack!, token).ConfigureAwait(false);
            },
            cancellationToken: cancellationToken,
            owner: CalcOwner).ConfigureAwait(false);

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

        // A pack member is checked and issued with its pack: its own sheet shows the pack's checker, check date and client (Black B-A-13).
        Evidence? packEvidence = null;

        if (instance.PackId is { } packId && await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false) is { EvidenceId: { } packEvidenceId })
            packEvidence = await FindEvidenceAsync(packEvidenceId, cancellationToken).ConfigureAwait(false);

        var status = instance.Status switch
        {
            CalcInstanceStatus.Checked => CalcSheetStatus.Checked,
            CalcInstanceStatus.Issued => CalcSheetStatus.Issued,
            CalcInstanceStatus.Superseded => CalcSheetStatus.Superseded,
            _ => CalcSheetStatus.Draft,
        };
        CalcCountersignature? packSecond = null;

        if (instance.PackId is { } secondPackId && instance.SecondCheck is null && await _store.FindPackAsync(secondPackId, cancellationToken).ConfigureAwait(false) is { } secondPack)
            packSecond = secondPack.SecondCheck;

        var header = await HeaderAsync(entry, instance, evidence, status, instance.IssuedAt, instance.IssueReference, packEvidence?.Issue?.Client ?? evidence?.Issue?.Client, cancellationToken, packEvidence, packSecond).ConfigureAwait(false);
        var footer = instance.Issue is { } issue
            ? new CalcSheetFooter(entry.Reference, CalcTemplateHasher.Short(instance.TemplateHash), issue.EngineVersion, issue.KnownAnswerPassed, issue.KnownAnswerTotal, issue.TestRunAt, issue.ApplicationVersion, issue.FeatureNote)
            : Footer(entry, KnownAnswerRunner.Run(entry.Calculator, _time));
        return await FinishSheetAsync(entry, inputs, result, header, footer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The one place every sheet is finished (preview, issue, pack, PDF): the rows of a table pinned to a licensed record are
    /// withheld, and linked and recorded inputs are named by their source (Red R-A-01).
    /// </summary>
    private async Task<CalcSheet> FinishSheetAsync(CalcCatalogueEntry entry, CalcInputSet inputs, CalcRunResult result, CalcSheetHeader header, CalcSheetFooter footer, CancellationToken cancellationToken)
    {
        var withheld = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var value in inputs.Values.Where(v => v.Rows is not null))
        {
            if (await LicensedRecordPinAsync(value.Provenance, cancellationToken).ConfigureAwait(false) is { } pin)
                withheld[value.Symbol] = $"{CalcRecordPin.Prefix}{pin.Id:N}@{pin.Revision}";
        }

        // A value linked from a calculation that holds an input pinned to a licensed record is withheld like a licensed value itself: the
        // inputs list names the link and says so in place of the number (the working may still show the value used — the user's own calculation, §A5).
        var traced = new Dictionary<Guid, bool>();
        var derived = new HashSet<string>(StringComparer.Ordinal);
        var shown = new CalcInputSet();

        foreach (var value in inputs.Values)
        {
            if (value.Rows is not null)
            {
                var rows = withheld.ContainsKey(value.Symbol)
                    ? value.Rows
                    : await WithholdCellsAsync(value.Rows, traced, static (cell, _) => cell with { Number = null, Unit = null, Text = LicensedDerivedText, Flag = null }, cancellationToken).ConfigureAwait(false);
                shown.Set(ReferenceEquals(rows, value.Rows) ? value : value with { Rows = rows });
                continue;
            }

            if (await LicensedUpstreamAsync(value.Provenance, traced, cancellationToken).ConfigureAwait(false) is not null)
                derived.Add(value.Symbol);

            shown.Set(value);
        }

        var sheet = CalcSheetBuilder.Build(entry.Template, shown, result, header, footer, null, withheld.Count == 0 ? null : withheld);
        return await DescribeLinksAsync(sheet, shown, derived, cancellationToken).ConfigureAwait(false);
    }

    /// <param name="checkEvidence">The Evidence whose check the header shows when it is not the calculation's own (a pack member shows its pack's).</param>
    /// <param name="second">The second check a pack gives its members (ADR-0173); an instance's own second check takes precedence.</param>
    private async Task<CalcSheetHeader> HeaderAsync(CalcCatalogueEntry entry, CalcInstance instance, Evidence? evidence, CalcSheetStatus status, DateTimeOffset? issuedAt, string? issueReference, string? client, CancellationToken cancellationToken, Evidence? checkEvidence = null, CalcCountersignature? second = null)
    {
        var project = await ProjectLabelAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        var check = (checkEvidence ?? evidence)?.Check;
        return new CalcSheetHeader(
            project, instance.Name, entry.Template.Title, instance.Revision, status,
            evidence?.AuthorIdentityId ?? instance.CreatedBy, CheckerLabel(check?.CheckerName ?? instance.CheckedBy, instance.SecondCheck ?? second),
            instance.CreatedAt, check?.DateUtc ?? instance.CheckedAt, issuedAt, client, issueReference);
    }

    private CalcSheetFooter Footer(CalcCatalogueEntry entry, KnownAnswerReport report) =>
        new(entry.Reference, entry.ShortHash, _engine.EngineVersion, report.Passed, report.Total, report.RunAt, _applicationVersion, FeatureNoteFor(entry.Template));

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
