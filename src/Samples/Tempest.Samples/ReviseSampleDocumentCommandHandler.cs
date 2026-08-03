using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="ReviseSampleDocumentCommand"/> by revising
/// <see cref="EngineeringDataSampleModule.SampleDocumentId"/> through
/// <see cref="IEngineeringDocumentStore"/>.
/// </summary>
public sealed class ReviseSampleDocumentCommandHandler : ICommandHandler<ReviseSampleDocumentCommand>
{
    private readonly IEngineeringDocumentStore _documentStore;
    private readonly EngineeringDataSampleModule _module;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReviseSampleDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="documentStore">The Engineering Data Model service this handler revises through.</param>
    /// <param name="module">The module whose own sample document this handler revises.</param>
    public ReviseSampleDocumentCommandHandler(IEngineeringDocumentStore documentStore, EngineeringDataSampleModule module)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(module);

        _documentStore = documentStore;
        _module = module;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ReviseSampleDocumentCommand command, CancellationToken cancellationToken)
    {
        if (_module.SampleDocumentId is not { } documentId)
            return CommandResult.Failure("The module's own sample document has not been created yet.");

        var revision = await _documentStore.ReviseAsync(documentId, "Revised via command.", "Manual revision.", cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"Document '{documentId}' revised to revision {revision.RevisionNumber}.");
    }
}
