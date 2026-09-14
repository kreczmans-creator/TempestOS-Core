using System.Linq;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Versioning;

namespace Tempest.Workspace.Evidence;

/// <summary>Issues a checked piece of evidence to the client and moves it to <see cref="EvidenceStatus.Issued"/> (<see cref="IEvidenceService.IssueAsync"/>).</summary>
public sealed class IssueEvidenceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="IssueEvidenceCommand"/> class.</summary>
    public IssueEvidenceCommand(Guid targetObjectId, string targetKind, string issueReference, string revision, string client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        IssueReference = issueReference;
        Revision = revision;
        Client = client;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the issue's own reference.</summary>
    public string IssueReference { get; }

    /// <summary>Gets the revision issued.</summary>
    public string Revision { get; }

    /// <summary>Gets the client the evidence is issued to.</summary>
    public string Client { get; }
}

/// <summary>
/// Handles <see cref="IssueEvidenceCommand"/>: where a renderer is
/// available, renders the issue sheet and attaches it (bytes and metadata,
/// one transaction, `WP 17.1B`) <em>before</em> asking
/// <see cref="IEvidenceService.IssueAsync"/> to record the issue itself —
/// so the attachment id is already known when the one issue transaction
/// commits, and "the record" and "the pointer" land together (`WP 18.2B`, §2; `B2`, `WP 20.3A`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two commits, not three (`B2`).</b> Issuing evidence with a sheet is
/// now: (1) <see cref="Tempest.Core.EngineeringDomain.IHasAttachments.AttachContentAsync"/>,
/// called from inside <see cref="IEvidenceService.IssueAsync"/>'s own
/// <c>attachIssueSheetAsync</c> callback once every refusal check has
/// passed, commits the sheet's bytes and attachment metadata together
/// (one transaction, `WP 17.1B`); (2) <see cref="IEvidenceService.IssueAsync"/>
/// itself commits the issue record with the real attachment id already in
/// it — "the record" and "the pointer" (the old third commit) are now the
/// same write. A fault in (1) leaves the evidence merely
/// <see cref="Tempest.Core.Evidence.EvidenceStatus.Checked"/>, with at
/// worst one harmless, unreferenced attachment (the same "orphaned bytes
/// are harmless" outcome <c>AttachContentAsync</c>'s own remarks already
/// accept). A fault in (2) leaves the evidence Checked too, for the
/// identical reason. Neither fault can produce the old hazard — an Issued
/// record with no sheet — because the record is never written until the
/// sheet already exists. Reaching this from three commits to two, rather
/// than one, still holds: folding the byte write itself into the issue
/// transaction would require widening <c>EngineeringObjectBase</c>'s own
/// private <c>MutateAndPersistAsync</c> into a protected member
/// <c>Evidence</c> could call — a substrate change outside this Work
/// Package's own "files you own" list.
/// </para>
/// <para>
/// <b>No renderer, no sheet, still issued.</b> <paramref name="issueSheetRenderer"/>
/// is <see langword="null"/> for every composition root that has none —
/// today, the console harness, which references
/// <c>Tempest.Desktop.IssueSheets.IssueSheetRenderer</c>'s own assembly
/// not at all. Issuing still succeeds; only the sheet is skipped (one
/// commit only). The Desktop composition root always supplies one
/// (<c>WorkspaceHost</c>'s own remarks).
/// </para>
/// </remarks>
public sealed class IssueEvidenceCommandHandler : ICommandHandler<IssueEvidenceCommand>
{
    private readonly IEvidenceService _service;
    private readonly EngineeringDomainContext _domainContext;
    private readonly IIssueSheetRenderer? _issueSheetRenderer;
    private readonly IPrincipalDirectory _principalDirectory;
    private readonly IPlatformVersionProvider _platformVersionProvider;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="IssueEvidenceCommandHandler"/> class.</summary>
    /// <param name="service">Records the issue itself.</param>
    /// <param name="domainContext">Resolves the evidence's own enclosing project and the store's own write sequence at issue.</param>
    /// <param name="issueSheetRenderer">Renders the issue sheet — <see langword="null"/> where no renderer is available (the console harness); the sheet is then skipped, not refused.</param>
    /// <param name="principalDirectory">Resolves the author's and checker's own display names from their identity ids.</param>
    /// <param name="platformVersionProvider">Names the running build on the sheet's own footer.</param>
    /// <param name="timeProvider">When the sheet is generated. Defaults to <see cref="TimeProvider.System"/>.</param>
    public IssueEvidenceCommandHandler(
        IEvidenceService service,
        EngineeringDomainContext domainContext,
        IIssueSheetRenderer? issueSheetRenderer,
        IPrincipalDirectory principalDirectory,
        IPlatformVersionProvider platformVersionProvider,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(principalDirectory);
        ArgumentNullException.ThrowIfNull(platformVersionProvider);

        _service = service;
        _domainContext = domainContext;
        _issueSheetRenderer = issueSheetRenderer;
        _principalDirectory = principalDirectory;
        _platformVersionProvider = platformVersionProvider;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    /// <remarks>Refused, not thrown, unless the evidence is <see cref="EvidenceStatus.Checked"/>.</remarks>
    public async Task<CommandResult> HandleAsync(IssueEvidenceCommand command, CancellationToken cancellationToken)
    {
        // Set only when attachIssueSheetAsync below actually ran and
        // attached a sheet — the closure is the only way HandleAsync learns
        // that, since a refused issue never calls it at all (`B2`).
        var attachedIssueSheet = false;

        var result = await _service.IssueAsync(
            command.TargetObjectId, command.IssueReference, command.Revision, command.Client,
            attachIssueSheetAsync: _issueSheetRenderer is null
                ? null
                : async (evidence, issuedAtUtc, token) =>
                {
                    var model = await BuildModelAsync(evidence, command, issuedAtUtc, token).ConfigureAwait(false);
                    var bytes = _issueSheetRenderer.Render(model);

                    var attachment = await evidence.AttachContentAsync(
                        $"{SanitiseFileNameSegment(command.IssueReference)}-{SanitiseFileNameSegment(command.Revision)}-issue-sheet.pdf",
                        "application/pdf", bytes, token).ConfigureAwait(false);

                    attachedIssueSheet = true;
                    return attachment.Id;
                },
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            return CommandResult.Failure(result.Reason ?? "The issue was refused.");

        return attachedIssueSheet
            ? CommandResult.Success($"Issued as '{command.IssueReference}'; issue sheet attached.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Success($"Issued as '{command.IssueReference}'.", command.TargetObjectId, command.TargetKind);
    }

    /// <summary>
    /// Builds the sheet's own model from the not-yet-issued
    /// <paramref name="evidence"/> and <paramref name="command"/> — never
    /// from <see cref="Tempest.Core.Evidence.Evidence.Issue"/>, which is
    /// still <see langword="null"/> here: this runs from
    /// <see cref="IEvidenceService.IssueAsync"/>'s own
    /// <c>attachIssueSheetAsync</c> callback, strictly before the issue
    /// record that would set it ever commits (`B2`).
    /// </summary>
    private async Task<IssueSheetModel> BuildModelAsync(
        Tempest.Core.Evidence.Evidence evidence, IssueEvidenceCommand command, DateTimeOffset issuedAtUtc, CancellationToken cancellationToken)
    {
        string projectCode = string.Empty;
        string projectName = string.Empty;
        if (evidence.ParentId is { } projectId
            && await _domainContext.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            projectCode = (project as IHasBusinessIdentifier)?.Identifier ?? string.Empty;
            projectName = (project as IHasBusinessIdentifier)?.DisplayName ?? string.Empty;
        }

        var authorDisplayName = _principalDirectory.Describe(evidence.AuthorIdentityId);
        var checkerPrincipalDisplayName = evidence.Check?.CheckerIdentityId is { } checkerId
            ? _principalDirectory.Describe(checkerId)
            : null;

        if (evidence.Check is not { } check)
            throw new InvalidOperationException($"Evidence '{evidence.Id}' has not been checked; an issue sheet can only be rendered for checked, issued evidence.");

        return new IssueSheetModel(
            ProjectCode: projectCode,
            ProjectName: projectName,
            Client: command.Client,
            EvidenceReference: evidence.Identifier,
            Title: evidence.DisplayName,
            Revision: command.Revision,
            IssueReference: command.IssueReference,
            IssueDateUtc: issuedAtUtc,
            Classification: evidence.Classification,
            AuthorDisplayName: authorDisplayName,
            AuthorDateUtc: evidence.CreatedAt,
            CheckerName: check.CheckerName,
            CheckerOrganisation: check.CheckerOrganisation,
            CheckerPrincipalDisplayName: checkerPrincipalDisplayName,
            CheckDateUtc: check.DateUtc,
            Outcome: check.Outcome,
            Citations: [.. evidence.Citations.Select(c => new IssueSheetCitationRow(c.Pin.Library, c.Pin.RecordId, c.Pin.RevisionNumber, c.SourceCitationSnapshot))],
            DeclaredFigures: [.. evidence.DeclaredFigures],
            GeneratedAtUtc: _time.GetUtcNow(),
            ApplicationVersionText: FormatApplicationVersion(_platformVersionProvider.Version),
            StoreSequence: _domainContext.PersistenceStore.CurrentSequence);
    }

    /// <summary>
    /// <c>"TempestOS &lt;version&gt; (&lt;commit&gt;)"</c> — the identical
    /// format <c>Tempest.Desktop.MainWindow.DescribeBuild</c> stamps on the
    /// title bar, reproduced here because this Workspace-layer handler
    /// cannot reach that Desktop-only method.
    /// </summary>
    private static string FormatApplicationVersion(PlatformVersion version)
    {
        var semantic = version.SemanticVersion;
        if (string.IsNullOrWhiteSpace(semantic))
            return "TempestOS";

        var plus = semantic.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
            return $"TempestOS {semantic}";

        var metadata = semantic[(plus + 1)..];
        var commit = metadata.Length > 7 ? metadata[..7] : metadata;
        return $"TempestOS {semantic[..plus]} ({commit})";
    }

    private static string SanitiseFileNameSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars);
    }
}
