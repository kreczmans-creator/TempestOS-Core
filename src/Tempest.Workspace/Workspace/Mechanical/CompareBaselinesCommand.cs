using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Compares two <c>Configuration</c>/<c>Baseline</c>/<c>Release</c> objects'
/// own <see cref="IConfiguration.MemberRevisions"/> — added/removed/
/// revision-changed members — a plain Workspace-layer diff over data both
/// objects already carry; no new Domain read is needed (`WP 9.0B`).
/// Not <see cref="IWorkspaceCommand"/> — this command acts on two objects,
/// neither more "the" target than the other.
/// </summary>
public sealed class CompareBaselinesCommand : ICommand
{
    public CompareBaselinesCommand(Guid firstId, Guid secondId)
    {
        FirstId = firstId;
        SecondId = secondId;
    }

    /// <summary>Gets the first (typically older) configuration's own Id.</summary>
    public Guid FirstId { get; }

    /// <summary>Gets the second (typically newer) configuration's own Id.</summary>
    public Guid SecondId { get; }
}

/// <summary>Handles <see cref="CompareBaselinesCommand"/>.</summary>
public sealed class CompareBaselinesCommandHandler : ICommandHandler<CompareBaselinesCommand>
{
    private readonly EngineeringDomainContext _context;

    public CompareBaselinesCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(CompareBaselinesCommand command, CancellationToken cancellationToken)
    {
        var first = await _context.Repository.FindAsync(command.FirstId, cancellationToken).ConfigureAwait(false);
        var second = await _context.Repository.FindAsync(command.SecondId, cancellationToken).ConfigureAwait(false);

        if (first is not IConfiguration firstConfiguration)
            return CommandResult.Failure($"'{command.FirstId}' was not found, or is not a Configuration/Baseline/Release.");

        if (second is not IConfiguration secondConfiguration)
            return CommandResult.Failure($"'{command.SecondId}' was not found, or is not a Configuration/Baseline/Release.");

        var before = firstConfiguration.MemberRevisions.ToDictionary(m => m.ObjectId, m => m.RevisionNumber);
        var after = secondConfiguration.MemberRevisions.ToDictionary(m => m.ObjectId, m => m.RevisionNumber);

        var added = after.Keys.Except(before.Keys).ToList();
        var removed = before.Keys.Except(after.Keys).ToList();
        var revisionChanged = before.Keys.Intersect(after.Keys).Where(id => before[id] != after[id]).ToList();

        return CommandResult.Success(
            $"{added.Count} added, {removed.Count} removed, {revisionChanged.Count} revision-changed " +
            $"(comparing '{command.FirstId}' → '{command.SecondId}').");
    }
}
