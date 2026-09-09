using Tempest.Core.Bearings;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Settings;
using Tempest.Core.Standards;

namespace Tempest.Core.Evidence;

/// <summary>The concrete <see cref="IEvidenceService"/> implementation (`ADR-0148`).</summary>
/// <remarks>
/// <para>
/// <b>The governed seam over the five reference libraries.</b> A citation
/// names its library as a string (mirroring <c>ReferencePin.Library</c>'s
/// own documented reason: the five catalogues are each generic over their
/// own definition type, so no single typed handle spans them). This
/// service is the one place that knows all five and dispatches by
/// <c>IReferenceDataCatalog{TDefinition}.LibraryName</c>, exactly as
/// <c>GovernedBracketCheckService</c> resolves one.
/// </para>
/// <para>
/// <b>The independent-check rule</b> (`Evidence:IndependentCheck`, Product
/// Owner 2026-09-09) is read from <see cref="IConfigurationProvider"/> by
/// default, and from <see cref="ISettingsProvider"/> when one is supplied —
/// this service registers its own definition so a future Settings screen
/// (`WP 18.2A`) can read and write it without knowing this key exists
/// anywhere else.
/// </para>
/// </remarks>
public sealed class EvidenceService : IEvidenceService
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming whether a check must be independent of its evidence's own author.</summary>
    public const string IndependentCheckConfigurationKey = "Evidence:IndependentCheck";

    /// <summary>The <see cref="ISettingsProvider"/> key mirroring <see cref="IndependentCheckConfigurationKey"/> as a runtime-mutable setting.</summary>
    public const string IndependentCheckSettingKey = "Evidence.IndependentCheck";

    private readonly EngineeringDomainContext _context;
    private readonly IMaterialCatalog _materials;
    private readonly IFastenerCatalog _fasteners;
    private readonly IBearingCatalog _bearings;
    private readonly IStandardCatalog _standards;
    private readonly IConstantCatalog _constants;
    private readonly ICurrentPrincipalAccessor _principals;
    private readonly IConfigurationProvider _configuration;
    private readonly ISettingsProvider? _settings;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="EvidenceService"/> class.</summary>
    public EvidenceService(
        EngineeringDomainContext context,
        IMaterialCatalog materials,
        IFastenerCatalog fasteners,
        IBearingCatalog bearings,
        IStandardCatalog standards,
        IConstantCatalog constants,
        ICurrentPrincipalAccessor principals,
        IConfigurationProvider configuration,
        ISettingsProvider? settings = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(fasteners);
        ArgumentNullException.ThrowIfNull(bearings);
        ArgumentNullException.ThrowIfNull(standards);
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(principals);
        ArgumentNullException.ThrowIfNull(configuration);

        _context = context;
        _materials = materials;
        _fasteners = fasteners;
        _bearings = bearings;
        _standards = standards;
        _constants = constants;
        _principals = principals;
        _configuration = configuration;
        _settings = settings;
        _time = timeProvider ?? TimeProvider.System;

        if (_settings is not null)
        {
            var configuredDefault =
                _configuration.TryGetValue(IndependentCheckConfigurationKey, out var configuredRaw)
                && bool.TryParse(configuredRaw, out var configuredValue)
                && configuredValue
                    ? bool.TrueString
                    : bool.FalseString;

            _settings.RegisterDefinition(new SettingDefinition(
                IndependentCheckSettingKey, "Evidence — independent check required", configuredDefault));
        }
    }

    /// <inheritdoc />
    public async Task<Evidence> CreateAsync(
        Guid? parentId, string title, EvidenceClassification classification, Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (parentId is { } requestedParentId
            && await _context.Repository.FindAsync(requestedParentId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new ArgumentException(
                $"The selected parent '{requestedParentId}' no longer exists; select where the new evidence should go and try again.",
                nameof(parentId));
        }

        var authorId = _context.ResolveCurrentPrincipalId();

        var created = await new EngineeringObjectFactory<Evidence>(
            Evidence.CanonicalKind,
            _context,
            (doc, rev) => new Evidence(
                doc, rev, _context, identifier: null, title, EngineeringObjectMetadata.Empty,
                classification, subjectId, authorId))
            .CreateAsync($"{title} — evidence recorded in Tempest.", cancellationToken)
            .ConfigureAwait(false);

        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return (Evidence)created;
    }

    /// <inheritdoc />
    public async Task<EvidenceCitationResult> CiteAsync(Guid evidenceId, string library, string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var evidence = await FindEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);
        if (evidence is null)
            return new EvidenceCitationResult(EvidenceRefusal.EvidenceNotFound, $"No evidence '{evidenceId}' is registered.", null, null);

        var lookup = await FindRecordAsync(library, recordId, cancellationToken).ConfigureAwait(false);
        if (lookup is null)
        {
            return new EvidenceCitationResult(
                EvidenceRefusal.RecordNotFound, $"No record '{recordId}' is registered in '{library}'.", evidence, null);
        }

        if (lookup.Value.ValidationState != ReferenceValidationState.Released)
        {
            return new EvidenceCitationResult(
                EvidenceRefusal.RecordNotReleased,
                $"Record '{recordId}' in '{library}' is {lookup.Value.ValidationState}, not Released. Evidence may not cite reference data nobody has verified.",
                evidence,
                null);
        }

        var citation = new EvidenceCitation(lookup.Value.Pin, lookup.Value.RecordId, SourceCitationSnapshot: lookup.Value.Source);
        await evidence.AddCitationAsync(citation, cancellationToken).ConfigureAwait(false);

        return new EvidenceCitationResult(EvidenceRefusal.None, null, evidence, citation);
    }

    /// <inheritdoc />
    public async Task<Evidence> RemoveCitationAsync(Guid evidenceId, ReferencePin pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);

        var evidence = await FindEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"No evidence '{evidenceId}' is registered.", nameof(evidenceId));

        await evidence.RemoveCitationAsync(pin, cancellationToken).ConfigureAwait(false);

        return evidence;
    }

    /// <inheritdoc />
    public async Task<Evidence> DeclareFigureAsync(Guid evidenceId, string name, DeclaredFigureRole role, string quantity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(quantity);

        var evidence = await FindEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"No evidence '{evidenceId}' is registered.", nameof(evidenceId));

        // Refused as an exception, not a result: an unrecognised unit is a
        // caller programming/typing error, not an engineering-governance
        // finding a surface shows in its status bar — the same distinction
        // GovernedBracketCheckService draws for a negative load or a zero
        // area.
        var parsed = EvidenceUnitCatalog.Parse(quantity);

        await evidence.AddDeclaredFigureAsync(new DeclaredFigure(name, role, parsed.ToString()), cancellationToken).ConfigureAwait(false);

        return evidence;
    }

    /// <inheritdoc />
    public async Task<EvidenceActionResult> RecordCheckAsync(
        Guid evidenceId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkerOrganisation);
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);

        var evidence = await FindEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);
        if (evidence is null)
            return new EvidenceActionResult(EvidenceRefusal.EvidenceNotFound, $"No evidence '{evidenceId}' is registered.", null);

        if (!EvidenceStatusTransitions.IsPermitted(evidence.Status, EvidenceStatus.Checked))
        {
            return new EvidenceActionResult(
                EvidenceRefusal.TransitionNotPermitted,
                $"Evidence '{evidenceId}' is {evidence.Status}; it cannot be checked from that status.",
                evidence);
        }

        var principal = _principals.Current;
        string? checkerIdentityId = null;

        if (await IsIndependentCheckRequiredAsync(cancellationToken).ConfigureAwait(false))
        {
            if (principal is null || string.IsNullOrWhiteSpace(principal.Identity.Id))
            {
                return new EvidenceActionResult(
                    EvidenceRefusal.NoPrincipalSignedIn,
                    "The independent-check rule is on and nobody is signed in; a check nobody can be held to is not a check.",
                    evidence);
            }

            if (string.Equals(principal.Identity.Id, evidence.AuthorIdentityId, StringComparison.Ordinal))
            {
                return new EvidenceActionResult(
                    EvidenceRefusal.CheckerMustDifferFromAuthor,
                    $"The independent-check rule is on: principal '{principal.Identity.Id}' authored this evidence and may not also check it.",
                    evidence);
            }

            checkerIdentityId = principal.Identity.Id;
        }

        var recordedBy = principal?.Identity.Id ?? _context.ResolveCurrentPrincipalId();
        var check = new CheckRecord(checkerName, checkerOrganisation, checkerIdentityId, recordedBy, _time.GetUtcNow(), statement, outcome);

        await evidence.RecordCheckAsync(check, cancellationToken).ConfigureAwait(false);

        return new EvidenceActionResult(EvidenceRefusal.None, null, evidence);
    }

    /// <inheritdoc />
    public async Task<EvidenceActionResult> IssueAsync(Guid evidenceId, string issueReference, string revision, string client, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);

        var evidence = await FindEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);
        if (evidence is null)
            return new EvidenceActionResult(EvidenceRefusal.EvidenceNotFound, $"No evidence '{evidenceId}' is registered.", null);

        if (!EvidenceStatusTransitions.IsPermitted(evidence.Status, EvidenceStatus.Issued))
        {
            return new EvidenceActionResult(
                EvidenceRefusal.TransitionNotPermitted,
                $"Evidence '{evidenceId}' is {evidence.Status}; it cannot be issued from that status. Check it first.",
                evidence);
        }

        var issue = new IssueRecord(issueReference, revision, client, _time.GetUtcNow(), IssueSheetAttachmentId: null);
        await evidence.RecordIssueAsync(issue, cancellationToken).ConfigureAwait(false);

        return new EvidenceActionResult(EvidenceRefusal.None, null, evidence);
    }

    /// <inheritdoc />
    public async Task<EvidenceActionResult> ReviseAsync(Guid evidenceId, CancellationToken cancellationToken = default)
    {
        var evidence = await FindEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false);
        if (evidence is null)
            return new EvidenceActionResult(EvidenceRefusal.EvidenceNotFound, $"No evidence '{evidenceId}' is registered.", null);

        if (!EvidenceStatusTransitions.IsPermitted(evidence.Status, EvidenceStatus.Draft))
        {
            return new EvidenceActionResult(
                EvidenceRefusal.TransitionNotPermitted,
                $"Evidence '{evidenceId}' is {evidence.Status}; only Issued evidence can be revised.",
                evidence);
        }

        var revised = (Evidence)await evidence.ReviseAsync(
            $"{evidence.DisplayName} — revised.",
            "Revised: reopened for further work.",
            cancellationToken).ConfigureAwait(false);

        await revised.SetStatusOnlyAsync(
            EvidenceStatus.Draft,
            "Revised — reopened as Draft; the issued revision stays readable via its own revision history.",
            cancellationToken).ConfigureAwait(false);

        return new EvidenceActionResult(EvidenceRefusal.None, null, revised);
    }

    private async Task<Evidence?> FindEvidenceAsync(Guid evidenceId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(evidenceId, cancellationToken).ConfigureAwait(false) as Evidence;

    private async Task<bool> IsIndependentCheckRequiredAsync(CancellationToken cancellationToken)
    {
        if (_settings is not null)
        {
            var stored = await _settings.GetValueAsync(IndependentCheckSettingKey, cancellationToken).ConfigureAwait(false);
            if (bool.TryParse(stored, out var settingValue))
                return settingValue;
        }

        return _configuration.TryGetValue(IndependentCheckConfigurationKey, out var configured)
            && bool.TryParse(configured, out var configuredValue)
            && configuredValue;
    }

    private readonly record struct ReferenceRecordLookup(ReferenceValidationState ValidationState, ReferencePin Pin, string RecordId, string? Source);

    private async Task<ReferenceRecordLookup?> FindRecordAsync(string library, string recordId, CancellationToken cancellationToken)
    {
        if (string.Equals(library, _materials.LibraryName, StringComparison.OrdinalIgnoreCase))
        {
            var record = await _materials.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
            return record is null ? null : new ReferenceRecordLookup(record.ValidationState, ReferencePin.For(_materials.LibraryName, record), record.Id, record.Source?.ToString());
        }

        if (string.Equals(library, _fasteners.LibraryName, StringComparison.OrdinalIgnoreCase))
        {
            var record = await _fasteners.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
            return record is null ? null : new ReferenceRecordLookup(record.ValidationState, ReferencePin.For(_fasteners.LibraryName, record), record.Id, record.Source?.ToString());
        }

        if (string.Equals(library, _bearings.LibraryName, StringComparison.OrdinalIgnoreCase))
        {
            var record = await _bearings.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
            return record is null ? null : new ReferenceRecordLookup(record.ValidationState, ReferencePin.For(_bearings.LibraryName, record), record.Id, record.Source?.ToString());
        }

        if (string.Equals(library, _standards.LibraryName, StringComparison.OrdinalIgnoreCase))
        {
            var record = await _standards.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
            return record is null ? null : new ReferenceRecordLookup(record.ValidationState, ReferencePin.For(_standards.LibraryName, record), record.Id, record.Source?.ToString());
        }

        if (string.Equals(library, _constants.LibraryName, StringComparison.OrdinalIgnoreCase))
        {
            var record = await _constants.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
            return record is null ? null : new ReferenceRecordLookup(record.ValidationState, ReferencePin.For(_constants.LibraryName, record), record.Id, record.Source?.ToString());
        }

        return null;
    }
}
