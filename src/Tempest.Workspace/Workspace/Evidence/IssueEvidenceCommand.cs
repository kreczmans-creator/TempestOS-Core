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
/// Handles <see cref="IssueEvidenceCommand"/>: records the issue
/// (<see cref="IEvidenceService.IssueAsync"/>), then — where a renderer is
/// available — renders the issue sheet, attaches it, and points the
/// issue record at the attachment (`WP 18.2B`, §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Three transactions, not one.</b> The brief's own hope was that the
/// sheet could attach "in the same transaction as the issue": it cannot,
/// without widening <c>EngineeringObjectBase</c>'s own private
/// <c>MutateAndPersistAsync</c> (the only place that folds a type-state
/// write and an attachment's byte write into one <c>ExecuteWriteAsync</c>
/// call) into a protected member `Evidence` could call — a substrate
/// change outside this Work Package's own "files you own" list. What
/// actually happens: (1) <see cref="IEvidenceService.IssueAsync"/> commits
/// the issue record (attachment id still <see langword="null"/>); (2)
/// <see cref="Tempest.Core.EngineeringDomain.IHasAttachments.AttachContentAsync"/>
/// commits the sheet's own bytes and attachment metadata together (that
/// pairing <em>is</em> one transaction, `WP 17.1B`); (3)
/// <see cref="IEvidenceService.RecordIssueSheetAsync"/> commits the issue
/// record's own attachment id. A crash between (1) and (2) leaves an
/// Issued record with no sheet — recoverable by Reissuing is not offered,
/// but the record is never Issued-and-lying-about-a-sheet, matching how
/// <c>AttachContentAsync</c>'s own remarks already accept a crash between
/// bytes and the record that names them as producing orphaned, harmless
/// bytes rather than a false state. A crash between (2) and (3) leaves an
/// orphaned attachment (harmless, per the identical remarks) and an issue
/// record that still says "no sheet" — recoverable the same way.
/// </para>
/// <para>
/// <b>No renderer, no sheet, still issued.</b> <paramref name="issueSheetRenderer"/>
/// is <see langword="null"/> for every composition root that has none —
/// today, the console harness, which references
/// <c>Tempest.Desktop.IssueSheets.IssueSheetRenderer</c>'s own assembly
/// not at all. Issuing still succeeds; only the sheet is skipped. The
/// Desktop composition root always supplies one (<c>WorkspaceHost</c>'s
/// own remarks).
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
        var result = await _service.IssueAsync(command.TargetObjectId, command.IssueReference, command.Revision, command.Client, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
            return CommandResult.Failure(result.Reason ?? "The issue was refused.");

        var evidence = result.Evidence!;

        if (_issueSheetRenderer is null)
            return CommandResult.Success($"Issued as '{command.IssueReference}'.", command.TargetObjectId, command.TargetKind);

        var model = await BuildModelAsync(evidence, cancellationToken).ConfigureAwait(false);
        var bytes = _issueSheetRenderer.Render(model);

        var attachment = await evidence.AttachContentAsync(
            $"{SanitiseFileNameSegment(command.IssueReference)}-{SanitiseFileNameSegment(command.Revision)}-issue-sheet.pdf",
            "application/pdf", bytes, cancellationToken).ConfigureAwait(false);

        await _service.RecordIssueSheetAsync(evidence.Id, attachment.Id, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Issued as '{command.IssueReference}'; issue sheet attached.", command.TargetObjectId, command.TargetKind);
    }

    private async Task<IssueSheetModel> BuildModelAsync(Tempest.Core.Evidence.Evidence evidence, CancellationToken cancellationToken)
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

        var context = new IssueSheetContext(
            ProjectCode: projectCode,
            ProjectName: projectName,
            AuthorDisplayName: authorDisplayName,
            CheckerPrincipalDisplayName: checkerPrincipalDisplayName,
            ApplicationVersionText: FormatApplicationVersion(_platformVersionProvider.Version),
            StoreSequence: _domainContext.PersistenceStore.CurrentSequence,
            GeneratedAtUtc: _time.GetUtcNow());

        return IssueSheetModel.From(evidence, context);
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
